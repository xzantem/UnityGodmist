using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public sealed class GalduryteStoneData
    {
        [field: SerializeField] public ItemDefinition Material { get; private set; }
        [field: SerializeField] public GalduryteTier Tier { get; private set; }

        public GalduryteStoneData() {}

        public GalduryteStoneData(ItemDefinition material, GalduryteTier tier)
        {
            Material = material;
            Tier = tier;
        }
    }

    [Serializable]
    public sealed class GaldurytePowderData
    {
        [field: SerializeField] public ItemDefinition Material { get; private set; }
        [field: SerializeField] public GaldurytePowderColor Color { get; private set; }

        public GaldurytePowderData() {}

        public GaldurytePowderData(ItemDefinition material, GaldurytePowderColor color)
        {
            Material = material;
            Color = color;
        }
    }
}
