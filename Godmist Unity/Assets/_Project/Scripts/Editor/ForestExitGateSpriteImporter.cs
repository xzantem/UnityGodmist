using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    // This runtime-loaded sprite needs no prefab or definition reference.
    public sealed class ForestExitGateSpriteImporter : AssetPostprocessor
    {
        const string RuntimePath = "Assets/Resources/Godmist/ForestArt/Exploration/exit-gate-v1.png";
        const string SourcePath = "Assets/_Project/Art/ForestExploration/forest-exit-gate-v1-160.png";
        const string RuntimePath2 = "Assets/Resources/Godmist/ForestArt/Exploration/exit-gate-v2.png";
        const string SourcePath2 = "Assets/_Project/Art/ForestExploration/forest-exit-gate-v2-128.png";
        static Vector2 Pivot(string path) => new(.5f, path == RuntimePath2 || path == SourcePath2 ? .285f : .245f);

        void OnPreprocessTexture()
        {
            if (assetPath == RuntimePath || assetPath == SourcePath || assetPath == RuntimePath2 || assetPath == SourcePath2) Configure((TextureImporter)assetImporter);
        }

        [InitializeOnLoadMethod]
        static void EnsureProfile() => EditorApplication.delayCall += ApplyProfiles;

        public static void ApplyProfiles()
        {
            foreach (var path in new[] { RuntimePath, SourcePath, RuntimePath2, SourcePath2 })
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && importer.spritePixelsPerUnit == 32 && importer.filterMode == FilterMode.Point
                    && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed
                    && importer.npotScale == TextureImporterNPOTScale.None && importer.maxTextureSize == 256
                    && importer.alphaIsTransparency && importer.wrapMode == TextureWrapMode.Clamp
                    && settings.spriteMeshType == SpriteMeshType.FullRect && settings.spriteAlignment == (int)SpriteAlignment.Custom
                    && settings.spritePivot == Pivot(path)) continue;
                Configure(importer); importer.SaveAndReimport();
            }
        }

        static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 256;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = Pivot(importer.assetPath); importer.SetTextureSettings(settings);
        }
    }
}
