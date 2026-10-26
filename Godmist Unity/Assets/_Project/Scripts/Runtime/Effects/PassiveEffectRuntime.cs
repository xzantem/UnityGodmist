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
        // Stable reference to a static passive definition. EffectId remains for
        // compatibility with existing status/effect creators and save files.
        public string DefinitionId = string.Empty;
        // Human-readable source/effect label for inspect and combat feedback. Empty
        // values intentionally fall back to the effect id and glossary.
        public string DisplayName = string.Empty;
        public List<PassiveEffectSourceRuntime> Sources = new();
        public int Stacks = 1;
        public PassiveEffectStackingRule StackingRule =
            PassiveEffectStackingRule.Unique;

        public PassiveEffectLifetime Lifetime =
            PassiveEffectLifetime.Persistent;

        public int RemainingDuration;
        public PassiveEffectTickPhase TickPhase =
            PassiveEffectTickPhase.None;

        public List<PassiveEffectPayloadRuntime> Payloads = new();
        // Mutable counters/flags used by future triggered passives. Keeping them
        // outside typed payloads prevents every new passive family from requiring
        // a new runtime container and keeps persistence data-driven.
        public List<PassiveEffectRuntimeStateValue> StateValues = new();

        public T GetPayload<T>() where T : PassiveEffectPayloadRuntime
        {
            return Payloads?.Find(payload => payload is T) as T;
        }

        public void SetPayload<T>(T payload) where T : PassiveEffectPayloadRuntime
        {
            Payloads ??= new List<PassiveEffectPayloadRuntime>();
            Payloads.RemoveAll(existing => existing is T);
            if (payload != null) Payloads.Add(payload);
        }

        public PassiveEffectRuntimeStateValue GetStateValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return StateValues?.Find(value => value != null && value.Key == key);
        }

        public void SetStateValue(PassiveEffectRuntimeStateValue value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.Key)) return;
            StateValues ??= new List<PassiveEffectRuntimeStateValue>();
            StateValues.RemoveAll(existing => existing != null && existing.Key == value.Key);
            StateValues.Add(value);
        }

        public bool RemoveStateValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || StateValues == null) return false;
            return StateValues.RemoveAll(value => value != null && value.Key == key) > 0;
        }

        // Compatibility accessors keep existing effect creators concise while the
        // runtime state itself is held only by typed payload objects.
        public int TotalDamage
        {
            get => GetPayload<DamageOverTimePayload>()?.RemainingTotalDamage ?? 0;
            set => SetPayload(new DamageOverTimePayload { RemainingTotalDamage = value });
        }

        public float StatusHealPercent
        {
            get => GetPayload<SleepHealingPayload>()?.MaxHealthPercent ?? 0f;
            set => SetPayload(new SleepHealingPayload { MaxHealthPercent = value });
        }

        public bool IsUniqueCombatBuff
        {
            get => GetPayload<UniqueCombatBuffPayload>() != null;
            set
            {
                if (value && GetPayload<UniqueCombatBuffPayload>() == null)
                    SetPayload(new UniqueCombatBuffPayload());
                if (!value) Payloads?.RemoveAll(payload => payload is UniqueCombatBuffPayload);
            }
        }

        public UniqueBuffType UniqueBuffType
        {
            get => GetPayload<UniqueCombatBuffPayload>()?.BuffType ?? default;
            set
            {
                var payload = GetPayload<UniqueCombatBuffPayload>() ?? new UniqueCombatBuffPayload();
                payload.BuffType = value;
                SetPayload(payload);
            }
        }

        public float ActionPointModifier
        {
            get => GetPayload<UniqueCombatBuffPayload>()?.ActionPointModifier ?? 0;
            set
            {
                var payload = GetPayload<UniqueCombatBuffPayload>() ?? new UniqueCombatBuffPayload();
                payload.ActionPointModifier = value;
                SetPayload(payload);
            }
        }

        public bool BlocksResourceRegeneration
        {
            get => GetPayload<ResourceRegenerationBlockPayload>() != null;
            set
            {
                if (value) SetPayload(new ResourceRegenerationBlockPayload());
                else Payloads?.RemoveAll(payload => payload is ResourceRegenerationBlockPayload);
            }
        }

        public float DamageRedirectPercent
        {
            get => GetPayload<DamageRedirectPayload>()?.RedirectPercent ?? 0f;
            set => SetPayload(new DamageRedirectPayload { RedirectPercent = value });
        }

        public string OnHitStatusId
        {
            get => GetPayload<OnHitStatusPayload>()?.StatusId ?? string.Empty;
            set
            {
                var payload = GetPayload<OnHitStatusPayload>() ?? new OnHitStatusPayload();
                payload.StatusId = value ?? string.Empty;
                SetPayload(payload);
            }
        }

        public float OnHitStatusChance
        {
            get => GetPayload<OnHitStatusPayload>()?.Chance ?? 0f;
            set
            {
                var payload = GetPayload<OnHitStatusPayload>() ?? new OnHitStatusPayload();
                payload.Chance = value;
                SetPayload(payload);
            }
        }

        public int OnHitStatusDurationTurns
        {
            get => GetPayload<OnHitStatusPayload>()?.DurationTurns ?? 0;
            set
            {
                var payload = GetPayload<OnHitStatusPayload>() ?? new OnHitStatusPayload();
                payload.DurationTurns = value;
                SetPayload(payload);
            }
        }

        public SkillData CounterattackSkill
        {
            get => GetPayload<CounterattackPayload>()?.Skill;
            set
            {
                var payload = GetPayload<CounterattackPayload>() ?? new CounterattackPayload();
                payload.Skill = value;
                SetPayload(payload);
            }
        }

        public SkillRangeData CounterattackRange
        {
            get => GetPayload<CounterattackPayload>()?.Range;
            set
            {
                var payload = GetPayload<CounterattackPayload>() ?? new CounterattackPayload();
                payload.Range = value;
                SetPayload(payload);
            }
        }

        public List<SkillEffectData> CounterattackEffects
        {
            get => GetPayload<CounterattackPayload>()?.Effects ?? new List<SkillEffectData>();
            set
            {
                var payload = GetPayload<CounterattackPayload>() ?? new CounterattackPayload();
                payload.Effects = value ?? new List<SkillEffectData>();
                SetPayload(payload);
            }
        }

        public List<StatModifier> StatModifiers
        {
            get => GetPayload<StatModifiersPayload>()?.Modifiers ?? new List<StatModifier>();
            set => SetPayload(new StatModifiersPayload { Modifiers = value ?? new List<StatModifier>() });
        }

        public List<GameplayVariableModifier> VariableModifiers
        {
            get => GetPayload<VariableModifiersPayload>()?.Modifiers ?? new List<GameplayVariableModifier>();
            set => SetPayload(new VariableModifiersPayload { Modifiers = value ?? new List<GameplayVariableModifier>() });
        }

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

            if (totalDamage > 0)
            {
                SetPayload(new DamageOverTimePayload
                {
                    RemainingTotalDamage = totalDamage,
                    DamagePerTurn = totalDamage > 0 && remainingDuration > 0
                        ? (int)Math.Ceiling(totalDamage / (double)remainingDuration)
                        : 0
                });
            }

            if (statusHealPercent > 0f)
            {
                SetPayload(new SleepHealingPayload
                {
                    MaxHealthPercent = statusHealPercent
                });
            }
        }
    }

}
