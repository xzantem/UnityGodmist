using System.Collections.Generic;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonCorridorPathfinderTests
    {
        [Test]
        public void TryFindPath_AvoidsBlockedCells()
        {
            var blockedCells = new HashSet<Vector2Int>
            {
                new(1, 0),
                new(2, 0),
                new(3, 0)
            };

            var foundPath = DungeonCorridorPathfinder.TryFindPath(
                new Vector2Int(0, 0),
                new Vector2Int(4, 0),
                blockedCells,
                new RectInt(-2, -2, 9, 5),
                out var path);

            Assert.That(foundPath, Is.True);
            Assert.That(path, Is.Not.Empty);

            foreach (var cell in path)
            {
                Assert.That(
                    blockedCells.Contains(cell),
                    Is.False);
            }
        }

        [Test]
        public void HasDirectAccess_ReturnsFalseWhenWallBlocksTheLine()
        {
            var floor = CreateLinearFloor(3);
            floor.Map.TryAddWall(
                new Vector2Int(1, 0),
                new Vector2Int(2, 0));

            Assert.That(
                DungeonExplorationInteractionUtility.HasDirectAccess(
                    floor,
                    new Vector2Int(0, 0),
                    new Vector2Int(2, 0)),
                Is.False);
        }

        [Test]
        public void HasDirectAccess_ReturnsFalseWhenObstacleBlocksTheLine()
        {
            var floor = CreateLinearFloor(3);
            var definition = ScriptableObject.CreateInstance<ObstacleDungeonObjectDefinition>();
            var obstacle = new ObstacleDungeonObjectInstance(
                "obstacle.test",
                definition,
                new Vector2Int(1, 0));
            floor.TryAddObject(obstacle);

            Assert.That(
                DungeonExplorationInteractionUtility.HasDirectAccess(
                    floor,
                    new Vector2Int(0, 0),
                    new Vector2Int(2, 0)),
                Is.False);

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void HasDirectAccessToDoor_AllowsInteractionFromEitherSide()
        {
            var floor = CreateLinearFloor(2);
            var edge = new DungeonCellEdge(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));
            floor.Map.TrySetDoorState(edge, false);

            Assert.That(
                DungeonExplorationInteractionUtility.HasDirectAccessToDoor(
                    floor,
                    edge.FirstCell,
                    edge),
                Is.True);
            Assert.That(
                DungeonExplorationInteractionUtility.HasDirectAccessToDoor(
                    floor,
                    edge.SecondCell,
                    edge),
                Is.True);
        }

        private static DungeonFloorInstance CreateLinearFloor(int cellCount)
        {
            var floor = new DungeonFloorInstance(0);
            var map = new DungeonFloorMap();

            for (var x = 0; x < cellCount; x++)
            {
                map.TryAddFloorCell(new Vector2Int(x, 0), "room.test");
            }

            Assert.That(floor.TrySetMap(map), Is.True);
            return floor;
        }
    }
}
