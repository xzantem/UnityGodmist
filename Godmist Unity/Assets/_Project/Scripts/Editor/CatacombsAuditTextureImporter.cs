using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the runtime-loaded Catacombs audit props on the project's native
/// pixel-art import profile without hand-editing generated .meta files.
/// </summary>
public sealed class CatacombsAuditTextureImporter : AssetPostprocessor
{
    private const string AssetRoot = "Assets/_Project/Resources/CatacombsAudit/";

    [InitializeOnLoadMethod]
    private static void EnsureImportedProfile()
    {
        EditorApplication.delayCall += ApplyImportedProfile;
    }

    private void OnPreprocessTexture()
    {
        if (!IsAuditAsset(assetPath)) return;
        Configure((TextureImporter)assetImporter, assetPath);
    }

    private static void ApplyImportedProfile()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { AssetRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsAuditAsset(path)) continue;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || HasProfile(importer, path)) continue;

            Configure(importer, path);
            importer.SaveAndReimport();
        }
    }

    private static bool IsAuditAsset(string path)
        => path.Replace('\\', '/').StartsWith(AssetRoot, System.StringComparison.Ordinal)
           && path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);

    private static bool HasProfile(TextureImporter importer, string path)
    {
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        var expectedPivot = path.EndsWith("catacombs-pillar.png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("catacombs-lever.png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("catacombs-lever-v3.png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("catacombs-lever-v4.png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("catacombs-ground-dagger-v2.png", System.StringComparison.OrdinalIgnoreCase)
            ? new Vector2(.5f, 0f)
            : new Vector2(.5f, .5f);

        return importer.textureType == TextureImporterType.Sprite
            && importer.spriteImportMode == SpriteImportMode.Single
            && Mathf.Abs(importer.spritePixelsPerUnit - 32f) < .001f
            && importer.filterMode == FilterMode.Point
            && importer.wrapMode == TextureWrapMode.Clamp
            && !importer.mipmapEnabled
            && importer.textureCompression == TextureImporterCompression.Uncompressed
            && importer.alphaIsTransparency
            && settings.spriteMeshType == SpriteMeshType.FullRect
            && settings.spriteAlignment == (int)SpriteAlignment.Custom
            && Vector2.Distance(settings.spritePivot, expectedPivot) < .001f
            && standalone.overridden
            && standalone.format == TextureImporterFormat.RGBA32
            && standalone.textureCompression == TextureImporterCompression.Uncompressed;
    }

    private static void Configure(TextureImporter importer, string path)
    {
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
        settings.spritePivot = path.EndsWith("catacombs-pillar.png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("catacombs-lever.png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("catacombs-lever-v3.png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("catacombs-lever-v4.png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("catacombs-ground-dagger-v2.png", System.StringComparison.OrdinalIgnoreCase)
            ? new Vector2(.5f, 0f)
            : new Vector2(.5f, .5f);
        importer.SetTextureSettings(settings);

        var standalone = importer.GetPlatformTextureSettings("Standalone");
        standalone.overridden = true;
        standalone.format = TextureImporterFormat.RGBA32;
        standalone.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(standalone);
    }
}
