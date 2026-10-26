using Godmist.Runtime.Characters;
using UnityEngine;

namespace Godmist.Systems.Combat
{
    public class ManaResourceRule : IResourceRule
    {
        public int ProcessTurnStart(ResourcePool pool)
        {
            if (pool == null)
            {
                return 0;
            }

            return pool.Restore(pool.Regeneration);
        }

        public int ProcessMovement(
            ResourcePool pool,
            int spentActionPoints)
        {
            return 0;
        }

        public int ProcessTurnEnd(ResourcePool pool, int remainingActionPoints, int maxActionPoints)
        {
            if (pool == null || maxActionPoints <= 0) return 0;
            var remaining = Mathf.Clamp01(remainingActionPoints / (float)maxActionPoints);
            return pool.Restore(Mathf.Max(0, Mathf.RoundToInt(pool.Regeneration * RegenerationMultiplier(remaining))));
        }

        // Retain some recovery after a fully spent turn without rewarding a
        // half-spent turn more than the configured base regeneration.
        public static float RegenerationMultiplier(float remainingFraction)
        {
            var remaining = Mathf.Clamp01(remainingFraction);
            return remaining <= .5f ? Mathf.Lerp(.25f, 1f, remaining * 2f)
                : Mathf.Lerp(1f, 2f, (remaining - .5f) * 2f);
        }

        public int ProcessOffensiveSkillUsed(ResourcePool pool)
        {
            return 0;
        }
    }
}
