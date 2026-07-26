using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatOutcomeSystem
    {
        private readonly Action<string> battleLog;

        public CombatOutcomeSystem(Action<string> battleLog = null)
        {
            this.battleLog = battleLog;
        }

        public void EvaluateCombatOutcome(CombatSession session)
        {
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }

            EvaluateVictoryCondition(session);
            EvaluateDefeatCondition(session);
        }

        public void EvaluateVictoryCondition(CombatSession session)
        {
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }

            var anyEnemiesLeft = session.Units.Exists(
                unit => unit != null
                    && !unit.IsPlayerSide
                    && unit.Character != null
                    && unit.Character.LifeState == CharacterLifeState.Alive);

            if (!anyEnemiesLeft)
            {
                session.Outcome = CombatOutcomeState.Victory;
                battleLog?.Invoke("Zwyciestwo.");
            }
        }

        public void EvaluateDefeatCondition(CombatSession session)
        {
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }

            var anyPlayerUnitsLeft = session.Units.Exists(
                unit => unit != null
                    && unit.IsPlayerSide
                    && unit.Character != null
                    && unit.Character.LifeState != CharacterLifeState.Dead);

            if (!anyPlayerUnitsLeft)
            {
                session.Outcome = CombatOutcomeState.Defeat;
                battleLog?.Invoke("Porazka.");
            }
        }
    }
}
