using System;
using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using UnityEngine;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using Godmist.Data.Configuration;
using System.Linq;

namespace Godmist.Systems.Combat
{
    public enum SimpleCombatAiActionType
    {
        Movement,
        Skill,
        Interaction
    }

    public readonly struct SimpleCombatAiAction
    {
        public SimpleCombatAiAction(
            SimpleCombatAiActionType type,
            SkillData skill = null,
            IReadOnlyList<GridPosition> movementPath = null)
        {
            Type = type;
            Skill = skill;
            MovementPath = movementPath;
        }

        public SimpleCombatAiActionType Type { get; }
        public SkillData Skill { get; }
        public IReadOnlyList<GridPosition> MovementPath { get; }
    }

    public sealed class SimpleCombatAiTurnState
    {
        internal readonly HashSet<SkillData> UsedZeroActionSkills = new();
    }

    public class SimpleCombatAiSystem
    {
        private readonly Action<string> battleLog;
        private readonly Action<CombatSession> endTurn;
        private readonly Action<CombatSession> evaluateOutcome;

        private readonly SkillExecutionSystem skillExecutionSystem;
        private readonly CombatTargetingService targetingService;
        private readonly CombatMovementSystem movementSystem;
        private readonly CombatLineOfSightService lineOfSightService;
        private readonly CombatInteractionSystem interactionSystem;
        private static readonly Vector2Int[] MovementDirections =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
        };

        public SimpleCombatAiSystem(
            Action<string> battleLog = null,
            Action<CombatSession> endTurn = null,
            Action<CombatSession> evaluateOutcome = null,
            SkillExecutionSystem skillExecutionSystem = null,
            CombatTargetingService targetingService = null,
            CombatMovementSystem movementSystem = null,
            CombatLineOfSightService lineOfSightService = null,
            CombatInteractionSystem interactionSystem = null)
        {
            this.battleLog = battleLog;
            this.endTurn = endTurn;
            this.evaluateOutcome = evaluateOutcome;
            this.skillExecutionSystem = skillExecutionSystem;
            this.targetingService = targetingService ?? new CombatTargetingService();
            this.movementSystem = movementSystem ?? new CombatMovementSystem(battleLog);
            this.lineOfSightService = lineOfSightService ?? new CombatLineOfSightService();
            this.interactionSystem = interactionSystem ?? new CombatInteractionSystem(battleLog);
        }

        public void ExecuteSimpleAiTurn(CombatSession session, BattleUnit aiUnit)
        {
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing || aiUnit?.Character == null
                || aiUnit.Character.LifeState != CharacterLifeState.Alive) return;

            var turnState = CreateTurnState();
            while (TryExecuteNextAction(session, aiUnit, turnState, out _))
            {
            }

            if (session.Outcome == CombatOutcomeState.Ongoing) endTurn?.Invoke(session);
        }

        public SimpleCombatAiTurnState CreateTurnState()
        {
            return new SimpleCombatAiTurnState();
        }

        public bool TryExecuteNextAction(
            CombatSession session,
            BattleUnit aiUnit,
            SimpleCombatAiTurnState turnState,
            out SimpleCombatAiAction action)
        {
            action = default;
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing
                || aiUnit?.Character == null
                || aiUnit.Character.LifeState != CharacterLifeState.Alive
                || aiUnit.CurrentActionPoints <= 0)
            {
                return false;
            }

            session.Contact?.Refresh(session);
            turnState ??= CreateTurnState();
            if (TryExecuteBestSkill(
                    session,
                    aiUnit,
                    turnState.UsedZeroActionSkills,
                    out var executedSkill))
            {
                if (CombatActionCostCalculator.CalculateSkillCost(aiUnit, executedSkill) == 0)
                {
                    turnState.UsedZeroActionSkills.Add(executedSkill);
                }

                action = new SimpleCombatAiAction(
                    SimpleCombatAiActionType.Skill,
                    executedSkill);
                return true;
            }

            var hasTaunter = StatusEffectSystem.TryGetTaunter(
                session,
                aiUnit,
                out var taunter);
            var target = hasTaunter && (session.Contact == null || session.Contact.Sees(aiUnit, taunter))
                ? taunter : FindClosestPlayerUnit(session, aiUnit);
            if (target == null)
            {
                if (session.Contact != null && session.Contact.TryGetSearchPosition(session, aiUnit, out var last)
                    && TryFindPathToPosition(session, aiUnit, last, session.IsPositionOccupied(last, aiUnit), out var searchPath)
                    && TryExecuteMovementPath(session, aiUnit, searchPath, out var completed, searching: true))
                {
                    action = new SimpleCombatAiAction(completed == null ? SimpleCombatAiActionType.Interaction
                        : SimpleCombatAiActionType.Movement, movementPath: completed);
                    return true;
                }
                evaluateOutcome?.Invoke(session);
                return false;
            }

            if (!TryMoveAiForRole(session, aiUnit, target, out var movementPath))
            {
                return false;
            }

            action = new SimpleCombatAiAction(
                movementPath == null ? SimpleCombatAiActionType.Interaction : SimpleCombatAiActionType.Movement,
                movementPath: movementPath);
            return true;
        }

        private bool TryExecuteBestSkill(
            CombatSession session,
            BattleUnit aiUnit,
            ISet<SkillData> excludedZeroActionSkills,
            out SkillData executedSkill)
        {
            executedSkill = null;

            if (skillExecutionSystem == null) return false;

            var selection = FindBestSkillSelection(session, aiUnit, excludedZeroActionSkills);

            if (selection == null) return false;

            var wasExecuted = skillExecutionSystem.TryExecute(
                session,
                aiUnit,
                selection.Skill,
                selection.TargetsByModeIndex,
                teleportDestination: null,
                objectPlacementDestination: selection.SummonPlacementDestination);

            if (wasExecuted)
            {
                executedSkill = selection.Skill;
            }

            return wasExecuted;
        }

        private AiSkillSelection FindBestSkillSelection(
            CombatSession session,
            BattleUnit aiUnit,
            ISet<SkillData> excludedZeroActionSkills)
        {
            var activeSkills = aiUnit?.Character?.GetActiveSkills();
            if (activeSkills == null) return null;

            AiSkillSelection bestSelection = null;

            foreach (var skill in activeSkills)
            {
                if (!CanAffordSkill(aiUnit, skill)) continue;

                if (CombatActionCostCalculator.CalculateSkillCost(aiUnit, skill) == 0
                    && excludedZeroActionSkills != null
                    && excludedZeroActionSkills.Contains(skill))
                {
                    continue;
                }

                GridPosition? summonPlacementDestination = null;
                Dictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex;

                if (IsSummonOnlySkill(skill))
                {
                    if (!TryFindSummonPlacement(
                            session,
                            aiUnit,
                            skill.Effects[0],
                            skill.RequiresLineOfSight,
                            out var placement))
                    {
                        continue;
                    }

                    summonPlacementDestination = placement;
                    targetsByModeIndex = new Dictionary<
                        int,
                        IReadOnlyList<BattleUnit>>();
                }
                else if (!TryBuildTargets(
                             session,
                             aiUnit,
                             skill,
                             out targetsByModeIndex))
                {
                    continue;
                }

                var score = CalculateSkillScore(aiUnit, skill, targetsByModeIndex);

                if (score <= 0f) continue;

                if (bestSelection == null || score > bestSelection.Score)
                {
                    bestSelection = new AiSkillSelection
                    {
                        Skill = skill,
                        TargetsByModeIndex = targetsByModeIndex,
                        SummonPlacementDestination = summonPlacementDestination,
                        Score = score
                    };
                }
            }
            return bestSelection;
        }

        private bool TryFindSummonPlacement(
            CombatSession session,
            BattleUnit caster,
            SkillEffectData effect,
            bool requiresLineOfSight,
            out GridPosition position)
        {
            position = default;

            if (session?.Grid == null || caster == null || effect == null)
            {
                return false;
            }

            foreach (var candidate in targetingService.GetPositionsInRange(
                session,
                caster.Position,
                effect.SummonPlacementRange))
            {
                if (!session.Grid.IsWalkable(candidate)
                    || session.Grid.IsBlocked(candidate)
                    || session.IsPositionOccupied(candidate)
                    || session.IsPositionOccupiedByMapObject(candidate)
                    || (requiresLineOfSight && !lineOfSightService.HasLineOfSight(
                        session, caster.Position, candidate)))
                {
                    continue;
                }

                position = candidate;
                return true;
            }

            return false;
        }

        private static bool IsSummonOnlySkill(SkillData skill)
        {
            if (skill?.Effects == null || skill.Effects.Count != 1)
            {
                return false;
            }

            return skill.Effects[0]?.EffectType == EffectType.Summon;
        }

        private bool CanAffordSkill(BattleUnit unit, SkillData skill)
        {
            if (unit?.Character == null || skill == null) return false;

            if (!string.IsNullOrWhiteSpace(skill.RequiredCasterStatusId)
                && !StatusEffectSystem.HasStatus(unit, skill.RequiredCasterStatusId)) return false;

            var actionCost = CombatActionCostCalculator.CalculateSkillCost(unit, skill);

            if (unit.CurrentActionPoints < actionCost || !WarriorCombatSystem.CanActivate(unit,skill)) return false;

            if (PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, skill) <= 0) return true;

            var resourcePool = unit.Character.ResourcePool;

            return resourcePool != null
                && resourcePool.ResourceType == skill.ResourceType
                && (resourcePool.CurrentValue >= PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, skill)
                    || WarriorCombatSystem.CanOverspend(unit,skill,PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit,skill)));
        }

        private bool TryBuildTargets(CombatSession session, BattleUnit caster,
            SkillData skill, out Dictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>();

            if (skill?.TargetModes == null || skill.TargetModes.Count == 0
                || skill.Effects == null || skill.Effects.Count == 0) return false;

            for (var index = 0; index < skill.TargetModes.Count; index++)
            {
                var mode = PassiveEffectCombatExecutionSystem.ResolveMode(caster, skill, skill.TargetModes[index]);

                if (mode == null) return false;

                var requiredEnemyTarget = mode.TargetingType == TargetingType.Enemies
                    && StatusEffectSystem.TryGetTaunter(
                        session,
                        caster,
                        out var taunter)
                    ? taunter
                    : null;

                IReadOnlyList<BattleUnit> targets;

                if (mode.TargetType == TargetType.Single || mode.TargetType == TargetType.AreaAroundTile)
                {
                    if (!TryFindBestTargetPosition(
                            session,
                            caster,
                            skill,
                            mode,
                            index,
                            requiredEnemyTarget,
                            out var selectedPosition)) return false;

                    targets = targetingService.GetTargets(
                        session,
                        caster,
                        mode,
                        selectedPosition,
                        requiredEnemyTarget: requiredEnemyTarget);
                }
                else
                {
                    targets = targetingService.GetTargets(
                        session,
                        caster,
                        mode,
                        null,
                        requiredEnemyTarget: requiredEnemyTarget);
                }

                targets = FilterTargetsByLineOfSight(session, caster, skill, targets);

                if (targets.Count == 0) return false;

                targetsByModeIndex[index] = targets;
            }

            return true;
        }

        private bool TryFindBestTargetPosition(
            CombatSession session,
            BattleUnit caster,
            SkillData skill,
            SkillTargetModeData mode,
            int targetModeIndex,
            BattleUnit requiredEnemyTarget,
            out GridPosition selectedPosition)
        {
            selectedPosition = default;

            var selectablePositions = targetingService.GetSelectablePositions(
                    session,
                    caster,
                    mode,
                    requiredEnemyTarget: requiredEnemyTarget)
                .Where(position => !skill.RequiresLineOfSight
                || lineOfSightService.HasLineOfSight(session, caster.Position, position));

            var bestScore = float.MinValue;
            var foundPosition = false;

            foreach (var position in selectablePositions)
            {
                var targets = targetingService.GetTargets(
                    session,
                    caster,
                    mode,
                    position,
                    requiredEnemyTarget: requiredEnemyTarget);

                targets = FilterTargetsByLineOfSight(session, caster, skill, targets);

                var score = CalculateTargetGroupScore(
                    caster,
                    skill,
                    targetModeIndex,
                    targets);
                if(mode.TargetingType==TargetingType.Enemies && targets.Count>0)score*=targets.Average(t=>WarriorCombatSystem.TargetPriority(session,t));

                if (!foundPosition || score > bestScore)
                {
                    selectedPosition = position;
                    bestScore = score;
                    foundPosition = true;
                }
            }

            return foundPosition;
        }

        private static float CalculateTargetGroupScore(
            BattleUnit caster,
            SkillData skill,
            int targetModeIndex,
            IReadOnlyList<BattleUnit> targets)
        {
            if (skill == null || targets == null || targets.Count == 0) return float.MinValue;

            return CalculateEffectsScore(caster, skill, targetModeIndex, targets);
        }

        private static float CalculateSkillScore(BattleUnit caster, SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            var score = 0f;

            foreach (var effect in skill.Effects)
            {
                if (effect?.EffectType == EffectType.Summon)
                {
                    score += Ai(GameDefineIds.AiSummonScore, 75f);
                    continue;
                }

                if (effect == null || !targetsByModeIndex.TryGetValue(effect.TargetModeIndex, out var targets))
                    continue;

                score += CalculateEffectsScore(caster, skill, effect.TargetModeIndex, targets, effect);

                if (effect.DelayCasterTurns > 0)
                {
                    score += Ai(GameDefineIds.AiDelayedEffectPerTarget, 25f) * targets.Count;
                }
            }

            var actionCost = CombatActionCostCalculator.CalculateSkillCost(caster, skill);

            return score + CalculateRoleBonus(caster, skill, targetsByModeIndex)
                + CalculatePersonalityBonus(caster, skill, targetsByModeIndex)
                - ActionPointUnits.ToPoints(actionCost) * Ai(GameDefineIds.AiActionCostPenaltyPerPoint, .35f)
                - PassiveEffectCombatExecutionSystem.ResolveResourceCost(caster, skill) * Ai(GameDefineIds.AiResourceCostPenaltyPerPoint, .1f);
        }

        private static float CalculatePersonalityBonus(
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            var personality = caster?.Character?.Definition?.AiPersonality
                ?? CombatAiPersonality.Neutral;
            var bonus = 0f;

            foreach (var effect in skill.Effects)
            {
                if (effect == null || !targetsByModeIndex.TryGetValue(
                        effect.TargetModeIndex, out var targets))
                {
                    continue;
                }

                var count = targets.Count;
                bonus += personality switch
                {
                    CombatAiPersonality.Aggressive
                        when effect.EffectType == EffectType.DealDamage =>
                            Ai(GameDefineIds.AiAggressiveDamagePerTarget, 30f) * count,
                    CombatAiPersonality.Aggressive
                        when effect.EffectType == EffectType.ApplyStatus =>
                            Ai(GameDefineIds.AiAggressiveStatusPerTarget, 15f) * count,
                    CombatAiPersonality.Cautious
                        when effect.EffectType == EffectType.Heal
                        || effect.EffectType == EffectType.Shield =>
                            Ai(GameDefineIds.AiCautiousRecoveryPerTarget, 30f) * count,
                    _ => 0f
                };
            }

            return bonus;
        }

        private static float CalculateRoleBonus(
            BattleUnit caster,
            SkillData skill,
            IReadOnlyDictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            var role = caster?.Character?.Definition?.AiRole ?? CombatAiRole.Balanced;
            var bonus = 0f;

            foreach (var effect in skill.Effects)
            {
                if (effect == null || !targetsByModeIndex.TryGetValue(
                        effect.TargetModeIndex, out var targets))
                {
                    continue;
                }

                var count = targets.Count;
                bonus += role switch
                {
                    CombatAiRole.Dps when effect.EffectType == EffectType.DealDamage
                        => Ai(GameDefineIds.AiDpsDamagePerTarget, 60f) * count,
                    CombatAiRole.Dps when effect.EffectType == EffectType.ApplyStatus
                        => Ai(GameDefineIds.AiDpsStatusPerTarget, 20f) * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.Shield
                        => Ai(GameDefineIds.AiTankShieldPerTarget, 35f) * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.ApplyStatus
                        && string.Equals(effect.StatusId, StatusEffectIds.Taunt,
                            StringComparison.OrdinalIgnoreCase) =>
                            Ai(GameDefineIds.AiTankTauntPerTarget, 90f) * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.Push
                        || effect.EffectType == EffectType.Pull =>
                            Ai(GameDefineIds.AiTankPushPullPerTarget, 25f) * count,
                    CombatAiRole.Healer when effect.EffectType == EffectType.Heal
                        || effect.EffectType == EffectType.RestoreResource =>
                        Ai(GameDefineIds.AiHealerRecoveryPerTarget, 70f) * count,
                    CombatAiRole.Support when effect.EffectType == EffectType.Shield
                        || effect.EffectType == EffectType.ModifyStat
                        || effect.EffectType == EffectType.ModifyVar =>
                        Ai(GameDefineIds.AiSupportUtilityPerTarget, 55f) * count,
                    CombatAiRole.Support when effect.EffectType == EffectType.ApplyStatus
                        => Ai(GameDefineIds.AiSupportStatusPerTarget, 40f) * count,
                    _ => 0f
                };
            }

            return bonus;
        }

        private bool TryMoveAiForRole(
            CombatSession session,
            BattleUnit aiUnit,
            BattleUnit target,
            out List<GridPosition> movementPath)
        {
            movementPath = null;
            var role = aiUnit?.Character?.Definition?.AiRole ?? CombatAiRole.Balanced;
            var personality = aiUnit?.Character?.Definition?.AiPersonality
                ?? CombatAiPersonality.Neutral;
            var desiredRange = GetPreferredCombatRange(aiUnit);
            var distance = GetManhattanDistance(aiUnit.Position, target.Position);
            var isSquishyRangedRole = role == CombatAiRole.Dps
                || role == CombatAiRole.Healer;

            if (personality != CombatAiPersonality.Aggressive
                && isSquishyRangedRole && desiredRange > 1
                && distance <= desiredRange
                && lineOfSightService.HasLineOfSight(session, aiUnit.Position, target.Position))
            {
                return distance < desiredRange
                    && TryMoveAiAwayFromTarget(
                        session,
                        aiUnit,
                        target,
                        desiredRange,
                        out movementPath);
            }

            return TryMoveAiTowardsTarget(session, aiUnit, target, out movementPath);
        }

        private static int GetPreferredCombatRange(BattleUnit unit)
        {
            var maxRange = 1;
            foreach (var skill in unit?.Character?.GetActiveSkills()
                         ?? Array.Empty<SkillData>())
            {
                if (skill?.TargetModes == null || skill.Effects == null) continue;
                foreach (var effect in skill.Effects)
                {
                    if (effect == null
                        || effect.TargetModeIndex < 0
                        || effect.TargetModeIndex >= skill.TargetModes.Count)
                    {
                        continue;
                    }

                    var mode = PassiveEffectCombatExecutionSystem.ResolveMode(unit, skill, skill.TargetModes[effect.TargetModeIndex]);
                    if (mode?.TargetingType == TargetingType.Enemies
                        && mode.TargetType != TargetType.Caster
                        && mode.AbsoluteRange != null)
                    {
                        maxRange = Mathf.Max(
                            maxRange,
                            mode.AbsoluteRange.MaxRange);
                    }
                }
            }

            return maxRange;
        }

        private bool TryMoveAiAwayFromTarget(
            CombatSession session,
            BattleUnit aiUnit,
            BattleUnit target,
            int desiredRange,
            out List<GridPosition> movementPath)
        {
            movementPath = null;
            GridPosition? bestPosition = null;
            var bestDistance = GetManhattanDistance(aiUnit.Position, target.Position);

            foreach (var candidate in session.Grid.GetNeighbours(aiUnit.Position))
            {
                var candidateDistance = GetManhattanDistance(candidate, target.Position);
                if ((session.EvaluateTrapRisk?.Invoke(aiUnit, new[] { candidate }) ?? 0f) > 0f
                    || candidateDistance <= bestDistance || candidateDistance > desiredRange
                    || session?.Grid == null || !session.Grid.IsWithinBounds(candidate)
                    || session.Grid.IsBlocked(candidate)
                    || !session.CanMoveBetween(aiUnit, aiUnit.Position, candidate)
                    || session.IsPositionOccupied(candidate, aiUnit)
                    || session.DoesObjectBlockMovement(session.GetMapObjectAt(candidate)))
                {
                    continue;
                }

                bestPosition = candidate;
                bestDistance = candidateDistance;
            }

            if (!bestPosition.HasValue)
            {
                return false;
            }

            movementPath = new List<GridPosition> { bestPosition.Value };
            return movementSystem.TryMoveAlongPath(session, aiUnit, movementPath);
        }

        private static float CalculateEffectsScore(
            BattleUnit caster,
            SkillData skill,
            int targetModeIndex,
            IReadOnlyList<BattleUnit> targets,
            SkillEffectData singleEffect = null)
        {
            var score = 0f;

            foreach (var authoredEffect in skill.Effects)
            {
                if (authoredEffect == null || authoredEffect.TargetModeIndex != targetModeIndex
                    || singleEffect != null && authoredEffect != singleEffect)
                {
                    continue;
                }

                var effect = PassiveEffectCombatExecutionSystem.ResolveEffect(caster, skill, authoredEffect);
                foreach (var target in targets)
                {
                    if (target?.Character == null) continue;

                    switch (effect.EffectType)
                    {
                        case EffectType.DealDamage:
                        {
                            var expectedDamage = CalculateExpectedDamage(caster, effect) * effect.ExpectedHitCount
                                * PassiveEffectCombatExecutionSystem.ResolveParameter(caster, skill, "damage_multiplier", 1f);
                            score += expectedDamage;
                            if (expectedDamage >= target.Character.CurrentHealth)
                            {
                                score += Ai(GameDefineIds.AiLethalDamageBonus, 10000f);
                            }
                            break;
                        }
                        case EffectType.ApplyStatus:
                            score += IsHardControl(effect.StatusId)
                                ? Ai(GameDefineIds.AiHardControlBonus, 300f)
                                : Ai(GameDefineIds.AiSoftControlBonus, 100f);
                            break;
                        case EffectType.Heal:
                            score += GetMissingHealth(target);
                            if (target.Character.LifeState == CharacterLifeState.Downed)
                            {
                                score += Ai(GameDefineIds.AiDownedRecoveryBonus, 5000f);
                            }
                            break;
                        case EffectType.RestoreResource:
                            var resourcePool = target.Character.ResourcePool;
                            if (resourcePool != null)
                            {
                                score += Mathf.Min(effect.BaseValue,
                                    Mathf.Max(0, resourcePool.MaxValue - resourcePool.CurrentValue));
                            }
                            break;
                        case EffectType.Shield:
                            score += Mathf.Max(0, effect.BaseValue)
                                * Ai(GameDefineIds.AiShieldValueMultiplier, .5f);
                            break;
                        case EffectType.ModifyStat:
                        case EffectType.ModifyVar:
                            score += Ai(GameDefineIds.AiModifyBonus, 40f);
                            break;
                        case EffectType.Push:
                        case EffectType.Pull:
                            score += Ai(GameDefineIds.AiPushPullBonus, 30f);
                            break;
                        case EffectType.Fallback:
                            score += Ai(GameDefineIds.AiFallbackBonus, 10f);
                            break;
                    }
                }
            }

            return score;
        }

        private static float CalculateExpectedDamage(BattleUnit caster, SkillEffectData effect)
        {
            if (caster?.Character?.Stats == null || effect == null) return 0f;

            var minAttack = caster.Character.Stats.MinAttack.GetValue();
            var maxAttack = caster.Character.Stats.MaxAttack.GetValue();
            var attackValue = effect.DamageValueSource switch
            {
                DamageValueSource.MinAttack => minAttack,
                DamageValueSource.MaxAttack => maxAttack,
                DamageValueSource.AverageAttack => (minAttack + maxAttack) * 0.5f,
                _ => (minAttack + maxAttack) * 0.5f
            };

            return Mathf.Max(0f, attackValue * effect.DamageMultiplier);
        }

        private static float GetMissingHealth(BattleUnit target)
        {
            if (target?.Character == null) return 0f;

            var maxHealth = target.Character.Stats?.MaxHealth.GetValue()
                ?? target.Character.CurrentHealth;
            return Mathf.Max(0f, maxHealth - target.Character.CurrentHealth);
        }

        private static bool IsHardControl(string statusId)
        {
            return string.Equals(statusId, StatusEffectIds.Stun, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Immobilize, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Freeze, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Sleep, StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusId, StatusEffectIds.Paralysis, StringComparison.OrdinalIgnoreCase);
        }

        private static float Ai(string id, float fallback)
        {
            return GameDefinesRuntime.GetFloat(id, fallback);
        }

        public BattleUnit FindClosestPlayerUnit(CombatSession session, BattleUnit aiUnit)
        {
            BattleUnit closestUnit = null;
            var closestDistance = int.MaxValue;

            foreach (var unit in session.Units)
            {
                if (!unit.IsPlayerSide)
                {
                    continue;
                }

                if (unit.Character == null
                    || unit.Character.LifeState != CharacterLifeState.Alive)
                {
                    continue;
                }

                if (session.Contact != null && !session.Contact.Sees(aiUnit, unit)) continue;
                var distance = GetManhattanDistance(aiUnit.Position, unit.Position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestUnit = unit;
                }
            }

            return closestUnit;
        }

        public bool TryMoveAiTowardsTarget(
            CombatSession session,
            BattleUnit aiUnit,
            BattleUnit target)
        {
            return TryMoveAiTowardsTarget(session, aiUnit, target, out _);
        }

        private bool TryMoveAiTowardsTarget(
            CombatSession session,
            BattleUnit aiUnit,
            BattleUnit target,
            out List<GridPosition> movementPath)
        {
            movementPath = null;
            if (session?.Grid == null
                || aiUnit?.Character == null
                || target?.Character == null)
            {
                return false;
            }

            if (!TryFindPathToTarget(session, aiUnit, target, out var path)) return false;
            return TryExecuteMovementPath(session, aiUnit, path, out movementPath);
        }

        private bool TryExecuteMovementPath(CombatSession session, BattleUnit aiUnit,
            List<GridPosition> path, out List<GridPosition> movementPath, bool searching = false)
        {
            movementPath = null;
            if (path == null || path.Count == 0) return false;
            // Opening is a separate action so the existing driver presents/synchronizes it
            // before movement. Never toggle an already-open door.
            var firstDoor = FindClosedDirectDoor(session, aiUnit.Position, path[0]);
            if (firstDoor != null)
                return interactionSystem.TryInteract(session, aiUnit, path[0]);

            var movementSegment = new List<GridPosition>();
            var previous = aiUnit.Position;
            foreach (var next in path)
            {
                if (FindClosedDirectDoor(session, previous, next) != null) break;
                movementSegment.Add(next);
                previous = next;
            }

            // The movement owner applies real costs/statuses and may stop before the planned
            // endpoint. Present only the completed prefix, retaining its aggregated feedback.
            movementSystem.TryMoveAlongPath(session, aiUnit, movementSegment, out var completedPath,
                searching ? () =>
                {
                    session.Contact?.Refresh(session);
                    return FindClosestPlayerUnit(session, aiUnit) != null;
                } : null);
            if (completedPath.Count == 0) return false;
            movementPath = completedPath;
            return true;
        }

        private bool TryFindPathToTarget(
            CombatSession session, BattleUnit aiUnit, BattleUnit target,
            out List<GridPosition> path)
        {
            path = null;
            if (session?.Grid == null || aiUnit?.Character == null || target?.Character == null
                || aiUnit.Character.LifeState != CharacterLifeState.Alive
                || target.Character.LifeState != CharacterLifeState.Alive) return false;

            return TryFindPathToPosition(session, aiUnit, target.Position, true, out path);
        }

        private bool TryFindPathToPosition(CombatSession session, BattleUnit aiUnit, GridPosition destination,
            bool stopAdjacent, out List<GridPosition> path)
        {
            path = null;
            var start = aiUnit.Position;
            // Cache route costs and order incrementally: sorting every frontier while
            // recalculating every full path stalls large/disconnected layered rooms.
            var order = Comparer<(GridPosition cell, List<GridPosition> path, float risk, int cost, string traps, int serial)>.Create((a, b) =>
            {
                var result = a.risk.CompareTo(b.risk);
                if (result == 0) result = a.cost.CompareTo(b.cost);
                return result != 0 ? result : a.serial.CompareTo(b.serial);
            });
            var pending = new SortedSet<(GridPosition cell, List<GridPosition> path, float risk, int cost, string traps, int serial)>(order);
            var serial = 0;
            pending.Add((start, new List<GridPosition>(), 0f, 0, "", serial++));
            var best = new Dictionary<(GridPosition, string), (float risk, int cost)> { [(start, "")] = (0f, 0) };
            while (pending.Count > 0)
            {
                var node = pending.Min;
                pending.Remove(node);
                var current = node.cell;
                if (best.TryGetValue((current, node.traps), out var known)
                    && (known.risk < node.risk || known.risk == node.risk && known.cost < node.cost)) continue;
                if (stopAdjacent ? IsAdjacent(current, destination) && CanPlanAcross(session, aiUnit, current, destination)
                    : current == destination)
                {
                    path = node.path;
                    if (stopAdjacent && FindClosedDirectDoor(session, current, destination) != null) path.Add(destination);
                    return true;
                }
                foreach (var candidate in PlanningNeighbours(session, current))
                {
                    if (candidate == start || node.path.Contains(candidate) || session.IsPositionOccupied(candidate, aiUnit)
                        || session.DoesObjectBlockMovement(session.GetMapObjectAt(candidate))
                        || !CanPlanAcross(session, aiUnit, current, candidate)) continue;
                    var next = new List<GridPosition>(node.path) { candidate };
                    var risk = session.EvaluateTrapRisk?.Invoke(aiUnit, next) ?? 0f;
                    if (!float.IsFinite(risk)) continue;
                    var key = (candidate, session.TrapRouteState?.Invoke(next) ?? "");
                    var cost = node.cost + (aiUnit.MovementStepsThisTurn + next.Count
                        + session.Grid.GetMovementSurcharge(current, candidate)) * ActionPointUnits.PerPoint;
                    if (best.TryGetValue(key, out var old) && (old.risk < risk || old.risk == risk && old.cost <= cost)) continue;
                    best[key] = (risk, cost);
                    pending.Add((candidate, next, risk, cost, key.Item2, serial++));
                }
            }
            return false;
        }

        private static IEnumerable<GridPosition> PlanningNeighbours(CombatSession session, GridPosition current)
        {
            var candidates = new HashSet<GridPosition>(session.Grid.GetNeighbours(current));
            foreach (var door in session.MapObjects.OfType<DoorCombatMapObject>())
            {
                if (door.Edge.FirstCell == current) candidates.Add(door.Edge.SecondCell);
                else if (door.Edge.SecondCell == current) candidates.Add(door.Edge.FirstCell);
            }
            return candidates;
        }

        private static bool CanPlanAcross(
            CombatSession session, BattleUnit unit, GridPosition from, GridPosition to)
        {
            // AI navigation never expands into an exit cell, including AI-controlled allies.
            if (!session.Grid.IsWalkable(to) || session.Grid.IsBlocked(to)
                || session.ExternalPositionOccupied?.Invoke(to) == true) return false;
            if (session.CanMoveBetween(unit, from, to)) return true;
            return FindClosedDirectDoor(session, from, to) != null;
        }

        private static DoorCombatMapObject FindClosedDirectDoor(
            CombatSession session, GridPosition from, GridPosition to)
        {
            var edge = new BattleCellEdge(from, to);
            // A door must not turn a solid dungeon wall into a planned passage.
            if (session.DungeonContext != null)
                foreach (var wall in session.DungeonContext.WallEdges)
                    if (new BattleCellEdge(
                            DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, wall.FirstCell),
                            DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, wall.SecondCell))
                        .Equals(edge)) return null;

            foreach (var mapObject in session.MapObjects)
                if (mapObject is DoorCombatMapObject door && !door.IsOpen
                    && door.CanInteract() && door.Edge.Equals(edge)) return door;
            return null;
        }

        private static List<GridPosition> BuildPath(
            IReadOnlyDictionary<GridPosition, GridPosition> previous,
            GridPosition start,
            GridPosition destination)
        {
            var path = new List<GridPosition>();
            var current = destination;
            while (current != start)
            {
                path.Insert(0, current);
                if (!previous.TryGetValue(current, out current))
                {
                    return new List<GridPosition>();
                }
            }

            return path;
        }

        public int GetManhattanDistance(GridPosition a, GridPosition b)
        {
            return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
        }

        public bool IsAdjacent(GridPosition a, GridPosition b)
        {
            return GetManhattanDistance(a, b) == 1;
        }

        private IReadOnlyList<BattleUnit> FilterTargetsByLineOfSight(CombatSession session, BattleUnit caster,
            SkillData skill, IReadOnlyList<BattleUnit> targets)
        {
            if (targets != null && session.Contact != null && !caster.IsPlayerSide)
                targets = targets.Where(target => target != null && (!target.IsPlayerSide || session.Contact.Sees(caster, target))).ToList();
            if (targets == null || skill == null || !skill.RequiresLineOfSight)
            {
                return targets ?? new List<BattleUnit>();
            }

            return targets.Where(target => target != null
                && lineOfSightService.HasLineOfSight(session, caster.Position, target.Position)).ToList();
        }

        private sealed class AiSkillSelection
        {
            public SkillData Skill;
            public Dictionary<int, IReadOnlyList<BattleUnit>> TargetsByModeIndex;
            public GridPosition? SummonPlacementDestination;
            public float Score;
        }
    }
}
