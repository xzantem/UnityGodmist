using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public partial class CombatMovementSystem
    {
        // Cost-ordered search stops at the first legal casting position. Labels retain
        // both steps and cost: a cheaper arrival with more steps is not equivalent
        // when later steps become more expensive. Queries never move a live unit.
        public bool TryFindSkillApproach(CombatSession session, BattleUnit unit, int budget,
            Func<GridPosition, int, bool> canCastFrom, Func<GridPosition, bool> visible,
            out List<GridPosition> path, out int cost)
        {
            path = null; cost = 0;
            if (session?.Grid == null || unit?.Character?.LifeState != CharacterLifeState.Alive
                || canCastFrom == null || budget < 0) return false;
            if (canCastFrom(unit.Position, 0)) { path = new(); return true; }
            if ((StatusEffectSystem.IsHardCrowdControlled(unit) && !StatusEffectSystem.IsSleeping(unit)) || StatusEffectSystem.IsImmobilized(unit)
                || unit.IsStationarySummon || SkeletonSlayerBossSystem.BlocksMovement(unit)) return false;
            var queue = new SortedSet<(int Cost, int Steps, int Id, GridPosition Cell, List<GridPosition> Path)>(
                Comparer<(int Cost, int Steps, int Id, GridPosition Cell, List<GridPosition> Path)>.Create((a,b) =>
                { var c = a.Cost.CompareTo(b.Cost); if (c != 0) return c; c = a.Steps.CompareTo(b.Steps); return c != 0 ? c : a.Id.CompareTo(b.Id); }));
            var labels = new Dictionary<GridPosition, List<(int Steps, int Cost)>>();
            var id = 0;
            queue.Add((0, 0, id++, unit.Position, new List<GridPosition>()));
            var discount = ScoutCombatSystem.MovementDiscount(unit);
            var free = WarriorCombatSystem.FreeSteps(unit);
            while (queue.Count > 0)
            {
                var current = queue.Min; queue.Remove(current);
                if (current.Steps > 0 && canCastFrom(current.Cell, current.Cost))
                { path = current.Path; cost = current.Cost; return true; }
                if (current.Steps > 0 && session.IsExitPosition(current.Cell)) continue;
                foreach (var next in session.Grid.GetNeighbours(current.Cell))
                {
                    if (next == unit.Position || current.Path.Contains(next) || visible?.Invoke(next) == false
                        || !session.CanMoveBetween(unit, current.Cell, next, true)
                        || IsKnownPositionOccupied(session, next, unit)
                        || session.DoesObjectBlockMovement(session.GetMapObjectAt(next))) continue;
                    var candidate = new List<GridPosition>(current.Path) { next };
                    var nextCost = CalculatePathCost(session, unit.Position, unit.MovementStepsThisTurn, candidate, discount, free);
                    if (nextCost < 0 || nextCost > budget) continue;
                    if (!labels.TryGetValue(next, out var at)) labels[next] = at = new();
                    if (at.Exists(l => l.Steps <= candidate.Count && l.Cost <= nextCost)) continue;
                    at.RemoveAll(l => l.Steps >= candidate.Count && l.Cost >= nextCost);
                    at.Add((candidate.Count, nextCost));
                    queue.Add((nextCost, candidate.Count, id++, next, candidate));
                }
            }
            return false;
        }
    }
}
