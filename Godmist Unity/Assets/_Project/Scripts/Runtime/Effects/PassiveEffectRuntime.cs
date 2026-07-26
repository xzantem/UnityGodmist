using System;
using System.Collections.Generic;

namespace Godmist.Runtime.Effects
{
    [Serializable]
    public class PassiveEffectRuntime
    {
        public string EffectId = string.Empty;
        public List<string> SourceEntityIds = new();
        public int Stacks = 1;
        public int RemainingDuration;
        public int TotalDamage;

        public PassiveEffectRuntime()
        {
        }

        public PassiveEffectRuntime(
            string effectId,
            List<string> sourceEntityIds,
            int stacks,
            int remainingDuration,
            int totalDamage)
        {
            EffectId = effectId ?? string.Empty;
            SourceEntityIds = sourceEntityIds != null
                ? new List<string>(sourceEntityIds)
                : new List<string>();

            Stacks = stacks;
            RemainingDuration = remainingDuration;
            TotalDamage = totalDamage;
        }
    }
}