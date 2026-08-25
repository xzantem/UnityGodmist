

using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class ScoutKnifeSystem
    {
        public const int DefaultMaxKnives = 5;

        public static void ResetForCombat(CombatSession session)
        {
            foreach (var unit in session?.Units ?? new List<BattleUnit>())
            {
                if (unit?.Character?.ClassData?.Id != "class.scout") continue;
                unit.MaxKnives = Math.Max(0, unit.MaxKnives <= 0 ? DefaultMaxKnives : unit.MaxKnives);
                unit.AvailableKnives = unit.MaxKnives;
            }
        }

        public static bool CanPay(BattleUnit caster, Godmist.Data.Skills.SkillData skill) =>
            skill == null || skill.KnifeCost <= 0 || caster?.AvailableKnives >= skill.KnifeCost;

        public static bool TrySpendAndDrop(CombatSession session, BattleUnit caster,
            Godmist.Data.Skills.SkillData skill, IReadOnlyList<BattleUnit> targets)
        {
            if (skill?.KnifeCost <= 0) return true;
            if (!CanPay(caster, skill)) return false;
            caster.AvailableKnives -= skill.KnifeCost;
            var target = targets != null && targets.Count > 0 ? targets[0] : null;
            if (target == null) return true;
            for (var index = 0; index < skill.KnifeCost; index++)
            {
                if (!TryFindRandomDropPosition(session, target, out var position)) continue;
                session.MapObjects.Add(new KnifeCombatMapObject(
                    $"knife.{caster.Character.CharacterId}.{Guid.NewGuid():N}", position,
                    caster.Character.CharacterId, 1));
            }
            return true;
        }

        public static int RecoverAtPosition(CombatSession session, BattleUnit unit)
        {
            if (session?.MapObjects == null || unit?.Character == null) return 0;
            var recovered = 0;
            for (var index = session.MapObjects.Count - 1; index >= 0; index--)
            {
                if (session.MapObjects[index] is not KnifeCombatMapObject knife
                    || knife.Position != unit.Position
                    || !string.Equals(knife.OwnerCharacterId, unit.Character.CharacterId,
                        StringComparison.OrdinalIgnoreCase)) continue;
                var amount = Math.Min(Math.Max(0, unit.MaxKnives - unit.AvailableKnives), knife.KnifeCount);
                if (amount <= 0) continue;
                unit.AvailableKnives += amount;
                recovered += amount;
                session.MapObjects.RemoveAt(index);
            }
            return recovered;
        }

        private static bool TryFindRandomDropPosition(CombatSession session,
            BattleUnit target, out GridPosition position)
        {
            var candidates = new List<GridPosition>();
            for (var x = -1; x <= 1; x++) for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                var candidate = new GridPosition(target.Position.X + x, target.Position.Y + y);
                if (!session.Grid.IsWalkable(candidate) || session.Grid.IsBlocked(candidate)
                    || session.IsPositionOccupied(candidate) || session.IsPositionOccupiedByMapObject(candidate)) continue;
                candidates.Add(candidate);
            }
            if (candidates.Count == 0) { position = default; return false; }
            position = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return true;
        }
    }

    public class CombatMovementSystem
    {
        private static readonly Vector2Int[] MovementDirections =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
        };

        private readonly Action<string> battleLog;
        private readonly ResourceRuleSystem resourceRuleSystem;
        private readonly StatusEffectSystem statusEffectSystem;
        private readonly CombatFeedbackBus feedbackBus;

        public CombatMovementSystem(
            Action<string> battleLog = null,
            ResourceRuleSystem resourceRuleSystem = null,
            StatusEffectSystem statusEffectSystem = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.battleLog = battleLog;
            this.resourceRuleSystem = resourceRuleSystem;
            this.statusEffectSystem = statusEffectSystem;
            this.feedbackBus = feedbackBus;
        }

        public Dictionary<GridPosition, List<GridPosition>> CalculateReachablePaths(
            CombatSession session, 
            BattleUnit unit)
        {
            var reachablePaths = new Dictionary<
                GridPosition, 
                List<GridPosition>>();

            if (session?.Grid == null || unit?.Character == null 
                || unit.Character.LifeState != CharacterLifeState.Alive) return reachablePaths;

            if ((StatusEffectSystem.IsHardCrowdControlled(unit)
                    && !StatusEffectSystem.IsSleeping(unit))
                || StatusEffectSystem.IsImmobilized(unit))
            {
                return reachablePaths;
            }

            var queue = new Queue<GridPosition>();
            var paths = new Dictionary<GridPosition, List<GridPosition>>();

            queue.Enqueue(unit.Position);
            paths[unit.Position] = new List<GridPosition>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentPath = paths[current];

                foreach (var direction in MovementDirections)
                {
                    var candidate = new GridPosition(
                        current.X + direction.x, 
                        current.Y + direction.y);

                    if (paths.ContainsKey(candidate))
                    {
                        continue;
                    }
                    if (!session.Grid.CanMoveBetween(current, candidate)
                        || session.IsPositionOccupied(candidate, unit)
                        || session.DoesObjectBlockMovement(
                            session.GetMapObjectAt(candidate)))
                    {
                        continue;
                    }

                    var nextPath = new List<GridPosition>(currentPath)
                    {
                        candidate
                    };

                    var pathCost = CalculatePathCost(
                        unit, 
                        nextPath.Count);

                    if (pathCost > unit.CurrentActionPoints)
                    {
                        continue;
                    }

                    paths[candidate] = nextPath;
                    reachablePaths[candidate] = nextPath;
                    queue.Enqueue(candidate);
                }
            }

            return reachablePaths;
        }

        public bool TryMoveAlongPath(
            CombatSession session,
            BattleUnit unit, 
            IReadOnlyList<GridPosition> path)
        {
            if (path == null || path.Count == 0)
            {
                return false;
            }
            
            foreach (var targetPosition in path)
            {
                var deltaX = targetPosition.X - unit.Position.X;
                var deltaY = targetPosition.Y - unit.Position.Y;

                if (!TryMoveOneStep(
                        session, 
                        unit, 
                        deltaX, 
                        deltaY))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryMoveOneStep(
            CombatSession session,
            BattleUnit unit,
            int deltaX,
            int deltaY)
        {
            if (session?.Grid == null
                || unit?.Character == null
                || unit.Character.LifeState != CharacterLifeState.Alive)
            {
                return false;
            }

            if ((StatusEffectSystem.IsHardCrowdControlled(unit)
                    && !StatusEffectSystem.IsSleeping(unit))
                || StatusEffectSystem.IsImmobilized(unit))
            {
                battleLog?.Invoke(
                    $"{unit.Character.Definition.DisplayName} " +
                    "nie może się ruszyć przez status kontroli.");

                return false;
            }

            if (Math.Abs(deltaX) + Mathf.Abs(deltaY) != 1)
            {
                return false;
            }

            var movementCost =
                CombatActionCostCalculator.CalculateMovementCost(unit);

            if (unit.CurrentActionPoints < movementCost)
            {
                return false;
            }

            if (!session.Grid.TryMove(
                    unit.Position,
                    deltaX,
                    deltaY,
                    out var targetPosition))
            {
                return false;
            }

            if (session.IsPositionOccupied(targetPosition, unit))
            {
                return false;
            }

            var mapObject = session.GetMapObjectAt(targetPosition);

            if (session.DoesObjectBlockMovement(mapObject))
            {
                return false;
            }

            if (StatusEffectSystem.IsSleeping(unit))
            {
                if (statusEffectSystem == null)
                {
                    return false;
                }

                statusEffectSystem.RemoveStatus(unit, StatusEffectIds.Sleep);
            }

            unit.Position = targetPosition;
            unit.CurrentActionPoints -= movementCost;
            if (movementCost > 0)
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    unit, CombatFeedbackType.ActionPointSpend, movementCost));
            }
            unit.MovementStepsThisTurn++;

            resourceRuleSystem?.ProcessUnitMoved(unit, movementCost);
            var recoveredKnives = ScoutKnifeSystem.RecoverAtPosition(session, unit);
            if (recoveredKnives > 0)
            {
                battleLog?.Invoke($"{unit.Character.Definition.DisplayName} odzyskuje {recoveredKnives} sztylet.");
            }
            CombatAuraSystem.Refresh(session, statusEffectSystem);

            battleLog?.Invoke(
                $"{unit.Character.Definition.DisplayName} " +
                $"moves to [{unit.Position.X}, {unit.Position.Y}] " +
                $"for {movementCost} AP.");

            return true;
        }

        public static int CalculatePathCost(
            BattleUnit unit, 
            int pathLength)
        {
            var firstStepCost = unit.MovementStepsThisTurn + 1;

            return pathLength
                * (2 * firstStepCost + pathLength - 1) 
                / 2;
        } 
    }
}
