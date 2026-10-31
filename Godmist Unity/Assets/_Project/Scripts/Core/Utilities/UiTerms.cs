using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godmist.Core.Enums;

namespace Godmist.Core.Utilities
{
    /// <summary>
    /// Polish UI terminology. Presentation only: never changes identifiers, saved
    /// data, modifier operations or values. ResourceType.None means generic item copy.
    /// Keep names here; value formatting and gameplay calculations are separate.
    /// </summary>
    public static class UiTerms
    {
        public const string PercentagePoints = "pp";
        public const string ActionPoints = "Punkty akcji";
        public const string ActionPointsShort = "PA";
        public const string Health = "Zdrowie";
        public const string HealthShort = "HP";
        public const string Attack = "Atak";
        public const string Stacks = "Nałożenia";
        public const string Charges = "Ładunki";
        public const string Duration = "Czas trwania";
        public const string IncomingCritChance = "Podatność na trafienia krytyczne";
        public const string IncomingCritDamage = "Otrzymywane obrażenia krytyczne";
        public const string Defenses = "Obrona fizyczna i magiczna";
        public const string PeriodicDamageReceived = "Otrzymywane obrażenia okresowe";
        public const string DamageNextDirectHit = "Obrażenia następnego bezpośredniego trafienia";
        public const string RemainingHealing = "Pozostałe leczenie";
        public const string FreeSteps = "Darmowe kroki w tej turze";
        public const string Upkeep = "Podtrzymanie";
        public const string Healing = "Leczenie";
        public const string ResourceRecovery = "Odzysk zasobu";
        public const string TargetPriority = "Priorytet celu dla przeciwników";
        public static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

        public static string Resource(ResourceType type, bool genitive = false, bool accusative = false) => type switch
        {
            ResourceType.Rage => genitive ? "furii" : accusative ? "furię" : "Furia",
            ResourceType.Momentum => genitive ? "impetu" : accusative ? "impet" : "Impet",
            ResourceType.Mana => genitive ? "many" : accusative ? "manę" : "Mana",
            _ => genitive ? "zasobu" : accusative ? "zasób" : "Zasób"
        };

        public static string Stat(CharacterStatType type, ResourceType resource = ResourceType.None)
        {
            if (type == CharacterStatType.MaxResource && resource != ResourceType.None)
                return resource == ResourceType.Momentum ? "Maksymalny impet" : "Maksymalna " + Resource(resource).ToLowerInvariant();
            if (type == CharacterStatType.ResourceRegeneration)
                return "Regeneracja " + Resource(resource, genitive: true);
            return type switch
            {
                CharacterStatType.MaxHealth => "Maksymalne zdrowie",
                CharacterStatType.MinAttack => "Minimalny atak",
                CharacterStatType.MaxAttack => "Maksymalny atak",
                CharacterStatType.PhysicalDefense => "Obrona fizyczna",
                CharacterStatType.MagicalDefense => "Obrona magiczna",
                CharacterStatType.Dodge => "Unik",
                CharacterStatType.CritChance => "Szansa krytyczna",
                CharacterStatType.CritDamage => "Obrażenia krytyczne",
                CharacterStatType.Speed => "Szybkość",
                CharacterStatType.ResourceRegeneration => "Regeneracja zasobu",
                CharacterStatType.WeaponAccuracy => "Celność",
                CharacterStatType.MaxResource => "Maksymalny zasób",
                CharacterStatType.PhysicalDefensePenetration => "Penetracja fizyczna",
                CharacterStatType.MagicalDefensePenetration => "Penetracja magiczna",
                CharacterStatType.HealingPower => "Siła leczenia",
                _ => type.ToString()
            };
        }

        public static string Resistance(ResistanceType type, bool subjectOnly = false)
        {
            var subject = type switch
            {
                ResistanceType.Bleed => "krwawienie",
                ResistanceType.Poison => "zatrucie",
                ResistanceType.Fire => "podpalenie",
                ResistanceType.Weakening => "osłabienia",
                ResistanceType.Suppression => "kontrolę",
                ResistanceType.Move => "przemieszczenie",
                ResistanceType.All => "wszystkie efekty",
                _ => type.ToString()
            };
            return subjectOnly ? subject : type == ResistanceType.All ? "Wszystkie odporności" : "Odporność na " + subject;
        }

        // The section heading already says "Odporności"; keep its rows compact.
        public static string ResistanceRow(ResistanceType type) => type switch
        {
            ResistanceType.Suppression => "Kontrola",
            ResistanceType.All => Resistance(type),
            _ => Capitalize(Resistance(type, subjectOnly: true))
        };

        private static string Capitalize(string value) => char.ToUpperInvariant(value[0]) + value.Substring(1);

        public static string Variable(GameplayVariableType type, string referenceId = null, ResourceType resource = ResourceType.None) => type switch
        {
            GameplayVariableType.DamageDealtPercent => "Zadawane obrażenia",
            GameplayVariableType.DamageReceivedPercent => "Otrzymywane obrażenia",
            GameplayVariableType.ExperienceGainPercent => "Zdobywane doświadczenie",
            GameplayVariableType.GoldGainPercent => "Zdobywane złoto",
            GameplayVariableType.ResourceGenerationPercent => "Generowanie " + Resource(resource, genitive: true),
            GameplayVariableType.HitChanceBonus => "Szansa trafienia",
            GameplayVariableType.DamageAgainstTag => "Obrażenia przeciwko " + (referenceId switch
            { "Undead" => "nieumarłym", "Human" => "ludziom", "Beast" => "bestiom", "Demon" => "demonom", _ => referenceId }),
            GameplayVariableType.DamageInEnvironment => "Obrażenia w terenie " + (referenceId switch
            { "Indoor" => "zamkniętym", "Outdoor" => "otwartym", "Warm" => "ciepłym", "Cold" => "zimnym", _ => referenceId }),
            GameplayVariableType.DamageDealtPercentInDungeonTheme => string.IsNullOrWhiteSpace(referenceId)
                ? "Zadawane obrażenia w lochu" : "Zadawane obrażenia w " + DungeonLocative(referenceId),
            _ => type.ToString()
        };

        public static string DungeonLocative(string id) => id?.Trim().ToLowerInvariant() switch
        {
            "catacombs" => "Katakumbach", "forest" => "Lesie", "elvenruins" => "Elfiańskich Ruinach",
            "docks" => "Dokach", "desert" => "Pustyni", "temple" => "Świątyni", "mountains" => "Górach",
            "swamp" => "Bagnach", "story" => "Lochu Fabularnym", _ => id
        };

        public static string Galduryte(GalduryteEffectType type) => type switch
        {
            GalduryteEffectType.GeneralDamagePercent => Variable(GameplayVariableType.DamageDealtPercent),
            GalduryteEffectType.CriticalChancePercent => Stat(CharacterStatType.CritChance),
            GalduryteEffectType.AccuracyPercent => Variable(GameplayVariableType.HitChanceBonus),
            GalduryteEffectType.MaxHealthPercent => Stat(CharacterStatType.MaxHealth),
            GalduryteEffectType.PhysicalDefensePercent => Stat(CharacterStatType.PhysicalDefense),
            GalduryteEffectType.SpeedPercent => Stat(CharacterStatType.Speed),
            GalduryteEffectType.PhysicalDamagePercent => "Obrażenia fizyczne",
            GalduryteEffectType.MagicDamagePercent => "Obrażenia magiczne",
            GalduryteEffectType.CriticalDamageFlat => Stat(CharacterStatType.CritDamage),
            GalduryteEffectType.SuppressionChance => "Szansa na kontrolę",
            GalduryteEffectType.DebuffChance => "Szansa na osłabienie",
            GalduryteEffectType.DotChance => "Szansa na krwawienie, zatrucie lub podpalenie",
            GalduryteEffectType.BleedOnAttack => "Krwawienie przy trafieniu",
            GalduryteEffectType.PoisonOnAttack => "Zatrucie przy trafieniu",
            GalduryteEffectType.BurnOnAttack => "Podpalenie przy trafieniu",
            GalduryteEffectType.HealOnAttack => "Leczenie przy trafieniu",
            GalduryteEffectType.ResourceOnAttack => "Odzysk zasobu przy trafieniu",
            GalduryteEffectType.ActionValueOnAttack => "Przyspieszenie przy trafieniu",
            GalduryteEffectType.ItemDropChance => "Szansa znalezienia przedmiotu",
            GalduryteEffectType.ResourceGenerationPercent => Variable(GameplayVariableType.ResourceGenerationPercent),
            GalduryteEffectType.UndeadDamagePercent => "Obrażenia przeciwko nieumarłym",
            GalduryteEffectType.HumanDamagePercent => "Obrażenia przeciwko ludziom",
            GalduryteEffectType.BeastDamagePercent => "Obrażenia przeciwko bestiom",
            GalduryteEffectType.DemonDamagePercent => "Obrażenia przeciwko demonom",
            GalduryteEffectType.StunOnAttack => "Ogłuszenie przy trafieniu",
            GalduryteEffectType.FreezeOnAttack => "Zamrożenie przy trafieniu",
            GalduryteEffectType.SlowOnAttack => "Spowolnienie przy trafieniu",
            GalduryteEffectType.ExperiencePercent => Variable(GameplayVariableType.ExperienceGainPercent),
            GalduryteEffectType.GoldPercent => Variable(GameplayVariableType.GoldGainPercent),
            GalduryteEffectType.PhysicalPenetration => Stat(CharacterStatType.PhysicalDefensePenetration),
            GalduryteEffectType.MagicPenetration => Stat(CharacterStatType.MagicalDefensePenetration),
            GalduryteEffectType.DodgePercent => Stat(CharacterStatType.Dodge),
            GalduryteEffectType.MagicDefensePercent => Stat(CharacterStatType.MagicalDefense),
            GalduryteEffectType.AllDefensePercent => Defenses,
            GalduryteEffectType.ActionPointsPercent => "Maksymalne punkty akcji",
            GalduryteEffectType.DotResistance => "Odporność na krwawienie, zatrucie i podpalenie",
            GalduryteEffectType.SuppressionResistance => Resistance(ResistanceType.Suppression),
            GalduryteEffectType.DebuffResistance => Resistance(ResistanceType.Weakening),
            GalduryteEffectType.AllResistance => Resistance(ResistanceType.All),
            GalduryteEffectType.PhysicalDamageReduction => "Otrzymywane obrażenia fizyczne",
            GalduryteEffectType.MagicDamageReduction => "Otrzymywane obrażenia magiczne",
            GalduryteEffectType.AllDamageReduction => Variable(GameplayVariableType.DamageReceivedPercent),
            GalduryteEffectType.MaxResourcePercent => Stat(CharacterStatType.MaxResource),
            GalduryteEffectType.BleedDamageReduction => "Otrzymywane obrażenia od krwawienia",
            GalduryteEffectType.PoisonDamageReduction => "Otrzymywane obrażenia od zatrucia",
            GalduryteEffectType.BurnDamageReduction => "Otrzymywane obrażenia od podpalenia",
            GalduryteEffectType.ResourceCostReduction => "Koszt zasobu umiejętności",
            GalduryteEffectType.CriticalAvoidance => "Szansa uniknięcia trafienia krytycznego",
            GalduryteEffectType.HealthPerTurn => "Leczenie na początku tury",
            GalduryteEffectType.ResourcePerTurn => "Odzysk zasobu na początku tury",
            _ => type.ToString()
        };

        public static string Status(string id, string fallback = null) => id?.ToLowerInvariant() switch
        {
            "bleed" => "Krwawienie", "poison" => "Zatrucie", "burn" => "Podpalenie", "slow" => "Spowolnienie",
            "stun" => "Ogłuszenie", "freeze" => "Zamrożenie", "sleep" => "Sen", "paralysis" => "Paraliż",
            "immobilize" => "Unieruchomienie", "chill" => "Oziębienie", "taunt" => "Prowokacja",
            "weakening" => "Osłabienie", "invisibility" => "Niewidzialność", "regeneration" => "Regeneracja",
            "counterattack" => "Kontratak", _ => fallback ?? id ?? string.Empty
        };

        public static string GlossaryTitle(string id, string fallback) => id?.ToLowerInvariant() switch
        {
            "ap" => ActionPoints, "stacks" => Stacks, "duration" => Duration, "upkeep" => Upkeep,
            "speed" => Stat(CharacterStatType.Speed),
            "defense.physical" => Stat(CharacterStatType.PhysicalDefense),
            "defense.magical" => Stat(CharacterStatType.MagicalDefense),
            "crit-chance" => Stat(CharacterStatType.CritChance), "crit-damage" => Stat(CharacterStatType.CritDamage),
            "resource-regeneration" => Stat(CharacterStatType.ResourceRegeneration),
            "resource-recovery" => ResourceRecovery,
            "control-resistance" => Resistance(ResistanceType.Suppression),
            "fire-resistance" => Resistance(ResistanceType.Fire),
            "dot-resistance" => Galduryte(GalduryteEffectType.DotResistance),
            _ => Status(id, Present(fallback))
        };

        public static string Damage(DamageType type, bool genitive = false) => type switch
        {
            DamageType.Physical => genitive ? "fizycznych" : "Obrażenia fizyczne",
            DamageType.Magical => genitive ? "magicznych" : "Obrażenia magiczne",
            DamageType.Inevitable => genitive ? "nieuchronnych" : "Obrażenia nieuchronne",
            DamageType.Direct => genitive ? "bezpośrednich" : "Obrażenia bezpośrednie",
            DamageType.Bleed => genitive ? "od krwawienia" : "Obrażenia od krwawienia",
            DamageType.Poison => genitive ? "od zatrucia" : "Obrażenia od zatrucia",
            DamageType.Burn => genitive ? "od podpalenia" : "Obrażenia od podpalenia",
            _ => type.ToString()
        };

        // Compatibility vocabulary for authored copy in existing assets and old saves.
        // Only complete lexical aliases are replaced; rich-text tags/IDs stay untouched.
        // This is not a number parser: values, operations and sentence structure remain intact.
        private static readonly (Regex Pattern, string Replacement)[] Aliases = BuildAliases();

        private static (Regex, string)[] BuildAliases()
        {
            var aliases = new List<(Regex, string)>();
            void Add(string old, string replacement) => aliases.Add((new Regex(@"(?<![\p{L}\p{N}_.\\/])" + Regex.Escape(old) + @"(?![\p{L}\p{N}_]|[.\\/][\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), replacement));
            Add("Penetracja obrony fizycznej", "Penetracja fizyczna");
            Add("Penetracja obrony magicznej", "Penetracja magiczna");
            Add("Przebicie obrony fizycznej", "Penetracja fizyczna");
            Add("Przebicie obrony magicznej", "Penetracja magiczna");
            Add("przebicia obrony fizycznej", "penetracji fizycznej");
            Add("przebicia obrony magicznej", "penetracji magicznej");
            Add("Przebicie fizyczne", "Penetracja fizyczna");
            Add("Przebicie magiczne", "Penetracja magiczna");
            Add("Szansa na trafienie krytyczne", "Szansa krytyczna");
            Add("szansy na trafienie krytyczne", "szansy krytycznej");
            Add("Szansa na kryt.", "Szansa krytyczna");
            Add("Szansa kryt.", "Szansa krytyczna");
            Add("Mnożnik obrażeń krytycznych", "Obrażenia krytyczne");
            Add("Obrazenia kryt.", "Obrażenia krytyczne");
            Add("Obrazenia krytyczne", "Obrażenia krytyczne");
            Add("Obrazenia otrzymywane", "Otrzymywane obrażenia");
            Add("Obrażenia zadawane", "Zadawane obrażenia");
            Add("Obrażenia otrzymywane", "Otrzymywane obrażenia");
            Add("Wszystkie otrzymywane obrażenia", "Otrzymywane obrażenia");
            Add("Szansa na trafienie", "Szansa trafienia");
            Add("Celność broni", "Celność");
            Add("Celnosc", "Celność");
            Add("Obrona fiz.", "Obrona fizyczna");
            Add("Obrona mag.", "Obrona magiczna");
            Add("Atak min.", "Minimalny atak");
            Add("Atak maks.", "Maksymalny atak");
            Add("Szybkosc", "Szybkość");
            Add("Moc leczenia", "Siła leczenia");
            Add("mocy leczenia", "siły leczenia");
            Add("Maks. zasob", "Maksymalny zasób");
            Add("Maks. zdrowie", "Maksymalne zdrowie");
            Add("Generacja surowca", "Generowanie zasobu");
            Add("Generacja zasobu", "Generowanie zasobu");
            Add("surowca", "zasobu");
            Add("surowiec", "zasób");
            Add("surowcem", "zasobem");
            aliases.Add((new Regex(@"(?<![\p{L}\p{N}_])Wściekłość(?![\p{L}\p{N}_])"), "Furia"));
            Add("wściekłości", "furii");
            Add("wściekłość", "furię");
            Add("odporność na ruch", "odporność na przemieszczenie");
            Add("odporności na ruch", "odporności na przemieszczenie");
            Add("odporność na ogień", "odporność na podpalenie");
            Add("odporności na ogień", "odporności na podpalenie");
            Add("odporność na osłabienie", "odporność na osłabienia");
            Add("odporności na osłabienie", "odporności na osłabienia");
            Add("Płonięcie", "Podpalenie");
            Add("płonięcia", "podpalenia");
            Add("płonięciu", "podpaleniu");
            Add("wychłodzenie", "oziębienie");
            Add("wychłodzenia", "oziębienia");
            Add("nieuniknione", "nieuchronne");
            Add("nieuniknionych", "nieuchronnych");
            Add("humanoidom", "ludziom");
            Add("Stosy", "Nałożenia");
            Add("stosów", "nałożeń");
            Add("stos.", "nał.");
            Add("Utrzymanie", "Podtrzymanie");
            Add("utrzymania", "podtrzymania");
            Add("p.p.", "pp");
            Add("AP", "PA");
            Add("PW", "furii");
            Add("MP", "many");
            aliases.Add((new Regex(@"(?<![\p{L}\p{N}_])PP(?![\p{L}\p{N}_])"), "impetu"));
            return aliases.ToArray();
        }

        public static string Present(string text, ResourceType resource = ResourceType.None)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            // Split off tags to protect link IDs, sprite names and other TMP markup.
            return Regex.Replace(text, @"<[^>]*>|[^<]+|<", part =>
            {
                if (part.Value.StartsWith("<", StringComparison.Ordinal)) return part.Value;
                var value = part.Value;
                foreach (var alias in Aliases)
                    value = alias.Pattern.Replace(value, match => MatchCase(match.Value, alias.Replacement));
                if (resource != ResourceType.None)
                {
                    var maximum = Stat(CharacterStatType.MaxResource, resource);
                    value = ReplaceTerm(value, "Maksymalny zasób", maximum);
                    value = ReplaceTerm(value, "maksymalnego zasobu", resource == ResourceType.Momentum ? "maksymalnego impetu" : "maksymalnej " + Resource(resource, genitive: true));
                    value = ReplaceTerm(value, "zasobu", Resource(resource, genitive: true));
                    value = Regex.Replace(value, @"\b(przywraca|odnawia|odzyskuje|zużywa|odbiera) zasób\b",
                        match => match.Groups[1].Value + " " + Resource(resource, accusative: true),
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    value = ReplaceTerm(value, "zasób", Resource(resource));
                }
                return value;
            });
        }

        private static string ReplaceTerm(string value, string old, string replacement) => Regex.Replace(value,
            @"(?<![\p{L}\p{N}_])" + Regex.Escape(old) + @"(?![\p{L}\p{N}_])",
            match => MatchCase(match.Value, replacement), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static string MatchCase(string source, string replacement)
        {
            // Old resource acronyms become inflected nouns after values, not capitals.
            if (source is "PW" or "MP" or "PP") return replacement;
            return char.IsUpper(source[0])
                ? char.ToUpperInvariant(replacement[0]) + replacement.Substring(1)
                : char.ToLowerInvariant(replacement[0]) + replacement.Substring(1);
        }
    }
}
