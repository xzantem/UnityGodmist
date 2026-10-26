using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class BiomeRoomProductionTests
    {
        private static GameContentCatalog Catalog => AssetDatabase.LoadAssetAtPath<GameContentCatalog>(
            "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/biome-room-authoring-20261023"));

        [TestCase("dungeon.forest.expedition", DungeonLength.Short)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.forest.expedition", DungeonLength.Long)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Short)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Normal)]
        [TestCase("dungeon.catacombs.expedition", DungeonLength.Long)]
        public void RoutedProductionLayoutsPreservePlanGatesAndSave(string id, DungeonLength length)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            Assert.That(definition.RoomTemplateCatalog.UseRoutedConnections, Is.True);
            var times = new List<long>();
            for (var seed = 231023; seed < 231031; seed++)
            {
                var timer = Stopwatch.StartNew();
                var dungeon = new DungeonGeneratorService().Generate(definition, seed, 10, length);
                new DungeonEncounterPlacementSystem().Place(dungeon);
                new DungeonObjectPlacementSystem().Place(dungeon, Catalog.EquipmentPartDefinitions);
                timer.Stop(); times.Add(timer.ElapsedMilliseconds);
                foreach (var floor in dungeon.Floors)
                {
                    Assert.That(floor.Rooms.All(r => definition.RoomTemplateCatalog.ProductionTemplates.Any(t => t.Id == r.TemplateId)), Is.True);
                    Assert.That(floor.Rooms.Select(r => r.TemplateId).Distinct().Count(), Is.GreaterThanOrEqualTo(6));
                    Assert.That(floor.Connections.All(c => c.HasGeometry), Is.True);
                    foreach (var connection in floor.Connections)
                    {
                        var normal = connection.SecondDoorCell - connection.FirstDoorCell;
                        var lateral = normal.x != 0 ? Vector2Int.up : Vector2Int.right;
                        for (var i = 0; i < connection.DoorwayWidth; i++)
                        {
                            var end = connection.GetEntranceCell(connection.SecondRoomId);
                            Assert.That(floor.Map.IsWalkable(end + lateral * i), Is.True);
                            Assert.That(floor.Map.CanMoveBetween(end - normal + lateral * i,
                                end + lateral * i, true), Is.True);
                        }
                        Assert.That(connection.CorridorCells.All(c => floor.Map.TryGetRoomId(c, out var owner)
                            && owner == connection.SecondRoomId), Is.True);
                    }
                    DungeonBranchTopologyTests.CheckTopology(dungeon, floor);
                    var startRoom = floor.Rooms.Single(r => r.RoomType == DungeonRoomType.Start);
                    var startCell = floor.Map.FloorCells.First(c => floor.Map.TryGetRoomId(c, out var owner) && owner == startRoom.RoomId);
                    var finalId = floor.Rooms.Single(r => r.RoomType == DungeonRoomType.Final).RoomId;
                    var reached = new HashSet<Vector2Int> { startCell };
                    var pending = new Queue<Vector2Int>(); pending.Enqueue(startCell);
                    var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
                    while (pending.Count > 0)
                    {
                        var cell = pending.Dequeue();
                        foreach (var direction in directions)
                        {
                            var next = cell + direction;
                            if (floor.Map.CanMoveBetween(cell, next) && reached.Add(next)) pending.Enqueue(next);
                        }
                    }
                    Assert.That(reached.Any(c => floor.Map.TryGetRoomId(c, out var owner) && owner == finalId), Is.False,
                        "A corridor or vegetation fringe must not create a physical shortcut past locked gates.");
                }
                if (seed != 231023) continue;
                var save = new DungeonSaveData { DungeonId = id, Seed = seed, DungeonLevel = 10, Length = length,
                    LayoutVersion = DungeonLayoutSnapshot.CurrentVersion, Layout = DungeonLayoutSnapshot.Capture(dungeon) };
                foreach (var floor in dungeon.Floors)
                {
                    save.FloorDeltas.Add(new DungeonFloorDeltaSaveData { FloorIndex = floor.FloorIndex,
                        ObjectDeltas = floor.Objects.Select(o => new DungeonObjectDeltaSaveData { ObjectInstanceId = o.InstanceId,
                            HasChestLootState = o is ChestDungeonObjectInstance }).ToList(),
                        ProgressionGateDeltas = floor.Connections.Where(c => c.HasProgressionGate)
                            .Select(c => new DungeonProgressionGateDeltaSaveData { GateId = c.ProgressionGateId }).ToList() });
                    save.EncounterDeltas.AddRange(floor.Encounters.Select(e => new DungeonEncounterDeltaSaveData { EncounterInstanceId = e.InstanceId }));
                }
                var json = JsonUtility.ToJson(save, true);
                Assert.That(DungeonLayoutSnapshot.TryRestore(JsonUtility.FromJson<DungeonSaveData>(json), definition, Catalog, out var restored), Is.True);
                Assert.That(JsonUtility.ToJson(DungeonLayoutSnapshot.Capture(restored).First()),
                    Is.EqualTo(JsonUtility.ToJson(save.Layout.First())));
                Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, id + "-" + length + ".json"), json);
            }
            File.WriteAllText(Path.Combine(Output, id + "-" + length + "-timing.txt"), string.Join(",", times));
        }

        [Test]
        public void ProductionPortsAreExplicitAndOldTemplatesRemainResolvable()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:DungeonRoomTemplateCatalog"))
            {
                var fixture = AssetDatabase.LoadAssetAtPath<DungeonRoomTemplateCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (!fixture.Id.StartsWith("rooms.catacombs.tactical.", StringComparison.Ordinal)) continue;
                Assert.That(fixture.UseRoutedConnections, Is.False, "Isolated combat fixtures must keep their authored room.");
                Assert.That(fixture.ProductionTemplates, Is.Empty);
            }
            foreach (var id in new[] { "dungeon.forest.expedition", "dungeon.catacombs.expedition" })
            {
                var catalog = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id).RoomTemplateCatalog;
                Assert.That(catalog.ProductionTemplates, Has.Count.GreaterThanOrEqualTo(16));
                Assert.That(catalog.Templates.Any(t => t != null && !catalog.ProductionTemplates.Contains(t)), Is.True);
                foreach (var room in catalog.ProductionTemplates)
                {
                    room.ValidateSurfaceGeometry();
                    Assert.That(room.BoundaryEdgesConfigured, Is.True);
                    // A referenced hand-authored asset keeps its existing ID;
                    // generated blueprint assets retain the production namespace.
                    Assert.That(room.Id.Contains(".production.")||AssetDatabase.GetAssetPath(room)==
                        "Assets/_Project/ScriptableObjects/Dungeons/ProductionRooms/DRT_catacombs_4way_tomb.asset",Is.True);
                    Assert.That(room.Id, Does.Not.Contain("test").And.Not.Contain("tactical").And.Not.Contain("prototype"));
                    Assert.That(room.AllowedBoundaryEdges.Count, Is.LessThan(DungeonRoomBoundaryUtility.EnumerateExternalBoundaryEdges(room).Count()));
                }
            }
        }

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void EveryAuthoredRotationHasCompatibleFurnitureAndEnemySlots(string id)
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == id);
            foreach (var template in definition.RoomTemplateCatalog.ProductionTemplates)
            foreach (var rotation in template.AllowedRotationDegrees)
            {
                var floor = new DungeonFloorInstance(0);
                floor.TryAddRoom(new DungeonRoomInstance("room", template.RoomType, template.Id, Vector2Int.zero, false, rotation));
                floor.TrySetMap(new DungeonFloorMapBuilder().Build(floor, definition.RoomTemplateCatalog));
                var candidates = new DungeonSemanticAnchorResolver().GetCandidates(floor, definition.RoomTemplateCatalog,
                    new[] { DungeonSemanticAnchorType.Obstacle, DungeonSemanticAnchorType.Chest,
                        DungeonSemanticAnchorType.Lever, DungeonSemanticAnchorType.Condition, DungeonSemanticAnchorType.Reward }, _ => true, false);
                var enemies = template.SemanticAnchors.Where(a => a.Type == DungeonSemanticAnchorType.Enemy)
                    .Select(a => DungeonRoomTransformUtility.TransformCell(a.LocalCell, template.SizeInCells, rotation)).ToHashSet();
                var anchors = candidates.Where(c => c.Anchor.IsRequired && c.Anchor.ObjectDefinition != null).ToList();
                var choices = anchors.ToDictionary(a => a, a => new List<DungeonObjectDefinition> { a.Anchor.ObjectDefinition });
                var assign = typeof(DungeonObjectPlacementSystem).GetMethod("TryAssignPlacementPlan", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That((bool)assign.Invoke(null, new object[] { anchors, choices, floor, enemies, null, null }), Is.True,
                    template.Id + " rotation " + rotation);
            }
        }

        [Test]
        public void AuthoredObjectAnchorsHaveUnoccupiedPlacementOptions()
        {
            var definition = Catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.catacombs.expedition");
            var dungeon = new DungeonGeneratorService().Generate(definition, 231023, 10, DungeonLength.Short);
            new DungeonEncounterPlacementSystem().Place(dungeon);
            var failures = new List<string>();
            foreach (var floor in dungeon.Floors)
            {
                var reserved = new DungeonEnemyAnchorResolver().GetReservedCells(floor, definition.RoomTemplateCatalog, false);
                var candidates = new DungeonSemanticAnchorResolver().GetCandidates(floor, definition.RoomTemplateCatalog,
                    Enum.GetValues(typeof(DungeonSemanticAnchorType)).Cast<DungeonSemanticAnchorType>().Where(t => t != DungeonSemanticAnchorType.Enemy).ToArray(), _ => true, false);
                var method = typeof(DungeonObjectPlacementSystem).GetMethod("BuildPlacementOptions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                foreach (var candidate in candidates.Where(c => c.Anchor.ObjectDefinition != null))
                {
                    var options = (System.Collections.ICollection)method.Invoke(null, new object[] { candidate,
                        new List<DungeonObjectDefinition> { candidate.Anchor.ObjectDefinition }, floor, reserved });
                    if (options.Count == 0) failures.Add(candidate.Room.TemplateId + "/" + candidate.Anchor.Id
                        + " cells=" + string.Join(";", candidate.Cells) + " reserved=" + string.Join(";", candidate.Cells.Where(reserved.Contains)));
                }
                var mandatory = candidates.Where(c => c.Anchor.IsRequired && c.Anchor.ObjectDefinition != null).ToList();
                var assign = typeof(DungeonObjectPlacementSystem).GetMethod("TryAssignPlacementPlan", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                foreach (var group in mandatory.GroupBy(c => c.Room.RoomId))
                {
                    var anchors = group.ToList();
                    var choices = anchors.ToDictionary(a => a, a => new List<DungeonObjectDefinition> { a.Anchor.ObjectDefinition });
                    var args = new object[] { anchors, choices, floor, reserved, null, null };
                    if (!(bool)assign.Invoke(null, args)) failures.Add("Combined " + group.First().Room.TemplateId + " rotation=" + group.First().Room.RotationDegrees
                        + "\n" + string.Join("\n", group.Select(c => c.Anchor.Id + " " + c.Anchor.ObjectDefinition.Id + " " + string.Join(";", c.Cells))));
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void RoutedPassageRejectsDisconnectedAndMissingEntranceBands()
        {
            var connection = new DungeonRoomConnection("a", "b");
            var cells = Enumerable.Range(1, 4).SelectMany(x => new[] { new Vector2Int(x, 0), new Vector2Int(x, 1) }).ToList();
            Assert.That(connection.TrySetRoutedPassage(Vector2Int.zero, Vector2Int.right, new Vector2Int(5, 0), 2, cells), Is.True);
            cells.Add(new Vector2Int(20, 0));
            Assert.That(connection.TrySetRoutedPassage(Vector2Int.zero, Vector2Int.right, new Vector2Int(5, 0), 2, cells), Is.False);
            Assert.That(connection.CorridorCells, Has.Count.EqualTo(8));
        }
    }
}
