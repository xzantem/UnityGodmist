using System;
using Godmist.Core.Enums;
using Godmist.Data.Items;
using System.Collections.Generic;

namespace Godmist.Runtime.Items
{
    public sealed class ConsumableItemInstance : ItemInstance
    {
        public const int DefaultBombRange = 5;
        public const int DefaultBombAreaSize = 3;
        public const int MaxBombRange = 8;
        public const int MaxBombAreaSize = 5;

        public int CurrentCharges;
        public List<ConsumableEffectRuntime> RuntimeEffects = new();
        // The area value is the complete side length (3 means 3x3, 4 means
        // 4x4, 5 means 5x5), not a geometric radius measured from the tile.
        public int BlastRange = DefaultBombRange;
        public int BlastAreaSize = DefaultBombAreaSize;
        public int CatalystDurationModifier;
        public float CatalystEffectMultiplier = 1f;
        public int CatalystChargeBonus;
        public ConsumableItemInstance(ConsumableItemDefinition definition, string instanceId = null) : base(definition, instanceId)
        {
            CurrentCharges = MaxCharges;
        }
        public ConsumableItemDefinition ConsumableDefinition => Definition as ConsumableItemDefinition;
        public int MaxCharges => ConsumableDefinition == null
            ? 0
            : Math.Max(1, Math.Min(10, ConsumableDefinition.MaxCharges + CatalystChargeBonus));
        public bool HasCharges => CurrentCharges > 0;

        public bool IsBomb => ConsumableDefinition?.UseType == ConsumableUseType.Bomb;

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
                    CatalystDurationModifier = GetCatalystTierValue(tier, 0, 1, 1, 2, 2, 3);
                    break;
                case ConsumableCatalystType.Strength:
                    CatalystEffectMultiplier = 1f + GetCatalystTierValue(tier, 0f, .10f, .12f, .15f, .20f, .30f);
                    break;
                case ConsumableCatalystType.Condensation:
                    CatalystDurationModifier = -GetCatalystTierValue(tier, 0, 1, 1, 2, 2, 3);
                    break;
                case ConsumableCatalystType.Range:
                    if (!IsBomb) break;
                    ConfigureBombTargeting(
                        DefaultBombRange + GetCatalystTierValue(tier, 0, 1, 1, 2, 2, 3),
                        GetCatalystTierValue(tier, DefaultBombAreaSize, 3, 4, 4, 5, 5));
                    break;
                case ConsumableCatalystType.Charges:
                    CatalystChargeBonus = GetCatalystTierValue(tier, 0, 1, 2, 3, 4, 5);
                    CurrentCharges = Math.Min(CurrentCharges, MaxCharges);
                    break;
            }
        }

        private static int GetCatalystTierValue(int tier, params int[] values)
        {
            return values[Math.Max(0, Math.Min(values.Length - 1, tier))];
        }

        private static float GetCatalystTierValue(int tier, params float[] values)
        {
            return values[Math.Max(0, Math.Min(values.Length - 1, tier))];
        }
        public override bool CanStackWith(ItemInstance other)
        {
            return MaxCharges == 1 && base.CanStackWith(other);
        }
    }
}
