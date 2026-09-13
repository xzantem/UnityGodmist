using UnityEngine;
using System.Collections.Generic;
using Godmist.Data.Effects;

namespace Godmist.Data.Items
{
    [CreateAssetMenu(
        fileName = "UniqueEquipmentDefinition",
        menuName = "Godmist/Items/Unique Equipment Definition")]
    public sealed class UniqueEquipmentDefinition : EquipmentItemDefinition
    {
        [field: SerializeField] public EquipmentItemDefinition BaseDefinition { get; private set; }
        [field: SerializeField] public List<PassiveEffectDefinition> Effects { get; private set; } = new();

        public EquipmentItemDefinition EffectiveDefinition => BaseDefinition != null
            ? BaseDefinition
            : this;
    }
}
