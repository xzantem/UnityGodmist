using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Data.Items;
using UnityEditor;
using UnityEngine;

namespace Godmist.Editor
{
    public static class ForestContainersSetup
    {
        public const string Root = "Assets/_Project/ScriptableObjects/Dungeons/Forest/";
        public const string ChestPath = Root + "DO_Forest_Chest.asset";
        public const string CachePath = Root + "DO_Forest_Cache.asset";
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        [MenuItem("Godmist/Content/Pass 1b/Configure Forest Containers")]
        public static void Configure()
        {
            var dungeon = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(ForestPrototypeSetup.DefinitionPath);
            if (dungeon == null || dungeon.LootTableCatalog == null)
                throw new InvalidOperationException("Author Forest loot first.");
            var table = AssetDatabase.LoadAssetAtPath<LootTableData>(Root + "Loot/LT_Forest_Chest_1_10_Production.asset");
            if (table == null) throw new InvalidOperationException("Missing Forest chest table.");
            ConfigureContainer(ChestPath, "object.forest.chest", "Leśna skrzynia", table);
            ConfigureContainer(CachePath, "object.forest.cache", "Leśna skrytka", table);
            WireIfAuthored(dungeon);
            AssetDatabase.SaveAssets();
            ForestExpeditionsSetup.RefreshIfAuthored();
        }

        private static void ConfigureContainer(string path, string id, string label, LootTableData table)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ChestDungeonObjectDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ChestDungeonObjectDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            var data = new SerializedObject(definition);
            data.FindProperty("<Id>k__BackingField").stringValue = id;
            data.FindProperty("<DisplayName>k__BackingField").stringValue = label;
            data.FindProperty("<SpawnWeight>k__BackingField").intValue = 1;
            data.FindProperty("<LootTable>k__BackingField").objectReferenceValue = table;
            data.FindProperty("<LockType>k__BackingField").enumValueIndex = (int)ChestLockType.Unlocked;
            data.FindProperty("<RequiredKey>k__BackingField").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        public static void WireIfAuthored(DungeonDefinition dungeon)
        {
            var chest = AssetDatabase.LoadAssetAtPath<ChestDungeonObjectDefinition>(ChestPath);
            var cache = AssetDatabase.LoadAssetAtPath<ChestDungeonObjectDefinition>(CachePath);
            if (dungeon == null || chest == null || cache == null) return;
            foreach (var container in new[] { chest, cache })
                if (!dungeon.ObjectDefinitions.Contains(container)) dungeon.ObjectDefinitions.Add(container);
            foreach (var room in dungeon.RoomTemplateCatalog.Templates)
            {
                if (room.SemanticAnchors.RemoveAll(a => a.Id.StartsWith("forest.container.", StringComparison.Ordinal)) > 0)
                    EditorUtility.SetDirty(room);
                if (room.RoomType != DungeonRoomType.Optional) continue;
                var occupied = room.SemanticAnchors.SelectMany(a =>
                    Enumerable.Range(0, a.AreaSize.x).SelectMany(x => Enumerable.Range(0, a.AreaSize.y)
                        .Select(y => a.LocalCell + new Vector2Int(x, y)))).ToHashSet();
                var floor = room.Footprint.FloorCellOffsets.ToHashSet();
                var center = (Vector2)room.SizeInCells * .5f;
                // An accessible pocket beside the treeline, away from enemies and the central route.
                var cells = floor.Where(c => !occupied.Contains(c) && !room.DifficultTerrainCells.Contains(c)
                    && Directions.All(d => floor.Contains(c + d))
                    && Directions.Any(d => floor.Contains(c + d) && !occupied.Contains(c + d))
                    && !occupied.Any(o => (o-c).sqrMagnitude <= 2))
                    .OrderBy(c => Directions.Min(d => DistanceToEdge(c, d, floor)))
                    .ThenByDescending(c => ((Vector2)c-center).sqrMagnitude)
                    .ThenBy(c => c.y).ThenBy(c => c.x).ToArray();
                if (cells.Length == 0) throw new InvalidOperationException("No accessible Forest container anchor: " + room.Id);
                room.SemanticAnchors.Add(new DungeonSemanticAnchorData("forest.container.cache",
                    DungeonSemanticAnchorType.Chest, cells[0], Vector2Int.one,
                    isRequired: true, objectDefinition: cache));
                EditorUtility.SetDirty(room);
            }
            EditorUtility.SetDirty(dungeon);
        }

        private static int DistanceToEdge(Vector2Int cell, Vector2Int direction, System.Collections.Generic.HashSet<Vector2Int> floor)
        {
            var distance = 0;
            while (floor.Contains(cell + direction * (distance + 1))) distance++;
            return distance;
        }
    }
}
