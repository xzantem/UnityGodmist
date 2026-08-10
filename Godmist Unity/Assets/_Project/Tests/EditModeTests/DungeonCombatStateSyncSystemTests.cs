using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonCombatStateSyncSystemTests
    {
        [Test]
        public void TrySyncDoorStates_OpenCombatDoor_UpdatesDungeonDoor()
        {
            var firstCell = new Vector2Int(0, 0);
            var secondCell = new Vector2Int(1, 0);

            var map = new DungeonFloorMap();
            map.TryAddFloorCell(firstCell, "room.test");
            map.TryAddFloorCell(secondCell, "room.test");

            var floor = new DungeonFloorInstance(0);
            floor.TrySetMap(map);

            var doorDefinition = ScriptableObject.CreateInstance<
                DoorDungeonObjectDefinition>();

            var dungeonDoor = new DoorDungeonObjectInstance(
                "door.test",
                doorDefinition,
                new DungeonCellEdge(firstCell, secondCell));

            floor.TryAddObject(dungeonDoor);

            var dungeon = new DungeonInstance(
                "dungeon.test",
                123);

            dungeon.TryAddFloor(floor);

            var gameSession = new GameSession();
            gameSession.TrySetActiveDungeon(dungeon);

            var context = new DungeonCombatContext(
                "dungeon.test",
                0,
                "encounter.test",
                "room.test",
                firstCell,
                secondCell,
                new[] { firstCell, secondCell },
                null,
                null);

            gameSession.TrySetActiveDungeonCombatContext(context);

            var combatSession = new CombatSession
            {
                CombatId = "encounter.test"
            };

            combatSession.MapObjects.Add(
                new DoorCombatMapObject(
                    "door.test",
                    new GridPosition(0, 0),
                    new GridPosition(1, 0),
                    DoorInteractionMode.DirectOnly,
                    isOpen: true));

            var system = new DungeonCombatStateSyncSystem();

            var result = system.TrySyncDoorStates(
                gameSession,
                combatSession);

            Assert.That(result, Is.True);
            Assert.That(dungeonDoor.IsOpen, Is.True);
            Assert.That(map.CanMoveBetween(firstCell, secondCell), Is.True);
        }

        [Test]
        public void TrySyncDoorStates_WrongCombatId_DoesNotChangeDungeonDoor()
        {
            var firstCell = new Vector2Int(0, 0);
            var secondCell = new Vector2Int(1, 0);

            var map = new DungeonFloorMap();

            map.TryAddFloorCell(firstCell, "room.test");
            map.TryAddFloorCell(secondCell, "room.test");

            var floor = new DungeonFloorInstance(0);
            floor.TrySetMap(map);

            var doorDefinition = ScriptableObject.CreateInstance<
                DoorDungeonObjectDefinition>();

            var dungeonDoor = new DoorDungeonObjectInstance(
                "door.test",
                doorDefinition,
                new DungeonCellEdge(firstCell, secondCell));

            floor.TryAddObject(dungeonDoor);

            var dungeon = new DungeonInstance(
                "dungeon.test",
                123);

            dungeon.TryAddFloor(floor);

            var gameSession = new GameSession();
            gameSession.TrySetActiveDungeon(dungeon);

            var context = new DungeonCombatContext(
                "dungeon.test",
                0,
                "encounter.expected",
                "room.test",
                firstCell,
                secondCell,
                new[] { firstCell, secondCell },
                null,
                null);

            gameSession.TrySetActiveDungeonCombatContext(context);

            var combatSession = new CombatSession
            {
                CombatId = "encounter.other"
            };

            combatSession.MapObjects.Add(
                new DoorCombatMapObject(
                    "door.test",
                    new GridPosition(0, 0),
                    new GridPosition(1, 0),
                    DoorInteractionMode.DirectOnly,
                    isOpen: true));

            var system = new DungeonCombatStateSyncSystem();

            var result = system.TrySyncDoorStates(
                gameSession,
                combatSession);

            Assert.That(result, Is.False);
            Assert.That(dungeonDoor.IsOpen, Is.False);

            Assert.That(
                map.CanMoveBetween(firstCell, secondCell),
                Is.False);
        }
    }
    
}