using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonLayoutView : MonoBehaviour
    {
        [SerializeField] private DungeonRoomView roomPrefab;
        [SerializeField] private Transform roomsRoot;

        [SerializeField] private DungeonDoorwayView doorwayPrefab;
        [SerializeField] private Transform doorwaysRoot;

        private readonly List<GameObject> spawnedObjects = new();
        private readonly Dictionary<string, DungeonRoomView> roomViewsById = new();
        private readonly Dictionary<string, DungeonRoomTemplateDefinition>
            templatesById = new();

        private Bounds worldBounds;
        private bool hasWorldBounds;

        public void Rebuild(
            DungeonInstance dungeon,
            DungeonRoomTemplateCatalog catalog)
        {
            Clear();

            if (dungeon == null || catalog == null)
            {
                Debug.LogError(
                    "DungeonLayoutView: brak DungeonInstance lub katalogu szablonow.");
                return;
            }

            BuildTemplateLookup(catalog);

            if (!dungeon.TryGetCurrentFloor(out var floor))
            {
                Debug.LogError(
                    "DungeonLayoutView: aktywne pietro lochu nie istnieje.");
                return;
            }

            foreach (var room in floor.Rooms)
            {
                if (!templatesById.TryGetValue(
                        room.TemplateId,
                        out var template))
                {
                    Debug.LogError(
                        $"DungeonLayoutView: brak szablonu '{room.TemplateId}'.");
                    continue;
                }

                var roomView = Instantiate(
                    roomPrefab,
                    roomsRoot);

                roomView.Bind(room, template);

                roomViewsById.Add(room.RoomId, roomView);
                spawnedObjects.Add(roomView.gameObject);

                AddRoomBounds(roomView.WorldBounds);
            }
            
            foreach (var connection in floor.Connections)
            {
                if (!connection.HasGeometry)
                {
                    Debug.LogError(
                        $"DungeonLayoutView: polaczenie '{connection.FirstRoomId}' "
                        + $"-> '{connection.SecondRoomId}' nie ma geometrii.");

                    continue;
                }

                var doorwayView = Instantiate(
                    doorwayPrefab,
                    doorwaysRoot);

                doorwayView.Bind(connection);

                spawnedObjects.Add(doorwayView.gameObject);
            }
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = worldBounds;
            return hasWorldBounds;
        }

        private void Clear()
        {
            foreach (var spawnedObject in spawnedObjects)
            {
                if (spawnedObject != null)
                {
                    Destroy(spawnedObject);
                }
            }

            spawnedObjects.Clear();
            roomViewsById.Clear();
            templatesById.Clear();
            hasWorldBounds = false;
        }

        private void BuildTemplateLookup(
            DungeonRoomTemplateCatalog catalog)
        {
            foreach (var template in catalog.Templates)
            {
                if (template == null
                    || string.IsNullOrWhiteSpace(template.Id))
                {
                    continue;
                }

                if (!templatesById.TryAdd(template.Id, template))
                {
                    Debug.LogError(
                        $"DungeonLayoutView: powtorzone TemplateId '{template.Id}'.");
                }
            }
        }

        private void AddRoomBounds(Bounds roomBounds)
        {
            if (!hasWorldBounds)
            {
                worldBounds = roomBounds;
                hasWorldBounds = true;
                return;
            }

            worldBounds.Encapsulate(roomBounds);
        }
    }
}