using System;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        public int X;
        public int Y;
        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }
        public bool Equals(GridPosition other)
        {
            return X == other.X && Y == other.Y;
        }
        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }
        public static bool operator ==(GridPosition left, GridPosition right)
        {
            return left.Equals(right);
        }
        public static bool operator !=(GridPosition left, GridPosition right)
        {
            return !left.Equals(right);
        }
    }
}