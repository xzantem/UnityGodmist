using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class CombatVariableSystem
    {
        public float GetMultiplier(
            CharacterInstance character,
            GameplayVariableType variableType)
        {
            if (character?.PassiveEffects == null)
            {
                return 1f;
            }

            var additivePercent = 0f;
            var multiplicativePercent = 1f;

            foreach (var effect in character.PassiveEffects)
            {
                var payload = effect.GetPayload<VariableModifiersPayload>();
                if (!PassiveEffectLifecycleSystem.IsActive(effect)
                    || payload == null)
                {
                    continue;
                }

                foreach (var modifier in payload.Modifiers)
                {
                    if (modifier == null
                        || modifier.VariableType != variableType)
                    {
                        continue;
                    }

                    if (modifier.Stacking == StatModifierStacking.Multiplicative)
                    {
                        multiplicativePercent *= 1f + modifier.Value;
                        continue;
                    }

                    additivePercent += modifier.Value;
                }
            }

            return Mathf.Max(
                0f,
                (1f + additivePercent) * multiplicativePercent);
        }

        public int ApplyDamageModifiers(
            BattleUnit source,
            BattleUnit target,
            int requestedDamage)
        {
            if (requestedDamage <= 0)
            {
                return 0;
            }

            var dealtMultiplier = GetMultiplier(
                source?.Character,
                GameplayVariableType.DamageDealtPercent);

            var receivedMultiplier = GetMultiplier(
                target?.Character,
                GameplayVariableType.DamageReceivedPercent);

            return Mathf.Max(
                0,
                Mathf.RoundToInt(
                    requestedDamage * dealtMultiplier * receivedMultiplier));
        }
    }
}
