using System;
using Godmist.Core.Enums;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public class DungeonFloorMap
    {
        private readonly HashSet<Vector2Int> floorCells = new();
        private readonly Dictionary<Vector2Int, string> roomIdByCell = new();
        private readonly Dictionary<DungeonCellEdge, DungeonWallType> wallTypes = new();
        private readonly HashSet<Vector2Int> difficultTerrain = new();
        public IReadOnlyCollection<Vector2Int> DifficultTerrainCells => difficultTerrain;
        public bool IsDifficultTerrain(Vector2Int cell) => difficultTerrain.Contains(cell);
        public void SetDifficultTerrain(Vector2Int cell)
        {
            if (!IsWalkable(cell)) throw new ArgumentException("Terrain must be on a floor cell.");
            difficultTerrain.Add(cell);
        }
        public IReadOnlyDictionary<DungeonCellEdge, DungeonWallType> WallTypes => wallTypes;
        public DungeonWallType GetWallType(DungeonCellEdge edge) => wallTypes.TryGetValue(edge, out var type) ? type : DungeonWallType.Full;
        public bool SetWallType(DungeonCellEdge edge, DungeonWallType type)
        {
            if (!floorCells.Contains(edge.FirstCell) && !floorCells.Contains(edge.SecondCell)) return false;
            wallEdges.Add(edge); wallTypes[edge] = type; return true;
        }

        private readonly HashSet<DungeonCellEdge> wallEdges = new();
        private readonly HashSet<DungeonCellEdge> closedDoorEdges = new();
        private readonly HashSet<DungeonCellEdge> closedProgressionGateEdges = new();

        public IReadOnlyCollection<Vector2Int> FloorCells => floorCells;
        public IReadOnlyCollection<DungeonCellEdge> WallEdges => wallEdges;
        public IReadOnlyCollection<DungeonCellEdge> ClosedProgressionGateEdges =>
            closedProgressionGateEdges;

        public event Action ProgressionGateStateChanged;

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

        public bool TrySetDoorState(
            DungeonCellEdge edge,
            bool isOpen)
        {
            if (!floorCells.Contains(edge.FirstCell)
                || !floorCells.Contains(edge.SecondCell))
            {
                return false;
            }

            if (isOpen)
            {
                closedDoorEdges.Remove(edge);
                return true;
            }

            closedDoorEdges.Add(edge);
            return true;
        }

        public bool TrySetProgressionGateState(
            DungeonCellEdge edge,
            bool isOpen)
        {
            if (!floorCells.Contains(edge.FirstCell)
                || !floorCells.Contains(edge.SecondCell))
            {
                return false;
            }

            if (isOpen)
            {
                if (closedProgressionGateEdges.Remove(edge))
                {
                    ProgressionGateStateChanged?.Invoke();
                }

                return true;
            }

            if (closedProgressionGateEdges.Add(edge))
            {
                ProgressionGateStateChanged?.Invoke();
            }

            return true;
        }

        public bool CanMoveBetween(
            Vector2Int firstCell,
            Vector2Int secondCell,
            bool ignoreClosedDoorEdges = false)
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

            var edge = new DungeonCellEdge(firstCell, secondCell);

            return (!HasWallBetween(firstCell, secondCell)
                    || GetWallType(new DungeonCellEdge(firstCell, secondCell)) == DungeonWallType.Low)
                && (ignoreClosedDoorEdges
                    || (!closedDoorEdges.Contains(edge)
                        && !closedProgressionGateEdges.Contains(edge)));
        }
    }
}
