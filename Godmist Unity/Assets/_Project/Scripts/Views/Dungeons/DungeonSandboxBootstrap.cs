using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonSandboxBootstrap : MonoBehaviour
    {
        [SerializeField] private DungeonFloorMapView floorMapView;
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

                if (!objectSpawner.Rebuild(floor, partySpawner.PartyController)) return;

                if (!encounterSpawner.Rebuild(floor, partySpawner.PartyController)) return;

                if (!TryGetRoomCenter(
                        dungeon,
                        DungeonRoomType.Final,
                        floorMapView,
                        out var exitPosition))
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

                var exitView = Instantiate(exitPrefab, exitParent);

                exitView.Initialize(partySpawner.PartyController, exitPosition);

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
                DungeonRoomType.Start,
                floorMapView,
                out position);
        }

        private static bool TryGetRoomCenter(
            DungeonInstance dungeon,
            DungeonRoomType roomType,
            DungeonFloorMapView floorMapView,
            out Vector2 center)
        {
            center = default;

            if (floorMapView == null
                || !dungeon.TryGetCurrentFloor(out var floor)
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

                    var preferredCell = room.LayoutPosition
                                        + new Vector2Int(
                                            template.SizeInCells.x / 2,
                                            template.SizeInCells.y / 2);

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
