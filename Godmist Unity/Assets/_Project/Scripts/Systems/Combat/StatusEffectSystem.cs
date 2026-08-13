using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class StatusEffectSystem
    {
        public const string StunId = StatusEffectIds.Stun;
        public const string ImmobilizeId = StatusEffectIds.Immobilize;
        public const float ChillMaximumSpeed = 20f;

        private readonly Action<string> battleLog;
        private readonly CombatDamageSystem damageSystem;
        private readonly PassiveEffectLifecycleSystem passiveEffectLifecycleSystem;

        public StatusEffectSystem(
            Action<string> battleLog = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            Action<CombatSession> evaluateOutcome = null,
            CombatDamageSystem damageSystem = null,
            PassiveEffectLifecycleSystem passiveEffectLifecycleSystem = null)
        {
            this.battleLog = battleLog;

            this.damageSystem = damageSystem ?? new CombatDamageSystem(
                battleLog,
                evaluateOutcome,
                removeUnitFromCombat);

            var recoverySystem = new CombatRecoverySystem(battleLog);

            this.passiveEffectLifecycleSystem = passiveEffectLifecycleSystem
                ?? new PassiveEffectLifecycleSystem(
                    battleLog,
                    new IPassiveEffectTurnProcessor[]
                    {
                        new DamageOverTimePassiveEffectProcessor(this.damageSystem),
                        new SleepPassiveEffectProcessor(recoverySystem)
                    });
        }

        public bool ApplyStatus(
            BattleUnit source,
            BattleUnit target,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex)
        {
            if (target?.Character == null || effect == null)
            {
                return false;
            }

            if (target.Character.LifeState != CharacterLifeState.Alive)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(effect.StatusId))
            {
                Debug.LogWarning("ApplyStatus nie ma zdefiniowanego statusId.");
                return false;
            }

            if (!PassesEffectChance(target, effect))
            {
                battleLog?.Invoke(
                    $"{target.Character.Definition.DisplayName} " +
                    $"odporna na efekt {effect.StatusId}.");

                return false;
            }

            var runtimeEffect = CreateRuntimeEffect(source, skill, effect, effectIndex);

            if (!passiveEffectLifecycleSystem.TryApplyTimedEffect(
                    target.Character,
                    runtimeEffect,
                    ResolveStackingMode(effect.StatusId),
                    out _))
            {
                return false;
            }

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} " +
                $"otrzymuje status {effect.StatusId}.");

            return true;
        }

        public bool ApplyGuaranteedStun(
            BattleUnit source,
            BattleUnit target,
            SkillData skill,
            int effectIndex,
            int durationTurns)
        {
            if (target?.Character == null
                || target.Character.LifeState != CharacterLifeState.Alive) return false;

            var sourceEffect = skill != null && effectIndex >= 0
                ? PassiveEffectSourceFactory.FromSkill(source, skill, effectIndex)
                : PassiveEffectSourceFactory.FromRuntimeSystem(source, StatusEffectIds.Stun);

            var runtimeEffect = new PassiveEffectRuntime(
                StatusEffectIds.Stun,
                new List<PassiveEffectSourceRuntime> { sourceEffect },
                1,
                Mathf.Max(1, durationTurns),
                0,
                PassiveEffectLifetime.Timed,
                ResolveTickPhase(StatusEffectIds.Stun));

            if (!passiveEffectLifecycleSystem.TryApplyTimedEffect(
                    target.Character,
                    runtimeEffect,
                    ResolveStackingMode(StatusEffectIds.Stun),
                    out _)) return false;

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} otrzymuje ogłuszenie.");

            return true;
        }

        public bool RemoveStatus(BattleUnit target, string statusId)
        {
            if (target?.Character == null)
            {
                return false;
            }

            var wasRemoved = passiveEffectLifecycleSystem.RemoveEffect(
                target.Character,
                statusId);

            if (wasRemoved)
            {
                battleLog?.Invoke(
                    $"{target.Character.Definition.DisplayName} " +
                    $"traci status {statusId}.");
            }

            return wasRemoved;
        }

        public static bool IsStunned(BattleUnit unit)
        {
            return HasStatus(unit, StunId);
        }

        public static bool IsImmobilized(BattleUnit unit)
        {
            return HasStatus(unit, ImmobilizeId);
        }

        public static bool IsFrozen(BattleUnit unit)
        {
            return HasStatus(unit, StatusEffectIds.Freeze);
        }

        public static bool IsSleeping(BattleUnit unit)
        {
            return HasStatus(unit, StatusEffectIds.Sleep);
        }

        public static bool IsParalyzed(BattleUnit unit)
        {
            return HasStatus(unit, StatusEffectIds.Paralysis);
        }
        public static bool IsTaunted(BattleUnit unit)
        {
            return HasStatus(unit, StatusEffectIds.Taunt);
        }

        public static bool IsInvisible(BattleUnit unit)
        {
            return HasStatus(unit, StatusEffectIds.Invisibility);
        }

        public static bool TryGetTaunter(
            CombatSession session,
            BattleUnit tauntedUnit,
            out BattleUnit taunter)
        {
            taunter = null;

            if (session?.Units == null || tauntedUnit?.Character?.PassiveEffects == null)
            {
                return false;
            }

            var tauntEffect = FindActiveStatus(
                tauntedUnit,
                StatusEffectIds.Taunt);

            if (tauntEffect == null)
            {
                return false;
            }

            for (var sourceIndex = tauntEffect.Sources.Count - 1; sourceIndex >= 0; sourceIndex--)
            {
                var sourceCharacterId =
                    tauntEffect.Sources[sourceIndex]?.SourceCharacterId;

                if (string.IsNullOrWhiteSpace(sourceCharacterId))
                {
                    continue;
                }

                foreach (var candidate in session.Units)
                {
                    if (candidate?.Character == null
                        || candidate.Character.LifeState == CharacterLifeState.Dead
                        || !string.Equals(
                            candidate.Character.CharacterId,
                            sourceCharacterId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    taunter = candidate;
                    return true;
                }
            }

            tauntedUnit.Character.PassiveEffects.Remove(tauntEffect);
            return false;
        }

        public static int RemoveTauntsAppliedBy(
            CombatSession session,
            string sourceCharacterId)
        {
            if (session?.Units == null || string.IsNullOrWhiteSpace(sourceCharacterId))
            {
                return 0;
            }

            var removedCount = 0;

            foreach (var unit in session.Units)
            {
                if (unit?.Character?.PassiveEffects == null)
                {
                    continue;
                }

                for (var effectIndex = unit.Character.PassiveEffects.Count - 1;
                    effectIndex >= 0;
                    effectIndex--)
                {
                    var effect = unit.Character.PassiveEffects[effectIndex];

                    if (!string.Equals(
                            effect?.EffectId,
                            StatusEffectIds.Taunt,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var wasAppliedBySource = false;

                    foreach (var source in effect.Sources)
                    {
                        if (string.Equals(
                                source?.SourceCharacterId,
                                sourceCharacterId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            wasAppliedBySource = true;
                            break;
                        }
                    }

                    if (!wasAppliedBySource)
                    {
                        continue;
                    }

                    unit.Character.PassiveEffects.RemoveAt(effectIndex);
                    removedCount++;
                }
            }

            return removedCount;
        }

        public static bool IsHardCrowdControlled(BattleUnit unit)
        {
            return IsStunned(unit)
                || IsFrozen(unit)
                || IsSleeping(unit)
                || IsParalyzed(unit);
        }

        public static bool TryBreakFreeze(
            BattleUnit unit,
            out int remainingTurns)
        {
            remainingTurns = 0;

            if (unit?.Character?.PassiveEffects == null)
            {
                return false;
            }

            var freeze = unit.Character.PassiveEffects.Find(effect =>
                effect != null
                && effect.Lifetime == PassiveEffectLifetime.Timed
                && effect.RemainingDuration > 0
                && string.Equals(
                    effect.EffectId,
                    StatusEffectIds.Freeze,
                    StringComparison.OrdinalIgnoreCase));

            if (freeze == null)
            {
                return false;
            }

            remainingTurns = freeze.RemainingDuration;
            unit.Character.PassiveEffects.Remove(freeze);
            return true;
        }

        private static PassiveEffectRuntime FindActiveStatus(
            BattleUnit unit,
            string statusId)
        {
            if (unit?.Character?.PassiveEffects == null
                || string.IsNullOrWhiteSpace(statusId))
            {
                return null;
            }

            foreach (var effect in unit.Character.PassiveEffects)
            {
                if (effect != null
                    && effect.RemainingDuration > 0
                    && string.Equals(
                        effect.EffectId,
                        statusId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return effect;
                }
            }

            return null;
        }
        
        public static bool HasStatus(BattleUnit unit, string statusId)
        {
            return unit?.Character != null
                && PassiveEffectLifecycleSystem.HasActiveEffect(
                    unit.Character,
                    statusId);
        }

        public void ProcessTurnStart(CombatSession session, BattleUnit unit)
        {
            passiveEffectLifecycleSystem.ProcessTurnStart(session, unit);
        }

        public void ProcessTurnEnd(BattleUnit unit)
        {
            passiveEffectLifecycleSystem.ProcessTurnEnd(unit);
        }

        private static PassiveEffectRuntime CreateRuntimeEffect(
            BattleUnit source, SkillData skill,
            SkillEffectData effect, int effectIndex)
        {
            var duration = Mathf.Max(1, effect.DurationTurns);
            var damagePerTurn = CalculateStatusDamagePerTurn(source, effect);
            var totalDamage = CalculateTotalDamage(damagePerTurn, duration);

            var sourceEffect = skill != null && effectIndex >= 0
                ? PassiveEffectSourceFactory.FromSkill(
                    source,
                    skill,
                    effectIndex)
                : PassiveEffectSourceFactory.FromRuntimeSystem(
                    source,
                    $"status.{effect.StatusId}");

            var runtimeEffect = new PassiveEffectRuntime(
                effect.StatusId,
                new List<PassiveEffectSourceRuntime> { sourceEffect },
                Mathf.Max(1, effect.Stacks),
                duration,
                totalDamage,
                PassiveEffectLifetime.Timed,
                ResolveTickPhase(effect.StatusId),
                effect.StatusHealPercent);

            if (string.Equals(
                    effect.StatusId,
                    StatusEffectIds.Chill,
                    StringComparison.OrdinalIgnoreCase))
            {
                runtimeEffect.StatModifiers.Add(new StatModifier
                {
                    StatType = CharacterStatType.Speed,
                    Operation = StatModifierOperation.Maximum,
                    Stacking = StatModifierStacking.Additive,
                Value = ChillMaximumSpeed
            });
            }

            return runtimeEffect;
        }

        public bool PassesMovementEffectChance(
            BattleUnit target,
            float baseChance)
        {
            var clampedChance = Mathf.Clamp01(baseChance);

            if (clampedChance <= 0f)
            {
                return false;
            }

            var resistance = target?.Character?.Resistances?.MoveResistance ?? 0f;
            var finalChance = CalculateEffectChance(clampedChance, resistance);

            return UnityEngine.Random.value <= finalChance;
        }

        private bool PassesEffectChance(
            BattleUnit target,
            SkillEffectData effect)
        {
            var baseChance = Mathf.Clamp01(effect.BaseChance);

            if (baseChance <= 0f)
            {
                return false;
            }

            var resistance = GetResistance(target, effect.StatusId);
            var chance = CalculateEffectChance(baseChance, resistance);

            return UnityEngine.Random.value <= chance;
        }

        private static float CalculateEffectChance(
            float baseChance,
            float resistance)
        {
            var s = Mathf.Clamp01(baseChance);
            var r = Mathf.Clamp01(resistance);

            if (s <= 0f)
            {
                return 0f;
            }

            if (r <= 0.5f)
            {
                var bonus = EvaluateChanceBonus(s);

                return Mathf.Clamp01(
                    s + (1f - 2f * r) * (1f - s) * bonus);
            }

            return Mathf.Clamp01((2f - 2f * r) * s);
        }

        private static float EvaluateChanceBonus(float baseChance)
        {
            if (baseChance <= 0.01f)
            {
                return Mathf.Lerp(
                    0f,
                    0.0404f,
                    baseChance / 0.01f);
            }

            if (baseChance <= 0.03f)
            {
                return Mathf.Lerp(
                    0.0404f,
                    0.0722f,
                    (baseChance - 0.01f) / 0.02f);
            }

            if (baseChance <= 0.25f)
            {
                return Mathf.Lerp(
                    0.0722f,
                    0.3333f,
                    (baseChance - 0.03f) / 0.22f);
            }

            if (baseChance <= 0.5f)
            {
                return Mathf.Lerp(
                    0.3333f,
                    0.4f,
                    (baseChance - 0.25f) / 0.25f);
            }

            return Mathf.Lerp(
                0.4f,
                0f,
                (baseChance - 0.5f) / 0.5f);
        }

        private static float GetResistance(
            BattleUnit target,
            string statusId)
        {
            if (target?.Character?.Resistances == null)
            {
                return 0f;
            }

            var resistances = target.Character.Resistances;

            return statusId.ToLowerInvariant() switch
            {
                StatusEffectIds.Bleed => resistances.BleedResistance,
                StatusEffectIds.Poison => resistances.PoisonResistance,
                StatusEffectIds.Burn => resistances.FireResistance,
                StatusEffectIds.Weakening => resistances.WeakeningResistance,
                StunId => resistances.SuppressionResistance,
                ImmobilizeId => resistances.SuppressionResistance,
                StatusEffectIds.Taunt => resistances.SuppressionResistance,
                StatusEffectIds.Freeze => resistances.SuppressionResistance,
                StatusEffectIds.Chill => resistances.SuppressionResistance,
                StatusEffectIds.Sleep => resistances.SuppressionResistance,
                StatusEffectIds.Paralysis => resistances.SuppressionResistance,
                _ => 0f
            };
        }

        private static PassiveEffectTickPhase ResolveTickPhase(string statusId)
        {
            if (IsDamageOverTimeStatus(statusId)
                || string.Equals(
                    statusId,
                    StatusEffectIds.Invisibility,
                    StringComparison.OrdinalIgnoreCase))
            {
                return PassiveEffectTickPhase.TurnStart;
            }

            return PassiveEffectTickPhase.TurnEnd;
        }

        private static bool IsDamageOverTimeStatus(string statusId)
        {
            return string.Equals(statusId, StatusEffectIds.Bleed,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Poison,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Burn,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static PassiveEffectStackingMode ResolveStackingMode(
            string statusId)
        {
            if (IsDamageOverTimeStatus(statusId))
            {
                return PassiveEffectStackingMode.ReplaceDurationAndAddTotalDamage;
            }
            return statusId.ToLowerInvariant() switch
            {
                StunId
                    => PassiveEffectStackingMode.ReplaceDuration,

                ImmobilizeId
                    => PassiveEffectStackingMode.ReplaceDuration,

                StatusEffectIds.Sleep
                    => PassiveEffectStackingMode.ReplaceDuration,

                StatusEffectIds.Freeze
                    => PassiveEffectStackingMode.ReplaceDuration,

                StatusEffectIds.Paralysis
                    => PassiveEffectStackingMode.ReplaceDuration,

                StatusEffectIds.Taunt
                    => PassiveEffectStackingMode.ReplaceDuration,

                StatusEffectIds.Invisibility
                    => PassiveEffectStackingMode.ReplaceDuration,

                _ => PassiveEffectStackingMode.MaxDurationAndValues
            };
        }

        private static int CalculateTotalDamage(
            int damagePerTurn,
            int duration)
        {
            var total = (long)damagePerTurn * duration;

            return total >= int.MaxValue
                ? int.MaxValue
                : (int)total;
        }

        private static int CalculateStatusDamagePerTurn(
            BattleUnit source,
            SkillEffectData effect)
        {
            if (effect == null || effect.StatusDamageMultiplier <= 0f)
            {
                return 0;
            }

            return CombatDamageValueCalculator.CalculateDamageValue(
                source,
                DamageValueSource.AverageAttack,
                effect.StatusDamageMultiplier);
        }
    }
}
