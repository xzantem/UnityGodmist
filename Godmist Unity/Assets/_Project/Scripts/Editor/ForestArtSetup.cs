using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Godmist.Editor
{
    /// <summary>Presentation-only wiring of the approved pass 1b native exports.</summary>
    public static class ForestArtSetup
    {
        const string ConfigPath="Assets/_Project/Scripts/Editor/ForestArtBindings.json";
        [Serializable] public class Config {public Import[] imports;public Binding[] bindings;}
        [Serializable] public class Import {public string path;public float pivotX,pivotY;public int width;public string sha256;}
        [Serializable] public class Binding {public string asset,field;public string[] sprites;}
        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Art")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var config=JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
            AssetDatabase.Refresh();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach(var item in config.imports)
                {
                    var importer=AssetImporter.GetAtPath(item.path) as TextureImporter;
                    if(importer==null)throw new InvalidOperationException("Missing art: "+item.path);
                    importer.textureType=item.path.Contains("/CharacterAnimations/") || item.path.Contains("/CombatPoses/") ? TextureImporterType.Default : TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                    importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;
                    importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
                    importer.wrapMode=TextureWrapMode.Clamp;importer.alphaIsTransparency=true;
                    importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=256;
                    var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                    settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(item.pivotX,item.pivotY);
                    settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
                }
            }
            finally{AssetDatabase.StopAssetEditing();}
            foreach(var binding in config.bindings)
            {
                var asset=AssetDatabase.LoadMainAssetAtPath(binding.asset);
                if(asset==null)throw new InvalidOperationException("Missing definition: "+binding.asset);
                var data=new SerializedObject(asset);var field=data.FindProperty("<"+binding.field+">k__BackingField") ?? data.FindProperty(binding.field);
                if(field==null)throw new InvalidOperationException(binding.asset+": "+binding.field);
                if(field.isArray)
                {
                    field.arraySize=binding.sprites.Length;
                    for(var i=0;i<binding.sprites.Length;i++)field.GetArrayElementAtIndex(i).objectReferenceValue=Sprite(binding.sprites[i]);
                }
                else field.objectReferenceValue=Sprite(binding.sprites[0]);
                if(binding.field=="IdleAnimationFrames")data.FindProperty("<IdleAnimationFramesPerSecond>k__BackingField").floatValue=2.5f;
                if(binding.field=="RunAnimationFrames")data.FindProperty("<RunAnimationFramesPerSecond>k__BackingField").floatValue=5f;
                if(binding.field=="TransformationFrames")data.FindProperty("<TransformationFramesPerSecond>k__BackingField").floatValue=5f;
                data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);
            }
            Glossary("status.forest.demonic_plague","Demoniczna Zaraza","plague-status-32-v1.png");
            Glossary("forest.rotten_aura","Zgniła Aura","rotten-aura-32-v1.png");
            Glossary("forest.demonic_plague","Demoniczna Zaraza","demonic-plague-32-v1.png");
            AssetDatabase.SaveAssets();
        }
        static Sprite Sprite(string path)=>AssetDatabase.LoadAssetAtPath<Sprite>(path)??throw new InvalidOperationException("Missing sprite: "+path);
        static void Glossary(string id,string title,string file)
        {
            var asset=AssetDatabase.LoadMainAssetAtPath("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var data=new SerializedObject(asset);var entries=data.FindProperty("entries");SerializedProperty entry=null;
            for(var i=0;i<entries.arraySize;i++)if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue==id)entry=entries.GetArrayElementAtIndex(i);
            if(entry==null){entries.InsertArrayElementAtIndex(entries.arraySize);entry=entries.GetArrayElementAtIndex(entries.arraySize-1);entry.FindPropertyRelative("Description").stringValue="";entry.FindPropertyRelative("Aliases").ClearArray();}
            entry.FindPropertyRelative("Id").stringValue=id;entry.FindPropertyRelative("Title").stringValue=title;
            entry.FindPropertyRelative("Icon").objectReferenceValue=Sprite("Assets/Resources/Godmist/ForestArt/Icons/"+file);
            data.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);
        }
    }
}
