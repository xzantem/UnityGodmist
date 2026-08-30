using System;
using Godmist.Core.Types;
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
    }
}
