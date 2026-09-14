using System;
using Godmist.Data.Configuration;
namespace Godmist.Systems.Combat
{
    public static class CombatEconomyScaling
    {
        public static int RollEnemyGold(int level, double sample)
        {
            var baseline = Scale(GameDefinesRuntime.GetInt(GameDefineIds.EnemyGoldBase, 10), level);
            var minimum = (long)Math.Ceiling(baseline * .5d);
            var maximum = Math.Min(int.MaxValue, (long)Math.Floor(baseline * 1.5d));
            var offset = (long)Math.Floor(Math.Clamp(sample, 0d, 1d) * (maximum - minimum + 1));
            return (int)Math.Min(maximum, minimum + offset);
        }

        public static int Scale(int baseGold, int level)
        {
            if (baseGold <= 0) return 0;
            var result = Math.Ceiling(Math.Max(0, baseGold) * Math.Pow(Math.Max(1d, GameDefinesRuntime.GetFloat(GameDefineIds.CombatEconomyGrowthBase, 4f)), (Math.Max(1, level) - 1) / Math.Max(1d, GameDefinesRuntime.GetFloat(GameDefineIds.CombatEconomyLevelSpan, 10f))));
            return double.IsInfinity(result) || result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }
}
