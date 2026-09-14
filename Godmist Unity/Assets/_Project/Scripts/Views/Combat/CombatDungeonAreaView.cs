using System.Collections.Generic;
using Godmist.Core.Enums;
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
        [SerializeField] private CombatWallSegmentView lowWallPrefab;
        [SerializeField] private CombatWallSegmentView railingPrefab;

        [Header("Objects")]
        [SerializeField] private SpriteRenderer blockingObjectPrefab;
        [SerializeField] private CombatDoorView doorPrefab;
        [SerializeField] private Transform objectsRoot;

        private readonly List<GameObject> spawnedWalls = new();
        private readonly List<GameObject> spawnedObjects = new();

        private Bounds worldBounds;
        private bool hasWorldBounds;

        public void Rebuild(
            DungeonCombatContext context,
            CombatSandboxBootstrap sandboxBootstrap)
        {
            ClearArea();

            if (context == null
                || sandboxBootstrap == null
                || floorTilemap == null
                || floorTile == null
                || wallSegmentPrefab == null)
            {
                Debug.LogError(
                    "CombatDungeonAreaView: brak kontekstu, bootstrapu, Tilemapy, Tile lub prefabu sciany.");

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

                var type = context.GetWallType(worldEdge);
                var prefab = type == DungeonWallType.Low ? lowWallPrefab
                    : type == DungeonWallType.Railing ? railingPrefab : wallSegmentPrefab;
                if (prefab == null) { Debug.LogError("Missing wall prefab: " + type); continue; }
                var wallSegment = Instantiate(
                    prefab,
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

            SpawnMapObjects(context, sandboxBootstrap);
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

        private void SpawnMapObjects(
            DungeonCombatContext context,
            CombatSandboxBootstrap sandboxBootstrap)
        {
            var objectParent = objectsRoot != null
                ? objectsRoot
                : transform;

            foreach (var mapObject in context.MapObjects)
            {
                if (mapObject.ObjectType == DungeonObjectType.Obstacle)
                {
                    SpawnObstacle(context, mapObject, objectParent);
                    continue;
                }

                if (mapObject.ObjectType == DungeonObjectType.Door
                    && mapObject.UsesEdge)
                {
                    SpawnDoor(
                        context,
                        sandboxBootstrap,
                        mapObject,
                        objectParent);
                }
            }
        }

        private void SpawnObstacle(
            DungeonCombatContext context,
            DungeonCombatMapObjectSnapshot mapObject,
            Transform objectParent)
        {
            if (blockingObjectPrefab == null || !mapObject.BlocksMovement)
            {
                return;
            }

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

        private void SpawnDoor(
            DungeonCombatContext context,
            CombatSandboxBootstrap sandboxBootstrap,
            DungeonCombatMapObjectSnapshot snapshot,
            Transform objectParent)
        {
            if (doorPrefab == null)
            {
                Debug.LogError(
                    "CombatDungeonAreaView: brak prefabu CombatDoorView.");

                return;
            }

            var door = sandboxBootstrap.GetCombatMapObject(
                snapshot.ObjectInstanceId) as DoorCombatMapObject;

            if (door == null)
            {
                Debug.LogError(
                    $"CombatDungeonAreaView: brak runtime drzwi '{snapshot.ObjectInstanceId}'.");

                return;
            }

            var view = Instantiate(
                doorPrefab,
                objectParent);

            view.name = $"CombatDoor_{door.ObjectId}";
            view.Initialize(
                sandboxBootstrap,
                door,
                floorTilemap);

            spawnedObjects.Add(view.gameObject);
        }

        private void RebuildWorldBounds(DungeonCombatContext context)
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