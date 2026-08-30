using System;
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

    public static class DungeonRoomTransformUtility
    {
        private static readonly int[] SupportedRotations =
        {
            0,
            90,
            180,
            270
        };

        public static IReadOnlyList<int> AllRotations => SupportedRotations;

        public static int NormalizeRotation(int rotationDegrees)
        {
            var normalized = rotationDegrees % 360;
            if (normalized < 0) normalized += 360;

            return normalized switch
            {
                0 or 90 or 180 or 270 => normalized,
                _ => throw new ArgumentException(
                    "Rotacja pokoju musi wynosic 0, 90, 180 albo 270 stopni.",
                    nameof(rotationDegrees))
            };
        }

        public static Vector2Int GetRotatedSize(
            Vector2Int sizeInCells,
            int rotationDegrees)
        {
            var rotation = NormalizeRotation(rotationDegrees);

            return rotation == 90 || rotation == 270
                ? new Vector2Int(sizeInCells.y, sizeInCells.x)
                : sizeInCells;
        }

        public static Vector2Int TransformCell(
            Vector2Int localCell,
            Vector2Int sourceSize,
            int rotationDegrees)
        {
            var rotation = NormalizeRotation(rotationDegrees);

            return rotation switch
            {
                0 => localCell,
                90 => new Vector2Int(
                    sourceSize.y - 1 - localCell.y,
                    localCell.x),
                180 => new Vector2Int(
                    sourceSize.x - 1 - localCell.x,
                    sourceSize.y - 1 - localCell.y),
                270 => new Vector2Int(
                    localCell.y,
                    sourceSize.x - 1 - localCell.x),
                _ => localCell
            };
        }

        public static Vector2Int InverseTransformCell(
            Vector2Int transformedCell,
            Vector2Int sourceSize,
            int rotationDegrees)
        {
            var rotation = NormalizeRotation(rotationDegrees);
            var transformedSize = GetRotatedSize(sourceSize, rotation);
            var inverseRotation = (360 - rotation) % 360;

            return TransformCell(
                transformedCell,
                transformedSize,
                inverseRotation);
        }

        public static DungeonEdgeDirection TransformDirection(
            DungeonEdgeDirection direction,
            int rotationDegrees)
        {
            var rotation = NormalizeRotation(rotationDegrees);

            for (var index = 0; index < rotation / 90; index++)
            {
                direction = direction switch
                {
                    DungeonEdgeDirection.Up => DungeonEdgeDirection.Left,
                    DungeonEdgeDirection.Left => DungeonEdgeDirection.Down,
                    DungeonEdgeDirection.Down => DungeonEdgeDirection.Right,
                    DungeonEdgeDirection.Right => DungeonEdgeDirection.Up,
                    _ => direction
                };
            }

            return direction;
        }

        public static DungeonEdgeDirection InverseTransformDirection(
            DungeonEdgeDirection direction,
            int rotationDegrees)
        {
            var rotation = NormalizeRotation(rotationDegrees);

            for (var index = 0; index < rotation / 90; index++)
            {
                direction = direction switch
                {
                    DungeonEdgeDirection.Up => DungeonEdgeDirection.Right,
                    DungeonEdgeDirection.Right => DungeonEdgeDirection.Down,
                    DungeonEdgeDirection.Down => DungeonEdgeDirection.Left,
                    DungeonEdgeDirection.Left => DungeonEdgeDirection.Up,
                    _ => direction
                };
            }

            return direction;
        }

        public static Vector2Int GetDirectionOffset(
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

        public static RectInt TransformArea(
            Vector2Int localCell,
            Vector2Int areaSize,
            Vector2Int sourceSize,
            int rotationDegrees)
        {
            var firstCorner = TransformCell(
                localCell,
                sourceSize,
                rotationDegrees);
            var secondCorner = TransformCell(
                localCell + new Vector2Int(areaSize.x - 1, 0),
                sourceSize,
                rotationDegrees);
            var thirdCorner = TransformCell(
                localCell + new Vector2Int(0, areaSize.y - 1),
                sourceSize,
                rotationDegrees);
            var fourthCorner = TransformCell(
                localCell + areaSize - Vector2Int.one,
                sourceSize,
                rotationDegrees);

            var minimum = new Vector2Int(
                Mathf.Min(
                    firstCorner.x,
                    secondCorner.x,
                    thirdCorner.x,
                    fourthCorner.x),
                Mathf.Min(
                    firstCorner.y,
                    secondCorner.y,
                    thirdCorner.y,
                    fourthCorner.y));

            return new RectInt(
                minimum,
                GetRotatedSize(areaSize, rotationDegrees));
        }

        public static bool ContainsTransformedCell(
            DungeonRoomFootprint footprint,
            Vector2Int transformedCell,
            Vector2Int sourceSize,
            int rotationDegrees)
        {
            var authoredCell = InverseTransformCell(
                transformedCell,
                sourceSize,
                rotationDegrees);

            return footprint == null
                || footprint.Contains(authoredCell, sourceSize);
        }

        public static bool AllowsTransformedBoundaryEdge(
            DungeonRoomTemplateDefinition template,
            Vector2Int transformedCell,
            DungeonEdgeDirection transformedDirection,
            int rotationDegrees)
        {
            if (template == null) return false;

            var authoredCell = InverseTransformCell(
                transformedCell,
                template.SizeInCells,
                rotationDegrees);
            var authoredDirection = InverseTransformDirection(
                transformedDirection,
                rotationDegrees);

            return template.AllowsBoundaryEdge(
                authoredCell,
                authoredDirection);
        }

        public static IEnumerable<Vector2Int> EnumerateTransformedFootprint(
            DungeonRoomTemplateDefinition template,
            int rotationDegrees)
        {
            if (template == null) yield break;

            var footprint = template.Footprint ?? new DungeonRoomFootprint();

            foreach (var localCell in footprint.Enumerate(template.SizeInCells))
            {
                yield return TransformCell(
                    localCell,
                    template.SizeInCells,
                    rotationDegrees);
            }
        }

        public static bool IsSupportedRotation(int rotationDegrees)
        {
            var normalized = rotationDegrees % 360;
            if (normalized < 0) normalized += 360;

            return normalized == 0
                || normalized == 90
                || normalized == 180
                || normalized == 270;
        }
    }
}
