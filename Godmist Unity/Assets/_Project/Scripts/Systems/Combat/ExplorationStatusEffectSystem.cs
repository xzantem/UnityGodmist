using System.Collections.Generic;
using System.Linq;
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
            if (!float.IsFinite(seconds) || seconds <= 0f) return;
            var visited = new HashSet<Godmist.Runtime.Characters.CharacterInstance>();
            foreach (var occupant in occupants)
            {
                var unit = occupant.Unit;
                if (unit?.Character == null || occupant.Session != null || unit.Character.LifeState != CharacterLifeState.Alive) continue;
                var character = unit.Character;
                if (!visited.Add(character)) continue;
                if (!character.PassiveEffects.Any(e => e.Lifetime == Godmist.Core.Enums.PassiveEffectLifetime.Timed))
                { character.ExplorationStatusElapsedSeconds = 0f; continue; }
                var time = character.ExplorationStatusElapsedSeconds;
                time += seconds;
                var damage = new CombatDamageSystem(feedbackBus: feedback);
                var status = new StatusEffectSystem(damageSystem: damage, feedbackBus: feedback);
                while (time >= ExplorationTickSeconds && character.LifeState == CharacterLifeState.Alive)
                {
                    time -= ExplorationTickSeconds;
                    status.ProcessTurnStart(environment, unit);
                    status.ProcessTurnEnd(unit);
                }
                character.ExplorationStatusElapsedSeconds = time;
            }
        }

    }
}
