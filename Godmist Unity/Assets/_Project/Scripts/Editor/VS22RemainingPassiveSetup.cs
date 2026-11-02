using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the approved VS-22 passive icons for the five production trees that
/// were authored after the first three VS-22 importers. The tree data itself is
/// authored by the VS-07/VS-09 setup scripts; this tool only imports art and
/// binds each node icon, reusing SkillData.Icon for granted-skill nodes.
/// </summary>
public static class VS22RemainingPassiveSetup
{
    private const string PassiveArtRoot = "Assets/_Project/Art/Icons/Passives/VS22/";

    private static string Field(string name) => "<" + name + ">k__BackingField";

    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static string ReviewRoot => Path.GetFullPath(Path.Combine(ProjectRoot, "..", "output", "asset-review"));

    private sealed class TreeSpec
    {
        public string DisplayName;
        public string TreeId;
        public string NodePrefix;
        public string TreeAssetPath;
        public string CatalogRelativePath;
        public string SourceDirectory;
        public string TargetDirectory;
        public int PassiveCount;
        public string[] GrantedSkillNodeIds;
    }

    [Serializable]
    private sealed class PassiveCatalog
    {
        public string format;
        public CatalogTree tree;
        public CatalogScope scope;
        public PassiveCatalogEntry[] entries;
    }

    [Serializable]
    private sealed class CatalogTree
    {
        public string fullId;
        public string name;
    }

    [Serializable]
    private sealed class CatalogScope
    {
        public int entryCount;
        public string[] excludedGrantedSkillNodes;
    }

    [Serializable]
    private sealed class PassiveCatalogEntry
    {
        public string fullId;
        public string slug;
        public string outputPng;
    }

    private static readonly TreeSpec[] RemainingTrees =
    {
        new TreeSpec
        {
            DisplayName = "Scout Blade Dancer",
            TreeId = "tree.scout.blade_dancer",
            NodePrefix = "node.scout.blade_dancer.",
            TreeAssetPath = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Scout_BladeDancer.asset",
            CatalogRelativePath = "passives-scout-blade-dancer/blade-dancer-passives-catalog.json",
            SourceDirectory = "passives-scout-blade-dancer",
            TargetDirectory = "ScoutBladeDancer",
            PassiveCount = 17,
            GrantedSkillNodeIds = new[]
            {
                "node.scout.blade_dancer.poisoned_dagger",
                "node.scout.blade_dancer.quick_strikes",
                "node.scout.blade_dancer.stimulating_tonic"
            }
        },
        new TreeSpec
        {
            DisplayName = "Mage Warlock",
            TreeId = "tree.mage.warlock",
            NodePrefix = "node.mage.warlock.",
            TreeAssetPath = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Mage_Warlock.asset",
            CatalogRelativePath = "passives-mage-warlock/warlock-passives-catalog.json",
            SourceDirectory = "passives-mage-warlock",
            TargetDirectory = "MageWarlock",
            PassiveCount = 15,
            GrantedSkillNodeIds = new[]
            {
                "node.mage.warlock.offering",
                "node.mage.warlock.magic_focus",
                "node.mage.warlock.sacrifice",
                "node.mage.warlock.energy_split",
                "node.mage.warlock.ritual"
            }
        },
        new TreeSpec
        {
            DisplayName = "Mage Fire",
            TreeId = "tree.mage.fire",
            NodePrefix = "node.mage.fire.",
            TreeAssetPath = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Mage_Fire.asset",
            CatalogRelativePath = "passives-mage-fire/fire-passives-catalog.json",
            SourceDirectory = "passives-mage-fire",
            TargetDirectory = "MageFire",
            PassiveCount = 18,
            GrantedSkillNodeIds = new[]
            {
                "node.mage.fire.fire_aura",
                "node.mage.fire.fire_arrow"
            }
        },
        new TreeSpec
        {
            DisplayName = "Paladin Priest",
            TreeId = "tree.paladin.priest",
            NodePrefix = "node.paladin.priest.",
            TreeAssetPath = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Paladin_Priest.asset",
            CatalogRelativePath = "passives-paladin-priest/priest-passives-catalog.json",
            SourceDirectory = "passives-paladin-priest",
            TargetDirectory = "PaladinPriest",
            PassiveCount = 17,
            GrantedSkillNodeIds = new[]
            {
                "node.paladin.priest.group_heal",
                "node.paladin.priest.holy_regeneration",
                "node.paladin.priest.blessing"
            }
        },
        new TreeSpec
        {
            DisplayName = "Paladin Crusader",
            TreeId = "tree.paladin.crusader",
            NodePrefix = "node.paladin.crusader.",
            TreeAssetPath = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Paladin_Crusader.asset",
            CatalogRelativePath = "passives-paladin-crusader/crusader-passives-catalog.json",
            SourceDirectory = "passives-paladin-crusader",
            TargetDirectory = "PaladinCrusader",
            PassiveCount = 17,
            GrantedSkillNodeIds = new[]
            {
                "node.paladin.crusader.judgement_field",
                "node.paladin.crusader.blinding_light",
                "node.paladin.crusader.grounding_wave"
            }
        }
    };

    [MenuItem("Godmist/Content/Configure VS-22 Remaining passive art")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play Mode before authoring remaining VS-22 passive art.");

        foreach (var spec in RemainingTrees)
        {
            var entries = LoadAndValidateCatalog(spec);
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var entry in entries)
                sprites.Add(entry.fullId, ImportPassiveSprite(spec, entry));
            BindTree(spec, entries, sprites);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Verify();
        Debug.Log("VS-22: imported and bound five remaining production passive trees (84 passive icons); granted skill nodes reuse SkillData.Icon.");
    }

    [MenuItem("Godmist/Content/Verify VS-22 Remaining passive art")]
    public static void Verify()
    {
        foreach (var spec in RemainingTrees)
            VerifyTree(spec);

        Debug.Log("VS-22: verified 5/5 remaining production passive trees (84 imported passive icons and 16 granted skill nodes).");
    }

    [MenuItem("Godmist/Content/Verify VS-22 All production passive art")]
    public static void VerifyAllProduction()
    {
        VS22ScoutAssassinPassiveSetup.Verify();
        VS22WarriorBrawlerPassiveSetup.Verify();
        VS22WarriorCommanderPassiveSetup.Verify();
        Verify();
        Debug.Log("VS-22: verified all 8 production passive trees (160/160 nodes, 133 imported passive icons, 27 granted skill icon reuses).");
    }

    /// <summary>
    /// Used by the class content setup scripts when they rebuild a tree. It
    /// keeps an already imported passive icon across a later content rerun;
    /// granted-skill nodes deliberately return null so the caller can bind the
    /// active SkillData.Icon instead.
    /// </summary>
    internal static Sprite TryLoadPassiveIcon(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;

        foreach (var spec in RemainingTrees)
        {
            if (!nodeId.StartsWith(spec.NodePrefix, StringComparison.Ordinal))
                continue;
            if (Array.IndexOf(spec.GrantedSkillNodeIds, nodeId) >= 0)
                return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(PassiveTargetPath(spec, nodeId.Substring(spec.NodePrefix.Length)));
        }

        return null;
    }

    private static IReadOnlyList<PassiveCatalogEntry> LoadAndValidateCatalog(TreeSpec spec, bool requireSourceFiles = true)
    {
        var catalogPath = Path.Combine(Application.dataPath, "_Project/Editor/ArtCatalogs", spec.CatalogRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(catalogPath))
            throw new FileNotFoundException("Missing " + spec.DisplayName + " passive catalog", catalogPath);

        var catalog = JsonUtility.FromJson<PassiveCatalog>(File.ReadAllText(catalogPath));
        if (catalog == null || catalog.entries == null || catalog.entries.Length != spec.PassiveCount)
            throw new InvalidOperationException(spec.DisplayName + " passive catalog must contain exactly " + spec.PassiveCount + " entries.");
        if (catalog.tree == null || !string.Equals(catalog.tree.fullId, spec.TreeId, StringComparison.Ordinal))
            throw new InvalidOperationException(spec.DisplayName + " passive catalog tree ID does not match " + spec.TreeId + ".");
        if (catalog.scope == null || catalog.scope.excludedGrantedSkillNodes == null)
            throw new InvalidOperationException(spec.DisplayName + " passive catalog is missing excluded granted-skill metadata.");

        var grantedIds = new HashSet<string>(spec.GrantedSkillNodeIds, StringComparer.Ordinal);
        if (!grantedIds.SetEquals(catalog.scope.excludedGrantedSkillNodes))
            throw new InvalidOperationException(spec.DisplayName + " catalog granted-skill IDs do not match the configured tree contract.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in catalog.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.fullId) || string.IsNullOrWhiteSpace(entry.slug) || string.IsNullOrWhiteSpace(entry.outputPng))
                throw new InvalidOperationException(spec.DisplayName + " passive catalog contains an incomplete entry.");
            if (!entry.fullId.StartsWith(spec.NodePrefix, StringComparison.Ordinal) || !ids.Add(entry.fullId))
                throw new InvalidOperationException("Invalid or duplicate " + spec.DisplayName + " passive ID: " + entry.fullId);
            if (!slugs.Add(entry.slug))
                throw new InvalidOperationException("Duplicate " + spec.DisplayName + " passive slug: " + entry.slug);
            if (grantedIds.Contains(entry.fullId))
                throw new InvalidOperationException("Granted skill node must not be in " + spec.DisplayName + " passive catalog: " + entry.fullId);

            var sourcePath = Path.Combine(ReviewRoot, spec.SourceDirectory, entry.outputPng.Replace('/', Path.DirectorySeparatorChar));
            if (requireSourceFiles && !File.Exists(sourcePath))
                throw new FileNotFoundException("Missing " + spec.DisplayName + " passive PNG", sourcePath);
        }

        if (catalog.scope.entryCount != 0 && catalog.scope.entryCount != catalog.entries.Length)
            throw new InvalidOperationException(spec.DisplayName + " catalog scope count does not match entries.");
        return catalog.entries;
    }

    private static Sprite ImportPassiveSprite(TreeSpec spec, PassiveCatalogEntry entry)
    {
        var sourcePath = Path.Combine(ReviewRoot, spec.SourceDirectory, entry.outputPng.Replace('/', Path.DirectorySeparatorChar));
        var targetAssetPath = PassiveTargetPath(spec, entry.slug);
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

    private static void BindTree(TreeSpec spec, IReadOnlyList<PassiveCatalogEntry> entries, IReadOnlyDictionary<string, Sprite> sprites)
    {
        var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(spec.TreeAssetPath)
            ?? throw new InvalidOperationException("Missing " + spec.DisplayName + " tree: " + spec.TreeAssetPath);
        var entryIds = new HashSet<string>(entries.Select(entry => entry.fullId), StringComparer.Ordinal);
        var grantedIds = new HashSet<string>(spec.GrantedSkillNodeIds, StringComparer.Ordinal);
        var serializedObject = new SerializedObject(tree);
        var nodes = serializedObject.FindProperty(Field("Nodes"))
            ?? throw new InvalidOperationException(spec.DisplayName + " tree nodes field is missing.");
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < nodes.arraySize; index++)
        {
            var node = nodes.GetArrayElementAtIndex(index);
            var idProperty = node.FindPropertyRelative(Field("Id"));
            var nodeId = idProperty.stringValue;
            if (!seen.Add(nodeId))
                throw new InvalidOperationException("Duplicate " + spec.DisplayName + " node ID: " + nodeId);
            var icon = node.FindPropertyRelative(Field("Icon"));

            if (entryIds.Contains(nodeId))
            {
                icon.objectReferenceValue = sprites[nodeId];
                continue;
            }

            if (!grantedIds.Contains(nodeId))
                throw new InvalidOperationException("Unexpected " + spec.DisplayName + " node ID: " + nodeId);
            var grantedSkill = node.FindPropertyRelative(Field("GrantedSkill")).objectReferenceValue as SkillData;
            if (grantedSkill == null || grantedSkill.Icon == null)
                throw new InvalidOperationException("Granted skill icon is missing for " + nodeId);
            icon.objectReferenceValue = grantedSkill.Icon;
        }

        if (!seen.SetEquals(entryIds.Concat(grantedIds)))
            throw new InvalidOperationException(spec.DisplayName + " nodes do not match the approved passive catalog.");
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tree);
    }

    private static void VerifyTree(TreeSpec spec)
    {
        var entries = LoadAndValidateCatalog(spec, requireSourceFiles: false);
        var entryIds = new HashSet<string>(entries.Select(entry => entry.fullId), StringComparer.Ordinal);
        var grantedIds = new HashSet<string>(spec.GrantedSkillNodeIds, StringComparer.Ordinal);
        var tree = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(spec.TreeAssetPath)
            ?? throw new InvalidOperationException("Missing " + spec.DisplayName + " tree: " + spec.TreeAssetPath);
        if (!string.Equals(tree.Id, spec.TreeId, StringComparison.Ordinal))
            throw new InvalidOperationException(spec.DisplayName + " tree ID changed: " + tree.Id);
        if (tree.Nodes == null || tree.Nodes.Count != entryIds.Count + grantedIds.Count)
            throw new InvalidOperationException(spec.DisplayName + " tree must contain " + (entryIds.Count + grantedIds.Count) + " nodes.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in tree.Nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id) || !seen.Add(node.Id))
                throw new InvalidOperationException(spec.DisplayName + " tree contains a missing or duplicate node ID.");
            if (!node.Id.StartsWith(spec.NodePrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected " + spec.DisplayName + " node ID: " + node.Id);

            if (entryIds.Contains(node.Id))
            {
                var expectedPath = PassiveTargetPath(spec, node.Id.Substring(spec.NodePrefix.Length));
                AssertImportedTexture(expectedPath);
                var expectedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(expectedPath);
                if (node.Icon != expectedIcon)
                    throw new InvalidOperationException("Passive icon is not bound to " + node.Id);
                continue;
            }

            if (!grantedIds.Contains(node.Id))
                throw new InvalidOperationException("Unexpected non-passive " + spec.DisplayName + " node: " + node.Id);
            if (node.GrantedSkill == null || node.GrantedSkill.Icon == null)
                throw new InvalidOperationException("Granted skill icon is missing for " + node.Id);
            if (node.Icon != node.GrantedSkill.Icon)
                throw new InvalidOperationException("Granted skill node does not reuse SkillData.Icon: " + node.Id);
        }

        if (!seen.SetEquals(entryIds.Concat(grantedIds)))
            throw new InvalidOperationException(spec.DisplayName + " node IDs do not match the approved passive catalog.");
    }

    private static string PassiveTargetPath(TreeSpec spec, string slug)
    {
        return PassiveArtRoot + spec.TargetDirectory + "/" + slug + "-32.png";
    }

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
