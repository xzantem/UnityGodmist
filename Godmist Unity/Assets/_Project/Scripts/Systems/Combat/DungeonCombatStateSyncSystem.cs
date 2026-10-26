using System;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Quests;

namespace Godmist.Systems.Combat
{
    public class DungeonCombatStateSyncSystem
    {
        // Exploration and parallel combats share the floor, but own separate runtime door objects.
        public void RefreshDoorsFromFloor(DungeonFloorInstance floor, CombatSession session)
        {
            if (floor?.Map == null || session?.Grid == null || session.DungeonContext?.FloorIndex != floor.FloorIndex) return;
            foreach (var item in session.MapObjects)
            {
                if (item is not DoorCombatMapObject door) continue;
                foreach (var source in floor.Objects)
                {
                    if (source is not DoorDungeonObjectInstance dungeonDoor || source.InstanceId != door.ObjectId) continue;
                    door.IsOpen = !dungeonDoor.IsActive || dungeonDoor.IsOpen;
                    session.Grid.SetBlockedEdge(door.Edge.FirstCell, door.Edge.SecondCell, door.BlocksMovement);
                    break;
                }
            }
        }

        public bool TrySyncDoorStates(
            GameSession gameSession,
            CombatSession combatSession)
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

                if (!encounter.IsReinforcement)
                {
                    foreach (var enemy in encounter.Enemies)
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
            }

            return true;
        }
    }
}
