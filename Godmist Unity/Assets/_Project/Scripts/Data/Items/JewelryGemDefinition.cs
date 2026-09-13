using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Data.Items
{
    /// <summary>
    /// One globally authored gem. Its cost and bonuses are independent of the
    /// metal and jewelry slot into which it is crafted.
    /// </summary>
    [CreateAssetMenu(fileName = "JewelryGemDefinition", menuName = "Godmist/Items/Jewelry Gem Definition")]
    public sealed class JewelryGemDefinition : ScriptableObject
    {
        [field: SerializeField] public ItemDefinition CraftingMaterial { get; private set; }
        [field: SerializeField, Min(1)] public int Amount { get; private set; } = 1;
        [field: SerializeField] public List<JewelryModifierData> Modifiers { get; private set; } = new();
    }
}
