using System;
using Godmist.Core.Enums;
using System.Collections.Generic;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class BattleGrid
    {
        public int Width;
        public int Height;

        private readonly HashSet<GridPosition> walkablePositions = new();
        private readonly HashSet<GridPosition> blockedPositions = new();
        private readonly Dictionary<BattleCellEdge, DungeonWallType> wallTypes = new();
        public void SetWallEdge(GridPosition from, GridPosition to, DungeonWallType type)
            => wallTypes[new BattleCellEdge(from, to)] = type;
        private readonly HashSet<GridPosition> difficultTerrain = new();
        public void SetDifficultTerrain(GridPosition cell) => difficultTerrain.Add(cell);
        public bool IsDifficultTerrain(GridPosition cell) => difficultTerrain.Contains(cell);
        public int GetTerrainMovementSurcharge(GridPosition cell) => IsDifficultTerrain(cell) ? 1 : 0;
        public int GetMovementSurcharge(GridPosition from, GridPosition to)
            => (wallTypes.TryGetValue(new BattleCellEdge(from, to), out var type) && type == DungeonWallType.Low ? 2 : 0)
                + GetTerrainMovementSurcharge(to);
        public bool CanDisplaceBetween(GridPosition from, GridPosition to)
            => CanMoveBetween(from, to) && !wallTypes.ContainsKey(new BattleCellEdge(from, to));

        private readonly HashSet<BattleCellEdge> blockedEdges = new();

        private readonly bool usesExplicitWalkablePositions;

        public BattleGrid()
        {
        }

        public BattleGrid(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public BattleGrid(
            int width,
            int height,
            bool nextUsesExplicitWalkablePositions)
        {
            Width = width;
            Height = height;
            usesExplicitWalkablePositions =
                nextUsesExplicitWalkablePositions;
        }

        public bool IsWithinBounds(GridPosition position)
        {
            return position.X >= 0
                && position.Y >= 0
                && position.X < Width
                && position.Y < Height;
        }

        public bool IsWalkable(GridPosition position)
        {
            if (!IsWithinBounds(position)) return false;

            return !usesExplicitWalkablePositions
                || walkablePositions.Contains(position);
        }

        public bool IsBlocked(GridPosition position)
        {
            return !IsWalkable(position)
                || blockedPositions.Contains(position);
        }

        public void SetWalkable(
            GridPosition position,
            bool isWalkable = true)
        {
            if (!IsWithinBounds(position)) return;

            if (isWalkable)
            {
                walkablePositions.Add(position);
                return;
            }

            walkablePositions.Remove(position);
        }

        public void SetBlocked(
            GridPosition position,
            bool isBlocked = true)
        {
            if (!IsWithinBounds(position)) return;

            if (isBlocked)
            {
                blockedPositions.Add(position);
                return;
            }

            blockedPositions.Remove(position);
        }

        public void SetBlockedEdge(
            GridPosition firstCell,
            GridPosition secondCell,
            bool isBlocked = true)
        {
            if (!IsWithinBounds(firstCell)
                || !IsWithinBounds(secondCell))
            {
                return;
            }

            var edge = new BattleCellEdge(firstCell, secondCell);

            if (isBlocked)
            {
                blockedEdges.Add(edge);
                return;
            }

            blockedEdges.Remove(edge);
        }

        public bool HasBlockedEdge(
            GridPosition firstCell,
            GridPosition secondCell)
        {
            if (!IsWithinBounds(firstCell)
                || !IsWithinBounds(secondCell))
            {
                return false;
            }

            var edge = new BattleCellEdge(firstCell, secondCell);
            return blockedEdges.Contains(edge)
                || (wallTypes.TryGetValue(edge, out var type) && type != DungeonWallType.Low);
        }

        public GridPosition GetOffsetPosition(
            GridPosition origin,
            int deltaX,
            int deltaY)
        {
            return new GridPosition(
                origin.X + deltaX,
                origin.Y + deltaY);
        }

        public bool CanMoveBetween(
            GridPosition firstCell,
            GridPosition secondCell)
        {
            var deltaX = secondCell.X - firstCell.X;
            var deltaY = secondCell.Y - firstCell.Y;

            if (Math.Abs(deltaX) + Math.Abs(deltaY) != 1)
            {
                return false;
            }

            return IsWalkable(firstCell)
                && IsWalkable(secondCell)
                && !IsBlocked(firstCell)
                && !IsBlocked(secondCell)
                && !HasBlockedEdge(firstCell, secondCell)
                && (!wallTypes.TryGetValue(new BattleCellEdge(firstCell, secondCell), out var type)
                    || type == DungeonWallType.Low);
        }

        public bool TryMove(
            GridPosition origin,
            int deltaX,
            int deltaY,
            out GridPosition target)
        {
            target = GetOffsetPosition(origin, deltaX, deltaY);

            return CanMoveBetween(origin, target);
        }
    }
}