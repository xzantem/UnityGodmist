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
            if (Effects == null || Effects.Count == 0) return "Brak efektów";

            var lines = Effects.ConvertAll(GalduryteRuntimeText.FormatEffect);
            if (Effects.Exists(effect => effect != null && GalduryteRuntimeText.IsAttackTrigger(effect.EffectType)))
                lines.Add("Efekty przy trafieniu działają raz na użycie umiejętności i nie są wyzwalane przez obrażenia okresowe. Udany efekt statusu obejmuje wszystkich trafionych wrogów.");
            return string.Join("\n", lines);
        }

        private string GetGeneratedDisplayName()
        {
            var stoneName = string.IsNullOrWhiteSpace(StoneDisplayName)
                ? GetDefaultStoneName(Tier)
                : StoneDisplayName.Trim();
            return $"{stoneName} Galduryt";
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
        public static bool IsAttackTrigger(GalduryteEffectType type) => type is
            GalduryteEffectType.BleedOnAttack or GalduryteEffectType.PoisonOnAttack
            or GalduryteEffectType.BurnOnAttack or GalduryteEffectType.StunOnAttack
            or GalduryteEffectType.FreezeOnAttack or GalduryteEffectType.SlowOnAttack
            or GalduryteEffectType.HealOnAttack or GalduryteEffectType.ResourceOnAttack
            or GalduryteEffectType.ActionValueOnAttack;

        private static bool IsReduction(GalduryteEffectType type) => type is
            GalduryteEffectType.PhysicalDamageReduction or GalduryteEffectType.MagicDamageReduction
            or GalduryteEffectType.AllDamageReduction or GalduryteEffectType.BleedDamageReduction
            or GalduryteEffectType.PoisonDamageReduction or GalduryteEffectType.BurnDamageReduction
            or GalduryteEffectType.ResourceCostReduction;

        private static bool IsAttackChance(GalduryteEffectType type) => type is
            GalduryteEffectType.BleedOnAttack or GalduryteEffectType.PoisonOnAttack
            or GalduryteEffectType.BurnOnAttack or GalduryteEffectType.StunOnAttack
            or GalduryteEffectType.FreezeOnAttack or GalduryteEffectType.SlowOnAttack;

        public static string FormatEffect(GalduryteEffectInstance effect)
        {
            if (effect == null) return string.Empty;
            var percent = FormatPercent(effect.Value);
            return effect.EffectType switch
            {
                GalduryteEffectType.CriticalDamageFlat =>
                    $"Mnożnik obrażeń krytycznych: +{effect.Value:0.##}×",
                GalduryteEffectType.BleedOnAttack =>
                    $"{percent} szansy na krwawienie (3 tury, 60% średniego ataku na turę).",
                GalduryteEffectType.PoisonOnAttack =>
                    $"{percent} szansy na zatrucie (3 tury, 60% średniego ataku na turę).",
                GalduryteEffectType.BurnOnAttack =>
                    $"{percent} szansy na podpalenie (3 tury, 60% średniego ataku na turę).",
                GalduryteEffectType.StunOnAttack => $"{percent} szansy na ogłuszenie na 3 tury.",
                GalduryteEffectType.FreezeOnAttack => $"{percent} szansy na zamrożenie na 3 tury.",
                GalduryteEffectType.SlowOnAttack =>
                    $"{percent} szansy na spowolnienie o 20% na 3 tury.",
                GalduryteEffectType.HealOnAttack =>
                    $"Po trafieniu odnawia zdrowie równe {percent} bezpośrednich obrażeń tego użycia umiejętności.",
                GalduryteEffectType.ResourceOnAttack =>
                    $"Po trafieniu odnawia {percent} maksymalnego surowca.",
                GalduryteEffectType.ActionValueOnAttack =>
                    $"Po trafieniu przyspiesza następny ruch o {effect.Value:0.##} jednostki czasu.",
                GalduryteEffectType.ItemDropChance =>
                    $"Szansa na znalezienie przedmiotu: +{percent}. Przy rozpoczęciu wyprawy liczy się średnia premia aktywnej drużyny.",
                GalduryteEffectType.HealthPerTurn =>
                    $"Na początku swojej tury odnawia {percent} maksymalnego zdrowia.",
                GalduryteEffectType.ResourcePerTurn =>
                    $"Na początku swojej tury odnawia {percent} maksymalnego surowca.",
                GalduryteEffectType.CriticalAvoidance =>
                    $"{percent} szansy, że trafienie krytyczne zada zwykłe obrażenia.",
                _ when IsReduction(effect.EffectType) =>
                    $"{GetEffectName(effect.EffectType)}: −{percent}",
                _ => $"{GetEffectName(effect.EffectType)}: +{percent}"
            };
        }

        public static string FormatEffectRange(GalduryteEffectType type, float minimum, float maximum)
        {
            if (type == GalduryteEffectType.ActionValueOnAttack)
                return $"{GetEffectName(type)}: {minimum:0.##}–{maximum:0.##} jednostki czasu";
            if (type == GalduryteEffectType.CriticalDamageFlat)
                return $"{GetEffectName(type)}: +{minimum:0.##}–{maximum:0.##}×";
            var range = $"{FormatPercent(minimum)}–{FormatPercent(maximum)}";
            if (type == GalduryteEffectType.HealthPerTurn)
                return $"Na początku tury odnawia {range} maksymalnego zdrowia";
            if (type == GalduryteEffectType.ResourcePerTurn)
                return $"Na początku tury odnawia {range} maksymalnego surowca";
            if (IsReduction(type)) return $"{GetEffectName(type)}: {range} mniej";
            if (IsAttackChance(type) || type == GalduryteEffectType.CriticalAvoidance)
                return $"{GetEffectName(type)}: {range}";
            return $"{GetEffectName(type)}: +{range}";
        }

        public static string GetEffectName(GalduryteEffectType effectType) => effectType switch
        {
            GalduryteEffectType.GeneralDamagePercent => "Zadawane obrażenia",
            GalduryteEffectType.PhysicalDamagePercent => "Obrażenia fizyczne",
            GalduryteEffectType.MagicDamagePercent => "Obrażenia magiczne",
            GalduryteEffectType.CriticalChancePercent => "Kryt",
            GalduryteEffectType.CriticalDamageFlat => "Mnożnik obrażeń krytycznych",
            GalduryteEffectType.AccuracyPercent => "Szansa na trafienie",
            GalduryteEffectType.SuppressionChance => "Szansa na ogłuszenie, zamrożenie i podobne efekty",
            GalduryteEffectType.DebuffChance => "Szansa na osłabienie",
            GalduryteEffectType.DotChance => "Szansa na krwawienie, zatrucie lub podpalenie",
            GalduryteEffectType.BleedOnAttack => "Szansa na krwawienie przy trafieniu",
            GalduryteEffectType.PoisonOnAttack => "Szansa na zatrucie przy trafieniu",
            GalduryteEffectType.BurnOnAttack => "Szansa na podpalenie przy trafieniu",
            GalduryteEffectType.HealOnAttack => "Zdrowie odnawiane po trafieniu",
            GalduryteEffectType.ResourceOnAttack => "Surowiec odnawiany po trafieniu",
            GalduryteEffectType.ActionValueOnAttack => "Przyspieszenie następnego ruchu po trafieniu",
            GalduryteEffectType.ItemDropChance => "Szansa na znalezienie przedmiotu",
            GalduryteEffectType.ResourceGenerationPercent => "Generacja surowca",
            GalduryteEffectType.UndeadDamagePercent => "Obrażenia przeciwko nieumarłym",
            GalduryteEffectType.HumanDamagePercent => "Obrażenia przeciwko ludziom",
            GalduryteEffectType.BeastDamagePercent => "Obrażenia przeciwko bestiom",
            GalduryteEffectType.DemonDamagePercent => "Obrażenia przeciwko demonom",
            GalduryteEffectType.StunOnAttack => "Szansa na ogłuszenie przy trafieniu",
            GalduryteEffectType.FreezeOnAttack => "Szansa na zamrożenie przy trafieniu",
            GalduryteEffectType.SlowOnAttack => "Szansa na spowolnienie przy trafieniu",
            GalduryteEffectType.ExperiencePercent => "Zdobywane doświadczenie",
            GalduryteEffectType.GoldPercent => "Zdobywane złoto",
            GalduryteEffectType.PhysicalPenetration => "Penetracja obrony fizycznej",
            GalduryteEffectType.MagicPenetration => "Penetracja obrony magicznej",
            GalduryteEffectType.MaxHealthPercent => "Maksymalne zdrowie",
            GalduryteEffectType.DodgePercent => "Unik",
            GalduryteEffectType.PhysicalDefensePercent => "Obrona fizyczna",
            GalduryteEffectType.MagicDefensePercent => "Obrona magiczna",
            GalduryteEffectType.AllDefensePercent => "Obrona fizyczna i magiczna",
            GalduryteEffectType.SpeedPercent => "Szybkość",
            GalduryteEffectType.ActionPointsPercent => "Maksymalne punkty akcji",
            GalduryteEffectType.DotResistance => "Odporność na krwawienie, zatrucie i podpalenie",
            GalduryteEffectType.SuppressionResistance => "Odporność na ogłuszenie, zamrożenie i podobne efekty",
            GalduryteEffectType.DebuffResistance => "Odporność na osłabienia",
            GalduryteEffectType.AllResistance => "Wszystkie odporności",
            GalduryteEffectType.PhysicalDamageReduction => "Otrzymywane obrażenia fizyczne",
            GalduryteEffectType.MagicDamageReduction => "Otrzymywane obrażenia magiczne",
            GalduryteEffectType.AllDamageReduction => "Wszystkie otrzymywane obrażenia",
            GalduryteEffectType.MaxResourcePercent => "Maksymalny surowiec",
            GalduryteEffectType.BleedDamageReduction => "Otrzymywane obrażenia od krwawienia",
            GalduryteEffectType.PoisonDamageReduction => "Otrzymywane obrażenia od zatrucia",
            GalduryteEffectType.BurnDamageReduction => "Otrzymywane obrażenia od podpalenia",
            GalduryteEffectType.ResourceCostReduction => "Koszt surowca umiejętności",
            GalduryteEffectType.CriticalAvoidance => "Szansa na nieotrzymanie ciosu krytycznego",
            GalduryteEffectType.HealthPerTurn => "Zdrowie odnawiane na początku tury",
            GalduryteEffectType.ResourcePerTurn => "Surowiec odnawiany na początku tury",
            _ => effectType.ToString()
        };

        public static string FormatPercent(float value) =>
            $"{value * 100f:0.##}%";
    }
}
