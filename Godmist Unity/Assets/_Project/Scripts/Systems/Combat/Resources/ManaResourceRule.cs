using Godmist.Runtime.Characters;

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

        public int ProcessDamageDealt(ResourcePool pool)
        {
            return 0;
        }
    }
}