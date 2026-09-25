using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Data.Configuration;

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
                var standingHealth = Math.Max(1, GameDefinesRuntime.GetInt(
                    GameDefineIds.ReviveHealth, 1));
                foreach (var unit in session.Units)
                {
                    if (unit?.IsPlayerSide != true
                        || unit.Character?.LifeState != CharacterLifeState.Downed) continue;
                    unit.Character.LifeState = CharacterLifeState.Alive;
                    unit.Character.CurrentHealth = standingHealth;
                    unit.DownedTurnsRemaining = 0;
                }
                battleLog?.Invoke("Zwyciestwo.");
            }
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
