using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonShadowFogOptimizationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
        static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
        static object Invoke(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/shadow-fog-pass-20261017"));

        [Test]
        public void RecycledFogPatchesMatchFullRebuildDuringMovementAndTransitions()
        {
            using var incremental = new FogFixture(); using var reference = new FogFixture();
            var target = new RenderTexture(256, 256, 0);
            try
            {
                target.Create(); var commands = new CommandBuffer();
                try
                {
                    void Compare()
                    {
                        reference.FullRebuild();
                        CollectionAssert.AreEqual(incremental.Render(target, commands), reference.Render(target, commands));
                        foreach (var cell in incremental.Projection.Keys)
                            Assert.That(incremental.Tilemap.GetColor(new Vector3Int(cell.x, cell.y, 0)),
                                Is.EqualTo(reference.Tilemap.GetColor(new Vector3Int(cell.x, cell.y, 0))));
                    }
                    int highWater = 0;
                    for (int step = 0; step < 12; step++)
                    {
                        incremental.Project(step % 4); reference.Project(step % 4);
                        highWater = Math.Max(highWater, Read<IDictionary>(incremental.View, "fogCellVisuals").Count);
                        Assert.That(Read<Mesh>(incremental.View, "fogBlendMesh").vertexCount, Is.EqualTo(highWater * 81), "Slots must be recycled; movement cannot grow mesh indefinitely.");
                        Compare();
                        foreach (float tick in new[] { .1f, .2f })
                        { Invoke(incremental.View, "TickFogAnimation", tick); Invoke(reference.View, "TickFogAnimation", tick); Compare(); }
                    }
                    incremental.Project(-1); reference.Project(-1); Compare();
                    Assert.That(Read<Mesh>(incremental.View, "fogBlendMesh").vertexCount, Is.Zero);
                    Assert.That(Read<IDictionary>(incremental.View, "fogMeshSlots").Count, Is.Zero);
                    incremental.Project(0); reference.Project(0); Compare();
                }
                finally { commands.Dispose(); }
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void StableFogTopologyAvoidsRebuildingVerticesAndMeasuresReferenceCost()
        {
            using var fixture = new FogFixture(40);
            fixture.Project(0);
            var mesh = Read<Mesh>(fixture.View, "fogBlendMesh");
            var before = mesh.vertices; var indices = mesh.triangles;
            double Measure(bool full)
            {
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 25; i++)
                    if (full) fixture.FullRebuild(); else Invoke(fixture.View, "BuildFogMesh");
                return watch.Elapsed.TotalMilliseconds / 25;
            }
            Measure(false); Measure(true);
            double retained = Measure(false), rebuilt = Measure(true);
            CollectionAssert.AreEqual(before, mesh.vertices); CollectionAssert.AreEqual(indices, mesh.triangles);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "fog-topology-comparison.json"), JsonUtility.ToJson(new FogMetric {
                retainedMs = retained, fullRebuildMs = rebuilt, activeCells = Read<IDictionary>(fixture.View, "fogCellVisuals").Count,
                vertices = mesh.vertexCount }, true));
        }

        [Test]
        public void ForestDecorationIndexIncludesChangedEndpointsAndGateMemory()
        {
            using var fixture = new FogFixture();
            var view = fixture.View;
            var decorationType = typeof(DungeonFloorMapView).GetNestedType("PresentationDecoration", BindingFlags.NonPublic);
            var list = Read<IList>(view, "presentationDecorations");
            for (int i = 0; i < 100; i++)
            {
                var decoration = Activator.CreateInstance(decorationType, true);
                decorationType.GetField("Cell").SetValue(decoration, new Vector2Int(i, 0)); list.Add(decoration);
            }
            var wall = Activator.CreateInstance(decorationType, true);
            decorationType.GetField("IsWall").SetValue(wall, true);
            var edge = new DungeonSurfaceEdge(new DungeonSurfacePosition(new Vector2Int(2, 0)), new DungeonSurfacePosition(new Vector2Int(3, 0)));
            decorationType.GetField("Edge").SetValue(wall, edge); list.Add(wall);
            Invoke(view, "RebuildFogDecorationIndex"); Set(view, "incrementalForestFog", true);
            Invoke(view, "Change", new Vector2Int(2, 0)); Invoke(view, "Change", new Vector2Int(3, 0));
            var selected = ((IEnumerable<int>)Invoke(view, "FogDecorationIndices")).ToArray();
            CollectionAssert.AreEquivalent(new[] { 2, 3, 100 }, selected);
            Invoke(view, "ClearPresentationDecorations");
            Assert.That(Read<bool>(view, "fogDecorationIndexReady"), Is.False);
        }

        sealed class FogFixture : IDisposable
        {
            public readonly GameObject Root = new("Fog retained topology", typeof(Grid));
            public readonly DungeonFloorMapView View;
            public readonly Tilemap Tilemap;
            readonly Tile tile = ScriptableObject.CreateInstance<Tile>();
            readonly int size;
            public Dictionary<Vector2Int, DungeonKnowledge> Projection => Read<Dictionary<Vector2Int, DungeonKnowledge>>(View, "fogProjection");
            public FogFixture(int size = 12)
            {
                this.size = size;
                var tiles = new GameObject("Tiles", typeof(Tilemap), typeof(TilemapRenderer)); tiles.transform.SetParent(Root.transform);
                Tilemap = tiles.GetComponent<Tilemap>(); View = Root.AddComponent<DungeonFloorMapView>();
                Set(View, "floorTilemap", Tilemap); Set(View, "floorTile", tile);
                var map = new DungeonFloorMap();
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                Set(View, "boundMap", map); Read<DungeonElevationViewState>(View, "elevation").Bind(map);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                floor.Fog.SetVisible(new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)) });
                View.ApplyFog(floor); Set(View, "snapFogAnimation", false);
            }
            public void Project(int step)
            {
                var previous = Read<Dictionary<Vector2Int, DungeonKnowledge>>(View, "previousFogProjection");
                previous.Clear(); foreach (var pair in Projection) previous.Add(pair.Key, pair.Value);
                Projection.Clear();
                var presented = Read<Dictionary<Vector3Int, (TileBase Tile, Color Color)>>(View, "presentedFloorCells");
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var state = step >= 0 && x >= step && x < size - 4 + step && y >= 1 && y < size - 1
                        ? x % 3 == 0 ? DungeonKnowledge.Remembered : DungeonKnowledge.Visible : DungeonKnowledge.Unexplored;
                    Projection[cell] = state;
                    if (state == DungeonKnowledge.Unexplored) continue;
                    var color = state == DungeonKnowledge.Remembered ? DungeonFloorMapView.Dim(Color.white) : Color.white;
                    var cell3 = new Vector3Int(x, y, 0); Tilemap.SetTile(cell3, tile); Tilemap.SetTileFlags(cell3, TileFlags.None);
                    presented[cell3] = (tile, color);
                }
                Invoke(View, "RefreshSoftFog");
            }
            public void FullRebuild() { Invoke(View, "ClearSoftFog"); Invoke(View, "BuildFogMesh"); }
            public byte[] Render(RenderTexture target, CommandBuffer commands)
            {
                commands.Clear(); commands.SetRenderTarget(target); commands.ClearRenderTarget(false, true, Color.white);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1, size + 1, -1, size + 1, -1, 1), true));
                var renderer = Read<MeshRenderer>(View, "fogBlendRenderer");
                if (renderer != null && renderer.enabled)
                    commands.DrawMesh(Read<Mesh>(View, "fogBlendMesh"), Matrix4x4.identity, renderer.sharedMaterial);
                Graphics.ExecuteCommandBuffer(commands); return ReadPixels(target);
            }
            public void Dispose() { Object.DestroyImmediate(Root); Object.DestroyImmediate(tile); }
        }

        static byte[] ReadPixels(RenderTexture target)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        [Serializable] sealed class FogMetric { public double retainedMs, fullRebuildMs; public int activeCells, vertices; }
    }
}
