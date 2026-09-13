using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(fileName = "ConsumableItemDefinition", menuName = "Godmist/Items/Consumable Item Definition")]
    public class ConsumableItemDefinition : ItemDefinition
    {
        [field: SerializeField] public ConsumableUseType UseType { get; private set; }
        [field: SerializeField, Min(1)] public int MaxCharges { get; private set; } = 1;
        public bool IsReusable => MaxCharges > 1;
        public bool IsCombatOnly => UseType == ConsumableUseType.Bomb;
    }
}
