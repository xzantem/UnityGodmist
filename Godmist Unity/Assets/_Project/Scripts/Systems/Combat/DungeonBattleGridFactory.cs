using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class DungeonBattleGridFactory
    {
        private static readonly Unity.Profiling.ProfilerMarker CreateSample = new("Godmist.Combat.CreateGrid");
        public bool TryCreate(
            DungeonCombatContext context,
            out BattleGrid battleGrid)
        {
            using var sample = CreateSample.Auto();
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

            if (context.SurfaceMap != null && context.SurfaceMap.HasElevatedSurfaces)
            {
                foreach (var surface in context.SurfaceMap.Surfaces)
                    if (context.ContainsWorldCell(surface.Cell)) grid.SetWalkable(ToBattleTraversalPosition(context, new DungeonTraversalPosition(surface)));
                foreach (var stair in context.SurfaceMap.Stairs)
                    if (context.SurfaceMap.TryGetStairPath(stair, out var path)
                        && context.ContainsWorldCell(stair.First.Cell) && context.ContainsWorldCell(stair.Second.Cell))
                        for (var i = 1; i < path.Count - 1; i++)
                            if (context.ContainsWorldCell(path[i])) grid.SetWalkable(ToBattleTraversalPosition(context, new DungeonTraversalPosition(stair, i)));
                grid.TraversalNeighbours = origin => GetTraversalNeighbours(context, origin);
            }
            else foreach (var worldCell in context.WorldCells) grid.SetWalkable(ToBattlePosition(context, worldCell));

            foreach (var cell in context.DifficultTerrainCells)
                if (context.ContainsWorldCell(cell)) grid.SetDifficultTerrain(ToBattlePosition(context, cell));

            if (context.SurfaceMap != null)
            {
                foreach (var edge in context.SurfaceMap.SurfaceWallEdges)
                    if (context.ContainsWorldCell(edge.FirstCell) || context.ContainsWorldCell(edge.SecondCell))
                        grid.SetWallEdge(ToBattleTraversalPosition(context, new DungeonTraversalPosition(edge.First)),
                            ToBattleTraversalPosition(context, new DungeonTraversalPosition(edge.Second)), context.SurfaceMap.GetWallType(edge));
            }
            else foreach (var edge in context.WallEdges)
                grid.SetWallEdge(ToBattlePosition(context, edge.FirstCell), ToBattlePosition(context, edge.SecondCell), context.GetWallType(edge));

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

                if (mapObject.Obstacle != null || !mapObject.BlocksMovement)
                {
                    continue;
                }

                grid.SetBlocked(
                    ToBattlePosition(
                        context,
                        mapObject.WorldPosition));
            }

            if (grid.TraversalNeighbours != null)
                foreach (var from in grid.Positions)
                foreach (var to in grid.GetNeighbours(from))
                {
                    if (!from.IsOnStairs && !to.IsOnStairs) continue;
                    var landing = !from.IsOnStairs ? from : !to.IsOnStairs ? to : (GridPosition?)null;
                    if (!landing.HasValue) continue;
                    var edge = new DungeonSurfaceEdge(new DungeonCellEdge(ToWorldPosition(context, from), ToWorldPosition(context, to)), landing.Value.Level);
                    if (context.SurfaceMap.GetWallType(edge) == Godmist.Core.Enums.DungeonWallType.Low)
                        grid.SetWallEdge(from, to, Godmist.Core.Enums.DungeonWallType.Low);
                }
            battleGrid = grid;
            return true;
        }

        private static IEnumerable<GridPosition> GetTraversalNeighbours(DungeonCombatContext context, GridPosition position)
        {
            foreach (var next in context.SurfaceMap.GetTraversalNeighbours(ToDungeonTraversalPosition(context, position)))
                yield return ToBattleTraversalPosition(context, next);
        }

        public static GridPosition ToBattleTraversalPosition(DungeonCombatContext context, DungeonTraversalPosition position)
        {
            var cell = position.Surface.Cell;
            if (position.IsOnStairs && (context.SurfaceMap == null || !context.SurfaceMap.TryGetTraversalCell(position, out cell)))
                throw new System.ArgumentException("Position does not belong to the dungeon traversal map.");
            var result = ToBattlePosition(context, cell);
            result.Level = position.IsOnStairs ? position.Stair.First.Level : position.Surface.Level;
            if (position.IsOnStairs) { result.Stair = position.Stair; result.StairStep = position.StairStep; }
            return result;
        }

        public static DungeonTraversalPosition ToDungeonTraversalPosition(DungeonCombatContext context, GridPosition position)
            => position.IsOnStairs ? new DungeonTraversalPosition(position.Stair, position.StairStep)
                : new DungeonTraversalPosition(new DungeonSurfacePosition(ToWorldPosition(context, position), position.Level));

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
