using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>
    /// Keeps the 64 px Raven Fantasy source icons suitable for crisp uGUI use.
    /// Source folders intentionally stay outside _Project because they are third-party art.
    /// </summary>
    internal sealed class RavenFantasyIconImportPostprocessor : AssetPostprocessor
    {
        private static readonly string[] SourceFolders =
        {
            "Assets/d/Icons/General/RavenFantasy/Separated Files/64x64",
            "Assets/d/Items/AccessoriesAndArmor",
            "Assets/d/Icons/Skills"
        };

        private const int IconPixelsPerUnit = 64;

        private void OnPreprocessTexture()
        {
            if (!IsRavenFantasyIcon(assetPath))
            {
                return;
            }

            Configure((TextureImporter)assetImporter);
        }

        [MenuItem("Godmist/Art/Configure Raven Fantasy 64px Icons")]
        private static void ConfigureAllIcons()
        {
            var configuredCount = 0;

            foreach (var sourceFolder in SourceFolders)
            {
                var iconGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { sourceFolder });
                foreach (var iconGuid in iconGuids)
                {
                    var iconPath = AssetDatabase.GUIDToAssetPath(iconGuid);
                    AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
                    configuredCount++;
                }
            }

            Debug.Log($"Configured {configuredCount} Raven Fantasy 64 px icons for uGUI.");
        }

        private static bool IsRavenFantasyIcon(string path)
        {
            foreach (var sourceFolder in SourceFolders)
            {
                if (path.StartsWith(sourceFolder + "/"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = IconPixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = IconPixelsPerUnit;
        }
    }
}
