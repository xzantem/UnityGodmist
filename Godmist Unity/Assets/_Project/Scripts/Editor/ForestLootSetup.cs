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
    /// <summary>Authors forest loot brackets while preserving boss references and presentation.</summary>
    public static partial class ForestLootSetup
    {
        private const string Root = "Assets/_Project/ScriptableObjects/";
        private const string LootRoot = Root + "Dungeons/Forest/Loot/";
        private static string F(string name) => "<" + name + ">k__BackingField";

        private static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Forest loot: missing " + path);

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
            if (property == null) throw new InvalidOperationException("Forest loot: missing serialized field");
            switch (value)
            {
                case null: property.objectReferenceValue = null; break;
                case UnityEngine.Object asset: property.objectReferenceValue = asset; break;
                case string str: property.stringValue = str; break;
                case bool flag: property.boolValue = flag; break;
                case Enum enumeration: property.enumValueIndex = Convert.ToInt32(enumeration); break;
                case int integer: property.intValue = integer; break;
                case float number: property.floatValue = number; break;
                default: throw new ArgumentException("Unsupported Forest loot serialized value: " + value);
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
                : throw new InvalidOperationException("Forest loot: missing item " + id);

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
                        (Item(items,"item.smithing.nickel_ore"),3,5),
                        (Item(items,"item.smithing.deer_skin"),1,2),
                        (Item(items,"item.smithing.birch_wood"),1,2),
                        (Item(items,"item.smithing.palm_fibre"),3,4),
                        (Item(items,"item.smithing.yellow_energy_dust"),1,2) }
                    : new[] {
                        (Item(items,"item.smithing.nickel_ore"),3,5),
                        (Item(items,"item.smithing.wild_dog_skin"),2,5),
                        (Item(items,"item.smithing.linden_wood"),1,2),
                        (Item(items,"item.smithing.bukomina_fibre"),3,4),
                        (Item(items,"item.smithing.red_energy_dust"),1,2) };
                return basePool;
            }

            void ConfigureBracket(SerializedProperty pools, bool high)
            {
                Pool(pools, .8f,
                    (Item(items, high ? "item.alchemy.alcohol" : "item.alchemy.weak_alcohol"),1,2));
                foreach (var chance in new[] { 1f, .7f, .3f })
                    Pool(pools, chance, "staples", Staples(high));
                if (high) Pool(pools, .8f, (Item(items,"item.smithing.meteorite_shard"),2,3));
                Pool(pools, .2f, "alchemy",
                    (Item(items,"item.vs10.szyszka"),1,1),
                    (Item(items,"item.material.elf_ear"),1,1));
                if (high)
                {
                    Pool(pools, .6f, (Item(items,"item.runic.solid_limestone"),1,2));
                    Pool(pools, .15f, (Item(items,"item.runic.topaz"),1,1));
                    Pool(pools, .15f, (Item(items,"item.runic.amber"),1,1));
                }

            }

            var bag = Bag("ID_Forest_Resources", "item.lootbag.forest.resources",
                "Leśna torba zasobów", "Worek z materiałami zebranymi w lesie.", so => {
                    Bracket(so, LootLevelBracket.Level1To10, pools => ConfigureBracket(pools, false));
                    Bracket(so, LootLevelBracket.Level11To20, pools => ConfigureBracket(pools, true));
                });
            var calibrated = new SerializedObject(bag);
            CraftingMaterialDropSetup.Configure(calibrated, true);
            calibrated.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bag);
            return bag;
        }

        private static LootBagItemDefinition ConfigureEquipment(
            string file, string id, string name, ItemType type,
            IReadOnlyList<EquipmentItemDefinition> definitions)
        {
            return Bag(file, id, name,
                type == ItemType.Weapon ? "Worek z orężem znalezionym w lesie."
                    : "Worek z pancerzami znalezionymi w lesie.", so => {
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
            return Bag("ID_Forest_Galdurytes", "item.lootbag.forest.galdurytes",
                "Torba z galdurytami", "Worek z runicznymi kamieniami znalezionymi w lesie.", so => {
                    Bracket(so, LootLevelBracket.Level11To20, pools => {
                        foreach (var chance in new[] { 1f, 1f, 1f, .8f, .55f, .4f, .25f })
                        {
                            Pool(pools, chance, (generic, 1, 1));
                            var pool = pools.GetArrayElementAtIndex(pools.arraySize - 1);
                            Sub(pool.FindPropertyRelative(F("Entries")).GetArrayElementAtIndex(0), "FixedGalduryteLevel", 20);
                        }
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
            var catalog = GetOrCreate<DungeonLootTableCatalog>(LootRoot + "DLC_Forest_Loot.asset");
            var so = new SerializedObject(catalog);
            var list = so.FindProperty(F("Entries"));
            var bosses = catalog.Entries.Where(e => e != null && e.Theme == DungeonTheme.Forest)
                .ToDictionary(e => e.Bracket, e => e.BossLootTable);
            list.ClearArray();
            foreach (var bracket in new[] { LootLevelBracket.Level1To10, LootLevelBracket.Level11To20 })
            {
                var entry = Add(list);
                Sub(entry, "Theme", DungeonTheme.Forest);
                Sub(entry, "Bracket", bracket);
                Sub(entry, "EnemyLootTable", bracket == LootLevelBracket.Level1To10 ? enemyLow : enemyHigh);
                Sub(entry, "ChestLootTable", bracket == LootLevelBracket.Level1To10 ? chestLow : chestHigh);
                Sub(entry, "BossLootTable", bosses.TryGetValue(bracket, out var boss) ? boss : null);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void AddCatalogItem(SerializedProperty list, ItemDefinition item)
        {
            for (var i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
            Add(list).objectReferenceValue = item;
        }

        private static void ConfigureMaterial(string file, string id, string name, int price, RarityTier rarity, MaterialDiscipline discipline)
        {
            var item = GetOrCreate<ItemDefinition>(LootRoot + file + ".asset");
            var so = new SerializedObject(item);
            Field(so, "Id", id); Field(so, "DisplayName", name); Field(so, "Description", "");
            Field(so, "ItemType", ItemType.Material); Field(so, "MaterialDiscipline", discipline);
            Field(so, "BaseRarity", rarity); Field(so, "BaseValue", price); Field(so, "IsStackable", true);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(item);
        }

        public static void WireIfAuthored(DungeonDefinition dungeon)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DungeonLootTableCatalog>(LootRoot + "DLC_Forest_Loot.asset");
            if (catalog == null || dungeon == null) return;
            var so = new SerializedObject(dungeon); Field(so, "LootTableCatalog", catalog);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(dungeon);
        }

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Loot")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Forest loot: exit Play Mode first");
            if (!AssetDatabase.IsValidFolder(LootRoot.TrimEnd('/')))
                AssetDatabase.CreateFolder(Root + "Dungeons/Forest", "Loot");
            ConfigureMaterial("ID_RedEnergyDust", "item.smithing.red_energy_dust", "Czerwony pył energetyczny", 13, RarityTier.Common, MaterialDiscipline.Smithing);
            ConfigureMaterial("ID_YellowEnergyDust", "item.smithing.yellow_energy_dust", "Żółty pył energetyczny", 74, RarityTier.Uncommon, MaterialDiscipline.Smithing);
            var items = IndexItems();
            var resources = ConfigureResources(items);
            var weapons = ConfigureEquipment("ID_Forest_Weapons",
                "item.lootbag.forest.weapons", "Torba z bronią", ItemType.Weapon, Equipment(ItemType.Weapon));
            var armor = ConfigureEquipment("ID_Forest_Armor",
                "item.lootbag.forest.armor", "Torba z pancerzami", ItemType.Armor, Equipment(ItemType.Armor));
            var galduryte = Item(items, "item.galduryte.generic");
            var galdurytes = ConfigureGaldurytes(galduryte);

            var enemyLow = Table("LT_Forest_Enemy_1_10_Production",
                (resources,.5f,1,1));
            var enemyHigh = Table("LT_Forest_Enemy_11_20_Production",
                (resources,.5f,1,1), (galdurytes,.01f,1,1));
            var chestLow = Table("LT_Forest_Chest_1_10_Production",
                (resources,1f,2,3), (weapons,.04f,1,1), (armor,.04f,1,1));
            var chestHigh = Table("LT_Forest_Chest_11_20_Production",
                (resources,1f,2,3), (weapons,.04f,1,1),
                (armor,.04f,1,1), (galdurytes,.02f,1,1));
            DungeonFoodSetup.ConfigureTables(enemyLow, enemyHigh, chestLow, chestHigh);
            ConfigureCatalog(enemyLow, enemyHigh, chestLow, chestHigh);
            BranchEquipmentRewardsSetup.ConfigureBudget(chestLow);
            BranchEquipmentRewardsSetup.ConfigureBudget(chestHigh);
            var dungeon = Require<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            WireIfAuthored(dungeon);

            var content = Require<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
            var catalog = new SerializedObject(content);
            var list = catalog.FindProperty(F("ItemDefinitions"));
            foreach (var bag in new ItemDefinition[] { resources, weapons, armor, galdurytes,
                Item(items,"item.smithing.red_energy_dust"), Item(items,"item.smithing.yellow_energy_dust") })
                AddCatalogItem(list, bag);
            catalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
        }
    }
}
