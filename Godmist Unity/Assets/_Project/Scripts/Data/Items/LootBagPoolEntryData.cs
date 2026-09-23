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

        [field: SerializeField]
        public bool HasFixedEquipmentQuality { get; private set; }

        [field: SerializeField]
        public EquipmentQuality EquipmentQuality { get; private set; } = EquipmentQuality.Normal;
    }
}
