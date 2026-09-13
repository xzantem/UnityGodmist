using System;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Items
{
    /// <summary>
    /// Static effect payload used by alchemy recipes. Runtime consumable state
    /// is still represented by <see cref="ConsumableEffectRuntime"/>.
    /// </summary>
    [Serializable]
    public sealed class ConsumableEffectDefinition
    {
        [field: SerializeField] public ItemDefinition SourceIngredient { get; private set; }
        [field: SerializeField] public ConsumableRuntimeEffectType EffectType { get; private set; }
        [field: SerializeField] public float Value { get; private set; }
        [field: SerializeField] public int DurationTurns { get; private set; }
        [field: SerializeField, Range(0f, 1f)] public float Chance { get; private set; } = 1f;
        [field: SerializeField] public int TargetModeIndex { get; private set; }
        [field: SerializeField] public string StatusId { get; private set; } = string.Empty;
        [field: SerializeField] public ResistanceType ResistanceType { get; private set; } = ResistanceType.Weakening;
        [field: SerializeField] public TargetType TargetType { get; private set; } = TargetType.Caster;
        [field: SerializeField] public TargetingType TargetingType { get; private set; } = TargetingType.Allies;

    }
}
