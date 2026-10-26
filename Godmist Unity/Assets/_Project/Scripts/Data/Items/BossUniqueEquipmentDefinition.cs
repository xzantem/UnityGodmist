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

        [field: SerializeField] public List<PassiveEffectDefinition> EffectsLevel20 { get; private set; } = new();
        [field: SerializeField] public List<PassiveEffectDefinition> EffectsLevel30 { get; private set; } = new();
        [field: SerializeField] public List<PassiveEffectDefinition> EffectsLevel40 { get; private set; } = new();
        [field: SerializeField] public List<PassiveEffectDefinition> EffectsLevel50 { get; private set; } = new();
        [field: SerializeField] public List<Godmist.Data.Characters.CharacterDefinition> SummonPool { get; private set; } = new();
        public List<PassiveEffectDefinition> GetEffects(int level)
        {
            if (level >= 50 && EffectsLevel50?.Count > 0) return EffectsLevel50;
            if (level >= 40 && EffectsLevel40?.Count > 0) return EffectsLevel40;
            if (level >= 30 && EffectsLevel30?.Count > 0) return EffectsLevel30;
            return level >= 20 && EffectsLevel20?.Count > 0 ? EffectsLevel20 : Effects;
        }

        public EquipmentItemDefinition EffectiveDefinition => BaseDefinition != null
            ? BaseDefinition
            : this;
    }
}
