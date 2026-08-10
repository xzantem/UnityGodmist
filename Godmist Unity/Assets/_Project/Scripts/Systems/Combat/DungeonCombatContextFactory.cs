using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class DungeonCombatContextFactory
    {
        public bool TryCreate(
            DungeonInstance dungeon,
            DungeonEncounterInstance encounter,
            out DungeonCombatContext context)
        {
            context = null;

            if (dungeon == null || encounter == null)
            {
                return false;
            }

            if (!dungeon.TryGetCurrentFloor(out var floor)
                || floor.Map == null)
            {
                return false;
            }

            var combatCells = CollectEncounterRoomCells(
                floor.Map,
                encounter.RoomId);

            if (combatCells.Count == 0)
            {
                return false;
            }

            var combatCellSet = new HashSet<Vector2Int>(combatCells);
            var wallEdges = CollectInternalWalls(
                floor.Map,
                combatCellSet);

            var mapObjects = CollectActiveObjects(
                floor,
                combatCellSet);

            context = new DungeonCombatContext(
                dungeon.DungeonId,
                floor.FloorIndex,
                encounter.InstanceId,
                encounter.RoomId,
                ResolvePartyAnchor(dungeon, encounter),
                encounter.ExplorationPosition,
                combatCells,
                wallEdges,
                mapObjects);

            return true;
        }

        private static List<Vector2Int> CollectEncounterRoomCells(
            DungeonFloorMap map,
            string roomId)
        {
            var cells = new List<Vector2Int>();

            foreach (var cell in map.FloorCells)
            {
                if (!map.TryGetRoomId(cell, out var cellRoomId)
                    || cellRoomId != roomId)
                {
                    continue;
                }

                cells.Add(cell);
            }

            return cells;
        }

        private static List<DungeonCellEdge> CollectInternalWalls(
            DungeonFloorMap map,
            HashSet<Vector2Int> combatCells)
        {
            var walls = new List<DungeonCellEdge>();

            foreach (var wallEdge in map.WallEdges)
            {
                if (combatCells.Contains(wallEdge.FirstCell)
                    && combatCells.Contains(wallEdge.SecondCell))
                {
                    walls.Add(wallEdge);
                }
            }

            return walls;
        }

        private static List<DungeonCombatMapObjectSnapshot>
            CollectActiveObjects(
                DungeonFloorInstance floor,
                HashSet<Vector2Int> combatCells)
        {
            var snapshots = new List<DungeonCombatMapObjectSnapshot>();

            foreach (var dungeonObject in floor.Objects)
            {
                if (dungeonObject == null
                    || !dungeonObject.IsActive
                    || !combatCells.Contains(dungeonObject.Position))
                {
                    continue;
                }

                var blocksMovement = false;
                var blocksLineOfSight = false;

                if (dungeonObject is DoorDungeonObjectInstance door)
                {
                    if (!combatCells.Contains(door.Edge.FirstCell)
                        || !combatCells.Contains(door.Edge.SecondCell))
                    {
                        continue;
                    }

                    snapshots.Add(new DungeonCombatMapObjectSnapshot(
                        door.InstanceId,
                        door.Edge,
                        door.IsOpen,
                        door.InteractionMode));

                    continue;
                }

                if (dungeonObject is ObstacleDungeonObjectInstance obstacle)
                {
                    blocksMovement = obstacle.BlocksMovement;
                    blocksLineOfSight = obstacle.BlocksLineOfSight;
                }

                snapshots.Add(new DungeonCombatMapObjectSnapshot(
                    dungeonObject.InstanceId,
                    dungeonObject.ObjectType,
                    dungeonObject.Position,
                    blocksMovement,
                    blocksLineOfSight));
            }

            return snapshots;
        }

        private static Vector2Int ResolvePartyAnchor(
            DungeonInstance dungeon,
            DungeonEncounterInstance encounter)
        {
            if (dungeon.HasPlayerPosition)
            {
                return new Vector2Int(
                    Mathf.RoundToInt(dungeon.PlayerPosition.x),
                    Mathf.RoundToInt(dungeon.PlayerPosition.y));
            }

            return encounter.ExplorationPosition;
        }
    }
}