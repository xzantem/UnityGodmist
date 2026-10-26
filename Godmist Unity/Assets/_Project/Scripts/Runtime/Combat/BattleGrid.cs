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
        [NonSerialized] private int navigationRevision;
        public int NavigationRevision => navigationRevision;
        [NonSerialized] public Func<GridPosition, IEnumerable<GridPosition>> TraversalNeighbours;
        public IEnumerable<GridPosition> Positions
        {
            get
            {
                if (usesExplicitWalkablePositions)
                { foreach (var position in walkablePositions) yield return position; yield break; }
                for (var x = 0; x < Width; x++) for (var y = 0; y < Height; y++) yield return new GridPosition(x, y);
            }
        }
        public IEnumerable<GridPosition> GetPositionsAt(int x, int y)
        {
            if (TraversalNeighbours == null) { yield return new GridPosition(x, y); yield break; }
            if (positionsByColumn.TryGetValue((x, y), out var column))
                foreach (var position in column) yield return position;
        }
        public IEnumerable<GridPosition> GetNeighbours(GridPosition origin)
        {
            if (TraversalNeighbours != null)
            { foreach (var next in TraversalNeighbours(origin)) yield return next; yield break; }
            yield return GetOffsetPosition(origin, 1, 0);
            yield return GetOffsetPosition(origin, -1, 0);
            yield return GetOffsetPosition(origin, 0, 1);
            yield return GetOffsetPosition(origin, 0, -1);
        }

        private readonly HashSet<GridPosition> walkablePositions = new();
        private readonly Dictionary<(int X, int Y), HashSet<GridPosition>> positionsByColumn = new();
        private readonly HashSet<GridPosition> blockedPositions = new();
        private readonly Dictionary<BattleCellEdge, DungeonWallType> wallTypes = new();
        public void SetWallEdge(GridPosition from, GridPosition to, DungeonWallType type)
        {
            var edge = new BattleCellEdge(from, to);
            if (!wallTypes.TryGetValue(edge, out var previous) || previous != type) navigationRevision++;
            wallTypes[edge] = type;
        }
        public bool HasWallEdge(GridPosition from, GridPosition to)
            => wallTypes.ContainsKey(new BattleCellEdge(from, to));
        private readonly HashSet<GridPosition> difficultTerrain = new();
        public void SetDifficultTerrain(GridPosition cell) { if (difficultTerrain.Add(cell)) navigationRevision++; }
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

        // Geometry is read-only during an AI query; only door edges need an overlay.
        public BattleGrid CopyForPlanning()
        {
            var copy = new BattleGrid(Width, Height, usesExplicitWalkablePositions)
            { TraversalNeighbours = TraversalNeighbours, DynamicBlocked = DynamicBlocked };
            foreach (var cell in walkablePositions) copy.SetWalkable(cell);
            copy.blockedPositions.UnionWith(blockedPositions);
            copy.blockedEdges.UnionWith(blockedEdges);
            copy.difficultTerrain.UnionWith(difficultTerrain);
            foreach (var wall in wallTypes) copy.wallTypes.Add(wall.Key, wall.Value);
            copy.navigationRevision = navigationRevision;
            return copy;
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

            return usesExplicitWalkablePositions ? walkablePositions.Contains(position)
                : position.Level == 0 && !position.IsOnStairs;
        }

        public System.Func<GridPosition, bool> DynamicBlocked { get; set; }
        public bool IsBlocked(GridPosition position)
        {
            return !IsWalkable(position)
                || blockedPositions.Contains(position) || DynamicBlocked?.Invoke(position) == true;
        }

        public void SetWalkable(
            GridPosition position,
            bool isWalkable = true)
        {
            if (!IsWithinBounds(position)) return;

            if (isWalkable)
            {
                if (walkablePositions.Add(position))
                {
                    navigationRevision++;
                    var key = (position.X, position.Y);
                    if (!positionsByColumn.TryGetValue(key, out var column))
                        positionsByColumn[key] = column = new HashSet<GridPosition>();
                    column.Add(position);
                }
                return;
            }

            if (walkablePositions.Remove(position)
                && positionsByColumn.TryGetValue((position.X, position.Y), out var remainingColumn))
            {
                navigationRevision++;
                remainingColumn.Remove(position);
                if (remainingColumn.Count == 0) positionsByColumn.Remove((position.X, position.Y));
            }
        }

        public void SetBlocked(
            GridPosition position,
            bool isBlocked = true)
        {
            if (!IsWithinBounds(position)) return;

            if (isBlocked)
            {
                if (blockedPositions.Add(position)) navigationRevision++;
                return;
            }

            if (blockedPositions.Remove(position)) navigationRevision++;
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
                if (blockedEdges.Add(edge)) navigationRevision++;
                return;
            }

            if (blockedEdges.Remove(edge)) navigationRevision++;
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
                origin.Y + deltaY, origin.Level);
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

            if (TraversalNeighbours != null)
            {
                var connected = false;
                foreach (var candidate in TraversalNeighbours(firstCell)) if (candidate == secondCell) { connected = true; break; }
                if (!connected) return false;
            }
            else if (firstCell.Level != secondCell.Level || firstCell.IsOnStairs || secondCell.IsOnStairs) return false;

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
