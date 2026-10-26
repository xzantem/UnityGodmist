using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class PatrolMaterialOptimizationTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Read<T>(object obj, string name) => (T)obj.GetType().GetField(name, Flags).GetValue(obj);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Flags).SetValue(obj, value);
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/patrol-material-pass-20261017"));
        delegate bool SelectFacing(ExplorationCharacterView actor, bool blockedOnly, out DungeonTraversalPosition result);

        [Test]
        public void BufferedNeighboursMatchOrderedIteratorAcrossStairsDoorsWallsAndGates()
        {
            using var fixture = new PatrolFixture(); var map = fixture.Floor.Map;
            var upper = new DungeonSurfacePosition(new Vector2Int(2, 0), 1);
            map.TryAddFloorCell(upper, "upper");
            Assert.That(map.TryAddStairs(new DungeonSurfacePosition(Vector2Int.zero), upper,
                new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) }), Is.True);
            var positions = map.Surfaces.Select(s => new DungeonTraversalPosition(s))
                .Concat(map.Stairs.Select(s => new DungeonTraversalPosition(s, 1)))
                .Append(new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(100, 100)))).ToArray();
            var buffer = new List<DungeonTraversalPosition>(32);
            Func<DungeonSurfaceEdge, bool> doorPolicy = _ => false;
            void Compare()
            {
                foreach (var p in positions)
                {
                    map.CollectTraversalNeighbours(p, buffer, doorPolicy);
                    CollectionAssert.AreEqual(map.GetTraversalNeighbours(p, doorPolicy).ToArray(), buffer);
                }
            }
            Compare(); map.TryAddWall(Vector2Int.zero, Vector2Int.up); Compare();
            var door = new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(1, 2));
            map.TrySetDoorState(door, false); Compare(); doorPolicy = _ => true; Compare(); map.TrySetDoorState(door, true); Compare();
            var gate = new DungeonSurfaceEdge(new DungeonSurfacePosition(new Vector2Int(2, 1)), new DungeonSurfacePosition(new Vector2Int(2, 2)));
            map.TrySetProgressionGateState(gate, false); Compare(); map.TrySetProgressionGateState(gate, true); Compare();
            Assert.That(CountAllocations(() => GC.KeepAlive(new byte[4096])), Is.GreaterThan(0));
            long allocations = CountAllocations(() => {
                for (int i = 0; i < 1000; i++) foreach (var p in positions) map.CollectTraversalNeighbours(p, buffer, doorPolicy);
            });
            Assert.That(allocations, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void FacingPreservesStableRankingTrapAvoidanceAndBlockedOnlyRule(bool blockedOnly)
        {
            using var fixture = new PatrolFixture();
            var map = fixture.Floor.Map;
            void Compare()
            {
                using var batch = fixture.Party.BeginTraversalQueryBatch();
                var expected = fixture.Legacy(blockedOnly);
                var found = fixture.Select(fixture.Actor, blockedOnly, out var actual);
                Assert.That(found, Is.EqualTo(expected.HasValue));
                if (found) Assert.That(actual, Is.EqualTo(expected.Value));
            }
            Compare();
            Read<HashSet<Vector2Int>>(fixture.Owner, "patrolTrapCells").Add(new Vector2Int(2, 1)); Compare();
            map.TryAddWall(new Vector2Int(1, 1), new Vector2Int(1, 2)); Compare();
            var gate = new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(0, 1));
            map.TrySetProgressionGateState(gate, false); Compare(); map.TrySetProgressionGateState(gate, true); Compare();
            fixture.Actor.transform.position = new Vector3(.5f, .5f); Compare();
            foreach (var next in map.GetTraversalNeighbours(fixture.Actor.DungeonPosition).ToArray())
                map.TryAddWall(fixture.Actor.DungeonPosition.Surface, next.Surface);
            Compare();
        }

        [Test]
        public void WarmFacingSelectionAllocatesNothingAndMeasuresLegacyAlgorithm()
        {
            using var fixture = new PatrolFixture(); using var batch = fixture.Party.BeginTraversalQueryBatch();
            for (int i = 0; i < 20; i++) { fixture.Select(fixture.Actor, false, out _); fixture.Legacy(false); }
            long controlAllocations = CountAllocations(() => GC.KeepAlive(new byte[4096]));
            Assert.That(controlAllocations, Is.GreaterThan(0), "The recorder must detect a known allocation before any zero assertion.");
            const int iterations = 256;
            var watch = new Stopwatch();
            long bufferedAllocations = CountAllocations(() => { for (int i = 0; i < iterations; i++) fixture.Select(fixture.Actor, false, out _); });
            long legacyAllocations = CountAllocations(() => { for (int i = 0; i < iterations; i++) fixture.Legacy(false); });
            watch.Start();
            for (int i = 0; i < iterations; i++) fixture.Select(fixture.Actor, false, out _);
            watch.Stop(); double bufferedMs = watch.Elapsed.TotalMilliseconds / iterations;
            watch.Reset(); watch.Start();
            for (int i = 0; i < iterations; i++) fixture.Legacy(false);
            watch.Stop();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "facing-comparison.json"), JsonUtility.ToJson(new FacingMetric {
                iterations = iterations, bufferedAllocations = bufferedAllocations, legacyAllocations = legacyAllocations, controlAllocations = controlAllocations,
                bufferedMs = bufferedMs, legacyMs = watch.Elapsed.TotalMilliseconds / iterations }, true));
            Assert.That(bufferedAllocations, Is.Zero, "Selection and its batched passability queries must not allocate after warmup.");
            Assert.That(legacyAllocations, Is.GreaterThan(0));
        }

        [TestCase(false)] [TestCase(true)]
        public void UnchangedReceiverDoesNotUploadAndExternalBlockOrSpriteChangesStayLive(bool underlay)
        {
            var root = new GameObject("Receiver update equivalence");
            var material = new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
            var otherTexture = new Texture2D(2, 2); var otherSprite = Sprite.Create(otherTexture, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                var rig = root.AddComponent<DungeonSilhouetteLighting>(); Set(rig, "floorMaterial", material);
                var renderer = root.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                var block = new MaterialPropertyBlock(); block.SetFloat("_TestExternalValue", 17);
                if (underlay) block.SetVector("_ForestPatchBounds", new Vector4(2, 3, 4, 5));
                renderer.SetPropertyBlock(block); material.SetFloat("_ForestSurfaceEnabled", 1);
                var apply = (Action<SpriteRenderer, bool, bool>)Delegate.CreateDelegate(typeof(Action<SpriteRenderer, bool, bool>), rig,
                    typeof(DungeonSilhouetteLighting).GetMethod("ApplyGroundLighting", Flags));
                apply(renderer, false, false); int uploads = rig.ReceiverPropertyBlockUploadCount;
                for (int i = 0; i < 50; i++) apply(renderer, false, false);
                Assert.That(rig.ReceiverPropertyBlockUploadCount, Is.EqualTo(uploads));
                renderer.GetPropertyBlock(block);
                Assert.That(block.HasFloat("_ForestSurfaceEnabled"), Is.True, "A zero override must be explicit when the shared material enables surface blending.");
                Assert.That(block.GetFloat("_ForestSurfaceEnabled"), Is.EqualTo(underlay ? 1 : 0));
                Assert.That(block.GetFloat("_TestExternalValue"), Is.EqualTo(17));
                block.Clear(); block.SetFloat("_TestExternalValue", 29); renderer.SetPropertyBlock(block);
                apply(renderer, false, false); Assert.That(rig.ReceiverPropertyBlockUploadCount, Is.EqualTo(uploads + 1));
                renderer.GetPropertyBlock(block); Assert.That(block.GetFloat("_TestExternalValue"), Is.EqualTo(29));
                Assert.That(block.GetFloat("_ForestSurfaceEnabled"), Is.Zero);
                renderer.sprite = otherSprite; apply(renderer, false, false);
                renderer.GetPropertyBlock(block); Assert.That(block.GetTexture("_MainTex"), Is.EqualTo(otherTexture));
                Assert.That(rig.ReceiverPropertyBlockUploadCount, Is.EqualTo(uploads + 2));
            }
            finally { Object.DestroyImmediate(root); if (material != null) Object.DestroyImmediate(material); Object.DestroyImmediate(sprite); Object.DestroyImmediate(otherSprite); Object.DestroyImmediate(otherTexture); }
        }

        [Test]
        public void MeasureWarmGroundReceiverUploadsAgainstUnconditionalReference()
        {
            var root = new GameObject("Receiver upload benchmark");
            var material = new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                var rig = root.AddComponent<DungeonSilhouetteLighting>(); Set(rig, "floorMaterial", material);
                var apply = (Action<SpriteRenderer, bool, bool>)Delegate.CreateDelegate(typeof(Action<SpriteRenderer, bool, bool>), rig,
                    typeof(DungeonSilhouetteLighting).GetMethod("ApplyGroundLighting", Flags));
                var renderers = new SpriteRenderer[256];
                for (int i = 0; i < renderers.Length; i++)
                {
                    var prop = new GameObject("Ground prop", typeof(SpriteRenderer)); prop.transform.SetParent(root.transform);
                    renderers[i] = prop.GetComponent<SpriteRenderer>(); renderers[i].sprite = sprite; apply(renderers[i], false, false);
                }
                var legacyBlocks = new Dictionary<SpriteRenderer, MaterialPropertyBlock>();
                foreach (var renderer in renderers) legacyBlocks.Add(renderer, new MaterialPropertyBlock());
                var registrations = Read<Dictionary<SpriteRenderer, (Material material, int layer, int order, bool groundOrder)>>(rig, "presentationMaterials");
                void Legacy()
                {
                    foreach (var renderer in renderers)
                    {
                        if (!renderer.enabled || renderer.forceRenderingOff) continue;
                        if (!registrations.ContainsKey(renderer)) throw new InvalidOperationException("Reference requires warmed receivers.");
                        if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
                        var legacyBlock = legacyBlocks[renderer];
                        renderer.GetPropertyBlock(legacyBlock);
                        legacyBlock.SetTexture("_MainTex", renderer.sprite.texture);
                        legacyBlock.SetFloat("_ForestSurfaceEnabled", legacyBlock.GetVector("_ForestPatchBounds").z > 0 ? 1 : 0);
                        var readabilitySprite = renderer.sprite;
                        legacyBlock.SetFloat("_ForestPropReadability", 0);
                        renderer.SetPropertyBlock(legacyBlock);
                    }
                }
                void Optimized() { foreach (var renderer in renderers) apply(renderer, false, false); }
                for (int i = 0; i < 4; i++) { Legacy(); Optimized(); }
                int uploads = rig.ReceiverPropertyBlockUploadCount;
                var oldTimes = new double[15]; var newTimes = new double[15]; var watch = new Stopwatch();
                for (int i = 0; i < 15; i++)
                {
                    watch.Restart(); Legacy(); watch.Stop(); oldTimes[i] = watch.Elapsed.TotalMilliseconds;
                    watch.Restart(); Optimized(); watch.Stop(); newTimes[i] = watch.Elapsed.TotalMilliseconds;
                }
                Array.Sort(oldTimes); Array.Sort(newTimes);
                Directory.CreateDirectory(Output);
                File.WriteAllText(Path.Combine(Output, "receiver-comparison.json"), JsonUtility.ToJson(new ReceiverMetric {
                    receivers = renderers.Length, legacyMedianMs = oldTimes[7], optimizedMedianMs = newTimes[7],
                    legacyUploads = renderers.Length * 15, optimizedUploads = rig.ReceiverPropertyBlockUploadCount - uploads }, true));
                Assert.That(rig.ReceiverPropertyBlockUploadCount, Is.EqualTo(uploads));
            }
            finally { Object.DestroyImmediate(root); if (material != null) Object.DestroyImmediate(material); Object.DestroyImmediate(sprite); }
        }

        sealed class PatrolFixture : IDisposable
        {
            public readonly GameObject Root = new("Patrol selection fixture", typeof(Grid));
            public readonly DungeonFloorInstance Floor = new(0);
            public readonly ExplorationPartyController Party;
            public readonly ExplorationCharacterView Actor;
            public readonly DungeonCombatCoordinator Owner;
            public readonly SelectFacing Select;
            readonly DungeonFloorMapView view;
            public PatrolFixture()
            {
                var tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(Root.transform);
                view = Root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tiles.GetComponent<Tilemap>());
                var map = new DungeonFloorMap(); Floor.TrySetMap(map);
                for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                Party = Root.AddComponent<ExplorationPartyController>(); Party.ConfigureTraversal(Floor, view, null);
                var actorRoot = new GameObject("Enemy"); actorRoot.transform.SetParent(Root.transform);
                Actor = actorRoot.AddComponent<ExplorationCharacterView>(); Actor.transform.position = new Vector3(1.5f, 1.5f); Actor.SetEnemyInstanceId("test");
                Owner = Root.AddComponent<DungeonCombatCoordinator>(); Set(Owner, "floor", Floor); Set(Owner, "party", Party); Set(Owner, "map", view);
                Select = (SelectFacing)Delegate.CreateDelegate(typeof(SelectFacing), Owner, typeof(DungeonCombatCoordinator).GetMethod("TrySelectPatrolFacing", Flags));
            }
            public DungeonTraversalPosition? Legacy(bool blockedOnly)
            {
                var traps = Read<HashSet<Vector2Int>>(Owner, "patrolTrapCells");
                bool Allowed(DungeonTraversalPosition p) => Floor.Map.TryGetTraversalCell(p, out var cell) && !traps.Contains(cell) && Party.CanEnterTraversal(Actor, p);
                var ranked = Floor.Map.GetTraversalNeighbours(Actor.DungeonPosition, Party.CanOpenTraversalDoor).Where(Allowed)
                    .OrderByDescending(p => Floor.Map.GetTraversalNeighbours(p, Party.CanOpenTraversalDoor).Count(Allowed)).ToArray();
                if (ranked.Length == 0) return null;
                if (blockedOnly && ranked.Any(p => {
                    Floor.Map.TryGetTraversalCell(p, out var cell);
                    var world = (Vector2)view.CellToWorldCenter(cell) - Actor.MovementColliderCenterOffset;
                    return Vector2.Dot((world - Actor.Position).normalized, Actor.FacingDirection.normalized) > .7f;
                })) return null;
                return ranked[0];
            }
            public void Dispose() { Object.DestroyImmediate(Root); }
        }
        static long CountAllocations(Action action)
        {
            using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "GC.Alloc", 1,
                Unity.Profiling.ProfilerRecorderOptions.SumAllSamplesInFrame | Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if (!recorder.Valid) throw new InvalidOperationException("GC.Alloc recorder unavailable.");
            action(); recorder.Stop();
            return recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
        }
        [Serializable] sealed class FacingMetric { public int iterations; public long bufferedAllocations, legacyAllocations, controlAllocations; public double bufferedMs, legacyMs; }
        [Serializable] sealed class ReceiverMetric { public int receivers, legacyUploads, optimizedUploads; public double legacyMedianMs, optimizedMedianMs; }
    }
}
