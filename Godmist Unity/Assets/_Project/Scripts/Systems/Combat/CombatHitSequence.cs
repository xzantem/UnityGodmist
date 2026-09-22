using System;
namespace Godmist.Systems.Combat
{
    public static class CombatHitSequence
    {
        // Each additional slot gets its own roll, including after a failed roll.
        public static int RollHitCount(int baseHits, int additionalTrials, float chance, Func<float> roll)
        {
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            var hits = Math.Max(1, baseHits);
            for (var i = 0; i < Math.Max(0, additionalTrials); i++)
                if (chance >= 1f || chance > 0f && roll() < chance) hits++;
            return hits;
        }
    }
}
