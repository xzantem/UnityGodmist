using System;

namespace Godmist.Core.Utilities
{
    /// <summary>
    /// A partially refreshed uniform phase. For a predetermined probability sequence,
    /// each marginal success chance equals its input; outcomes are not independent.
    /// The caller owns lifetime, eligibility and isolation between gameplay sources.
    /// </summary>
    public sealed class ContinuousChanceStream
    {
        private readonly Random random;
        private double phase;

        public ContinuousChanceStream(int seed)
        {
            random = new Random(seed);
            phase = random.NextDouble();
        }

        public bool Roll(double chance)
        {
            if (double.IsNaN(chance) || chance < 0 || chance > 1)
                throw new ArgumentOutOfRangeException(nameof(chance));
            // Guaranteed and disabled outcomes do not consume phase or randomness.
            if (chance == 0) return false;
            if (chance == 1) return true;
            if (random.NextDouble() < 0.5) phase = random.NextDouble();
            phase += chance;
            if (phase < 1) return false;
            phase -= 1;
            return true;
        }
    }
}
