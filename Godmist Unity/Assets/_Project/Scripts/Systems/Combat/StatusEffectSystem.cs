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

        public bool ApplyOnHitStatusBuff(
            BattleUnit source,
            BattleUnit target,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex)
        {
            if (target?.Character == null || effect == null
                || string.IsNullOrWhiteSpace(effect.OnHitStatusId))
            {
                return false;
            }

            var effectId = $"{skill?.SkillId ?? "skill"}.on_hit_buff.{effectIndex}";
            var runtimeEffect = new PassiveEffectRuntime(
                effectId,
                new List<PassiveEffectSourceRuntime>
                {
                    PassiveEffectSourceFactory.FromSkill(source, skill, effectIndex)
                },
                stacks: 1,
                remainingDuration: Mathf.Max(1, effect.DurationTurns),
                totalDamage: 0,
                lifetime: PassiveEffectLifetime.Timed,
                tickPhase: PassiveEffectTickPhase.TurnEnd)
            {
                OnHitStatusId = effect.OnHitStatusId,
                OnHitStatusChance = effect.OnHitStatusChance,
                OnHitStatusDurationTurns = effect.OnHitStatusDurationTurns
            };

            return passiveEffectLifecycleSystem.TryApplyTimedEffect(
                target.Character,
                runtimeEffect,
                PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
        }

        public bool ApplyUniqueCombatBuff(
            BattleUnit source,
            BattleUnit target,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex)
        {
            if (target?.Character == null || effect == null) return false;

            var runtimeEffect = CreateTimedCombatBuff(
                source, skill, effectIndex,
                $"{skill?.SkillId ?? "skill"}.unique_buff.{effectIndex}",
                effect.DurationTurns);
            runtimeEffect.IsUniqueCombatBuff = true;
            runtimeEffect.UniqueBuffType = effect.UniqueBuffType;
            runtimeEffect.Stacks = effect.UniqueBuffStacks;
            return passiveEffectLifecycleSystem.TryApplyTimedEffect(target.Character,
                runtimeEffect, PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
        }

        public bool ApplyActionPointBuff(BattleUnit source, BattleUnit target,
            SkillData skill, SkillEffectData effect, int effectIndex)
        {
            if (target?.Character == null || effect == null
                || effect.ActionPointModifier == 0) return false;

            var runtimeEffect = CreateTimedCombatBuff(source, skill, effectIndex,
                $"{skill?.SkillId ?? "skill"}.action_points.{effectIndex}",
                effect.DurationTurns);
            runtimeEffect.ActionPointModifier = effect.ActionPointModifier;
            return passiveEffectLifecycleSystem.TryApplyTimedEffect(target.Character,
                runtimeEffect, PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
        }

        public bool ApplyResourceRegenerationBlock(BattleUnit source,
            BattleUnit target, SkillData skill, SkillEffectData effect, int effectIndex)
        {
            if (target?.Character == null || effect == null
                || effect.ResourceRegenerationBlockTurns <= 0) return false;

            var runtimeEffect = CreateTimedCombatBuff(source, skill, effectIndex,
                $"{skill?.SkillId ?? "skill"}.resource_block.{effectIndex}",
                effect.ResourceRegenerationBlockTurns);
            runtimeEffect.BlocksResourceRegeneration = true;
            return passiveEffectLifecycleSystem.TryApplyTimedEffect(target.Character,
                runtimeEffect, PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
        }

        public bool ApplyDamageRedirectBuff(BattleUnit source, BattleUnit target,
            SkillData skill, SkillEffectData effect, int effectIndex)
        {
            if (source?.Character == null || target?.Character == null
                || source == target || effect.RedirectedDamagePercent <= 0f)
            {
                return false;
            }

            var runtimeEffect = CreateTimedCombatBuff(source, skill, effectIndex,
                $"{skill?.SkillId ?? "skill"}.damage_redirect.{effectIndex}",
                effect.DurationTurns);
            runtimeEffect.DamageRedirectPercent = effect.RedirectedDamagePercent;
            return passiveEffectLifecycleSystem.TryApplyTimedEffect(target.Character,
                runtimeEffect, PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
        }

        public int ApplyActiveOnHitStatuses(BattleUnit source, BattleUnit target)
        {
            if (source?.Character?.PassiveEffects == null || target == null)
            {
                return 0;
            }

            var appliedCount = 0;
            foreach (var effect in source.Character.PassiveEffects)
            {
                if (!PassiveEffectLifecycleSystem.IsActive(effect)
                    || string.IsNullOrWhiteSpace(effect.OnHitStatusId))
                {
                    continue;
                }

                if (ApplyStatus(source, target, effect.OnHitStatusId,
                        effect.OnHitStatusChance,
                        Mathf.Max(1, effect.OnHitStatusDurationTurns),
                        1, 0f, 0f, effect.EffectId))
                {
                    appliedCount++;
                }
            }

            return appliedCount;
        }

        private static PassiveEffectRuntime CreateTimedCombatBuff(
            BattleUnit source, SkillData skill, int effectIndex,
            string effectId, int durationTurns)
        {
            return new PassiveEffectRuntime(
                effectId,
                new List<PassiveEffectSourceRuntime>
                {
                    PassiveEffectSourceFactory.FromSkill(source, skill, effectIndex)
                },
                stacks: 1,
                remainingDuration: Mathf.Max(1, durationTurns),
                totalDamage: 0,
                lifetime: PassiveEffectLifetime.Timed,
                tickPhase: PassiveEffectTickPhase.TurnEnd);
        }

        public bool ApplyStatus(
            BattleUnit source,
            BattleUnit target,
            string statusId,
            float baseChance,
            int durationTurns,
            int stacks,
            float statusDamageMultiplier,
            float statusHealPercent,
            string sourceId)
        {
            if (target?.Character == null
                || target.Character.LifeState != CharacterLifeState.Alive
                || string.IsNullOrWhiteSpace(statusId))
            {
                return false;
            }

            var clampedChance = Mathf.Clamp01(baseChance);
            var resistance = GetResistance(target, statusId);
            var chance = CalculateEffectChance(clampedChance, resistance);

            if (UnityEngine.Random.value > chance)
            {
                return false;
            }

            var duration = Mathf.Max(1, durationTurns);
            var damagePerTurn = statusDamageMultiplier <= 0f
                ? 0
                : CombatDamageValueCalculator.CalculateDamageValue(
                    source,
                    DamageValueSource.AverageAttack,
                    statusDamageMultiplier);

            var runtimeEffect = new PassiveEffectRuntime(
                statusId,
                new List<PassiveEffectSourceRuntime>
                {
                    PassiveEffectSourceFactory.FromRuntimeSystem(
                        source,
                        sourceId)
                },
                Mathf.Max(1, stacks),
                duration,
                damagePerTurn * duration,
                PassiveEffectLifetime.Timed,
                ResolveTickPhase(statusId),
                statusHealPercent);

            if (!passiveEffectLifecycleSystem.TryApplyTimedEffect(
                    target.Character,
                    runtimeEffect,
                    ResolveStackingMode(statusId),
                    out _))
            {
                return false;
            }

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} otrzymuje status {statusId}.");

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

        public static int RemoveStatusAppliedBySource(
            BattleUnit target,
            string statusId,
            string sourceId)
        {
            if (target?.Character?.PassiveEffects == null
                || string.IsNullOrWhiteSpace(statusId)
                || string.IsNullOrWhiteSpace(sourceId))
            {
                return 0;
            }

            var removed = 0;
            for (var index = target.Character.PassiveEffects.Count - 1;
                index >= 0; index--)
            {
                var effect = target.Character.PassiveEffects[index];
                if (!string.Equals(effect?.EffectId, statusId,
                        StringComparison.OrdinalIgnoreCase)
                    || effect.Sources == null
                    || !effect.Sources.Exists(source => string.Equals(
                        source?.SourceId, sourceId,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                target.Character.PassiveEffects.RemoveAt(index);
                removed++;
            }

            return removed;
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

            if (string.Equals(effect.StatusId, StatusEffectIds.Counterattack,
                    StringComparison.OrdinalIgnoreCase))
            {
                runtimeEffect.CounterattackSkill = skill;
                runtimeEffect.CounterattackRange = effect.CounterattackRange;
                runtimeEffect.CounterattackEffects =
                    effect.CounterattackEffects != null
                        ? new List<SkillEffectData>(effect.CounterattackEffects)
                        : new List<SkillEffectData>();
            }

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

            var baseResistance = target?.Character?.Resistances?.MoveResistance ?? 0f;
            var modifier = target?.ResistanceModifiers != null
                && target.ResistanceModifiers.TryGetValue(ResistanceType.Move, out var value)
                ? value : 0f;
            var resistance = Mathf.Clamp01(baseResistance + modifier);
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
            var resistanceType = statusId.ToLowerInvariant() switch
            {
                StatusEffectIds.Bleed => ResistanceType.Bleed,
                StatusEffectIds.Poison => ResistanceType.Poison,
                StatusEffectIds.Burn => ResistanceType.Fire,
                StunId => ResistanceType.Suppression,
                ImmobilizeId => ResistanceType.Suppression,
                StatusEffectIds.Taunt => ResistanceType.Suppression,
                StatusEffectIds.Freeze => ResistanceType.Suppression,
                StatusEffectIds.Chill => ResistanceType.Suppression,
                StatusEffectIds.Sleep => ResistanceType.Suppression,
                StatusEffectIds.Paralysis => ResistanceType.Suppression,
                _ => (ResistanceType?)null
            };

            if (!resistanceType.HasValue) return 0f;

            var baseResistance = resistanceType.Value switch
            {
                ResistanceType.Bleed => resistances.BleedResistance,
                ResistanceType.Poison => resistances.PoisonResistance,
                ResistanceType.Fire => resistances.FireResistance,
                ResistanceType.Weakening => resistances.WeakeningResistance,
                ResistanceType.Suppression => resistances.SuppressionResistance,
                ResistanceType.Move => resistances.MoveResistance,
                _ => 0f
            };
            var modifier = target.ResistanceModifiers != null
                && target.ResistanceModifiers.TryGetValue(resistanceType.Value, out var value)
                ? value : 0f;
            return Mathf.Clamp01(baseResistance + modifier);
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

                StatusEffectIds.Counterattack
                    => PassiveEffectStackingMode.ReplaceDurationAndValues,

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
