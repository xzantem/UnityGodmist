using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class DungeonGraphTests
    {
        [Test]
        public void Floor_AddsUniqueRoomsAndUndirectedConnection()
        {
            var floor = new DungeonFloorInstance(0);

            var start = new DungeonRoomInstance(
                "room.start",
                DungeonRoomType.Start);

            var corridor = new DungeonRoomInstance(
                "room.corridor.01",
                DungeonRoomType.Corridor);

            var final = new DungeonRoomInstance(
                "room.final",
                DungeonRoomType.Final);

            Assert.That(floor.TryAddRoom(start), Is.True);
            Assert.That(floor.TryAddRoom(corridor), Is.True);
            Assert.That(floor.TryAddRoom(final), Is.True);
            Assert.That(floor.TryAddRoom(start), Is.False);

            Assert.That(
                floor.TryAddConnection(
                    "room.start",
                    "room.corridor.01"),
                Is.True);

            Assert.That(
                floor.TryAddConnection(
                    "room.corridor.01",
                    "room.start"),
                Is.False);

            Assert.That(
                floor.TryAddConnection(
                    "room.corridor.01",
                    "room.final"),
                Is.True);

            Assert.That(floor.Connections, Has.Count.EqualTo(2));
        }

        [Test]
        public void Floor_RejectsConnectionToMissingRoom()
        {
            var floor = new DungeonFloorInstance(0);

            floor.TryAddRoom(
                new DungeonRoomInstance(
                    "room.start",
                    DungeonRoomType.Start));

            Assert.That(
                floor.TryAddConnection(
                    "room.start",
                    "room.missing"),
                Is.False);
        }

        [Test]
        public void Dungeon_ChangesFloorOnlyToExistingFloor()
        {
            var dungeon = new DungeonInstance(
                "dungeon.test.graph",
                123);

            var floorZero = new DungeonFloorInstance(0);
            var floorOne = new DungeonFloorInstance(1);

            Assert.That(dungeon.TryAddFloor(floorZero), Is.True);
            Assert.That(dungeon.TryAddFloor(floorOne), Is.True);
            Assert.That(dungeon.TryAddFloor(floorOne), Is.False);

            Assert.That(dungeon.TrySetCurrentFloorIndex(1), Is.True);
            Assert.That(dungeon.CurrentFloorIndex, Is.EqualTo(1));

            Assert.That(dungeon.TryGetCurrentFloor(out var currentFloor), Is.True);
            Assert.That(currentFloor, Is.SameAs(floorOne));

            Assert.That(dungeon.TrySetCurrentFloorIndex(2), Is.False);
        }

        [Test]
        public void Room_CanBeMarkedVisitedOnlyOnce()
        {
            var room = new DungeonRoomInstance(
                "room.normal.01",
                DungeonRoomType.Normal);

            Assert.That(room.TryMarkVisited(), Is.True);
            Assert.That(room.TryMarkVisited(), Is.False);
            Assert.That(room.IsVisited, Is.True);
        }
    }
}