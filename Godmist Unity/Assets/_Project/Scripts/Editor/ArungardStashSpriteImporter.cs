using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the runtime-loaded Arungard stash bitmap on the project's native
/// pixel-art import settings without requiring a serialized scene reference.
/// </summary>
public sealed class ArungardStashSpriteImporter : AssetPostprocessor
{
    private const string AssetPath = "Assets/Resources/Arungard/CampaignStorageChestV1.png";

    [InitializeOnLoadMethod]
    private static void EnsureImportedProfile()
    {
        EditorApplication.delayCall += ApplyImportedProfile;
    }

    private void OnPreprocessTexture()
    {
        if (assetPath != AssetPath) return;

        var importer = (TextureImporter)this.assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(.5f, 0f);
        importer.SetTextureSettings(settings);
    }

    private static void ApplyImportedProfile()
    {
        var importer = AssetImporter.GetAtPath(AssetPath) as TextureImporter;
        if (importer == null) return;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (importer.textureType == TextureImporterType.Sprite
            && importer.spriteImportMode == SpriteImportMode.Single
            && Mathf.Abs(importer.spritePixelsPerUnit - 32f) < .001f
            && importer.filterMode == FilterMode.Point
            && importer.wrapMode == TextureWrapMode.Clamp
            && !importer.mipmapEnabled
            && importer.textureCompression == TextureImporterCompression.Uncompressed
            && importer.alphaIsTransparency
            && settings.spriteMeshType == SpriteMeshType.FullRect
            && settings.spriteAlignment == (int)SpriteAlignment.Custom
            && Vector2.Distance(settings.spritePivot, new Vector2(.5f, 0f)) < .001f)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(.5f, 0f);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
}
