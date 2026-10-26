using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class LightSourceMeshOptimizationTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
        private static T Bind<T>(object owner, string name) where T : Delegate => (T)owner.GetType().GetMethod(name, Fields).CreateDelegate(typeof(T), owner);

        [TestCase(16, false)] [TestCase(128, false)] [TestCase(256, false)] [TestCase(384, false)]
        [TestCase(31, true)] [TestCase(96, true)]
        public void VolumeMeshMatchesOriginalVerticesUvsIndicesBoundsAndFormat(int size, bool tight)
        {
            var texture = MakeTexture(size); Sprite sprite = null; Mesh legacy = null, actual = null;
            try
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.37f, .18f), 32, 0,
                    tight ? SpriteMeshType.Tight : SpriteMeshType.FullRect);
                legacy = LegacySilhouetteVolumeMesh.Build(sprite); actual = SilhouetteVolumeMesh.Build(sprite);
                CollectionAssert.AreEqual(legacy.vertices, actual.vertices);
                CollectionAssert.AreEqual(legacy.uv, actual.uv);
                CollectionAssert.AreEqual(legacy.triangles, actual.triangles);
                Assert.That(actual.bounds, Is.EqualTo(legacy.bounds));
                Assert.That(actual.indexFormat, Is.EqualTo(legacy.indexFormat));
                Assert.That(actual.vertexCount, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(actual); Object.DestroyImmediate(legacy); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void AnimationTexturesShareOnlyIdenticalGeometryAndReleaseMeshesOnce()
        {
            var root = new GameObject("Shared silhouette geometry"); var rig = root.AddComponent<DungeonSilhouetteLighting>();
            var textures = new[] { MakeTexture(128), MakeTexture(128) }; var sprites = new List<Sprite>();
            try
            {
                var get = Bind<Func<Sprite, Mesh>>(rig, "GetMesh");
                for (int i = 0; i < 40; i++) sprites.Add(Sprite.Create(textures[i & 1], new Rect(0, 0, 128, 128), new Vector2(.5f, .25f), 32, 0, SpriteMeshType.FullRect));
                var first = get(sprites[0]);
                foreach (var sprite in sprites) Assert.That(get(sprite), Is.SameAs(first));
                Assert.That(rig.ShadowMeshBuildCount, Is.EqualTo(1));
                foreach (var changed in new[] {
                    Sprite.Create(textures[0], new Rect(0, 0, 128, 128), new Vector2(.5f, .3f), 32, 0, SpriteMeshType.FullRect),
                    Sprite.Create(textures[0], new Rect(0, 0, 64, 128), new Vector2(.5f, .25f), 32, 0, SpriteMeshType.FullRect),
                    Sprite.Create(textures[0], new Rect(0, 0, 128, 128), new Vector2(.5f, .25f), 64, 0, SpriteMeshType.FullRect),
                    Sprite.Create(textures[0], new Rect(32, 0, 64, 128), new Vector2(.5f, .25f), 32, 0, SpriteMeshType.FullRect) })
                { sprites.Add(changed); Assert.That(get(changed), Is.Not.SameAs(first)); }
                Assert.That(rig.ShadowMeshBuildCount, Is.EqualTo(5));
                var owned = Read<IDictionary>(rig, "sharedMeshes").Values.Cast<Mesh>().ToArray();
                Bind<Action>(rig, "OnDisable")();
                Assert.That(owned.All(mesh => mesh == null), Is.True);
                Assert.That(Read<IDictionary>(rig, "meshes").Count, Is.Zero);
                Assert.That(Read<IDictionary>(rig, "sharedMeshes").Count, Is.Zero);
                Assert.That(get(sprites[0]), Is.Not.SameAs(first), "Re-enabled owner must rebuild disposed geometry.");
            }
            finally { Bind<Action>(rig, "OnDisable")(); Object.DestroyImmediate(root); foreach (var sprite in sprites) Object.DestroyImmediate(sprite); foreach (var texture in textures) Object.DestroyImmediate(texture); }
        }

        [Test]
        public void SourceSelectionMatchesOriginalStableNearestEightDuringPanZoomAndFogChanges()
        {
            using var fixture = new SourceFixture();
            var refresh = Bind<Action<Vector2, float>>(fixture.Rig, "RefreshLightSources");
            var resolve = typeof(DungeonSilhouetteLighting).GetMethod("ResolveTorchSortingOrder", Fields);
            var radius = Read<float>(fixture.Rig, "torchRadius");
            int queries = 0, originalQueries = 0;
            for (int step = 0; step < 7; step++)
            {
                var camera = new Vector2(step * 3 - 9, 0); float halfWidth = step % 2 == 0 ? 6 : 12;
                var viewport = new Rect(camera.x - halfWidth, -4, halfWidth * 2, 8);
                Set(fixture.Rig, "cameraGroundBounds", viewport);
                if (step == 2 || step == 4)
                {
                    fixture.Floor.Fog.SetVisible(fixture.Torches.Where((_, index) => step == 4 || index % 2 == 0)
                        .Select(torch => new DungeonTraversalPosition(Read<DungeonSurfacePosition>(torch, "cell"))));
                    fixture.Terrain.ApplyFog(fixture.Floor);
                }
                var expected = fixture.Torches.Where(torch =>
                        !fixture.Terrain.IsSurfaceOccluded(Read<DungeonSurfacePosition>(torch, "cell"))
                        && DistanceSquaredToRect(Read<Vector2>(torch, "position"), viewport)
                            <= Mathf.Pow(radius * Read<float>(torch, "radiusScale") + 1, 2))
                    .OrderBy(torch => (Read<Vector2>(torch, "position") - camera).sqrMagnitude).Take(8).ToArray();
                var oldCount = fixture.Rig.TorchSortingQueryCount;
                refresh(camera, step * .2f);
                CollectionAssert.AreEqual(expected, Read<IList>(fixture.Rig, "activeTorches").Cast<object>().ToArray());
                int visibleCount = 0;
                foreach (var torch in fixture.Torches)
                {
                    var view = Read<SpriteRenderer>(torch, "view"); var bounds = view.bounds;
                    bool visible = bounds.max.x >= viewport.xMin && bounds.min.x <= viewport.xMax
                        && bounds.max.y >= viewport.yMin && bounds.min.y <= viewport.yMax
                        && !fixture.Terrain.IsSurfaceOccluded(Read<DungeonSurfacePosition>(torch, "cell"));
                    Assert.That(view.enabled, Is.EqualTo(visible));
                    if (!visible) continue;
                    visibleCount++;
                    Assert.That(view.sortingLayerID, Is.EqualTo(fixture.Terrain.FloorFeedbackSortingLayerID));
                    Assert.That(view.sortingOrder, Is.EqualTo((int)resolve.Invoke(fixture.Rig, new[] { torch })));
                }
                Assert.That(fixture.Rig.TorchSortingQueryCount - oldCount, Is.EqualTo(visibleCount));
                queries += visibleCount; originalQueries += fixture.Torches.Count;
                var data = Read<Vector4[]>(fixture.Rig, "lightData");
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.That(new Vector2(data[i].x, data[i].y), Is.EqualTo(Read<Vector2>(expected[i], "position")));
                    Assert.That(data[i].z, Is.EqualTo(radius * Read<float>(expected[i], "radiusScale")));
                }
            }
            Assert.That(queries, Is.LessThan(originalQueries / 2));
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/light-source-mesh-pass-20261017"));
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "source-query-counts.json"), "{\"legacySortingQueries\":" + originalQueries + ",\"localSortingQueries\":" + queries + "}");
        }

        [Test]
        public void StationaryTorchAndFlameRemainVisibleOutsideCurrentFogVisibility()
        {
            using var fixture=new SourceFixture();
            var torch=fixture.Torches[40];var view=Read<SpriteRenderer>(torch,"view");
            var companion=new GameObject("Stationary flame").AddComponent<SpriteRenderer>();companion.transform.SetParent(view.transform);companion.sprite=fixture.Sprite;
            Set(torch,"companions",new[]{companion});Set(torch,"carried",false);
            Set(fixture.Rig,"cameraGroundBounds",new Rect(-5,-5,10,10));
            fixture.Floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());fixture.Terrain.ApplyFog(fixture.Floor);
            var present=typeof(DungeonSilhouetteLighting).GetMethod("RefreshTorchPresentation",Fields);
            present.Invoke(fixture.Rig,new object[]{torch,fixture.Terrain.FloorFeedbackSortingLayerID});
            Assert.That(view.enabled,Is.True);Assert.That(companion.enabled,Is.True);
            Set(fixture.Rig,"cameraGroundBounds",new Rect(95,-5,10,10));
            present.Invoke(fixture.Rig,new object[]{torch,fixture.Terrain.FloorFeedbackSortingLayerID});
            Assert.That(view.enabled,Is.False);Assert.That(companion.enabled,Is.False);
        }

        [Test]
        public void HiddenTorchAndCompanionsRefreshOwnerOrderBeforeBecomingVisibleAgain()
        {
            using var fixture = new SourceFixture();
            var torch = fixture.Torches[40]; var view = Read<SpriteRenderer>(torch, "view");
            var companion = new GameObject("Side flame").AddComponent<SpriteRenderer>(); companion.transform.SetParent(view.transform); companion.sprite = fixture.Sprite;
            Set(torch, "companions", new[] { companion }); Set(torch, "carried", true);
            var group = fixture.Root.AddComponent<SortingGroup>(); Set(torch, "mountGroup", group);
            var present = typeof(DungeonSilhouetteLighting).GetMethod("RefreshTorchPresentation", Fields);
            foreach (var offset in new[] { 0f, 100f, 0f })
            {
                group.sortingOrder += 51;
                Set(fixture.Rig, "cameraGroundBounds", new Rect(offset - 5, -5, 10, 10));
                int before = fixture.Rig.TorchSortingQueryCount;
                present.Invoke(fixture.Rig, new object[] { torch, fixture.Terrain.FloorFeedbackSortingLayerID });
                bool visible = offset == 0;
                Assert.That(view.enabled, Is.EqualTo(visible)); Assert.That(companion.enabled, Is.EqualTo(visible));
                Assert.That(fixture.Rig.TorchSortingQueryCount - before, Is.EqualTo(visible ? 1 : 0));
                if (visible)
                { Assert.That(view.sortingOrder, Is.EqualTo(group.sortingOrder + 1)); Assert.That(companion.sortingOrder, Is.EqualTo(view.sortingOrder)); }
            }
        }

        [Test]
        public void ArenaSourcesRespectOverlayCreationRemovalAndLiveVisibility()
        {
            var root = new GameObject("Arena source transitions");
            try
            {
                var terrain = root.AddComponent<DungeonFloorMapView>(); var map = new DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "arena"); Set(terrain, "boundMap", map);
                var tree = new GameObject("Tree").AddComponent<SpriteRenderer>(); tree.transform.SetParent(root.transform);
                var flame = new GameObject("Flame").AddComponent<SpriteRenderer>(); flame.transform.SetParent(tree.transform);
                var edge = new DungeonSurfaceEdge(new DungeonSurfacePosition(Vector2Int.zero), new DungeonSurfacePosition(Vector2Int.up));
                Read<List<(SpriteRenderer Tree, DungeonSurfaceEdge Edge)>>(terrain, "entFireTrees").Add((tree, edge));
                var overlays = Read<Dictionary<SpriteRenderer, SpriteRenderer>>(terrain, "entFireOverlays");
                Assert.That(terrain.ArenaFireLightSources(), Is.Empty);
                overlays.Add(tree, flame);
                Assert.That(terrain.ArenaFireLightSources().Single().flame, Is.SameAs(flame));
                flame.enabled = false; Assert.That(terrain.ArenaFireLightSources(), Is.Empty); flame.enabled = true;
                tree.enabled = false; Assert.That(terrain.ArenaFireLightSources(), Is.Empty); tree.enabled = true;
                tree.forceRenderingOff = true; Assert.That(terrain.ArenaFireLightSources(), Is.Empty); tree.forceRenderingOff = false;
                tree.gameObject.SetActive(false); Assert.That(terrain.ArenaFireLightSources(), Is.Empty); tree.gameObject.SetActive(true);
                tree.transform.position = new Vector3(3, 2, 0);
                Assert.That(terrain.ArenaFireLightSources().Single().position, Is.EqualTo(new Vector2(3, 2)));
                overlays.Clear(); Assert.That(terrain.ArenaFireLightSources(), Is.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class SourceFixture : IDisposable
        {
            public readonly GameObject Root = new("Source selection", typeof(Grid));
            public readonly DungeonFloorInstance Floor = new(0);
            public readonly DungeonFloorMapView Terrain;
            public readonly DungeonSilhouetteLighting Rig;
            public readonly Texture2D Texture = MakeTexture(16);
            public readonly Sprite Sprite;
            public readonly List<object> Torches = new();
            public SourceFixture()
            {
                Sprite = Sprite.Create(Texture, new Rect(0, 0, 16, 16), new Vector2(.5f, 0), 32, 0, SpriteMeshType.FullRect);
                var floor = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)); floor.transform.SetParent(Root.transform);
                Terrain = Root.AddComponent<DungeonFloorMapView>(); Set(Terrain, "floorTilemap", floor.GetComponent<Tilemap>());
                var map = new DungeonFloorMap(); Floor.TrySetMap(map);
                for (int y = -8; y <= 8; y++) for (int x = -60; x <= 60; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                Set(Terrain, "boundMap", map); Read<DungeonElevationViewState>(Terrain, "elevation").Bind(map);
                Rig = Root.AddComponent<DungeonSilhouetteLighting>(); Set(Rig, "terrain", Terrain); Set(Rig, "torchMap", map);
                Set(Rig, "party", Root.AddComponent<ExplorationPartyController>());
                var list = Read<IList>(Rig, "torches"); var type = typeof(DungeonSilhouetteLighting).GetNestedType("Torch", BindingFlags.NonPublic);
                for (int i = 0; i < 80; i++)
                {
                    var cell = new Vector2Int(i - 40, (i % 5) - 2); var position = (Vector2)Terrain.CellToWorldCenter(cell);
                    var view = new GameObject("Fixture " + i).AddComponent<SpriteRenderer>(); view.transform.SetParent(Root.transform); view.sprite = Sprite; view.transform.position = position;
                    var torch = Activator.CreateInstance(type, true); Set(torch, "view", view); Set(torch, "position", position);
                    Set(torch, "cell", new DungeonSurfacePosition(cell)); Set(torch, "radiusScale", .65f + .1f * (i % 3)); Set(torch, "phase", i * .1f);
                    list.Add(torch); Torches.Add(torch);
                }
            }
            public void Dispose() { Bind<Action>(Rig, "OnDisable")(); Object.DestroyImmediate(Root); Object.DestroyImmediate(Sprite); Object.DestroyImmediate(Texture); }
        }

        private static Texture2D MakeTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(x > size / 5 && y > size / 7 && x + y < size * 1.6f ? 255 : 0));
            texture.SetPixels32(pixels); texture.Apply(); return texture;
        }
        private static float DistanceSquaredToRect(Vector2 point, Rect rect)
        {
            float dx = Mathf.Max(Mathf.Max(rect.xMin - point.x, 0f), point.x - rect.xMax);
            float dy = Mathf.Max(Mathf.Max(rect.yMin - point.y, 0f), point.y - rect.yMax);
            return dx * dx + dy * dy;
        }
    }
}
