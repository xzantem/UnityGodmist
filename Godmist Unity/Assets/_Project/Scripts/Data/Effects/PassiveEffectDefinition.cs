using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Interfaces;
using UnityEngine;

namespace Godmist.Data.Effects
{
    [Serializable]
    public sealed class PassiveEffectDefinition : IIdentifiable
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public string DisplayName { get; private set; } = string.Empty;
        [field: SerializeField, TextArea(2, 5)] public string Description { get; private set; } = string.Empty;
        [field: SerializeField] public PassiveEffectLifetime Lifetime { get; private set; } =
            PassiveEffectLifetime.Persistent;
        [field: SerializeField, Min(0)] public int DefaultDurationTurns { get; private set; }
        [field: SerializeField] public PassiveEffectTickPhase TickPhase { get; private set; } =
            PassiveEffectTickPhase.None;
        [field: SerializeField] public PassiveEffectStackingRule StackingRule { get; private set; } =
            PassiveEffectStackingRule.Unique;
        [field: SerializeField] public PassiveEffectConditionCombination ConditionCombination { get; private set; } =
            PassiveEffectConditionCombination.All;
        [field: SerializeField] public List<PassiveEffectConditionDefinition> Conditions { get; private set; } = new();
        [field: SerializeField] public List<PassiveEffectTriggerDefinition> Triggers { get; private set; } = new();
        [field: SerializeField] public List<PassiveEffectOperationDefinition> Operations { get; private set; } = new();
        [field: SerializeField] public PassiveEffectTooltipDefinition Tooltip { get; private set; } = new();

        public bool IsAlwaysActive => Conditions == null || Conditions.Count == 0;
        public bool IsTriggered => Triggers != null && Triggers.Count > 0;
    }

    [Serializable]
    public sealed class PassiveEffectConditionDefinition
    {
        [field: SerializeField] public PassiveEffectConditionType Type { get; private set; } =
            PassiveEffectConditionType.Always;
        [field: SerializeField] public PassiveEffectComparisonType Comparison { get; private set; } =
            PassiveEffectComparisonType.Equal;
        [field: SerializeField] public string ReferenceId { get; private set; } = string.Empty;
        [field: SerializeField] public PassiveEffectValueDefinition Value { get; private set; } = new();
        [field: SerializeField] public bool Negated { get; private set; }
        [field: SerializeField] public List<PassiveEffectParameterDefinition> Parameters { get; private set; } = new();
    }

    [Serializable]
    public sealed class PassiveEffectTriggerDefinition
    {
        [field: SerializeField] public PassiveEffectTriggerType Type { get; private set; } =
            PassiveEffectTriggerType.AlwaysActive;
        [field: SerializeField] public string EventId { get; private set; } = string.Empty;
        [field: SerializeField] public List<PassiveEffectParameterDefinition> Parameters { get; private set; } = new();
    }

    [Serializable]
    public sealed class PassiveEffectOperationDefinition
    {
        [field: SerializeField] public string Id { get; private set; } = string.Empty;
        [field: SerializeField] public PassiveEffectOperationType Type { get; private set; } =
            PassiveEffectOperationType.ModifyStat;
        [field: SerializeField] public PassiveEffectTargetType Target { get; private set; } =
            PassiveEffectTargetType.Self;
        [field: SerializeField] public string ReferenceId { get; private set; } = string.Empty;
        [field: SerializeField] public CharacterStatType StatType { get; private set; } =
            CharacterStatType.MaxHealth;
        [field: SerializeField] public GameplayVariableType VariableType { get; private set; } =
            GameplayVariableType.DamageDealtPercent;
        [field: SerializeField] public ResistanceType ResistanceType { get; private set; }
        [field: SerializeField] public StatModifierOperation ModifierOperation { get; private set; } =
            StatModifierOperation.Percent;
        [field: SerializeField] public StatModifierStacking ModifierStacking { get; private set; } =
            StatModifierStacking.Additive;
        [field: SerializeField] public PassiveEffectValueDefinition Value { get; private set; } = new();
        [field: SerializeField, Min(0)] public int DurationTurns { get; private set; }
        [field: SerializeField] public List<PassiveEffectParameterDefinition> Parameters { get; private set; } = new();
    }

    [Serializable]
    public sealed class PassiveEffectValueDefinition
    {
        [field: SerializeField] public PassiveEffectValueSourceType Source { get; private set; } =
            PassiveEffectValueSourceType.Constant;
        [field: SerializeField] public float NumberValue { get; private set; }
        [field: SerializeField] public string ReferenceId { get; private set; } = string.Empty;
        [field: SerializeField] public string StateKey { get; private set; } = string.Empty;
        [field: SerializeField] public float Scale { get; private set; } = 1f;
        [field: SerializeField] public float Offset { get; private set; }
    }

    [Serializable]
    public sealed class PassiveEffectParameterDefinition
    {
        [field: SerializeField] public string Key { get; private set; } = string.Empty;
        [field: SerializeField] public string StringValue { get; private set; } = string.Empty;
        [field: SerializeField] public float NumberValue { get; private set; }
        [field: SerializeField] public int IntegerValue { get; private set; }
        [field: SerializeField] public bool BooleanValue { get; private set; }
    }

    [Serializable]
    public sealed class PassiveEffectTooltipDefinition
    {
        [field: SerializeField] public string TitleOverride { get; private set; } = string.Empty;
        [field: SerializeField, TextArea(2, 5)] public string DescriptionOverride { get; private set; } = string.Empty;
        [field: SerializeField, TextArea(1, 3)] public string ConditionText { get; private set; } = string.Empty;
        [field: SerializeField, TextArea(1, 3)] public string TriggerText { get; private set; } = string.Empty;
        [field: SerializeField] public bool ShowRuntimeState { get; private set; } = true;
    }
}
