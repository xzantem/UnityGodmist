using System;
using System.Collections.Generic;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Core.Types;
using Godmist.Data.Configuration;
using Godmist.Data.Items;
using Godmist.Runtime.Items;

namespace Godmist.Systems.Items
{
    public class EquipmentGenerationService
    {
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

            if (definition is BossUniqueEquipmentDefinition
                && quality != EquipmentQuality.Masterpiece)
            {
                return null;
            }

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

        public EquipmentItemInstance GenerateBossUnique(
            BossUniqueEquipmentDefinition definition,
            IReadOnlyList<EquipmentPartDefinition> availableParts)
        {
            if (definition == null) return null;

            return Generate(
                definition,
                availableParts,
                EquipmentQuality.Masterpiece,
                RollRarity());
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

            if (definition is BossUniqueEquipmentDefinition
                && quality != EquipmentQuality.Masterpiece)
            {
                return null;
            }

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
            item.BaseStatModifiers = BuildFinalItemStats(
                selected,
                definition.ItemType,
                definition.RequiredClass.Id,
                quality,
                bracket);
            item.StatModifiers = item.BaseStatModifiers.Select(modifier => new StatModifier
            {
                StatType = modifier.StatType,
                Operation = modifier.Operation,
                Stacking = modifier.Stacking,
                Value = modifier.Value
            }).ToList();
            return item;
        }

        public static int GetQualityLevelAdjustment(EquipmentQuality quality) => quality switch
        {
            EquipmentQuality.Poor => GameDefinesRuntime.GetInt(
                GameDefineIds.EquipmentPoorLevelAdjustment, -3),
            EquipmentQuality.Excellent => GameDefinesRuntime.GetInt(
                GameDefineIds.EquipmentExcellentLevelAdjustment, 3),
            EquipmentQuality.Masterpiece => GameDefinesRuntime.GetInt(
                GameDefineIds.EquipmentMasterpieceLevelAdjustment, 5),
            _ => 0
        };

        private EquipmentQuality RollQuality()
        {
            var poor = Math.Max(0, GameDefinesRuntime.GetInt(
                GameDefineIds.EquipmentQualityPoorWeight, 1));
            var normal = Math.Max(0, GameDefinesRuntime.GetInt(
                GameDefineIds.EquipmentQualityNormalWeight, 1));
            var excellent = Math.Max(0, GameDefinesRuntime.GetInt(
                GameDefineIds.EquipmentQualityExcellentWeight, 1));
            var total = poor + normal + excellent;
            if (total <= 0) return EquipmentQuality.Normal;

            var roll = random.Next(total);
            if (roll < poor) return EquipmentQuality.Poor;
            if (roll < poor + normal) return EquipmentQuality.Normal;
            return EquipmentQuality.Excellent;
        }

        private RarityTier RollRarity()
        {
            var roll = random.NextDouble();
            var cumulativeChance = 0d;

            foreach (var entry in GetRarityDistribution())
            {
                cumulativeChance += entry.Chance;
                if (roll < cumulativeChance) return entry.Rarity;
            }

            return RarityTier.Godly;
        }

        private static (RarityTier Rarity, double Chance)[] GetRarityDistribution()
        {
            return new[]
            {
                (RarityTier.Trash, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityTrashWeight, .02f))),
                (RarityTier.Broken, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityBrokenWeight, .03f))),
                (RarityTier.Damaged, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityDamagedWeight, .04f))),
                (RarityTier.Common, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityCommonWeight, .5f))),
                (RarityTier.Uncommon, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityUncommonWeight, .2f))),
                (RarityTier.Rare, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityRareWeight, .1f))),
                (RarityTier.Ancient, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityAncientWeight, .05f))),
                (RarityTier.Legendary, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityLegendaryWeight, .03f))),
                (RarityTier.Mythic, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityMythicWeight, .02f))),
                (RarityTier.Godly, (double)Math.Max(0f, GameDefinesRuntime.GetFloat(
                    GameDefineIds.EquipmentRarityGodlyWeight, .01f)))
            };
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
            ItemType itemType,
            string classId,
            EquipmentQuality quality,
            int requiredLevel)
        {
            var result = new List<StatModifier>();
            foreach (CharacterStatType stat in Enum.GetValues(typeof(CharacterStatType)))
            {
                var baseValue = parts.Sum(part => GetBase(part, stat));
                var percent = parts.Sum(part => GetPercent(part, stat));
                if (Math.Abs(baseValue) < 0.0001f && Math.Abs(percent) < 0.0001f) continue;

                // Quality scaling uses the item's required/base level. The displayed item level
                // already includes the quality level offset and must not scale the stat bonus.
                var qualityAdjustment = GetQualityStatAdjustment(
                    stat,
                    itemType,
                    classId,
                    quality,
                    requiredLevel);
                var value = (baseValue + qualityAdjustment) * (1f + percent);
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

        private static float GetQualityStatAdjustment(
            CharacterStatType stat,
            ItemType itemType,
            string classId,
            EquipmentQuality quality,
            int requiredLevel)
        {
            var profile = GetQualityProfile(itemType, classId);
            var qualityMultiplier = quality switch
            {
                EquipmentQuality.Poor => -1f,
                EquipmentQuality.Excellent => 1f,
                EquipmentQuality.Masterpiece => 4f,
                _ => 0f
            };

            var perLevel = stat switch
            {
                CharacterStatType.MinAttack => profile.MinAttackPerLevel,
                CharacterStatType.MaxAttack => profile.MaxAttackPerLevel,
                CharacterStatType.ResourceRegeneration => profile.ResourceRegenerationPerLevel,
                CharacterStatType.MaxResource => profile.MaxResourcePerLevel,
                CharacterStatType.MaxHealth => profile.MaxHealthPerLevel,
                CharacterStatType.PhysicalDefense => profile.PhysicalDefensePerLevel,
                CharacterStatType.MagicalDefense => profile.MagicalDefensePerLevel,
                _ => 0f
            };

            if (Math.Abs(perLevel) > 0.0001f)
                return perLevel * requiredLevel * qualityMultiplier;

            var flat = stat switch
            {
                CharacterStatType.CritChance => profile.CritChanceFlat,
                CharacterStatType.CritDamage => profile.CritDamageFlat,
                CharacterStatType.WeaponAccuracy => profile.WeaponAccuracyFlat,
                CharacterStatType.Dodge => profile.DodgeFlat,
                _ => 0f
            };
            return flat * qualityMultiplier;
        }

        // Profile values are the positive Excellent adjustment. Poor negates one profile,
        // Excellent applies one profile, and Masterpiece applies four profiles.
        // Values named PerLevel are applied before part percentages are resolved.
        private static QualityProfile GetQualityProfile(ItemType itemType, string classId)
        {
            if (itemType == ItemType.Weapon)
            {
                return classId switch
                {
                    "class.warrior" => new QualityProfile
                    {
                        MinAttackPerLevel = 1f,
                        MaxAttackPerLevel = 2f,
                        CritChanceFlat = 0.01f,
                        CritDamageFlat = 0.02f,
                        WeaponAccuracyFlat = 1f
                    },
                    "class.scout" => new QualityProfile
                    {
                        MinAttackPerLevel = 0.4f,
                        MaxAttackPerLevel = 2f,
                        CritChanceFlat = 0.01f,
                        CritDamageFlat = 0.03f,
                        WeaponAccuracyFlat = 1f
                    },
                    "class.mage" => new QualityProfile
                    {
                        MinAttackPerLevel = 0.8f,
                        MaxAttackPerLevel = 2.4f,
                        ResourceRegenerationPerLevel = 0.05f,
                        CritDamageFlat = 0.02f,
                        MaxResourcePerLevel = 0.3f
                    },
                    "class.paladin" => new QualityProfile
                    {
                        MinAttackPerLevel = 0.4f,
                        MaxAttackPerLevel = 1.2f,
                        CritChanceFlat = 0.01f,
                        CritDamageFlat = 0.02f,
                        WeaponAccuracyFlat = 2f
                    },
                    _ => default
                };
            }

            return classId switch
            {
                "class.warrior" => new QualityProfile
                {
                    PhysicalDefensePerLevel = 1.2f,
                    MagicalDefensePerLevel = 0.8f,
                    DodgeFlat = 2f,
                    MaxHealthPerLevel = 10f
                },
                "class.scout" => new QualityProfile
                {
                    PhysicalDefensePerLevel = 0.8f,
                    MagicalDefensePerLevel = 0.4f,
                    DodgeFlat = 2f,
                    MaxHealthPerLevel = 6f
                },
                "class.mage" => new QualityProfile
                {
                    PhysicalDefensePerLevel = 0.4f,
                    MagicalDefensePerLevel = 1.2f,
                    DodgeFlat = 2f,
                    MaxHealthPerLevel = 4f
                },
                "class.paladin" => new QualityProfile
                {
                    PhysicalDefensePerLevel = 1.6f,
                    MagicalDefensePerLevel = 1.6f,
                    DodgeFlat = 2f,
                    MaxHealthPerLevel = 14f
                },
                _ => default
            };
        }

        private struct QualityProfile
        {
            public float MinAttackPerLevel;
            public float MaxAttackPerLevel;
            public float CritChanceFlat;
            public float CritDamageFlat;
            public float WeaponAccuracyFlat;
            public float ResourceRegenerationPerLevel;
            public float MaxResourcePerLevel;
            public float MaxHealthPerLevel;
            public float DodgeFlat;
            public float PhysicalDefensePerLevel;
            public float MagicalDefensePerLevel;
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
