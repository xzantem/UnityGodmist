using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    /// <summary>
    /// Template for a dynamically generated potion or bomb. Its name, rarity,
    /// value and effects belong to the crafted instance, not the asset.
    /// </summary>
    [CreateAssetMenu(fileName = "CID_AlchemyConsumable", menuName = "Godmist/Items/Alchemy Consumable Template")]
    public sealed class AlchemyConsumableDefinition : ConsumableItemDefinition
    {
        private void OnValidate()
        {
            ItemType = UseType == ConsumableUseType.Bomb
                ? Godmist.Core.Enums.ItemType.Bomb
                : Godmist.Core.Enums.ItemType.Potion;
        }
    }
}
