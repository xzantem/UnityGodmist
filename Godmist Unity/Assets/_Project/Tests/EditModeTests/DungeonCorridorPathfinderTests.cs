using System.Collections.Generic;
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
    }
}