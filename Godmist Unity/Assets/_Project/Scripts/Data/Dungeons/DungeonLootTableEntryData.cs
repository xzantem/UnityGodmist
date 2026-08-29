using System;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    [Serializable]
    public class DungeonLootTableEntryData
    {
        [field: SerializeField]
        public DungeonTheme Theme { get; private set; }

        [field: SerializeField]
        public LootLevelBracket Bracket { get; private set; }

        [field: SerializeField]
        public LootTableData EnemyLootTable { get; private set; }

        [field: SerializeField]
        public LootTableData ChestLootTable { get; private set; }

        [field: SerializeField]
        public LootTableData BossLootTable { get; private set; }
    }
}
