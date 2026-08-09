using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class CombatLineOfSightServiceTests
    {
        [Test]
        public void HasLineOfSight_WithoutBlocker_ReturnsTrue()
        {
            var session = CreateSession();
            var service = new CombatLineOfSightService();

            var result = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            Assert.That(result, Is.True);
        }

        [Test]
        public void HasLineOfSight_HighObstacleBetweenUnits_ReturnsFalse()
        {
            var session = CreateSession();

            session.MapObjects.Add(
                new ObstacleCombatMapObject(
                    "obstacle.wall.01",
                    new GridPosition(3, 1),
                    blocksMovement: true,
                    blocksLineOfSight: true));

            var service = new CombatLineOfSightService();

            var result = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            Assert.That(result, Is.False);
        }

        [Test]
        public void HasLineOfSight_LowObstacleBetweenUnits_ReturnsTrue()
        {
            var session = CreateSession();

            session.MapObjects.Add(
                new ObstacleCombatMapObject(
                    "obstacle.rock.01",
                    new GridPosition(3, 1),
                    blocksMovement: true,
                    blocksLineOfSight: false));

            var service = new CombatLineOfSightService();

            var result = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            Assert.That(result, Is.True);
        }

        [Test]
        public void HasLineOfSight_ClosedDoorBlocksButOpenDoorDoesNot()
        {
            var session = CreateSession();

            var door = new DoorCombatMapObject(
                "door.test.01",
                new GridPosition(3, 1),
                new GridPosition(4, 1),
                DoorInteractionMode.DirectAndLever);

            session.MapObjects.Add(door);

            var service = new CombatLineOfSightService();

            var closedDoorResult = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            door.IsOpen = true;

            var openDoorResult = service.HasLineOfSight(
                session,
                new GridPosition(1, 1),
                new GridPosition(5, 1));

            Assert.That(closedDoorResult, Is.False);
            Assert.That(openDoorResult, Is.True);
        }

        private static CombatSession CreateSession()
        {
            return new CombatSession
            {
                Grid = new BattleGrid(8, 8)
            };
        }
    }
}