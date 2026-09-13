using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public sealed class CombatTeleportSystem
    {
        private readonly Action<string> battleLog;
        private readonly CombatLineOfSightService lineOfSightService;
        private readonly StatusEffectSystem statusEffectSystem;

        public CombatTeleportSystem(
            Action<string> battleLog = null,
            CombatLineOfSightService lineOfSightService = null,
            StatusEffectSystem statusEffectSystem = null)
        {
            this.battleLog = battleLog;
            this.lineOfSightService =
                lineOfSightService ?? new CombatLineOfSightService();
            this.statusEffectSystem = statusEffectSystem;
        }

        public bool CanTeleport(
            CombatSession session,
            BattleUnit caster,
            BattleUnit target,
            GridPosition destination,
            bool requiresLineOfSight)
        {
            if (session?.Grid == null || caster?.Character == null
                || target?.Character == null)
            {
                return false;
            }

            if (caster.Character.LifeState != CharacterLifeState.Alive
                || target.Character.LifeState == CharacterLifeState.Dead
                || StatusEffectSystem.IsImmobilized(caster))
            {
                return false;
            }

            if (destination == target.Position
                || !session.Grid.IsWalkable(destination)
                || session.Grid.IsBlocked(destination)
                || IsOccupiedByLivingUnit(session, destination, target))
            {
                return false;
            }

            if (!requiresLineOfSight)
            {
                return true;
            }

            return lineOfSightService.HasLineOfSight(
                       session,
                       caster.Position,
                       target.Position)
                   && lineOfSightService.HasLineOfSight(
                       session,
                       caster.Position,
                       destination);
        }

        public bool TryTeleport(
            CombatSession session,
            BattleUnit caster,
            BattleUnit target,
            GridPosition destination,
            bool requiresLineOfSight)
        {
            if (!CanTeleport(
                    session,
                    caster,
                    target,
                    destination,
                    requiresLineOfSight))
            {
                return false;
            }

            target.Position = destination;
            session.NotifyUnitEnteredCell(target);

            CombatAuraSystem.Refresh(session, statusEffectSystem);

            battleLog?.Invoke(
                $"{GetDisplayName(target)} is teleported.");

            return true;
        }

        private static bool IsOccupiedByLivingUnit(
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
                    return true;
                }
            }

            return false;
        }

        private static string GetDisplayName(BattleUnit unit)
        {
            return unit?.Character?.Definition?.DisplayName
                ?? unit?.Character?.CharacterId
                ?? "Unit";
        }
    }
}
