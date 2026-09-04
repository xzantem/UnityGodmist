using System;
using Godmist.Data.Configuration;
using Godmist.Runtime.NPC;

namespace Godmist.Systems.NPC
{
    /// <summary>
    /// Applies cumulative gold spending at an NPC to its persistent loyalty.
    /// Thresholds are read from GameDefines so balance stays data-driven.
    /// </summary>
    public sealed class NpcLoyaltyService
    {
        public int InitialLevel
        {
            get
            {
                var maximum = MaximumLevel;
                var configured = GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyInitialLevel, 1);
                return Math.Max(1, Math.Min(maximum, configured));
            }
        }

        public int MaximumLevel => Math.Max(1,
            GameDefinesRuntime.GetInt(GameDefineIds.NpcLoyaltyMaximumLevel, 15));

        public int GetGoldCostForNextLevel(int currentLevel)
        {
            if (currentLevel < InitialLevel || currentLevel >= MaximumLevel) return 0;

            var baseValue = Math.Max(.0001d,
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyThresholdBase, 4f));
            var exponentOffset = GameDefinesRuntime.GetFloat(
                GameDefineIds.NpcLoyaltyThresholdExponentOffset, 4f);
            var levelDivisor = Math.Max(.0001d,
                GameDefinesRuntime.GetFloat(GameDefineIds.NpcLoyaltyThresholdLevelDivisor, 3f));
            var exponent = currentLevel / levelDivisor + exponentOffset;
            var result = Math.Ceiling(Math.Pow(baseValue, exponent));

            if (double.IsNaN(result) || result <= 0d) return 1;
            if (double.IsInfinity(result) || result >= int.MaxValue) return int.MaxValue;
            return Math.Max(1, (int)result);
        }

        public long GetGoldRequiredToReachLevel(int targetLevel)
        {
            if (targetLevel <= InitialLevel) return 0;

            var clampedTarget = Math.Min(targetLevel, MaximumLevel);
            long total = 0;
            for (var level = InitialLevel; level < clampedTarget; level++)
            {
                total = Math.Min(int.MaxValue,
                    total + GetGoldCostForNextLevel(level));
            }

            return total;
        }

        public long GetGoldSpentTowardNextLevel(NpcInstance npc)
        {
            if (npc == null || npc.SpentGoldTotal < 0) return 0;

            var currentLevel = NormalizeLevel(npc.LoyaltyLevel);
            if (currentLevel >= MaximumLevel) return 0;

            var spentAtCurrentLevel = (long)npc.SpentGoldTotal
                - GetGoldRequiredToReachLevel(currentLevel);
            return Math.Max(0, spentAtCurrentLevel);
        }

        public int GetGoldRequiredForNextLevel(NpcInstance npc) =>
            npc == null ? 0 : GetGoldCostForNextLevel(NormalizeLevel(npc.LoyaltyLevel));

        public bool TryRecordGoldSpent(NpcInstance npc, int amount)
        {
            if (npc == null || amount <= 0 || npc.SpentGoldTotal < 0) return false;

            var total = Math.Min(int.MaxValue, (long)npc.SpentGoldTotal + amount);
            npc.SpentGoldTotal = (int)total;

            var currentLevel = NormalizeLevel(npc.LoyaltyLevel);
            while (currentLevel < MaximumLevel
                && total >= GetGoldRequiredToReachLevel(currentLevel + 1))
            {
                currentLevel++;
            }

            npc.LoyaltyLevel = currentLevel;
            return true;
        }

        private int NormalizeLevel(int level) =>
            Math.Max(InitialLevel, Math.Min(MaximumLevel, level));
    }
}
