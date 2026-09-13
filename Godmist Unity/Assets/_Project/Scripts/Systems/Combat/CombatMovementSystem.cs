

using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class ScoutKnifeSystem
    {
        public static void ResetForCombat(CombatSession session, bool preserveInitialized = false)
        {
            foreach (var unit in session?.Units ?? new List<BattleUnit>())
            {
                if (unit == null || (preserveInitialized && unit.HasInitializedCombatResources)) continue;
                unit.HasInitializedCombatResources = true;
                if (unit.Character?.ClassData?.Id != "class.scout") continue;
                unit.MaxKnives = Math.Max(0, unit.MaxKnives <= 0
                    ? GameDefinesRuntime.GetInt(GameDefineIds.DefaultMaxKnives, 5)
                    : unit.MaxKnives);
                unit.AvailableKnives = unit.MaxKnives;
            }
        }

        public static bool CanPay(BattleUnit caster, Godmist.Data.Skills.SkillData skill) =>
            skill == null || skill.KnifeCost <= 0 || caster?.AvailableKnives >= skill.KnifeCost;

        public static bool TrySpendAndDrop(CombatSession session, BattleUnit caster,
            Godmist.Data.Skills.SkillData skill, IReadOnlyList<BattleUnit> targets,
            bool preventConsumption = false)
        {
            if (skill?.KnifeCost <= 0) return true;
            if (preventConsumption) return true;
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
        private readonly PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher;

        public CombatMovementSystem(
            Action<string> battleLog = null,
            ResourceRuleSystem resourceRuleSystem = null,
            StatusEffectSystem statusEffectSystem = null,
            CombatFeedbackBus feedbackBus = null,
            PassiveEffectCombatEventDispatcher passiveEffectEventDispatcher = null)
        {
            this.battleLog = battleLog;
            this.resourceRuleSystem = resourceRuleSystem;
            this.statusEffectSystem = statusEffectSystem;
            this.feedbackBus = feedbackBus;
            this.passiveEffectEventDispatcher = passiveEffectEventDispatcher;
        }

        public Dictionary<GridPosition, List<GridPosition>> CalculateReachablePaths(
            CombatSession session, 
            BattleUnit unit)
        {
            return CalculateReachablePaths(
                session,
                unit,
                unit?.CurrentActionPoints ?? 0,
                unit?.MovementStepsThisTurn ?? 0);
        }

        public Dictionary<GridPosition, List<GridPosition>>
            CalculateReachablePathsForNextTurn(
                CombatSession session,
                BattleUnit unit)
        {
            return CalculateReachablePaths(
                session,
                unit,
                unit?.MaxActionPoints ?? 0,
                movementStepsAlreadyTaken: 0);
        }

        private Dictionary<GridPosition, List<GridPosition>> CalculateReachablePaths(
            CombatSession session,
            BattleUnit unit,
            int availableActionPoints,
            int movementStepsAlreadyTaken)
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

            var queue = new Queue<(GridPosition position, List<GridPosition> path)>();
            var labels = new Dictionary<GridPosition, List<(int steps, int cost)>>
            {
                [unit.Position] = new() { (0, 0) }
            };
            var bestCosts = new Dictionary<GridPosition, int>();
            queue.Enqueue((unit.Position, new List<GridPosition>()));
            while (queue.Count > 0)
            {
                var (current, currentPath) = queue.Dequeue();
                foreach (var direction in MovementDirections)
                {
                    var candidate = new GridPosition(current.X + direction.x, current.Y + direction.y);
                    if (!session.CanMoveBetween(unit, current, candidate)
                        || session.IsPositionOccupied(candidate, unit)
                        || session.DoesObjectBlockMovement(session.GetMapObjectAt(candidate))) continue;
                    var nextPath = new List<GridPosition>(currentPath) { candidate };
                    var cost = CalculatePathCost(session, unit.Position, movementStepsAlreadyTaken, nextPath);
                    if (cost > availableActionPoints) continue;
                    if (!labels.TryGetValue(candidate, out var candidates))
                        labels[candidate] = candidates = new List<(int steps, int cost)>();
                    // A cheaper but longer route and a shorter costly route can both matter,
                    // because the ordinary step cost increases throughout the turn.
                    if (candidates.Exists(label => label.steps <= nextPath.Count && label.cost <= cost)) continue;
                    candidates.RemoveAll(label => label.steps >= nextPath.Count && label.cost >= cost);
                    candidates.Add((nextPath.Count, cost));
                    if (!bestCosts.TryGetValue(candidate, out var best) || cost < best)
                    {
                        bestCosts[candidate] = cost;
                        reachablePaths[candidate] = nextPath;
                    }
                    if (!session.IsExitPosition(candidate)) queue.Enqueue((candidate, nextPath));
                }
            }

            return reachablePaths;
        }

        public bool TryMoveAlongPath(
            CombatSession session,
            BattleUnit unit, 
            IReadOnlyList<GridPosition> path)
        {
            return TryMoveAlongPath(session, unit, path, out _);
        }

        public bool TryMoveAlongPath(
            CombatSession session,
            BattleUnit unit,
            IReadOnlyList<GridPosition> path,
            out List<GridPosition> completedPath)
        {
            completedPath = new List<GridPosition>();
            if (session?.Grid == null
                || unit?.Character == null
                || path == null
                || path.Count == 0)
            {
                return false;
            }

            var totalMovementCost = 0;
            var completed = true;
            foreach (var targetPosition in path)
            {
                var deltaX = targetPosition.X - unit.Position.X;
                var deltaY = targetPosition.Y - unit.Position.Y;

                if (!TryMoveOneStepInternal(
                        session, 
                        unit, 
                        deltaX, 
                        deltaY,
                        publishFeedback: false,
                        processResource: false,
                        writeBattleLog: false,
                        publishPassiveEvent: false,
                        out var movementCost))
                {
                    completed = false;
                    break;
                }

                completedPath.Add(targetPosition);
                totalMovementCost += movementCost;
            }

            if (totalMovementCost > 0)
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    unit,
                    CombatFeedbackType.ActionPointSpend,
                    totalMovementCost));
                resourceRuleSystem?.ProcessUnitMoved(unit, totalMovementCost);
                (passiveEffectEventDispatcher ?? session.PassiveEffectEventDispatcher)
                    ?.Publish(
                        PassiveEffectTriggerType.OnMove,
                        new PassiveEffectCombatContext(
                            session: session,
                            source: unit,
                            movementCost: totalMovementCost,
                            movementSteps: unit.MovementStepsThisTurn,
                            distance: completedPath.Count,
                            eventValue: totalMovementCost));
                battleLog?.Invoke(
                    $"{unit.Character.Definition.DisplayName} " +
                    $"moves along a path for {totalMovementCost} AP.");
            }

            return completed;
        }

        public bool TryMoveOneStep(
            CombatSession session,
            BattleUnit unit,
            int deltaX,
            int deltaY)
        {
            return TryMoveOneStepInternal(
                session,
                unit,
                deltaX,
                deltaY,
                publishFeedback: true,
                processResource: true,
                writeBattleLog: true,
                publishPassiveEvent: true,
                out _);
        }

        private bool TryMoveOneStepInternal(
            CombatSession session,
            BattleUnit unit,
            int deltaX,
            int deltaY,
            bool publishFeedback,
            bool processResource,
            bool writeBattleLog,
            bool publishPassiveEvent,
            out int movementCost)
        {
            movementCost = 0;
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

            movementCost = PassiveEffectCombatExecutionSystem.ApplyMovementCost(
                session,
                unit,
                CombatActionCostCalculator.CalculateMovementCost(unit));

            var nextCell = new GridPosition(unit.Position.X + deltaX, unit.Position.Y + deltaY);
            movementCost += session.Grid.GetMovementSurcharge(unit.Position, nextCell);
            if (unit.CurrentActionPoints < movementCost)
            {
                return false;
            }

            var targetPosition = new GridPosition(unit.Position.X + deltaX, unit.Position.Y + deltaY);
            if (!session.CanMoveBetween(unit, unit.Position, targetPosition))
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
            if (publishFeedback && movementCost > 0)
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    unit, CombatFeedbackType.ActionPointSpend, movementCost));
            }
            unit.MovementStepsThisTurn++;
            session.NotifyUnitEnteredCell(unit);

            if (processResource)
            {
                resourceRuleSystem?.ProcessUnitMoved(unit, movementCost);
            }
            var recoveredKnives = ScoutKnifeSystem.RecoverAtPosition(session, unit);
            if (recoveredKnives > 0)
            {
                battleLog?.Invoke($"{unit.Character.Definition.DisplayName} odzyskuje {recoveredKnives} sztylet.");
            }
            CombatAuraSystem.Refresh(session, statusEffectSystem);

            if (publishPassiveEvent)
            {
                (passiveEffectEventDispatcher ?? session.PassiveEffectEventDispatcher)
                    ?.Publish(
                        PassiveEffectTriggerType.OnMove,
                        new PassiveEffectCombatContext(
                            session: session,
                            source: unit,
                            movementCost: movementCost,
                            movementSteps: unit.MovementStepsThisTurn,
                            distance: 1,
                            eventValue: movementCost));
            }

            if (writeBattleLog)
            {
                battleLog?.Invoke(
                    $"{unit.Character.Definition.DisplayName} " +
                    $"moves to [{unit.Position.X}, {unit.Position.Y}] " +
                    $"for {movementCost} AP.");
            }

            return true;
        }

        public static int CalculatePathCost(CombatSession session, GridPosition origin,
            int movementStepsAlreadyTaken, IReadOnlyList<GridPosition> path)
        {
            var cost = CalculatePathCost(movementStepsAlreadyTaken, path.Count);
            foreach (var cell in path)
            {
                cost += session.Grid.GetMovementSurcharge(origin, cell);
                origin = cell;
            }
            return cost;
        }

        public static int CalculatePathCost(
            BattleUnit unit, 
            int pathLength)
        {
            return CalculatePathCost(unit?.MovementStepsThisTurn ?? 0, pathLength);
        }

        public static int CalculatePathCost(
            int movementStepsAlreadyTaken,
            int pathLength)
        {
            var firstStepCost = movementStepsAlreadyTaken + 1;

            return pathLength
                * (2 * firstStepCost + pathLength - 1) 
                / 2;
        } 
    }
}
