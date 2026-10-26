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
        public void ParallelCombatRefreshesDoorAndBlockedEdgeAfterExplorationToggle()
        {
            var first = new Vector2Int(-4, 7); var second = new Vector2Int(-3, 7);
            var map = new DungeonFloorMap();
            map.TryAddFloorCell(first, "room"); map.TryAddFloorCell(second, "room");
            map.TryAddFloorCell(new DungeonSurfacePosition(first, 1), "room");
            var floor = new DungeonFloorInstance(0); floor.TrySetMap(map);
            var definition = ScriptableObject.CreateInstance<DoorDungeonObjectDefinition>();
            try
            {
                floor.TryAddObject(new DoorDungeonObjectInstance("shared", definition, new DungeonCellEdge(first, second)));
                var context = new DungeonCombatContext("dungeon", 0, "combat", "room", first, second,
                    new[] { first, second }, null, null, surfaceMap: map);
                Assert.That(new DungeonBattleGridFactory().TryCreate(context, out var grid), Is.True);
                var runtimeDoor = new DoorCombatMapObject("shared", new GridPosition(0, 0), new GridPosition(1, 0), DoorInteractionMode.DirectOnly);
                var session = new CombatSession { DungeonContext = context, Grid = grid };
                session.MapObjects.Add(runtimeDoor);
                new CombatInteractionSystem().InitializeMapObjects(session);
                var sync = new DungeonCombatStateSyncSystem();
                Assert.That(grid.CanMoveBetween(runtimeDoor.Edge.FirstCell, runtimeDoor.Edge.SecondCell), Is.False);
                foreach (var open in new[] { true, false, true })
                {
                    Assert.That(floor.TrySetDoorOpen("shared", open), Is.True);
                    sync.RefreshDoorsFromFloor(floor, session);
                    Assert.That(runtimeDoor.IsOpen, Is.EqualTo(open));
                    Assert.That(grid.CanMoveBetween(runtimeDoor.Edge.FirstCell, runtimeDoor.Edge.SecondCell), Is.EqualTo(open));
                    Assert.That(grid.HasBlockedEdge(runtimeDoor.Edge.FirstCell, runtimeDoor.Edge.SecondCell), Is.EqualTo(!open));
                }
            }
            finally { Object.DestroyImmediate(definition); }
        }

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
