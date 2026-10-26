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
    /// Centralizes the approved content-pass effect pools. Each color contributes
    /// at most one effect, selected uniformly within that color.
    /// </summary>
    public static class GalduryteRuntimeRules
    {
        private static readonly IReadOnlyList<GalduryteEffectDefinition> WeaponPool =
            new List<GalduryteEffectDefinition>
            {
                new(GalduryteEffectType.GeneralDamagePercent, GaldurytePowderColor.Red),
                new(GalduryteEffectType.PhysicalDamagePercent, GaldurytePowderColor.Red),
                new(GalduryteEffectType.MagicDamagePercent, GaldurytePowderColor.Red),
                new(GalduryteEffectType.CriticalChancePercent, GaldurytePowderColor.Blue),
                new(GalduryteEffectType.CriticalDamageFlat, GaldurytePowderColor.Blue),
                new(GalduryteEffectType.AccuracyPercent, GaldurytePowderColor.Purple),
                new(GalduryteEffectType.SuppressionChance, GaldurytePowderColor.Purple),
                new(GalduryteEffectType.DebuffChance, GaldurytePowderColor.Purple),
                new(GalduryteEffectType.DotChance, GaldurytePowderColor.Purple),
                new(GalduryteEffectType.BleedOnAttack, GaldurytePowderColor.Lemon),
                new(GalduryteEffectType.PoisonOnAttack, GaldurytePowderColor.Lemon),
                new(GalduryteEffectType.BurnOnAttack, GaldurytePowderColor.Lemon),
                new(GalduryteEffectType.HealOnAttack, GaldurytePowderColor.Emerald),
                new(GalduryteEffectType.ResourceOnAttack, GaldurytePowderColor.Emerald),
                new(GalduryteEffectType.ActionValueOnAttack, GaldurytePowderColor.Emerald),
                new(GalduryteEffectType.ItemDropChance, GaldurytePowderColor.Black),
                new(GalduryteEffectType.ResourceGenerationPercent, GaldurytePowderColor.Black),
                new(GalduryteEffectType.UndeadDamagePercent, GaldurytePowderColor.Pink),
                new(GalduryteEffectType.HumanDamagePercent, GaldurytePowderColor.Pink),
                new(GalduryteEffectType.BeastDamagePercent, GaldurytePowderColor.Pink),
                new(GalduryteEffectType.DemonDamagePercent, GaldurytePowderColor.Pink),
                new(GalduryteEffectType.StunOnAttack, GaldurytePowderColor.White),
                new(GalduryteEffectType.FreezeOnAttack, GaldurytePowderColor.White),
                new(GalduryteEffectType.SlowOnAttack, GaldurytePowderColor.White),
                new(GalduryteEffectType.ExperiencePercent, GaldurytePowderColor.Gold),
                new(GalduryteEffectType.GoldPercent, GaldurytePowderColor.Gold),
                new(GalduryteEffectType.PhysicalPenetration, GaldurytePowderColor.Amber),
                new(GalduryteEffectType.MagicPenetration, GaldurytePowderColor.Amber),
            };

        private static readonly IReadOnlyList<GalduryteEffectDefinition> ArmorPool =
            new List<GalduryteEffectDefinition>
            {
                new(GalduryteEffectType.MaxHealthPercent, GaldurytePowderColor.Red),
                new(GalduryteEffectType.DodgePercent, GaldurytePowderColor.Red),
                new(GalduryteEffectType.PhysicalDefensePercent, GaldurytePowderColor.Blue),
                new(GalduryteEffectType.MagicDefensePercent, GaldurytePowderColor.Blue),
                new(GalduryteEffectType.AllDefensePercent, GaldurytePowderColor.Blue),
                new(GalduryteEffectType.SpeedPercent, GaldurytePowderColor.Purple),
                new(GalduryteEffectType.ActionPointsPercent, GaldurytePowderColor.Purple),
                new(GalduryteEffectType.DotResistance, GaldurytePowderColor.Lemon),
                new(GalduryteEffectType.SuppressionResistance, GaldurytePowderColor.Lemon),
                new(GalduryteEffectType.DebuffResistance, GaldurytePowderColor.Lemon),
                new(GalduryteEffectType.AllResistance, GaldurytePowderColor.Lemon),
                new(GalduryteEffectType.PhysicalDamageReduction, GaldurytePowderColor.Emerald),
                new(GalduryteEffectType.MagicDamageReduction, GaldurytePowderColor.Emerald),
                new(GalduryteEffectType.AllDamageReduction, GaldurytePowderColor.Emerald),
                new(GalduryteEffectType.ItemDropChance, GaldurytePowderColor.Black),
                new(GalduryteEffectType.MaxResourcePercent, GaldurytePowderColor.Black),
                new(GalduryteEffectType.BleedDamageReduction, GaldurytePowderColor.Pink),
                new(GalduryteEffectType.PoisonDamageReduction, GaldurytePowderColor.Pink),
                new(GalduryteEffectType.BurnDamageReduction, GaldurytePowderColor.Pink),
                new(GalduryteEffectType.ResourceCostReduction, GaldurytePowderColor.White),
                new(GalduryteEffectType.CriticalAvoidance, GaldurytePowderColor.White),
                new(GalduryteEffectType.GoldPercent, GaldurytePowderColor.Gold),
                new(GalduryteEffectType.ExperiencePercent, GaldurytePowderColor.Gold),
                new(GalduryteEffectType.HealthPerTurn, GaldurytePowderColor.Amber),
                new(GalduryteEffectType.ResourcePerTurn, GaldurytePowderColor.Amber),
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
                GalduryteEffectType.GeneralDamagePercent => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.PhysicalDamagePercent => new[] { 0.03f, 0.036f, 0.042f, 0.048f, 0.06f },
                GalduryteEffectType.MagicDamagePercent => new[] { 0.03f, 0.036f, 0.042f, 0.048f, 0.06f },
                GalduryteEffectType.CriticalChancePercent => new[] { 0.01f, 0.014f, 0.018f, 0.022f, 0.03f },
                GalduryteEffectType.CriticalDamageFlat => new[] { 0.1f, 0.13f, 0.16f, 0.19f, 0.25f },
                GalduryteEffectType.AccuracyPercent => new[] { 0.01f, 0.014f, 0.018f, 0.022f, 0.03f },
                GalduryteEffectType.SuppressionChance => new[] { 0.01f, 0.014f, 0.018f, 0.022f, 0.03f },
                GalduryteEffectType.DebuffChance => new[] { 0.01f, 0.014f, 0.018f, 0.022f, 0.03f },
                GalduryteEffectType.DotChance => new[] { 0.01f, 0.014f, 0.018f, 0.022f, 0.03f },
                GalduryteEffectType.BleedOnAttack => new[] { 0.0075f, 0.01f, 0.0125f, 0.015f, 0.02f },
                GalduryteEffectType.PoisonOnAttack => new[] { 0.0075f, 0.01f, 0.0125f, 0.015f, 0.02f },
                GalduryteEffectType.BurnOnAttack => new[] { 0.0075f, 0.01f, 0.0125f, 0.015f, 0.02f },
                GalduryteEffectType.HealOnAttack => new[] { 0.015f, 0.02f, 0.025f, 0.03f, 0.04f },
                GalduryteEffectType.ResourceOnAttack => new[] { 0.015f, 0.02f, 0.025f, 0.03f, 0.04f },
                GalduryteEffectType.ActionValueOnAttack => new[] { 2f, 2.5f, 3f, 3.5f, 4.5f },
                GalduryteEffectType.ItemDropChance => new[] { 0.03f, 0.036f, 0.042f, 0.048f, 0.06f },
                GalduryteEffectType.ResourceGenerationPercent => new[] { 0.04f, 0.048f, 0.056f, 0.064f, 0.08f },
                GalduryteEffectType.UndeadDamagePercent => new[] { 0.04f, 0.048f, 0.056f, 0.064f, 0.08f },
                GalduryteEffectType.HumanDamagePercent => new[] { 0.04f, 0.048f, 0.056f, 0.064f, 0.08f },
                GalduryteEffectType.BeastDamagePercent => new[] { 0.04f, 0.048f, 0.056f, 0.064f, 0.08f },
                GalduryteEffectType.DemonDamagePercent => new[] { 0.04f, 0.048f, 0.056f, 0.064f, 0.08f },
                GalduryteEffectType.StunOnAttack => new[] { 0.0075f, 0.01f, 0.0125f, 0.015f, 0.02f },
                GalduryteEffectType.FreezeOnAttack => new[] { 0.0075f, 0.01f, 0.0125f, 0.015f, 0.02f },
                GalduryteEffectType.SlowOnAttack => new[] { 0.0075f, 0.01f, 0.0125f, 0.015f, 0.02f },
                GalduryteEffectType.ExperiencePercent => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.GoldPercent => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.PhysicalPenetration => new[] { 0.05f, 0.06f, 0.07f, 0.08f, 0.1f },
                GalduryteEffectType.MagicPenetration => new[] { 0.05f, 0.06f, 0.07f, 0.08f, 0.1f },
                GalduryteEffectType.MaxHealthPercent => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.DodgePercent => new[] { 0.03f, 0.036f, 0.042f, 0.048f, 0.06f },
                GalduryteEffectType.PhysicalDefensePercent => new[] { 0.03f, 0.036f, 0.042f, 0.048f, 0.06f },
                GalduryteEffectType.MagicDefensePercent => new[] { 0.03f, 0.036f, 0.042f, 0.048f, 0.06f },
                GalduryteEffectType.AllDefensePercent => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.SpeedPercent => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.ActionPointsPercent => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.DotResistance => new[] { 0.015f, 0.02f, 0.025f, 0.03f, 0.04f },
                GalduryteEffectType.SuppressionResistance => new[] { 0.015f, 0.02f, 0.025f, 0.03f, 0.04f },
                GalduryteEffectType.DebuffResistance => new[] { 0.015f, 0.02f, 0.025f, 0.03f, 0.04f },
                GalduryteEffectType.AllResistance => new[] { 0.01f, 0.014f, 0.018f, 0.022f, 0.03f },
                GalduryteEffectType.PhysicalDamageReduction => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.MagicDamageReduction => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.AllDamageReduction => new[] { 0.015f, 0.02f, 0.025f, 0.03f, 0.04f },
                GalduryteEffectType.MaxResourcePercent => new[] { 0.03f, 0.036f, 0.042f, 0.048f, 0.06f },
                GalduryteEffectType.BleedDamageReduction => new[] { 0.04f, 0.048f, 0.056f, 0.064f, 0.08f },
                GalduryteEffectType.PoisonDamageReduction => new[] { 0.04f, 0.048f, 0.056f, 0.064f, 0.08f },
                GalduryteEffectType.BurnDamageReduction => new[] { 0.04f, 0.048f, 0.056f, 0.064f, 0.08f },
                GalduryteEffectType.ResourceCostReduction => new[] { 0.02f, 0.026f, 0.032f, 0.038f, 0.05f },
                GalduryteEffectType.CriticalAvoidance => new[] { 0.03f, 0.036f, 0.042f, 0.048f, 0.06f },
                GalduryteEffectType.HealthPerTurn => new[] { 0.004f, 0.0052f, 0.0064f, 0.0076f, 0.01f },
                GalduryteEffectType.ResourcePerTurn => new[] { 0.005f, 0.008f, 0.011f, 0.014f, 0.02f },
                _ => throw new ArgumentOutOfRangeException(nameof(effectType))
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

            if ((itemLevel < 11 || itemLevel > 20) || !TryGetEffectCountRange(itemLevel, out var minimum, out var maximum))
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

            var colorPool = pool.Where(effect => effect.PowderColor == guaranteedColor).ToList();
            var guaranteed = colorPool.Count == 0 ? null : colorPool[random.Next(colorPool.Count)];
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
            if ((itemLevel < 11 || itemLevel > 20) || !TryGetEffectCountRange(itemLevel, out _, out var maximum))
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
