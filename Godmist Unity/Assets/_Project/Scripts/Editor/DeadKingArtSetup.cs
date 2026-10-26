using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Native, presentation-only import/wiring for the checked Dead King v2 pack.</summary>
    public static class DeadKingArtSetup
    {
        private const string ConfigPath = "Assets/_Project/Scripts/Editor/DeadKingArtBindings.json";
        [Serializable] public sealed class Config { public Import[] imports; public Binding[] bindings; public FloatBinding[] floats; public GlossaryBinding[] glossary; public VectorBinding[] vectors; }
        [Serializable] public sealed class Import { public string path, sha256; public float pivotX, pivotY; public bool texture; }
        [Serializable] public sealed class Binding { public string asset, field; public string[] sprites; }
        [Serializable] public sealed class FloatBinding { public string asset, field; public float value; }
        [Serializable] public sealed class VectorBinding { public string asset, field; public float x, y; }
        [Serializable] public sealed class GlossaryBinding { public string id, title, sprite; }

        [MenuItem("Godmist/Content/Dead King 2.0/Configure Approved Art")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
            Configure(config);
        }

        internal static void Configure(Config config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var item in config.imports)
                {
                    using var hash = SHA256.Create();
                    var actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(item.path))).Replace("-", "").ToLowerInvariant();
                    if (actual != item.sha256) throw new InvalidOperationException("Changed source art: " + item.path);
                    var importer = AssetImporter.GetAtPath(item.path) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("Missing texture: " + item.path);
                    importer.textureType = item.texture ? TextureImporterType.Default : TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 32;
                    importer.filterMode = FilterMode.Point;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.anisoLevel = 0;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.crunchedCompression = false;
                    importer.maxTextureSize = 256;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(item.pivotX, item.pivotY);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
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
                    importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            foreach (var binding in config.bindings)
            {
                var data = Definition(binding.asset);
                var field = Field(data, binding.field);
                if (field.isArray)
                {
                    field.arraySize = binding.sprites.Length;
                    for (var i = 0; i < binding.sprites.Length; i++) field.GetArrayElementAtIndex(i).objectReferenceValue = Sprite(binding.sprites[i]);
                }
                else field.objectReferenceValue = Sprite(binding.sprites[0]);
                Save(data);
            }
            foreach (var binding in config.floats)
            {
                var data = Definition(binding.asset);
                Field(data, binding.field).floatValue = binding.value;
                Save(data);
            }
            foreach (var binding in config.vectors ?? Array.Empty<VectorBinding>())
            {
                var data = Definition(binding.asset);
                Field(data, binding.field).vector2Value = new Vector2(binding.x, binding.y);
                Save(data);
            }
            var glossary = Definition("Assets/_Project/ScriptableObjects/UI/CombatTooltipGlossary.asset");
            var entries = glossary.FindProperty("entries");
            foreach (var binding in config.glossary)
            {
                SerializedProperty entry = null;
                for (var i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue == binding.id) entry = entries.GetArrayElementAtIndex(i);
                if (entry == null)
                {
                    entries.InsertArrayElementAtIndex(entries.arraySize);
                    entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    entry.FindPropertyRelative("Description").stringValue = "";
                    entry.FindPropertyRelative("Aliases").ClearArray();
                }
                entry.FindPropertyRelative("Id").stringValue = binding.id;
                entry.FindPropertyRelative("Title").stringValue = binding.title;
                entry.FindPropertyRelative("Icon").objectReferenceValue = Sprite(binding.sprite);
            }
            Save(glossary);
            AssetDatabase.SaveAssets();
            Debug.Log($"DEAD_KING_ART_CONFIGURED: {config.imports.Length} textures, {config.bindings.Length} bindings.");
        }

        private static SerializedObject Definition(string path) => new(AssetDatabase.LoadMainAssetAtPath(path)
            ?? throw new InvalidOperationException("Missing asset: " + path));
        private static SerializedProperty Field(SerializedObject data, string name) => data.FindProperty("<" + name + ">k__BackingField")
            ?? data.FindProperty(name) ?? throw new InvalidOperationException("Missing field: " + name);
        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path)
            ?? throw new InvalidOperationException("Missing sprite: " + path);
        private static void Save(SerializedObject data) { data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(data.targetObject); }
    }
}
