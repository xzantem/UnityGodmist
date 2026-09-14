using System;
using System.Collections.Generic;
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

                floorMapView.Rebuild(floor.Map);

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

                if (!objectSpawner.Rebuild(floor, partySpawner.PartyController, dungeon.Definition.Theme)) return;

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

                SpawnFloorTransitions(
                    dungeon,
                    floor,
                    finalPosition,
                    exitParent);

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
            if (dungeon.CurrentFloorIndex < dungeon.Floors.Count - 1)
            {
                var descent = Instantiate(exitPrefab, exitParent);
                descent.InitializeTransition(
                    partySpawner.PartyController,
                    finalPosition,
                    () => TryChangeFloor(
                        dungeon,
                        dungeon.CurrentFloorIndex + 1,
                        DungeonRoomType.Start));
            }
            else
            {
                var exit = Instantiate(exitPrefab, exitParent);
                exit.Initialize(partySpawner.PartyController, finalPosition);
            }

            if (dungeon.CurrentFloorIndex <= 0
                || !TryGetRoomCenter(
                    dungeon,
                    floor,
                    DungeonRoomType.Start,
                    floorMapView,
                    out var startPosition))
            {
                return;
            }

            var ascent = Instantiate(exitPrefab, exitParent);
            ascent.InitializeTransition(
                partySpawner.PartyController,
                startPosition,
                () => TryChangeFloor(
                    dungeon,
                    dungeon.CurrentFloorIndex - 1,
                    DungeonRoomType.Final));
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

                foreach (var template in dungeon.Definition.RoomTemplateCatalog.Templates)
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
