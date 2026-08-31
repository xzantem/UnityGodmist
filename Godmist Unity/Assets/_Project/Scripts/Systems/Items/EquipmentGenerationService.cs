using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Items;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    public class EquipmentGenerationService
    {
        private static readonly (RarityTier Rarity, double Chance)[] RarityDistribution =
        {
            (RarityTier.Trash, 0.02),
            (RarityTier.Broken, 0.03),
            (RarityTier.Damaged, 0.04),
            (RarityTier.Common, 0.50),
            (RarityTier.Uncommon, 0.20),
            (RarityTier.Rare, 0.10),
            (RarityTier.Ancient, 0.05),
            (RarityTier.Legendary, 0.03),
            (RarityTier.Mythic, 0.02),
            (RarityTier.Godly, 0.01)
        };

        private readonly Random random;
        private readonly ItemInstanceFactory itemFactory;

        public EquipmentGenerationService(Random random = null, ItemInstanceFactory itemFactory = null)
        {
            this.random = random ?? new Random();
            this.itemFactory = itemFactory ?? new ItemInstanceFactory();
        }

        public EquipmentItemInstance Generate(
            EquipmentItemDefinition definition,
            IReadOnlyList<EquipmentPartDefinition> availableParts)
        {
            return Generate(definition, availableParts,
                RollQuality(), RollRarity());
        }

        public EquipmentItemInstance Generate(
            EquipmentItemDefinition definition,
            IReadOnlyList<EquipmentPartDefinition> availableParts,
            EquipmentQuality quality,
            RarityTier rarity)
        {
            if (definition == null || definition.RequiredClass == null
                || availableParts == null
                || !Enum.IsDefined(typeof(EquipmentQuality), quality)
                || !Enum.IsDefined(typeof(RarityTier), rarity)) return null;

            var kinds = GetRequiredKinds(definition);
            if (kinds == null) return null;

            var selected = new List<EquipmentPartDefinition>(3);
            foreach (var kind in kinds)
            {
                var candidates = availableParts.Where(part => part != null
                    && part.Kind == kind
                    && part.RequiredClass == definition.RequiredClass
                    && !string.IsNullOrWhiteSpace(part.Id)
                    && part.Stats != null).ToList();
                if (candidates.Count == 0) return null;
                selected.Add(candidates[random.Next(candidates.Count)]);
            }

            return GenerateFromSelectedParts(definition, selected, quality, rarity);
        }

        /// <summary>
        /// Creates equipment from an explicit, player-selected set of three parts.
        /// The generator remains the sole owner of the resulting runtime payload.
        /// </summary>
        public EquipmentItemInstance GenerateFromSelectedParts(
            EquipmentItemDefinition definition,
            IReadOnlyList<EquipmentPartDefinition> selectedParts,
            EquipmentQuality quality)
        {
            return GenerateFromSelectedParts(
                definition,
                selectedParts,
                quality,
                RollRarity());
        }

        public EquipmentItemInstance GenerateFromSelectedParts(
            EquipmentItemDefinition definition,
            IReadOnlyList<EquipmentPartDefinition> selectedParts,
            EquipmentQuality quality,
            RarityTier rarity)
        {
            if (definition == null || definition.RequiredClass == null
                || selectedParts == null
                || !Enum.IsDefined(typeof(EquipmentQuality), quality)
                || !Enum.IsDefined(typeof(RarityTier), rarity)) return null;

            var kinds = GetRequiredKinds(definition);
            if (kinds == null || selectedParts.Count != kinds.Length) return null;

            var selected = new List<EquipmentPartDefinition>(selectedParts);

            foreach (var kind in kinds)
            {
                var count = selected.Count(part => part != null
                    && part.Kind == kind
                    && part.RequiredClass == definition.RequiredClass
                    && !string.IsNullOrWhiteSpace(part.Id)
                    && part.Stats != null);

                if (count != 1) return null;
            }

            var item = itemFactory.Create(definition) as EquipmentItemInstance;
            if (item == null) return null;

            item.CurrentRarity = rarity;
            item.Quality = quality;
            item.PartIds = selected.Select(part => part.Id).ToList();
            var bracket = selected.Max(part => (int)part.LevelBracket);
            item.ItemLevel = Math.Max(1, bracket + GetQualityLevelAdjustment(quality));
            item.RequiredLevel = bracket;
            item.StatModifiers = BuildFinalItemStats(selected, quality);
            return item;
        }

        public static int GetQualityLevelAdjustment(EquipmentQuality quality) => quality switch
        {
            EquipmentQuality.Poor => -3,
            EquipmentQuality.Excellent => 3,
            EquipmentQuality.Masterpiece => 5,
            _ => 0
        };

        private EquipmentQuality RollQuality()
        {
            return random.Next(3) switch
            {
                0 => EquipmentQuality.Poor,
                1 => EquipmentQuality.Normal,
                _ => EquipmentQuality.Excellent
            };
        }

        private RarityTier RollRarity()
        {
            var roll = random.NextDouble();
            var cumulativeChance = 0d;

            foreach (var entry in RarityDistribution)
            {
                cumulativeChance += entry.Chance;
                if (roll < cumulativeChance) return entry.Rarity;
            }

            return RarityDistribution[RarityDistribution.Length - 1].Rarity;
        }

        private static EquipmentPartKind[] GetRequiredKinds(EquipmentItemDefinition definition)
        {
            return definition.ItemType switch
            {
                ItemType.Weapon => new[] { EquipmentPartKind.WeaponHead, EquipmentPartKind.WeaponBinder, EquipmentPartKind.WeaponHandle },
                ItemType.Armor => new[] { EquipmentPartKind.ArmorPlate, EquipmentPartKind.ArmorBinder, EquipmentPartKind.ArmorBase },
                _ => null
            };
        }

        private static List<StatModifier> BuildFinalItemStats(
            IEnumerable<EquipmentPartDefinition> parts,
            EquipmentQuality quality)
        {
            var result = new List<StatModifier>();
            foreach (CharacterStatType stat in Enum.GetValues(typeof(CharacterStatType)))
            {
                var baseValue = parts.Sum(part => GetBase(part, stat));
                var percent = parts.Sum(part => GetPercent(part, stat));
                if (Math.Abs(baseValue) < 0.0001f && Math.Abs(percent) < 0.0001f) continue;

                var value = baseValue * (1f + percent)
                    + GetQualityStatAdjustment(stat, quality);
                result.Add(new StatModifier
                {
                    StatType = stat,
                    Operation = StatModifierOperation.FlatBase,
                    Stacking = StatModifierStacking.Additive,
                    Value = value
                });
            }
            return result;
        }

        private static float GetQualityStatAdjustment(CharacterStatType stat, EquipmentQuality quality)
        {
            var adjustment = GetQualityLevelAdjustment(quality);
            return stat is CharacterStatType.CritChance or CharacterStatType.CritDamage or CharacterStatType.Dodge
                ? adjustment * 0.01f
                : adjustment;
        }

        private static float GetBase(EquipmentPartDefinition part, CharacterStatType stat)
        {
            if (!AllowsBase(part, stat)) return 0f;
            var stats = part.Stats;
            return stat switch
            {
            CharacterStatType.MinAttack => stats.MinAttackBase,
            CharacterStatType.MaxAttack => stats.MaxAttackBase,
            CharacterStatType.CritChance => stats.CritChanceBase,
            CharacterStatType.CritDamage => stats.CritDamageBase,
            CharacterStatType.WeaponAccuracy => stats.WeaponAccuracyBase,
            CharacterStatType.MaxResource => stats.MaxResourceBase,
            CharacterStatType.ResourceRegeneration => stats.ResourceRegenerationBase,
            CharacterStatType.MaxHealth => stats.MaxHealthBase,
            CharacterStatType.Dodge => stats.DodgeBase,
            CharacterStatType.PhysicalDefense => stats.PhysicalDefenseBase,
            CharacterStatType.MagicalDefense => stats.MagicalDefenseBase,
            _ => 0f
            };
        }

        private static float GetPercent(EquipmentPartDefinition part, CharacterStatType stat)
        {
            if (!AllowsPercent(part, stat)) return 0f;
            var stats = part.Stats;
            return stat switch
            {
            CharacterStatType.MinAttack => stats.MinAttackPercent,
            CharacterStatType.MaxAttack => stats.MaxAttackPercent,
            CharacterStatType.CritChance => stats.CritChancePercent,
            CharacterStatType.CritDamage => stats.CritDamagePercent,
            CharacterStatType.WeaponAccuracy => stats.WeaponAccuracyPercent,
            CharacterStatType.MaxResource => stats.MaxResourcePercent,
            CharacterStatType.ResourceRegeneration => stats.ResourceRegenerationPercent,
            CharacterStatType.MaxHealth => stats.MaxHealthPercent,
            CharacterStatType.Dodge => stats.DodgePercent,
            CharacterStatType.PhysicalDefense => stats.PhysicalDefensePercent,
            CharacterStatType.MagicalDefense => stats.MagicalDefensePercent,
            _ => 0f
            };
        }

        private static bool AllowsBase(EquipmentPartDefinition part, CharacterStatType stat)
        {
            var mage = part.RequiredClass?.Id == "class.mage";
            return part.Kind switch
            {
                EquipmentPartKind.WeaponHead => stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack or CharacterStatType.CritDamage,
                EquipmentPartKind.WeaponBinder => mage ? stat == CharacterStatType.ResourceRegeneration : stat == CharacterStatType.CritChance,
                EquipmentPartKind.WeaponHandle => mage ? stat == CharacterStatType.MaxResource : stat == CharacterStatType.WeaponAccuracy,
                EquipmentPartKind.ArmorPlate => stat is CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense,
                EquipmentPartKind.ArmorBinder => stat == CharacterStatType.MaxHealth,
                EquipmentPartKind.ArmorBase => stat == CharacterStatType.Dodge,
                _ => false
            };
        }

        private static bool AllowsPercent(EquipmentPartDefinition part, CharacterStatType stat)
        {
            var mage = part.RequiredClass?.Id == "class.mage";
            return part.Kind switch
            {
                EquipmentPartKind.WeaponHead => mage
                    ? stat is CharacterStatType.ResourceRegeneration or CharacterStatType.MaxResource
                    : stat is CharacterStatType.CritChance or CharacterStatType.WeaponAccuracy,
                EquipmentPartKind.WeaponBinder => mage
                    ? stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack or CharacterStatType.CritDamage or CharacterStatType.MaxResource
                    : stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack or CharacterStatType.CritDamage or CharacterStatType.WeaponAccuracy,
                EquipmentPartKind.WeaponHandle => mage
                    ? stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack or CharacterStatType.ResourceRegeneration or CharacterStatType.CritDamage
                    : stat is CharacterStatType.MinAttack or CharacterStatType.MaxAttack or CharacterStatType.CritChance or CharacterStatType.CritDamage,
                EquipmentPartKind.ArmorPlate => stat is CharacterStatType.MaxHealth or CharacterStatType.Dodge,
                EquipmentPartKind.ArmorBinder => stat is CharacterStatType.Dodge or CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense,
                EquipmentPartKind.ArmorBase => stat is CharacterStatType.MaxHealth or CharacterStatType.PhysicalDefense or CharacterStatType.MagicalDefense,
                _ => false
            };
        }
    }
}
