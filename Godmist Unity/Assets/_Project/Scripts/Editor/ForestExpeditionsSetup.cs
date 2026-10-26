using System;
using Godmist.Data.Dungeons;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ForestExpeditionsSetup
    {
        public const string RegularPath = "Assets/_Project/ScriptableObjects/Dungeons/Forest/DD_Forest_Expedition.asset";
        public const string BossPath = "Assets/_Project/ScriptableObjects/Dungeons/Forest/DD_Forest_BossExpedition.asset";
        public const string CatalogPath = "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset";

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Expeditions")]
        public static void Configure()
        {
            var prototype = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            if (prototype == null || catalog == null || prototype.BossEncounterDefinition == null || prototype.LootTableCatalog == null)
                throw new InvalidOperationException("Author Forest enemies, Ent and loot before enabling expeditions.");
            ConfigureVariant(prototype, catalog, RegularPath, "dungeon.forest.expedition", "Skażony Las — wyprawa", false);
            ConfigureVariant(prototype, catalog, BossPath, "dungeon.forest.boss_expedition", "Skażony Las — Zarażony Ent", true);
            if(AssetDatabase.LoadAssetAtPath<DungeonDefinition>(VS16CatacombsBossSetup.BanditDungeonPath)!=null)
                VS16CatacombsBossSetup.ConfigureBanditLeaderLoot();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void RefreshIfAuthored()
        {
            if (AssetDatabase.LoadAssetAtPath<DungeonDefinition>(RegularPath) != null ||
                AssetDatabase.LoadAssetAtPath<DungeonDefinition>(BossPath) != null) Configure();
        }

        private static void ConfigureVariant(DungeonDefinition prototype, GameContentCatalog catalog,
            string path, string id, string label, bool boss)
        {
            var definition = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<DungeonDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            EditorUtility.CopySerialized(prototype, definition);
            definition.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("<Id>k__BackingField").stringValue = id;
            serialized.FindProperty("<DisplayName>k__BackingField").stringValue = label;
            serialized.FindProperty("<IsAvailableAsOffer>k__BackingField").boolValue = true;
            if (!boss) serialized.FindProperty("<BossEncounterDefinition>k__BackingField").objectReferenceValue = null;
            var levels = serialized.FindProperty("<FixedOfferLevels>k__BackingField");
            levels.arraySize = boss ? 2 : 0;
            if (boss)
            {
                levels.GetArrayElementAtIndex(0).intValue = 10;
                levels.GetArrayElementAtIndex(1).intValue = 20;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            DungeonLengthSetup.Apply(definition);
            ForestExplorationObjectsSetup.WireIfAuthored(definition);
            if (!catalog.DungeonDefinitions.Contains(definition)) catalog.DungeonDefinitions.Add(definition);
            EditorUtility.SetDirty(definition);
        }
    }
}
