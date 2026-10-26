using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using Godmist.Runtime.Game;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public static class DungeonCombatArenaSystem
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public static bool CanReceiveReinforcements(GameSession game, CombatSession session) => game.PlayerParty.ActiveMemberIds.Any(id =>
            game.CharactersById.TryGetValue(id, out var character) && character.LifeState == CharacterLifeState.Alive
            && !session.Units.Any(unit => unit.IsPlayerSide && unit.Character == character));

        // Read the shared floor only; cells outside the room never become combat tiles.
        public static bool BlocksEnvironmentSight(DungeonFloorInstance floor, DungeonCombatContext context,
            GridPosition from, GridPosition to)
        {
            if (context.SurfaceMap?.HasElevatedSurfaces == true)
            {
                var map = context.SurfaceMap;
                if (!map.TryGetSightPoint(DungeonBattleGridFactory.ToDungeonTraversalPosition(context, from), out var start)
                    || !map.TryGetSightPoint(DungeonBattleGridFactory.ToDungeonTraversalPosition(context, to), out var end)) return true;
                foreach (var item in floor.Objects)
                {
                    if (item == null || !item.IsActive) continue;
                    if (item is DoorDungeonObjectInstance outsideDoor)
                    {
                        if (!outsideDoor.IsOpen && !context.ContainsWorldCell(outsideDoor.Edge.FirstCell)
                            && !context.ContainsWorldCell(outsideDoor.Edge.SecondCell)
                            && DungeonFloorMap.SightCrossesEdge(start, end, new DungeonSurfaceEdge(outsideDoor.Edge))) return true;
                    }
                    else if (item is ObstacleDungeonObjectInstance outsideObstacle
                        && outsideObstacle.BlocksLineOfSight)
                    {
                        foreach (var occupiedCell in outsideObstacle.EnumerateOccupiedCells())
                            if (!context.ContainsWorldCell(occupiedCell)
                                && DungeonFloorMap.SightCrossesCell(start, end, new DungeonSurfacePosition(occupiedCell))) return true;
                    }
                }
                return false;
            }
            bool Crosses(DungeonCellEdge edge) => CombatLineOfSightService.CrossesEdge(from, to,
                new BattleCellEdge(DungeonBattleGridFactory.ToBattlePosition(context, edge.FirstCell),
                    DungeonBattleGridFactory.ToBattlePosition(context, edge.SecondCell)));
            if (floor.Map.BlocksGroundSightStep(
                DungeonBattleGridFactory.ToWorldPosition(context, from),
                DungeonBattleGridFactory.ToWorldPosition(context, to))) return true;
            var world = DungeonBattleGridFactory.ToWorldPosition(context, to);
            foreach (var obj in floor.Objects)
            {
                if (obj == null || !obj.IsActive) continue;
                // In-arena objects use combat runtime (including temporary state changes).
                if (obj is DoorDungeonObjectInstance door)
                {
                    if (!context.ContainsWorldCell(door.Edge.FirstCell)
                        && !context.ContainsWorldCell(door.Edge.SecondCell)
                        && !door.IsOpen && Crosses(door.Edge)) return true;
                }
                else if (obj is ObstacleDungeonObjectInstance obstacle
                    && obstacle.BlocksLineOfSight && obstacle.OccupiesCell(world)
                    && !context.ContainsWorldCell(world)) return true;
            }
            return false;
        }

        public static void RefreshExits(CombatSession session, DungeonFloorInstance floor, ISet<Vector2Int> explorationCells, ISet<DungeonTraversalPosition> explorationPositions = null)
        {
            // Build lookup tables once, rather than scanning every floor object
            // separately for each door and each candidate boundary neighbour.
            var doors = new Dictionary<string, DoorDungeonObjectInstance>();
            var blockedCells = new HashSet<Vector2Int>();
            foreach (var item in floor.Objects)
            {
                if (item is DoorDungeonObjectInstance door && !doors.ContainsKey(door.InstanceId))
                    doors.Add(door.InstanceId, door);
                if (!session.IsDungeonWide && item is ObstacleDungeonObjectInstance obstacle
                    && obstacle.IsActive && obstacle.BlocksMovement)
                    foreach (var occupiedCell in obstacle.EnumerateOccupiedCells()) blockedCells.Add(occupiedCell);
            }
            foreach (var combatDoor in session.MapObjects.OfType<DoorCombatMapObject>())
            {
                if (!doors.TryGetValue(combatDoor.ObjectId, out var door)) continue;
                combatDoor.IsOpen = door.IsOpen;
                session.Grid.SetBlockedEdge(combatDoor.Edge.FirstCell, combatDoor.Edge.SecondCell, !door.IsOpen);
            }
            session.OpenExitEdges.Clear();
            if (session.IsDungeonWide) return;
            var context = session.DungeonContext;
            if (context.SurfaceMap?.HasElevatedSurfaces == true)
            {
                foreach (var inside in session.Grid.Positions)
                foreach (var outside in floor.Map.GetTraversalNeighbours(DungeonBattleGridFactory.ToDungeonTraversalPosition(context, inside)))
                {
                    var target = DungeonBattleGridFactory.ToBattleTraversalPosition(context, outside);
                    if (session.Grid.IsWalkable(target) || session.IsPositionOccupied(target)
                        || explorationPositions?.Contains(outside) == true) continue;
                    floor.Map.TryGetTraversalCell(outside, out var cell);
                    if (!outside.IsOnStairs && outside.Surface.Level == 0
                        && (explorationPositions == null && explorationCells.Contains(cell)
                            || blockedCells.Contains(cell))) continue;
                    session.OpenExitEdges.Add(new BattleCellEdge(inside, target));
                }
                return;
            }
            foreach (var cell in context.WorldCells)
            foreach (var direction in Directions)
            {
                var outside = cell + direction;
                if (context.ContainsWorldCell(outside) || !floor.Map.CanMoveBetween(cell, outside)) continue;
                if (session.IsPositionOccupied(DungeonBattleGridFactory.ToBattlePosition(context, outside))) continue;
                if (explorationCells.Contains(outside)) continue;
                if (blockedCells.Contains(outside)) continue;
                session.OpenExitEdges.Add(new BattleCellEdge(
                    DungeonBattleGridFactory.ToBattlePosition(context, cell),
                    DungeonBattleGridFactory.ToBattlePosition(context, outside)));
            }
        }

    }
}
