using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Game;
using Godmist.Core.Enums;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class LeaderClickMovementOptimizationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private sealed class FloorFixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly ObstacleDungeonObjectDefinition Definition;
            public readonly DungeonFloorInstance Floor;
            public readonly ExplorationPartyController Party;
            public readonly ExplorationCharacterView Actor;
            public readonly Vector2 Start = new(1.5f, 1.3f);
            public readonly Vector2 Goal = new(18.5f, 1.3f);
            public readonly DungeonTraversalPosition Target = new(new DungeonSurfacePosition(new Vector2Int(18, 1)));
            public readonly Func<ExplorationCharacterView, Queue<DungeonTraversalPosition>, Vector2?, float, bool> Advance;
            public readonly Func<ExplorationCharacterView, DungeonTraversalPosition, Vector2, DungeonTraversalPosition, Vector2, bool> Validate;

            public Func<Vector3, bool> BindLeader()
            {
                var character = new CharacterInstance { CharacterId = "click-test-leader", CurrentHealth = 100,
                    LifeState = CharacterLifeState.Alive, AvailabilityState = CharacterAvailabilityState.Available };
                var session = new GameSession(GameModeState.Exploration);
                Assert.That(session.TryRegisterCharacter(character), Is.True);
                Assert.That(session.TryAddCharacterToActiveParty(character.CharacterId), Is.True);
                Actor.Bind(character);
                Party.Initialize(session, new[] { Actor });
                Party.ConfigureTraversal(Floor, Actor.TerrainView, null);
                return (Func<Vector3, bool>)typeof(ExplorationPartyController).GetMethod("TrySetTraversalDestination", Private)
                    .CreateDelegate(typeof(Func<Vector3, bool>), Party);
            }

            public FloorFixture(int obstacles)
            {
                Root = new GameObject("Leader route floor", typeof(Grid));
                Definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
                var tiles = new GameObject("tiles", typeof(Tilemap)); tiles.transform.SetParent(Root.transform);
                var view = Root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private).SetValue(view, tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap();
                for (var y = 0; y < 30; y++)
                    for (var x = 0; x < 60; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                Floor = new DungeonFloorInstance(0); Floor.TrySetMap(map);
                for (var i = 0; i < obstacles; i++)
                    Assert.That(Floor.TryAddObject(new ObstacleDungeonObjectInstance("far-" + i,
                        Definition, new Vector2Int(25 + i % 35, i / 35))), Is.True);
                var actorObject = new GameObject("leader", typeof(CircleCollider2D)); actorObject.transform.SetParent(Root.transform);
                var circle = actorObject.GetComponent<CircleCollider2D>(); circle.radius = .196f; circle.offset = new Vector2(0, .2f);
                Actor = actorObject.AddComponent<ExplorationCharacterView>();
                typeof(ExplorationCharacterView).GetField("movementCollider", Private).SetValue(Actor, circle);
                Actor.ConfigureTerrain(map, view, Floor); Actor.SetPosition(Start);
                Party = Root.AddComponent<ExplorationPartyController>(); Party.ConfigureTraversal(Floor, view, null);
                Physics2D.SyncTransforms();
                Advance = (Func<ExplorationCharacterView, Queue<DungeonTraversalPosition>, Vector2?, float, bool>)
                    typeof(ExplorationPartyController).GetMethod("AdvanceRoute", Private).CreateDelegate(
                        typeof(Func<ExplorationCharacterView, Queue<DungeonTraversalPosition>, Vector2?, float, bool>), Party);
                Validate = (Func<ExplorationCharacterView, DungeonTraversalPosition, Vector2, DungeonTraversalPosition, Vector2, bool>)
                    typeof(ExplorationPartyController).GetMethod("CanNavigateDirectly", Private, null,
                        new[] { typeof(ExplorationCharacterView), typeof(DungeonTraversalPosition), typeof(Vector2),
                            typeof(DungeonTraversalPosition), typeof(Vector2) }, null).CreateDelegate(
                        typeof(Func<ExplorationCharacterView, DungeonTraversalPosition, Vector2, DungeonTraversalPosition, Vector2, bool>), Party);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(Definition);
                Physics2D.SyncTransforms();
            }
        }

        [Test]
        public void NewClickRejectsBlockedEndpointAndSeesDeactivationWithoutLeakingScope()
        {
            using var floor = new FloorFixture(100);
            var click = floor.BindLeader();
            Assert.That(click(floor.Goal), Is.True);
            var blocker = new ObstacleDungeonObjectInstance("blocked-goal", floor.Definition, new Vector2Int(18, 1));
            Assert.That(floor.Floor.TryAddObject(blocker), Is.True);
            var originalDestination = typeof(ExplorationPartyController).GetField("movementDestination", Private).GetValue(floor.Party);
            Assert.That(click(floor.Goal), Is.False);
            Assert.That(typeof(ExplorationPartyController).GetField("movementDestination", Private).GetValue(floor.Party), Is.EqualTo(originalDestination));
            Assert.That(typeof(ExplorationPartyController).GetField("followerNavigationRevisionBatch", Private).GetValue(floor.Party), Is.False);
            blocker.TryDeactivate();
            Assert.That(click(floor.Goal), Is.True);
        }

        [Test]
        public void WholeClickPreservesNativeCornersAndMeasuresPlanningAndSmoothingCost()
        {
            using var floor = new FloorFixture(200);
            var wall = new GameObject("physical click divider", typeof(BoxCollider2D)); wall.transform.SetParent(floor.Root.transform);
            wall.transform.position = new Vector2(10.5f, 1f); wall.GetComponent<BoxCollider2D>().size = new Vector2(.25f, 2f);
            Physics2D.SyncTransforms();
            var click = floor.BindLeader();
            var plan = typeof(ExplorationPartyController).GetMethod("FindTraversalPathWithTargetWorld", Private);
            var smooth = typeof(ExplorationPartyController).GetMethod("SmoothTraversalPath", Private);
            var queue = (Queue<DungeonTraversalPosition>)typeof(ExplorationPartyController).GetField("traversalPath", Private).GetValue(floor.Party);
            DungeonTraversalPosition[] LivePlanAndSmooth()
            {
                var args = new object[] { floor.Actor, floor.Target, null, (Vector2?)floor.Goal };
                Assert.That(plan.Invoke(floor.Party, args), Is.True);
                var path = (List<DungeonTraversalPosition>)args[2];
                smooth.Invoke(floor.Party, new object[] { floor.Actor, floor.Target, floor.Goal, path, false });
                return path.Skip(1).ToArray();
            }
            var wasEnabled = ProfilerDriver.enabled; ProfilerDriver.enabled = false;
            try
            {
                var expected = LivePlanAndSmooth();
                Assert.That(expected.Length, Is.GreaterThan(1), "The divider must require corners rather than a direct line.");
                Assert.That(click(floor.Goal), Is.True);
                Assert.That(queue.ToArray(), Is.EqualTo(expected));
                var report = new PlanningReport { queries = 4, obstacles = 200 };
                for (var i = 0; i < report.queries; i++)
                {
                    void Live()
                    {
                        var started = Stopwatch.GetTimestamp();
                        var path = LivePlanAndSmooth();
                        report.livePlanningAndSmoothingMs += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                        Assert.That(path, Is.EqualTo(expected));
                    }
                    void Indexed()
                    {
                        var started = Stopwatch.GetTimestamp(); var result = click(floor.Goal);
                        report.indexedWholeClickMs += (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                        Assert.That(result, Is.True); Assert.That(queue.ToArray(), Is.EqualTo(expected));
                    }
                    if (i % 2 == 0) { Live(); Indexed(); } else { Indexed(); Live(); }
                }
                report.livePlanningAndSmoothingMs /= report.queries; report.indexedWholeClickMs /= report.queries;
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/new-target-planning-pass-20261017"));
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "planning-comparison.json"), JsonUtility.ToJson(report, true));
                TestContext.WriteLine(JsonUtility.ToJson(report));
            }
            finally { ProfilerDriver.enabled = wasEnabled; }
        }

        [Test]
        public void RouteStepSeesNewBlockerAndDeactivationBeforeNextMove()
        {
            using var floor = new FloorFixture(100);
            var route = new Queue<DungeonTraversalPosition>(new[] { floor.Target });
            Assert.That(floor.Advance(floor.Actor, route, floor.Goal, 0f), Is.True);
            Assert.That(floor.Actor.Position.x, Is.GreaterThan(floor.Start.x));
            var blocker = new ObstacleDungeonObjectInstance("new-blocker", floor.Definition, new Vector2Int(8, 1));
            Assert.That(floor.Floor.TryAddObject(blocker), Is.True);
            var before = floor.Actor.Position;
            Assert.That(floor.Advance(floor.Actor, route, floor.Goal, 0f), Is.False);
            Assert.That(floor.Actor.Position, Is.EqualTo(before));
            blocker.TryDeactivate();
            Assert.That(floor.Advance(floor.Actor, route, floor.Goal, 0f), Is.True);
            Assert.That(floor.Actor.Position.x, Is.GreaterThan(before.x));
        }

        [Test]
        public void IndexedValidationMatchesLiveFootprintAndMeasuresLargeFloorCost()
        {
            using var floor = new FloorFixture(1000);
            var wasEnabled = ProfilerDriver.enabled;
            ProfilerDriver.enabled = false;
            try
            {
                bool Check() => floor.Validate(floor.Actor, floor.Actor.DungeonPosition, floor.Start, floor.Target, floor.Goal);
                Assert.That(Check(), Is.True);
                using (floor.Party.BeginTraversalQueryBatch()) Assert.That(Check(), Is.True);
                var report = new ValidationReport { queries = 12, obstacles = 1000 };
                var sentinelBefore = GC.GetAllocatedBytesForCurrentThread();
                var sentinel = new byte[8192];
                report.allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() > sentinelBefore;
                GC.KeepAlive(sentinel);
                for (var i = 0; i < report.queries; i++)
                {
                    void Measure(bool indexed)
                    {
                        var bytes = GC.GetAllocatedBytesForCurrentThread();
                        var started = Stopwatch.GetTimestamp();
                        bool result;
                        if (indexed) { using var batch = floor.Party.BeginTraversalQueryBatch(); result = Check(); }
                        else result = Check();
                        var elapsed = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                        var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                        Assert.That(result, Is.True);
                        if (indexed) { report.indexedMs += elapsed; report.indexedBytes += allocated; }
                        else { report.liveMs += elapsed; report.liveBytes += allocated; }
                    }
                    // Alternate the order to limit systematic warm-up/load bias.
                    Measure(i % 2 == 0); Measure(i % 2 != 0);
                }
                report.liveMs /= report.queries; report.indexedMs /= report.queries;
                report.liveBytes /= report.queries; report.indexedBytes /= report.queries;
                if (report.allocationCounterAvailable)
                    Assert.That(report.indexedBytes, Is.LessThan(report.liveBytes / 5), "Remote floor props must not allocate iterators for every route sample.");
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/leader-click-movement-pass-20261017"));
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "validation-comparison.json"), JsonUtility.ToJson(report, true));
                TestContext.WriteLine(JsonUtility.ToJson(report));
            }
            finally { ProfilerDriver.enabled = wasEnabled; }
        }

        [Serializable] private sealed class ValidationReport
        {
            public int queries, obstacles;
            public bool allocationCounterAvailable;
            public double liveMs, indexedMs;
            public long liveBytes, indexedBytes;
        }
        [Serializable] private sealed class PlanningReport
        {
            public int queries, obstacles;
            public double livePlanningAndSmoothingMs, indexedWholeClickMs;
        }
    }
}
