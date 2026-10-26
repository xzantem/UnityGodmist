using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonSandboxBootstrap : MonoBehaviour
    {
        [SerializeField] private DungeonFloorMapView floorMapView;
        [SerializeField] private Godmist.Views.Lighting.DungeonSilhouetteLighting silhouetteLighting;
        [SerializeField] private DungeonCombatCoordinator combatCoordinator;
        [SerializeField] private ExplorationCameraFollow cameraFollow;
        [SerializeField] private DungeonPartySpawner partySpawner;
        [SerializeField] private DungeonEncounterSpawner encounterSpawner;
        [SerializeField] private DungeonObjectSpawner objectSpawner;

        [SerializeField] private DungeonExitView exitPrefab;
        [SerializeField] private Transform exitsRoot;

        private void Start()
        {
            var session = GameBootstrap.Instance?.Session;
            var dungeon = session?.ActiveDungeon;

            if (dungeon == null || dungeon.Definition == null)
            {
                Debug.LogError(
                    "DungeonSandboxBootstrap: brak aktywnego lochu w GameSession.");

                return;
            }

            if (floorMapView == null
                || partySpawner == null
                || encounterSpawner == null || objectSpawner == null)
            {
                Debug.LogError(
                    "DungeonSandboxBootstrap: brak DungeonFloorMapView, "
                    + "DungeonPartySpawner, DungeonEncounterSpawner "
                    + "lub DungeonObjectSpawner.");

                return;
            }

            if (dungeon.Definition.RoomTemplateCatalog == null)
            {
                Debug.LogError(
                    "DungeonSandboxBootstrap: aktywny loch nie ma RoomTemplateCatalog.");

                return;
            }

            try
            {
                if (!dungeon.TryGetCurrentFloor(out var floor) || floor.Map == null)
                {
                    Debug.LogError("DungeonSandboxBootstrap: aktywne pietro nie ma wygenerowanej mapy.");

                    return;
                }

                var isCatacombs = ConfigureFloorPresentation(
                    dungeon.Definition, floor, dungeon.IsAssetAuditRoom);
                floorMapView.PrepareForestTransitionBoundaries(floor);
                floorMapView.Rebuild(floor.Map, deferForestGround: true);

                if (!TryGetDungeonSpawnPosition(
                        dungeon,
                        floorMapView,
                        out var spawnPosition))
                {
                    Debug.LogError("DungeonSandboxBootstrap: nie znaleziono pozycji spawnu druzyny.");

                    return;
                }

                if (!partySpawner.TrySpawn(session, spawnPosition)) return;

                if (dungeon.HasPendingPlayerPartyPositions)
                {
                    partySpawner.PartyController.RestorePartyPositions(
                        dungeon.PendingPlayerPartyPositions,
                        dungeon.PlayerPosition);
                    dungeon.ClearPendingPlayerPartyPositions();
                }

                if (!objectSpawner.Rebuild(
                        floor,
                        partySpawner.PartyController,
                        dungeon.Definition.Theme,
                        dungeon.Definition.Id)) return;

                if (!encounterSpawner.Rebuild(floor, partySpawner.PartyController, dungeon.DungeonLevel)) return;
                if (combatCoordinator == null || !combatCoordinator.Initialize(session, floor,
                    floorMapView, partySpawner.PartyController, encounterSpawner, objectSpawner))
                { Debug.LogError("DungeonSandboxBootstrap: brak poprawnej integracji seamless."); return; }

                if (!TryGetRoomCenter(
                        dungeon,
                        floor,
                        DungeonRoomType.Final,
                        floorMapView,
                        out var finalPosition))
                {
                    Debug.LogError("DungeonSandboxBootstrap: nie znaleziono pokoju Final.");

                    return;
                }

                if (exitPrefab == null)
                {
                    Debug.LogError("DungeonSandboxBootstrap: brak prefabu DungeonExitView.");

                    return;
                }

                var exitParent = exitsRoot != null ? exitsRoot : transform;


                var occupiedPresentationCells = new HashSet<Vector2Int>(
                    floor.Objects.Where(value => value != null).Select(value => value.Position));
                occupiedPresentationCells.UnionWith(
                    floor.EnemyBodies.Where(value => value != null).Select(value => value.Position));
                occupiedPresentationCells.Add(floorMapView.WorldToCell(spawnPosition));
                occupiedPresentationCells.Add(floorMapView.WorldToCell(finalPosition));
                if (dungeon.IsAssetAuditRoom && dungeon.HasAssetAuditDagger)
                    occupiedPresentationCells.Add(dungeon.AssetAuditDaggerCell);
                foreach (var member in partySpawner.PartyController.ActiveMemberViews)
                    if (member != null)
                        occupiedPresentationCells.Add(floorMapView.WorldToCell(member.Position));
                if (dungeon.HasPlayerPosition)
                    occupiedPresentationCells.Add(floorMapView.WorldToCell(dungeon.PlayerPosition));
                floorMapView.SetForestPathObstacles(floor.Objects.OfType<ObstacleDungeonObjectInstance>()
                    .Where(o => o.IsActive && o.BlocksMovement).SelectMany(o => o.EnumerateOccupiedCells()));
                floorMapView.SetPresentationOccupancy(occupiedPresentationCells);

                if (floorMapView.UsesVegetationPresentation)
                    foreach(var tree in floor.Objects.OfType<ObstacleDungeonObjectInstance>().Where(o=>o.IsActive && o.Definition.Id == "object.forest.tree.prototype"))
                    {
                        var view = objectSpawner.SpawnedObjects.FirstOrDefault(v=>v.ObjectInstanceId==tree.InstanceId);
                        if(view != null) foreach(var sr in view.GetComponentsInChildren<SpriteRenderer>()) floorMapView.RegisterForestCanopy(sr);
                    }
                silhouetteLighting?.Initialize(partySpawner.PartyController, encounterSpawner, objectSpawner, floorMapView);
                SpawnFloorTransitions(dungeon, floor, finalPosition, exitParent);
                if (silhouetteLighting != null && isCatacombs)
                {
                    foreach (var view in objectSpawner.SpawnedObjects)
                        if (view is DungeonSpikeTrapView
                            || view is DungeonConditionView
                            || (view is DungeonObstacleView obstacle
                                && obstacle.IsGroundPresentation))
                        {
                            silhouetteLighting.RegisterGroundObjectView(view);
                        }
                        else if (view is DungeonLeverView)
                        {
                            // The Catacombs audit lever is a floor-standing
                            // prop. Use the same floor response as its stone
                            // base and traps; the generic body path assumes a
                            // tall wall-facing silhouette and made this low
                            // object read brighter and warmer than the tile.
                            silhouetteLighting.RegisterGroundObjectView(view);
                            foreach (var renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
                                silhouetteLighting.RegisterGroundContactPresentation(renderer);
                        }
                }

                if (floorMapView.TryGetWorldBounds(out var worldBounds))
                {
                    cameraFollow?.SetWorldBounds(worldBounds);
                    cameraFollow?.SnapToLeader();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static bool TryGetDungeonSpawnPosition(
            DungeonInstance dungeon,
            DungeonFloorMapView floorMapView,
            out Vector2 position)
        {
            position = default;

            if (dungeon == null) return false;

            if (dungeon.IsAssetAuditRoom
                && dungeon.TryGetCurrentFloor(out var auditFloor)
                && auditFloor.Map != null
                && auditFloor.Map.IsWalkable(dungeon.AssetAuditSpawnCell))
            {
                position = floorMapView.CellToWorldCenter(dungeon.AssetAuditSpawnCell);
                return true;
            }

            if (dungeon.HasPlayerPosition)
            {
                position = dungeon.PlayerPosition;
                return true;
            }

            return TryGetRoomCenter(
                dungeon,
                null,
                DungeonRoomType.Start,
                floorMapView,
                out position);
        }

        private void SpawnFloorTransitions(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            Vector2 finalPosition,
            Transform exitParent)
        {
            finalPosition = floorMapView.ResolveForestTransitionPosition(floor, finalPosition, DungeonRoomType.Final);
            if (dungeon.CurrentFloorIndex < dungeon.Floors.Count - 1)
            {
                var descent = Instantiate(exitPrefab, exitParent);
                descent.InitializeTransition(
                    partySpawner.PartyController,
                    finalPosition,
                    () => TryChangeFloor(
                        dungeon,
                        dungeon.CurrentFloorIndex + 1,
                        dungeon.IsAssetAuditRoom
                            ? DungeonRoomType.Final
                            : DungeonRoomType.Start), requiresClearedExitRoom: true);
                RegisterTransitionLighting(descent);
            }
            else
            {
                var exit = Instantiate(exitPrefab, exitParent);
                exit.Initialize(partySpawner.PartyController, finalPosition);
                RegisterTransitionLighting(exit);
            }

            if (dungeon.CurrentFloorIndex <= 0
                || !TryGetRoomCenter(
                    dungeon,
                    floor,
                    dungeon.IsAssetAuditRoom
                        ? DungeonRoomType.Final
                        : DungeonRoomType.Start,
                    floorMapView,
                    out var startPosition))
            {
                return;
            }

            var ascent = Instantiate(exitPrefab, exitParent);
            startPosition = floorMapView.ResolveForestTransitionPosition(floor, startPosition, DungeonRoomType.Start);
            ascent.InitializeTransition(
                partySpawner.PartyController,
                startPosition,
                () => TryChangeFloor(
                    dungeon,
                    dungeon.CurrentFloorIndex - 1,
                    DungeonRoomType.Final), goesUp: true);
            RegisterTransitionLighting(ascent);
        }

        private void RegisterTransitionLighting(DungeonExitView transition)
        {
            floorMapView?.StyleForestTransition(transition);
            transition.ConfigureGroundPresentation(floorMapView);
            transition.ConfigureExitState(GameBootstrap.Instance?.Session?.ActiveDungeon);
            transition.ConfigureFogPresentation();
            if (silhouetteLighting == null) return;
            foreach (var renderer in transition.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null)
                {
                    if (transition.UsesStandingPresentation) silhouetteLighting.RegisterGroundContactPresentation(renderer);
                    else silhouetteLighting.RegisterGroundPresentation(renderer);
                }
        }

        private bool TryChangeFloor(
            DungeonInstance dungeon,
            int targetFloorIndex,
            DungeonRoomType targetRoomType)
        {
            var controller = partySpawner?.PartyController;

            if (dungeon == null
                || controller?.ActiveLeaderTransform == null
                || !dungeon.TryGetFloor(targetFloorIndex, out var targetFloor)
                || !TryGetRoomCenter(
                    dungeon,
                    targetFloor,
                    targetRoomType,
                    floorMapView,
                    out var targetLeaderPosition))
            {
                return false;
            }

            var sourceLeaderPosition = (Vector2)controller.ActiveLeaderTransform.position;
            var positions = new Dictionary<string, Vector2>();

            foreach (var memberView in controller.ActiveMemberViews)
            {
                if (memberView == null
                    || string.IsNullOrWhiteSpace(memberView.CharacterId))
                {
                    continue;
                }

                positions[memberView.CharacterId] = targetLeaderPosition
                    + ((Vector2)memberView.transform.position - sourceLeaderPosition);
            }

            return GameBootstrap.Instance != null
                && GameBootstrap.Instance.TryChangeDungeonFloor(
                    targetFloorIndex,
                    targetLeaderPosition,
                    positions);
        }

        private static bool TryGetRoomCenter(
            DungeonInstance dungeon,
            DungeonFloorInstance floor,
            DungeonRoomType roomType,
            DungeonFloorMapView floorMapView,
            out Vector2 center)
        {
            center = default;

            if (floorMapView == null
                || (floor == null && !dungeon.TryGetCurrentFloor(out floor))
                || floor.Map == null)
            {
                return false;
            }

            foreach (var room in floor.Rooms)
            {
                if (room.RoomType != roomType)
                {
                    continue;
                }

                foreach (var template in dungeon.Definition.RoomTemplateCatalog.AllTemplates)
                {
                    if (template == null || template.Id != room.TemplateId) continue;

                    var roomSize = DungeonRoomTransformUtility.GetRotatedSize(
                        template.SizeInCells,
                        room.RotationDegrees);
                    var preferredCell = room.LayoutPosition
                                        + new Vector2Int(
                                            roomSize.x / 2,
                                            roomSize.y / 2);

                    var hasCandidate = false;
                    var selectedCell = default(Vector2Int);
                    var selectedDistance = int.MaxValue;

                    foreach (var cell in floor.Map.FloorCells)
                    {
                        if (!floor.Map.TryGetRoomId(cell, out var roomId)
                            || roomId != room.RoomId)
                        {
                            continue;
                        }

                        var offset = cell - preferredCell;
                        var distance = offset.sqrMagnitude;

                        if (hasCandidate
                            && (distance > selectedDistance
                                || (distance == selectedDistance
                                    && CompareCells(cell, selectedCell) >= 0)))
                        {
                            continue;
                        }

                        selectedCell = cell;
                        selectedDistance = distance;
                        hasCandidate = true;
                    }

                    if (!hasCandidate)
                    {
                        return false;
                    }

                    center = floorMapView.CellToWorldCenter(selectedCell);

                    return true;
                }
            }

            return false;
        }

        private bool ConfigureFloorPresentation(
            DungeonDefinition definition,
            DungeonFloorInstance floor,
            bool isAssetAuditRoom)
        {
            // Presentation belongs to the biome. Boss/procedural expeditions
            // have different IDs but use the same imported Catacombs art.
            var isCatacombs = definition.Theme == DungeonTheme.Catacombs;
            floorMapView.ConfigureCatacombsArtPresentation(isCatacombs);
            floorMapView.ConfigureVegetationPresentation(definition.VegetationPresentation);
            floorMapView.ConfigureCatacombsAuditDiagnostics(isAssetAuditRoom && isCatacombs);
            floorMapView.ConfigureAssetAuditStructuralPreview(isAssetAuditRoom && isCatacombs);
            floorMapView.ConfigureForestPresentationFloor(floor);
            return isCatacombs;
        }

        private static int CompareCells(
            Vector2Int first,
            Vector2Int second)
        {
            var xComparison = first.x.CompareTo(second.x);

            return xComparison != 0
                ? xComparison
                : first.y.CompareTo(second.y);
        }
    }
}
