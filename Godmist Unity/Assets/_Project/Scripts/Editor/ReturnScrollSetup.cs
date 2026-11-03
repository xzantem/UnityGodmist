using System;
using Godmist.Data.Items;
using Godmist.Data.NPC;
using UnityEditor;

namespace Godmist.Editor
{
    public static class ReturnScrollSetup
    {
        public const string Root = "Assets/_Project/ScriptableObjects/";
        public const string ScrollPath = Root + "Items/Consumables/CID_ReturnScroll.asset";
        public const string MerchantPath = Root + "NPC/Roles/TRD_Arungard_Enchanter.asset";
        public const string GroupId = "return-scroll";
        public const int Price = 200;
        public const float StockChance = .2f;
        public const float ChestChance = .04f;
        public static readonly string[] ChestPaths = {
            Root + "Catacombs/Loot/LT_Catacombs_Chest_1_10_Production.asset",
            Root + "Catacombs/Loot/LT_Catacombs_Chest_11_20_Production.asset",
            Root + "Dungeons/Forest/Loot/LT_Forest_Chest_1_10_Production.asset",
            Root + "Dungeons/Forest/Loot/LT_Forest_Chest_11_20_Production.asset"
        };
        static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing return-scroll content: " + path);
        static string F(string field) => "<" + field + ">k__BackingField";

        [MenuItem("Godmist/Content/Configure Return Scroll Supply")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            ConfigureScroll(Require<ConsumableItemDefinition>(ScrollPath));
            ConfigureMerchant(Require<TraderRoleData>(MerchantPath));
            foreach (var path in ChestPaths) ConfigureChestDrop(Require<LootTableData>(path));
        }

        public static void ConfigureScroll(ConsumableItemDefinition scroll)
        {
            var data = new SerializedObject(scroll);
            data.FindProperty(F("BaseValue")).intValue = Price;
            data.FindProperty(F("IsStackable")).boolValue = true;
            data.FindProperty(F("Description")).stringValue = "Wraca z drużyną do Arungardu poza walką. Zachowuje wyprawę do wznowienia.";
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(scroll);
        }

        public static void ConfigureMerchant(TraderRoleData role)
        {
            var data = new SerializedObject(role);
            var groups = data.FindProperty(F("RotatingStock"));
            SerializedProperty group = null;
            for (var i = 0; i < groups.arraySize; i++)
            {
                var candidate = groups.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("Id").stringValue != GroupId) continue;
                if (group != null) throw new InvalidOperationException("Duplicate return-scroll stock group.");
                group = candidate;
            }
            group ??= groups.GetArrayElementAtIndex(groups.arraySize++);
            group.FindPropertyRelative("Id").stringValue = GroupId;
            group.FindPropertyRelative("Slots").intValue = 1;
            group.FindPropertyRelative("AvailabilityChance").floatValue = StockChance;
            group.FindPropertyRelative("GuaranteedInitially").boolValue = true;
            var candidates = group.FindPropertyRelative("Candidates");
            candidates.arraySize = 1;
            var entry = candidates.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("Item").objectReferenceValue = Require<ConsumableItemDefinition>(ScrollPath);
            entry.FindPropertyRelative("Limit").intValue = 1;
            entry.FindPropertyRelative("MinimumLevel").intValue = 1;
            entry.FindPropertyRelative("MaximumLevel").intValue = 50;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(role);
        }

        public static void ConfigureChestDrop(LootTableData table)
        {
            var scroll = Require<ConsumableItemDefinition>(ScrollPath);
            var data = new SerializedObject(table);
            var entries = data.FindProperty(F("Entries"));
            SerializedProperty entry = null;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var candidate = entries.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative(F("ItemDefinition")).objectReferenceValue != scroll) continue;
                if (entry != null) throw new InvalidOperationException("Duplicate return-scroll loot entry.");
                entry = candidate;
            }
            entry ??= entries.GetArrayElementAtIndex(entries.arraySize++);
            entry.FindPropertyRelative(F("ItemDefinition")).objectReferenceValue = scroll;
            entry.FindPropertyRelative(F("DropChance")).floatValue = ChestChance;
            var amount = entry.FindPropertyRelative(F("Amount"));
            amount.FindPropertyRelative("Min").intValue = 1;
            amount.FindPropertyRelative("Max").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(table);
        }
    }
}
