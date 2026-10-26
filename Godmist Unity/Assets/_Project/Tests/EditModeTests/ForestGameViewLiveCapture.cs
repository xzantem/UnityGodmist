using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godmist.Tests.EditMode
{
    // Opt-in, bounded Game View capture. Never invokes Camera.Render or changes
    // camera targets: those additional renders may conceal a render-order bug.
    [InitializeOnLoad]
    internal static class ForestGameViewLiveCapture
    {
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/fog-seed-959042448-20261024/game-view"));
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static bool running;
        static double nextCheck;
        static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
        static ForestGameViewLiveCapture() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + .25;
            var request = Path.Combine(Output, "capture.request");
            if (running || !EditorApplication.isPlaying || EditorApplication.isPaused
                || EditorApplication.isCompiling || !File.Exists(request)) return;
            var lighting = Enumerable.Range(0, SceneManager.sceneCount)
                .SelectMany(i => SceneManager.GetSceneAt(i).GetRootGameObjects())
                .SelectMany(root => root.GetComponentsInChildren<DungeonSilhouetteLighting>())
                .FirstOrDefault(value => value.isActiveAndEnabled
                    && Read<DungeonFloorMapView>(value, "terrain")?.UsesVegetationPresentation == true);
            if (lighting == null) return;
            var map = Read<DungeonFloorMapView>(lighting, "terrain");
            if (map.FogFloor == null || Read<SpriteRenderer>(map, "forestKnowledgeRenderer") == null) return;
            File.Delete(request); running = true;
            lighting.StartCoroutine(Capture(lighting));
        }

        static IEnumerator Capture(DungeonSilhouetteLighting lighting)
        {
            var map = Read<DungeonFloorMapView>(lighting, "terrain");
            var camera = map.PresentationCamera;
            var trees = Read<List<SpriteRenderer>>(lighting, "localWallRenderers").Where(r => r != null).ToArray();
            var block = new MaterialPropertyBlock();
            var dungeon = Read<Godmist.Views.Exploration.ExplorationPartyController>(lighting, "party").Session.ActiveDungeon;
            var wait = new WaitForEndOfFrame();
            double deadline = EditorApplication.timeSinceStartup + 24, next = 0;
            int frame = 0;
            var lines = new List<string> { $"seed={dungeon.Seed} length={dungeon.Length} level={dungeon.DungeonLevel} floor={dungeon.CurrentFloorIndex}" };
            File.WriteAllText(Path.Combine(Output, "state.txt"), "Capturing final Game View for 24 seconds.");
            try
            {
                while (lighting != null && EditorApplication.isPlaying && EditorApplication.timeSinceStartup < deadline)
                {
                    yield return wait;
                    if (Time.realtimeSinceStartupAsDouble < next) continue;
                    next = Time.realtimeSinceStartupAsDouble + .08;
                    var pixels = ScreenCapture.CaptureScreenshotAsTexture();
                    try { File.WriteAllBytes(Path.Combine(Output, $"frame-{frame:000}.png"), pixels.EncodeToPNG()); }
                    finally { UnityEngine.Object.Destroy(pixels); }
                    var fog = map.FogFloor.Fog;
                    var filter = Read<SpriteRenderer>(map, "forestKnowledgeRenderer");
                    var fogRenderer = Read<MeshRenderer>(map, "fogBlendRenderer");
                    var values = Read<Color32[]>(map, "forestKnowledgePixels");
                    uint hash = 2166136261;
                    foreach (var value in values) unchecked { hash = (hash ^ value.a) * 16777619; }
                    lines.Add(FormattableString.Invariant($"frame={frame++} gameFrame={Time.frameCount} time={Time.time:F5} camera={camera.transform.position} zoom={camera.orthographicSize} fogRevision={fog.Revision} visible={fog.Visible.Count} discovered={fog.Discovered.Count} filter={filter.enabled}/{filter.forceRenderingOff}/{filter.sortingOrder}/{filter.sharedMaterial.shader.name}/{hash:X8} fogMesh={fogRenderer.enabled}/{fogRenderer.forceRenderingOff}/{fogRenderer.sortingOrder} passage={lighting.LastForestPassageRevealCount} foreground={lighting.LastForestForegroundRevealCount}"));
                    foreach (var tree in trees)
                    {
                        if (tree == null) continue;
                        tree.GetPropertyBlock(block);
                        lines.Add(FormattableString.Invariant($"tree={tree.GetInstanceID()} name={tree.name} at={tree.transform.position} enabled={tree.enabled}/{tree.forceRenderingOff} color={tree.color} material={tree.sharedMaterial.name} fade={block.GetFloat("_ForestCanopyFade"):F5} reveal={block.GetFloat("_ForestCanopy"):F5} root={block.GetFloat("_ForestCanopyRootTopY"):F5} order={tree.sortingOrder}"));
                    }
                    File.WriteAllLines(Path.Combine(Output, "frames.txt"), lines);
                }
            }
            finally
            {
                running = false;
                File.WriteAllText(Path.Combine(Output, "state.txt"), "Finished: " + frame + " final Game View frames.");
            }
        }
    }
}
