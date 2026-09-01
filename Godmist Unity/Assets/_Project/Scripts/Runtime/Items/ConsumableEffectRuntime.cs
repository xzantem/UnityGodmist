using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public sealed class ConsumableEffectRuntime
    {
        public ConsumableRuntimeEffectType EffectType;
        public float Value;
        public int DurationTurns;
        public float Chance = 1f;
        public int TargetModeIndex;
        public string StatusId = string.Empty;
        public ResistanceType ResistanceType = ResistanceType.Weakening;
        public TargetType TargetType = TargetType.Caster;
        public TargetingType TargetingType = TargetingType.Allies;
    }
}
