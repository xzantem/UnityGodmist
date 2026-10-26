using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>Import rules for the prepared Gothic UI artwork, scoped to its own folder.</summary>
public sealed class GodmistGothicUISpriteImport : AssetPostprocessor
{
    public const string Root = "Assets/_Project/UI/Sprites/GodmistGothic/";
    public const string AtlasPath = Root + "GodmistGothicUI.spriteatlasv2";

    [Serializable]
    public sealed class SpriteEntry
    {
        public string name;
        public int width;
        public int height;
        public float[] border;
        public float[] pivot;
        public string group;
        public string state;
    }

    [Serializable]
    public sealed class Manifest
    {
        public int version;
        public SpriteEntry[] sprites;
    }

    public static Manifest ReadManifest()
    {
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "sprite-manifest.json"));
        if (manifest == null || manifest.version != 1 || manifest.sprites == null)
            throw new InvalidDataException("Missing or unsupported Godmist Gothic UI sprite manifest.");
        if (manifest.sprites.Any(entry => entry == null || string.IsNullOrEmpty(entry.name) ||
                entry.width < 1 || entry.height < 1 || entry.border == null || entry.border.Length != 4 ||
                entry.pivot == null || entry.pivot.Length != 2))
            throw new InvalidDataException("Invalid Godmist Gothic UI sprite dimensions, pivot or nine-slice definition.");
        return manifest;
    }

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root, StringComparison.Ordinal) ||
            !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
        var name = Path.GetFileNameWithoutExtension(assetPath);
        var entry = ReadManifest().sprites.SingleOrDefault(item => item.name == name);
        if (entry == null) throw new InvalidDataException("Unlisted Gothic UI sprite: " + assetPath);
        ConfigureImporter((TextureImporter)assetImporter, entry);
    }

    private static void ConfigureImporter(TextureImporter importer, SpriteEntry entry)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.spritePivot = new Vector2(entry.pivot[0], entry.pivot[1]);
        importer.spriteBorder = new Vector4(entry.border[0], entry.border[1], entry.border[2], entry.border[3]);
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        var platform = importer.GetDefaultPlatformTextureSettings();
        platform.format = TextureImporterFormat.RGBA32;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        platform.maxTextureSize = 2048;
        importer.SetPlatformTextureSettings(platform);
    }

    [MenuItem("Godmist/UI/Prepare Gothic UI sprite imports")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before preparing UI artwork.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var manifest = ReadManifest();
        foreach (var entry in manifest.sprites)
        {
            var path = Root + entry.name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Missing Gothic UI sprite.", path);
            ConfigureImporter(importer, entry);
            importer.SaveAndReimport();
        }

        // V2 matches the project's existing Sprite Packer mode. Explicit packables
        // prevent unrelated images added to this folder from entering the atlas.
        var atlas = new SpriteAtlasAsset();
        atlas.Add(manifest.sprites.Select(entry => (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Sprite>(Root + entry.name + ".png")).ToArray());
        SpriteAtlasAsset.Save(atlas, AtlasPath);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        // Unity 6000.4 stores V2 settings on the importer, not SpriteAtlasAsset.
        var atlasImporter = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
        atlasImporter.includeInBuild = true;
        atlasImporter.packingSettings = new SpriteAtlasPackingSettings
        {
            enableRotation = false,
            enableTightPacking = false,
            padding = 4
        };
        atlasImporter.textureSettings = new SpriteAtlasTextureSettings
        {
            filterMode = FilterMode.Point,
            generateMipMaps = false,
            readable = false,
            sRGB = true
        };
        atlasImporter.SetPlatformSettings(new TextureImporterPlatformSettings
        {
            name = "DefaultTexturePlatform",
            maxTextureSize = 2048,
            format = TextureImporterFormat.RGBA32,
            textureCompression = TextureImporterCompression.Uncompressed
        });
        atlasImporter.SaveAndReimport();
        AssetDatabase.SaveAssets();
        Verify();
    }

    [MenuItem("Godmist/UI/Verify Gothic UI sprite imports")]
    public static void Verify()
    {
        var manifest = ReadManifest();
        foreach (var entry in manifest.sprites)
        {
            var path = Root + entry.name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || importer == null) throw new InvalidDataException("Unimported UI sprite: " + path);
            var border = new Vector4(entry.border[0], entry.border[1], entry.border[2], entry.border[3]);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (sprite.rect.width != entry.width || sprite.rect.height != entry.height || sprite.border != border ||
                sprite.pivot != new Vector2(entry.width * .5f, entry.height * .5f) ||
                importer.spritePixelsPerUnit != 100 || importer.filterMode != FilterMode.Point ||
                importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                settings.spriteMeshType != SpriteMeshType.FullRect)
                throw new InvalidDataException("Incorrect Gothic UI sprite import: " + path);
        }
        var atlasImporter = AssetImporter.GetAtPath(AtlasPath) as SpriteAtlasImporter;
        var packedAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        if (atlasImporter == null || packedAtlas == null) throw new InvalidDataException("Gothic UI atlas was not imported.");
        var packing = atlasImporter.packingSettings;
        var texture = atlasImporter.textureSettings;
        if (packing.enableRotation || packing.enableTightPacking || packing.padding != 4 ||
            texture.filterMode != FilterMode.Point || texture.generateMipMaps ||
            packedAtlas.spriteCount != manifest.sprites.Length)
            throw new InvalidDataException($"Incorrect Gothic UI atlas: rotation={packing.enableRotation}, tight={packing.enableTightPacking}, padding={packing.padding}, filter={texture.filterMode}, mipmaps={texture.generateMipMaps}, sprites={packedAtlas.spriteCount}/{manifest.sprites.Length}, mode={EditorSettings.spritePackerMode}.");
        Debug.Log("GODMIST_GOTHIC_UI_IMPORT_PASS: " + manifest.sprites.Length + " sprites, Point, Full Rect, 9-slice, atlas V2.");
    }
}
