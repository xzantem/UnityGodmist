using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public static class DungeonCorridorPathfinder
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static bool TryFindPath(
            Vector2Int startCell,
            Vector2Int targetCell,
            HashSet<Vector2Int> blockedCells,
            RectInt searchBounds,
            out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();

            if (!searchBounds.Contains(startCell)
                || !searchBounds.Contains(targetCell)
                || blockedCells.Contains(startCell)
                || blockedCells.Contains(targetCell))
            {
                return false;
            }

            var cellsToVisit = new Queue<Vector2Int>();
            var previousCellByCell =
                new Dictionary<Vector2Int, Vector2Int>();

            cellsToVisit.Enqueue(startCell);
            previousCellByCell.Add(startCell, startCell);

            while (cellsToVisit.Count > 0)
            {
                var currentCell = cellsToVisit.Dequeue();

                if (currentCell == targetCell)
                {
                    path = RebuildPath(
                        previousCellByCell,
                        startCell,
                        targetCell);

                    return true;
                }

                foreach (var direction in Directions)
                {
                    var nextCell = currentCell + direction;

                    if (!searchBounds.Contains(nextCell)
                        || blockedCells.Contains(nextCell)
                        || previousCellByCell.ContainsKey(nextCell))
                    {
                        continue;
                    }

                    previousCellByCell.Add(nextCell, currentCell);
                    cellsToVisit.Enqueue(nextCell);
                }
            }

            return false;
        }

        private static List<Vector2Int> RebuildPath(
            Dictionary<Vector2Int, Vector2Int> previousCellByCell,
            Vector2Int startCell,
            Vector2Int targetCell)
        {
            var path = new List<Vector2Int>();
            var currentCell = targetCell;

            while (currentCell != startCell)
            {
                path.Add(currentCell);
                currentCell = previousCellByCell[currentCell];
            }

            path.Add(startCell);
            path.Reverse();

            return path;
        }
    }
}