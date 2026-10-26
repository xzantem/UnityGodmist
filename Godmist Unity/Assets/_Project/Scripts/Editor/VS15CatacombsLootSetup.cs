using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Data.World;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Authors the two Catacombs loot brackets without touching the VS-16 boss bag.</summary>
    public static class VS15CatacombsLootSetup
    {
        private const string Root = "Assets/_Project/ScriptableObjects/";
        private const string LootRoot = Root + "Catacombs/Loot/";
        private static string F(string name) => "<" + name + ">k__BackingField";

        private static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("VS-15: missing " + path);

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static SerializedProperty Add(SerializedProperty list)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            return list.GetArrayElementAtIndex(list.arraySize - 1);
        }

        private static void Set(SerializedProperty property, object value)
        {
            if (property == null) throw new InvalidOperationException("VS-15: missing serialized field");
            switch (value)
            {
                case null: property.objectReferenceValue = null; break;
                case UnityEngine.Object asset: property.objectReferenceValue = asset; break;
                case string str: property.stringValue = str; break;
                case bool flag: property.boolValue = flag; break;
                case Enum enumeration: property.enumValueIndex = Convert.ToInt32(enumeration); break;
                case int integer: property.intValue = integer; break;
                case float number: property.floatValue = number; break;
                default: throw new ArgumentException("Unsupported VS-15 serialized value: " + value);
            }
        }

        private static void Field(SerializedObject data, string field, object value) =>
            Set(data.FindProperty(F(field)), value);
        private static void Sub(SerializedProperty data, string field, object value) =>
            Set(data.FindPropertyRelative(F(field)), value);

        private static Dictionary<string, ItemDefinition> IndexItems()
        {
            var result = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileName(path).StartsWith("JGD_", StringComparison.Ordinal))
                    continue;
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item != null && !string.IsNullOrWhiteSpace(item.Id))
                    result.TryAdd(item.Id, item);
            }
            return result;
        }

        private static ItemDefinition Item(IReadOnlyDictionary<string, ItemDefinition> items, string id) =>
            items.TryGetValue(id, out var item) ? item
                : throw new InvalidOperationException("VS-15: missing item " + id);

        private static void Entry(SerializedProperty entries, ItemDefinition item,
            int min, int max, EquipmentQuality? quality = null)
        {
            var entry = Add(entries);
            Sub(entry, "ItemDefinition", item);
            Sub(entry, "SelectionWeight", 1f);
            var amount = entry.FindPropertyRelative(F("Amount"));
            Set(amount.FindPropertyRelative("Min"), min);
            Set(amount.FindPropertyRelative("Max"), max);
            Sub(entry, "HasFixedEquipmentQuality", quality.HasValue);
            Sub(entry, "EquipmentQuality", quality ?? EquipmentQuality.Normal);
        }

        private static void Pool(SerializedProperty pools, float chance,
            params (ItemDefinition Item, int Min, int Max)[] entries)
            => Pool(pools, chance, null, entries);

        private static void Pool(SerializedProperty pools, float chance, string distinctGroup,
            params (ItemDefinition Item, int Min, int Max)[] entries)
        {
            var pool = Add(pools);
            Sub(pool, "DropChance", chance);
            Sub(pool, "DistinctSelectionGroup", distinctGroup ?? string.Empty);
            var list = pool.FindPropertyRelative(F("Entries"));
            list.ClearArray();
            foreach (var entry in entries) Entry(list, entry.Item, entry.Min, entry.Max);
        }

        private static void Bracket(SerializedObject bag, LootLevelBracket bracket,
            Action<SerializedProperty> configure)
        {
            var list = bag.FindProperty(F("Brackets"));
            var data = Add(list);
            Sub(data, "Bracket", bracket);
            var pools = data.FindPropertyRelative(F("Pools"));
            pools.ClearArray();
            configure(pools);
        }

        private static LootBagItemDefinition Bag(string file, string id, string name,
            string description, Action<SerializedObject> configure)
        {
            var bag = GetOrCreate<LootBagItemDefinition>(LootRoot + file + ".asset");
            var so = new SerializedObject(bag);
            Field(so, "Id", id);
            Field(so, "DisplayName", name);
            Field(so, "Description", description);
            Field(so, "ItemType", ItemType.Lootbag);
            Field(so, "BaseRarity", RarityTier.Common);
            Field(so, "BaseValue", 0);
            Field(so, "Weight", 1f);
            Field(so, "IsStackable", false);
            so.FindProperty(F("Brackets")).ClearArray();
            configure(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bag);
            return bag;
        }

        private static LootBagItemDefinition ConfigureResources(IReadOnlyDictionary<string, ItemDefinition> items)
        {
            (ItemDefinition, int, int)[] Staples(bool high)
            {
                var basePool = high
                    ? new[] {
                        (Item(items,"item.smithing.iron_ore"),3,5),
                        (Item(items,"item.smithing.deer_skin"),1,2),
                        (Item(items,"item.smithing.birch_wood"),1,2),
                        (Item(items,"item.smithing.palm_fibre"),3,4) }
                    : new[] {
                        (Item(items,"item.smithing.iron_ore"),3,5),
                        (Item(items,"item.smithing.goat_skin"),1,3),
                        (Item(items,"item.smithing.larch_wood"),1,2),
                        (Item(items,"item.smithing.cotton_fibre"),3,4) };
                return basePool;
            }

            void ConfigureBracket(SerializedProperty pools, bool high)
            {
                Pool(pools, .8f,
                    (Item(items, high ? "item.alchemy.alcohol" : "item.alchemy.weak_alcohol"),1,2),
                    (Item(items, high ? "item.alchemy.gunpowder" : "item.alchemy.weak_gunpowder"),1,2));
                foreach (var chance in new[] { 1f, .7f, .3f })
                    Pool(pools, chance, "staples", Staples(high));
                if (high) Pool(pools, .8f, (Item(items,"item.smithing.coal"),2,3));
                Pool(pools, .2f, "alchemy",
                    (Item(items,"item.alchemy.common_reaper"),1,1),
                    (Item(items,"item.alchemy.puffball"),1,1),
                    (Item(items,"item.alchemy.skeleton_bone"),1,1),
                    (Item(items,"item.alchemy.undead_brain"),1,1));
                if (high)
                {
                    Pool(pools, .6f, (Item(items,"item.runic.solid_limestone"),1,2));
                    Pool(pools, .3f, (Item(items,"item.vs10.granat"),1,2));
                    Pool(pools, .15f, (Item(items,"item.runic.amber"),1,1));
                }

            }

            var bag = Bag("ID_Catacombs_Resources", "item.lootbag.catacombs.resources",
                "Koścista torba zasobów", "Worek z materiałami zebranymi w katakumbach.", so => {
                    Bracket(so, LootLevelBracket.Level1To10, pools => ConfigureBracket(pools, false));
                    Bracket(so, LootLevelBracket.Level11To20, pools => ConfigureBracket(pools, true));
                });
            var calibrated = new SerializedObject(bag);
            CraftingMaterialDropSetup.Configure(calibrated, false);
            calibrated.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bag);
            return bag;
        }

        private static LootBagItemDefinition ConfigureEquipment(
            string file, string id, string name, ItemType type,
            IReadOnlyList<EquipmentItemDefinition> definitions)
        {
            return Bag(file, id, name,
                type == ItemType.Weapon ? "Worek z orężem znalezionym w katakumbach."
                    : "Worek z pancerzami znalezionymi w katakumbach.", so => {
                    foreach (var bracket in new[] {
                        LootLevelBracket.Level1To10, LootLevelBracket.Level11To20 })
                        Bracket(so, bracket, pools => {
                            foreach (var chance in new[] { 1f, 1f, .8f, .5f, .25f })
                            {
                                var pool = Add(pools);
                                Sub(pool, "DropChance", chance);
                                var entries = pool.FindPropertyRelative(F("Entries"));
                                entries.ClearArray();
                                foreach (var definition in definitions)
                                    foreach (var quality in new[] { EquipmentQuality.Poor,
                                        EquipmentQuality.Normal, EquipmentQuality.Excellent })
                                        Entry(entries, definition, 1, 1, quality);
                            }
                        });
                });
        }

        private static LootBagItemDefinition ConfigureGaldurytes(ItemDefinition generic)
        {
            return Bag("ID_Catacombs_Galdurytes", "item.lootbag.catacombs.galdurytes",
                "Torba z galdurytami", "Worek z runicznymi kamieniami znalezionymi w katakumbach.", so => {
                    Bracket(so, LootLevelBracket.Level11To20, pools => {
                        foreach (var chance in new[] { 1f, 1f, 1f, .8f, .55f, .4f, .25f })
                            Pool(pools, chance, (generic, 1, 1));
                    });
                });
        }

        private static LootTableData Table(string file,
            params (ItemDefinition Item, float Chance, int Min, int Max)[] entries)
        {
            var table = GetOrCreate<LootTableData>(LootRoot + file + ".asset");
            var so = new SerializedObject(table);
            var list = so.FindProperty(F("Entries"));
            list.ClearArray();
            foreach (var entry in entries)
            {
                var data = Add(list);
                Sub(data, "ItemDefinition", entry.Item);
                Sub(data, "DropChance", entry.Chance);
                var amount = data.FindPropertyRelative(F("Amount"));
                Set(amount.FindPropertyRelative("Min"), entry.Min);
                Set(amount.FindPropertyRelative("Max"), entry.Max);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            return table;
        }

        private static EquipmentItemDefinition[] Equipment(ItemType type)
        {
            var paths = type == ItemType.Weapon
                ? new[] { "EID_WarriorLongsword", "EID_ScoutSwordAndDagger",
                    "EID_MageWand", "EID_PaladinHammer" }
                : new[] { "EID_WarriorHauberk", "EID_ScoutTunic",
                    "EID_MageRobe", "EID_PaladinPlateArmor" };
            return paths.Select(name => Require<EquipmentItemDefinition>(
                Root + "Equipment/" + name + ".asset")).ToArray();
        }

        private static void ConfigureCatalog(LootTableData enemyLow, LootTableData enemyHigh,
            LootTableData chestLow, LootTableData chestHigh)
        {
            var catalog = Require<DungeonLootTableCatalog>(Root + "Dungeons/DLC_Catacombs_Loot.asset");
            var so = new SerializedObject(catalog);
            var list = so.FindProperty(F("Entries"));
            var bossLow = list.arraySize > 0
                ? list.GetArrayElementAtIndex(0).FindPropertyRelative(F("BossLootTable")).objectReferenceValue
                : null;
            list.ClearArray();
            foreach (var bracket in new[] { LootLevelBracket.Level1To10, LootLevelBracket.Level11To20 })
            {
                var entry = Add(list);
                Sub(entry, "Theme", DungeonTheme.Catacombs);
                Sub(entry, "Bracket", bracket);
                Sub(entry, "EnemyLootTable", bracket == LootLevelBracket.Level1To10 ? enemyLow : enemyHigh);
                Sub(entry, "ChestLootTable", bracket == LootLevelBracket.Level1To10 ? chestLow : chestHigh);
                Sub(entry, "BossLootTable", bracket == LootLevelBracket.Level1To10 ? bossLow : null);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureSpeciesDrops(IReadOnlyDictionary<string, ItemDefinition> items)
        {
            var bone = Table("LT_Catacombs_Skeleton",
                (Item(items,"item.alchemy.skeleton_bone"),.05f,1,1));
            var brain = Table("LT_Catacombs_Undead",
                (Item(items,"item.alchemy.undead_brain"),.1f,1,1));
            foreach (var key in new[] { "swordsman", "archer", "occultist", "defender",
                "undead", "ghoul", "wraith" })
            {
                var enemy = Require<CharacterDefinition>(Root + "Catacombs/Enemies/CD_Catacombs_" + key + ".asset");
                var so = new SerializedObject(enemy);
                Field(so, "LootTable", key is "swordsman" or "archer" or "occultist" or "defender"
                    ? bone : key is "undead" or "ghoul" ? brain : null);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(enemy);
            }
        }

        private static void AddCatalogItem(SerializedProperty list, ItemDefinition item)
        {
            for (var i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
            Add(list).objectReferenceValue = item;
        }

        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("VS-15: exit Play Mode first");
            if (!AssetDatabase.IsValidFolder(LootRoot.TrimEnd('/')))
                AssetDatabase.CreateFolder(Root + "Catacombs", "Loot");
            var items = IndexItems();
            var resources = ConfigureResources(items);
            var weapons = ConfigureEquipment("ID_Catacombs_Weapons",
                "item.lootbag.catacombs.weapons", "Torba z bronią", ItemType.Weapon, Equipment(ItemType.Weapon));
            var armor = ConfigureEquipment("ID_Catacombs_Armor",
                "item.lootbag.catacombs.armor", "Torba z pancerzami", ItemType.Armor, Equipment(ItemType.Armor));
            var galduryte = Item(items, "item.galduryte.generic");
            var galdurytes = ConfigureGaldurytes(galduryte);

            var generic = new SerializedObject(galduryte);
            Field(generic, "BaseValue", 136);
            generic.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(galduryte);

            var enemyLow = Table("LT_Catacombs_Enemy_1_10_Production",
                (resources,.5f,1,1));
            var enemyHigh = Table("LT_Catacombs_Enemy_11_20_Production",
                (resources,.5f,1,1), (galdurytes,.01f,1,1));
            var chestLow = Table("LT_Catacombs_Chest_1_10_Production",
                (resources,1f,2,3), (weapons,.04f,1,1), (armor,.04f,1,1));
            var chestHigh = Table("LT_Catacombs_Chest_11_20_Production",
                (resources,1f,2,3), (weapons,.04f,1,1),
                (armor,.04f,1,1), (galdurytes,.02f,1,1));
            DungeonFoodSetup.ConfigureTables(enemyLow, enemyHigh, chestLow, chestHigh);
            ConfigureCatalog(enemyLow, enemyHigh, chestLow, chestHigh);
            BranchEquipmentRewardsSetup.ConfigureBudget(chestLow);
            BranchEquipmentRewardsSetup.ConfigureBudget(chestHigh);
            ConfigureSpeciesDrops(items);

            var content = Require<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
            var catalog = new SerializedObject(content);
            var list = catalog.FindProperty(F("ItemDefinitions"));
            foreach (var bag in new[] { resources, weapons, armor, galdurytes })
                AddCatalogItem(list, bag);
            catalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
        }
    }
}
