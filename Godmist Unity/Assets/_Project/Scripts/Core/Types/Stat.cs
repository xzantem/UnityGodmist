using System;

namespace Godmist.Core.Types
{
    [Serializable]
    public struct Stat
    {
        public float BaseValue;

        public Stat(float baseValue)
        {
            BaseValue = baseValue;
        }

        public float GetValue()
        {
            return BaseValue;
        }
    }
}