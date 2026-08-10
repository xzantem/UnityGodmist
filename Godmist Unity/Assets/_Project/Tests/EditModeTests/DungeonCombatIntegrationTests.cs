using Godmist.Core.Enums;
using Godmist.Data.Combat;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonCombatIntegrationTests
    {
        [Test]
        public void Victory_OpenDoorInCombat_PersistsDoorAndClearsEncounter()
        {
            var gameSession = CreateGameSession(
                out var dungeon,
                out var encounter,
                out var context,
                out var firstWorldCell,
                out var secondWorldCell);

            var gridFactory = new DungeonBattleGridFactory();

            var createdGrid = gridFactory.TryCreate(
                context,
                out var battleGrid);

            Assert.That(createdGrid, Is.True);

            var firstBattleCell = DungeonBattleGridFactory.ToBattlePosition(
                context,
                firstWorldCell);

            var secondBattleCell = DungeonBattleGridFactory.ToBattlePosition(
                context,
                secondWorldCell);

            Assert.That(
                battleGrid.CanMoveBetween(
                    firstBattleCell,
                    secondBattleCell),
                Is.False);

            var combatSession = CreateCombatSessionWithOpenDoor(
                context,
                encounter.InstanceId,
                firstWorldCell,
                secondWorldCell,
                CombatOutcomeState.Victory);

            var outcomeSystem = new DungeonCombatOutcomeSystem();

            var applied = outcomeSystem.TryApplyOutcome(
                gameSession,
                combatSession);

            Assert.That(applied, Is.True);

            Assert.That(
                dungeon.IsEncounterCleared(encounter.InstanceId),
                Is.True);

            Assert.That(
                dungeon.TryGetCurrentFloor(out var floor),
                Is.True);

            Assert.That(
                floor.Map.CanMoveBetween(
                    firstWorldCell,
                    secondWorldCell),
                Is.True);
        }

        [Test]
        public void Escape_OpenDoorInCombat_PersistsDoorWithoutClearingEncounter()
        {
            var gameSession = CreateGameSession(
                out var dungeon,
                out var encounter,
                out var context,
                out var firstWorldCell,
                out var secondWorldCell);

            var combatSession = CreateCombatSessionWithOpenDoor(
                context,
                encounter.InstanceId,
                firstWorldCell,
                secondWorldCell,
                CombatOutcomeState.Escaped);

            var outcomeSystem = new DungeonCombatOutcomeSystem();

            var applied = outcomeSystem.TryApplyOutcome(
                gameSession,
                combatSession);

            Assert.That(applied, Is.True);

            Assert.That(
                dungeon.IsEncounterCleared(encounter.InstanceId),
                Is.False);

            Assert.That(
                dungeon.TryGetCurrentFloor(out var floor),
                Is.True);

            Assert.That(
                floor.Map.CanMoveBetween(
                    firstWorldCell,
                    secondWorldCell),
                Is.True);
        }

        private static GameSession CreateGameSession(
            out DungeonInstance dungeon,
            out DungeonEncounterInstance encounter,
            out DungeonCombatContext context,
            out Vector2Int firstWorldCell,
            out Vector2Int secondWorldCell)
        {
            firstWorldCell = new Vector2Int(10, 20);
            secondWorldCell = new Vector2Int(11, 20);

            var map = new DungeonFloorMap();

            map.TryAddFloorCell(firstWorldCell, "room.encounter");
            map.TryAddFloorCell(secondWorldCell, "room.encounter");

            var floor = new DungeonFloorInstance(0);

            floor.TryAddRoom(
                new DungeonRoomInstance(
                    "room.encounter",
                    DungeonRoomType.Normal));

            floor.TrySetMap(map);

            var doorDefinition = ScriptableObject.CreateInstance<
                DoorDungeonObjectDefinition>();

            var door = new DoorDungeonObjectInstance(
                "door.encounter",
                doorDefinition,
                new DungeonCellEdge(
                    firstWorldCell,
                    secondWorldCell));

            floor.TryAddObject(door);

            var encounterDefinition = ScriptableObject.CreateInstance<
                EncounterDefinition>();

            encounter = new DungeonEncounterInstance(
                "encounter.test",
                encounterDefinition,
                "room.encounter",
                new RectInt(10, 20, 2, 1));

            floor.TryAddEncounter(encounter);

            dungeon = new DungeonInstance(
                "dungeon.test",
                123);

            dungeon.TryAddFloor(floor);
            dungeon.SetPlayerPosition(firstWorldCell);

            var contextFactory = new DungeonCombatContextFactory();

            var createdContext = contextFactory.TryCreate(
                dungeon,
                encounter,
                out context);

            Assert.That(createdContext, Is.True);

            var gameSession = new GameSession();

            gameSession.TrySetActiveDungeon(dungeon);

            var setContext = gameSession.TrySetActiveDungeonCombatContext(
                context);

            Assert.That(setContext, Is.True);

            return gameSession;
        }

        private static CombatSession CreateCombatSessionWithOpenDoor(
            DungeonCombatContext context,
            string encounterId,
            Vector2Int firstWorldCell,
            Vector2Int secondWorldCell,
            CombatOutcomeState outcome)
        {
            var combatSession = new CombatSession
            {
                CombatId = encounterId,
                Outcome = outcome
            };

            combatSession.MapObjects.Add(
                new DoorCombatMapObject(
                    "door.encounter",
                    DungeonBattleGridFactory.ToBattlePosition(
                        context,
                        firstWorldCell),
                    DungeonBattleGridFactory.ToBattlePosition(
                        context,
                        secondWorldCell),
                    DoorInteractionMode.DirectOnly,
                    isOpen: true));

            return combatSession;
        }
    }
}