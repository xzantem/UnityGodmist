using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Systems.Combat;
using NUnit.Framework;

namespace Godmist.Tests.EditMode
{
    public class DungeonCombatSpawnResolverTests
    {
        [Test]
        public void ResolveOpposingEdges_UsesLeftAndRight_ForHorizontalApproach()
        {
            DungeonCombatSpawnResolver.ResolveOpposingEdges(
                new GridPosition(1, 3),
                new GridPosition(6, 3),
                out var partyEdge,
                out var enemyEdge);

            Assert.That(partyEdge, Is.EqualTo(CombatSpawnEdge.Left));
            Assert.That(enemyEdge, Is.EqualTo(CombatSpawnEdge.Right));
        }

        [Test]
        public void ResolveOpposingEdges_UsesBottomAndTop_ForVerticalApproach()
        {
            DungeonCombatSpawnResolver.ResolveOpposingEdges(
                new GridPosition(3, 1),
                new GridPosition(3, 6),
                out var partyEdge,
                out var enemyEdge);

            Assert.That(partyEdge, Is.EqualTo(CombatSpawnEdge.Bottom));
            Assert.That(enemyEdge, Is.EqualTo(CombatSpawnEdge.Top));
        }

        [Test]
        public void TryResolveGroupPositions_SpawnsPartyAlongChosenEdge()
        {
            var grid = new BattleGrid(8, 6);
            var resolver = new DungeonCombatSpawnResolver();
            var occupiedPositions = new HashSet<GridPosition>();

            var result = resolver.TryResolveGroupPositions(
                grid,
                CombatSpawnEdge.Left,
                3,
                occupiedPositions,
                out var positions);

            Assert.That(result, Is.True);
            Assert.That(positions, Has.Count.EqualTo(3));

            foreach (var position in positions)
            {
                Assert.That(position.X, Is.EqualTo(0));
                Assert.That(grid.IsWalkable(position), Is.True);
            }
        }

        [Test]
        public void TryResolveGroupPositions_KeepsEnemyGroupOnOppositeEdge()
        {
            var grid = new BattleGrid(8, 6);
            var resolver = new DungeonCombatSpawnResolver();
            var occupiedPositions = new HashSet<GridPosition>();

            resolver.TryResolveGroupPositions(
                grid,
                CombatSpawnEdge.Left,
                2,
                occupiedPositions,
                out _);

            var result = resolver.TryResolveGroupPositions(
                grid,
                CombatSpawnEdge.Right,
                2,
                occupiedPositions,
                out var enemyPositions);

            Assert.That(result, Is.True);
            Assert.That(enemyPositions, Has.Count.EqualTo(2));

            foreach (var position in enemyPositions)
            {
                Assert.That(position.X, Is.EqualTo(7));
                Assert.That(occupiedPositions.Contains(position), Is.True);
            }
        }
    }
}