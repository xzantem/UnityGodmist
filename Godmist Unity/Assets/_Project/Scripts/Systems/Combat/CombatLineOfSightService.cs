using System;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatLineOfSightService
    {
        public bool HasLineOfSight(
            CombatSession session,
            GridPosition origin,
            GridPosition destination)
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
                var previousPosition = new GridPosition(currentX, currentY);
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

                if (BlocksLineOfSight(
                        session,
                        previousPosition,
                        currentPosition))
                {
                    return false;
                }

                if (currentPosition == destination) return true;
            }

            return true;
        }

        private static bool BlocksLineOfSight(
            CombatSession session,
            GridPosition previousPosition,
            GridPosition currentPosition)
        {
            if (session.EnvironmentBlocksLineOfSight?.Invoke(previousPosition, currentPosition) == true)
                return true;
            if (session.DungeonContext != null)
                foreach (var wall in session.DungeonContext.WallEdges)
                    if (session.DungeonContext.GetWallType(wall) == Godmist.Core.Enums.DungeonWallType.Full
                        && CrossesEdge(previousPosition, currentPosition, new BattleCellEdge(
                        DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, wall.FirstCell),
                        DungeonBattleGridFactory.ToBattlePosition(session.DungeonContext, wall.SecondCell))))
                        return true;
            foreach (var mapObject in session.MapObjects)
            {
                if (mapObject == null || !session.DoesObjectBlockLineOfSight(mapObject))
                {
                    continue;
                }

                if (mapObject is DoorCombatMapObject door)
                {
                    if (CrossesEdge(
                            previousPosition,
                            currentPosition,
                            door.Edge))
                    {
                        return true;
                    }

                    continue;
                }

                if (mapObject.Position == currentPosition)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool CrossesEdge(
            GridPosition previousPosition,
            GridPosition currentPosition,
            BattleCellEdge edge)
        {
            if (IsSameEdge(
                    previousPosition,
                    currentPosition,
                    edge))
            {
                return true;
            }

            var horizontalStep = new GridPosition(
                currentPosition.X,
                previousPosition.Y);

            var verticalStep = new GridPosition(
                previousPosition.X,
                currentPosition.Y);

            return IsSameEdge(previousPosition, horizontalStep, edge)
                || IsSameEdge(horizontalStep, currentPosition, edge)
                || IsSameEdge(previousPosition, verticalStep, edge)
                || IsSameEdge(verticalStep, currentPosition, edge);
        }

        private static bool IsSameEdge(
            GridPosition firstCell,
            GridPosition secondCell,
            BattleCellEdge edge)
        {
            var deltaX = Math.Abs(secondCell.X - firstCell.X);
            var deltaY = Math.Abs(secondCell.Y - firstCell.Y);

            if (deltaX + deltaY != 1) return false;

            return (edge.FirstCell == firstCell
                    && edge.SecondCell == secondCell)
                || (edge.FirstCell == secondCell
                    && edge.SecondCell == firstCell);
        }
    }
}