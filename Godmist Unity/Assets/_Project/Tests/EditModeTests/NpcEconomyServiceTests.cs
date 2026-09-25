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

        [Test]
        public void StockTopUpRotationAndBuybackHaveSeparateLifecycles()
        {
            var definition = CreateNpcDefinition("npc.stock");
            var role = ScriptableObject.CreateInstance<TraderRoleData>();
            createdObjects.Add(role);
            definition.Roles.Add(role);
            var ore = StockItem("ore");
            role.LocalStock.Add(new TraderStockEntry { Item = ore, Limit = 20 });
            var group = new TraderStockGroup { Id = "ores", Slots = 2 };
            for (var i = 0; i < 4; i++) group.Candidates.Add(new TraderStockEntry { Item = StockItem("other" + i), Limit = 3 });
            role.RotatingStock.Add(group);
            var npc = new NpcInstanceFactory().Create(definition);
            var service = new TraderStockService(new System.Random(7));
            Assert.That(npc.ShopInventory.Items.FindAll(i => i.Definition == ore).Count, Is.EqualTo(20));
            var previous = new List<string>(npc.RotatingStockItemIds);
            // Sold-out entries must still count as positions in the rotation.
            npc.ShopInventory.Items.Clear();
            var buyback = new Godmist.Runtime.NPC.BuybackEntry(
                new Godmist.Runtime.Items.ItemInstance(ore, "buyback"), 7);
            npc.ShopInventory.BuybackStock.Add(buyback);
            service.Refresh(npc, 1);
            Assert.That(npc.RotatingStockItemIds.FindAll(id => previous.Contains(id)).Count, Is.EqualTo(1));
            Assert.That(npc.ShopInventory.Items.Count, Is.EqualTo(26));
            Assert.That(npc.ShopInventory.BuybackStock[0], Is.SameAs(buyback));
            service.Refresh(npc, 1);
            Assert.That(npc.ShopInventory.Items.Count, Is.EqualTo(26));
        }

        [Test]
        public void ReturnRestocksOncePerDungeonOnlyAfterCompletedFloor()
        {
            var session = new GameSession();
            var definition = CreateNpcDefinition("npc.stock.return");
            var role = ScriptableObject.CreateInstance<TraderRoleData>();
            createdObjects.Add(role); definition.Roles.Add(role);
            var ore = StockItem("ore");
            role.LocalStock.Add(new TraderStockEntry { Item = ore, Limit = 20 });
            var npc = new NpcInstanceFactory().Create(definition);
            session.TryRegisterNpc(npc);
            npc.ShopInventory.Items.Clear();
            var dungeon = CreateDungeon("stock.dungeon", 3);
            var service = new TraderStockService();
            Assert.That(service.TryRefreshAfterReturn(session, dungeon), Is.False);
            dungeon.TrySetCurrentFloorIndex(1);
            Assert.That(service.TryRefreshAfterReturn(session, dungeon), Is.True);
            Assert.That(npc.ShopInventory.Items.Count, Is.EqualTo(20));
            npc.ShopInventory.Items.Clear();
            dungeon.TrySetCurrentFloorIndex(2);
            Assert.That(service.TryRefreshAfterReturn(session, dungeon), Is.False);
            Assert.That(npc.ShopInventory.Items, Is.Empty);
            var next = CreateDungeon("stock.dungeon", 2);
            next.TrySetCurrentFloorIndex(1);
            Assert.That(service.TryRefreshAfterReturn(session, next), Is.True);
            Assert.That(npc.ShopInventory.Items.Count, Is.EqualTo(20));
        }

        [Test]
        public void StockLevelGateAndExhaustedRotationPoolDoNotDuplicateItems()
        {
            var definition = CreateNpcDefinition("npc.stock.level");
            var role = ScriptableObject.CreateInstance<TraderRoleData>();
            createdObjects.Add(role); definition.Roles.Add(role);
            var group = new TraderStockGroup { Id = "rare", Slots = 2 };
            group.Candidates.Add(new TraderStockEntry { Item = StockItem("coal"), Limit = 10, MinimumLevel = 11 });
            role.RotatingStock.Add(group);
            var npc = new NpcInstanceFactory().Create(definition);
            Assert.That(npc.ShopInventory.Items, Is.Empty);
            var service = new TraderStockService();
            service.Refresh(npc, 11);
            service.Refresh(npc, 11);
            Assert.That(npc.ShopInventory.Items.Count, Is.EqualTo(10));
            Assert.That(npc.RotatingStockItemIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void SoldOutRotationPositionsSurviveJsonRestoreWithoutRefill()
        {
            var definition = CreateNpcDefinition("npc.stock.save");
            var role = ScriptableObject.CreateInstance<TraderRoleData>();
            createdObjects.Add(role); definition.Roles.Add(role);
            var item = StockItem("saved.material");
            var group = new TraderStockGroup { Id = "ore", Slots = 1 };
            group.Candidates.Add(new TraderStockEntry { Item = item, Limit = 5 });
            role.RotatingStock.Add(group);
            var npc = new NpcInstanceFactory().Create(definition);
            npc.ShopInventory.Items.Clear();
            var session = new GameSession(); session.TryRegisterNpc(npc);
            var catalog = ScriptableObject.CreateInstance<Godmist.Data.World.GameContentCatalog>();
            createdObjects.Add(catalog);
            catalog.NpcDefinitions.Add(definition); catalog.ItemDefinitions.Add(item);
            var snapshot = new Godmist.Systems.SaveLoad.GameSessionSaveSnapshotFactory().CreateSnapshot(session, "stock");
            var serializer = new Godmist.Systems.SaveLoad.GameSaveJsonSerializer();
            Assert.That(serializer.TryDeserialize(serializer.Serialize(snapshot), out var data), Is.True);
            Assert.That(new Godmist.Systems.SaveLoad.GameSessionRestoreFactory().TryCreate(data, catalog, out var restored), Is.True);
            var restoredNpc = restored.NpcsById[npc.NpcId];
            Assert.That(restoredNpc.ShopInventory.Items, Is.Empty);
            Assert.That(restoredNpc.RotatingStockItemIds, Is.EqualTo(new[] { item.Id }));
        }

        [TestCase(1, 5)]
        [TestCase(11, 6)]
        [TestCase(20, 6)]
        public void AuthoredBlacksmithStockImportsAndResolvesInCatalog(int level, int slots)
        {
            var role = UnityEditor.AssetDatabase.LoadAssetAtPath<TraderRoleData>(
                "Assets/_Project/ScriptableObjects/NPC/Roles/TRD_Arungard_Blacksmith.asset");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>(
                "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
            Assert.That(role.LocalStock.Count, Is.EqualTo(8));
            Assert.That(role.RotatingStock.Count, Is.EqualTo(5));
            var definition = CreateNpcDefinition("npc.authoring.test");
            definition.Roles.Add(role);
            var npc = new NpcInstanceFactory().Create(definition, level);
            Assert.That(npc.RotatingStockItemIds.Count, Is.EqualTo(slots));
            var entries = new List<TraderStockEntry>(role.LocalStock);
            foreach (var group in role.RotatingStock) entries.AddRange(group.Candidates);
            foreach (var entry in entries)
            {
                Assert.That(entry.Item, Is.Not.Null);
                Assert.That(catalog.ItemDefinitions.Contains(entry.Item), Is.True, entry.Item.Id);
                if (entry.IsAvailable(level) && (role.LocalStock.Contains(entry)
                    || npc.RotatingStockItemIds.Contains(entry.Item.Id)))
                    Assert.That(npc.ShopInventory.Items.FindAll(i => i.Definition == entry.Item).Count,
                        Is.EqualTo(entry.Limit), entry.Item.Id);
            }
            new TraderStockService(new System.Random(12)).Refresh(npc, level);
            Assert.That(npc.RotatingStockItemIds.Count, Is.EqualTo(slots));
        }

        private Godmist.Data.Items.ItemDefinition StockItem(string id)
        {
            var item = ScriptableObject.CreateInstance<Godmist.Data.Items.ItemDefinition>();
            createdObjects.Add(item);
            SetAutoProperty(item, "Id", id);
            SetAutoProperty(item, "ItemType", Godmist.Core.Enums.ItemType.Material);
            return item;
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
