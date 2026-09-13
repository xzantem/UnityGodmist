using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonFloorMapView : MonoBehaviour
    {
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private TileBase floorTile;
        [SerializeField] private DungeonWallSegmentView wallSegmentPrefab;
        [SerializeField] private Transform wallsRoot;
        [SerializeField] private DungeonWallSegmentView lowWallPrefab;
        [SerializeField] private DungeonWallSegmentView railingPrefab;

        private readonly List<GameObject> spawnedWalls = new();

        private DungeonFloorMap boundMap;
        private Bounds worldBounds;
        private bool hasWorldBounds;

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

            foreach (var cell in map.FloorCells)
            {
                floorTilemap.SetTile(
                    new Vector3Int(cell.x, cell.y, 0),
                    floorTile);
            }

            foreach (var wallEdge in map.WallEdges)
            {
                SpawnWallSegment(wallEdge, map.GetWallType(wallEdge));
            }

            foreach (var gateEdge in map.ClosedProgressionGateEdges)
            {
                SpawnWallSegment(gateEdge);
            }

            RebuildWorldBounds(map);
        }

        private void OnDestroy()
        {
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
            if (floorTilemap != null)
            {
                floorTilemap.ClearAllTiles();
            }

            foreach (var spawnedWall in spawnedWalls)
            {
                if (spawnedWall != null)
                {
                    Destroy(spawnedWall);
                }
            }

            spawnedWalls.Clear();
            hasWorldBounds = false;
        }

        private void SpawnWallSegment(DungeonCellEdge edge, DungeonWallType type = DungeonWallType.Full)
        {
            var parent = wallsRoot != null ? wallsRoot : transform;

            var prefab = type == DungeonWallType.Low ? lowWallPrefab
                : type == DungeonWallType.Railing ? railingPrefab : wallSegmentPrefab;
            if (prefab == null) { Debug.LogError("Missing wall prefab: " + type); return; }
            var wallSegment = Instantiate(
                prefab,
                parent);

            wallSegment.Bind(edge, floorTilemap, type != DungeonWallType.Low
                || !boundMap.IsWalkable(edge.FirstCell) || !boundMap.IsWalkable(edge.SecondCell));
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

            foreach (var cell in map.FloorCells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

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
