using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godmist.Views.Dungeons;
using Godmist.Views.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Godmist.Tests.EditMode
{
    // Explicit file request, 24 seconds, existing gameplay camera only.
    // No desktop capture, window activation, profiler or gameplay changes.
    [InitializeOnLoad]
    internal static class ForestVisibilityLiveCapture
    {
        static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/forest-thin-dividers-shadow-stability-20261022/live"));
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static double deadline, nextSample;
        static int frame;
        static DungeonSilhouetteLighting lighting;
        static DungeonFloorMapView terrain;
        static readonly List<string> lines = new();
        static T Read<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        static ForestVisibilityLiveCapture() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextSample) return;
            nextSample = EditorApplication.timeSinceStartup + .25;
            if (deadline == 0)
            {
                string request = Path.Combine(Output, "capture.request");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused || !File.Exists(request)) return;
                var roots = Enumerable.Range(0, SceneManager.sceneCount)
                    .SelectMany(i => SceneManager.GetSceneAt(i).GetRootGameObjects());
                lighting = roots.SelectMany(r => r.GetComponentsInChildren<DungeonSilhouetteLighting>())
                    .FirstOrDefault(l => l.isActiveAndEnabled && Read<DungeonFloorMapView>(l,"terrain") is {UsesVegetationPresentation:true});
                if (lighting == null)
                {
                    File.WriteAllText(Path.Combine(Output,"state.txt"),"Waiting for active forest lighting. Scenes: "
                        +string.Join(",",Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i).name)));
                    return;
                }
                terrain = Read<DungeonFloorMapView>(lighting, "terrain");
                if (terrain == null || !terrain.UsesVegetationPresentation) return;
                File.Delete(request); frame = 0; lines.Clear();
                var session=Read<Godmist.Views.Exploration.ExplorationPartyController>(lighting,"party")?.Session;
                var dungeon=session?.ActiveDungeon;
                lines.Add($"dungeon={dungeon?.DungeonId} seed={dungeon?.Seed} length={dungeon?.Length} level={dungeon?.DungeonLevel} floor={dungeon?.CurrentFloorIndex} position={dungeon?.PlayerPosition}");
                deadline = EditorApplication.timeSinceStartup + 24;
                File.WriteAllText(Path.Combine(Output, "state.txt"), "Capturing forest gameplay for 24 seconds.");
            }
            if (!EditorApplication.isPlaying || lighting == null || EditorApplication.timeSinceStartup > deadline)
            {
                deadline = 0;
                File.WriteAllLines(Path.Combine(Output, "frames.txt"), lines);
                File.WriteAllText(Path.Combine(Output, "state.txt"), "Finished: " + frame + " gameplay frames.");
                return;
            }
            var camera = Read<Camera>(lighting, "worldCamera");
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var pixels = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, $"frame-{frame:000}.png"), pixels.EncodeToPNG());
                var filter = Read<SpriteRenderer>(terrain, "forestKnowledgeRenderer");
                var contact = Read<object>(lighting, "contactShadows");
                var mesh = contact == null ? null : Read<MeshRenderer>(contact, "renderer");
                var mask = Read<RenderTexture>(lighting, "mask");
                var floor = Read<UnityEngine.Tilemaps.TilemapRenderer>(lighting, "floorReceiver");
                var fogMesh = Read<MeshRenderer>(terrain, "fogBlendRenderer");
                var casters = Read<System.Collections.IDictionary>(lighting, "entries");
                var states = casters.Keys.Cast<SpriteRenderer>().Where(r => r != null)
                    .Select(r => $"{r.GetInstanceID()}:{r.enabled}/{r.forceRenderingOff}/{r.color.a:F2}/{r.sharedMaterial?.shader.name}");
                lines.Add($"frame={frame++} time={Time.time:F3} camera={camera.transform.position} fogDisabled={terrain.FogFloor?.Fog.DebugFogDisabled}"
                    + $" visible={terrain.FogFloor?.Fog.Visible.Count} discovered={terrain.FogFloor?.Fog.Discovered.Count}"
                    + $" filter={filter?.enabled}/{filter?.sortingOrder} contacts={mesh?.enabled}/{mesh?.forceRenderingOff}/{mesh?.sortingOrder}"
                    + $" floorOrder={floor?.sortingOrder} fogOrder={fogMesh?.sortingOrder}"
                    + $" quads={lighting.ContactShadowQuadCount} sources={lighting.ActiveTorchCount} casters={lighting.LastCasterCount}"
                    + $" maskReady={mask != null && mask.IsCreated()} rebuilds={lighting.ShadowMaskRebuildCount} casterStates={string.Join(";",states)}");
                var inputs=Read<Vector4[]>(lighting,"lightData");
                lines.Add("lights="+string.Join(";",inputs.Take(lighting.ActiveTorchCount).Select(v=>$"{v.x:F4}/{v.y:F4}/{v.z:F4}/{v.w:F4}"))
                    +" shadowBounds="+Read<Rect>(lighting,"bounds")+" staticRebuilds="+lighting.StaticShadowMaskRebuildCount
                    +" contactsBounds="+mesh?.bounds+" contactsPosition="+mesh?.transform.position);
                if(mask!=null && mask.IsCreated())lines.Add("shadowPixels="+ShadowSamples(mask,lighting.ActiveTorchCount));
            }
            catch (Exception error)
            {
                lines.Add(error.ToString()); deadline = EditorApplication.timeSinceStartup;
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Object.DestroyImmediate(pixels); RenderTexture.ReleaseTemporary(target);
                File.WriteAllLines(Path.Combine(Output, "frames.txt"), lines);
            }
        }

        static string ShadowSamples(RenderTexture mask,int count)
        {
            var previous=RenderTexture.active;
            int size=Math.Min(128,Math.Min(mask.width,mask.height));
            var pixels=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
            var samples=new List<string>();
            try
            {
                for(int slice=0;slice<count;slice++)
                {
                    Graphics.SetRenderTarget(mask,0,CubemapFace.Unknown,slice);
                    pixels.ReadPixels(new Rect((mask.width-size)/2,(mask.height-size)/2,size,size),0,0);pixels.Apply();
                    var values=pixels.GetRawTextureData<Color32>();uint hash=2166136261;int shaded=0;
                    for(int i=0;i<values.Length;i++)unchecked{hash=(hash^values[i].r)*16777619;if(values[i].r>0)shaded++;}
                    samples.Add($"{slice}:{hash:X8}/{shaded}");
                }
                return string.Join(";",samples);
            }
            finally{RenderTexture.active=previous;Object.DestroyImmediate(pixels);}
        }
    }
}
