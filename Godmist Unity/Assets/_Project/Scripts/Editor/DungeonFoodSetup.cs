using System;
using System.IO;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Systems.Items;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class DungeonFoodSetup
    {
        public const string FoodPath = "Assets/_Project/ScriptableObjects/Items/CID_TravelRation.asset";
        public const string IconPath = "Assets/_Project/Art/Icons/Items/VS22/travel-ration.png";
        public const string CatalogPath = "Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset";
        public const string KitPath = "Assets/_Project/ScriptableObjects/Items/ID_CampingKit.asset";
        static string F(string name) => "<" + name + ">k__BackingField";
        [MenuItem("Godmist/Content/Configure dungeon food and camp recovery")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var biome in new[] { "Forest", "Catacombs" })
            {
                var folder = biome == "Forest" ? "Dungeons/Forest/Loot/" : "Catacombs/Loot/";
                LootTableData Load(string kind, string bracket) => AssetDatabase.LoadAssetAtPath<LootTableData>(
                    "Assets/_Project/ScriptableObjects/" + folder + "LT_" + biome + "_" + kind + "_" + bracket + "_Production.asset");
                ConfigureTables(Load("Enemy", "1_10"), Load("Enemy", "11_20"), Load("Chest", "1_10"), Load("Chest", "11_20"));
            }
            Backup(KitPath);
            var kit = AssetDatabase.LoadAssetAtPath<ItemDefinition>(KitPath);
            var data = new SerializedObject(kit);
            var description = data.FindProperty(F("Description"));
            description.stringValue = description.stringValue.Replace("40%", "60%");
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(kit);
        }
        // Also called by full biome authoring, so it cannot erase the new drop.
        public static void ConfigureTables(LootTableData enemyLow, LootTableData enemyHigh, LootTableData chestLow, LootTableData chestHigh)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var food = EnsureFood();
            AddFood(enemyLow, food, DungeonFoodRules.EnemyDropChance); AddFood(enemyHigh, food, DungeonFoodRules.EnemyDropChance);
            AddFood(chestLow, food, DungeonFoodRules.ChestDropChance); AddFood(chestHigh, food, DungeonFoodRules.ChestDropChance);
        }
        static ConsumableItemDefinition EnsureFood()
        {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.maxTextureSize = 32; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (icon == null) throw new InvalidOperationException("Missing food icon.");
            var food = AssetDatabase.LoadAssetAtPath<ConsumableItemDefinition>(FoodPath);
            if (food == null) { food = ScriptableObject.CreateInstance<ConsumableItemDefinition>(); AssetDatabase.CreateAsset(food, FoodPath); }
            var data = new SerializedObject(food);
            data.FindProperty(F("Id")).stringValue = DungeonFoodRules.ItemId;
            data.FindProperty(F("DisplayName")).stringValue = "Prowiant";
            data.FindProperty(F("Description")).stringValue = "Porcja chleba i suszonego mięsa zawinięta w płótno.";
            data.FindProperty(F("Icon")).objectReferenceValue = icon;
            data.FindProperty(F("ItemType")).intValue = (int)ItemType.Food;
            data.FindProperty(F("UseType")).intValue = (int)ConsumableUseType.Food;
            data.FindProperty(F("MaxCharges")).intValue = 1;
            data.FindProperty(F("BaseRarity")).intValue = (int)RarityTier.Common;
            data.FindProperty(F("BaseValue")).intValue = 4; data.FindProperty(F("Weight")).floatValue = .1f;
            data.FindProperty(F("IsStackable")).boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(food);
            Backup(CatalogPath);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(CatalogPath);
            var content = new SerializedObject(catalog); var list = content.FindProperty(F("ItemDefinitions"));
            var found = false;
            for (var n = 0; n < list.arraySize; n++) if (list.GetArrayElementAtIndex(n).objectReferenceValue == food) found = true;
            if (!found) { list.InsertArrayElementAtIndex(list.arraySize); list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = food; }
            content.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(catalog);
            return food;
        }
        static void AddFood(LootTableData table, ConsumableItemDefinition food, float chance)
        {
            if (table == null) throw new InvalidOperationException("Missing production loot table.");
            Backup(AssetDatabase.GetAssetPath(table));
            var data = new SerializedObject(table); var entries = data.FindProperty(F("Entries"));
            SerializedProperty entry = null;
            for (var n = 0; n < entries.arraySize; n++)
                if (entries.GetArrayElementAtIndex(n).FindPropertyRelative(F("ItemDefinition")).objectReferenceValue == food)
                    entry = entries.GetArrayElementAtIndex(n);
            if (entry == null) { entries.InsertArrayElementAtIndex(entries.arraySize); entry = entries.GetArrayElementAtIndex(entries.arraySize - 1); }
            entry.FindPropertyRelative(F("ItemDefinition")).objectReferenceValue = food;
            entry.FindPropertyRelative(F("DropChance")).floatValue = chance;
            var amount = entry.FindPropertyRelative(F("Amount"));
            amount.FindPropertyRelative("Min").intValue = amount.FindPropertyRelative("Max").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(table);
        }
        static void Backup(string path)
        {
            var backup = Path.GetFullPath("../LocalHandoffs/dungeon-food-recovery-20261024/before/" + path);
            if (!File.Exists(path) || File.Exists(backup)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(path, backup);
        }
    }
}
