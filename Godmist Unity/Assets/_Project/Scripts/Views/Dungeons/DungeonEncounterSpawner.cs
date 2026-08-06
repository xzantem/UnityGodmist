using System.Collections.Generic;
using Godmist.Runtime.Dungeons;
using Godmist.Views.Exploration;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonEncounterSpawner : MonoBehaviour
    {
        [SerializeField] private EncounterTriggerView encounterTriggerPrefab;
        [SerializeField] private Transform encountersRoot;

        private readonly List<EncounterTriggerView> spawnedTriggers = new();

        public bool Rebuild(
            DungeonFloorInstance floor,
            ExplorationPartyController partyController)
        {
            ClearSpawnedTriggers();

            if (floor == null || partyController == null)
            {
                Debug.LogError(
                    "DungeonEncounterSpawner: brak pietra lub PartyController.");

                return false;
            }

            if (encounterTriggerPrefab == null)
            {
                Debug.LogError(
                    "DungeonEncounterSpawner: brak prefabu EncounterTriggerView.");

                return false;
            }

            foreach (var encounterInstance in floor.Encounters)
            {
                if (encounterInstance == null) continue;

                var parent = encountersRoot != null
                    ? encountersRoot
                    : transform;

                var triggerView = Instantiate(
                    encounterTriggerPrefab,
                    parent);

                triggerView.name =
                    $"EncounterTrigger_{encounterInstance.InstanceId}";

                triggerView.Initialize(
                    partyController,
                    encounterInstance);

                spawnedTriggers.Add(triggerView);
            }

            return true;
        }

        private void OnDestroy()
        {
            ClearSpawnedTriggers();
        }

        private void ClearSpawnedTriggers()
        {
            foreach (var triggerView in spawnedTriggers)
            {
                if (triggerView != null)
                {
                    Destroy(triggerView.gameObject);
                }
            }

            spawnedTriggers.Clear();
        }
    }
}