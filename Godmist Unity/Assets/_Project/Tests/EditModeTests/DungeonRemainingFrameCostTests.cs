using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonRemainingFrameCostTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Read<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
        static void Invoke(object obj, string method) => obj.GetType().GetMethod(method, Private).Invoke(obj, null);

        [Test]
        public void PatrolQueryScopeMatchesLiveObstaclesAndRestoresAfterNestedFailure()
        {
            var root = new GameObject("Patrol query scope");
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>();
            try
            {
                var floor = new Godmist.Runtime.Dungeons.DungeonFloorInstance(0);
                var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
                map.TryAddFloorCell(Vector2Int.zero, "room"); floor.TrySetMap(map);
                var party = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>();
                party.GetType().GetField("traversalFloor", Private).SetValue(party, floor);
                var position = new Godmist.Runtime.Dungeons.DungeonTraversalPosition(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(Vector2Int.zero));
                Assert.Throws<InvalidOperationException>(() => {
                    using var outer = party.BeginTraversalQueryBatch();
                    Assert.That(party.CanEnterTraversal(null, position), Is.True);
                    using (party.BeginTraversalQueryBatch()) Assert.That(party.CanEnterTraversal(null, position), Is.True);
                    throw new InvalidOperationException("scope test");
                });
                var blocker = new Godmist.Runtime.Dungeons.ObstacleDungeonObjectInstance("new", definition, Vector2Int.zero);
                floor.TryAddObject(blocker);
                Assert.That(party.CanEnterTraversal(null, position), Is.False, "Outside scope, newly added obstacles are immediately live.");
                using (party.BeginTraversalQueryBatch())
                {
                    Assert.That(party.CanEnterTraversal(null, position), Is.False, "Next patrol batch sees changed objects.");
                    blocker.TryDeactivate();
                    Assert.That(party.CanEnterTraversal(null, position), Is.True, "Destruction remains live inside a batch.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void HiddenSpriteFirstRevealStillFadesAndRepeatedTintDoesNotRestartIt()
        {
            var root = new GameObject("Lazy fog transition", typeof(SpriteRenderer));
            try
            {
                var map = root.AddComponent<DungeonFloorMapView>();
                var sprite = root.GetComponent<SpriteRenderer>();
                typeof(DungeonFloorMapView).GetField("snapFogAnimation", Private).SetValue(map, false);
                var tint = typeof(DungeonFloorMapView).GetMethod("TintFog", Private);
                var tick = typeof(DungeonFloorMapView).GetMethod("AdvanceFogAnimation", Private);
                sprite.forceRenderingOff = true;
                tint.Invoke(map, new object[] { sprite, Godmist.Runtime.Dungeons.DungeonKnowledge.Unexplored });
                sprite.forceRenderingOff = false;
                tint.Invoke(map, new object[] { sprite, Godmist.Runtime.Dungeons.DungeonKnowledge.Visible });
                Assert.That(sprite.color.a, Is.Zero);
                tick.Invoke(map, new object[] { .2f });
                var alpha = sprite.color.a;
                tint.Invoke(map, new object[] { sprite, Godmist.Runtime.Dungeons.DungeonKnowledge.Visible });
                Assert.That(sprite.color.a, Is.EqualTo(alpha));
                tick.Invoke(map, new object[] { .2f });
                Assert.That(sprite.color, Is.EqualTo(Color.white));
                tint.Invoke(map, new object[] { sprite, Godmist.Runtime.Dungeons.DungeonKnowledge.Remembered });
                tick.Invoke(map, new object[] { .4f });
                Assert.That(sprite.color.r, Is.EqualTo(.55f).Within(.0001f));
                sprite.forceRenderingOff = true;
                tint.Invoke(map, new object[] { sprite, Godmist.Runtime.Dungeons.DungeonKnowledge.Unexplored });
                Assert.That(sprite.forceRenderingOff, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void IncrementalForestUnderlaysMatchAllOwnerKnowledgeThroughRevealDebugAndRebuild()
        {
            var root = new GameObject("Incremental forest fog", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var tiles = new GameObject("Floor", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer));
                tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                typeof(DungeonFloorMapView).GetField("floorTile", Private).SetValue(view, tile);
                typeof(DungeonFloorMapView).GetField("wallSegmentPrefab", Private).SetValue(view, UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                view.ConfigureVegetationPresentation(catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.forest.expedition").VegetationPresentation);
                for (var rebuild = 0; rebuild < 2; rebuild++)
                {
                    var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
                    for (var x = 0; x < 5; x++) for (var y = 0; y < 5; y++) map.TryAddFloorCell(new Vector2Int(x + rebuild * 10, y), "room");
                    foreach (var cell in map.FloorCells)
                        foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                            if (!map.IsWalkable(cell + direction)) map.TryAddWall(cell, cell + direction);
                    var floor = new Godmist.Runtime.Dungeons.DungeonFloorInstance(0); floor.TrySetMap(map); view.Rebuild(map);
                    void Check()
                    {
                        view.ApplyFog(floor);
                        var grounds = Read<List<(SpriteRenderer Renderer, Godmist.Runtime.Dungeons.DungeonSurfaceEdge[] Edges)>>(view, "vegetationGround");
                        Assert.That(grounds.Count, Is.GreaterThan(0));
                        var colors = Read<Dictionary<SpriteRenderer, Color>>(view, "fogBaseColors");
                        var walls = Read<List<(Godmist.Runtime.Dungeons.DungeonSurfaceEdge Edge, DungeonWallSegmentView View, Renderer[] Renderers)>>(view, "wallRenderers");
                        foreach (var wall in walls)
                        {
                            var state = (Godmist.Runtime.Dungeons.DungeonKnowledge)Math.Max((int)view.Knowledge(new Godmist.Runtime.Dungeons.DungeonTraversalPosition(wall.Edge.First)), (int)view.Knowledge(new Godmist.Runtime.Dungeons.DungeonTraversalPosition(wall.Edge.Second)));
                            foreach (var sprite in wall.Renderers.OfType<SpriteRenderer>())
                            {
                                Assert.That(sprite.forceRenderingOff, Is.EqualTo(state == Godmist.Runtime.Dungeons.DungeonKnowledge.Unexplored));
                                if (state == Godmist.Runtime.Dungeons.DungeonKnowledge.Unexplored) continue;
                                var expected = colors[sprite];
                                if (state == Godmist.Runtime.Dungeons.DungeonKnowledge.Remembered) expected = DungeonFloorMapView.Dim(expected);
                                Assert.That(sprite.color, Is.EqualTo(expected));
                            }
                        }
                        foreach (var ground in grounds)
                        {
                            var state = ground.Edges.Select(e => (Godmist.Runtime.Dungeons.DungeonKnowledge)Math.Max((int)view.Knowledge(new Godmist.Runtime.Dungeons.DungeonTraversalPosition(e.First)), (int)view.Knowledge(new Godmist.Runtime.Dungeons.DungeonTraversalPosition(e.Second)))).Max();
                            Assert.That(ground.Renderer.forceRenderingOff, Is.EqualTo(state == Godmist.Runtime.Dungeons.DungeonKnowledge.Unexplored));
                            if (state == Godmist.Runtime.Dungeons.DungeonKnowledge.Unexplored) continue;
                            var expected = colors[ground.Renderer];
                            if (state == Godmist.Runtime.Dungeons.DungeonKnowledge.Remembered) expected = DungeonFloorMapView.Dim(expected);
                            Assert.That(ground.Renderer.color, Is.EqualTo(expected));
                        }
                    }
                    floor.Fog.SetVisible(new[] { new Godmist.Runtime.Dungeons.DungeonTraversalPosition(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(new Vector2Int(rebuild * 10, 0))) }); Check(); Check();
                    floor.Fog.SetVisible(new[] { new Godmist.Runtime.Dungeons.DungeonTraversalPosition(new Godmist.Runtime.Dungeons.DungeonSurfacePosition(new Vector2Int(rebuild * 10 + 4, 4))) }); Check();
                    floor.Fog.SetVisible(Array.Empty<Godmist.Runtime.Dungeons.DungeonTraversalPosition>()); Check();
                    floor.Fog.ToggleDebugFog(); Check(); floor.Fog.ToggleDebugFog(); Check();
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(tile); }
        }
        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void MeasureRemainingProductionStages(string id)
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/dungeon-whole-frame-20261016"));
            var stage = File.ReadAllText(Path.Combine(output, "stage.txt")).Trim();
            var metrics = new List<Metric>();
            var probe = new DungeonEntryPerformanceTests { OutputOverride = output, StageOverride = "stages-" + stage };
            probe.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var coordinator = Read<DungeonCombatCoordinator>(bootstrap, "combatCoordinator");
                var floor = session.ActiveDungeon.Floors[0];
                new DungeonFogSystem().Update(floor, party.ActiveMemberViews.Select(v => v.DungeonPosition).ToArray());
                map.ApplyFog(floor);
                void Measure(string name, Action action)
                {
                    action(); var times = new double[7];
                    for (var i = 0; i < times.Length; i++)
                    { var watch = Stopwatch.StartNew(); action(); times[i] = watch.Elapsed.TotalMilliseconds; }
                    Array.Sort(times); metrics.Add(new Metric { name = name, medianMs = times[3], maxMs = times[6] });
                    File.WriteAllText(Path.Combine(output, stage + "-stages-" + id + ".json"), JsonUtility.ToJson(new Report { metrics = metrics.ToArray() }, true));
                }
                Measure("fog total", () => map.ApplyFog(floor));
                Measure("fog vegetation ground", () => Invoke(map, "RefreshVegetationGround"));
                Measure("fog decorations", () => Invoke(map, "RefreshPresentationDecorations"));
                Measure("fog soft boundary", () => Invoke(map, "RefreshSoftFog"));
                Measure("patrols", () => {
                    typeof(DungeonCombatCoordinator).GetField("patrolElapsed", Private).SetValue(coordinator, .021f);
                    typeof(DungeonCombatCoordinator).GetField("nextPatrolSense", Private).SetValue(coordinator, 0f);
                    Invoke(coordinator, "UpdatePatrols");
                });
            };
            probe.MeasureProductionFloorEntry(id, DungeonLength.Long);
            Assert.That(metrics.Count, Is.EqualTo(5));
        }
        [Serializable] sealed class Metric { public string name; public double medianMs, maxMs; }
        [Serializable] sealed class Report { public Metric[] metrics; }
    }
}
