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
                        if (IsExternalBoundaryEdge(cell, direction, size))
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
            Vector2Int size)
        {
            return direction switch
            {
                DungeonEdgeDirection.Up => localCell.y == size.y - 1,
                DungeonEdgeDirection.Right => localCell.x == size.x - 1,
                DungeonEdgeDirection.Down => localCell.y == 0,
                DungeonEdgeDirection.Left => localCell.x == 0,
                _ => false
            };
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
