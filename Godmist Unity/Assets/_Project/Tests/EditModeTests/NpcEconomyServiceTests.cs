using System.Collections.Generic;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Data.NPC;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.NPC;
using Godmist.Systems.NPC;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class NpcEconomyServiceTests
    {
        private readonly List<Object> createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            Godmist.Data.Configuration.GameDefinesRuntime.Configure(null);
        }

        [TearDown]
        public void TearDown()
        {
            Godmist.Data.Configuration.GameDefinesRuntime.Configure(null);

            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null) Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void Factory_StartsNpcWithOneBasePayoutAndNoReceivedHistory()
        {
            var definition = CreateNpcDefinition("npc.economy.factory");
            var npc = new NpcInstanceFactory().Create(definition);

            Assert.That(npc.GoldBalance, Is.EqualTo(250));
            Assert.That(npc.TotalGoldReceived, Is.EqualTo(0));
            Assert.That(npc.TotalGoldSpent, Is.EqualTo(0));
            Assert.That(npc.GuildPayoutProgress, Is.EqualTo(0f));
        }

        [Test]
        public void PayoutUsesHighestActivePartyLevelAndResetsProgressWithoutOverflow()
        {
            var session = new GameSession();
            var character = new CharacterInstance
            {
                CharacterId = "hero",
                Level = 6
            };
            Assert.That(session.TryRegisterCharacter(character), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);

            var npcDefinition = CreateNpcDefinition("npc.economy.payout");
            var npc = new NpcInstance
            {
                NpcId = npcDefinition.Id,
                Definition = npcDefinition,
                GoldBalance = 0
            };
            Assert.That(session.TryRegisterNpc(npc), Is.True);

            var secondNpcDefinition = CreateNpcDefinition("npc.economy.payout.second_npc");
            SetAutoProperty(secondNpcDefinition, "GuildPayoutBaseAmount", 300);
            var secondNpc = new NpcInstance
            {
                NpcId = secondNpcDefinition.Id,
                Definition = secondNpcDefinition,
                GoldBalance = 0
            };
            Assert.That(session.TryRegisterNpc(secondNpc), Is.True);

            var dungeon = CreateDungeon("dungeon.economy.payout", 3);
            Assert.That(dungeon.TrySetCurrentFloorIndex(1), Is.True);
            Assert.That(dungeon.TrySetCurrentFloorIndex(2), Is.True);
            Assert.That(dungeon.TryMarkCompleted(), Is.True);

            var service = new NpcEconomyService();
            Assert.That(
                service.TryApplyDungeonProgress(
                    session,
                    dungeon,
                    out var progressAwarded,
                    out var payoutsGranted),
                Is.True);

            Assert.That(progressAwarded, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(payoutsGranted, Is.EqualTo(0));
            Assert.That(npc.GuildPayoutProgress, Is.EqualTo(.4f).Within(.0001f));

            dungeon = CreateDungeon("dungeon.economy.payout.second", 3);
            Assert.That(dungeon.TrySetCurrentFloorIndex(1), Is.True);
            Assert.That(dungeon.TrySetCurrentFloorIndex(2), Is.True);
            Assert.That(dungeon.TryMarkCompleted(), Is.True);
            Assert.That(
                service.TryApplyDungeonProgress(
                    session,
                    dungeon,
                    out _,
                    out payoutsGranted),
                Is.True);

            Assert.That(payoutsGranted, Is.EqualTo(0));
            Assert.That(npc.GuildPayoutProgress, Is.EqualTo(.8f).Within(.0001f));

            dungeon = CreateDungeon("dungeon.economy.payout.third", 3);
            Assert.That(dungeon.TrySetCurrentFloorIndex(1), Is.True);
            Assert.That(dungeon.TrySetCurrentFloorIndex(2), Is.True);
            Assert.That(dungeon.TryMarkCompleted(), Is.True);
            Assert.That(
                service.TryApplyDungeonProgress(
                    session,
                    dungeon,
                    out _,
                    out payoutsGranted),
                Is.True);

            Assert.That(payoutsGranted, Is.EqualTo(2));
            Assert.That(npc.GuildPayoutProgress, Is.EqualTo(0f));
            Assert.That(npc.GoldBalance, Is.EqualTo(500));
            Assert.That(npc.TotalGoldReceived, Is.EqualTo(500));
            Assert.That(secondNpc.GoldBalance, Is.EqualTo(600));
            Assert.That(secondNpc.TotalGoldReceived, Is.EqualTo(600));
        }

        [Test]
        public void PartialDungeonProgressCreditsOnlyNewlyReachedFloors()
        {
            var session = new GameSession();
            var npcDefinition = CreateNpcDefinition("npc.economy.partial");
            var npc = new NpcInstance
            {
                NpcId = npcDefinition.Id,
                Definition = npcDefinition
            };
            Assert.That(session.TryRegisterNpc(npc), Is.True);

            var dungeon = CreateDungeon("dungeon.economy.partial", 4);
            Assert.That(dungeon.TrySetCurrentFloorIndex(2), Is.True);

            var service = new NpcEconomyService();
            Assert.That(
                service.TryApplyDungeonProgress(
                    session,
                    dungeon,
                    out var progressAwarded,
                    out _),
                Is.True);

            Assert.That(progressAwarded, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(dungeon.GuildProgressCreditedFloorCount, Is.EqualTo(2));

            Assert.That(
                service.TryApplyDungeonProgress(
                    session,
                    dungeon,
                    out _,
                    out _),
                Is.False);
            Assert.That(npc.GuildPayoutProgress, Is.EqualTo(.2f).Within(.0001f));
        }

        [Test]
        public void TreasuryAccountingSeparatesReceivedAndSpentGold()
        {
            var npc = new NpcInstance { GoldBalance = 100 };
            var service = new NpcEconomyService();

            Assert.That(service.TryReceiveGold(npc, 40), Is.True);
            Assert.That(service.TrySpendGold(npc, 70), Is.True);
            Assert.That(service.TrySpendGold(npc, 100), Is.False);

            Assert.That(npc.GoldBalance, Is.EqualTo(70));
            Assert.That(npc.TotalGoldReceived, Is.EqualTo(40));
            Assert.That(npc.TotalGoldSpent, Is.EqualTo(70));
        }

        private NpcDefinition CreateNpcDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<NpcDefinition>();
            createdObjects.Add(definition);
            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            SetAutoProperty(definition, "Roles", new List<NpcRoleData>());
            return definition;
        }

        private DungeonDefinition CreateDungeonDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<DungeonDefinition>();
            createdObjects.Add(definition);
            SetAutoProperty(definition, "Id", id);
            SetAutoProperty(definition, "DisplayName", id);
            return definition;
        }

        private DungeonInstance CreateDungeon(string id, int floorCount)
        {
            var dungeon = new DungeonInstance(CreateDungeonDefinition(id), 1);
            for (var index = 0; index < floorCount; index++)
            {
                Assert.That(dungeon.TryAddFloor(new DungeonFloorInstance(index)), Is.True);
            }

            return dungeon;
        }

        private static void SetAutoProperty(
            object target,
            string propertyName,
            object value)
        {
            var type = target.GetType();
            FieldInfo field = null;

            while (type != null && field == null)
            {
                field = type.GetField(
                    $"<{propertyName}>k__BackingField",
                    BindingFlags.Instance
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
