# CP2-03 — audyt gotowości systemów

Status: **READY_FOR_SMOKE — przegląd dokumentu**, 2026-10-31. Bez implementacji gameplayu.

Baza: `889456c9c`, branch `codex/v0.2.0-content-pass-2`. Karta 02 zaakceptowana, zacommitowana i wypchnięta. Źródło wymagań: `../design/content_pass_2.xlsx`, SHA256 `83cb291dd139a4e34497d2d752c8289a9545442d3c714537502c6909566618b7`, migawka i katalog CP2-02. Decyzje źródłowe z katalogu nadal obowiązują. To ocena kontraktów kodu; nie jest wynikiem testu nowych assetów ani potwierdzeniem balansu L40.

## Wynik i zasady oceny

Istnieją fundamenty: efekty aktywne, pasywki, geometria AoE, przemieszczenia, przywołania, obiekty obszarowe, statusy, skalowanie i zapis lochu. Nowy content wymaga jednak rozszerzeń systemowych, szczególnie wielopolowości, typowanego terenu, cyklu śmierci i zdarzeń obrażeń. Samo utworzenie assetów nie wystarczy.

Oznaczenia w macierzy: **D** — istniejący kontrakt pozwala przygotować dane, następnie potrzebny test integracyjny; **R** — istniejący system wymaga rozszerzenia danych i kodu; **K** — brak odpowiedniego kontraktu ogólnego, potrzebna implementacja; **P** — decyzja projektowa przed implementacją danego wymagania; **O** — odłożone poza obecny zakres. D nie oznacza, że nowy content już działa w grze. R nie oznacza konieczności przebudowy całego systemu.

Właściciel oznacza kartę i system odpowiedzialne za jeden wspólny kontrakt. Późniejsze karty są konsumentami; nie implementują własnej odmiany tej samej mechaniki. Podkarty poniżej rozbijają istniejące karty 01–56, nie tworzą nowej numeracji głównych kart ani wpisów w zewnętrznym Trello.

## Dowody w aktualnym kodzie

Ścieżki poniżej są względem `Assets/_Project/Scripts/`.

| Kod dowodu | Pliki / punkt integracji | Ustalony kontrakt i ograniczenie |
|---|---|---|
| E1 | `Core/Enums/EffectType.cs`, `Data/Effects/SkillEffectData.cs`, `Systems/Combat/SkillExecutionSystem.cs` | Aktywne obrażenia, statusy, leczenie, tarcze, koszty, AP/AV, push/pull/charge, teleport, summon i obiekt mają wykonanie. Są też efekty specjalne wojownika, maga i paladyna; ich obecność nie dowodzi obsługi dowolnej nowej mechaniki. |
| E2 | `Data/Skills/SkillRangeData.cs`, `Core/Enums/RangeShape.cs`, `Systems/Combat/CombatTargetingService.cs` | Istnieją square, line, cone, cross/X i inne kształty oraz tryby celowania. Geometria efektu i wybór celów muszą pozostać zgodne z preview, AI i LoS. |
| E3 | `Systems/Combat/PassiveEffectOperationCapabilities.cs`, `PassiveEffectCombatExecutionSystem.cs` i `.Reactions.cs` | Walidator realnych możliwości jest węższy od enumów. Ogólnie rozwiązywane cele: Self, Source, PrimaryTarget, Attacker, AllTargets, LowestHealthAlly. Enum SelectedAlly/AllAllies/ProtectedAlly/Nearest/Area nie daje sam obsługi. Statyczne modyfikatory tylko Self/Constant. TargetCount/RuntimeState nie są ogólnie wspieranymi źródłami wartości. |
| E4 | `Runtime/Combat/PassiveEffectCombatContext.cs`, `Core/Enums/PassiveEffectTriggerType.cs` | Są OnMove, OnKill, OnHeal, OnDamageTaken, tury i zdarzenia statusów. Kontekst ma HealthDamage/EventValue, lecz nie osobny kompletny raport raw/mitigated/shield/prevented/overheal. Brak ogólnych triggerów OnDeath, OnResurrect, OnShieldBroken i OnCombatStart. |
| E5 | `Runtime/Combat/BattleGrid.cs`, `Systems/Combat/CombatMovementSystem.cs`, `DungeonBattleGridFactory.cs` | Trudny teren to zbiór pól z dopłatą +1; nie model wody/błota. Kopia planistyczna kopiuje teren i rewizję. Ruch, zajętość i blokowanie opierają się na jednej Position jednostki. |
| E6 | `Runtime/Combat/BattleUnit.cs`, `Systems/Combat/CombatTeleportSystem.cs` | Brak ogólnej wielopolowości i zestawu nowych traitów. Teleport przyjmuje caster, target, destination, sprawdza życie, immobilize rzucającego, LoS i zajętość jednego pola; wywołuje cell-entry i refresh aur. Nie trzeba pisać drugiego systemu teleportów sojusznika. |
| E7 | `Systems/Combat/SummonSystem.cs`, `Data/Effects/SkillEffectData.cs` | Przywołuje CharacterDefinition na poziomie rzucającego; obsługuje pulę definicji, procent HP, część statów rzucającego, limit, czas, grupę i stationary/persistent. Nie kopiuje wybranego dowolnego sojusznika. Persistent oznacza tu czas życia przywołania, nie zapis do save. |
| E8 | `Systems/Combat/SummonedObjectSystem.cs`, `SummonedObjectEffectExecutor.cs`, `Data/Combat/SummonedObjectDefinition.cs`, `Core/Enums/SummonedObjectEffectType.cs` | Istnieje baza obszarów i efektów obrażeń/statusu/leczenia/zasobów/tarczy/statystyk. Nie utożsamiać obiektu z aktorem posiadającym HP, turę i skillset. |
| E9 | `Systems/Combat/CombatDamageSystem.cs`, `CombatRecoverySystem.cs` | Heal zwraca faktycznie przywrócone HP i publikuje OnHeal; Dead odrzuca, Downed może podnieść. MarkDead czyści prowokację i usuwa wroga z walki. Redirect już wykonuje osobną obronę protektora i blokuje dalszy redirect. To nie ogólne wskrzeszenie ani zdarzenie overheal. |
| E10 | `Systems/Combat/DeadKingBossSystem.cs`, `ForestEntBossSystem.cs`, `Data/Characters/CharacterDefinition.cs` | Jest SecondPhaseDefinition; istniejące systemy mają własne stany, progi i opóźnione przejścia faz. Nie ma dowodu, że nowy boss zacznie zmieniać fazę od samej referencji. ForestEnt ma analog reakcji na DOT i terenu, ale to owner konkretnego bossa. |
| E11 | `Systems/Combat/WarriorCombatSystem*.cs`, `MageCombatSystem*.cs`, `ScoutCombatSystem*.cs`, `PaladinCombatSystem*.cs` | Klasy mają specjalizowane hooki, stan i modyfikatory istniejących skilli. Nowe drzewka i T5 muszą wejść w ten sam pipeline; enum operacji pasywnej nie zastępuje integracji tych hooków. |
| E12 | `Systems/Characters/CharacterProgressionSystem.cs`, `CharacterLevelGrowthSystem.cs`, `CharacterInstanceFactory.cs` | Limit i wzrost korzystają z konfiguracji, factory ogranicza poziom do MaximumLevel. Fallback limitu to 50, nie dowód kompletnej gry L40. Krzywe i odblokowania trzeba zweryfikować w CP2-04. |
| E13 | `Core/Serialization/DungeonSaveData.cs`, `Systems/SaveLoad/GameSessionSaveSnapshotFactory.cs` | Zapisywany jest aktywny loch, layout i delty; snapshot odrzuca sesję HasCombat. Nowe stany tylko-walki nie wymagają dodawania zapisu walki. Stos utrzymywany przez cały loch wymaga jawnej persystencji i resetu. |
| E14 | `Core/Enums/DungeonTheme.cs`, katalog CP2-02, `Systems/Items/ConsumableService.cs` | Sea ma istniejący odpowiednik Docks; nie dodawać nowej wartości przez przesunięcie enumów. ConsumableService ma ścieżki efektów i zdejmowania wskazanego statusu; oczyszczenie wszystkich efektów mikstur wymaga identyfikacji ich pochodzenia. |

## Macierz: przestrzeń, teren i bazowe wykonanie

| ID | Wymaganie / źródło arkusza | Ocena | Owner → konsumenci | Brak / sposób integracji | Test ryzyka |
|---|---|---|---|---|---|
| S01 | Zwykłe damage, penetracja, heal, status, koszt, buff, AV; Przeciwnicy X:Z, Bossowie AH:AJ | D, E1/E3 | karta autora skilla → 28/33/38 i bossowie | Konfigurować wspólne efekty, nie osobne executory per przeciwnik. Każdy procent przypisać do Atk/MaxHP/missingHP i jednostek AP. | Hit/miss, multi-hit, odporność, koszt i opis dają zgodny wynik. |
| S02 | Linie, stożki, kwadraty, krzyże; Bossowie AH13:AJ24 | D/R, E2/E5 | 25A → wszyscy autorzy | Kształty istnieją; zakres liczb z arkusza wymaga mapowania na konkretne maski i uwzględnienia footprintu. | Jedna jednostka 2×2 trafiona raz przez AoE, zgodność AI/preview/wykonania. |
| S03 | Woda i błoto; Cechy przeciwników, Alfa-Rekin, Ent | K, E5 | 23A → 32/34/37/39 | Typowany teren, dopłata ruchu, zakaz ataku/kary uniku, odporności traitów, rewizja planowania. Nie zastępować wodą starego bool difficultTerrain. | Bazowy koszt różny od 1, wejście/wyjście, odporna jednostka, preview AP i AI identyczne. |
| S04 | Zmiana pola na wodę, czasowe bagno i zatrute pola; AG18/AH17/AI22/AJ22 | R, E5/E8/E10 | 23B → 34/39 | Wspólna warstwa nakładek: źródło, czas, priorytet, usuwanie jednego źródła, odświeżanie aur. Oddzielić bazowy typ podłoża od obszaru trucizny. | Dwie nakładki wygasają w różnej kolejności; śmierć bossa nie kasuje obcego terenu. |
| S05 | Latanie, lewitacja, niematerialność; arkusz Cechy | K, E5/E6 | 24A/B → 28/33/38 oraz stare Wraith/DeadKing | Osobne reguły przejścia i zakończenia ruchu, pułapek, krawędzi, drzwi, jednostek; latanie także zasięg i koszt przełączenia. Nie jedna flaga IgnoreObstacles. | Przejście przez przeszkodę, legalny koniec, zamknięte drzwi, LoS, atak melee/ranged obu stron. |
| S06 | Morski, bagienny; Cechy | R, E5 | 23A → 24/34/38/39 | Odporności na konkretny teren, nie ogólna odporność na wszelkie kary. | Drowned odporny na wodę i błoto; Sharkman tylko wodę. |
| S07 | Potężny 2×2; stare bossy i nowe Cerber/Shark/Alligator/Ent/Plant | K, E5/E6 | 25A → 24/28/30/34/38/39/40 | Zestaw zajętych komórek + kotwica, odległość między footprintami, legalność całości, sweep ruchu/charge/push/teleport/summon. Wspólna implementacja także dla starych bossów. | Korytarz 1 pola, drzwi/schody, obrót przy ścianie, nielegalny teleport, uniknięcie podwójnego damage. |
| S08 | Ogromny 3×3 | O | 25B | Przypisany Wiwernie poza obecnymi pięcioma biomami. Model footprintu projektować rozszerzalny; pełnego contentu i testów 3×3 nie wymagać do L40. | Przy przyszłym włączeniu osobny pakiet geometrii. |
| S09 | Stacjonarny + teleportujące rośliny | R, E6/E7 | 26A → 38/40 | Istniejąca blokada IsStationarySummon to analog. Cecha zwykłego aktora blokuje chodzenie; teleport jest oddzielną akcją, a nie ruchem pieszym. Forced movement wymaga rozstrzygnięcia. | Roślina nie chodzi, wykonuje legalny teleport; AI nie planuje kroków. |
| S10 | Niezniszczalny | O | 26B | W arkuszu przypisany przyszłej Posągowej Straży, poza bieżącymi biomami. Nie blokuje aktualnych encounterów. Gdy wejdzie: targeting/damage i warunek zwycięstwa muszą być spójne. | Przyszły encounter kończy się po śmierci zwykłych wrogów, nie wisi na posągu. |
| S11 | Piasek/lawa; Pustynny/Piekielny | R/O | 23C → 24 | Tagi występują już na obecnych wrogach; samo przypisanie nie dowodzi potrzeby authoringu lawy/piasku w aktualnych pokojach. Obsłużyć dane traitów, aktywne podłoża dopiero z konsumentem. | Odporność nie usuwa ognia z innych źródeł. |
| S12 | Teleport siebie/sojusznika, swap, push/pull/charge | D/R, E1/E6 | 25A geometria; 10 autor sojusznika → 28/33/38/40 | API teleportu ma osobnego targeta. Rozszerzać wybór legalnego miejsca i UI/AI, utrzymać cell-entry/aura. Nie omijać geometrii przez bezpośrednie Position=. | Sojusznik/Downed, immobilize, occupied, pułapka po wejściu, footprint i preview. |
| S13 | Wybrane n<5 celów i koszt 10×n; Przeciwnicy Y6 | K | 28B → Imp | Jawna wielokrotna selekcja i atomowy koszt od liczby unikatowych celów; AI i UI ten sam plan. | 0/1/4 celów, anulowanie, brak many, ten sam cel nie liczony dwa razy. |
| S14 | Macka jako punkt początku ataku; X5:Z5 | R | 28C → Warlock | Oddzielić źródło statystyk/kosztu/kill credit od geometrycznego początku ataku. | Macka za ścianą, śmierć macki między preview a akcją, LoS i limit zasięgu. |

## Macierz: zdarzenia, summon i cykl życia

| ID | Wymaganie / źródło | Ocena | Owner → konsumenci | Brak / sposób integracji | Test ryzyka |
|---|---|---|---|---|---|
| L01 | HP damage, mitigated/prevented damage, shield, overheal, damage przy dodge; Drzewka 11/14/33/82/87/108 | R, E4/E9 | 06A → 05/09/10/11/12/38 | Jeden raport rozliczenia: przed obroną, po obronie, absorpcja tarczy, utrata HP, powód zera, heal requested/actual/overflow. Liczenie hipotetycznego damage nie wykonuje hit/crit/statusów ponownie. | Miss/dodge/blok/tarcza/redirect nie dublują proców; reflection bez pętli. |
| L02 | OnDeath, odrodzenie, heal po własnej śmierci, death of ally/summon | K, E4/E9 | 06B → 28/33/35/40 | Wspólny punkt przed finalizacją śmierci i zdarzenie po niej. Obsłużyć koszty/ICD, credit, turn queue, cleanup. Downed→Alive nie jest revive Dead. | DOT i multi-hit lethal, śmierć kilku aktorów, ostatni wróg odradza się bez przedwczesnego zwycięstwa. |
| L03 | Darmowe akcje i limity na turę/głowę, cooldown proców | R, E4/E11 | 30A → 11/35/40 | Osobny budżet reakcji/akcji od AP, stabilny numer tury właściciela, ochrona reentrancy. CP2-11 może wcześniej użyć kontraktu cooldownu; nie czekać na asset Cerbera. | Akcja 0 AP nie zapętla AI; dodatkowa tura nie resetuje wszystkich limitów. |
| L04 | Stałe przywołanie, pula, %HP, poziom; bossowie i drzewka | D/R, E7 | 28C → 30/35/40 | Użyć SummonSystem; doprecyzować bazę %HP, sterowanie AI zwykłego niestacjonarnego summona i dziedziczenie statów. | Limit/replace, brak miejsca, HP/level L1/L20/L40, summon nie daje wielokrotnego lootu. |
| L05 | 2–3 kopie wybranego celu z 1 HP i eksplozja wszystkich iluzji; Y8:Z8 | K | 28D | Snapshot dozwolonych statów/skilli bez kopiowania trwałej tożsamości, ekwipunku i inwestycji; grupa ownera, zużycie grupy, odrębny origin każdej eksplozji. | Klon klona, śmierć ownera, nakładanie AoE i brak pętli przy summon skillu klona. |
| L06 | Korzenie: 3 HP, własne ataki, skalowanie co turę, death feedback, konsumowanie; AG23:AJ24 | R, E7/E9 | 40A → 40B | Korzeń jest aktorem, nie tylko polem. Wspólna grupa ownera z rozróżnieniem death/consume/explode, statycznym HP i naliczaniem wieku. | Zniszczenie, eksplozja i zjedzenie nie wywołują tego samego feedbacku omyłkowo. |
| L07 | Obszary HOT, DOT, kontroli, aury wokół noży, premia korzeni wewnątrz | R, E8/E11 | 11B → 09/12/34/39/40 | Rozszerzyć istniejące obiekty, source token, wejście/wyjście/tick, odbiorcy i dynamiczny bonus. Żywy aktor-korzeń osobno. | Nakładanie, tura autora vs celu, przejście przez obszar, cleanup autora, brak wielokrotnego aura buff. |
| L08 | Status-specific remove / wszystkie DOT / weakening / efekty mikstur | D/R, E1/E14 | 20A filtry → 33/38 | Wspólny filtr po kategorii i pochodzeniu, nie lista przetłumaczonych nazw. Cleansing: usuwa wszystkie efekty mikstur i toksyczność do 0 (Nowe przedmioty I160). | Zachować buffy niemiksturowe; usunąć wszystkie właściwe źródła i odtworzyć staty. |
| L09 | Niecelowalna papuga przy sojuszniku; Z13 | K | 33B | Osobny stan targetability/attachment; niewidzialność nie wystarczy. Ustalić AoE, pozycję i odczepienie po śmierci nosiciela. | Nie wybiera jej AI/UI; obrażenia obszarowe według jednej reguły; koniec regen przy śmierci. |
| L10 | Harpun w celu i wymaganie kolejnego skilla; X11/Z11 | R, E1/E3 | 33A | Źródłowany znacznik celu, czas/zużycie i odpowiednia kondycja. | Dwa harpuny różnych jednostek; pull nie zużywa cudzego znacznika. |
| L11 | Jeden losowy status / losowa bomba wroga; Z16/Y21 | R | 33A losowanie wariantu → 38 | Wybór jednej gałęzi efektu/puli. Trzy niezależne szanse nie są równoważne. Bomba AI nie zużywa ekwipunku gracza. | Dokładnie jedna gałąź, deterministyczna możliwość testowania, brak nielegalnej definicji. |
| L12 | Leczenie za rzeczywiste self-damage/oddawanie obrażeń równych heal; X3/Y19 | R, E9 | 28A wynik efektu → 38 | Poprzedni efekt zwraca ilość, nie tylko succeeded. Transfer w wyniku jednej akcji, odrębne cele i ochrona pętli. | Overheal=0, self-kill, redukcja/tarcza na damage źródłowym, kilka roślin. |

## Macierz: drzewka i klasy

Zwykłe bonusy statystyk, odporności, kosztu i parametrów istniejących skilli pozostają u autora odpowiedniej karty (S01/E3/E11). Poniżej dodatkowe wymagania wszystkich opisanych grup; puste projekty są jawnie odłożone, nie uznane za zaimplementowane.

| ID | Zakres arkusza Drzewka | Ocena | Owner | Rozszerzenie i test odbioru |
|---|---|---|---|---|
| C01 | Wódz 2:6: rozkazy, execute, pojedynek, stos na jednym celu, zasięg | R | 05A | Rozszerzyć WarriorCombatSystem i parametry rozkazów; przełączenie celu resetuje tylko właściwy stos. Execute ocenia przewidywane zabójstwo raz, nie wykonuje podwójnego damage; dokładnie jeden sąsiad uwzględnia footprint. |
| C02 | Zabijaka 8:12: szał, low HP, crit/missingHP, koniec DeathWish, lethal resource | R | 05B + 06A/B | Rozszerzyć istniejący DeathWish; zachować bank prevented damage do wygaśnięcia i prawidłowy kill event. Test tarczy, śmierci i przerwania buffa, bez dodatkowej nagrody od każdego hitu po lethal. |
| C03 | Zbrojny 14:27: bank mitigated damage, blok, res/stun, cap hit, magic dodge, flat armor-level reduction | R/K | 06A/C | Nowe drzewko wpiąć w raport obrażeń, accuracy i status resolution; ustalić porządek flat/percent/armor i cap trafienia po nieudanym stun. Test jednego działania z kilkoma efektami i brakiem sukcesu statusu. |
| C04 | Zbrojny 28:39: dungeon kill stacks, adapt skillID, dystans, overheal, start shield, lowHP/revive | R/K | 06B/C/D | ICD i encounter state osobno od stanu lochu. Adaptation po stabilnym SkillId, nie nazwie. Test zapisu między walkami, opuszczenia lochu, odrodzenia i pełnego HP przy leczeniu. |
| C05 | Zbrojny 40:44, T6/T7 | P/O | 06, decyzja zakresu | Nie implementować tylko dlatego, że istnieją opisy. Po dopuszczeniu: wydłużenie buffa, anulowanie własnego sleep/channel, revive chance, redirect z osobną obroną. Redirect ma analog E9, pozostałe kontrakty ocenić z L01/L02. |
| C06 | Skrytobójca 46:50 | R | 07A | Koszt AP, guaranteed buffs i charge mają bazę; miss reroll musi być jednym rozliczeniem kontaktu, execute na ogłuszonym nie omija odporności/klasy bossa bez specyfikacji. Test proca i zużycia buffów na miss/dodge. |
| C07 | Tancerz 52:56 | R | 07B | Rozszerzyć ScoutCombatSystem: stack po pickup, bank następnego ataku, poison aura, conditional ranged/melee, limit i koszt noży. Test podniesienia kilku noży, utraty celu, śmierci i darmowego rzutu. |
| C08 | Strażnik 58:83 | P/R | 08 projekt → 09 | Rework jest bramką. Kandydaci: root OnMove, opcjonalny krok reakcyjny, magic-half-dodge, knife counter, push, wall stun, aura noży, reflection przed obroną także po dodge. Wspólne raporty L01 i geometria 25A; test zakazu pętli reakcji i akceptacji opcjonalnego ruchu. Nie wdrażać całej starej listy przed 08. |
| C09 | Czarnoksiężnik 85:89 | R/P | 10A | Adaptation ma bazę E11; bank hipotetycznego damage po dodge korzysta z L01. Teleport ally korzysta z E6, przyspieszenie AV z E1. D86: 1 rank, dwie wartości — rozstrzygnąć przed authoringiem. Test maksymalnego banku, kolejnego ataku i teleportu obcego celu. |
| C10 | Piromanta 91:95 | R | 10B | Parametry FireArrow/burn rozbudować w MageCombatSystem.Fire; minimalny incoming roll to hook w losowaniu damage, nie mnożnik redukcji. Nowy AoE ze wspólnego executora. Test zmiany tylko właściwych rzutów i burn po multi-hit. |
| C11 | Kriomanta 97:107 | R/P | 11A | Chill/slow/freeze, heal reduction, stun→freeze, bleed, conditional crit, zmniejszenie czasu freeze zamiast break; bez zastępowania bazowego TryBreakFreeze osobną kopią. D105 ma 2 ranki i 3 skale. Test Freeze+Stun tego samego efektu i kolejności konsumowania Freeze. |
| C12 | Kriomanta 108:122 | K/R/P | 11B/C | Cumulative damage per turn→shield, ICD, shield-break debuff, aura, współdzielenie freeze, execute z ograniczeniem dla bossa, obszar on-kill i missingHP scaling. L01/L03/L07. D110 nazywa Mroźną Osłonę, D111 Aurę Mrozu; zakres bonusu wymaga decyzji. Test ticków DOT, częściowo zużytej tarczy, przeniesienia tury i limitu execute bossa. |
| C13 | Kapłan 124:128 | R | 12A | Premia damage przed obroną, ładunki z heal z overflow, bonus następnego heal i wyczyszczenie po walce; pole HOT narasta z turą pobytu. L01/L07. Test actual heal=0, 4.ładunku, wielu celów i końca walki. |
| C14 | Paladyn: puste T5/3.drzewko/global | P/O | 13 → 14 | Specyfikacja niezamknięta; obecny PaladinCombatSystem nie stanowi potwierdzenia gotowości nieopisanych mechanik. Ponowić punktową ocenę po projekcie, nie blokować innych klas. |

## Macierz: sześciu bossów

Każdy boss korzysta z jednego wspólnego kontraktu przejścia faz (29A): warunek zgłoszenia, chwila wykonania, carry-over HP/MP/AP/statusów, dokładnie jedno przejście, brak przejścia po finalnej śmierci. Same warunki pozostają danymi lub polityką danego encountera. Nie przenosić bez decyzji reguły „dopiero następna tura”, używanej przez starych bossów.

| ID | Boss / komórki | Ocena | Owner | Braki i test ryzyka |
|---|---|---|---|---|
| B01 | Jarl AG13:AJ14 | R/K/P | 29A/B | Faza po allies=0 OR HP≤50%, redukcja za sojuszników, aura end-turn, self-lossHP, missingHP multiplier, shield bypass, heal po kill. L01/L02, S02. Test śmierci ostatniego sojusznika w AoE, lethal zamiast fazy, redukcji >100% i shield bypass bez usunięcia tarczy. |
| B02 | Cerber AG15:AJ16 | K/P | 30A/B | Trzy adresowalne głowy z HP i statusem, budżet jednej akcji/głowę zamiast AP, progi agregacji, wyłączenie bocznych głów, summon dwóch jednostek i duplicate-next-attack. Nie modelować jako trzy zwykłe niezależne bossy dające trzy nagrody. Test wyboru głowy na footprint2×2, AoE, zeroHP i dokładnie jednej nagrody. |
| B03 | Alfa-Rekin AG17:AJ18 | R/K/P | 34A/B | Typowana woda, kondycja częściowego footprintu na wodzie, cap3kroki na lądzie, ograniczenie skillsetu, zmiana losowych pól i damage od utworzenia wody, czasowa kara-off i invis. S03/S04/S07. Test brzegu, charge przez wodę/ląd i powrotu ograniczeń po wygaśnięciu. |
| B04 | Kapitan AG19:AJ20 | R/K/P | 35A/B | Summon z puli co4/2tury, ally-death stacks, AV100% jednego/wszystkich, delayed per-turn stun i odrębne buffy drużyn. L02/L03/L04/L07. Test śmierci przywołania a stosów, AV bez podwójnej tury i storm bez natychmiastowego potrójnego ticka. |
| B05 | Bagnisty Ent AG21:AJ22 | R/K/P | 39A/B | Aura, bonus po DOT i MP, timedmud, safe square i opóźniony efekt po2turach na oznaczonych polach. Analog ForestEnt/DeadKing, lecz nowy owner encountera na wspólnym terenie/zdarzeniach. Test braku rekursji DOT→bonus→DOT oraz zmiany pola przed detonacją. |
| B06 | Ludożerna Roślina AG23:AJ24 | R/K/P | 40A/B | Darmowy teleport i summon na starym miejscu, 3HP korzenie, wzrost/obszarowy bonus damage, consume/explode i Głód, execute zakorzenionego poniżej progu. L02/L03/L06, S07/S09/S12. Test nielegalnego miejsca po teleportacji, zużycia wszystkich korzeni i niepowielania Głodu. |

## Progresja, przedmioty i trwałość

| ID | Zakres | Ocena | Owner | Brak i weryfikacja |
|---|---|---|---|---|
| P01 | XP, punkty, poziomy treści, limity i odblokowania L40 | R/P, E12 | 04 → 16–22/31/36/41/53 | Oddzielić level postaci, materiału i zakres dropu. Sprawdzić config cap i clamp factory, krzywe wzrostu i oferty. Test L20→21, 30→31, 39→40, import starej postaci; istniejący fallback50 nie ustala release cap. |
| P02 | Resist update, nowe skille i statystyki bossów | D/R/P | 22, 28/29/30/33/34/35/38/39/40 | Użyć CharacterResistanceData/GrowthData, jawnie zmapować odporności statusów. Kolumny [50] są punktem danych, nie poleceniem wydania L50. Test tabeli L1/10/20/30/40 i jednostek %, bez interpolowania brakującego designu po cichu. |
| P03 | Receptury/części, biżuteria, alchemia, galduryt, unikatowe dropy | D/R/P | 16–21/42–46 | Katalog ma ID i źródła; authoring przez Editor API, walidacja referencji/ilości/wariantów i istniejących usług. Brakujące yield oraz efekty unikatów pozostają decyzjami, nie gotowością runtime. Test uzyskania i użycia rzeczywistego przedmiotu z przypisanej puli. |
| P04 | Respec płatny i zmiany drzewek | R | 15; integracja 54 | Transakcja waluta→refund→przeliczenie runtime; stabilne tree/node/effect IDs i usunięcie źródeł buffów starej inwestycji. Test niewystarczającej waluty, ponownego wczytania i wielokrotnego respec bez narastania statów. |
| P05 | Per-dungeon kill stacks Zbrojnego | K, E13 | 06D; migracja/test 54 | Stan przy postaci/aktywnym runie z DungeonId, nie wyłącznie BattleUnit. Domyślne0 w starym save; zachowanie między walkami, reset po wyjściu/zakończeniu/porzuceniu. Test save→load w tym samym lochu i nowego runu. |
| P06 | Teren walki, faza, korzenie, cooldown, bank next-attack | R, E13 | owner mechaniki; integracja 54 | Stan encounter-local, odpięcie subskrypcji i cleanup na każdym zakończeniu. HasCombat blokuje snapshot: audyt nie rozszerza zakresu o save w połowie walki. Jeżeli teren ma zostać w świecie po walce, wymaga jawnej delty lochu. | Test dwóch kolejnych walk, ucieczki/śmierci/wyjścia i ponownego wejścia bez wiszących źródeł. |
| P07 | Nowe assety i enumy, starsze save | R | autor assetu + 54 | Zachować GUID/ID, nie renumerować enumów; Morze mapować na Docks. Rozszerzenie serializacji przez domyślne wartości/migrację adekwatną do zmiany. | Stary save z aktywnym lochem i zapis po craftingu/odblokowaniu nowego contentu. |

## Podkarty implementacyjne i zależności

Proponowane rozbicie jest częścią wyniku audytu. Każda podkarta kończy się działającym kontraktem i testami z odpowiednich wierszy; nie wystarczy dodać enum/pole.

| Podkarta | Zakres / warunek zakończenia | Zależności | Save |
|---|---|---|---|
| 06A Raport rozliczenia obrażeń i leczenia | L01, bez zmiany istniejących wyników; stage/order/source i zabezpieczenie reentrancy | 04 przed wartościami balansu | Bez nowego save; encounter-local |
| 06B Cykl lethal/death/revive i zdarzenia sojusznika | L02, współpraca z Downed, kill credit, queue i victory | 06A | Bez zapisu walki |
| 06C Zbrojny: defense i reakcje | C03/C04 na wspólnych kontraktach | 06A/B | Inwestycje drzewa |
| 06D Stan utrzymywany przez loch | P05, reset i migracja | 06C, obecny save lochu | Tak, domyślne0 i test starego save |
| 11A Statusy mrozu; 11B obszary; 11C tarcza i cooldown | C11/C12, L07; wspólny kontrakt obszarów dostępny innym autorom | 06A, kontrakt limitów z 30A; rozwiązane pytania frost | Encounter-local; definicje trwałe |
| 20A Oczyszczanie według pochodzenia/kategorii | L08, zachowanie I160, shared filter dla konsyliarza | 19 i ustalone yield przy recipe | Zachować źródła istniejących efektów mikstur |
| 23A Typy terenu; 23B nakładki źródłowe; 23C dalsze podłoża | S03/S04/S06; 23C tylko z aktualnym konsumentem | 25A kontrakt zajętych pól, nie gotowe modele bossów | Layout/delta tylko dla trwałego świata |
| 24A Przechodzenie i koniec ruchu; 24B latanie i relacja ataku | S05; 1 polityka używana przez ruch/AI/preview/forced actions | 25A i 23A | Dane definicji, tryb encounter-local |
| 25A Footprint2×2 i wspólna geometria; 25B3×3 później | S02/S07/S12, stare bossy w regresji | 03; kontrakt ustalić przed 23/24 | Definicje; pozycja kotwicy kompatybilna |
| 26A Stationary; 26B Indestructible później | S09, zakaz chodzenia niezależny od teleportu | 25A | Definicje |
| 28A Wynik efektu; 28B multi-select; 28C summon/origin; 28D klony | S13/S14/L04/L05/L12 | 06A/B, 24/25, uzgodniony kontrakt klona | Encounter-local |
| 29A Fazy; 29B Jarl | Jeden kontrakt request/commit/cleanup + warunek Jarla | 06B, 28, geometria; pytania faz | Encounter-local |
| 30A Budżety akcji i ICD; 30B głowy Cerbera | L03 + B02, modele celowania głów i reward | 30A po 06B/25A; 30B dodatkowo po 29A i decyzji głów | Encounter-local |
| 33A Pool/mark/cleanse; 33B attachment i revive | L09/L10/L11 oraz Phoenix | 06B, 20A, 24/25/26 | Encounter-local |
| 34A Woda i fazy; 34B skille | B03 bez osobnego wariantu terenu | 23A/B, 25A, 29A | Encounter-local |
| 35A Wsparcie i death stacks; 35B skille | B04 z pulą SummonSystem i AV | 06B, 28C, 29A, 30A | Encounter-local |
| 38A Rośliny i dzielenie heal; 38B reszta wrogów | L11/L12, S09, pozostałe S01 | 24/25/26A, 28A, 33A | Encounter-local |
| 39A Aura/DOT/fazy; 39B teren i telegraph | B05, bez kopiowania całego ForestEntBossSystem | 06A/B, 11B, 23, 29A | Encounter-local |
| 40A Korzenie i teleport; 40B consume/explode/Głód | B06 i L06 | 06B, 11B, 25/26A, 28C, 29A, 30A | Encounter-local |

Wydzielenie wspólnych kontraktów z 06/11/28/29/30 pozwala wykonać je wcześniej niż pełny content ich kart. Nie oznacza wymogu skończenia całego Zbrojnego lub Cerbera przed innym biomem. Jeżeli implementacja ujawni brak kolejnego hooka, dopisać konsumenta do tego samego ownera zamiast tworzyć równoległy mechanizm.

## Kiedy potrzebne są karty 23–26

| Integracja | 23 teren | 24 ruch | 25 footprint | 26 pozostałe |
|---|---|---|---|---|
| Katakumby/Las, regresja obecnego contentu | Zachować istniejący difficult terrain | Niematerialny: Wraith i DeadKing | 2×2: SkeletonSlayer, DeadKing, ForestEnt | Bez nowego obowiązkowego indestructible |
| Elfie Ruiny 27–31 | Typy można przygotować; aktywna lawa nie wynika z przypisań | Flying Imp, levitation Eayan; reguły Infernal | Cerber 2×2; wspólne celowanie głów w 30B | Brak obowiązkowego 26B |
| Morze 32–36 | Woda + zmiana pól obowiązkowo przed 34 | Flying Phoenix/Parrot, Sea | AlphaShark 2×2 | Brak obowiązkowego 26B |
| Bagno 37–41 | Błoto + nakładki/telegraph obowiązkowo przed 39 | Swamp/Sea, levitation Guardian | Alligator/Ent/bossPlant2×2 | Stationary obowiązkowo przed 38/40 |

Rekomendowana kolejność: **04 → wspólne 25A/06A/06B oraz kontrakt limitów 30A → rozszerzenia klas w 05–12 (według zamknięcia designu) → 23A/B + 24A/B + 26A → kontrakty 28A–D/29A → biomy Ruiny, Morze, Bagno**. Art i pokoje mogą powstawać równolegle logicznie, ale odbiór encountera czeka na jego zależności. CP2-15–22 i katalog danych nie muszą czekać na wszystkie mechaniki bossów. Paladyn/unikaty po projekcie zgodnie z wcześniejszą decyzją. CP2-47 pozostaje późniejszą decyzją; ten audyt nie uruchamia kontrolowanego RNG.

## Decyzje projektowe do zamknięcia razem przed odpowiednimi kartami

Nie blokują zakończenia audytu ani startu kontraktu progresji 04. Blokują dopiero authoring wskazanego wymagania. Nie dobierać za użytkownika liczb ani istotnie różnych zachowań.

1. **Fazy, 29–40:** kiedy dokładnie następuje przejście i jakie HP/MP/AP/statusy przechodzą? Jarl i Cerber mają warunki w AG13/AG15; dla Shark/Captain/Ent/Plant arkusz opisuje obie fazy, lecz nie podaje analogicznego pełnego warunku przejścia. Czy wspólny próg to HP50% i zmiana na początku następnej tury, czy każdy ma inny?
2. **Cerber, 30:** jak wybiera się głowę na modelu2×2; czy AoE trafia jedną, wszystkie czy ciało; czy HP ciała jest sumą głów? „Średnie HP poniżej40%” dotyczy pozostałych żywych czy wszystkich trzech głów? Jak odpowiada temu 25%MaxHP na każdą głowę i CC tylko zaatakowanej głowy?
3. **Teren/cechy, 23–26/34:** przy footprint2×2 które pola decydują o byciu w wodzie i karach ruchu/ataku? Czy Stationary pozwala na przymusowe przesunięcie? Jaki jest stan początkowy i UX przełączania Flying za1punkt ruchu?
4. **Iluzje i papuga, 28/33:** które skille/passives/staty kopiuje iluzja i kto nią steruje? Czy attached papuga nadal otrzymuje AoE; gdzie kończy po śmierci nosiciela? Koszt i moment odrodzenia Feniksa, jeśli w chwili śmierci nie ma45MP/pełnego budżetu AP?
5. **Dokładne odwołania drzewek, 10/11:** D86: jedna ranga czy dwie i która wartość; D105: dwie rangi czy trzy wartości; D110: modyfikacja Mroźnej Osłony czy Aury Mrozu; D122: powiększenie rozmiaru obszaru czy zasięgu rzucenia? D104/D110 mają tę samą nazwę Śnieżny Duch — stabilne nodeID pozostają osobne.
6. **Projekty odłożone:** 08/13 zamykają Strażnika/Paladyna; 06 potwierdza zakres T6/T7; 42–44 projektują brakujące efekty unikatów. Brak yield w recepturach wskazany w CP2-02 rozstrzygnąć przy 19/20. Nie powtarzać rozstrzygniętych pytań o skóry/proch/kamienie i źródła zastępcze.

## Weryfikacja i odbiór

Przejrzano wymagania aktywnych skilli, opisanych węzłów/globalnych pasywek, bossów i cech z migawki CP2-02, a kod sprawdzono punktowo pod kontrakty E1–E14. Macierz grupuje równoważne mechaniki; nie deklaruje osobnego testu runtime każdego węzła. Brakujące projekty i przyszłe cechy są jawne. Zweryfikowano źródłowy hash XLSX, istnienie przywołanych plików i ID kart oraz aktualny walidator katalogu. Wyniki automatycznej kontroli dokumentu: `Tools/ContentPass2/system-audit-validation.json`.

Nie zmieniono XLSX, C#, assetów Unity ani formatu save; nie uruchamiano Unity ani nowych testów gameplayowych. Testy w macierzach są kryteriami przyszłych kart, nie raportem PASS. Brak manualnej konfiguracji Editora do przekazania w tej karcie. Odbiór: przegląd właścicieli, proponowanego rozbicia i decyzji; kolejna karta 04 może zacząć od ustalenia progresji bez rozstrzygania wszystkich bossów teraz.
