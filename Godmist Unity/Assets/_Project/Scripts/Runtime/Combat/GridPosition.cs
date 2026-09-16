using System;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>
    {
        public int X;
        public int Y;
        public int Level;
        public Godmist.Runtime.Dungeons.DungeonStairConnection Stair;
        public int StairStep;
        public bool IsOnStairs => StairStep > 0;
        public GridPosition(int x, int y, int level = 0)
        {
            X = x;
            Y = y;
            Level = level;
            Stair = default;
            StairStep = 0;
        }
        public bool Equals(GridPosition other)
        {
            return X == other.X && Y == other.Y && Level == other.Level && StairStep == other.StairStep
                && (!IsOnStairs || Stair.Equals(other.Stair));
        }
        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }
        public override int GetHashCode()
        {
            return IsOnStairs ? HashCode.Combine(X, Y, Level, Stair, StairStep) : HashCode.Combine(X, Y, Level);
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