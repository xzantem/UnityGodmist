using System;

namespace Godmist.Runtime.Characters
{
    [Serializable]
    public class ShieldLayer
    {
        public string SourceId { get; }
        public int CurrentValue { get; private set; }
        // Zero is untimed, preserving the lifetime of existing shield sources.
        public int RemainingTurns { get; private set; }

        public ShieldLayer(int value, string sourceId = null, int remainingTurns = 0)
        {
            CurrentValue = Math.Max(0, value);
            SourceId = sourceId;
            RemainingTurns = Math.Max(0, remainingTurns);
        }

        public bool AdvanceTurn() => RemainingTurns > 0 && --RemainingTurns == 0;

        public int Absorb(int incomingDamage)
        {
            if (incomingDamage <= 0 || CurrentValue <= 0) return 0;

            var absorbedDamage = Math.Min(CurrentValue, incomingDamage);

            CurrentValue -= absorbedDamage;

            return absorbedDamage;
        }
    }
}
