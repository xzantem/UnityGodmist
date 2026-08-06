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
                || encounterSpawner == null)
            {
                Debug.LogError(
                    "DungeonSandboxBootstrap: brak DungeonFloorMapView, "
                    + "DungeonPartySpawner lub DungeonEncounterSpawner.");

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
                if (!dungeon.TryGetCurrentFloor(out var floor)
                    || floor.Map == null)
                {
                    Debug.LogError(
                        "DungeonSandboxBootstrap: aktywne pietro nie ma wygenerowanej mapy.");

                    return;
                }

                floorMapView.Rebuild(floor.Map);

                if (!TryGetRoomCenter(
                        dungeon,
                        DungeonRoomType.Start,
                        out var startPosition))
                {
                    Debug.LogError(
                        "DungeonSandboxBootstrap: nie znaleziono pokoju Start.");

                    return;
                }

                if (!partySpawner.TrySpawn(session, startPosition))
                {
                    return;
                }

                if (!encounterSpawner.Rebuild(
                        floor,
                        partySpawner.PartyController))
                {
                    return;
                }

                if (!TryGetRoomCenter(
                        dungeon,
                        DungeonRoomType.Final,
                        out var exitPosition))
                {
                    Debug.LogError(
                        "DungeonSandboxBootstrap: nie znaleziono pokoju Final.");

                    return;
                }

                if (exitPrefab == null)
                {
                    Debug.LogError(
                        "DungeonSandboxBootstrap: brak prefabu DungeonExitView.");

                    return;
                }

                var exitParent = exitsRoot != null ? exitsRoot : transform;

                var exitView = Instantiate(
                    exitPrefab,
                    exitParent);

                exitView.Initialize(
                    partySpawner.PartyController,
                    exitPosition);

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

        private static bool TryGetRoomCenter(
            DungeonInstance dungeon,
            DungeonRoomType roomType,
            out Vector2 center)
        {
            center = default;

            if (!dungeon.TryGetCurrentFloor(out var floor))
            {
                return false;
            }

            foreach (var room in floor.Rooms)
            {
                if (room.RoomType != roomType)
                {
                    continue;
                }

                foreach (var template in dungeon
                             .Definition
                             .RoomTemplateCatalog
                             .Templates)
                {
                    if (template == null || template.Id != room.TemplateId)
                    {
                        continue;
                    }

                    center = new Vector2(
                        room.LayoutPosition.x + template.SizeInCells.x * 0.5f,
                        room.LayoutPosition.y + template.SizeInCells.y * 0.5f);

                    return true;
                }
            }

            return false;
        }
    }
}