using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class LightFogMapOptimizationTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/light-fog-map-pass-20261017"));

        private sealed class FogFixture : IDisposable
        {
            public readonly GameObject Root = new("Fog delta fixture", typeof(Grid));
            public readonly DungeonFloorMapView View;
            public readonly Tilemap Tiles;
            private readonly Tile tile = ScriptableObject.CreateInstance<Tile>();
            private readonly int size;
            public readonly Action Refresh, Build;
            public readonly Action<float> Tick;
            private readonly Dictionary<Vector2Int, DungeonKnowledge> projection;
            public FogFixture(int size = 14)
            {
                this.size = size;
                var go = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(Root.transform);
                Tiles = go.GetComponent<Tilemap>(); View = Root.AddComponent<DungeonFloorMapView>(); Set(View, "floorTilemap", Tiles); Set(View, "floorTile", tile);
                var map = new DungeonFloorMap();
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                Set(View, "boundMap", map); Read<DungeonElevationViewState>(View, "elevation").Bind(map);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                floor.Fog.SetVisible(new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)) });
                View.ApplyFog(floor); Set(View, "snapFogAnimation", false);
                projection = Read<Dictionary<Vector2Int, DungeonKnowledge>>(View, "fogProjection");
                Refresh = Bind<Action>(View, "RefreshSoftFog"); Build = Bind<Action>(View, "BuildFogMesh"); Tick = Bind<Action<float>>(View, "TickFogAnimation");
            }
            public void Project(int step, bool reference)
            {
                var previous = Read<Dictionary<Vector2Int, DungeonKnowledge>>(View, "previousFogProjection");
                previous.Clear(); foreach (var pair in projection) previous.Add(pair.Key, pair.Value);
                projection.Clear();
                var presented = Read<Dictionary<Vector3Int, (TileBase Tile, Color Color)>>(View, "presentedFloorCells");
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    if (step == 3 && x == 0 || step == 4) continue;
                    var cell = new Vector2Int(x, y);
                    var state = y == 0 || y == size - 1 ? DungeonKnowledge.Unexplored
                        : x % 3 == 0 ? DungeonKnowledge.Remembered : DungeonKnowledge.Visible;
                    if (step == 1 && x == 5 && y == 5) state = DungeonKnowledge.Unexplored;
                    if (step == 2 && x == 5 && y == 5) state = DungeonKnowledge.Remembered;
                    projection[cell] = state;
                    if (state == DungeonKnowledge.Unexplored) continue;
                    var color = state == DungeonKnowledge.Remembered ? DungeonFloorMapView.Dim(Color.white) : Color.white;
                    if (step == 2 && x == 8 && y == 6) color *= .8f;
                    var key = new Vector3Int(x, y, 0); Tiles.SetTile(key, tile); Tiles.SetTileFlags(key, TileFlags.None); presented[key] = (tile, color);
                }
                if (reference) Read<IDictionary>(View, "fogTargetPatterns").Clear();
                Refresh();
            }
            public byte[] Render(RenderTexture target, CommandBuffer commands)
            {
                commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(false, true, Color.white);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1, size + 1, -1, size + 1, -1, 1), true));
                var renderer = Read<MeshRenderer>(View, "fogBlendRenderer");
                if (renderer != null && renderer.enabled) commands.DrawMesh(Read<Mesh>(View, "fogBlendMesh"), Matrix4x4.identity, renderer.sharedMaterial);
                Graphics.ExecuteCommandBuffer(commands);
                var previous = RenderTexture.active;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
                try
                {
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                    return image.GetRawTextureData<byte>().ToArray();
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
            }
            public void Dispose() { Object.DestroyImmediate(Root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void DeltaFogMatchesFullPatternScanPixelsThroughRevealReversalCoverageAndColorChanges()
        {
            using var delta = new FogFixture(); using var reference = new FogFixture();
            var target = new RenderTexture(256, 256, 0); target.Create();
            using var commands = new CommandBuffer();
            try
            {
                foreach (int step in new[] { 0, 0, 1, 2, 3, 0, 4, 0 })
                {
                    delta.Project(step, false); reference.Project(step, true);
                    void Compare()
                    {
                        var pixels = delta.Render(target, commands);
                        CollectionAssert.AreEqual(reference.Render(target, commands), pixels);
                        if (step != 4) Assert.That(pixels.Any(value => value < 200), Is.True, "Comparison must contain visible fog, not empty white renders.");
                        foreach (var pair in Read<Dictionary<Vector2Int, DungeonKnowledge>>(delta.View, "fogProjection"))
                            Assert.That(delta.Tiles.GetColor(new Vector3Int(pair.Key.x, pair.Key.y, 0)),
                                Is.EqualTo(reference.Tiles.GetColor(new Vector3Int(pair.Key.x, pair.Key.y, 0))));
                    }
                    Compare();
                    foreach (float elapsed in new[] { .1f, .2f }) { delta.Tick(elapsed); reference.Tick(elapsed); Compare(); }
                }
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void StableFogAvoidsPatternEvaluationAndColorRewritesAndMeasuresForcedScan()
        {
            using var fixture = new FogFixture(40);
            fixture.Project(0, false); fixture.Tick(.4f); fixture.Project(0, false);
            int patterns = fixture.View.FogPatternEvaluationCount, writes = fixture.View.FogColorPatchWriteCount;
            int uploads = fixture.View.FogColorUploadCount;
            fixture.Project(0, false); fixture.Build();
            Assert.That(fixture.View.FogPatternEvaluationCount, Is.EqualTo(patterns));
            Assert.That(fixture.View.FogColorPatchWriteCount, Is.EqualTo(writes));
            Assert.That(fixture.View.FogColorUploadCount, Is.EqualTo(uploads));
            var targetCache = Read<IDictionary>(fixture.View, "fogTargetPatterns");
            var retained = new double[15]; var full = new double[15]; var watch = new Stopwatch();
            for (int i = 0; i < 15; i++)
            {
                watch.Restart(); fixture.Refresh(); watch.Stop(); retained[i] = watch.Elapsed.TotalMilliseconds;
                targetCache.Clear(); watch.Restart(); fixture.Refresh(); watch.Stop(); full[i] = watch.Elapsed.TotalMilliseconds;
            }
            Array.Sort(retained); Array.Sort(full);
            Assert.That(fixture.View.FogPatternEvaluationCount, Is.GreaterThan(patterns));
            Assert.That(fixture.View.FogColorPatchWriteCount, Is.GreaterThan(writes));
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "fog-delta-comparison.json"), JsonUtility.ToJson(new FogMetric {
                retainedMs = retained[7], forcedScanMs = full[7], patches = Read<IDictionary>(fixture.View, "fogCellVisuals").Count,
                evaluations = fixture.View.FogPatternEvaluationCount - patterns, colorWrites = fixture.View.FogColorPatchWriteCount - writes }, true));
        }

        [Test]
        public void IndexedDecorationsPreserveVisibleOrderAndLiveRuntimePropsAcrossPanMoveRebuildAndRemoval()
        {
            var root = new GameObject("Decoration coverage");
            var vegetation = ScriptableObject.CreateInstance<DungeonVegetationPresentation>();
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "vegetationPresentation", vegetation);
                var type = typeof(DungeonFloorMapView).GetNestedType("PresentationDecoration", BindingFlags.NonPublic);
                var list = Read<IList>(view, "presentationDecorations");
                var all = new List<(SpriteRenderer Renderer, bool Wall)>();
                SpriteRenderer Add(int index)
                {
                    var go = new GameObject("Decoration " + index, typeof(SpriteRenderer)); go.transform.SetParent(root.transform);
                    go.transform.localPosition = new Vector3(index * 2, 0); var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
                    var decoration = Activator.CreateInstance(type, true); type.GetField("Object").SetValue(decoration, go);
                    type.GetField("Renderer").SetValue(decoration, renderer); type.GetField("IsWall").SetValue(decoration, index % 2 == 0);
                    list.Add(decoration); all.Add((renderer, index % 2 == 0)); return renderer;
                }
                for (int i = 0; i < 300; i++) Add(i);
                var runtime = new GameObject("Moving dagger", typeof(SpriteRenderer)); runtime.transform.SetParent(root.transform);
                var moving = runtime.GetComponent<SpriteRenderer>(); moving.sprite = sprite; view.RegisterGroundLightingPresentation(moving);
                var result = new List<SpriteRenderer>();
                void Compare(Rect area)
                {
                    bool Visible(SpriteRenderer r) => r != null && r.bounds.max.x >= area.xMin && r.bounds.min.x <= area.xMax
                        && r.bounds.max.y >= area.yMin && r.bounds.min.y <= area.yMax;
                    view.CollectLightingWallRenderers(area, result);
                    CollectionAssert.AreEqual(all.Where(item => item.Wall && Visible(item.Renderer)).Select(item => item.Renderer), result);
                    view.CollectLightingGroundRenderers(area, result);
                    CollectionAssert.AreEqual(all.Where(item => !item.Wall && Visible(item.Renderer)).Select(item => item.Renderer).Concat(new[] { moving }), result);
                    Assert.That(result.Count, Is.LessThan(20));
                }
                Compare(new Rect(-1, -1, 10, 3)); Compare(new Rect(90, -1, 10, 3));
                moving.transform.position = new Vector3(200, 0); Compare(new Rect(199, -1, 10, 3));
                Add(300); Compare(new Rect(599, -1, 10, 3));
                root.transform.position = Vector3.right * 1000; Compare(new Rect(999, -1, 10, 3));
                Object.DestroyImmediate(all[0].Renderer.gameObject); Compare(new Rect(999, -1, 10, 3));
                Bind<Action>(view, "ClearPresentationDecorations")();
                all.Clear(); Add(0); Compare(new Rect(999, -1, 10, 3));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(vegetation); Object.DestroyImmediate(sprite); }
        }

        private static T Bind<T>(object owner, string method) where T : Delegate => (T)owner.GetType().GetMethod(method, Private).CreateDelegate(typeof(T), owner);
        private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
        [Serializable] private sealed class FogMetric { public double retainedMs, forcedScanMs; public int patches, evaluations, colorWrites; }
    }
}
