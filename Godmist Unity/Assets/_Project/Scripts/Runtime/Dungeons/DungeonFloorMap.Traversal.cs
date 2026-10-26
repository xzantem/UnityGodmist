using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Utilities;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    /// <summary>Identifies either a floor surface or an interior cell of one explicit stair route.</summary>
    public readonly struct DungeonTraversalPosition : IEquatable<DungeonTraversalPosition>
    {
        public DungeonSurfacePosition Surface { get; }
        public DungeonStairConnection Stair { get; }
        public int StairStep { get; }
        public bool IsOnStairs => StairStep > 0;
        public DungeonTraversalPosition(DungeonSurfacePosition surface) { Surface = surface; Stair = default; StairStep = 0; }
        public DungeonTraversalPosition(DungeonStairConnection stair, int step)
        {
            if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
            Surface = default; Stair = stair; StairStep = step;
        }
        public bool Equals(DungeonTraversalPosition other) => IsOnStairs == other.IsOnStairs && (IsOnStairs
            ? Stair.Equals(other.Stair) && StairStep == other.StairStep : Surface == other.Surface);
        public override bool Equals(object obj) => obj is DungeonTraversalPosition other && Equals(other);
        public override int GetHashCode() => IsOnStairs ? HashCode.Combine(Stair, StairStep) : Surface.GetHashCode();
        public static bool operator ==(DungeonTraversalPosition a, DungeonTraversalPosition b) => a.Equals(b);
        public static bool operator !=(DungeonTraversalPosition a, DungeonTraversalPosition b) => !a.Equals(b);
    }

    public partial class DungeonFloorMap
    {
        private readonly Dictionary<DungeonStairConnection, IReadOnlyList<Vector2Int>> stairPaths = new();
        private readonly Dictionary<DungeonSurfacePosition, List<DungeonStairConnection>> stairsByLanding = new();
        private static readonly Vector2Int[] traversalDirections = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public bool TryAddStairs(DungeonSurfacePosition first, DungeonSurfacePosition second, IReadOnlyList<Vector2Int> authoredPath)
        {
            if (first.Level == second.Level || !IsWalkable(first) || !IsWalkable(second)) return false;
            var stair = new DungeonStairConnection(first, second);
            if (stairs.Contains(stair)) return false;
            var resolved = DungeonStairPathUtility.TryResolve(first.Cell, second.Cell, authoredPath, out var path, out _);
            // Preserve unresolved historical endpoints, but never expose them as traversable shortcuts.
            if (!resolved && authoredPath != null && authoredPath.Count != 0) return false;
            stairs.Add(stair);
            TraversalRevision++;
            if (resolved)
            {
                if (stair.First != first) Array.Reverse(path);
                stairPaths.Add(stair, Array.AsReadOnly(path));
                IndexStair(stair.First, stair); IndexStair(stair.Second, stair);
            }
            return true;
        }

        private void IndexStair(DungeonSurfacePosition landing, DungeonStairConnection stair)
        {
            if (!stairsByLanding.TryGetValue(landing, out var connections)) stairsByLanding.Add(landing, connections = new List<DungeonStairConnection>());
            connections.Add(stair);
            connections.Sort((a, b) => { var first = a.First.CompareTo(b.First); return first != 0 ? first : a.Second.CompareTo(b.Second); });
        }

        public bool TryGetStairPath(DungeonStairConnection stair, out IReadOnlyList<Vector2Int> path)
            => stairPaths.TryGetValue(stair, out path);

        public bool TryGetTraversalCell(DungeonTraversalPosition position, out Vector2Int cell)
        {
            cell = default;
            if (!position.IsOnStairs)
            {
                cell = position.Surface.Cell;
                return IsWalkable(position.Surface);
            }
            if (!stairPaths.TryGetValue(position.Stair, out var path) || position.StairStep >= path.Count - 1) return false;
            cell = path[position.StairStep]; return true;
        }

        public bool CanTraverseBetweenSurfaces(DungeonSurfacePosition first, DungeonSurfacePosition second, Func<DungeonSurfaceEdge, bool> canOpenDoor = null)
            => first.Level == second.Level && IsWalkable(first) && IsWalkable(second)
                && DungeonStairPathUtility.AreAdjacent(first.Cell, second.Cell)
                && IsTraversalEdgeOpen(new DungeonSurfaceEdge(first, second), canOpenDoor);

        private bool IsTraversalEdgeOpen(DungeonSurfaceEdge edge, Func<DungeonSurfaceEdge, bool> canOpenDoor = null)
            => (!wallEdges.Contains(edge) || GetWallType(edge) is DungeonWallType.Low or DungeonWallType.TreeTrunk)
                && (!closedDoorEdges.Contains(edge) || canOpenDoor?.Invoke(edge) == true) && !closedProgressionGateEdges.Contains(edge);

        private bool IsStairStepOpen(DungeonStairConnection stair, IReadOnlyList<Vector2Int> path, int from, int to, Func<DungeonSurfaceEdge, bool> canOpenDoor = null)
        {
            if ((from == 0 || to == 0) && !IsTraversalEdgeOpen(new DungeonSurfaceEdge(new DungeonCellEdge(path[0], path[1]), stair.First.Level), canOpenDoor)) return false;
            var last = path.Count - 1;
            return (from != last && to != last) || IsTraversalEdgeOpen(new DungeonSurfaceEdge(new DungeonCellEdge(path[last - 1], path[last]), stair.Second.Level), canOpenDoor);
        }

        private static DungeonTraversalPosition StairPosition(DungeonStairConnection stair, int index, int count)
            => index == 0 ? new DungeonTraversalPosition(stair.First)
                : index == count - 1 ? new DungeonTraversalPosition(stair.Second) : new DungeonTraversalPosition(stair, index);

        /// <summary>Same ordered neighbours as GetTraversalNeighbours, in a caller-owned reusable buffer.</summary>
        public void CollectTraversalNeighbours(DungeonTraversalPosition position, List<DungeonTraversalPosition> result,
            Func<DungeonSurfaceEdge, bool> canOpenDoor = null)
        {
            result.Clear();
            if (!TryGetTraversalCell(position, out _)) return;
            if (position.IsOnStairs)
            {
                var path = stairPaths[position.Stair];
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    int next = position.StairStep + direction;
                    if (IsStairStepOpen(position.Stair, path, position.StairStep, next, canOpenDoor))
                        result.Add(StairPosition(position.Stair, next, path.Count));
                }
                return;
            }
            foreach (var direction in traversalDirections)
            {
                var next = new DungeonSurfacePosition(position.Surface.Cell + direction, position.Surface.Level);
                if (CanTraverseBetweenSurfaces(position.Surface, next, canOpenDoor)) result.Add(new DungeonTraversalPosition(next));
            }
            if (!stairsByLanding.TryGetValue(position.Surface, out var routes)) return;
            foreach (var stair in routes)
            {
                var path = stairPaths[stair];
                int from = position.Surface == stair.First ? 0 : path.Count - 1;
                int to = from == 0 ? 1 : from - 1;
                if (IsStairStepOpen(stair, path, from, to, canOpenDoor)) result.Add(StairPosition(stair, to, path.Count));
            }
        }

        /// <summary>Each edge is one ordinary movement step. No implicit transitions between overlapping projections.</summary>
        public IEnumerable<DungeonTraversalPosition> GetTraversalNeighbours(DungeonTraversalPosition position, Func<DungeonSurfaceEdge, bool> canOpenDoor = null)
        {
            if (!TryGetTraversalCell(position, out _)) yield break;
            if (position.IsOnStairs)
            {
                var path = stairPaths[position.Stair];
                foreach (var next in new[] { position.StairStep - 1, position.StairStep + 1 })
                    if (IsStairStepOpen(position.Stair, path, position.StairStep, next, canOpenDoor)) yield return StairPosition(position.Stair, next, path.Count);
                yield break;
            }
            foreach (var direction in traversalDirections)
            {
                var next = new DungeonSurfacePosition(position.Surface.Cell + direction, position.Surface.Level);
                if (CanTraverseBetweenSurfaces(position.Surface, next, canOpenDoor)) yield return new DungeonTraversalPosition(next);
            }
            if (!stairsByLanding.TryGetValue(position.Surface, out var routes)) yield break;
            foreach (var stair in routes)
            {
                var path = stairPaths[stair];
                var from = position.Surface == stair.First ? 0 : path.Count - 1;
                var to = from == 0 ? 1 : from - 1;
                if (IsStairStepOpen(stair, path, from, to, canOpenDoor)) yield return StairPosition(stair, to, path.Count);
            }
        }
    }
}
