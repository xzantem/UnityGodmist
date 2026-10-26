using System;
using System.Linq;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Lighting;
using UnityEngine;

public static partial class VS23DungeonLightingSetup
{
    private static void ValidateFogLightBudget(DungeonSilhouetteLighting light, DungeonFloorMapView map, Camera camera)
    {
        var fog = map.FogFloor.Fog;
        var visible = fog.Visible.ToArray();
        bool debug = fog.DebugFogDisabled;
        var cameraPosition = camera.transform.position;
        var actor = light.gameObject.scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<ExplorationCharacterView>())
            .First(view => view.IsVisibleForInspection && view.BodyRenderer.enabled);
        var actorPosition = actor.transform.position;
        try
        {
            if (fog.DebugFogDisabled) fog.ToggleDebugFog();
            light.SendMessage("LateUpdate");
            int lights = light.ActiveTorchCount;
            if (lights == 0 || lights > 8) throw new Exception("Invalid local torch budget");
            fog.SetVisible(Array.Empty<DungeonTraversalPosition>(), Array.Empty<DungeonTraversalPosition>());
            light.SendMessage("LateUpdate");
            if (light.ActiveTorchCount != lights)
                throw new Exception("Fog switched off nearby torch illumination");
            if (light.GetComponentsInChildren<SpriteRenderer>().Any(renderer => renderer.name == "Wall torch" && renderer.enabled))
                throw new Exception("Hidden torch fixture leaked through fog");
            fog.SetVisible(visible, Array.Empty<DungeonTraversalPosition>());
            fog.ToggleDebugFog();
            light.SendMessage("LateUpdate");
            if (light.ActiveTorchCount != lights)
                throw new Exception("Debug fog changed local light selection");
            int casters = light.LastCasterCount;
            int draws = light.LastShadowDrawCount;
            actor.transform.position += new Vector3(1000, 1000, 0);
            light.SendMessage("LateUpdate");
            if (light.LastCasterCount >= casters || light.LastShadowDrawCount >= draws)
                throw new Exception("Far-off revealed actor still incurred shadow rendering");
            actor.transform.position = actorPosition;
            camera.transform.position += new Vector3(1000, 1000, 0);
            light.SendMessage("LateUpdate");
            if (light.ActiveTorchCount != 0 || light.LastShadowDrawCount != 0)
                throw new Exception("Offscreen lights still incurred shadow rendering");
            Debug.Log("FOG TORCH BUDGET PASS localLights="+lights+" casters="+casters+" shadowDraws="+draws
                +" distantCameraDraws="+light.LastShadowDrawCount);
        }
        finally
        {
            actor.transform.position = actorPosition;
            camera.transform.position = cameraPosition;
            fog.SetVisible(visible, Array.Empty<DungeonTraversalPosition>());
            if (fog.DebugFogDisabled != debug) fog.ToggleDebugFog();
            light.SendMessage("LateUpdate");
        }
    }

    private static void ValidateTorchSpectrum()
    {
        var cameraObject = new GameObject("Torch spectrum check camera");
        var spriteObject = new GameObject("Torch spectrum check surface");
        var material = new Material(Shader.Find("Godmist/Dungeon/Silhouette Floor"));
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,2,2), new Vector2(.5f,.5f), 2);
        // Read linear HDR values so display gamma and an 8-bit target's
        // saturation cannot masquerade as a change in source spectrum.
        var target = new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var image = new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);
        var previous = RenderTexture.active;
        try
        {
            spriteObject.layer = 31;
            var renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.allowHDR = true;
            camera.orthographicSize = .25f;
            camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1<<31;
            target.Create();
            camera.targetTexture = target;
            material.SetFloat("_Ambient",0);
            material.SetFloat("_ReceiveShadows",0);
            material.SetInt("_TorchCount",1);
            material.SetVectorArray("_Torches", new[] { new Vector4(0,0,5,1) });
            material.SetVectorArray("_TorchColors", new[] { new Vector4(1.45f,.55f,.14f,1) });
            var colors = new Color[3];
            for (int index=0; index<3; index++)
            {
                float distance = new[] { .5f, 2f, 4f }[index];
                spriteObject.transform.position = new Vector3(distance,0,0);
                cameraObject.transform.position = new Vector3(distance,0,-10);
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,32,32),0,0);
                image.Apply();
                colors[index] = image.GetPixel(16,16);
                if (colors[index].r < .05f || colors[index].g/colors[index].r > .85f
                    || colors[index].b/colors[index].r > .7f)
                    throw new Exception("Torch pool lost warm color at distance "+distance+": "+colors[index]);
            }
            if (colors[2].r >= colors[0].r) throw new Exception("Torch illumination does not attenuate");
            if (Mathf.Abs(colors[0].g/colors[0].r-colors[2].g/colors[2].r) > .15f)
                throw new Exception("Torch spectrum changes with distance");
            Debug.Log("TORCH WARM SPECTRUM PASS near="+colors[0]+" middle="+colors[1]+" far="+colors[2]);
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(spriteObject);
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(image);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
