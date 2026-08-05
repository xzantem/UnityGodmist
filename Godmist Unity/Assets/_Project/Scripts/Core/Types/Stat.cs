using System;

namespace Godmist.Core.Types
{
    [Serializable]
    public struct Stat
    {
        public float BaseValue;

        [NonSerialized] private float effectiveValue;
        [NonSerialized] private bool hasEffectiveValue;

        public Stat(float baseValue)
        {
            BaseValue = baseValue;
            effectiveValue = baseValue;
            hasEffectiveValue = false;
        }

        public float GetValue()
        {
            return hasEffectiveValue ? effectiveValue : BaseValue;
        }

        public void SetEffectiveValue(float value)
        {
            effectiveValue = value;
            hasEffectiveValue = true;
        }

        public void ResetEffectiveValue()
        {
            effectiveValue = BaseValue;
            hasEffectiveValue = false;
        }
    }
}