using System;
using System.Collections.Generic;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Core.Enums;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class MovementSpikeOptimizationTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private delegate bool ValidateSegment(ExplorationCharacterView actor, DungeonTraversalPosition start,
            Vector2 from, DungeonTraversalPosition end, Vector2 to);

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root = new("Movement spike fixture", typeof(Grid));
            public readonly DungeonFloorInstance Floor = new(0);
            public readonly ObstacleDungeonObjectDefinition Obstacle = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            public readonly ExplorationPartyController Party;
            public readonly ExplorationCharacterView Actor;
            public readonly DungeonFloorMapView View;
            public readonly Func<int> Revision;
            public readonly ValidateSegment Validate;
            public readonly Func<DungeonTraversalPosition, Vector2, bool> Fits;

            public Fixture(bool layered = false)
            {
                var tiles = new GameObject("Tiles", typeof(Tilemap)); tiles.transform.SetParent(Root.transform);
                View = Root.AddComponent<DungeonFloorMapView>(); Set(View, "floorTilemap", tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap(); Floor.TrySetMap(map);
                for (int y = 0; y < 20; y++) for (int x = 0; x < 50; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                if (layered) map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(49, 19), 1), "upper");
                for (int i = 0; i < 200; i++)
                    Floor.TryAddObject(new ObstacleDungeonObjectInstance("far-" + i, Obstacle, new Vector2Int(30 + i % 20, 5 + i / 20)));
                var actorRoot = new GameObject("Follower", typeof(CircleCollider2D)); actorRoot.transform.SetParent(Root.transform);
                var collider = actorRoot.GetComponent<CircleCollider2D>(); collider.radius = .196f; collider.offset = Vector2.up * .2f;
                Actor = actorRoot.AddComponent<ExplorationCharacterView>(); Set(Actor, "movementCollider", collider);
                Actor.Bind(new CharacterInstance { CharacterId = "follower", CurrentHealth = 100, LifeState = CharacterLifeState.Alive });
                Actor.ConfigureTerrain(map, View, Floor); Actor.SetPosition(new Vector2(2.5f, 2.3f));
                Party = Root.AddComponent<ExplorationPartyController>(); Party.ConfigureTraversal(Floor, View, null);
                Actor.CanEnterTraversal = p => Party.CanEnterTraversal(Actor, p);
                Actor.CanEnterTraversalAtWorld = (p, world) => Party.CanEnterTraversalAtWorld(Actor, p, world);
                Revision = (Func<int>)typeof(ExplorationPartyController).GetMethod("CurrentTraversalNavigationRevision", Private).CreateDelegate(typeof(Func<int>), Party);
                Validate = (ValidateSegment)typeof(ExplorationPartyController).GetMethod("CanNavigateDirectly", Private, null,
                    new[] { typeof(ExplorationCharacterView), typeof(DungeonTraversalPosition), typeof(Vector2), typeof(DungeonTraversalPosition), typeof(Vector2) }, null)
                    .CreateDelegate(typeof(ValidateSegment), Party);
                Fits = (Func<DungeonTraversalPosition, Vector2, bool>)typeof(ExplorationCharacterView).GetMethod("FitsTraversalCollider", Private)
                    .CreateDelegate(typeof(Func<DungeonTraversalPosition, Vector2, bool>), Actor);
                Physics2D.SyncTransforms();
            }
            public DungeonTraversalPosition Position(Vector2 world)
                => new(new DungeonSurfacePosition(View.WorldToCell(world)));
            public void Dispose()
            {
                Object.DestroyImmediate(Root); Object.DestroyImmediate(Obstacle); Physics2D.SyncTransforms();
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void WarmShortAndLongFootprintValidationAvoidsManagedAllocations(bool longSegment)
        {
            using var fixture = new Fixture();
            var from = fixture.Actor.Position;
            var to = from + Vector2.right * (longSegment ? 20 : .1f);
            var start = fixture.Position(from); var end = fixture.Position(to);
            using (fixture.Party.BeginTraversalQueryBatch())
            {
                Assert.That(fixture.Validate(fixture.Actor, start, from, end, to), Is.True);
                bool valid = true;
                Action run = () => { for (int i = 0; i < 128; i++) valid &= fixture.Validate(fixture.Actor, start, from, end, to); };
                run();
                Assert.That(CountAllocations(() => GC.KeepAlive(new byte[4096])), Is.GreaterThan(0), "Allocation counter requires a positive control.");
                Assert.That(CountAllocations(run), Is.Zero);
                Assert.That(valid, Is.True);
            }

            // Live obstacle state must still be observed in the next query batch.
            var blocker = new ObstacleDungeonObjectInstance("new-blocker", fixture.Obstacle, fixture.View.WorldToCell(to));
            fixture.Floor.TryAddObject(blocker);
            using var changedBatch = fixture.Party.BeginTraversalQueryBatch();
            Assert.That(fixture.Validate(fixture.Actor, start, from, end, to), Is.False);
            blocker.TryDeactivate();
            Assert.That(fixture.Validate(fixture.Actor, start, from, end, to), Is.True);
        }

        [Test]
        public void LayeredColliderKeepsWallAndGateChecksWithoutNeighbourIteratorAllocations()
        {
            using var fixture = new Fixture(true);
            var current = fixture.Actor.DungeonPosition; var target = fixture.Actor.Position + Vector2.right * .1f;
            using var batch = fixture.Party.BeginTraversalQueryBatch();
            Assert.That(fixture.Fits(current, target), Is.True);
            bool valid = true;
            Action run = () => { for (int i = 0; i < 128; i++) valid &= fixture.Fits(current, target); };
            run();
            Assert.That(CountAllocations(run), Is.Zero);
            Assert.That(valid, Is.True);
            var adjacent = new Vector2Int(3, 2);
            var edge = new DungeonSurfaceEdge(new DungeonSurfacePosition(new Vector2Int(2, 2)), new DungeonSurfacePosition(adjacent));
            var nearEdge = new Vector2(2.95f, 2.3f);
            Assert.That(fixture.Fits(current, nearEdge), Is.True);
            fixture.Floor.Map.TrySetProgressionGateState(edge, false);
            Assert.That(fixture.Fits(current, nearEdge), Is.False);
            fixture.Floor.Map.TrySetProgressionGateState(edge, true);
            Assert.That(fixture.Fits(current, nearEdge), Is.True);
            fixture.Floor.Map.TryAddWall(new Vector2Int(2, 2), adjacent);
            Assert.That(fixture.Fits(current, nearEdge), Is.False);
        }

        [Test]
        public void NavigationRevisionScansOncePerUnchangedBatchAndSeesTopologyChangesImmediately()
        {
            using var fixture = new Fixture();
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Godmist.Navigation.Revision", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            Assert.That(recorder.Valid, Is.True);
            int initial;
            using (fixture.Party.BeginTraversalQueryBatch())
            {
                initial = fixture.Revision();
                for (int i = 0; i < 128; i++) fixture.Revision();
                using (fixture.Party.BeginTraversalQueryBatch()) fixture.Revision();
            }
            recorder.Stop();
            Assert.That(recorder.GetSample(0).Count, Is.EqualTo(1));
            Assert.That(Read<bool>(fixture.Party, "followerNavigationRevisionBatch"), Is.False);
            using (fixture.Party.BeginTraversalQueryBatch())
            {
                var previous = fixture.Revision();
                fixture.Floor.Map.TryAddWall(new Vector2Int(2, 2), new Vector2Int(3, 2));
                Assert.That(fixture.Revision(), Is.Not.EqualTo(previous));
                Assert.That(fixture.Revision(), Is.Not.EqualTo(initial));
            }
            var obstacle = (ObstacleDungeonObjectInstance)fixture.Floor.Objects[0];
            var before = fixture.Revision(); obstacle.TryDeactivate();
            Assert.That(fixture.Revision(), Is.Not.EqualTo(before), "Outside a batch object state is always read fresh.");
        }

        [Test]
        public void FollowerTickRestoresAnEnclosingBatchInsteadOfDisablingItsObstacleIndex()
        {
            using var fixture = new Fixture();
            var session = new GameSession(GameModeState.Exploration);
            var leader = fixture.Actor.RuntimeCharacter;
            Assert.That(session.TryRegisterCharacter(leader), Is.True);
            Assert.That(session.TryAddCharacterToActiveParty(leader.CharacterId), Is.True);
            fixture.Party.Initialize(session, new[] { fixture.Actor });
            using (fixture.Party.BeginTraversalQueryBatch())
            {
                var revision = fixture.Revision();
                typeof(ExplorationPartyController).GetMethod("MoveFollowers", Private).Invoke(fixture.Party, new object[] { fixture.Actor });
                Assert.That(Read<bool>(fixture.Party, "followerNavigationRevisionBatch"), Is.True);
                Assert.That(fixture.Revision(), Is.EqualTo(revision));
            }
            Assert.That(Read<bool>(fixture.Party, "followerNavigationRevisionBatch"), Is.False);
        }

        private static long CountAllocations(Action action)
        {
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if (!recorder.Valid) throw new InvalidOperationException("GC.Alloc counter unavailable.");
            action(); recorder.Stop();
            return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
        }
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
        private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    }
}
