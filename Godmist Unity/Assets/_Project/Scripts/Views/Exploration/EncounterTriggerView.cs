using Godmist.Data.Combat;
using Godmist.Systems.Combat;
using Godmist.Views.Game;
using UnityEngine;

namespace Godmist.Views.Exploration
{
    public class EncounterTriggerView : MonoBehaviour
    {
        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private EncounterDefinition encounterDefinition;
        [SerializeField] private float triggerRadius = 1.25f;
        [SerializeField] private float actionPointsPerSpeed = 0.25f;

        private bool isTriggered;
        private bool isArmed;
        private bool hasInitialized;

        private void Update()
        {
            if (!hasInitialized)
            {
                TryInitializeTrigger();
                return;
            }

            if (isTriggered || partyController == null || encounterDefinition == null) return;

            if (IsEncounterCleared())
            {
                gameObject.SetActive(false);
                return;
            }

            if (!isArmed)
            {
                if (!IsLeaderWithinRange()) isArmed = true;

                return;
            }

            if (!IsLeaderWithinRange()) return;

            TryStartEncounter();
        }

        private void TryStartEncounter()
        {
            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null || gameBootstrap.Session.ActiveCombat != null) return;

            var factory = new CombatSessionFactory(actionPointsPerSpeed);

            if (!factory.TryCreate(
                    gameBootstrap.Session,
                    encounterDefinition,
                    out var combatSession))
            {
                Debug.LogError(
                    "EncounterTriggerView: nie udalo sie utworzyc CombatSession.");
                return;
            }

            if (!gameBootstrap.Session.TrySetActiveCombat(combatSession)) return;

            isTriggered = true;
            gameBootstrap.StartCombat();
        }

        private void TryInitializeTrigger()
        {
            if (partyController == null || encounterDefinition == null) return;
            if (partyController.ActiveLeaderTransform == null) return;

            hasInitialized = true;

            if (IsEncounterCleared())
            {
                gameObject.SetActive(false);
                return;
            }

            // Po ucieczce drużyna wraca obok encounteru.
            // Musi najpierw oddalić się poza zasięg, aby wywołać go ponownie.
            isArmed = !IsLeaderWithinRange();
        }

        private bool IsLeaderWithinRange()
        {
            if (partyController?.ActiveLeaderTransform == null) return false;

            var offset = partyController.ActiveLeaderTransform.position - transform.position;

            return offset.sqrMagnitude <= triggerRadius * triggerRadius;
        }

        private bool IsEncounterCleared()
        {
            var activeDungeon = GameBootstrap.Instance?.Session.ActiveDungeon;

            return activeDungeon != null
                && activeDungeon.IsEncounterCleared(encounterDefinition.Id);
        }
    }
}