

using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
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

        public CombatMovementSystem(Action<string> battleLog = null, ResourceRuleSystem resourceRuleSystem = null)
        {
            this.battleLog = battleLog;
            this.resourceRuleSystem = resourceRuleSystem;
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

            if (StatusEffectSystem.IsStunned(unit) 
                || StatusEffectSystem.IsImmobilized(unit)) return reachablePaths;

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
                        || session.IsPositionOccupied(candidate, unit))
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
            if (StatusEffectSystem.IsStunned(unit) || StatusEffectSystem.IsImmobilized(unit))
            {
                battleLog?.Invoke($"{unit.Character.Definition.DisplayName} " +
                    "nie może się ruszyć przez unieruchomienie.");
                return false;
            }

            if (Math.Abs(deltaX) + Mathf.Abs(deltaY) != 1) return false;

            var movementCost = CombatActionCostCalculator.CalculateMovementCost(unit);

            if (unit.CurrentActionPoints < movementCost) return false;
            
            if (!session.Grid.TryMove(
                unit.Position, 
                deltaX, 
                deltaY, 
                out var targetPosition)) return false;

            if (session.IsPositionOccupied(targetPosition, unit)) return false;

            unit.Position = targetPosition;
            unit.CurrentActionPoints -= movementCost;
            unit.MovementStepsThisTurn++;
            resourceRuleSystem?.ProcessUnitMoved(unit, movementCost);

            battleLog?.Invoke(
                $"{unit.Character.Definition.DisplayName} " +
                $"moves to [{unit.Position.X}, {unit.Position.Y}] " +
                $"for {movementCost} AP.");

            return true;
        }
        private static int CalculatePathCost(
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
