using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class StatusEffectSystem
    {
        private const string BleedId = "bleed";
        public const string StunId = "stun";
        public const string ImmobilizeId = "immobilize";

        private readonly Action<string> battleLog;
        private readonly Action<CombatSession> evaluateOutcome;
        private readonly CombatDamageSystem damageSystem;

        public StatusEffectSystem(
            Action<string> battleLog = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            Action<CombatSession> evaluateOutcome = null, CombatDamageSystem damageSystem = null)
        {
            this.battleLog = battleLog;
            this.evaluateOutcome = evaluateOutcome;
            this.damageSystem = damageSystem ?? new CombatDamageSystem(
                battleLog,
                evaluateOutcome,
                removeUnitFromCombat);
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

            var duration = Mathf.Max(1, effect.DurationTurns);
            var stacks = Mathf.Max(1, effect.Stacks);
            var damagePerTurn = CalculateStatusDamagePerTurn(source, effect);

            var totalDamage = CalculateTotalDamage(damagePerTurn, duration);

            var sourceId = source?.Character?.Definition?.Id ?? string.Empty;

            var existingEffect = FindEffect(target.Character.PassiveEffects, effect.StatusId);

            if (existingEffect == null)
            {
                var sourceIds = new List<string>();

                if (!string.IsNullOrWhiteSpace(sourceId))
                {
                    sourceIds.Add(sourceId);
                }

                target.Character.PassiveEffects.Add(
                    new PassiveEffectRuntime(
                        effect.StatusId,
                        sourceIds,
                        IsBleed(effect.StatusId) ? 1 : stacks,
                        duration,
                        totalDamage,
                        isTimed: true));
            }
            else if (IsBleed(effect.StatusId))
            {
                existingEffect.RemainingDuration = duration;
                existingEffect.TotalDamage = AddSafely(
                    existingEffect.TotalDamage,
                    totalDamage);

                AddSource(
                    existingEffect.SourceEntityIds,
                    sourceId);

                existingEffect.Stacks = 1;
            }
            else
            {
                existingEffect.RemainingDuration =
                    Mathf.Max(
                        existingEffect.RemainingDuration,
                        duration);

                existingEffect.Stacks =
                    Mathf.Max(existingEffect.Stacks, stacks);

                existingEffect.TotalDamage =
                    Mathf.Max(
                        existingEffect.TotalDamage,
                        totalDamage);

                AddSource(
                    existingEffect.SourceEntityIds,
                    sourceId);
            }

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} " +
                $"otrzymuje status {effect.StatusId}.");

            return true;
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
            if (unit?.Character?.PassiveEffects == null || string.IsNullOrWhiteSpace(statusId)) return false;

            foreach (var effect in unit.Character.PassiveEffects)
            {
                if (effect != null
                    && effect.IsTimed
                    && effect.RemainingDuration > 0
                    && string.Equals(
                        effect.EffectId,
                        statusId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public void ProcessTurnStart(CombatSession session, BattleUnit unit)
        {
            if (session == null || unit?.Character == null || unit.Character.LifeState != CharacterLifeState.Alive 
             || unit.Character.PassiveEffects == null) return;

            var effects = unit.Character.PassiveEffects;

            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];

                if (effect == null)
                {
                    effects.RemoveAt(i);
                    continue;
                }

                if (!effect.IsTimed)
                {
                    continue;
                }

                if (effect.RemainingDuration <= 0)
                {
                    effects.RemoveAt(i);
                    continue;
                }

                if (IsBleed(effect.EffectId))
                {
                    ProcessBleedTick(session, unit, effect);

                    if (unit.Character.LifeState != CharacterLifeState.Alive) return;
                }

                if (effect.RemainingDuration <= 0 || (IsBleed(effect.EffectId) && effect.TotalDamage <= 0))
                {
                    effects.RemoveAt(i);

                    battleLog?.Invoke($"Status {effect.EffectId} wygasa u {unit.Character.Definition.DisplayName}.");
                }
            }
        }
        public void ProcessTurnEnd(BattleUnit unit)
        {
            if (unit?.Character == null || unit.Character.LifeState != CharacterLifeState.Alive
                || unit.Character.PassiveEffects == null) return;
            
            var effects = unit.Character.PassiveEffects;

            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];

                if (effect == null)
                {
                    effects.RemoveAt(i);
                    continue;
                }

                if (!effect.IsTimed)
                {
                    continue;
                }

                if (IsBleed(effect.EffectId)) continue;

                effect.RemainingDuration--;

                if (effect.RemainingDuration > 0) continue;

                effects.RemoveAt(i);

                battleLog?.Invoke($"Status {effect.EffectId} wygasa u " +
                    $"{unit.Character.Definition.DisplayName}.");
            }
        }

        private void ProcessBleedTick(
            CombatSession session,
            BattleUnit unit,
            PassiveEffectRuntime effect)
        {
            var remainingTurns =
                Mathf.Max(1, effect.RemainingDuration);

            var rawDamage = Mathf.CeilToInt(
                effect.TotalDamage / (float)remainingTurns);

            effect.TotalDamage = Mathf.Max(
                0,
                effect.TotalDamage - rawDamage);

            effect.RemainingDuration--;

            var resistance = GetBleedResistance(unit);
            var multiplier = CalculateBleedDamageMultiplier(
                resistance);

            var finalDamage = Mathf.Max(
                0,
                Mathf.RoundToInt(rawDamage * multiplier));

            damageSystem.ApplyDamage(session, null, unit, finalDamage, DamageType.Bleed);
        }

        private bool PassesEffectChance(
            BattleUnit target,
            SkillEffectData effect)
        {
            var baseChance =
                Mathf.Clamp01(effect.BaseChance);

            if (baseChance <= 0f)
            {
                return false;
            }

            var resistance = GetResistance(
                target,
                effect.StatusId);

            var chance = CalculateEffectChance(
                baseChance,
                resistance);

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

            float chance;

            if (r <= 0.5f)
            {
                var bonus = EvaluateChanceBonus(s);

                chance = s
                    + (1f - 2f * r)
                    * (1f - s)
                    * bonus;
            }
            else
            {
                chance = (2f - 2f * r) * s;
            }

            return Mathf.Clamp01(chance);
        }

        private static float EvaluateChanceBonus(
            float baseChance)
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

        private static float CalculateBleedDamageMultiplier(
            float resistance)
        {
            var r = Mathf.Clamp01(resistance);

            if (r <= 0.5f)
            {
                return 2f - 2f * r;
            }

            return 1.5f - r;
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
                "bleed" => resistances.BleedResistance,
                "poison" => resistances.PoisonResistance,
                "fire" => resistances.FireResistance,
                "weakening" => resistances.WeakeningResistance,
                "stun" => resistances.SuppressionResistance,
                "immobilize" => resistances.SuppressionResistance,
                "sleep" => resistances.SuppressionResistance,
                "paralysis" => resistances.SuppressionResistance,
                _ => 0f
            };
        }

        private static float GetBleedResistance(
            BattleUnit target)
        {
            return Mathf.Clamp01(
                target?.Character?.Resistances
                    ?.BleedResistance ?? 0f);
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

        private static int AddSafely(
            int first,
            int second)
        {
            if (second > int.MaxValue - first)
            {
                return int.MaxValue;
            }

            return first + second;
        }

        private static bool IsBleed(string statusId)
        {
            return string.Equals(
                statusId,
                BleedId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static PassiveEffectRuntime FindEffect(
            List<PassiveEffectRuntime> effects,
            string effectId)
        {
            foreach (var effect in effects)
            {
                if (effect != null
                    && string.Equals(
                        effect.EffectId,
                        effectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return effect;
                }
            }

            return null;
        }

        private static void AddSource(
            List<string> sourceIds,
            string sourceId)
        {
            if (sourceIds == null
                || string.IsNullOrWhiteSpace(sourceId)
                || sourceIds.Contains(sourceId))
            {
                return;
            }

            sourceIds.Add(sourceId);
        }
        private static int CalculateStatusDamagePerTurn(BattleUnit source, SkillEffectData effect)
        {
            if (effect == null || effect.StatusDamageMultiplier <= 0f) return 0;

            return CombatDamageValueCalculator.CalculateDamageValue(source, DamageValueSource.AverageAttack, effect.StatusDamageMultiplier);
        }
    }
}
