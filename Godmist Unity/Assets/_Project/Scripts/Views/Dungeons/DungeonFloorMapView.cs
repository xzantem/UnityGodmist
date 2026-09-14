using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public partial class DungeonFloorMapView : MonoBehaviour
    {
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private TileBase floorTile;
        public float WallHalfThickness => wallSegmentPrefab != null ? wallSegmentPrefab.Thickness * .5f : 0;
        [SerializeField] private TileBase difficultTerrainTile;
        [SerializeField] private DungeonWallSegmentView wallSegmentPrefab;
        [SerializeField] private Transform wallsRoot;
        [SerializeField] private DungeonWallSegmentView lowWallPrefab;
        [SerializeField] private DungeonWallSegmentView railingPrefab;

        private readonly List<GameObject> spawnedWalls = new();
        private readonly List<(DungeonSurfaceEdge Edge, Renderer[] Renderers)> wallRenderers = new();
        private readonly DungeonElevationViewState elevation = new();
        public int PresentationRevision { get; private set; }
        public int ViewedLevel => elevation.Level;
        public void FocusElevation(DungeonTraversalPosition position, bool resumeFollowing = false)
        { if (elevation.Focus(position, resumeFollowing)) RefreshElevationPresentation(); }
        public bool IsTraversalSelectable(DungeonTraversalPosition position) => elevation.IsSelectable(position);
        public bool IsManualElevation => elevation.IsManual;

        public bool StepViewedLevel(int direction)
        {
            if (!elevation.Step(direction)) return false;
            RefreshElevationPresentation();
            return true;
        }

        public void FollowElevation(int level, bool resumeFollowing = false)
        {
            if (elevation.Follow(level, resumeFollowing)) RefreshElevationPresentation();
        }

        public bool TryGetVisibleSurface(Vector3 worldPosition, out DungeonSurfacePosition surface)
            => elevation.TryGetVisibleSurface(WorldToCell(worldPosition), out surface);

        public bool TryGetVisibleTraversal(Vector3 worldPosition, out DungeonTraversalPosition position)
            => elevation.TryGetVisiblePosition(WorldToCell(worldPosition), out position);
        public bool IsTraversalVisible(DungeonTraversalPosition position) => elevation.IsVisible(position);
        public bool IsSurfaceVisible(DungeonSurfacePosition surface) => elevation.IsVisible(surface);
        public bool IsSurfaceOccluded(DungeonSurfacePosition surface) => surface.Level > elevation.ProjectionLevel
            || (elevation.TryGetVisibleSurface(surface.Cell, out var covering) && covering.Level > surface.Level);

        private readonly Dictionary<Vector3Int, (TileBase Tile, Color Color)> presentedFloorCells = new();
        private readonly HashSet<Vector3Int> visibleFloorCells = new();
        private readonly List<Vector3Int> removedFloorCells = new();

        private void RefreshElevationPresentation()
        {
            if (floorTilemap == null || boundMap == null) return;
            PresentationRevision++;
            visibleFloorCells.Clear();
            foreach (var surface in elevation.VisibleSurfaces)
            {
                var cell = new Vector3Int(surface.Cell.x, surface.Cell.y, 0);
                visibleFloorCells.Add(cell);
                var difficult = surface.Level == 0 && boundMap.IsDifficultTerrain(surface.Cell);
                var tile = difficult && difficultTerrainTile != null ? difficultTerrainTile : floorTile;
                var color = difficult ? difficultTerrainColor : Color.white;
                if (presentedFloorCells.TryGetValue(cell, out var previous) && previous.Tile == tile && previous.Color == color) continue;
                floorTilemap.SetTile(cell, tile);
                floorTilemap.SetTileFlags(cell, TileFlags.None);
                floorTilemap.SetColor(cell, color);
                presentedFloorCells[cell] = (tile, color);
            }
            removedFloorCells.Clear();
            foreach (var cell in presentedFloorCells.Keys) if (!visibleFloorCells.Contains(cell)) removedFloorCells.Add(cell);
            foreach (var cell in removedFloorCells) { floorTilemap.SetTile(cell, null); presentedFloorCells.Remove(cell); }
            RefreshStairPresentation();
            RefreshCutawayPresentation();
            foreach (var wall in wallRenderers)
                foreach (var renderer in wall.Renderers)
                    if (renderer != null) renderer.forceRenderingOff = !elevation.IsVisible(wall.Edge);
        }

        private DungeonFloorMap boundMap;
        private Bounds worldBounds;
        private bool hasWorldBounds;

        [SerializeField] private Color difficultTerrainColor = new(.55f, .43f, .3f, 1f);

        public void Rebuild(DungeonFloorMap map)
        {
            if (!ReferenceEquals(boundMap, map))
            {
                if (boundMap != null)
                {
                    boundMap.ProgressionGateStateChanged
                        -= HandleProgressionGateStateChanged;
                }

                boundMap = map;

                if (boundMap != null)
                {
                    boundMap.ProgressionGateStateChanged
                        += HandleProgressionGateStateChanged;
                }
            }

            elevation.Bind(map);
            Clear();

            if (map == null
                || floorTilemap == null
                || floorTile == null
                || wallSegmentPrefab == null)
            {
                Debug.LogError(
                    "DungeonFloorMapView: brak mapy, Tilemapy, Tile lub prefabu sciany.");

                return;
            }

            foreach (var edge in map.SurfaceWallEdges)
                SpawnWallSegment(edge, map.GetWallType(edge));
            foreach (var edge in map.SurfaceClosedProgressionGateEdges)
                SpawnWallSegment(edge);
            BuildStairPresentation();
            BuildLedgePresentation();
            RefreshElevationPresentation();

            RebuildWorldBounds(map);
        }

        private void OnDestroy()
        {
            ReleaseStairSprite();
            if (boundMap != null)
            {
                boundMap.ProgressionGateStateChanged
                    -= HandleProgressionGateStateChanged;
            }
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = worldBounds;
            return hasWorldBounds;
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            if (floorTilemap == null)
            {
                return Vector2Int.zero;
            }

            var cell = floorTilemap.WorldToCell(worldPosition);

            return new Vector2Int(cell.x, cell.y);
        }

        public Vector2 CellToWorldCenter(Vector2Int cell)
        {
            if (floorTilemap == null)
            {
                return cell;
            }

            return floorTilemap.GetCellCenterWorld(
                new Vector3Int(cell.x, cell.y, 0));
        }

        private void Clear()
        {
            ClearStairPresentation();
            if (floorTilemap != null)
            {
                floorTilemap.ClearAllTiles();
                presentedFloorCells.Clear();
            }

            foreach (var spawnedWall in spawnedWalls)
            {
                if (spawnedWall != null)
                {
                    Destroy(spawnedWall);
                }
            }

            spawnedWalls.Clear();
            wallRenderers.Clear();
            hasWorldBounds = false;
        }

        private void SpawnWallSegment(DungeonSurfaceEdge edge, DungeonWallType type = DungeonWallType.Full)
        {
            var parent = wallsRoot != null ? wallsRoot : transform;

            var prefab = type == DungeonWallType.Low ? lowWallPrefab
                : type == DungeonWallType.Railing ? railingPrefab : wallSegmentPrefab;
            if (prefab == null) { Debug.LogError("Missing wall prefab: " + type); return; }
            var wallSegment = Instantiate(
                prefab,
                parent);

            // Presentation focus never toggles physics: existing ground movement keeps its ground colliders.
            wallSegment.Bind(edge.CellEdge, floorTilemap, edge.Level == 0 && (type != DungeonWallType.Low
                || !boundMap.IsWalkable(edge.First) || !boundMap.IsWalkable(edge.Second)));
            wallRenderers.Add((edge, wallSegment.GetComponentsInChildren<Renderer>(true)));
            spawnedWalls.Add(wallSegment.gameObject);
        }

        private void HandleProgressionGateStateChanged()
        {
            if (boundMap != null)
            {
                Rebuild(boundMap);
            }
        }

        private void RebuildWorldBounds(DungeonFloorMap map)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            void Include(Vector2Int cell)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            foreach (var surface in map.Surfaces) Include(surface.Cell);
            foreach (var stair in map.Stairs)
                if (map.TryGetStairPath(stair, out var path))
                    foreach (var cell in path) Include(cell);

            if (minX == int.MaxValue)
            {
                return;
            }

            var minimum = floorTilemap.CellToWorld(
                new Vector3Int(minX, minY, 0));

            var maximum = floorTilemap.CellToWorld(
                new Vector3Int(maxX + 1, maxY + 1, 0));

            worldBounds.SetMinMax(minimum, maximum);
            hasWorldBounds = true;
        }
    }
}
