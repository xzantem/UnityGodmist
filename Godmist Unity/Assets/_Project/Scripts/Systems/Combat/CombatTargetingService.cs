using System;
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
            bool includeDownedTargets = false)
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
                case TargetType.Single:
                    if (!selectedPosition.HasValue || !IsValidTargetPosition(session, caster, mode, selectedPosition.Value))
                        break;

                    var referenceTarget = FindEligibleUnitAtPosition(session, caster, mode.TargetingType, selectedPosition.Value, includeDownedTargets);

                    if (referenceTarget == null)
                        break;

                    if (mode.UseRelativeRange) AddAreaTargets(session, caster, mode.TargetingType, 
                        referenceTarget.Position, mode.RelativeRange, true, targets, includeDownedTargets);
                    else targets.Add(referenceTarget);

                    break;

                case TargetType.AreaAroundCaster:
                    AddAreaTargets(session, caster, mode.TargetingType, caster.Position, mode.AbsoluteRange, false, targets, includeDownedTargets);

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
                                mode.RelativeRange, true, targets, includeDownedTargets);
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
                        if (IsEligibleTarget(caster, unit, mode.TargetingType, includeDownedTargets))
                        {
                            targets.Add(unit);
                        }
                    }

                    break;
            }

            return targets;
        }

        public IReadOnlyList<GridPosition>
            GetSelectablePositions(
                CombatSession session,
                BattleUnit caster,
                SkillTargetModeData mode,
                bool includeDownedTargets = false)
        {
            var positions = new List<GridPosition>();

            if (session?.Grid == null
                || caster?.Character == null
                || mode == null
                || !RequiresTileSelection(mode))
            {
                return positions;
            }

            for (var x = 0; x < session.Grid.Width; x++)
            {
                for (var y = 0; y < session.Grid.Height; y++)
                {
                    var position = new GridPosition(x, y);

                    if (!IsValidTargetPosition(session, caster, mode, position))
                    {
                        continue;
                    }

                    if (GetTargets(session, caster, mode, position, includeDownedTargets).Count > 0)
                    {
                        positions.Add(position);
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
                for (var x = 0; x < session.Grid.Width; x++)
                {
                    for (var y = 0; y < session.Grid.Height; y++)
                    {
                        var position = new GridPosition(x, y);

                        if (IsPositionWithinRange(
                                caster.Position,
                                position,
                                mode.AbsoluteRange))
                        {
                            positions.Add(position);
                        }
                    }
                }

                return positions;
            }

            if (!RequiresTileSelection(mode))
            {
                return positions;
            }

            for (var x = 0; x < session.Grid.Width; x++)
            {
                for (var y = 0; y < session.Grid.Height; y++)
                {
                    var position = new GridPosition(x, y);

                    if (IsPositionWithinRange(
                            caster.Position,
                            position,
                            mode.AbsoluteRange))
                    {
                        positions.Add(position);
                    }
                }
            }

            return positions;
        }

        public IReadOnlyList<GridPosition> GetRelativeRangePositions(
            CombatSession session,
            BattleUnit caster,
            SkillTargetModeData mode,
            GridPosition center)
        {
            var positions = new List<GridPosition>();

            if (session?.Grid == null
                || mode == null
                || !mode.UseRelativeRange)
            {
                return positions;
            }

            for (var x = 0; x < session.Grid.Width; x++)
            {
                for (var y = 0; y < session.Grid.Height; y++)
                {
                    var position = new GridPosition(x, y);

                    if (IsPositionWithinRelativeRange(
                            caster.Position,
                            center,
                            position,
                            mode.RelativeRange))
                    {
                        positions.Add(position);
                    }
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
                    var squaredDistance =
                        distanceX * distanceX
                        + distanceY * distanceY;

                    return squaredDistance
                            >= range.MinRange * range.MinRange
                        && squaredDistance
                            <= range.MaxRange * range.MaxRange;
                }

                default:
                    return false;
            }
        }
        private static bool IsPositionWithinRelativeRange(
            GridPosition casterPosition,
            GridPosition referencePosition,
            GridPosition position,
            SkillRangeData range)
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

            var directionX = Math.Sign(
                referencePosition.X - casterPosition.X);

            var directionY = Math.Sign(
                referencePosition.Y - casterPosition.Y);

            if (directionX == 0) directionX = 1;
            if (directionY == 0) directionY = 1;

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
                && IsPositionWithinRange(
                    caster.Position,
                    position,
                    mode.AbsoluteRange);
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
                if (unit != null && unit.Position == position && IsEligibleTarget(caster, unit, targetingType, includeDownedTargets))
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
            GridPosition position, bool includeDownedTargets)
        {
            foreach (var unit in session.Units)
            {
                if (unit != null && unit.Position == position
                    && IsEligibleTarget(caster, unit, targetingType, includeDownedTargets))
                {
                    return unit;
                }
            }

            return null;
        }

        private static void AddAreaTargets(
            CombatSession session,
            BattleUnit caster,
            TargetingType targetingType,
            GridPosition center,
            SkillRangeData range,
            bool isRelativeRange,
            List<BattleUnit> targets, bool includeDownedTargets)
        {
            foreach (var unit in session.Units)
            {
                if (!IsEligibleTarget(caster, unit, targetingType, includeDownedTargets))
                    continue;

                var isInRange = isRelativeRange ? IsPositionWithinRelativeRange(caster.Position, center,unit.Position, range)
                    : IsPositionWithinRange(center, unit.Position, range);

                if (isInRange)
                    targets.Add(unit);
            }
        }

        private static bool IsEligibleTarget(
            BattleUnit caster,
            BattleUnit target,
            TargetingType targetingType, bool includeDownedTargets)
        {
            if (target?.Character == null || target.Character.LifeState == CharacterLifeState.Dead) return false;

            if (target.Character.LifeState != CharacterLifeState.Alive && 
                (!includeDownedTargets || target.Character.LifeState != CharacterLifeState.Downed)) return false;

            return targetingType switch
            {
                TargetingType.Enemies =>
                    target.IsPlayerSide != caster.IsPlayerSide,

                TargetingType.Allies =>
                    target.IsPlayerSide == caster.IsPlayerSide
                    && !ReferenceEquals(target, caster),

                TargetingType.All => true,

                _ => false
            };
        }

        private static bool RequiresTileSelection(
            SkillTargetModeData mode)
        {
            return mode.TargetType == TargetType.Single
                || mode.TargetType == TargetType.AreaAroundTile;
        }
        public IReadOnlyList<GridPosition> GetEligibleTargetPositions(
            CombatSession session,
            BattleUnit caster,
            SkillTargetModeData mode,
            bool includeDownedTargets = false)
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
                            null, includeDownedTargets))
                {
                    positions.Add(target.Position);
                }

                return new List<GridPosition>(positions);
            }

            if (!RequiresTileSelection(mode))
            {
                return new List<GridPosition>();
            }

            if (mode.TargetType == TargetType.Single)
            {
                foreach (var unit in session.Units)
                {
                    if (unit == null
                        || !IsEligibleTarget(
                            caster,
                            unit,
                            mode.TargetingType, includeDownedTargets)
                        || !IsValidTargetPosition(
                            session,
                            caster,
                            mode,
                            unit.Position))
                    {
                        continue;
                    }

                    positions.Add(unit.Position);
                }

                return new List<GridPosition>(positions);
            }

            foreach (var center in GetSelectablePositions(session, caster, mode, includeDownedTargets))
            {
                foreach (var target in GetTargets(session, caster, mode, center, includeDownedTargets))
                {
                    positions.Add(target.Position);
                }
            }

            return new List<GridPosition>(positions);
        }
    }
}
