using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Godmist.Views.Dungeons
{
    public class DungeonObjectSpawner : MonoBehaviour
    {
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private DungeonChestView chestPrefab;
        [SerializeField] private DungeonEnemyBodyView enemyBodyPrefab;
        [SerializeField] private DungeonLootWindowView dungeonLootWindowPrefab;
        [SerializeField] private DungeonObstacleView obstaclePrefab;
        [SerializeField] private DungeonDoorView doorPrefab;
        [SerializeField] private DungeonSpikeTrapView spikeTrapPrefab;
        [SerializeField] private DungeonLeverView leverPrefab;
        [SerializeField] private DungeonConditionView conditionPrefab;
        [SerializeField] private Transform objectsRoot;

        private readonly List<DungeonObjectView> spawnedObjects = new();
        private readonly List<GameObject> spawnedEnemyBodies = new();

        public bool Rebuild(
            DungeonFloorInstance floor,
            ExplorationPartyController partyController)
        {
            ClearSpawnedObjects();

            if (floor == null || partyController == null || floorTilemap == null)
            {
                Debug.LogError(
                    "DungeonObjectSpawner: brak pietra, PartyController lub Tilemapy.");

                return false;
            }

            foreach (var dungeonObject in floor.Objects)
            {
                if (dungeonObject == null || !dungeonObject.IsActive)
                {
                    continue;
                }

                var parent = objectsRoot != null ? objectsRoot : transform;

                if (dungeonObject is DoorDungeonObjectInstance door)
                {
                    if (doorPrefab == null)
                    {
                        Debug.LogError(
                            "DungeonObjectSpawner: brak prefabu DungeonDoorView.");

                        return false;
                    }

                    var doorView = Instantiate(doorPrefab, parent);

                    doorView.name = $"Door_{door.InstanceId}";
                    doorView.Initialize(
                        partyController,
                        floor,
                        door,
                        floorTilemap);

                    spawnedObjects.Add(doorView);
                    continue;
                }

                var worldPosition = floorTilemap.GetCellCenterWorld(
                    new Vector3Int(
                        dungeonObject.Position.x,
                        dungeonObject.Position.y,
                        0));

                if (dungeonObject is ChestDungeonObjectInstance chest)
                {
                    if (chestPrefab == null)
                    {
                        Debug.LogError(
                            "DungeonObjectSpawner: brak prefabu DungeonChestView.");

                        return false;
                    }

                    var chestView = Instantiate(chestPrefab, parent);

                    chestView.name = $"Chest_{chest.InstanceId}";
                    chestView.Initialize(
                        partyController,
                        floor,
                        chest,
                        worldPosition,
                        floorTilemap);

                    spawnedObjects.Add(chestView);
                    continue;
                }

                if (dungeonObject is ObstacleDungeonObjectInstance obstacle)
                {
                    if (obstaclePrefab == null)
                    {
                        Debug.LogError(
                            "DungeonObjectSpawner: brak prefabu DungeonObstacleView.");

                        return false;
                    }

                    var obstacleView = Instantiate(obstaclePrefab, parent);

                    obstacleView.name = $"Obstacle_{obstacle.InstanceId}";
                    obstacleView.Initialize(
                        obstacle,
                        worldPosition);

                    spawnedObjects.Add(obstacleView);
                    continue;
                }

                if (dungeonObject is SpikeTrapDungeonObjectInstance spikeTrap)
                {
                    var spikeTrapView = spikeTrapPrefab != null
                        ? Instantiate(spikeTrapPrefab, parent)
                        : DungeonObjectView.CreateFallback<DungeonSpikeTrapView>(
                            parent,
                            $"SpikeTrap_{spikeTrap.InstanceId}");

                    spikeTrapView.name = $"SpikeTrap_{spikeTrap.InstanceId}";
                    spikeTrapView.Initialize(
                        partyController,
                        floor,
                        spikeTrap,
                        floorTilemap,
                        worldPosition);

                    spawnedObjects.Add(spikeTrapView);
                    continue;
                }

                if (dungeonObject is LeverDungeonObjectInstance lever)
                {
                    var leverView = leverPrefab != null
                        ? Instantiate(leverPrefab, parent)
                        : DungeonObjectView.CreateFallback<DungeonLeverView>(
                            parent,
                            $"Lever_{lever.InstanceId}");

                    leverView.name = $"Lever_{lever.InstanceId}";
                    leverView.Initialize(
                        partyController,
                        floor,
                        lever,
                        worldPosition,
                        floorTilemap);

                    spawnedObjects.Add(leverView);
                    continue;
                }

                if (dungeonObject is ConditionDungeonObjectInstance condition)
                {
                    var conditionView = conditionPrefab != null
                        ? Instantiate(conditionPrefab, parent)
                        : DungeonObjectView.CreateFallback<DungeonConditionView>(
                            parent,
                            $"Condition_{condition.InstanceId}");

                    conditionView.name = $"Condition_{condition.InstanceId}";
                    conditionView.Initialize(
                        partyController,
                        floor,
                        condition,
                        worldPosition,
                        floorTilemap);

                    spawnedObjects.Add(conditionView);
                }
            }

            foreach (var body in floor.EnemyBodies)
            {
                if (body == null)
                {
                    continue;
                }

                var worldPosition = floorTilemap.GetCellCenterWorld(
                    new Vector3Int(
                        body.Position.x,
                        body.Position.y,
                        0));

                var parent = objectsRoot != null ? objectsRoot : transform;
                var bodyView = enemyBodyPrefab != null
                    ? Instantiate(enemyBodyPrefab, parent)
                    : DungeonEnemyBodyView.CreateFallback(parent);

                bodyView.name = $"EnemyBody_{body.InstanceId}";
                bodyView.Initialize(
                    partyController,
                    floor,
                    body,
                    worldPosition,
                    dungeonLootWindowPrefab,
                    floorTilemap);

                spawnedEnemyBodies.Add(bodyView.gameObject);
            }

            return true;
        }

        private void OnDestroy()
        {
            ClearSpawnedObjects();
        }

        private void ClearSpawnedObjects()
        {
            foreach (var objectView in spawnedObjects)
            {
                if (objectView != null)
                {
                    Destroy(objectView.gameObject);
                }
            }

            spawnedObjects.Clear();

            foreach (var bodyObject in spawnedEnemyBodies)
            {
                if (bodyObject != null)
                {
                    Destroy(bodyObject);
                }
            }

            spawnedEnemyBodies.Clear();
        }
    }
}
