using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public class EnvironmentRegressionOptimizationTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Flags).GetValue(owner);
        static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags).SetValue(owner, value);
        static T Bind<T>(object owner, string method) where T : Delegate => (T)owner.GetType().GetMethod(method, Flags).CreateDelegate(typeof(T), owner);

        sealed class ForestFixture : IDisposable
        {
            public readonly GameObject Root = new("Forest fog ownership", typeof(Grid));
            public readonly DungeonFloorMapView View;
            public readonly DungeonFloorMap Map = new();
            public readonly DungeonFloorInstance Floor = new(0);
            readonly Tile tile = ScriptableObject.CreateInstance<Tile>();
            public ForestFixture()
            {
                var tiles = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(Root.transform, false);
                View = Root.AddComponent<DungeonFloorMapView>(); Set(View, "floorTilemap", tiles.GetComponent<Tilemap>()); Set(View, "floorTile", tile);
                Set(View, "wallSegmentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                View.ConfigureVegetationPresentation(AssetDatabase.LoadAssetAtPath<DungeonVegetationPresentation>("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset"));
                for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++) Map.TryAddFloorCell(new Vector2Int(x, y), "room");
                foreach (var cell in Map.FloorCells) foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                    if (!Map.IsWalkable(cell + direction)) Map.TryAddWall(cell, cell + direction);
                Floor.TrySetMap(Map); View.Rebuild(Map);
                Floor.Fog.SetVisible(Map.Surfaces.Select(s => new DungeonTraversalPosition(s))); View.ApplyFog(Floor);
            }
            public void Dispose() { Object.DestroyImmediate(Root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void UnknownBoundariesSkipElevationWhileKnownBoundariesAndPublicQueriesRemainLive()
        {
            using var fixture = new ForestFixture(); var view = fixture.View;
            fixture.Floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            fixture.Floor.Fog.SetMapDiscovered(false);
            int queries = view.FogElevationQueryCount; view.ApplyFog(fixture.Floor);
            Assert.That(view.FogElevationQueryCount, Is.EqualTo(queries), "Unknown boundaries cannot become visible by an elevation query.");
            var walls = Read<List<(DungeonSurfaceEdge Edge, DungeonWallSegmentView View, Renderer[] Renderers)>>(view, "wallRenderers");
            Assert.That(walls.SelectMany(w => w.Renderers).All(r => r.forceRenderingOff), Is.True);
            fixture.Floor.Fog.SetVisible(fixture.Map.Surfaces.Select(s => new DungeonTraversalPosition(s))); view.ApplyFog(fixture.Floor);
            Assert.That(view.FogElevationQueryCount, Is.GreaterThan(queries));
            var elevation = Read<DungeonElevationViewState>(view, "elevation");
            foreach (var wall in walls) foreach (var renderer in wall.Renderers)
                Assert.That(renderer.forceRenderingOff, Is.EqualTo(!elevation.IsVisible(wall.Edge)));
            var edge = new DungeonSurfaceEdge(new DungeonCellEdge(Vector2Int.zero, Vector2Int.right));
            fixture.Floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>());
            Assert.That(view.EdgeKnowledge(edge), Is.EqualTo(DungeonKnowledge.Remembered));
            fixture.Floor.Fog.SetMapDiscovered(false);
            Assert.That(view.EdgeKnowledge(edge), Is.EqualTo(DungeonKnowledge.Unexplored), "Public calls after the refresh must remain live.");
        }

        [Test]
        public void GeometryIndexReusesDebugChangesAndMatchesOriginalMembershipAfterTopologyAndRebuild()
        {
            using var fixture = new ForestFixture(); var view = fixture.View;
            void Compare()
            {
                var expectedWalls = new Dictionary<Vector2Int, List<int>>(); var expectedGround = new Dictionary<Vector2Int, List<DungeonSurfaceEdge>>();
                var walls = Read<List<(DungeonSurfaceEdge Edge, DungeonWallSegmentView View, Renderer[] Renderers)>>(view, "wallRenderers");
                void Wall(Vector2Int cell, int index) { if (!expectedWalls.TryGetValue(cell, out var list)) expectedWalls.Add(cell, list = new()); list.Add(index); }
                void Ground(Vector2Int cell, DungeonSurfaceEdge edge) { if (!expectedGround.TryGetValue(cell, out var list)) expectedGround.Add(cell, list = new()); list.Add(edge); }
                for (int i = 0; i < walls.Count; i++) { Wall(walls[i].Edge.First.Cell, i); Wall(walls[i].Edge.Second.Cell, i); }
                foreach (var edge in Read<Dictionary<DungeonSurfaceEdge, List<int>>>(view, "vegetationGroundByEdge").Keys)
                { Ground(edge.First.Cell, edge); Ground(edge.Second.Cell, edge); }
                var actualWalls = Read<Dictionary<Vector2Int, List<int>>>(view, "fogWallsByCell"); var actualGround = Read<Dictionary<Vector2Int, List<DungeonSurfaceEdge>>>(view, "fogGroundEdgesByCell");
                CollectionAssert.AreEquivalent(expectedWalls.Keys, actualWalls.Keys); CollectionAssert.AreEquivalent(expectedGround.Keys, actualGround.Keys);
                foreach (var pair in expectedWalls) CollectionAssert.AreEqual(pair.Value, actualWalls[pair.Key]);
                foreach (var pair in expectedGround) CollectionAssert.AreEqual(pair.Value, actualGround[pair.Key]);
            }
            Compare(); int builds = view.FogGeometryIndexBuildCount;
            for (int repeat = 0; repeat < 6; repeat++) { fixture.Floor.Fog.ToggleDebugFog(); view.ApplyFog(fixture.Floor); Compare(); }
            fixture.Floor.Fog.SetMapDiscovered(false); view.ApplyFog(fixture.Floor); Compare();
            Assert.That(view.FogGeometryIndexBuildCount, Is.EqualTo(builds));
            fixture.Map.TryAddFloorCell(new Vector2Int(5, 2), "room"); view.ApplyFog(fixture.Floor); Compare();
            Assert.That(view.FogGeometryIndexBuildCount, Is.EqualTo(builds + 1));
            view.Rebuild(fixture.Map); view.ApplyFog(fixture.Floor); Compare();
            Assert.That(view.FogGeometryIndexBuildCount, Is.EqualTo(builds + 2), "Same-map rebuild must invalidate renderer ownership even when counts match.");
        }

        [Test]
        public void RestoreTargetsSkipsExactMatchesButRepairsExternalAndSubEpsilonColors()
        {
            var root = new GameObject("Exact fog colour restoration");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>(); var renderer = root.AddComponent<SpriteRenderer>();
                var type = typeof(DungeonFloorMapView).GetNestedType("FogSpriteVisual", BindingFlags.NonPublic);
                var visual = Activator.CreateInstance(type, true); var baseColor = new Color(.4f, .5f, .6f, 1);
                Set(visual, "BaseColor", baseColor); Set(visual, "Target", new Vector2(.55f, 1));
                var expected = new Color(baseColor.r * .55f, baseColor.g * .55f, baseColor.b * .55f, 1);
                var visuals = Read<IDictionary>(view, "fogSpriteVisuals"); var restore = Bind<Action>(view, "RestoreFogAnimationTargets");
                renderer.color = expected; visuals.Add(renderer, visual); restore(); Assert.That(view.FogSpriteTargetWriteCount, Is.Zero);
                renderer.color = new Color(expected.r + .0000001f, expected.g, expected.b, expected.a);
                Assert.That(renderer.color.Equals(expected), Is.False); visuals.Add(renderer, visual); restore();
                Assert.That(renderer.color.Equals(expected), Is.True); Assert.That(view.FogSpriteTargetWriteCount, Is.EqualTo(1));
                renderer.color = Color.magenta; visuals.Add(renderer, visual); restore();
                Assert.That(renderer.color.Equals(expected), Is.True); Assert.That(view.FogSpriteTargetWriteCount, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
