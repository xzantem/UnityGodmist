using System;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Quests;

namespace Godmist.Systems.Combat
{
    public class DungeonCombatStateSyncSystem
    {
        public bool TrySyncDoorStates(
            GameSession gameSession,
            CombatSession combatSession,
            QuestObjectiveProgressSystem questProgress = null)
        {
            if (gameSession == null
                || combatSession == null
                || gameSession.ActiveDungeon == null
                || (combatSession.DungeonContext ?? gameSession.ActiveDungeonCombatContext) == null)
            {
                return false;
            }

            var context = combatSession.DungeonContext ?? gameSession.ActiveDungeonCombatContext;

            if (!combatSession.IsDungeonWide && !string.Equals(
                    combatSession.CombatId,
                    context.EncounterInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!gameSession.ActiveDungeon.TryGetFloor(
                    context.FloorIndex,
                    out var floor))
            {
                return false;
            }

            foreach (var mapObject in combatSession.MapObjects)
            {
                if (mapObject is not DoorCombatMapObject combatDoor)
                {
                    continue;
                }

                if (!TrySyncDoorState(floor, combatDoor))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TrySyncDoorState(
            DungeonFloorInstance floor,
            DoorCombatMapObject combatDoor)
        {
            foreach (var dungeonObject in floor.Objects)
            {
                if (dungeonObject is not DoorDungeonObjectInstance dungeonDoor
                    || !string.Equals(
                        dungeonDoor.InstanceId,
                        combatDoor.ObjectId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (dungeonDoor.IsOpen == combatDoor.IsOpen)
                {
                    return true;
                }

                return floor.TrySetDoorOpen(
                    dungeonDoor.InstanceId,
                    combatDoor.IsOpen);
            }

            return false;
        }

        public bool TrySyncDefeatedEnemyStates(
            GameSession gameSession,
            CombatSession combatSession,
            QuestObjectiveProgressSystem questProgress = null)
        {
            if (gameSession == null
                || combatSession == null
                || gameSession.ActiveDungeon == null
                || (combatSession.DungeonContext ?? gameSession.ActiveDungeonCombatContext) == null)
            {
                return false;
            }

            if (combatSession.IsDungeonWide)
            {
                foreach (var result in DungeonCombatMembershipSystem.EncounterResults(gameSession.ActiveDungeon, combatSession))
                    if (!TrySyncDefeatedEnemyStates(gameSession, result, questProgress)) return false;
                return true;
            }

            var context = combatSession.DungeonContext ?? gameSession.ActiveDungeonCombatContext;

            if (!combatSession.IsDungeonWide && !string.Equals(
                    combatSession.CombatId,
                    context.EncounterInstanceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!gameSession.ActiveDungeon.TryGetFloor(
                    context.FloorIndex,
                    out var floor)
                || !floor.TryGetEncounter(
                    context.EncounterInstanceId,
                    out var encounter))
            {
                return false;
            }

            foreach (var enemyInstanceId
                in combatSession.DefeatedEnemyInstanceIds)
            {
                if (encounter.IsEnemyDefeated(enemyInstanceId))
                {
                    continue;
                }

                if (!encounter.TryMarkEnemyDefeated(enemyInstanceId))
                {
                    return false;
                }

                foreach (var enemy in encounter.Definition.Enemies)
                {
                    if (enemy?.InstanceId != enemyInstanceId
                        || enemy.CharacterDefinition == null) continue;
                    (questProgress ??= new QuestObjectiveProgressSystem(
                        gameSession, new QuestService(gameSession)))
                        .ReportEnemyDefeated(
                            gameSession.ActiveDungeon.DungeonId,
                            gameSession.ActiveDungeon.DungeonLevel,
                            enemy.CharacterDefinition.Id,
                            false);
                    break;
                }
            }

            return true;
        }
    }
}
