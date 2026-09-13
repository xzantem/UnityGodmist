using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    public sealed class GalduryteEffectDefinition
    {
        public GalduryteEffectDefinition(
            GalduryteEffectType effectType,
            GaldurytePowderColor powderColor)
        {
            EffectType = effectType;
            PowderColor = powderColor;
        }

        public GalduryteEffectType EffectType { get; }
        public GaldurytePowderColor PowderColor { get; }
    }

    /// <summary>
    /// Centralizes the rules for the first galduryte content slice. The pools
    /// intentionally contain only three unique colors per target, so tiers 2
    /// and 3 fail explicitly until their additional effects are authored.
    /// </summary>
    public static class GalduryteRuntimeRules
    {
        private static readonly IReadOnlyList<GalduryteEffectDefinition> WeaponPool =
            new List<GalduryteEffectDefinition>
            {
                new(GalduryteEffectType.GeneralDamagePercent, GaldurytePowderColor.Red),
                new(GalduryteEffectType.CriticalChancePercent, GaldurytePowderColor.Blue),
                new(GalduryteEffectType.AccuracyPercent, GaldurytePowderColor.Purple)
            };

        private static readonly IReadOnlyList<GalduryteEffectDefinition> ArmorPool =
            new List<GalduryteEffectDefinition>
            {
                new(GalduryteEffectType.MaxHealthPercent, GaldurytePowderColor.Red),
                new(GalduryteEffectType.PhysicalDefensePercent, GaldurytePowderColor.Blue),
                new(GalduryteEffectType.SpeedPercent, GaldurytePowderColor.Purple)
            };

        public static int GetLevel(GalduryteTier tier) => tier switch
        {
            GalduryteTier.Limestone => 11,
            GalduryteTier.Granite => 21,
            GalduryteTier.Marble => 41,
            _ => 0
        };

        public static bool TryGetEffectCountRange(
            int itemLevel,
            out int minimum,
            out int maximum)
        {
            if (itemLevel >= 11 && itemLevel <= 20)
            {
                minimum = 2;
                maximum = 3;
                return true;
            }

            if (itemLevel >= 21 && itemLevel <= 40)
            {
                minimum = 3;
                maximum = 4;
                return true;
            }

            if (itemLevel >= 41 && itemLevel <= 50)
            {
                minimum = 5;
                maximum = 6;
                return true;
            }

            minimum = 0;
            maximum = 0;
            return false;
        }

        public static IReadOnlyList<GalduryteEffectDefinition> GetEffectPool(
            GalduryteTargetType targetType) => targetType switch
            {
                GalduryteTargetType.Weapon => WeaponPool,
                GalduryteTargetType.Armor => ArmorPool,
                _ => Array.Empty<GalduryteEffectDefinition>()
            };

        public static bool TryRollRarity(
            Random random,
            out RarityTier rarity)
        {
            rarity = RarityTier.Common;
            if (random == null) return false;

            var roll = random.NextDouble();
            var cumulative = 0d;
            foreach (var entry in RarityWeights)
            {
                cumulative += entry.Weight;
                if (roll < cumulative)
                {
                    rarity = entry.Rarity;
                    return true;
                }
            }

            rarity = RarityTier.Godly;
            return true;
        }

        public static GalduryteEffectGrade RollGrade(
            RarityTier rarity,
            Random random)
        {
            if (random == null) return GalduryteEffectGrade.D;

            var roll = random.NextDouble();
            var thresholds = rarity switch
            {
                RarityTier.Common => new[] { .50d, 1d },
                RarityTier.Uncommon => new[] { .25d, .75d, 1d },
                RarityTier.Rare => new[] { .50d, .75d, 1d },
                RarityTier.Ancient => new[] { .25d, .75d, 1d },
                RarityTier.Legendary => new[] { .50d, .75d, 1d },
                RarityTier.Mythic => new[] { .25d, .75d, 1d },
                RarityTier.Godly => new[] { .50d, 1d },
                _ => new[] { 1d }
            };
            var grades = rarity switch
            {
                RarityTier.Common => new[] { GalduryteEffectGrade.D, GalduryteEffectGrade.C },
                RarityTier.Uncommon => new[] { GalduryteEffectGrade.D, GalduryteEffectGrade.C, GalduryteEffectGrade.B },
                RarityTier.Rare => new[] { GalduryteEffectGrade.C, GalduryteEffectGrade.B, GalduryteEffectGrade.A },
                RarityTier.Ancient => new[] { GalduryteEffectGrade.C, GalduryteEffectGrade.B, GalduryteEffectGrade.A },
                RarityTier.Legendary => new[] { GalduryteEffectGrade.B, GalduryteEffectGrade.A, GalduryteEffectGrade.S },
                RarityTier.Mythic => new[] { GalduryteEffectGrade.B, GalduryteEffectGrade.A, GalduryteEffectGrade.S },
                RarityTier.Godly => new[] { GalduryteEffectGrade.A, GalduryteEffectGrade.S },
                _ => new[] { GalduryteEffectGrade.D }
            };

            for (var index = 0; index < thresholds.Length; index++)
            {
                if (roll < thresholds[index]) return grades[index];
            }

            return grades[^1];
        }

        public static float GetEffectValue(
            GalduryteEffectType effectType,
            GalduryteEffectGrade grade)
        {
            var index = Math.Max(0, Math.Min(4, (int)grade));
            var values = effectType switch
            {
                GalduryteEffectType.PhysicalDefensePercent => new[] { .03f, .036f, .042f, .048f, .06f },
                GalduryteEffectType.CriticalChancePercent => new[] { .01f, .014f, .018f, .022f, .03f },
                _ => new[] { .02f, .026f, .032f, .038f, .05f }
            };
            return values[index];
        }

        public static bool TryRollEffects(
            GalduryteTargetType targetType,
            GaldurytePowderColor guaranteedColor,
            RarityTier rarity,
            int itemLevel,
            Random random,
            out List<GalduryteEffectInstance> effects,
            out string failureReason)
        {
            effects = new List<GalduryteEffectInstance>();
            failureReason = string.Empty;
            if (random == null)
            {
                failureReason = "Brak generatora losowego galdurytu.";
                return false;
            }

            if (!TryGetEffectCountRange(itemLevel, out var minimum, out var maximum))
            {
                failureReason = $"Poziom galdurytu {itemLevel} nie ma skonfigurowanej liczby efektów.";
                return false;
            }

            var pool = GetEffectPool(targetType);
            var availableColors = pool.Select(effect => effect.PowderColor).Distinct().Count();
            // A tier is supported only when the complete configured range can be
            // generated. Otherwise a tier 2/3 roll could randomly choose its
            // lower bound and silently produce a partial v1 galduryte.
            if (pool.Count < maximum || availableColors < maximum)
            {
                failureReason =
                    $"Pula efektów dla galdurytu {targetType} zawiera tylko {availableColors} "
                    + $"unikalne efekty, a poziom {itemLevel} wymaga zakresu {minimum}-{maximum}.";
                return false;
            }

            var effectCount = random.Next(minimum, maximum + 1);

            var guaranteed = pool.FirstOrDefault(effect => effect.PowderColor == guaranteedColor);
            if (guaranteed == null)
            {
                failureReason = $"Proszek {guaranteedColor} nie ma efektu dla celu {targetType}.";
                return false;
            }

            effects.Add(CreateEffect(guaranteed, rarity, random));
            var selectedColors = new HashSet<GaldurytePowderColor> { guaranteed.PowderColor };

            var candidates = pool
                .Where(effect => !selectedColors.Contains(effect.PowderColor))
                .OrderBy(_ => random.Next())
                .Where(effect => selectedColors.Add(effect.PowderColor))
                .Take(effectCount - 1)
                .ToList();

            if (candidates.Count != effectCount - 1)
            {
                effects.Clear();
                failureReason = "Nie udało się wybrać wymaganej liczby różnych puli efektów.";
                return false;
            }

            effects.AddRange(candidates.Select(effect => CreateEffect(effect, rarity, random)));
            return true;
        }

        public static bool CanGenerateEffects(
            GalduryteTargetType targetType,
            GaldurytePowderColor guaranteedColor,
            int itemLevel,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (!TryGetEffectCountRange(itemLevel, out _, out var maximum))
            {
                failureReason = $"Poziom galdurytu {itemLevel} nie ma skonfigurowanej liczby efektów.";
                return false;
            }

            var pool = GetEffectPool(targetType);
            var availableColors = pool.Select(effect => effect.PowderColor).Distinct().Count();
            if (pool.Count < maximum || availableColors < maximum)
            {
                failureReason =
                    $"Pula efektów dla galdurytu {targetType} zawiera tylko {availableColors} "
                    + $"unikalne efekty, a poziom {itemLevel} wymaga pełnego zakresu efektów do {maximum}.";
                return false;
            }

            if (!pool.Any(effect => effect.PowderColor == guaranteedColor))
            {
                failureReason = $"Proszek {guaranteedColor} nie ma efektu dla celu {targetType}.";
                return false;
            }

            return true;
        }

        public static string GetStoneName(GalduryteTier tier) => tier switch
        {
            GalduryteTier.Limestone => "Wapienny",
            GalduryteTier.Granite => "Granitowy",
            GalduryteTier.Marble => "Marmurowy",
            _ => "Nieznany"
        };

        public static string GetPowderColorName(GaldurytePowderColor color) => color switch
        {
            GaldurytePowderColor.Red => "Czerwony",
            GaldurytePowderColor.Blue => "Niebieski",
            GaldurytePowderColor.Purple => "Purpurowy",
            GaldurytePowderColor.Lemon => "Cytrynowy",
            GaldurytePowderColor.Emerald => "Szmaragdowy",
            GaldurytePowderColor.Black => "Czarny",
            GaldurytePowderColor.Pink => "Różowy",
            GaldurytePowderColor.White => "Biały",
            GaldurytePowderColor.Gold => "Złoty",
            GaldurytePowderColor.Amber => "Bursztynowy",
            _ => "Nieznany"
        };

        public static string GetRarityName(RarityTier rarity) => rarity switch
        {
            RarityTier.Common => "Pospolity",
            RarityTier.Uncommon => "Niepospolity",
            RarityTier.Rare => "Rzadki",
            RarityTier.Ancient => "Starożytny",
            RarityTier.Legendary => "Legendarny",
            RarityTier.Mythic => "Mityczny",
            RarityTier.Godly => "Boski",
            _ => rarity.ToString()
        };

        private static GalduryteEffectInstance CreateEffect(
            GalduryteEffectDefinition definition,
            RarityTier rarity,
            Random random)
        {
            var grade = RollGrade(rarity, random);
            return new GalduryteEffectInstance(
                definition.EffectType,
                definition.PowderColor,
                grade,
                GetEffectValue(definition.EffectType, grade));
        }

        private static readonly (RarityTier Rarity, double Weight)[] RarityWeights =
        {
            (RarityTier.Common, .35d),
            (RarityTier.Uncommon, .25d),
            (RarityTier.Rare, .15d),
            (RarityTier.Ancient, .10d),
            (RarityTier.Legendary, .075d),
            (RarityTier.Mythic, .05d),
            (RarityTier.Godly, .025d)
        };
    }
}
