using Godmist.Core.Enums;
using Godmist.Data.Characters;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "EquipmentItemDefinition",
        menuName = "Godmist/Items/Equipment Item Definition")]
    public class EquipmentItemDefinition : ItemDefinition
    {
        [field: SerializeField] public CharacterClassData RequiredClass { get; private set; }
        [field: SerializeField] public EquipmentSlot JewelrySlot { get; private set; }

        public EquipmentSlot EquipmentSlot => ItemType switch
        {
            ItemType.Weapon => EquipmentSlot.Weapon,
            ItemType.Armor => EquipmentSlot.Armor,
            ItemType.Jewelry when JewelrySlot is EquipmentSlot.Necklace
                or EquipmentSlot.Ring or EquipmentSlot.Bracelet or EquipmentSlot.Earrings
                => JewelrySlot,
            _ => throw new System.InvalidOperationException(
                "EquipmentItemDefinition wymaga poprawnego typu i slotu wyposazenia.")
        };
    }
}
