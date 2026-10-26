using System;
using System.Linq;
using Godmist.Views.UI.Workspace;
using UnityEditor;
using UnityEngine;

public static class ServiceIconSetup
{
    public static readonly string[] Names = { "trade", "equipment-crafting", "material-crafting", "equipment-upgrade",
        "equipment-reforge", "equipment-infusion", "jewelry-crafting", "galduryte-crafting", "galduryte-identification",
        "galduryte-socketing", "potion-crafting", "potion-refill", "bomb-crafting", "bomb-refill", "galduryte-extraction", "companion-recovery" };
    public static void Configure()
    {
        var sprites = new Sprite[Names.Length];
        for (int i=0; i<Names.Length; i++)
        {
            string path="Assets/_Project/Art/Icons/Services/VS23/"+Names[i]+"-32.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=32; importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true;
            importer.SaveAndReimport(); sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprites[i]==null || sprites[i].rect.size!=new Vector2(32,32)) throw new Exception("Invalid service icon "+Names[i]);
        }
        const string prefab="Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab";
        var root=PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            var view=root.GetComponentInChildren<NpcServicesWindowView>(true);
            var data=new SerializedObject(view); var definitions=data.FindProperty("tabDefinitions");
            definitions.arraySize=Names.Length;
            for(int i=0;i<Names.Length;i++)
            {
                var entry=definitions.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Kind").enumValueIndex=i;
                entry.FindPropertyRelative("Icon").objectReferenceValue=sprites[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root,prefab);
            Verify(view); AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("SERVICE ICON IMPORT / ALL 16 MAPPINGS PASS");
        if(Application.isBatchMode) EditorApplication.Exit(0);
    }
    public static void Verify(NpcServicesWindowView view)
    {
        var data=new SerializedObject(view); var definitions=data.FindProperty("tabDefinitions");
        if(definitions.arraySize!=16) throw new Exception("Service icon mapping count mismatch");
        for(int i=0;i<16;i++)
        {
            var entry=definitions.GetArrayElementAtIndex(i);
            var icon=(Sprite)entry.FindPropertyRelative("Icon").objectReferenceValue;
            if(entry.FindPropertyRelative("Kind").enumValueIndex!=i || icon==null || icon.name!=Names[i]+"-32")
                throw new Exception("Incorrect service icon mapping "+i);
        }
    }
}
