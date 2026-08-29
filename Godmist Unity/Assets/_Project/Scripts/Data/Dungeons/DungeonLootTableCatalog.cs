using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [CreateAssetMenu(
        fileName = "DungeonLootTableCatalog",
        menuName = "Godmist/Dungeons/Dungeon Loot Table Catalog")]
    public class DungeonLootTableCatalog : ScriptableObject
    {
        [field: SerializeField]
        public List<DungeonLootTableEntryData> Entries { get; private set; } = new();

        public bool TryGetTables(
            DungeonTheme theme,
            int dungeonLevel,
            out LootTableData enemyLootTable,
            out LootTableData chestLootTable)
        {
            enemyLootTable = null;
            chestLootTable = null;

            var bracket = LootLevelBracketResolver.Resolve(dungeonLevel);

            if (Entries == null) return false;

            foreach (var entry in Entries)
            {
                if (entry == null
                    || entry.Theme != theme
                    || entry.Bracket != bracket)
                {
                    continue;
                }

                enemyLootTable = entry.EnemyLootTable;
                chestLootTable = entry.ChestLootTable;
                return enemyLootTable != null || chestLootTable != null;
            }

            return false;
        }

        public bool TryGetEnemyLootTable(
            DungeonTheme theme,
            int dungeonLevel,
            out LootTableData lootTable)
        {
            TryGetTables(
                theme,
                dungeonLevel,
                out lootTable,
                out _);
            return lootTable != null;
        }

        public bool TryGetChestLootTable(
            DungeonTheme theme,
            int dungeonLevel,
            out LootTableData lootTable)
        {
            TryGetTables(theme, dungeonLevel, out _, out lootTable);
            return lootTable != null;
        }

        public bool TryGetBossLootTable(
            DungeonTheme theme,
            int dungeonLevel,
            out LootTableData lootTable)
        {
            lootTable = null;

            var bracket = LootLevelBracketResolver.Resolve(dungeonLevel);

            if (Entries == null) return false;

            foreach (var entry in Entries)
            {
                if (entry == null
                    || entry.Theme != theme
                    || entry.Bracket != bracket)
                {
                    continue;
                }

                lootTable = entry.BossLootTable;
                return lootTable != null;
            }

            return false;
        }
    }
}
