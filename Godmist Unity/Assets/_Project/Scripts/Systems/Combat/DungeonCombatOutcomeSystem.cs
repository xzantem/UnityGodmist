using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;

namespace Godmist.Systems.Combat
{
    public class DungeonCombatOutcomeSystem
    {
        private readonly DungeonCombatStateSyncSystem stateSyncSystem;

        public DungeonCombatOutcomeSystem(
            DungeonCombatStateSyncSystem stateSyncSystem = null)
        {
            this.stateSyncSystem =
                stateSyncSystem ?? new DungeonCombatStateSyncSystem();
        }

        public bool TryApplyOutcome(
            GameSession gameSession,
            CombatSession combatSession)
        {
            if (combatSession == null
                || (combatSession.Outcome != CombatOutcomeState.Victory
                    && combatSession.Outcome != CombatOutcomeState.Escaped))
            {
                return false;
            }

            if (gameSession == null
                || gameSession.ActiveDungeon == null
                || gameSession.ActiveDungeonCombatContext == null)
            {
                return false;
            }

            var context = gameSession.ActiveDungeonCombatContext;

            if (!string.Equals(
                    combatSession.CombatId,
                    context.EncounterInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!stateSyncSystem.TrySyncDoorStates(
                    gameSession,
                    combatSession))
            {
                return false;
            }

            if (combatSession.Outcome == CombatOutcomeState.Escaped)
            {
                return true;
            }

            var dungeon = gameSession.ActiveDungeon;

            return dungeon.IsEncounterCleared(context.EncounterInstanceId)
                || dungeon.TryMarkEncounterCleared(
                    context.EncounterInstanceId);
        }
    }
}