using Godmist.Runtime.Dungeons;
using Godmist.Systems.Combat;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public class EncounterTriggerView : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D triggerCollider;
        [SerializeField] private float actionPointsPerSpeed = 0.25f;

        private ExplorationPartyController partyController;
        private DungeonEncounterInstance encounterInstance;

        private bool isTriggered;
        private bool isArmed;
        private bool hasInitialized;

        public void Initialize(
            ExplorationPartyController nextPartyController,
            DungeonEncounterInstance nextEncounterInstance)
        {
            partyController = nextPartyController;
            encounterInstance = nextEncounterInstance;

            if (partyController == null || encounterInstance == null)
            {
                Debug.LogError(
                    "EncounterTriggerView: brak PartyController lub DungeonEncounterInstance.");

                enabled = false;
                return;
            }

            ConfigureTriggerArea();

            hasInitialized = true;

            if (IsEncounterCleared())
            {
                gameObject.SetActive(false);
                return;
            }

            // Encounter nie uruchamia się natychmiast, jeżeli drużyna
            // została odtworzona wewnątrz jego obszaru.
            isArmed = !IsLeaderInsideTriggerArea();
        }

        private void Update()
        {
            if (!hasInitialized || isTriggered) return;

            if (partyController == null || encounterInstance == null) return;

            if (IsEncounterCleared())
            {
                gameObject.SetActive(false);
                return;
            }

            if (!isArmed)
            {
                if (!IsLeaderInsideTriggerArea())
                {
                    isArmed = true;
                }

                return;
            }

            if (!IsLeaderInsideTriggerArea()) return;

            TryStartEncounter();
        }

        private void ConfigureTriggerArea()
        {
            var triggerArea = encounterInstance.TriggerArea;

            transform.position = new Vector3(
                triggerArea.xMin + triggerArea.width * 0.5f,
                triggerArea.yMin + triggerArea.height * 0.5f,
                0f);

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<BoxCollider2D>();
            }

            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            triggerCollider.isTrigger = true;
            triggerCollider.offset = Vector2.zero;
            triggerCollider.size = new Vector2(
                triggerArea.width,
                triggerArea.height);
        }

        private void TryStartEncounter()
        {
            var gameBootstrap = GameBootstrap.Instance;
            var gameSession = gameBootstrap != null
                ? gameBootstrap.Session
                : null;

            if (gameSession == null || gameSession.ActiveCombat != null)
            {
                return;
            }

            var factory = new CombatSessionFactory(actionPointsPerSpeed);

            if (!factory.TryCreate(
                    gameSession,
                    encounterInstance,
                    out var combatSession))
            {
                Debug.LogError(
                    "EncounterTriggerView: nie udalo sie utworzyc CombatSession.");

                return;
            }

            if (!gameSession.TrySetActiveCombat(combatSession)) return;

            isTriggered = true;
            gameBootstrap.StartCombat();
        }

        private bool IsLeaderInsideTriggerArea()
        {
            if (partyController?.ActiveLeaderTransform == null) return false;

            var leaderPosition = partyController.ActiveLeaderTransform.position;
            var triggerArea = encounterInstance.TriggerArea;

            return leaderPosition.x >= triggerArea.xMin
                && leaderPosition.x < triggerArea.xMax
                && leaderPosition.y >= triggerArea.yMin
                && leaderPosition.y < triggerArea.yMax;
        }

        private bool IsEncounterCleared()
        {
            var activeDungeon = GameBootstrap.Instance?.Session.ActiveDungeon;

            return activeDungeon != null
                && activeDungeon.IsEncounterCleared(
                    encounterInstance.InstanceId);
        }

        private void OnDrawGizmosSelected()
        {
            if (encounterInstance == null) return;

            var area = encounterInstance.TriggerArea;

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);

            Gizmos.DrawCube(
                new Vector3(
                    area.xMin + area.width * 0.5f,
                    area.yMin + area.height * 0.5f,
                    0f),
                new Vector3(area.width, area.height, 0f));
        }
    }
}