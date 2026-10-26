using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the approved VS-22 Scout Assassin passive icons and binds them to
/// the existing Scout Assassin tree without rewriting gameplay data.
/// </summary>
public static class VS22ScoutAssassinPassiveSetup
{
    internal const string PassiveArtRoot = "Assets/_Project/Art/Icons/Passives/VS22/ScoutAssassin/";

    private const string TreeAssetPath = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Scout_Assassin.asset";
    private const string CatalogRelativePath = "passives-scout-assassin/assassin-passives-catalog.json";
    private const string NodePrefix = "node.scout.assassin.";

    private static readonly HashSet<string> GrantedSkillNodeIds = new HashSet<string>(StringComparer.Ordinal)
    {
        NodePrefix + "ricochet",
        NodePrefix + "weak_point",
        NodePrefix + "shadow_warrior"
    };

    private static string Field(string name) => "<" + name + ">k__BackingField";

    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static string ReviewRoot => Path.GetFullPath(Path.Combine(ProjectRoot, "..", "output", "asset-review"));

    [Serializable]
    private sealed class PassiveCatalog
    {
        public PassiveCatalogEntry[] entries;
    }

    [Serializable]
    private sealed class PassiveCatalogEntry
    {
        public string fullId;
        public string slug;
        public string outputPng;
    }

    [MenuItem("Godmist/Content/Configure VS-22 Scout Assassin passive art")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before authoring VS-22 Scout Assassin passive art.");

        var entries = LoadAndValidateCatalog();
        var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        foreach (var entry in entries)
            sprites[entry.fullId] = ImportPassiveSprite(entry);

        BindTree(entries, sprites);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Verify();
        Debug.Log("VS-22: imported and bound 17 Scout Assassin passive icons; 3 granted skill nodes reuse SkillData.Icon.");
    }

    [MenuItem("Godmist/Content/Verify VS-22 Scout Assassin passive art")]
    public static void Verify()
    {
        var entries = LoadAndValidateCatalog(requireSourceFiles: false);
        var entryIds = new HashSet<string>(entries.Select(entry => entry.fullId), StringComparer.Ordinal);
        var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(TreeAssetPath)
            ?? throw new InvalidOperationException("Missing Scout Assassin tree: " + TreeAssetPath);
        if (!string.Equals(tree.Id, "tree.scout.assassin", StringComparison.Ordinal))
            throw new InvalidOperationException("Scout Assassin tree ID changed: " + tree.Id);
        if (tree.Nodes == null || tree.Nodes.Count != 20)
            throw new InvalidOperationException("Scout Assassin tree must contain 20 nodes.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in tree.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id) || !seen.Add(node.Id))
                throw new InvalidOperationException("Scout Assassin tree contains a missing or duplicate node ID.");
            if (!node.Id.StartsWith(NodePrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected Scout Assassin node ID: " + node.Id);

            if (entryIds.Contains(node.Id))
            {
                var expectedPath = PassiveTargetPath(node.Id.Substring(NodePrefix.Length));
                AssertImportedTexture(expectedPath);
                var expectedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(expectedPath);
                if (node.Icon != expectedIcon)
                    throw new InvalidOperationException("Passive icon is not bound to " + node.Id);
                continue;
            }

            if (!GrantedSkillNodeIds.Contains(node.Id))
                throw new InvalidOperationException("Unexpected non-passive Scout Assassin node: " + node.Id);
            if (node.GrantedSkill == null || node.GrantedSkill.Icon == null)
                throw new InvalidOperationException("Granted skill icon is missing for " + node.Id);
            if (node.Icon != node.GrantedSkill.Icon)
                throw new InvalidOperationException("Granted skill node does not reuse SkillData.Icon: " + node.Id);
        }

        if (!seen.SetEquals(entryIds.Concat(GrantedSkillNodeIds)))
            throw new InvalidOperationException("Scout Assassin node IDs do not match the approved passive catalog.");

        Debug.Log("VS-22: verified 20/20 Scout Assassin node icon references (17 imported passives, 3 granted skills).");
    }

    /// <summary>
    /// Used by the VS-07 seed to keep imported passive icons when it rebuilds
    /// the node array. Returns null for granted skill nodes and other trees.
    /// </summary>
    internal static Sprite TryLoadPassiveIcon(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || !nodeId.StartsWith(NodePrefix, StringComparison.Ordinal))
            return null;
        if (GrantedSkillNodeIds.Contains(nodeId))
            return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(PassiveTargetPath(nodeId.Substring(NodePrefix.Length)));
    }

    private static IReadOnlyList<PassiveCatalogEntry> LoadAndValidateCatalog(bool requireSourceFiles = true)
    {
        var catalogPath = Path.Combine(Application.dataPath, "_Project/Editor/ArtCatalogs", CatalogRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(catalogPath))
            throw new FileNotFoundException("Missing Scout Assassin passive catalog", catalogPath);

        var catalog = JsonUtility.FromJson<PassiveCatalog>(File.ReadAllText(catalogPath));
        if (catalog == null || catalog.entries == null || catalog.entries.Length != 17)
            throw new InvalidOperationException("Scout Assassin passive catalog must contain exactly 17 entries.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in catalog.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.fullId) || string.IsNullOrWhiteSpace(entry.slug) || string.IsNullOrWhiteSpace(entry.outputPng))
                throw new InvalidOperationException("Scout Assassin passive catalog contains an incomplete entry.");
            if (!entry.fullId.StartsWith(NodePrefix, StringComparison.Ordinal) || !ids.Add(entry.fullId))
                throw new InvalidOperationException("Invalid or duplicate Scout Assassin passive ID: " + entry.fullId);
            if (!slugs.Add(entry.slug))
                throw new InvalidOperationException("Duplicate Scout Assassin passive slug: " + entry.slug);
            if (GrantedSkillNodeIds.Contains(entry.fullId))
                throw new InvalidOperationException("Granted skill node must not be in passive catalog: " + entry.fullId);

            var sourcePath = Path.Combine(ReviewRoot, "passives-scout-assassin", entry.outputPng.Replace('/', Path.DirectorySeparatorChar));
            if (requireSourceFiles && !File.Exists(sourcePath))
                throw new FileNotFoundException("Missing Scout Assassin passive PNG", sourcePath);
        }

        var expectedIds = new HashSet<string>(new[]
        {
            NodePrefix + "backstab", NodePrefix + "farsight", NodePrefix + "critical_point",
            NodePrefix + "leg_sweep", NodePrefix + "throw_power", NodePrefix + "knife_bounce",
            NodePrefix + "rapid_strikes", NodePrefix + "deep_analysis", NodePrefix + "running_attack",
            NodePrefix + "surprise", NodePrefix + "karambit_style", NodePrefix + "throat_cut",
            NodePrefix + "keen_eye", NodePrefix + "sharp_knife", NodePrefix + "light_step",
            NodePrefix + "precise_strike", NodePrefix + "pommel_strike"
        }, StringComparer.Ordinal);
        if (!ids.SetEquals(expectedIds))
            throw new InvalidOperationException("Scout Assassin passive catalog IDs do not match the approved 17-node set.");

        return catalog.entries;
    }

    private static Sprite ImportPassiveSprite(PassiveCatalogEntry entry)
    {
        var sourcePath = Path.Combine(ReviewRoot, "passives-scout-assassin", entry.outputPng.Replace('/', Path.DirectorySeparatorChar));
        var targetAssetPath = PassiveTargetPath(entry.slug);
        var targetPath = Path.Combine(ProjectRoot, targetAssetPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
        if (!FilesEqual(sourcePath, targetPath))
            File.Copy(sourcePath, targetPath, true);

        AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(targetAssetPath) as TextureImporter
            ?? throw new InvalidOperationException("Missing texture importer: " + targetAssetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        var defaults = importer.GetDefaultPlatformTextureSettings();
        defaults.textureCompression = TextureImporterCompression.Uncompressed;
        defaults.crunchedCompression = false;
        importer.SetPlatformTextureSettings(defaults);
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        standalone.textureCompression = TextureImporterCompression.Uncompressed;
        standalone.crunchedCompression = false;
        importer.SetPlatformTextureSettings(standalone);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(targetAssetPath)
            ?? throw new InvalidOperationException("Missing imported passive sprite: " + targetAssetPath);
    }

    private static void BindTree(IReadOnlyList<PassiveCatalogEntry> entries, IReadOnlyDictionary<string, Sprite> sprites)
    {
        var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(TreeAssetPath)
            ?? throw new InvalidOperationException("Missing Scout Assassin tree: " + TreeAssetPath);
        var entryIds = new HashSet<string>(entries.Select(entry => entry.fullId), StringComparer.Ordinal);
        var serializedObject = new SerializedObject(tree);
        var nodes = serializedObject.FindProperty(Field("Nodes"))
            ?? throw new InvalidOperationException("Scout Assassin tree nodes field is missing.");
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < nodes.arraySize; index++)
        {
            var node = nodes.GetArrayElementAtIndex(index);
            var idProperty = node.FindPropertyRelative(Field("Id"));
            var nodeId = idProperty.stringValue;
            if (!seen.Add(nodeId))
                throw new InvalidOperationException("Duplicate Scout Assassin node ID: " + nodeId);

            var icon = node.FindPropertyRelative(Field("Icon"));
            if (entryIds.Contains(nodeId))
            {
                icon.objectReferenceValue = sprites[nodeId];
                continue;
            }

            if (!GrantedSkillNodeIds.Contains(nodeId))
                throw new InvalidOperationException("Unexpected Scout Assassin node ID: " + nodeId);
            var grantedSkill = node.FindPropertyRelative(Field("GrantedSkill")).objectReferenceValue as SkillData;
            if (grantedSkill == null || grantedSkill.Icon == null)
                throw new InvalidOperationException("Granted skill icon is missing for " + nodeId);
            icon.objectReferenceValue = grantedSkill.Icon;
        }

        if (!seen.SetEquals(entryIds.Concat(GrantedSkillNodeIds)))
            throw new InvalidOperationException("Scout Assassin nodes do not match the approved passive catalog.");
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tree);
    }

    private static string PassiveTargetPath(string slug) => PassiveArtRoot + slug + "-32.png";

    private static void AssertImportedTexture(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (texture == null || importer == null)
            throw new InvalidOperationException("Missing imported passive texture: " + path);
        if (texture.width != 32 || texture.height != 32 || importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single || importer.spritePixelsPerUnit != 32f ||
            importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp || importer.mipmapEnabled ||
            importer.GetDefaultPlatformTextureSettings().textureCompression != TextureImporterCompression.Uncompressed ||
            importer.GetPlatformTextureSettings("Standalone").textureCompression != TextureImporterCompression.Uncompressed)
            throw new InvalidOperationException("Invalid passive texture import settings: " + path);
    }

    private static bool FilesEqual(string sourcePath, string targetPath)
    {
        if (!File.Exists(targetPath))
            return false;
        var sourceInfo = new FileInfo(sourcePath);
        var targetInfo = new FileInfo(targetPath);
        if (sourceInfo.Length != targetInfo.Length)
            return false;
        return File.ReadAllBytes(sourcePath).SequenceEqual(File.ReadAllBytes(targetPath));
    }
}
