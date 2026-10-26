using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Runtime.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class ExplorationConsolidatedOptimizationTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
        static void Set(object owner, string name, object value)
        {
            for (var type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Flags);
                if (field == null) continue;
                field.SetValue(owner, value); return;
            }
            throw new MissingFieldException(name);
        }
        static T Bind<T>(object owner, string name) where T : Delegate => (T)owner.GetType().GetMethod(name, Flags).CreateDelegate(typeof(T), owner);
        static void EqualPixels(Color32[] expected, Color32[] actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
                if (!expected[i].Equals(actual[i])) Assert.Fail("Raster differs at pixel " + i);
        }

        [Test]
        public void CoalescedFogColoursMatchImmediateWritesDuringRetargetTicksAndExternalChanges()
        {
            var type = typeof(LightFogMapOptimizationTests).GetNestedType("FogFixture", BindingFlags.NonPublic);
            using var optimized = (IDisposable)Activator.CreateInstance(type, new object[] { 14 });
            using var reference = (IDisposable)Activator.CreateInstance(type, new object[] { 14 });
            var view = Read<DungeonFloorMapView>(optimized, "View");
            var other = Read<DungeonFloorMapView>(reference, "View");
            var tiles = Read<Tilemap>(optimized, "Tiles"); var oldTiles = Read<Tilemap>(reference, "Tiles");
            var project = Bind<Action<int, bool>>(optimized, "Project"); var oldProject = Bind<Action<int, bool>>(reference, "Project");
            var flush = Bind<Action>(view, "FlushFloorPresentationColors");
            var tick = Read<Action<float>>(optimized, "Tick"); var oldTick = Read<Action<float>>(reference, "Tick");
            foreach (int step in new[] { 0, 1, 2, 3, 0, 4, 0 })
            {
                Set(view, "batchingFloorColors", true);
                try { project(step, false); }
                finally { Set(view, "batchingFloorColors", false); flush(); }
                oldProject(step, false);
                foreach (float elapsed in new[] { 0f, .07f, .3f })
                {
                    if (elapsed > 0) { tick(elapsed); oldTick(elapsed); }
                    for (int y = 0; y < 14; y++) for (int x = 0; x < 14; x++)
                        Assert.That(tiles.GetColor(new Vector3Int(x, y, 0)), Is.EqualTo(oldTiles.GetColor(new Vector3Int(x, y, 0))));
                    CollectionAssert.AreEqual(Read<List<Color>>(other, "fogColors"), Read<List<Color>>(view, "fogColors"));
                }
            }
            var cell = new Vector3Int(2, 2, 0);
            tiles.SetColor(cell, Color.magenta); oldTiles.SetColor(cell, Color.magenta);
            var write = Bind<Action<Vector3Int, Color>>(view, "SetFloorPresentationColor");
            write(cell, Color.green); oldTiles.SetColor(cell, Color.green);
            Assert.That(tiles.GetColor(cell), Is.EqualTo(oldTiles.GetColor(cell)), "No cross-frame colour cache may hide a later write.");
            Set(view, "batchingFloorColors", true);
            write(cell, Color.red); write(cell, Color.blue); write(cell, Color.yellow);
            int before = view.FogFloorColorWriteCount;
            Set(view, "batchingFloorColors", false); flush();
            Assert.That(view.FogFloorColorWriteCount - before, Is.EqualTo(1));
            Assert.That(tiles.GetColor(cell), Is.EqualTo(Color.yellow));
            Assert.That(Read<IDictionary>(view, "pendingFloorColors").Count, Is.Zero);
        }

        [Test]
        public void ProductionFogRefreshCoalescesWritesAndKeepsFinalTileColoursAcrossDebugReset()
        {
            var type = typeof(LightFogMapOptimizationTests).GetNestedType("FogFixture", BindingFlags.NonPublic);
            using var fixture = (IDisposable)Activator.CreateInstance(type, new object[] { 14 });
            var view = Read<DungeonFloorMapView>(fixture, "View"); var tiles = Read<Tilemap>(fixture, "Tiles");
            var floor = view.FogFloor;
            // A partial reveal has actual bright/dark boundaries. A wholly
            // visible masonry map intentionally has no soft-fog patches.
            floor.Fog.SetVisible(floor.Map.FloorCells.Where(c => c.x < 7 && c.y < 7)
                .Select(c => new DungeonTraversalPosition(new DungeonSurfacePosition(c))).ToArray());
            int before = view.FogFloorColorCoalescedWriteCount;
            view.ApplyFog(floor);
            Assert.That(view.FogFloorColorCoalescedWriteCount, Is.GreaterThan(before));
            void Check()
            {
                foreach (var pair in Read<Dictionary<Vector3Int, (TileBase Tile, Color Color)>>(view, "presentedFloorCells"))
                    Assert.That(tiles.GetColor(pair.Key), Is.EqualTo(pair.Value.Color));
                Assert.That(Read<IDictionary>(view, "pendingFloorColors").Count, Is.Zero);
                Assert.That(Read<bool>(view, "batchingFloorColors"), Is.False);
            }
            Check(); floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); view.ApplyFog(floor); Check();
            floor.Fog.ToggleDebugFog(); view.ApplyFog(floor); Check();
            floor.Fog.ToggleDebugFog(); view.ApplyFog(floor); Check();
        }

        [Test]
        public void ObjectRasterBoundsMatchOriginalFootprintsAndDoorPixelsAtEveryViewportEdge()
        {
            var root = new GameObject("Minimap exact clipping");
            var obstacle = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            var doorDefinition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            try
            {
                var view = root.AddComponent<DungeonMinimapView>();
                var bounds = Bind<Func<DungeonObjectInstance, (int, int, int, int)>>(view, "ObjectRasterBounds");
                var intersects = (Func<(int, int, int, int), bool>)typeof(DungeonMinimapView).GetMethod("RasterIntersects", BindingFlags.Static | BindingFlags.NonPublic).CreateDelegate(typeof(Func<(int, int, int, int), bool>));
                var drawObstacle = Bind<Action<ObstacleDungeonObjectInstance, Color>>(view, "DrawObstacle");
                var drawDoor = Bind<Action<DungeonCellEdge, Color>>(view, "DrawEdge");
                var fill = (Action<Color32[], int, int, int, int, Color32>)typeof(DungeonMinimapView).GetMethod("Fill", BindingFlags.Static | BindingFlags.NonPublic).CreateDelegate(typeof(Action<Color32[], int, int, int, int, Color32>));
                var actual = Read<Color32[]>(view, "terrain"); var expected = new Color32[actual.Length];
                var random = new System.Random(18102026);
                for (int i = 0; i < 120; i++)
                {
                    Set(obstacle, "<Id>k__BackingField", i % 2 == 0 ? "object.obstacle.sarcophagus" : "object.obstacle.test");
                    var center = new Vector2Int(i % 3 - 1, i % 5 - 2); Set(view, "center", center);
                    var p = center + new Vector2Int(random.Next(-18, 19), random.Next(-18, 19));
                    Array.Clear(actual, 0, actual.Length); Array.Clear(expected, 0, expected.Length);
                    DungeonObjectInstance obj;
                    if (i % 3 == 0)
                    {
                        var edge = new DungeonCellEdge(p, p + (i % 2 == 0 ? Vector2Int.right : Vector2Int.up));
                        obj = new DoorDungeonObjectInstance("door", doorDefinition, edge);
                        var delta = edge.SecondCell - edge.FirstCell;
                        int x = 126 + (edge.FirstCell.x - center.x) * 8 + delta.x * 4;
                        int y = 126 + (edge.FirstCell.y - center.y) * 8 + delta.y * 4;
                        fill(expected, x - (delta.x == 0 ? 2 : 1), y - (delta.y == 0 ? 2 : 1), delta.x == 0 ? 5 : 2, delta.y == 0 ? 5 : 2, Color.red);
                        drawDoor(edge, Color.red);
                    }
                    else
                    {
                        var instance = new ObstacleDungeonObjectInstance("obstacle", obstacle, p); obj = instance;
                        var min = p; var max = p;
                        foreach (var cell in instance.EnumerateOccupiedCells()) { min = Vector2Int.Min(min, cell); max = Vector2Int.Max(max, cell); }
                        fill(expected, 126 + (min.x - center.x) * 8 - 3, 126 + (min.y - center.y) * 8 - 3, (max.x - min.x) * 8 + 6, (max.y - min.y) * 8 + 6, Color.red);
                        drawObstacle(instance, Color.red);
                    }
                    EqualPixels(expected, actual);
                    Assert.That(intersects(bounds(obj)), Is.EqualTo(expected.Any(pixel => pixel.a != 0)));
                }
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(obstacle); Object.DestroyImmediate(doorDefinition); }
        }

        [Test]
        public void MinimapSkipsDistantKnowledgeQueriesButKeepsPartiallyVisibleSarcophagus()
        {
            var root = new GameObject("Minimap object budget");
            var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                Set(definition, "<Id>k__BackingField", "object.obstacle.sarcophagus");
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap(); map.TryAddFloorCell(Vector2Int.zero, "room"); floor.TrySetMap(map); floor.Fog.ToggleDebugFog();
                for (int i = 0; i < 1000; i++) floor.TryAddObject(new ObstacleDungeonObjectInstance("far" + i, definition, new Vector2Int(1000 + i, 1000)));
                floor.TryAddObject(new ObstacleDungeonObjectInstance("partial", definition, new Vector2Int(0, -16)));
                var view = root.AddComponent<DungeonMinimapView>(); view.Bind(floor, null, null, null);
                Bind<Action>(view, "DrawTerrain")();
                Assert.That(view.MinimapObjectKnowledgeQueryCount, Is.EqualTo(1));
                Assert.That(Read<Color32[]>(view, "terrain")[126], Is.EqualTo((Color32)Read<Color>(view, "obstacleColor")));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }

        [Test]
        public void NoAggroDiscardsQueuedAlertsAndMergesWithoutEndingCombat()
        {
            var root=new GameObject("Noaggro pending membership");
            try
            {
                var owner=root.AddComponent<DungeonCombatCoordinator>();
                var game=new GameSession{DebugEnemyAggroDisabled=true};Set(owner,"game",game);
                var first=new CombatSession{MembershipChangePending=true};var second=new CombatSession{MembershipChangePending=true};
                Read<List<CombatSession>>(game,"combats").AddRange(new[]{first,second});
                Read<List<(CombatSession,CombatSession)>>(owner,"pendingMerges").Add((first,second));
                // A real queued tuple with invalid actors must be discarded before inspecting it.
                Read<IList>(owner,"pendingRoomAlerts").Add((default(ExplorationCharacterView),default(EncounterTriggerView),default(BattleUnit),default(GridPosition)));
                Bind<Action>(owner,"ProcessRoomAlerts")();Bind<Action>(owner,"ProcessPendingMerges")();
                Assert.That(Read<IList>(owner,"pendingRoomAlerts"),Is.Empty);
                Assert.That(Read<IList>(owner,"pendingMerges"),Is.Empty);
                Assert.That(first.MembershipChangePending||second.MembershipChangePending,Is.False);
                Assert.That(game.Combats.Count,Is.EqualTo(2));
                Assert.That(first.Outcome,Is.EqualTo(CombatOutcomeState.Ongoing));
            }
            finally{Object.DestroyImmediate(root);}
        }

        [TestCase(false)] [TestCase(true)]
        public void DetectionTickReadsEachActorOnceAndDiscardsSnapshotsBeforeNextTick(bool noAggro)
        {
            var root = new GameObject("Detection query batch"); var definition = ScriptableObject.CreateInstance<EncounterDefinition>();
            try
            {
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
                for (int x = 0; x < 130; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room"); floor.TrySetMap(map);
                var owner = root.AddComponent<DungeonCombatCoordinator>(); Set(owner, "floor", floor);
                var game=new GameSession{DebugEnemyAggroDisabled=noAggro};Set(owner,"game",game);
                var party = root.AddComponent<ExplorationPartyController>(); Set(owner, "party", party);
                var spawner = root.AddComponent<DungeonEncounterSpawner>(); Set(owner, "encounters", spawner);
                ExplorationCharacterView Actor(string id, int x)
                {
                    var go = new GameObject(id); go.transform.SetParent(root.transform); go.transform.position = new Vector3(x + .5f, .5f);
                    var actor = go.AddComponent<ExplorationCharacterView>();
                    Set(actor, "<RuntimeCharacter>k__BackingField", new CharacterInstance { CharacterId = id, LifeState = CharacterLifeState.Alive });
                    return actor;
                }
                var members = Read<Dictionary<string, ExplorationCharacterView>>(party, "viewsById");
                for (int i = 0; i < 4; i++) members.Add("player" + i, Actor("player" + i, i));
                var enemies = new List<ExplorationCharacterView>();
                for (int i = 0; i < 20; i++) { var enemy = Actor("enemy" + i, 100 + i); enemy.SetEnemyInstanceId("enemy" + i); enemies.Add(enemy); }
                var trigger = root.AddComponent<EncounterTriggerView>();
                Set(trigger, "encounterInstance", new DungeonEncounterInstance("batch", definition, "room", new RectInt(0, 0, 130, 1))); Set(trigger, "enemyViews", enemies);
                Read<List<EncounterTriggerView>>(spawner, "spawnedTriggers").Add(trigger);
                var tick = Bind<Action>(owner, "UpdateDetection");
                var awareness=floor.GetEnemyAwareness(Godmist.Systems.Combat.DungeonCombatMembershipSystem.EnemyKey("batch",enemies[0].EnemyInstanceId));
                awareness.TryRestore(1f);
                Set(owner, "detectionElapsed", .1f); tick();
                Assert.That(owner.DetectionPositionReadCount, Is.EqualTo(noAggro?0:24), "Four targets must not be reread for every enemy; noaggro skips detection queries.");
                Assert.That(Read<IDictionary>(owner, "detectionPositions").Count, Is.Zero);
                enemies[0].transform.position += Vector3.right;
                Set(owner, "detectionElapsed", .1f); tick();
                Assert.That(owner.DetectionPositionReadCount, Is.EqualTo(noAggro?0:48));
                if(noAggro)
                {
                    Assert.That(awareness.Progress,Is.Zero);
                    game.DebugEnemyAggroDisabled=false;Set(owner,"detectionElapsed",.1f);tick();
                    Assert.That(owner.DetectionPositionReadCount,Is.EqualTo(24),"Normal detection resumes after noaggro off.");
                }
                Assert.That(Read<IDictionary>(owner, "detectionPositions").Count, Is.Zero);
                var read = Bind<Func<ExplorationCharacterView, DungeonTraversalPosition>>(owner, "ReadDetectionPosition");
                read(enemies[0]); // A deliberately stale private snapshot must not affect the public API.
                Set(enemies[0], "<VisionAngle>k__BackingField", 360f);
                enemies[0].transform.position = new Vector3(4.5f, .5f);
                Assert.That(owner.CanEnemyDetect(enemies[0], members["player0"]), Is.True);
                enemies[0].transform.position = new Vector3(100.5f, .5f);
                Assert.That(owner.CanEnemyDetect(enemies[0], members["player0"]), Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(definition); }
        }
    }
}
