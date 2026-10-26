using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Godmist.Core.Enums;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public sealed class DungeonLightingOptimizationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Read<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/dungeon-lighting-pass-20261016"));

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void MeasureProductionLightingAndPreserveShadowGeometry(string id)
        {
            var stage = File.ReadAllText(Path.Combine(Output, "stage.txt")).Trim();
            Assert.That(typeof(DungeonSilhouetteLighting).GetField("localWallRenderers", Private) != null,
                Is.EqualTo(stage == "after"), "The imported lighting assembly must match the requested baseline/final implementation.");
            var metrics = new List<Metric>();
            var captures = new List<Capture>();
            var probe = new DungeonEntryPerformanceTests { OutputOverride = Output, StageOverride = "lighting-" + stage };
            probe.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var rig = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var camera = Read<Camera>(rig, "worldCamera");
                camera.aspect = 16f / 9f;
                var floor = session.ActiveDungeon.Floors[0];
                new DungeonFogSystem().Update(floor, party.ActiveMemberViews.Select(v => v.DungeonPosition).ToArray());
                map.ApplyFog(floor);
                var leader = party.ActiveLeaderTransform.position;
                camera.transform.position = new Vector3(leader.x, leader.y, -10);
                var refresh = typeof(DungeonSilhouetteLighting).GetMethod("LateUpdate", Private);
                void Refresh() { typeof(DungeonSilhouetteLighting).GetField("lastLightingFrame", Private).SetValue(rig, int.MinValue); refresh.Invoke(rig, null); }
                void Measure(string name)
                {
                    Refresh();
                    var samples = new double[15];
                    for (var i = 0; i < samples.Length; i++) { var watch = Stopwatch.StartNew(); Refresh(); samples[i] = watch.Elapsed.TotalMilliseconds; }
                    Array.Sort(samples); metrics.Add(new Metric { name = name, medianMs = samples[7], maxMs = samples[14] });
                    File.WriteAllText(Path.Combine(Output, stage + "-" + id + "-timings.json"), JsonUtility.ToJson(new Metrics { values = metrics.ToArray() }, true));
                }
                void Snapshot(string name)
                {
                    Refresh();
                    if (stage == "after") CheckCoverage(map, Read<Rect>(rig, "cameraGroundBounds"), id.Contains("forest"));
                    var contact = Read<object>(rig, "contactShadows");
                    var mesh = Read<Mesh>(contact, "mesh");
                    using var stream = new MemoryStream();
                    using var writer = new BinaryWriter(stream);
                    foreach (var v in mesh.vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                    foreach (var c in mesh.colors32) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
                    foreach (var i in mesh.triangles) writer.Write(i);
                    captures.Add(new Capture { name = name, contact = Hash(stream.ToArray()), mask = MaskHash(rig.ShadowMask, Math.Max(1, Read<System.Collections.ICollection>(rig, "activeTorches").Count)), casters = rig.LastCasterCount });
                }
                Measure("normal fog"); Snapshot("normal fog");
                floor.Fog.ToggleDebugFog(); map.ApplyFog(floor);
                Measure("revealed map"); Snapshot("revealed map");
                var rooms = floor.Rooms.Where(r => r != null).ToArray();
                foreach (var index in new[] { rooms.Length / 3, rooms.Length * 2 / 3 })
                {
                    var position = map.CellToWorldCenter(rooms[index].LayoutPosition + Vector2Int.one * 2);
                    camera.transform.position = new Vector3(position.x, position.y, -10);
                    Snapshot("pan " + index);
                }
                camera.orthographicSize *= 1.5f; Snapshot("zoom");
            };
            probe.MeasureProductionFloorEntry(id, DungeonLength.Long);
            var json = JsonUtility.ToJson(new Captures { values = captures.ToArray() }, true);
            File.WriteAllText(Path.Combine(Output, stage + "-" + id + "-shadows.json"), json);
            if (stage == "after") Assert.That(json, Is.EqualTo(File.ReadAllText(Path.Combine(Output, "before-" + id + "-shadows.json"))), "Contact geometry and projected shadow pixels must remain identical.");
        }
        static void CheckCoverage(DungeonFloorMapView map, Rect viewport, bool forest)
        {
            static bool Intersects(SpriteRenderer r, Rect rect)
            {
                if (r == null) return false;
                var bounds = r.bounds;
                return bounds.max.x >= rect.xMin && bounds.min.x <= rect.xMax
                    && bounds.max.y >= rect.yMin && bounds.min.y <= rect.yMax;
            }
            var walls = new List<SpriteRenderer>(); var ground = new List<SpriteRenderer>();
            map.CollectLightingWallRenderers(viewport, walls); map.CollectLightingGroundRenderers(viewport, ground);
            // The revealed long Catacombs contain many renderers. Repeated
            // NUnit collection scans make this membership check quadratic.
            var selectedWalls = new HashSet<SpriteRenderer>(walls);
            var selectedGround = new HashSet<SpriteRenderer>(ground);
            foreach (var renderer in map.LightingWallRenderers.Where(r => Intersects(r, viewport)))
                Assert.That(selectedWalls.Contains(renderer), Is.True, "Broad phase must retain every potentially lit wall, even while hidden.");
            foreach (var renderer in map.LightingPresentationRenderers.Where(r => Intersects(r, viewport)))
                Assert.That(selectedGround.Contains(renderer), Is.True, "Broad phase must retain every potentially lit underlay/prop.");
            if (forest)
            {
                Assert.That(walls.Count, Is.LessThan(map.LightingWallRenderers.Count() / 2));
                Assert.That(ground.Count, Is.LessThan(map.LightingPresentationRenderers.Count() / 2));
            }
        }

        [Test]
        public void BoundaryCoverageFollowsOwnerTransformUnregistrationAndMapRebuild()
        {
            var root = new GameObject("Boundary lighting coverage", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            try
            {
                var tiles = new GameObject("Floor", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer)); tiles.transform.SetParent(root.transform);
                var view = root.AddComponent<DungeonFloorMapView>();
                typeof(DungeonFloorMapView).GetField("floorTilemap", Private).SetValue(view, tiles.GetComponent<UnityEngine.Tilemaps.Tilemap>());
                typeof(DungeonFloorMapView).GetField("floorTile", Private).SetValue(view, tile);
                typeof(DungeonFloorMapView).GetField("wallSegmentPrefab", Private).SetValue(view, UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonWallSegment.prefab").GetComponent<DungeonWallSegmentView>());
                var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                view.ConfigureVegetationPresentation(catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.forest.expedition").VegetationPresentation);
                var map = new Godmist.Runtime.Dungeons.DungeonFloorMap();
                for (var x = -2; x < 3; x++) for (var y = -2; y < 3; y++) map.TryAddFloorCell(new Vector2Int(x, y), "room");
                foreach (var cell in map.FloorCells)
                    foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                        if (!map.IsWalkable(cell + direction)) map.TryAddWall(cell, cell + direction);
                view.Rebuild(map);
                var first = view.LightingWallRenderers.First();
                var list = new List<SpriteRenderer>();
                Rect Around(SpriteRenderer sprite) => new((Vector2)sprite.bounds.center - Vector2.one * .01f, Vector2.one * .02f);
                var oldViewport = Around(first);
                view.CollectLightingWallRenderers(oldViewport, list); Assert.That(list, Does.Contain(first));
                root.transform.position += new Vector3(100, -100);
                view.CollectLightingWallRenderers(oldViewport, list); Assert.That(list, Is.Empty, "Owner movement must invalidate world-space buckets.");
                view.CollectLightingWallRenderers(Around(first), list); Assert.That(list, Does.Contain(first));
                CheckCoverage(view, Around(first), false);
                var underlay = view.LightingPresentationRenderers.First(r => r.name == "Forest floor under vegetation");
                view.CollectLightingGroundRenderers(Around(underlay), list); Assert.That(list, Does.Contain(underlay));
                view.UnregisterLightingPresentation(underlay);
                view.CollectLightingGroundRenderers(Around(underlay), list); Assert.That(list, Has.No.Member(underlay));
                view.RegisterGroundLightingPresentation(underlay);
                view.CollectLightingGroundRenderers(Around(underlay), list); Assert.That(list, Does.Contain(underlay));
                var replacement = new Godmist.Runtime.Dungeons.DungeonFloorMap(); replacement.TryAddFloorCell(Vector2Int.zero, "new");
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left }) replacement.TryAddWall(Vector2Int.zero, direction);
                view.Rebuild(replacement);
                view.CollectLightingWallRenderers(oldViewport, list); Assert.That(list, Is.Empty);
                view.CollectLightingGroundRenderers(oldViewport, list); Assert.That(list, Has.No.Member(underlay));
                CheckCoverage(view, Around(view.LightingWallRenderers.First()), false);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(tile); }
        }

        [Test]
        public void RuntimeGroundMotionRegistrationAndActorReclassificationStayLive()
        {
            var root = new GameObject("Live lighting registration");
            try
            {
                var map = root.AddComponent<DungeonFloorMapView>();
                var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Godmist.Data.World.GameContentCatalog>("Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset");
                map.ConfigureVegetationPresentation(catalog.DungeonDefinitions.Single(d => d != null && d.Id == "dungeon.forest.expedition").VegetationPresentation);
                var list = new List<SpriteRenderer>(); var viewport = new Rect(-1, -1, 2, 2);
                map.CollectLightingGroundRenderers(viewport, list); // Warm the static index first.
                var prop = new GameObject("Moving dropped item", typeof(SpriteRenderer)); prop.transform.SetParent(root.transform);
                var renderer = prop.GetComponent<SpriteRenderer>(); prop.transform.position = Vector3.one * 100;
                map.RegisterGroundLightingPresentation(renderer, true);
                map.CollectLightingGroundRenderers(viewport, list); Assert.That(list, Does.Contain(renderer));
                prop.transform.position = Vector3.zero;
                map.CollectLightingGroundRenderers(viewport, list); Assert.That(list, Does.Contain(renderer));
                map.RegisterActorLightingPresentation(renderer);
                map.CollectLightingGroundRenderers(viewport, list); Assert.That(list, Has.No.Member(renderer));
                Assert.That(map.ActorLightingPresentationRenderers, Does.Contain(renderer));
                map.RegisterGroundLightingPresentation(renderer);
                map.CollectLightingGroundRenderers(viewport, list); Assert.That(list, Does.Contain(renderer));
                Assert.That(map.ActorLightingPresentationRenderers, Has.No.Member(renderer));
                map.UnregisterLightingPresentation(renderer);
                map.CollectLightingGroundRenderers(viewport, list); Assert.That(list, Has.No.Member(renderer));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        static string MaskHash(RenderTexture mask, int slices)
        {
            var active = RenderTexture.active;
            var image = new Texture2D(mask.width, mask.height, TextureFormat.RGBA32, false, true);
            try
            {
                using var stream = new MemoryStream();
                for (var slice = 0; slice < slices; slice++)
                {
                    Graphics.SetRenderTarget(mask, 0, CubemapFace.Unknown, slice);
                    image.ReadPixels(new Rect(0, 0, mask.width, mask.height), 0, 0); image.Apply();
                    var pixels = image.GetRawTextureData<byte>().ToArray(); stream.Write(pixels, 0, pixels.Length);
                }
                return Hash(stream.ToArray());
            }
            finally { RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(image); }
        }
        [Serializable] sealed class Metric { public string name; public double medianMs, maxMs; }
        [Serializable] sealed class Metrics { public Metric[] values; }
        [Serializable] sealed class Capture { public string name, contact, mask; public int casters; }
        [Serializable] sealed class Captures { public Capture[] values; }
    }
}
