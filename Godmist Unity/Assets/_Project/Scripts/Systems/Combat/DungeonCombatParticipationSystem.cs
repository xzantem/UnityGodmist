using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public static class DungeonCombatParticipationSystem
    {
        public static bool TryJoin(CombatSession session, BattleUnit unit, GridPosition preferred,
            ISet<GridPosition> reservedArrivals = null, bool allowDowned = false)
        {
            if (session?.IsSeamless != true || session.Outcome != CombatOutcomeState.Ongoing
                || unit?.Character == null || (unit.Character.LifeState != CharacterLifeState.Alive
                    && !(allowDowned && unit.IsPlayerSide && unit.Character.LifeState == CharacterLifeState.Downed))
                || session.Units.Contains(unit)) return false;
            var occupied = new HashSet<GridPosition>(session.Units.Select(value => value.Position));
            if (reservedArrivals != null) occupied.UnionWith(reservedArrivals);
            AddExternalReservations(session, occupied);
            if (!DungeonCombatPositionResolver.TryResolve(session.Grid, preferred, occupied, out var position)) return false;
            unit.Position = position;
            unit.CurrentActionValue = Math.Max(unit.CurrentActionValue, CombatTurnSystem.CalculateBaseActionValue(unit));
            session.Units.Add(unit);
            session.DepartedUnits.Remove(unit.Character.CharacterId);
            session.TurnParticipants.Add(new CombatTurnParticipant(unit));
            CombatTurnSystem.ReorderParticipants(session);
            return true;
        }

        public static void AddExternalReservations(CombatSession session, ISet<GridPosition> occupied)
        {
            if (session.ExternalPositionOccupied == null) return;
            foreach (var position in session.Grid.Positions)
            {
                if (session.ExternalPositionOccupied(position)) occupied.Add(position);
            }
        }

        public static bool TryLeave(CombatSession session, BattleUnit unit)
        {
            if (session?.IsSeamless != true || unit?.IsPlayerSide != true
                || unit.Character?.LifeState != CharacterLifeState.Alive
                || !session.Units.Contains(unit)) return false;
            session.Contact?.Refresh(session);
            if (session.IsDungeonWide ? session.Contact?.CanLeave(session, unit) != true
                : !session.IsExitPosition(unit.Position)) return false;
            session.Contact?.Forget(unit);
            unit.CurrentActionValue = Math.Max(unit.CurrentActionValue, CombatTurnSystem.CalculateBaseActionValue(unit));
            session.DepartedUnits[unit.Character.CharacterId] = unit;
            session.Units.Remove(unit);
            session.TurnParticipants.RemoveAll(value => value.Unit == unit);
            if (session.ActiveUnit == unit)
            {
                session.ActiveUnit = null;
                session.ActiveParticipant = null;
                session.ActiveSummonedObject = null;
            }
            CombatTurnSystem.ReorderParticipants(session);
            return true;
        }
    }
}
