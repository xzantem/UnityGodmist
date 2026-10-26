using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Items;
using Godmist.Systems.Dungeons;
using Godmist.Systems.Items;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class BranchEquipmentRewardTests
    {
        private static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");

        [OneTimeSetUp] public void AuthorRequestedRewards() => BranchEquipmentRewardsSetup.Configure();

        private static bool IsEquipment(ItemInstance item) => item.Definition.ItemType is ItemType.Weapon or ItemType.Armor;
        private static DungeonDefinition Definition(string theme) => Catalog.DungeonDefinitions.Single(d =>
            d != null && d.Id == "dungeon." + theme + ".expedition");

        [TestCase("catacombs", 1)] [TestCase("catacombs", 11)]
        [TestCase("forest", 1)] [TestCase("forest", 11)]
        public void RegularEnemiesHaveNoEquipmentButRetainResourcesAndGaldurytes(string theme, int level)
        {
            var definition = Definition(theme);
            Assert.That(definition.LootTableCatalog.TryGetTables(definition.Theme, level, out var enemy, out var chest), Is.True);
            Assert.That(enemy.Entries.Count, Is.EqualTo(level == 1 ? 1 : 2));
            Assert.That(enemy.Entries[0].DropChance, Is.EqualTo(.5f));
            Assert.That(enemy.Entries.All(e => e.ItemDefinition.ItemType == ItemType.Lootbag), Is.True);
            Assert.That(enemy.Entries.Any(e => e.ItemDefinition.Id.EndsWith(".weapons") || e.ItemDefinition.Id.EndsWith(".armor")), Is.False);
            if (level == 11) Assert.That(enemy.Entries[1].DropChance, Is.EqualTo(.01f));
            Assert.That(chest.ExpectedEquipmentPerDungeon, Is.EqualTo(5));
            var random = new System.Random(231023 + level);
            var loot = new LootTableSystem(() => (float)random.NextDouble(), lootLevel: level);
            for (var i = 0; i < 100; i++)
                Assert.That(loot.RollLoot(enemy).OfType<LootBagItemInstance>().SelectMany(b => b.Contents).Any(IsEquipment), Is.False);
        }

        [TestCase("catacombs")] [TestCase("forest")]
        public void ActualEnemyRewardsIncludingSpeciesLootContainNoEquipment(string theme)
        {
            var dungeon = new DungeonGeneratorService().Generate(Definition(theme), 231023, 10, DungeonLength.Short);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            new DungeonEncounterRewardPreparationSystem().Prepare(dungeon, Catalog.EquipmentPartDefinitions);
            foreach (var encounter in dungeon.Floors.SelectMany(f => f.Encounters))
            {
                Assert.That(dungeon.TryGetPreRolledRewards(encounter.InstanceId, out var rewards), Is.True);
                foreach (var item in rewards.EnemyRewards.SelectMany(r => r.Items))
                {
                    Assert.That(IsEquipment(item), Is.False);
                    if (item is LootBagItemInstance bag) Assert.That(bag.Contents.Any(IsEquipment), Is.False);
                }
            }
        }

        [TestCase("catacombs", 1, 1)] [TestCase("catacombs", 1, 3)] [TestCase("catacombs", 1, 5)]
        [TestCase("catacombs", 11, 1)] [TestCase("catacombs", 11, 3)] [TestCase("catacombs", 11, 5)]
        [TestCase("forest", 1, 1)] [TestCase("forest", 1, 3)] [TestCase("forest", 1, 5)]
        [TestCase("forest", 11, 1)] [TestCase("forest", 11, 3)] [TestCase("forest", 11, 5)]
        public void EntireExpeditionAveragesFiveEquipmentItemsRegardlessOfChestCount(string theme, int level, int chestCount)
        {
            var definition = Definition(theme);
            definition.LootTableCatalog.TryGetChestLootTable(definition.Theme, level, out var chest);
            var random = new System.Random(231023 + level * 17 + chestCount);
            var loot = new LootTableSystem(() => (float)random.NextDouble(), lootLevel: level);
            const int samples = 1000;
            var total = 0;
            for (var sample = 0; sample < samples; sample++)
                for (var i = 0; i < chestCount; i++) total += loot.RollChestLoot(chest, chestCount).Count(IsEquipment);
            var mean = total / (float)samples;
            TestContext.WriteLine($"{theme} level={level} chests={chestCount}: mean equipment={mean}");
            Assert.That(mean, Is.EqualTo(5).Within(.4f));
        }

        [TestCase("catacombs", DungeonLength.Short)] [TestCase("catacombs", DungeonLength.Normal)] [TestCase("catacombs", DungeonLength.Long)]
        [TestCase("forest", DungeonLength.Short)] [TestCase("forest", DungeonLength.Normal)] [TestCase("forest", DungeonLength.Long)]
        public void EveryBranchDestinationAndOnlyThatRoomContainsOneRewardChest(string theme, DungeonLength length)
        {
            var definition = Definition(theme);
            foreach (var seed in new[] { 6, 171026 })
            {
                var dungeon = new DungeonGeneratorService().Generate(definition, seed, 10, length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                var placer = new DungeonObjectPlacementSystem();
                placer.Place(dungeon, Catalog.EquipmentPartDefinitions);
                foreach (var floor in dungeon.Floors)
                {
                    var chests = floor.Objects.OfType<ChestDungeonObjectInstance>().ToArray();
                    Assert.That(chests.Length, Is.EqualTo(floor.Rooms.Count(r => r.RoomType == DungeonRoomType.Optional)));
                    foreach (var chest in chests)
                    {
                        Assert.That(floor.Map.TryGetRoomId(chest.Position, out var id), Is.True);
                        var room = floor.Rooms.Single(r => r.RoomId == id);
                        Assert.That(room.RoomType, Is.EqualTo(DungeonRoomType.Optional));
                        Assert.That(room.IsBranchTransitRoom, Is.False);
                    }
                }
                var before = dungeon.Floors.SelectMany(f => f.Objects).OfType<ChestDungeonObjectInstance>()
                    .SelectMany(c => c.Items).Select(i => i.InstanceId).ToArray();
                placer.Place(dungeon, Catalog.EquipmentPartDefinitions);
                Assert.That(dungeon.Floors.SelectMany(f => f.Objects).OfType<ChestDungeonObjectInstance>()
                    .SelectMany(c => c.Items).Select(i => i.InstanceId), Is.EqualTo(before), "Placement must not reroll saved contents.");
                TestContext.WriteLine($"{theme} {length} seed={seed}: branches/chests={dungeon.Floors.Sum(f => f.Rooms.Count(r => r.RoomType == DungeonRoomType.Optional))}");
            }
        }

        [TestCase("catacombs")] [TestCase("forest")]
        public void FullSaveRestorePreservesActualChestContents(string theme)
        {
            var game = new GameSession(GameModeState.Exploration);
            var hero = EnemyScalingBalanceTests.Hero(0, 1, false, true);
            game.TryRegisterCharacter(hero); game.TryAddCharacterToActiveParty(hero.CharacterId); game.TrySetActiveLeader(hero.CharacterId);
            var dungeon = new DungeonGeneratorService().Generate(Definition(theme), 231023, 10, DungeonLength.Short);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            new DungeonObjectPlacementSystem().Place(dungeon, Catalog.EquipmentPartDefinitions);
            game.TrySetActiveDungeon(dungeon);
            var save = new GameSessionSaveSnapshotFactory().CreateSnapshot(game, "branch-equipment");
            Assert.That(new GameSessionRestoreFactory().TryCreate(JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save)), Catalog, out var restored), Is.True);
            var expected = dungeon.Floors.SelectMany(f => f.Objects).OfType<ChestDungeonObjectInstance>()
                .SelectMany(c => c.Items).Select(i => i.InstanceId).ToArray();
            Assert.That(restored.ActiveDungeon.Floors.SelectMany(f => f.Objects).OfType<ChestDungeonObjectInstance>()
                .SelectMany(c => c.Items).Select(i => i.InstanceId), Is.EqualTo(expected));
        }
    }
}
