using System.Collections.Generic;
using Godmist.Runtime.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Combat
{
    public class CombatDungeonAreaView : MonoBehaviour
    {
        [Header("Floor")]
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private TileBase floorTile;

        [Header("Walls")]
        [SerializeField] private CombatWallSegmentView wallSegmentPrefab;
        [SerializeField] private Transform wallsRoot;

        [Header("Blocking Objects")]
        [SerializeField] private SpriteRenderer blockingObjectPrefab;
        [SerializeField] private Transform objectsRoot;

        private readonly List<GameObject> spawnedWalls = new();
        private readonly List<GameObject> spawnedObjects = new();

        private Bounds worldBounds;
        private bool hasWorldBounds;

        public void Rebuild(DungeonCombatContext context)
        {
            ClearArea();

            if (context == null
                || floorTilemap == null
                || floorTile == null
                || wallSegmentPrefab == null)
            {
                Debug.LogError(
                    "CombatDungeonAreaView: brak kontekstu, Tilemapy, Tile lub prefabu sciany.");

                return;
            }

            foreach (var worldCell in context.WorldCells)
            {
                var localPosition = ToBattlePosition(
                    context,
                    worldCell);

                floorTilemap.SetTile(
                    new Vector3Int(
                        localPosition.X,
                        localPosition.Y,
                        0),
                    floorTile);
            }

            foreach (var worldEdge in context.WallEdges)
            {
                var wallParent = wallsRoot != null
                    ? wallsRoot
                    : transform;

                var wallSegment = Instantiate(
                    wallSegmentPrefab,
                    wallParent);

                wallSegment.Bind(
                    new BattleCellEdge(
                        ToBattlePosition(
                            context,
                            worldEdge.FirstCell),
                        ToBattlePosition(
                            context,
                            worldEdge.SecondCell)),
                    floorTilemap);

                spawnedWalls.Add(wallSegment.gameObject);
            }

            SpawnBlockingObjects(context);
            RebuildWorldBounds(context);
        }

        public void ClearArea()
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

            foreach (var spawnedObject in spawnedObjects)
            {
                if (spawnedObject != null)
                {
                    Destroy(spawnedObject);
                }
            }

            spawnedWalls.Clear();
            spawnedObjects.Clear();
            hasWorldBounds = false;
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = worldBounds;
            return hasWorldBounds;
        }

        private void SpawnBlockingObjects(
            DungeonCombatContext context)
        {
            if (blockingObjectPrefab == null) return;

            var objectParent = objectsRoot != null
                ? objectsRoot
                : transform;

            foreach (var mapObject in context.MapObjects)
            {
                if (!mapObject.BlocksMovement) continue;

                var localPosition = ToBattlePosition(
                    context,
                    mapObject.WorldPosition);

                var view = Instantiate(
                    blockingObjectPrefab,
                    objectParent);

                view.transform.position = new Vector3(
                    localPosition.X,
                    localPosition.Y,
                    -0.1f);

                spawnedObjects.Add(view.gameObject);
            }
        }

        private void RebuildWorldBounds(
            DungeonCombatContext context)
        {
            var bounds = context.WorldBounds;

            var minimum = floorTilemap.CellToWorld(
                Vector3Int.zero);

            var maximum = floorTilemap.CellToWorld(
                new Vector3Int(
                    bounds.width,
                    bounds.height,
                    0));

            worldBounds.SetMinMax(minimum, maximum);
            hasWorldBounds = true;
        }

        private static GridPosition ToBattlePosition(
            DungeonCombatContext context,
            Vector2Int worldPosition)
        {
            return new GridPosition(
                worldPosition.x - context.WorldBounds.xMin,
                worldPosition.y - context.WorldBounds.yMin);
        }
    }
}