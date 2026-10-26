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
        public IReadOnlyList<DungeonObjectView> SpawnedObjects => spawnedObjects;
        private readonly List<GameObject> spawnedEnemyBodies = new();
        private readonly HashSet<string> spawnedBodyIds = new();
        private bool catacombsAuditPresentationLogged;
        private DungeonFloorMapView elevationView;
        public void BindElevationView(DungeonFloorMapView view)
        {
            elevationView = view;
            // Register every prop/door lane before binding any sorting group.
            // Rebuilding all wall joins once per newly encountered baseline
            // made large floors pay the same setup cost dozens of times.
            // Native masonry still owns its immediate corner-overlay refresh.
            // Forest has no such overlays and can reserve all lanes up front.
            if (view?.UsesVegetationPresentation == true) view.ReserveObjectDepthBaselines(spawnedObjects);
            foreach (var item in spawnedObjects) item.BindElevationView(view);
            foreach (var item in spawnedEnemyBodies) if (item != null) item.GetComponent<DungeonEnemyBodyView>()?.BindElevationView(view);
        }
        public void RefreshDoorViews()
        {
            foreach (var view in spawnedObjects)
                if (view is DungeonDoorView door) door.RefreshState();
        }

        public bool Rebuild(
            DungeonFloorInstance floor,
            ExplorationPartyController partyController,
            DungeonTheme theme = DungeonTheme.Story,
            string dungeonId = null)
        {
            var useCatacombsVisuals = theme == DungeonTheme.Catacombs;
            var useForestVisuals = theme == DungeonTheme.Forest;
            if (!useCatacombsVisuals) catacombsAuditPresentationLogged = false;
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
                if (dungeonObject == null || (!dungeonObject.IsActive
                    && (!floor.Fog.Objects.TryGetValue(dungeonObject.InstanceId, out var known) || !known.Active)))
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

                    doorView.BindElevationView(elevationView);

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

                    chestView.BindElevationView(elevationView);

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
                    if (useCatacombsVisuals)
                    {
                        DungeonCatacombsObjectVisuals.TryApplyObstacle(
                            obstacleView,
                            obstacle.Definition as ObstacleDungeonObjectDefinition);
                    }
                    obstacleView.Initialize(
                        obstacle,
                        worldPosition);

                    obstacleView.BindElevationView(elevationView);

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
                    if (useCatacombsVisuals)
                    {
                        DungeonCatacombsObjectVisuals.TryApplyTrap(
                            spikeTrapView,
                            spikeTrap.Definition as SpikeTrapDungeonObjectDefinition);
                    }
                    if(useForestVisuals)DungeonForestObjectVisuals.TryApplyTrap(spikeTrapView,spikeTrap.Definition as SpikeTrapDungeonObjectDefinition);
                    spikeTrapView.Initialize(
                        partyController,
                        floor,
                        spikeTrap,
                        floorTilemap,
                        worldPosition);

                    spikeTrapView.BindElevationView(elevationView);

                    spawnedObjects.Add(spikeTrapView);
                    continue;
                }

                if (dungeonObject is LeverDungeonObjectInstance lever)
                {
                    var leverView = useForestVisuals ? DungeonForestObjectVisuals.CreateLeverView(parent,$"Lever_{lever.InstanceId}",lever.Definition as LeverDungeonObjectDefinition) : null;
                    leverView ??= leverPrefab != null
                        ? Instantiate(leverPrefab, parent)
                        : useCatacombsVisuals
                          ? DungeonCatacombsObjectVisuals.CreateLeverView(
                            parent,
                            $"Lever_{lever.InstanceId}",
                            lever.Definition as LeverDungeonObjectDefinition)
                          ?? DungeonObjectView.CreateFallback<DungeonLeverView>(
                              parent,
                              $"Lever_{lever.InstanceId}")
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

                    leverView.BindElevationView(elevationView);

                    spawnedObjects.Add(leverView);
                    continue;
                }

                if (dungeonObject is ConditionDungeonObjectInstance condition)
                {
                    var conditionView = useForestVisuals ? DungeonForestObjectVisuals.CreateConditionView(parent,$"Condition_{condition.InstanceId}",condition.Definition as ConditionDungeonObjectDefinition) : null;
                    conditionView ??= conditionPrefab != null
                        ? Instantiate(conditionPrefab, parent)
                        : useCatacombsVisuals
                          ? DungeonCatacombsObjectVisuals.CreateConditionView(
                            parent,
                            $"Condition_{condition.InstanceId}",
                            condition.Definition as ConditionDungeonObjectDefinition)
                          ?? DungeonObjectView.CreateFallback<DungeonConditionView>(
                              parent,
                              $"Condition_{condition.InstanceId}")
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

                    conditionView.BindElevationView(elevationView);

                    spawnedObjects.Add(conditionView);
                }
            }

            RefreshBodies(floor, partyController);
            if (useCatacombsVisuals && !catacombsAuditPresentationLogged)
            {
                LogCatacombsAuditPresentation(floor);
                catacombsAuditPresentationLogged = true;
            }
            return true;
        }

        private void LogCatacombsAuditPresentation(DungeonFloorInstance floor)
        {
            foreach (var view in spawnedObjects)
            {
                if (view == null || !floor.TryGetObject(view.InstanceId, out var dungeonObject))
                    continue;

                foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer == null || !renderer.enabled || renderer.sprite == null)
                        continue;

                    var bounds = renderer.bounds;
                    Debug.Log(
                        $"Catacombs audit binding objectId={dungeonObject.InstanceId} "
                        + $"definition={dungeonObject.Definition?.Id} cell={dungeonObject.Position} "
                        + $"view={view.name} sprite={renderer.sprite.name} "
                        + $"texture={renderer.sprite.texture?.name} "
                        + $"worldBoundsCenter={bounds.center} worldBoundsSize={bounds.size}",
                        view);
                }
            }
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

                bodyView.BindElevationView(elevationView);
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
                    if (Application.isPlaying) Destroy(objectView.gameObject); else DestroyImmediate(objectView.gameObject);
                }
            }

            spawnedObjects.Clear();

            foreach (var bodyObject in spawnedEnemyBodies)
            {
                if (bodyObject != null)
                {
                    if (Application.isPlaying) Destroy(bodyObject); else DestroyImmediate(bodyObject);
                }
            }

            spawnedEnemyBodies.Clear();
            spawnedBodyIds.Clear();
        }
    }
}
