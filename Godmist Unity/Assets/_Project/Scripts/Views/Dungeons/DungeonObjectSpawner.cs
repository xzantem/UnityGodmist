using System;
using System.Collections.Generic;
using Godmist.Data.Dungeons;
using Godmist.Core.Enums;
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
        [SerializeField] private List<ObstaclePrefabBinding> obstaclePrefabs = new();

        [Serializable]
        public sealed class ObstaclePrefabBinding
        {
            public ObstacleDungeonObjectDefinition definition;
            public DungeonObstacleView prefab;
        }

        public bool ValidateObstaclePrefabs(out string error)
        {
            var definitions = new HashSet<ObstacleDungeonObjectDefinition>();
            foreach (var binding in obstaclePrefabs)
            {
                if (binding == null || binding.definition == null || binding.prefab == null)
                {
                    error = "Przeszkoda wymaga definicji i prefabu.";
                    return false;
                }
                if (!definitions.Add(binding.definition))
                {
                    error = "Powtórzone przypisanie prefabu przeszkody: " + binding.definition.name;
                    return false;
                }
                if (!binding.prefab.HasBlockingCollider && binding.definition.BlocksMovement)
                {
                    error = "Przeszkoda blokująca ruch wymaga podpiętego collidera: " + binding.definition.name;
                    return false;
                }
            }
            error = null;
            return true;
        }

        public DungeonObstacleView ResolveObstaclePrefab(ObstacleDungeonObjectDefinition definition)
        {
            foreach (var binding in obstaclePrefabs)
                if (binding != null && binding.definition == definition && definition != null)
                    return binding.prefab;
            return obstaclePrefab;
        }

        [SerializeField] private DungeonDoorView doorPrefab;
        [SerializeField] private DungeonSpikeTrapView spikeTrapPrefab;
        [SerializeField] private List<TrapPrefabBinding> trapPrefabs = new();
        [Serializable]
        public sealed class TrapPrefabBinding
        {
            public SpikeTrapDungeonObjectDefinition definition;
            public DungeonTheme theme;
            public DungeonSpikeTrapView prefab;
        }
        public DungeonSpikeTrapView ResolveTrapPrefab(SpikeTrapDungeonObjectDefinition definition, DungeonTheme theme)
        {
            foreach(var binding in trapPrefabs)
                if(binding != null && binding.definition == definition && binding.theme == theme) return binding.prefab;
            return spikeTrapPrefab;
        }

        [SerializeField] private DungeonLeverView leverPrefab;
        [SerializeField] private DungeonConditionView conditionPrefab;
        [SerializeField] private Transform objectsRoot;

        private readonly List<DungeonObjectView> spawnedObjects = new();
        private readonly List<GameObject> spawnedEnemyBodies = new();
        private readonly HashSet<string> spawnedBodyIds = new();
        public void RefreshDoorViews()
        {
            foreach (var view in spawnedObjects)
                if (view is DungeonDoorView door) door.RefreshState();
        }

        public bool Rebuild(
            DungeonFloorInstance floor,
            ExplorationPartyController partyController, DungeonTheme theme = DungeonTheme.Story)
        {
            if (!ValidateObstaclePrefabs(out var obstacleError))
            {
                Debug.LogError("DungeonObjectSpawner: " + obstacleError);
                return false;
            }
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
                        floorTilemap,
                        dungeonLootWindowPrefab);

                    spawnedObjects.Add(chestView);
                    continue;
                }

                if (dungeonObject is ObstacleDungeonObjectInstance obstacle)
                {
                    var selectedPrefab = ResolveObstaclePrefab(obstacle.Definition as ObstacleDungeonObjectDefinition);
                    if (selectedPrefab == null)
                    {
                        Debug.LogError(
                            "DungeonObjectSpawner: brak prefabu DungeonObstacleView.");

                        return false;
                    }

                    var obstacleView = Instantiate(selectedPrefab, parent);

                    obstacleView.name = $"Obstacle_{obstacle.InstanceId}";
                    obstacleView.Initialize(
                        obstacle,
                        worldPosition);

                    spawnedObjects.Add(obstacleView);
                    continue;
                }

                if (dungeonObject is SpikeTrapDungeonObjectInstance spikeTrap)
                {
                    var trapPrefab = ResolveTrapPrefab(spikeTrap.Definition as SpikeTrapDungeonObjectDefinition, theme);
                    if (trapPrefab == null)
                    {
                        Debug.LogError("DungeonObjectSpawner: brak prefabu pułapki.");
                        return false;
                    }
                    var spikeTrapView = Instantiate(trapPrefab, parent);

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

            RefreshBodies(floor, partyController);
            return true;
        }

        public void RefreshBodies(DungeonFloorInstance floor, ExplorationPartyController partyController)
        {
            foreach (var body in floor.EnemyBodies)
            {
                if (body == null || spawnedBodyIds.Contains(body.InstanceId))
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
                spawnedBodyIds.Add(body.InstanceId);
            }


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
            spawnedBodyIds.Clear();
        }
    }
}
