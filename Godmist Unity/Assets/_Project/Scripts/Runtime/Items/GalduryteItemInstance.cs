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

        public string GetEffectSummary() => GetEffects(false);

        public string GetEffectDetails() => GetEffects(true);

        private string GetEffects(bool detailed)
        {
            if (IsDestroyed) return "Zniszczony — nie daje efektów.";
            if (!IsIdentified) return "Niezidentyfikowany";
            if (Effects == null || Effects.Count == 0) return "Brak efektów";

            var lines = new List<string>();
            foreach (var effect in Effects)
                if (effect != null) lines.Add(detailed
                    ? GalduryteRuntimeText.FormatEffect(effect) : GalduryteRuntimeText.FormatEffectSummary(effect));
            if (lines.Count == 0) return "Brak efektów";
            if (detailed && Effects.Exists(effect => effect != null && GalduryteRuntimeText.IsAttackTrigger(effect.EffectType)))
                lines.Add(GalduryteRuntimeText.AttackTriggerRules);
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
        public const string AttackTriggerRules = "Efekty przy trafieniu działają raz na użycie umiejętności i nie są wyzwalane przez obrażenia okresowe. Udany efekt statusu obejmuje wszystkich trafionych wrogów.";

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

        // All item surfaces share these names, signs and units. Detailed mechanics
        // are separate explanatory content, never a window-specific effect list.
        public static string FormatEffectSummary(GalduryteEffectInstance effect) => effect == null
            ? string.Empty : $"{GetEffectName(effect.EffectType)}: {FormatEffectValue(effect.EffectType, effect.Value, effect.Value)}";

        public static string FormatEffectRange(GalduryteEffectType type, float minimum, float maximum) =>
            $"{GetEffectName(type)}: {FormatEffectValue(type, minimum, maximum)}";

        private static string FormatEffectValue(GalduryteEffectType type, float minimum, float maximum)
        {
            var absolute = type is GalduryteEffectType.ActionValueOnAttack or GalduryteEffectType.CriticalDamageFlat;
            var value = absolute ? $"{minimum:0.##}" : FormatPercent(minimum);
            if (minimum != maximum) value += "–" + (absolute ? $"{maximum:0.##}" : FormatPercent(maximum));
            if (type == GalduryteEffectType.ActionValueOnAttack) return value + " j. czasu";
            if (type == GalduryteEffectType.CriticalDamageFlat) return "+" + value + "×";
            if (type == GalduryteEffectType.HealOnAttack) return value + " obrażeń";
            if (type == GalduryteEffectType.ResourceOnAttack) return value + " maks.";
            if (type is GalduryteEffectType.HealthPerTurn or GalduryteEffectType.ResourcePerTurn)
                return "+" + value + " maks.";
            if (IsReduction(type)) return "−" + value;
            return (IsAttackChance(type) || type == GalduryteEffectType.CriticalAvoidance ? "" : "+") + value;
        }

        public static bool HasEffectDetails(GalduryteEffectType type) => IsAttackTrigger(type)
            || type is GalduryteEffectType.ItemDropChance or GalduryteEffectType.HealthPerTurn
                or GalduryteEffectType.ResourcePerTurn or GalduryteEffectType.CriticalAvoidance;

        public static string GetEffectName(GalduryteEffectType effectType) => effectType switch
        {
            GalduryteEffectType.GeneralDamagePercent => "Zadawane obrażenia",
            GalduryteEffectType.PhysicalDamagePercent => "Obrażenia fizyczne",
            GalduryteEffectType.MagicDamagePercent => "Obrażenia magiczne",
            GalduryteEffectType.CriticalChancePercent => "Szansa krytyczna",
            GalduryteEffectType.CriticalDamageFlat => "Mnożnik obrażeń krytycznych",
            GalduryteEffectType.AccuracyPercent => "Szansa na trafienie",
            GalduryteEffectType.SuppressionChance => "Szansa na kontrolę",
            GalduryteEffectType.DebuffChance => "Szansa na osłabienie",
            GalduryteEffectType.DotChance => "Szansa na krwawienie, zatrucie lub podpalenie",
            GalduryteEffectType.BleedOnAttack => "Krwawienie przy trafieniu",
            GalduryteEffectType.PoisonOnAttack => "Zatrucie przy trafieniu",
            GalduryteEffectType.BurnOnAttack => "Podpalenie przy trafieniu",
            GalduryteEffectType.HealOnAttack => "Leczenie przy trafieniu",
            GalduryteEffectType.ResourceOnAttack => "Surowiec przy trafieniu",
            GalduryteEffectType.ActionValueOnAttack => "Przyspieszenie przy trafieniu",
            GalduryteEffectType.ItemDropChance => "Znajdowanie przedmiotów",
            GalduryteEffectType.ResourceGenerationPercent => "Generacja surowca",
            GalduryteEffectType.UndeadDamagePercent => "Obrażenia przeciwko nieumarłym",
            GalduryteEffectType.HumanDamagePercent => "Obrażenia przeciwko ludziom",
            GalduryteEffectType.BeastDamagePercent => "Obrażenia przeciwko bestiom",
            GalduryteEffectType.DemonDamagePercent => "Obrażenia przeciwko demonom",
            GalduryteEffectType.StunOnAttack => "Ogłuszenie przy trafieniu",
            GalduryteEffectType.FreezeOnAttack => "Zamrożenie przy trafieniu",
            GalduryteEffectType.SlowOnAttack => "Spowolnienie przy trafieniu",
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
            GalduryteEffectType.SuppressionResistance => "Odporność na kontrolę",
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
            GalduryteEffectType.CriticalAvoidance => "Uniknięcie krytyka",
            GalduryteEffectType.HealthPerTurn => "Zdrowie na początku tury",
            GalduryteEffectType.ResourcePerTurn => "Surowiec na początku tury",
            _ => effectType.ToString()
        };

        public static string FormatPercent(float value) =>
            $"{value * 100f:0.##}%";
    }
}
