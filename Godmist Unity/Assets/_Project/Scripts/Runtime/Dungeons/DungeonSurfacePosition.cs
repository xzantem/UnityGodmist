using System;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    /// <summary>A walkable surface within one dungeon floor, not a floor index.</summary>
    public readonly struct DungeonSurfacePosition : IEquatable<DungeonSurfacePosition>, IComparable<DungeonSurfacePosition>
    {
        public Vector2Int Cell { get; }
        public int Level { get; }
        public DungeonSurfacePosition(Vector2Int cell, int level = 0) { Cell = cell; Level = level; }
        public bool Equals(DungeonSurfacePosition other) => Cell == other.Cell && Level == other.Level;
        public override bool Equals(object obj) => obj is DungeonSurfacePosition other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Cell, Level);
        public int CompareTo(DungeonSurfacePosition other)
        {
            var result = Level.CompareTo(other.Level);
            if (result == 0) result = Cell.x.CompareTo(other.Cell.x);
            return result == 0 ? Cell.y.CompareTo(other.Cell.y) : result;
        }
        public static bool operator ==(DungeonSurfacePosition a, DungeonSurfacePosition b) => a.Equals(b);
        public static bool operator !=(DungeonSurfacePosition a, DungeonSurfacePosition b) => !a.Equals(b);
        public override string ToString() => $"({Cell.x}, {Cell.y}, level {Level})";
    }

    /// <summary>Explicit, bidirectional stair connection; never inferred from overlapping XY.</summary>
    public readonly struct DungeonStairConnection : IEquatable<DungeonStairConnection>
    {
        public DungeonSurfacePosition First { get; }
        public DungeonSurfacePosition Second { get; }
        public DungeonStairConnection(DungeonSurfacePosition first, DungeonSurfacePosition second)
        {
            if (first.Level == second.Level)
                throw new ArgumentException("Stairs must connect different surface levels.");
            // Endpoints are explicit: the future authoring pass determines the stair footprint.
            if (first.CompareTo(second) < 0) { First = first; Second = second; }
            else { First = second; Second = first; }
        }
        public bool Equals(DungeonStairConnection other) => First == other.First && Second == other.Second;
        public override bool Equals(object obj) => obj is DungeonStairConnection other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(First, Second);
    }
}

namespace Godmist.Runtime.Dungeons
{
    /// <summary>An ordinary cardinal edge on one elevation; stairs are separate links.</summary>
    public readonly struct DungeonSurfaceEdge : System.IEquatable<DungeonSurfaceEdge>
    {
        public DungeonCellEdge CellEdge { get; }
        public int Level { get; }
        public Vector2Int FirstCell => CellEdge.FirstCell;
        public Vector2Int SecondCell => CellEdge.SecondCell;
        public DungeonSurfacePosition First => new(FirstCell, Level);
        public DungeonSurfacePosition Second => new(SecondCell, Level);
        public DungeonSurfaceEdge(DungeonCellEdge edge, int level = 0) { CellEdge = edge; Level = level; }
        public DungeonSurfaceEdge(DungeonSurfacePosition first, DungeonSurfacePosition second)
        {
            if (first.Level != second.Level) throw new System.ArgumentException("Ordinary edges require equal levels; use stairs for elevation changes.");
            CellEdge = new DungeonCellEdge(first.Cell, second.Cell);
            Level = first.Level;
        }
        public bool Equals(DungeonSurfaceEdge other) => Level == other.Level && CellEdge.Equals(other.CellEdge);
        public override bool Equals(object obj) => obj is DungeonSurfaceEdge other && Equals(other);
        public override int GetHashCode() => unchecked(CellEdge.GetHashCode() * 397 ^ Level);
    }
}
