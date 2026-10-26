using Godmist.Runtime.Characters;

namespace Godmist.Systems.Combat
{
    public interface IResourceRule
    {
        int ProcessTurnStart(ResourcePool pool);

        int ProcessMovement(
            ResourcePool pool,
            int spentActionPoints);

        int ProcessOffensiveSkillUsed(ResourcePool pool);
    }
}
