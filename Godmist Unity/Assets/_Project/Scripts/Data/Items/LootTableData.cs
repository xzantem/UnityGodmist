using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "LootTable",
        menuName = "Godmist/Items/Loot Table")]
    public class LootTableData : ScriptableObject
    {
        [field: SerializeField]
        public List<LootEntryData> Entries { get; private set; } = new();

        [field: SerializeField, Min(0f)]
        [field: Tooltip("Expected weapon/armor items across all branch reward chests in a new dungeon. Zero preserves ordinary table rolls.")]
        public float ExpectedEquipmentPerDungeon { get; private set; }
    }
}
