using System;
using System.Linq;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatTargetingService
    {
        public IReadOnlyList<BattleUnit> GetTargets(
            CombatSession session,
            BattleUnit caster,
            SkillTargetModeData mode,
            GridPosition? selectedPosition,
            bool includeDownedTargets = false,
            BattleUnit requiredEnemyTarget = null,
            int rotationQuarterTurns = 0)
        {
            var targets = new List<BattleUnit>();

            if (session == null
                || caster?.Character == null
                || mode == null
                || !session.Units.Contains(caster)
                || caster.Character.LifeState
                    != CharacterLifeState.Alive)
            {
                return targets;
            }

            if (mode.TargetType == TargetType.Caster)
            {
                targets.Add(caster);
                return targets;
            }

            switch (mode.TargetType)
            {
                case TargetType.Chain:
                case TargetType.MultipleChosen:
                case TargetType.Single:
                    if (!selectedPosition.HasValue
                        || (!IsValidTargetPosition(session, caster, mode, selectedPosition.Value)
                            && !(mode.IncludesCaster && selectedPosition.Value == caster.Position)))
                        break;

                    var referenceTarget = FindEligibleUnitAtPosition(
                        session,
                        caster,
                        mode.TargetingType,
                        selectedPosition.Value,
                        includeDownedTargets,
                        mode.IncludesCaster);

                    if (referenceTarget == null)
                        break;

                    if (mode.UseRelativeRange && mode.TargetType != TargetType.Chain) AddAreaTargets(session, caster, mode.TargetingType,
                        referenceTarget.Position, mode.RelativeRange, true, targets, includeDownedTargets,
                        rotationQuarterTurns);
                    else targets.Add(referenceTarget);

                    break;

                case TargetType.AreaAroundCaster:
                    if (IsDirectionalShape(
                            mode.AbsoluteRange?.Shape ?? RangeShape.None))
                    {
                        if (selectedPosition.HasValue
                            && IsValidDirectionSelection(
                                session,
                                caster,
                                mode.AbsoluteRange,
                                selectedPosition.Value))
                        {
                            AddDirectedAreaTargets(
                                session,
                                caster,
                                mode.TargetingType,
                                caster.Position,
                                selectedPosition.Value,
                                mode.AbsoluteRange,
                                targets,
                                includeDownedTargets,
                                rotationQuarterTurns);
                        }
                    }
                    else
                    {
                        AddAreaTargets(
                            session,
                            caster,
                            mode.TargetingType,
                            caster.Position,
                            mode.AbsoluteRange,
                            false,
                            targets,
                            includeDownedTargets);
                    }

                    break;

                case TargetType.AreaAroundTile:
                    if (selectedPosition.HasValue
                        && IsValidTargetPosition(
                            session,
                            caster,
                            mode,
                            selectedPosition.Value))
                    {
                        if (mode.UseRelativeRange)
                        {
                            AddAreaTargets(session, caster, mode.TargetingType, selectedPosition.Value, 
                                mode.RelativeRange, true, targets, includeDownedTargets,
                                rotationQuarterTurns);
                        }
                        else
                        {
                            AddUnitAtPosition(session, caster, mode.TargetingType,  selectedPosition.Value, targets, includeDownedTargets);
                        }
                    }

                    break;
                case TargetType.All:
                    foreach (var unit in session.Units)
                    {
                        if (StatusEffectSystem.IsInvisible(unit))
                        {
                            continue;
                        }
                        if (IsEligibleTarget(caster, unit, mode.TargetingType, includeDownedTargets))
                        {
                            targets.Add(unit);
                        }
                    }

                    break;
            }

            if (mode.TargetType == TargetType.Chain && targets.Count == 1)
            {
                while(targets.Count < mode.ChainTargets)
                {
                    var previous = targets[targets.Count-1];
                    var next = session.Units.Where(u => !targets.Contains(u) && IsEligibleTarget(caster,u,mode.TargetingType,includeDownedTargets)
                        && !StatusEffectSystem.IsInvisible(u) && IsPositionWithinRange(previous.Position,u.Position,mode.RelativeRange)
                        && new CombatLineOfSightService().HasLineOfSight(session,previous.Position,u.Position))
                        .OrderBy(u => Math.Abs(u.Position.X-previous.Position.X)+Math.Abs(u.Position.Y-previous.Position.Y))
                        .ThenBy(u => u.Character.CharacterId,StringComparer.Ordinal).FirstOrDefault();
                    if(next==null)break;
                    targets.Add(next);
                }
            }
            var context = session.DungeonContext;
            if (context?.SurfaceMap?.HasElevatedSurfaces == true)
            {
                var center = mode.TargetType == TargetType.AreaAroundTile
                    || (mode.TargetType == TargetType.Single && mode.UseRelativeRange)
                    ? selectedPosition ?? caster.Position : caster.Position;
                var origin = DungeonBattleGridFactory.ToDungeonTraversalPosition(context, center);
                targets.RemoveAll(unit => context.SurfaceMap.BlocksFloorSight(origin,
                    DungeonBattleGridFactory.ToDungeonTraversalPosition(context, unit.Position)));
            }

            if (requiredEnemyTarget != null
                && mode.TargetingType == TargetingType.Enemies
                && !targets.Contains(requiredEnemyTarget))
            {
                targets.Clear();
            }

            return targets;
        }

        public IReadOnlyList<GridPosition>
            GetSelectablePositions(
                CombatSession session,
                BattleUnit caster,
                SkillTargetModeData mode,
                bool includeDownedTargets = false,
                BattleUnit requiredEnemyTarget = null)
        {
            var positions = new List<GridPosition>();

            if (session?.Grid == null
                || caster?.Character == null
                || mode == null)
            {
                return positions;
            }

            if (IsDirectionalCasterArea(mode))
            {
                foreach (var position in GetDirectionSelectionPositions(
                             session,
                             caster,
                             mode.AbsoluteRange))
                {
                    positions.Add(position);
                }

                return positions;
            }

            if (!RequiresTileSelection(mode))
            {
                return positions;
            }

            for (var x = (int)Math.Max(0L, (long)caster.Position.X - (mode.AbsoluteRange?.MaxRange ?? 0));
                 x <= Math.Min(session.Grid.Width - 1L, (long)caster.Position.X + (mode.AbsoluteRange?.MaxRange ?? 0)); x++)
            {
                for (var y = (int)Math.Max(0L, (long)caster.Position.Y - (mode.AbsoluteRange?.MaxRange ?? 0));
                     y <= Math.Min(session.Grid.Height - 1L, (long)caster.Position.Y + (mode.AbsoluteRange?.MaxRange ?? 0)); y++)
                {
                    foreach (var position in session.Grid.GetPositionsAt(x, y))
                    {

                    if (!IsValidTargetPosition(session, caster, mode, position)
                        && !(mode.IncludesCaster && position == caster.Position))
                    {
                        continue;
                    }

                    var canTargetEmptyTile = mode.TargetType == TargetType.AreaAroundTile
                        && requiredEnemyTarget == null;

                    if (canTargetEmptyTile || GetTargets(session, caster, mode, position,
                            includeDownedTargets, requiredEnemyTarget).Count > 0)
                    {
                        positions.Add(position);
                    }
                }
                }
            }

            return positions;
        }

        public IReadOnlyList<GridPosition> GetAbsoluteRangePositions(
            CombatSession session,
            BattleUnit caster,
            SkillTargetModeData mode)
        {
            var positions = new List<GridPosition>();

            if (session?.Grid == null
                || caster?.Character == null
                || mode == null)
            {
                return positions;
            }

            if (mode.TargetType == TargetType.AreaAroundCaster)
            {
                if (IsDirectionalCasterArea(mode))
                {
                    return GetDirectionSelectionPositions(
                        session,
                        caster,
                        mode.AbsoluteRange);
                }

                for (var x = (int)Math.Max(0L, (long)caster.Position.X - (mode.AbsoluteRange?.MaxRange ?? 0));
                 x <= Math.Min(session.Grid.Width - 1L, (long)caster.Position.X + (mode.AbsoluteRange?.MaxRange ?? 0)); x++)
                {
                    for (var y = (int)Math.Max(0L, (long)caster.Position.Y - (mode.AbsoluteRange?.MaxRange ?? 0));
                     y <= Math.Min(session.Grid.Height - 1L, (long)caster.Position.Y + (mode.AbsoluteRange?.MaxRange ?? 0)); y++)
                    {
                        foreach (var position in session.Grid.GetPositionsAt(x, y))
                    {

                        if (IsPositionWithinRange(
                                caster.Position,
                                position,
                                mode.AbsoluteRange))
                        {
                            positions.Add(position);
                        }
                    }
                }
                }

                return positions;
            }

            if (!RequiresTileSelection(mode))
            {
                return positions;
            }

            for (var x = (int)Math.Max(0L, (long)caster.Position.X - (mode.AbsoluteRange?.MaxRange ?? 0));
                 x <= Math.Min(session.Grid.Width - 1L, (long)caster.Position.X + (mode.AbsoluteRange?.MaxRange ?? 0)); x++)
            {
                for (var y = (int)Math.Max(0L, (long)caster.Position.Y - (mode.AbsoluteRange?.MaxRange ?? 0));
                     y <= Math.Min(session.Grid.Height - 1L, (long)caster.Position.Y + (mode.AbsoluteRange?.MaxRange ?? 0)); y++)
                {
                    foreach (var position in session.Grid.GetPositionsAt(x, y))
                    {

                    if (IsPositionWithinRange(
                            caster.Position,
                            position,
                            mode.AbsoluteRange))
                    {
                        positions.Add(position);
                    }
                }
                }
            }

            return positions;
        }

        public IReadOnlyList<GridPosition> GetRelativeRangePositions(
            CombatSession session,
            BattleUnit caster,
            SkillTargetModeData mode,
            GridPosition center,
            int rotationQuarterTurns = 0)
        {
            if (mode == null || !mode.UseRelativeRange)
            {
                return new List<GridPosition>();
            }

            return GetRelativeRangePositions(
                session,
                caster,
                mode.RelativeRange,
                center,
                rotationQuarterTurns);
        }

        public IReadOnlyList<GridPosition> GetRelativeRangePositions(
            CombatSession session,
            BattleUnit caster,
            SkillRangeData range,
            GridPosition center,
            int rotationQuarterTurns = 0)
        {
            var positions = new List<GridPosition>();

            if (session?.Grid == null || caster?.Character == null || range == null || range.Shape == RangeShape.None)
            {
                return positions;
            }

            if (IsDirectionalShape(range.Shape))
            {
                return GetDirectionalRangePositions(
                    session,
                    caster,
                    range,
                    center,
                    center,
                    rotationQuarterTurns);
            }

            for (var x = (int)Math.Max(0L, (long)center.X - range.MaxRange); x <= Math.Min(session.Grid.Width - 1L, (long)center.X + range.MaxRange); x++)
            {
                for (var y = (int)Math.Max(0L, (long)center.Y - range.MaxRange); y <= Math.Min(session.Grid.Height - 1L, (long)center.Y + range.MaxRange); y++)
                {
                    foreach (var position in session.Grid.GetPositionsAt(x, y))
                    {

                    if (IsPositionWithinRelativeRange(
                            caster.Position,
                            center,
                            position,
                            range,
                            rotationQuarterTurns))
                    {
                        positions.Add(position);
                    }
                }
                }
            }

            return positions;
        }

        public IReadOnlyList<GridPosition> GetPositionsInRange(
            CombatSession session,
            GridPosition center,
            SkillRangeData range)
        {
            var positions = new List<GridPosition>();

            if (session?.Grid == null || range == null || range.Shape == RangeShape.None)
            {
                return positions;
            }

            for (var x = (int)Math.Max(0L, (long)center.X - range.MaxRange); x <= Math.Min(session.Grid.Width - 1L, (long)center.X + range.MaxRange); x++)
            {
                for (var y = (int)Math.Max(0L, (long)center.Y - range.MaxRange); y <= Math.Min(session.Grid.Height - 1L, (long)center.Y + range.MaxRange); y++)
                {
                    foreach (var position in session.Grid.GetPositionsAt(x, y))
                    {

                    if (IsPositionWithinRange(center, position, range))
                    {
                        positions.Add(position);
                    }
                }
                }
            }

            return positions;
        }

        public IReadOnlyList<GridPosition> GetDirectionalRangePositions(
            CombatSession session,
            BattleUnit caster,
            SkillRangeData range,
            GridPosition directionReference,
            GridPosition? originOverride = null,
            int rotationQuarterTurns = 0)
        {
            var positions = new List<GridPosition>();
            if (session?.Grid == null
                || caster?.Character == null
                || range == null
                || !IsDirectionalShape(range.Shape)
                || (range.Shape != RangeShape.Rectangle
                    && !IsValidDirectionSelection(
                        session,
                        caster,
                        range,
                        directionReference)))
            {
                return positions;
            }

            var directionX = range.Shape == RangeShape.Rectangle
                ? 1
                : Math.Sign(directionReference.X - caster.Position.X);
            var directionY = range.Shape == RangeShape.Rectangle
                ? 0
                : Math.Sign(directionReference.Y - caster.Position.Y);
            if (directionX == 0 && directionY == 0)
            {
                return positions;
            }

            if ((range.Shape == RangeShape.Diagonal || range.Shape == RangeShape.ThreeRays)
                && directionX != 0
                && directionY != 0)
            {
                var distanceX = Math.Abs(
                    directionReference.X - caster.Position.X);
                var distanceY = Math.Abs(
                    directionReference.Y - caster.Position.Y);
                if (distanceX >= distanceY)
                {
                    directionY = 0;
                }
                else
                {
                    directionX = 0;
                }
            }
            RotateDirection(ref directionX, ref directionY, rotationQuarterTurns);
            var origin = originOverride ?? caster.Position;
            var candidates = new HashSet<GridPosition>();

            switch (range.Shape)
            {
                case RangeShape.Line:
                    AddRay(candidates, origin, directionX, directionY, range);
                    break;

                case RangeShape.ThreeRays:
                    AddRay(candidates, origin, directionX, directionY, range);
                    AddDiagonalRays(candidates, origin, directionX, directionY, range);
                    break;

                case RangeShape.TwoSidedLine:
                    AddRay(candidates, origin, directionX, directionY, range);
                    AddRay(candidates, origin, -directionX, -directionY, range);
                    break;

                case RangeShape.Diagonal:
                    AddDiagonalRays(candidates, origin, directionX, directionY, range);
                    break;

                case RangeShape.Rectangle:
                    AddRectangle(candidates, origin, directionX, directionY, range);
                    break;
            }

            foreach (var position in candidates)
            {
                if (session.Grid.IsWithinBounds(position))
                {
                    foreach (var layered in session.Grid.GetPositionsAt(position.X, position.Y)) positions.Add(layered);
                }
            }

            return positions;
        }

        public static bool IsPositionWithinRange(
            GridPosition origin,
            GridPosition position,
            SkillRangeData range)
        {
            if (range == null)
            {
                return false;
            }

            var distanceX = Math.Abs(position.X - origin.X);
            var distanceY = Math.Abs(position.Y - origin.Y);

            switch (range.Shape)
            {
                case RangeShape.Square:
                {
                    var distance = Math.Max(
                        distanceX,
                        distanceY);

                    return distance >= range.MinRange
                        && distance <= range.MaxRange;
                }

                case RangeShape.Radial:
                case RangeShape.Ring:
                {
                    var roundedDistance = Math.Floor(Math.Sqrt(
                        (double)distanceX * distanceX + (double)distanceY * distanceY) + 0.5d);
                    return roundedDistance >= range.MinRange
                        && roundedDistance <= range.MaxRange;
                }

                case RangeShape.XShape:
                    return distanceX == distanceY
                        && distanceX >= range.MinRange
                        && distanceX <= range.MaxRange;

                default:
                    return false;
            }
        }
        public static bool IsPositionWithinRelativeRange(
            GridPosition casterPosition,
            GridPosition referencePosition,
            GridPosition position,
            SkillRangeData range,
            int rotationQuarterTurns = 0)
        {
            if (range == null)
            {
                return false;
            }

            if (range.Shape != RangeShape.Square)
            {
                return IsPositionWithinRange(
                    referencePosition,
                    position,
                    range);
            }

            var sideLength = range.MaxRange - range.MinRange + 1;

            if (sideLength <= 0)
            {
                return false;
            }

            if (sideLength % 2 == 1)
            {
                var radius = sideLength / 2;
                var distanceX = Math.Abs(
                    position.X - referencePosition.X);

                var distanceY = Math.Abs(
                    position.Y - referencePosition.Y);

                return distanceX <= radius
                    && distanceY <= radius;
            }

            // A square's orientation is controlled exclusively by the rotation
            // state. It must not change merely because the selected tile is on a
            // different side of the caster.
            var directionX = 1;
            var directionY = 1;

            RotateDirection(
                ref directionX,
                ref directionY,
                rotationQuarterTurns);

            var offsetX = (position.X - referencePosition.X)
                * directionX;

            var offsetY = (position.Y - referencePosition.Y)
                * directionY;

            return offsetX >= 0 && offsetX < sideLength
                && offsetY >= 0 && offsetY < sideLength;
        }

        private static bool IsValidTargetPosition(
            CombatSession session,
            BattleUnit caster,
            SkillTargetModeData mode,
            GridPosition position)
        {
            return session.Grid != null
                && session.Grid.IsWithinBounds(position)
                && (session.Grid.IsWalkable(position) || position == caster.Position)
                && IsPositionWithinRange(
                    caster.Position,
                    position,
                    mode.AbsoluteRange);
        }

        public static bool IsDirectionalShape(RangeShape shape)
        {
            return shape == RangeShape.ThreeRays || shape == RangeShape.Line
                || shape == RangeShape.Diagonal
                || shape == RangeShape.TwoSidedLine
                || shape == RangeShape.Rectangle;
        }

        public static bool IsRotatableRelativeShape(RangeShape shape)
        {
            return shape == RangeShape.Square
                || IsDirectionalShape(shape);
        }

        private static bool IsDirectionalCasterArea(SkillTargetModeData mode)
        {
            return mode?.TargetType == TargetType.AreaAroundCaster
                && IsDirectionalShape(mode.AbsoluteRange?.Shape ?? RangeShape.None);
        }

        private static IReadOnlyList<GridPosition> GetDirectionSelectionPositions(
            CombatSession session,
            BattleUnit caster,
            SkillRangeData range)
        {
            var positions = new List<GridPosition>();
            if (session?.Grid == null || caster == null || range == null)
            {
                return positions;
            }

            for (var x = 0; x < session.Grid.Width; x++)
            {
                for (var y = 0; y < session.Grid.Height; y++)
                {
                    foreach (var position in session.Grid.GetPositionsAt(x, y))
                    {
                    if (position == caster.Position)
                    {
                        continue;
                    }

                    positions.Add(position);
                }
                }
            }

            return positions;
        }

        private static bool IsValidDirectionSelection(
            CombatSession session,
            BattleUnit caster,
            SkillRangeData range,
            GridPosition position)
        {
            if (session?.Grid == null || caster == null || range == null
                || !session.Grid.IsWithinBounds(position))
            {
                return false;
            }

            var deltaX = Math.Sign(position.X - caster.Position.X);
            var deltaY = Math.Sign(position.Y - caster.Position.Y);
            if (deltaX == 0 && deltaY == 0) return false;
            return true;
        }

        private static void AddRay(
            ISet<GridPosition> positions,
            GridPosition origin,
            int directionX,
            int directionY,
            SkillRangeData range)
        {
            for (var distance = range.MinRange; distance <= range.MaxRange; distance++)
            {
                positions.Add(new GridPosition(
                    origin.X + directionX * distance,
                    origin.Y + directionY * distance));
            }
        }

        private static void AddDiagonalRays(
            ISet<GridPosition> positions,
            GridPosition origin,
            int directionX,
            int directionY,
            SkillRangeData range)
        {
            if (directionX != 0)
            {
                AddRay(positions, origin, directionX, 1, range);
                AddRay(positions, origin, directionX, -1, range);
                return;
            }

            AddRay(positions, origin, 1, directionY, range);
            AddRay(positions, origin, -1, directionY, range);
        }

        private static void AddRectangle(
            ISet<GridPosition> positions,
            GridPosition origin,
            int directionX,
            int directionY,
            SkillRangeData range)
        {
            var perpendicularX = -directionY;
            var perpendicularY = directionX;
            var lateralMin = range.RectangleWidth % 2 == 0
                ? 0
                : -(range.RectangleWidth / 2);
            var lateralMax = lateralMin + range.RectangleWidth - 1;

            var finalDistance = range.MinRange + range.MaxRange - 1;
            for (var distance = range.MinRange; distance <= finalDistance; distance++)
            {
                for (var lateral = lateralMin; lateral <= lateralMax; lateral++)
                {
                    positions.Add(new GridPosition(
                        origin.X + directionX * distance + perpendicularX * lateral,
                        origin.Y + directionY * distance + perpendicularY * lateral));
                }
            }
        }

        private static void RotateDirection(
            ref int directionX,
            ref int directionY,
            int rotationQuarterTurns)
        {
            for (var index = 0; index < (rotationQuarterTurns % 4 + 4) % 4; index++)
            {
                (directionX, directionY) = (-directionY, directionX);
            }
        }

        private void AddDirectedAreaTargets(
            CombatSession session,
            BattleUnit caster,
            TargetingType targetingType,
            GridPosition origin,
            GridPosition directionReference,
            SkillRangeData range,
            List<BattleUnit> targets,
            bool includeDownedTargets,
            int rotationQuarterTurns = 0)
        {
            var affectedPositions = new HashSet<GridPosition>(
                GetDirectionalRangePositions(
                    session,
                    caster,
                    range,
                    directionReference,
                    origin,
                    rotationQuarterTurns));
            foreach (var unit in session.Units)
            {
                if (IsEligibleTarget(caster, unit, targetingType, includeDownedTargets, true)
                    && affectedPositions.Contains(unit.Position))
                {
                    targets.Add(unit);
                }
            }
        }

        private static void AddUnitAtPosition(
            CombatSession session,
            BattleUnit caster,
            TargetingType targetingType,
            GridPosition position,
            List<BattleUnit> targets, bool includeDownedTargets)
        {
            foreach (var unit in session.Units)
            {
                if (unit != null && unit.Position == position &&
                    IsEligibleTarget(caster, unit, targetingType, includeDownedTargets, false))
                {
                    targets.Add(unit);
                    return;
                }
            }
        }
        private static BattleUnit FindEligibleUnitAtPosition(
            CombatSession session,
            BattleUnit caster,
            TargetingType targetingType,
            GridPosition position,
            bool includeDownedTargets,
            bool includesCaster = false)
        {
            foreach (var unit in session.Units)
            {
                if (unit != null && unit.Position == position
                    && IsEligibleTarget(caster, unit, targetingType, includeDownedTargets, false, includesCaster))
                {
                    return unit;
                }
            }

            return null;
        }

        private void AddAreaTargets(
            CombatSession session,
            BattleUnit caster,
            TargetingType targetingType,
            GridPosition center,
            SkillRangeData range,
            bool isRelativeRange,
            List<BattleUnit> targets, bool includeDownedTargets,
            int rotationQuarterTurns = 0)
        {
            if (isRelativeRange && IsDirectionalShape(range?.Shape ?? RangeShape.None))
            {
                AddDirectedAreaTargets(
                    session,
                    caster,
                    targetingType,
                    center,
                    center,
                    range,
                    targets,
                    includeDownedTargets,
                    rotationQuarterTurns);
                return;
            }

            foreach (var unit in session.Units)
            {
                if (!IsEligibleTarget(caster, unit, targetingType, includeDownedTargets, true))
                    continue;

                var isInRange = isRelativeRange
                    ? IsPositionWithinRelativeRange(
                        caster.Position,
                        center,
                        unit.Position,
                        range,
                        rotationQuarterTurns)
                    : IsPositionWithinRange(center, unit.Position, range);

                if (isInRange)
                    targets.Add(unit);
            }
        }

        private static bool IsEligibleTarget(
            BattleUnit caster,
            BattleUnit target,
            TargetingType targetingType,
            bool includeDownedTargets,
            bool allowInvisibleEnemyTarget = false,
            bool includesCaster = false)
        {
            if (target?.Character == null || target.Character.LifeState == CharacterLifeState.Dead) return false;

            if (target.Character.LifeState != CharacterLifeState.Alive && 
                (!includeDownedTargets || target.Character.LifeState != CharacterLifeState.Downed)) return false;

            if (StatusEffectSystem.IsParalyzed(target)) return false;

            if (!allowInvisibleEnemyTarget && IsInvisibleToEnemy(caster, target)) return false;

            return targetingType switch
            {
                TargetingType.Enemies =>
                    target.IsPlayerSide != caster.IsPlayerSide,

                TargetingType.Allies =>
                    target.IsPlayerSide == caster.IsPlayerSide
                    && (includesCaster || !ReferenceEquals(target, caster)),

                TargetingType.All => true,

                _ => false
            };
        }

        private static bool RequiresTileSelection(
            SkillTargetModeData mode)
        {
            return mode.TargetType == TargetType.Single || mode.TargetType == TargetType.MultipleChosen || mode.TargetType == TargetType.Chain
                || mode.TargetType == TargetType.AreaAroundTile;
        }
        public IReadOnlyList<GridPosition> GetEligibleTargetPositions(
            CombatSession session,
            BattleUnit caster,
            SkillTargetModeData mode,
            bool includeDownedTargets = false,
            BattleUnit requiredEnemyTarget = null)
        {
            var positions = new HashSet<GridPosition>();

            if (session?.Grid == null
                || caster?.Character == null
                || mode == null)
            {
                return new List<GridPosition>();
            }

            if (mode.TargetType == TargetType.AreaAroundCaster
                || mode.TargetType == TargetType.All)
            {
                foreach (var target in GetTargets(
                            session,
                            caster,
                            mode,
                            null,
                            includeDownedTargets,
                            requiredEnemyTarget))
                {
                    if (IsInvisibleToEnemy(caster, target))
                    {
                        continue;
                    }

                    positions.Add(target.Position);
                }

                return new List<GridPosition>(positions);
            }

            if (!RequiresTileSelection(mode))
            {
                return new List<GridPosition>();
            }

            if (mode.TargetType == TargetType.Single || mode.TargetType == TargetType.MultipleChosen || mode.TargetType == TargetType.Chain)
            {
                foreach (var unit in session.Units)
                {
                    if (unit == null
                        || !IsEligibleTarget(
                            caster,
                            unit,
                            mode.TargetingType,
                            includeDownedTargets,
                            false,
                            mode.IncludesCaster)
                        || (!IsValidTargetPosition(
                            session,
                            caster,
                            mode,
                            unit.Position)
                            && !(mode.IncludesCaster && unit == caster)))
                    {
                        continue;
                    }

                    positions.Add(unit.Position);
                }

                return new List<GridPosition>(positions);
            }

            foreach (var center in GetSelectablePositions(
                         session,
                         caster,
                         mode,
                         includeDownedTargets,
                         requiredEnemyTarget))
            {
                foreach (var target in GetTargets(
                             session,
                             caster,
                             mode,
                             center,
                             includeDownedTargets,
                             requiredEnemyTarget))
                {
                    if (IsInvisibleToEnemy(caster, target))
                    {
                        continue;
                    }

                    positions.Add(target.Position);
                }
            }

            return new List<GridPosition>(positions);
        }

        private static bool IsInvisibleToEnemy(
            BattleUnit observer,
            BattleUnit target)
        {
            return StatusEffectSystem.IsInvisible(target)
                && observer != null
                && target != null
                && observer.IsPlayerSide != target.IsPlayerSide;
        }
    }
}
