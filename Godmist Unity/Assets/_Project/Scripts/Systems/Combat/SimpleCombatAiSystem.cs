using System;
using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Core.Enums;
using UnityEngine;
using Godmist.Data.Skills;
using Godmist.Data.Effects;
using System.Linq;

namespace Godmist.Systems.Combat
{
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

            var hasTaunter = StatusEffectSystem.TryGetTaunter(
                session,
                aiUnit,
                out var taunter);

            var usedSkill = TryExecuteBestSkill(session, aiUnit);

            if (!usedSkill)
            {
                var target = hasTaunter
                    ? taunter
                    : FindClosestPlayerUnit(session, aiUnit);

                if (target == null)
                {
                    evaluateOutcome?.Invoke(session);
                    return;
                }

                while (session.Outcome == CombatOutcomeState.Ongoing && aiUnit.CurrentActionPoints > 0
                    && !IsAdjacent(aiUnit.Position, target.Position))
                {
                    if (!TryMoveAiTowardsTarget(session, aiUnit, target))
                    {
                        break;
                    }
                }
                usedSkill = TryExecuteBestSkill(session, aiUnit);
            }
            if (session.Outcome == CombatOutcomeState.Ongoing) endTurn?.Invoke(session);
        }

        private bool TryExecuteBestSkill(CombatSession session, BattleUnit aiUnit)
        {
            if (skillExecutionSystem == null) return false;

            var selection = FindBestSkillSelection(session, aiUnit);

            if (selection == null) return false;

            return skillExecutionSystem.TryExecute(
                session,
                aiUnit,
                selection.Skill,
                selection.TargetsByModeIndex,
                teleportDestination: null,
                objectPlacementDestination: selection.SummonPlacementDestination);
        }

        private AiSkillSelection FindBestSkillSelection(CombatSession session, BattleUnit aiUnit)
        {
            if (aiUnit?.Character?.Skills == null) return null;

            AiSkillSelection bestSelection = null;

            foreach (var skill in aiUnit.Character.Skills)
            {
                if (!CanAffordSkill(aiUnit, skill)) continue;

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

                var score = CalculateTargetGroupScore(mode, targets);

                if (!foundPosition || score > bestScore)
                {
                    selectedPosition = position;
                    bestScore = score;
                    foundPosition = true;
                }
            }

            return foundPosition;
        }

        private static float CalculateTargetGroupScore(SkillTargetModeData mode, IReadOnlyList<BattleUnit> targets)
        {
            if (mode == null || targets == null || targets.Count == 0) return float.MinValue;

            var score = targets.Count * 1000f;

            foreach (var target in targets)
            {
                if (target?.Character == null) continue;
                
                var maxHealth = target.Character.Stats != null ? target.Character.Stats.MaxHealth.GetValue()
                    : target.Character.CurrentHealth;

                var missingHealth = Mathf.Max(0f, maxHealth - target.Character.CurrentHealth);

                if (mode.TargetingType == TargetingType.Enemies) score += missingHealth;
                else if (mode.TargetingType == TargetingType.Allies) score += missingHealth * 2f;
            }

            return score;
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

                switch (effect.EffectType)
                {
                    case EffectType.DealDamage:
                        score += 200f * targets.Count;
                        break;

                    case EffectType.ApplyStatus:
                        score += 100f * targets.Count;
                        break;

                    case EffectType.Heal:
                        foreach (var target in targets)
                        {
                            var maxHealth = target.Character.Stats.MaxHealth.GetValue();
                            var missingHealth = Mathf.Max(0f, maxHealth - target.Character.CurrentHealth);

                            score += missingHealth * 2f;
                        }
                        break;

                    case EffectType.RestoreResource:
                        foreach (var target in targets)
                        {
                            var resourcePool = target.Character.ResourcePool;

                            if (resourcePool != null)
                            {
                                score += Mathf.Max(0, resourcePool.MaxValue - resourcePool.CurrentValue);
                            }
                        }
                        break;

                    case EffectType.Shield:
                        score += 25f * targets.Count;
                        break;
                    
                    case EffectType.Push:
                        score += 30f * targets.Count;
                        break;

                    case EffectType.Pull:
                        score += 30f * targets.Count;
                        break;

                    case EffectType.Fallback:
                        score += 10f * targets.Count;
                        break;
                }
            }

            var actionCost = CombatActionCostCalculator.CalculateSkillCost(caster, skill);

            return score - actionCost * 0.1f;
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
            if (!TryFindNextStep(
                    session,
                    aiUnit,
                    target,
                    out var nextStep))
            {
                Debug.LogWarning(
                    $"{aiUnit.Character.Definition.DisplayName} " +
                    "nie znalazł ścieżki do celu.");

                return false;
            }

            var deltaX = nextStep.X - aiUnit.Position.X;
            var deltaY = nextStep.Y - aiUnit.Position.Y;

            return movementSystem.TryMoveOneStep(
                session,
                aiUnit,
                deltaX,
                deltaY);
        }
        private bool TryFindNextStep(
            CombatSession session,
            BattleUnit aiUnit,
            BattleUnit target,
            out GridPosition nextStep)
        {
            nextStep = default;

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

                    visited.Add(candidate);
                    previous[candidate] = current;
                    queue.Enqueue(candidate);
                }
            }
            if (!foundGoal)
            {
                return false;
            }

            while (previous.TryGetValue(
                goal,
                out var parent)
                && parent != start)
            {
                goal = parent;
            }

            nextStep = goal;
            return true;
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
