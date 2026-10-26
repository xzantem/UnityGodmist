using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class LightingCoverageFogMeshTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);
        private static T Bind<T>(object owner, string name) where T : Delegate => (T)owner.GetType().GetMethod(name, Fields).CreateDelegate(typeof(T), owner);

        [Test]
        public void PartialFogUploadsMatchOriginalStreamsAndGpuPixelsThroughRetirementReuseAndGrowth()
        {
            var type = typeof(LightFogMapOptimizationTests).GetNestedType("FogFixture", BindingFlags.NonPublic);
            var fixture = Activator.CreateInstance(type, new object[] { 14 });
            var view = Read<DungeonFloorMapView>(fixture, "View");
            var project = Bind<Action<int, bool>>(fixture, "Project"); var tick = Read<Action<float>>(fixture, "Tick");
            var mirror = new Mesh { indexFormat = IndexFormat.UInt32 };
            var target = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var image = new Texture2D(128, 128, TextureFormat.RGBA32, false, true); target.Create();
            using var commands = new CommandBuffer();
            bool nonEmpty = false;
            try
            {
                byte[] Render(Mesh mesh, Material material)
                {
                    commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(false, true, Color.white);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1, 15, -1, 15, -1, 1), true));
                    if (mesh != null && mesh.vertexCount > 0) commands.DrawMesh(mesh, Matrix4x4.identity, material);
                    Graphics.ExecuteCommandBuffer(commands); var previous = RenderTexture.active;
                    try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); return image.GetRawTextureData<byte>().ToArray(); }
                    finally { RenderTexture.active = previous; }
                }
                void Compare()
                {
                    var actual = Read<Mesh>(view, "fogBlendMesh"); if (actual == null) return;
                    var vertices = Read<List<Vector3>>(view, "fogVertices"); var colors = Read<List<Color>>(view, "fogColors");
                    var uv = Read<List<Vector2>>(view, "fogUVs"); var indices = Read<List<int>>(view, "fogIndices");
                    CollectionAssert.AreEqual(vertices, actual.vertices); CollectionAssert.AreEqual(colors, actual.colors);
                    CollectionAssert.AreEqual(uv, actual.uv); CollectionAssert.AreEqual(indices, actual.triangles);
                    mirror.Clear(); mirror.SetVertices(vertices); mirror.SetColors(colors); mirror.SetUVs(0, uv); mirror.SetTriangles(indices, 0);
                    // ClearSoftFog deliberately disables the empty renderer and
                    // Unity retains its previous bounds. Only live geometry has
                    // a recomputed bound in the original production path.
                    if (vertices.Count > 0) { mirror.RecalculateBounds(); Assert.That(actual.bounds, Is.EqualTo(mirror.bounds)); }
                    else Assert.That(Read<MeshRenderer>(view, "fogBlendRenderer").enabled, Is.False);
                    var material = Read<Material>(view, "fogBlendMaterial");
                    var pixels = Render(actual, material); CollectionAssert.AreEqual(Render(mirror, material), pixels);
                    nonEmpty |= pixels.Where((_, index) => index % 4 != 3).Any(value => value != 255);
                }
                foreach (var step in new[] { 0, 1, 2, 3, 4, 0 })
                { project(step, false); Compare(); tick(.07f); Compare(); tick(.3f); Compare(); }
                Assert.That(nonEmpty, Is.True);
                var cpu = Read<List<Color>>(view, "fogColors"); var slots = Read<IDictionary>(view, "fogMeshSlots");
                var first = slots.Values.Cast<int>().Min(); int start = first * 81;
                for (int i = 0; i < 81; i++) cpu[start + i] = new Color(0, 0, 0, .37f);
                Read<HashSet<int>>(view, "dirtyFogColorSlots").Add(first);
                int before = view.FogColorVertexUploadCount;
                Bind<Action<bool>>(view, "UploadFogColors")(false); Compare();
                Assert.That(view.FogColorVertexUploadCount - before, Is.EqualTo(81));
                Assert.That(cpu.Count, Is.GreaterThan(162));
                var vertices = Read<List<Vector3>>(view, "fogVertices");
                for (int i = 0; i < 81; i++) vertices[start + i] += Vector3.right * .01f;
                Read<List<int>>(view, "dirtyFogGeometrySlots").Add(first);
                before = view.FogGeometryVertexUploadCount;
                Bind<Action<bool>>(view, "UploadFogGeometry")(false);
                Read<Mesh>(view, "fogBlendMesh").RecalculateBounds(); Compare();
                Assert.That(view.FogGeometryVertexUploadCount - before, Is.EqualTo(81));
                // Also compare the actual MeshRenderer/camera path, including
                // its bounds and stream bindings, against the old mesh layout.
                foreach (var renderer in Read<GameObject>(fixture, "Root").GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                var cameraObject = new GameObject("Fog stream camera", typeof(Camera));
                var cameraTarget = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); cameraTarget.Create();
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 8;
                camera.aspect = 1; camera.transform.position = new Vector3(7, 7, -10); camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white; camera.targetTexture = cameraTarget;
                var filter = Read<MeshRenderer>(view, "fogBlendRenderer").GetComponent<MeshFilter>(); var actualMesh = filter.sharedMesh;
                try
                {
                    byte[] CameraPixels()
                    {
                        camera.Render(); camera.Render(); var previous = RenderTexture.active;
                        try { RenderTexture.active = cameraTarget; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); return image.GetRawTextureData<byte>().ToArray(); }
                        finally { RenderTexture.active = previous; }
                    }
                    var actualPixels = CameraPixels(); filter.sharedMesh = mirror;
                    CollectionAssert.AreEqual(CameraPixels(), actualPixels);
                }
                finally { filter.sharedMesh = actualMesh; Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(cameraTarget); }
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/lighting-coverage-fogmesh-pass-20261017"));
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "fog-upload-counts.json"), "{\"fullVertices\":" + cpu.Count + ",\"partialVertices\":81}");
            }
            finally { ((IDisposable)fixture).Dispose(); Object.DestroyImmediate(mirror); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }

        [Test]
        public void CameraOnlyCoverageMatchesFullPixelsAndInvalidatesWorldKnowledgeAndTransforms()
        {
            var root = new GameObject("Camera coverage delta", typeof(Grid)); root.layer = 30;
            var tile = ScriptableObject.CreateInstance<Tile>(); var texture = new Texture2D(4, 4);
            texture.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray()); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4); tile.sprite = sprite;
            var target = new RenderTexture(128, 128, 24); var image = new Texture2D(128, 128, TextureFormat.RGBA32, false); target.Create();
            try
            {
                var tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer)); tiles.layer = 30; tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tiles.GetComponent<Tilemap>()); Set(view, "floorTile", tile);
                view.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                var map = new DungeonFloorMap(); var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                for (int y = 0; y < 12; y++) for (int x = 0; x < 40; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                foreach (var cell in map.FloorCells.ToArray()) foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                    if (!map.IsWalkable(cell + direction)) map.TryAddWall(cell, cell + direction);
                Set(view, "wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                view.Rebuild(map);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                floor.Fog.SetVisible(map.FloorCells.Select(cell => new DungeonTraversalPosition(new DungeonSurfacePosition(cell)))); view.ApplyFog(floor);
                var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1; camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = target;
                camera.transform.position = new Vector3(5, 2.5f, -10); view.ConfigurePresentationCamera(camera);
                byte[] Pixels()
                {
                    camera.Render(); camera.Render(); var previous = RenderTexture.active;
                    try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); return image.GetRawTextureData<byte>().ToArray(); }
                    finally { RenderTexture.active = previous; }
                }
                bool nonEmpty = false;
                foreach (var x in new[] { 5f, 10f, 16f, 23f })
                {
                    camera.orthographicSize = x == 16 ? 2.5f : x == 23 ? 1.5f : 2f;
                    camera.transform.position = new Vector3(x, 2.5f, -10); int revision = view.PresentationRevision;
                    int before = view.CoverageOnlyRefreshCount; view.EnsureCameraPresentation();
                    Assert.That(view.CoverageOnlyRefreshCount, Is.GreaterThan(before)); Assert.That(view.PresentationRevision, Is.EqualTo(revision + 1));
                    var actual = Pixels(); view.ApplyFog(floor); CollectionAssert.AreEqual(Pixels(), actual);
                    nonEmpty |= actual.Where((_, index) => index % 4 != 3).Any(value => value != 0);
                }
                Assert.That(nonEmpty, Is.True);
                var current = Bind<Func<bool>>(view, "WorldPresentationIsCurrent"); Assert.That(current(), Is.True);
                Bind<Action>(view, "RefreshWallRendererCache")(); Assert.That(current(), Is.False);
                view.ApplyFog(floor); Assert.That(current(), Is.True);
                Bind<Action>(view, "InvalidateForestLightingCoverage")(); Assert.That(current(), Is.False);
                view.ApplyFog(floor); Assert.That(current(), Is.True);
                var edge = new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right));
                floor.Fog.ClosedGates.Add(edge); Assert.That(current(), Is.False); floor.Fog.ClosedGates.Remove(edge);
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); Assert.That(current(), Is.False);
                camera.transform.position += Vector3.right * 7; int count = view.CoverageOnlyRefreshCount; view.EnsureCameraPresentation();
                Assert.That(view.CoverageOnlyRefreshCount, Is.EqualTo(count)); Assert.That(current(), Is.True);
                root.transform.position += Vector3.right; Assert.That(current(), Is.False);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }

        [Test]
        public void StaticCoverageQueryCacheKeepsOrderFreshLifetimesAndInvalidatesOnAddAndClear()
        {
            var root = new GameObject("Coverage query cache"); var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4);
            try
            {
                var type = typeof(DungeonFloorMapView).GetNestedType("ForestLightingCoverage", BindingFlags.NonPublic);
                var coverage = Activator.CreateInstance(type, true); var add = Bind<Action<SpriteRenderer, bool>>(coverage, "Add");
                var collect = Bind<Action<Rect, bool, List<SpriteRenderer>, bool>>(coverage, "Collect");
                var renderers = new List<SpriteRenderer>();
                for (int i = 0; i < 30; i++) { var renderer = new GameObject("Source").AddComponent<SpriteRenderer>(); renderer.transform.SetParent(root.transform); renderer.sprite = sprite; renderer.transform.position = new Vector3(i - 10, 0); add(renderer, true); renderers.Add(renderer); }
                var viewport = new Rect(-1, -1, 8, 2); var output = new List<SpriteRenderer>();
                for (int pass = 0; pass < 10; pass++)
                {
                    output.Clear(); collect(viewport, true, output, true);
                    CollectionAssert.AreEqual(renderers.Where(renderer => renderer.bounds.max.x >= viewport.xMin && renderer.bounds.min.x <= viewport.xMax), output);
                }
                Assert.That((int)type.GetProperty("QueryBuildCount").GetValue(coverage), Is.EqualTo(1));
                Object.DestroyImmediate(renderers[10].gameObject); output.Clear(); collect(viewport, true, output, true);
                Assert.That(output.All(renderer => renderer != null), Is.True);
                var extra = new GameObject("New source").AddComponent<SpriteRenderer>(); extra.transform.SetParent(root.transform); extra.sprite = sprite; add(extra, true);
                output.Clear(); collect(viewport, true, output, true); Assert.That(output.Last(), Is.SameAs(extra));
                Assert.That((int)type.GetProperty("QueryBuildCount").GetValue(coverage), Is.EqualTo(2));
                Bind<Action>(coverage, "Clear")(); output.Clear(); collect(viewport, true, output, true); Assert.That(output, Is.Empty);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void DuplicateContactBandsPreserveFirstOpacityWithoutRepeatingNativeProjection()
        {
            var type = typeof(ShadowContactOptimizationTests).GetNestedType("ContactFixture", BindingFlags.NonPublic);
            var fixture = Activator.CreateInstance(type, true);
            try
            {
                var contact = Read<object>(fixture, "Contact");
                var bands = Enumerable.Range(0, 24).Select(i => (Rect: new Rect(2.2f, 2.4f, .6f, .03f), Opacity: (byte)(i == 0 ? 80 : 200))).ToList();
                var legacy = ((List<Vector3> Vertices, List<Color32> Colors, int Queries))type.GetMethod("LegacyClip", Fields).Invoke(fixture, new object[] { bands });
                var emit = Bind<Action<Rect, byte>>(contact, "Emit");
                foreach (var band in bands) emit(band.Rect, band.Opacity);
                CollectionAssert.AreEqual(legacy.Vertices, Read<List<Vector3>>(contact, "vertices"));
                CollectionAssert.AreEqual(legacy.Colors, Read<List<Color32>>(contact, "colors"));
                Assert.That(legacy.Vertices.Count, Is.GreaterThan(0));
                Assert.That((int)contact.GetType().GetProperty("BandProjectionCount").GetValue(contact), Is.EqualTo(1));
            }
            finally { ((IDisposable)fixture).Dispose(); }
        }
    }
}
