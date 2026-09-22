using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class CombatRecoverySystem
    {
        private readonly Action<string> battleLog;
        private readonly CombatFeedbackBus feedbackBus;

        public CombatRecoverySystem(
            Action<string> battleLog = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.battleLog = battleLog;
            this.feedbackBus = feedbackBus;
        }

        public int Heal(BattleUnit target, int requestedAmount, PassiveEffectCombatContext context = null)
        {
            if (target?.Character == null || target.Character.LifeState == CharacterLifeState.Dead || requestedAmount <= 0) return 0;

            var stats = target.Character.Stats;

            if (stats == null) return 0;

            var maxHealth = Mathf.Max(0, Mathf.RoundToInt(stats.MaxHealth.GetValue()));

            if (maxHealth <= 0) return 0;

            var previousHealth = target.Character.CurrentHealth;

            target.Character.CurrentHealth = Mathf.Min(maxHealth, previousHealth + requestedAmount);

            var restoredHealth = target.Character.CurrentHealth - previousHealth;

            if (restoredHealth <= 0) return 0;

            if (target.Character.LifeState == CharacterLifeState.Downed)
            {
                target.Character.LifeState = CharacterLifeState.Alive;
                target.DownedTurnsRemaining = 0;

                battleLog?.Invoke($"{target.Character.Definition.DisplayName} zostaje podniesiony i odzyskuje {restoredHealth} HP.");
                feedbackBus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.Heal, restoredHealth));

                context?.Session?.PassiveEffectEventDispatcher?.Publish(PassiveEffectTriggerType.OnHeal,
                    context.WithOutcome(succeeded: true, eventValue: restoredHealth));
                return restoredHealth;
            }

            battleLog?.Invoke($"{target.Character.Definition.DisplayName} odzyskuje {restoredHealth} HP.");
            feedbackBus?.Publish(new CombatFeedbackEvent(target, CombatFeedbackType.Heal, restoredHealth));

            context?.Session?.PassiveEffectEventDispatcher?.Publish(PassiveEffectTriggerType.OnHeal,
                context.WithOutcome(succeeded: true, eventValue: restoredHealth));
            return restoredHealth;
        }

        public int RestoreResource(BattleUnit target, int requestedAmount)
        {
            if (target?.Character == null || target.Character.LifeState != CharacterLifeState.Alive 
                || target.Character.ResourcePool == null || requestedAmount <= 0) return 0;

            // A resource-regeneration block means no positive resource gain from
            // any combat source: rules, skills, drains and summoned objects.
            // It deliberately does not affect spending or draining the resource.
            if (target.Character.PassiveEffects != null
                && target.Character.PassiveEffects.Exists(effect =>
                    PassiveEffectLifecycleSystem.IsActive(effect)
                    && effect.GetPayload<ResourceRegenerationBlockPayload>() != null))
            {
                return 0;
            }

            var restoredAmount =target.Character.ResourcePool.Restore(requestedAmount);

            if (restoredAmount > 0)
            {
                battleLog?.Invoke($"{target.Character.Definition.DisplayName} odzyskuje {restoredAmount} zasobu.");
                feedbackBus?.Publish(new CombatFeedbackEvent(
                    target,
                    CombatFeedbackType.ResourceGain,
                    restoredAmount,
                    resourceType: target.Character.ResourcePool.ResourceType));
            }

            return restoredAmount;
        }
    }
}
