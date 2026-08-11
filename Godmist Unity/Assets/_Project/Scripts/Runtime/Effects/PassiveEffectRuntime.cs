using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Core.Types;

namespace Godmist.Runtime.Effects
{
    [Serializable]
    public class PassiveEffectRuntime
    {
        public string EffectId = string.Empty;
        public List<string> SourceEntityIds = new();
        public int Stacks = 1;

        // False means a permanent passive or modifier.
        public bool IsTimed;
        public int RemainingDuration;
        public PassiveEffectTickPhase TickPhase = PassiveEffectTickPhase.None;
        public bool RemoveOnCombatEnd;

        public int TotalDamage;
        public List<StatModifier> StatModifiers = new();

        public PassiveEffectRuntime()
        {
        }

        public PassiveEffectRuntime(
            string effectId,
            List<string> sourceEntityIds,
            int stacks,
            int remainingDuration,
            int totalDamage,
            bool? isTimed = null,
            PassiveEffectTickPhase tickPhase = PassiveEffectTickPhase.None,
            bool removeOnCombatEnd = false)
        {
            EffectId = effectId ?? string.Empty;
            SourceEntityIds = sourceEntityIds != null
                ? new List<string>(sourceEntityIds)
                : new List<string>();

            Stacks = stacks;
            IsTimed = isTimed ?? remainingDuration > 0;
            RemainingDuration = remainingDuration;
            TickPhase = IsTimed ? tickPhase : PassiveEffectTickPhase.None;
            TotalDamage = totalDamage;
            RemoveOnCombatEnd = removeOnCombatEnd;
        }
    }
}