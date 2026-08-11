using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Effects;

namespace Godmist.Systems.Combat
{
    public class PassiveEffectLifecycleSystem
    {
        public IReadOnlyList<PassiveEffectRuntime> GetEffectsForTick(
            CharacterInstance character,
            PassiveEffectTickPhase tickPhase)
        {
            var effectsForTick = new List<PassiveEffectRuntime>();

            if (character?.PassiveEffects == null
                || tickPhase == PassiveEffectTickPhase.None)
            {
                return effectsForTick;
            }

            foreach (var effect in character.PassiveEffects)
            {
                if (!IsActive(effect) || effect.TickPhase != tickPhase)
                {
                    continue;
                }

                effectsForTick.Add(effect);
            }

            return effectsForTick;
        }

        public bool AdvanceDurationAndRemoveIfExpired(
            CharacterInstance character,
            PassiveEffectRuntime effect)
        {
            if (character?.PassiveEffects == null
                || effect == null
                || !effect.IsTimed)
            {
                return false;
            }

            effect.RemainingDuration--;

            if (effect.RemainingDuration > 0)
            {
                return false;
            }

            return character.PassiveEffects.Remove(effect);
        }

        public int RemoveExpiredEffects(CharacterInstance character)
        {
            if (character?.PassiveEffects == null)
            {
                return 0;
            }

            var removedCount = 0;

            for (var index = character.PassiveEffects.Count - 1; index >= 0; index--)
            {
                var effect = character.PassiveEffects[index];

                if (effect == null || HasExpired(effect))
                {
                    character.PassiveEffects.RemoveAt(index);
                    removedCount++;
                }
            }

            return removedCount;
        }

        public bool RemoveEffect(CharacterInstance character, string effectId)
        {
            if (character?.PassiveEffects == null
                || string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            for (var index = character.PassiveEffects.Count - 1; index >= 0; index--)
            {
                var effect = character.PassiveEffects[index];

                if (effect == null)
                {
                    character.PassiveEffects.RemoveAt(index);
                    continue;
                }

                if (!string.Equals(
                        effect.EffectId,
                        effectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                character.PassiveEffects.RemoveAt(index);
                return true;
            }

            return false;
        }

        public static bool IsActive(PassiveEffectRuntime effect)
        {
            return effect != null
                && (!effect.IsTimed || effect.RemainingDuration > 0);
        }

        public static bool HasExpired(PassiveEffectRuntime effect)
        {
            return effect != null
                && effect.IsTimed
                && effect.RemainingDuration <= 0;
        }
    }
}