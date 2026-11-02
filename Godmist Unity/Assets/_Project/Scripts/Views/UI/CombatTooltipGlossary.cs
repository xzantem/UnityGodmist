using Godmist.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Godmist.Views.UI
{
    [CreateAssetMenu(fileName = "CombatTooltipGlossary", menuName = "Godmist/Combat/Tooltip Glossary")]
    public class CombatTooltipGlossary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string Id;
            public Sprite Icon;
            public string Title;
            [Tooltip("Dodatkowe formy nazwy wykrywane w opisach, np. 'jednostkę', 'jednostki'.")]
            public List<string> Aliases = new();
            [TextArea] public string Description;
        }

        [SerializeField] private List<Entry> entries = new();

        public static IReadOnlyList<Entry> BuiltInEntries => CombatTooltipGlossaryFallback.Entries;

        public bool TryGet(string id, out Entry result)
        {
            foreach (var entry in entries ?? new List<Entry>())
            {
                if (entry != null && string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    result = PresentEntry(entry);
                    return true;
                }
            }

            if (CombatTooltipGlossaryFallback.TryGet(id, out result)) return true;

            result = null;
            return false;
        }

        public static Entry PresentEntry(Entry entry)
        {
            if (entry == null) return null;
            var title = UiTerms.GlossaryTitle(entry.Id, entry.Title);
            var description = UiTerms.Present(entry.Description);
            if (title == entry.Title && description == entry.Description) return entry;
            var aliases = entry.Aliases == null ? new List<string>() : new List<string>(entry.Aliases);
            if (!string.IsNullOrWhiteSpace(entry.Title)) aliases.Add(entry.Title);
            return new Entry
            {
                Id = entry.Id, Icon = entry.Icon,
                Title = title,
                Description = description, Aliases = aliases
            };
        }

        public string Linkify(string richText) => Linkify(richText, null);

        /// <summary>
        /// Adds links for authored and code-backed glossary entries. The optional
        /// excluded id is used by nested entry cards to prevent a card from linking
        /// back to itself and growing an endless tooltip chain.
        /// </summary>
        public string Linkify(string richText, string excludedId)
        {
            return LinkifyEntries(richText, entries, excludedId);
        }

        /// <summary>
        /// Null-glossary fallback for scenes or prefabs whose serialized reference
        /// has not been assigned yet. It intentionally shares the same parser as an
        /// authored glossary so link behavior stays identical on both paths.
        /// </summary>
        public static string LinkifyFallback(string richText, string excludedId = null)
        {
            return LinkifyEntries(richText, null, excludedId);
        }

        private static string LinkifyEntries(
            string richText,
            IEnumerable<Entry> authoredEntries,
            string excludedId)
        {
            if (string.IsNullOrWhiteSpace(richText)) return richText ?? string.Empty;

            var candidates = new List<(Entry Entry, string Term, int Priority, int Order)>();
            var authoredEntriesById = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var candidateOrder = 0;
            foreach (var entry in authoredEntries ?? Array.Empty<Entry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                authoredEntriesById[entry.Id] = entry;
                AddCandidate(candidates, entry, UiTerms.GlossaryTitle(entry.Id, entry.Title), 0, ref candidateOrder);
                AddCandidate(candidates, entry, entry.Title, 0, ref candidateOrder);
                if (entry.Aliases != null)
                    foreach (var alias in entry.Aliases)
                        AddCandidate(candidates, entry, alias, 0, ref candidateOrder);
            }

            foreach (var entry in CombatTooltipGlossaryFallback.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                if (authoredEntriesById.TryGetValue(entry.Id, out var authoredEntry))
                {
                    // Authored title/copy/icon remain authoritative; fallback
                    // aliases only fill Polish inflections missing from the asset.
                    if (entry.Aliases != null)
                        foreach (var alias in entry.Aliases)
                        {
                            AddCandidate(candidates, authoredEntry, alias, 1, ref candidateOrder);
                            AddCandidate(candidates, authoredEntry, UiTerms.Present(alias), 1, ref candidateOrder);
                        }
                    continue;
                }

                AddCandidate(candidates, entry, entry.Title, 1, ref candidateOrder);
                if (entry.Aliases != null)
                    foreach (var alias in entry.Aliases)
                    {
                        AddCandidate(candidates, entry, alias, 1, ref candidateOrder);
                        AddCandidate(candidates, entry, UiTerms.Present(alias), 1, ref candidateOrder);
                    }
            }

            candidates.Sort((left, right) =>
            {
                var length = right.Term.Length.CompareTo(left.Term.Length);
                if (length != 0) return length;
                var semantic = EntryPriority(left.Entry).CompareTo(EntryPriority(right.Entry));
                if (semantic != 0) return semantic;
                var priority = left.Priority.CompareTo(right.Priority);
                return priority != 0 ? priority : left.Order.CompareTo(right.Order);
            });
            var result = new StringBuilder(richText.Length + 32);
            var linkDepth = 0;
            var linkedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < richText.Length;)
            {
                if (richText[index] == '<')
                {
                    var tagEnd = richText.IndexOf('>', index);
                    if (tagEnd >= 0)
                    {
                        var tag = richText.Substring(index, tagEnd - index + 1);
                        if (tag.StartsWith("<link", StringComparison.OrdinalIgnoreCase))
                        {
                            linkDepth++;
                            if (TryReadLinkId(tag, out var linkId)) linkedIds.Add(linkId);
                        }
                        else if (tag.StartsWith("</link", StringComparison.OrdinalIgnoreCase)) linkDepth = Mathf.Max(0, linkDepth - 1);
                        result.Append(tag);
                        index = tagEnd + 1;
                        continue;
                    }
                }
                if (linkDepth == 0
                    && TryMatchCandidate(richText, index, candidates, linkedIds, excludedId, out var entry, out var length))
                {
                    result.Append("<link=\"").Append(entry.Id).Append("\"><color=#7EDBFF>")
                        .Append(richText, index, length).Append("</color></link>");
                    linkedIds.Add(entry.Id);
                    index += length;
                    continue;
                }
                result.Append(richText[index++]);
            }
            return result.ToString();
        }

        private static int EntryPriority(Entry entry)
        {
            // Both the generic debuff and the weakening status are displayed
            // as "Osłabienie". Prefer the concrete runtime status when both
            // entries expose the same visible term.
            return string.Equals(entry?.Id, "weakening", StringComparison.OrdinalIgnoreCase) ? 0
                : string.Equals(entry?.Id, "debuff", StringComparison.OrdinalIgnoreCase) ? 1
                : 2;
        }

        private static void AddCandidate(
            List<(Entry Entry, string Term, int Priority, int Order)> candidates,
            Entry entry,
            string term,
            int priority,
            ref int order)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            foreach (var candidate in candidates)
                if (candidate.Entry == entry && string.Equals(candidate.Term, term, StringComparison.OrdinalIgnoreCase)) return;
            candidates.Add((entry, term, priority, order++));
        }

        private static bool TryMatchCandidate(
            string text,
            int start,
            List<(Entry Entry, string Term, int Priority, int Order)> candidates,
            HashSet<string> linkedIds,
            string excludedId,
            out Entry entry,
            out int length)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Entry == null
                    || string.Equals(candidate.Entry.Id, excludedId, StringComparison.OrdinalIgnoreCase)
                    || linkedIds.Contains(candidate.Entry.Id)) continue;
                length = candidate.Term.Length;
                if (start + length <= text.Length
                    && string.Compare(text, start, candidate.Term, 0, length, StringComparison.OrdinalIgnoreCase) == 0
                    && HasTermBoundaries(text, start, length))
                {
                    entry = candidate.Entry;
                    return true;
                }
            }
            entry = null;
            length = 0;
            return false;
        }

        private static bool HasTermBoundaries(string text, int start, int length)
        {
            var beforeIsWord = start > 0 && char.IsLetterOrDigit(text[start - 1]);
            var after = start + length;
            var afterIsWord = after < text.Length && char.IsLetterOrDigit(text[after]);
            if (beforeIsWord || afterIsWord) return false;

            // Internal ids such as "skill.mage.fire_arrow" and serialized paths
            // are technical text, not player-facing glossary prose. Punctuation
            // around a normal sentence is still linkable, while identifier-like
            // separators keep short aliases such as AP from linking accidentally.
            return !IsIdentifierSeparator(text, start - 1)
                && !IsIdentifierSeparator(text, after);
        }

        private static bool IsIdentifierSeparator(string text, int index)
        {
            if (index <= 0 || index >= text.Length - 1) return false;
            var character = text[index];
            if (character is not ('.' or '_' or '/' or '\\')) return false;
            return char.IsLetterOrDigit(text[index - 1])
                && char.IsLetterOrDigit(text[index + 1]);
        }

        private static bool TryReadLinkId(string tag, out string id)
        {
            id = null;
            var equals = tag.IndexOf('=');
            if (equals < 0) return false;
            var start = equals + 1;
            while (start < tag.Length && char.IsWhiteSpace(tag[start])) start++;
            if (start >= tag.Length) return false;
            var quote = tag[start];
            if (quote != '\"' && quote != '\'') return false;
            var end = tag.IndexOf(quote, start + 1);
            if (end <= start + 1) return false;
            id = tag.Substring(start + 1, end - start - 1);
            return !string.IsNullOrWhiteSpace(id);
        }
    }

    /// <summary>
    /// Code-backed glossary content used when a scene has no serialized glossary
    /// reference, and as a safe expansion for authored assets that predate a term.
    /// </summary>
    internal static class CombatTooltipGlossaryFallback
    {
        private static readonly IReadOnlyList<CombatTooltipGlossary.Entry> entries = BuildEntries();
        private static readonly Dictionary<string, CombatTooltipGlossary.Entry> byId = BuildLookup();

        public static IReadOnlyList<CombatTooltipGlossary.Entry> Entries => entries;

        public static bool TryGet(string id, out CombatTooltipGlossary.Entry result)
        {
            if (!string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id, out result)) return true;
            result = null;
            return false;
        }

        private static Dictionary<string, CombatTooltipGlossary.Entry> BuildLookup()
        {
            var result = new Dictionary<string, CombatTooltipGlossary.Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                result[entry.Id] = entry;
            }
            AddAlias(result, "hp", "health");
            AddAlias(result, "hitpoints", "health");
            AddAlias(result, "action_points", "ap");
            AddAlias(result, "stamina", "momentum");
            return result;
        }

        private static void AddAlias(
            Dictionary<string, CombatTooltipGlossary.Entry> lookup,
            string alias,
            string id)
        {
            if (lookup.TryGetValue(id, out var entry)) lookup[alias] = entry;
        }

        private static IReadOnlyList<CombatTooltipGlossary.Entry> BuildEntries()
        {
            return new List<CombatTooltipGlossary.Entry>
            {
                Status("weakening", "Osłabienie", "Zmniejsza zadawane obrażenia."),
                Status("bleed", "Krwawienie", "Zadaje obrażenia na początku każdej tury."),
                Status("poison", "Zatrucie", "Zadaje obrażenia na początku każdej tury."),
                Status("burn", "Płonięcie", "Zadaje obrażenia na początku każdej tury."),
                Status("stun", "Ogłuszenie", "Nie może wykonywać akcji ani się poruszać."),
                Status("freeze", "Zamrożenie", "Nie może wykonywać akcji ani się poruszać. Pierwszy bezpośredni cios przerywa zamrożenie i otrzymuje bazowo +50% obrażeń za każdą pozostałą turę zamrożenia."),
                Status("sleep", "Sen", "Nie może wykonywać akcji. Ruch budzi postać i kończy ten efekt."),
                Status("paralysis", "Paraliż", "Nie może wykonywać akcji ani się poruszać. Nie może być celem umiejętności i nie otrzymuje obrażeń."),
                Status("immobilize", "Unieruchomienie", "Nie może się poruszać ani używać umiejętności przemieszczających."),
                Status("chill", "Oziębienie", "Ogranicza szybkość do 20, jeśli była wyższa. Wpływa przez to zarówno na częstotliwość tur, jak i pulę AP."),
                Status("slow", "Spowolnienie", "Zmniejsza szybkość o 20%.", "spowolnienie", "spowolnienia", "spowolnieniem"),
                Status("taunt", "Prowokacja", "Umiejętności ofensywne mogą celować wyłącznie w postać, która nałożyła Prowokację."),
                Status("invisibility", "Niewidzialność", "Ukryta przed wrogami. Nie może być bezpośrednio wybrana ani objęta automatycznym obszarowym celowaniem. Efekty kierowane w teren nadal mogą ją trafić."),
                Status("regeneration", "Regeneracja zdrowia", "Efekt przywracający zdrowie na końcu własnej tury. To inna mechanika niż regeneracja many lub generowanie furii i impetu.", "regeneracja", "regeneracji zdrowia"),
                Status("counterattack", "Kontratak", "Wykonuje kontratak po ataku przeciwnika."),

                ClassState("paladin.holy_regeneration", "Święta Regeneracja", "Okresowo przywraca zdrowie przez czas trwania efektu."),
                ClassState("paladin.blessing", "Błogosławieństwo", "Zwiększa zadawane i ogranicza otrzymywane obrażenia."),
                ClassState("warrior.order", "Rozkaz Unicestwienia", "Zwiększa podatność oznaczonego celu na obrażenia."),
                ClassState("warrior.protection", "Rozkaz Ochrony", "Ogranicza otrzymywane obrażenia."),
                ClassState("warrior.trauma", "Trauma", "Zwiększa obrażenia okresowe otrzymywane przez cel."),
                ClassState("warrior.strategy", "Strategia", "Umożliwia wykonanie darmowych kroków."),
                ClassState("scout.anger", "Złość", "Złość zwiadowcy narasta przeciwko oznaczonemu celowi."),
                ClassState("scout.weak_point", "Słaby Punkt", "Zwiększa podatność celu na trafienia krytyczne."),
                ClassState("scout.embedded_knives", "Wbite sztylety", "Sztylety wbite w cel; liczba stosów oznacza ich liczbę."),
                ClassState("scout.analysis", "Dogłębna Analiza", "Zwiększa penetrację dzięki analizie przeciwnika."),

                Term("health", "Zdrowie", "Obrażenia najpierw pochłania tarcza. Przy 0 HP bohater zostaje powalony; powalenie i śmierć to różne stany.", "zdrowie", "zdrowia", "zdrowiu", "zdrowiem", "HP"),
                Term("ap", "Punkty Akcji", "Budżet własnej tury, odnawiany na jej początku. Maksymalna pula zależy od szybkości. Koszt skilla podany w % AP odnosi się do maksymalnej puli, nie do pozostałych AP. Ruch także zużywa AP. Niewydane AP wpływają na naturalną regenerację many bohaterów.", "Punkty Akcji", "Punktów Akcji", "punktów akcji", "AP"),
                Term("energy", "Energia", "Niezależny od Punktów Akcji zasób używany przez niektóre umiejętności.", "energia", "energii", "energię", "energią"),
                Term("mana", "Mana", "Zasób maga, paladyna i części przeciwników. Bohaterowie odzyskują naturalną manę na końcu własnej tury: 100%/50%/0% AP daje odpowiednio 200%/100%/25% regeneracji. Pomiędzy tymi progami odzysk rośnie liniowo. NPC i przywołania sterowane przez AI odzyskują zwykłą regenerację na początku tury. Poza walką mana nie odnawia się automatycznie. Mikstury i odpoczynek w obozie nadal ją przywracają. Bezpośredni odzysk ze skilli i odrębne efekty EQ nie korzystają z mnożnika AP.", "mana", "many", "manę", "maną"),
                Term("rage", "Furia", "Bazowo +10 za użycie ofensywnej umiejętności zadającej obrażenia bezpośrednie lub okresowe, także przy pudle, raz za użycie niezależnie od liczby celów i trafień; kolejne ticki nie generują furii. Wyładowanie nie generuje bazowej furii. Pierwsza pełna własna tura bez ofensywy nie powoduje straty; na końcu drugiej i każdej kolejnej traci 10. Każda akcja ofensywna resetuje licznik, również samo odepchnięcie, ogłuszenie lub osłabienie bez obrażeń. Tura odebrana przez ogłuszenie, zamrożenie, paraliż lub sen nie zwiększa licznika. Dotyczy to też unieruchomienia, jeśli nie było dostępnego ataku na przeciwnika. Poza walką traci 10 co 6 sekund. Brak zwykłej pasywnej regeneracji jak przy manie.", "furia", "furii", "furię", "furią"),
                Term("momentum", "Impet", "Ruch daje kolejno +2, +4, +6 impetu i o 2 więcej za każdą następną kratkę, niezależnie od kosztu AP. W walce seria zaczyna się od nowa w każdej turze; tura bez ruchu odbiera 10 impetu. Poza walką można zgromadzić maksymalnie 50 impetu. Każde 6 sekund bez ruchu odbiera 10 impetu i resetuje serię. Ponad 100 impetu generacja z ruchu działa z połową wydajności. Każde pełne 25 impetu ponad 100 daje +4 szybkości. Udany odzysk sztyletów daje dodatkowo +8, raz za odzysk niezależnie od liczby sztyletów.", "impet", "impetu", "impetem", "stamina", "staminy"),
                Term("action", "Akcja", "Działanie wykonywane przez jednostkę podczas tury, na przykład ruch albo użycie umiejętności.", "akcja", "akcji", "akcje", "akcję", "akcją"),
                Term("movement", "Ruch", "Zmiana pola zajmowanego przez jednostkę. Ruch jest akcją.", "ruch", "ruchu", "ruchem"),
                Term("turn", "Tura", "Kolej jednostki na działanie. Efekty statusu mogą uruchamiać się na początku lub końcu tury.", "tura", "tury", "turze", "turę", "tur"),
                Term("duration", "Czas trwania", "Liczony według własnych tur jednostki mającej efekt, nie wspólnej rundy wszystkich postaci. Obrażenia okresowe zwykle działają na początku, a leczenie okresowe na końcu tury. Podczas powalenia czas efektów nadal upływa bez wykonywania ich ticków.", "czas trwania", "czasu trwania"),
                Term("stacks", "Stosy", "Liczba nałożeń lub ładunków efektu. Nie każdy status sumuje się tak samo: zależnie od efektu kolejne nałożenie odświeża czas, łączy obrażenia albo tworzy niezależny efekt. Zgrupowana ikona DoT może przedstawiać kilka efektów o różnych pozostałych czasach.", "stosy", "stosów", "stos"),
                Term("buff", "Wzmocnienie", "Korzystny efekt statusu działający na jednostkę.", "wzmocnienie", "wzmocnieniem", "wzmocnienia"),
                Term("debuff", "Osłabienie", "Szkodliwy efekt statusu działający na jednostkę.", "osłabienie", "osłabieniem", "osłabienia"),
                Term("status-effect", "Efekt statusu", "Tymczasowy stan jednostki. Może być korzystnym wzmocnieniem albo szkodliwym osłabieniem.", "efekt statusu", "efektu statusu", "efekty statusu", "efektów statusu"),
                Term("target", "Cel", "Jednostka lub obiekt wybrany przez akcję.", "cel", "celu", "celem"),
                Term("unit", "Jednostka", "Każda postać albo przeciwnik na polu walki.", "jednostka", "jednostki", "jednostce", "jednostkę", "jednostką", "jednostek"),
                Term("damage.physical", "Obrażenia fizyczne", "Zwykle wymagają testu trafienia z celności broni, celności skilla i uniku celu. Po trafieniu obrażenia ogranicza obrona fizyczna. Efekt może ignorować obronę fizyczną.", "obrażenia fizyczne", "obrażeń fizycznych", "obrażeniami fizycznymi"),
                Term("damage.magical", "Obrażenia magiczne", "Nie korzystają ze zwykłego testu celności fizycznej i uniku. Podlegają obronie magicznej, chyba że efekt ją ignoruje. Szansa na dołączony status jest rozpatrywana osobno.", "obrażenia magiczne", "obrażeń magicznych", "obrażeniami magicznymi"),
                Term("damage", "Obrażenia", "Zmniejszają zdrowie celu. Tarcza pochłania obrażenia, jeśli cel ją ma.", "obrażenia", "obrażeń", "obrażeniami"),
                Term("speed", "Szybkość", "Wpływa na częstotliwość własnych tur i maksymalną pulę AP. Większa szybkość oznacza częstsze działanie, nie tylko wcześniejsze miejsce w kolejce. Bazowy odstęp tur to AV / szybkość; maksymalne AP wynoszą bazowo 0,25 × szybkość, przed dodatkowymi modyfikatorami.", "szybkość", "szybkości", "szybkością"),
                Term("armor", "Pancerz", "Pancerz opisuje ochronę wynikającą z obrony fizycznej lub magicznej.", "pancerz", "pancerza", "pancerzem"),
                Term("defense.physical", "Obrona fizyczna", "Zmniejsza obrażenia fizyczne według zależności: obrażenia² / (obrażenia + skuteczna obrona). 100 obrony nie oznacza 100% redukcji. Większe pojedyncze uderzenia przebijają tę samą obronę skuteczniej.", "obrona fizyczna", "obrony fizycznej", "obronę fizyczną"),
                Term("defense.magical", "Obrona magiczna", "Zmniejsza obrażenia magiczne według zależności: obrażenia² / (obrażenia + skuteczna obrona). Jest niezależna od obrony fizycznej. Nie jest odpornością na statusy ani DoT.", "obrona magiczna", "obrony magicznej", "obronę magiczną"),
                Term("resistance", "Odporność", "Rodzaje odporności mają różne zastosowania. Odporność na kontrolę wpływa na szansę statusów kontroli. Odporności na krwawienie, zatrucie i ogień wpływają również na obrażenia odpowiednich DoT. Obrona fizyczna i magiczna to odrębne statystyki. Zależności nie są prostym odjęciem procentów od szansy skilla.", "odporność", "odporności", "odporność na"),
                Term("control", "Kontrola", "Ogłuszenie, zamrożenie, sen, paraliż, unieruchomienie, oziębienie i prowokacja. Odporność na kontrolę zmienia szansę ich nałożenia. Statusy kontroli korzystają ze wspólnej tolerancji, więc zmiana ogłuszenia na inny rodzaj kontroli nie omija jej.", "kontrola", "kontrolę", "kontroli"),
                Term("penetration", "Penetracja", "Ogranicza skuteczną obronę: obrona / (1 + penetracja). Przykład: +100% penetracji zmniejsza obronę do połowy, nie ignoruje jej całkowicie. Efekt ignorujący obronę jest odrębną mechaniką.", "penetracja", "penetracji", "przebicie", "przebicia"),
                Term("critical", "Trafienie krytyczne", "Trafienie krytyczne zadaje zwiększone obrażenia zgodnie ze statystyką Obrażenia krytyczne.", "trafienie krytyczne", "trafienia krytyczne", "krytycznie", "krytyczny"),
                Term("crit-chance", "Szansa krytyczna", "Szansa krytyczna określa możliwość wykonania trafienia krytycznego.", "szansa krytyczna", "szansy krytycznej", "szansę krytyczną"),
                Term("crit-damage", "Obrażenia krytyczne", "Mnożnik obrażeń po trafieniu krytycznym. Wartość ×1,5 oznacza 150% zwykłych obrażeń, czyli premię +50%, a nie +150%.", "obrażenia krytyczne", "obrażeń krytycznych"),
                Term("dodge", "Unik", "Zmniejsza szansę zwykłego trafienia fizycznego w zależności od celności napastnika. 20 uniku nie oznacza stałej 20% szansy uniknięcia każdego ataku. Zwykły unik nie chroni przed obrażeniami magicznymi.", "unik", "uniku", "uniki", "uników"),
                Term("accuracy", "Celność", "Dla zwykłego trafienia fizycznego najpierw uśrednia się celność broni i skilla. Szansa trafienia wynosi celność² / (celność + unik), w procentach, ograniczona do 0–100%. Premie do trafienia mogą ją następnie zmienić. Nie jest to prosty wynik celność minus unik.", "celność", "celności"),
                Term("range", "Zasięg", "Zasięg określa odległość, w której akcja może wybrać cel lub pole.", "zasięg", "zasięgu", "zasięgiem"),
                Term("line-of-sight", "Linia wzroku", "Linia wzroku określa, czy między rzucającym a celem nie ma przeszkody.", "linia wzroku", "linii wzroku"),
                Term("area", "Obszar", "Obszar obejmuje wiele pól lub jednostek zgodnie z działaniem umiejętności.", "obszar", "obszaru", "obszarze"),
                Term("healing", "Leczenie", "Leczenie przywraca zdrowie jednostki.", "leczenie", "leczenia", "leczy"),
                Term("resource", "Zasób", "Zasób klasowy jest niezależny od Punktów Akcji i może być zużywany przez umiejętności.", "zasób", "zasobu", "zasobem", "zasoby"),
                Term("resource-regeneration", "Regeneracja zasobu", "Statystyka określająca naturalny odzysk, przede wszystkim many. Bohater w walce otrzymuje ją z mnożnikiem pozostałych AP na końcu tury. Nie oznacza automatycznej regeneracji furii ani impetu.", "regeneracja many", "regeneracji many", "regeneracji zasobu"),
                Term("resource-recovery", "Odzysk zasobu", "Bezpośrednie przywrócenie zasobu przez umiejętność lub efekt EQ. To nie naturalna regeneracja na końcu tury; mnożnik pozostałych AP nie powiększa go automatycznie. Blokada odzysku i limit puli nadal obowiązują.", "odzysk many", "odzyskiwanie zasobu"),
                Term("downed", "Powalenie", "Bohater z 0 HP nie wykonuje akcji. Bazowo ma 3 własne tury do śmierci; można go podnieść odpowiednią akcją. Bazowe podniesienie przywraca 1 HP, więc nie gwarantuje bezpieczeństwa. Statusy czasowe wygasają podczas powalenia, ale nie zadają kolejnych ticków DoT.", "powalony", "powalonego", "powalona"),
                Term("death", "Śmierć", "Stan odrębny od powalenia. Zwykłe podniesienie powalonej postaci nie wskrzesza martwej. Możliwość przywrócenia zależy od odpowiedniej mechaniki wskrzeszenia.", "martwy", "martwej"),
                Term("rear-attack", "Atak od tyłu", "Napastnik musi znajdować się w tylnej połowie względem kierunku, w którym patrzy cel: kąt większy niż 90° od jego przodu. Pozycja dokładnie z boku nie jest tyłem. Samo zajście od tyłu nie daje każdemu atakowi premii — musi ją zapewniać skill lub talent.", "cios w plecy", "za plecami", "od tyłu"),
                Term("status-tolerance", "Tolerancja statusu", "Udane nałożenie statusu buduje czasową tolerancję. Ogłuszenie, zamrożenie, sen, paraliż, unieruchomienie, oziębienie i prowokacja współdzielą jedną pulę: zmiana rodzaju kontroli nie omija ochrony. Bazowo każdy stos dodaje 25 punktów procentowych tolerancji, do 100%. Dla kontroli pierwszy efekt daje dodatkowe 2 tury ochrony, a kolejne wydłużają ją o swój czas. Pozostałe statusy mają osobne pule.", "tolerancja", "tolerancji"),
                Term("dot", "Obrażenia okresowe", "Krwawienie, zatrucie i płonięcie zadają obrażenia w kolejnych tickach, zwykle na początku własnej tury celu. Pokazana łączna pula obrażeń efektu nie oznacza takiej wartości przy każdym ticku. Rodzaj odporności celu wpływa na skuteczność DoT.", "DoT", "obrażeń okresowych", "obrażenia w czasie"),
                Term("shield", "Tarcza", "Pochłania obrażenia przed zdrowiem. Może składać się z warstw o różnych źródłach i czasach trwania; ponowne użycie tego samego skilla może zastąpić jego warstwę zamiast dodawać kolejną.", "tarczy", "tarczę"),
                Term("status.forest.demonic_plague", "Demoniczna Zaraza", "W fazie II obrażenia enta nakładają zarazę, maksymalnie 5 stosów. Na początku własnej tury celu każdy stos zadaje 0,5% jego maksymalnego HP jako obrażenia bezpośrednie i odnawia entowi 0,5 many; ułamki many są zachowywane. Znika po zakończeniu działania źródła."),
                Term("forest.demonic_plague", "Demoniczna Zaraza", "W fazie II ent nakłada stosy Demonicznej Zarazy swoimi obrażeniami. Do 5 stosów; każdy zadaje 0,5% maksymalnego HP celu na początku jego tury i odnawia entowi 0,5 many."),
                Term("forest.rotten_aura", "Zgniła Aura", "Na początku własnej tury ent próbuje zatruć przeciwników do 3 pól od siebie na tym samym poziomie. Bazowa szansa wynosi 50%; odporność celu może ją zmienić."),
                Term("catacombs.dead_king.despair", "Aura Rozpaczy", "W fazie I Król i jego sojusznicy otrzymują o 25% mniej obrażeń od ataków dystansowych."),
                Term("catacombs.spectral_knight.shell", "Widmowa powłoka", "Każde bezpośrednie trafienie odbiera 1 HP. Trzecie trafienie niszczy Rycerza. Krwawienie, trucizna i podpalenie nie zadają mu obrażeń."),
                Term("catacombs.dead_king.rage", "Widmowy Szał", "W fazie II Król zadaje o 30% więcej obrażeń i otrzymuje o 50% więcej obrażeń."),
                Term("catacombs.skeleton_slayer.armor", "Kościana Zbroja", "Ogranicza otrzymywane obrażenia fizyczne o 35%."),
                Term("catacombs.skeleton_slayer.revenge", "Zemsta Zabójcy", "Każde otrzymane obrażenia zwiększają regenerację AP o 3%, do 160% podstawowego limitu."),
                Term("catacombs.skeleton_slayer.defense", "Nieugięta Obrona", "Ogranicza otrzymywane obrażenia o 50% i gromadzi obrażenia przed obroną na następny atak. Brak obrażeń powoduje ogłuszenie na 2 tury."),
                Term("catacombs.skeleton_slayer.hammer", "Młot Zniszczenia", "Po przygotowaniu wykonuje 5 trafień we wrogów na Polu Impetu, ignorując obronę. Otrzymanie 10% maksymalnego HP obrażeń przerywa przygotowanie."),
                Term("catacombs.skeleton_slayer.retaliation", "Odwet", "Następny atak zadaje dodatkowe obrażenia zgromadzone przez Nieugiętą Obronę."),
                Term("forest.bandit_leader.hidden_hand", "Niewidzialna Ręka", "Stała niewidzialność w fazie I. Pokonanie obstawy przygotowuje fazę II na własną turę; pułapki dowódcy wtedy detonują."),
                Term("forest.bandit_leader.commander", "Nieugięty Dowódca", "Na początku własnej tury, jeśli od poprzedniej stracił mniej niż 20% maksymalnego HP, regeneruje 10% maksymalnego HP i uzyskuje niewidzialność."),
                Term("control-resistance", "Odporność na kontrolę", "Zmienia szansę nałożenia statusów kontroli. Dodatkowo działa wspólna tolerancja narastająca po udanych nałożeniach. Wartość nie oznacza prostego odjęcia tylu punktów od szansy skilla.", "odporności na kontrolę", "odporność na kontrolę"),
                Term("dot-resistance", "Odporność na DoT", "Odporności na krwawienie, zatrucie i ogień zmieniają obrażenia właściwych efektów okresowych. Bazowa krzywa daje ×2 obrażeń przy 0% odporności, ×1 przy 50% i ×0,5 przy 100%. Osobne premie zmniejszające otrzymywane obrażenia DoT działają dodatkowo.", "odporności na DoT", "odporność na obrażenia okresowe"),
                Term("fire-resistance", "Odporność na ogień", "Wpływa na szansę płonięcia i obrażenia okresowe płonięcia. Bezpośrednia magiczna część Kuli Ognia podlega obronie magicznej; nie należy utożsamiać jej z DoT płonięcia.", "odporności na ogień"),
                Term("mana-overflow", "Nadmiar many", "Odpowiedni talent pozwala odzyskiwać manę ponad zwykłe maksimum. Ten dodatkowy zapas nie zwiększa podstawy kosztów wyrażonych w % maksymalnej many. Związane z nim premie i zużycie nadmiaru zależą od talentu.", "nadmiaru many"),
                Term("rage-shortage", "Niedobór furii", "Skill droższy od maksymalnej puli furii można uruchomić dopiero z pełną pulą. Brakująca część kosztu powoduje czasową karę do obrażeń; nie daje darmowego użycia przy dowolnie niskiej furii.", "niedobór wściekłości", "niedoboru furii"),
                Term("upkeep", "Utrzymanie", "Odrębny koszt aktywnej umiejętności przełączanej. Może być pobierany cyklicznie, gdy efekt pozostaje włączony. Nie jest kosztem samego włączenia; szczegóły i wartość zależą od skilla.", "utrzymanie", "utrzymania")
            };
        }

        private static CombatTooltipGlossary.Entry Status(string id, string title, string description, params string[] aliases) =>
            Term(id, title, description, aliases);

        private static CombatTooltipGlossary.Entry ClassState(string id, string title, string description) =>
            Term(id, title, description);

        private static CombatTooltipGlossary.Entry Term(string id, string title, string description, params string[] aliases)
        {
            return new CombatTooltipGlossary.Entry
            {
                Id = id,
                Title = UiTerms.GlossaryTitle(id, title),
                Description = UiTerms.Present(description),
                Aliases = aliases == null ? new List<string> { title } : new List<string>(aliases) { title }
            };
        }
    }
}
