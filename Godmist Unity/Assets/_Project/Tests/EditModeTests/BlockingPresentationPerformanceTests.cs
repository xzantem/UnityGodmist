using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Combat;
using Godmist.Views.Dungeons;
using Godmist.Views.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class BlockingPresentationPerformanceTests
    {
        [Test]
        public void LargeSelectableRangeMeasuresColdAndRepeatedMarkers()
        {
            var root = new GameObject("target marker benchmark");
            try
            {
                var view = root.AddComponent<CombatGridFeedbackRenderer>();
                var cells = Enumerable.Range(0, 32).SelectMany(x => Enumerable.Range(0, 32)
                    .Select(y => new GridPosition(x, y))).ToArray();
                var timer = Stopwatch.StartNew();
                view.ShowOutline(CombatGridFeedbackLayer.SkillTarget, cells, Color.red);
                var cold = timer.Elapsed.TotalMilliseconds;
                timer.Restart();
                for (var i = 0; i < 10; i++)
                    view.ShowOutline(CombatGridFeedbackLayer.SkillTarget, cells, Color.red);
                TestContext.WriteLine($"1024 selectable cells: cold={cold:F3}ms, 10 repeats={timer.Elapsed.TotalMilliseconds:F3}ms, sprite renderers={root.GetComponentsInChildren<SpriteRenderer>(true).Length}, mesh renderers={root.GetComponentsInChildren<MeshRenderer>(true).Length}");
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(1));
                Assert.That(root.GetComponentInChildren<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(4096));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(CombatGridFeedbackLayer.SkillTarget)]
        [TestCase(CombatGridFeedbackLayer.ChosenSkillTargets)]
        [TestCase(CombatGridFeedbackLayer.MovementPreview)]
        [TestCase(CombatGridFeedbackLayer.ActiveMovementUnit)]
        public void CornerBatchPreservesTextureMappingTintVisibilityAndLifetime(CombatGridFeedbackLayer layer)
        {
            var root = new GameObject("corner batch invalidation");
            Mesh mesh = null;
            Material material = null;
            try
            {
                var view = root.AddComponent<CombatGridFeedbackRenderer>();
                view.SetGridMapping(new Vector2(3, 4), 2f);
                var cells = new[] { new GridPosition(0, 0), new GridPosition(1, 0), new GridPosition(1, 0) };
                view.ShowOutline(layer, cells, Color.red);
                mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                var renderer = root.GetComponentInChildren<MeshRenderer>();
                material = renderer.sharedMaterial;
                Assert.That(mesh.vertexCount, Is.EqualTo(8), "Duplicate cells must not double opacity.");
                Assert.That(mesh.vertices[0], Is.EqualTo(new Vector3(2, 3, -.2f)));
                Assert.That(mesh.uv.Take(4), Is.EqualTo(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }));
                var sprite = (Sprite)typeof(CombatGridFeedbackRenderer).GetField("targetCornerSprite",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Assert.That(material.mainTexture, Is.SameAs(sprite.texture));
                Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                view.ClearOutline(layer);
                Assert.That(renderer.enabled, Is.False);
                view.ShowOutline(layer, cells, Color.red);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(root.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                view.IsPositionVisible = p => p.X == 1;
                view.RefreshVisibility();
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                Assert.That(mesh.vertices[0].x, Is.EqualTo(4));
                var tint = new Color(.2f, .6f, .9f, .4f);
                view.ShowOutline(layer, cells, Color.white, _ => tint);
                Assert.That(mesh.colors[0], Is.EqualTo(tint));
                tint = Color.green;
                view.RefreshVisibility();
                Assert.That(mesh.colors[0], Is.EqualTo(tint));
                view.SetGridMapping(Vector2.zero, 1f);
                Set(view, "feedbackZ", -.4f);
                view.ShowOutline(layer, cells, Color.blue);
                Assert.That(mesh.vertices[0], Is.EqualTo(new Vector3(.5f, -.5f, -.4f)));
                view.IsPositionVisible = _ => false;
                view.RefreshVisibility();
                Assert.That(renderer.enabled, Is.False);
                view.IsPositionVisible = null;
                view.RefreshVisibility();
                Assert.That(mesh.vertexCount, Is.EqualTo(8));
                Assert.That(renderer.enabled, Is.True);
                // EditMode does not dispatch lifecycle callbacks for this
                // non-ExecuteAlways behaviour; exercise the runtime callback.
                typeof(CombatGridFeedbackRenderer).GetMethod("OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
                Assert.That(renderer.enabled, Is.False);
                typeof(CombatGridFeedbackRenderer).GetMethod("OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            }
            finally { Object.DestroyImmediate(root); }
            Assert.That(mesh == null, Is.True, "Generated mesh must be released with the owner.");
            Assert.That(material == null, Is.True, "Generated material must be released with the owner.");
        }

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void CornerBatchPixelsMatchOriginalSpriteGeometry()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Requires a graphics device.");
            var root = new GameObject("corner pixel comparison");
            var target = new RenderTexture(128, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(128, 64, TextureFormat.RGBA32, false, true);
            var referenceMeshes = new List<Mesh>();
            var previous = RenderTexture.active;
            try
            {
                Assert.That(target.Create(), Is.True);
                var view = root.AddComponent<CombatGridFeedbackRenderer>();
                var cells = new[] { new GridPosition(0, 0), new GridPosition(1, 0) };
                Color Tint(GridPosition p) => p.X == 0 ? new Color(.3f, .8f, 1f, .7f) : Color.red;
                view.ShowOutline(CombatGridFeedbackLayer.SkillTarget, cells, Color.white, Tint);
                var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                var material = root.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                var sprite = (Sprite)typeof(CombatGridFeedbackRenderer).GetField("targetCornerSprite",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                Color32[] Read(bool batch)
                {
                    using var commands = new UnityEngine.Rendering.CommandBuffer();
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(false, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity,
                        GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1, 3, -1, 1, -10, 10), true));
                    if (batch) commands.DrawMesh(mesh, Matrix4x4.identity, material);
                    else foreach (var cell in cells)
                    {
                        // Use Unity's original Sprite geometry/UVs as reference,
                        // submitted separately with its original cell transform.
                        var reference = new Mesh
                        {
                            vertices = sprite.vertices.Select(v => (Vector3)v).ToArray(),
                            uv = sprite.uv,
                            triangles = sprite.triangles.Select(i => (int)i).ToArray(),
                            colors = Enumerable.Repeat(Tint(cell), sprite.vertices.Length).ToArray()
                        };
                        referenceMeshes.Add(reference);
                        commands.DrawMesh(reference, Matrix4x4.Translate(new Vector3(cell.X, cell.Y, -.2f)), material);
                    }
                    Graphics.ExecuteCommandBuffer(commands);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 128, 64), 0, 0); pixels.Apply();
                    return pixels.GetPixels32();
                }
                var actual = Read(true);
                var expected = Read(false);
                Assert.That(actual.Any(p => p.a > 0), Is.True, "Comparison must contain rendered corners.");
                Assert.That(actual, Is.EqualTo(expected), "Batch must preserve sprite tint, opacity and pixel geometry.");
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (var mesh in referenceMeshes) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [Test]
        public void BattleLogBurstMeasuresForcedGlobalLayouts()
        {
            var root = new GameObject("log", typeof(RectTransform), typeof(Canvas), typeof(ScrollRect));
            var layouts = 0;
            void Count() => layouts++;
            try
            {
                var log = root.AddComponent<CombatBattleLog>();
                var content = new GameObject("content", typeof(RectTransform));
                content.transform.SetParent(root.transform);
                root.GetComponent<ScrollRect>().content = content.GetComponent<RectTransform>();
                Set(log, "scrollRect", root.GetComponent<ScrollRect>());
                var textObject = new GameObject("log text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                textObject.transform.SetParent(content.transform);
                var text = textObject.GetComponent<TMPro.TextMeshProUGUI>();
                Set(log, "logText", text);
                Canvas.willRenderCanvases += Count;
                var timer = Stopwatch.StartNew();
                for (var i = 0; i < 100; i++) log.AddEntry("damage " + i);
                TestContext.WriteLine($"100 battle log lines: {timer.Elapsed.TotalMilliseconds:F2} ms, forced canvas passes={layouts}");
                Assert.That(layouts, Is.Zero, "Combat state changes must not force global canvas/layout work.");
                Assert.That(((List<string>)typeof(CombatBattleLog).GetField("entries",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(log)).Count, Is.EqualTo(100));
                typeof(CombatBattleLog).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(log, null);
                Assert.That(text.text, Does.StartWith("damage 0\n\ndamage 1"));
                Assert.That(text.text, Does.EndWith("damage 99"));
                Canvas.ForceUpdateCanvases();
                Assert.That(layouts, Is.EqualTo(1), "The ordinary canvas pass must be enough to flush the log.");
                Assert.That((bool)typeof(CombatBattleLog).GetField("scrollPending",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(log), Is.False);
                Set(log, "maximumEntries", 2);
                log.ClearLog(); log.AddEntry("first"); log.AddEntry("second"); log.AddEntry("third");
                Assert.That((List<string>)typeof(CombatBattleLog).GetField("entries",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(log), Is.EqualTo(new[] { "second", "third" }));
            }
            finally { Canvas.willRenderCanvases -= Count; Object.DestroyImmediate(root); }
        }

        [Test]
        public void RepeatedFogAndColdHighlightsMeasurePresentationWork()
        {
            var root = new GameObject("fog perf", typeof(Grid));
            var tiles = new GameObject("tiles", typeof(Tilemap), typeof(TilemapRenderer));
            var wall = new GameObject("wall template", typeof(DungeonWallSegmentView), typeof(SpriteRenderer));
            var feedbackObject = new GameObject("feedback");
            var tile = ScriptableObject.CreateInstance<Tile>();
            try
            {
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                Set(view, "floorTilemap", tiles.GetComponent<Tilemap>());
                Set(view, "floorTile", tile);
                Set(view, "wallSegmentPrefab", wall.GetComponent<DungeonWallSegmentView>());
                var floor = new DungeonFloorInstance(0);
                var map = new DungeonFloorMap();
                for (var x = 0; x < 40; x++) for (var y = 0; y < 40; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                floor.TrySetMap(map);
                view.Rebuild(map);
                var visible = map.Surfaces.Where(p => p.Cell.x < 20).Select(p => new DungeonTraversalPosition(p)).ToArray();
                floor.Fog.SetVisible(visible); view.ApplyFog(floor);
                var fogMesh = (Mesh)typeof(DungeonFloorMapView).GetField("fogBlendMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var fogVertices = fogMesh.vertices;
                var timer = Stopwatch.StartNew();
                for (var i = 0; i < 10; i++) view.ApplyFog(floor);
                TestContext.WriteLine($"10 unchanged fog refreshes, 1600 cells: {timer.Elapsed.TotalMilliseconds:F2} ms");
                Assert.That(fogMesh.vertices, Is.EqualTo(fogVertices));
                timer.Restart();
                for (var i = 0; i < 10; i++)
                {
                    floor.Fog.SetVisible(map.Surfaces.Where(p => p.Cell.x < 20 + i % 2)
                        .Select(p => new DungeonTraversalPosition(p)));
                    view.ApplyFog(floor);
                }
                TestContext.WriteLine($"10 changed fog refreshes, 1600 cells: {timer.Elapsed.TotalMilliseconds:F2} ms");
                Assert.That(view.IsCurrentlySeen(new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero))), Is.True);
                floor.Fog.SetVisible(map.Surfaces.Select(p => new DungeonTraversalPosition(p)));
                view.ApplyFog(floor);
                Assert.That(fogMesh.vertexCount, Is.Zero, "No darker neighbour means no fog boundary.");
                floor.Fog.SetVisible(visible); view.ApplyFog(floor);
                Assert.That(fogMesh.vertexCount, Is.GreaterThan(0), "A boundary must return after the footprint changes.");
                timer.Restart();
                var feedback = feedbackObject.AddComponent<CombatGridFeedbackRenderer>();
                TestContext.WriteLine($"Highlight initialization (includes atlas preload): {timer.Elapsed.TotalMilliseconds:F2} ms");
                var cells = Enumerable.Range(0, 32).SelectMany(x => Enumerable.Range(0, 32).Select(y => new GridPosition(x, y))).ToArray();
                timer.Restart();
                feedback.ShowOutline(CombatGridFeedbackLayer.SkillRange, cells, Color.white);
                feedback.ShowFill(CombatGridFeedbackLayer.SkillRange, cells, Color.white);
                TestContext.WriteLine($"Cold 1024-cell range outline+fill: {timer.Elapsed.TotalMilliseconds:F2} ms");
                timer.Restart();
                for (var i = 0; i < 10; i++)
                {
                    feedback.ShowOutline(CombatGridFeedbackLayer.SkillRange, cells, Color.white);
                    feedback.ShowFill(CombatGridFeedbackLayer.SkillRange, cells, Color.white);
                }
                TestContext.WriteLine($"10 repeated 1024-cell range outline+fill: {timer.Elapsed.TotalMilliseconds:F2} ms");
            }
            finally { Object.DestroyImmediate(feedbackObject); Object.DestroyImmediate(root); Object.DestroyImmediate(wall); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void ReusedHighlightStillUpdatesTintVisibilityGeometryAndDynamicColors()
        {
            var root = new GameObject("highlight invalidation");
            try
            {
                var view = root.AddComponent<CombatGridFeedbackRenderer>();
                var cells = new[] { new GridPosition(0, 0), new GridPosition(1, 0) };
                view.ShowFill(CombatGridFeedbackLayer.SkillRange, cells, Color.red);
                var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                var before = mesh.vertices;
                view.ClearFill(CombatGridFeedbackLayer.SkillRange);
                view.ShowFill(CombatGridFeedbackLayer.SkillRange, cells, Color.red);
                Assert.That(root.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(root.GetComponentInChildren<MeshRenderer>().enabled, Is.True);
                view.ShowFill(CombatGridFeedbackLayer.SkillRange, cells, Color.blue);
                Assert.That(mesh.colors[0].b, Is.EqualTo(1f));
                Assert.That(mesh.colors[0].r, Is.Zero);
                Set(view, "gridOrigin", new Vector2(3, 4));
                view.ShowFill(CombatGridFeedbackLayer.SkillRange, cells, Color.blue);
                Assert.That(mesh.vertices[0], Is.Not.EqualTo(before[0]));
                view.IsPositionVisible = p => p.X == 0;
                view.RefreshVisibility();
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                var tint = Color.red;
                Color Dynamic(GridPosition p) => tint;
                view.ShowFill(CombatGridFeedbackLayer.SkillRange, cells, Color.white, Dynamic);
                tint = Color.green;
                view.ShowFill(CombatGridFeedbackLayer.SkillRange, cells, Color.white, Dynamic);
                Assert.That(mesh.colors[0].g, Is.EqualTo(1f));
                Assert.That(mesh.colors[0].r, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
