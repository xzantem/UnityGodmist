using System.Collections.Generic;
using System.Linq;
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
                if (CanDamage(obj) && area.Contains(obj.Position)
                    && (!skill.RequiresLineOfSight || sight.HasLineOfSight(session, caster.Position, obj.Position, obj))) result.Add(obj);
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
        public static bool IsCollisionBarrier(CombatSession session, GridPosition position)
            => session.ObjectDamageAction?.Barriers.Any(o => o.Position == position) == true;
        public static bool Impact(CombatSession session, GridPosition from, GridPosition to)
        {
            if (session?.Grid == null || session.Grid.HasBlockedEdge(from, to)) return false;
            var obj = session.ObjectDamageAction?.Barriers.FirstOrDefault(o => o.Position == to)
                ?? session.MapObjects.OfType<ObstacleCombatMapObject>().FirstOrDefault(o => o.Position == to && o.BlocksMovement);
            if (obj == null || !CanDamage(obj)) return false;
            var scope = session.ObjectDamageAction;
            if (scope != null && (!scope.Collisions.Add(obj.ObjectId) || scope.Direct.Contains(obj.ObjectId))) return false;
            var changed = obj.Source.ApplyHits(1);
            if (changed) session.ObjectDurabilityChanged?.Invoke(obj);
            return changed;
        }
    }
}
