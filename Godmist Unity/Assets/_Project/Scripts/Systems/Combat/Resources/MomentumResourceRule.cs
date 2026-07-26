using System;
using Godmist.Runtime.Characters;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class MomentumResourceRule : IResourceRule
    {
        private const int HighMomentumThreshold = 100;
        private const int MomentumPerSpentActionPoint = 2;

        public int ProcessTurnStart(ResourcePool pool)
        {
            return 0;
        }

        public int ProcessMovement(
            ResourcePool pool,
            int spentActionPoints)
        {
            if (pool == null || spentActionPoints <= 0)
            {
                return 0;
            }

            var rawGain = spentActionPoints
                * MomentumPerSpentActionPoint;

            var normalGain = 0;

            if (pool.CurrentValue < HighMomentumThreshold)
            {
                normalGain = Math.Min(
                    rawGain,
                    HighMomentumThreshold - pool.CurrentValue);

                rawGain -= normalGain;
            }

            var effectiveGain = normalGain
                + rawGain * 0.5f
                + pool.FractionalGainRemainder;

            var wholeGain = Mathf.FloorToInt(effectiveGain);

            pool.FractionalGainRemainder =
                effectiveGain - wholeGain;

            var restored = pool.Restore(wholeGain);

            if (pool.CurrentValue >= pool.MaxValue)
            {
                pool.FractionalGainRemainder = 0f;
            }

            return restored;
        }

        public int ProcessDamageDealt(ResourcePool pool)
        {
            return 0;
        }
    }
}