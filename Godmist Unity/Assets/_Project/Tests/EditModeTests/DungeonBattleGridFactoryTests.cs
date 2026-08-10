using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonBattleGridFactoryTests
    {
        [Test]
        public void TryCreate_MapsWorldCellsToLocalBattleGrid()
        {
            var context = CreateContext(
                new[]
                {
                    new Vector2Int(10, 20),
                    new Vector2Int(11, 20),
                    new Vector2Int(10, 21)
                });

            var factory = new DungeonBattleGridFactory();

            var result = factory.TryCreate(context, out var grid);

            Assert.That(result, Is.True);
            Assert.That(grid.Width, Is.EqualTo(2));
            Assert.That(grid.Height, Is.EqualTo(2));

            Assert.That(
                grid.IsWalkable(new GridPosition(0, 0)),
                Is.True);

            Assert.That(
                grid.IsWalkable(new GridPosition(1, 0)),
                Is.True);

            Assert.That(
                grid.IsWalkable(new GridPosition(0, 1)),
                Is.True);

            Assert.That(
                DungeonBattleGridFactory.ToBattlePosition(
                    context,
                    new Vector2Int(11, 20)),
                Is.EqualTo(new GridPosition(1, 0)));

            Assert.That(
                DungeonBattleGridFactory.ToWorldPosition(
                    context,
                    new GridPosition(0, 1)),
                Is.EqualTo(new Vector2Int(10, 21)));
        }

        [Test]
        public void TryCreate_LeavesMissingCellInsideBoundsUnwalkable()
        {
            var context = CreateContext(
                new[]
                {
                    new Vector2Int(10, 20),
                    new Vector2Int(11, 20),
                    new Vector2Int(10, 21)
                });

            var factory = new DungeonBattleGridFactory();

            factory.TryCreate(context, out var grid);

            var missingCell = new GridPosition(1, 1);

            Assert.That(grid.IsWithinBounds(missingCell), Is.True);
            Assert.That(grid.IsWalkable(missingCell), Is.False);
            Assert.That(grid.IsBlocked(missingCell), Is.True);
        }

        [Test]
        public void TryCreate_BlocksPositionOfBlockingDungeonObject()
        {
            var blockedObject = new DungeonCombatMapObjectSnapshot(
                "obstacle.01",
                DungeonObjectType.Obstacle,
                new Vector2Int(11, 20),
                blocksMovement: true,
                blocksLineOfSight: true);

            var context = CreateContext(
                new[]
                {
                    new Vector2Int(10, 20),
                    new Vector2Int(11, 20)
                },
                mapObjects: new[] { blockedObject });

            var factory = new DungeonBattleGridFactory();

            factory.TryCreate(context, out var grid);

            Assert.That(
                grid.IsWalkable(new GridPosition(1, 0)),
                Is.True);

            Assert.That(
                grid.IsBlocked(new GridPosition(1, 0)),
                Is.True);
        }

        [Test]
        public void TryCreate_BlocksMovementAcrossSnapshotWallEdge()
        {
            var wallEdge = new DungeonCellEdge(
                new Vector2Int(10, 20),
                new Vector2Int(11, 20));

            var context = CreateContext(
                new[]
                {
                    new Vector2Int(10, 20),
                    new Vector2Int(11, 20)
                },
                wallEdges: new[] { wallEdge });

            var factory = new DungeonBattleGridFactory();

            factory.TryCreate(context, out var grid);

            var firstCell = new GridPosition(0, 0);
            var secondCell = new GridPosition(1, 0);

            Assert.That(
                grid.HasBlockedEdge(firstCell, secondCell),
                Is.True);

            Assert.That(
                grid.CanMoveBetween(firstCell, secondCell),
                Is.False);

            Assert.That(
                grid.TryMove(firstCell, 1, 0, out _),
                Is.False);
        }

        [Test]
        public void DefaultBattleGrid_KeepsEveryCellWalkable()
        {
            var grid = new BattleGrid(3, 2);

            Assert.That(
                grid.IsWalkable(new GridPosition(0, 0)),
                Is.True);

            Assert.That(
                grid.IsWalkable(new GridPosition(2, 1)),
                Is.True);

            Assert.That(
                grid.TryMove(
                    new GridPosition(0, 0),
                    1,
                    0,
                    out var target),
                Is.True);

            Assert.That(
                target,
                Is.EqualTo(new GridPosition(1, 0)));
        }

        [Test]
        public void TryCreate_ClosedDoorBlocksOnlyItsEdge()
        {
            var firstWorldCell = new Vector2Int(10, 20);
            var secondWorldCell = new Vector2Int(11, 20);

            var door = new DungeonCombatMapObjectSnapshot(
                "door.test",
                new DungeonCellEdge(
                    firstWorldCell,
                    secondWorldCell),
                isOpen: false,
                DoorInteractionMode.DirectOnly);

            var context = CreateContext(
                new[]
                {
                    firstWorldCell,
                    secondWorldCell
                },
                mapObjects: new[] { door });

            var factory = new DungeonBattleGridFactory();

            factory.TryCreate(context, out var grid);

            var firstBattleCell = new GridPosition(0, 0);
            var secondBattleCell = new GridPosition(1, 0);

            Assert.That(grid.IsBlocked(firstBattleCell), Is.False);
            Assert.That(grid.IsBlocked(secondBattleCell), Is.False);

            Assert.That(
                grid.HasBlockedEdge(
                    firstBattleCell,
                    secondBattleCell),
                Is.True);

            Assert.That(
                grid.CanMoveBetween(
                    firstBattleCell,
                    secondBattleCell),
                Is.False);
        }

        private static DungeonCombatContext CreateContext(
            Vector2Int[] worldCells,
            DungeonCellEdge[] wallEdges = null,
            DungeonCombatMapObjectSnapshot[] mapObjects = null)
        {
            return new DungeonCombatContext(
                "dungeon.test",
                0,
                "encounter.test",
                "room.test",
                new Vector2Int(10, 20),
                new Vector2Int(11, 20),
                worldCells,
                wallEdges,
                mapObjects);
        }
    }
}