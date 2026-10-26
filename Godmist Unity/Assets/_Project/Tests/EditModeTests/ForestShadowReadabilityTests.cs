using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    public sealed class ForestShadowReadabilityTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/forest-night-readability-20261022"));
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        static void Call(object owner, string name, params object[] args) =>
            owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        static void RefreshAtFixedTorchPhase(DungeonSilhouetteLighting lighting)
        {
            // Force a scheduled tick while keeping identical torch flicker in A/B renders.
            typeof(DungeonSilhouetteLighting).GetField("lastLightingFrame", Private).SetValue(lighting, int.MinValue);
            Call(lighting, "RefreshLighting", 1f);
        }

        [TestCase(1f)]
        [TestCase(.4f)]
        public void ActorReadabilityPreservesAuthoredSilhouetteWithoutAddingContour(float opacity)
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../LocalHandoffs/forest-actor-contours-20261022"));
            Directory.CreateDirectory(output);
            var root = new GameObject("Actor contour GPU regression");
            var material = new Material(Shader.Find("Godmist/Dungeon/Torch Body"));
            var target = new RenderTexture(1024, 384, 24);
            var pixels = new Texture2D(1024, 384, TextureFormat.RGBA32, false);
            var priorActive = RenderTexture.active;
            var camera = new GameObject("Actor contour camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            var views = new List<SpriteRenderer>();
            try
            {
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                material.SetFloat("_Ambient", .8f);
                material.SetFloat("_ForestShadowDetail", .3f);
                material.SetFloat("_IsActor", 1);
                var names = new[] { "jerzy-front-64-v1", "wladimir-front-64-v1",
                    "alison-front-64-v2", "reimond-front-64-v1" };
                for (int i = 0; i < names.Length; i++)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/_Project/Art/Characters/Sprites/VS22/" + names[i] + ".png");
                    Assert.That(sprite, Is.Not.Null, names[i]);
                    var view = new GameObject(names[i]).AddComponent<SpriteRenderer>();
                    view.transform.SetParent(root.transform, false); view.gameObject.layer = 30;
                    view.transform.position = new Vector3(i * 2.5f, 0);
                    view.sprite = sprite; view.sharedMaterial = material;
                    view.color = new Color(1, 1, 1, opacity);
                    var uv = sprite.uv;
                    var block = new MaterialPropertyBlock();
                    block.SetVector("_ForestPropUV", new Vector4(uv.Min(p => p.x), uv.Min(p => p.y),
                        uv.Max(p => p.x), uv.Max(p => p.y)));
                    block.SetVector("_ForestPropTexel", new Vector4(1f / sprite.texture.width, 1f / sprite.texture.height, 0, 0));
                    view.SetPropertyBlock(block); views.Add(view);
                }
                camera.transform.position = new Vector3(3.75f, 1, -10);
                camera.orthographic = true; camera.orthographicSize = 1.875f; camera.aspect = 1024f / 384;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 30; camera.targetTexture = target;
                Color32[] Render(float readability, float profile, string label)
                {
                    foreach (var view in views)
                    {
                        var block = new MaterialPropertyBlock(); view.GetPropertyBlock(block);
                        block.SetFloat("_ForestPropReadability", readability);
                        block.SetFloat("_ForestReadabilityProfile", profile);
                        view.SetPropertyBlock(block);
                    }
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1024, 384), 0, 0); pixels.Apply();
                    if (label != null) File.WriteAllBytes(Path.Combine(output,
                        FormattableString.Invariant($"actors-{opacity:F1}-{label}.png")), pixels.EncodeToPNG());
                    return pixels.GetPixels32();
                }
                var source = Render(0, 1, "source");
                var corrected = Render(.55f, 1, "corrected");
                Assert.That(source.Count(c => c.a > 0), Is.GreaterThan(1000), "Real actor sprites must render.");
                int changedAlpha = Enumerable.Range(0, source.Length).Count(i => source[i].a != corrected[i].a);
                Assert.That(changedAlpha, Is.Zero,
                    "Readability must not expand or change alpha of native actor pixels, including faded edges.");
                var props = Render(.55f, 2, null);
                int expanded = Enumerable.Range(0, source.Length).Count(i => source[i].a == 0 && props[i].a > 0);
                Assert.That(expanded, Is.GreaterThan(100), "The probe must detect contour dilation when enabled for props.");
                File.WriteAllText(Path.Combine(output, FormattableString.Invariant($"alpha-{opacity:F1}.txt")),
                    $"Actor alpha changes={changedAlpha}; positive-control prop contour pixels={expanded}");
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = priorActive;
                Object.DestroyImmediate(root); Object.DestroyImmediate(material);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [TestCase("Godmist/Dungeon/Silhouette Floor")]
        [TestCase("Godmist/Dungeon/Torch Body")]
        [TestCase("Godmist/Dungeon/Torch Wall")]
        public void ShadowToneGpuPreservesHueBlackAndTransparency(string shaderName)
        {
            var root = new GameObject("Forest shadow tone GPU probe");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(Color.white, 4).ToArray()); texture.Apply();
            var material = new Material(Shader.Find(shaderName));
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f, 1);
            var target = new RenderTexture(32, 32, 24);
            var pixels = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var priorActive = RenderTexture.active;
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            try
            {
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                var view = new GameObject("Swatch").AddComponent<SpriteRenderer>();
                view.transform.SetParent(root.transform, false); view.gameObject.layer = 30;
                view.sprite = sprite; view.sharedMaterial = material;
                material.SetFloat("_Ambient", 1f);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 30; camera.targetTexture = target;
                Color Render(Color source, float strength)
                {
                    texture.SetPixels(Enumerable.Repeat(source, 4).ToArray()); texture.Apply();
                    material.SetFloat("_ForestShadowDetail", strength);
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); pixels.Apply();
                    return pixels.GetPixel(16, 16);
                }
                var dark = new Color(.12f, .08f, .04f, .65f);
                var before = Render(dark, 0); var after = Render(dark, 1);
                // The PNG/readback is sRGB in a linear project; compare hue in
                // the shader's working space, not gamma-encoded channel ratios.
                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                { before = before.linear; after = after.linear; }
                Assert.That(after.r, Is.GreaterThan(before.r * 1.2f));
                float gain = after.r / before.r;
                Assert.That(after.g, Is.EqualTo(before.g * gain).Within(2f / 255));
                Assert.That(after.b, Is.EqualTo(before.b * gain).Within(2f / 255));
                Assert.That(after.a, Is.EqualTo(before.a).Within(1f / 255));
                var black = Render(new Color(0, 0, 0, 1), 1);
                Assert.That(black.r + black.g + black.b, Is.Zero);
                var brightBefore = Render(Color.white, 0); var brightAfter = Render(Color.white, 1);
                Assert.That(brightAfter.r, Is.EqualTo(brightBefore.r).Within(1f / 255));
                Assert.That(brightAfter.g, Is.EqualTo(brightBefore.g).Within(1f / 255));
                Assert.That(brightAfter.b, Is.EqualTo(brightBefore.b).Within(1f / 255));
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = priorActive;
                Object.DestroyImmediate(root); Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture); Object.DestroyImmediate(material);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }

        [TestCase("dungeon.forest.expedition")]
        [TestCase("dungeon.catacombs.expedition")]
        public void ProductionLightingRetainsDetailsWithoutChangingFogOrOtherBiome(string id)
        {
            Directory.CreateDirectory(Output);
            bool forest = id.Contains("forest");
            var fixture = new DungeonEntryPerformanceTests
            {
                OutputOverride = Output, StageOverride = forest ? "forest" : "catacombs", SeedOverride = 171026
            };
            fixture.FrameProbe = (bootstrap, session) =>
            {
                var map = Read<DungeonFloorMapView>(bootstrap, "floorMapView");
                var lighting = Read<DungeonSilhouetteLighting>(bootstrap, "silhouetteLighting");
                var party = Read<DungeonPartySpawner>(bootstrap, "partySpawner").PartyController;
                var actors = party.ActiveMemberViews.ToArray();
                var enemies = Read<DungeonEncounterSpawner>(bootstrap, "encounterSpawner").EnemyViews;
                var floor = session.ActiveDungeon.Floors[0];
                var camera = Read<Camera>(lighting, "worldCamera");
                foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.gameObject.layer = 30;
                var target = new RenderTexture(1280, 720, 24);
                var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
                var priorTarget = camera.targetTexture;
                var priorMask = camera.cullingMask;
                var priorActive = RenderTexture.active;
                float configured = Read<float>(lighting, "forestAmbient");
                float configuredDetail = Read<float>(lighting, "forestShadowDetail");
                var report = new List<string>();
                try
                {
                    camera.targetTexture = target; camera.cullingMask = 1 << 30;
                    camera.aspect = 16f / 9; camera.orthographicSize = 5.5f;
                    var entrance = map.WorldToCell(actors[0].Position);
                    var room = floor.Rooms.First(r => r.RoomType != DungeonRoomType.Start);
                    var nearRoom = room.LayoutPosition + Vector2Int.one * 3;
                    var interior = floor.Map.FloorCells.OrderBy(c => (c - nearRoom).sqrMagnitude).First();
                    foreach (var region in new[] { ("entrance", entrance), ("interior", interior) })
                    {
                        var cells = floor.Map.FloorCells.Where(c => !floor.Objects.OfType<ObstacleDungeonObjectInstance>()
                            .Any(o => o.IsActive && o.BlocksMovement && o.OccupiesCell(c)))
                            .OrderBy(c => (c - region.Item2).sqrMagnitude).ThenBy(c => c.y).ThenBy(c => c.x)
                            .Take(actors.Length).ToArray();
                        for (int i = 0; i < actors.Length; i++) actors[i].SetPosition(map.CellToWorldCenter(cells[i]));
                        camera.transform.position = new Vector3(actors[0].Position.x, actors[0].Position.y + 1.5f, -10);
                        new DungeonFogSystem().Update(floor, actors.Select(a =>
                            new DungeonTraversalPosition(new DungeonSurfacePosition(map.WorldToCell(a.Position)))).ToArray());
                        map.ApplyFog(floor);
                        foreach (var a in actors) Call(a, "LateUpdate");
                        foreach (var a in enemies) if (a != null) Call(a, "LateUpdate");
                        Call(lighting, "RefreshForestPartyReveal");
                        Call(lighting, "AdvanceForestCanopyFade", .5f);
                        var knowledge = floor.Map.FloorCells.Select(c => floor.Fog.Get(
                            new DungeonTraversalPosition(new DungeonSurfacePosition(c)))).ToArray();
                        Color32[] baseline = null;
                        var values = new[] { .22f, configured };
                        foreach (float ambient in values)
                        {
                            typeof(DungeonSilhouetteLighting).GetField("forestAmbient", Private).SetValue(lighting, ambient);
                            typeof(DungeonSilhouetteLighting).GetField("forestShadowDetail", Private).SetValue(lighting,
                                ambient == configured ? configuredDetail : 0f);
                            RefreshAtFixedTorchPhase(lighting);
                            float expected = forest ? ambient : Read<float>(lighting, "ambient");
                            foreach (string field in new[] { "floorMaterial", "bodyMaterial", "flameMaterial" })
                            {
                                var material = Read<Material>(lighting, field);
                                if (material != null) Assert.That(material.GetFloat("_Ambient"), Is.EqualTo(expected).Within(.0001f), field);
                                if (material != null) Assert.That(material.GetFloat("_ForestShadowDetail"),
                                    Is.EqualTo(forest && ambient == configured ? configuredDetail : 0f), field);
                            }
                            SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active = target;
                            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                            var data = pixels.GetPixels32();
                            var label = FormattableString.Invariant($"{(forest ? "forest" : "catacombs")}-{region.Item1}-{ambient:F2}");
                            File.WriteAllBytes(Path.Combine(Output, label + ".png"), pixels.EncodeToPNG());
                            if (baseline == null) baseline = data;
                            else if (!forest) Assert.That(data, Is.EqualTo(baseline), "Forest fill must not change Catacombs pixels.");
                            else
                            {
                                var dark = Enumerable.Range(0, baseline.Length).Where(i =>
                                    baseline[i].r + baseline[i].g + baseline[i].b > 6 &&
                                    baseline[i].r + baseline[i].g + baseline[i].b < 90).ToArray();
                                Assert.That(dark.Length, Is.GreaterThan(1000));
                                double before = dark.Average(i => (baseline[i].r + baseline[i].g + baseline[i].b) / 3d);
                                double after = dark.Average(i => (data[i].r + data[i].g + data[i].b) / 3d);
                                Assert.That(after, Is.GreaterThan(before * 1.25), "Shadow details should get useful diffuse fill.");
                                Assert.That(after, Is.LessThan(before * 2.2), "The night profile must retain darkness instead of looking like daylight.");
                                report.Add($"{label}: dark pixels={dark.Length}, mean {before:F2} -> {after:F2}");
                            }
                            Assert.That(floor.Map.FloorCells.Select(c => floor.Fog.Get(
                                new DungeonTraversalPosition(new DungeonSurfacePosition(c)))), Is.EqualTo(knowledge));
                        }
                        if (forest)
                        {
                            typeof(DungeonSilhouetteLighting).GetField("forestAmbient", Private).SetValue(lighting, configured);
                            typeof(DungeonSilhouetteLighting).GetField("forestShadowDetail", Private).SetValue(lighting, configuredDetail);
                            RefreshAtFixedTorchPhase(lighting);
                            floor.Fog.SetVisible(Array.Empty<DungeonTraversalPosition>()); map.ApplyFog(floor);
                            camera.Render(); RenderTexture.active = target;
                            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                            File.WriteAllBytes(Path.Combine(Output, "forest-" + region.Item1 + "-remembered.png"), pixels.EncodeToPNG());
                            var colors = Read<List<Color>>(map, "fogColors").Where(c => c.a > .001f).ToArray();
                            Assert.That(colors, Is.Not.Empty);
                            Assert.That(colors.All(c => Mathf.Abs(c.r - 1f) < .001f && Mathf.Abs(c.g - 1f) < .001f && Mathf.Abs(c.b - 1f) < .001f), Is.True);
                        }
                    }
                    File.WriteAllLines(Path.Combine(Output, (forest ? "forest" : "catacombs") + "-measurements.txt"), report);
                }
                finally
                {
                    typeof(DungeonSilhouetteLighting).GetField("forestAmbient", Private).SetValue(lighting, configured);
                    typeof(DungeonSilhouetteLighting).GetField("forestShadowDetail", Private).SetValue(lighting, configuredDetail);
                    camera.targetTexture = priorTarget; camera.cullingMask = priorMask; RenderTexture.active = priorActive;
                    Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
                }
            };
            fixture.MeasureProductionFloorEntry(id, DungeonLength.Long);
        }
    }
}
