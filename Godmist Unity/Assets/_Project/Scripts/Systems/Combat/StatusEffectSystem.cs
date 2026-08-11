using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class StatusEffectSystem
    {
        public const string StunId = StatusEffectIds.Stun;
        public const string ImmobilizeId = StatusEffectIds.Immobilize;

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

            this.passiveEffectLifecycleSystem = passiveEffectLifecycleSystem
                ?? new PassiveEffectLifecycleSystem(
                    battleLog,
                    new IPassiveEffectTurnProcessor[]
                    {
                        new BleedPassiveEffectProcessor(this.damageSystem)
                    });
        }

        public bool ApplyStatus(
            BattleUnit source,
            BattleUnit target,
            SkillEffectData effect)
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

            var runtimeEffect = CreateRuntimeEffect(source, effect);

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
            BattleUnit source,
            SkillEffectData effect)
        {
            var duration = Mathf.Max(1, effect.DurationTurns);
            var sourceIds = new List<string>();
            var sourceId = source?.Character?.Definition?.Id ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                sourceIds.Add(sourceId);
            }

            var damagePerTurn = CalculateStatusDamagePerTurn(source, effect);
            var totalDamage = CalculateTotalDamage(damagePerTurn, duration);

            return new PassiveEffectRuntime(
                effect.StatusId,
                sourceIds,
                Mathf.Max(1, effect.Stacks),
                duration,
                totalDamage,
                isTimed: true,
                tickPhase: ResolveTickPhase(effect.StatusId));
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
                BleedPassiveEffectProcessor.BleedId => resistances.BleedResistance,
                StatusEffectIds.Poison => resistances.PoisonResistance,
                StatusEffectIds.Burn => resistances.FireResistance,
                StatusEffectIds.Weakening => resistances.WeakeningResistance,
                StunId => resistances.SuppressionResistance,
                ImmobilizeId => resistances.SuppressionResistance,
                StatusEffectIds.Sleep => resistances.SuppressionResistance,
                StatusEffectIds.Paralysis => resistances.SuppressionResistance,
                _ => 0f
            };
        }

        private static PassiveEffectTickPhase ResolveTickPhase(string statusId)
        {
            return string.Equals(
                statusId,
                BleedPassiveEffectProcessor.BleedId,
                StringComparison.OrdinalIgnoreCase)
                ? PassiveEffectTickPhase.TurnStart
                : PassiveEffectTickPhase.TurnEnd;
        }

        private static PassiveEffectStackingMode ResolveStackingMode(
            string statusId)
        {
            return statusId.ToLowerInvariant() switch
            {
                BleedPassiveEffectProcessor.BleedId
                    => PassiveEffectStackingMode
                        .ReplaceDurationAndAddTotalDamage,

                StatusEffectIds.Poison
                    => PassiveEffectStackingMode
                        .ReplaceDurationAndAddTotalDamage,

                StatusEffectIds.Burn
                    => PassiveEffectStackingMode
                        .ReplaceDurationAndAddTotalDamage,

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