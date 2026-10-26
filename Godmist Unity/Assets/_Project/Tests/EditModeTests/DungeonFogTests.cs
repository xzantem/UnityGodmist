using System;
using System.Collections.Generic;
using System.Linq;
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
        [TestCase(false)]
        [TestCase(true)]
        public void BackgroundFogMatchesSynchronousVisibilityWithoutFrameDependentDelay(bool layered)
        {
            var floor = new DungeonFloorInstance(0);
            var geometry = new DungeonFloorMap();
            for (var x = 0; x < 60; x++) for (var y = 0; y < 60; y++)
                geometry.TryAddFloorCell(new Vector2Int(x, y), "room");
            geometry.TryAddWall(new Vector2Int(10, 10), new Vector2Int(11, 10));
            geometry.TrySetProgressionGateState(new DungeonCellEdge(new Vector2Int(20, 20), new Vector2Int(21, 20)), false);
            if (layered)
            {
                var landing = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
                geometry.TryAddFloorCell(landing, "upper");
                geometry.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), landing,
                    new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) });
            }
            floor.TrySetMap(geometry);
            var observers = new[] { At(0), At(59) };
            var expected = new HashSet<DungeonTraversalPosition>();
            var discovered = new HashSet<DungeonTraversalPosition>();
            new DungeonVisibilitySystem().CollectPartyVisibleAndDiscovered(floor, observers, expected, discovered);
            // Seen static wall endpoints persist beyond the ordinary terrain radius.
            foreach (var cell in new[] { new Vector2Int(10, 10), new Vector2Int(11, 10) })
            {
                var position = new DungeonTraversalPosition(new DungeonSurfacePosition(cell));
                if (expected.Contains(position)) discovered.Add(position);
            }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            using var job = new DungeonFogSystem().CreateUpdateJob(floor, observers, background: true);
            // No owner ticks are necessary for the expensive calculation itself.
            var worker = (System.Threading.Tasks.Task<bool>)typeof(DungeonFogUpdateJob)
                .GetField("worker", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(job);
            Assert.That(worker.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(floor.Fog.Visible, Is.Empty, "Workers must never publish to live state.");
            job.AdvanceSlice();
            Assert.That(job.Applied, Is.True);
            Assert.That(floor.Fog.Visible, Is.EquivalentTo(expected));
            Assert.That(floor.Fog.Discovered, Is.EquivalentTo(discovered));
            TestContext.WriteLine($"Background fog 60x60, layered={layered}: {timer.Elapsed.TotalMilliseconds:F1} ms (one publish tick)");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CoordinatorPublishesFogDuringContinuousMovementAndCatchesUpAfterStopping(bool moving)
        {
            var root = new GameObject("fog coordinator regression");
            var actorObject = new GameObject("observer");
            try
            {
                var floor = new DungeonFloorInstance(0);
                var geometry = new DungeonFloorMap();
                for (var x = 0; x < 80; x++) for (var y = 0; y < 80; y++)
                    geometry.TryAddFloorCell(new Vector2Int(x, y), "room");
                floor.TrySetMap(geometry);
                var coordinator = root.AddComponent<DungeonCombatCoordinator>();
                var party = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                var map = root.AddComponent<DungeonFloorMapView>();
                var feedback = root.AddComponent<Godmist.Views.Combat.CombatGridFeedbackRenderer>();
                var actor = actorObject.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.Bind(new Godmist.Runtime.Characters.CharacterInstance
                { CharacterId = "observer", LifeState = CharacterLifeState.Alive });
                var views = (Dictionary<string, Godmist.Views.Exploration.ExplorationCharacterView>)typeof(
                    Godmist.Views.Exploration.ExplorationPartyController).GetField("viewsById",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(party);
                views.Add("observer", actor);
                var type = typeof(DungeonCombatCoordinator);
                void Set(string name, object value) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(coordinator, value);
                Set("floor", floor); Set("party", party); Set("map", map); Set("gridFeedback", feedback);
                var update = type.GetMethod("UpdateFog", BindingFlags.Instance | BindingFlags.NonPublic);
                void Tick(bool force = false)
                {
                    // Advance a deterministic number of work steps as well: this
                    // regression checks scheduling correctness, not machine speed.
                    var pending = (DungeonFogUpdateJob)type.GetField("fogJob",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
                    if (pending != null) System.Threading.Thread.Sleep(10);
                    // EditMode drives the scene-owner method explicitly; emulate a new rendered frame.
                    Set("lastFogSliceFrame", -1); Set("nextFogTime", 0f);
                    update.Invoke(coordinator, new object[] { force });
                }
                Tick(true);
                for (var i = 0; i < 120; i++)
                {
                    if (moving) actor.transform.position = new Vector3((i + 1) % 10, 0, 0);
                    Tick();
                }
                Assert.That(floor.Fog.Revision, Is.GreaterThan(0), "Movement must not starve publication indefinitely.");
                Assert.That(map.FogFloor, Is.SameAs(floor), "The scene owner must publish the result to the map view.");
                actor.transform.position = new Vector3(79, 0, 0);
                for (var i = 0; i < 100; i++) Tick();
                var appliedObservers = (DungeonTraversalPosition[])type.GetField("fogObservers",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator);
                Assert.That(appliedObservers, Is.EquivalentTo(new[] { At(79) }));
                Assert.That(floor.Fog.Get(At(79)), Is.EqualTo(DungeonKnowledge.Visible));
                actor.transform.position = new Vector3(78, 0, 0);
                Tick();
                Assert.That(type.GetField("fogJob", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(coordinator),
                    Is.Not.Null, "This map requires multiple slices, so death occurs while a job is pending.");
                actor.RuntimeCharacter.LifeState = CharacterLifeState.Dead;
                var revision = floor.Fog.Revision;
                Tick();
                Assert.That(floor.Fog.Revision, Is.EqualTo(revision), "The dead observer's pending result must be canceled.");
                Tick();
                Assert.That(floor.Fog.Visible, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actorObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SlicedFogMatchesSynchronousVisibilityAndPublishesOnlyCompleteResult()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var x = 0; x < 20; x++) for (var y = 0; y < 20; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
            var upper = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
            map.TryAddFloorCell(upper, "room");
            map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper,
                new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) });
            map.TryAddWall(new Vector2Int(7, 0), new Vector2Int(8, 0));
            floor.TrySetMap(map);
            var observers = new[] { At(0), At(19) };
            var expectedVisible = new HashSet<DungeonTraversalPosition>();
            var expectedDiscovered = new HashSet<DungeonTraversalPosition>();
            new DungeonVisibilitySystem().CollectPartyVisibleAndDiscovered(floor, observers, expectedVisible, expectedDiscovered);
            foreach (var position in new[] { At(7), At(8) })
                if (expectedVisible.Contains(position)) expectedDiscovered.Add(position);
            floor.Fog.SetVisible(new[] { At(0) });
            using var job = new DungeonFogSystem().CreateUpdateJob(floor, observers);
            var revision = floor.Fog.Revision;
            var slices = 0;
            while (!job.IsComplete)
            {
                job.AdvanceSlice(maxSteps: 1);
                Assert.That(++slices, Is.LessThan(4000));
                if (job.IsComplete) break;
                Assert.That(floor.Fog.Revision, Is.EqualTo(revision));
                Assert.That(floor.Fog.Visible, Is.EquivalentTo(new[] { At(0) }));
                Assert.That(floor.Fog.Discovered, Is.EquivalentTo(new[] { At(0) }));
            }
            Assert.That(slices, Is.GreaterThan(400));
            Assert.That(job.Applied, Is.True);
            Assert.That(floor.Fog.Visible, Is.EquivalentTo(expectedVisible));
            Assert.That(floor.Fog.Discovered, Is.EquivalentTo(expectedDiscovered));
            job.AdvanceSlice();
            Assert.That(floor.Fog.Revision, Is.EqualTo(revision + 1));
        }

        [Test]
        public void IndependentPendingFogJobsDoNotShareVisibilityBuffers()
        {
            var first = Floor();
            var second = Floor();
            var system = new DungeonFogSystem();
            using var firstJob = system.CreateUpdateJob(first, new[] { At(0) });
            using var secondJob = system.CreateUpdateJob(second, new[] { At(5) });
            for (var i = 0; !firstJob.IsComplete || !secondJob.IsComplete; i++)
            {
                Assert.That(i, Is.LessThan(100));
                firstJob.AdvanceSlice(maxSteps: 1);
                secondJob.AdvanceSlice(maxSteps: 1);
            }
            Assert.That(firstJob.Applied && secondJob.Applied, Is.True);
            Assert.That(first.Fog.Visible, Is.EquivalentTo(second.Fog.Visible));
            Assert.That(first.Fog.Visible.Count, Is.EqualTo(6));
        }

        [Test]
        public void SlicedFogWithNoObserversClearsVisibilityButPreservesDiscovery()
        {
            var floor = Floor();
            floor.Fog.SetVisible(new[] { At(0) });
            using var job = new DungeonFogSystem().CreateUpdateJob(floor, Array.Empty<DungeonTraversalPosition>());
            job.AdvanceSlice();
            Assert.That(job.Applied, Is.True);
            Assert.That(floor.Fog.Visible, Is.Empty);
            Assert.That(floor.Fog.Discovered, Is.EquivalentTo(new[] { At(0) }));
        }

        [TestCase("wall", false)]
        [TestCase("wall", true)]
        [TestCase("gate", false)]
        [TestCase("gate", true)]
        [TestCase("door", false)]
        [TestCase("door", true)]
        [TestCase("obstacle", false)]
        [TestCase("obstacle", true)]
        [TestCase("observer", false)]
        [TestCase("observer", true)]
        [TestCase("owner", false)]
        [TestCase("owner", true)]
        [TestCase("debug", false)]
        [TestCase("debug", true)]
        [TestCase("dispose", false)]
        [TestCase("dispose", true)]
        [TestCase("other-result", false)]
        [TestCase("other-result", true)]
        public void SlicedFogCancelsStaleWorkWithoutRevealingPartialKnowledge(string change, bool background)
        {
            var floor = Floor();
            var definition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            var obstacleDefinition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            try
            {
                var edge = new DungeonCellEdge(Vector2Int.zero, Vector2Int.right);
                floor.TryAddObject(new DoorDungeonObjectInstance("door", definition, edge));
                var obstacle = new ObstacleDungeonObjectInstance("obstacle", obstacleDefinition, new Vector2Int(2, 0));
                floor.TryAddObject(obstacle);
                floor.Fog.SetVisible(new[] { At(0) });
                var observers = new[] { At(0) };
                var validOwner = true;
                using var job = new DungeonFogSystem().CreateUpdateJob(floor, observers, () => validOwner);
                job.AdvanceSlice(maxSteps: 1);
                Assert.That(job.IsComplete, Is.False);
                switch (change)
                {
                    case "wall": floor.Map.TryAddWall(Vector2Int.zero, Vector2Int.right); break;
                    case "gate": floor.Map.TrySetProgressionGateState(edge, false); break;
                    case "door": floor.TrySetDoorOpen("door", true); break;
                    case "obstacle": obstacle.TryDeactivate(); break;
                    case "observer": observers[0] = At(1); break;
                    case "owner": validOwner = false; break;
                    case "debug": floor.Fog.ToggleDebugFog(); break;
                    case "dispose": job.Dispose(); break;
                    case "other-result": floor.Fog.SetVisible(new[] { At(0) }); break;
                }
                var revision = floor.Fog.Revision;
                job.AdvanceSlice();
                Assert.That(job.IsCanceled, Is.True);
                Assert.That(job.Applied, Is.False);
                Assert.That(floor.Fog.Revision, Is.EqualTo(revision));
                Assert.That(floor.Fog.Visible, Is.EquivalentTo(new[] { At(0) }));
                Assert.That(floor.Fog.Discovered, Is.EquivalentTo(new[] { At(0) }));
                Assert.That(floor.Fog.Objects, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(obstacleDefinition);
            }
        }

        [Test]
        public void CombinedVisibilityMatchesIndependentRaysAfterDoorAndObstacleChanges()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var x = 0; x < 14; x++) for (var y = 0; y < 4; y++)
                map.TryAddFloorCell(new Vector2Int(x, y), "room");
            floor.TrySetMap(map);
            var doorDefinition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            var obstacleDefinition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            typeof(ObstacleDungeonObjectDefinition).GetField("<BlocksLineOfSight>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obstacleDefinition, true);
            try
            {
                var door = new DoorDungeonObjectInstance("door", doorDefinition,
                    new DungeonCellEdge(new Vector2Int(3, 0), new Vector2Int(4, 0)));
                var obstacle = new ObstacleDungeonObjectInstance("obstacle", obstacleDefinition, new Vector2Int(7, 0));
                floor.TryAddObject(door); floor.TryAddObject(obstacle);
                var system = new DungeonVisibilitySystem();
                var fogSystem = new DungeonFogSystem();
                var actual = new HashSet<DungeonTraversalPosition>();
                var discovered = new HashSet<DungeonTraversalPosition>();
                var observers = new[] { At(0), At(13) };
                var remembered = new HashSet<DungeonTraversalPosition>();
                void Check()
                {
                    var expected = new HashSet<DungeonTraversalPosition>();
                    var expectedDiscovered = new HashSet<DungeonTraversalPosition>();
                    foreach (var surface in map.Surfaces)
                        foreach (var observer in observers)
                        {
                            var target = new DungeonTraversalPosition(surface);
                            map.TryGetTraversalCell(observer, out var origin);
                            if ((surface.Cell - origin).sqrMagnitude > 400 || !system.HasLineOfSight(floor, observer, target)) continue;
                            expected.Add(target);
                            expectedDiscovered.Add(target);
                        }
                    system.CollectPartyVisibleAndDiscovered(floor, observers, actual, discovered);
                    Assert.That(actual.SetEquals(expected), Is.True);
                    Assert.That(discovered.SetEquals(expectedDiscovered), Is.True);
                    using (var job = fogSystem.CreateUpdateJob(floor, observers, background: true))
                    {
                        var worker = (System.Threading.Tasks.Task<bool>)typeof(DungeonFogUpdateJob)
                            .GetField("worker", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(job);
                        Assert.That(worker.Wait(TimeSpan.FromSeconds(10)), Is.True);
                        job.AdvanceSlice();
                        Assert.That(job.Applied, Is.True);
                        Assert.That(floor.Fog.Visible, Is.EquivalentTo(expected));
                        remembered.UnionWith(expectedDiscovered);
                        // The obstacle at 7 and subsequently added wall 9–10 are
                        // static knowledge; unrelated distant floor is not.
                        foreach (var position in new[] { At(7), At(9), At(10) })
                            if (expected.Contains(position) && (position.Equals(At(7)) || map.SurfaceWallEdges.Any())) remembered.Add(position);
                        Assert.That(floor.Fog.Discovered, Is.EquivalentTo(remembered));
                    }
                    system.CollectPartyVisible(floor, observers, 20, actual);
                    Assert.That(actual.SetEquals(expectedDiscovered), Is.True);
                }
                Check();
                floor.TrySetDoorOpen("door", true); Check();
                obstacle.TryDeactivate(); Check();
                map.TryAddWall(new Vector2Int(9, 0), new Vector2Int(10, 0)); Check();
                system.CollectPartyVisibleAndDiscovered(floor, Array.Empty<DungeonTraversalPosition>(), actual, discovered);
                Assert.That(actual, Is.Empty); Assert.That(discovered, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(doorDefinition);
                UnityEngine.Object.DestroyImmediate(obstacleDefinition);
            }
        }

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
        public void ToggleFogShowsCurrentObjectStateWithoutRevealingItsKnowledge()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room");
            floor.TrySetMap(map);
            var definition = ScriptableObject.CreateInstance<SpikeTrapDungeonObjectDefinition>();
            var trap = new SpikeTrapDungeonObjectInstance("trap", definition, Vector2Int.zero);
            var mapObject = new GameObject("map view");
            var trapObject = new GameObject("trap view");
            try
            {
                var mapView = mapObject.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetProperty("FogFloor").SetValue(mapView, floor);
                var renderer = trapObject.AddComponent<SpriteRenderer>();
                var trapView = trapObject.AddComponent<DungeonSpikeTrapView>();
                trapView.Initialize(null, floor, trap, null, Vector3.zero);
                trapView.BindElevationView(mapView);
                var presentation = typeof(DungeonObjectView).GetMethod("PresentationState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var refresh = typeof(DungeonObjectView).GetMethod("LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That((bool)presentation.Invoke(trapView, new object[] { true }), Is.False);
                refresh.Invoke(trapView, null);
                Assert.That(renderer.forceRenderingOff, Is.True);
                floor.Fog.ToggleDebugFog();
                Assert.That(mapView.IsObjectCurrentlySeen(trap), Is.True);
                Assert.That((bool)presentation.Invoke(trapView, new object[] { true }), Is.True);
                refresh.Invoke(trapView, null);
                Assert.That(renderer.forceRenderingOff, Is.False);
                Assert.That(floor.Fog.Objects, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(trapObject);
                UnityEngine.Object.DestroyImmediate(mapObject);
                UnityEngine.Object.DestroyImmediate(definition);
            }
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
        public void NearbyObserverBehindWallCannotRevealTerrainBeyondOtherObserversRange()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var x = 0; x < 31; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            map.TryAddWall(Vector2Int.zero, Vector2Int.right);
            floor.TrySetMap(map);
            var fog = new DungeonFogSystem();
            fog.Update(floor, new[] { At(0), At(30) });
            Assert.That(floor.Fog.Get(At(4)), Is.EqualTo(DungeonKnowledge.Unexplored));
            fog.Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.Get(At(4)), Is.EqualTo(DungeonKnowledge.Unexplored));
        }

        [Test]
        public void SeenTerrainWithinTwentyCellsIsRememberedAndBeyondBoundaryStaysUnknown()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();
            for (var x = 0; x < 25; x++) map.TryAddFloorCell(new Vector2Int(x, 0), "room");
            floor.TrySetMap(map);
            var fog = new DungeonFogSystem();
            fog.Update(floor, new[] { At(0) });
            Assert.That(floor.Fog.Get(At(20)), Is.EqualTo(DungeonKnowledge.Visible));
            Assert.That(floor.Fog.Get(At(21)), Is.EqualTo(DungeonKnowledge.Unexplored));
            fog.Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.Get(At(20)), Is.EqualTo(DungeonKnowledge.Remembered));
            Assert.That(floor.Fog.Get(At(21)), Is.EqualTo(DungeonKnowledge.Unexplored));
            fog.Update(floor, new[] { At(1) });
            fog.Update(floor, Array.Empty<DungeonTraversalPosition>());
            Assert.That(floor.Fog.Get(At(21)), Is.EqualTo(DungeonKnowledge.Remembered));
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
            Assert.That(floor.Fog.Visible.Count, Is.EqualTo(333),"Euclidean radius 20 clips the far corner of this 20x20 room.");
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
