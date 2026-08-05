using System;
using Godmist.Core.Enums;

namespace Godmist.Core.Types
{
    [Serializable]
    public class StatModifier
    {
        public CharacterStatType StatType;
        public StatModifierOperation Operation;
        public StatModifierStacking Stacking = StatModifierStacking.Additive;
        public float Value;
    }
}