using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public partial class SimpleCombatAiSystem
    {
        // At most two safe firing-position steps once per turn. Keep enough AP
        // for a basic attack; use real movement/resource rules rather than a free refill.
        private IEnumerable<bool> FindMomentumRepositionWork(CombatSession session, BattleUnit unit,
            SimpleCombatAiTurnState state, AiResult<List<GridPosition>> result)
        {
            if (state.MomentumRepositioned || unit.IsPlayerSide || unit.IsStationarySummon
                || unit.Character.ResourcePool?.ResourceType != ResourceType.Momentum
                || unit.Character.Definition?.Id?.StartsWith("enemy.", StringComparison.Ordinal) != true
                || ResourceRuleSystem.IsResourceGainBlocked(unit)
                || StatusEffectSystem.TryGetTaunter(session, unit, out _)) yield break;
            var specials = unit.Character.GetActiveSkills().Where(s => s != null
                && PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, s) > 0).ToArray();
            if (specials.Length == 0) yield break;
            if (!specials.Any(s => PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, s)
                > unit.Character.ResourcePool.CurrentValue)) yield break;
            var basic = unit.Character.GetBasicAttackSkill();
            if (basic == null || PassiveEffectCombatExecutionSystem.ResolveResourceCost(unit, basic) > 0) yield break;
            var budget = unit.CurrentActionPoints - CombatActionCostCalculator.CalculateSkillCost(unit, basic);
            if (budget <= 0) yield break;
            var target = FindClosestPlayerUnit(session, unit);
            var attacks = GetAvailableRangedAttacks(unit);
            if (target == null || attacks.Count == 0
                || !CanUseRangedSkillAgainst(session, target, unit.Position, attacks)) yield break;
            var path = new List<GridPosition>();
            var visited = new HashSet<GridPosition> { unit.Position };
            var from = unit.Position;
            for (var step = 0; step < 2; step++)
            {
                GridPosition? best = null; var bestDistance = -1; var bestCost = 0;
                foreach (var direction in MovementDirections)
                {
                    yield return false;
                    var candidate = session.Grid.GetOffsetPosition(from, direction.x, direction.y);
                    if (visited.Contains(candidate) || !session.CanMoveBetween(unit, from, candidate)
                        || session.IsPositionOccupied(candidate, unit)
                        || session.DoesObjectBlockMovement(session.GetMapObjectAt(candidate))
                        || !session.Grid.CanMoveBetween(from, candidate)
                        || SkillData.IsPointBlank(candidate.X - target.Position.X, candidate.Y - target.Position.Y)
                        || !CanUseRangedSkillAgainst(session, target, candidate, attacks)) continue;
                    var cost = (unit.MovementStepsThisTurn + path.Count + 1
                        + session.Grid.GetMovementSurcharge(from, candidate)) * ActionPointUnits.PerPoint;
                    cost = PassiveEffectCombatExecutionSystem.ApplyMovementCost(session, unit, cost);
                    if (cost <= 0 || cost > budget) continue;
                    var distance = Math.Abs(candidate.X - target.Position.X) + Math.Abs(candidate.Y - target.Position.Y);
                    if (distance <= bestDistance) continue;
                    best = candidate; bestDistance = distance; bestCost = cost;
                }
                if (!best.HasValue) break;
                from = best.Value; path.Add(from); visited.Add(from); budget -= bestCost;
            }
            if (path.Count > 0) result.Value = path;
        }
    }
}
