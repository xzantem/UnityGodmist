using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;

namespace Godmist.Runtime.Effects
{
    [Serializable]
    public abstract class PassiveEffectPayloadRuntime
    {
        public abstract PassiveEffectPayloadType Type { get; }
    }

    [Serializable]
    public sealed class DamageOverTimePayload : PassiveEffectPayloadRuntime
    {
        public int RemainingTotalDamage;
        // Preserve the value calculated when the status was applied. The total
        // damage is still authoritative for ticking, but this keeps UI previews
        // exact after an OnHit proc instead of having to infer it from duration.
        public int DamagePerTurn;
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.DamageOverTime;
    }

    [Serializable]
    public sealed class SleepHealingPayload : PassiveEffectPayloadRuntime
    {
        public float MaxHealthPercent;
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.SleepHealing;
    }

    [Serializable]
    public sealed class UniqueCombatBuffPayload : PassiveEffectPayloadRuntime
    {
        public UniqueBuffType BuffType;
        public int ActionPointModifier;
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.UniqueCombatBuff;
    }

    [Serializable]
    public sealed class ResourceRegenerationBlockPayload : PassiveEffectPayloadRuntime
    {
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.ResourceRegenerationBlock;
    }

    [Serializable]
    public sealed class DamageRedirectPayload : PassiveEffectPayloadRuntime
    {
        public float RedirectPercent;
        public string ProtectorCharacterId = string.Empty;
        public string ProtectorDisplayName = string.Empty;
        public string ProtectedCharacterId = string.Empty;
        public string ProtectedCharacterDisplayName = string.Empty;
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.DamageRedirect;
    }

    [Serializable]
    public sealed class OnHitStatusPayload : PassiveEffectPayloadRuntime
    {
        public string StatusId = string.Empty;
        public float Chance;
        public int DurationTurns;
        public float StatusDamageMultiplier;
        public int StatusDamagePerTurn;
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.OnHitStatus;
    }

    [Serializable]
    public sealed class CounterattackPayload : PassiveEffectPayloadRuntime
    {
        public SkillData Skill;
        public SkillRangeData Range;
        public List<SkillEffectData> Effects = new();
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.Counterattack;
    }

    [Serializable]
    public sealed class StatModifiersPayload : PassiveEffectPayloadRuntime
    {
        public List<StatModifier> Modifiers = new();
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.StatModifiers;
    }

    [Serializable]
    public sealed class VariableModifiersPayload : PassiveEffectPayloadRuntime
    {
        public List<GameplayVariableModifier> Modifiers = new();
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.VariableModifiers;
    }

    [Serializable]
    public sealed class ResistanceModifierPayload : PassiveEffectPayloadRuntime
    {
        public ResistanceType ResistanceType;
        public float Value;
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.ResistanceModifier;
    }

    [Serializable]
    public sealed class PeriodicRecoveryPayload : PassiveEffectPayloadRuntime
    {
        public float HealthPercent;
        public float ResourcePercent;
        public float HealthRoundingCarry;
        public float ResourceRoundingCarry;
        public override PassiveEffectPayloadType Type => PassiveEffectPayloadType.PeriodicRecovery;
    }
}
