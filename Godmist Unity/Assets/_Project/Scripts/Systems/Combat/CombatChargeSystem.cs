using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public sealed class CombatChargeSystem
    {
        private readonly Action<string> battleLog;
        private readonly StatusEffectSystem statusEffectSystem;

        public CombatChargeSystem(
            Action<string> battleLog = null,
            StatusEffectSystem statusEffectSystem = null)
        {
            this.battleLog = battleLog;
            this.statusEffectSystem = statusEffectSystem ?? new StatusEffectSystem(battleLog);
        }

        public bool CanStartCharge(
            CombatSession session,
            BattleUnit caster,
            BattleUnit selectedTarget,
            SkillEffectData effect)
        {
            if (session?.Grid == null || caster?.Character == null
                || selectedTarget?.Character == null || effect == null) return false;

            if (caster.Character.LifeState != CharacterLifeState.Alive
                || selectedTarget.Character.LifeState == CharacterLifeState.Dead
                || caster.IsPlayerSide == selectedTarget.IsPlayerSide
                || StatusEffectSystem.IsImmobilized(caster)) return false;

            var path = BuildPath(
                caster.Position,
                selectedTarget.Position,
                Mathf.Max(1, effect.DisplacementDistance));

            foreach (var position in path)
            {
                if (!session.Grid.IsWalkable(position)) break;

                var occupant = FindUnitAt(session, position, caster);

                if (occupant != null && occupant.IsPlayerSide == caster.IsPlayerSide)
                {
                    battleLog?.Invoke("Charge is blocked by an ally.");
                    return false;
                }
            }

            return true;
        }

        public bool TryCharge(
            CombatSession session,
            BattleUnit caster,
            BattleUnit selectedTarget,
            SkillData skill,
            SkillEffectData effect,
            int effectIndex)
        {
            if (!CanStartCharge(session, caster, selectedTarget, effect)) return false;

            var requestedDistance = Mathf.Max(1, effect.DisplacementDistance);

            var path = BuildPath(
                caster.Position,
                selectedTarget.Position,
                requestedDistance);

            var casterStartPosition = caster.Position;
            var movedDistance = 0;

            for (var pathIndex = 0; pathIndex < path.Count; pathIndex++)
            {
                var nextPosition = path[pathIndex];

                if (!CanTraverse(session, caster.Position, nextPosition))
                {
                    battleLog?.Invoke("Charge is stopped by terrain.");
                    return true;
                }

                var occupant = FindUnitAt(session, nextPosition, caster);

                if (occupant == null)
                {
                    caster.Position = nextPosition;
                    CombatAuraSystem.Refresh(session, statusEffectSystem);
                    movedDistance++;
                    continue;
                }

                if (occupant.IsPlayerSide == caster.IsPlayerSide)
                {
                    battleLog?.Invoke("Charge is blocked by an ally.");
                    return true;
                }

                if (!statusEffectSystem.PassesMovementEffectChance(
                        occupant,
                        effect.BaseChance))
                {
                    battleLog?.Invoke(
                        $"{GetDisplayName(occupant)} resists Charge.");

                    return true;
                }

                var remainingDistance = requestedDistance - movedDistance;

                return TryPushChain(
                    session,
                    casterStartPosition,
                    caster,
                    occupant,
                    skill,
                    effectIndex,
                    effect.BaseChance,
                    path,
                    pathIndex,
                    remainingDistance);
            }

            return true;
        }

        private bool TryPushChain(
            CombatSession session,
            GridPosition casterStartPosition,
            BattleUnit caster,
            BattleUnit firstTarget,
            SkillData skill,
            int effectIndex,
            float baseChance,
            IReadOnlyList<GridPosition> path,
            int contactPathIndex,
            int remainingDistance)
        {
            var chain = new List<BattleUnit> { caster, firstTarget };

            for (var pushStep = 0; pushStep < remainingDistance; pushStep++)
            {
                var pathIndex = contactPathIndex + pushStep;

                if (pathIndex >= path.Count)
                {
                    return true;
                }

                var previousPosition = pathIndex == 0
                    ? casterStartPosition
                    : path[pathIndex - 1];

                var currentPosition = path[pathIndex];

                var deltaX = currentPosition.X - previousPosition.X;
                var deltaY = currentPosition.Y - previousPosition.Y;

                if (deltaX == 0 && deltaY == 0)
                {
                    continue;
                }

                while (true)
                {
                    var frontUnit = chain[chain.Count - 1];

                    var frontDestination = new GridPosition(
                        frontUnit.Position.X + deltaX,
                        frontUnit.Position.Y + deltaY);

                    if (!CanTraverse(
                            session,
                            frontUnit.Position,
                            frontDestination))
                    {
                        var stunDuration = remainingDistance - pushStep + 1;

                        statusEffectSystem.ApplyGuaranteedStun(
                            caster,
                            frontUnit,
                            skill,
                            effectIndex,
                            stunDuration);

                        battleLog?.Invoke(
                            $"{GetDisplayName(frontUnit)} hits an obstacle and is stunned for "
                            + $"{stunDuration} turn(s).");

                        return true;
                    }

                    var blockingUnit = FindUnitAt(
                        session,
                        frontDestination,
                        chain);

                    if (blockingUnit == null)
                    {
                        break;
                    }

                    if (blockingUnit.IsPlayerSide == caster.IsPlayerSide)
                    {
                        battleLog?.Invoke(
                            "Charge chain is blocked by an ally.");

                        return true;
                    }

                    if (!statusEffectSystem.PassesMovementEffectChance(
                            blockingUnit,
                            baseChance))
                    {
                        battleLog?.Invoke(
                            $"{GetDisplayName(blockingUnit)} resists Charge.");

                        return true;
                    }

                    chain.Add(blockingUnit);
                }

                for (var index = chain.Count - 1; index >= 0; index--)
                {
                    var unit = chain[index];

                    unit.Position = new GridPosition(
                        unit.Position.X + deltaX,
                        unit.Position.Y + deltaY);
                }
            }

            battleLog?.Invoke(
                $"{GetDisplayName(caster)} completes Charge.");

            return true;
        }

        private static List<GridPosition> BuildPath(
            GridPosition origin,
            GridPosition destination,
            int requestedDistance)
        {
            var deltaX = destination.X - origin.X;
            var deltaY = destination.Y - origin.Y;

            var absoluteX = Mathf.Abs(deltaX);
            var absoluteY = Mathf.Abs(deltaY);
            var dominantLength = Mathf.Max(absoluteX, absoluteY);

            var positions = new List<GridPosition>();

            if (dominantLength == 0)
            {
                return positions;
            }

            var directionX = Math.Sign(deltaX);
            var directionY = Math.Sign(deltaY);

            for (var step = 1; step <= requestedDistance; step++)
            {
                var offsetX = dominantLength == absoluteX
                    ? directionX * step
                    : directionX * Mathf.RoundToInt(
                        step * absoluteX / (float)dominantLength);

                var offsetY = dominantLength == absoluteY
                    ? directionY * step
                    : directionY * Mathf.RoundToInt(
                        step * absoluteY / (float)dominantLength);

                positions.Add(new GridPosition(
                    origin.X + offsetX,
                    origin.Y + offsetY));
            }

            return positions;
        }

        private static BattleUnit FindUnitAt(
            CombatSession session,
            GridPosition position,
            BattleUnit ignoredUnit)
        {
            foreach (var unit in session.Units)
            {
                if (unit == null || unit == ignoredUnit
                    || unit.Character == null
                    || unit.Character.LifeState == CharacterLifeState.Dead)
                {
                    continue;
                }

                if (unit.Position == position)
                {
                    return unit;
                }
            }

            return null;
        }

        private static BattleUnit FindUnitAt(
            CombatSession session,
            GridPosition position,
            IReadOnlyCollection<BattleUnit> ignoredUnits)
        {
            foreach (var unit in session.Units)
            {
                if (unit == null || ignoredUnits.Contains(unit)
                    || unit.Character == null
                    || unit.Character.LifeState == CharacterLifeState.Dead)
                {
                    continue;
                }

                if (unit.Position == position)
                {
                    return unit;
                }
            }

            return null;
        }

        private static bool CanTraverse(
            CombatSession session,
            GridPosition from,
            GridPosition to)
        {
            if (session?.Grid == null
                || session.Grid.IsBlocked(to))
            {
                return false;
            }

            var deltaX = to.X - from.X;
            var deltaY = to.Y - from.Y;

            if (deltaX == 0 || deltaY == 0)
            {
                return session.Grid.CanMoveBetween(from, to);
            }

            var horizontal = new GridPosition(
                from.X + deltaX,
                from.Y);

            var vertical = new GridPosition(
                from.X,
                from.Y + deltaY);

            var throughHorizontal =
                session.Grid.CanMoveBetween(from, horizontal)
                && session.Grid.CanMoveBetween(horizontal, to);

            var throughVertical =
                session.Grid.CanMoveBetween(from, vertical)
                && session.Grid.CanMoveBetween(vertical, to);

            return throughHorizontal || throughVertical;
        }

        private static string GetDisplayName(BattleUnit unit)
        {
            return unit?.Character?.Definition?.DisplayName
                ?? unit?.Character?.CharacterId
                ?? "Unit";
        }
    }
}
