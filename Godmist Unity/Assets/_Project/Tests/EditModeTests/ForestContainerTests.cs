using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Editor;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestContainerTests
    {
        static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(ForestExpeditionsSetup.CatalogPath);
        static DungeonDefinition Definition(bool boss) => AssetDatabase.LoadAssetAtPath<DungeonDefinition>(boss ? ForestExpeditionsSetup.BossPath : ForestExpeditionsSetup.RegularPath);
        static DungeonInstance Generate(bool boss, int level, int seed)
        {
            var dungeon = new DungeonGeneratorService().Generate(Definition(boss), seed, level);
            new DungeonObjectPlacementSystem().Place(dungeon, Catalog.EquipmentPartDefinitions);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            return dungeon;
        }

        [Test]
        public void ImportedContainerAnchorsAndTablesAreValidAndReauthoringIsIdempotent()
        {
            foreach (var boss in new[] { false, true })
            {
                var definition = Definition(boss);
                Assert.DoesNotThrow(() => DungeonObjectPlacementSystem.ValidateContentDefinitions(definition));
                Assert.That(definition.ObjectDefinitions.OfType<ChestDungeonObjectDefinition>().Count(), Is.EqualTo(2));
                foreach (var room in definition.RoomTemplateCatalog.Templates)
                {
                    var anchors = room.SemanticAnchors.Where(a => a.Id.StartsWith("forest.container.")).ToArray();
                    var eligible = room.RoomType == DungeonRoomType.Normal || room.RoomType == DungeonRoomType.Optional;
                    Assert.That(anchors.Length, Is.EqualTo(eligible ? 1 : 0), room.Id);
                    if (!eligible) continue;
                    var anchor = anchors.Single();
                    Assert.That(anchor.ObjectDefinition.Id, Is.EqualTo(room.RoomType == DungeonRoomType.Optional ? "object.forest.cache" : "object.forest.chest"));
                    Assert.That(room.Footprint.FloorCellOffsets, Has.Member(anchor.LocalCell));
                    Assert.That(room.SemanticAnchors.Any(a => a != anchor && a.LocalCell == anchor.LocalCell), Is.False);
                }
            }
            ForestContainersSetup.Configure();
            ForestContainersSetup.Configure();
            Assert.That(Definition(false).ObjectDefinitions.OfType<ChestDungeonObjectDefinition>().Count(), Is.EqualTo(2));
        }

        [TestCase(false, 1)] [TestCase(false, 10)] [TestCase(false, 11)] [TestCase(false, 20)]
        [TestCase(true, 10)] [TestCase(true, 20)]
        public void GeneratedContainersHaveThemedLootAndReachableAdjacentCells(bool boss, int level)
        {
            foreach (var seed in new[] { 42, 219, 587 })
            {
                var dungeon = Generate(boss, level, seed);
                var containers = dungeon.Floors.SelectMany(f => f.Objects).OfType<ChestDungeonObjectInstance>().ToArray();
                Assert.That(containers.Length, Is.GreaterThan(0));
                Assert.That(containers.Any(c => c.Definition.Id == "object.forest.cache"), Is.True);
                foreach (var container in containers)
                {
                    Assert.That(container.IsUnlocked, Is.True);
                    // Chest rolls unpack loot bags, so players receive their actual contents.
                    Assert.That(container.Items.Any(i => i.Definition.Id == "item.vs10.szyszka"), Is.True);
                    Assert.That(container.Items.Any(i => i.Definition.Id.Contains("resin")), Is.False);
                    Assert.That(container.Items.Any(i => i.Definition.Id.StartsWith("item.lootbag.")), Is.False);
                    var floor = dungeon.Floors.Single(f => f.Objects.Contains(container));
                    Assert.That(new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }.Any(d =>
                        floor.Map.FloorCells.Contains(container.Position + d)
                        && !floor.Objects.Any(o => o.Position == container.Position + d)), Is.True);
                }
            }
        }

        [TestCase(false, 10)] [TestCase(true, 20)]
        public void LootedContainersStayEmptyAndUntouchedContainersKeepExactLootAfterSave(bool boss, int level)
        {
            var dungeon = Generate(boss, level, 219);
            var containers = dungeon.Floors.SelectMany(f => f.Objects).OfType<ChestDungeonObjectInstance>().ToArray();
            Assert.That(containers.Length, Is.GreaterThanOrEqualTo(2));
            var emptied = containers[0];
            Assert.That(emptied.TryClaimItems(out _), Is.True);
            emptied.TryClaimGold(out _);
            var expected = containers.ToDictionary(c => c.InstanceId, c => (c.Gold, ids: c.Items.Select(i => i.InstanceId).ToArray()));
            var session = new GameSession(GameModeState.Exploration);
            Assert.That(session.TrySetActiveDungeon(dungeon), Is.True);
            var json = new GameSaveJsonSerializer();
            for (var pass = 0; pass < 2; pass++)
            {
                var snapshot = new GameSessionSaveSnapshotFactory().CreateSnapshot(session, "forest containers");
                Assert.That(json.TryDeserialize(json.Serialize(snapshot), out var data), Is.True);
                Assert.That(new GameSessionRestoreFactory().TryCreate(data, Catalog, out session), Is.True);
                var restored = session.ActiveDungeon.Floors.SelectMany(f => f.Objects).OfType<ChestDungeonObjectInstance>().ToArray();
                Assert.That(restored.Length, Is.EqualTo(expected.Count));
                foreach (var container in restored)
                {
                    Assert.That(container.Gold, Is.EqualTo(expected[container.InstanceId].Gold));
                    Assert.That(container.Items.Select(i => i.InstanceId), Is.EqualTo(expected[container.InstanceId].ids));
                }
            }
        }
    }
}
