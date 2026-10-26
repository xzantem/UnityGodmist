using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Godmist.Data.Dungeons;
using Godmist.Views.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static partial class VS23DungeonLightingSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity";
    private const string Flag = "DungeonLightingSmoke";
    private static int stage, frames, priorCasters;
    private static double started;
    private static SpriteRenderer hidden;
    private static Camera camera;
    private static Material floorBefore;
    private static Vector2 lightBeforePan;
    private static bool presentationValidated;
    public static void Configure()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var bootstrap = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DungeonSandboxBootstrap>(true)).Single();
        var boot = new SerializedObject(bootstrap);
        var map = new SerializedObject(boot.FindProperty("floorMapView").objectReferenceValue);
        var tilemap = (Tilemap)map.FindProperty("floorTilemap").objectReferenceValue;
        var follow = (ExplorationCameraFollow)boot.FindProperty("cameraFollow").objectReferenceValue;
        var lighting = bootstrap.GetComponent<DungeonSilhouetteLighting>();
        if (lighting == null) lighting = bootstrap.gameObject.AddComponent<DungeonSilhouetteLighting>();
        var data = new SerializedObject(lighting);
        data.FindProperty("worldCamera").objectReferenceValue = follow.GetComponent<Camera>();
        data.FindProperty("floorReceiver").objectReferenceValue = tilemap.GetComponent<TilemapRenderer>();
        data.FindProperty("maskShader").objectReferenceValue = Shader.Find("Godmist/Dungeon/Animated Silhouette Volume Mask");
        data.FindProperty("bodyShader").objectReferenceValue = Shader.Find("Godmist/Dungeon/Torch Body");
        data.FindProperty("floorShader").objectReferenceValue = Shader.Find("Godmist/Dungeon/Silhouette Floor");
        data.FindProperty("torchShader").objectReferenceValue = Shader.Find("Godmist/Dungeon/Pixel Torch");
        data.FindProperty("wallShader").objectReferenceValue = Shader.Find("Godmist/Dungeon/Torch Wall");
        data.FindProperty("ambient").floatValue = .22f;
        const string torchPath="Assets/_Project/Art/Tiles/DungeonTorch32-v3.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(torchPath);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=32; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Custom;
        settings.spritePivot=new Vector2(.5f,3f/32f);
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        data.FindProperty("torchSprite").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(torchPath);
        const string torchMaskPath="Assets/_Project/Art/Tiles/DungeonTorch32-v3-flame-mask.png";
        ConfigureFlameMaskTexture(torchMaskPath);
        data.FindProperty("torchFlameMask").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Texture2D>(torchMaskPath);
        ConfigureInsetSprite("Assets/_Project/Resources/DungeonDecor/wall-grate-inset-24x16.png");
        ConfigureInsetSprite("Assets/_Project/Resources/DungeonDecor/wall-ossuary-inset-24x16.png");
        data.FindProperty("torchRadius").floatValue=5f;
        data.FindProperty("torchColor").colorValue=new Color(1.45f,.55f,.14f,1);

        var profiles = data.FindProperty("contacts"); profiles.arraySize = 2;
        var jerzy = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Sprites/VS22/jerzy-front-64-v1.png").OfType<Sprite>().First();
        var column = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Dungeon/PF_DungeonPillar.prefab")
            .GetComponentsInChildren<SpriteRenderer>(true).First(r => r.sprite != null).sprite;
        SetProfile(profiles.GetArrayElementAtIndex(0), jerzy, Vector2.zero, -4f / 15f, 23f / 192f);
        SetProfile(profiles.GetArrayElementAtIndex(1), column, new Vector2(0, .15625f), 0, 0);
        data.ApplyModifiedPropertiesWithoutUndo();
        boot.FindProperty("silhouetteLighting").objectReferenceValue = lighting; boot.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("DUNGEON LIGHTING CONFIGURED");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
    private static void SetProfile(SerializedProperty p, Sprite sprite, Vector2 foot, float slope, float offset)
    {
        p.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        p.FindPropertyRelative("footLocal").vector2Value = foot;
        p.FindPropertyRelative("slope").floatValue = slope; p.FindPropertyRelative("offset").floatValue = offset;
    }
    private static void ConfigureInsetSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Missing dungeon inset asset: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
    private static void ConfigureFlameMaskTexture(string path)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        if(importer==null) throw new InvalidOperationException("Missing authored torch flame mask: "+path);
        importer.textureType=TextureImporterType.Default;
        importer.textureShape=TextureImporterShape.Texture2D;
        importer.sRGBTexture=false;
        importer.mipmapEnabled=false;
        importer.filterMode=FilterMode.Point;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.isReadable=true;
        importer.SaveAndReimport();
    }
    public static void Smoke()
    {
        // Keep test autosaves completely separate from the user's campaign.
        VolumeProjectionCheck.Run();
        PlayerSettings.productName = "GodmistLightingIsolatedSmoke";
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
        presentationValidated = false;
        SessionState.SetBool(Flag, true); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Resume()
    { if (SessionState.GetBool(Flag, false)) { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; } }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Dungeon lighting smoke timed out, stage " + stage);
            var boot = GameBootstrap.Instance; if (boot == null || boot.IsSceneLoading) return;
            if (stage == 0)
            {
                if (!boot.TryStartNewGame(out var message)) throw new Exception(message);
                stage++; return;
            }
            if (stage == 1)
            {
                if (SceneManager.GetActiveScene().name != "Arungard") return;
                var definition = AssetDatabase.FindAssets("t:DungeonDefinition").Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<DungeonDefinition>).First(d => d != null && d.IsProcedural && d.name.ToLowerInvariant().Contains("catacomb"));
                if (!boot.TryEnterDungeon(definition, 230412)) throw new Exception("Cannot enter test dungeon");
                stage++; return;
            }
            if (SceneManager.GetActiveScene().name != "DungeonSandbox" || ++frames < 20) return;
            var scene = SceneManager.GetActiveScene();
            var light = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DungeonSilhouetteLighting>(true)).Single();
            var serialized = new SerializedObject(light);
            var floor = (TilemapRenderer)serialized.FindProperty("floorReceiver").objectReferenceValue;
            // A presentation rebuild replaces wall SpriteRenderers. Give the
            // scene-owned lighting pass a frame window to rebind their
            // material variants before validating the native wall contract.
            if (stage == 6)
            {
                stage = 2;
                return;
            }
            if (stage == 2)
            {
                var map=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<DungeonFloorMapView>()).Single();
                if (!presentationValidated)
                {
                    var presentation = map.PresentationRenderers.Where(renderer => renderer != null).ToArray();
                    var presentationNames = presentation
                        .Where(renderer => renderer.sprite != null)
                        .GroupBy(renderer => renderer.sprite.name)
                        .ToDictionary(group => group.Key, group => group.Count());
                    var agedCells = map.LightingMap.FloorCells.Count(cell => DungeonFloorMapView.ResolvePresentationFloorVariant(cell) == 1);
                    if (map.PresentationFloorDecorationCount != 0 || map.PresentationWallDecorationCount == 0)
                        throw new Exception("Presentation floor props must be disabled while wall insets remain authored");
                    if (!presentationNames.ContainsKey("wall-grate-inset-24x16")
                        || !presentationNames.ContainsKey("wall-ossuary-inset-24x16"))
                        throw new Exception("Presentation wall run missing grate/ossuary anchors");
                    var northWalls = map.LightingWallRenderers
                        .Where(renderer => renderer != null)
                        .Select(renderer => new
                        {
                            Renderer = renderer,
                            View = renderer.GetComponentInParent<DungeonWallSegmentView>()
                        })
                        .Where(entry => entry.View != null
                            && entry.View.SurfaceProfile == DungeonWallSurfaceProfile.North
                            && entry.Renderer == entry.View.GetComponent<SpriteRenderer>())
                        .ToArray();
                    foreach (var panel in presentation.Where(renderer => renderer != null
                        && renderer.sprite != null
                        && (renderer.sprite.name == "wall-grate-inset-24x16"
                            || renderer.sprite.name == "wall-ossuary-inset-24x16")))
                    {
                        var wall = northWalls
                            .OrderBy(entry => Vector2.Distance(entry.Renderer.bounds.center, panel.bounds.center))
                            .FirstOrDefault();
                        if (wall == null)
                            throw new Exception("Wall inset has no native North wall owner");
                        var faceTop = wall.Renderer.bounds.min.y
                            + DungeonWallSegmentView.NativeNorthWallFaceDepthWorld;
                        if (panel.bounds.min.y < wall.Renderer.bounds.min.y - .001f
                            || panel.bounds.max.y > faceTop + .001f
                            || panel.bounds.min.x < wall.Renderer.bounds.min.x - .001f
                            || panel.bounds.max.x > wall.Renderer.bounds.max.x + .001f)
                            throw new Exception("Wall inset extends beyond the native wall face");
                    }
                    if (agedCells == 0 || map.PresentationFloorVariantTileCount != 16)
                        throw new Exception("Authored floor variant module was not loaded");
                    if (presentation.Any(renderer => renderer.GetComponent<Collider2D>() != null
                        || renderer.GetComponent<Collider>() != null))
                        throw new Exception("Presentation decoration introduced a collider");
                    var signatureBeforeRebuild = map.PresentationLayoutSignature;
                    map.SetPresentationOccupancy(map.PresentationOccupiedCells.ToArray());
                    if (signatureBeforeRebuild != map.PresentationLayoutSignature)
                        throw new Exception("Presentation decoration layout is not deterministic across rebuild");
                    Debug.Log("DUNGEON PRESENTATION PASS floor=" + map.PresentationFloorDecorationCount
                        + " wall=" + map.PresentationWallDecorationCount
                        + " agedCells=" + agedCells
                        + " variantTiles=" + map.PresentationFloorVariantTileCount
                        + " sprites=" + string.Join(",", presentationNames.Select(pair => pair.Key + "=" + pair.Value)));
                    presentationValidated = true;
                    frames = 0;
                    stage = 6;
                    return;
                }
                foreach(var wall in map.LightingWallRenderers)
                    if(wall!=null && wall.sharedMaterial.shader.name=="Godmist/Dungeon/Silhouette Floor")
                        throw new Exception("Wall/occlusion renderer incorrectly uses floor material");
                foreach(var wall in map.LightingWallRenderers)
                {
                    if(wall==null) continue;
                    bool black = wall.color.r + wall.color.g + wall.color.b < .001f;
                    bool lit = wall.sharedMaterial.shader.name == "Godmist/Dungeon/Torch Wall";
                    if(black && lit) throw new Exception("Void mask received wall lighting material");
                    if(!black && !lit) throw new Exception("Visible stone missing local wall lighting");
                }
                Debug.Log("LOCAL WALL MATERIALS / BLACK MASKS PASS");
                int actors=0,stoneProps=0;
                var bodyBlock=new MaterialPropertyBlock();
                foreach(var receiver in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<SpriteRenderer>()))
                {
                    if(receiver.sharedMaterial==null || receiver.sharedMaterial.shader.name!="Godmist/Dungeon/Torch Body") continue;
                    bodyBlock.Clear(); receiver.GetPropertyBlock(bodyBlock);
                    bool actor=receiver.GetComponentInParent<ExplorationCharacterView>()!=null;
                    if(Mathf.Abs(bodyBlock.GetFloat("_IsActor")-(actor?1:0))>.01f)
                        throw new Exception("Wrong surface response for "+receiver.name);
                    if(actor) actors++; else stoneProps++;
                }
                if(actors<4 || stoneProps<1) throw new Exception("Actor/stone surface test missing receivers");
                Debug.Log("ACTOR / STONE RESPONSE PASS actors="+actors+" props="+stoneProps);
                if (light.LastCasterCount < 4 || light.ShadowMask == null) throw new Exception("Missing dungeon casters");
                if (floor.sharedMaterial.shader.name != "Godmist/Dungeon/Silhouette Floor") throw new Exception("Missing floor receiver");
                camera = (Camera)serialized.FindProperty("worldCamera").objectReferenceValue;
                if (camera.targetTexture != null) throw new Exception("Game camera was hijacked");
                Capture(camera, "dungeon-lighting");
                var torchView=light.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled && r.name=="Wall torch")
                    .OrderBy(r=>Vector2.Distance(r.transform.position,camera.transform.position)).First();
                ValidateTorchLighting(camera, torchView, light.ActiveTorchCount);
                ValidateTorchAnimation(camera, torchView);
                var panelView = map.PresentationRenderers
                    .Where(r => r != null && r.enabled && r.sprite != null
                        && (r.sprite.name == "wall-grate-inset-24x16" || r.sprite.name == "wall-ossuary-inset-24x16"))
                    .OrderBy(r => Vector2.Distance(r.transform.position, camera.transform.position))
                    .FirstOrDefault();
                var savedPosition=camera.transform.position; var savedSize=camera.orthographicSize;
                var closeupTarget = panelView != null ? panelView.transform : torchView.transform;
                camera.transform.position=new Vector3(closeupTarget.position.x,closeupTarget.position.y,savedPosition.z);
                camera.orthographicSize=2; Capture(camera,"wall-local-closeup");
                camera.transform.position=savedPosition; camera.orthographicSize=savedSize;
                var litWalls=map.LightingWallRenderers.Where(w=>w!=null && w.sharedMaterial.shader.name=="Godmist/Dungeon/Torch Wall").ToArray();
                if(litWalls.Length==0) throw new Exception("No wall receivers tested");
                var mats=litWalls.Select(w=>w.sharedMaterial).Distinct().ToArray();
                foreach(var m in mats) { m.SetFloat("_Ambient",0); m.SetInt("_TorchCount",0); }
                Capture(camera, "wall-black-diagnostic");
                var litImage=new Texture2D(2,2); var darkImage=new Texture2D(2,2);
                litImage.LoadImage(File.ReadAllBytes("../output/asset-review/realtime-shadows/dungeon/dungeon-lighting.png"));
                darkImage.LoadImage(File.ReadAllBytes("../output/asset-review/realtime-shadows/dungeon/wall-black-diagnostic.png"));
                var litPixels=litImage.GetPixels32(); var darkPixels=darkImage.GetPixels32(); int changed=0;
                for(int i=0;i<litPixels.Length;i++) if(litPixels[i].r>darkPixels[i].r+3) changed++;
                UnityEngine.Object.Destroy(litImage); UnityEngine.Object.Destroy(darkImage);
                if(changed<100) throw new Exception("Wall shader did not affect rendered pixels");
                Debug.Log("WALL PIXEL RESPONSE PASS count="+changed);
                var previous = RenderTexture.active; Graphics.SetRenderTarget(light.ShadowMask,0,CubemapFace.Unknown,0);
                var tex = new Texture2D(1024, 1024, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0,0,1024,1024),0,0); tex.Apply();
                var pixels = tex.GetPixels32().Count(c => c.r > 127); UnityEngine.Object.Destroy(tex); RenderTexture.active = previous;
                if (light.TorchCount == 0 || light.ActiveTorchCount == 0) throw new Exception("Missing stationary torches");
                if (pixels < 20) throw new Exception("Empty dungeon shadow mask");
                Debug.Log("DUNGEON LIGHTING LIVE PASS casters=" + light.LastCasterCount + " maskPixels=" + pixels);
                ValidateFogLightBudget(light, map, camera);
                ValidateTorchSpectrum();
                hidden = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<ExplorationCharacterView>())
                    .First(v => v.IsVisibleForInspection).BodyRenderer;
                priorCasters = light.LastCasterCount; hidden.enabled = false; frames = 0; stage++; return;
            }
            if (stage == 3)
            {
                if (light.LastCasterCount >= priorCasters) throw new Exception("Hidden renderer still casts shadow");
                hidden.enabled = true; light.enabled = false;
                floorBefore = floor.sharedMaterial;
                if (floorBefore.shader.name == "Godmist/Dungeon/Silhouette Floor") throw new Exception("Receiver not restored on disable");
                var restoredMap=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<DungeonFloorMapView>()).Single();
                if(restoredMap.LightingWallRenderers.Any(w=>w!=null && w.sharedMaterial.shader.name=="Godmist/Dungeon/Torch Wall"))
                    throw new Exception("Wall material not restored after disable");
                Capture(camera, "dungeon-lighting-off");
                Debug.Log("DUNGEON LIGHTING HIDDEN/RESTORE PASS");
                light.enabled = true; frames = 0; stage++; return;
            }
            if (light.LastCasterCount < 4 || floor.sharedMaterial == floorBefore) throw new Exception("Lighting did not recover after enable");
            if (stage == 4)
            {
                lightBeforePan = light.FirstTorchPosition;
                var leader = scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ExplorationCharacterView>())
                    .First(v=>v.CharacterId == boot.Session.PlayerParty.ActiveLeaderId);
                leader.transform.position += Vector3.right;
                for (int phase=0; phase<3; phase++)
                {
                    int peaks=0;
                    for (int sample=1; sample<10000; sample++)
                    {
                        float time=sample*.001f;
                        float value=DungeonSilhouetteLighting.TorchIntensity(time,phase);
                        if(value<.8f || value>1.1f) throw new Exception("Unbounded torch flicker");
                        if(value>DungeonSilhouetteLighting.TorchIntensity(time-.001f,phase)
                            && value>DungeonSilhouetteLighting.TorchIntensity(time+.001f,phase)) peaks++;
                    }
                    if(peaks<20 || peaks>30) throw new Exception("Torch pulse frequency outside 2-3 Hz: "+peaks/10f);
                }
                Debug.Log("TORCH PULSE 2-3 HZ PASS");
                var follow = camera.GetComponent<ExplorationCameraFollow>(); if (follow != null) follow.enabled = false;
                camera.transform.position += Vector3.right * 5f;
                camera.orthographicSize *= 1.2f;
                frames = 0; stage++; return;
            }
            if (Vector2.Distance(lightBeforePan, light.FirstTorchPosition) > .001f || camera.targetTexture != null)
                throw new Exception("Light moved with camera or camera target was replaced");
            Capture(camera, "dungeon-lighting-pan-zoom");
            Debug.Log("DUNGEON LIGHTING PAN/ZOOM/LEADER-MOVE PASS");
                Debug.Log("DUNGEON LIGHTING SMOKE PASS"); Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    private static void Capture(Camera camera, string name)
    {
        var target = new RenderTexture(960, 540, 24); target.Create(); var old = camera.targetTexture; camera.targetTexture = target;
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var tex = new Texture2D(960,540,TextureFormat.RGBA32,false); tex.ReadPixels(new Rect(0,0,960,540),0,0); tex.Apply();
        Directory.CreateDirectory("../output/asset-review/realtime-shadows/dungeon");
        File.WriteAllBytes("../output/asset-review/realtime-shadows/dungeon/"+name+".png",tex.EncodeToPNG());
        camera.targetTexture = old; RenderTexture.active = previous; UnityEngine.Object.Destroy(tex); target.Release(); UnityEngine.Object.Destroy(target);
    }
    private static void ValidateTorchLighting(Camera camera, SpriteRenderer torch, int activeTorchCount)
    {
        var savedPosition = camera.transform.position;
        var savedSize = camera.orthographicSize;
        var material = torch.sharedMaterial;
        if (material == null || material.shader == null || material.shader.name != "Godmist/Dungeon/Pixel Torch")
            throw new Exception("Wall torch is missing the shared pixel torch material");
        if (material.GetInt("_TorchCount") != activeTorchCount)
            throw new Exception("Wall torch did not receive the active wall light set");

        camera.transform.position = new Vector3(torch.transform.position.x, torch.transform.position.y, savedPosition.z);
        camera.orthographicSize = 1.5f;
        Capture(camera, "torch-local-lit");
        material.SetFloat("_Ambient", 0);
        material.SetInt("_TorchCount", 0);
        Capture(camera, "torch-local-dark");
        material.SetFloat("_Ambient", .22f);
        material.SetInt("_TorchCount", activeTorchCount);
        var bounds = torch.bounds;
        var min = camera.WorldToViewportPoint(new Vector3(bounds.min.x,bounds.min.y,torch.transform.position.z));
        var max = camera.WorldToViewportPoint(new Vector3(bounds.max.x,bounds.max.y,torch.transform.position.z));
        var source = LoadSpriteSource(torch.sprite);
        camera.transform.position = savedPosition;
        camera.orthographicSize = savedSize;

        var lit = LoadCapture("torch-local-lit");
        var dark = LoadCapture("torch-local-dark");
        int litMetal=0, darkMetal=0, changedMetal=0, flamePixels=0;
        var litPixels=lit.GetPixels32(); var darkPixels=dark.GetPixels32();
        var sourcePixels=source.GetPixels32();
        var spriteRect=torch.sprite.rect;
        var spritePivot=torch.sprite.pivot;
        var pixelsPerUnit=torch.sprite.pixelsPerUnit;
        for (int sy=0; sy<source.height; sy++) for (int sx=0; sx<source.width; sx++)
        {
            if (sourcePixels[sy*source.width+sx].a < 32) continue;
            if(sx<spriteRect.x || sx>=spriteRect.xMax || sy<spriteRect.y || sy>=spriteRect.yMax) continue;
            int x=Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(min.x,max.x,(sx-spriteRect.x+.5f)/spriteRect.width)*lit.width),0,lit.width-1);
            int y=Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(min.y,max.y,(sy-spriteRect.y+.5f)/spriteRect.height)*lit.height),0,lit.height-1);
            int index=y*lit.width+x; var a=litPixels[index]; var b=darkPixels[index];
            int litValue=a.r+a.g+a.b, darkValue=b.r+b.g+b.b;
            if (sy >= Mathf.RoundToInt(source.height*.58f) && b.r > b.b*1.1f && b.r > 30) flamePixels++;
            if (sy < Mathf.RoundToInt(source.height*.58f) && litValue > 45) { litMetal++; if (litValue > darkValue+12) changedMetal++; }
            if (sy < Mathf.RoundToInt(source.height*.58f) && darkValue > 45) darkMetal++;
        }
        UnityEngine.Object.Destroy(source);
        UnityEngine.Object.Destroy(lit); UnityEngine.Object.Destroy(dark);
        if (changedMetal < 10 || litMetal < 20 || changedMetal*5 < litMetal)
            throw new Exception("Torch metal did not respond to wall lighting: lit="+litMetal+" dark="+darkMetal+" changed="+changedMetal);
        if (flamePixels < 3)
            throw new Exception("Torch flame was lost when ambient/local metal lighting was disabled");
        Debug.Log("TORCH WALL LIGHT RESPONSE PASS metalLit="+litMetal+" metalChanged="+changedMetal+" darkMetal="+darkMetal+" darkFlame="+flamePixels);
    }
    private static void ValidateTorchAnimation(Camera camera, SpriteRenderer torch)
    {
        // Render the animation independently of gameplay camera pixel snapping,
        // post effects and overlapping dungeon renderers.
        var testCameraObject=new GameObject("Torch animation validation camera");
        var testCamera=testCameraObject.AddComponent<Camera>();
        testCamera.CopyFrom(camera);
        testCamera.enabled=false;
        testCamera.cullingMask=1<<31;
        testCamera.clearFlags=CameraClearFlags.SolidColor;
        testCamera.backgroundColor=Color.clear;
        var testTorchObject=new GameObject("Torch animation validation sprite");
        testTorchObject.layer=31;
        var testTorch=testTorchObject.AddComponent<SpriteRenderer>();
        testTorch.sprite=torch.sprite;
        testTorch.sharedMaterial=torch.sharedMaterial;
        testTorch.color=torch.color;
        camera=testCamera;
        torch=testTorch;
        var savedPosition=camera.transform.position;
        var savedSize=camera.orthographicSize;
        var savedAspect=camera.aspect;
        var material=torch.sharedMaterial;
        var oldAmbient=material.GetFloat("_Ambient");
        var oldTorchCount=material.GetInt("_TorchCount");
        var block=new MaterialPropertyBlock(); torch.GetPropertyBlock(block);
        var oldPhase=block.GetFloat("_Phase");
        var source=LoadSpriteSource(torch.sprite);
        var flameMask=BuildFlameMask(source);
        var authoredMask=material.GetTexture("_FlameMask") as Texture2D;
        if(authoredMask==null) throw new Exception("Torch flame mask was not assigned to the runtime material");
        var authoredPixels=authoredMask.GetPixels32();
        int maskMismatch=0, authoredCount=0, expectedCount=0;
        for(int index=0;index<flameMask.Length;index++)
        {
            bool authored=authoredPixels[index].r>127;
            if(authored) authoredCount++;
            if(flameMask[index]) expectedCount++;
            if(authored!=flameMask[index]) maskMismatch++;
        }
        if(maskMismatch!=0 || authoredCount!=expectedCount)
            throw new Exception("Torch flame mask does not match the source-aligned authored component: mismatches="+maskMismatch);
        camera.transform.position=new Vector3(torch.bounds.center.x,torch.bounds.center.y,-10);
        // Exactly six output pixels per source pixel, with aligned boundaries.
        camera.orthographicSize=540f/(32f*6f)*.5f;
        // Projection samples must use the same aspect as Capture's target,
        // not the editor/game display that happens to be active in batch mode.
        camera.aspect=960f/540f;
        material.SetFloat("_Ambient",1f);
        material.SetInt("_TorchCount",0);
        var bounds=torch.bounds;
        var min=camera.WorldToViewportPoint(new Vector3(bounds.min.x,bounds.min.y,torch.transform.position.z));
        var max=camera.WorldToViewportPoint(new Vector3(bounds.max.x,bounds.max.y,torch.transform.position.z));
        var phases=new[]{0f,1.9f,3.7f};
        for(int phase=0;phase<phases.Length;phase++)
        {
            block.SetFloat("_Phase",phases[phase]); torch.SetPropertyBlock(block);
            Capture(camera,"torch-animation-phase-"+phase);
        }
        block.SetFloat("_Phase",oldPhase); torch.SetPropertyBlock(block);
        material.SetFloat("_Ambient",oldAmbient);
        material.SetInt("_TorchCount",oldTorchCount);
        var images=phases.Select((_,index)=>LoadCapture("torch-animation-phase-"+index)).ToArray();
        var sourcePixels=source.GetPixels32();
        var pixels=images.Select(image=>image.GetPixels32()).ToArray();
        var spriteRect=torch.sprite.rect;
        var spritePivot=torch.sprite.pivot;
        var pixelsPerUnit=torch.sprite.pixelsPerUnit;
        int flameMinY=source.height, flameMaxY=0;
        for(int index=0;index<flameMask.Length;index++) if(flameMask[index])
        { int y=index/source.width; flameMinY=Mathf.Min(flameMinY,y); flameMaxY=Mathf.Max(flameMaxY,y); }
        int staticSamples=0, staticChanges=0, flameSamples=0, flameChanges=0, baseSamples=0, baseChanges=0, middleChanges=0;
        string staticChangeDetail="";
        for(int sy=0;sy<source.height;sy++) for(int sx=0;sx<source.width;sx++)
        {
            int sourceIndex=sy*source.width+sx;
            if(sourcePixels[sourceIndex].a<32) continue;
            if(sx<spriteRect.x || sx>=spriteRect.xMax || sy<spriteRect.y || sy>=spriteRect.yMax) continue;
            // Tight sprite meshes have trimmed bounds; map the authored texel
            // through the sprite pivot rather than stretching it across bounds.
            var pixelWorld=torch.transform.TransformPoint(new Vector3(
                (sx-spriteRect.x+.5f-spritePivot.x)/pixelsPerUnit,
                (sy-spriteRect.y+.5f-spritePivot.y)/pixelsPerUnit,0));
            var viewport=camera.WorldToViewportPoint(pixelWorld);
            int x=Mathf.Clamp(Mathf.FloorToInt(viewport.x*images[0].width),0,images[0].width-1);
            int y=Mathf.Clamp(Mathf.FloorToInt(viewport.y*images[0].height),0,images[0].height-1);
            var first=pixels[0][y*images[0].width+x];
            var second=pixels[1][y*images[0].width+x];
            var third=pixels[2][y*images[0].width+x];
            if(flameMask[sourceIndex])
            {
                flameSamples++;
                bool changed=RgbDelta(first,second)>12 || RgbDelta(second,third)>12;
                if(changed) flameChanges++;
                int relativeY=sy-flameMinY;
                // The contact row stays spatially attached; its flame color
                // is still allowed to flicker. The next row is animated fire.
                if(relativeY==0)
                {
                    baseSamples++;
                    if(Mathf.Abs(first.a-second.a)>6 || Mathf.Abs(second.a-third.a)>6)
                    {
                        baseChanges++;
                        Debug.Log("TORCH CONTACT sample="+sx+","+sy+" output="+x+","+y+" alpha="+first.a+","+second.a+","+third.a);
                    }
                }
                if(relativeY>=Mathf.RoundToInt((flameMaxY-flameMinY)*.30f)
                    && relativeY<=Mathf.RoundToInt((flameMaxY-flameMinY)*.75f)
                    && changed) middleChanges++;
            }
            else
            {
                staticSamples++;
                if(RgbDelta(first,second)>6 || RgbDelta(second,third)>6)
                { staticChanges++; if(staticChangeDetail.Length==0) staticChangeDetail=" src="+sx+","+sy+" rgb="+first.r+","+first.g+","+first.b+"/"+second.r+","+second.g+","+second.b+"/"+third.r+","+third.g+","+third.b; }
            }
        }
        foreach(var image in images) UnityEngine.Object.Destroy(image);
        UnityEngine.Object.DestroyImmediate(testTorchObject);
        UnityEngine.Object.DestroyImmediate(testCameraObject);
        UnityEngine.Object.Destroy(source);
        if(staticSamples<20 || staticChanges!=0)
            throw new Exception("Torch iron moved with flame UV animation: samples="+staticSamples+" changes="+staticChanges+staticChangeDetail);
        if(flameSamples<20 || flameChanges<8 || middleChanges<2 || baseSamples==0 || baseChanges!=0)
            throw new Exception("Torch flame did not animate as a full anchored component: samples="+flameSamples+" changes="+flameChanges+" middle="+middleChanges+" base="+baseSamples+" baseChanges="+baseChanges);
        Debug.Log("TORCH FLAME-ONLY ANIMATION PASS mask="+authoredCount+" staticSamples="+staticSamples+" staticChanges="+staticChanges+" flameSamples="+flameSamples+" flameChanges="+flameChanges+" middleChanges="+middleChanges+" baseChanges="+baseChanges);
    }
    private static int RgbDelta(Color32 a, Color32 b)
    { return Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b); }
    private static bool[] BuildFlameMask(Texture2D source)
    {
        var pixels=source.GetPixels32();
        var remaining=new HashSet<int>();
        int regionStart=Mathf.RoundToInt(source.height*.58f);
        for(int y=regionStart;y<source.height;y++) for(int x=0;x<source.width;x++)
        {
            var c=pixels[y*source.width+x];
            if(c.a>=128 && c.r>=c.b*1.25f && Mathf.Max(c.r,Mathf.Max(c.g,c.b))>50) remaining.Add(y*source.width+x);
        }
        var largest=new List<int>();
        while(remaining.Count>0)
        {
            int start=remaining.First(); remaining.Remove(start);
            var component=new List<int>(); var queue=new Queue<int>(); queue.Enqueue(start);
            while(queue.Count>0)
            {
                int index=queue.Dequeue(); component.Add(index);
                int x=index%source.width; int y=index/source.width;
                var neighbours=new[]{index-1,index+1,index-source.width,index+source.width};
                foreach(var neighbour in neighbours)
                {
                    if(neighbour<0 || neighbour>=pixels.Length) continue;
                    int nx=neighbour%source.width;
                    if(Mathf.Abs(nx-x)!=1 && neighbour!=index-source.width && neighbour!=index+source.width) continue;
                    if(remaining.Remove(neighbour)) queue.Enqueue(neighbour);
                }
            }
            if(component.Count>largest.Count) largest=component;
        }
        var mask=new bool[pixels.Length]; foreach(var index in largest) mask[index]=true;
        return mask;
    }
    private static Texture2D LoadCapture(string name)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        var path="../output/asset-review/realtime-shadows/dungeon/"+name+".png";
        if (!texture.LoadImage(File.ReadAllBytes(path))) throw new Exception("Could not read torch capture: "+name);
        return texture;
    }
    private static Texture2D LoadSpriteSource(Sprite sprite)
    {
        var assetPath=AssetDatabase.GetAssetPath(sprite.texture);
        var projectRoot=Directory.GetParent(Application.dataPath).FullName;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(projectRoot,assetPath))))
            throw new Exception("Could not read torch source sprite");
        return texture;
    }
    private static void Finish(int code)
    { SessionState.SetBool(Flag,false); EditorApplication.update -= Tick; EditorApplication.Exit(code); }
}
