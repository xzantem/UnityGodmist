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
            bool Crosses(DungeonCellEdge edge) => CombatLineOfSightService.CrossesEdge(from, to,
                new BattleCellEdge(DungeonBattleGridFactory.ToBattlePosition(context, edge.FirstCell),
                    DungeonBattleGridFactory.ToBattlePosition(context, edge.SecondCell)));
            foreach (var wall in floor.Map.WallEdges)
                if (Crosses(wall)) return true;
            foreach (var gate in floor.Map.ClosedProgressionGateEdges)
                if (Crosses(gate)) return true;
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
                else if (!context.ContainsWorldCell(obj.Position)
                    && obj is ObstacleDungeonObjectInstance obstacle
                    && obstacle.BlocksLineOfSight && obj.Position == world) return true;
            }
            return false;
        }

        public static void RefreshExits(CombatSession session, DungeonFloorInstance floor, ISet<Vector2Int> explorationCells)
        {
            foreach (var combatDoor in session.MapObjects.OfType<DoorCombatMapObject>())
            {
                var door = floor.Objects.OfType<DoorDungeonObjectInstance>().FirstOrDefault(value => value.InstanceId == combatDoor.ObjectId);
                if (door == null) continue;
                combatDoor.IsOpen = door.IsOpen;
                session.Grid.SetBlockedEdge(combatDoor.Edge.FirstCell, combatDoor.Edge.SecondCell, !door.IsOpen);
            }
            session.OpenExitEdges.Clear();
            var context = session.DungeonContext;
            foreach (var cell in context.WorldCells)
            foreach (var direction in Directions)
            {
                var outside = cell + direction;
                if (context.ContainsWorldCell(outside) || !floor.Map.CanMoveBetween(cell, outside)) continue;
                if (session.IsPositionOccupied(DungeonBattleGridFactory.ToBattlePosition(context, outside))) continue;
                if (explorationCells.Contains(outside)) continue;
                if (floor.Objects.OfType<ObstacleDungeonObjectInstance>().Any(
                    obstacle => obstacle.IsActive && obstacle.BlocksMovement && obstacle.Position == outside)) continue;
                session.OpenExitEdges.Add(new BattleCellEdge(
                    DungeonBattleGridFactory.ToBattlePosition(context, cell),
                    DungeonBattleGridFactory.ToBattlePosition(context, outside)));
            }
        }

    }
}
