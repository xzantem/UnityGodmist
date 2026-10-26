using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>
    /// Keeps the runtime Resources packs in pixel-art import settings. The
    /// resolver creates the grounded Sprite at runtime, so no serialized
    /// Sprite references or hand-authored meta files are required.
    /// </summary>
    public sealed class CharacterAnimationTextureImporter : AssetPostprocessor
    {
        private static readonly string[] ResourceRoots =
        {
            "Assets/Resources/Godmist/CharacterAnimations",
            "Assets/Resources/Godmist/CombatPoses"
        };

        [InitializeOnLoadMethod]
        private static void ScheduleExistingProfileRepair()
        {
            EditorApplication.delayCall += ApplyImportedProfiles;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        private static void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ApplyImportedProfiles;
        }

        private static bool IsPackTexture(string path)
        {
            path = path.Replace('\\', '/');
            if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return false;
            foreach (var root in ResourceRoots)
                if (path.StartsWith(root + "/", System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void OnPreprocessTexture()
        {
            if (!IsPackTexture(assetPath)) return;
            Configure((TextureImporter)assetImporter);
        }

        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 0;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            var defaults = importer.GetDefaultPlatformTextureSettings();
            defaults.format = TextureImporterFormat.RGBA32;
            defaults.textureCompression = TextureImporterCompression.Uncompressed;
            defaults.crunchedCompression = false;
            importer.SetPlatformTextureSettings(defaults);
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.format = TextureImporterFormat.RGBA32;
            standalone.textureCompression = TextureImporterCompression.Uncompressed;
            standalone.crunchedCompression = false;
            importer.SetPlatformTextureSettings(standalone);
        }

        private static bool HasProfile(TextureImporter importer)
        {
            var defaults = importer.GetDefaultPlatformTextureSettings();
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            return importer.textureType == TextureImporterType.Default
                && importer.filterMode == FilterMode.Point && importer.anisoLevel == 0
                && importer.wrapMode == TextureWrapMode.Clamp && !importer.mipmapEnabled
                && importer.alphaIsTransparency && importer.npotScale == TextureImporterNPOTScale.None
                && importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.crunchedCompression
                && defaults.format == TextureImporterFormat.RGBA32
                && defaults.textureCompression == TextureImporterCompression.Uncompressed && !defaults.crunchedCompression
                && standalone.overridden && standalone.format == TextureImporterFormat.RGBA32
                && standalone.textureCompression == TextureImporterCompression.Uncompressed && !standalone.crunchedCompression;
        }

        [MenuItem("Godmist/Content/Characters/Repair Pixel Art Import Settings")]
        public static void ApplyImportedProfiles()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Changing the postprocessor does not fix assets whose existing meta
            // was created before the script compiled. Repair only those profiles.
            var changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var root in ResourceRoots)
                {
                    if (!AssetDatabase.IsValidFolder(root)) continue;
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!IsPackTexture(path)) continue;
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null || HasProfile(importer)) continue;
                        Configure(importer);
                        importer.SaveAndReimport();
                        changed++;
                    }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            if (changed > 0) Debug.Log($"CHARACTER_PIXEL_IMPORT_REPAIRED: {changed} textures; Point, RGBA32, no compression/mipmaps.");
        }
    }
}
