using System;
using Godmist.Core.Types;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public class LootBagPoolEntryData
    {
        [field: SerializeField]
        public ItemDefinition ItemDefinition { get; private set; }

        [field: SerializeField]
        public IntRange Amount { get; private set; } = new(1, 1);

        // Relative choice weight inside this pool. Missing/zero legacy values
        // are treated as one by the sampler; this does not change drop chance.
        [field: SerializeField, Min(0f)]
        public float SelectionWeight { get; private set; } = 1f;

        [field: SerializeField]
        public bool HasFixedEquipmentQuality { get; private set; }

        [field: SerializeField]
        public EquipmentQuality EquipmentQuality { get; private set; } = EquipmentQuality.Normal;

        [field: SerializeField]
        public RarityTier MinimumGalduryteRarity { get; private set; } = RarityTier.Common;

        // Zero preserves existing loot definitions; a bag may explicitly promise a level.
        [field: SerializeField, Range(0, 20)]
        public int FixedGalduryteLevel { get; private set; }
    }
}
