using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonAssetAuditRoomTests
    {
        private const string DungeonPath =
            "Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Expedition.asset";
        private const string CatalogPath =
            "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset";

        [Test]
        public void CatacombsAuditRoomCoversAuthoredObjectsAndAllEnemyArchetypes()
        {
            var dungeon = Build(240917);
            var floor = dungeon.Floors[0];

            Assert.That(dungeon.IsAssetAuditRoom, Is.True);
            Assert.That(dungeon.Floors, Has.Count.EqualTo(2));
            Assert.That(floor.Rooms, Has.Count.EqualTo(1));
            Assert.That(floor.Rooms[0].RoomType, Is.EqualTo(Godmist.Core.Enums.DungeonRoomType.Final));
            Assert.That(floor.Map, Is.Not.Null);
            Assert.That(floor.Map.IsWalkable(dungeon.AssetAuditSpawnCell), Is.True);
            Assert.That(dungeon.HasAssetAuditDagger, Is.True);
            Assert.That(floor.Map.HasAssetAuditDagger, Is.True);
            Assert.That(floor.Map.AssetAuditDaggerCell, Is.EqualTo(dungeon.AssetAuditDaggerCell));
            Assert.That(floor.Map.IsWalkable(dungeon.AssetAuditDaggerCell), Is.True);
            Assert.That(floor.Map.HasElevatedSurfaces, Is.True);
            Assert.That(floor.Map.Stairs.Count(), Is.EqualTo(1));
            Assert.That(floor.Map.Stairs.Single().First.Level, Is.EqualTo(0));
            Assert.That(floor.Map.Stairs.Single().Second.Level, Is.EqualTo(1));
            Assert.That(floor.Map.SurfaceWallTypes.Values, Does.Contain(Godmist.Core.Enums.DungeonWallType.Full));
            Assert.That(floor.Map.SurfaceWallTypes.Values, Does.Contain(Godmist.Core.Enums.DungeonWallType.Low));
            Assert.That(floor.Map.SurfaceWallTypes.Values, Does.Contain(Godmist.Core.Enums.DungeonWallType.Railing));
            var levelZeroLowEdges = floor.Map.SurfaceWallTypes
                .Where(pair => pair.Key.Level == 0 && pair.Value == Godmist.Core.Enums.DungeonWallType.Low)
                .Select(pair => pair.Key.CellEdge)
                .ToArray();
            Assert.That(levelZeroLowEdges.Any(edge => edge.FirstCell.y == edge.SecondCell.y), Is.True,
                "audit room contains a horizontal low-wall sample");
            Assert.That(levelZeroLowEdges.Any(edge => edge.FirstCell.x == edge.SecondCell.x), Is.True,
                "audit room contains a vertical low-wall sample");
            Assert.That(floor.Map.Stairs.Single().First.Cell, Is.EqualTo(new Vector2Int(8, 5)));
            Assert.That(floor.Map.Stairs.Single().Second.Cell, Is.EqualTo(new Vector2Int(8, 9)));
            Assert.That(floor.Map.TryGetStairPath(floor.Map.Stairs.Single(), out var stairPath), Is.True);
            Assert.That(stairPath, Is.EqualTo(new[]
            {
                new Vector2Int(8, 5), new Vector2Int(8, 6), new Vector2Int(8, 7),
                new Vector2Int(8, 8), new Vector2Int(8, 9)
            }));
            var stairCells = stairPath.ToHashSet();
            Assert.That(floor.Objects.Select(instance => instance.Position).Intersect(stairCells), Is.Empty,
                "audit props must not occupy the stair route");

            Assert.That(floor.Objects.OfType<DoorDungeonObjectInstance>().Count(), Is.EqualTo(2));
            Assert.That(floor.Objects.OfType<ChestDungeonObjectInstance>().Count(), Is.EqualTo(2));
            Assert.That(floor.Objects.OfType<ObstacleDungeonObjectInstance>().Count(), Is.EqualTo(4));
            Assert.That(floor.Objects.OfType<SpikeTrapDungeonObjectInstance>().Count(), Is.EqualTo(4));
            Assert.That(floor.Objects.OfType<LeverDungeonObjectInstance>().Count(), Is.EqualTo(1));
            Assert.That(floor.Objects.OfType<ConditionDungeonObjectInstance>().Count(), Is.EqualTo(1));

            var expectedObjectIds = new[]
            {
                "dungeon_object.catacombs_door",
                "object.chest.debug",
                "object.condition.debug",
                "object.lever.debug",
                "object.obstacle.debug",
                "object.obstacle.pillar",
                "object.obstacle.rubble",
                "object.obstacle.sarcophagus",
                "object.spike_trap.debug",
                "object.trap.gas",
                "object.trap.graveseal",
                "object.trap.spikes"
            };
            foreach (var id in expectedObjectIds)
                Assert.That(floor.Objects.Any(instance => instance.Definition.Id == id), Is.True, id);

            var doors = floor.Objects.OfType<DoorDungeonObjectInstance>().ToArray();
            Assert.That(doors.Select(door => door.IsOpen).Distinct(), Is.EquivalentTo(new[] { false, true }));
            Assert.That(doors.Any(door => door.Edge.FirstCell.y == door.Edge.SecondCell.y), Is.True);
            Assert.That(doors.Any(door => door.Edge.FirstCell.x == door.Edge.SecondCell.x), Is.True);

            var unlockedChests = floor.Objects.OfType<ChestDungeonObjectInstance>()
                .Where(chest => chest.InstanceId.Contains(".chest.unlocked.")).ToArray();
            Assert.That(unlockedChests.Length, Is.EqualTo(dungeon.Definition.ObjectDefinitions.OfType<ChestDungeonObjectDefinition>().Count()));
            Assert.That(floor.Objects.Select(o => o.InstanceId), Is.Unique);
            foreach (var unlockedChest in unlockedChests)
            {
                Assert.That(unlockedChest.IsUnlocked, Is.True);
                Assert.That(unlockedChest.Items.Select(item => item.Definition.Id),
                    Is.EquivalentTo(new[] { "item.lootbag.catacombs.dead_king", "CampingKit" }));
            }

            var expectedEnemyIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "enemy.catacombs.archer",
                "enemy.catacombs.dead_king",
                "enemy.catacombs.defender",
                "enemy.catacombs.ghoul",
                "enemy.catacombs.occultist",
                "enemy.catacombs.swordsman",
                "enemy.catacombs.undead",
                "enemy.catacombs.wraith"
            };
            var enemyIds = floor.Encounters
                .SelectMany(encounter => encounter.Enemies)
                .Select(enemy => enemy.CharacterDefinition.Id)
                .ToHashSet(StringComparer.Ordinal);
            CollectionAssert.AreEquivalent(expectedEnemyIds, enemyIds);
            Assert.That(floor.Encounters.Any(encounter =>
                encounter.Definition.Id == "encounter.catacombs.dead_king"), Is.True);

            var enemyPositions = floor.Encounters
                .SelectMany(encounter => encounter.EnemyExplorationPositions.Values)
                .ToArray();
            Assert.That(enemyPositions, Has.Length.EqualTo(expectedEnemyIds.Count));
            Assert.That(enemyPositions, Is.Unique);
            var bossPosition = floor.Encounters
                .SelectMany(encounter => encounter.Enemies.Select(enemy =>
                    (enemy, encounter)))
                .Where(pair => pair.enemy.CharacterDefinition.Id == "enemy.catacombs.dead_king")
                .Select(pair => pair.encounter.EnemyExplorationPositions[pair.enemy.InstanceId])
                .Single();
            Assert.That(enemyPositions.Where(position => position != bossPosition)
                .All(position => ChebyshevDistance(position, bossPosition) >= 3), Is.True);
            for (var first = 0; first < enemyPositions.Length; first++)
            {
                var requiredDistance = enemyPositions[first] == bossPosition ? 3 : 2;
                for (var second = first + 1; second < enemyPositions.Length; second++)
                {
                    var pairDistance = enemyPositions[second] == bossPosition ? 3 : 2;
                    Assert.That(
                        ChebyshevDistance(enemyPositions[first], enemyPositions[second]),
                        Is.GreaterThanOrEqualTo(Mathf.Max(requiredDistance, pairDistance)));
                }
            }

            var bossObjectPositions = floor.Objects
                .Select(instance => instance.Position)
                .ToArray();
            Assert.That(bossObjectPositions.All(position =>
                ChebyshevDistance(position, bossPosition) >= 3), Is.True);

            foreach (var door in doors)
                floor.Map.TrySetDoorState(door.Edge, true);
            var reachable = new HashSet<Vector2Int> { dungeon.AssetAuditSpawnCell };
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(dungeon.AssetAuditSpawnCell);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var neighbour in new[]
                {
                    current + Vector2Int.up,
                    current + Vector2Int.right,
                    current + Vector2Int.down,
                    current + Vector2Int.left
                })
                {
                    if (!floor.Map.CanMoveBetween(current, neighbour)
                        || !reachable.Add(neighbour))
                        continue;
                    pending.Enqueue(neighbour);
                }
            }
            Assert.That(reachable, Is.SupersetOf(floor.Map.FloorCells));

            var traversalStart = new DungeonTraversalPosition(
                new DungeonSurfacePosition(dungeon.AssetAuditSpawnCell));
            var traversalReachable = new HashSet<DungeonTraversalPosition> { traversalStart };
            var traversalPending = new Queue<DungeonTraversalPosition>();
            traversalPending.Enqueue(traversalStart);
            while (traversalPending.Count > 0)
            {
                foreach (var neighbour in floor.Map.GetTraversalNeighbours(traversalPending.Dequeue()))
                {
                    if (traversalReachable.Add(neighbour))
                        traversalPending.Enqueue(neighbour);
                }
            }
            var elevatedSurfaces = floor.Map.Surfaces
                .Where(surface => surface.Level == 1)
                .Select(surface => new DungeonTraversalPosition(surface));
            Assert.That(elevatedSurfaces.All(traversalReachable.Contains), Is.True,
                "level-aware traversal must reach every balcony cell through the authored stairs");

            var elevatedCells = floor.Map.Surfaces
                .Where(surface => surface.Level == 1)
                .Select(surface => surface.Cell)
                .ToHashSet();
            var directions = new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            var perimeterEdges = elevatedCells.Sum(cell => directions.Count(direction =>
                !elevatedCells.Contains(cell + direction)));
            var internalEdges = elevatedCells.Sum(cell => directions.Count(direction =>
                elevatedCells.Contains(cell + direction))) / 2;
            Assert.That(elevatedCells, Has.Count.EqualTo(4));
            Assert.That(perimeterEdges, Is.EqualTo(10), "four inline balcony cells have ten perimeter edges");
            Assert.That(internalEdges, Is.EqualTo(3), "three adjacent balcony seams are internal");
            var stairPathLast = stairPath[stairPath.Count - 1];
            var stairPathPrevious = stairPath[stairPath.Count - 2];
            var openArrivalEdges = elevatedCells.Count(cell => cell == stairPathLast
                && elevatedCells.Contains(cell)
                && cell + (stairPathPrevious - cell) == stairPathPrevious);
            Assert.That(openArrivalEdges, Is.EqualTo(1), "only the authored stair arrival edge is open");

            Assert.That(Signature(dungeon), Is.EqualTo(Signature(Build(240917))));
        }

        [Test]
        public void StructuralPreviewShowsUpperGalleryThroughFogButKeepsLocalCutaway()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(new Vector2Int(0, 0), "ground");
            map.TryAddFloorCell(new Vector2Int(1, 0), "ground");
            map.TryAddFloorCell(new Vector2Int(2, 0), "ground");
            map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(2, 0), 1), "balcony");
            var first = new DungeonSurfacePosition(Vector2Int.zero);
            var second = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
            Assert.That(map.TryAddStairs(first, second,
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }), Is.True);

            var ground = new DungeonTraversalPosition(first);
            var upperGround = new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(2, 0)));
            var upper = new DungeonTraversalPosition(second);
            var step = new DungeonTraversalPosition(new DungeonStairConnection(first, second), 1);
            var state = new DungeonElevationViewState();
            state.Bind(map);
            state.SetFogVisibility(new[] { ground, upperGround });
            Assert.That(state.IsVisible(upper), Is.False, "normal fog keeps the upper gallery cut away");

            state.ConfigureStructuralPreview(true);
            Assert.That(state.IsVisible(upper), Is.True);
            Assert.That(state.IsVisible(step), Is.True);
            Assert.That(state.ProjectionLevel, Is.EqualTo(1));

            Assert.That(state.Focus(upperGround), Is.True);
            Assert.That(state.IsVisible(upper), Is.False,
                "standing directly below the balcony still preserves the local cutaway");

            state.ConfigureStructuralPreview(false);
            Assert.That(state.IsVisible(upper), Is.False,
                "disabling the fixture mode restores the normal fog projection");
        }

        [Test]
        public void StructuralPreviewShowsAllAuditBalconyCellsAndEveryStairRoutePosition()
        {
            var dungeon = Build(240917);
            var map = dungeon.Floors[0].Map;
            var stair = map.Stairs.Single();
            Assert.That(map.TryGetStairPath(stair, out var stairPath), Is.True);
            var upperCells = map.Surfaces.Where(surface => surface.Level == 1).ToArray();
            Assert.That(upperCells, Has.Length.EqualTo(4));

            var fogVisible = map.Surfaces
                .Where(surface => surface.Level == 0
                    && (upperCells.Any(upper => upper.Cell == surface.Cell)
                        || stairPath.Contains(surface.Cell)))
                .Select(surface => new DungeonTraversalPosition(surface))
                .ToArray();
            var state = new DungeonElevationViewState();
            state.Bind(map);
            state.SetFogVisibility(fogVisible);
            Assert.That(upperCells.All(surface => !state.IsVisible(surface)), Is.True,
                "normal fog hides the four upper gallery cells when their ground is visible");

            state.ConfigureStructuralPreview(true);
            Assert.That(upperCells.All(surface => state.IsVisible(surface)), Is.True);
            Assert.That(stairPath.All(cell => state.TryGetVisiblePosition(cell, out _)), Is.True,
                "structural preview exposes both stair landings and all three interior stair positions");
        }

        private static DungeonInstance Build(int seed)
        {
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(DungeonPath);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            return DungeonAssetAuditRoomBuilder.Build(definition, catalog, seed);
        }

        private static int ChebyshevDistance(Vector2Int first, Vector2Int second)
            => Mathf.Max(Mathf.Abs(first.x - second.x), Mathf.Abs(first.y - second.y));

        private static string Signature(DungeonInstance dungeon)
        {
            var floor = dungeon.Floors[0];
            var objects = floor.Objects
                .Select(instance => instance is DoorDungeonObjectInstance door
                    ? $"{instance.InstanceId}:{instance.Position}:{door.Edge}:{door.IsOpen}"
                    : $"{instance.InstanceId}:{instance.Position}")
                .OrderBy(value => value, StringComparer.Ordinal);
            var enemies = floor.Encounters
                .SelectMany(encounter => encounter.EnemyExplorationPositions
                    .Select(pair => $"{encounter.InstanceId}:{pair.Key}:{pair.Value}"))
                .OrderBy(value => value, StringComparer.Ordinal);
            return string.Join("|", objects.Concat(enemies));
        }
    }
}
