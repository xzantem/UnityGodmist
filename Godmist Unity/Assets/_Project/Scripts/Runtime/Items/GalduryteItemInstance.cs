using System;
using System.Collections.Generic;
using Godmist.Core.Enums;
using Godmist.Data.Items;

namespace Godmist.Runtime.Items
{
    [Serializable]
    public sealed class GalduryteEffectInstance
    {
        public GalduryteEffectType EffectType;
        public GaldurytePowderColor PowderColor;
        public GalduryteEffectGrade Grade;
        public float Value;

        public GalduryteEffectInstance() {}

        public GalduryteEffectInstance(
            GalduryteEffectType effectType,
            GaldurytePowderColor powderColor,
            GalduryteEffectGrade grade,
            float value)
        {
            EffectType = effectType;
            PowderColor = powderColor;
            Grade = grade;
            Value = value;
        }
    }

    [Serializable]
    public sealed class GalduryteItemInstance : ItemInstance
    {
        public GalduryteTargetType TargetType;
        public GalduryteTier Tier;
        public GaldurytePowderColor GuaranteedPowderColor;
        public string StoneDefinitionId = string.Empty;
        public string PowderDefinitionId = string.Empty;
        public string StoneDisplayName = string.Empty;
        public int CraftedBaseValue;
        public bool IsIdentified;
        public bool IsDestroyed;
        public List<GalduryteEffectInstance> Effects = new();

        public GalduryteItemDefinition GalduryteDefinition =>
            Definition as GalduryteItemDefinition;

        public override string DisplayName =>
            string.IsNullOrWhiteSpace(CustomName)
                ? GetGeneratedDisplayName()
                : CustomName;

        public override int BaseValue => CraftedBaseValue > 0
            ? CraftedBaseValue
            : base.BaseValue;

        public GalduryteItemInstance() {}

        public GalduryteItemInstance(
            GalduryteItemDefinition definition,
            string instanceId = null)
            : base(definition, instanceId)
        {
            IsIdentified = false;
            CurrentRarity = RarityTier.Common;
        }

        public override bool CanStackWith(ItemInstance other) => false;

        public string GetEffectSummary()
        {
            if (IsDestroyed) return "Zniszczony — nie daje efektów.";
            if (!IsIdentified) return "Niezidentyfikowany";
            return Effects == null || Effects.Count == 0
                ? "Brak efektów"
                : string.Join("\n", Effects.ConvertAll(FormatEffect));
        }

        private string GetGeneratedDisplayName()
        {
            var stoneName = string.IsNullOrWhiteSpace(StoneDisplayName)
                ? GetDefaultStoneName(Tier)
                : StoneDisplayName.Trim();
            return $"{stoneName} Galduryt";
        }

        private string FormatEffect(GalduryteEffectInstance effect)
        {
            if (effect == null) return string.Empty;
            return $"{GalduryteRuntimeText.GetEffectName(effect.EffectType)}: "
                + $"+{GalduryteRuntimeText.FormatPercent(effect.Value)}";
        }

        private static string GetDefaultStoneName(GalduryteTier tier) => tier switch
        {
            GalduryteTier.Limestone => "Wapienny",
            GalduryteTier.Granite => "Granitowy",
            GalduryteTier.Marble => "Marmurowy",
            _ => "Nieznany"
        };
    }

    public static class GalduryteRuntimeText
    {
        public static string GetEffectName(GalduryteEffectType effectType) => effectType switch
        {
            GalduryteEffectType.GeneralDamagePercent => "Obrażenia ogólne",
            GalduryteEffectType.CriticalChancePercent => "Szansa na trafienie krytyczne",
            GalduryteEffectType.AccuracyPercent => "Szansa na trafienie",
            GalduryteEffectType.MaxHealthPercent => "Maksymalne zdrowie",
            GalduryteEffectType.PhysicalDefensePercent => "Obrona fizyczna",
            GalduryteEffectType.SpeedPercent => "Szybkość",
            _ => effectType.ToString()
        };

        public static string FormatPercent(float value) =>
            $"{value * 100f:0.##}%";
    }
}
