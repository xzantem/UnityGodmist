using System;
using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Core.Enums;
using Godmist.Core.Constants;
using UnityEngine;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using System.Linq;

namespace Godmist.Systems.Combat
{
    public enum SimpleCombatAiActionType
    {
        Movement,
        Skill
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
            CombatLineOfSightService lineOfSightService = null)
        {
            this.battleLog = battleLog;
            this.endTurn = endTurn;
            this.evaluateOutcome = evaluateOutcome;
            this.skillExecutionSystem = skillExecutionSystem;
            this.targetingService = targetingService ?? new CombatTargetingService();
            this.movementSystem = movementSystem ?? new CombatMovementSystem(battleLog);
            this.lineOfSightService = lineOfSightService ?? new CombatLineOfSightService();
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
            var target = hasTaunter ? taunter : FindClosestPlayerUnit(session, aiUnit);
            if (target == null)
            {
                evaluateOutcome?.Invoke(session);
                return false;
            }

            if (!TryMoveAiForRole(session, aiUnit, target, out var movementPath))
            {
                return false;
            }

            action = new SimpleCombatAiAction(
                SimpleCombatAiActionType.Movement,
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

            var actionCost = CombatActionCostCalculator.CalculateSkillCost(unit, skill);

            if (unit.CurrentActionPoints < actionCost) return false;

            if (skill.ResourceCost <= 0) return true;

            var resourcePool = unit.Character.ResourcePool;

            return resourcePool != null
                && resourcePool.ResourceType == skill.ResourceType
                && resourcePool.CurrentValue >= skill.ResourceCost;
        }

        private bool TryBuildTargets(CombatSession session, BattleUnit caster, 
            SkillData skill, out Dictionary<int, IReadOnlyList<BattleUnit>> targetsByModeIndex)
        {
            targetsByModeIndex = new Dictionary<int, IReadOnlyList<BattleUnit>>();

            if (skill?.TargetModes == null || skill.TargetModes.Count == 0 
                || skill.Effects == null || skill.Effects.Count == 0) return false;

            for (var index = 0; index < skill.TargetModes.Count; index++)
            {
                var mode = skill.TargetModes[index];

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
                    score += 75f;
                    continue;
                }

                if (effect == null || !targetsByModeIndex.TryGetValue(effect.TargetModeIndex, out var targets))
                    continue;

                score += CalculateEffectsScore(caster, skill, effect.TargetModeIndex, targets, effect);

                if (effect.DelayCasterTurns > 0)
                {
                    score += 25f * targets.Count;
                }
            }

            var actionCost = CombatActionCostCalculator.CalculateSkillCost(caster, skill);

            return score + CalculateRoleBonus(caster, skill, targetsByModeIndex)
                + CalculatePersonalityBonus(caster, skill, targetsByModeIndex)
                - actionCost * 0.35f - skill.ResourceCost * 0.1f;
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
                        when effect.EffectType == EffectType.DealDamage => 30f * count,
                    CombatAiPersonality.Aggressive
                        when effect.EffectType == EffectType.ApplyStatus => 15f * count,
                    CombatAiPersonality.Cautious
                        when effect.EffectType == EffectType.Heal
                        || effect.EffectType == EffectType.Shield => 30f * count,
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
                        => 60f * count,
                    CombatAiRole.Dps when effect.EffectType == EffectType.ApplyStatus
                        => 20f * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.Shield
                        => 35f * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.ApplyStatus
                        && string.Equals(effect.StatusId, StatusEffectIds.Taunt,
                            StringComparison.OrdinalIgnoreCase) => 90f * count,
                    CombatAiRole.Tank when effect.EffectType == EffectType.Push
                        || effect.EffectType == EffectType.Pull => 25f * count,
                    CombatAiRole.Healer when effect.EffectType == EffectType.Heal
                        || effect.EffectType == EffectType.RestoreResource => 70f * count,
                    CombatAiRole.Support when effect.EffectType == EffectType.Shield
                        || effect.EffectType == EffectType.ModifyStat
                        || effect.EffectType == EffectType.ModifyVar => 55f * count,
                    CombatAiRole.Support when effect.EffectType == EffectType.ApplyStatus
                        => 40f * count,
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
                && distance <= desiredRange)
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

                    var mode = skill.TargetModes[effect.TargetModeIndex];
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

            foreach (var direction in MovementDirections)
            {
                var candidate = new GridPosition(aiUnit.Position.X + direction.x,
                    aiUnit.Position.Y + direction.y);
                var candidateDistance = GetManhattanDistance(candidate, target.Position);
                if (candidateDistance <= bestDistance || candidateDistance > desiredRange
                    || session?.Grid == null || !session.Grid.IsWithinBounds(candidate)
                    || session.Grid.IsBlocked(candidate)
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

            foreach (var effect in skill.Effects)
            {
                if (effect == null || effect.TargetModeIndex != targetModeIndex
                    || singleEffect != null && effect != singleEffect)
                {
                    continue;
                }

                foreach (var target in targets)
                {
                    if (target?.Character == null) continue;

                    switch (effect.EffectType)
                    {
                        case EffectType.DealDamage:
                        {
                            var expectedDamage = CalculateExpectedDamage(caster, effect);
                            score += expectedDamage;
                            if (expectedDamage >= target.Character.CurrentHealth)
                            {
                                score += 10000f;
                            }
                            break;
                        }
                        case EffectType.ApplyStatus:
                            score += IsHardControl(effect.StatusId) ? 300f : 100f;
                            break;
                        case EffectType.Heal:
                            score += GetMissingHealth(target);
                            if (target.Character.LifeState == CharacterLifeState.Downed)
                            {
                                score += 5000f;
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
                            score += Mathf.Max(0, effect.BaseValue) * 0.5f;
                            break;
                        case EffectType.ModifyStat:
                        case EffectType.ModifyVar:
                            score += 40f;
                            break;
                        case EffectType.Push:
                        case EffectType.Pull:
                            score += 30f;
                            break;
                        case EffectType.Fallback:
                            score += 10f;
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

            if (GetManhattanDistance(aiUnit.Position, target.Position) <= 1)
            {
                return false;
            }

            if (!TryFindPathToTarget(
                    session,
                    aiUnit,
                    target,
                    out var path))
            {
                return false;
            }

            if (!movementSystem.TryMoveAlongPath(
                session,
                aiUnit,
                path))
            {
                return false;
            }

            movementPath = path;
            return true;
        }

        private bool TryFindPathToTarget(
            CombatSession session,
            BattleUnit aiUnit,
            BattleUnit target,
            out List<GridPosition> path)
        {
            path = null;

            if (session?.Grid == null
                || aiUnit?.Character == null
                || target?.Character == null
                || aiUnit.Character.LifeState != CharacterLifeState.Alive
                || target.Character.LifeState != CharacterLifeState.Alive)
            {
                return false;
            }
            var start = aiUnit.Position; 
            var queue = new Queue<GridPosition>();
            var visited = new HashSet<GridPosition>();
            var previous = new Dictionary<
                GridPosition,
                GridPosition>();

            queue.Enqueue(start);
            visited.Add(start);

            GridPosition goal = default;
            var foundGoal = false;
            var bestReachablePosition = start;
            var bestReachableDistance = GetManhattanDistance(
                start,
                target.Position);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current != start
                    && IsAdjacent(current, target.Position))
                {
                    goal = current;
                    foundGoal = true;
                    break;
                }

                if (current != start)
                {
                    var currentDistance = GetManhattanDistance(
                        current,
                        target.Position);
                    if (currentDistance < bestReachableDistance)
                    {
                        bestReachablePosition = current;
                        bestReachableDistance = currentDistance;
                    }
                }

                foreach (var direction in MovementDirections)
                {
                    var candidate = new GridPosition(
                        current.X + direction.x,
                        current.Y + direction.y);

                    if (visited.Contains(candidate))
                    {
                        continue;
                    }
                    if (!session.Grid.IsWithinBounds(candidate))
                    {
                        continue;
                    }
                    if (session.Grid.IsBlocked(candidate))
                    {
                        continue;
                    }
                    if (session.IsPositionOccupied(candidate, aiUnit))
                    {
                        continue;
                    }

                    var nextPathLength = BuildPath(previous, start, current).Count + 1;
                    if (CombatMovementSystem.CalculatePathCost(
                            aiUnit,
                            nextPathLength)
                        > aiUnit.CurrentActionPoints)
                    {
                        continue;
                    }

                    visited.Add(candidate);
                    previous[candidate] = current;
                    queue.Enqueue(candidate);
                }
            }
            if (!foundGoal)
            {
                goal = bestReachablePosition;
                if (goal == start)
                {
                    return false;
                }
            }

            path = BuildPath(previous, start, goal);
            return true;
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
