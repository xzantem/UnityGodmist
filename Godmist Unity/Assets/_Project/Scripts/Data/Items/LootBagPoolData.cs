using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public class LootBagPoolData
    {
        [field: SerializeField, Range(0f, 1f)]
        public float DropChance { get; private set; } = 1f;

        [field: SerializeField]
        public List<LootBagPoolEntryData> Entries { get; private set; } = new();

        [field: SerializeField]
        public string DistinctSelectionGroup { get; private set; } = string.Empty;
    }
}
