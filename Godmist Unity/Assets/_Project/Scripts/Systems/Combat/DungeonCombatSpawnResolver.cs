using System.Collections.Generic;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class DungeonCombatSpawnResolver
    {
        public bool TryResolveGroupPositions(
            BattleGrid grid,
            GridPosition anchor,
            int memberCount,
            ISet<GridPosition> occupiedPositions,
            out List<GridPosition> positions)
        {
            positions = new List<GridPosition>();

            if (grid == null
                || memberCount <= 0
                || occupiedPositions == null)
            {
                return false;
            }

            for (var memberIndex = 0;
                 memberIndex < memberCount;
                 memberIndex++)
            {
                if (!TryFindNearestFreePosition(
                        grid,
                        anchor,
                        occupiedPositions,
                        out var position))
                {
                    return false;
                }

                positions.Add(position);
                occupiedPositions.Add(position);
            }

            return true;
        }

        private static bool TryFindNearestFreePosition(
            BattleGrid grid,
            GridPosition anchor,
            ISet<GridPosition> occupiedPositions,
            out GridPosition position)
        {
            position = default;

            var maximumDistance = grid.Width + grid.Height;

            for (var distance = 0;
                 distance <= maximumDistance;
                 distance++)
            {
                for (var y = 0; y < grid.Height; y++)
                {
                    for (var x = 0; x < grid.Width; x++)
                    {
                        var candidate = new GridPosition(x, y);

                        if (GetManhattanDistance(anchor, candidate)
                            != distance)
                        {
                            continue;
                        }

                        if (!grid.IsWalkable(candidate)
                            || grid.IsBlocked(candidate)
                            || occupiedPositions.Contains(candidate))
                        {
                            continue;
                        }

                        position = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static int GetManhattanDistance(
            GridPosition first,
            GridPosition second)
        {
            return System.Math.Abs(first.X - second.X)
                + System.Math.Abs(first.Y - second.Y);
        }
    }
}