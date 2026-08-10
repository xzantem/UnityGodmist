using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DoorDungeonObjectInstanceTests
    {
        [Test]
        public void TryAddObject_ClosedDoor_BlocksDungeonEdge()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();

            var firstCell = new Vector2Int(0, 0);
            var secondCell = new Vector2Int(1, 0);

            map.TryAddFloorCell(firstCell, "room.a");
            map.TryAddFloorCell(secondCell, "room.a");
            floor.TrySetMap(map);

            var definition = ScriptableObject.CreateInstance<
                DoorDungeonObjectDefinition>();

            var door = new DoorDungeonObjectInstance(
                "door.01",
                definition,
                new DungeonCellEdge(firstCell, secondCell));

            Assert.That(floor.TryAddObject(door), Is.True);
            Assert.That(map.CanMoveBetween(firstCell, secondCell), Is.False);
        }

        [Test]
        public void TrySetDoorOpen_OpenDoor_UnblocksDungeonEdge()
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();

            var firstCell = new Vector2Int(0, 0);
            var secondCell = new Vector2Int(1, 0);

            map.TryAddFloorCell(firstCell, "room.a");
            map.TryAddFloorCell(secondCell, "room.a");
            floor.TrySetMap(map);

            var definition = ScriptableObject.CreateInstance<
                DoorDungeonObjectDefinition>();

            var door = new DoorDungeonObjectInstance(
                "door.01",
                definition,
                new DungeonCellEdge(firstCell, secondCell));

            floor.TryAddObject(door);

            Assert.That(floor.TrySetDoorOpen("door.01", true), Is.True);
            Assert.That(map.CanMoveBetween(firstCell, secondCell), Is.True);
        }
    }
}