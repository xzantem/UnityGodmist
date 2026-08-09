using System;
using UnityEngine;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public readonly struct BattleCellEdge : IEquatable<BattleCellEdge>
    {
        public GridPosition FirstCell { get; }
        public GridPosition SecondCell { get; }

        public BattleCellEdge(
            GridPosition firstCell,
            GridPosition secondCell)
        {
            var deltaX = secondCell.X - firstCell.X;
            var deltaY = secondCell.Y - firstCell.Y;

            if (Mathf.Abs(deltaX) + Mathf.Abs(deltaY) != 1)
            {
                throw new ArgumentException(
                    "Krawedz walki musi laczyc dwa sasiednie pola.");
            }

            if (IsBefore(firstCell, secondCell))
            {
                FirstCell = firstCell;
                SecondCell = secondCell;
                return;
            }

            FirstCell = secondCell;
            SecondCell = firstCell;
        }

        public bool Equals(BattleCellEdge other)
        {
            return FirstCell == other.FirstCell
                && SecondCell == other.SecondCell;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleCellEdge other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FirstCell.GetHashCode() * 397)
                    ^ SecondCell.GetHashCode();
            }
        }

        private static bool IsBefore(
            GridPosition firstCell,
            GridPosition secondCell)
        {
            return firstCell.X < secondCell.X
                || firstCell.X == secondCell.X
                && firstCell.Y <= secondCell.Y;
        }
    }
}