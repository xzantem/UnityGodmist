using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatShieldSystem
    {
        private readonly Action<string> battleLog;
        private readonly CombatFeedbackBus feedbackBus;

        public CombatShieldSystem(
            Action<string> battleLog = null,
            CombatFeedbackBus feedbackBus = null)
        {
            this.battleLog = battleLog;
            this.feedbackBus = feedbackBus;
        }

        public int GrantShield(
            BattleUnit target,
            int requestedAmount,
            string sourceId = null)
        {
            if (target?.Character == null || target.Character.LifeState != CharacterLifeState.Alive 
                || requestedAmount <= 0) return 0;

            target.Character.ShieldPool ??= new ShieldPool();
            target.Character.ShieldPool.AddLayer(requestedAmount, sourceId);

            battleLog?.Invoke($"{target.Character.Definition.DisplayName} otrzymuje {requestedAmount} tarczy.");
            feedbackBus?.Publish(new CombatFeedbackEvent(
                target, CombatFeedbackType.ShieldGain, requestedAmount));

            return requestedAmount;
        }
    }
}
