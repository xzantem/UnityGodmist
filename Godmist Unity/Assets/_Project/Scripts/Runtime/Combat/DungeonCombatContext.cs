using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Runtime.Combat
{
    public sealed class DungeonCombatContext
    {
        private readonly List<Vector2Int> worldCells;
        private readonly List<DungeonCellEdge> wallEdges;
        private readonly HashSet<Vector2Int> worldCellSet;
        private readonly HashSet<DungeonCellEdge> fullWallEdges;
        public bool HasFullWall(DungeonCellEdge edge) => fullWallEdges.Contains(edge);
        private readonly List<DungeonCombatMapObjectSnapshot> mapObjects;

        public DungeonFloorMap SurfaceMap { get; }
        public bool IsFloorWide { get; }
        public string DungeonId { get; }
        public int FloorIndex { get; }
        public string EncounterInstanceId { get; }
        public string EncounterRoomId { get; }
        public int DungeonLevel { get; }
        public Vector2Int PartyAnchorWorldPosition { get; }
        public Vector2Int EnemyAnchorWorldPosition { get; }
        public RectInt WorldBounds { get; }

        public IReadOnlyCollection<Vector2Int> DifficultTerrainCells { get; }
        public IReadOnlyList<Vector2Int> WorldCells => worldCells;
        public IReadOnlyList<DungeonCellEdge> WallEdges => wallEdges;
        private readonly Dictionary<DungeonCellEdge, DungeonWallType> wallTypes;
        public DungeonWallType GetWallType(DungeonCellEdge edge) => wallTypes.TryGetValue(edge, out var type) ? type : DungeonWallType.Full;
        public IReadOnlyList<DungeonCombatMapObjectSnapshot> MapObjects => mapObjects;

        public DungeonCombatContext(
            string dungeonId,
            int floorIndex,
            string encounterInstanceId,
            string encounterRoomId,
            Vector2Int partyAnchorWorldPosition,
            Vector2Int enemyAnchorWorldPosition,
            IEnumerable<Vector2Int> nextWorldCells,
            IEnumerable<DungeonCellEdge> nextWallEdges,
            IEnumerable<DungeonCombatMapObjectSnapshot> nextMapObjects,
            int dungeonLevel = 1,
            IReadOnlyDictionary<DungeonCellEdge, DungeonWallType> nextWallTypes = null,
            IEnumerable<Vector2Int> difficultTerrainCells = null,
            DungeonFloorMap surfaceMap = null, bool isFloorWide = false)
        {
            if (string.IsNullOrWhiteSpace(dungeonId))
            {
                throw new ArgumentException(
                    "DungeonId nie moze byc puste.",
                    nameof(dungeonId));
            }

            if (floorIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(floorIndex));
            }

            if (string.IsNullOrWhiteSpace(encounterInstanceId))
            {
                throw new ArgumentException(
                    "EncounterInstanceId nie moze byc puste.",
                    nameof(encounterInstanceId));
            }

            if (string.IsNullOrWhiteSpace(encounterRoomId))
            {
                throw new ArgumentException(
                    "EncounterRoomId nie moze byc puste.",
                    nameof(encounterRoomId));
            }

            worldCells = nextWorldCells != null
                ? new List<Vector2Int>(nextWorldCells)
                : new List<Vector2Int>();

            if (worldCells.Count == 0)
            {
                throw new ArgumentException(
                    "Kontekst walki musi zawierac co najmniej jedna komorke.",
                    nameof(nextWorldCells));
            }

            worldCellSet = new HashSet<Vector2Int>(worldCells);

            wallEdges = nextWallEdges != null
                ? new List<DungeonCellEdge>(nextWallEdges)
                : new List<DungeonCellEdge>();

            wallTypes = new Dictionary<DungeonCellEdge, DungeonWallType>();
            foreach (var edge in wallEdges)
                if (nextWallTypes != null && nextWallTypes.TryGetValue(edge, out var type)) wallTypes[edge] = type;

            fullWallEdges = new HashSet<DungeonCellEdge>();
            foreach (var edge in wallEdges)
                if (GetWallType(edge) == DungeonWallType.Full) fullWallEdges.Add(edge);

            mapObjects = nextMapObjects != null
                ? new List<DungeonCombatMapObjectSnapshot>(nextMapObjects)
                : new List<DungeonCombatMapObjectSnapshot>();

            DifficultTerrainCells = new HashSet<Vector2Int>(difficultTerrainCells ?? Array.Empty<Vector2Int>());
            SurfaceMap = surfaceMap;
            IsFloorWide = isFloorWide;
            DungeonId = dungeonId;
            FloorIndex = floorIndex;
            EncounterInstanceId = encounterInstanceId;
            EncounterRoomId = encounterRoomId;
            DungeonLevel = Mathf.Clamp(dungeonLevel, 1, 50);
            PartyAnchorWorldPosition = partyAnchorWorldPosition;
            EnemyAnchorWorldPosition = enemyAnchorWorldPosition;
            WorldBounds = CalculateBounds(worldCells);
        }

        public bool ContainsWorldCell(Vector2Int cell)
        {
            return worldCellSet.Contains(cell);
        }

        private static RectInt CalculateBounds(
            IReadOnlyList<Vector2Int> cells)
        {
            var minX = cells[0].x;
            var minY = cells[0].y;
            var maxX = cells[0].x;
            var maxY = cells[0].y;

            foreach (var cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return new RectInt(
                minX,
                minY,
                maxX - minX + 1,
                maxY - minY + 1);
        }
    }

    public sealed class DungeonCombatMapObjectSnapshot
    {
        public ObstacleDungeonObjectInstance Obstacle { get; }
        public string ObjectInstanceId { get; }
        public DungeonObjectType ObjectType { get; }

        public Vector2Int WorldPosition { get; }
        public bool UsesEdge { get; }
        public DungeonCellEdge Edge { get; }

        public bool BlocksMovement { get; }
        public bool BlocksLineOfSight { get; }
        public bool IsOpen { get; }
        public DoorInteractionMode DoorInteractionMode { get; }

        public DungeonCombatMapObjectSnapshot(
            string objectInstanceId,
            DungeonObjectType objectType,
            Vector2Int worldPosition,
            bool blocksMovement,
            bool blocksLineOfSight, ObstacleDungeonObjectInstance obstacle = null)
        {
            ObjectInstanceId = objectInstanceId;
            Obstacle = obstacle;
            ObjectType = objectType;
            WorldPosition = worldPosition;
            BlocksMovement = blocksMovement;
            BlocksLineOfSight = blocksLineOfSight;
        }

        public DungeonCombatMapObjectSnapshot(
            string objectInstanceId,
            DungeonCellEdge edge,
            bool isOpen,
            DoorInteractionMode doorInteractionMode)
        {
            ObjectInstanceId = objectInstanceId;
            ObjectType = DungeonObjectType.Door;
            WorldPosition = edge.FirstCell;
            UsesEdge = true;
            Edge = edge;
            IsOpen = isOpen;
            DoorInteractionMode = doorInteractionMode;
            BlocksMovement = !isOpen;
            BlocksLineOfSight = !isOpen;
        }
    }
}
