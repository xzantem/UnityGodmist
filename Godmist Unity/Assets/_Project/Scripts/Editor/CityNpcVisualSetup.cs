using System;
using System.IO;
using System.Linq;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using Godmist.Views.NPC;
using Godmist.Views.UI.Workspace;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CityNpcVisualSetup
{
    private const string Flag="CityNpcVisualSmoke";
    private static int stage,frames;
    private static double started;
    private static string SpriteFile(string id) => id switch
    {
        "npc.arungard.alchemist_01"=>"alchemist-64-v2",
        "npc.arungard.blacksmith_01"=>"blacksmith-64-v1",
        "npc.arungard.enchanter_01"=>"enchanter-64-v1",
        "npc.arungard.priest_01"=>"priest-64-v1",
        "npc.arungard.questgiver_01"=>"catacomb-guardian-64-v1",
        _=>throw new Exception("Unknown town NPC: "+id)
    };
    public static void Configure()
    {
        var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/City/Arungard.unity");
        var bootstrap=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ExplorationSandboxBootstrap>(true)).Single();
        if(bootstrap.NpcBindings.Count!=5) throw new Exception("Expected five town NPCs");
        foreach(var binding in bootstrap.NpcBindings)
        {
            string path="Assets/_Project/Art/Characters/NPC/VS22/"+SpriteFile(binding.Definition.Id)+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom; settings.spritePivot=new Vector2(.5f,3f/64);
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var definition=new SerializedObject(binding.Definition);
            definition.FindProperty("<Portrait>k__BackingField").objectReferenceValue=sprite;
            definition.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(binding.Definition);
            var view=new SerializedObject(binding.NpcView);
            var body=(SpriteRenderer)view.FindProperty("bodyRenderer").objectReferenceValue;
            body.sprite=sprite; body.color=Color.white; EditorUtility.SetDirty(body);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("CITY NPC VISUAL CONFIGURATION PASS");
        if(Application.isBatchMode) EditorApplication.Exit(0);
    }
    public static void Smoke()
    {
        PlayerSettings.productName="GodmistCityNpcIsolatedSmoke";
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
        SessionState.SetBool(Flag,true); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] private static void Resume()
    { if(SessionState.GetBool(Flag,false)) { started=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; } }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying) return;
        try
        {
            if(EditorApplication.timeSinceStartup-started>120) throw new Exception("City NPC smoke timeout");
            var game=GameBootstrap.Instance; if(game==null || game.IsSceneLoading) return;
            if(stage==0) { if(!game.TryStartNewGame(out var message)) throw new Exception(message); stage++; return; }
            if(SceneManager.GetActiveScene().name!="Arungard" || ++frames<20) return;
            var scene=SceneManager.GetActiveScene();
            var bootstrap=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ExplorationSandboxBootstrap>(true)).Single();
            if(bootstrap.NpcBindings.Count!=5) throw new Exception("Runtime NPC count mismatch");
            var camera=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Camera>()).First(c=>c.CompareTag("MainCamera"));
            var follow=camera.GetComponent<ExplorationCameraFollow>(); if(follow!=null) follow.enabled=false;
            var cameraPosition=camera.transform.position; float size=camera.orthographicSize;
            foreach(var binding in bootstrap.NpcBindings)
            {
                var view=binding.NpcView;
                var data=new SerializedObject(view);
                var body=(SpriteRenderer)data.FindProperty("bodyRenderer").objectReferenceValue;
                if(view.NpcId!=binding.Definition.Id || body.sprite!=binding.Definition.Portrait
                    || body.sprite.name!=SpriteFile(view.NpcId)) throw new Exception("Wrong runtime NPC sprite: "+view.NpcId);
                if(body.color!=Color.white) throw new Exception("Authored tint remains: "+view.NpcId);
                if(!view.IsPointerHit(body.bounds.center)) throw new Exception("NPC pointer hit lost: "+view.NpcId);
                view.SetInspectionHighlight(true); if(body.color==Color.white) throw new Exception("Inspection highlight missing");
                view.SetInspectionHighlight(false); if(body.color!=Color.white) throw new Exception("Highlight restored tinted color");
                camera.transform.position=new Vector3(body.bounds.center.x,body.bounds.center.y,cameraPosition.z);
                camera.orthographicSize=2;
                Capture(camera,SpriteFile(view.NpcId));
                Debug.Log("CITY NPC SPRITE / WHITE TINT / HIGHLIGHT / POINTER PASS "+view.NpcId);
            }
            camera.transform.position=cameraPosition; camera.orthographicSize=size;
            Capture(camera,"city-npcs-runtime");
            var dialogue=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<NpcDialogueController>(true)).Single();
            if(!game.Session.TryRequestNpcInteraction("npc.arungard.blacksmith_01")) throw new Exception("Cannot request blacksmith services");
            var dialogueData=new SerializedObject(dialogue);
            var choices=(Transform)dialogueData.FindProperty("choicesContent").objectReferenceValue;
            var button=choices.GetChild(0).GetComponent<Button>();
            if(!button.gameObject.activeInHierarchy || button.GetComponentInChildren<TMPro.TMP_Text>().text!="Chcę skorzystać z twoich usług")
                throw new Exception("First dialogue service choice is unavailable");
            button.onClick.Invoke();
            var services=UIWorkspaceWindowManager.Instance.WorkspaceRoot.GetComponentsInChildren<NpcServicesWindowView>(true).Single();
            ServiceIconSetup.Verify(services);
            var tabs=services.GetComponentsInChildren<NpcServiceTabView>();
            if(tabs.Length<5) throw new Exception("Missing blacksmith service tabs");
            foreach(var tab in tabs)
            {
                var data=new SerializedObject(tab); var image=(Image)data.FindProperty("icon").objectReferenceValue;
                if(image.sprite==null || !AssetDatabase.GetAssetPath(image.sprite).Contains("Icons/Services/VS23"))
                    throw new Exception("Legacy icon remained in active service tab");
            }
            Debug.Log("LIVE BLACKSMITH SERVICES / NEW ICONS PASS tabs="+tabs.Length);
            Debug.Log("CITY NPC VISUAL SMOKE PASS"); Finish(0);
        }
        catch(Exception e) { Debug.LogException(e); Finish(1); }
    }
    private static void Capture(Camera camera,string name)
    {
        var target=new RenderTexture(640,360,24); target.Create(); var old=camera.targetTexture; var previous=RenderTexture.active;
        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        var tex=new Texture2D(640,360,TextureFormat.RGBA32,false); tex.ReadPixels(new Rect(0,0,640,360),0,0); tex.Apply();
        Directory.CreateDirectory("../output/asset-review/city-npc-integration");
        File.WriteAllBytes("../output/asset-review/city-npc-integration/"+name+".png",tex.EncodeToPNG());
        camera.targetTexture=old; RenderTexture.active=previous;
        UnityEngine.Object.Destroy(tex); target.Release(); UnityEngine.Object.Destroy(target);
    }
    private static void Finish(int code)
    { SessionState.SetBool(Flag,false); EditorApplication.update-=Tick; EditorApplication.Exit(code); }
}
