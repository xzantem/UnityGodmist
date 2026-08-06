using System.Collections.Generic;
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

        private readonly List<GameObject> spawnedWalls = new();

        private Bounds worldBounds;
        private bool hasWorldBounds;

        public void Rebuild(DungeonFloorMap map)
        {
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
                var parent = wallsRoot != null ? wallsRoot : transform;

                var wallSegment = Instantiate(
                    wallSegmentPrefab,
                    parent);

                wallSegment.Bind(wallEdge, floorTilemap);
                spawnedWalls.Add(wallSegment.gameObject);
            }

            RebuildWorldBounds(map);
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = worldBounds;
            return hasWorldBounds;
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