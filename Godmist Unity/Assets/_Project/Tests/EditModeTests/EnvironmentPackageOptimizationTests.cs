using System;
using System.Collections;
using System.Collections.Generic;
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
    public class EnvironmentPackageOptimizationTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Flags).GetValue(owner);
        static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags).SetValue(owner, value);
        static T Bind<T>(object owner, string method) where T : Delegate => (T)owner.GetType().GetMethod(method, Flags).CreateDelegate(typeof(T), owner);

        [TestCase(0f)] [TestCase(1f)]
        public void UnitProjectionMatchesNativeAtPixelEdgesAndRejectsOtherGridGeometry(float depth)
        {
            var root = new GameObject("Unit projection", typeof(Grid));
            var tiles = new GameObject("Tiles", typeof(Tilemap)); tiles.transform.SetParent(root.transform, false);
            var view = root.AddComponent<DungeonFloorMapView>(); var grid = root.GetComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, depth);
            Set(view, "floorTilemap", tiles.GetComponent<Tilemap>());
            var contactType = typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows");
            var contact = Activator.CreateInstance(contactType, new object[] { root.transform, Shader.Find("Sprites/Default") });
            Set(contact, "terrain", view); var project = Bind<Func<Vector2, Vector2Int>>(contact, "ProjectCorner");
            try
            {
                Assert.That(view.HasUnitWorldCellProjection(), Is.True);
                Set(contact, "unitWorldCellProjection", true);
                for (int y = -40; y <= 40; y++) for (int x = -40; x <= 40; x++)
                    foreach (float delta in new[] { -.0001f, 0, .0001f, 1f / 32 })
                    {
                        var point = new Vector2(x + delta, y - delta);
                        Assert.That(project(point), Is.EqualTo(view.WorldToCell(point)));
                    }
                void Fallback()
                {
                    Assert.That(view.HasUnitWorldCellProjection(), Is.False);
                    Set(contact, "unitWorldCellProjection", false);
                    foreach (var point in new[] { new Vector2(-2.01f, .9999f), new Vector2(3.2f, -7.1f) })
                        Assert.That(project(point), Is.EqualTo(view.WorldToCell(point)));
                }
                root.transform.position = new Vector3(.25f, -4, 0); Fallback(); root.transform.position = Vector3.zero;
                root.transform.localScale = new Vector3(2, 1, 1); Fallback(); root.transform.localScale = Vector3.one;
                root.transform.rotation = Quaternion.Euler(0, 0, 45); Fallback(); root.transform.rotation = Quaternion.identity;
                tiles.transform.localPosition = Vector3.right; Fallback(); tiles.transform.localPosition = Vector3.zero;
                grid.cellSize = new Vector3(2, 1, 1); Fallback(); grid.cellSize = Vector3.one;
                grid.cellGap = new Vector3(.1f, 0, 0); Fallback(); grid.cellGap = Vector3.zero;
                grid.cellSwizzle = GridLayout.CellSwizzle.YXZ; Fallback(); grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
                tiles.GetComponent<Tilemap>().tileAnchor = new Vector3(.25f, .5f, 0); Fallback();
                tiles.GetComponent<Tilemap>().tileAnchor = new Vector3(.5f, .5f, 0);
                grid.cellLayout = GridLayout.CellLayout.Isometric; Fallback();
                grid.cellLayout = GridLayout.CellLayout.Hexagon; Fallback();
                grid.cellLayout = GridLayout.CellLayout.Rectangle;
                Assert.That(view.HasUnitWorldCellProjection(), Is.True, "The guard must be re-evaluated after live edits.");
            }
            finally { Bind<Action>(contact, "Dispose")(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void ContactCellStateMatchesNativeAcrossFogLevelsAnchorsAndTransforms()
        {
            var root = new GameObject("Contact cell visibility", typeof(Grid));
            var tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(root.transform, false);
            var view = root.AddComponent<DungeonFloorMapView>(); var tile = ScriptableObject.CreateInstance<Tile>();
            Set(view, "floorTilemap", tiles.GetComponent<Tilemap>()); Set(view, "floorTile", tile);
            var map = new DungeonFloorMap(); var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
            for (int x = -2; x <= 2; x++) foreach (int level in new[] { 0, 2 }) map.TryAddFloorCell(new DungeonSurfacePosition(new Vector2Int(x, 0), level), "r");
            Set(view, "boundMap", map); Read<DungeonElevationViewState>(view, "elevation").Bind(map);
            var type = typeof(DungeonSilhouetteLighting).Assembly.GetType("Godmist.Views.Lighting.DungeonContactShadows");
            var contact = Activator.CreateInstance(type, new object[] { root.transform, Shader.Find("Sprites/Default") });
            Set(contact, "terrain", view);
            var state = Bind<Func<Vector2Int, (Vector3 Center, bool Visible)>>(contact, "ReadCellState");
            try
            {
                void Compare()
                {
                    Set(contact, "unitWorldCellProjection", view.HasUnitWorldCellProjection());
                    for (int x = -3; x <= 3; x++)
                    {
                        var cell = new Vector2Int(x, 0); var center = view.CellToWorldCenter(cell);
                        bool visible = view.TryGetVisibleTraversal(center, out var position) && view.IsTraversalVisible(position);
                        Assert.That(state(cell), Is.EqualTo(((Vector3)center, visible)));
                    }
                }
                floor.Fog.SetVisible(map.Surfaces.Select(s => new DungeonTraversalPosition(s))); view.ApplyFog(floor); Compare();
                view.FocusElevation(new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero, 2)), true); Compare();
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); view.ApplyFog(floor); Compare();
                floor.Fog.ToggleDebugFog(); view.ApplyFog(floor); Compare();
                tiles.GetComponent<Tilemap>().tileAnchor = new Vector3(2.25f, .5f, 0); Compare();
                root.transform.localScale = new Vector3(2, .75f, 1); Compare();
                root.transform.position = new Vector3(-3.25f, .4f); Compare();
            }
            finally { Bind<Action>(contact, "Dispose")(); Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void LightingBoundsSnapshotEndsAtRefreshBoundaryAndOtherCallersStayLive()
        {
            var root = new GameObject("Bounds lifetime"); var source = new GameObject("Source", typeof(SpriteRenderer));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            try
            {
                var renderer = source.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
                var rig = root.AddComponent<DungeonSilhouetteLighting>(); var read = Bind<Func<SpriteRenderer, Bounds>>(rig, "ReadCasterBounds");
                var refresh = Bind<Action<float>>(rig, "RefreshLighting");
                Set(rig, "receiverBoundsBatch", true); var first = read(renderer); int count = rig.ReceiverBoundsReadCount;
                Assert.That(read(renderer), Is.EqualTo(first)); Assert.That(rig.ReceiverBoundsReadCount, Is.EqualTo(count));
                // Even an early exit releases the batch and its retained bounds.
                refresh(0); Assert.That(Read<IDictionary>(rig, "preparedReceiverBounds").Count, Is.Zero);
                Assert.That(Read<bool>(rig, "receiverBoundsBatch"), Is.False);
                renderer.transform.position += Vector3.right * 3;
                Assert.That(read(renderer), Is.EqualTo(renderer.bounds)); Assert.That(read(renderer), Is.Not.EqualTo(first));
                renderer.transform.localScale = new Vector3(2, .5f, 1);
                Assert.That(read(renderer), Is.EqualTo(renderer.bounds));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(source); Object.DestroyImmediate(sprite); }
        }

        [Test]
        public void FogColorRetargetKeepsSlotsGeometryAndUploadsExactColors()
        {
            var type = typeof(LightFogMapOptimizationTests).GetNestedType("FogFixture", BindingFlags.NonPublic);
            using var fixture = (IDisposable)Activator.CreateInstance(type, new object[] { 14 });
            var view = Read<DungeonFloorMapView>(fixture, "View");
            Bind<Action<int, bool>>(fixture, "Project")(0, false); Read<Action<float>>(fixture, "Tick")(.4f);
            var projection = Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "fogProjection");
            var previous = Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "previousFogProjection");
            previous.Clear(); foreach (var pair in projection) previous.Add(pair.Key, pair.Value);
            var slots = Read<Dictionary<Vector2Int, int>>(view, "fogMeshSlots"); Assert.That(slots.Count, Is.GreaterThan(0));
            var savedSlots = new Dictionary<Vector2Int, int>(slots);
            var mesh = Read<Mesh>(view, "fogBlendMesh"); var vertices = mesh.vertices; var indices = mesh.triangles;
            var cells = Read<Dictionary<Vector3Int, (TileBase Tile, Color Color)>>(view, "presentedFloorCells");
            foreach (var cell in savedSlots.Keys)
            {
                var key = new Vector3Int(cell.x, cell.y, 0); var current = cells[key]; cells[key] = (current.Tile, current.Color * .8f);
            }
            var tiles = Read<Tilemap>(fixture, "Tiles"); tiles.GetComponent<TilemapRenderer>().sortingOrder = 17;
            Read<MeshRenderer>(view, "fogBlendRenderer").enabled = false;
            int uploads = view.FogGeometryVertexUploadCount, colorOnly = view.FogColorOnlyRefreshCount;
            Read<Action>(fixture, "Refresh")();
            Assert.That(view.FogColorOnlyRefreshCount, Is.EqualTo(colorOnly + 1));
            Assert.That(view.FogGeometryVertexUploadCount, Is.EqualTo(uploads));
            CollectionAssert.AreEqual(vertices, mesh.vertices); CollectionAssert.AreEqual(indices, mesh.triangles);
            foreach (var pair in savedSlots) Assert.That(slots[pair.Key], Is.EqualTo(pair.Value));
            CollectionAssert.AreEqual(Read<List<Color>>(view, "fogColors"), mesh.colors);
            Assert.That(Read<MeshRenderer>(view, "fogBlendRenderer").sortingOrder, Is.EqualTo(18));
            Assert.That(Read<MeshRenderer>(view, "fogBlendRenderer").enabled, Is.True);
            Read<Action<float>>(fixture, "Tick")(.1f);
            CollectionAssert.AreEqual(Read<List<Color>>(view, "fogColors"), mesh.colors);
        }

        [Test]
        public void PatrolPoseSnapshotMatchesLiveFlatAndLayeredPositions()
        {
            var root = new GameObject("Patrol pose", typeof(Grid)); var tiles = new GameObject("Tiles", typeof(Tilemap)); tiles.transform.SetParent(root.transform, false);
            var actorObject = new GameObject("Actor", typeof(Rigidbody2D)); actorObject.transform.SetParent(root.transform, false);
            var actor = actorObject.AddComponent<ExplorationCharacterView>(); var view = root.AddComponent<DungeonFloorMapView>();
            Set(view, "floorTilemap", tiles.GetComponent<Tilemap>());
            try
            {
                void Compare()
                { actor.ReadTraversalPosition(out var world, out var position); Assert.That(world, Is.EqualTo(actor.Position)); Assert.That(position, Is.EqualTo(actor.DungeonPosition)); }
                actor.SetPosition(new Vector2(-.2f, 3.75f)); Compare(); // city fallback
                var map = new DungeonFloorMap(); map.TryAddFloorCell(Vector2Int.zero, "r"); actor.ConfigureTerrain(map, view); Compare();
                actor.SetPosition(new Vector2(2.1f, -1.1f)); Compare();
                var upper = new DungeonSurfacePosition(Vector2Int.zero, 2); map.TryAddFloorCell(upper, "upper");
                Set(actor, "localTraversalPosition", new DungeonTraversalPosition(upper));
                actorObject.transform.position = new Vector3(3.7f, 4.2f); Compare();
                actor.ReadTraversalPosition(out _, out var logical); Assert.That(logical.Surface, Is.EqualTo(upper));
                actorObject.transform.position = new Vector3(8.7f, 9.2f); Compare();
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
