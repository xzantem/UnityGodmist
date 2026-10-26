using System;
using Godmist.Core.Enums;
using Godmist.Runtime.Characters;
using Godmist.Runtime.Combat;

namespace Godmist.Systems.Combat
{
    public class CombatShieldSystem
    {
        public const string MageShieldSourceId = "skill.mage.shield";
        public const int MageShieldDuration = 3;
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
            string sourceId = null,
            int durationTurns = 0,
            bool replaceSameSource = false)
        {
            if (target?.Character == null || target.Character.LifeState != CharacterLifeState.Alive 
                || requestedAmount <= 0) return 0;

            target.Character.ShieldPool ??= new ShieldPool();
            if (replaceSameSource) target.Character.ShieldPool.RemoveLayersBySourceId(sourceId);
            target.Character.ShieldPool.AddLayer(requestedAmount, sourceId, durationTurns);

            battleLog?.Invoke($"{target.Character.Definition.DisplayName} otrzymuje {requestedAmount} tarczy.");
            feedbackBus?.Publish(new CombatFeedbackEvent(
                target, CombatFeedbackType.ShieldGain, requestedAmount));

            return requestedAmount;
        }
    }
}
