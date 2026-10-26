using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class FogCameraLightBudgetTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
        private static T Bind<T>(object owner, string method) where T : Delegate => (T)Delegate.CreateDelegate(typeof(T), owner, owner.GetType().GetMethod(method, Private | BindingFlags.Public));

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void LightingAndContactCoverageReuseBothExactQueriesAndEvictLeastRecentlyUsed(bool ground, bool exact)
        {
            var root = new GameObject("Alternating light coverage");
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            try
            {
                var type = typeof(DungeonFloorMapView).GetNestedType("ForestLightingCoverage", BindingFlags.NonPublic);
                var cached = Activator.CreateInstance(type, true); var cold = Activator.CreateInstance(type, true);
                var add = Bind<Action<SpriteRenderer, bool>>(cached, "Add");
                var coldAdd = Bind<Action<SpriteRenderer, bool>>(cold, "Add");
                var collect = Bind<Action<Rect, bool, List<SpriteRenderer>, bool>>(cached, "Collect");
                var coldCollect = Bind<Action<Rect, bool, List<SpriteRenderer>, bool>>(cold, "Collect");
                var clearCold = Bind<Action>(cold, "Clear");
                var sources = new List<SpriteRenderer>(); var actual = new List<SpriteRenderer>(); var expected = new List<SpriteRenderer>();
                for (var i = 0; i < 40; i++)
                {
                    var renderer = new GameObject("Source").AddComponent<SpriteRenderer>(); renderer.transform.SetParent(root.transform);
                    renderer.sprite = sprite; renderer.transform.position = new Vector3(i - 20, 0);
                    sources.Add(renderer); add(renderer, i % 2 == 0);
                }
                var a = new Rect(-8, -1, 16, 2); var b = Rect.MinMaxRect(-8.1f, -1.1f, 8.1f, 1.1f); var c = new Rect(12, -1, 16, 2);
                void Check(Rect viewport)
                {
                    clearCold();
                    for (var i = 0; i < sources.Count; i++) coldAdd(sources[i], i % 2 == 0);
                    actual.Clear(); expected.Clear(); collect(viewport, ground, actual, exact); coldCollect(viewport, ground, expected, exact);
                    CollectionAssert.AreEqual(expected, actual);
                }
                int Builds() => (int)type.GetProperty("QueryBuildCount").GetValue(cached);
                for (var i = 0; i < 20; i++) { Check(a); Check(b); }
                Assert.That(Builds(), Is.EqualTo(2), "The light and contact fringe must not evict each other.");
                Check(a); Check(c); Check(a); Assert.That(Builds(), Is.EqualTo(3));
                Check(b); Assert.That(Builds(), Is.EqualTo(4));
                Object.DestroyImmediate(sources[20].gameObject); Check(a); Check(b);
                Assert.That(actual.All(renderer => renderer != null), Is.True);
                Assert.That(Builds(), Is.EqualTo(4));
                var extra = new GameObject("Added source").AddComponent<SpriteRenderer>(); extra.transform.SetParent(root.transform); extra.sprite = sprite;
                sources.Add(extra); add(extra, true);
                Check(a); Check(b); Assert.That(Builds(), Is.EqualTo(6), "Adding a source invalidates both saved rectangles.");
                Bind<Action>(cached, "Clear")(); actual.Clear(); collect(a, ground, actual, exact); Assert.That(actual, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); }
        }

        [Test]
        public void StableInteriorIsOmittedWhileBoundaryColoursAndTransitionsStayLive()
        {
            var type = typeof(LightFogMapOptimizationTests).GetNestedType("FogFixture", BindingFlags.NonPublic);
            using var fixture = (IDisposable)Activator.CreateInstance(type, new object[] { 40 });
            var view = (DungeonFloorMapView)type.GetField("View").GetValue(fixture);
            var projection = Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "fogProjection");
            var previous = Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "previousFogProjection");
            var presented = Read<Dictionary<Vector3Int, (TileBase Tile, Color Color)>>(view, "presentedFloorCells");
            var tiles = (Tilemap)type.GetField("Tiles").GetValue(fixture);
            var tile = Read<Tile>(fixture, "tile");
            var refresh = Bind<Action>(view, "RefreshSoftFog");
            var tick = Bind<Action<float>>(view, "TickFogAnimation");
            projection.Clear(); previous.Clear();
            for (var y = 0; y < 40; y++) for (var x = 0; x < 40; x++)
            {
                var cell = new Vector2Int(x, y); projection[cell] = DungeonKnowledge.Visible;
                var key = new Vector3Int(x, y, 0); tiles.SetTile(key, tile); tiles.SetTileFlags(key, TileFlags.None);
                presented[key] = (tile, Color.white);
            }
            projection[new Vector2Int(20, 20)] = DungeonKnowledge.Unexplored;
            refresh(); tick(.4f);
            foreach (var pair in projection) previous[pair.Key] = pair.Value;
            var count = view.FogPatternCandidateCount;
            refresh();
            Assert.That(view.FogPatternCandidateCount - count, Is.LessThan(100), "Do not revisit all 1599 known interior cells.");
            var boundary = new Vector3Int(19, 20, 0);
            presented[boundary] = (tile, Color.red);
            refresh(); tick(.4f);
            Assert.That(tiles.GetColor(boundary), Is.EqualTo(Color.red));
            projection[new Vector2Int(20, 20)] = DungeonKnowledge.Visible;
            refresh(); tick(.1f);
            // Preserve the active transition on a refresh without knowledge changes.
            previous.Clear(); foreach (var pair in projection) previous[pair.Key] = pair.Value;
            refresh(); tick(.4f);
            Assert.That(tiles.GetColor(new Vector3Int(20, 20, 0)), Is.EqualTo(Color.white));
        }

        [Test]
        public void FogProjectionUsesTwoOwnerBuffersAndPreservesPreviousSnapshotExactly()
        {
            var type = typeof(LightFogMapOptimizationTests).GetNestedType("FogFixture", BindingFlags.NonPublic);
            using var fixture = (IDisposable)Activator.CreateInstance(type, new object[] { 14 });
            var view = (DungeonFloorMapView)type.GetField("View").GetValue(fixture);
            var current = Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "fogProjection");
            var reusable = Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "previousFogProjection");
            var expected = current.ToArray();
            Bind<Action>(view, "BeginFogAnimationRefresh")();
            Assert.That(Read<object>(view, "previousFogProjection"), Is.SameAs(current));
            Assert.That(Read<object>(view, "fogProjection"), Is.SameAs(reusable));
            reusable.Clear(); reusable.Add(new Vector2Int(100, 100), DungeonKnowledge.Remembered);
            CollectionAssert.AreEquivalent(expected, current);
            Bind<Action>(view, "BeginFogAnimationRefresh")();
            Assert.That(Read<object>(view, "previousFogProjection"), Is.SameAs(reusable));
            Assert.That(Read<object>(view, "fogProjection"), Is.SameAs(current));
        }

        [Test]
        public void CameraPanReusesKnowledgeAndMatchesFullRefreshAfterVisibilityChanges()
        {
            var root = new GameObject("Fog budget camera fixture", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                var child = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer)); child.transform.SetParent(root.transform);
                var tiles = child.GetComponent<Tilemap>();
                var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tiles); Set(view, "floorTile", tile);
                view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                var map = new DungeonFloorMap();
                for (var y = 0; y < 14; y++) for (var x = 0; x < 60; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                Set(view, "boundMap", map); Read<DungeonElevationViewState>(view, "elevation").Bind(map);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                floor.Fog.SetVisible(map.FloorCells.Select(cell => new DungeonTraversalPosition(new DungeonSurfacePosition(cell))));
                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.orthographic = true; camera.orthographicSize = 3; camera.aspect = 1; camera.transform.position = new Vector3(8, 6, -10);
                view.ConfigurePresentationCamera(camera); view.ApplyFog(floor);
                foreach (var x in new[] { 11f, 15f, 21f })
                {
                    var before = view.FogCoverageKnowledgeReuseCount;
                    camera.transform.position = new Vector3(x, 6, -10); view.EnsureCameraPresentation();
                    Assert.That(view.FogCoverageKnowledgeReuseCount, Is.GreaterThan(before));
                    var projection = Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "fogProjection").ToArray();
                    var presented = Read<Dictionary<Vector3Int, (TileBase, Color)>>(view, "presentedFloorCells").ToArray();
                    view.ApplyFog(floor);
                    CollectionAssert.AreEquivalent(projection, Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "fogProjection"));
                    CollectionAssert.AreEquivalent(presented, Read<Dictionary<Vector3Int, (TileBase, Color)>>(view, "presentedFloorCells"));
                }
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); view.ApplyFog(floor);
                Assert.That(Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "fogProjection").Values.All(value => value != DungeonKnowledge.Visible), Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void BoundsAreReadOncePerReceiverPreparationAndFreshInNextScope()
        {
            var root = new GameObject("Receiver snapshot fixture"); var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4);
            try
            {
                var rig = root.AddComponent<DungeonSilhouetteLighting>();
                var renderer = new GameObject("Receiver", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>(); renderer.transform.SetParent(root.transform); renderer.sprite = sprite;
                var read = Bind<Func<SpriteRenderer, Bounds>>(rig, "ReadPreparedReceiverBounds");
                Assert.That(read(renderer), Is.EqualTo(renderer.bounds));
                for (var i = 0; i < 100; i++) read(renderer);
                Assert.That(rig.ReceiverBoundsReadCount, Is.EqualTo(1));
                Read<IDictionary>(rig, "preparedReceiverBounds").Clear();
                renderer.transform.position += Vector3.right * 10;
                Assert.That(read(renderer), Is.EqualTo(renderer.bounds)); Assert.That(rig.ReceiverBoundsReadCount, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void RepeatedForestContactsKeepExactFirstBandsWithoutRegeneratingDuplicates()
        {
            var root = new GameObject("Contact source fixture");
            var type = typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows");
            var contact = (object)Activator.CreateInstance(type, root.transform, Shader.Find("Sprites/Default"));
            try
            {
                var add = Bind<Action<Vector2, float>>(contact, "AddForestContact");
                var bands = Read<IList>(contact, "bands");
                add(Vector2.one, 1f); var first = bands.Cast<object>().ToArray();
                for (var i = 0; i < 100; i++) add(Vector2.one, 1f);
                CollectionAssert.AreEqual(first, bands);
                Assert.That((int)type.GetProperty("ForestContactSourceCount").GetValue(contact), Is.EqualTo(1));
                add(Vector2.one + Vector2.right, 1f);
                Assert.That((int)type.GetProperty("ForestContactSourceCount").GetValue(contact), Is.EqualTo(2));
                Assert.That(bands.Count, Is.EqualTo(first.Length * 2));
            }
            finally { type.GetMethod("Dispose").Invoke(contact, null); Object.DestroyImmediate(root); }
        }

        [Test]
        public void PreparedContactBandsMatchUncachedBandsAfterPositionAndWidthChanges()
        {
            var root = new GameObject("Prepared contact fixture");
            var source = root.AddComponent<SpriteRenderer>();
            var type = typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows");
            var contact = Activator.CreateInstance(type, root.transform, Shader.Find("Sprites/Default"));
            try
            {
                var plain = Bind<Action<Vector2, float>>(contact, "AddForestContact");
                var prepared = Bind<Action<SpriteRenderer, Vector2, float>>(contact, "AddForestContactForSource");
                var bands = Read<IList>(contact, "bands");
                var sources = Read<HashSet<(Vector2, float)>>(contact, "forestContactSources");
                void Reset() { bands.Clear(); sources.Clear(); }
                foreach (var foot in new[] { Vector2.one, new Vector2(3, 2), new Vector2(-1, 5), new Vector2(.015625f - .000001f, 1), new Vector2(.015625f + .000001f, 1) })
                foreach (var width in new[] { .5f, 1.1f, 3f })
                {
                    Reset(); plain(foot, width); var expected = bands.Cast<object>().ToArray();
                    Reset(); prepared(source, foot, width); CollectionAssert.AreEqual(expected, bands);
                    var count = (int)type.GetProperty("ForestBandTranslationCount").GetValue(contact);
                    Reset(); prepared(source, foot, width); CollectionAssert.AreEqual(expected, bands);
                    Assert.That((int)type.GetProperty("ForestBandTranslationCount").GetValue(contact), Is.EqualTo(count));
                }
            }
            finally { type.GetMethod("Dispose").Invoke(contact, null); Object.DestroyImmediate(root); }
        }

        [Test]
        public void ContactCacheKeepsExactClippedMeshAndReleasesHiddenSources()
        {
            var fixtureType = typeof(ShadowContactOptimizationTests).GetNestedType("ContactFixture", BindingFlags.NonPublic);
            using var fixture = (IDisposable)Activator.CreateInstance(fixtureType, true);
            var root = (GameObject)fixtureType.GetField("Root").GetValue(fixture);
            var view = (DungeonFloorMapView)fixtureType.GetField("View").GetValue(fixture);
            var floor = (DungeonFloorInstance)fixtureType.GetField("Floor").GetValue(fixture);
            var contact = fixtureType.GetField("Contact").GetValue(fixture);
            var texture = new Texture2D(8, 8); var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * .5f, 8);
            try
            {
                view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                floor.Fog.SetVisible(floor.Map.FloorCells.Select(cell => new DungeonTraversalPosition(new DungeonSurfacePosition(cell)))); view.ApplyFog(floor);
                var source = new GameObject("Contact source", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>(); source.transform.SetParent(root.transform); source.sprite = sprite;
                var rebuild = contact.GetType().GetMethod("Rebuild");
                var renderer = root.GetComponentInChildren<TilemapRenderer>();
                void Rebuild()
                {
                    rebuild.Invoke(contact, new object[] { view, renderer, new Rect(-1, -1, 12, 12), null, Array.Empty<SpriteRenderer>(), null, new[] { source } });
                    var bands = Read<List<(Rect Rect, byte Opacity)>>(contact, "bands");
                    var expected = ((List<Vector3> Vertices, List<Color32> Colors, int Queries))fixtureType.GetMethod("LegacyClip").Invoke(fixture, new object[] { bands });
                    CollectionAssert.AreEqual(expected.Vertices, Read<Mesh>(contact, "mesh").vertices);
                    CollectionAssert.AreEqual(expected.Colors, Read<Mesh>(contact, "mesh").colors32);
                }
                source.transform.position = new Vector3(2, 2); Rebuild();
                Assert.That(Read<IDictionary>(contact, "forestSourceBands").Contains(source), Is.True);
                var count = (int)contact.GetType().GetProperty("ForestBandTranslationCount").GetValue(contact);
                Rebuild(); Assert.That((int)contact.GetType().GetProperty("ForestBandTranslationCount").GetValue(contact), Is.EqualTo(count));
                source.transform.position += Vector3.right; Rebuild();
                source.transform.localScale = new Vector3(1.5f, 1, 1); Rebuild();
                source.enabled = false; Rebuild();
                Assert.That(Read<IDictionary>(contact, "forestSourceBands").Contains(source), Is.False);
                source.enabled = true; Rebuild();
                Assert.That(Read<IDictionary>(contact, "forestSourceBands").Contains(source), Is.True);
            }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }
    }
}
