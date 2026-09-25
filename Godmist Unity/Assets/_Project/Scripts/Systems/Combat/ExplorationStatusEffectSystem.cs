using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Systems.Dungeons;

namespace Godmist.Systems.Combat
{
    public sealed class ExplorationStatusEffectSystem
    {
        public const float ExplorationTickSeconds = 6f;
        private readonly CombatSession environment = new();
        public void Advance(float seconds, IReadOnlyList<DungeonTrapOccupant> occupants, CombatFeedbackBus feedback = null)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f || occupants == null) return;
            var visited = new HashSet<Godmist.Runtime.Characters.CharacterInstance>();
            foreach (var occupant in occupants)
            {
                var unit = occupant.Unit;
                if (unit?.Character == null || occupant.Session != null || unit.Character.LifeState != CharacterLifeState.Alive) continue;
                var character = unit.Character;
                if (!visited.Add(character)) continue;
                ResetExplorationMomentum(unit);
                var time = character.ExplorationStatusElapsedSeconds;
                time += seconds;
                if (time < ExplorationTickSeconds)
                {
                    character.ExplorationStatusElapsedSeconds = time;
                    continue;
                }
                var damage = new CombatDamageSystem(feedbackBus: feedback);
                var status = new StatusEffectSystem(damageSystem: damage, feedbackBus: feedback);
                var resources = new ResourceRuleSystem(feedbackBus: feedback);
                while (time >= ExplorationTickSeconds && character.LifeState == CharacterLifeState.Alive)
                {
                    time -= ExplorationTickSeconds;
                    resources.ProcessExplorationTick(unit);
                    status.ProcessTurnStart(environment, unit);
                    status.ProcessTurnEnd(unit);
                    ResetExplorationMomentum(unit);
                }
                character.ExplorationStatusElapsedSeconds = time;
            }
        }

        private static void ResetExplorationMomentum(BattleUnit unit)
        {
            var pool = unit.Character.ResourcePool;
            if (pool?.ResourceType != ResourceType.Momentum) return;
            pool.CurrentValue = 0;
            pool.FractionalGainRemainder = 0f;
        }

    }
}
