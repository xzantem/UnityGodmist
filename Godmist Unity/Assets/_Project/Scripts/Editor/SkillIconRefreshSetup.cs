using System;
using System.Linq;
using Godmist.Data.Skills;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Scoped VS22 icon authoring. Never rebuild skills or talent mechanics.</summary>
    public static class SkillIconRefreshSetup
    {
        public const string Art = "Assets/_Project/Art/Icons/";
        public const string Brawler = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Warrior_Brawler.asset";
        public const string Dancer = "Assets/_Project/ScriptableObjects/Skills/SkillTrees/ST_Scout_BladeDancer.asset";
        public static readonly string[] Icons = { "Skills/VS22/discharge-32.png",
            "Passives/VS22/WarriorBrawler/wounding-32.png", "Passives/VS22/WarriorBrawler/larger_wound-32.png",
            "Passives/VS22/WarriorBrawler/revenge-32.png", "Passives/VS22/ScoutBladeDancer/efficient_recovery-32.png" };
        static string Field(string name) => "<" + name + ">k__BackingField";

        [MenuItem("Godmist/Content/Apply dedicated Discharge and Recovery icons")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before changing icon assets.");
            // Resolve every source and binding before changing any serialized references.
            var sprites = Icons.Select(path => Import(Art + path)).ToArray();
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(WarriorDischargeSetup.Path)
                ?? throw new InvalidOperationException("Missing Discharge skill.");
            var brawler = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(Brawler);
            var dancer = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>(Dancer);
            foreach (var id in new[] { "bleeding_destruction", "wounding", "larger_wound", "revenge" })
                if (brawler == null || !brawler.Nodes.Any(n => n.Id == "node.warrior.brawler." + id))
                    throw new InvalidOperationException("Missing Brawler node: " + id);
            if (dancer == null || !dancer.Nodes.Any(n => n.Id == "node.scout.blade_dancer.efficient_recovery"))
                throw new InvalidOperationException("Missing Efficient Recovery node.");
            var so = new SerializedObject(skill);
            so.FindProperty(Field("Icon")).objectReferenceValue = sprites[0];
            so.ApplyModifiedPropertiesWithoutUndo();
            Bind(brawler, "node.warrior.brawler.bleeding_destruction", sprites[0]);
            Bind(brawler, "node.warrior.brawler.wounding", sprites[1]);
            Bind(brawler, "node.warrior.brawler.larger_wound", sprites[2]);
            Bind(brawler, "node.warrior.brawler.revenge", sprites[3]);
            Bind(dancer, "node.scout.blade_dancer.efficient_recovery", sprites[4]);
            AssetDatabase.SaveAssetIfDirty(skill);
            AssetDatabase.SaveAssetIfDirty(brawler);
            AssetDatabase.SaveAssetIfDirty(dancer);
        }

        static Sprite Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("Missing icon: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            foreach (var platform in new[] { "DefaultTexturePlatform", "Standalone" })
            {
                var settings = platform == "DefaultTexturePlatform" ? importer.GetDefaultPlatformTextureSettings()
                    : importer.GetPlatformTextureSettings(platform);
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path)
                ?? throw new InvalidOperationException("Icon did not import: " + path);
        }

        static void Bind(SkillTreeDefinition tree, string id, Sprite icon)
        {
            var so = new SerializedObject(tree);
            var nodes = so.FindProperty(Field("Nodes"));
            for (var i = 0; i < nodes.arraySize; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i);
                if (node.FindPropertyRelative(Field("Id")).stringValue != id) continue;
                node.FindPropertyRelative(Field("Icon")).objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
            throw new InvalidOperationException("Missing node: " + id);
        }
    }
}
