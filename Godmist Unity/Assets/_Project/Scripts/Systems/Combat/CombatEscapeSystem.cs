using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class CombatEscapeSystem
    {
        public const int BaseEscapeChancePercent = 60;
        public const int EscapeChanceIncreasePercent = 10;

        private readonly Action<string> battleLog;
        private readonly Func<int> rollPercent;

        public CombatEscapeSystem(Action<string> battleLog = null, Func<int> rollPercent = null)
        {
            this.battleLog = battleLog;
            this.rollPercent = rollPercent ?? (() => UnityEngine.Random.Range(1, 101));
        }

        public bool TryEscape(CombatSession session, BattleUnit actor)
        {
            if (session == null || actor?.Character == null || session.ActiveUnit != actor
                || !actor.IsPlayerSide || actor.Character.LifeState != CharacterLifeState.Alive
                || session.Outcome != CombatOutcomeState.Ongoing) return false;

            if (actor.IsReadyToEscape)
            {
                CompleteEscape(session);
                return true;
            }

            var roll = rollPercent();
            var succeeded = roll <= session.CurrentEscapeChancePercent;

            if (succeeded)
            {
                actor.IsReadyToEscape = true;

                battleLog?.Invoke($"{GetUnitName(actor)} przygotowuje się do ucieczki." +
                    $"Przy następnej okazji może opuścić walkę.");

                return true;
            }

            session.CurrentEscapeChancePercent = Mathf.Min(100, 
                session.CurrentEscapeChancePercent + EscapeChanceIncreasePercent);
            
            battleLog?.Invoke($"{GetUnitName(actor)} nie przygotowuje się do ucieczki." +
                $"Rzut: {roll}, kolejna szansa: {session.CurrentEscapeChancePercent}%.");

            return true;
        }

        private void CompleteEscape(CombatSession session)
        {
            var playerUnits = session.Units.Where(unit => unit != null && unit.IsPlayerSide).ToList();
            var mainCharacterWasAbandoned = false;

            foreach (var unit in playerUnits)
            {
                var escaped = unit.Character != null && unit.Character.LifeState == CharacterLifeState.Alive
                    && unit.IsReadyToEscape;

                if (escaped)
                {
                    battleLog?.Invoke($"{GetUnitName(unit)} ucieka z walki.");
                }
                else if(unit.Character != null && unit.Character.LifeState != CharacterLifeState.Dead)
                {
                    if (unit.Character.Definition != null
                        && unit.Character.Definition.IsMainCharacter)
                    {
                        mainCharacterWasAbandoned = true;
                    }
                    unit.Character.CurrentHealth = 0;
                    unit.Character.LifeState = CharacterLifeState.Dead;
                    unit.DownedTurnsRemaining = 0;

                    battleLog?.Invoke($"{GetUnitName(unit)} zostaje porzucony i ginie.");
                }

            }

            session.ActiveUnit = null;
            session.ActiveTurnIndex = -1;
            session.CurrentEscapeChancePercent = BaseEscapeChancePercent;
            session.Outcome = mainCharacterWasAbandoned
                ? CombatOutcomeState.Defeat
                : CombatOutcomeState.Escaped;

            battleLog?.Invoke(mainCharacterWasAbandoned
                ? "Główna postać została porzucona. Drużyna ponosi porażkę."
                : "Drużyna opuszcza walkę.");
        }

        private static string GetUnitName(BattleUnit unit)
        {
            return unit?.Character?.Definition?.DisplayName ?? "Jednostka";
        }
    }
}
