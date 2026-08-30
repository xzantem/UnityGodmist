using System.Collections.Generic;
using Godmist.Data.Dungeons;
using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonLayoutView : MonoBehaviour
    {
        [SerializeField] private DungeonDoorwayView doorwayPrefab;
        [SerializeField] private Transform doorwaysRoot;

        private readonly List<GameObject> spawnedObjects = new();

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

            if (!dungeon.TryGetCurrentFloor(out var floor))
            {
                Debug.LogError(
                    "DungeonLayoutView: aktywne pietro lochu nie istnieje.");
                return;
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
            bounds = default;
            return false;
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
        }
    }
}
