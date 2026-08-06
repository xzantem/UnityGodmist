using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonFloorMap
    {
        private readonly HashSet<Vector2Int> floorCells = new();
        private readonly Dictionary<Vector2Int, string> roomIdByCell = new();
        private readonly HashSet<DungeonCellEdge> wallEdges = new();

        public IReadOnlyCollection<Vector2Int> FloorCells => floorCells;
        public IReadOnlyCollection<DungeonCellEdge> WallEdges => wallEdges;

        public bool TryAddFloorCell(
            Vector2Int cell,
            string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)
                || floorCells.Contains(cell))
            {
                return false;
            }

            floorCells.Add(cell);
            roomIdByCell.Add(cell, roomId);

            return true;
        }

        public bool TryAddWall(
            Vector2Int firstCell,
            Vector2Int secondCell)
        {
            if (!floorCells.Contains(firstCell))
            {
                return false;
            }

            return wallEdges.Add(
                new DungeonCellEdge(
                    firstCell,
                    secondCell));
        }

        public bool IsWalkable(Vector2Int cell)
        {
            return floorCells.Contains(cell);
        }

        public bool TryGetRoomId(
            Vector2Int cell,
            out string roomId)
        {
            return roomIdByCell.TryGetValue(cell, out roomId);
        }

        public bool HasWallBetween(
            Vector2Int firstCell,
            Vector2Int secondCell)
        {
            var delta = secondCell - firstCell;

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                return false;
            }

            return wallEdges.Contains(
                new DungeonCellEdge(
                    firstCell,
                    secondCell));
        }

        public bool CanMoveBetween(
            Vector2Int firstCell,
            Vector2Int secondCell)
        {
            if (!IsWalkable(firstCell)
                || !IsWalkable(secondCell))
            {
                return false;
            }

            var delta = secondCell - firstCell;

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                return false;
            }

            return !HasWallBetween(firstCell, secondCell);
        }
    }
}