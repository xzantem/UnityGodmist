using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Dungeons
{
    public static class DungeonExplorationInteractionUtility
    {
        public static bool HasDirectAccess(
            DungeonFloorInstance floor,
            Vector2Int startCell,
            Vector2Int targetCell)
        {
            if (floor?.Map == null
                || !floor.Map.IsWalkable(startCell)
                || !floor.Map.IsWalkable(targetCell))
            {
                return false;
            }

            if (startCell == targetCell)
            {
                return true;
            }

            var line = BuildGridLine(startCell, targetCell);

            for (var index = 1; index < line.Count; index++)
            {
                var currentCell = line[index];

                if (!floor.Map.CanMoveBetween(
                        line[index - 1],
                        currentCell))
                {
                    return false;
                }

                if (index < line.Count - 1
                    && IsBlockedByObstacle(floor, currentCell))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool HasDirectAccessToDoor(
            DungeonFloorInstance floor,
            Vector2Int startCell,
            DungeonCellEdge doorEdge)
        {
            return HasDirectAccess(floor, startCell, doorEdge.FirstCell)
                   || HasDirectAccess(floor, startCell, doorEdge.SecondCell);
        }

        private static bool IsBlockedByObstacle(
            DungeonFloorInstance floor,
            Vector2Int cell)
        {
            foreach (var dungeonObject in floor.Objects)
            {
                if (dungeonObject is not ObstacleDungeonObjectInstance obstacle
                    || obstacle.Position != cell)
                {
                    continue;
                }

                if (obstacle.BlocksMovement || obstacle.BlocksLineOfSight)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Vector2Int> BuildGridLine(
            Vector2Int startCell,
            Vector2Int targetCell)
        {
            var result = new List<Vector2Int> { startCell };
            var delta = targetCell - startCell;
            var absoluteDelta = new Vector2Int(
                Mathf.Abs(delta.x),
                Mathf.Abs(delta.y));
            var step = new Vector2Int(
                delta.x == 0 ? 0 : delta.x > 0 ? 1 : -1,
                delta.y == 0 ? 0 : delta.y > 0 ? 1 : -1);
            var current = startCell;

            if (absoluteDelta.x >= absoluteDelta.y)
            {
                var error = absoluteDelta.x / 2;

                while (current.x != targetCell.x)
                {
                    error -= absoluteDelta.y;

                    if (error < 0)
                    {
                        current.y += step.y;
                        result.Add(current);
                        error += absoluteDelta.x;
                    }

                    current.x += step.x;
                    result.Add(current);
                }

                while (current.y != targetCell.y)
                {
                    current.y += step.y;
                    result.Add(current);
                }

                return result;
            }

            var verticalError = absoluteDelta.y / 2;

            while (current.y != targetCell.y)
            {
                verticalError -= absoluteDelta.x;

                if (verticalError < 0)
                {
                    current.x += step.x;
                    result.Add(current);
                    verticalError += absoluteDelta.y;
                }

                current.y += step.y;
                result.Add(current);
            }

            while (current.x != targetCell.x)
            {
                current.x += step.x;
                result.Add(current);
            }

            return result;
        }
    }

    public static class DungeonExplorationPathfinder
    {
        private static readonly Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        public static bool TryFindPath(
            DungeonFloorMap map,
            Vector2Int startCell,
            Vector2Int targetCell,
            out List<Vector2Int> path)
        {
            path = new List<Vector2Int>();

            if (map == null
                || !map.IsWalkable(startCell)
                || !map.IsWalkable(targetCell))
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

                foreach (var direction in directions)
                {
                    var nextCell = currentCell + direction;

                    if (previousCellByCell.ContainsKey(nextCell)
                        || !map.CanMoveBetween(currentCell, nextCell))
                    {
                        continue;
                    }

                    previousCellByCell.Add(nextCell, currentCell);
                    cellsToVisit.Enqueue(nextCell);
                }
            }

            return false;
        }

        public static bool TryFindPath(DungeonFloorMap map, DungeonTraversalPosition start,
            DungeonTraversalPosition target, out List<DungeonTraversalPosition> path,
            System.Func<DungeonTraversalPosition, bool> canEnter = null,
            System.Func<DungeonSurfaceEdge, bool> canOpenDoor = null)
        {
            path = new List<DungeonTraversalPosition>();
            if (map == null || !map.TryGetTraversalCell(start, out _) || !map.TryGetTraversalCell(target, out _)) return false;
            var previous = new Dictionary<DungeonTraversalPosition, DungeonTraversalPosition> { [start] = start };
            var pending = new Queue<DungeonTraversalPosition>(); pending.Enqueue(start);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (current == target)
                {
                    while (current != start) { path.Add(current); current = previous[current]; }
                    path.Add(start); path.Reverse(); return true;
                }
                foreach (var next in map.GetTraversalNeighbours(current, canOpenDoor))
                {
                    if (previous.ContainsKey(next) || (canEnter != null && !canEnter(next))) continue;
                    previous.Add(next, current); pending.Enqueue(next);
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
