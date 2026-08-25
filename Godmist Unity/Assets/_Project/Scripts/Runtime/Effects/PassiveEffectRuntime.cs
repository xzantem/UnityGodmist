using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;

namespace Godmist.Runtime.Effects
{
    [Serializable]
    public class PassiveEffectRuntime
    {
        public string EffectId = string.Empty;
        public List<PassiveEffectSourceRuntime> Sources = new();
        public int Stacks = 1;

        public PassiveEffectLifetime Lifetime =
            PassiveEffectLifetime.Persistent;

        public int RemainingDuration;
        public PassiveEffectTickPhase TickPhase =
            PassiveEffectTickPhase.None;

        public int TotalDamage;
        public float StatusHealPercent;
        public bool IsUniqueCombatBuff;
        public UniqueBuffType UniqueBuffType;
        public int ActionPointModifier;
        public bool BlocksResourceRegeneration;
        public float DamageRedirectPercent;
        public string OnHitStatusId = string.Empty;
        public float OnHitStatusChance;
        public int OnHitStatusDurationTurns;
        public SkillData CounterattackSkill;
        public SkillRangeData CounterattackRange;
        public List<SkillEffectData> CounterattackEffects = new();
        public List<StatModifier> StatModifiers = new();
        public List<GameplayVariableModifier> VariableModifiers = new();

        public PassiveEffectRuntime()
        {
        }

        public PassiveEffectRuntime(
            string effectId,
            List<PassiveEffectSourceRuntime> sources,
            int stacks,
            int remainingDuration,
            int totalDamage,
            PassiveEffectLifetime lifetime,
            PassiveEffectTickPhase tickPhase =
                PassiveEffectTickPhase.None,
                float statusHealPercent = 0f)
        {
            EffectId = effectId ?? string.Empty;
            Sources = sources != null
                ? new List<PassiveEffectSourceRuntime>(sources)
                : new List<PassiveEffectSourceRuntime>();

            Stacks = stacks;
            Lifetime = lifetime;
            RemainingDuration = lifetime == PassiveEffectLifetime.Timed
                ? remainingDuration
                : 0;

            TickPhase = lifetime == PassiveEffectLifetime.Timed
                ? tickPhase
                : PassiveEffectTickPhase.None;

            TotalDamage = totalDamage;
            StatusHealPercent = statusHealPercent;
        }
    }
}
