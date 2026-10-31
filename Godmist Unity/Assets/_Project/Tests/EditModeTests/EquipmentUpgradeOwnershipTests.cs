using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Characters;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Runtime.NPC;
using Godmist.Systems.Characters;
using Godmist.Systems.Items;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class EquipmentUpgradeOwnershipTests
    {
        private readonly List<ScriptableObject> objects = new();
        private GameSession session;
        private CharacterInstance owner, other;
        private EquipmentItemDefinition definition;
        private GameContentCatalog catalog;
        private NpcInstance smith;
        private EquipmentItemInstance item;
        private CountingRandom random;
        private EquipmentUpgradeService service;

        [SetUp]
        public void SetUp()
        {
            session = new GameSession(GameModeState.Exploration, "ownership.test");
            session.TryAddGold(100000);
            var characterClass = Create<CharacterClassData>();
            Set(characterClass, "Id", "class.scout");
            owner = AddCharacter("owner", characterClass);
            other = AddCharacter("other", characterClass);
            definition = Create<EquipmentItemDefinition>();
            Set(definition, "Id", "equipment.test");
            Set(definition, "ItemType", ItemType.Weapon);
            Set(definition, "RequiredClass", characterClass);
            catalog = Create<GameContentCatalog>();
            var material = Create<ItemDefinition>();
            Set(material, "Id", "material.test");
            Set(material, "BaseValue", 10);
            var part = Create<EquipmentPartDefinition>();
            Set(part, "Id", "part.test");
            var requirement = new CraftingMaterialRequirement();
            Set(requirement, "Material", material);
            Set(requirement, "Amount", 1);
            Set(part, "CraftingMaterials", new List<CraftingMaterialRequirement> { requirement });
            catalog.EquipmentPartDefinitions.Add(part);
            var npcDefinition = Create<NpcDefinition>();
            Set(npcDefinition, "Id", "smith.test");
            Set(npcDefinition, "Roles", new List<NpcRoleData> { Create<BlacksmithRoleData>() });
            smith = new NpcInstance { Definition = npcDefinition, NpcId = "smith.test", GoldBalance = 50, SpentGoldTotal = 123, ShopInventory = new ShopInventory() };
            item = MakeItem("owned.item");
            random = new CountingRandom();
            service = new EquipmentUpgradeService(random);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in objects) UnityEngine.Object.DestroyImmediate(obj);
            objects.Clear();
        }

        [TestCase("inventory", false)] [TestCase("inventory", true)]
        [TestCase("equipped", false)] [TestCase("equipped", true)]
        [TestCase("other-inventory", false)] [TestCase("other-inventory", true)]
        [TestCase("other-equipped", false)] [TestCase("other-equipped", true)]
        [TestCase("stash", false)] [TestCase("stash", true)]
        public void OwnedLocations_AreAcceptedAndChargeExactlyOnce(string location, bool reforge)
        {
            Place(location);
            var preview = Preview(reforge);
            Assert.That(preview.CanExecute, Is.True, preview.FailureReason);
            var gold = session.Gold;
            var npcGold = smith.GoldBalance;
            var spent = smith.SpentGoldTotal;
            Assert.That(Execute(reforge, null, out var paid, out var reason), Is.True, reason);
            Assert.That(paid, Is.EqualTo(preview.GoldCost));
            Assert.That(session.Gold, Is.EqualTo(gold - paid));
            Assert.That(smith.GoldBalance, Is.EqualTo(npcGold + paid));
            Assert.That(smith.SpentGoldTotal, Is.EqualTo(spent + paid));
            Assert.That(random.Calls, Is.EqualTo(1));
            if (reforge) Assert.That(item.CurrentRarity, Is.EqualTo(RarityTier.Uncommon));
            else Assert.That(item.UpgradeProgress, Is.EqualTo(.2f));
        }

        [TestCase("unowned", false)] [TestCase("unowned", true)]
        [TestCase("foreign-session", false)] [TestCase("foreign-session", true)]
        [TestCase("bench", false)] [TestCase("bench", true)]
        [TestCase("pending", false)] [TestCase("pending", true)]
        [TestCase("shop", false)] [TestCase("shop", true)]
        [TestCase("same-id-copy", false)] [TestCase("same-id-copy", true)]
        public void UnavailableInstances_AreRejectedBeforeAnySideEffect(string location, bool reforge)
        {
            switch (location)
            {
                case "foreign-session": new GameSession(GameModeState.Exploration, "foreign").Stash.Items.Add(item); break;
                case "bench": owner.Inventory.Items.Add(item); Assert.That(session.TryMoveActiveMemberToBench(owner.CharacterId), Is.True); break;
                case "pending": session.AddPendingLoot(new[] { item }); break;
                case "shop": smith.ShopInventory.Items.Add(item); break;
                case "same-id-copy": session.Stash.Items.Add(item); item = MakeItem(item.InstanceId); break;
            }
            AssertRejected(reforge);
        }

        [TestCase(false)] [TestCase(true)]
        public void OwnershipIsRecheckedAfterPreview(bool reforge)
        {
            owner.Inventory.Items.Add(item);
            Assert.That(Preview(reforge).CanExecute, Is.True);
            owner.Inventory.Items.Remove(item);
            AssertRejected(reforge);
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void EquippedOwnerIsResolvedFromSession_EvenWithNullOrWrongOwner(bool reforge, bool wrongOwner)
        {
            owner.Equipment.Weapon = item;
            new CharacterStatPipelineSystem().Refresh(owner);
            var before = reforge ? owner.Stats.CritChance.GetValue() : owner.Stats.MinAttack.GetValue();
            other.Stats.MinAttack.SetEffectiveValue(777);
            other.Stats.CritChance.SetEffectiveValue(777);
            Assert.That(Execute(reforge, wrongOwner ? other : null, out _, out var reason), Is.True, reason);
            var after = reforge ? owner.Stats.CritChance.GetValue() : owner.Stats.MinAttack.GetValue();
            Assert.That(after, Is.GreaterThan(before));
            Assert.That(other.Stats.MinAttack.GetValue(), Is.EqualTo(777));
            Assert.That(other.Stats.CritChance.GetValue(), Is.EqualTo(777));
            Assert.That(EquipmentServiceOwnership.FindEquippedOwner(session, item), Is.SameAs(owner));
        }

        [Test]
        public void SocketingAndUpgradeExposeTheSameAccessibleEquipment()
        {
            owner.Inventory.Items.Add(item);
            other.Equipment.Weapon = MakeItem("second.item");
            session.Stash.Items.Add(MakeItem("stash.item"));
            session.AddPendingLoot(new[] { MakeItem("pending.item") });
            Assert.That(new GalduryteSocketingService().GetSocketableEquipment(session),
                Is.EquivalentTo(EquipmentServiceOwnership.GetAccessibleEquipment(session)));
            Assert.That(EquipmentServiceOwnership.GetAccessibleEquipment(session).Count(), Is.EqualTo(3));
        }

        private void AssertRejected(bool reforge)
        {
            var gold = session.Gold;
            var npcGold = smith.GoldBalance;
            var received = smith.TotalGoldReceived;
            var spent = smith.SpentGoldTotal;
            var loyalty = smith.LoyaltyLevel;
            var modifiers = item.StatModifiers;
            var baseline = item.BaseStatModifiers;
            Assert.That(Preview(reforge).CanExecute, Is.False);
            Assert.That(Execute(reforge, owner, out var paid, out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(paid, Is.Zero);
            Assert.That(session.Gold, Is.EqualTo(gold));
            Assert.That(smith.GoldBalance, Is.EqualTo(npcGold));
            Assert.That(smith.TotalGoldReceived, Is.EqualTo(received));
            Assert.That(smith.SpentGoldTotal, Is.EqualTo(spent));
            Assert.That(smith.LoyaltyLevel, Is.EqualTo(loyalty));
            Assert.That(random.Calls, Is.Zero);
            Assert.That(item.UpgradeProgress, Is.Zero);
            Assert.That(item.CurrentRarity, Is.EqualTo(RarityTier.Common));
            Assert.That(item.StatModifiers, Is.SameAs(modifiers));
            Assert.That(item.BaseStatModifiers, Is.SameAs(baseline));
        }

        private void Place(string location)
        {
            switch (location)
            {
                case "inventory": owner.Inventory.Items.Add(item); break;
                case "equipped": owner.Equipment.Weapon = item; break;
                case "other-inventory": other.Inventory.Items.Add(item); break;
                case "other-equipped": other.Equipment.Weapon = item; break;
                case "stash": session.Stash.Items.Add(item); break;
            }
        }

        private EquipmentUpgradePreview Preview(bool reforge) => reforge
            ? service.CreateReforgePreview(session, smith, item, catalog)
            : service.CreateUpgradePreview(session, smith, item, .2f, .7f, catalog);

        private bool Execute(bool reforge, CharacterInstance hint, out int paid, out string reason)
            => reforge ? service.TryReforge(session, smith, hint, item, catalog, out _, out paid, out reason)
                : service.TryUpgrade(session, smith, hint, item, .2f, .7f, catalog, out _, out paid, out reason);

        private EquipmentItemInstance MakeItem(string id) => new(definition, id)
        {
            CurrentRarity = RarityTier.Common, PartIds = new List<string> { "part.test" },
            BaseStatModifiers = new List<StatModifier>
            {
                new() { StatType = CharacterStatType.MinAttack, Operation = StatModifierOperation.FlatBase, Value = 10 },
                new() { StatType = CharacterStatType.CritChance, Operation = StatModifierOperation.FlatAbsolute, Value = .1f }
            },
            StatModifiers = new List<StatModifier>
            {
                new() { StatType = CharacterStatType.MinAttack, Operation = StatModifierOperation.FlatBase, Value = 10 },
                new() { StatType = CharacterStatType.CritChance, Operation = StatModifierOperation.FlatAbsolute, Value = .1f }
            }
        };

        private CharacterInstance AddCharacter(string id, CharacterClassData characterClass)
        {
            var character = new CharacterInstance { CharacterId = id, ClassData = characterClass,
                LifeState = CharacterLifeState.Alive, AvailabilityState = CharacterAvailabilityState.Available,
                Stats = new CharacterStats { MinAttack = new Stat(100), MaxAttack = new Stat(100), MaxHealth = new Stat(100) } };
            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(id), Is.True);
            return character;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            objects.Add(value);
            return value;
        }

        private static void Set(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            Assert.Fail("Missing fixture field: " + name);
        }

        private sealed class CountingRandom : System.Random
        {
            public int Calls;
            public override double NextDouble() { Calls++; return 0; }
        }
    }
}
