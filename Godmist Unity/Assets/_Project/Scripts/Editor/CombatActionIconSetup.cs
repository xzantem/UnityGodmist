using System;
using System.Linq;
using Godmist.Views.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Godmist.Editor
{
    public static class CombatActionIconSetup
    {
        public const string Art="Assets/_Project/Art/Icons/CombatActions/";
        public const string Prefab="Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CombatActionPanel.prefab";
        public const string Sandbox="Assets/_Project/Scenes/Sandbox/CombatSandbox.unity";

        [MenuItem("Godmist/Content/UI/Apply current Godmist combat action icons")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var previousActive=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(Sandbox);
            var alreadyOpen=scene.IsValid() && scene.isLoaded;
            if(alreadyOpen && scene.isDirty) throw new InvalidOperationException("CombatSandbox has unsaved changes; save or discard them before icon authoring.");
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                if(!alreadyOpen)scene=EditorSceneManager.OpenScene(Sandbox,OpenSceneMode.Additive);
                var prefabViews=root.GetComponentsInChildren<CombatActionButtonsView>(true);
                var sceneViews=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CombatActionButtonsView>(true)).ToArray();
                if(prefabViews.Length==0 || sceneViews.Length==0) throw new InvalidOperationException("Missing authored combat action view.");
                foreach(var view in prefabViews)
                { Icon(view,"endTurnButton","ActionIcon"); Icon(view,"consumablesButton","ActionIcon"); Icon(view,"escapeButton","ActionIcon"); }
                foreach(var view in sceneViews)
                { Icon(view,"endTurnButton","IconGlyph"); Icon(view,"consumablesButton","IconGlyph"); Icon(view,"escapeButton","IconGlyph"); }
                var end=Import(Art+"icon_combat_end_turn-32.png");
                var items=Import(Art+"icon_combat_items-32.png");
                var escape=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"icon_combat_escape.png") ?? throw new InvalidOperationException("Missing dedicated escape icon.");
                var disabled=(Material)new SerializedObject(prefabViews[0]).FindProperty("escapeDisabledMaterial").objectReferenceValue;
                if(disabled==null)throw new InvalidOperationException("Missing authored disabled-icon material.");
                foreach(var view in prefabViews) Bind(view,end,items,escape,disabled,"ActionIcon");
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);
                foreach(var view in sceneViews) Bind(view,end,items,escape,disabled,"IconGlyph");
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save CombatSandbox icon references.");

            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if(!alreadyOpen && scene.IsValid() && scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
                if(previousActive.IsValid() && previousActive.isLoaded)SceneManager.SetActiveScene(previousActive);
            }
        }
        private static Image Icon(CombatActionButtonsView view,string field,string glyphName)
        {
            var button=(Button)new SerializedObject(view).FindProperty(field).objectReferenceValue;
            if(button==null)throw new InvalidOperationException("Missing "+field);
            return button.GetComponentsInChildren<Image>(true).Single(i=>i.name==glyphName);
        }
        private static void Bind(CombatActionButtonsView view,Sprite end,Sprite items,Sprite escape,Material disabled,string glyphName)
        {
            Set(Icon(view,"endTurnButton",glyphName),end);
            Set(Icon(view,"consumablesButton",glyphName),items);
            var escapeImage=Icon(view,"escapeButton",glyphName);
            Set(escapeImage,escape);
            var state=new SerializedObject(view);
            state.FindProperty("escapeIcon").objectReferenceValue=escapeImage;
            state.FindProperty("escapeDisabledMaterial").objectReferenceValue=disabled;
            state.ApplyModifiedPropertiesWithoutUndo();
            if(PrefabUtility.IsPartOfPrefabInstance(view))PrefabUtility.RecordPrefabInstancePropertyModifications(view);
        }
        private static void Set(Image image,Sprite sprite)
        {
            image.sprite=sprite;image.color=Color.white;image.type=Image.Type.Simple;
            image.preserveAspect=true;image.raycastTarget=false;
            image.rectTransform.localRotation=Quaternion.identity;
            if(PrefabUtility.IsPartOfPrefabInstance(image))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                PrefabUtility.RecordPrefabInstancePropertyModifications(image.rectTransform);
            }
        }
        private static Sprite Import(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=32;
            importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            foreach(var platform in new[]{"DefaultTexturePlatform","Standalone"})
            {
                var s=platform=="DefaultTexturePlatform"?importer.GetDefaultPlatformTextureSettings():importer.GetPlatformTextureSettings(platform);
                s.textureCompression=TextureImporterCompression.Uncompressed;s.crunchedCompression=false;
                importer.SetPlatformTextureSettings(s);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Failed import: "+path);
        }
    }
}
