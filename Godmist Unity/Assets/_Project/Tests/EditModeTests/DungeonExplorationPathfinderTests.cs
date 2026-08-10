using Godmist.Runtime.Dungeons;
using Godmist.Systems.Dungeons;
using NUnit.Framework;
using UnityEngine;

namespace Godmist.Tests.EditMode
{
    public class DungeonExplorationPathfinderTests
    {
        [Test]
        public void TryFindPath_UsesOpenRouteAroundWall()
        {
            var map = CreateSquareMap();

            map.TryAddWall(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                out var path);

            Assert.That(result, Is.True);
            Assert.That(path, Is.EqualTo(new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(1, 0)
            }));
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenWallBlocksOnlyRoute()
        {
            var map = new DungeonFloorMap();

            map.TryAddFloorCell(new Vector2Int(0, 0), "room");
            map.TryAddFloorCell(new Vector2Int(1, 0), "room");

            map.TryAddWall(
                new Vector2Int(0, 0),
                new Vector2Int(1, 0));

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                out var path);

            Assert.That(result, Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void TryFindPath_ReturnsFalse_WhenTargetIsOutsideMap()
        {
            var map = CreateSquareMap();

            var result = DungeonExplorationPathfinder.TryFindPath(
                map,
                new Vector2Int(0, 0),
                new Vector2Int(3, 3),
                out var path);

            Assert.That(result, Is.False);
            Assert.That(path, Is.Empty);
        }

        private static DungeonFloorMap CreateSquareMap()
        {
            var map = new DungeonFloorMap();

            map.TryAddFloorCell(new Vector2Int(0, 0), "room");
            map.TryAddFloorCell(new Vector2Int(1, 0), "room");
            map.TryAddFloorCell(new Vector2Int(0, 1), "room");
            map.TryAddFloorCell(new Vector2Int(1, 1), "room");

            return map;
        }
    }
}