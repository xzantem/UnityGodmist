using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "EquipmentItemDefinition",
        menuName = "Godmist/Items/Equipment Item Definition")]
    public class EquipmentItemDefinition : ItemDefinition
    {
        [field: SerializeField] public EquipmentSlot EquipmentSlot { get; private set; }
    }
}