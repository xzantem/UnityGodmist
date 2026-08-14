using System;
using System.Collections.Generic;
using Godmist.Core.Constants;
using Godmist.Core.Enums;
using Godmist.Data.Effects;
using Godmist.Data.Skills;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public class CombatReactionSystem
    {
        private readonly Action<string> battleLog;

        public CombatReactionSystem(Action<string> battleLog = null)
        {
            this.battleLog = battleLog;
        }

        public bool TryConsumeCounterattack(
            BattleUnit defender,
            BattleUnit attacker,
            out SkillData reactionSkill,
            out IReadOnlyList<SkillEffectData> reactionEffects)
        {
            reactionSkill = null;
            reactionEffects = null;

            if (defender?.Character == null
                || attacker?.Character == null
                || defender.Character.LifeState != CharacterLifeState.Alive
                || attacker.Character.LifeState != CharacterLifeState.Alive
                || defender.IsPlayerSide == attacker.IsPlayerSide
                || defender.HasReacted)
            {
                return false;
            }

            var counterattack = FindCounterattack(defender);
            if (counterattack?.CounterattackSkill == null
                || counterattack.CounterattackRange == null
                || counterattack.CounterattackEffects == null
                || counterattack.CounterattackEffects.Count == 0
                || !CombatTargetingService.IsPositionWithinRange(
                    defender.Position,
                    attacker.Position,
                    counterattack.CounterattackRange))
            {
                return false;
            }

            defender.HasReacted = true;
            reactionSkill = counterattack.CounterattackSkill;
            reactionEffects = counterattack.CounterattackEffects;
            battleLog?.Invoke($"{defender.Character.Definition.DisplayName} kontratakuje.");
            return true;
        }

        private static PassiveEffectRuntime FindCounterattack(BattleUnit unit)
        {
            if (unit?.Character?.PassiveEffects == null)
            {
                return null;
            }

            foreach (var effect in unit.Character.PassiveEffects)
            {
                if (effect != null
                    && effect.Lifetime == PassiveEffectLifetime.Timed
                    && effect.RemainingDuration > 0
                    && string.Equals(effect.EffectId, StatusEffectIds.Counterattack,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return effect;
                }
            }

            return null;
        }
    }
}
