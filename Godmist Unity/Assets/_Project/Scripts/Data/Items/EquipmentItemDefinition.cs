using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Characters;
using Godmist.Data.Effects;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "EquipmentItemDefinition",
        menuName = "Godmist/Items/Equipment Item Definition")]
    public class EquipmentItemDefinition : ItemDefinition
    {
        [field: SerializeField] public bool IsStarterEquipment { get; private set; }
        [field: SerializeField] public CharacterClassData RequiredClass { get; private set; }

        public virtual EquipmentSlot EquipmentSlot => ItemType switch
        {
            ItemType.Weapon => EquipmentSlot.Weapon,
            ItemType.Armor => EquipmentSlot.Armor,
            _ => throw new System.InvalidOperationException(
                "EquipmentItemDefinition wymaga poprawnego typu i slotu wyposazenia.")
        };
    }
}
