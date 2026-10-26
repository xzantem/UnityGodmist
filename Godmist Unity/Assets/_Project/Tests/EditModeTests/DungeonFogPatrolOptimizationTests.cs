using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonFogPatrolOptimizationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/fog-patrol-pass-20261017"));
        static T Read<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        static void Invoke(object obj, string method) => obj.GetType().GetMethod(method, Private).Invoke(obj, null);

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void MeasureProductionFogAndPatrolsAndComparePresentation(string id)
        {
            var stage = File.ReadAllText(Path.Combine(Output, "stage.txt")).Trim();
            Assert.That(typeof(DungeonFloorMapView).GetField("fogTargetPatterns", Private) != null, Is.EqualTo(stage == "after"), "Imported assembly must match measurement stage.");
            var metrics = new List<Metric>(); var snapshots = new List<string>();
            var probe = new DungeonEntryPerformanceTests { OutputOverride = Output, StageOverride = stage };
            probe.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var coordinator = Read<DungeonCombatCoordinator>(bootstrap, "combatCoordinator");
                map.ConfigurePresentationCamera(null); // This fixture compares the full-floor reference snapshots.
                var floor = session.ActiveDungeon.Floors[0];
                new DungeonFogSystem().Update(floor, party.ActiveMemberViews.Select(v => v.DungeonPosition).ToArray());
                void Measure(string name, Action action)
                {
                    action(); var samples = new double[15];
                    for (var i = 0; i < samples.Length; i++) { var timer = Stopwatch.StartNew(); action(); samples[i] = timer.Elapsed.TotalMilliseconds; }
                    Array.Sort(samples); metrics.Add(new Metric { name = name, medianMs = samples[7], maxMs = samples[14] });
                }
                void Snapshot()
                {
                    using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
                    var tiles = Read<UnityEngine.Tilemaps.Tilemap>(map, "floorTilemap");
                    foreach (var cell in floor.Map.FloorCells.OrderBy(c => c.x).ThenBy(c => c.y))
                    { var c = new Vector3Int(cell.x, cell.y, 0); writer.Write(tiles.HasTile(c)); Write(writer, tiles.GetColor(c)); }
                    foreach (var sprite in map.GetComponentsInChildren<SpriteRenderer>(true).OrderBy(s => s.transform.position.x).ThenBy(s => s.transform.position.y).ThenBy(s => s.name, StringComparer.Ordinal))
                    { writer.Write(sprite.name); writer.Write(sprite.enabled); writer.Write(sprite.forceRenderingOff); Write(writer, sprite.color); }
                    var mesh = Read<Mesh>(map, "fogBlendMesh");
                    if (mesh != null)
                    {
                        var vertices = mesh.vertices; var colors = mesh.colors;
                        // Recycled patches retain transparent spare capacity.
                        // Compare every active vertex against the original
                        // compact mesh, and assert discarded slots are invisible.
                        var slotsField = typeof(DungeonFloorMapView).GetField("fogMeshSlots", Private);
                        var slots = slotsField == null ? null : new HashSet<int>(((Dictionary<Vector2Int, int>)slotsField.GetValue(map)).Values);
                        if (slots != null) foreach (var i in Enumerable.Range(0, vertices.Length).Where(i => !slots.Contains(i / 81)))
                            Assert.That(colors[i].a, Is.Zero, "Retired fog patches must be fully transparent.");
                        foreach (var i in Enumerable.Range(0, vertices.Length).Where(i => slots == null || slots.Contains(i / 81))
                            .OrderBy(i => vertices[i].x).ThenBy(i => vertices[i].y).ThenBy(i => colors[i].a))
                        { writer.Write(vertices[i].x); writer.Write(vertices[i].y); Write(writer, colors[i]); }
                    }
                    writer.Flush(); snapshots.Add(Convert.ToBase64String(SHA256.Create().ComputeHash(stream.ToArray())));
                }
                void Apply() { map.ApplyFog(floor); Snapshot(); }
                Apply();
                Measure("fog total edit snap", () => map.ApplyFog(floor));
                Set(map, "snapFogAnimation", false);
                Measure("soft fog unchanged runtime", () => Invoke(map, "RefreshSoftFog"));
                Measure("fog vegetation ground", () => Invoke(map, "RefreshVegetationGround"));
                Measure("fog decorations", () => Invoke(map, "RefreshPresentationDecorations"));
                var rooms = floor.Rooms.Where(r => r != null).ToArray();
                foreach (var n in new[] { rooms.Length / 3, rooms.Length * 2 / 3 })
                {
                    var observer = floor.Map.FloorCells.First(c => floor.Map.TryGetRoomId(new DungeonSurfacePosition(c), out var room) && room == rooms[n].RoomId);
                    new DungeonFogSystem().Update(floor, new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(observer)) }); Apply(); Apply();
                }
                floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); Apply();
                floor.Fog.ToggleDebugFog(); Apply();
                Set(map, "snapFogAnimation", false);
                Measure("soft fog revealed runtime", () => Invoke(map, "RefreshSoftFog"));
                floor.Fog.ToggleDebugFog(); Apply();
                void Patrol(bool sense)
                { Set(coordinator, "patrolElapsed", .021f); Set(coordinator, "nextPatrolSense", sense ? 0f : float.MaxValue); Invoke(coordinator, "UpdatePatrols"); }
                Measure("patrol sense", () => Patrol(true));
                Measure("patrol motion", () => Patrol(false));
                File.WriteAllText(Path.Combine(Output, stage + "-" + id + "-timings.json"), JsonUtility.ToJson(new Metrics { values = metrics.ToArray() }, true));
                File.WriteAllText(Path.Combine(Output, stage + "-" + id + "-fog.json"), JsonUtility.ToJson(new Snapshots { values = snapshots.ToArray() }, true));
                if (stage == "after") Assert.That(File.ReadAllText(Path.Combine(Output, stage + "-" + id + "-fog.json")), Is.EqualTo(File.ReadAllText(Path.Combine(Output, "before-" + id + "-fog.json"))));
            };
            probe.MeasureProductionFloorEntry(id, DungeonLength.Long);
        }
        static void Write(BinaryWriter w, Color c) { w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a); }

        [TestCase(false)] [TestCase(true)]
        public void ReusedStandingQueryMatchesOriginalWithTriggersActorsAndManyHits(bool queryTriggers)
        {
            var root = new GameObject("Standing query equivalence");
            GameObject solidRoot = null;
            var old = Physics2D.queriesHitTriggers;
            try
            {
                Physics2D.queriesHitTriggers = queryTriggers;
                var actor = root.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                var circle = root.AddComponent<CircleCollider2D>(); circle.radius = .3f; circle.offset = new Vector2(.1f, .2f); Set(actor, "movementCollider", circle);
                root.transform.localScale = new Vector3(1.4f, .8f, 1);
                for (var i = 0; i < 40; i++)
                {
                    var other = new GameObject("Trigger", typeof(BoxCollider2D)); other.transform.SetParent(root.transform);
                    other.GetComponent<BoxCollider2D>().isTrigger = true;
                }
                solidRoot = new GameObject("Solid", typeof(BoxCollider2D)); solidRoot.transform.SetParent(root.transform);
                var solid = solidRoot.GetComponent<BoxCollider2D>();
                void Check()
                {
                    Physics2D.SyncTransforms();
                    foreach (var p in new[] { Vector2.zero, Vector2.right * 5, Vector2.left })
                    {
                        var expected = !Physics2D.OverlapCircleAll(p + actor.MovementColliderCenterOffset, actor.MovementRadius,
                            Physics2D.GetLayerCollisionMask(actor.gameObject.layer)).Any(h => !h.isTrigger && h.GetComponentInParent<Godmist.Views.Exploration.ExplorationCharacterView>() == null);
                        for (var i = 0; i < 3; i++) Assert.That(actor.CanStandAt(p), Is.EqualTo(expected));
                    }
                }
                // A child of an actor is ignored; a separate solid is not.
                Check(); solidRoot.transform.SetParent(null, true); Check();
                solid.isTrigger = true; Check(); solid.isTrigger = false; solid.transform.position += Vector3.right * 5; Check();
                UnityEngine.Object.DestroyImmediate(solidRoot);
            }
            finally { Physics2D.queriesHitTriggers = old; UnityEngine.Object.DestroyImmediate(solidRoot); UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ForestFogInvalidatesGateMemoryEvenWithoutEndpointKnowledgeChanges()
        {
            var root = new GameObject("Forest gate fog", typeof(Grid)); var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var tiles = new GameObject("Floor", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>()); Set(view, "floorTile", tile);
                Set(view, "wallSegmentPrefab", UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                view.ConfigureVegetationPresentation(catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.forest.expedition").VegetationPresentation);
                var map = new DungeonFloorMap();
                for (var x = 0; x < 5; x++) for (var y = 0; y < 5; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                foreach (var cell in map.FloorCells) foreach (var dir in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                    if (!map.IsWalkable(cell + dir)) map.TryAddWall(cell, cell + dir);
                var gate = new DungeonSurfaceEdge(new DungeonCellEdge(new Vector2Int(2, 2), new Vector2Int(3, 2)));
                Assert.That(map.TrySetProgressionGateState(gate, false), Is.True); view.Rebuild(map);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                floor.Fog.SetVisible(map.FloorCells.Select(c => new DungeonTraversalPosition(new DungeonSurfacePosition(c)))); view.ApplyFog(floor);
                var walls = Read<List<(DungeonSurfaceEdge Edge, DungeonWallSegmentView View, Renderer[] Renderers)>>(view, "wallRenderers");
                var gateIndex = walls.FindIndex(w => w.Edge.Equals(gate)); Assert.That(gateIndex, Is.GreaterThanOrEqualTo(0));
                foreach (var known in new[] { true, false, true })
                {
                    if (known) floor.Fog.ClosedGates.Add(gate); else floor.Fog.ClosedGates.Remove(gate);
                    view.ApplyFog(floor);
                    Assert.That(Read<List<int>>(view, "orderedFogWalls"), Does.Contain(gateIndex));
                    var before = walls.SelectMany(w => w.Renderers).Select(r => (r.forceRenderingOff, ((SpriteRenderer)r).color)).ToArray();
                    Set(view, "fogBoundaryRevision", -1); view.ApplyFog(floor);
                    var after = walls.SelectMany(w => w.Renderers).Select(r => (r.forceRenderingOff, ((SpriteRenderer)r).color)).ToArray();
                    Assert.That(after, Is.EqualTo(before), "Incremental presentation must equal a forced full refresh.");
                }
                floor.Fog.SetMapDiscovered(false); view.ApplyFog(floor); Assert.That(Read<bool>(view, "incrementalForestFog"), Is.False);
                map.TryAddFloorCell(new Vector2Int(5, 2), "room"); view.ApplyFog(floor); Assert.That(Read<bool>(view, "incrementalForestFog"), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(tile); }
        }

        [Test]
        public void PatrolShortcutsMatchBfsAndNeverKeepBlockedCellsAcrossQueries()
        {
            var root = new GameObject("Patrol query equivalence", typeof(Grid));
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>();
            try
            {
                var floor = new DungeonFloorInstance(0); var map = new DungeonFloorMap();
                for (var x = 0; x < 8; x++) for (var y = 0; y < 3; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                floor.TrySetMap(map);
                var tiles = new GameObject("Floor", typeof(UnityEngine.Tilemaps.Tilemap)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                var party = root.AddComponent<Godmist.Views.Exploration.ExplorationPartyController>(); party.ConfigureTraversal(floor, view, null);
                var actorRoot = new GameObject("Enemy"); actorRoot.transform.SetParent(root.transform);
                var actor = actorRoot.AddComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
                actor.SetEnemyInstanceId("guard"); actor.ConfigureTerrain(map, view, floor);
                actor.SetPosition(view.CellToWorldCenter(new Vector2Int(1, 1)));
                var coordinator = root.AddComponent<DungeonCombatCoordinator>(); Set(coordinator, "floor", floor); Set(coordinator, "map", view); Set(coordinator, "party", party);
                var query = typeof(DungeonCombatCoordinator).GetMethod("TryBuildPatrolPath", Private);
                DungeonTraversalPosition At(int x) => new(new DungeonSurfacePosition(new Vector2Int(x, 1)));
                void Compare(int x)
                {
                    var expected = DungeonExplorationPathfinder.TryFindPath(map, actor.DungeonPosition, At(x), out var path,
                        p => party.CanEnterTraversal(actor, p), party.CanOpenTraversalDoor);
                    var args = new object[] { actor, At(x), null };
                    Assert.That((bool)query.Invoke(coordinator, args), Is.EqualTo(expected));
                    Assert.That((List<DungeonTraversalPosition>)args[2], Is.EqualTo(path));
                }
                Compare(1); Compare(2); Compare(6);
                var blocked = new ObstacleDungeonObjectInstance("blocker", definition, new Vector2Int(2, 1)); floor.TryAddObject(blocked);
                Compare(2); Compare(6); blocked.TryDeactivate(); Compare(2);
                var edge = new DungeonCellEdge(new Vector2Int(1, 1), new Vector2Int(2, 1));
                map.TrySetDoorState(edge, false); Compare(2); map.TrySetDoorState(edge, true); Compare(2);
                var collider = new GameObject("Physical blocker", typeof(BoxCollider2D)); collider.transform.SetParent(root.transform);
                collider.transform.position = view.CellToWorldCenter(new Vector2Int(2, 1)); Physics2D.SyncTransforms();
                Compare(2); Compare(6);
                collider.transform.position += Vector3.up * 10; Physics2D.SyncTransforms(); Compare(2); Compare(6);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void UnchangedSoftFogKeepsAnActiveTransitionAndClearsTheLastMesh()
        {
            var root = new GameObject("Soft fog target cache", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var tiles = new GameObject("Floor", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer)); tiles.transform.SetParent(root.transform);
                var tilemap = tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tilemap); Set(view, "floorTile", tile);
                Set(view, "wallSegmentPrefab", UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var map = new DungeonFloorMap();
                for (var x = -1; x <= 1; x++) for (var y = -1; y <= 1; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                view.Rebuild(map); var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                floor.Fog.SetVisible(new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero)) }); view.ApplyFog(floor);
                Set(view, "snapFogAnimation", false);
                var projection = Read<Dictionary<Vector2Int, DungeonKnowledge>>(view, "fogProjection");
                var presented = Read<Dictionary<Vector3Int, (UnityEngine.Tilemaps.TileBase Tile, Color Color)>>(view, "presentedFloorCells");
                projection[Vector2Int.zero] = DungeonKnowledge.Remembered; presented[Vector3Int.zero] = (tile, DungeonFloorMapView.Dim(Color.white));
                Invoke(view, "RefreshSoftFog");
                var tick = typeof(DungeonFloorMapView).GetMethod("TickFogAnimation", Private);
                tick.Invoke(view, new object[] { .1f }); var intermediate = tilemap.GetColor(Vector3Int.zero);
                Invoke(view, "RefreshSoftFog"); Assert.That(tilemap.GetColor(Vector3Int.zero), Is.EqualTo(intermediate));
                tick.Invoke(view, new object[] { .2f });
                Assert.That(tilemap.GetColor(Vector3Int.zero).r, Is.EqualTo(.55f).Within(.00001f));
                projection[Vector2Int.zero] = DungeonKnowledge.Unexplored; Invoke(view, "RefreshSoftFog");
                Assert.That(Read<MeshRenderer>(view, "fogBlendRenderer").enabled, Is.False);
                Assert.That(Read<Mesh>(view, "fogBlendMesh").vertexCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(tile); }
        }
        [Serializable] sealed class Metric { public string name; public double medianMs, maxMs; }
        [Serializable] sealed class Metrics { public Metric[] values; }
        [Serializable] sealed class Snapshots { public string[] values; }
    }
}
