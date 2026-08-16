using System;
using Godmist.Core.Enums;

namespace Godmist.Core.Types
{
    [Serializable]
    public class GameplayVariableModifier
    {
        public GameplayVariableType VariableType;
        public StatModifierStacking Stacking = StatModifierStacking.Additive;
        public float Value;
    }
}