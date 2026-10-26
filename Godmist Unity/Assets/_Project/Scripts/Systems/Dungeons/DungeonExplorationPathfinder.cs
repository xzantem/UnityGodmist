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
                    || !obstacle.IsActive
                    || !obstacle.OccupiesCell(cell))
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

        private static readonly Vector2Int[] anyAngleDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        private const float AnyAngleHeuristicWeight = 1f;

        private readonly struct OpenNode
        {
            public readonly DungeonTraversalPosition Position;
            public readonly float Priority;
            public readonly float Cost;
            public readonly long Order;

            public OpenNode(DungeonTraversalPosition position, float priority, float cost, long order)
            {
                Position = position;
                Priority = priority;
                Cost = cost;
                Order = order;
            }
        }

        private sealed class OpenNodeQueue
        {
            private readonly List<OpenNode> heap = new();
            private long nextOrder;
            public int Count => heap.Count;

            public void Enqueue(DungeonTraversalPosition position, float priority, float cost)
            {
                var node = new OpenNode(position, priority, cost, nextOrder++);
                heap.Add(node);
                var index = heap.Count - 1;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (ComesBefore(heap[parent], node)) break;
                    heap[index] = heap[parent];
                    index = parent;
                }

                heap[index] = node;
            }

            public OpenNode Dequeue()
            {
                var result = heap[0];
                var last = heap[heap.Count - 1];
                heap.RemoveAt(heap.Count - 1);
                if (heap.Count == 0) return result;

                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= heap.Count) break;
                    var right = left + 1;
                    var child = right < heap.Count && ComesBefore(heap[right], heap[left]) ? right : left;
                    if (ComesBefore(last, heap[child])) break;
                    heap[index] = heap[child];
                    index = child;
                }

                heap[index] = last;
                return result;
            }

            private static bool ComesBefore(OpenNode first, OpenNode second)
                => first.Priority < second.Priority
                    || Mathf.Approximately(first.Priority, second.Priority) && first.Order < second.Order;
        }

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

        /// <summary>
        /// Finds an any-angle route for exploration using Euclidean traversal cost.
        /// Cardinal and diagonal neighbours keep the search connected around
        /// obstacles; the parent LOS shortcut supplies longer straight segments
        /// when the caller can prove the actor footprint and topology are safe.
        /// </summary>
        public static bool TryFindAnyAnglePath(
            DungeonFloorMap map,
            DungeonTraversalPosition start,
            DungeonTraversalPosition target,
            out List<DungeonTraversalPosition> path,
            System.Func<DungeonTraversalPosition, bool> canEnter = null,
            System.Func<DungeonSurfaceEdge, bool> canOpenDoor = null,
            System.Func<DungeonTraversalPosition, DungeonTraversalPosition, bool> canNavigateDirectly = null)
            => TryFindAnyAnglePath(map, start, target, out path, canEnter, canOpenDoor, canNavigateDirectly,
                out _);

        public static bool TryFindAnyAnglePath(
            DungeonFloorMap map,
            DungeonTraversalPosition start,
            DungeonTraversalPosition target,
            out List<DungeonTraversalPosition> path,
            System.Func<DungeonTraversalPosition, bool> canEnter,
            System.Func<DungeonSurfaceEdge, bool> canOpenDoor,
            System.Func<DungeonTraversalPosition, DungeonTraversalPosition, bool> canNavigateDirectly,
            out int expandedNodes)
        {
            path = new List<DungeonTraversalPosition>();
            expandedNodes = 0;
            if (map == null || !map.TryGetTraversalCell(start, out _)
                || !map.TryGetTraversalCell(target, out _)
                || canEnter != null && (!canEnter(start) || !canEnter(target))) return false;
            if (start == target)
            {
                path.Add(start);
                return true;
            }

            var costs = new Dictionary<DungeonTraversalPosition, float> { [start] = 0f };
            var previous = new Dictionary<DungeonTraversalPosition, DungeonTraversalPosition> { [start] = start };
            var directChecks = new Dictionary<(DungeonTraversalPosition First, DungeonTraversalPosition Second), bool>();
            bool CanNavigateCached(DungeonTraversalPosition first, DungeonTraversalPosition second)
            {
                var key = (first, second);
                if (directChecks.TryGetValue(key, out var result)) return result;
                result = canNavigateDirectly(first, second);
                directChecks[key] = result;
                return result;
            }
            var pending = new OpenNodeQueue();
            pending.Enqueue(start, AnyAngleHeuristic(map, start, target) * AnyAngleHeuristicWeight, 0f);

            while (pending.Count > 0)
            {
                var currentNode = pending.Dequeue();
                if (!costs.TryGetValue(currentNode.Position, out var currentCost)
                    || currentNode.Cost > currentCost + .0001f) continue;
                expandedNodes++;
                if (currentNode.Position == target)
                {
                    path = RebuildAnyAnglePath(previous, start, target);
                    return true;
                }

                foreach (var next in GetAnyAngleNeighbours(map, currentNode.Position, canOpenDoor, canEnter,
                             canNavigateDirectly == null ? null : CanNavigateCached))
                {
                    var candidateParent = currentNode.Position;
                    var candidateCost = currentCost + TraversalDistance(map, currentNode.Position, next);
                    if (previous.TryGetValue(currentNode.Position, out var parent)
                        && parent != currentNode.Position
                        && CanNavigateBetween(map, parent, next, canOpenDoor,
                            canNavigateDirectly == null ? null : CanNavigateCached, canEnter))
                    {
                        var parentCost = costs[parent] + TraversalDistance(map, parent, next);
                        if (parentCost <= candidateCost + .0001f)
                        {
                            candidateParent = parent;
                            candidateCost = parentCost;
                        }
                    }

                    if (costs.TryGetValue(next, out var oldCost) && candidateCost + .0001f >= oldCost) continue;
                    costs[next] = candidateCost;
                    previous[next] = candidateParent;
                    pending.Enqueue(next, candidateCost + AnyAngleHeuristic(map, next, target) * AnyAngleHeuristicWeight, candidateCost);
                }
            }

            return false;
        }

        private static IEnumerable<DungeonTraversalPosition> GetAnyAngleNeighbours(
            DungeonFloorMap map,
            DungeonTraversalPosition current,
            System.Func<DungeonSurfaceEdge, bool> canOpenDoor,
            System.Func<DungeonTraversalPosition, bool> canEnter,
            System.Func<DungeonTraversalPosition, DungeonTraversalPosition, bool> canNavigateDirectly)
        {
            foreach (var next in map.GetTraversalNeighbours(current, canOpenDoor))
            {
                if (canEnter != null && !canEnter(next)
                    || !CanUseTraversalNeighbour(map, current, next, canOpenDoor, canNavigateDirectly)) continue;
                yield return next;
            }

            if (current.IsOnStairs) yield break;
            foreach (var direction in anyAngleDirections)
            {
                if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 2) continue;
                var diagonalSurface = new DungeonSurfacePosition(current.Surface.Cell + direction, current.Surface.Level);
                var diagonal = new DungeonTraversalPosition(diagonalSurface);
                var corner = new DungeonTraversalPosition(new DungeonSurfacePosition(
                    new Vector2Int(diagonalSurface.Cell.x, current.Surface.Cell.y), current.Surface.Level));
                var other = new DungeonTraversalPosition(new DungeonSurfacePosition(
                    new Vector2Int(current.Surface.Cell.x, diagonalSurface.Cell.y), current.Surface.Level));
                if ((canEnter != null && !canEnter(diagonal))
                    || !map.TryGetTraversalCell(diagonal, out _)
                    || canNavigateDirectly == null && canEnter != null && (!canEnter(corner) || !canEnter(other))
                    || !CanNavigateBetween(map, current, diagonal, canOpenDoor, canNavigateDirectly, canEnter)) continue;
                yield return diagonal;
            }
        }

        private static bool CanUseTraversalNeighbour(
            DungeonFloorMap map,
            DungeonTraversalPosition current,
            DungeonTraversalPosition next,
            System.Func<DungeonSurfaceEdge, bool> canOpenDoor,
            System.Func<DungeonTraversalPosition, DungeonTraversalPosition, bool> canNavigateDirectly)
        {
            if (canNavigateDirectly == null || current.IsOnStairs || next.IsOnStairs
                || current.Surface.Level != next.Surface.Level) return true;
            if (canNavigateDirectly(current, next)) return true;

            var first = current.Surface;
            var second = next.Surface;
            var edge = new DungeonSurfaceEdge(new DungeonCellEdge(first.Cell, second.Cell), first.Level);
            if (map.GetWallType(edge) == Godmist.Core.Enums.DungeonWallType.Low) return true;

            // A closed, directly openable door remains an explicit cardinal
            // transition; the executor opens it before moving through the edge.
            return !map.CanTraverseBetweenSurfaces(first, second)
                && map.CanTraverseBetweenSurfaces(first, second, canOpenDoor);
        }

        private static bool CanNavigateBetween(
            DungeonFloorMap map,
            DungeonTraversalPosition first,
            DungeonTraversalPosition second,
            System.Func<DungeonSurfaceEdge, bool> canOpenDoor,
            System.Func<DungeonTraversalPosition, DungeonTraversalPosition, bool> canNavigateDirectly,
            System.Func<DungeonTraversalPosition, bool> canEnter)
        {
            if (first == second) return true;
            if (canEnter != null && (!canEnter(first) || !canEnter(second))) return false;
            if (canNavigateDirectly != null) return canNavigateDirectly(first, second);
            if (first.IsOnStairs || second.IsOnStairs)
                return new List<DungeonTraversalPosition>(map.GetTraversalNeighbours(first, canOpenDoor)).Contains(second);
            if (first.Surface.Level != second.Surface.Level) return false;
            var delta = second.Surface.Cell - first.Surface.Cell;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                return map.CanTraverseBetweenSurfaces(first.Surface, second.Surface, canOpenDoor);
            if (Mathf.Abs(delta.x) != 1 || Mathf.Abs(delta.y) != 1) return false;
            var corner = new DungeonSurfacePosition(new Vector2Int(second.Surface.Cell.x, first.Surface.Cell.y), first.Surface.Level);
            var other = new DungeonSurfacePosition(new Vector2Int(first.Surface.Cell.x, second.Surface.Cell.y), first.Surface.Level);
            var cornerPosition = new DungeonTraversalPosition(corner);
            var otherPosition = new DungeonTraversalPosition(other);
            if (canEnter != null && (!canEnter(cornerPosition) || !canEnter(otherPosition))) return false;
            // A direct diagonal is a continuous move, so it may only cross
            // already open edges. Openable doors stay explicit cardinal nodes;
            // otherwise the executor cannot trigger them before the shortcut.
            return map.CanTraverseBetweenSurfaces(first.Surface, corner)
                && map.CanTraverseBetweenSurfaces(corner, second.Surface)
                && map.CanTraverseBetweenSurfaces(first.Surface, other)
                && map.CanTraverseBetweenSurfaces(other, second.Surface);
        }

        private static float TraversalDistance(DungeonFloorMap map, DungeonTraversalPosition first, DungeonTraversalPosition second)
        {
            if (!map.TryGetTraversalCell(first, out var firstCell) || !map.TryGetTraversalCell(second, out var secondCell)) return float.PositiveInfinity;
            return Mathf.Max(1f, Vector2.Distance(firstCell, secondCell));
        }

        private static float AnyAngleHeuristic(DungeonFloorMap map, DungeonTraversalPosition current, DungeonTraversalPosition target)
        {
            if (!map.TryGetTraversalCell(current, out var currentCell) || !map.TryGetTraversalCell(target, out var targetCell)) return 0f;
            return Vector2.Distance(currentCell, targetCell);
        }

        private static List<DungeonTraversalPosition> RebuildAnyAnglePath(
            Dictionary<DungeonTraversalPosition, DungeonTraversalPosition> previous,
            DungeonTraversalPosition start,
            DungeonTraversalPosition target)
        {
            var path = new List<DungeonTraversalPosition>();
            var current = target;
            while (current != start)
            {
                path.Add(current);
                current = previous[current];
            }

            path.Add(start);
            path.Reverse();
            return path;
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
