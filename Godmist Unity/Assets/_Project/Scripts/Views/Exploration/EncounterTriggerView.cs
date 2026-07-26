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

        private void Update()
        {
            if (isTriggered || partyController == null || encounterDefinition == null) return;
            if (partyController.ActiveLeaderTransform == null) return;

            var offset = partyController.ActiveLeaderTransform.position - transform.position;

            if (offset.sqrMagnitude > triggerRadius * triggerRadius) return;

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
    }
}