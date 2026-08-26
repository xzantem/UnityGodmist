using System;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Runtime.Combat;
using UnityEngine;
using System.Collections.Generic;
using Godmist.Core.Constants;

namespace Godmist.Systems.Combat
{
    public class SkillExecutionSystem
    {
        private readonly Action<string> battleLog;
        private readonly Action<BattleUnit> removeUnitFromCombat;
        private readonly Action<CombatSession> evaluateOutcome;
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly CombatDamageSystem damageSystem;
        private readonly CombatRecoverySystem recoverySystem;
        private readonly CombatShieldSystem shieldSystem;
        private readonly CombatLineOfSightService lineOfSightService;
        private readonly ModifyStatSystem modifyStatSystem;
        private readonly ModifyVarSystem modifyVarSystem;
        private readonly CombatChargeSystem chargeSystem;
        private readonly CombatTeleportSystem teleportSystem;
        private readonly SummonedObjectSystem summonedObjectSystem;
        private readonly SummonSystem summonSystem;
        private readonly CombatReactionSystem reactionSystem;
        private readonly CombatFeedbackBus feedbackBus;

        public SkillExecutionSystem(
            Action<string> battleLog = null,
            Action<BattleUnit> removeUnitFromCombat = null,
            Action<CombatSession> evaluateOutcome = null,
            StatusEffectSystem statusEffectSystem = null,
            CombatDamageSystem damageSystem = null,
            CombatRecoverySystem recoverySystem = null,
            CombatShieldSystem shieldSystem = null,
            CombatLineOfSightService lineOfSightService = null,
            ModifyStatSystem modifyStatSystem = null,
            ModifyVarSystem modifyVarSystem = null,
            CombatChargeSystem chargeSystem = null,
            CombatTeleportSystem teleportSystem = null,
            SummonedObjectSystem summonedObjectSystem = null,
            SummonSystem summonSystem = null,
            CombatReactionSystem reactionSystem = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.battleLog = battleLog;
            this.removeUnitFromCombat = removeUnitFromCombat;
            this.evaluateOutcome = evaluateOutcome;
            this.statusEffectSystem = statusEffectSystem;

            this.damageSystem = damageSystem
                ?? new CombatDamageSystem(
                    battleLog,
                    evaluateOutcome,
                    removeUnitFromCombat);

            this.recoverySystem = recoverySystem
                ?? new CombatRecoverySystem(battleLog);

            this.shieldSystem = shieldSystem
                ?? new CombatShieldSystem(battleLog);

            this.lineOfSightService = lineOfSightService
                ?? new CombatLineOfSightService();

            this.modifyStatSystem = modifyStatSystem
                ?? new ModifyStatSystem();

            this.modifyVarSystem = modifyVarSystem
                ?? new ModifyVarSystem();

            this.chargeSystem = chargeSystem
                ?? new CombatChargeSystem(
                    battleLog,
                    this.statusEffectSystem);

            this.teleportSystem = teleportSystem
                ?? new CombatTeleportSystem(
                    battleLog,
                    this.lineOfSightService,
                    this.statusEffectSystem);

            this.summonedObjectSystem = summonedObjectSystem
                ?? new SummonedObjectSystem(battleLog);
            this.summonSystem = summonSystem ?? new SummonSystem(battleLog);
            this.reactionSystem = reactionSystem ?? new CombatReactionSystem(battleLog);
            this.feedbackBus = feedbackBus;
        }

        public bool TryExecute(CombatSession session, BattleUnit caster, SkillData skill, BattleUnit target)
        {
            if (target == null || skill?.TargetModes == null || skill.TargetModes.Count != 1) return false;

            var targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>
                { [0] = new List<BattleUnit> { target } };

            return TryExecute(session, caster, skill, targetsByModeIndex);
        }

        public bool TryExecute(CombatSession session, BattleUnit caster, SkillData skill, 
            IReadOnlyList<BattleUnit> targets)
        {
            if (targets == null || skill?.TargetModes == null || skill.TargetModes.Count != 1) return false;

            var targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>
                { [0] = targets };

            return TryExecute(session, caster, skill, targetsByModeIndex);
        }

        public bool TryExecute(
            CombatSession session,
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            return TryExecute(
                session,
                caster,
                skill,
                targetsByModeIndex,
                null);
        }

        public bool TryExecute(
            CombatSession session,
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex,
            GridPosition? teleportDestination)
        {
            return TryExecute(
                session,
                caster,
                skill,
                targetsByModeIndex,
                teleportDestination,
                null);
        }

        public bool TryExecute(
            CombatSession session,
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex,
            GridPosition? teleportDestination,
            GridPosition? objectPlacementDestination)
        {
            return TryExecute(session, caster, skill, targetsByModeIndex,
                teleportDestination, objectPlacementDestination, null);
        }

        public bool TryExecute(
            CombatSession session,
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex,
            GridPosition? teleportDestination,
            GridPosition? objectPlacementDestination,
            IReadOnlyDictionary<int, GridPosition?> selectedPositionsByModeIndex)
        {
            var isToggleDeactivation = IsToggleActive(caster, skill);
            if (!CanExecute(
                    session,
                    caster,
                    skill,
                    targetsByModeIndex,
                    teleportDestination,
                    objectPlacementDestination,
                    isToggleDeactivation))
            {
                return false;
            }

            if (isToggleDeactivation)
            {
                DeactivateToggle(
                    session,
                    caster,
                    skill.SkillId,
                    markUsedThisTurn: false,
                    writeBattleLog: true);
                return true;
            }

            if (!ScoutKnifeSystem.TrySpendAndDrop(
                    session,
                    caster,
                    skill,
                    ResolveKnifeTargets(skill, targetsByModeIndex)))
            {
                return false;
            }

            var actionCost =
                CombatActionCostCalculator.CalculateSkillCost(caster, skill);

            caster.CurrentActionPoints -= actionCost;
            if (actionCost > 0)
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    caster, CombatFeedbackType.ActionPointSpend, actionCost));
            }

            var spentResource = caster.Character.ResourcePool?.Drain(skill.ResourceCost) ?? 0;
            caster.LastSkillResourceSpent = spentResource;
            if (spentResource > 0)
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    caster,
                    CombatFeedbackType.ResourceSpend,
                    spentResource,
                    resourceType: caster.Character.ResourcePool.ResourceType));
            }

            battleLog?.Invoke(
                $"{caster.Character.Definition.DisplayName} uses {skill.DisplayName}.");

            var hitByTarget = new Dictionary<BattleUnit, bool>();
            var resultsByEffectIndex =
                new Dictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>();

            for (var effectIndex = 0;
                effectIndex < skill.Effects.Count;
                effectIndex++)
            {
                var effect = skill.Effects[effectIndex];

                if (effect.EffectType == EffectType.CreateObject)
                {
                    var resultsForEffect =
                        new Dictionary<BattleUnit, EffectExecutionResult>();

                    var created = summonedObjectSystem.TryCreate(
                        session,
                        caster,
                        skill,
                        effect,
                        effectIndex,
                        objectPlacementDestination.Value,
                        out _);

                    resultsForEffect[caster] =
                        new EffectExecutionResult
                        {
                            Succeeded = created
                        };

                    resultsByEffectIndex[effectIndex] = resultsForEffect;
                    continue;
                }

                if (effect.EffectType == EffectType.Summon)
                {
                    var resultsForEffect =
                        new Dictionary<BattleUnit, EffectExecutionResult>();
                    var summoned = objectPlacementDestination.HasValue
                        && summonSystem.TrySummon(session, caster, skill, effect,
                            effectIndex, objectPlacementDestination.Value, out _);
                    resultsForEffect[caster] = new EffectExecutionResult
                    {
                        Succeeded = summoned
                    };
                    resultsByEffectIndex[effectIndex] = resultsForEffect;
                    continue;
                }

                var targets = targetsByModeIndex[effect.TargetModeIndex];

                if (effect.DelayCasterTurns > 0)
                {
                    ScheduleEffect(session, caster, skill, effect, effectIndex,
                        targets, selectedPositionsByModeIndex);
                    continue;
                }

                var resultsForNormalEffect =
                    new Dictionary<BattleUnit, EffectExecutionResult>();

                foreach (var target in targets)
                {
                    if (target?.Character == null
                        || target.Character.LifeState == CharacterLifeState.Dead)
                    {
                        continue;
                    }

                    if (!ShouldExecuteEffect(
                            session,
                            effect,
                            caster,
                            target,
                            resultsByEffectIndex))
                    {
                        resultsForNormalEffect[target] =
                            EffectExecutionResult.None;

                        continue;
                    }

                    // Damage effects roll accuracy independently for every hit in a
                    // multi-hit sequence. Other hit-gated effects retain the existing
                    // per-target roll shared by their follow-up effects.
                    var wasHit = effect.EffectType == EffectType.DealDamage
                        || !RequiresHitRoll(effect)
                        || ResolveTargetHit(
                            caster,
                            target,
                            skill,
                            hitByTarget);

                    if (!wasHit)
                    {
                        resultsForNormalEffect[target] =
                            EffectExecutionResult.None;

                        continue;
                    }

                    resultsForNormalEffect[target] = ExecuteEffect(
                        session,
                        caster,
                        target,
                        skill,
                        effect,
                        effectIndex,
                        wasHit,
                        teleportDestination,
                        isReactionExecution: false);
                }

                resultsByEffectIndex[effectIndex] =
                    resultsForNormalEffect;
            }

            if (IsOffensiveSkill(skill))
            {
                statusEffectSystem?.RemoveStatus(
                    caster,
                    StatusEffectIds.Invisibility);
            }

            CombatAuraSystem.Refresh(session, statusEffectSystem);
            if (skill.IsToggle)
            {
                caster.ActiveToggleSkillIds ??= new HashSet<string>();
                caster.ActiveToggleSkillIds.Add(skill.SkillId);
                caster.ToggleSkillIdsUsedThisTurn?.Add(skill.SkillId);
            }
            CombatTurnSystem.RefreshMaxActionPoints(session);
            return true;
        }

        private static IReadOnlyList<BattleUnit> ResolveKnifeTargets(
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            if (skill?.Effects == null || targetsByModeIndex == null) return null;
            foreach (var effect in skill.Effects)
            {
                if (effect != null && targetsByModeIndex.TryGetValue(
                        effect.TargetModeIndex, out var targets)) return targets;
            }
            return null;
        }

        private void ScheduleEffect(
            CombatSession session, BattleUnit caster, SkillData skill,
            SkillEffectData effect, int effectIndex,
            IReadOnlyList<BattleUnit> targets,
            IReadOnlyDictionary<int, GridPosition?> selectedPositions)
        {
            var target = targets != null && targets.Count > 0 ? targets[0] : null;
            GridPosition? position = target?.Position;
            if (selectedPositions != null
                && selectedPositions.TryGetValue(effect.TargetModeIndex, out var selected)
                && selected.HasValue)
                position = selected;

            session.ScheduledEffects ??= new List<ScheduledCombatEffect>();
            session.ScheduledEffects.Add(new ScheduledCombatEffect(
                caster.Character.CharacterId, skill, effect, effectIndex,
                effect.DelayCasterTurns + 1, target?.Character?.CharacterId, position));
            battleLog?.Invoke($"{caster.Character.Definition.DisplayName} planuje efekt za {effect.DelayCasterTurns} tur.");
        }

        private void ExecuteScheduledEffect(CombatSession session, BattleUnit caster,
            ScheduledCombatEffect scheduled)
        {
            var targets = ResolveScheduledTargets(session, caster, scheduled);
            var hitByTarget = new Dictionary<BattleUnit, bool>();
            var results = new Dictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>();

            foreach (var target in targets)
            {
                if (target?.Character == null
                    || target.Character.LifeState == CharacterLifeState.Dead
                    || !ShouldExecuteEffect(session, scheduled.Effect, caster, target, results))
                    continue;

                var wasHit = scheduled.Effect.EffectType == EffectType.DealDamage
                    || !RequiresHitRoll(scheduled.Effect)
                    || ResolveTargetHit(caster, target, scheduled.Skill, hitByTarget);
                if (wasHit)
                    ExecuteEffect(session, caster, target, scheduled.Skill,
                        scheduled.Effect, scheduled.EffectIndex, true, null);
            }

            battleLog?.Invoke($"Wykonuje sie opozniony efekt {scheduled.Skill.DisplayName}.");
        }

        private static IReadOnlyList<BattleUnit> ResolveScheduledTargets(
            CombatSession session, BattleUnit caster, ScheduledCombatEffect scheduled)
        {
            var targets = new List<BattleUnit>();
            if (session?.Units == null || scheduled?.Skill?.TargetModes == null
                || scheduled.Effect.TargetModeIndex < 0
                || scheduled.Effect.TargetModeIndex >= scheduled.Skill.TargetModes.Count)
                return targets;

            var mode = scheduled.Skill.TargetModes[scheduled.Effect.TargetModeIndex];
            if (mode?.TargetType == TargetType.Single)
            {
                foreach (var unit in session.Units)
                    if (unit?.Character?.CharacterId == scheduled.TargetCharacterId)
                    {
                        targets.Add(unit);
                        break;
                    }
                return targets;
            }
            if (mode?.TargetType == TargetType.Caster)
            {
                targets.Add(caster);
                return targets;
            }
            if (mode?.TargetType == TargetType.AreaAroundTile
                && scheduled.TargetPosition.HasValue)
            {
                foreach (var unit in session.Units)
                {
                    if (unit?.Character == null
                        || unit.Character.LifeState != CharacterLifeState.Alive
                        || !IsScheduledTargetEligible(caster, unit,
                            mode.TargetingType)
                        || !CombatTargetingService.IsPositionWithinRange(
                            scheduled.TargetPosition.Value,
                            unit.Position,
                            mode.RelativeRange))
                    {
                        continue;
                    }

                    targets.Add(unit);
                }
            }

            return targets;
        }

        private static bool IsScheduledTargetEligible(
            BattleUnit caster,
            BattleUnit target,
            TargetingType targetingType)
        {
            return targetingType == TargetingType.All
                || targetingType == TargetingType.Allies
                    && target.IsPlayerSide == caster.IsPlayerSide
                || targetingType == TargetingType.Enemies
                    && target.IsPlayerSide != caster.IsPlayerSide;
        }

        private EffectExecutionResult ExecuteEffect(
            CombatSession session,
            BattleUnit caster,
            BattleUnit target,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex,
            bool wasHit,
            GridPosition? teleportDestination)
        {
            return ExecuteEffect(
                session,
                caster,
                target,
                skill,
                effect,
                effectIndex,
                wasHit,
                teleportDestination,
                isReactionExecution: false);
        }

        private EffectExecutionResult ExecuteEffect(
            CombatSession session,
            BattleUnit caster,
            BattleUnit target,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex,
            bool wasHit,
            GridPosition? teleportDestination,
            bool isReactionExecution)
        {
            switch (effect.EffectType)
            {
                case EffectType.DealDamage:
                {
                    var totalHealthDamage = 0;
                    var hit = false;
                    var critical = false;
                    for (var hitIndex = 0; hitIndex < effect.DamageHitCount; hitIndex++)
                    {
                        var multiplier = effect.DamageMultiplier;
                        if (UnityEngine.Random.value <= effect.BonusDamageMultiplierChance)
                            multiplier *= effect.BonusDamageMultiplier;

                        var forceMaximumDamage = CombatDamageSystem.HasUniqueBuff(
                            caster, UniqueBuffType.MaximumDamage);
                        var valueSource = forceMaximumDamage
                            ? DamageValueSource.MaxAttack
                            : effect.DamageValueSource;

                        var attackValue = CombatDamageValueCalculator.CalculateDamageValue(
                            caster, valueSource, multiplier);
                        var damageResult = effect.AlwaysHits
                            ? damageSystem.ResolveAndApplyDamage(
                                session, caster, target, attackValue, effect.DamageType,
                                wasHit: true,
                                bonusDefensePenetration: effect.BonusPenetration)
                            : damageSystem.ResolveAndApplyDamage(
                                session, caster, target, attackValue, effect.DamageType,
                                skill.SkillAccuracy,
                                effect.BonusPenetration);
                        hit |= damageResult.Hit;
                        critical |= damageResult.IsCritical;
                        totalHealthDamage += damageResult.HealthDamage;

                        if (!damageResult.Hit) continue;
                        if (forceMaximumDamage)
                        {
                            CombatDamageSystem.TryConsumeUniqueBuff(
                                caster, UniqueBuffType.MaximumDamage);
                        }
                        ApplyDamageFollowUps(caster, target, skill, effect, effectIndex,
                            damageResult.HealthDamage);
                        statusEffectSystem?.ApplyActiveOnHitStatuses(caster, target);
                        if (!isReactionExecution)
                            TryExecuteCounterattack(session, target, caster);
                    }

                    return new EffectExecutionResult
                    {
                        Succeeded = hit,
                        HealthDamage = totalHealthDamage,
                        WasCritical = critical
                    };
                }
                case EffectType.Push:
                {
                    var wasPushed = TryApplyForcedDisplacement(
                        session,
                        target,
                        caster,
                        effect.DisplacementDistance,
                        ForcedDisplacementDirection.AwayFromReference,
                        "odepchnięty");

                    return new EffectExecutionResult { Succeeded = wasPushed };
                }

                case EffectType.Pull:
                {
                    if (statusEffectSystem == null
                        || !statusEffectSystem.PassesMovementEffectChance(
                            target,
                            effect.BaseChance))
                    {
                        battleLog?.Invoke(
                            $"{target.Character.Definition.DisplayName} opiera się przyciągnięciu.");

                        return new EffectExecutionResult { Succeeded = false };
                    }

                    var wasPulled = TryApplyForcedDisplacement(
                        session,
                        target,
                        caster,
                        effect.DisplacementDistance,
                        ForcedDisplacementDirection.TowardsReference,
                        "przyciągnięty");

                    return new EffectExecutionResult { Succeeded = wasPulled };
                }

                case EffectType.Fallback:
                {
                    var wasMovedBack = TryApplyForcedDisplacement(
                        session,
                        caster,
                        target,
                        effect.DisplacementDistance,
                        ForcedDisplacementDirection.AwayFromReference,
                        "wycofany");

                    return new EffectExecutionResult { Succeeded = wasMovedBack };
                }
                case EffectType.Charge:
                {
                    var charged = chargeSystem != null
                        && chargeSystem.TryCharge(
                            session,
                            caster,
                            target,
                            skill,
                            effect,
                            effectIndex);

                    return new EffectExecutionResult { Succeeded = charged };
                }

                case EffectType.Teleport:
                {
                    if (!teleportDestination.HasValue)
                    {
                        return EffectExecutionResult.None;
                    }

                    var teleported = teleportSystem != null
                        && teleportSystem.TryTeleport(
                            session,
                            caster,
                            target,
                            teleportDestination.Value,
                            skill.RequiresLineOfSight);

                    return new EffectExecutionResult { Succeeded = teleported };
                }
                case EffectType.ApplyStatus:
                {
                    var wasApplied = statusEffectSystem != null && 
                        statusEffectSystem.ApplyStatus(caster, target, skill, effect, effectIndex);
                    return new EffectExecutionResult { Succeeded = wasApplied };
                }
                case EffectType.Heal:
                {
                    var restoredHealth = recoverySystem.Heal(target, effect.BaseValue);
                    return new EffectExecutionResult 
                        { Succeeded = restoredHealth > 0, HealthRestored = restoredHealth };
                }
                case EffectType.RestoreResource:
                {
                    var restoredResource = recoverySystem.RestoreResource(target, effect.BaseValue);
                    return new EffectExecutionResult 
                        { Succeeded = restoredResource > 0, ResourceRestored = restoredResource };
                }
                case EffectType.Shield:
                {
                    var shieldAmount = effect.BaseValue + Mathf.FloorToInt(
                        CombatDamageValueCalculator.CalculateDamageValue(
                            caster, DamageValueSource.AverageAttack, 1f)
                        * effect.ShieldAverageAttackMultiplier);
                    var grantedShield = shieldSystem.GrantShield(target, shieldAmount, skill.SkillId);

                    return new EffectExecutionResult
                        { Succeeded = grantedShield > 0, ShieldGranted = grantedShield };
                }
                case EffectType.ModifyStat:
                {
                    var wasApplied = modifyStatSystem != null && modifyStatSystem.Apply(caster, target, skill, effect, effectIndex);
                    return new EffectExecutionResult { Succeeded = wasApplied };
                }
                case EffectType.ModifyVar:
                {
                    var wasApplied = modifyVarSystem != null 
                        && modifyVarSystem.Apply(caster, target, skill, effect, effectIndex);

                    return new EffectExecutionResult { Succeeded = wasApplied };
                }
                case EffectType.ModifyActionValue:
                {
                    var maximumActionValue = CombatTurnSystem.CalculateBaseActionValue(target);
                    var change = maximumActionValue * Mathf.Abs(effect.ActionValuePercent);
                    target.CurrentActionValue = Mathf.Max(0f,
                        target.CurrentActionValue + (effect.ActionValuePercent >= 0f
                            ? -change : change));
                    CombatTurnSystem.ReorderParticipants(session);
                    return new EffectExecutionResult { Succeeded = !Mathf.Approximately(change, 0f) };
                }
                case EffectType.DrainResource:
                {
                    var drained = target.Character.ResourcePool?.Drain(effect.BaseValue) ?? 0;
                    return new EffectExecutionResult { Succeeded = drained > 0, ResourceRestored = drained };
                }
                case EffectType.BlockResourceRegeneration:
                {
                    var wasApplied = statusEffectSystem != null
                        && statusEffectSystem.ApplyResourceRegenerationBlock(
                            caster, target, skill, effect, effectIndex);
                    return new EffectExecutionResult { Succeeded = wasApplied };
                }
                case EffectType.SacrificeHealthForResource:
                {
                    var maximumHealth = target.Character.Stats == null
                        ? 0
                        : Mathf.RoundToInt(target.Character.Stats.MaxHealth.GetValue());
                    var healthCost = Mathf.FloorToInt(maximumHealth * effect.HealthSacrificePercent);
                    healthCost = Mathf.Clamp(healthCost, 0, Mathf.Max(0, target.Character.CurrentHealth - 1));
                    if (healthCost <= 0 || effect.ResourceRestorePerSacrificedHealth <= 0f)
                        return EffectExecutionResult.None;
                    target.Character.CurrentHealth -= healthCost;
                    var restored = recoverySystem.RestoreResource(target,
                        Mathf.FloorToInt(healthCost * effect.ResourceRestorePerSacrificedHealth));
                    return new EffectExecutionResult { Succeeded = restored > 0, ResourceRestored = restored };
                }
                case EffectType.ModifyResistance:
                {
                    target.ResistanceModifiers ??= new Dictionary<ResistanceType, float>();
                    target.ResistanceModifiers.TryGetValue(effect.ResistanceType,
                        out var currentModifier);
                    target.ResistanceModifiers[effect.ResistanceType] =
                        currentModifier + effect.ModifierValue;
                    return new EffectExecutionResult { Succeeded = !Mathf.Approximately(effect.ModifierValue, 0f) };
                }
                case EffectType.ApplyUniqueBuff:
                {
                    var wasApplied = ApplyUniqueBuff(
                        session, caster, target, skill, effect, effectIndex);
                    return new EffectExecutionResult { Succeeded = wasApplied };
                }
                default:
                    Debug.LogWarning($"Nieobslugiwany typ efektu w skillu '{effect.EffectType}'.");
                    return EffectExecutionResult.None;
            }
        }

        private static bool ShouldExecuteEffect(
            CombatSession session,
            SkillEffectData effect,
            BattleUnit caster,
            BattleUnit target,
            IReadOnlyDictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>
                resultsByEffectIndex)
        {
            if (effect?.Conditions == null || effect.Conditions.Count == 0)
            {
                return true;
            }

            foreach (var condition in effect.Conditions)
            {
                if (!EvaluateCondition(
                        session,
                        condition,
                        caster,
                        target,
                        resultsByEffectIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateCondition(
            CombatSession session,
            SkillEffectConditionData condition,
            BattleUnit caster,
            BattleUnit target,
            IReadOnlyDictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>
                resultsByEffectIndex)
        {
            if (condition == null)
            {
                return false;
            }

            switch (condition.ConditionType)
            {
                case EffectConditionType.Always:
                    return true;

                case EffectConditionType.ReferencedEffectSucceeded:
                    return TryGetReferencedEffectResult(
                        condition,
                        target,
                        resultsByEffectIndex,
                        out var successResult)
                        && successResult.Succeeded;

                case EffectConditionType.ReferencedEffectDealtHealthDamage:
                    return TryGetReferencedEffectResult(
                        condition,
                        target,
                        resultsByEffectIndex,
                        out var damageResult)
                        && damageResult.HealthDamage > 0;

                case EffectConditionType.TargetHasStatus:
                    return HasStatus(target, condition.StatusId);

                case EffectConditionType.CasterHasStatus:
                    return HasStatus(caster, condition.StatusId);

                case EffectConditionType.TargetHealthBelowPercent:
                    return IsHealthBelowPercent(
                        target,
                        condition.HealthPercentThreshold);

                case EffectConditionType.CasterHealthBelowPercent:
                    return IsHealthBelowPercent(
                        caster,
                        condition.HealthPercentThreshold);

                case EffectConditionType.MinimumAdjacentAllies:
                    return CountAdjacentAllies(session, caster)
                        >= condition.IntegerThreshold;

                case EffectConditionType.MinimumResourceSpent:
                    return caster != null
                        && caster.LastSkillResourceSpent >= condition.IntegerThreshold;

                case EffectConditionType.TargetHasActiveShield:
                    return target?.Character?.ShieldPool?.TotalShield > 0;

                default:
                    return false;
            }
        }

        private static bool TryGetReferencedEffectResult(
            SkillEffectConditionData condition,
            BattleUnit target,
            IReadOnlyDictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>
                resultsByEffectIndex,
            out EffectExecutionResult result)
        {
            result = EffectExecutionResult.None;

            if (condition == null
                || resultsByEffectIndex == null
                || !resultsByEffectIndex.TryGetValue(
                    condition.ReferencedEffectIndex,
                    out var resultsForEffect)
                || resultsForEffect == null)
            {
                return false;
            }

            return resultsForEffect.TryGetValue(target, out result);
        }

        private static bool HasStatus(
            BattleUnit unit,
            string statusId)
        {
            if (unit?.Character?.PassiveEffects == null
                || string.IsNullOrWhiteSpace(statusId))
            {
                return false;
            }

            foreach (var passiveEffect
                in unit.Character.PassiveEffects)
            {
                if (passiveEffect != null
                    && string.Equals(
                        passiveEffect.EffectId,
                        statusId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsHealthBelowPercent(BattleUnit unit, float threshold)
        {
            if (unit?.Character?.Stats == null) return false;

            var maxHealth = unit.Character.Stats.MaxHealth.GetValue();

            if (maxHealth <= 0f) return false;

            var currentPercent = unit.Character.CurrentHealth / maxHealth;

            return currentPercent < Mathf.Clamp01(threshold);
        }

        private static int CountAdjacentAllies(CombatSession session, BattleUnit unit)
        {
            if (session?.Units == null || unit == null) return 0;
            var count = 0;
            foreach (var candidate in session.Units)
            {
                if (candidate == null || candidate == unit
                    || candidate.Character?.LifeState != CharacterLifeState.Alive
                    || candidate.IsPlayerSide != unit.IsPlayerSide)
                {
                    continue;
                }

                if (Mathf.Max(Mathf.Abs(candidate.Position.X - unit.Position.X),
                        Mathf.Abs(candidate.Position.Y - unit.Position.Y)) == 1)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ConditionRequiresStatusId(
            EffectConditionType conditionType)
        {
            return conditionType == EffectConditionType.TargetHasStatus 
                || conditionType == EffectConditionType.CasterHasStatus;
        }

        private static bool ConditionRequiresEffectReference(
            EffectConditionType conditionType)
        {
            return conditionType
                == EffectConditionType.ReferencedEffectSucceeded
                || conditionType
                == EffectConditionType.ReferencedEffectDealtHealthDamage;
        }

        private static bool RequiresHitRoll(SkillEffectData effect)
        {
            if (effect == null || effect.AlwaysHits)
            {
                return false;
            }

            if (effect.EffectType == EffectType.DealDamage)
            {
                return !CombatDamageSystem.IsGuaranteedHitDamageType(
                    effect.DamageType);
            }

            return effect.Polarity == EffectPolarity.Negative;
        }

        private bool ResolveTargetHit(
            BattleUnit caster,
            BattleUnit target,
            SkillData skill,
            Dictionary<BattleUnit, bool> hitByTarget)
        {
            if (hitByTarget.TryGetValue(target, out var wasHit))
            {
                return wasHit;
            }

            wasHit = CombatDamageSystem.RollHit(
                caster,
                target,
                skill.SkillAccuracy);

            hitByTarget.Add(target, wasHit);

            if (!wasHit)
            {
                battleLog?.Invoke(
                    $"{caster.Character.Definition.DisplayName} chybia "
                    + $"{target.Character.Definition.DisplayName}.");
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    target,
                    CombatFeedbackType.Miss));
            }

            return wasHit;
        }

        private struct EffectExecutionResult
        {
            public static EffectExecutionResult None => new();
            public bool Succeeded;
            public int HealthDamage;
            public bool WasCritical;
            public int HealthRestored;
            public int ResourceRestored;
            public int ShieldGranted;
        }

        private static bool IsOffensiveSkill(SkillData skill)
        {
            if (skill?.TargetModes == null || skill.Effects == null)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect == null
                    || effect.TargetModeIndex < 0
                    || effect.TargetModeIndex >= skill.TargetModes.Count)
                {
                    continue;
                }

                var targetMode = skill.TargetModes[effect.TargetModeIndex];

                if (targetMode != null
                    && targetMode.TargetType != TargetType.Caster
                    && targetMode.TargetingType == TargetingType.Enemies)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMobilityEffect(SkillData skill)
        {
            if (skill?.Effects == null) return false;

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType == EffectType.Charge
                    || effect?.EffectType == EffectType.Teleport) return true;
            }

            return false;
        }

        private static bool ContainsTarget(
            IReadOnlyList<BattleUnit> targets,
            BattleUnit requiredTarget)
        {
            if (targets == null || requiredTarget == null)
            {
                return false;
            }

            foreach (var target in targets)
            {
                if (ReferenceEquals(target, requiredTarget))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanResolveWithoutUnitTargets(
            SkillTargetModeData targetMode)
        {
            return targetMode?.TargetType == TargetType.AreaAroundTile
                || targetMode?.TargetType == TargetType.AreaAroundCaster;
        }

        private bool CanExecute(
            CombatSession session,
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex,
            GridPosition? teleportDestination,
            GridPosition? objectPlacementDestination,
            bool isToggleDeactivation)
        {
            if (session == null || caster?.Character == null
                || skill == null || targetsByModeIndex == null
                || caster.Character.LifeState != CharacterLifeState.Alive)
            {
                return false;
            }

            if (StatusEffectSystem.IsImmobilized(caster)
                && ContainsMobilityEffect(skill))
            {
                battleLog?.Invoke(
                    $"{caster.Character.Definition.DisplayName} cannot use a mobility skill while immobilized.");

                return false;
            }

            if (StatusEffectSystem.IsHardCrowdControlled(caster))
            {
                battleLog?.Invoke(
                    $"{caster.Character.Definition.DisplayName} cannot use skills while crowd controlled.");

                return false;
            }

            if (session.Outcome != CombatOutcomeState.Ongoing
                || session.ActiveUnit != caster
                || !session.Units.Contains(caster))
            {
                return false;
            }

            if (skill.Effects == null || skill.Effects.Count == 0)
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' has no effects.");
                return false;
            }

            if (!isToggleDeactivation
                && skill.IsToggle
                && !ToggleSkillRules.IsValid(skill))
            {
                Debug.LogWarning(
                    $"Toggle skill '{skill.SkillId}' contains an unsupported effect.");
                return false;
            }

            if (!isToggleDeactivation && !ScoutKnifeSystem.CanPay(caster, skill))
            {
                battleLog?.Invoke("Brak dostępnych sztyletów.");
                return false;
            }

            var isObjectPlacementOnlySkill = IsObjectPlacementOnlySkill(skill);

            if (ContainsEffectType(skill, EffectType.CreateObject)
                && ContainsEffectType(skill, EffectType.Summon))
            {
                Debug.LogWarning(
                    $"Skill '{skill.SkillId}' mixes CreateObject and Summon. " +
                    "This is not supported in v1.");
                return false;
            }

            if (!isToggleDeactivation
                && skill.IsToggle
                && caster.ToggleSkillIdsUsedThisTurn != null
                && caster.ToggleSkillIdsUsedThisTurn.Contains(skill.SkillId))
            {
                battleLog?.Invoke("Toggle can be activated only once per turn.");
                return false;
            }

            if (isToggleDeactivation) return true;

            if (!isObjectPlacementOnlySkill
                && (skill.TargetModes == null || skill.TargetModes.Count == 0))
            {
                Debug.LogWarning($"Skill '{skill.SkillId}' has no target modes.");
                return false;
            }

            if (!isObjectPlacementOnlySkill)
            {
                var hasTaunter = StatusEffectSystem.TryGetTaunter(
                    session,
                    caster,
                    out var taunter);

                for (var targetModeIndex = 0;
                    targetModeIndex < skill.TargetModes.Count;
                    targetModeIndex++)
                {
                    var targetMode = skill.TargetModes[targetModeIndex];

                    if (targetMode == null)
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' has an empty target mode.");

                        return false;
                    }

                    if (!targetsByModeIndex.TryGetValue(
                            targetModeIndex,
                            out var targets)
                        || targets == null
                        || (!CanResolveWithoutUnitTargets(targetMode)
                            && targets.Count == 0))
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' has no targets for target mode {targetModeIndex}.");

                        return false;
                    }

                    if (hasTaunter
                        && targetMode.TargetingType == TargetingType.Enemies
                        && !ContainsTarget(targets, taunter))
                    {
                        battleLog?.Invoke(
                            $"{caster.Character.Definition.DisplayName} must include " +
                            $"{taunter.Character.Definition.DisplayName} because of Taunt.");

                        return false;
                    }

                    var canTargetDowned =
                        CanTargetModeIncludeDownedTargets(
                            skill,
                            targetModeIndex);

                    foreach (var target in targets)
                    {
                        if (target?.Character == null
                            || !session.Units.Contains(target)
                            || target.Character.LifeState == CharacterLifeState.Dead)
                        {
                            return false;
                        }

                        if (targetMode.TargetType == TargetType.Single
                            && StatusEffectSystem.IsInvisible(target)
                            && target.IsPlayerSide != caster.IsPlayerSide)
                        {
                            return false;
                        }

                        if (targetMode.TargetType == TargetType.All
                            && StatusEffectSystem.IsInvisible(target))
                        {
                            return false;
                        }

                        if (StatusEffectSystem.IsParalyzed(target))
                        {
                            battleLog?.Invoke(
                                $"{target.Character.Definition.DisplayName} cannot be targeted while paralyzed.");

                            return false;
                        }

                        if (skill.RequiresLineOfSight
                            && !lineOfSightService.HasLineOfSight(
                                session,
                                caster.Position,
                                target.Position))
                        {
                            battleLog?.Invoke("No line of sight to target.");
                            return false;
                        }

                        if (target.Character.LifeState == CharacterLifeState.Downed
                            && !canTargetDowned)
                        {
                            return false;
                        }
                    }
                }

                foreach (var targetModeIndex in targetsByModeIndex.Keys)
                {
                    if (targetModeIndex < 0
                        || targetModeIndex >= skill.TargetModes.Count)
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' received an unknown target mode index {targetModeIndex}.");

                        return false;
                    }
                }
            }

            var actionCost =
                CombatActionCostCalculator.CalculateSkillCost(caster, skill);

            if (caster.CurrentActionPoints < actionCost)
            {
                battleLog?.Invoke("Not enough AP.");
                return false;
            }

            if (skill.ResourceCost > 0
                && caster.Character.ResourcePool == null)
            {
                Debug.LogWarning(
                    $"Skill '{skill.SkillId}' requires a resource pool.");

                return false;
            }

            if (skill.ResourceCost > 0
                && caster.Character.ResourcePool.ResourceType != skill.ResourceType)
            {
                Debug.LogWarning(
                    $"Skill '{skill.SkillId}' uses an incompatible resource type.");

                return false;
            }

            if (caster.Character.ResourcePool != null
                && caster.Character.ResourcePool.CurrentValue < skill.ResourceCost)
            {
                battleLog?.Invoke("Not enough resource.");
                return false;
            }

            for (var effectIndex = 0;
                effectIndex < skill.Effects.Count;
                effectIndex++)
            {
                var effect = skill.Effects[effectIndex];

                if (effect == null)
                {
                    Debug.LogWarning(
                        $"Skill '{skill.SkillId}' contains an empty effect.");

                    return false;
                }

                if (effect.EffectType == EffectType.CreateObject)
                {
                    if (!isObjectPlacementOnlySkill)
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' mixes CreateObject with normal target effects. " +
                            "This is not supported in v1.");

                        return false;
                    }

                    if (effect.SummonedObjectDefinition == null
                        || !objectPlacementDestination.HasValue
                        || !IsValidObjectPlacement(
                            session,
                            caster,
                            skill,
                            effect,
                            objectPlacementDestination.Value))
                    {
                        return false;
                    }

                    if (effect.Conditions != null
                        && effect.Conditions.Count > 0)
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' uses conditions on CreateObject. " +
                            "This is not supported in v1.");

                        return false;
                    }

                    continue;
                }

                if (effect.EffectType == EffectType.Summon)
                {
                    if (!isObjectPlacementOnlySkill
                        || effect.SummonedCharacterDefinition == null
                        || effect.SummonedClassData == null
                        || !objectPlacementDestination.HasValue
                        || !IsValidSummonPlacement(
                            session,
                            caster,
                            skill,
                            effect,
                            objectPlacementDestination.Value)
                        || (effect.Conditions != null
                            && effect.Conditions.Count > 0))
                    {
                        return false;
                    }

                    continue;
                }

                if (skill.TargetModes == null
                    || effect.TargetModeIndex < 0
                    || effect.TargetModeIndex >= skill.TargetModes.Count)
                {
                    Debug.LogWarning(
                        $"Skill '{skill.SkillId}' has an effect with an invalid target mode index.");

                    return false;
                }

                if (effect.EffectType == EffectType.ModifyStat
                    && Mathf.Approximately(effect.ModifierValue, 0f))
                {
                    Debug.LogWarning(
                        $"Skill '{skill.SkillId}' has ModifyStat with value 0.");

                    return false;
                }

                if (effect.EffectType == EffectType.ModifyVar
                    && Mathf.Approximately(effect.ModifierValue, 0f))
                {
                    Debug.LogWarning(
                        $"Skill '{skill.SkillId}' has ModifyVar with value 0.");

                    return false;
                }

                if (effect.EffectType == EffectType.ApplyStatus
                    && string.IsNullOrWhiteSpace(effect.StatusId))
                {
                    Debug.LogWarning(
                        $"Skill '{skill.SkillId}' has ApplyStatus without a status ID.");

                    return false;
                }

                if (effect.EffectType == EffectType.Charge)
                {
                    var targets = targetsByModeIndex[effect.TargetModeIndex];

                    if (targets.Count != 1
                        || !chargeSystem.CanStartCharge(
                            session,
                            caster,
                            targets[0],
                            effect))
                    {
                        return false;
                    }
                }

                if (effect.EffectType == EffectType.Teleport)
                {
                    var targets = targetsByModeIndex[effect.TargetModeIndex];

                    if (targets.Count != 1
                        || !teleportDestination.HasValue
                        || !teleportSystem.CanTeleport(
                            session,
                            caster,
                            targets[0],
                            teleportDestination.Value,
                            skill.RequiresLineOfSight))
                    {
                        return false;
                    }
                }

                if (effect.Conditions == null)
                {
                    continue;
                }

                foreach (var condition in effect.Conditions)
                {
                    if (condition == null)
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' contains an empty effect condition.");

                        return false;
                    }

                    if (ConditionRequiresStatusId(condition.ConditionType)
                        && string.IsNullOrWhiteSpace(condition.StatusId))
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' has a status condition without a status ID.");

                        return false;
                    }

                    if (ConditionRequiresEffectReference(condition.ConditionType)
                        && (condition.ReferencedEffectIndex < 0
                            || condition.ReferencedEffectIndex >= effectIndex))
                    {
                        Debug.LogWarning(
                            $"Skill '{skill.SkillId}' has an invalid referenced effect index " +
                            $"{condition.ReferencedEffectIndex} in effect {effectIndex}.");

                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsValidObjectPlacement(
            CombatSession session,
            BattleUnit caster,
            SkillData skill,
            SkillEffectData effect,
            GridPosition position)
        {
            if (session?.Grid == null
                || caster == null
                || effect?.SummonedObjectDefinition == null)
            {
                return false;
            }

            if (!session.Grid.IsWalkable(position)
                || session.Grid.IsBlocked(position)
                || session.IsPositionOccupiedByMapObject(position))
            {
                return false;
            }

            if (!CombatTargetingService.IsPositionWithinRange(
                    caster.Position,
                    position,
                    effect.ObjectPlacementRange))
            {
                return false;
            }

            if (skill.RequiresLineOfSight
                && !lineOfSightService.HasLineOfSight(
                    session,
                    caster.Position,
                    position))
            {
                return false;
            }

            return true;
        }

        private void ApplyDamageFollowUps(
            BattleUnit caster, BattleUnit target, SkillData skill, SkillEffectData effect,
            int effectIndex, int healthDamage)
        {
            if (healthDamage <= 0) return;
            if (effect.LifeStealPercent > 0f)
                recoverySystem.Heal(caster, Mathf.FloorToInt(healthDamage * effect.LifeStealPercent));
            if (effect.ResourceDrainPercent > 0f)
            {
                var drained = target.Character.ResourcePool?.Drain(
                    Mathf.FloorToInt(healthDamage * effect.ResourceDrainPercent)) ?? 0;
                if (drained > 0) recoverySystem.RestoreResource(caster, drained);
            }
        }

        private bool ApplyUniqueBuff(CombatSession session, BattleUnit caster,
            BattleUnit target, SkillData skill, SkillEffectData effect, int effectIndex)
        {
            switch (effect.UniqueBuffType)
            {
                case UniqueBuffType.ActionPointsNextTurn:
                    return statusEffectSystem != null
                        && statusEffectSystem.ApplyActionPointBuff(caster, target,
                            skill, effect, effectIndex);
                case UniqueBuffType.OnHitStatus:
                    return statusEffectSystem != null
                        && statusEffectSystem.ApplyOnHitStatusBuff(caster, target,
                            skill, effect, effectIndex);
                case UniqueBuffType.DamageRedirect:
                    return statusEffectSystem != null
                        && statusEffectSystem.ApplyDamageRedirectBuff(caster, target,
                            skill, effect, effectIndex);
                case UniqueBuffType.AuraStatus:
                    var wasRegistered = CombatAuraSystem.Register(caster, skill,
                        effect, effectIndex);
                    if (wasRegistered) CombatAuraSystem.Refresh(session, statusEffectSystem);
                    return wasRegistered;
                default:
                    return statusEffectSystem != null
                        && statusEffectSystem.ApplyUniqueCombatBuff(caster, target,
                            skill, effect, effectIndex);
            }
        }

        private void DeactivateToggle(
            CombatSession session,
            BattleUnit caster,
            string skillId,
            bool markUsedThisTurn,
            bool writeBattleLog)
        {
            if (session?.Units == null || caster?.Character == null
                || string.IsNullOrWhiteSpace(skillId))
            {
                return;
            }

            var sourcePrefix = $"{skillId}.effect.";
            foreach (var target in session.Units)
            {
                if (target?.Character?.PassiveEffects == null) continue;
                for (var effectIndex = target.Character.PassiveEffects.Count - 1;
                    effectIndex >= 0;
                    effectIndex--)
                {
                    var effect = target.Character.PassiveEffects[effectIndex];
                    if (effect?.Sources == null) continue;

                    effect.Sources.RemoveAll(source => source != null
                        && string.Equals(
                            source.SourceCharacterId,
                            caster.Character.CharacterId,
                            StringComparison.OrdinalIgnoreCase)
                        && source.SourceId.StartsWith(
                            sourcePrefix,
                            StringComparison.OrdinalIgnoreCase));
                    if (effect.Sources.Count == 0)
                    {
                        target.Character.PassiveEffects.RemoveAt(effectIndex);
                    }
                }

                target.Character.ShieldPool?.RemoveLayersBySourceId(skillId);
            }

            CombatAuraSystem.Unregister(session, caster, skillId);

            caster.ActiveToggleSkillIds?.Remove(skillId);
            if (markUsedThisTurn)
            {
                caster.ToggleSkillIdsUsedThisTurn ??= new HashSet<string>();
                caster.ToggleSkillIdsUsedThisTurn.Add(skillId);
            }

            CombatAuraSystem.Refresh(session, statusEffectSystem);
            CombatTurnSystem.RefreshMaxActionPoints(session);

            if (writeBattleLog)
            {
                battleLog?.Invoke(
                    $"{caster.Character.Definition?.DisplayName ?? caster.Character.CharacterId} "
                    + $"wyłącza {skillId}.");
            }
        }

        public void ProcessToggleUpkeep(CombatSession session, BattleUnit caster)
        {
            if (session?.Units == null || caster?.Character == null
                || caster.ActiveToggleSkillIds == null
                || caster.ActiveToggleSkillIds.Count == 0)
            {
                return;
            }

            var activeSkills = caster.Character.GetActiveSkills();
            var activeIds = new List<string>(caster.ActiveToggleSkillIds);
            foreach (var skillId in activeIds)
            {
                var skill = FindSkill(activeSkills, skillId);
                var resourcePool = caster.Character.ResourcePool;
                var upkeepCost = ToggleSkillUpkeepCalculator.Calculate(
                    skill,
                    resourcePool);
                var hasConfiguredUpkeep = skill != null
                    && (skill.ToggleUpkeepFlatResourceCost > 0
                        || skill.ToggleUpkeepMaxResourcePercent > 0f);

                if (skill == null
                    || (hasConfiguredUpkeep
                        && (resourcePool == null
                            || resourcePool.CurrentValue < upkeepCost)))
                {
                    DeactivateToggle(
                        session,
                        caster,
                        skillId,
                        markUsedThisTurn: false,
                        writeBattleLog: false);
                    battleLog?.Invoke(
                        $"{caster.Character.Definition?.DisplayName ?? caster.Character.CharacterId} "
                        + $"nie może opłacić podtrzymania {skill?.DisplayName ?? skillId}; "
                        + "toggle zostaje wyłączony.");
                    continue;
                }

                if (upkeepCost <= 0) continue;

                var spentResource = resourcePool.Drain(upkeepCost);
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    caster,
                    CombatFeedbackType.ResourceSpend,
                    spentResource,
                    resourceType: resourcePool.ResourceType));
                battleLog?.Invoke(
                    $"{caster.Character.Definition?.DisplayName ?? caster.Character.CharacterId} "
                    + $"płaci {spentResource} {resourcePool.ResourceType} "
                    + $"za podtrzymanie {skill.DisplayName}.");
            }
        }

        public void DeactivateAllToggles(CombatSession session, BattleUnit caster)
        {
            if (caster?.ActiveToggleSkillIds == null) return;

            var activeIds = new List<string>(caster.ActiveToggleSkillIds);
            foreach (var skillId in activeIds)
            {
                DeactivateToggle(
                    session,
                    caster,
                    skillId,
                    markUsedThisTurn: false,
                    writeBattleLog: false);
            }
        }

        private static bool IsToggleActive(BattleUnit caster, SkillData skill)
        {
            return skill?.IsToggle == true
                && caster?.ActiveToggleSkillIds != null
                && caster.ActiveToggleSkillIds.Contains(skill.SkillId);
        }

        private static SkillData FindSkill(
            IReadOnlyList<SkillData> skills,
            string skillId)
        {
            if (skills == null || string.IsNullOrWhiteSpace(skillId)) return null;

            foreach (var skill in skills)
            {
                if (skill != null && string.Equals(
                        skill.SkillId,
                        skillId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return skill;
                }
            }

            return null;
        }

        public void ProcessScheduledEffects(
            CombatSession session,
            BattleUnit caster)
        {
            if (session?.ScheduledEffects == null || caster?.Character == null
                || caster.Character.LifeState == CharacterLifeState.Dead)
            {
                return;
            }

            for (var index = session.ScheduledEffects.Count - 1; index >= 0; index--)
            {
                var scheduled = session.ScheduledEffects[index];
                if (scheduled == null || scheduled.CasterCharacterId
                    != caster.Character.CharacterId)
                {
                    continue;
                }

                scheduled.RemainingCasterTurns--;
                if (scheduled.RemainingCasterTurns > 0)
                {
                    continue;
                }

                session.ScheduledEffects.RemoveAt(index);
                ExecuteScheduledEffect(session, caster, scheduled);
            }
        }

        private void TryExecuteCounterattack(
            CombatSession session,
            BattleUnit defender,
            BattleUnit attacker)
        {
            if (!reactionSystem.TryConsumeCounterattack(
                    defender,
                    attacker,
                    out var reactionSkill,
                    out var reactionEffects))
            {
                return;
            }

            if (reactionEffects == null)
            {
                return;
            }

            var hitByTarget = new Dictionary<BattleUnit, bool>();
            var resultsByEffectIndex =
                new Dictionary<int, Dictionary<BattleUnit, EffectExecutionResult>>();

            for (var effectIndex = 0;
                effectIndex < reactionEffects.Count;
                effectIndex++)
            {
                var effect = reactionEffects[effectIndex];
                if (effect == null
                    || attacker.Character.LifeState == CharacterLifeState.Dead)
                {
                    continue;
                }

                if (!ShouldExecuteEffect(session, effect, defender, attacker, resultsByEffectIndex))
                {
                    resultsByEffectIndex[effectIndex] =
                        new Dictionary<BattleUnit, EffectExecutionResult>
                        {
                            [attacker] = EffectExecutionResult.None
                        };
                    continue;
                }

                var wasHit = effect.EffectType == EffectType.DealDamage
                    || !RequiresHitRoll(effect)
                    || ResolveTargetHit(defender, attacker, reactionSkill, hitByTarget);
                var result = wasHit
                    ? ExecuteEffect(
                        session,
                        defender,
                        attacker,
                        reactionSkill,
                        effect,
                        effectIndex,
                        wasHit,
                        teleportDestination: null,
                        isReactionExecution: true)
                    : EffectExecutionResult.None;

                resultsByEffectIndex[effectIndex] =
                    new Dictionary<BattleUnit, EffectExecutionResult>
                    {
                        [attacker] = result
                    };
            }
        }

        public int CancelScheduledEffectsByCaster(
            CombatSession session,
            string casterCharacterId)
        {
            if (session?.ScheduledEffects == null
                || string.IsNullOrWhiteSpace(casterCharacterId))
            {
                return 0;
            }

            return session.ScheduledEffects.RemoveAll(effect => effect != null
                && string.Equals(effect.CasterCharacterId, casterCharacterId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private bool IsValidSummonPlacement(
            CombatSession session,
            BattleUnit caster,
            SkillData skill,
            SkillEffectData effect,
            GridPosition position)
        {
            if (session?.Grid == null
                || caster == null
                || effect?.SummonedCharacterDefinition == null
                || effect.SummonedClassData == null)
            {
                return false;
            }

            if (!session.Grid.IsWalkable(position)
                || session.Grid.IsBlocked(position)
                || session.IsPositionOccupied(position)
                || session.IsPositionOccupiedByMapObject(position))
            {
                return false;
            }

            if (!CombatTargetingService.IsPositionWithinRange(
                    caster.Position,
                    position,
                    effect.SummonPlacementRange))
            {
                return false;
            }

            return !skill.RequiresLineOfSight
                || lineOfSightService.HasLineOfSight(
                    session,
                    caster.Position,
                    position);
        }

        private static bool IsObjectPlacementOnlySkill(SkillData skill)
        {
            if (skill?.Effects == null || skill.Effects.Count == 0)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType != EffectType.CreateObject
                    && effect?.EffectType != EffectType.Summon)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsEffectType(
            SkillData skill,
            EffectType effectType)
        {
            if (skill?.Effects == null)
            {
                return false;
            }

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType == effectType)
                {
                    return true;
                }
            }

            return false;
        }
        
        private static bool CanTargetModeIncludeDownedTargets(
            SkillData skill,
            int targetModeIndex)
        {
            if (skill?.Effects == null || targetModeIndex < 0) return false;

            foreach (var effect in skill.Effects)
            {
                if (effect == null || effect.TargetModeIndex != targetModeIndex) continue;

                if (effect.EffectType == EffectType.Heal
                    || effect.EffectType == EffectType.Teleport) return true;
            }

            return false;
        }
        private enum ForcedDisplacementDirection
        {
            AwayFromReference,
            TowardsReference
        }

        private bool TryApplyForcedDisplacement(
            CombatSession session,
            BattleUnit movedUnit,
            BattleUnit referenceUnit,
            int displacementDistance,
            ForcedDisplacementDirection direction,
            string actionDescription)
        {
            if (session?.Grid == null || movedUnit == null || referenceUnit == null)
            {
                return false;
            }

            var directionX = movedUnit.Position.X - referenceUnit.Position.X;
            var directionY = movedUnit.Position.Y - referenceUnit.Position.Y;

            if (direction == ForcedDisplacementDirection.TowardsReference)
            {
                directionX = -directionX;
                directionY = -directionY;
            }

            if (directionX == 0 && directionY == 0)
            {
                battleLog?.Invoke(
                    $"Nie można określić kierunku przesunięcia dla "
                    + $"{movedUnit.Character.Definition.DisplayName}.");

                return false;
            }

            var requestedDistance = Mathf.Max(1, displacementDistance);
            var dominantAxisLength = Mathf.Max(
                Mathf.Abs(directionX),
                Mathf.Abs(directionY));

            var stepRatioX = directionX / (float)dominantAxisLength;
            var stepRatioY = directionY / (float)dominantAxisLength;

            var previousOffsetX = 0;
            var previousOffsetY = 0;
            var movedTiles = 0;

            for (var stepIndex = 1; stepIndex <= requestedDistance; stepIndex++)
            {
                var offsetX = Mathf.RoundToInt(stepRatioX * stepIndex);
                var offsetY = Mathf.RoundToInt(stepRatioY * stepIndex);

                var stepX = offsetX - previousOffsetX;
                var stepY = offsetY - previousOffsetY;

                previousOffsetX = offsetX;
                previousOffsetY = offsetY;

                if (stepX == 0 && stepY == 0)
                {
                    continue;
                }

                var nextPosition = new GridPosition(
                    movedUnit.Position.X + stepX,
                    movedUnit.Position.Y + stepY);

                if (!CanForcedDisplacementMove(
                        session,
                        movedUnit,
                        nextPosition,
                        stepX,
                        stepY))
                {
                    break;
                }

                movedUnit.Position = nextPosition;
                CombatAuraSystem.Refresh(session, statusEffectSystem);
                movedTiles++;
            }

            if (movedTiles <= 0)
            {
                battleLog?.Invoke(
                    $"{movedUnit.Character.Definition.DisplayName} nie może zostać "
                    + $"{actionDescription}.");

                return false;
            }

            battleLog?.Invoke(
                $"{movedUnit.Character.Definition.DisplayName} zostaje "
                + $"{actionDescription} o {movedTiles} pole/pola.");

            return true;
        }

        private static bool CanForcedDisplacementMove(
            CombatSession session,
            BattleUnit movedUnit,
            GridPosition nextPosition,
            int stepX,
            int stepY)
        {
            var currentPosition = movedUnit.Position;
            var isDiagonal = stepX != 0 && stepY != 0;

            if (!isDiagonal)
            {
                return session.Grid.CanMoveBetween(currentPosition, nextPosition)
                    && !session.IsPositionOccupied(nextPosition, movedUnit);
            }

            return session.Grid.IsWalkable(currentPosition)
                && session.Grid.IsWalkable(nextPosition)
                && !session.Grid.IsBlocked(currentPosition)
                && !session.Grid.IsBlocked(nextPosition)
                && !session.IsPositionOccupied(nextPosition, movedUnit);
        }
    }

    /// <summary>
    /// Keeps auras combat-scoped and recomputable from positions. The aura owns only
    /// its own status instances (identified by AuraId), so re-evaluation cannot remove
    /// an identical status applied by another skill.
    /// </summary>
    public static class CombatAuraSystem
    {
        public static int Unregister(
            CombatSession session,
            BattleUnit caster,
            string skillId)
        {
            if (caster?.ActiveAuras == null || string.IsNullOrWhiteSpace(skillId))
            {
                return 0;
            }

            var auraPrefix = $"{skillId}.aura.";
            var removedAuras = caster.ActiveAuras.FindAll(aura => aura?.AuraId != null
                && aura.AuraId.StartsWith(auraPrefix,
                    StringComparison.OrdinalIgnoreCase));

            foreach (var aura in removedAuras)
            {
                foreach (var unit in session?.Units ?? new List<BattleUnit>())
                {
                    StatusEffectSystem.RemoveStatusAppliedBySource(
                        unit,
                        aura.StatusId,
                        aura.AuraId);
                }
            }

            caster.ActiveAuras.RemoveAll(aura => aura?.AuraId != null
                && aura.AuraId.StartsWith(auraPrefix,
                    StringComparison.OrdinalIgnoreCase));
            return removedAuras.Count;
        }

        public static bool Register(BattleUnit caster, SkillData skill,
            SkillEffectData effect, int effectIndex)
        {
            if (caster?.Character == null || effect == null
                || string.IsNullOrWhiteSpace(effect.StatusId))
            {
                return false;
            }

            caster.ActiveAuras ??= new List<CombatAuraRuntime>();
            var auraId = $"{skill?.SkillId ?? "aura"}.aura.{effectIndex}";
            caster.ActiveAuras.RemoveAll(aura => string.Equals(aura?.AuraId,
                auraId, StringComparison.OrdinalIgnoreCase));
            caster.ActiveAuras.Add(new CombatAuraRuntime
            {
                AuraId = auraId,
                StatusId = effect.StatusId,
                Radius = effect.AuraRadius,
                IncludesCaster = effect.AuraIncludesCaster,
                TargetingType = effect.AuraTargetingType,
                Chance = effect.BaseChance,
                DurationTurns = Mathf.Max(1, effect.DurationTurns)
            });
            return true;
        }

        public static void Refresh(CombatSession session,
            StatusEffectSystem statusEffectSystem)
        {
            if (session?.Units == null || statusEffectSystem == null) return;

            foreach (var source in session.Units)
            {
                if (source?.Character == null || source.ActiveAuras == null) continue;

                foreach (var aura in source.ActiveAuras)
                {
                    if (aura == null || string.IsNullOrWhiteSpace(aura.StatusId)) continue;

                    foreach (var candidate in session.Units)
                    {
                        StatusEffectSystem.RemoveStatusAppliedBySource(
                            candidate, aura.StatusId, aura.AuraId);
                    }

                    if (source.Character.LifeState != CharacterLifeState.Alive) continue;

                    foreach (var target in session.Units)
                    {
                        if (target?.Character == null
                            || target.Character.LifeState != CharacterLifeState.Alive
                            || !MatchesTargeting(source, target, aura.TargetingType)
                            || (!aura.IncludesCaster && target == source)
                            || !IsInRange(source.Position, target.Position, aura.Radius))
                        {
                            continue;
                        }

                        statusEffectSystem.ApplyStatus(source, target, aura.StatusId,
                            aura.Chance, aura.DurationTurns, 1, 0f, 0f, aura.AuraId);
                    }
                }
            }
        }

        private static bool IsInRange(GridPosition source, GridPosition target, int radius)
        {
            return Mathf.Max(Mathf.Abs(source.X - target.X),
                Mathf.Abs(source.Y - target.Y)) <= Mathf.Max(0, radius);
        }

        private static bool MatchesTargeting(BattleUnit source, BattleUnit target,
            TargetingType targetingType)
        {
            return targetingType switch
            {
                TargetingType.Allies => source.IsPlayerSide == target.IsPlayerSide,
                TargetingType.Enemies => source.IsPlayerSide != target.IsPlayerSide,
                TargetingType.All => true,
                _ => false
            };
        }
    }
}
