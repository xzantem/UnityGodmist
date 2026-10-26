using System;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;

namespace Godmist.Systems.Combat
{
    public class CombatReviveSystem
    {
        private static int ReviveHealth => GameDefinesRuntime.GetInt(
            GameDefineIds.ReviveHealth, 1);

        private readonly Action<string> battleLog;

        public CombatReviveSystem(
            Action<string> battleLog = null)
        {
            this.battleLog = battleLog;
        }

        public static bool StandUp(CharacterInstance character)
        {
            if (character?.LifeState != CharacterLifeState.Downed) return false;
            character.CurrentHealth = Math.Max(1, ReviveHealth);
            character.LifeState = CharacterLifeState.Alive;
            character.ExplorationStatusElapsedSeconds = 0f;
            return true;
        }

        public bool TryReviveExploration(CharacterInstance actor, CharacterInstance target)
        {
            if (actor == null || ReferenceEquals(actor, target)
                || actor.LifeState != CharacterLifeState.Alive || !StandUp(target)) return false;
            battleLog?.Invoke($"{target.Definition?.DisplayName ?? target.CharacterId} zostaje podniesiony.");
            return true;
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

            if (!IsWithinInteractionRange(session, actor.Position, target.Position))
                return false;

            StandUp(target.Character);
            target.DownedTurnsRemaining = 0;

            battleLog?.Invoke(
                $"{target.Character.Definition.DisplayName} " +
                "zostaje podniesiony.");

            return true;
        }

        private static bool IsWithinInteractionRange(CombatSession session, GridPosition from, GridPosition to)
        {
            if (Math.Max(Math.Abs((long)from.X - to.X), Math.Abs((long)from.Y - to.Y)) > 1)
                return false;
            if (from.Level == to.Level && !from.IsOnStairs && !to.IsOnStairs)
                return true;

            // Matching map coordinates on another surface are not adjacent.
            // Stairs may connect surfaces, but only through a real neighbouring step.
            if (session.Grid != null)
                foreach (var neighbour in session.Grid.GetNeighbours(from))
                    if (neighbour == to) return true;
            return false;
        }
    }
}
