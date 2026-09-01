using System;
using UnityEngine;

namespace Godmist.Data.Items
{
    [Serializable]
    public class CraftingMaterialRequirement
    {
        [field: SerializeField] public ItemDefinition Material { get; private set; }
        [field: SerializeField, Min(1)] public int Amount { get; private set; } = 1;
    }
}
