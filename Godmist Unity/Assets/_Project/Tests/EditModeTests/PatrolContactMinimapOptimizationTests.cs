using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class PatrolContactMinimapOptimizationTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
        static T Bind<T>(object owner, string name) where T : Delegate => owner.GetType().GetMethod(name, Flags).CreateDelegate(typeof(T), owner) as T;
        delegate void Footprint(out Vector2 offset, out float radius);

        [Test]
        public void MovementFootprintMatchesAuthoredGeometryAndObservesEveryLaterMutation()
        {
            var root = new GameObject("Footprint parent");
            try
            {
                var actorObject = new GameObject("Actor"); actorObject.transform.SetParent(root.transform);
                var actor = actorObject.AddComponent<ExplorationCharacterView>();
                var child = new GameObject("Authored collider"); child.transform.SetParent(actorObject.transform);
                var circle = child.AddComponent<CircleCollider2D>(); Set(actor, "movementCollider", circle);
                var read = Bind<Footprint>(actor, "ReadMovementFootprint");
                for (int i = 0; i < 12; i++)
                {
                    root.transform.position = new Vector3(i * .3f, -i * .1f);
                    root.transform.localScale = new Vector3(i % 2 == 0 ? .65f : -1.3f, .8f + i * .07f, 1);
                    actorObject.transform.localPosition = new Vector3(.2f * i, -.3f);
                    child.transform.localPosition = new Vector3(.1f, .2f * i);
                    child.transform.localEulerAngles = Vector3.forward * (i * 7);
                    circle.radius = .1f + i * .015f; circle.offset = new Vector2(.1f, -.02f * i); circle.enabled = i % 2 == 0;
                    Physics2D.SyncTransforms();
                    read(out var offset, out var radius);
                    Assert.That(offset, Is.EqualTo(actor.MovementColliderCenterOffset));
                    Assert.That(radius, Is.EqualTo(actor.MovementRadius));
                }
                var box = child.AddComponent<BoxCollider2D>(); Set(actor, "movementCollider", box); Physics2D.SyncTransforms();
                read(out var boxOffset, out var boxRadius);
                Assert.That(boxOffset, Is.EqualTo(actor.MovementColliderCenterOffset)); Assert.That(boxRadius, Is.EqualTo(.2f));
                Set(actor, "movementCollider", null); read(out boxOffset, out boxRadius);
                Assert.That(boxOffset, Is.EqualTo(Vector2.zero)); Assert.That(boxRadius, Is.EqualTo(.2f));
            }
            finally { Object.DestroyImmediate(root); Physics2D.SyncTransforms(); }
        }

        [Test]
        public void RasterRowFillsMatchScalarPixelsIncludingClippingAndOverlays()
        {
            var method = typeof(DungeonMinimapView).GetMethod("Fill", BindingFlags.NonPublic | BindingFlags.Instance);
            var owner=new GameObject("Raster size context");
            var view=owner.AddComponent<DungeonMinimapView>();
            try
            {
            var fill = (Action<Color32[], int, int, int, int, Color32>)method.CreateDelegate(typeof(Action<Color32[], int, int, int, int, Color32>),view);
            var expected = new Color32[252 * 252]; var actual = new Color32[expected.Length];
            var random = new System.Random(181026);
            for (int i = 0; i < 64; i++)
            {
                int x = random.Next(-40, 280), y = random.Next(-40, 280), width = random.Next(-3, 300), height = random.Next(-3, 300);
                var color = new Color32((byte)i, (byte)(255 - i), (byte)(i * 3), (byte)(i * 2));
                for (int row = Mathf.Max(0, y); row < Mathf.Min(252, y + height); row++)
                for (int col = Mathf.Max(0, x); col < Mathf.Min(252, x + width); col++) expected[row * 252 + col] = color;
                fill(actual, x, y, width, height, color);
                for (int pixel = 0; pixel < expected.Length; pixel++)
                {
                    var a = expected[pixel]; var b = actual[pixel];
                    if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a)
                        Assert.Fail("Raster/overlay step " + i + " differs at pixel " + pixel);
                }
            }
            }
            finally {UnityEngine.Object.DestroyImmediate(owner);}
        }

        [TestCase(GridLayout.CellLayout.Rectangle)]
        [TestCase(GridLayout.CellLayout.Isometric)]
        [TestCase(GridLayout.CellLayout.Hexagon)]
        public void SharedMinimapBasisPreservesOriginalProjectionAcrossTransforms(GridLayout.CellLayout layout)
        {
            var root = new GameObject("Minimap basis", typeof(Grid));
            try
            {
                var grid = root.GetComponent<Grid>(); grid.cellLayout = layout; grid.cellSize = new Vector3(.8f, 1.3f, 1);
                var tiles = new GameObject("Tiles", typeof(Tilemap)); tiles.transform.SetParent(root.transform);
                var map = root.AddComponent<DungeonFloorMapView>(); Set(map, "floorTilemap", tiles.GetComponent<Tilemap>());
                var minimap = root.AddComponent<DungeonMinimapView>(); Set(minimap, "map", map);
                var basis = Bind<Func<(Vector2, Vector2)>>(minimap, "ReadCellBasis");
                var project = Bind<Func<Vector3, Vector2Int, (Vector2, Vector2), Vector2>>(minimap, "ProjectContinuousCell");
                for (int pass = 0; pass < 4; pass++)
                {
                    root.transform.position = new Vector3(pass * .3f, -.2f * pass);
                    root.transform.localEulerAngles = Vector3.forward * (pass * 13);
                    root.transform.localScale = new Vector3(pass % 2 == 0 ? .7f : -1.1f, 1.4f, 1);
                    var shared = basis();
                    for (int i = 0; i < 40; i++)
                    {
                        var world = new Vector3(-5.3f + i * .37f, 3.1f - i * .19f);
                        var cell = map.WorldToCell(world);
                        var relative = (Vector2)world - map.CellToWorldCenter(cell);
                        var origin = map.CellToWorldCenter(Vector2Int.zero);
                        var dx = map.CellToWorldCenter(Vector2Int.right) - origin;
                        var dy = map.CellToWorldCenter(Vector2Int.up) - origin;
                        var expected = cell + new Vector2(dx.sqrMagnitude > .0001f ? Vector2.Dot(relative, dx) / dx.sqrMagnitude : 0,
                            dy.sqrMagnitude > .0001f ? Vector2.Dot(relative, dy) / dy.sqrMagnitude : 0);
                        Assert.That(project(world, cell, shared), Is.EqualTo(expected));
                    }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MarkersSkipOnlyExactlyUnchangedInputsAndStillReactToPositionColorSizeAndRemoval()
        {
            var root = new GameObject("Markers", typeof(RectTransform), typeof(CanvasRenderer), typeof(DungeonMinimapMarkers));
            try
            {
                var markers = root.GetComponent<DungeonMinimapMarkers>(); int dirty = 0;
                markers.RegisterDirtyVerticesCallback(() => dirty++);
                void Frame(Vector2 position, Color color) { markers.ClearMarkers(); markers.AddMarker(position, color); markers.Refresh(); }
                Frame(Vector2.zero, Color.red); Assert.That(dirty, Is.EqualTo(1));
                for (int i = 0; i < 50; i++) Frame(Vector2.zero, Color.red);
                Assert.That(dirty, Is.EqualTo(1));
                Frame(new Vector2(.0000001f, 0), Color.red); Assert.That(dirty, Is.EqualTo(2), "Compare exact coordinates, not approximate Vector2 equality.");
                Frame(Vector2.zero, Color.blue); Assert.That(dirty, Is.EqualTo(3));
                Set(markers, "markerSize", 7f); Frame(Vector2.zero, Color.blue); Assert.That(dirty, Is.EqualTo(4));
                markers.ClearMarkers(); markers.Refresh(); Assert.That(dirty, Is.EqualTo(5));
                markers.ClearMarkers(); markers.Refresh(); Assert.That(dirty, Is.EqualTo(5));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ContactReuseMatchesColdMeshAndGpuPixelsThroughLocalAndUnrelatedFogChanges()
        {
            var type = typeof(ShadowContactOptimizationTests).GetNestedType("ContactFixture", BindingFlags.NonPublic);
            using var fixture = (IDisposable)Activator.CreateInstance(type, true);
            var root = Read<GameObject>(fixture, "Root"); var view = Read<DungeonFloorMapView>(fixture, "View");
            var floor = Read<DungeonFloorInstance>(fixture, "Floor"); var contact = Read<object>(fixture, "Contact");
            var rebuild = Bind<Action<DungeonFloorMapView, Renderer, Rect, IEnumerable<DungeonObjectView>, IEnumerable<SpriteRenderer>, Sprite, IEnumerable<SpriteRenderer>>>(contact, "Rebuild");
            var texture = new Texture2D(16, 16); var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * .5f, 16);
            var prop = new GameObject("Contact prop", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>(); prop.transform.SetParent(root.transform); prop.sprite = sprite; prop.transform.position = new Vector3(2.5f, 2.5f);
            // Twelve world units at the authored 32 pixels/unit: a one-pixel
            // contact band must cover actual samples in the positive control.
            const int resolution = 384;
            var target = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create();
            var pixels = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            using var commands = new CommandBuffer();
            var viewport = new Rect(-1, -1, 12, 12);
            var visible = floor.Map.FloorCells.Select(cell => new DungeonTraversalPosition(new DungeonSurfacePosition(cell))).ToArray();
            var floorRenderer = root.GetComponentInChildren<TilemapRenderer>();
            void Rebuild() => rebuild(view, floorRenderer, viewport, Array.Empty<DungeonObjectView>(), Array.Empty<SpriteRenderer>(), null, new[] { prop });
            byte[] Pixels()
            {
                commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(false, true, Color.white);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1, 11, -1, 11, -1, 1), true));
                var mesh = Read<Mesh>(contact, "mesh"); if (mesh.vertexCount > 0) commands.DrawMesh(mesh, Matrix4x4.identity, Read<Material>(contact, "material"));
                Graphics.ExecuteCommandBuffer(commands); var previous = RenderTexture.active;
                try { RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0); pixels.Apply(); return pixels.GetRawTextureData<byte>().ToArray(); }
                finally { RenderTexture.active = previous; }
            }
            void CompareCold()
            {
                var mesh = Read<Mesh>(contact, "mesh"); var vertices = mesh.vertices; var colors = mesh.colors32; var triangles = mesh.triangles; var before = Pixels();
                var wallQuads = (int)contact.GetType().GetProperty("WallQuadCount").GetValue(contact);
                Set(contact, "previousMap", null); Rebuild();
                CollectionAssert.AreEqual(vertices, mesh.vertices); CollectionAssert.AreEqual(colors, mesh.colors32); CollectionAssert.AreEqual(triangles, mesh.triangles);
                Assert.That(before.SequenceEqual(Pixels()), Is.True, "Cached and cold contact pixels must match exactly.");
                Assert.That((int)contact.GetType().GetProperty("WallQuadCount").GetValue(contact), Is.EqualTo(wallQuads));
            }
            try
            {
                floor.Fog.SetVisible(visible); view.ApplyFog(floor); Rebuild();
                Assert.That(Read<Mesh>(contact, "mesh").vertexCount, Is.GreaterThan(0));
                Assert.That(Pixels().Where((_, i) => i % 4 != 3).Any(value => value < 255), Is.True, "GPU comparison requires visible contact pixels.");
                int builds = (int)contact.GetType().GetProperty("RebuildCount").GetValue(contact);
                floor.Fog.SetVisible(visible.Where(p => p.Surface.Cell != new Vector2Int(7, 7)).ToArray()); view.ApplyFog(floor); Rebuild();
                Assert.That((int)contact.GetType().GetProperty("RebuildCount").GetValue(contact), Is.EqualTo(builds));
                Assert.That((int)contact.GetType().GetProperty("ContactVisibilityReuseCount").GetValue(contact), Is.GreaterThan(0)); CompareCold();
                foreach (var seen in new[] { Array.Empty<DungeonTraversalPosition>(), visible })
                { floor.Fog.SetVisible(seen); view.ApplyFog(floor); Rebuild(); CompareCold(); }
                prop.transform.position += Vector3.right; Rebuild(); CompareCold();
                prop.transform.localScale = new Vector3(2, 1, 1); Rebuild(); CompareCold();
                prop.forceRenderingOff = true; Rebuild(); CompareCold();
                prop.forceRenderingOff = false; Rebuild(); CompareCold();
                floor.Map.TryAddFloorCell(new Vector2Int(8, 2), "room"); view.ApplyFog(floor); Rebuild(); CompareCold();
                root.transform.position += Vector3.right; view.ApplyFog(floor); Rebuild(); CompareCold();
            }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
        }
    }
}
