using Godmist.Runtime.Characters;
using Godmist.Data.Configuration;

namespace Godmist.Systems.Combat
{
    public class RageResourceRule : IResourceRule
    {
        private static int RagePerHit => GameDefinesRuntime.GetInt(
            GameDefineIds.RagePerHit, 10);
        private static int RageDecayPerMissedTurn => GameDefinesRuntime.GetInt(
            GameDefineIds.RageDecayPerMissedTurn, 10);

        public int ProcessTurnStart(ResourcePool pool)
        {
            if (pool == null)
            {
                return 0;
            }

            if (!pool.HasStartedTurn)
            {
                pool.HasStartedTurn = true;
                pool.HasGainedResourceSinceLastTurn = false;

                return 0;
            }

            var lostRage = 0;

            if (!pool.HasGainedResourceSinceLastTurn)
            {
                lostRage = pool.Drain(RageDecayPerMissedTurn);
            }

            pool.HasGainedResourceSinceLastTurn = false;

            return -lostRage;
        }

        public int ProcessMovement(
            ResourcePool pool,
            int spentActionPoints)
        {
            return 0;
        }

        public int ProcessDamageDealt(ResourcePool pool)
        {
            if (pool == null)
            {
                return 0;
            }

            // Even at max Rage, a successful hit prevents next-turn decay.
            pool.HasGainedResourceSinceLastTurn = true;

            return pool.Restore(RagePerHit);
        }
    }
}
