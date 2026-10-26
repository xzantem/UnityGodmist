using System.Collections.Generic;
using System.Linq;
using Godmist.Runtime.Combat;
using Godmist.Core.Enums;

namespace Godmist.Systems.Combat
{
    public static class CombatPersistentVfxGeometry
    {
        public static List<GridPosition> FieldCells(CombatSession session, SummonedCombatMapObject field)
        {
            var source = session?.Units.FirstOrDefault(u => u.Character?.CharacterId == field?.CreatorCharacterId);
            if (source == null || session.Grid == null || !PaladinCombatSystem.IsField(field) || field.IsExpired()) return new();
            var radius = 2 + PaladinCombatSystem.C(source, "field_range");
            return Nearby(session, field.Position, radius).Where(p => session.Grid.IsWalkable(p)
                && PaladinCombatSystem.FieldContains(session, field, p)).ToList();
        }

        public static List<GridPosition> AuraCells(CombatSession session, BattleUnit owner, CombatAuraRuntime aura)
        {
            if (session?.Grid == null || owner?.Character?.LifeState != CharacterLifeState.Alive
                || aura == null || aura.RemainingTurns <= 0) return new();
            var context = session.DungeonContext;
            return Nearby(session, owner.Position, aura.Radius).Where(p => session.Grid.IsWalkable(p)
                && (aura.IncludesCaster || p != owner.Position)
                && (context?.SurfaceMap?.HasElevatedSurfaces != true || !context.SurfaceMap.BlocksFloorSight(
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(context, owner.Position),
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(context, p)))).ToList();
        }

        static IEnumerable<GridPosition> Nearby(CombatSession session, GridPosition center, int radius)
        {
            radius = System.Math.Max(0, radius);
            for (var x = System.Math.Max(0, center.X - radius); x <= System.Math.Min(session.Grid.Width - 1, center.X + radius); x++)
                for (var y = System.Math.Max(0, center.Y - radius); y <= System.Math.Min(session.Grid.Height - 1, center.Y + radius); y++)
                    foreach (var position in session.Grid.GetPositionsAt(x, y)) yield return position;
        }
    }
}
