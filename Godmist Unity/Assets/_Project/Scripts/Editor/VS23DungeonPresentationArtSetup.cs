using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VS23DungeonPresentationArtSetup
{
    private const string DecorFolder = "Assets/_Project/Resources/DungeonDecor";
    private const string FloorVariantFolder = "Assets/_Project/Resources/DungeonFloorVariants";

    [MenuItem("Godmist/Content/Configure dungeon presentation enrichment")]
    public static void Configure()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { DecorFolder, FloorVariantFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (!paths.Any())
            throw new System.InvalidOperationException("No dungeon presentation sprites found in " + DecorFolder);

        foreach (var path in paths)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                continue;

            Configure(importer);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Dungeon presentation sprites configured: {paths.Length}");
    }

    private static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
}
