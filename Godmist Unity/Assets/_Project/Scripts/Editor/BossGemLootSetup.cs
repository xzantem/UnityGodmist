using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using Godmist.Data.World;
using Godmist.Runtime.Items;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    /// <summary>Scoped authoring for the approved v0.1.1 boss gems. No save/runtime migration.</summary>
    public static class BossGemLootSetup
    {
        public const string JadeId = "item.runic.jadeite";
        public const string ObsidianId = "item.runic.obsidian";
        public const string Root = "Assets/_Project/ScriptableObjects/";
        public const string JadePath = Root + "Items/Materials/Runeforging/ID_Jadeite.asset";
        public const string JadeGemPath = Root + "Equipment/Jewelry/JGD_Jadeite.asset";
        public const string CatalogPath = Root + "World/GC_MainContentCatalog.asset";
        public const string IconRoot = "Assets/_Project/Art/Icons/Items/VS22/";
        public const string JadeIconPath = IconRoot + "gem-icons-jadeite-32-v1.png";
        public const string IconLibraryPath = "Assets/Resources/Godmist/ItemIcons/ItemIconLibrary.asset";
        public static readonly string[] ResourceBags = {
            Root + "Catacombs/Loot/ID_Catacombs_Resources.asset",
            Root + "Dungeons/Forest/Loot/ID_Forest_Resources.asset"
        };
        public static readonly string[] CatacombsBags = {
            Root + "Catacombs/Loot/ID_Catacombs_DeadKingBag.asset",
            Root + "Catacombs/Loot/ID_Catacombs_SkeletonSlayerBag.asset"
        };
        public static readonly string[] ForestBags = {
            Root + "Dungeons/Forest/Loot/ID_Forest_EntBag.asset",
            Root + "Dungeons/Forest/Loot/ID_Forest_BanditLeaderBag.asset"
        };

        static string F(string name) => "<" + name + ">k__BackingField";
        static T Require<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing boss gem dependency: " + path);
        static string Absolute(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        static void Save(SerializedObject data)
        {
            if (data.ApplyModifiedPropertiesWithoutUndo()) AssetDatabase.SaveAssetIfDirty(data.targetObject);
        }

        [MenuItem("Godmist/Content/v0.1.1/Apply approved boss gems")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying boss gem content.");
            // Validate shipped dependencies before changing any existing data.
            foreach (var path in ResourceBags.Concat(CatacombsBags).Concat(ForestBags)) Require<LootBagItemDefinition>(path);
            var catalog = Require<GameContentCatalog>(CatalogPath);
            var obsidian = Require<ItemDefinition>(Root + "Items/Materials/Runeforging/ID_Obsidian.asset");
            var sourceGem = Require<JewelryGemDefinition>(Root + "Equipment/Jewelry/JGD_Obsidian.asset");
            var library = Require<ItemIconLibrary>(IconLibraryPath);
            var jewelry = catalog.ItemDefinitions.OfType<JewelryItemDefinition>().ToArray();
            if (jewelry.Length != 4 || jewelry.Any(item => item.Gems.All(gem => gem != sourceGem)))
                throw new InvalidOperationException("Expected four production jewelry definitions containing Obsidian.");
            var icon = ImportSprite(JadeIconPath);
            var jade = AssetDatabase.LoadAssetAtPath<ItemDefinition>(JadePath);
            if (jade == null)
            {
                jade = ScriptableObject.CreateInstance<ItemDefinition>();
                EditorUtility.CopySerialized(obsidian, jade);
                jade.name = "ID_Jadeite";
                AssetDatabase.CreateAsset(jade, JadePath);
            }
            else if (jade.Id != JadeId) throw new InvalidOperationException("Unexpected asset at " + JadePath);
            var data = new SerializedObject(jade);
            data.FindProperty(F("Id")).stringValue = JadeId;
            data.FindProperty(F("DisplayName")).stringValue = "Jadeit";
            data.FindProperty(F("Description")).stringValue = "Kamień szlachetny pozyskiwany z bossów Lasu, używany do wyrobu biżuterii.";
            data.FindProperty(F("Icon")).objectReferenceValue = icon;
            data.FindProperty(F("BaseRarity")).intValue = (int)RarityTier.Rare;
            data.FindProperty(F("BaseValue")).intValue = 2847;
            Save(data);

            var gem = AssetDatabase.LoadAssetAtPath<JewelryGemDefinition>(JadeGemPath);
            if (gem == null)
            {
                gem = ScriptableObject.CreateInstance<JewelryGemDefinition>();
                EditorUtility.CopySerialized(sourceGem, gem);
                gem.name = "JGD_Jadeite";
                AssetDatabase.CreateAsset(gem, JadeGemPath);
            }
            data = new SerializedObject(gem);
            data.FindProperty(F("CraftingMaterial")).objectReferenceValue = jade;
            data.FindProperty(F("Amount")).intValue = 1;
            var modifiers = data.FindProperty(F("Modifiers"));
            if (modifiers.arraySize != 1) throw new InvalidOperationException("Expected the single approved biome modifier.");
            var modifier = modifiers.GetArrayElementAtIndex(0);
            modifier.FindPropertyRelative(F("GameplayVariable")).intValue = (int)GameplayVariableType.DamageDealtPercentInDungeonTheme;
            modifier.FindPropertyRelative(F("ReferenceId")).stringValue = "Forest";
            modifier.FindPropertyRelative(F("Value")).floatValue = .035f;
            Save(data);
            data = new SerializedObject(catalog);
            AddReference(data.FindProperty(F("ItemDefinitions")), jade);
            Save(data);
            foreach (var definition in jewelry)
            {
                data = new SerializedObject(definition);
                AddReference(data.FindProperty(F("Gems")), gem);
                Save(data);
            }
            ConfigureJewelryIcons(jewelry, jade, library);
            foreach (var path in ResourceBags)
            {
                data = new SerializedObject(Require<LootBagItemDefinition>(path));
                RemoveFromResources(data);
                Save(data);
            }
            foreach (var path in CatacombsBags.Concat(ForestBags))
            {
                data = new SerializedObject(Require<LootBagItemDefinition>(path));
                ConfigureBossBag(data, ForestBags.Contains(path));
                Save(data);
            }
        }

        // Shared by existing complete boss authoring tools, so regenerating a boss
        // cannot silently remove the focused v0.1.1 change.
        public static void ConfigureBossBag(SerializedObject data, bool forest)
        {
            var material = Require<ItemDefinition>(forest ? JadePath : Root + "Items/Materials/Runeforging/ID_Obsidian.asset");
            var brackets = data.FindProperty(F("Brackets"));
            for (var b = 0; b < brackets.arraySize; b++)
            {
                var bracket = brackets.GetArrayElementAtIndex(b);
                var level = (LootLevelBracket)bracket.FindPropertyRelative(F("Bracket")).intValue;
                if (level is not (LootLevelBracket.Level1To10 or LootLevelBracket.Level11To20)) continue;
                var pools = bracket.FindPropertyRelative(F("Pools"));
                SerializedProperty selected = null;
                for (var p = 0; p < pools.arraySize; p++)
                {
                    var entries = pools.GetArrayElementAtIndex(p).FindPropertyRelative(F("Entries"));
                    for (var e = 0; e < entries.arraySize; e++)
                    {
                        if (entries.GetArrayElementAtIndex(e).FindPropertyRelative(F("ItemDefinition")).objectReferenceValue != material) continue;
                        if (entries.arraySize != 1 || selected != null) throw new InvalidOperationException("Boss gem pool is mixed or duplicated: " + data.targetObject.name);
                        selected = pools.GetArrayElementAtIndex(p).Copy();
                    }
                }
                selected ??= pools.GetArrayElementAtIndex(pools.arraySize++);
                selected.FindPropertyRelative(F("DropChance")).floatValue = .5f;
                selected.FindPropertyRelative(F("DistinctSelectionGroup")).stringValue = string.Empty;
                var list = selected.FindPropertyRelative(F("Entries"));
                if (list.arraySize != 1) { list.ClearArray(); list.arraySize = 1; }
                var entry = list.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative(F("ItemDefinition")).objectReferenceValue = material;
                entry.FindPropertyRelative(F("Amount")).FindPropertyRelative("Min").intValue = 1;
                entry.FindPropertyRelative(F("Amount")).FindPropertyRelative("Max").intValue = 2;
                entry.FindPropertyRelative(F("SelectionWeight")).floatValue = 1;
                entry.FindPropertyRelative(F("HasFixedEquipmentQuality")).boolValue = false;
                entry.FindPropertyRelative(F("EquipmentQuality")).intValue = (int)EquipmentQuality.Normal;
                entry.FindPropertyRelative(F("MinimumGalduryteRarity")).intValue = (int)RarityTier.Common;
                entry.FindPropertyRelative(F("FixedGalduryteLevel")).intValue = 0;
            }
        }

        public static void RemoveFromResources(SerializedObject data)
        {
            var brackets = data.FindProperty(F("Brackets"));
            for (var b = 0; b < brackets.arraySize; b++)
            {
                var pools = brackets.GetArrayElementAtIndex(b).FindPropertyRelative(F("Pools"));
                for (var p = pools.arraySize - 1; p >= 0; p--)
                {
                    var pool = pools.GetArrayElementAtIndex(p);
                    var entries = pool.FindPropertyRelative(F("Entries"));
                    var total = 0d; var removed = 0d;
                    var indices = new List<int>();
                    for (var e = 0; e < entries.arraySize; e++)
                    {
                        var entry = entries.GetArrayElementAtIndex(e);
                        var weight = entry.FindPropertyRelative(F("SelectionWeight")).floatValue;
                        var effective = weight > 0 && !float.IsNaN(weight) && !float.IsInfinity(weight) ? weight : 1f;
                        total += effective;
                        var item = entry.FindPropertyRelative(F("ItemDefinition")).objectReferenceValue as ItemDefinition;
                        if (item?.Id is not (ObsidianId or JadeId)) continue;
                        removed += effective; indices.Add(e);
                    }
                    if (indices.Count == 0) continue;
                    if (!string.IsNullOrWhiteSpace(pool.FindPropertyRelative(F("DistinctSelectionGroup")).stringValue))
                        throw new InvalidOperationException("Gem unexpectedly belongs to a distinct-selection resource pool.");
                    if (indices.Count == entries.arraySize) { pools.DeleteArrayElementAtIndex(p); continue; }
                    // Removing an entry alone would buff every remaining gem.
                    // Shrink the activation chance by exactly the removed share.
                    pool.FindPropertyRelative(F("DropChance")).floatValue *= (float)((total - removed) / total);
                    for (var i = indices.Count - 1; i >= 0; i--) entries.DeleteArrayElementAtIndex(indices[i]);
                }
            }
        }

        static void AddReference(SerializedProperty array, UnityEngine.Object value)
        {
            for (var i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
            array.GetArrayElementAtIndex(array.arraySize++).objectReferenceValue = value;
        }

        static Sprite ImportSprite(string path)
        {
            if (!File.Exists(Absolute(path))) throw new InvalidOperationException("Missing jade art: " + path);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return Require<Sprite>(path);
        }

        static void ConfigureJewelryIcons(JewelryItemDefinition[] definitions, ItemDefinition jade, ItemIconLibrary library)
        {
            var data = new SerializedObject(library);
            var entries = data.FindProperty("entries");
            foreach (var definition in definitions)
            foreach (var metal in definition.Metals)
            {
                var emerald = definition.Gems.Single(gem => gem.CraftingMaterial?.Id == "item.vs10.szmaragd");
                var sourceKey = ItemIconPresentation.BuildJewelryKey(definition.JewelrySlot, metal.Material.Id, emerald.CraftingMaterial.Id);
                var source = library.Find(sourceKey) ?? throw new InvalidOperationException("Missing shipped emerald variant: " + sourceKey);
                var sourcePath = AssetDatabase.GetAssetPath(source);
                if (!sourcePath.EndsWith("-emerald.png", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected jewelry source: " + sourcePath);
                var targetPath = sourcePath.Replace("-emerald.png", "-jadeite.png");
                // Committed style-C art is generated offline from image_gen masters and registered socket masks.
                var sprite = ImportSprite(targetPath);
                var key = ItemIconPresentation.BuildJewelryKey(definition.JewelrySlot, metal.Material.Id, jade.Id);
                SerializedProperty row = null;
                for (var i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("Key").stringValue == key)
                        row = entries.GetArrayElementAtIndex(i);
                row ??= entries.GetArrayElementAtIndex(entries.arraySize++);
                row.FindPropertyRelative("Key").stringValue = key;
                row.FindPropertyRelative("Icon").objectReferenceValue = sprite;
            }
            Save(data);
        }

    }
}
