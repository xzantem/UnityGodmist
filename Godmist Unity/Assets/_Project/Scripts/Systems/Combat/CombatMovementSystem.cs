

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
                if (!unit.HasInitializedCombatResources && !unit.IsPlayerSide
                    && unit.Character?.Definition?.Id?.StartsWith("enemy.", StringComparison.Ordinal) == true
                    && unit.Character.ClassData?.StartingResourcePercent >= 0
                    && unit.Character.ResourcePool is { } pool)
                {
                    // Exploration can drain Momentum or regenerate Mana before
                    // contact. Apply the opening profile once on combat admission.
                    var previousResource = pool.CurrentValue;
                    pool.CurrentValue = Mathf.FloorToInt(pool.MaxValue
                        * Mathf.Clamp(unit.Character.ClassData.StartingResourcePercent, 0, 100) / 100f);
                    pool.HasStartedTurn = pool.HasGainedResourceSinceLastTurn = false;
                    pool.FractionalGainRemainder = 0f;
                    MomentumResourceRule.RefreshSpeedAfterChange(unit.Character, previousResource);
                }
                unit.HasInitializedCombatResources = true;
                if(unit.ScoutState.Facing==Vector2Int.zero)
                    foreach(var opponent in session.Units)
                        if(opponent!=null && opponent.IsPlayerSide!=unit.IsPlayerSide) {ScoutCombatSystem.Face(unit,opponent.Position);break;}
                if (unit.Character?.ClassData?.Id != "class.scout") continue;
                unit.MaxKnives = Math.Max(0, unit.MaxKnives <= 0
                    ? GameDefinesRuntime.GetInt(GameDefineIds.DefaultMaxKnives, 5)
                    : unit.MaxKnives - unit.ScoutState.ExtraKnifeCapacity);
                unit.ScoutState.ExtraKnifeCapacity = ScoutCombatSystem.BladeRank(unit, "spare_daggers");
                unit.MaxKnives += unit.ScoutState.ExtraKnifeCapacity;
                unit.AvailableKnives = unit.MaxKnives;
            }
        }

        public static bool CanPay(BattleUnit caster, Godmist.Data.Skills.SkillData skill, BattleUnit target = null) =>
            skill == null || skill.KnifeCost <= 0 || caster?.AvailableKnives >= skill.KnifeCost
            || skill.FreeKnivesInMelee && caster != null && target != null && caster.Position.Level == target.Position.Level
                && Godmist.Data.Skills.SkillData.IsPointBlank(caster.Position.X-target.Position.X,caster.Position.Y-target.Position.Y);

        public static bool CanTargetPosition(BattleUnit caster, Godmist.Data.Skills.SkillData skill, GridPosition target) =>
            skill == null || skill.KnifeCost <= 0 || caster?.AvailableKnives >= skill.KnifeCost
            || skill.FreeKnivesInMelee && caster != null && caster.Position.Level == target.Level
                && Godmist.Data.Skills.SkillData.IsPointBlank(caster.Position.X-target.X,caster.Position.Y-target.Y);

        public static bool TrySpendAndDrop(CombatSession session, BattleUnit caster,
            Godmist.Data.Skills.SkillData skill, IReadOnlyList<BattleUnit> targets,
            bool preventConsumption = false, GridPosition? fallbackTargetPosition = null, string actionId = null)
        {
            if (skill?.KnifeCost <= 0) return true;
            var meleeTarget = targets != null && targets.Count > 0 ? targets[0] : null;
            if (skill.FreeKnivesInMelee && meleeTarget != null && caster.Position.Level == meleeTarget.Position.Level
                && Godmist.Data.Skills.SkillData.IsPointBlank(
                    caster.Position.X - meleeTarget.Position.X, caster.Position.Y - meleeTarget.Position.Y)) return true;
            if (!CanPay(caster, skill)) return false;
            if (preventConsumption || UnityEngine.Random.value < ScoutCombatSystem.Points(caster, ScoutCombatSystem.BladeTree) * .01f) return true;
            caster.AvailableKnives -= skill.KnifeCost;
            var target = targets != null && targets.Count > 0 ? targets[0] : null;
            var targetPosition = target?.Position ?? fallbackTargetPosition;
            if (!targetPosition.HasValue) return true;
            if (!string.IsNullOrEmpty(actionId))
            {
                session.PendingScoutKnifeActions[actionId] = new PendingScoutKnives { Owner = caster, DropPosition = targetPosition.Value, Count = skill.KnifeCost };
                return true;
            }
            for (var index = 0; index < skill.KnifeCost; index++)
            {
                if (!TryFindRandomDropPosition(session, targetPosition.Value, out var position)) continue;
                session.MapObjects.Add(new KnifeCombatMapObject(
                    $"knife.{caster.Character.CharacterId}.{Guid.NewGuid():N}", position,
                    caster.Character.CharacterId, 1) { LandingOrigin = targetPosition.Value });
            }
            return true;
        }

        public static int RecoverAtPosition(CombatSession session, BattleUnit unit, CombatRecoverySystem recoverySystem = null)
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
                knife.KnifeCount -= amount;
                if (knife.KnifeCount == 0) session.MapObjects.RemoveAt(index);
            }
            ScoutCombatSystem.RewardKnifeRecovery(unit, recovered, recoverySystem);
            return recovered;
        }

        internal static bool TryFindRandomDropPosition(CombatSession session,
            GridPosition target, out GridPosition position)
        {
            var candidates = new List<GridPosition>();
            for (var x = -1; x <= 1; x++) for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                var candidate = session.Grid.GetOffsetPosition(target, x, y);
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
        private readonly CombatRecoverySystem recoverySystem;
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
            recoverySystem = new CombatRecoverySystem(battleLog, feedbackBus);
            this.passiveEffectEventDispatcher = passiveEffectEventDispatcher;
        }

        public Dictionary<GridPosition, List<GridPosition>> CalculateReachablePaths(
            CombatSession session, 
            BattleUnit unit, bool forPlayerPreview = false)
        {
            return CalculateReachablePaths(
                session,
                unit,
                unit?.CurrentActionPoints ?? 0,
                unit?.MovementStepsThisTurn ?? 0, forPlayerPreview);
        }

        public Dictionary<GridPosition, List<GridPosition>>
            CalculateReachablePathsForNextTurn(
                CombatSession session,
                BattleUnit unit, bool forPlayerPreview = false)
        {
            var resume = unit?.HasUnfinishedCombatTurn == true && session?.ActiveUnit != unit;
            return CalculateReachablePaths(
                session,
                unit,
                resume ? unit.CurrentActionPoints : unit?.MaxActionPoints ?? 0,
                movementStepsAlreadyTaken: resume ? unit.MovementStepsThisTurn : 0, forPlayerPreview: forPlayerPreview);
        }

        private Dictionary<GridPosition, List<GridPosition>> CalculateReachablePaths(
            CombatSession session,
            BattleUnit unit,
            int availableActionPoints,
            int movementStepsAlreadyTaken, bool forPlayerPreview = false)
        {
            var reachablePaths = new Dictionary<
                GridPosition, 
                List<GridPosition>>();

            if (session?.Grid == null || unit?.Character == null 
                || unit.Character.LifeState != CharacterLifeState.Alive) return reachablePaths;

            if ((StatusEffectSystem.IsHardCrowdControlled(unit)
                    && !StatusEffectSystem.IsSleeping(unit))
                || StatusEffectSystem.IsImmobilized(unit) || unit.IsStationarySummon || SkeletonSlayerBossSystem.BlocksMovement(unit))
            {
                return reachablePaths;
            }

            var queue = new Queue<(GridPosition position, List<GridPosition> path)>();
            var labels = new Dictionary<(GridPosition cell, string traps, GridPosition direction), List<(int steps, int cost, float risk, int turns)>>();
            var best = new Dictionary<GridPosition, (float risk, int cost, int turns)>();
            queue.Enqueue((unit.Position, new List<GridPosition>()));
            while (queue.Count > 0)
            {
                var (current, currentPath) = queue.Dequeue();
                foreach (var candidate in session.Grid.GetNeighbours(current))
                {
                    var direction = (x: candidate.X - current.X, y: candidate.Y - current.Y);
                    if (candidate == unit.Position || currentPath.Contains(candidate)
                        || !session.CanMoveBetween(unit, current, candidate, forPlayerPreview)
                        || (forPlayerPreview ? IsKnownPositionOccupied(session, candidate, unit) : session.IsPositionOccupied(candidate, unit))
                        || session.DoesObjectBlockMovement(session.GetMapObjectAt(candidate))) continue;
                    var nextPath = new List<GridPosition>(currentPath) { candidate };
                    var cost = CalculatePathCost(session, unit.Position, movementStepsAlreadyTaken, nextPath, ScoutCombatSystem.MovementDiscount(unit), WarriorCombatSystem.FreeSteps(unit));
                    if (cost > availableActionPoints) continue;
                    var risk = session.EvaluateTrapRisk?.Invoke(unit, nextPath) ?? 0f;
                    var turns = CountPathTurns(unit.Position, nextPath);
                    var key = (candidate, session.TrapRouteState?.Invoke(nextPath) ?? "", new GridPosition(direction.x, direction.y));
                    if (!labels.TryGetValue(key, out var candidates)) labels[key] = candidates = new();
                    if (candidates.Exists(label => label.steps <= nextPath.Count && label.cost <= cost
                        && label.risk <= risk && label.turns <= turns)) continue;
                    candidates.RemoveAll(label => label.steps >= nextPath.Count && label.cost >= cost
                        && label.risk >= risk && label.turns >= turns);
                    candidates.Add((nextPath.Count, cost, risk, turns));
                    if (!best.TryGetValue(candidate, out var old) || risk < old.risk
                        || risk == old.risk && (cost < old.cost || cost == old.cost && turns < old.turns))
                    {
                        best[candidate] = (risk, cost, turns);
                        reachablePaths[candidate] = nextPath;
                    }
                    if (!session.IsExitPosition(candidate)) queue.Enqueue((candidate, nextPath));
                }
            }

            return reachablePaths;
        }

        private static bool IsKnownPositionOccupied(CombatSession session, GridPosition position, BattleUnit ignored)
        {
            if ((session.ExternalPreviewPositionOccupied ?? session.ExternalPositionOccupied)?.Invoke(position) == true) return true;
            foreach (var unit in session.Units)
                if (unit != null && unit != ignored && unit.Position == position
                    && (session.IsUnitKnownForMovementPreview?.Invoke(unit) ?? !StatusEffectSystem.IsInvisibleToPlayer(unit))) return true;
            return false;
        }

        public bool TryMoveAlongPath(
            CombatSession session,
            BattleUnit unit, 
            IReadOnlyList<GridPosition> path)
        {
            return TryMoveAlongPath(session, unit, path, out _);
        }

        public static int CountPathTurns(GridPosition origin, IReadOnlyList<GridPosition> path)
        {
            int turns = 0;
            var previousDirection = new GridPosition(0, 0);
            for (int i = 0; i < path.Count; i++)
            {
                var direction = new GridPosition(path[i].X - origin.X, path[i].Y - origin.Y);
                if (i > 0 && direction != previousDirection) turns++;
                previousDirection = direction; origin = path[i];
            }
            return turns;
        }

        public bool TryMoveAlongPath(
            CombatSession session,
            BattleUnit unit,
            IReadOnlyList<GridPosition> path,
            out List<GridPosition> completedPath,
            Func<bool> stopAfterStep = null)
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
                        out var movementCost, targetPosition))
                {
                    completed = false;
                    break;
                }

                completedPath.Add(targetPosition);
                totalMovementCost += movementCost;
                if (stopAfterStep?.Invoke() == true)
                {
                    completed = completedPath.Count == path.Count;
                    break;
                }
            }

            if (completedPath.Count > 0)
                resourceRuleSystem?.ProcessUnitMoved(unit, totalMovementCost, completedPath.Count);
            if (totalMovementCost > 0)
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    unit,
                    CombatFeedbackType.ActionPointSpend,
                    totalMovementCost));
                (passiveEffectEventDispatcher ?? session.PassiveEffectEventDispatcher)
                    ?.Publish(
                        PassiveEffectTriggerType.OnMove,
                        new PassiveEffectCombatContext(
                            session: session,
                            source: unit,
                            movementCost: ActionPointUnits.ToPoints(totalMovementCost),
                            movementSteps: unit.MovementStepsThisTurn,
                            distance: completedPath.Count,
                            eventValue: ActionPointUnits.ToPoints(totalMovementCost)));
                battleLog?.Invoke(
                    $"{unit.Character.Definition.DisplayName} " +
                    $"moves along a path for {ActionPointUnits.Format(totalMovementCost)} AP.");
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
            out int movementCost, GridPosition? requestedPosition = null)
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
                || StatusEffectSystem.IsImmobilized(unit) || unit.IsStationarySummon || SkeletonSlayerBossSystem.BlocksMovement(unit))
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

            var nextCell = requestedPosition ?? session.Grid.GetOffsetPosition(unit.Position, deltaX, deltaY);
            movementCost += session.Grid.GetMovementSurcharge(unit.Position, nextCell) * ActionPointUnits.PerPoint;
            if(WarriorCombatSystem.FreeStep(unit))movementCost=0;
            if (unit.CurrentActionPoints < movementCost)
            {
                return false;
            }

            var targetPosition = nextCell;
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

            ScoutCombatSystem.Face(unit, targetPosition);
            unit.Position = targetPosition;
            unit.CurrentActionPoints -= movementCost;
            if (publishFeedback && movementCost > 0)
            {
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    unit, CombatFeedbackType.ActionPointSpend, movementCost));
            }
            WarriorCombatSystem.Moved(unit);
            unit.MovementStepsThisTurn++;
            session.NotifyUnitEnteredCell(unit);
            ScoutCombatSystem.OnMoved(session, unit, statusEffectSystem);

            if (processResource)
            {
                resourceRuleSystem?.ProcessUnitMoved(unit, movementCost);
            }
            var recoveredKnives = ScoutKnifeSystem.RecoverAtPosition(session, unit, recoverySystem);
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
                            movementCost: ActionPointUnits.ToPoints(movementCost),
                            movementSteps: unit.MovementStepsThisTurn,
                            distance: 1,
                            eventValue: ActionPointUnits.ToPoints(movementCost)));
            }

            if (writeBattleLog)
            {
                battleLog?.Invoke(
                    $"{unit.Character.Definition.DisplayName} " +
                    $"moves to [{unit.Position.X}, {unit.Position.Y}] " +
                    $"for {ActionPointUnits.Format(movementCost)} AP.");
            }

            return true;
        }

        public static int CalculatePathCost(CombatSession session, GridPosition origin,
            int movementStepsAlreadyTaken, IReadOnlyList<GridPosition> path, int discount = 0, int freeSteps = 0)
        {
            var cost = 0;
            for(var i=1;i<=path.Count;i++)if(i>freeSteps)cost+=Math.Max(0,movementStepsAlreadyTaken+i-discount)*ActionPointUnits.PerPoint;
            var stepIndex=0;
            foreach (var cell in path)
            {
                if(++stepIndex>freeSteps)cost += session.Grid.GetMovementSurcharge(origin, cell) * ActionPointUnits.PerPoint;
                origin = cell;
            }
            return cost;
        }

        public static int CalculatePathCost(
            BattleUnit unit, 
            int pathLength)
        {
            var cost=0;
            for(var i=1;i<=pathLength;i++)if(i>WarriorCombatSystem.FreeSteps(unit))cost+=Math.Max(0,(unit?.MovementStepsThisTurn??0)+i-ScoutCombatSystem.MovementDiscount(unit))*ActionPointUnits.PerPoint;
            return cost;
        }

        public static int CalculatePathCost(
            int movementStepsAlreadyTaken,
            int pathLength)
        {
            var firstStepCost = movementStepsAlreadyTaken + 1;

            return pathLength
                * (2 * firstStepCost + pathLength - 1) 
                / 2 * ActionPointUnits.PerPoint;
        } 
    }
}
