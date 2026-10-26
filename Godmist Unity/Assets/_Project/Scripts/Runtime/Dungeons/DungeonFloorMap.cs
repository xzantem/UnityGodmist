using System;
using System.Linq;
using Godmist.Core.Enums;
using System.Collections.Generic;
using UnityEngine;

namespace Godmist.Runtime.Dungeons
{
    public partial class DungeonFloorMap
    {
        // Cached level-zero projection for existing 2D consumers; room ownership is surface-keyed.
        private readonly HashSet<Vector2Int> floorCells = new();
        private readonly Dictionary<DungeonSurfacePosition, string> surfaceRooms = new();
        private bool hasElevatedSurfaces;
        private readonly HashSet<DungeonStairConnection> stairs = new();

        private readonly Dictionary<DungeonSurfaceEdge, DungeonWallType> wallTypes = new();
        private readonly HashSet<Vector2Int> difficultTerrain = new();
        public IReadOnlyCollection<Vector2Int> DifficultTerrainCells => difficultTerrain;
        public bool IsDifficultTerrain(Vector2Int cell) => difficultTerrain.Contains(cell);
        public void SetDifficultTerrain(Vector2Int cell)
        {
            if (!IsWalkable(cell)) throw new ArgumentException("Terrain must be on a floor cell.");
            difficultTerrain.Add(cell);
        }
        private IReadOnlyDictionary<DungeonCellEdge, DungeonWallType> groundWallTypes;
        private IReadOnlyCollection<DungeonCellEdge> groundWallEdges;
        private IReadOnlyCollection<DungeonCellEdge> groundClosedGates;
        public IReadOnlyDictionary<DungeonCellEdge, DungeonWallType> WallTypes => groundWallTypes ??= wallTypes
            .Where(p => p.Key.Level == 0).ToDictionary(p => p.Key.CellEdge, p => p.Value);
        public IReadOnlyDictionary<DungeonSurfaceEdge, DungeonWallType> SurfaceWallTypes => wallTypes;
        public IEnumerable<DungeonSurfaceEdge> SurfaceWallEdges => wallEdges;
        public IEnumerable<DungeonSurfaceEdge> SurfaceClosedProgressionGateEdges => closedProgressionGateEdges;
        public IEnumerable<DungeonSurfaceEdge> SurfaceClosedDoorEdges => closedDoorEdges;
        public DungeonWallType GetWallType(DungeonCellEdge edge) => GetWallType(new DungeonSurfaceEdge(edge));
        public DungeonWallType GetWallType(DungeonSurfaceEdge edge) => wallTypes.TryGetValue(edge, out var type) ? type : DungeonWallType.Full;
        public bool SetWallType(DungeonCellEdge edge, DungeonWallType type) => SetWallType(new DungeonSurfaceEdge(edge), type);
        public bool SetWallType(DungeonSurfaceEdge edge, DungeonWallType type)
        {
            if ((!IsWalkable(edge.First) && !IsWalkable(edge.Second)) || !Enum.IsDefined(typeof(DungeonWallType), type)) return false;
            var changed = !wallTypes.TryGetValue(edge, out var previousType) || previousType != type || !wallEdges.Contains(edge);
            wallEdges.Add(edge);
            wallTypes[edge] = type;
            if (edge.Level == 0) { groundWallEdges = null; groundWallTypes = null; }
            if (changed) { TraversalRevision++; StaticGeometryRevision++; }
            return true;
        }
        public bool TryAddWall(DungeonSurfacePosition first, DungeonSurfacePosition second)
        {
            if (first.Level != second.Level || !IsWalkable(first)) return false;
            var added = wallEdges.Add(new DungeonSurfaceEdge(first, second));
            if (added && first.Level == 0) groundWallEdges = null;
            if (added) { TraversalRevision++; StaticGeometryRevision++; }
            return added;
        }

        private readonly HashSet<DungeonSurfaceEdge> wallEdges = new();
        private readonly HashSet<DungeonSurfaceEdge> closedDoorEdges = new();
        private readonly HashSet<DungeonSurfaceEdge> closedProgressionGateEdges = new();
        public int TraversalRevision { get; private set; }
        // Doors and progression gates change traversal, not the authored floor/walls.
        // Presentation/navigation caches can retain that immutable geometry.
        public int StaticGeometryRevision { get; private set; }

        public IReadOnlyCollection<Vector2Int> FloorCells => floorCells;
        private readonly HashSet<Vector2Int> visualPathCells = new();
        public IReadOnlyCollection<Vector2Int> VisualPathCells => visualPathCells;
        public void AddVisualPathCell(Vector2Int cell)
        {
            if (!IsWalkable(cell)) throw new ArgumentException("Visual path must stay on the floor.");
            visualPathCells.Add(cell);
        }
        public IReadOnlyCollection<DungeonCellEdge> WallEdges => groundWallEdges ??= wallEdges.Where(e => e.Level == 0).Select(e => e.CellEdge).ToArray();
        public IReadOnlyCollection<DungeonCellEdge> ClosedProgressionGateEdges =>
            groundClosedGates ??= closedProgressionGateEdges.Where(e => e.Level == 0).Select(e => e.CellEdge).ToArray();

        public event Action ProgressionGateStateChanged;

        public bool TryAddFloorCell(
            Vector2Int cell,
            string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)
                || floorCells.Contains(cell))
            {
                return false;
            }

            floorCells.Add(cell);
            surfaceRooms.Add(new DungeonSurfacePosition(cell), roomId);
            StaticGeometryRevision++;
            TraversalRevision++;

            return true;
        }

        public bool TryAddWall(Vector2Int firstCell, Vector2Int secondCell)
            => TryAddWall(new DungeonSurfacePosition(firstCell), new DungeonSurfacePosition(secondCell));

        public bool IsWalkable(Vector2Int cell)
        {
            return floorCells.Contains(cell);
        }

        public bool TryGetRoomId(
            Vector2Int cell,
            out string roomId)
        {
            return surfaceRooms.TryGetValue(new DungeonSurfacePosition(cell), out roomId);
        }

        public bool HasWallBetween(
            Vector2Int firstCell,
            Vector2Int secondCell)
        {
            var delta = secondCell - firstCell;

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                return false;
            }

            return wallEdges.Contains(new DungeonSurfaceEdge(new DungeonCellEdge(firstCell, secondCell)));
        }

        public bool TrySetDoorState(DungeonCellEdge edge, bool isOpen)
            => TrySetDoorState(new DungeonSurfaceEdge(edge), isOpen);

        public bool TrySetDoorState(DungeonSurfaceEdge edge, bool isOpen)
        {
            if (!IsWalkable(edge.First)
                || !IsWalkable(edge.Second))
            {
                return false;
            }

            if (isOpen)
            {
                if (closedDoorEdges.Remove(edge)) TraversalRevision++;
                return true;
            }

            if (closedDoorEdges.Add(edge)) TraversalRevision++;
            return true;
        }

        public bool TrySetProgressionGateState(DungeonCellEdge edge, bool isOpen)
            => TrySetProgressionGateState(new DungeonSurfaceEdge(edge), isOpen);

        public bool TrySetProgressionGateState(IEnumerable<DungeonSurfaceEdge> edges, bool isOpen)
        {
            if(edges==null)return false;
            var batch=edges.ToArray();
            if(batch.Any(edge=>!IsWalkable(edge.First)||!IsWalkable(edge.Second)))return false;
            var changed=false;
            foreach(var edge in batch)changed|=isOpen?closedProgressionGateEdges.Remove(edge):closedProgressionGateEdges.Add(edge);
            if(changed)
            {
                if(batch.Any(edge=>edge.Level==0))groundClosedGates=null;
                TraversalRevision++;ProgressionGateStateChanged?.Invoke();
            }
            return true;
        }

        public bool TrySetProgressionGateState(DungeonSurfaceEdge edge, bool isOpen)
        {
            if (!IsWalkable(edge.First)
                || !IsWalkable(edge.Second))
            {
                return false;
            }

            if (isOpen)
            {
                if (closedProgressionGateEdges.Remove(edge))
                {
                    if (edge.Level == 0) groundClosedGates = null;
                    TraversalRevision++;
                    ProgressionGateStateChanged?.Invoke();
                }

                return true;
            }

            if (closedProgressionGateEdges.Add(edge))
            {
                if (edge.Level == 0) groundClosedGates = null;
                TraversalRevision++;
                ProgressionGateStateChanged?.Invoke();
            }

            return true;
        }

        public bool CanMoveBetween(
            Vector2Int firstCell,
            Vector2Int secondCell,
            bool ignoreClosedDoorEdges = false)
        {
            if (!IsWalkable(firstCell)
                || !IsWalkable(secondCell))
            {
                return false;
            }

            var delta = secondCell - firstCell;

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            {
                return false;
            }

            var edge = new DungeonSurfaceEdge(new DungeonCellEdge(firstCell, secondCell));

            return (!HasWallBetween(firstCell, secondCell)
                    || GetWallType(new DungeonCellEdge(firstCell, secondCell)) is DungeonWallType.Low or DungeonWallType.TreeTrunk)
                && (ignoreClosedDoorEdges
                    || (!closedDoorEdges.Contains(edge)
                        && !closedProgressionGateEdges.Contains(edge)));
        }
        // Level-zero API above remains compatible; surfaces are owned by this same map.
        public IEnumerable<DungeonSurfacePosition> Surfaces => surfaceRooms.Keys;
        public IEnumerable<DungeonStairConnection> Stairs => stairs;
        // Topology only grows through TryAddFloorCell. Position/visibility queries
        // ask this thousands of times per tick: never scan the entire floor.
        public bool HasElevatedSurfaces => hasElevatedSurfaces;
        public bool HasAssetAuditDagger { get; private set; }
        public Vector2Int AssetAuditDaggerCell { get; private set; }

        public void SetAssetAuditDaggerCell(Vector2Int cell)
        {
            if (!IsWalkable(cell))
                throw new ArgumentException("Asset-audit dagger must be placed on a floor cell.", nameof(cell));
            AssetAuditDaggerCell = cell;
            HasAssetAuditDagger = true;
        }

        public bool TryAddFloorCell(DungeonSurfacePosition position, string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId) || surfaceRooms.ContainsKey(position)) return false;
            if (position.Level == 0) floorCells.Add(position.Cell);
            surfaceRooms.Add(position, roomId);
            hasElevatedSurfaces |= position.Level != 0;
            StaticGeometryRevision++;
            TraversalRevision++;
            return true;
        }
        public bool IsWalkable(DungeonSurfacePosition position) => surfaceRooms.ContainsKey(position);
        public bool TryGetRoomId(DungeonSurfacePosition position, out string roomId)
            => surfaceRooms.TryGetValue(position, out roomId);

        public bool TryAddStairs(DungeonSurfacePosition first, DungeonSurfacePosition second)
            => TryAddStairs(first, second, null);

        /// <summary>Topology only. Unit movement integration follows separately.</summary>
        public IEnumerable<DungeonSurfacePosition> GetStairDestinations(DungeonSurfacePosition position)
        {
            foreach (var stair in stairs)
            {
                if (stair.First == position) yield return stair.Second;
                else if (stair.Second == position) yield return stair.First;
            }
        }
    }
}
