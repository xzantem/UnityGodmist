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
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonVisibilityBudgetTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/visibility-budget-pass-20261017"));
        static T Read<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        static void Invoke(object obj, string method) => obj.GetType().GetMethod(method, Private).Invoke(obj, null);

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void LocalPresentationMatchesFullCameraPixelsAndMeasuresCosts(string id)
        {
            Directory.CreateDirectory(Output); var metrics = new List<Metric>(); var checks = new List<string>();
            var probe = new DungeonEntryPerformanceTests { OutputOverride = Output, StageOverride = "visibility" };
            probe.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var rig = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var camera = Read<Camera>(rig, "worldCamera"); camera.aspect = 16f / 9f;
                var floor = session.ActiveDungeon.Floors[0];
                var target = new RenderTexture(640, 360, 24); var texture = new Texture2D(640, 360, TextureFormat.RGBA32, false);
                var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
                var objects = map.GetComponentsInParent<Transform>().Last().GetComponentsInChildren<DungeonObjectView>(true);
                var actors = party.ActiveMemberViews.Concat(Read<DungeonEncounterSpawner>(bootstrap, "encounterSpawner").Triggers.SelectMany(t => t.EnemyViews)).ToArray();
                var renderNumber = 0;
                var flames = bootstrap.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true)).Where(r => r.sharedMaterial != null
                    && r.sharedMaterial.shader.name == "Godmist/Dungeon/Pixel Torch").ToArray();
                var diagnostic = Path.Combine(Output, id + "-render-state.txt"); File.WriteAllText(diagnostic, "");
                void RefreshViews()
                {
                    foreach (var obj in objects) Invoke(obj, "LateUpdate");
                    foreach (var actor in actors) Invoke(actor, "LateUpdate");
                }
                void Lighting()
                { Set(rig, "lastLightingFrame", int.MinValue); Invoke(rig, "LateUpdate"); }
                string Pixels()
                {
                    // Native SortingGroups normally update once per PlayerLoop.
                    // Repeated Camera.Render calls in one synchronous EditMode
                    // test must not manually swap their native state each time.
                    RefreshViews(); Lighting();
                    if (flames.Length == 0) flames = rig.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sharedMaterial != null
                        && r.sharedMaterial.shader.name == "Godmist/Dungeon/Pixel Torch").ToArray();
                    renderNumber++;
                    File.AppendAllText(diagnostic, "render " + renderNumber + " floor=" + Read<UnityEngine.Tilemaps.TilemapRenderer>(rig, "floorReceiver").sortingOrder + " " + string.Join(";", party.ActiveMemberViews.Select(a => {
                        var body = Read<SpriteRenderer>(a, "bodyRenderer"); var group = Read<UnityEngine.Rendering.SortingGroup>(a, "worldDepthSortingGroup");
                        return a.CharacterId + ":off=" + body.forceRenderingOff + ":enabled=" + body.enabled + ":color=" + body.color + ":sprite=" + body.sprite.name
                            + ":dormant=" + Read<bool>(a, "presentationOutsideCamera") + ":fog=" + map.Knowledge(a.DungeonPosition) + ":group=" + (group != null ? group.enabled + "/" + group.sortingOrder : "none") + ":bodyOrder=" + body.sortingOrder;
                    })) + "\n");
                    camera.Render(); RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    var pixels = texture.GetRawTextureData<byte>().ToArray();
                    var maskedPixels = 0;
                    // Flame geometry intentionally uses Unity's live _Time even
                    // in EditMode. Exclude only those small sprite rectangles;
                    // compare all terrain, characters, props, shadows and fog.
                    foreach (var flame in flames)
                    {
                        if (!flame.enabled || flame.forceRenderingOff || !flame.gameObject.activeInHierarchy) continue;
                        var bounds = flame.bounds;
                        var min = camera.WorldToViewportPoint(bounds.min); var max = camera.WorldToViewportPoint(bounds.max);
                        var x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(min.x, max.x) * target.width) - 2, 0, target.width);
                        var x1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(min.x, max.x) * target.width) + 2, 0, target.width);
                        var y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(min.y, max.y) * target.height) - 2, 0, target.height);
                        var y1 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(min.y, max.y) * target.height) + 2, 0, target.height);
                        maskedPixels += (y1 - y0) * (x1 - x0);
                        for (var y = y0; y < y1; y++) for (var x = x0; x < x1; x++) Array.Clear(pixels, (y * target.width + x) * 4, 4);
                    }
                    Assert.That(maskedPixels, Is.LessThan(target.width * target.height * .05), "Only small animated flame rectangles may be excluded.");
                    File.AppendAllText(diagnostic, "maskedPixels=" + maskedPixels + "\n");
                    return Convert.ToBase64String(SHA256.Create().ComputeHash(pixels));
                }
                void Measure(string name, Action action)
                {
                    action(); var samples = new double[15];
                    for (var i = 0; i < samples.Length; i++) { var timer = Stopwatch.StartNew(); action(); samples[i] = timer.Elapsed.TotalMilliseconds; }
                    Array.Sort(samples); metrics.Add(new Metric { name = name, medianMs = samples[7], maxMs = samples[14] });
                }
                try
                {
                    camera.targetTexture = target;
                    var rooms = floor.Rooms.Where(r => r != null).ToArray();
                    foreach (var n in new[] { 0, rooms.Length / 3, rooms.Length * 2 / 3 })
                    {
                        var cell = floor.Map.FloorCells.First(c => floor.Map.TryGetRoomId(c, out var room) && room == rooms[n].RoomId);
                        new DungeonFogSystem().Update(floor, new[] { new DungeonTraversalPosition(new DungeonSurfacePosition(cell)) });
                        camera.transform.position = (Vector3)map.CellToWorldCenter(cell) + Vector3.back * 10;
                        foreach (var zoom in new[] { 5f, 9f })
                        {
                            camera.orthographicSize = zoom;
                            // Exercise discovery/pan catch-up before any full
                            // reference pass can pre-populate the same region.
                            map.ConfigurePresentationCamera(camera, 2 * Read<float>(rig, "torchRadius") * 1.3f + 2);
                            map.EnsureCameraPresentation(); map.ApplyFog(floor); Pixels(); Pixels(); var localFirst = Pixels();
                            map.ConfigurePresentationCamera(null); map.ApplyFog(floor); Pixels(); Pixels(); var full = Pixels();
                            File.WriteAllBytes(Path.Combine(Output, id + "-" + n + "-" + zoom + "-full.png"), texture.EncodeToPNG());
                            var repeat = Pixels();
                            File.WriteAllBytes(Path.Combine(Output, id + "-" + n + "-" + zoom + "-repeat.png"), texture.EncodeToPNG());
                            Assert.That(repeat, Is.EqualTo(full), "Unchanged full camera reference must be deterministic.");
                            Assert.That(localFirst, Is.EqualTo(full), "Camera catch-up before full reference: " + n + ":" + zoom);
                            map.ConfigurePresentationCamera(camera, 2 * Read<float>(rig, "torchRadius") * 1.3f + 2); map.ApplyFog(floor); Pixels(); Pixels(); var local = Pixels();
                            File.WriteAllBytes(Path.Combine(Output, id + "-" + n + "-" + zoom + "-local.png"), texture.EncodeToPNG());
                            Assert.That(local, Is.EqualTo(full), id + " camera " + n + " zoom " + zoom);
                            checks.Add(n + ":" + zoom + ":" + local);
                        }
                    }
                    floor.Fog.ToggleDebugFog(); map.ConfigurePresentationCamera(null); map.ApplyFog(floor); Pixels(); Pixels(); var revealed = Pixels();
                    map.ConfigurePresentationCamera(camera, 2 * Read<float>(rig, "torchRadius") * 1.3f + 2); map.ApplyFog(floor); Pixels(); Pixels(); Assert.That(Pixels(), Is.EqualTo(revealed)); checks.Add("debug");
                    floor.Fog.ToggleDebugFog();
                    map.ConfigurePresentationCamera(null); map.ApplyFog(floor); Measure("full fog", () => map.ApplyFog(floor));
                    map.ConfigurePresentationCamera(camera, 2 * Read<float>(rig, "torchRadius") * 1.3f + 2); map.ApplyFog(floor); Measure("local fog", () => map.ApplyFog(floor));
                    Measure("object and actor presentation", RefreshViews);
                    var allGroups = map.GetComponentsInChildren<UnityEngine.Rendering.SortingGroup>(true);
                    if (id.Contains("forest")) Assert.That(allGroups.Count(g => g.enabled), Is.LessThan(allGroups.Length / 2 + 1), "Disabled masonry must not keep sorting groups alive.");
                    File.WriteAllText(Path.Combine(Output, id + "-timings.json"), JsonUtility.ToJson(new Metrics { values = metrics.ToArray() }, true));
                    File.WriteAllText(Path.Combine(Output, id + "-pixels.txt"), string.Join("\n", checks));
                }
                finally
                { camera.targetTexture = previousTarget; RenderTexture.active = previousActive; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture); }
            };
            probe.MeasureProductionFloorEntry(id, DungeonLength.Long);
        }

        [Test]
        public void CameraQueryFollowsPanZoomAndRebindWithoutCullingColliders()
        {
            var root = new GameObject("Camera culling query");
            try
            {
                var view = root.AddComponent<DungeonFloorMapView>(); var cameraRoot = new GameObject("Camera", typeof(Camera)); cameraRoot.transform.SetParent(root.transform);
                var camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 1;
                camera.transform.position = Vector3.back * 10; view.ConfigurePresentationCamera(camera);
                Assert.That(view.IsInPresentationCamera(new Bounds(Vector3.zero, Vector3.one)), Is.True);
                Assert.That(view.IsInPresentationCamera(new Bounds(Vector3.right * 100, Vector3.one)), Is.False);
                camera.transform.position += Vector3.right * 100;
                Assert.That(view.IsInPresentationCamera(new Bounds(Vector3.right * 100, Vector3.one)), Is.True);
                Assert.That(view.IsInPresentationCamera(new Bounds(Vector3.zero, Vector3.one)), Is.False);
                camera.orthographicSize = 110; Assert.That(view.IsInPresentationCamera(new Bounds(Vector3.zero, Vector3.one)), Is.True);
                view.ConfigurePresentationCamera(null); Assert.That(view.IsInPresentationCamera(new Bounds(Vector3.right * 10000, Vector3.one)), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void FlatProjectionKeepsUnseenSurfacesAndInvalidatesOnGrowthAndElevation()
        {
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(Vector2Int.zero, "room"); map.TryAddFloorCell(Vector2Int.right, "room");
            var state = new DungeonElevationViewState(); state.Bind(map);
            var ground = new DungeonTraversalPosition(new DungeonSurfacePosition(Vector2Int.zero));
            for (var i = 0; i < 5; i++)
            {
                state.SetFogVisibility(i % 2 == 0 ? new[] { ground } : Array.Empty<DungeonTraversalPosition>());
                Assert.That(state.VisiblePositions.Count(), Is.EqualTo(2), "Fog knowledge does not remove flat projection geometry.");
            }
            map.TryAddFloorCell(Vector2Int.left, "room"); state.SetFogVisibility(Array.Empty<DungeonTraversalPosition>());
            Assert.That(state.VisiblePositions.Count(), Is.EqualTo(3));
            var upper = new DungeonSurfacePosition(Vector2Int.zero, 1); map.TryAddFloorCell(upper, "room"); state.Bind(map);
            state.SetFogVisibility(new[] { ground });
            Assert.That(state.TryGetVisibleSurface(Vector2Int.zero, out var visible), Is.True); Assert.That(visible.Level, Is.Zero);
            state.SetFogVisibility(new[] { new DungeonTraversalPosition(upper) });
            Assert.That(state.TryGetVisibleSurface(Vector2Int.zero, out visible), Is.True); Assert.That(visible.Level, Is.EqualTo(1));
        }

        [Test]
        public void OffscreenObstacleKeepsPhysicsLiveAndRestoresPresentationAtCameraReturn()
        {
            var root = new GameObject("Offscreen obstacle", typeof(Grid));
            var definition = ScriptableObject.CreateInstance<Godmist.Data.Dungeons.ObstacleDungeonObjectDefinition>();
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            var texture = new Texture2D(4, 4); var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4);
            try
            {
                var tiles = new GameObject("Tiles", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>(); Set(view, "floorTilemap", tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>()); Set(view, "floorTile", tile);
                Set(view, "wallSegmentPrefab", UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var map = new DungeonFloorMap(); map.TryAddFloorCell(Vector2Int.zero, "room"); map.TryAddFloorCell(new Vector2Int(100, 0), "room"); view.Rebuild(map);
                var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
                var instance = new ObstacleDungeonObjectInstance("obstacle", definition, new Vector2Int(100, 0)); floor.TryAddObject(instance);
                floor.Fog.SetVisible(map.FloorCells.Select(c => new DungeonTraversalPosition(new DungeonSurfacePosition(c))));
                floor.Fog.Objects[instance.InstanceId] = new DungeonObjectKnowledge { Active = true }; view.ApplyFog(floor);
                var art = new GameObject("Obstacle", typeof(SpriteRenderer), typeof(BoxCollider2D)); art.transform.SetParent(root.transform);
                var renderer = art.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
                var obstacle = art.AddComponent<DungeonObstacleView>(); Set(obstacle, "blockingCollider", art.GetComponent<BoxCollider2D>());
                obstacle.Initialize(instance, view.CellToWorldCenter(instance.Position)); obstacle.BindElevationView(view);
                var cameraRoot = new GameObject("Camera", typeof(Camera)); cameraRoot.transform.SetParent(root.transform);
                var camera = cameraRoot.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5; camera.transform.position = Vector3.back * 10;
                view.ConfigurePresentationCamera(camera); Invoke(obstacle, "LateUpdate");
                Assert.That(renderer.forceRenderingOff, Is.True); Assert.That(art.GetComponent<BoxCollider2D>().enabled, Is.EqualTo(instance.BlocksMovement));
                Assert.That(art.GetComponent<UnityEngine.Rendering.SortingGroup>().enabled, Is.False);
                camera.transform.position += Vector3.right * 100; Invoke(obstacle, "LateUpdate");
                Assert.That(renderer.forceRenderingOff, Is.False); Assert.That(art.GetComponent<UnityEngine.Rendering.SortingGroup>().enabled, Is.True);
                camera.transform.position -= Vector3.right * 100; Invoke(obstacle, "LateUpdate"); instance.TryDeactivate(); Invoke(obstacle, "LateUpdate");
                Assert.That(art.GetComponent<BoxCollider2D>().enabled, Is.False, "Offscreen destruction still updates gameplay physics.");
                camera.transform.position += Vector3.right * 100; Invoke(obstacle, "LateUpdate"); Assert.That(renderer.forceRenderingOff, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(definition); }
        }
        [Serializable] sealed class Metric { public string name; public double medianMs, maxMs; }
        [Serializable] sealed class Metrics { public Metric[] values; }
    }
}
