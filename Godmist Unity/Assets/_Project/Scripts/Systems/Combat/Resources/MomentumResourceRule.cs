using System;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Characters;
using Godmist.Data.Configuration;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class MomentumResourceRule : IResourceRule
    {
        private static int HighMomentumThreshold => GameDefinesRuntime.GetInt(
            GameDefineIds.MomentumHighThreshold, 100);
        private static int MomentumPerSpentActionPoint => GameDefinesRuntime.GetInt(
            GameDefineIds.MomentumPerSpentActionPoint, 2);

        public static int SpeedBonus(ResourcePool pool, int value) =>
            pool?.ResourceType == Godmist.Core.Enums.ResourceType.Momentum
                ? Math.Max(0, value - HighMomentumThreshold) / 25 * 4 : 0;

        // Refresh only when crossing a speed step, not on every moved tile.
        public static void RefreshSpeedAfterChange(Godmist.Runtime.Characters.CharacterInstance character, int previous)
        {
            var pool = character?.ResourcePool;
            if (SpeedBonus(pool, previous) != SpeedBonus(pool, pool?.CurrentValue ?? 0))
                new Godmist.Systems.Characters.CharacterStatPipelineSystem().Refresh(character);
        }

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

            var rawGain = ActionPointUnits.ToPoints(spentActionPoints)
                * MomentumPerSpentActionPoint;

            return RestoreMovementGain(pool, rawGain);
        }

        // The caller has already committed the cells and incremented its turn counter.
        public int ProcessMovementSteps(ResourcePool pool, int totalSteps, int completedSteps = 1)
        {
            if (pool == null || completedSteps <= 0 || totalSteps <= 0) return 0;
            var count = Math.Min(totalSteps, completedSteps);
            return RestoreMovementGain(pool, MovementGain(totalSteps, count));
        }

        public static float MovementGain(int totalSteps, int completedSteps)
            => (float)((double)completedSteps * (2d * totalSteps - completedSteps + 1d)
                * .5d * MomentumPerSpentActionPoint);

        private static int RestoreMovementGain(ResourcePool pool, float rawGain)
        {

            var normalGain = 0f;

            if (pool.CurrentValue < HighMomentumThreshold)
            {
                normalGain = Math.Min(
                    rawGain,
                    HighMomentumThreshold - pool.CurrentValue);

                rawGain -= normalGain;
            }

            var effectiveGain = normalGain
                + rawGain * GameDefinesRuntime.GetFloat(
                    GameDefineIds.MomentumHighThresholdEfficiency, .5f)
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

        public int ProcessOffensiveSkillUsed(ResourcePool pool)
        {
            return 0;
        }
    }
}
