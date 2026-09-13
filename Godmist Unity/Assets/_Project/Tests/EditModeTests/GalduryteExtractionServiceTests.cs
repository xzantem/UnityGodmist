using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class GalduryteExtractionServiceTests
    {
        private readonly List<ScriptableObject> assets = new();
        private GameSession session;
        private NpcInstance npc;
        private EquipmentItemInstance equipment;
        private GalduryteItemInstance galduryte;

        private sealed class FixedRandom : System.Random
        {
            public double Value;
            public int Calls;
            public override double NextDouble() { Calls++; return Value; }
        }

        [SetUp]
        public void SetUp()
        {
            session = new GameSession();
            session.TryAddGold(10000);
            var definition = Asset<NpcDefinition>();
            definition.Roles.Add(Asset<EnchanterRoleData>());
            npc = new NpcInstance { Definition = definition };
            var weapon = Asset<EquipmentItemDefinition>();
            SetItemField(weapon, "Id", "test.weapon");
            SetItemField(weapon, "ItemType", ItemType.Weapon);
            equipment = new EquipmentItemInstance(weapon) { UpgradeProgress = 1f };
            var stone = Asset<GalduryteItemDefinition>();
            SetItemField(stone, "Id", "test.galduryte");
            SetItemField(stone, "ItemType", ItemType.Galduryte);
            SetItemField(stone, "Weight", 1f);
            galduryte = new GalduryteItemInstance(stone)
            {
                CraftedBaseValue = 100, IsIdentified = true, Tier = GalduryteTier.Limestone,
                StoneDefinitionId = "test.stone", PowderDefinitionId = "test.powder",
                TargetType = GalduryteTargetType.Weapon, ItemLevel = 11,
                Effects = new List<GalduryteEffectInstance>
                {
                    new(GalduryteEffectType.GeneralDamagePercent, GaldurytePowderColor.Red, GalduryteEffectGrade.D, .02f)
                }
            };
            equipment.SocketedGaldurytes.Add(galduryte);
            session.Stash.Items.Add(equipment);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void Success_ReturnsSameInstanceToStashAndCharges()
        {
            var service = new GalduryteExtractionService(new FixedRandom());
            Assert.That(service.TryExtract(session, npc, equipment, galduryte, out var result, out var reason), Is.True, reason);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.PaidGold, Is.EqualTo(120));
            Assert.That(npc.GoldBalance, Is.EqualTo(120));
            Assert.That(npc.TotalGoldReceived, Is.EqualTo(120));
            Assert.That(session.Gold, Is.EqualTo(9880));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(120));
            Assert.That(equipment.SocketedGaldurytes, Is.Empty);
            Assert.That(session.Stash.Items.Last(), Is.SameAs(galduryte));
            Assert.That(galduryte.Effects, Has.Count.EqualTo(1));
        }

        [Test]
        public void Failure_DisablesEquippedEffectsAndPreservesOccupiedSocket()
        {
            var owner = MoveToCharacter(true);
            new CharacterStatPipelineSystem().Refresh(owner);
            Assert.That(owner.PassiveEffects.Any(effect => effect.DefinitionId == "equipment.socketed_galduryte"), Is.True);
            var service = new GalduryteExtractionService(new FixedRandom { Value = .99 });
            Assert.That(service.TryExtract(session, npc, equipment, galduryte, out var result, out _), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(npc.GoldBalance, Is.EqualTo(120));
            Assert.That(npc.TotalGoldReceived, Is.EqualTo(120));
            Assert.That(galduryte.IsDestroyed, Is.True);
            Assert.That(equipment.SocketedGaldurytes.Single(), Is.SameAs(galduryte));
            Assert.That(owner.PassiveEffects.Any(effect => effect.DefinitionId == "equipment.socketed_galduryte"), Is.False);
            Assert.That(owner.Inventory.Items, Is.Empty);
            Assert.That(session.Gold, Is.EqualTo(9880));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(120));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Success_ReturnsToEquipmentOwner(bool equipped)
        {
            var owner = MoveToCharacter(equipped);
            Assert.That(new GalduryteExtractionService(new FixedRandom()).TryExtract(session, npc, equipment, galduryte, out _, out _), Is.True);
            Assert.That(owner.Inventory.Items, Does.Contain(galduryte));
            Assert.That(session.Stash.Items, Has.No.Member(galduryte));
        }

        [Test]
        public void Destroyed_RetryCostsDoNotCompound_AndSuccessDeletesWithoutReturnCapacity()
        {
            var rng = new FixedRandom { Value = .99 };
            var service = new GalduryteExtractionService(rng);
            galduryte.IsDestroyed = true;
            session.Stash.MaxWeight = 0;
            for (var i = 0; i < 2; i++)
            {
                Assert.That(service.TryExtract(session, npc, equipment, galduryte, out var failed, out _), Is.True);
                Assert.That(failed.Succeeded, Is.False);
                Assert.That(failed.PaidGold, Is.EqualTo(180));
            }
            rng.Value = 0;
            Assert.That(service.TryExtract(session, npc, equipment, galduryte, out var result, out _), Is.True);
            Assert.That(result.RemovedDestroyedGalduryte, Is.True);
            Assert.That(result.PaidGold, Is.EqualTo(180));
            Assert.That(equipment.SocketedGaldurytes, Is.Empty);
            Assert.That(session.Stash.Items, Has.No.Member(galduryte));
        }

        [TestCase("capacity")]
        [TestCase("gold")]
        [TestCase("foreign")]
        [TestCase("selection")]
        public void Blocked_DoesNotChargeOrRoll(string block)
        {
            if (block == "capacity") session.Stash.MaxWeight = 0;
            if (block == "gold") session.TrySpendGold(session.Gold);
            if (block == "foreign") session.Stash.Items.Clear();
            if (block == "selection") equipment.SocketedGaldurytes.Clear();
            var before = session.Gold;
            var rng = new FixedRandom();
            Assert.That(new GalduryteExtractionService(rng).TryExtract(session, npc, equipment, galduryte, out _, out _), Is.False);
            Assert.That(rng.Calls, Is.Zero);
            Assert.That(session.Gold, Is.EqualTo(before));
            Assert.That(npc.SpentGoldTotal, Is.Zero);
        }

        [TestCase(1, .7f)]
        [TestCase(5, .7f)]
        [TestCase(6, .8f)]
        [TestCase(12, .8f)]
        public void Chance_UsesSingleLoyaltyThreshold(int level, float expected)
        {
            npc.LoyaltyLevel = level;
            Assert.That(new GalduryteExtractionService().GetSuccessChance(npc), Is.EqualTo(expected).Within(.0001));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SaveRoundTrip_PreservesSocketOwnershipAndDestroyedState(bool destroyed)
        {
            galduryte.IsDestroyed = destroyed;
            var catalog = Asset<GameContentCatalog>();
            catalog.ItemDefinitions.Add(equipment.Definition);
            catalog.ItemDefinitions.Add(galduryte.Definition);
            var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "extraction");
            var json = JsonUtility.ToJson(snapshot);
            var saved = JsonUtility.FromJson<GameSaveData>(json);
            Assert.That(new GameSessionRestoreFactory().TryCreate(saved, catalog, out var restored), Is.True);
            var restoredEquipment = (EquipmentItemInstance)restored.Stash.Items.Single();
            var restoredStone = restoredEquipment.SocketedGaldurytes.Single();
            Assert.That(restoredStone.IsDestroyed, Is.EqualTo(destroyed));
            Assert.That(restoredStone.InstanceId, Is.EqualTo(galduryte.InstanceId));
            Assert.That(restoredStone.Effects.Single().Value, Is.EqualTo(.02f));
            Assert.That(restoredStone.BaseValue, Is.EqualTo(100));
        }

        [Test]
        public void Socketing_CreditsNpcTreasuryExactlyOnce()
        {
            equipment.SocketedGaldurytes.Clear();
            equipment.ItemLevel = 12;
            equipment.CurrentRarity = RarityTier.Common;
            session.Stash.Items.Add(galduryte);
            Assert.That(new GalduryteSocketingService().TrySocket(session, npc, equipment, galduryte, out var paid, out var reason), Is.True, reason);
            Assert.That(paid, Is.EqualTo(50));
            Assert.That(npc.GoldBalance, Is.EqualTo(paid));
            Assert.That(npc.TotalGoldReceived, Is.EqualTo(paid));
            Assert.That(npc.SpentGoldTotal, Is.EqualTo(paid));
        }

        [Test]
        public void Socketing_RejectsForeignEquipmentAndCopiedGalduryte()
        {
            equipment.SocketedGaldurytes.Clear();
            equipment.ItemLevel = 12;
            equipment.CurrentRarity = RarityTier.Common;
            session.Stash.Items.Add(galduryte);
            var service = new GalduryteSocketingService();
            var foreign = new EquipmentItemInstance(equipment.EquipmentDefinition)
            { ItemLevel = 12, CurrentRarity = RarityTier.Common, UpgradeProgress = 1f };
            Assert.That(service.TrySocket(session, npc, foreign, galduryte, out _, out _), Is.False);
            var copy = new GalduryteItemInstance(galduryte.GalduryteDefinition, galduryte.InstanceId)
            { IsIdentified = true, ItemLevel = 11 };
            Assert.That(service.TrySocket(session, npc, equipment, copy, out _, out _), Is.False);
            Assert.That(session.Gold, Is.EqualTo(10000));
            Assert.That(session.Stash.Items, Does.Contain(galduryte));
        }

        [Test]
        public void HitChanceBonus_DoesNotChangeWeaponAccuracy_AndDestroyedStoneStopsBonus()
        {
            var owner = MoveToCharacter(true);
            galduryte.Effects.Clear();
            var pipeline = new CharacterStatPipelineSystem();
            pipeline.Refresh(owner);
            var accuracy = owner.WeaponAccuracy;
            galduryte.Effects.Add(new GalduryteEffectInstance(GalduryteEffectType.AccuracyPercent,
                GaldurytePowderColor.Red, GalduryteEffectGrade.D, .02f));
            pipeline.Refresh(owner);
            Assert.That(owner.WeaponAccuracy, Is.EqualTo(accuracy));
            Assert.That(Godmist.Systems.Combat.CombatDamageSystem.ApplyHitChanceBonus(owner, .5f), Is.EqualTo(.52f).Within(.00001));
            galduryte.IsDestroyed = true;
            pipeline.Refresh(owner);
            Assert.That(Godmist.Systems.Combat.CombatDamageSystem.ApplyHitChanceBonus(owner, .5f), Is.EqualTo(.5f));
        }

        private CharacterInstance MoveToCharacter(bool equipped)
        {
            var owner = new CharacterInstance { CharacterId = "owner", Stats = new CharacterStats() };
            session.TryRegisterCharacter(owner);
            session.TryAddCharacterToActiveParty(owner.CharacterId);
            session.Stash.Items.Clear();
            if (equipped) owner.Equipment.Weapon = equipment;
            else owner.Inventory.Items.Add(equipment);
            return owner;
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private static void SetItemField(ItemDefinition item, string name, object value) =>
            typeof(ItemDefinition).GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, value);
    }
}
