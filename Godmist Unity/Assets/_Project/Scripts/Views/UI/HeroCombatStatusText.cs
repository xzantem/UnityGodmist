using Godmist.Core.Utilities;
using System;
using System.Linq;
using System.Text;
using Godmist.Core.Enums;
using Godmist.Runtime.Combat;
using Godmist.Runtime.Effects;
using Godmist.Systems.Combat;

namespace Godmist.Views.UI
{
    /// <summary>Read-only presentation of hero effects, including effects restored with legacy labels.</summary>
    public static class HeroCombatStatusText
    {
        public static string Key(string id)
        {
            if (id?.StartsWith("mage.sacrifice.", StringComparison.Ordinal) == true) return "mage.sacrifice";
            if (id?.StartsWith("mage.adaptation.", StringComparison.Ordinal) == true) return "mage.adaptation";
            if (id?.StartsWith("mage.erosion.", StringComparison.Ordinal) == true) return "mage.erosion";
            if (id?.EndsWith(".paladin.slow", StringComparison.Ordinal) == true) return "paladin.field_slow";
            return id;
        }

        public static string Name(string id, string fallback = null) => Key(id) switch
        {
            "mage.sacrifice" => "Przejęcie Mocy",
            "mage.adaptation" => "Adaptacja Energetyczna",
            "mage.erosion" => "Wypalanie Odporności",
            "mage.tiring" => "Wycieńczające Zaklęcia",
            "warrior.strategy" => "Marsz Taktyczny",
            "warrior.amok" => "Amok",
            "warrior.shortage" => "Niedobór furii",
            "scout.knee_throw" => "Rzut w Kolano",
            "paladin.universal" => "Wszechstronna Łaska",
            "paladin.zeal.base" or "paladin.zeal.extra" => "Ożywcza Łaska",
            "paladin.torment" => "Udręczenie",
            "paladin.exposure" => "Odsłonięcie Słabości",
            "paladin.anointed" => "Namaszczony Wojownik",
            "paladin.field_slow" => "Pole Osądu",
            _ => fallback == null ? (UiTerms.Status(id) == id ? null : UiTerms.Status(id)) : UiTerms.Status(id, fallback)
        };

        public static string IconSource(string id) => Key(id) switch
        {
            "mage.sacrifice" => "skill.mage.sacrifice",
            "mage.adaptation" => "node.mage.warlock.adaptation",
            "mage.erosion" => "node.mage.fire.erosion",
            "mage.tiring" => "skill.mage.tiring",
            "warrior.amok" => "node.warrior.brawler.amok",
            "warrior.shortage" => "node.warrior.brawler.rage",
            "scout.knee_throw" => "node.scout.blade_dancer.knee_throw",
            "paladin.universal" => "node.paladin.priest.universal_grace",
            "paladin.zeal.base" or "paladin.zeal.extra" => "node.paladin.priest.battle_zeal",
            "paladin.torment" => "node.paladin.crusader.torment",
            "paladin.exposure" => "node.paladin.crusader.expose",
            "paladin.anointed" => "node.paladin.priest.anointed_warrior",
            "paladin.field_slow" => "skill.paladin.judgement_field",
            _ => null
        };

        public static bool IsFieldSlow(string id) => Key(id) == "paladin.field_slow";
        public static string Percent(float value) => (value * 100f).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + "%";

        public static string Describe(PassiveEffectRuntime effect, BattleUnit owner, CombatSession session = null)
        {
            if (effect == null) return string.Empty;
            var text = new StringBuilder();
            void Line(string value) { if (text.Length > 0) text.AppendLine(); text.Append(value); }
            var id = Key(effect.EffectId);
            var sourceId = effect.Sources?.FirstOrDefault()?.SourceCharacterId;
            var source = owner?.Character?.CharacterId == sourceId ? owner
                : session?.Units.FirstOrDefault(unit => unit.Character?.CharacterId == sourceId);
            switch (id)
            {
                case "scout.weak_point" when owner != null:
                    Line(UiTerms.IncomingCritChance + ": +10 pp");
                    Line(UiTerms.IncomingCritDamage + ": +" + (owner.ScoutState.WeakPointCritDamage * 100f).ToString("0.##", UiTerms.PolishCulture) + " pp");
                    break;
                case "scout.analysis" when owner != null:
                    Line(UiTerms.Stat(CharacterStatType.PhysicalDefensePenetration) + ": +" + Percent(owner.ScoutState.AnalysisPenetration) + ".");
                    break;
                case "scout.anger" when owner != null:
                    foreach (var group in owner.ScoutState.AngerBySource.GroupBy(entry => entry.Key.IsPlayerSide))
                    {
                        foreach (var entry in group)
                            Line("Złość od " + (entry.Key.Character?.Definition?.DisplayName ?? "zwiadowcy") + ": " + entry.Value + "/100.");
                        var bonus = group.Max(entry => entry.Value * (.0015f + .0005f * Math.Min(2, ScoutCombatSystem.BladeRank(entry.Key, "strong_insult"))));
                        Line("Obrażenia otrzymywane od " + (group.Key ? "drużyny" : "przeciwników") + ": +" + Percent(bonus) + ".");
                    }
                    break;
                case "scout.embedded_knives" when owner != null:
                    foreach (var knives in owner.ScoutState.Knives.GroupBy(knife => new { knife.Owner, knife.BleedMultiplier }))
                        Line("Sztylety: " + knives.Count() + ". Wyjęcie: 90% bazowej szansy na Krwawienie " + Percent(knives.Key.BleedMultiplier) + " średniego ataku na turę przez 3 tury.");
                    Line("Wyjęcie zużywa jeden wbity sztylet.");
                    break;
                case "warrior.order" when owner != null:
                    var order = owner.WarriorState.OrderSource;
                    Line(UiTerms.Variable(GameplayVariableType.DamageReceivedPercent) + " od sojuszników wydającego rozkaz" + ": +" + Percent(.2f + .05f * WarriorCombatSystem.C(order, "breakdown")) + ".");
                    Line(UiTerms.IncomingCritChance + " od tej strony" + ": +" + Percent(.5f * WarriorCombatSystem.C(order, "marking")) + " wartości bazowej.");
                    Line(UiTerms.Defenses + ": -" + Percent(1 - WarriorCombatSystem.DefenseMultiplier(owner, session ?? owner.WarriorState.Session)) + ".");
                    break;
                case "warrior.protection" when owner != null:
                    Line(UiTerms.Variable(GameplayVariableType.DamageReceivedPercent) + ": -" + Percent(1 - WarriorCombatSystem.Protection(session ?? owner.WarriorState.Session, owner)) + ".");
                    if (owner.WarriorState.Riposte)
                    {
                        Line("Riposta: 30% obrażeń przed obroną wraca do atakującego jako obrażenia nieuchronne.");
                        Line(UiTerms.TargetPriority + ": +" + Percent(WarriorCombatSystem.TargetPriority(session ?? owner.WarriorState.Session, owner) - 1) + ".");
                    }
                    break;
                case "warrior.trauma" when owner != null:
                    Line(UiTerms.PeriodicDamageReceived + ": +" + Percent(owner.WarriorState.TraumaBonus) + ".");
                    break;
                case "warrior.strategy" when owner != null:
                    Line(UiTerms.FreeSteps + ": " + WarriorCombatSystem.FreeSteps(owner) + "/2.");
                    break;
                case "paladin.holy_regeneration":
                    var remaining = effect.GetStateValue("remaining")?.IntegerValue;
                    if (remaining.HasValue)
                    {
                        Line(UiTerms.RemainingHealing + ": " + remaining.Value + " HP.");
                        Line("Na końcu następnej tury: " + (int)Math.Ceiling(remaining.Value / (double)Math.Max(1, effect.RemainingDuration)) + " HP przed ograniczeniem do maksymalnego zdrowia.");
                    }
                    var lasting = effect.GetStateValue("lasting")?.IntegerValue ?? 0;
                    if (lasting > 0 && effect.GetStateValue("tail")?.BooleanValue != true)
                        Line("Trwała Regeneracja: następnie " + (int)Math.Floor((effect.GetStateValue("total")?.IntegerValue ?? 0) * .25f) + " HP przez " + (lasting == 1 ? 4 : 2) + " tury.");
                    break;
                case "paladin.blessing":
                    var grace = effect.GetStateValue("grace")?.IntegerValue ?? 0;
                    Line("Łaska: " + grace + "/6.");
                    if (source != null)
                    {
                        var rank = PaladinCombatSystem.P(source, "grace_collection");
                        Line("Odzysk zasobu przy wygaśnięciu: " + (4 * rank * grace) + " (" + (4 * rank) + " za ładunek, przed limitem zasobu).");
                        if (PaladinCombatSystem.P(source, "salvation") > 0)
                            Line("Łaska Zwrotna: rzucający odzyska 50% zasobu faktycznie odnowionego celowi.");
                    }
                    break;
                case "paladin.exposure":
                    Line(UiTerms.DamageNextDirectHit + ": +" + Percent((float)(effect.GetStateValue("strength")?.NumberValue ?? 0)) + ".");
                    Line(UiTerms.Charges + ": 1. Zużywa się przy następnym bezpośrednim ataku.");
                    break;
                case "paladin.field_slow":
                    Line("Działa podczas przebywania w obszarze Pola Osądu.");
                    break;
                case "mage.adaptation" when owner != null:
                    var attackerId = effect.EffectId.Substring("mage.adaptation.".Length);
                    if (owner.MageState.Adaptation.TryGetValue(attackerId, out var adaptation))
                    {
                        var attacker = session?.Units.FirstOrDefault(unit => unit.Character?.CharacterId == attackerId);
                        Line("Redukcja obrażeń od " + (attacker?.Character?.Definition?.DisplayName ?? "źródła efektu") + ": " + adaptation.Reduction + ".");
                        Line("Pozostałe trafienia: " + adaptation.RemainingHits + "/3.");
                    }
                    break;
                case "mage.erosion" when owner != null:
                    if (owner.MageState.FireErosion.TryGetValue(effect.EffectId.Substring("mage.erosion.".Length), out var hits))
                        Line(UiTerms.Stacks + ": " + hits + "/5.");
                    break;
            }
            var unique = effect.GetPayload<UniqueCombatBuffPayload>();
            switch (unique?.BuffType)
            {
                case UniqueBuffType.GuaranteedCriticalHit: Line(UiTerms.Charges + ": 1. Następny atak zawsze trafia krytycznie."); break;
                case UniqueBuffType.GuaranteedDodge: Line(UiTerms.Charges + ": 1. Następny atak przeciwko tej postaci zawsze chybia."); break;
                case UniqueBuffType.MaximumDamage: Line(UiTerms.Charges + ": 1. Następny atak zada maksymalne obrażenia."); break;
                case UniqueBuffType.MageMaximumDamage: Line("Ataki zadają maksymalne obrażenia przez czas działania efektu."); break;
                case UniqueBuffType.Hawkeye: Line("Ataki nie chybiają. Obrażenia zwiększone o 5% za każde pole odległości."); break;
            }
            if (unique?.BuffType == UniqueBuffType.MageOffering && owner != null)
            {
                var bonus = MageCombatSystem.OfferingBonus(owner);
                Line(UiTerms.Variable(GameplayVariableType.DamageDealtPercent) + ": +" + Percent(bonus) + ".");
                Line(UiTerms.Variable(GameplayVariableType.DamageReceivedPercent) + ": +" + Percent(bonus * (1 - .1f * MageCombatSystem.W(owner, "calm"))) + ".");
            }
            if (owner != null && effect.Sources?.Any(item => item.SourceId?.StartsWith(WarriorCombatSystem.DeathWish + ".", StringComparison.Ordinal) == true) == true
                && effect.VariableModifiers.Any(modifier => modifier.VariableType == GameplayVariableType.DamageDealtPercent))
            {
                var skill = owner.Character.GetKnownSkills().FirstOrDefault(item => item.SkillId == WarriorCombatSystem.DeathWish);
                if (skill != null)
                    Line(UiTerms.Upkeep + ": " + ToggleSkillUpkeepCalculator.Calculate(skill, owner.Character.ResourcePool) + " furii na turę.");
                Line("Śmiertelne trafienie pozostawia 1 HP. Efekt kończy się przy braku zasobu na podtrzymanie.");
            }
            return UiTerms.Present(text.ToString(), Workspace.UIWorkspaceTooltipView.StatusResource(effect, owner));
        }
    }
}
