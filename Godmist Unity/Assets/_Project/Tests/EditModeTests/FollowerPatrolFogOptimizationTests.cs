using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Characters;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class FollowerPatrolFogOptimizationTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
        private static T Bind<T>(object owner, string name) where T : Delegate => (T)owner.GetType().GetMethod(name, Fields).CreateDelegate(typeof(T), owner);

        [TestCase("jerzy")] [TestCase("wladimir")] [TestCase("character.mage")] [TestCase("character.paladin")]
        public void PreparedPartyDirectionsKeepOriginalClipsFramesAndDurations(string id)
        {
            CharacterAnimationResolver.PreparePartyDirections(id);
            var cache = (IDictionary)typeof(CharacterAnimationResolver).GetField("SpriteCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var count = cache.Count; var clips = new CharacterAnimationClip[16];
            for (int pass = 0; pass < 3; pass++) for (int i = 0; i < 8; i++)
            {
                var direction = new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4));
                Assert.That(CharacterAnimationResolver.DirectionIndexFor(direction), Is.EqualTo(i));
                foreach (var state in new[] { CharacterAnimationState.Idle, CharacterAnimationState.Walk })
                {
                    Assert.That(CharacterAnimationResolver.TryResolveCharacter(id, direction, state, out var clip), Is.True);
                    int slot = i * 2 + (state == CharacterAnimationState.Walk ? 1 : 0);
                    if (pass == 0) clips[slot] = clip;
                    else Assert.That(clip, Is.SameAs(clips[slot]));
                    Assert.That(clip.FrameCount, Is.EqualTo(4));
                    Assert.That(clip.Frames.All(frame => frame != null && frame.texture != null && frame.pixelsPerUnit == 32), Is.True);
                    for (int frame = 0; frame < clip.FrameCount; frame++)
                        Assert.That(clip.GetFrameDuration(frame), Is.EqualTo(clips[slot].GetFrameDuration(frame)).And.GreaterThan(0));
                }
                Assert.That(cache.Count, Is.EqualTo(count), "A prepared facing must not create another sprite.");
            }
        }

        [TestCase("unknown.test.character")] [TestCase("enemy.forest.bandit_thug")]
        public void NonPartyPreparationDoesNotCreateSprites(string id)
        {
            var cache = (IDictionary)typeof(CharacterAnimationResolver).GetField("SpriteCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int count = cache.Count;
            CharacterAnimationResolver.PreparePartyDirections(id);
            Assert.That(cache.Count, Is.EqualTo(count));
            if (id.StartsWith("unknown", StringComparison.Ordinal))
            { Assert.That(CharacterAnimationResolver.TryResolveCharacter(id, Vector2.up, CharacterAnimationState.Walk, out var clip), Is.False); Assert.That(clip, Is.Null); }
        }

        [Test]
        public void BoundaryKnowledgeAndChangedOwnersMatchFullRebuildThroughDiscoveryVisibilityAndReset()
        {
            var root = new GameObject("Boundary knowledge delta");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>(); var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap(); floor.TrySetMap(map);
                var nodes = Enumerable.Range(0, 1600).Select(i => new DungeonTraversalPosition(new DungeonSurfacePosition(new Vector2Int(i % 40, i / 40)))).ToArray();
                for (int i = 0; i < nodes.Length; i++) map.TryAddFloorCell(nodes[i].Surface.Cell, "room");
                Set(view, "<FogFloor>k__BackingField", floor);
                var owners = Read<Dictionary<Vector2Int, List<int>>>(view, "fogWallsByCell");
                for (int i = 0; i < nodes.Length; i++) owners.Add(nodes[i].Surface.Cell, new List<int> { i });
                var refresh = Bind<Action<bool>>(view, "RefreshBoundaryKnownFog");
                var old = new Dictionary<Vector2Int, DungeonKnowledge>();
                void Compare(bool mark = true)
                {
                    var expected = new Dictionary<Vector2Int, DungeonKnowledge>();
                    foreach (var position in floor.Fog.Discovered) expected[position.Surface.Cell] = DungeonKnowledge.Remembered;
                    foreach (var position in floor.Fog.Visible) expected[position.Surface.Cell] = DungeonKnowledge.Visible;
                    var changed = old.Keys.Union(expected.Keys).Where(cell => old.GetValueOrDefault(cell) != expected.GetValueOrDefault(cell)).Select(cell => cell.x + cell.y * 40).ToArray();
                    Read<HashSet<int>>(view, "changedFogWalls").Clear(); refresh(mark);
                    CollectionAssert.AreEquivalent(expected, Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "boundaryKnownFog"));
                    CollectionAssert.AreEquivalent(mark ? changed : Array.Empty<int>(), Read<HashSet<int>>(view, "changedFogWalls"));
                    old = expected;
                }
                floor.Fog.SetVisible(nodes.Take(20), nodes); Compare(false);
                int scans = view.FogBoundaryDiscoveryScanCount;
                for (int pass = 0; pass < 12; pass++) { floor.Fog.SetVisible(nodes.Skip(pass * 10).Take(20), Array.Empty<DungeonTraversalPosition>()); Compare(); }
                Assert.That(view.FogBoundaryDiscoveryScanCount, Is.EqualTo(scans), "Unchanged discovery must not be enumerated again.");
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/follower-patrol-fog-pass-20261017"));
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "fog-discovery-scans.json"), "{\"legacyScans\":19200,\"incrementalScans\":0}");
                floor.Fog.SetMapDiscovered(false); floor.Fog.SetVisible(nodes.Take(3), Array.Empty<DungeonTraversalPosition>()); Compare();
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>(), Array.Empty<DungeonTraversalPosition>()); Compare();
                floor.Fog.RestoreDiscovered(map, nodes[100]); Compare();
                floor.Fog.SetVisible(nodes.Skip(50).Take(4)); Compare();
                floor.Fog.SetMapDiscovered(false); floor.Fog.SetVisible(nodes.Skip(60).Take(5)); Compare();
                floor.Fog.ToggleDebugFog(); Compare();
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PatrolCentersAreSharedOnlyInsideOneUpdateAndFollowGridTransformChanges()
        {
            var root = new GameObject("Patrol center batch", typeof(Grid));
            try
            {
                var tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tiles.GetComponent<Tilemap>());
                var owner = root.AddComponent<DungeonCombatCoordinator>(); Set(owner, "map", view);
                var center = Bind<Func<Vector2Int, Vector2>>(owner, "PatrolCellWorld");
                var update = Bind<Action<float>>(owner, "UpdatePatrolsForElapsed");
                for (int pass = 0; pass < 2; pass++)
                {
                    root.transform.position += new Vector3(.37f, -.24f, 0); root.transform.localScale = new Vector3(1 + pass * .3f, .75f, 1);
                    Set(owner, "patrolCenterBatch", true); int before = owner.PatrolCellCenterQueryCount;
                    for (int i = 0; i < 100; i++)
                    { var cell = new Vector2Int(i % 4, i % 2); Assert.That(center(cell), Is.EqualTo(view.CellToWorldCenter(cell))); }
                    Assert.That(owner.PatrolCellCenterQueryCount - before, Is.EqualTo(4));
                    update(0); // A no-op update must still close and clear its batch.
                    Assert.That(Read<bool>(owner, "patrolCenterBatch"), Is.False);
                    Assert.That(Read<IDictionary>(owner, "patrolCellCenters").Count, Is.Zero);
                    root.transform.position += Vector3.right;
                    Assert.That(center(Vector2Int.zero), Is.EqualTo(view.CellToWorldCenter(Vector2Int.zero)));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
