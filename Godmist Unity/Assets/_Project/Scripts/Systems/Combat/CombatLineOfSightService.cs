using System;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatLineOfSightService
    {
        public bool HasLineOfSight(CombatSession session, GridPosition origin, GridPosition destination)
        {
            if (session?.Grid == null
                || !session.Grid.IsWalkable(origin)
                || !session.Grid.IsWalkable(destination))
            {
                return false;
            }

            if (origin == destination) return true;

            var currentX = origin.X;
            var currentY = origin.Y;
            var deltaX = Math.Abs(destination.X - origin.X);
            var deltaY = Math.Abs(destination.Y - origin.Y);
            var stepX = origin.X < destination.X ? 1 : -1;
            var stepY = origin.Y < destination.Y ? 1 : -1;
            var error = deltaX - deltaY;

            while (currentX != destination.X || currentY != destination.Y)
            {
                var doubledError = error * 2;
                if (doubledError > -deltaY)
                {
                    error -= deltaY;
                    currentX += stepX;
                }
                
                if (doubledError < deltaX)
                {
                    error += deltaX;
                    currentY += stepY;
                }

                var currentPosition = new GridPosition(currentX, currentY);

                if (currentPosition == destination) return true;

                if (BlocksLineOfSight(session, currentPosition)) return false;
            }
            
            return true;
        }

        private static bool BlocksLineOfSight(CombatSession session, GridPosition position)
        {
            foreach (var mapObject in session.MapObjects)
            {
                if (mapObject != null && mapObject.Position == position
                    && mapObject.BlocksLineOfSight) return true;
            }

            return false;
        }
    }
}