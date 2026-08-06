using System;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public readonly struct DungeonCellEdge : IEquatable<DungeonCellEdge>
    {
        public Vector2Int FirstCell { get; }
        public Vector2Int SecondCell { get; }

        public DungeonCellEdge(
            Vector2Int firstCell,
            Vector2Int secondCell)
        {
            var delta = secondCell - firstCell;

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                throw new ArgumentException(
                    "Krawedz lochu musi laczyc dwie sasiednie komorki.");
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

        public bool Equals(DungeonCellEdge other)
        {
            return FirstCell == other.FirstCell
                   && SecondCell == other.SecondCell;
        }

        public override bool Equals(object obj)
        {
            return obj is DungeonCellEdge other && Equals(other);
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
            Vector2Int firstCell,
            Vector2Int secondCell)
        {
            return firstCell.x < secondCell.x
                   || firstCell.x == secondCell.x
                   && firstCell.y <= secondCell.y;
        }
    }
}