using System;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Runtime.Combat
{
    [Serializable]
    public class BattleGrid
    {
        public int Width;
        public int Height;

        private readonly HashSet<GridPosition> blockedPositions = new();

        public BattleGrid()
        {
        }
        public BattleGrid(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public bool IsWithinBounds(GridPosition position)
        {
            return position.X >= 0
                && position.Y >= 0
                && position.X < Width
                && position.Y < Height;
        }
        public bool IsBlocked(GridPosition position)
        {
            return blockedPositions.Contains(position);
        }
        public void SetBlocked(
            GridPosition position,
            bool isBlocked = true)
        {
            if(!IsWithinBounds(position))
            {
                return;
            }
            if (isBlocked)
            {
                blockedPositions.Add(position);
            }
            else
            {
                blockedPositions.Remove(position);
            }
        }
        public GridPosition GetOffsetPosition(GridPosition origin, int deltaX, int deltaY)
        {
            return new GridPosition(origin.X + deltaX, origin.Y + deltaY);
        }
        public bool TryMove(GridPosition origin, int deltaX, int deltaY, out GridPosition target)
        {
            target = GetOffsetPosition(origin, deltaX, deltaY);
            return IsWithinBounds(target) && !IsBlocked(target);
        }
    }
}