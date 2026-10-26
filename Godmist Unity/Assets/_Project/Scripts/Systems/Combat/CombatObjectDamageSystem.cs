using System.Collections.Generic;
using System.Linq;
using System;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
namespace Godmist.Systems.Combat
{
    public static class CombatObjectDamageSystem
    {
        public static bool CanDamage(CombatMapObject obj) => obj is ObstacleCombatMapObject o
            && o.Source is { IsActive: true, IsDestructible: true };
        public static bool SkillDamagesMode(SkillData skill, int mode) => skill?.Effects != null
            && skill.Effects.Any(e => e != null && e.TargetModeIndex == mode && (e.EffectType == EffectType.DealDamage || e.EffectType == EffectType.Charge));
        public static List<ObstacleCombatMapObject> Targets(CombatSession session, BattleUnit caster,
            SkillData skill, int modeIndex, GridPosition? selected, int rotation = 0)
        {
            var result = new List<ObstacleCombatMapObject>();
            if (session?.Grid == null || caster == null || skill?.TargetModes == null
                || modeIndex < 0 || modeIndex >= skill.TargetModes.Count) return result;
            var mode = skill.TargetModes[modeIndex];
            if (mode == null || mode.TargetType == TargetType.Caster) return result;
            var targeting = new CombatTargetingService();
            HashSet<GridPosition> area;
            if (mode.TargetType == TargetType.All) area = session.MapObjects.Select(o => o.Position).ToHashSet();
            else if (mode.TargetType == TargetType.AreaAroundCaster)
                area = CombatTargetingService.IsDirectionalShape(mode.AbsoluteRange.Shape)
                    ? (selected.HasValue ? targeting.GetDirectionalRangePositions(session, caster, mode.AbsoluteRange, selected.Value,
                        rotationQuarterTurns: rotation).ToHashSet() : new())
                    : targeting.GetAbsoluteRangePositions(session, caster, mode).ToHashSet();
            else
            {
                if (!selected.HasValue || !CombatTargetingService.IsPositionWithinRange(caster.Position, selected.Value, mode.AbsoluteRange)) return result;
                var selectedObject = session.GetMapObjectAt(selected.Value);
                if (skill.RequiresLineOfSight && !new CombatLineOfSightService().HasLineOfSight(session, caster.Position,
                    selected.Value, CanDamage(selectedObject) ? selectedObject : null)) return result;
                area = mode.UseRelativeRange ? targeting.GetRelativeRangePositions(session, caster, mode, selected.Value, rotation).ToHashSet()
                    : new HashSet<GridPosition> { selected.Value };
            }
            var sight = new CombatLineOfSightService();
            foreach (var obj in session.MapObjects.OfType<ObstacleCombatMapObject>())
                if (CanDamage(obj) && area.Any(obj.Occupies)
                    && (!skill.RequiresLineOfSight
                        || sight.HasLineOfSight(
                            session,
                            caster.Position,
                            selected.HasValue && obj.Occupies(selected.Value) ? selected.Value : obj.Position,
                            obj))) result.Add(obj);
            return result;
        }
        public static bool ApplyDamage(CombatSession session, ObstacleCombatMapObject obj, int hits)
        {
            if (!CanDamage(obj) || hits <= 0) return false;
            var scope = session.ObjectDamageAction;
            if (scope != null && scope.Direct.Add(obj.ObjectId) && scope.Collisions.Contains(obj.ObjectId)) hits--;
            var changed = hits > 0 && obj.Source.ApplyHits(hits);
            if (changed) session.ObjectDurabilityChanged?.Invoke(obj);
            return changed;
        }
        // An action's barriers retain their original footprint after an earlier
        // effect destroys the source. Live Occupies() intentionally hides inactive props.
        public static bool IsCollisionBarrier(CombatSession session, GridPosition position)
            => session.ObjectDamageAction?.Barriers.Any(o => o.EnumerateOccupiedPositions().Contains(position)) == true;
        public static bool Impact(CombatSession session, GridPosition from, GridPosition to)
        {
            if (session?.Grid == null || from.Level != to.Level) return false;
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1) return false;
            if (dx == 0 || dy == 0)
            {
                if (session.Grid.HasBlockedEdge(from, to)) return false;
            }
            else
            {
                // A diagonal charge touches a corner, not a BattleCellEdge. The
                // impact can reach the obstacle through either open orthogonal side.
                var horizontal = new GridPosition(from.X + dx, from.Y, from.Level);
                var vertical = new GridPosition(from.X, from.Y + dy, from.Level);
                var throughHorizontal = session.Grid.IsWalkable(horizontal)
                    && !session.Grid.IsBlocked(horizontal)
                    && !session.Grid.HasBlockedEdge(from, horizontal)
                    && !session.Grid.HasBlockedEdge(horizontal, to);
                var throughVertical = session.Grid.IsWalkable(vertical)
                    && !session.Grid.IsBlocked(vertical)
                    && !session.Grid.HasBlockedEdge(from, vertical)
                    && !session.Grid.HasBlockedEdge(vertical, to);
                if (!throughHorizontal && !throughVertical) return false;
            }
            var obj = session.ObjectDamageAction?.Barriers.FirstOrDefault(o => o.EnumerateOccupiedPositions().Contains(to))
                ?? session.MapObjects.OfType<ObstacleCombatMapObject>().FirstOrDefault(o => o.Occupies(to) && o.BlocksMovement);
            if (obj == null || !CanDamage(obj)) return false;
            var scope = session.ObjectDamageAction;
            if (scope != null && (!scope.Collisions.Add(obj.ObjectId) || scope.Direct.Contains(obj.ObjectId))) return false;
            var changed = obj.Source.ApplyHits(1);
            if (changed) session.ObjectDurabilityChanged?.Invoke(obj);
            return changed;
        }
    }
}
