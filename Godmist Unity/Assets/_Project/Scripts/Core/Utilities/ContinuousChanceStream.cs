using System;
using System.Collections.Generic;

namespace Godmist.Core.Utilities
{
    /// <summary>
    /// A stratified threshold bag, sized automatically for two rarer outcomes.
    /// For a predetermined probability sequence, each marginal success chance equals
    /// its input. This does not guarantee unbiased outcome-dependent probabilities.
    /// The caller owns lifetime, eligibility and isolation between gameplay sources.
    /// </summary>
    public sealed class ContinuousChanceStream
    {
        private readonly Random random;
        // Sparse Fisher-Yates: do not allocate a huge array for very rare chances.
        private readonly Dictionary<int, int> swaps = new();
        private int remaining;
        private int size;

        public int RemainingCount => remaining;
        public int PoolSize => size;

        public ContinuousChanceStream(int seed)
        {
            random = new Random(seed);
        }

        public bool Roll(double chance)
        {
            if (double.IsNaN(chance) || chance < 0 || chance > 1)
                throw new ArgumentOutOfRangeException(nameof(chance));
            // Guaranteed and disabled outcomes do not consume the bag or randomness.
            if (chance == 0) return false;
            if (chance == 1) return true;
            if (remaining == 0)
            {
                size = GetPoolSize(chance);
                remaining = size;
                swaps.Clear();
            }

            var slot = random.Next(remaining);
            var stratum = swaps.TryGetValue(slot, out var mapped) ? mapped : slot;
            var last = remaining - 1;
            if (slot != last)
                swaps[slot] = swaps.TryGetValue(last, out var tail) ? tail : last;
            swaps.Remove(last);
            remaining--;
            // Jitter is generated when drawn, equivalent in distribution to assigning
            // independent jitter before shuffling, without storing unused thresholds.
            return (stratum + random.NextDouble()) / size < chance;
        }

        private static int GetPoolSize(double chance)
        {
            var requested = 2 / Math.Min(chance, 1 - chance);
            // Technical index limit only (p below ~9.32e-10). It affects smoothing,
            // not the uniform marginal threshold, and avoids overflow/infinite sizes.
            if (requested >= int.MaxValue) return int.MaxValue;
            var nearest = Math.Round(requested);
            // Unity chances are floats promoted to doubles. Stabilize only the bag
            // size near integral boundaries (80% -> 10), never the success threshold.
            if (Math.Abs(requested - nearest) <= 1e-6 * requested)
                requested = nearest;
            return (int)Math.Ceiling(requested);
        }
    }
}
