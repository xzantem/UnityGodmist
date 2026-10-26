using System;
using System.Linq;
using Godmist.Views.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CatacombsContactArtSetup
{
    [MenuItem("Godmist/Content/Configure Catacombs Contact Art")]
    public static void Configure()
    {
        AssetDatabase.Refresh();
        var path = "Assets/_Project/Art/Props/Catacombs/CatacombsStairsDown32-v2.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(.5f,.5f);
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null || sprite.rect.size != new Vector2(32,32)) throw new Exception("Missing ground stairs.");
        var prefabPath = "Assets/_Project/Prefabs/Dungeon/PF_DungeonExitView.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var renderer = root.transform.Find("Marker").GetComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = Color.white;
            renderer.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity");
        var lighting = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DungeonSilhouetteLighting>(true)).Single();
        var data = new SerializedObject(lighting);
        var shader = Shader.Find("Godmist/Dungeon/Contact Shadows");
        var pillar = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Tiles/DungeonColumn32x64.png");
        if (shader == null || pillar == null) throw new Exception("Missing contact shadow references.");
        data.FindProperty("contactShader").objectReferenceValue = shader;
        data.FindProperty("pillarContactSprite").objectReferenceValue = pillar;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CATACOMBS CONTACT ART IMPORT PASS: ground stairs 32x32 Point/32PPU/centered; explicit contact shader and pillar wired.");
    }
}
