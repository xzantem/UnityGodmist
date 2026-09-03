using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public sealed class BossUniqueDropData
    {
        [field: SerializeField, Range(0f, 1f)]
        public float DropChance { get; private set; } = .30f;

        [field: SerializeField]
        public List<BossUniqueEquipmentDefinition> EquipmentDefinitions { get; private set; } = new();
    }
}
