using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using Godmist.Systems.Dungeons;
using UnityEditor;

namespace Godmist.Editor
{
    public static class BranchEquipmentRewardsSetup
    {
        private const string Root = "Assets/_Project/ScriptableObjects/Dungeons";

        [MenuItem("Godmist/Content/Configure branch equipment rewards")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring rewards.");
            var tables = new HashSet<LootTableData>();
            var rooms = new HashSet<DungeonRoomTemplateDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:DungeonDefinition", new[] { Root }))
            {
                var dungeon = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (dungeon == null || !dungeon.IsAvailableAsOffer || dungeon.IsStoryDungeon
                    || dungeon.Theme is not (DungeonTheme.Catacombs or DungeonTheme.Forest)) continue;
                foreach (var entry in dungeon.LootTableCatalog.Entries)
                {
                    if (entry.EnemyLootTable != null && tables.Add(entry.EnemyLootTable))
                    {
                        var state = new SerializedObject(entry.EnemyLootTable);
                        var entries = state.FindProperty("<Entries>k__BackingField");
                        for (var i = entries.arraySize - 1; i >= 0; i--)
                        {
                            var item = entries.GetArrayElementAtIndex(i).FindPropertyRelative("<ItemDefinition>k__BackingField")
                                .objectReferenceValue as ItemDefinition;
                            if (item is LootBagItemDefinition && (item.Id.EndsWith(".weapons", StringComparison.Ordinal)
                                || item.Id.EndsWith(".armor", StringComparison.Ordinal))) entries.DeleteArrayElementAtIndex(i);
                        }
                        if (state.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(entry.EnemyLootTable);
                    }
                    if (entry.ChestLootTable != null && tables.Add(entry.ChestLootTable)) ConfigureBudget(entry.ChestLootTable);
                }
                foreach (var room in dungeon.RoomTemplateCatalog.GenerationTemplates) rooms.Add(room);
            }
            ConfigureRooms(rooms);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureBudget(LootTableData table)
        {
            var state = new SerializedObject(table);
            state.FindProperty("<ExpectedEquipmentPerDungeon>k__BackingField").floatValue = 5f;
            if (state.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(table);
        }

        public static void ConfigureRooms(IEnumerable<DungeonRoomTemplateDefinition> rooms)
        {
            foreach (var room in rooms.Where(r => r != null).Distinct())
            {
                var chests = room.SemanticAnchors.Where(a => a.Type == DungeonSemanticAnchorType.Chest).ToArray();
                if (room.RoomType != DungeonRoomType.Optional)
                {
                    if (chests.Length == 0) continue;
                    room.SemanticAnchors.RemoveAll(a => a.Type == DungeonSemanticAnchorType.Chest);
                }
                else
                {
                    if (chests.Length != 1) throw new InvalidOperationException(room.Id + ": expected one branch reward chest.");
                    var a = chests[0];
                    if (a.IsRequired && a.SpawnChance == 1f) continue;
                    var index = room.SemanticAnchors.IndexOf(a);
                    room.SemanticAnchors[index] = new DungeonSemanticAnchorData(a.Id, a.Type, a.LocalCell, a.AreaSize,
                        isRequired: true, requiresAnchorId: a.RequiresAnchorId, objectDefinition: a.ObjectDefinition);
                }
                DungeonRoomContentPlanner.ValidateTemplate(room);
                EditorUtility.SetDirty(room);
            }
        }
    }
}
