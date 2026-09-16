using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Core.Serialization;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Systems.SaveLoad;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Tests.EditMode
{
    public class DungeonFogTests
    {
        [Test]
        public void ConsoleMapCommandsUseFogMemoryAndToggleDoesNotRevealKnowledge()
        {
            var session = new Godmist.Runtime.Game.GameSession();
            var dungeon = new DungeonInstance("debug", 2);
            session.TrySetActiveDungeon(dungeon);
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room");
            map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.right, 1), "room");
            floor.TrySetMap(map); dungeon.TryAddFloor(floor);
            var service = new Godmist.Systems.Debugging.DevConsoleCommandService();
            Assert.That(service.Execute("revealmap", session, null).Success, Is.True);
            Assert.That(floor.Fog.Discovered.Count, Is.EqualTo(2));
            Assert.That(floor.Fog.Visible, Is.Empty);
            Assert.That(DungeonFogPersistence.Capture(floor).Discovered.Count, Is.EqualTo(2));
            Assert.That(service.Execute("clearmap", session, null).Success, Is.True);
            Assert.That(floor.Fog.Discovered, Is.Empty);
            var revision = floor.Fog.DebugRevision;
            Assert.That(service.Execute("togglefog", session, null).Success, Is.True);
            Assert.That(floor.Fog.DebugFogDisabled, Is.True);
            Assert.That(floor.Fog.DebugRevision, Is.GreaterThan(revision));
            Assert.That(DungeonFogPersistence.Capture(floor).Discovered, Is.Empty);
            var go = new GameObject("fog test");
            try
            {
                var view = go.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetProperty("FogFloor").SetValue(view, floor);
                Assert.That(view.Knowledge(At(0)), Is.EqualTo(DungeonKnowledge.Visible));
                Assert.That(floor.Fog.IsVisible(At(0)), Is.False);
                Assert.That(service.Execute("togglefog", session, null).Success, Is.True);
                Assert.That(view.Knowledge(At(0)), Is.EqualTo(DungeonKnowledge.Unexplored));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Assert.That(service.Execute("togglefog", new Godmist.Runtime.Game.GameSession(), null).Success, Is.False);
        }

        [Test]
        public void WallHeavyVisibilityBatchMatchesReferenceAndReportsCost()
        {
            var map = new DungeonFloorMap();
            var nodes = new List<DungeonTraversalPosition>();
            for (var x = 0; x < 60; x++) for (var y = 0; y < 60; y++)
            {
                var cell = new Vector2Int(x, y);
                map.TryAddFloorCell(cell, "room");
                nodes.Add(new DungeonTraversalPosition(new DungeonSurfacePosition(cell)));
                if (x % 10 == 9 && y % 10 != 5) map.TryAddWall(cell, cell + Vector2Int.right);
                if (y % 10 == 9 && x % 10 != 5) map.TryAddWall(cell, cell + Vector2Int.up);
            }
            var expected = new bool[nodes.Count];
            var observer = At(5);
            map.TryGetSightPoint(observer, out var origin);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < nodes.Count; i++)
            {
                map.TryGetSightPoint(nodes[i], out var target);
                foreach (var edge in map.SurfaceWallEdges)
                    if (DungeonFloorMap.SightCrossesEdge(origin, target, edge)) { expected[i] = true; break; }
            }
            var referenceMs = watch.Elapsed.TotalMilliseconds;
            var actual = new bool[nodes.Count];
            watch.Restart();
            for (var i = 0; i < nodes.Count; i++) actual[i] = map.BlocksStructuralSight(observer, nodes[i]);
            var indexedMs = watch.Elapsed.TotalMilliseconds;
            Assert.That(actual, Is.EqualTo(expected));
            TestContext.WriteLine($"3600 cells, room walls: exhaustive {referenceMs:F2} ms; indexed {indexedMs:F2} ms");
        }

        [Test]
        public void NearbyObserverBehindWallDoesNotRecordDistantAlliesSight()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var x = 0; x < 12; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            map.TryAddWall(Vector2Int.zero, Vector2Int.right);
            floor.TrySetMap(map);
            var fog = new DungeonFogSystem();
            fog.Update(floor, new[] { At(0), At(10) });
            Assert.That(floor.Fog.Get(At(4)), Is.EqualTo(DungeonKnowledge.Visible));
            fog.Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.Get(At(4)), Is.EqualTo(DungeonKnowledge.Unexplored));
        }

        [Test]
        public void DistantVisibleTerrainIsOnlyRememberedWithinFiveCells()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var x = 0; x < 12; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            floor.TrySetMap(map);
            var fog = new DungeonFogSystem();
            fog.Update(floor, new[] { At(0) });
            Assert.That(floor.Fog.Get(At(6)), Is.EqualTo(DungeonKnowledge.Visible));
            fog.Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.Get(At(5)), Is.EqualTo(DungeonKnowledge.Remembered));
            Assert.That(floor.Fog.Get(At(6)), Is.EqualTo(DungeonKnowledge.Unexplored));
            fog.Update(floor, new[] { At(1) });
            fog.Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.Get(At(6)), Is.EqualTo(DungeonKnowledge.Remembered));
        }

        [Test]
        public void IndexedWallSightMatchesExhaustiveEdgesIncludingCornersAndNegativeCells()
        {
            var map = new DungeonFloorMap();
            var nodes = new List<DungeonTraversalPosition>();
            var random = new System.Random(147);
            for (var x = -4; x <= 4; x++) for (var y = -4; y <= 4; y++)
            {
                var cell = new Vector2Int(x, y);
                map.TryAddFloorCell(cell, "room");
                nodes.Add(new DungeonTraversalPosition(new DungeonSurfacePosition(cell)));
                if (random.Next(3) == 0) map.TryAddWall(cell, cell + Vector2Int.right);
                if (random.Next(3) == 0) map.TryAddWall(cell, cell + Vector2Int.up);
            }
            foreach (var from in nodes) foreach (var to in nodes)
            {
                map.TryGetSightPoint(from, out var a); map.TryGetSightPoint(to, out var b);
                var expected = false;
                foreach (var edge in map.SurfaceWallEdges)
                    if (DungeonFloorMap.SightCrossesEdge(a, b, edge)) { expected = true; break; }
                Assert.That(map.BlocksStructuralSight(from, to), Is.EqualTo(expected), $"{a} -> {b}");
            }
        }
        [Test]
        public void HiddenTimelineHoverCannotExposeCurrentTargetAndRecoversWhenVisible()
        {
            var instance = new GameObject("visibility-test");
            instance.SetActive(false);
            try
            {
                var map = instance.AddComponent<DungeonFloorMapView>();
                var spawner = instance.AddComponent<Godmist.Views.Combat.CombatUnitViewSpawner>();
                var bootstrap = instance.AddComponent<Godmist.Views.Combat.CombatSandboxBootstrap>();
                var hud = instance.AddComponent<Godmist.Views.UI.CombatHudView>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(Godmist.Views.Combat.CombatUnitViewSpawner).GetField("floorMap", flags).SetValue(spawner, map);
                typeof(Godmist.Views.Combat.CombatSandboxBootstrap).GetField("unitViewSpawner", flags).SetValue(bootstrap, spawner);
                typeof(Godmist.Views.UI.CombatHudView).GetField("sandboxBootstrap", flags).SetValue(hud, bootstrap);
                var unit = new Godmist.Runtime.Combat.BattleUnit();
                var visible = true;
                spawner.ExplorationPresentationVisible = _ => visible;
                hud.SetTimelineHover(null, unit);
                Assert.That(hud.HoveredUnit, Is.SameAs(unit));
                visible = false;
                Assert.That(hud.HoveredUnit, Is.Null);
                Assert.That(hud.HighlightedUnit, Is.Null);
                visible = true;
                Assert.That(hud.HoveredUnit, Is.SameAs(unit));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void UnknownTimelineEntryHasNoUnitReferenceOrPortraitAndRecoversWhenSeen()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatTurnPortrait.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<Godmist.Views.UI.CombatTurnPortraitView>();
                var unit = new Godmist.Runtime.Combat.BattleUnit { Character = new Godmist.Runtime.Characters.CharacterInstance { CharacterId = "secret" } };
                view.Bind(unit, true, known: false);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                Assert.That(typeof(Godmist.Views.UI.CombatTurnPortraitView).GetField("boundUnit", flags).GetValue(view), Is.Null);
                var serialized = new UnityEditor.SerializedObject(view);
                var label = serialized.FindProperty("unknownLabel").objectReferenceValue as Component;
                Assert.That(label, Is.Not.Null);
                Assert.That(label.gameObject.activeSelf, Is.True);
                var portrait = serialized.FindProperty("portrait").objectReferenceValue as UnityEngine.UI.Image;
                Assert.That(portrait.sprite, Is.Null);
                view.Bind(unit, true, known: true);
                Assert.That(label.gameObject.activeSelf, Is.False);
                Assert.That(typeof(Godmist.Views.UI.CombatTurnPortraitView).GetField("boundUnit", flags).GetValue(view), Is.SameAs(unit));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void DiscoveryRoundTripRetainsStairStep()
        {
            var floor = Floor();
            var upper = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
            floor.Map.TryAddFloorCell(upper, "room");
            floor.Map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper, new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) });
            var stair = new DungeonStairConnection(new DungeonSurfacePosition(Vector2Int.zero), upper);
            var step = new DungeonTraversalPosition(stair, 1);
            floor.Fog.SetVisible(new[] { step });
            var saved = DungeonFogPersistence.Capture(floor);
            var restored = new DungeonFloorInstance(0);
            restored.TrySetMap(floor.Map);
            Assert.That(DungeonFogPersistence.Restore(restored, saved), Is.True);
            Assert.That(restored.Fog.Get(step), Is.EqualTo(DungeonKnowledge.Remembered));
        }

        [Test]
        public void VisibilityBatchTimingOnTwentyByTwentyRoom()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var x = 0; x < 20; x++) for (var y = 0; y < 20; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            floor.TrySetMap(map);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            new DungeonFogSystem().Update(floor, new[] { At(0) });
            watch.Stop();
            TestContext.WriteLine("Fog 400-cell batch ms: " + watch.Elapsed.TotalMilliseconds);
            Assert.That(floor.Fog.Visible.Count, Is.EqualTo(400));
        }

        private static DungeonTraversalPosition At(int x, int level = 0) => new(new DungeonSurfacePosition(new Vector2Int(x, 0), level));
        private static DungeonFloorInstance Floor()
        {
            var floor = new DungeonFloorInstance(0);
            floor.TryAddRoom(new DungeonRoomInstance("room", DungeonRoomType.Start));
            var map = new DungeonFloorMap();
            for (var x = 0; x < 6; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            floor.TrySetMap(map);
            return floor;
        }

        [Test]
        public void DiscoveryRemainsAfterSightLossAndIsIndependentOfHeight()
        {
            var floor = Floor();
            floor.Map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "room");
            var system = new DungeonFogSystem();
            system.Update(floor, new[] { At(0) });
            Assert.That(floor.Fog.Get(At(0)), Is.EqualTo(DungeonKnowledge.Visible));
            Assert.That(floor.Fog.Get(At(0, 1)), Is.EqualTo(DungeonKnowledge.Unexplored));
            system.Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.Get(At(0)), Is.EqualTo(DungeonKnowledge.Remembered));
        }

        [Test]
        public void SharedSightRevealsBothSidesOfWallButSingleObserverDoesNot()
        {
            var floor = Floor();
            floor.Map.SetWallType(new DungeonCellEdge(new Vector2Int(2, 0), new Vector2Int(3, 0)), DungeonWallType.Full);
            var system = new DungeonFogSystem();
            system.Update(floor, new[] { At(0) });
            Assert.That(floor.Fog.Get(At(5)), Is.EqualTo(DungeonKnowledge.Unexplored));
            system.Update(floor, new[] { At(0), At(5) });
            Assert.That(floor.Fog.Get(At(5)), Is.EqualTo(DungeonKnowledge.Visible));
        }

        [Test]
        public void DoorMemoryDoesNotUpdateWithoutSightAndSurvivesSerialization()
        {
            var definition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            try
            {
                var floor = Floor();
                var door = new DoorDungeonObjectInstance("door", definition, new DungeonCellEdge(new Vector2Int(2, 0), new Vector2Int(3, 0)));
                floor.TryAddObject(door);
                var system = new DungeonFogSystem();
                system.Update(floor, new[] { At(0) });
                Assert.That(floor.Fog.Objects["door"].State, Is.False);
                Assert.That(floor.Fog.Get(At(5)), Is.EqualTo(DungeonKnowledge.Unexplored));
                system.Update(floor, Array.Empty<DungeonTraversalPosition>());
                floor.TrySetDoorOpen("door", true);
                system.Update(floor, Array.Empty<DungeonTraversalPosition>());
                Assert.That(floor.Fog.Objects["door"].State, Is.False);
                var saved = JsonUtility.FromJson<DungeonFogSaveData>(JsonUtility.ToJson(DungeonFogPersistence.Capture(floor)));
                var restored = Floor();
                restored.TryAddObject(new DoorDungeonObjectInstance("door", definition, door.Edge));
                restored.TrySetDoorOpen("door", true);
                Assert.That(DungeonFogPersistence.Restore(restored, saved), Is.True);
                Assert.That(restored.Fog.Objects["door"].State, Is.False);
                Assert.That(restored.Fog.Visible, Is.Empty);
                system.Update(restored, new[] { At(0) });
                Assert.That(restored.Fog.Objects["door"].State, Is.True);
                Assert.That(restored.Fog.Get(At(5)), Is.EqualTo(DungeonKnowledge.Visible));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void RememberedGateRemainsClosedUntilReobserved()
        {
            var floor = Floor();
            var gate = new DungeonSurfaceEdge(new DungeonCellEdge(new Vector2Int(2, 0), new Vector2Int(3, 0)));
            floor.Map.TrySetProgressionGateState(gate, false);
            var system = new DungeonFogSystem();
            system.Update(floor, new[] { At(0) });
            Assert.That(floor.Fog.ClosedGates.Contains(gate), Is.True);
            system.Update(floor, Array.Empty<DungeonTraversalPosition>());
            floor.Map.TrySetProgressionGateState(gate, true);
            system.Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.ClosedGates.Contains(gate), Is.True);
            system.Update(floor, new[] { At(0) });
            Assert.That(floor.Fog.ClosedGates, Is.Empty);
        }

        [Test]
        public void LegacySavePreservesMapKnowledgeWithoutGrantingCurrentVision()
        {
            var floor = Floor();
            Assert.That(DungeonFogPersistence.Restore(floor, null), Is.True);
            Assert.That(DungeonFogPersistence.Capture(floor), Is.Null);
            new DungeonFogSystem().Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.Get(At(5)), Is.EqualTo(DungeonKnowledge.Remembered));
            Assert.That(floor.Fog.Visible, Is.Empty);
        }

        [Test]
        public void DuplicateAndOutOfMapDiscoveryAreRejected()
        {
            var saved = new DungeonFogSaveData();
            saved.Discovered.Add(new DungeonActorPositionSaveData { X = 500 });
            Assert.That(DungeonFogPersistence.Restore(Floor(), saved), Is.False);
            saved.Discovered[0].X = 0;
            saved.Discovered.Add(new DungeonActorPositionSaveData());
            Assert.That(DungeonFogPersistence.Restore(Floor(), saved), Is.False);
        }

        [Test]
        public void FogCutawayUsesSharedSightAndManualHeightDoesNotGrantKnowledge()
        {
            var floor = Floor();
            floor.Map.TryAddFloorCell(new DungeonSurfacePosition(Vector2Int.zero, 1), "room");
            var elevation = new DungeonElevationViewState();
            elevation.Bind(floor.Map);
            elevation.SetFogVisibility(new[] { At(0) });
            Assert.That(elevation.IsVisible(At(0)), Is.True);
            Assert.That(elevation.IsVisible(At(0, 1)), Is.False);
            elevation.Step(-1);
            Assert.That(floor.Fog.Discovered, Is.Empty);
        }

        [Test]
        public void MapPresentsBlackUnknownAndDimRememberedWithoutChangingCollision()
        {
            var root = new GameObject("map", typeof(Grid));
            var tiles = new GameObject("tiles", typeof(Tilemap), typeof(TilemapRenderer));
            var wall = new GameObject("wall", typeof(DungeonWallSegmentView), typeof(SpriteRenderer));
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                tiles.transform.SetParent(root.transform);
                var map = root.AddComponent<DungeonFloorMapView>();
                void Set(string name, object value) => typeof(DungeonFloorMapView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(map, value);
                var tilemap = tiles.GetComponent<Tilemap>();
                Set("floorTilemap", tilemap); Set("floorTile", tile); Set("wallSegmentPrefab", wall.GetComponent<DungeonWallSegmentView>());
                var floor = Floor();
                map.Rebuild(floor.Map); map.ApplyFog(floor);
                Assert.That(tilemap.GetTile(Vector3Int.zero), Is.Null);
                floor.Fog.SetVisible(new[] { At(0) }); map.ApplyFog(floor);
                Assert.That(tilemap.GetColor(Vector3Int.zero), Is.EqualTo(Color.white));
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); map.ApplyFog(floor);
                Assert.That(tilemap.GetColor(Vector3Int.zero).r, Is.EqualTo(.55f).Within(.001f));
                Assert.That(map.IsTraversalVisible(At(0)), Is.False);
                Assert.That(floor.Map.IsWalkable(Vector2Int.zero), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(tile); }
        }
    }
}
