using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using Godmist.Data.World;
using UnityEditor;

namespace Godmist.Editor
{
    public static class AlchemyScarcitySetup
    {
        const string Root = "Assets/_Project/ScriptableObjects/";
        public const string ForestBag = Root + "Dungeons/Forest/Loot/ID_Forest_Resources.asset";
        public const string CatacombsBag = Root + "Catacombs/Loot/ID_Catacombs_Resources.asset";
        public const string SkeletonTable = Root + "Catacombs/Loot/LT_Catacombs_Skeleton.asset";
        public const string UndeadTable = Root + "Catacombs/Loot/LT_Catacombs_Undead.asset";
        public const string AlchemistStock = Root + "NPC/Roles/TRD_Arungard_Alchemist.asset";
        public static readonly string[] ForestIngredients = { "item.vs10.szyszka", "item.material.elf_ear" };
        public static readonly string[] CatacombsIngredients = { "item.alchemy.common_reaper", "item.alchemy.puffball", "item.alchemy.skeleton_bone", "item.alchemy.undead_brain" };
        static string F(string name) => "<" + name + ">k__BackingField";
        [MenuItem("Godmist/Content/Apply scarce alchemy drops and merchant stock")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var path in new[] { ForestBag, CatacombsBag, SkeletonTable, UndeadTable, AlchemistStock })
            {
                var backup = Path.GetFullPath("../LocalHandoffs/alchemy-scarcity-20261024/before-assets/" + path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            ConfigureBag(ForestBag, ForestIngredients);
            ConfigureBag(CatacombsBag, CatacombsIngredients);
            ConfigureSpecies(SkeletonTable, "item.alchemy.skeleton_bone", .05f);
            ConfigureSpecies(UndeadTable, "item.alchemy.undead_brain", .1f);
            var stock = AssetDatabase.LoadAssetAtPath<TraderRoleData>(AlchemistStock);
            var data = new SerializedObject(stock); var groups = data.FindProperty(F("RotatingStock"));
            for (var i = 0; i < groups.arraySize; i++)
            {
                var group = groups.GetArrayElementAtIndex(i);
                if (group.FindPropertyRelative("Id").stringValue is not ("herbs" or "monster-parts")) continue;
                var candidates = group.FindPropertyRelative("Candidates");
                for (var j = 0; j < candidates.arraySize; j++) candidates.GetArrayElementAtIndex(j).FindPropertyRelative("Limit").intValue = 1;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(stock);
            AssetDatabase.SaveAssets();
        }
        static void ConfigureBag(string path, string[] ids)
        {
            var bag = AssetDatabase.LoadAssetAtPath<LootBagItemDefinition>(path);
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalog>(Root + "World/GC_MainContentCatalog.asset");
            var data = new SerializedObject(bag); var brackets = data.FindProperty(F("Brackets"));
            for (var b = 0; b < brackets.arraySize; b++)
            {
                var indexes = bag.Brackets[b].Pools.Select((pool, i) => (pool, i))
                    .Where(p => p.pool.Entries.Any(e => e.ItemDefinition != null && ids.Contains(e.ItemDefinition.Id)))
                    .Select(p => p.i).ToArray();
                if (indexes.Length == 0) throw new InvalidOperationException("No ingredient pools in " + path);
                foreach (var index in indexes)
                    if (bag.Brackets[b].Pools[index].Entries.Any(e => e.ItemDefinition == null || !ids.Contains(e.ItemDefinition.Id)))
                        throw new InvalidOperationException("Refusing to replace a mixed resource pool in " + path);
                var pools = brackets.GetArrayElementAtIndex(b).FindPropertyRelative(F("Pools"));
                foreach (var index in indexes.Reverse()) pools.DeleteArrayElementAtIndex(index);
                pools.InsertArrayElementAtIndex(indexes[0]); var replacement = pools.GetArrayElementAtIndex(indexes[0]);
                replacement.FindPropertyRelative(F("DropChance")).floatValue = .2f;
                replacement.FindPropertyRelative(F("DistinctSelectionGroup")).stringValue = "alchemy";
                var entries = replacement.FindPropertyRelative(F("Entries")); entries.ClearArray(); entries.arraySize = ids.Length;
                for (var i = 0; i < ids.Length; i++)
                {
                    if (!catalog.TryGetItem(ids[i], out var item)) throw new InvalidOperationException("Missing " + ids[i]);
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative(F("ItemDefinition")).objectReferenceValue = item;
                    var amount = entry.FindPropertyRelative(F("Amount"));
                    amount.FindPropertyRelative("Min").intValue = 1; amount.FindPropertyRelative("Max").intValue = 1;
                    entry.FindPropertyRelative(F("HasFixedEquipmentQuality")).boolValue = false;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(bag);
        }
        static void ConfigureSpecies(string path, string id, float chance)
        {
            var table = AssetDatabase.LoadAssetAtPath<LootTableData>(path); var data = new SerializedObject(table);
            var index = table.Entries.FindIndex(e => e.ItemDefinition?.Id == id);
            if (index < 0) throw new InvalidOperationException("Missing species drop " + id);
            var entry = data.FindProperty(F("Entries")).GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(F("DropChance")).floatValue = chance;
            var amount = entry.FindPropertyRelative(F("Amount"));
            amount.FindPropertyRelative("Min").intValue = 1; amount.FindPropertyRelative("Max").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(table);
        }
    }
}
