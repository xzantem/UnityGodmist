using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatReviveSystem
    {
        private const int ReviveHealth = 1;

        private readonly Action<string> battleLog;

        public CombatReviveSystem(
            Action<string> battleLog = null)
        {
            this.battleLog = battleLog;
        }

        public bool TryRevive(
            CombatSession session,
            BattleUnit actor,
            BattleUnit target)
        {
            if (session == null
                || session.Outcome != CombatOutcomeState.Ongoing
                || actor?.Character == null
                || target?.Character == null)
            {
                return false;
            }

            if (!session.Units.Contains(actor)
                || !session.Units.Contains(target))
            {
                return false;
            }

            if (!actor.IsPlayerSide
                || !target.IsPlayerSide)
            {
                return false;
            }

            if (actor.Character.LifeState
                    != CharacterLifeState.Alive
                || target.Character.LifeState
                    != CharacterLifeState.Downed)
            {
                return false;
            }

            target.Character.CurrentHealth = ReviveHealth;
            target.Character.LifeState = CharacterLifeState.Alive;
            target.DownedTurnsRemaining = 0;

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} " +
                "zostaje podniesiony.");

            return true;
        }
    }
}