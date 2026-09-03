using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Core.Types;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class StatusEffectSystem
    {
        public const string StunId = StatusEffectIds.Stun;
        public const string ImmobilizeId = StatusEffectIds.Immobilize;
        public static float ChillMaximumSpeed => GameDefinesRuntime.GetFloat(
            GameDefineIds.ChillMaximumSpeed, 20f);

        public static bool IsDamageOverTimeStatus(string statusId)
        {
            return string.Equals(statusId, StatusEffectIds.Bleed,
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Poison,
                       StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Burn,
                       StringComparison.OrdinalIgnoreCase);
        }

        private readonly Action<string> battleLog;
        private readonly CombatDamageSystem damageSystem;
        private readonly PassiveEffectLifecycleSystem passiveEffectLifecycleSystem;
        private readonly CombatFeedbackBus feedbackBus;
        private readonly PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher;

        public StatusEffectSystem(
            Action<string> battleLog = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            Action<CombatSession> evaluateOutcome = null,
            CombatDamageSystem damageSystem = null,
            PassiveEffectLifecycleSystem passiveEffectLifecycleSystem = null,
            CombatFeedbackBus feedbackBus = null,
            PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher = null)
        {
            this.battleLog = battleLog;
            this.passiveEffectEventDispatcher = passiveEffectEventDispatcher;

            this.damageSystem = damageSystem ?? new CombatDamageSystem(
                battleLog,
                evaluateOutcome,
                removeUnitFromCombat,
                passiveEffectEventDispatcher: passiveEffectEventDispatcher);

            var recoverySystem = new CombatRecoverySystem(battleLog);

            this.passiveEffectLifecycleSystem = passiveEffectLifecycleSystem
                ?? new PassiveEffectLifecycleSystem(
                    battleLog,
                    new IPassiveEffectTurnProcessor[]
                    {
                        new DamageOverTimePassiveEffectProcessor(this.damageSystem),
                        new SleepPassiveEffectProcessor(recoverySystem),
                        new PeriodicRecoveryPassiveEffectProcessor(recoverySystem)
                    });
            this.feedbackBus = feedbackBus;
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

            if (!TryApplySkillRuntimeEffect(
                    target.Character,
                    runtimeEffect,
                    ResolveStackingMode(effect.StatusId)))
            {
                return false;
            }

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} " +
                $"otrzymuje status {effect.StatusId}.");

            target.AddStatusTolerance(effect.StatusId);

            PublishEffectFeedback(target, CombatFeedbackType.StatusApplied, effect.StatusId);
            PublishStatusEvent(source, target, skill?.SkillId, effect.StatusId,
                PassiveEffectTriggerType.OnStatusApplied);

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
            var runtimeEffect = CreateTimedCombatBuff(
                source,
                skill,
                effectIndex,
                effectId,
                effect.DurationTurns);
            runtimeEffect.SetPayload(new OnHitStatusPayload
            {
                StatusId = effect.OnHitStatusId,
                Chance = effect.OnHitStatusChance,
                DurationTurns = effect.OnHitStatusDurationTurns,
                StatusDamageMultiplier = effect.StatusDamageMultiplier,
                StatusDamagePerTurn = CalculateStatusDamagePerTurn(
                    source,
                    effect.OnHitStatusId,
                    effect.StatusDamageMultiplier)
            });

            return TryApplySkillRuntimeEffect(
                target.Character,
                runtimeEffect,
                PassiveEffectStackingMode.ReplaceDurationAndValues)
                && PublishEffectFeedback(
                    target,
                    CombatFeedbackType.BuffApplied,
                    ResolveSkillName(skill, effectId));
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
            runtimeEffect.SetPayload(new UniqueCombatBuffPayload
            {
                BuffType = effect.UniqueBuffType
            });
            runtimeEffect.Stacks = effect.UniqueBuffStacks;
            return TryApplySkillRuntimeEffect(target.Character,
                runtimeEffect, PassiveEffectStackingMode.ReplaceDurationAndValues)
                && PublishEffectFeedback(
                    target,
                    CombatFeedbackType.BuffApplied,
                    ResolveSkillName(skill, runtimeEffect.EffectId));
        }

        public bool ApplyActionPointBuff(BattleUnit source, BattleUnit target,
            SkillData skill, SkillEffectData effect, int effectIndex)
        {
            if (target?.Character == null || effect == null
                || effect.ActionPointModifier == 0) return false;

            return ApplyActionPointBuffInternal(
                source, target, skill, effect, effectIndex,
                effect.ActionPointModifier);
        }

        public bool ApplyActionPointPercentBuff(BattleUnit source, BattleUnit target,
            SkillData skill, SkillEffectData effect, int effectIndex)
        {
            if (target?.Character == null || effect == null
                || effect.PercentageValue <= 0f || target.MaxActionPoints <= 0)
            {
                return false;
            }

            var modifier = Mathf.CeilToInt(target.MaxActionPoints * effect.PercentageValue);
            return modifier > 0
                && ApplyActionPointBuffInternal(
                    source, target, skill, effect, effectIndex, modifier);
        }

        private bool ApplyActionPointBuffInternal(BattleUnit source, BattleUnit target,
            SkillData skill, SkillEffectData effect, int effectIndex, int modifier)
        {
            if (target?.Character == null || effect == null || modifier == 0) return false;

            var runtimeEffect = CreateTimedCombatBuff(source, skill, effectIndex,
                $"{skill?.SkillId ?? "skill"}.action_points.{effectIndex}",
                effect.DurationTurns);
            runtimeEffect.SetPayload(new UniqueCombatBuffPayload
            {
                BuffType = UniqueBuffType.ActionPointsNextTurn,
                ActionPointModifier = modifier
            });
            return TryApplySkillRuntimeEffect(target.Character,
                runtimeEffect, PassiveEffectStackingMode.ReplaceDurationAndValues)
                && PublishEffectFeedback(
                    target,
                    CombatFeedbackType.BuffApplied,
                    ResolveSkillName(skill, runtimeEffect.EffectId));
        }

        public bool ApplyResourceRegenerationBlock(BattleUnit source,
            BattleUnit target, SkillData skill, SkillEffectData effect, int effectIndex)
        {
            if (target?.Character == null || effect == null
                || (skill?.IsToggle != true
                    && effect.ResourceRegenerationBlockTurns <= 0)) return false;

            var runtimeEffect = CreateTimedCombatBuff(source, skill, effectIndex,
                $"{skill?.SkillId ?? "skill"}.resource_block.{effectIndex}",
                effect.ResourceRegenerationBlockTurns);
            runtimeEffect.SetPayload(new ResourceRegenerationBlockPayload());
            var applied = TryApplySkillRuntimeEffect(target.Character,
                runtimeEffect, PassiveEffectStackingMode.ReplaceDurationAndValues);
            if (applied)
            {
                PublishEffectFeedback(
                    target,
                    CombatFeedbackType.ResourceRegenerationBlocked,
                    ResolveSkillName(skill, runtimeEffect.EffectId));
            }

            return applied;
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
            runtimeEffect.SetPayload(new DamageRedirectPayload
            {
                RedirectPercent = effect.RedirectedDamagePercent,
                ProtectorCharacterId = source.Character.CharacterId,
                ProtectorDisplayName = source.Character.Definition?.DisplayName
                    ?? source.Character.CharacterId,
                ProtectedCharacterId = target.Character.CharacterId,
                ProtectedCharacterDisplayName = target.Character.Definition?.DisplayName
                    ?? target.Character.CharacterId
            });
            var applied = TryApplySkillRuntimeEffect(target.Character,
                runtimeEffect, PassiveEffectStackingMode.ReplaceDurationAndValues)
                && PublishEffectFeedback(
                    target,
                    CombatFeedbackType.BuffApplied,
                    ResolveSkillName(skill, runtimeEffect.EffectId));
            if (!applied) return false;

            source.OutgoingDamageRedirects ??= new List<CombatDamageRedirectRuntime>();
            source.OutgoingDamageRedirects.RemoveAll(link =>
                link == null
                || (link.Target == target
                    && string.Equals(link.SourceEffectId, runtimeEffect.EffectId,
                        StringComparison.OrdinalIgnoreCase)));
            source.OutgoingDamageRedirects.Add(new CombatDamageRedirectRuntime
            {
                Target = target,
                SourceEffectId = runtimeEffect.EffectId,
                TargetCharacterId = target.Character.CharacterId,
                TargetDisplayName = target.Character.Definition?.DisplayName
                    ?? target.Character.CharacterId,
                RedirectPercent = effect.RedirectedDamagePercent
            });
            return true;
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
                var payload = effect.GetPayload<OnHitStatusPayload>();
                if (!PassiveEffectLifecycleSystem.IsActive(effect)
                    || payload == null
                    || string.IsNullOrWhiteSpace(payload.StatusId))
                {
                    continue;
                }

                // Older runtime effects may have persisted the multiplier but not
                // the already calculated per-turn value. Recalculate it at proc
                // time while the source unit is available, and retain it for the
                // recipient's inspect tooltip.
                if (payload.StatusDamagePerTurn <= 0
                    && payload.StatusDamageMultiplier > 0f)
                {
                    payload.StatusDamagePerTurn = CalculateStatusDamagePerTurn(
                        source,
                        payload.StatusId,
                        payload.StatusDamageMultiplier);
                }

                if (ApplyStatus(source, target, payload.StatusId,
                        payload.Chance,
                        Mathf.Max(1, payload.DurationTurns),
                        1, payload.StatusDamageMultiplier, 0f, effect.EffectId,
                        CombatFeedbackType.ProcTriggered,
                        payload.StatusId,
                        publishFeedback: true,
                        statusDamagePerTurnOverride: payload.StatusDamagePerTurn))
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
            var isToggle = skill?.IsToggle == true;
            return new PassiveEffectRuntime(
                effectId,
                new List<PassiveEffectSourceRuntime>
                {
                    PassiveEffectSourceFactory.FromSkill(source, skill, effectIndex)
                },
                stacks: 1,
                remainingDuration: isToggle ? 0 : Mathf.Max(1, durationTurns),
                totalDamage: 0,
                lifetime: isToggle
                    ? PassiveEffectLifetime.CombatScoped
                    : PassiveEffectLifetime.Timed,
                tickPhase: isToggle
                    ? PassiveEffectTickPhase.None
                    : PassiveEffectTickPhase.TurnEnd)
            {
                DisplayName = ResolveSkillName(skill, effectId)
            };
        }

        private bool TryApplySkillRuntimeEffect(
            CharacterInstance target,
            PassiveEffectRuntime runtimeEffect,
            PassiveEffectStackingMode stackingMode)
        {
            return runtimeEffect.Lifetime == PassiveEffectLifetime.CombatScoped
                ? passiveEffectLifecycleSystem.TryApplyCombatScopedEffect(
                    target,
                    runtimeEffect,
                    stackingMode,
                    out _)
                : passiveEffectLifecycleSystem.TryApplyTimedEffect(
                    target,
                    runtimeEffect,
                    stackingMode,
                    out _);
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
            string sourceId,
            CombatFeedbackType feedbackType = CombatFeedbackType.StatusApplied,
            string feedbackLabel = null,
            bool publishFeedback = true,
            int statusDamagePerTurnOverride = 0)
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
            var damagePerTurn = !IsDamageOverTimeStatus(statusId)
                ? 0
                : statusDamagePerTurnOverride > 0
                    ? statusDamagePerTurnOverride
                    : statusDamageMultiplier <= 0f
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

            if (!string.IsNullOrWhiteSpace(feedbackLabel)
                && feedbackType == CombatFeedbackType.AuraApplied)
            {
                runtimeEffect.DisplayName = feedbackLabel;
            }

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

            target.AddStatusTolerance(statusId);

            if (publishFeedback)
            {
                PublishEffectFeedback(
                    target,
                    feedbackType,
                    feedbackLabel ?? statusId);
            }

            PublishStatusEvent(source, target, null, statusId,
                PassiveEffectTriggerType.OnStatusApplied);

            return true;
        }

        private bool PublishEffectFeedback(
            BattleUnit target,
            CombatFeedbackType type,
            string label)
        {
            feedbackBus?.Publish(new CombatFeedbackEvent(
                target,
                type,
                effectName: label));
            return true;
        }

        private static string ResolveSkillName(SkillData skill, string fallback)
        {
            return !string.IsNullOrWhiteSpace(skill?.DisplayName)
                ? skill.DisplayName
                : fallback ?? string.Empty;
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

            PublishEffectFeedback(
                target,
                CombatFeedbackType.StatusApplied,
                StatusEffectIds.Stun);

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

        public static bool HasStatusAppliedBySource(
            BattleUnit target,
            string statusId,
            string sourceId)
        {
            return target?.Character?.PassiveEffects != null
                && target.Character.PassiveEffects.Exists(effect =>
                    string.Equals(effect?.EffectId, statusId,
                        StringComparison.OrdinalIgnoreCase)
                    && effect.Sources != null
                    && effect.Sources.Exists(source => string.Equals(
                        source?.SourceId,
                        sourceId,
                        StringComparison.OrdinalIgnoreCase)));
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
            var activeStatusesBefore = GetActiveStatusIds(unit);
            passiveEffectLifecycleSystem.ProcessTurnEnd(unit);
            unit?.AdvanceStatusTolerances();
            foreach (var statusId in activeStatusesBefore)
            {
                if (!HasStatus(unit, statusId))
                {
                    PublishStatusEvent(unit, unit, null, statusId,
                        PassiveEffectTriggerType.OnStatusExpired);
                }
            }
        }

        private void PublishStatusEvent(
            BattleUnit source,
            BattleUnit target,
            string skillId,
            string statusId,
            PassiveEffectTriggerType triggerType)
        {
            passiveEffectEventDispatcher?.Publish(
                triggerType,
                new PassiveEffectCombatContext(
                    source: source,
                    target: target,
                    skillId: skillId,
                    statusId: statusId),
                eventId: statusId);
        }

        private static HashSet<string> GetActiveStatusIds(BattleUnit unit)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var effect in unit?.Character?.PassiveEffects
                ?? new List<PassiveEffectRuntime>())
            {
                if (PassiveEffectLifecycleSystem.IsActive(effect)
                    && !string.IsNullOrWhiteSpace(effect.EffectId))
                {
                    ids.Add(effect.EffectId);
                }
            }
            return ids;
        }

        public bool ApplyPeriodicRecovery(
            BattleUnit source,
            BattleUnit target,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex,
            bool resource)
        {
            if (target?.Character == null || effect == null || effect.PercentageValue <= 0f)
            {
                return false;
            }

            var runtimeEffect = new PassiveEffectRuntime(
                $"{skill?.SkillId ?? "consumable"}.periodic_recovery.{effectIndex}",
                new List<PassiveEffectSourceRuntime>
                {
                    PassiveEffectSourceFactory.FromSkill(source, skill, effectIndex)
                },
                1,
                Mathf.Max(1, effect.DurationTurns),
                0,
                PassiveEffectLifetime.Timed,
                PassiveEffectTickPhase.TurnEnd)
            {
                DisplayName = skill?.DisplayName ?? "Regeneracja"
            };
            runtimeEffect.SetPayload(new PeriodicRecoveryPayload
            {
                HealthPercent = resource ? 0f : effect.PercentageValue,
                ResourcePercent = resource ? effect.PercentageValue : 0f
            });

            var applied = passiveEffectLifecycleSystem.TryApplyTimedEffect(
                target.Character,
                runtimeEffect,
                PassiveEffectStackingMode.ReplaceDurationAndValues,
                out _);
            if (applied)
            {
                PublishEffectFeedback(
                    target,
                    CombatFeedbackType.BuffApplied,
                    runtimeEffect.DisplayName);
            }

            return applied;
        }

        private static PassiveEffectRuntime CreateRuntimeEffect(
            BattleUnit source, SkillData skill,
            SkillEffectData effect, int effectIndex)
        {
            var isToggle = skill?.IsToggle == true;
            var duration = isToggle ? 0 : Mathf.Max(1, effect.DurationTurns);
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
                isToggle
                    ? PassiveEffectLifetime.CombatScoped
                    : PassiveEffectLifetime.Timed,
                isToggle
                    ? PassiveEffectTickPhase.None
                    : ResolveTickPhase(effect.StatusId),
                effect.StatusHealPercent);

            if (string.Equals(effect.StatusId, StatusEffectIds.Counterattack,
                    StringComparison.OrdinalIgnoreCase))
            {
                runtimeEffect.SetPayload(new CounterattackPayload
                {
                    Skill = skill,
                    Range = effect.CounterattackRange,
                    Effects = effect.CounterattackEffects != null
                        ? new List<SkillEffectData>(effect.CounterattackEffects)
                        : new List<SkillEffectData>()
                });
            }

            if (string.Equals(
                    effect.StatusId,
                    StatusEffectIds.Chill,
                    StringComparison.OrdinalIgnoreCase))
            {
                runtimeEffect.StatModifiers = new List<StatModifier>
                {
                    new()
                    {
                        StatType = CharacterStatType.Speed,
                        Operation = StatModifierOperation.Maximum,
                        Stacking = StatModifierStacking.Additive,
                        Value = ChillMaximumSpeed
                    }
                };
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
            var modifier = ModifyResistanceSystem.GetModifier(
                target,
                ResistanceType.Move);
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

            var resistanceHalfPoint = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceResistanceHalfPoint, .5f);
            var resistanceMultiplier = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceResistanceMultiplier, 2f);

            if (r <= resistanceHalfPoint)
            {
                var bonus = EvaluateChanceBonus(s);

                return Mathf.Clamp01(
                    s + (1f - r / Mathf.Max(.0001f, resistanceHalfPoint))
                    * (1f - s) * bonus);
            }

            return Mathf.Clamp01(resistanceMultiplier * (1f - r) * s);
        }

        private static float EvaluateChanceBonus(float baseChance)
        {
            var input1 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveInput1, .01f);
            var input2 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveInput2, .03f);
            var input3 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveInput3, .25f);
            var input4 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveInput4, .5f);
            var output1 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveOutput1, .0404f);
            var output2 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveOutput2, .0722f);
            var output3 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveOutput3, .3333f);
            var output4 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveOutput4, .4f);
            var output5 = GameDefinesRuntime.GetFloat(
                GameDefineIds.StatusChanceCurveOutput5, 0f);

            if (baseChance <= input1)
            {
                return Mathf.Lerp(
                    0f,
                    output1,
                    baseChance / Mathf.Max(.0001f, input1));
            }

            if (baseChance <= input2)
            {
                return Mathf.Lerp(
                    output1,
                    output2,
                    (baseChance - input1) / Mathf.Max(.0001f, input2 - input1));
            }

            if (baseChance <= input3)
            {
                return Mathf.Lerp(
                    output2,
                    output3,
                    (baseChance - input2) / Mathf.Max(.0001f, input3 - input2));
            }

            if (baseChance <= input4)
            {
                return Mathf.Lerp(
                    output3,
                    output4,
                    (baseChance - input3) / Mathf.Max(.0001f, input4 - input3));
            }

            return Mathf.Lerp(
                output4,
                output5,
                (baseChance - input4) / Mathf.Max(.0001f, 1f - input4));
        }

        private static float GetResistance(
            BattleUnit target,
            string statusId)
        {
            if (target?.Character == null)
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
                ResistanceType.Bleed => resistances?.BleedResistance ?? 0f,
                ResistanceType.Poison => resistances?.PoisonResistance ?? 0f,
                ResistanceType.Fire => resistances?.FireResistance ?? 0f,
                ResistanceType.Weakening => resistances?.WeakeningResistance ?? 0f,
                ResistanceType.Suppression => resistances?.SuppressionResistance ?? 0f,
                ResistanceType.Move => resistances?.MoveResistance ?? 0f,
                _ => 0f
            };
            var modifier = ModifyResistanceSystem.GetModifier(
                target,
                resistanceType.Value);
            return Mathf.Clamp01(
                baseResistance
                + modifier
                + target.GetStatusTolerance(statusId));
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
            return effect == null
                ? 0
                : CalculateStatusDamagePerTurn(
                    source,
                    effect.StatusId,
                    effect.StatusDamageMultiplier);
        }

        private static int CalculateStatusDamagePerTurn(
            BattleUnit source,
            string statusId,
            float statusDamageMultiplier)
        {
            if (!IsDamageOverTimeStatus(statusId)
                || statusDamageMultiplier <= 0f)
            {
                return 0;
            }

            return CombatDamageValueCalculator.CalculateDamageValue(
                source,
                DamageValueSource.AverageAttack,
                statusDamageMultiplier);
        }
    }
}
