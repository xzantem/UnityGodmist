using System.Collections.Generic;
using Godmist.Core.Enums;
using UnityEngine;

namespace Godmist.Data.Dungeons
{
    public static class DungeonRoomBoundaryUtility
    {
        private static readonly DungeonEdgeDirection[] Directions =
        {
            DungeonEdgeDirection.Up,
            DungeonEdgeDirection.Right,
            DungeonEdgeDirection.Down,
            DungeonEdgeDirection.Left
        };

        public static IEnumerable<DungeonRoomBoundaryEdgeData>
            EnumerateExternalBoundaryEdges(
                DungeonRoomTemplateDefinition template)
        {
            if (template == null)
            {
                yield break;
            }

            var size = template.SizeInCells;
            var floorCells = new HashSet<Vector2Int>();
            var footprint = template.Footprint ?? new DungeonRoomFootprint();

            foreach (var cell in footprint.Enumerate(size))
            {
                floorCells.Add(cell);
            }

            foreach (var edge in EnumerateExternalBoundaryEdges(
                         size,
                         floorCells))
            {
                yield return edge;
            }
        }

        public static IEnumerable<DungeonRoomBoundaryEdgeData>
            EnumerateExternalBoundaryEdges(
                Vector2Int size,
                HashSet<Vector2Int> floorCells)
        {
            if (size.x <= 0 || size.y <= 0 || floorCells == null)
            {
                yield break;
            }

            var externallyReachableEmptyCells =
                FindExternallyReachableEmptyCells(size, floorCells);

            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    var cell = new Vector2Int(x, y);

                    if (!floorCells.Contains(cell))
                    {
                        continue;
                    }

                    foreach (var direction in Directions)
                    {
                        if (IsExternalBoundaryEdge(
                                cell,
                                direction,
                                size,
                                floorCells,
                                externallyReachableEmptyCells))
                        {
                            yield return new DungeonRoomBoundaryEdgeData(
                                cell,
                                direction);
                        }
                    }
                }
            }
        }

        public static bool IsExternalBoundaryEdge(
            Vector2Int localCell,
            DungeonEdgeDirection direction,
            Vector2Int size,
            HashSet<Vector2Int> floorCells)
        {
            if (floorCells == null || !floorCells.Contains(localCell))
            {
                return false;
            }

            var externallyReachableEmptyCells =
                FindExternallyReachableEmptyCells(size, floorCells);

            return IsExternalBoundaryEdge(
                localCell,
                direction,
                size,
                floorCells,
                externallyReachableEmptyCells);
        }

        private static bool IsExternalBoundaryEdge(
            Vector2Int localCell,
            DungeonEdgeDirection direction,
            Vector2Int size,
            HashSet<Vector2Int> floorCells,
            HashSet<Vector2Int> externallyReachableEmptyCells)
        {
            var directionOffset = direction switch
            {
                DungeonEdgeDirection.Up => Vector2Int.up,
                DungeonEdgeDirection.Right => Vector2Int.right,
                DungeonEdgeDirection.Down => Vector2Int.down,
                DungeonEdgeDirection.Left => Vector2Int.left,
                _ => Vector2Int.zero
            };

            if (directionOffset == Vector2Int.zero)
            {
                return false;
            }

            var adjacentCell = localCell + directionOffset;

            if (!IsInside(adjacentCell, size))
            {
                return true;
            }

            return !floorCells.Contains(adjacentCell)
                && externallyReachableEmptyCells.Contains(adjacentCell);
        }

        private static HashSet<Vector2Int>
            FindExternallyReachableEmptyCells(
                Vector2Int size,
                HashSet<Vector2Int> floorCells)
        {
            var reachable = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();

            for (var x = 0; x < size.x; x++)
            {
                TryEnqueueEmptyCell(
                    new Vector2Int(x, 0),
                    size,
                    floorCells,
                    reachable,
                    queue);
                TryEnqueueEmptyCell(
                    new Vector2Int(x, size.y - 1),
                    size,
                    floorCells,
                    reachable,
                    queue);
            }

            for (var y = 0; y < size.y; y++)
            {
                TryEnqueueEmptyCell(
                    new Vector2Int(0, y),
                    size,
                    floorCells,
                    reachable,
                    queue);
                TryEnqueueEmptyCell(
                    new Vector2Int(size.x - 1, y),
                    size,
                    floorCells,
                    reachable,
                    queue);
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();

                foreach (var direction in Directions)
                {
                    var adjacentCell = cell + GetDirectionOffset(direction);
                    TryEnqueueEmptyCell(
                        adjacentCell,
                        size,
                        floorCells,
                        reachable,
                        queue);
                }
            }

            return reachable;
        }

        private static void TryEnqueueEmptyCell(
            Vector2Int cell,
            Vector2Int size,
            HashSet<Vector2Int> floorCells,
            HashSet<Vector2Int> reachable,
            Queue<Vector2Int> queue)
        {
            if (!IsInside(cell, size)
                || floorCells.Contains(cell)
                || !reachable.Add(cell))
            {
                return;
            }

            queue.Enqueue(cell);
        }

        private static Vector2Int GetDirectionOffset(
            DungeonEdgeDirection direction)
        {
            return direction switch
            {
                DungeonEdgeDirection.Up => Vector2Int.up,
                DungeonEdgeDirection.Right => Vector2Int.right,
                DungeonEdgeDirection.Down => Vector2Int.down,
                DungeonEdgeDirection.Left => Vector2Int.left,
                _ => Vector2Int.zero
            };
        }

        private static bool IsInside(Vector2Int cell, Vector2Int size)
        {
            return cell.x >= 0
                && cell.y >= 0
                && cell.x < size.x
                && cell.y < size.y;
        }

        public static bool ContainsEdge(
            IEnumerable<DungeonRoomBoundaryEdgeData> edges,
            Vector2Int localCell,
            DungeonEdgeDirection direction)
        {
            if (edges == null)
            {
                return false;
            }

            foreach (var edge in edges)
            {
                if (edge != null
                    && edge.LocalCell == localCell
                    && edge.Direction == direction)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
