using System;
using Godmist.Core.Enums;
using Godmist.Data.Configuration;
using Godmist.Data.Items;
using System.Collections.Generic;

namespace Godmist.Runtime.Items
{
    public sealed class ConsumableItemInstance : ItemInstance
    {
        public static int DefaultBombRange => GameDefinesRuntime.GetInt(GameDefineIds.DefaultBombRange, 5);
        public static int DefaultBombAreaSize => GameDefinesRuntime.GetInt(GameDefineIds.DefaultBombAreaSize, 3);
        public static int MaxBombRange => GameDefinesRuntime.GetInt(GameDefineIds.MaxBombRange, 8);
        public static int MaxBombAreaSize => GameDefinesRuntime.GetInt(GameDefineIds.MaxBombAreaSize, 5);

        public int CurrentCharges;
        public List<ConsumableEffectRuntime> RuntimeEffects = new();
        // The area value is the complete side length (3 means 3x3, 4 means
        // 4x4, 5 means 5x5), not a geometric radius measured from the tile.
        public int BlastRange = DefaultBombRange;
        public int BlastAreaSize = DefaultBombAreaSize;
        public int CatalystDurationModifier;
        public float CatalystEffectMultiplier = 1f;
        public int CatalystChargeBonus;
        public string CatalystDisplayName = string.Empty;
        public int CraftedBaseValue;
        public ConsumableItemInstance(ConsumableItemDefinition definition, string instanceId = null) : base(definition, instanceId)
        {
            CurrentCharges = MaxCharges;
        }
        public ConsumableItemDefinition ConsumableDefinition => Definition as ConsumableItemDefinition;
        public int MaxCharges => ConsumableDefinition == null
            ? 0
            : Math.Max(1, Math.Min(
                GameDefinesRuntime.GetInt(GameDefineIds.MaxConsumableCharges, 10),
                ConsumableDefinition.MaxCharges + CatalystChargeBonus));
        public bool HasCharges => CurrentCharges > 0;

        public bool IsBomb => ConsumableDefinition?.UseType == ConsumableUseType.Bomb;

        public override int BaseValue => CraftedBaseValue > 0
            ? CraftedBaseValue
            : base.BaseValue;

        public void ConfigureBombTargeting(int range, int areaSize)
        {
            BlastRange = Math.Max(DefaultBombRange, Math.Min(MaxBombRange, range));
            BlastAreaSize = Math.Max(DefaultBombAreaSize, Math.Min(MaxBombAreaSize, areaSize));
        }

        public void ConfigureCatalyst(
            int durationModifier,
            float effectMultiplier,
            int chargeBonus)
        {
            CatalystDurationModifier = durationModifier;
            CatalystEffectMultiplier = Math.Max(0f, effectMultiplier);
            CatalystChargeBonus = Math.Max(0, chargeBonus);
            CurrentCharges = Math.Min(CurrentCharges, MaxCharges);
        }

        public void ApplyCatalyst(ConsumableCatalystType catalystType, int tier)
        {
            tier = Math.Max(0, Math.Min(5, tier));
            switch (catalystType)
            {
                case ConsumableCatalystType.Duration:
                    CatalystDurationModifier = GetCatalystDurationValue(tier);
                    break;
                case ConsumableCatalystType.Strength:
                    CatalystEffectMultiplier = 1f + GetCatalystStrengthValue(tier);
                    break;
                case ConsumableCatalystType.Condensation:
                    CatalystDurationModifier = -GetCatalystDurationValue(tier);
                    break;
                case ConsumableCatalystType.Range:
                    if (!IsBomb) break;
                    ConfigureBombTargeting(
                        DefaultBombRange + GetCatalystRangeValue(tier),
                        tier == 0 ? DefaultBombAreaSize : GetCatalystAreaValue(tier));
                    break;
                case ConsumableCatalystType.Charges:
                    CatalystChargeBonus = GetCatalystChargesValue(tier);
                    CurrentCharges = Math.Min(CurrentCharges, MaxCharges);
                    break;
            }
        }

        private static int GetCatalystDurationValue(int tier)
        {
            return GetIntegerCatalystTierValue(
                tier,
                GameDefineIds.CatalystDurationTier1, 1,
                GameDefineIds.CatalystDurationTier2, 1,
                GameDefineIds.CatalystDurationTier3, 2,
                GameDefineIds.CatalystDurationTier4, 2,
                GameDefineIds.CatalystDurationTier5, 3);
        }

        private static int GetCatalystRangeValue(int tier)
        {
            return GetIntegerCatalystTierValue(
                tier,
                GameDefineIds.CatalystRangeTier1, 1,
                GameDefineIds.CatalystRangeTier2, 1,
                GameDefineIds.CatalystRangeTier3, 2,
                GameDefineIds.CatalystRangeTier4, 2,
                GameDefineIds.CatalystRangeTier5, 3);
        }

        private static int GetCatalystAreaValue(int tier)
        {
            return GetIntegerCatalystTierValue(
                tier,
                GameDefineIds.CatalystAreaTier1, 3,
                GameDefineIds.CatalystAreaTier2, 4,
                GameDefineIds.CatalystAreaTier3, 4,
                GameDefineIds.CatalystAreaTier4, 5,
                GameDefineIds.CatalystAreaTier5, 5);
        }

        private static int GetCatalystChargesValue(int tier)
        {
            return GetIntegerCatalystTierValue(
                tier,
                GameDefineIds.CatalystChargesTier1, 1,
                GameDefineIds.CatalystChargesTier2, 2,
                GameDefineIds.CatalystChargesTier3, 3,
                GameDefineIds.CatalystChargesTier4, 4,
                GameDefineIds.CatalystChargesTier5, 5);
        }

        private static float GetCatalystStrengthValue(int tier)
        {
            if (tier <= 0) return 0f;
            return tier switch
            {
                1 => GameDefinesRuntime.GetFloat(GameDefineIds.CatalystStrengthTier1, .10f),
                2 => GameDefinesRuntime.GetFloat(GameDefineIds.CatalystStrengthTier2, .12f),
                3 => GameDefinesRuntime.GetFloat(GameDefineIds.CatalystStrengthTier3, .15f),
                4 => GameDefinesRuntime.GetFloat(GameDefineIds.CatalystStrengthTier4, .20f),
                _ => GameDefinesRuntime.GetFloat(GameDefineIds.CatalystStrengthTier5, .30f)
            };
        }

        private static int GetIntegerCatalystTierValue(
            int tier,
            string tier1Id, int tier1Fallback,
            string tier2Id, int tier2Fallback,
            string tier3Id, int tier3Fallback,
            string tier4Id, int tier4Fallback,
            string tier5Id, int tier5Fallback)
        {
            if (tier <= 0) return 0;
            return tier switch
            {
                1 => GameDefinesRuntime.GetInt(tier1Id, tier1Fallback),
                2 => GameDefinesRuntime.GetInt(tier2Id, tier2Fallback),
                3 => GameDefinesRuntime.GetInt(tier3Id, tier3Fallback),
                4 => GameDefinesRuntime.GetInt(tier4Id, tier4Fallback),
                _ => GameDefinesRuntime.GetInt(tier5Id, tier5Fallback)
            };
        }
        public override bool CanStackWith(ItemInstance other)
        {
            var effects = RuntimeEffects ?? new List<ConsumableEffectRuntime>();
            if (MaxCharges != 1 || !base.CanStackWith(other)
                || other is not ConsumableItemInstance consumable
                || !string.Equals(CustomName, consumable.CustomName, StringComparison.Ordinal)
                || CurrentCharges != consumable.CurrentCharges
                || BlastRange != consumable.BlastRange
                || BlastAreaSize != consumable.BlastAreaSize
                || CatalystDurationModifier != consumable.CatalystDurationModifier
                || CatalystEffectMultiplier != consumable.CatalystEffectMultiplier
                || CatalystChargeBonus != consumable.CatalystChargeBonus
                || CatalystDisplayName != consumable.CatalystDisplayName
                || CraftedBaseValue != consumable.CraftedBaseValue
                || effects.Count != (consumable.RuntimeEffects?.Count ?? 0))
                return false;

            for (var index = 0; index < effects.Count; index++)
            {
                var left = effects[index];
                var right = consumable.RuntimeEffects[index];
                if (left == null || right == null)
                {
                    if (left != right) return false;
                    continue;
                }
                if (left.EffectType != right.EffectType || left.Value != right.Value
                    || left.DurationTurns != right.DurationTurns || left.Chance != right.Chance
                    || left.TargetModeIndex != right.TargetModeIndex || left.StatusId != right.StatusId
                    || left.ResistanceType != right.ResistanceType || left.TargetType != right.TargetType
                    || left.TargetingType != right.TargetingType)
                    return false;
            }
            return true;
        }
    }
}
