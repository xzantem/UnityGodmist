using Godmist.Data.Configuration;
using UnityEngine;

namespace Godmist.Systems.NPC
{
    public static class HonorPriceSystem
    {
        private static readonly string[] TierNames =
        { "Wygnaniec", "Haniebny", "Niechlubny", "Podejrzany", "Rekrut", "Najemnik", "Bojownik", "Rycerz", "Przywódca" };

        public static string GetTierName(int honor)
        {
            var tier = GetTierIndex(honor);
            return tier < TierNames.Length ? TierNames[tier] : $"Tier {tier + 1}";
        }

        public static int GetTierIndex(int honor)
        {
            var count = Mathf.Clamp(GameDefinesRuntime.GetInt("economy.honor.tier_count", 9), 1, 20);
            var selected = 0;
            for (var tier = 1; tier < count; tier++)
                if (honor >= GameDefinesRuntime.GetInt($"economy.honor.tier_{tier}.minimum", int.MaxValue))
                    selected = tier;
            return selected;
        }

        public static float GetDiscountPercent(int honor)
        {
            var tier = GetTierIndex(honor);
            return Mathf.Clamp(GameDefinesRuntime.GetFloat($"economy.honor.tier_{tier}.discount_percent", 0f), 0f, 100f);
        }
    }
}
