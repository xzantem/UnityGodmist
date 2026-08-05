using System;
using Godmist.Core.Types;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public class LootEntryData
    {
        [field: SerializeField] public ItemDefinition ItemDefinition { get; private set; }

        [field: SerializeField, Range(0f, 1f)]
        public float DropChance { get; private set; } = 1f;

        [field: SerializeField]
        public IntRange Amount { get; private set; } = new(1, 1);
    }
}