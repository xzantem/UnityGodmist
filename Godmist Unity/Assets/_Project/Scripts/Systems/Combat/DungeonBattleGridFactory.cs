using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class DungeonBattleGridFactory
    {
        public bool TryCreate(
            DungeonCombatContext context,
            out BattleGrid battleGrid)
        {
            battleGrid = null;

            if (context == null
                || context.WorldCells == null
                || context.WorldCells.Count == 0)
            {
                return false;
            }

            var bounds = context.WorldBounds;

            if (bounds.width <= 0 || bounds.height <= 0)
            {
                return false;
            }

            var grid = new BattleGrid(
                bounds.width,
                bounds.height,
                nextUsesExplicitWalkablePositions: true);

            foreach (var worldCell in context.WorldCells)
            {
                grid.SetWalkable(
                    ToBattlePosition(context, worldCell));
            }

            foreach (var wallEdge in context.WallEdges)
            {
                var firstCell = ToBattlePosition(
                    context,
                    wallEdge.FirstCell);

                var secondCell = ToBattlePosition(
                    context,
                    wallEdge.SecondCell);

                grid.SetBlockedEdge(firstCell, secondCell);
            }

            foreach (var mapObject in context.MapObjects)
            {
                if (mapObject.UsesEdge)
                {
                    grid.SetBlockedEdge(
                        ToBattlePosition(
                            context,
                            mapObject.Edge.FirstCell),
                        ToBattlePosition(
                            context,
                            mapObject.Edge.SecondCell),
                        mapObject.BlocksMovement);

                    continue;
                }

                if (!mapObject.BlocksMovement)
                {
                    continue;
                }

                grid.SetBlocked(
                    ToBattlePosition(
                        context,
                        mapObject.WorldPosition));
            }

            battleGrid = grid;
            return true;
        }

        public static GridPosition ToBattlePosition(
            DungeonCombatContext context,
            Vector2Int worldPosition)
        {
            return new GridPosition(
                worldPosition.x - context.WorldBounds.xMin,
                worldPosition.y - context.WorldBounds.yMin);
        }

        public static Vector2Int ToWorldPosition(
            DungeonCombatContext context,
            GridPosition battlePosition)
        {
            return new Vector2Int(
                battlePosition.X + context.WorldBounds.xMin,
                battlePosition.Y + context.WorldBounds.yMin);
        }
    }
}