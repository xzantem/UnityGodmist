using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Explicit presentation-only import; preserves movement and LOS footprints.</summary>
    [InitializeOnLoad]
    public static class ForestMatureTreesSetup
    {
        public const string ArtRoot = "Assets/_Project/Art/ForestPrototype/";
        public static readonly string[] Tags = { "oak", "elm", "spruce", "hollow", "ancient" };
        public static string SpritePath(string tag) => ArtRoot + "forest-tree-mature-" + tag + "-v2-32ppu.png";
        public const string CompactTreePath = ArtRoot + "forest-tree-compact-oak-v1-32ppu.png";
        static readonly string Request = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../LocalHandoffs/forest-border-trees-20261018/import.request"));
        static ForestMatureTreesSetup() => EditorApplication.update += Tick;
        static void Tick()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(Request);
            try { Configure(); File.WriteAllText(Path.ChangeExtension(Request, ".state.txt"), "PASS " + DateTime.UtcNow.ToString("O")); }
            catch (Exception e) { File.WriteAllText(Path.ChangeExtension(Request, ".state.txt"), "ERROR " + e); Debug.LogException(e); }
        }
        [MenuItem("Godmist/Content/Forest/Import mature trees v2")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before importing trees.");
            PrepareCompactTree();
            var sprites = new Sprite[Tags.Length + 1];
            for (var i = 0; i < sprites.Length; i++)
            {
                var path = i == Tags.Length ? CompactTreePath : SpritePath(Tags[i]);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing tree: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 256;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(.5f, 8f / (i == Tags.Length ? 128f : Tags[i] == "ancient" ? 256f : 192f));
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path)
                    ?? throw new InvalidOperationException("Sprite import failed: " + path);
            }
            Bind("Assets/_Project/ScriptableObjects/Dungeons/Forest/DVP_Forest_Prototype.asset", "BoundaryTree", "TreeVariants", sprites, true);
            Bind("Assets/_Project/ScriptableObjects/Dungeons/Forest/DO_Forest_Tree_Prototype.asset", "PresentationSprite", "PresentationVariants", sprites, false);
            ForestPrototypeSetup.RefreshTreeFootprints();
            AssetDatabase.SaveAssets();
        }
        static void PrepareCompactTree()
        {
            if (File.Exists(CompactTreePath)) return;
            var rawPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../LocalHandoffs/forest-footprints-gapfill-combat-20261018/compact-tree-raw.png"));
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var native = new Texture2D(80, 128, TextureFormat.RGBA32, false);
            try
            {
                if (!raw.LoadImage(File.ReadAllBytes(rawPath))) throw new InvalidOperationException("Invalid generated tree PNG.");
                var pixels = raw.GetPixels32();
                int minX = raw.width, minY = raw.height, maxX = -1, maxY = -1;
                for (var y = 0; y < raw.height; y++) for (var x = 0; x < raw.width; x++)
                    if (pixels[y * raw.width + x].a > 16)
                    { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                if (maxX < minX) throw new InvalidOperationException("Generated tree is empty.");
                var width = maxX - minX + 1; var height = maxY - minY + 1;
                var ratio = Mathf.Min(72f / width, 104f / height);
                var w = Mathf.RoundToInt(width * ratio); var h = Mathf.RoundToInt(height * ratio);
                var output = new Color32[80 * 128];
                // Deterministic nearest-neighbour native pixel conversion.
                // Preserve generated colours/alpha; no creative repainting.
                for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                    output[(y + 8) * 80 + (80 - w) / 2 + x] = pixels[(minY + Mathf.Min(height - 1, Mathf.FloorToInt((y + .5f) * height / h))) * raw.width
                        + minX + Mathf.Min(width - 1, Mathf.FloorToInt((x + .5f) * width / w))];
                native.SetPixels32(output); native.Apply();
                File.WriteAllBytes(CompactTreePath, native.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(raw); UnityEngine.Object.DestroyImmediate(native); }
        }
        static void Bind(string path, string mainField, string variantsField, Sprite[] sprites, bool boundary)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path) ?? throw new InvalidOperationException("Missing definition: " + path);
            var data = new SerializedObject(asset);
            SerializedProperty Field(string name) => data.FindProperty("<" + name + ">k__BackingField") ?? data.FindProperty(name);
            Field(mainField).objectReferenceValue = sprites[0];
            var variants = Field(variantsField); variants.arraySize = boundary ? 6 : 3;
            for (var i = 0; i < 3; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i + 1];
            if (boundary)
            {
                variants.GetArrayElementAtIndex(3).objectReferenceValue = sprites[0];
                variants.GetArrayElementAtIndex(4).objectReferenceValue = sprites[3];
                variants.GetArrayElementAtIndex(5).objectReferenceValue = sprites[4];
                Field("TreeVisualOffsetInCells").vector2Value = Vector2.zero;
                var small = Field("SmallTreeVariants"); small.arraySize = 1;
                small.GetArrayElementAtIndex(0).objectReferenceValue = sprites[5];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
