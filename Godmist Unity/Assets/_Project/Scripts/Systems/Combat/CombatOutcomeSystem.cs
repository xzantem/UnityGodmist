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
            if (session?.Outcome == CombatOutcomeState.Victory)
            {
                StandUpDownedAllies(session);
                return;
            }
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing) return;

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
                StandUpDownedAllies(session);
                battleLog?.Invoke("Zwyciestwo.");
            }
        }

        public static void StandUpDownedAllies(CombatSession session)
        {
            if (session?.Outcome != CombatOutcomeState.Victory) return;
            foreach (var unit in session.Units)
                if (unit?.IsPlayerSide == true && CombatReviveSystem.StandUp(unit.Character))
                    unit.DownedTurnsRemaining = 0;
        }

        public void EvaluateDefeatCondition(CombatSession session)
        {
            if (session == null || session.Outcome != CombatOutcomeState.Ongoing)
            {
                return;
            }

            var anyLivingPlayerUnits = session.Units.Exists(
                unit => unit != null
                    && unit.IsPlayerSide
                    && unit.Character != null
                    && unit.Character.LifeState == CharacterLifeState.Alive);

            if (!anyLivingPlayerUnits)
            {
                if (session.IsSeamless && session.CanReceiveReinforcements
                    && session.Units.Exists(unit => unit?.IsPlayerSide == true
                        && unit.Character?.LifeState == CharacterLifeState.Downed)) return;
                if (session.IsSeamless && !session.Units.Exists(unit => unit?.IsPlayerSide == true
                    && unit.Character?.LifeState != CharacterLifeState.Dead))
                {
                    session.Outcome = session.DepartedUnits.Count > 0
                        ? CombatOutcomeState.Escaped : CombatOutcomeState.Defeat;
                    return;
                }
                session.Outcome = CombatOutcomeState.Defeat;
                battleLog?.Invoke("Porazka.");
            }
        }
    }
}
