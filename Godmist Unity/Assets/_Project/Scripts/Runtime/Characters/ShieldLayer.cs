using System;

namespace Godmist.Runtime.Characters
{
    [Serializable]
    public class ShieldLayer
    {
        public string SourceId { get; }
        public int CurrentValue { get; private set; }

        public ShieldLayer(int value, string sourceId = null)
        {
            CurrentValue = Math.Max(0, value);
            SourceId = sourceId;
        }

        public int Absorb(int incomingDamage)
        {
            if (incomingDamage <= 0 || CurrentValue <= 0) return 0;

            var absorbedDamage = Math.Min(CurrentValue, incomingDamage);

            CurrentValue -= absorbedDamage;

            return absorbedDamage;
        }
    }
}