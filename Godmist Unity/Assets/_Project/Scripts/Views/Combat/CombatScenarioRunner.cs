using Godmist.Data.Combat;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Game;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public class CombatScenarioRunner : MonoBehaviour
    {
        [SerializeField] private EncounterDefinition encounterDefinition;

        public bool HasEncounter => encounterDefinition != null;

        public bool TryCreateSession(
            GameSession gameSession,
            float actionPointsPerSpeed,
            out CombatSession combatSession)
        {
            combatSession = null;

            if (gameSession == null
                || gameSession.HasCombat
                || encounterDefinition == null)
            {
                return false;
            }

            var factory = new CombatSessionFactory(
                actionPointsPerSpeed);

            return factory.TryCreate(
                gameSession,
                encounterDefinition,
                out combatSession);
        }
    }
}