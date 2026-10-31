## v0.1.2 — lica i nakrywy czystych niskich narożników — 2026-10-31 — READY_FOR_SMOKE

Narożnik niskich murków ma poziomego właściciela, gdy taki odcinek istnieje: lico dochodzi pod całą szerokość capa, również dla N+E. Przy schodzącym niskim ramieniu S poziome lica nie wchodzą w obrys jego nakrywy; native UV po przycięciu zachowuje fazę. Wspólny narożnik rysuje zakręt oraz nakrywę nad licem, bez zmiany globalnych warstw postaci. Jawny opis końca rozróżnia wszystkie ramiona, rzeczywiście niskie ramiona, obecność pełnej ściany i właściciela; barierka nie dostaje kamiennej nakrywy. Pełne/mieszane styki zachowują wcześniejszy kompozytor.

365/365 końcowych testów PASS, bez skipów. Porównanie GPU z niezasłoniętymi prostymi odcinkami kontroluje lico, cap oraz UV w obu oświetleniach; testy obejmują lustra, T/krzyż, ponowną konfigurację, barierki, postacie, mgłę i bramy. Zmieniło się 61/711 układów, wyłącznie czystych niskich; 650 pozostałych, w tym wszystkie 406 mieszanych i 145 pełnych, identycznych pikselowo. Przejrzano wszystkie 61 zmian w obu trybach i konteksty większe. Serialized state, kolizje, mapa, gameplay i save bez zmian. Gameplay smoke NOT RUN; bez commit/push. Raport: `LocalHandoffs/catacombs-low-corner-face-20261031/report.html`. Aktualny atlas: `LocalHandoffs/catacombs-wall-atlas-20261031-fixed/index.html`. Starsze wpisy poniżej opisują wcześniejsze etapy napraw.

## v0.1.2 — mieszane pełne profile przy niskich murkach — 2026-10-31 — READY_FOR_SMOKE

Pełne profile South, Side i North przy niskim murku nie wykluczają wspólnego styku. Nakrywy składają się na odpowiadających im poziomach: Side zachowuje dolny cap i stopień, pełny północny Side zachowuje lico 0,75 i górny cap, a South łączy się z nakrywą niskiego murka. Sąsiednie stojące lico North pozostaje widoczne: maska South jest przycięta na jego rzeczywistej granicy; pierwotny zakres maski odtwarzany jest przed ponowną konfiguracją. Właściciel styku pochodzi z istniejącego sortowania głębokości. Zakończenia, kierunek skrętu i faza natywnych UV są zachowane. Nie dodano wyjątków po ID układu, nowych tekstur ani zmian serialized Unity state.

347/347 końcowych testów PASS, bez skipów; w tym ponowna konfiguracja 166 układów z wieloma pełnymi ramionami, geometria obu kierunków przejścia, kontrola szczelin na pikselach native/lit, mgła i bramy. W nowej macierzy 711×2 zmieniło się 48 układów wyłącznie z audytowanej rodziny; 663 pozostałe oraz sześć wcześniej zaakceptowanych większych kontroli identycznych pikselowo. Przejrzano wszystkie 48 zmian w obu trybach i większe konteksty. Kolizje, mapa, przechodzenie, gameplay i save bez zmian. Gameplay smoke NOT RUN; bez commita/pusha. Raport: `LocalHandoffs/catacombs-mixed-profiles-repair-20261031/report.html`; aktualny atlas: `LocalHandoffs/catacombs-wall-atlas-20261031-fixed/index.html`. Poniższy wpis zachowuje wcześniejszy etap napraw.

## v0.1.2 — mieszane styki murów Katakumb — 2026-10-30 — READY_FOR_SMOKE

Niski mur przy obniżonym pełnym profilu Side ma wspólny, pojedynczy styk: lico i nakrywa są wycinane z istniejących natywnych sprite'ów, a właściciela wybiera topologia wierzchołka. Po korekcie użytkownika kierunki są rozróżniane: niski N / pełny S zachowuje od dołu cap pełnego muru, jego zakończenie i następną nakrywę; pełny N / niski S zachowuje południowe lico pełnego muru, do którego stopy dochodzi niski odcinek. Nie wolno scalać obu typów w jeden ciągły cap. Nakładające się żądania zamknięcia krótkiego capu mają jednego właściciela. Pełny profil North zachowuje dotychczasowe zamknięcia. Profil South dzieli wysokość nakrywy z niskim murkiem: wspólny cap obejmuje wszystkie ramiona, a jego właściciel rysuje także nakładający się fragment południowej nakrywy ponad maską podłogi i sąsiednim licem. Maska nadal celowo zasłania podłogę; nie może wycinać narożnika muru. Diagnostyka używa koloru tła kamery projektu zamiast własnego odcienia. Dowody tej korekty: `LocalHandoffs/catacombs-south-low-corner-20261030/`. Nie zmienia to kolizji, przechodzenia przez murek, wysokości logicznej, formatu map/zapisów ani zasad mgły. Zachowano celowe obniżanie pełnych ścian; normalna wysokość z zanikaniem pozostaje osobną propozycją. W 711 układach lokalnych (native i z pochodnią) 31 wcześniejszych rozłączonych wariantów bez jednostronnych murków ma teraz ciągłą geometrię obrazu. Układy wyłącznie niskie lub wyłącznie pełne (305) pozostały identyczne piksel w piksel. Raport i smoke: `LocalHandoffs/catacombs-wall-joins-repair-20261030/`, korekta kierunków: `vertical-v2/`.

## v0.1.1 — kamienie bossów — 2026-10-27

Obsydian i jadeit nie wypadają z toreb zasobów. Każdy boss Katakumb ma dodatkową niezależną pulę 50% na 1–2 obsydianu, a każdy boss Lasu 50% na 1–2 jadeitu; dotyczy obu przedziałów toreb 1–10 i 11–20. Szanse odnoszą się do próby wygenerowania łupu, przed odfiltrowaniem pustej torby i przed istniejącymi bonusami dropu. Pozostałe pule bossów/unique zachowane. Bazowa podaż pozostałych materiałów z zasobów zachowana; przy dodatnim addytywnym bonusie dropu po usunięciu wpisu występuje mały wzrost udziału pozostałych kamieni (szczegóły w KANBAN).

Jadeit (`item.runic.jadeite`) jest rzadkim leśnym odpowiednikiem obsydianu: cena bazowa 2847, jedna sztuka na kamień biżuterii, +3,5% zadawanych obrażeń wyłącznie w Lesie. Dostępny we wszystkich czterech typach biżuterii przez istniejący crafting i losowanie; cztery kamienie sumują się do +14%. Obsydian zachowuje analogiczną premię dla Katakumb. Zmiany danych zapisano przez Editor API, bez zmiany formatu save ani istniejących ID. Już zapisane/prerollowane nagrody pozostają; nowy loot wymaga nowej wyprawy.

## Korekta zakończenia pionowego muru — 2026-10-26 — READY_FOR_SMOKE

Aktualna zatwierdzona zasada: górny odcinek cap → cap → cap → lico na wolnym dolnym końcu; dolny odcinek nadal ma zamknięty cap u góry. Lico otrzymuje wyłącznie południowy terminalny segment wewnętrznej przegrody, bez zamiany początku ciągu na lico. Natywny materiał 24px lica + 8px capa, bez nowej grafiki, zmiany kolizji, bram, low-wall lub serialized assets. 82/82 celowanych testów renderowania/ścian PASS. Porównanie i checks.xml: ../LocalHandoffs/side-terminal-face-20261026; aktualny render również w raporcie loot-portraits-walls-20261026/report.html. Manualny gameplay smoke NOT RUN; bez commit/push.

## Dropy i pionowe capy — 2026-10-26

Bezpośrednie źródła wyposażenia w skrzyniach/nagrodach jednostek losują każdą sztukę niezależnie. Definicje torby mogą służyć jako katalog wag/jakości, lecz nie wspólny rzut na pakiet. Prawdziwe lootbagi zachowują dotychczasowe działanie. Budżet oczekiwany skrzyń pozostaje 5 sztuk na wyprawę; nie jest limitem ani gwarancją każdej skrzyni. Istniejące zapisane/prerollowane łupy nie są zmieniane. Pionowy pełny mur używa ciągłego capa, bez przechodzenia w lico; wolne końce wewnętrznych przegród mają zamknięte obramowania. Floating text nie wyświetla dodatkowego komunikatu powalenia ani jego przyczyny; pozostaje liczba obrażeń w odpowiednim kolorze i animacja.

## Korekta: celowanie przy stopach, oryginalne zasięgi — 2026-10-26 — READY_FOR_SMOKE

Wycofano ciągłe obrysy z poprzedniej karty. SkillHover oraz SkillRange znów używają dotychczasowego zestawu pól, obwiedni, wypełnienia i kolorów; bez nowego okręgu, skalowania i zmiany typu zasięgu. Obszarowe AffectedArea wróciło do oryginalnej prezentacji; nie jest zamieniane na nowe narożniki przy jednostkach.

W opening preview poza walką wyłącznie znaczniki SkillTarget/ChosenSkillTargets mają dotychczasowe narożniki pod rzeczywistymi stopami. Wskazanie jednostki kursorem może użyć tego samego hitboxu przy stopach, także ponad granicą logicznego pola; wybierana jest nadal ta sama BattleUnit/pozycja logiczna. Zasady legalności, LOS, koszty, typ/promień skilla, AoE, teleport i ground placement pozostają. Resolver odrzuca niewidoczne/niewybieralne widoki i jest odłączany na cancel/commit; w walce wszystko nadal przy siatce.

31 EditMode PASS: identyczne vertexy/kolory dawnych highlightów Square/Radial/Ring przy włączonym resolverze stóp; klik przy stopach po przeciwnej stronie granicy pola; najbliższy cel i ukryte jednostki; znaczniki i reset na siatkę; regresje wejścia do walki, PA i pojedynczego DoT. Render porównawczy obejrzany. Import/kompilacja i diff check PASS. Serialized assets, żywopłot, tracker i obrażenia z poprzedniej karty bez nowych zmian. Manualny gameplay/FPS NOT RUN; bez commit/push.

Raport: LocalHandoffs/target-feet-correction-20261026/report.html; checks-final.xml; targeting-restored.png. Ten wpis zastępuje opis ciągłych obrysów z poprzedniej karty.

## Prezentacja eksploracji: Godmist UI, stałe obrażenia, swobodne markery i żywopłot — 2026-10-26 — READY_FOR_SMOKE

Śledzone zadanie przebudowane na natywnym PF_UIW_Section (ramka, tytuł, font/material), bez przechwytywania kliknięć. DamageRange ma wspólny formatter: stałe MinAttack/MaxAttack/AverageAttack pokazują N, rzeczywista zmienność nadal min–max; opis skilla i preview używają go wspólnie. Nie zmieniono obrażeń ani krytów.

W eksploracji zwykłe zasięgi Square/Radial/Ring mają ciągły obrys zakotwiczony w rzeczywistej pozycji. Wybór celów/legalność/LOS nadal z istniejącej siatki; kierunkowe AoE pozostawiają swój footprint. Markery celu/wybranych/trafionych postaci rysują się pod aktualnymi stopami, także w ruchu wewnątrz pola. Callbacki ownera eksploracji są usuwane na cancel/commit; walka nadal pokazuje znaczniki w polach. Bufory obrysu i aktualizacji markerów są wielokrotnie używane, lookup bez LINQ w LateUpdate.

Nowy atlas hedge-segments-v2.png (16 sprite'ów, 32 PPU, Point, alpha) oraz LowHedgeSegments na DVP_Forest_Prototype. Nowa grafika z built-in imagegen, a nie rozciągnięte LowShrub. H/V natywne, bez obrotu i skali; wnętrze pionowego segmentu nie powiela końcowych korzeni. Łączenia składane z właściwych ramion nowych grafik H/V według tego samego endpoint index co murki, osobne zakończenia. Test pikselowy kontroluje brak niepodłączonych ramion. Nie zmieniono kolizji, walki, niskich ścian katakumb ani dekoracyjnych LowShrub używanych poza żywopłotem.

55 unikalnych końcowych EditMode PASS; natywne rendery UI, obrysu/markerów i żywopłotu w lesie oraz L/T/+ obejrzane. Regresje wejścia do walki potwierdzają brak darmowych PA i podwójnego DoT. Serialized import przez Editor API: wspólny prefab trackera i DVP lasu; sceny zachowują dotychczasowe referencje. Brak zmiany save/schema, agentów, Computer Use, commit/push. Gameplay/FPS smoke NOT RUN.

Raport: LocalHandoffs/exploration-presentation-20261026/report.html. Wyniki: checks-final.xml. Grafika źródłowa i prompt są zachowane w tym samym folderze.

## Statystyki drugorzędne, mana, diagnostyka i UX — 2026-10-26 — READY_FOR_SMOKE

Wdrożono profile 14 zwykłych mobów względem wyposażonych bohaterów, z hybrydami (np. ghul). L1 obrońca: PDEF 80 → 40, MDEF 10 → 12; Reimond z wyposażeniem startowym ma 24/24. HP, ATK, bossowie/formy, celność i szybkość mobów bez zmian. Pełne skopiowanie przyrostów heroicznych dawało nadmierny buff na wysokich poziomach, dlatego zwykłe moby dostają część budżetu ekwipunku/biżuterii/drzewka. Pancerz obrońcy pozostaje wyjątkiem roli. Na L10–50 średni indeks fizycznych trafień do zwykłego moba względem poprzedniego wariantu: katakumby około −3% do −1%, las około +1.5% do +4%. To indeks statyczny, nie procentowa prognoza trudności wyprawy.

Walka: końcowa regeneracja many bohaterów 0/50/100% PA → 25/100/200% bazowej regeneracji, liniowo pomiędzy. Dla 15 regen daje 4/15/30 punktów. Automatyczna regeneracja w eksploracji nadal OFF; mikstury i obóz zachowane. Celny Rzut: SkillAccuracy 85 → 120, bez zmian kosztu/obrażeń. Rzut Sztyletem przy pustej puli nie pokazuje dalekich wrogów jako celów; darmowe zwarcie i odzysk własnych sztyletów zachowane, także spójność poziomu pola.

UI/świat: „Przywraca zdrowie: 15% maks. HP” i „Przywraca zasób: 20% maks. zasobu”; krótkie blokady usług zaklinacza z poziomem 11; kowal bez surowego/startowego gearu w głównej ofercie (również naprawa podczas load, bez naruszania odkupu i ekwipunku gracza). Śledzone zadanie ma wspólny prefab HUD pod minimapą, przypięty przez Editor API w Arungard/DungeonSandbox; odświeżanie zdarzeniami, nie skan co klatkę. Płomienie/elementy pochodni przygasają według wspólnego filtra zapamiętanego terenu, źródła oświetlenia pozostają aktywne.

Diagnostyka opt-in: `combattrace on|off|show|clear|export`, do 4000 wpisów w pamięci. Seed/piętro, koszty PA/zasobu, oceny planów i powody rezygnacji/przerwania, faktyczne HP/tarcze przed/po obrażeniach, źródło/cel/skill/kryt, jeżeli kontekst go zawiera. Plik tylko po export w persistentDataPath/CombatDiagnostics. Test obrońcy z zerową furią potwierdza podejście otwartą drogą i powód braku ruchu przy przegrodzie; nie jest to deklaracja naprawienia wszystkich przypadków AI bez ich zapisu.

193 unikalne końcowe EditMode PASS, 345 punktów kontroli HP/ATK, porównania statyczne L1/5/10/15/20/25/30/40/50 i kontrola pośrednich poziomów. Natywne rendery HUD oraz pochodni w obu lochach obejrzane. Starsze authoringi zachowują nowy wariant i raportują faktyczną zmianę stanu. Import/kompilacja i self-review PASS. Manualny gameplay/FPS smoke NOT RUN; bez agentów, Computer Use, commit/push.

Raport: LocalHandoffs/combat-economy-ui-20261026/report.html; wyniki: checks-final.xml; statystyki: stats-before.csv/stats-after.csv; skille/hity: theory-level-N.json. Powyżej L20 odniesienie ekwipunku korzysta z projektowych części referencyjnych 25/35/45 z arkusza, a nie wyłącznie przedmiotów już dostępnych w grze. All allies/enemies → skończony duży obszar: tylko analiza w raporcie, bez zmiany mechaniki; do ustalenia promień, LOS, poziomy oraz wyjątki bossów.

## Mana poza walką, wyjścia i Umarły Król — 2026-10-26 — READY_FOR_SMOKE

Poza walką automatyczna regeneracja many jest wyłączona (bazowa oraz pasywna z wyposażenia). Leczenie za manę pozostaje dostępne; jawny odzysk z mikstur i obozu, HP z wyposażenia oraz reguły regeneracji w turach walki pozostają. Zaktualizowany opis many i testy dawnego przyrostu eksploracyjnego.

Nowe schody w górę v3 i zamknięte zejście z kratą. Wyjście lasu ma zamknięty wariant furtki; wspólny pivot, rozmiar i położenie z otwartą. Stan otwiera się po pokonaniu straży końcowego pokoju, liczony ponownie tylko po zmianie liczby ukończonych encounterów. Zablokowane wyjście pozostaje widoczne w znanym terenie, ale nie ma aktywnego hover/interakcji ani popupu o oczyszczeniu. Potwierdzenie zakończenia wyprawy zachowane. Dodatkowa rejestracja schodów w oświetleniu gruntu i cleanup przy zniszczeniu.

Widmowy Rycerz: poprawione E/W w wyborze grafik idle/ruch/akcji, bez zmiany kierunków świata. Techniczny summon.health ukryty w statusach, w zamian prezentacyjna Widmowa powłoka z ikoną/opisem i pozostałymi trafieniami. HP/modyfikator przywołania i mechanika 3 trafień/odporności na DoT niezmienione. Cień Destrukcji korzysta z opisu właściwej mechaniki, bez pustego „przyspieszenia 0%”.

189 odrębnych końcowych EditMode PASS. Natywne rendery obejrzane; kontrolowana reakcja shaderów schodów na pochodnię: up 2,219× / down 2,369× jasności otoczenia. Sprawdzone mikstury, obóz, przepełnienie, regeneracja w walce, statusy/fazy Króla, zachowanie wyjść i mgły oraz pivot furtki. Import/kompilacja i self-review PASS; gameplay/FPS NOT RUN. Editor API: prefab wyjścia, 2 wpisy glosariusza i import 3 nowych sprite'ów; brak zmiany save/schema, ręcznego setupu, Computer Use, agentów, commit/push.

Raport/obrazy/prompty builtin image_gen: ../LocalHandoffs/mana-exits-deadking-20261026/report.html. Dokładne wyniki: validation.json oraz końcowe XML. Wcześniejsze informacje o naturalnym odzysku many co 6 s i grafice upstairs v2 są zastąpione tą decyzją.

## Korekta polaryzacji przeciwników — 2026-10-25 — READY_FOR_SMOKE

Wdrożono zatwierdzony wariant HP/MDEF dla14zwykłych mobków. ATK i jego cała krzywa, PDEF, dodge/crit/speed/PA, zasoby, skille, odporności statusów, łupy, encountery oraz wszystkie9pozostałych definicji przeciwników (bossowie/fazy i specjalny Rycerz) zachowane.929hashy assetów: dokładnie14zmian; wszystkie pola pozaGrowthData.HealthCurve, BaseStats.MagicalDefense oraz MDEF istniejącejDefenseCurve identyczne z zamrożonym stanem przed kartą. Nie zmieniano przywołań Druida.

CharacterGrowthData ma opcjonalną HealthCurve (absolutneHP, pusty/błędny profil zachowuje legacy). CharacterLevelGrowthSystem interpoluje jednostki wzrostu, fabryka stosuje krzywą również przyL1. Ranne/ocalałe zapisane jednostki nie dostają darmowego leczenia przy odtworzeniu/refreshu; HP ponad nowymax jest clampowane. Brak zmiany schematu save. Wcześniejsze współczynnikiHP w definicjach pozostawione jako fallback. EnemyRoleDurabilitySetup autoruje tylko zwykłe14ID przez EditorAPI, bez mnożenia już zmodyfikowanych wartości; oba wcześniejsze authoring paths zachowują nowy wariant i idempotencję.

Zachowano wszystkie zatwierdzone kotwiceHP1/10/20/30/40/48/50. Pierwszy eksport wszystkich50poziomów wykrył+6–7%pracy przyL15i około+3%przyL25; dodanoHP15/25i ponowiono produkcyjne eksporty wszystkich zmienionych poziomów11–29. Finalne eksporty wszystkich1–50zgodne z finalną krzywą. Wyjątek zaokrągleniaMDEF względem propozycji: WilkL48=61 zamiast60, pozostałe kotwice dokładne.

Wynik: średnia rozpiętość trwałości naL1 katakumby3,83×→2,33×, las2,76×→1,95×. Wszystkie poziomy: katakumby2,18–2,62×, las1,88–2,16×. Budżet mieszanej drużyny we wszystkich długościach/poziomach względem sprzed specjalizacji: katakumby−1,8%…+1,4%, las−2,6%…+1,7%; pojedyncze seedy odpowiednio około−5,5%…+6,4% i−6,2%…+5,7%. Normalne lochyL15 około+0,1%katakumby/−0,2%las. Klasy nie idealnie równe: Mag w katakumbach do+7,2%pracyL44; Mag w lesie od−5,9%L14do+4,6%L50. Nie utożsamiać budżetu basiców z procentem trudności całych walk.

Weryfikacja: autorowanie1PASS, dopasowaniepośrednie1PASS, finalna regresja111PASS/0FAIL/0SKIP (wzrost, staty, role, idempotencja, legacyfallback, save/restore, pełny ponowny eksport11–29). Pierwszy przebieg:116PASS/1FAIL; stara asercja ATK porównywała raw baseline z efektywnym ATK po istniejącym mnożniku0,85. Poprawiono wyłącznie test; frozen i current MiecznikL22 mają identyczne171,84584minimumATK. HistoricalHPassertions sprawdzają nadal legacy na odłączonej kopii bezHealthCurve; noweHPma własne testy wszystkich50poziomów.

156024produkcyjne podglądy skilli: basici4klas×2drzewka×4materiały na1–50; dodatkowe skille, prawdziwePA/zasoby, przebicie i pełny oczekiwanyDoT na17poziomach, także progi zależne od nowegoHP. Porównanie oryginalny stan / poprzednie role / wdrożenie. Bez AI/autoplay i losowańRNG. Druid przy1własnej narośli, bez osobistego udziału DPS/healu przywołań i bez wyceny setup/CC. Późny gear25/35/45 referencyjny, aktualna produkcyjna pula części kończy się15.

Te same36pełnych układów6seedów71016–71021 ×3długości ×2biomy, przeliczone na50poziomach; nie wygenerowano1800nowych map. Zachowane censusy1/10/20/50są identyczne poza poziomem statystyk; topologia i wybór encounterów bazują na seed. Żadne definicje map/encounterów nie zmienione.

Raport: ../LocalHandoffs/enemy-role-durability-implementation-20261025/report.html. Dowody: validation.json, regression-tests.xml, actual-stats.csv, profiles.csv, defense-spans.csv, dungeon-work.csv, per-route-work.csv, skill-costs-and-dot.csv, defender-armor.csv oraz applied-level-1…50.json. Pierwotne baseline'y i historyczne raporty zachowane; authoring starej karty nie jest częścią finalnego filtru odczytowego. Bez Computer Use, agentów, commit/push. Brak ręcznego wiring w Unity.

Smoke użytkownika (NOT RUN): świeże spotkanie Obrońca/Okultysta oraz grupa zDruidem, cios fizyczny i magiczny oraz kontrskill z przebiciem; zapis/wczytanie rannegoNPC bez darmowego leczenia. Szczególnie sprawdzić odczuwalny czas działania supportaDruida. Nie obiecywać identycznego zużyciaHP/zasobów na podstawie samej teorii.

## Propozycja zmniejszenia polaryzacji mobków — 2026-10-25 — PROPOSAL_ONLY

Nie wdrożono zmian gameplayu. Wariant: redystrybucja HP i obniżenie wybranych MDEF, bez zmian PDEF/ATK/dodge/crit/speed/PA/status resist/encounterów. Mocny fizyczny pancerz Obrońcy zachowany; jego HP na L1 339→209. Okultysta HP154→176, MDEF95→48. Rozpiętość średniej trwałości L1 katakumby3,83×→2,33×, las2,76×→1,95×; per-klasa nadal możliwe większe różnice, np. Wladimir katakumby3,14×. HP ma osobne proponowane kotwice1/10/20/30/40/48/50; nie kopiować procentuL1 na całą grę. MDEF stały mnożnik istniejącej krzywej.

7/7 nowych odczytowych testów ExportRoleProposalStatGrid PASS. Produkcyjne preview na detached runtime stats, source JSON i RNG niezmienione, wariant1,0 zgodny z frozen theory. Użyto144 poprzednich census instances (6seedów×3długości×2biomy×4poziomy); nie generowano nowych. Dla mieszanej drużyny łączne budżety długości/poziomów od−1,3% do+0,9% względem sprzed zmian; pojedynczy seed około−5,0%…+5,4%. Poszczególne klasy nie idealnie0: np. normalne L50 katakumby Mag+6,1%, fizyczne−1,6%…+0,8%. To teoria trwałości/basiców, nie dowód równej trudności pełnych kitów. Druid1growth, przywołania/CC/setup poza oceną osobistegoDPS. DoT model ma caveat progów zależnych od nowegoHP. Bossowie/fazy bez zmian. Przy wdrożeniu wymagane dopasowanie krzywych i testy poziomów pośrednich; jeszcze nie zaimplementowane.

Raport: ../LocalHandoffs/enemy-role-correction-proposal-20261025/report.html; proposed-stats.csv, profiles.csv, defense-spans.csv, per-route-work.csv, dungeon-work.csv, validation.json, grid-tests.xml. Scope-check nadal PASS: dokładnie22 wcześniej zatwierdzone assety zmienione względem stanu pierwotnego; brak nowych zmian definicji przez tę propozycję. Bez agentów, Computer Use, commit/push.

## Profil ofensywa/defensywa każdego mobka — 2026-10-25 — ANALYSIS_COMPLETE, NO_BALANCE_CHANGES

Użytkownik chce przed dalszym balansem ocenić każdego mobka: ile ataków bohatera wymaga zabicie go, ile własnych ataków potrzebuje na bohatera, proporcję osi i odchylenie od średniej oraz rozpiętość względem klas bohaterów. Bez zmian gameplayu i bez autoplay. Poprzednie+15,5% katakumb to praca basicami, nie pomiar całej trudności. Nerf przeciwnej osi może kompensować moc, ale zwiększa specjalizację — nie utożsamiać neutralnej siły z niewielką polaryzacją.

D=HPmobka/średni damage basicu bohatera; A=HPbohatera/średni damage basicu mobka; ofensywaO=1/A, defensywaD. Pudła/krytyki wliczone, ułamkowe indeksy bez overkilla. Obie osie normalizowane osobno do średniej mobków tego samego biomu/poziomu i tego samego bohatera/gear/build, potem równo uśrednione. ProporcjaR=Orel/Drel; indeksroli=log2R−średnia(log2R) po typach mobków. Mean0; dodatni ofensywny, ujemny defensywny, +1 oznacza2× bardziej ofensywną proporcję od środka geometrycznego, NIE procent mocy. Osobny wskaźnik siły wymianyD/A, przed/po na stałej starej bazie, geometryczne uśrednienie sparowanych relacji. Dzięki temu centrowanie roli nie ukrywa zbiorowego buffa. Bossów nie wliczano do średnich zwykłych przeciwników.

14 zwykłych mobków, 4 bohaterów ×2 legalne drzewka ×4 materiały ×7 poziomów. 4 modele: bezpośrednie basici; basic+pełny oczekiwanyDoT; tempoPA/AV; tempo+DoT. 25088 wariantów,784 profile mobków,448 profili bohaterów. Ten sam finalny theory.json poprzedniej karty (obrońca1500/1900 na48/50), bez implementacji proponowanego dalszego nerfa. HP/ATK/szybkość/zasoby przed/po zachowane.

Druid wymaga jawnego stanu: Gniew Natury zadaje0 przy0 własnych narośli. Nowy odczytowy EnemyBattleSimulationTests.RolePolarity.cs liczy produkcyjne podglądy przy0/1/3 naroślach w detached session, bez przywoływania/skilli/tur/RNG i zapisu assetów. 7PASS/0FAIL; model główny dokładnie1 narośl, osobne stany i ograniczenie: nie wyceniono leczenia/trucizny samych narośli. Druid nie jest klasyfikowany jako zwykły tank z zerowym atakiem. Ghul/Umarlak/Wilk i Drzewo mają osobny wynik zDoT; bez niego profil jest niepełny. DoT liczone z szansą/hitem/odpornością za cały czas jednej aplikacji, nie pewne natychmiastowe obrażenia.

WynikL1: rozpiętość defensywy katakumb przed2,57×, po3,83×; bohaterowie w tym samym benchmarku2,68×. Las przed2,53×, po2,76×; bohaterowie2,57×. Obrońca uśredniony po4 klasach: D15,7→28,8, A32,2→33,2, indeksroli−1,96 i siła wymiany+64%. OkultystaD6,1→7,6 A10,2→10,2, rola+1,46, siła+18,8%. UpiórD7,3→9,3 A14,5→14,5, rola+0,73, siła+27%. Paladyn w benchmarku katakumb rola−0,94, Mag+0,99 — słabsza skrajność niż mobki. Ghul bezDoT−0,78, zDoT około+0,2; nie uznawać go na podstawie samego bezpośredniego hitu za wyłącznie defensywnego.

Przykład kompensacji: przy pozostawieniu obecnej trwałości Obrońcy samo odtworzenie starego wskaźnika wymiany wymagałoby naL1 około39% dodatkowo mniej SKUTECZNYCH obrażeń basicu (nie równoznaczne zATK−39%). Względem Wladimira około−49,4%, Jerzego−45,2%, Maga−1,9%, Paladyna−49,1%. Jeden globalny nerf obrażeń nie odtworzy naraz każdej starej relacji. Podwojenie defensywy i połówka ofensywy utrzymuje iloczyn mocy, a polaryzacjęO/D przesuwa4× w stronę defensywy. Przed decyzją o zmianach przejrzeć obie osie, nie sam indeks roli.

Raport: ../LocalHandoffs/enemy-role-polarity-20261025/report.html. CSV: before-after, mob-profiles, hero-reference, polarization-spans, paired-inputs, druid-contexts, compensation-by-hero, boss-stat-controls oraz validation.json. Bossowie/fazy to osobne kontrole1×ATK, bez pełnych mechanik osłon/aur/niewidzialności; nie porównywać ich liczby hitów wprost z realnymi basicami zwykłych mobków. Widmowy Rycerz poza ilorazem, nadal3 trafienia. Wyższe części25/35/45 referencyjne z arkusza. Sprawdzone znaki i mean0 na niezależnych przykładach, widoczność buffa i algebra kompensacji; źródła niemutowane. Bez agentów, Computer Use, commit/push.

## Audyt trwałości przed/po balansem ról — 2026-10-25 — ANALYSIS_COMPLETE, RETUNING_REQUIRED

Zgodnie z prośbą porównano aktualne mobki z zamrożonymi definicjami SPRZED zmiany, a nie tylko między sobą. Gameplay niezmieniony; propozycja dalszego osłabienia Obrońcy jeszcze niewdrożona. Źródło: finalny theory.json poprzedniej karty, realne basici, te same drzewka/wyposażenie przed/po. 14 zwykłych mobków, 4 klasy ×2 drzewka ×4 materiały ×7 poziomów, 392 porównania i6272 warianty. Osobno HP/średni damage próby z pudłami/krytykami oraz HP/średni damage pod warunkiem trafienia. Indeksy ułamkowe bez overkilla, nie dokładne tury/ETTK.

Nowy odczytowy EnemyDurabilityRouteCensusTests eksportuje AKTUALNY generator/placement:36 pełnych układów (6 seedów71016–71021 ×3 długości ×2 biomy), każdy na1/10/20/50 =144 instancje. Wszystkie piętra/pokoje/odnogi/encountery, bez omijania i urywania po porażkach AI. Składy15 aktualnych encounterów zgodne z wejściem statystycznym. Nie użyto starych map lasu ze starą wersją patrolu. Census4PASS/0FAIL i niemutowanie definicji; bez symulacji walk, AI i zmian assetów. Końcowa kontrola929 hashy nadal dokładnie22 wcześniejsze zmienione definicje.

WYNIK: zmiana nie jest neutralna dla budżetu trwałości. Normalne lochy, praca mieszanej drużyny na równowartość pełnych pasków PA: las na1/10/20/50 +4,7/−5,8/−6,4/−5,1%; katakumby +15,5/+1,4/+0,9/+3,6%. W katakumbach na1 poszczególne klasy potrzebują +20,8% Wladimir/+17,4% Jerzy/+25,2% Mag/+22,4% Paladyn prób basiców. Na50 fizyczni −0,3/+0,3/+3,1%, Mag+18,1%. Las na50: fizyczni około−10..−11%, Mag+2,5%. Warianty długości i seedów w raporcie; krótkie katakumby na1 +17,8% mieszanej pracy, długie+14,8%.

Największe zmiany na1: Obrońca vs Cięcie16,3→33,3 prób (+104%); Okultysta vs Kula Energii5,2→12,3 (+136%); Upiór vs Kula Energii5,9→11,1 (+90%). Sam Obrońca odpowiada za+10,7pp z+15,5pp mieszanego budżetu normalnych katakumb. Niewielkie spadki presji wrogich basiców (około1–3%, zmiany krytyków) nie dowodzą kompensacji większej trwałości. Bossy oddzielnie: stat-only x1ATK przed/po, bez aktywnych osłon/aur/faz; Widmowy Rycerz nadal3 trafienia. Bez mieszania bossów do standardowej puli.

Wnioski ograniczone do basiców; nie dowodzą liczbowej zmiany całej trudności/zasobów. DoT/odporności, aktywne kontry, CC, leczenie, Kościana Osłona, geometria i rzeczywiste rotacje poza tym modelem. Późne części25/35/45 referencyjne z arkusza. Przed uznaniem balansu ról za neutralny potrzebna ponowna kalibracja względem łącznego budżetu składów i poziomów (proponowana tolerancja±5%, do decyzji), nie samych relacji pancerzy. Wcześniejszy READY_FOR_SMOKE oznaczał zakończenie implementacji i testów kodu, nie potwierdzenie neutralności trudności; ten audyt wyraźnie wskazuje nierozwiązany problem balansu.

Raport: ../LocalHandoffs/enemy-durability-before-after-20261025/report.html. CSV: per-enemy-before-after, per-build-indices, dungeon-work-summary, per-route-work, encounter-work, change-contributions, enemy-basic-pressure, boss-stat-only-indices; validation.json i census-tests.xml. Bez agentów, Computer Use, commit/push i mutacji gameplayu.

## Drugorzędne statystyki przeciwników według ról — 2026-10-25 — READY_FOR_SMOKE

Wdrożone 18 profili bazowych +4 warianty faz (22 definicje): obrony, uniki, krytyki i wybrane odporności zgodnie z zatwierdzonym kierunkiem lore/roli. Mocny pancerz Obrońcy, magiczna osłona Upiora/Okultysty, zwinność bestii/elfów, słabsza ochrona strzelców po dojściu, pień Drzewa/Enta. Upiór i Król odporni na bleed/poison; zachowana słabość drewna na burn. HP, ATK (łącznie z mnożnikiem .85), szybkość, celność, zasoby, skille, łupy, wygląd oraz reguła 3 trafień Widmowego Rycerza pozostają bez zmian.

Opcjonalna absolutna DefenseCurve w CharacterGrowthData; interpolacja po dotychczasowych jednostkach wzrostu, walidacja i zaokrąglenie wyniku. Brakująca/pusta/niepoprawna krzywa zachowuje legacy. Nowy EnemyRoleStatsSetup zapisuje celowane definicje przez Editor API i jest końcowym krokiem EnemyScalingBalanceSetup, więc ponowny pełny authoring nie cofa profili. Bez zmian schematu save i pracy per-frame; restore przelicza wzrost z definicji.

Użytkownik zastąpił akceptację przez autoplay porównaniem statystyk/teoretycznych trafień. Nie uruchamiano kolejnych symulacji AI po tej decyzji; wcześniejsze wyniki zachowane wyłącznie jako historyczne artefakty, nie dowód balansu. Finalny odczyt: 224 konfiguracje bohaterów (4 klasy ×2 legalne drzewa ×4 materiały ×7 poziomów), 546 wierszy statystyk, 9856 indeksów trafień, 18480 kart skilli. Ten sam gear builder co wcześniejsza propozycja: level-fit Common, jakość zależna od poziomu, biżuteria od10, bez unikatów/ulepszeń. Basici obejmują wszystkie materiały; dodatkowe skille/DoT materiał0. Uwzględnione trafienie, krytyk przed pancerzem, przebicie, faktyczne PA/zasoby, Wyładowanie i próg50 oraz szansa/czas DoT. Bez RNG, rzutów, decyzji i tur. HP/średni damage to ułamkowy indeks prób, nie dokładna prognoza tur/zabicia z overkillem.

Pomiar realnego Cięcia ujawnił przeszacowanie późnego Obrońcy: pierwotna propozycja dawała średnio74,7% redukcji na48 i72,4% na50. Skorygowano wyłącznie późny PDEF:1500 na48 i1900 na50, nowa kotwica48; MDEF322/360 zachowane. Finalne średnie redukcje przed przebiciem na1/10/20/30/40/48/50:66,2/65,5/66,7/67,2/67,3/64,4/64,0%. Każdy poziom sprawdza osobny test celu60–70%. Na50 indeks prób zwykłego Cięcia wzrósł16,4→21,6. Pozostałe profile zachowują zatwierdzone wartości.

Pełny parytet kitów NIE jest dowiedziony: same pancerze mają małą różnicę globalną (kontrola około−7,6% do+5,1% przewagi magii), ale istniejąca przewaga Maga w darmowych basicach pozostaje. Przykład50, damage/pasekPA vs średnia Wladimir/Jerzy: las75,8→51,4%, katakumby54,3→26,6%. Nowe pancerze zmniejszają tę różnicę, nie tworzą jej. Bez zmiany bohaterów/AI/celności czy kosztów w celu wymuszenia parytetu. Dalszy balans basiców i gwarantowanego trafienia wymaga osobnej decyzji. Bossowe aury/fazy/osłony i DoT pełnej rotacji opisano jako oddzielne ograniczenia; nie mieszano ich do standardowej puli15 encounterów. Części25/35/45 są referencyjne z arkusza; aktualna pula produkcyjna kończy się15.

Końcowy pakiet157 PASS/0 FAIL, 4 explicit authoringi pominięte; obejmuje7 odczytów teoretycznych, wzrost/stat pipeline, damage/statusy, Wyładowanie, stun/DoT i save/restore. Celowany authoring osobno1 PASS. Wczesny zbyt szeroki eksport przekroczył limit180s; finalnie rozdzielony na7 poziomów, wszystkie PASS. Spośród929 hashy zmienione dokładnie22 dozwolone definicje; pełne porównanie pól23 definicji i kontrola core na7 poziomach PASS. Sprawdzone przywrócenie stanu bohatera, niemutowanie źródeł i brak pobierania combat RNG. Kompilacja/import i final self-review PASS. Manualny gameplay/FPS NOT RUN. Bez ręcznego setupu, agentów, Computer Use, commit/push.

Raport i dane: ../LocalHandoffs/enemy-role-implementation-20261025/report.html; theory-stats.csv, theory-hit-indices.csv, theory-skills.csv, basic-attack-summary.csv, defender-comparison.csv, armor-only-screen.csv, validation.json oraz XML. Smoke: nowa walka z Obrońcą, zwykły cios vs Wyładowanie z Przełamaniem, fizyczny doskok do Okultysty/Upiora i obie fazy bossów. Balans całych kitów nie jest zamknięty samym statusem READY_FOR_SMOKE.

## Wdrożony balans dropu materiałów — 2026-10-25 — READY_FOR_SMOKE

Zgodnie z „dobra to zmieniaj drop” zapisano celowanym Editor API dwa produkcyjne assety toreb zasobów, las/katakumby i oba przedziały. Wdrożone proporcje kowalskie55/32/3/10% oraz80/8/3/9% (metale/skóry/drewno/włókna), wszystkie gatunki i profile biomów z uzgodnionej analizy oraz obniżenia ilości pod2,5 ekwiwalentu przeciętnego przedmiotu Normal na normalną wyprawę. Mnożniki względem wcześniejszej propozycji: próg5 las0,777678 /katakumby0,456628, próg15 las0,478574 /katakumby0,278843. Runiczne11–20:85% klejnotów/15% wapienia, to samo obniżenie ilości; bez nowych gwarantowanych runicznych dropów1–10. Nieużywane pyły energetyczne usunięte z podstawowych toreb. Zmiana dotyczy nowych losowań: już otrzymane torby i zachowane nagrody starej wyprawy nie są rerollowane.

Nowe opcjonalne SelectionWeight w LootBagPoolEntryData i ważony wybór w LootTableSystem; brakująca/zerowa/ujemna/niefinitywna waga oznacza1, stare pule zachowują wybór uniform i liczbę drawów RNG. Suma wag w double. Nowe profile stosują jednostkowe losowania i jedną część ułamkową kategorii; małe średnie nie są zaokrąglane do gwarantowanej1 sztuki. Bonus dropu nie jest dodawany niezależnie do każdego nowego gatunku. Nowe pule dopuszczają powtórzenia zwiększające stos, stare DistinctSelectionGroup nadal wykluczają powtórzone wpisy. Authoring kompletny lasu/katakumb kończy konfigurację tym samym samodzielnym profilem C#; nie wymaga lokalnych CSV.

144 pary pełnych clearów na identycznych seedach241026..241037 ×3 długości ×2 biomy ×L10/L20: identyczne piętra/pokoje/walki/wrogowie/skrzynie. Zaobserwowany budżet Normal na normalnej wyprawie: las L10 2,56, katakumby L10 2,35, las L20 2,68, katakumby L20 2,46. Wszystkie w celu2–3. To ekwiwalent wartości materiałów do przeciętnej broni/pancerza Normal, nie gwarancja integer craftów konkretnych receptur; początkowy zapas, dokupywanie, opłaty złota, bezpośrednie materiały poza podstawowymi torbami i gotowy gear poza metryką. Biżuteria współdzieli metale, nie jest darmowym dodatkiem do ekwiwalentu.

86 odrębnych przypadków ma końcowy PASS; końcowa regresja77 PASS/0 FAIL, explicit baseline/authoring/audyty osobno. 100000 losowań toreb obejmuje każdy materiał i bonus0/10%; niezależne testy rodzin/ilości, granice/legacy/duże wagi, distinct, zapis/wczytanie zachowanych łupów, alchemię, jedzenie i source authoring. Wczesne błędy dwóch helperów testowych naprawione i wszystkie przypadki ponownie PASS; historyczne XML zachowane. Spośród929 sprawdzonych assetów zmienione wyłącznie dwie torby; pełny Editor JSON potwierdza zachowanie dwóch nie-rzemieślniczych pul na każdy biom/przedział. Szanse całych toreb, alchemia, jedzenie, gotowy gear, ceny i NPC nie były zmieniane. Wyniki losowe pojedynczego seeda mogą się różnić przez zmienioną sekwencję losowania.

Kompilacja/import, scoped whitespace i self-review PASS. Brak nowej pracy per-frame i zmian formatu save; tylko generowanie zawartości. Manualny gameplay/FPS NOT RUN. Bez ręcznego setupu, agentów, Computer Use, commit/push. Raport: ../LocalHandoffs/crafting-material-drop-20261025/report.html; comparison.csv/json, validation.json, source.diff/asset.diff i scope.json. Smoke: rozpocząć NOWĄ wyprawę, otworzyć torby obu biomów i sprawdzić gatunki/ilości; stara mapa może mieć wcześniej zachowane nagrody. Ta sekcja zastępuje stan NOT_IMPLEMENTED wcześniejszych analiz.

## Ilość materiałów pod 2–3 przedmioty na normalną wyprawę — 2026-10-25 — RECALCULATED, NOT_IMPLEMENTED

Użytkownik ustalił cel2–3 zamiast zachowania obecnej wartości materiałów. Środek celu2,5 na tej samej miarze kosztowej co poprzednia tabela: ekwiwalent materiałów przeciętnej broni/pancerza Normal, nie gwarantowana liczba integer craftów. Na próbie144 wcześniejszych pełnych wypraw (12 seedów na biom/poziom/długość) mnożniki względem poprzedniej propozycji: próg5 las0,777678 (−22,23%), katakumby0,456628 (−54,34%); próg15 las0,478574 (−52,14%), katakumby0,278843 (−72,12%). Każdy profil normalnej wyprawy daje w tej próbie średnią2,5. Krótkie:1,13–1,58; długie:3,59–4,50. Jakości Poor/Excellent mają inne koszty, więc nie obowiązuje dla nich ta sama liczba przedmiotów.

Zachowane wszystkie proporcje wewnątrz każdego profilu biomu/puli: rodziny55/32/3/10 i80/8/3/9 oraz poszczególne gatunki. Runiczna pula11–20 skaluje ilość tym samym mnożnikiem, zachowując85/15; bez nowych dropów runicznych1–10. Różne mnożniki biomów zmieniają ich udział w łącznej podaży przy stałej liczbie wizyt; nie oznacza to zmiany lokalnych wag gatunków. Nie mnożyć szans całej torby ani alchemii/jedzenia/gotowego wyposażenia. Nie zaokrąglać wszystkich małych ilości w górę do1 — zachować średnią szansą/losowym zaokrągleniem.

Przeliczone ilości każdego surowca na torbę, runiczne ilości i każda długość/jakość: ../LocalHandoffs/material-drop-volume-20261025/report.html i plikiCSV. 14 kontroli rachunku/proporcji PASS. To nie pełna symulacja przyszłego samplera i wykonywania konkretnych receptur; złoto, wcześniejszy zapas, dokupywanie, bezpośrednie materiały poza podstawowymi torbami i gotowy sprzęt poza modelem. Biżuteria współdzieli metale i nie jest darmowym dodatkiem do ekwiwalentu broni/pancerzy. Bez zmiany assetów, gameplayu, sklepów, commit/push. Poprzednia propozycja zachowania wartości zostaje zastąpiona tym niższym budżetem; proporcje pozostają podstawą. Użytkownik poprosił o przeliczenie, nie wdrożenie.

## Materiały rzemieślnicze — analiza wszystkich receptur i propozycja — 2026-10-25 — ANALYSIS_COMPLETE, DROP_NOT_CHANGED

Zgodnie z prośbą użytkownika wykonano analizę i propozycję bez wdrażania dropu. Snapshot przez odczytowy test w Unity: 202 materiały, 114 receptur (90 Smithing, 13 Runic, 11 Alchemy), 195 aktywnych niestartowych części, wszystkie 352 kombinacje biżuterii i 13 toreb. Uwzględniono OutputAmount, pełne partie i współdzielone półprodukty, alternatywne receptury, wszystkie warianty części i jakości Poor/Normal/Excellent. 8192 pełne zestawy na scenariusz ×2 progi ×3 jakości =49 152 zestawy; to neutralny równy wybór wariantów, nie telemetria preferencji ani symulacja wypraw. Osobno koszty biżuterii i galdurytów. Zwykły upgrade kosztuje złoto, bez dopisywania fikcyjnego popytu na materiały.

Faktyczny katalog zawiera tylko progi części 5/15 i metali biżuterii 10/20. Enumy 25/35/45 i poziomy biżuterii 30/40/50 nie mają odpowiednich nowych części/metali. Galduryty: receptury granitu/marmuru są, ale aktywny EnchanterRole mapuje tylko wapień (11). Bez projektowania fikcyjnego popytu dalszych progów. Przy samych 8 sztukach gearu Normal: próg5 około49,2% metali/36,9% skór/2,6% drewna/11,4% włókien; próg15 około76,6/10,0/2,6/10,8. Wynik różni się od wcześniejszego przykładu jednej skóry kozy, bo uwzględnia wszystkie gatunki i uzyski.

Propozycja podstawowej puli kowalskiej, udział oczekiwanej liczby SUROWYCH sztuk (nie szanse wyboru nazwy): 55% metale/32% skóry/3% drewno/10% włókna dla 5, a 80/8/3/9 dla15; rezerwa na16 sztuk biżuterii odpowiedniego progu w modelu pełnego odnowienia czterech bohaterów. Konkretne procenty i średnie ilości KAŻDEGO surowca/biomu w proposed-material-shares.csv. Zróżnicowanie biomów zachowane; wspólny cel odtwarzany przez jedną torbę każdego biomu. Oczekiwany bazowy budżet wartości każdej puli/biomu zachowany, liczba surowców może wzrosnąć. Nie oznacza to gwarancji zwiększenia każdego rodzaju rudy w każdym biomie. Runiczna pula11–20:85% klejnotów/15% litego wapienia; rozpisane kolory i budżet. Bez nowej gwarantowanej puli runicznej1–10 ani zmian alchemii. Pyły energetyczne czerwony/żółty nie mają żadnego konsumenta recepturowego w aktualnym obiegu; rekomendowane wyłączenie z puli craftingu do nadania zastosowania, ich budżet nie został dopisany do nowych materiałów.

Pełny raport: ../LocalHandoffs/material-demand-20261025/report.html. CSV: wszystkie materiały/receptury/części/kombinacje biżuterii, popyt jakości, obecne torby, propozycja per surowiec/per biom i budżety. Eksport Unity PASS; 23 kontrole rachunku/inwariantów PASS. Brakujące odwołanie tylko w starej testowej torbie oznaczone osobno. Bez zapisu assetów/zmian gameplayu, sklepu, commit/push, agentów i Computer Use. Analiza oferty sklepowej oraz całych wypraw NOT RUN. Propozycja pozostaje do decyzji; nie oznaczać jako wdrożonego balansu.

## Furia: agresja chroni, obrażenia generują — 2026-10-25 — READY_FOR_SMOKE

Każdy zatwierdzony skill ofensywny resetuje licznik bezczynności i zapobiega utracie furii. Bazowe +10 generują tylko skille z obrażeniami kierowanymi na przeciwnika: bezpośrednimi albo DoT. Pudło/unik/absorpcja nadal nie odbierają generacji; raz za użycie niezależnie od liczby trafień/celów, bez kolejnego zysku z ticków i opóźnionych trafień. Efekt z obrażeniami na sobie/sojuszniku nie kwalifikuje osobnego osłabienia przeciwnika. Dotychczasowy wyjątek Wyładowania bez bazowej generacji pozostaje, zwrot za zabicie zachowany. Mana/impet, koszty, talenty, grace/CC/exploration decay zachowane.

Przegląd wszystkich produkcyjnych skilli z furią: bazowe +10 usunięte z Kopnięcia, Ogłuszającego Okrzyku, Rozkazu Unicestwienia i Zwodniczego Skoku ghula. Młot Zniszczenia zachowuje generację, bo obrażenia opóźnione realizuje istniejący owner bossa. Kopnięcie→Cięcie bez talentów: −20+0+10 = netto −10 furii. Bez sztucznej odporności: kolizyjne ogłuszenie nadal można ponawiać. Bonusy talentów mogą nadal poprawiać bilans rozwiniętych buildów; nie wprowadzono gwarancji ujemnego bilansu dla każdego buildu.

291 odrębnych końcowych PASS (290 w końcowej regresji i osobny celowany authoring słowniczka), zero nierozwiązanych FAIL. Sprawdzone wykonanie prawdziwych skilli, obie strony, kontrola/DoT/pudła/blokady/limity/opóźnienia, boss, Wyładowanie, stun/DoT i słowniczek. Zaktualizowany fallback i tylko opis furii w istniejącym assetcie przez Editor API; bez zmian assetów skilli/scen/prefabów. Kompilacja/import, scoped whitespace i self-review PASS. Bez nowego stanu runtime, schematu save i pracy per-frame. Manualny gameplay/FPS NOT RUN. Bez agentów, Computer Use, ręcznego setupu, commit/push. Dowody: ../LocalHandoffs/rage-damage-generation-20261025/README.md, classification.csv, validation.json, source.diff i test XML. Smoke: bilans Kopnięcie→Cięcie, tura samej kontroli i ponowne ogłuszenie kolizją.

## Balans DoT i Zatruty Sztylet 40 PP — 2026-10-25 — READY_FOR_SMOKE

Zmienione współczynniki na turę: Ognista Strzała 90→30% ×2, Kula Ognia 80→25% ×3, Zatruty Sztylet 90→40% ×3, Rozcinający Cios 90→40% ×4; NPC: przeszywający bełt 90→40% ×2, podpalona strzała 50→25% ×3, cięcie krzyżowe 85→30% ×3 na każdy z dwóch proców, toksyczne wymioty 75→50% ×3. Początkowe obrażenia, szanse, czasy, PA, trafienie, zasięgi i dodatkowe efekty zachowane. Zatruty Sztylet kosztuje 40 PP zamiast 25; koszt sztyletu na dystansie i brak kosztu sztyletu w zwarciu zachowane. Rank 0 Viruses nie blokuje już całej umiejętności przez opcjonalny zerowy debuff odporności; wyjątek ograniczony do tej umiejętności i odporności na truciznę. Ranki Venom/Viruses sprawdzone, inne niepoprawne zerowe efekty nadal odrzucane.

8 assetów skilli + 2 opisy w drzewkach zapisane celowanym Editor API, pełne źródła authoringu uaktualnione. Pełny JSON config przed/po: wyłącznie uzgodniony współczynnik/opis i koszt Sztyletu. Audyt 1536 rzeczywistych wykonań przed i tyle samo po na tych samych 64 seedach ×8 skilli ×3 odporności. Uwzględnia początkowe obrażenia, szanse, wszystkie ticki, dwa proce Cięcia Krzyżowego, zapłacone PA/surowiec oraz odzysk surowca. W 24 parach niezmienione obrażenia początkowe, częstość proców, PA i noże; surowiec zmienia się wyłącznie dla Sztyletu. To kontrolowany audyt jednego celu, nie pełny test balansu encounterów/AoE/buildów. Baseline Sztyletu wykonany po izolowanej naprawie ranku 0, przed balansem.

264 odrębne końcowe PASS, brak nierozwiązanych FAIL; osobny explicit authoring przeszedł, dlatego skip tego samego explicit w głównym przebiegu jest oczekiwany. Poprawiono przy okazji ujawnione stare rozbieżności: indeksy authoringu Miażdżącego Ciosu zgodne z istniejącą kolejnością debuff→stun i asercję lootów uwzględniającą już dodany Prowiant, bez zmiany tych assetów/gameplayu. Kompilacja, import, kontrola scope/whitespace i self-review PASS. Manualny gameplay/FPS NOT RUN. Bez zmiany schematu save; trwające DoT zachowują zapisany payload, nowe nałożenia używają nowych wartości. Bez ręcznego setupu, agentów, Computer Use, commit/push. Raport: ../LocalHandoffs/dot-balance-20261024/README.md, comparison.csv/json, validation.json. Smoke: Sztylet przy 40 i 39 PP, z/bez Viruses, zwarcie/dystans oraz pełny czas DoT. Dalsze statystyki mobów, materiały i pętla furii pozostają osobno.

## Krótsze uzgodnione ogłuszenia — 2026-10-24 — READY_FOR_SMOKE

Wdrożone: Prawy Sierpowy i Miażdżący Cios ogłuszają na 2 tury zamiast 3; Okrzyk na 1 turę przy wydaniu <50 furii i 2 tury przy ≥50 zamiast 3. Próg korzysta z faktycznie zapłaconej furii. Kolizja z przeszkodą lub ścianą ogłusza na 1 turę niezależnie od niewykorzystanego dystansu, zarówno dla zwykłego Push, jak i łańcucha pchania Charge; nadal tylko jednostka na czole uderzająca w barierę. Wspólna stała CollisionStunDuration=1, bez globalnego clampu innych stunów.

Szanse/obrażenia/koszty/ruch i kolejność efektów zachowane. Miażdżący Cios nadal najpierw obniża odporność na kontrolę o 30% na 3 tury, potem próbuje stuna. Podgląd szansy z debuffem nadal zgodny. Pozostałe ogłuszenia, unieruchomienia, freeze, bomby/pasywki i reguły furii bez zmian. Cztery produkcyjne assety DoT (Fireball/FireArrow/PoisonedDagger/BleedingDestruction) identyczne SHA256 przed/po. DoT pozostają następnym tematem do uzgodnienia.

Trzy assety skilli zapisane celowanym Editor API; aktualne opisy, specjalny tooltip Okrzyku i źródła authoringu (w tym starszy focused setup) zgodne. Unity dopisało bieżące pola domyślne schematu przy reserializacji dwóch starszych assetów. 126 różnych końcowych PASS: 15 nowych przypadków wykonania i kolizji + explicit authoring oraz regresje warrior/paladin/destructible/status/preview/October24. Kompilacja, kontrola źródeł i self-review PASS; manualny gameplay/FPS NOT RUN. Już odliczane statusy w starym zapisie nie są przepisywane; nowe wartości dotyczą nowych nałożeń. Bez ręcznego setupu, agentów, Computer Use, commit/push. Dowody: ../LocalHandoffs/stun-duration-20261024/README.md, validation.json i dot-asset-hashes-before/after.json.

## Jedzenie i mocniejszy obóz — 2026-10-24 — READY_FOR_SMOKE

Dodany Prowiant: jedna zużywana porcja leczy 10% maks. HP właściciela poza walką, z zaokrągleniem w górę i clampem do maks. HP. Przy pełnym HP, martwej postaci lub cudzej porcji brak użycia/zużycia; bez wskrzeszania i odnawiania zasobów. PPM „Zjedz”, zwięzły tooltip, własna ikona importowana do 32×32 (Point, alpha), stosowanie porcji zgodne z obecnym inventory. Jedzenie wykluczone z wyboru przedmiotów bojowych, wykonania akcji bojowej, alchemii i refill. Food dopisany na końcu enumów bez zmiany istniejących wartości. Zapis/wczytanie porcji w inventory/schowku sprawdzony, bez zmiany formatu save.

Dopisana niezależna pula: 35% na jedną porcję z przeciwnika, 50% na jedną ze skrzyni, w 8 produkcyjnych tabelach lasu/katakumb dla obu bracketów. Pozostałe wpisy/szanse zachowane; pełne źródła authoringu również dopisują jedzenie. Zapisane/prerolled nagrody pozostają nietknięte. Porównane te same 32 pełne generacje (4 seedy ×oba biomy ×3 długości na L10, Normal na L20): identyczne liczby pięter/pokoi/walk/wrogów/skrzyni. 0→322 porcji z 343 walk (0,94/walkę; las 0,82, katakumby 1,05), 0→46 porcji z 88 skrzyń (0,52/skrzynię). Bez bonusu dropu; istniejący bonus nadal działa. To średnia losowa, nie gwarantowana porcja na encounter.

Obóz odnawia teraz 60% maks. HP i many zamiast 40%; furia/impet i liczba zestawów bez zmian. Opis istniejącego zestawu oraz źródło authoringu uaktualnione. 159 odrębnych końcowych PASS, brak nierozwiązanych FAIL; celowany Editor API wykonany, imported icon obejrzany, kompilacja/kontrola źródeł/self-review PASS. Dwie nieaktualne asercje wcześniejszej regresji bomb/ikony Straż! poprawiono do istniejących zasad/assetu, bez zmiany ich gameplayu. Manualny gameplay/FPS NOT RUN. Brak ręcznego setupu, agentów, Computer Use, commit/push. Raport i pomiar: ../LocalHandoffs/dungeon-food-recovery-20261024/README.md, comparison.csv/json, validation.json. Ta sekcja zamyka uzgodniony punkt jedzenia i buffu obozu; szerszy balans CC/DoT, furii i materiałów pozostaje osobno.

## Baner użycia umiejętności — 2026-10-24 — READY_FOR_SMOKE

Zmieniony istniejący SkillCastBanner: 480×64 pod kolejką tur (szerokość ograniczona dostępnym miejscem), ciemna powierzchnia surface_solid, cienka ramka tooltip_frame, czcionka i kolory obecnego HUD. Ikona użytej umiejętności z CombatSkillPresentation.Skill oraz sama nazwa; nazwy bez prezentacji/ikony mają tekst bez pustego slotu. Czas 1,4 s z wygaszeniem przez ostatnie 0,25 s. Nowy cast podmienia treść i restartuje czas/alfa; koniec sesji, zmiana busa i disable czyszczą treść, ikonę i subskrypcję. Zachowane filtrowanie niewidocznych/obcych jednostek, synchroniczny pierwszy cast po ujawnieniu oraz rezerwa ActionSafeViewport. Baner nie blokuje kliknięć. Bez coroutine, nowych listenerów ani alokacji per-frame; alfa zmieniana tylko gdy faktycznie się zmienia.

Celowany zapis jednego prefabu przez Editor API i nowe pole referencji; bez ręcznego setupu ani zmian scen. 15 różnych przypadków końcowo PASS: mechanika prezentacji, binding/unbinding, legacy tekst, ukryte jednostki, viewport i pierwszy ujawniający cast (3 przypadki). Natywny render trzech przykładów obejrzany: ../LocalHandoffs/skill-cast-banner-20261024/banner-examples.png. Kompilacja, kontrola źródeł i self-review PASS. Manualny gameplay/FPS NOT RUN. Bez agentów, Computer Use, commit/push. Raport: ../LocalHandoffs/skill-cast-banner-20261024/README.md i validation.json. Ta sekcja zastępuje wcześniejsze odłożenie wyglądu nazwy skilla; pozostałe tematy do uzgodnienia pozostają odłożone.

## Wycieńczające Zaklęcia — kumulowany debuff — 2026-10-24 — READY_FOR_SMOKE

Wdrożone i wczytane: −5 szybkości przeciwnika na kumulację, do 3 (−15), 3 własne tury odbiorcy. Udane nałożenie odświeża cały efekt także przy limicie. Zachowana bazowa szansa 70% i istniejąca formuła odporności na osłabienie. Jeden rzut na cel z użycia skilla, także gdy pierwszy rzut zawiedzie; wielokrotne/opóźnione trafienia tego samego użycia nie powtarzają próby. Każdy cel AoE ma własny rzut. DoT, kopie i reakcje pasywek oraz dodatkowe obrażenia nie dodają stacków. Zachowana blokada odzysku many podczas toggle, brak bonusu szybkości maga. Wyłączenie skilla nie usuwa istniejącego debuffu przeciwnika. Jak wcześniej wymagane są obrażenia HP: całkowita absorpcja przez tarczę nie daje próby.

Jeden wspólny efekt `mage.tiring`, pełny modyfikator −5×liczba stacków i czytelna liczba kumulacji; nazwa/ikona oraz zapis/wczytanie zachowane. Stary efekt −3 aktualizuje się do nowych wartości przy kolejnym udanym nałożeniu lub sam wygasa. Śledzenie prób jest lokalne dla walki, leniwe i czyszczone po ostatnim efekcie użycia, bez pracy per-frame i zmian schematu save. Zaktualizowane oba źródła opisu i celowany asset przez Editor API; reserializacja dopisała aktualne wartości domyślne schematu.

165 różnych przypadków ma końcowy PASS (16 nowych przypadków mechaniki + explicit authoring + regresje statusów, obrażeń, prezentacji, save/restore i VFX). Import/kompilacja, kontrola źródeł i self-review PASS; reserializowany YAML ma standardowe końcowe spacje przy pustych stringach Unity; manualny gameplay/FPS NOT RUN. Brak ręcznego setupu, Computer Use, agentów, commit/push. Zakres i dowody: ../LocalHandoffs/mage-tiring-stacks-20261024/README.md oraz validation.json. Ta sekcja zastępuje wcześniejsze odłożenie buffu Wycieńczających; pozostałe nierozstrzygnięte tematy pozostają odłożone.

## Rzadkie składniki alchemiczne — 2026-10-24 — READY_FOR_SMOKE

Wdrożone/wczytane: jedna pula 20% na 1 składnik w obu torbach zasobów (także rozwijanych w skrzyniach); bezpośrednio ze szkieletu 5% na 1 kość, z nieumarłego 10% na 1 mózg. Alchemik zachowuje 3 typy ziół i 3 typy części, po 1 sztuce: 6 zamiast 27. Stary normalny zapas sklepu przycinany do nowych limitów podczas refresh/restore, bez refill na load. Buyback, zapasy gracza i zapisane zawartości łupów nietknięte. Inne pule, media, katalizatory i ceny bez zmiany konfiguracji. Pięć assetów zapisane celowanym Editor API, źródła authoringu spójne.

Powtórzone te same 144 generacje (12 seedów ×3 długości ×2 biomy × poziomy 10/20), identyczne liczby pięter/pokoi/walk/przeciwników w każdej parze. Katakumby L10 składniki krótka/normalna/długa: 48,25→4,00 /89,00→6,17 /130,00→9,42; kości 5,83→1,33 /8,17→2,58 /18,17→3,33. Normalne L20 90,17→6,75 składników i 11,83→2,33 kości. Las L10 35,33→0,50 /58,83→2,00 /94,17→3,00 używalnych składników; starsze ucho z leśnej puli nie jest skonfigurowanym składnikiem craftingu, referencję pozostawiono. Cel normalnych katakumb 5–8 składników i 1–3 kości osiągnięty średnio, bez twardego capu na seed. Bonus dropu w pomiarze zero.

118 różnych przypadków ma końcowy PASS; import/kompilacja, scoped diff-check i self-review PASS. Stary sklep, brak refill na load, buyback i zachowanie zapasów/torby gracza sprawdzone snapshot/restore. Gameplay/FPS NOT RUN, brak ręcznego setupu, Computer Use, agentów, commit/push. Nowe wartości dotyczą nowych generacji nagród; zapisane łupy nie są rerollowane. Raport i porównanie: ../LocalHandoffs/alchemy-scarcity-20261024/README.md (comparison.csv/json, before-results, after-results, validation.json). Ta sekcja zastępuje wcześniejsze odłożenie balansu składników i ilości u alchemika; materiały rzemieślnicze nadal osobnym tematem.

## Władimir — Wyładowanie i zamiana kitu — 2026-10-24 — READY_FOR_SMOKE

Wdrożone: Rozcinający Cios zamiast startowego Bandaża; Wyładowanie w Awanturniku: fizyczny cios 100% +1% za wydany punkt furii do 100 (mnożnik maks. 200%), minimum 20, zużycie do 100 z pozostawieniem nadwyżki, koszt 60% maks. AP jak Cięcie, zasięg 1 i celność 82. Brak generowania furii przez sam atak i brak redukcji tego kosztu. Żądza Zwycięstwa zwraca po bezpośrednim zabiciu 25%/50% wydatku (w dół); Przełamanie ignoruje 20%/40% obrony fizycznej, przy wydaniu co najmniej 50 furii podwójnie: 40%/80%. Atak/kryt i próg liczone przed zapłatą/zwrotem, podgląd i AI zgodne. Stare dwie pasywki nie wzmacniają już krwawienia. Pozostały balans CC/DoT i pętli furii nadal osobnym tematem.

ID węzłów i punkty zachowane; restore starego kitu migruje Bandaż i dawny grant/pasek bez utraty luk lub mutowania źródłowego snapshotu. Nowe ID skilla w katalogu, asset i trzy zmienione węzły zapisane Editor API, bez uruchamiania pełnego authoringu. 418 różnych przypadków ma końcowy PASS; 24 nowe przypadki mechaniki PASS, explicit setup osobno PASS. Import/kompilacja, scoped diff-check i końcowy self-review PASS; gameplay/FPS NOT RUN. Bez ręcznego setupu, Computer Use, agentów, commit/push. Zakres, raporty i smoke: ../LocalHandoffs/warrior-discharge-20261024/README.md. Ta sekcja zastępuje wcześniejsze odłożenie zamiany kitu.

## Poprawki walki i leczenia — 2026-10-24 — READY_FOR_SMOKE

Wczytane: Miażdżący Cios nakłada debuff przed stunem, a podgląd uwzględnia jego szansę (bez mutacji/RNG i dublowania zapisanego debuffu); 1 zwykły składnik alchemiczny napełnia miksturę do pełna; Wycieńczające Zaklęcia mają nazwę i ikonę także dla legacy źródeł; Święta Transfuzja leczy za 200% faktycznych obrażeń; leczenia siłą leczenia mają rozrzut (Uzdrowienie 100–140%, średnia zachowana) i podgląd zakresu krytycznego/szansy; pusta skrzynia zamyka się po zabraniu także złota; schody powrotne katakumb mają osobny sprite w górę; linka nie pozwala wybierać przeciwnika bez kosztu impetu, zachowując dostępny tańszy odzysk sztyletu; efektywny atak 23 produkcyjnych profili wrogów −15%, HP i wzrost bez zmian. Powtórzony native test dołączania przez niski murek: 4/4 sojuszników w obu biomach.

415 odrębnych końcowych przypadków PASS (ostatnia seria 412 PASS, 0 FAIL, 1 Explicit setup pominięty, wykonany osobno PASS). Obejrzane natywne rendery obu schodów z oświetleniem; kompilacja, scoped diff-check i self-review PASS. Manualny gameplay/FPS NOT RUN. Brak ręcznego setupu, Computer Use, agentów, commit/push; prefab i wartości definicji zapisane Editor API. Dowody/pełny zakres i smoke: ../LocalHandoffs/combat-balance-20261024/README.md; deduplikacja: validation.json. CSV rzeczywistych drugorzędnych statystyk wrogów na poziomach 1/10/25/48/50 przygotowane do dalszej oceny, bez zmiany tych statystyk.

Zgodnie z poleceniem „nie implementuj spraw do rozpatrzenia/uszczegółowienia” odłożone: pełna zamiana kitu wojownika (brak kosztu/formuły nowego skilla), lootbag rarity i podaż materiałów, +5/3 stacki Wycieńczających (obecnie to −3 szybkości przeciwnika), zmiana wyglądu nazwy po skillu, jedzenie/nowa regeneracja HP, ogólny balans CC/DoT i pętli furii, balans drugorzędnych statystyk per lore. Bez zmian tych tematów, bez nowego blokującego pytania.

## Paczka poprawek lochów, walki i łupu — 2026-10-24 — READY_FOR_SMOKE

Wczytane: wspólne LOS/widzenie/odkrycie z limitem 20 kratek (zastępuje wcześniejsze 5/bez limitu); trudny teren wyłączony w generacji i restore, niskie przeszkody pozostają; deduplikacja ofert „oczyść” per biom; podgląd zasięgu na hover i przewidywane leczenie; Zbierz wszystko [B] w prawdziwym prefabie, auto-close pustego ciała i bordowe/szare krzyżyki; żywopłot x2; spójne nowe ikony złota/XP/honoru; patrol 2 rzezimieszków + 1 strzelec jako wersjonowana definicja zgodna ze starymi zapisami; XP faktycznie na zabicie (także od pułapki poza walką), pozostaje po ucieczce, bez powtórzenia przy zwycięstwie/scaleniu; stun po push w ścianę/przeszkodę; pauza podczas walki z zatrzymaniem czasu, AI/deadline i blokadą zapisu. Widoczni pobliscy sojusznicy są włączani atomowo przy zauważonym starciu przez low edge, z zachowaniem pełnych ścian, legalnej pozycji i obserwacji 360° w walce.

Ucho elfa: faktyczny drop lasu item.material.elf_ear miał pustą ikonę; wcześniejsza kontrola poprawnego item.vs10.ucho_elfa dotyczyła innej definicji. Editor API podpiął 12 brakujących ikon materiałów. Audyt 258 produkcyjnych definicji: 0 braków; testowe definicje/puste legacy sloty wyłączone. Rodziny jakości i kamienie współdzielą istniejące bazowe ikony. Render prawdziwego okna pokazuje oba ucha, także przedmiot z odtworzonej wyłącznie w pamięci kampanii 7E8AB8.

652 różnych przypadków ma końcowy wynik PASS w celowanych seriach; bez pozostałych failures. Native oba biomy: 4/4 bohaterów przez murek przed kolejnym tickiem, XP callback przed końcem walki; pause/save/restore/AI i oba rastry map sprawdzone. Import/kompilacja i C# whitespace PASS; prefab zapisany przez Unity zachowuje dwa odstępy w pustych polach YAML, bez ręcznej edycji. Obejrzane rendery łupu i niskich przeszkód. FPS/manual gameplay/hotkey B smoke NOT RUN. Brak ręcznego setupu, drugiego edytora, Computer Use, agentów i commit/push. Ta sekcja rozstrzyga wcześniejszy BLOCKED_USER dotyczący XP oraz zastępuje dawne zasady trudnego terenu. Pełny zakres, smoke, grafiki, prompty builtin imagegen i audyt: ../LocalHandoffs/dungeon-polish-20261024/README.md; deduplikacja wyników: validation.json.

## Stabilność podłoża lasu — seed 959042448 — 2026-10-24 — READY_FOR_SMOKE

Reprodukcja w końcowej GameView przy nieruchomej kamerze: alfa filtru i wiedza stałe, różnice na dekoracyjnej ziemi poza przechodnią mapą.781 różnie zabarwionych płatów współdzieliło jeden sortingOrder. Każdy ma teraz osobny priorytet poniżej podłogi; ciągłe tło pozostaje pod wszystkimi płatami. Rezerwacja nowej głębokości zachowuje kolejność. Bez zmian RGB/alfa, oświetlenia, wiedzy/LOS, sprite'ów, geometrii/generacji/save/assetów, nowych rendererów lub zegara aktualizacji. Short377/Normal781/Long1341 priorytetów unikalnych.30 aktualnych celowanych testówPASS. Dwie stare fixture'y dostosowano do produkcyjnych pokoi/przegród i inicjalizacji świateł poFogFloor, zachowując silne asercje. Import/scoped self-review zakończone. Końcowa GameView:200 klatek, trzy poziomy zoomu;179 porównań stałego kadru i607760–687879 pikseli dekoracyjnego podłoża, zero zmienionych kanałów RGB. Kadry sprzed/po mają inny zoom/rozdzielczość, więc oceniono stabilność w czasie, nie bezpośrednią parity. Klatka150 obejrzana; las renderuje się prawidłowo. Dowody: ../LocalHandoffs/fog-seed-959042448-20261024/README.md. Brak Editor setupu, Computer Use, commit/push. FPS NOT RUN.

## Sztylety po Wymianie i szarżach — 2026-10-23 — READY_FOR_SMOKE; XP — BLOCKED_USER

Wczytane odzyskiwanie własnych sztyletów po zamianie miejsc, szarży (w tym po drodze i łańcuchu pchnięcia), teleportacji i przemieszczeniu wymuszonym. Wspólny CombatCellEntrySystem używa istniejącego ScoutKnifeSystem po obsłudze pułapek; martwy aktor nie podnosi. Zwykły ruch zachowuje poprzednią kolejność i feedback, limity/własność/+8 Momentum pozostają kanoniczne. 102/102 EditMode PASS, w tym 4 nowe regresje i rzeczywiste skille SD_ScoutSwap oraz SD_Charge. Kompilacja/import/self-review/scoped diff-check zakończone; bez ręcznego Editor setup/save/assetów/commit/push. Dowody: ../LocalHandoffs/knife-arrival-xp-20261023/README.md. Smoke: Wymiana na sztylet, szarża przez sztylet/na sztylet; zapas i +8 raz.

XP niezmienione: obecnie faktyczne przyznanie i popup na zwycięstwo. Oczekiwana odpowiedź, czy przenieść przyznanie na zabicie (zachować także po ucieczce), czy jedynie popup. Nie zaimplementowano zależnej zmiany przed decyzją.

## Rytm encounterów na grafie tras — 2026-10-23 — READY_FOR_SMOKE

Nowe wyprawy z planowanymi odnogami wybierają pokoje encounterów przez DungeonEncounterRoomSelector po dotychczasowym losowaniu budżetu/składów. Ocena używa rzeczywistych Connections: obu ramion pętli, dojść do odnóg i przejść przez skrzyżowania. Preferuje krótsze ciągi bez walki, pokrycie różnych części grafu i mniej sąsiadujących starć. Cel czterech pustych pokoi jest miękki. Finał jest zablokowany w selekcji; na ostatnim piętrze bossa liczy się jako już obsadzony, bez drugiej regularnej walki. Start to granica tempa, nie dodatkowy encounter. Końce odnóg nie mają nowej gwarancji walki. Dotychczasowa kwalifikacja szablonów, liczby/definicje walk, RNG składów, bramy, EXP/loot i geometria zachowane. Seedowane remisy korzystają z istniejącej potasowanej puli; nie zużywamy nowego combat RNG.

Owner: generation-only Systems/Dungeons/DungeonEncounterRoomSelector, wywołany trzema nowymi liniami DungeonEncounterPlacementSystem wyłącznie dla profili MaxBranchTransitRoomCount>0. Zachowany stary draw jako kandydat, greedy dobór i najwyżej cztery przebiegi poprawiających zamian; bez zmian runtime AI/patroli/merge/LOS. Odległości grafu przygotowane raz i najbliższe centra cache'owane podczas ocen. Brak zmian serialized assetów/scen/prefabów i schematu save. Pełne snapshoty zachowują każdą zapisaną lokalizację encountera; zwykłe ponowne Place nie przestawia istniejących walk.

50 par świeżych generacji / 76 pięter: osiem seedów × sześć wariantów oraz dwa dodatkowe seedy normalnych katakumb. Wszystkie pary mają identyczne pokoje, łączniki, pola, bramy oraz sekwencję definicji encounterów na każdym piętrze. Najdłuższa prosta trasa przez puste pokoje: średnio 8,42→3,72, maksimum 24→6; 75 pięter krócej, jedno bez zmiany, zero pogorszeń. Sąsiadujące pary encounterów 124→15. Bramy w tej metryce otwarte, Start wyłączony, bez patroli/łączenia walk i bez ponownego chodzenia po odwiedzonych pokojach. To miara grafu, nie czasu gameplayu.

38 odrębnych focused testów PASS: 32 w głównym zestawie, trzy dodatkowe (zamknięty cykl i dwa wyposażenia długich wypraw), dwa pełne save/restore i jeden jawny baseline. Dwa explicit helpery pominięte w głównym zestawie: baseline uruchomiony wcześniej, orphaned navigation cleanup niepotrzebny i NOT RUN. Oba schematy przed/po obejrzane. Import/kompilacja/self-review/whitespace PASS. Koszt generation-only selector dla 90 pokoi/17 walk: mediana 19,241 ms w EditMode (po cache; wcześniej 57,049); całe Place na 50 wyprawach mediana 21,148→22,976 ms, max 96,556→83,170 ms. Nie jest to benchmark FPS ani dowód braku kosztu. Gameplay smoke/FPS NOT RUN. Bez ręcznego setupu, drugiego Unity, Computer Use, agentów, commit/push. Dowody: ../LocalHandoffs/encounter-rhythm-20261023/README.md.

Smoke: nowe wyprawy Lasu i Katakumb, także bossowe; przejść oba ramiona pętli i dłuższą odnogę, sprawdzić mniej skupionych walk, wymagany finał oraz zapis/wczytanie. Zapisane mapy zachowują stary rozkład.

## Minimapka kamery i obrys viewportu — 2026-10-23 — READY_FOR_SMOKE

Minimapa śledzi środek aktualnej kamery prezentacji DungeonFloorMapView zamiast lidera, również w trybie F i podczas kadrowania walki. Ta sama kamera jest już jawnie przekazywana przez istniejące oświetlenie lochu; brak nowych referencji sceny lub wyszukiwań obiektów. Cztery rogi viewportu są rzutowane promieniami na płaszczyznę mapy i przeliczane tą samą bazą komórek co znaczniki. DungeonMinimapMarkers rysuje biały pusty obrys 1,5 jednostki UI w minimapie i mapie M, reagujący na pozycję, zoom oraz aspect. Każda krawędź jest przycinana osobno, bez sztucznych boków na granicy widżetu; znaczniki pozostają nad obrysem. Duża mapa nadal kadruje wyłącznie znany teren, niezależnie od kamery.

Projekcja aktualizuje się w Canvas.preWillRenderCanvases po LateUpdate kamer, a obsługa M/Esc zachowuje wcześniejszą kolejność. Subscribe/unsubscribe przy enable/disable/destroy; stałe bufory rogów i reuse istniejącej geometrii markerów. Sam zoom i sub-cell pan nie zmieniają rastra terenu, nie wyzwalają LOS ani odkrycia. 30/30 focused testów PASS (26 minimapy + 4 projekcji/cache markerów), w tym callback renderu, oddzielny ruch lidera/kamery, pan/zoom, pusta i biała geometria, clipping oraz brak dirty mesh dla stania. Obejrzano oba rendery produkcyjnego prefabu. 120 niezmienionych odświeżeń overview: 4,533 ms łącznie z reflection w EditMode; to nie pomiar FPS. Import/kompilacja i scoped self-review/whitespace PASS; manual gameplay/FPS NOT RUN. Bez serialized edits, ręcznego setupu, drugiego Unity, subagentów, commit/push. Raport: ../LocalHandoffs/map-camera-20261023/README.md.

Smoke: w F odsuń kamerę od drużyny, zmień zoom, otwórz M i porównaj biały obrys z rzeczywistym widokiem. Powrót do śledzenia lidera oraz wejście w walkę mają zachować śledzenie faktycznej kamery. Skala minimapy pozostaje dotychczasowa; fragment obrysu poza widżetem jest przycinany.

## Przekraczanie pionowego murka — 2026-10-23 — READY_FOR_SMOKE

Pionowy niski murek był sortowany według południowego końca całego segmentu, przez co nakrywa zasłaniała przechodzącą w poprzek postać. Widok aktora rezerwuje teraz istniejące pasmo przed przekraczanym low edge; uwzględnia też nakrywę jego bezpośredniej południowej kontynuacji. Korekta działa tylko podczas przekraczania: zwykła eksploracja, layered traversal, pożyczone widoki walki oraz CharacterView nowych jednostek/przywołań. Koniec ruchu, teleport, wyłączenie widoku i wyjście z combat czyszczą stan prezentacji. Koszt PA, czas przejścia 0,6 s, logika kolizji, geometria, fog, authoring i zapis bez zmian. Zapytania korzystają z lokalnych krawędzi i istniejących posortowanych pasm, bez skanowania wszystkich ścian.

33 focused testy PASS: 16 przypadków obu orientacji/kierunków i czterech ścieżek widoku, 2 statycznej głębi, 8 tactical terrain, 3 kosztów AP i 4 borrowed combat. GPU odtwarza poprzednie zasłanianie pionowej korony; porównania przed/po korzystają z tej samej pozy i natywnych sprite'ów. Testy sprawdzają także przywracanie głębi po zakończeniu/przerwaniu oraz odrzucenie teleportów/diagonali jako low edge. Import/kompilacja w istniejącym Unity zakończone; self-review i scoped diff check PASS. Gameplay smoke/FPS NOT RUN. Bez serialized assetów, ręcznego Editor setupu, commit/push. Dowody: ../LocalHandoffs/low-wall-crossing-20261023/README.md.

Smoke: w istniejącej mapie przejdź przez pionowy i poziomy murek w obie strony, także podczas walki. Nakrywa nie powinna przecinać postaci w trakcie przejścia; po odejściu obowiązuje normalna głębia.

## Funkcyjne układy niskich przeszkód i 4wayTomb — 2026-10-23 — READY_FOR_SMOKE

Do obu produkcyjnych katalogów katakumb (zwykłych oraz Skeleton Slayer) podpięto kopię DRT_Catacombs_4wayTomb, zachowując ID, rozmiar, granice, 20 murków i cztery sarkofagi. Oryginalny asset użytkownika pozostał identyczny bajtowo. Kopia: ProductionRooms/DRT_catacombs_4way_tomb.asset; RoomFunction=sarcophagus. Aktualny footprint sarkofagu jest światowym 1x2, dlatego kopia dopuszcza obrót 0; pozostałe ustawienia powodowały wyjście rekwizytu poza podłogę albo nakładanie na low edge. Manifest authoredRooms zachowuje integrację także przy pełnym authoringu; jawne zastosowanie authoringu odświeża kopię ze źródła, zwykły import jej nie nadpisuje.

Przeprojektowano 7 istniejących szablonów katakumb: dwie krypty L, cztery zatoki sarkofagów z nawą 2 pola, balustrady T przy filarach, otwarty dziedziniec rytuału U, galeria między podporami, strefa mechanizmu i dwie wnęki przy pieczęci. Przeprojektowano 4 leśne: osłona kieszeni skały/pnia, podmokłego zagajnika, lewego stanowiska zasadzki przy skale i wewnętrznego łuku przesieki. Każdy układ ma opis funkcji oraz sprawdzany skrót/obejście. Dwa północne sarkofagi w sali sarkofagów przesunięto o jedno pole. Uwzględniono pełne rezerwacje rekwizytów oraz tylko bezpieczne obroty: crypt_bend 0/90/270, sarcophagus i seal 0/180; pozostałe warianty zachowują cztery obroty. Nie zmieniono wejść, footprintów pokoi, kotew/liczby przeciwników, grafu tras, bram, trudnego terenu ani budżetów walk i łupów.

39 odrębnych focused testów PASS: 37 regularnych (produkcja trzech długości, bramy i save/restore, rotacje/meble, trasy z uwzględnieniem propów, rzeczywisty wybór pokoju przez generator) + 2 jawne audyty renderów. Pozostałe dwa jawne ogólne audyty poprzedniej karty NOT RUN. Obejrzano 7 natywnych renderów: 4wayTomb, crypt_bend, winch oraz wszystkie cztery leśne; reszta katakumb sprawdzona na planach i przez testy. Import/zapis przez otwarty Editor API, bez ręcznego Refresh, Computer Use, drugiego Unity lub subagentów. Self-review/scope PASS; gameplay smoke i FPS NOT RUN. 13 istniejących assetów zmienia wyłącznie low anchors, safe rotations, wskazane dwa rekwizyty oraz dwie listy katalogów; jeden nowy asset produkcyjny. Bez commit/push.

Nowe rozmieszczenie dotyczy nowych wypraw. Pełne snapshoty istniejących zapisów zachowują wcześniejszą geometrię i obiekty. Brak ręcznego setupu w Editorze. Raport i obrazki: ../LocalHandoffs/authored-low-barriers-20261023/README.md.

## Niskie przeszkody — orientacja i łączenia — 2026-10-23 — READY_FOR_SMOKE

Pionowy żywopłot obraca istniejący krzak o 90 stopni; jeden renderer na krawędź w obu kierunkach. Pozycja korzysta z środka sprite’a po obrocie, a pion ma grounded baseline na południowym końcu. Sąsiadujące odcinki spotykają się na wierzchołkach logicznych ścian, również w L/T/krzyżach.

Natywne murki mają domknięte końce capu, narożniki L, T i krzyże oraz osobne lico na odsłoniętym południowym końcu pionu. Lico pochodzi z tego samego wycinka North co poziom; pionowa nakrywa zachowuje pełne 32 px fazy Side, bez szczelin między komórkami. Topologia używa istniejącego indeksu WallsAtEndpoint/GetEdgeEndpoints, a capy tej samej funkcji CreateDedicatedCornerSprite co pełne ściany. Nowe maski ramion rozszerzają ją o końce/T/krzyże; dotychczasowe pełne ściany zachowują wcześniejszą ścieżkę. Jeden właściciel capu per wierzchołek, bez zaślepek na prostych kontynuacjach. Zostają ścisłe przycięcia do wysokich ścian, collidery, traversal, fog i materiały oświetlenia.

101 odrębnych focused testów PASS (99 regresyjnych + 2 jawne rendery produkcyjne; końcowy rerun 7 PASS obejmuje zmienioną głębię żywopłotu). Sprawdzono 15 masek wierzchołków, ciągłość pionowych nakryw, styk lica z capem, fog, UV, ruch/AP, wysokie ściany i natywne rendery obu biomów. Obejrzano macierze murków (native i z pochodniami), izolowane żywopłoty L/T oraz ujęcia prawdziwych map. Self-review PASS; gameplay smoke/FPS NOT RUN. Bez ręcznego Editor setupu, zmian assetów/scen/prefabów/PNG, commit/push. Raport: ../LocalHandoffs/low-wall-joins-20261023/README.md.

## Las i katakumby — trudny teren i niskie przeszkody — 2026-10-23 — READY_FOR_SMOKE

Przywrócono mechanikę do produkcyjnego authoringu, bez zmiany grafu tras, liczby walk lub kotew przeciwników. Las: 10 szablonów z 33 polami trudnego terenu, 4 szablony z 12 krawędziami niskich krzaków. Katakumby: 12 szablonów z 42 polami oraz 7 szablonów z 18 niskimi krawędziami. Małe płaty 3–4 pola i odcinki 2–3 krawędzie, z zachowaną drogą wokół każdego odcinka. Wejścia/wyjścia i areny pozostają bez nowych stref. Blueprinty jawnie zapisują współrzędne; pełny authoring je odtwarza, a osobny request zapisuje wyłącznie DifficultTerrainCells/StructuralAnchors przez Editor API. 22 assety terenowe; przy zapisie potwierdzono wyłączność tych dwóch pól.

Reguły bez zmian: trudny teren +1 PA przy wejściu i 50% prędkości eksploracji; niski murek/krzak +2 PA i przejście 0,6 s. Las używa istniejących natywnych LowShrub/variants, bez nowych bitmap. Krzaki nie są drzewami, nie wygaszają się jako korony i nie dostają blokujących colliderów; mają grounded depth, światło i wiedzę mgły per-edge. Jeden renderer na odcinek poziomy, dwa na pionowy, zamiast trzech nakładających się casterów.

Katakumby: UV crop obecnej tekstury North/Side usuwa dokładnie dolne 8 pikseli; bez edycji PNG lub zmiany roota/collidera. Child kompensuje skalę przed obrotem; zachowane przycięcie przy pełnych ścianach. Stary renderer z arkusza low-wall nie jest używany w produkcyjnej prezentacji. Trudny teren korzysta z obecnego kamienia, tintu i drobnego gruzu zamiast starego brązowego kafla testowego. Leśne błoto dostaje osobną statyczną maskę dokładnie według pól gameplayowych, bez skanowania co klatkę i bez podglądu hazardu na ściółce poza mapą. Maska ma jeden texel na pole i jawny release przy rebuild/teardown.

97 odrębnych focused testów PASS: ruch/AP/przejście przez murek, 18 generacji trzech długości po placement obiektów, authoring/obejścia, zapis snapshotów, dostęp do wyjścia, światło/mgła, UV/native bounds i macierz połączeń murków. Obejrzano oba biomy i obie orientacje murków; GPU potwierdził różnicę leśnej powierzchni trudnego terenu. Nowy test sprawdza ukrycie krzaków przed odkryciem oraz zniszczenie maski przy rebuild. Test starego arkusza junction dostosowano do pojedynczego cropu, zachowując ścisłe granice przy pełnych ścianach i collider roota. Fixture AuditMap dostał istniejący lowWallPrefab, już podpięty w scenie gry.

Self-review zakończony: lifecycle, fizyka, głębia, koszt renderowania, content i save compatibility sprawdzone. Stare snapshoty pozostają bez nowych hazardów; nowe rozmieszczenie wymaga nowej wyprawy. Brak ręcznego setupu, zmian scen/prefabów/ProjectSettings i PNG. Unity zaimportowane poza Play Mode; bez ręcznego Refresh/Computer Use/drugiego Unity/agentów, commit/push. Gameplay smoke i FPS NOT RUN. Raport: ../LocalHandoffs/biome-terrain-20261023/README.md.

## Las — wyjście na obrysie całej mapy — 2026-10-23 — READY_FOR_SMOKE

Błąd: wybór furtki sprawdzał tylko jedną kratkę poza pokojem. Brama mogła przeciąć wąską przegrodę między polanami i wyglądać jak ich połączenie. Wspólna reguła DungeonForestTransitionBoundary indeksuje najwyższe/najniższe pole każdej kolumny całej mapy. Całe trzykratkowe pasmo otworu musi leżeć na jej zewnętrznym horyzoncie; za progiem w kierunku wyjścia nie może być żadnej podłogi innego pokoju lub łącznika, niezależnie od odległości. Zachowany dostęp 3x2 bez aktywnych przeszkód, preferencja północ/południe i centrowanie w płaskiej krawędzi. Bez skanowania co klatkę: wybór tylko podczas generacji/budowy prezentacji.

Wszystkie trzy ścieżki embeddingu generatora sprawdzają tę samą regułę po realizacji mapy. Jeżeli Final (lub Start na dalszym piętrze) nie ma takiej krawędzi, istniejący retry rozmieszcza ponownie dokładnie ten sam zaplanowany graf. Bez zmiany liczby pokoi, pętli, odnóg, budżetu walk lub zasad bram. Katakumby bez zmiany kryteriów. Nowy seed może dać inny układ, gdy dotychczasowe rozmieszczenie było nieprawidłowe.

Stare pełne snapshoty nie są przebudowywane: widok najpierw szuka rzeczywistej zewnętrznej krawędzi także na nich. Tylko jeśli zapisany Final jest całkowicie osłonięty innymi pokojami po obu pionowych stronach, zachowuje historyczne działające wyjście, żeby nie zablokować zapisu. W takim historycznym układzie pełna gwarancja obrysu wymaga nowej wyprawy. Jawny test sprawdza brak przesuwania zapisanych pokoi/mapy. Nie twierdzimy, że ten fallback naprawia dawny zamknięty układ.

75 odrębnych focused EditMode PASS: 71 w integration-tests.xml oraz 3 nowe przypadki bossa i 1 nowy seed native w final-render-boss-tests.xml (7/7); końcowy native retest w final-native-tests.xml. 72 układy regularnego lasu (24 seedy x 3 długości) sprawdzone po placement obiektów; 6 wariantów bossa, dostęp do progu, scena natywna, światło/głębia, E/LPM/confirm i zgodność planu/pętli/bram w obu biomach. Przed poprawką odtworzono dwa FAIL na sąsiedniej i dalekiej polanie; dodatkowo odkryto seed9 Normal/Long bez żadnej zewnętrznej krawędzi, naprawiony przez walidację embeddingu. Rendery obejrzane; aktorzy w native audycie stoją po przechodniej stronie niezależnie od orientacji wyjścia.

Self-review: zachowany graph/RNG planu, brak nowej pracy per-frame, brak modyfikacji save schema, serialized state lub artu. Nowy .cs.meta wygenerowany przez Unity. Bez manualnego Refresh/Computer Use/drugiego Unity, commit/push. Manualny gameplay/FPS NOT RUN. Ręczny setup niepotrzebny. Smoke: ponowne wejście do sceny dla istniejącej mapy; pełna gwarancja zewnętrznego położenia po nowej generacji. Raport: ../LocalHandoffs/exit-exterior-edge-20261023/README.md.

## Las — weryfikacja odpowiedzi furtki na światło — 2026-10-23 — READY_FOR_SMOKE

Poprzedni test potwierdzał rejestrację renderera i shader, ale nie zmianę pikseli. Dodano kontrolowane porównanie GPU na rzeczywistych produkcyjnych rendererach furtki/przęseł: ten sam ambient 0.34, tekstury, tint i property blocks; bez lokalnych źródeł vs jedna pochodnia przy progu (radius5, intensity1, kolor produkcyjny). Kamera izoluje renderery na czerni, bez zmiany ich materiałów na shader diagnostyczny. Przywracane są wszystkie parametry materiałów, warstwy i kamera.

2/2 produkcyjne seedy PASS. Seed171026: średnia luminancja pikseli ogrodzenia 0.1930 → 0.3256, 1.69x; 17654/17684 pikseli jaśniejszych o >0.01. Reakcja na oświetlenie jest potwierdzona. Wygląd względnie jasnego drewna wynika z wartości źródłowej grafiki oraz białego tintu furtki (przęsła .82/.83/.78), nie z renderowania unlit. Ta karta nie zmienia tintu, artu ani runtime. Nie jest to pomiar FPS ani zapis ruchu prawdziwej pochodni w Play Mode; to kontrolowany GPU test właściwego shadera na receiverach pobranych z produkcyjnej sceny.

Oba obrazy ambient/torch obejrzane; self-review testu/restore zakończony. Brak ręcznego setupu. Dowody: ../LocalHandoffs/exit-light-response-20261023/README.md, response-*.txt, tests.xml oraz gate-*-ambient/torch.png. Bez serialized state, Computer Use, kolejnego Unity, commit/push.

## Las — głębia przęseł wyjściowych — 2026-10-23 — READY_FOR_SMOKE

Przęsła furtki zachowywały sortingOrder z chwili budowy. Późniejsza rejestracja koron/obiektów przebudowywała kompaktowe pasmo głębi, ale odświeżała tylko drzewa; przęsła mogły przykrywać bliższe korony. Na dwóch produkcyjnych seedach regresja przed poprawką FAIL: 14 zamiast -210 oraz -250 zamiast -1154. Teraz ConfigureWallDepthSorting odświeża także przęsła według ich rzeczywistego progu. Bez nowego Update/skanowania co klatkę, nowych warstw projektu, bitmap, zmian serialized state, gameplayu lub zapisu.

51/51 focused EditMode PASS (dwa produkcyjne audyty + WorldObjectPrimaryInteractionTests); dodatkowa końcowa kontrola renderów zapisana osobno. Kontrola obejmuje późno dodaną linię głębi, zgodność brama/przęsła, kolejność aktora przed/za progiem i zachowanie porządku po oświetleniu. Natywne rendery dwóch seedów obejrzane. Strona za progiem jest poza mapą i nie służy jako wizualny test aktora; jej relacja głębi sprawdzana liczbowo. Self-review: brak rekurencyjnej rejestracji baz w refreshu, istniejący cleanup zachowany. Ręczny setup niepotrzebny; smoke po ponownym wejściu do sceny: końce przęseł pod bliższymi drzewami i postacie przed ogrodzeniem. Bez Computer Use, drugiego Unity, commit/push. Raport: ../LocalHandoffs/exit-depth-layers-20261023/README.md.

## Las — furtka połączona z linią graniczną — 2026-10-23 — READY_FOR_SMOKE

Poprzednie wycięcie koron obejmowało całą furtkę z przęsłami i pozostawiało szeroką lukę. Teraz rezerwowany jest tylko otwór 1,6 x 2,4; brama jest centrowana względem rzeczywistego prostego fragmentu krawędzi, zamiast średniej X pokoju. Przęsła z natywnego wycinka istniejącej grafiki v2 biegną po zamkniętej krawędzi i kończą się przy pierwszych korzeniach. Na schodkowych końcach mogą wejść maks. 3 kratki w solidny las; nigdy na przechodnią ziemię poza właściwą krawędzią. To wizualna ciągłość istniejącej blokady granicznej, bez nowych colliderów lub zmian grafu/nav.

Odcinki mają per-edge wiedzę mgły, istniejące oświetlenie/sortowanie i jawny cleanup materiału/sprite'a przy rebuild/destroy. Nie są koronami ani dodatkową interakcją/outline'em. Dobór jest wykonywany podczas budowy sceny, bez nowego skanowania co klatkę. Brak zmian encounterów, zapisu, PNG, scen/prefabów i ScriptableObjects.

58/58 końcowych testów PASS: E/LPM/hover/confirm, dostęp do trzech długości, dwa natywne rendery i kontrola ciągłości przęseł, oświetlenie/cache pokrycia/mgła. Dwa rendery obejrzane przed/po. Szerszy zestaw 74/75: historyczny ProductionLowerRoomCanopiesHaveBeforeAndAfterRenders nadal oczekuje braku legalnych divider trees i wycofanych room.forest.long_glade/blind_glade; nie zmieniano ani nie wyłączano tego testu. Jego założenia są sprzed produkcyjnych szablonów i poprawki cienkich przegród. Ten historyczny test nie jest potwierdzeniem gotowości obecnego authoringu. Szczegóły: ../LocalHandoffs/exit-boundary-continuity-20261023/README.md.

Self-review zakończony; 924 ScriptableObjects bez zmian. Ręczny setup niepotrzebny. Smoke po ponownym wejściu do sceny: oba końce furtki połączone z lasem, nadal czytelny otwór i hover/LPM/E. Istniejąca mapa może być wznowiona. Bez Computer Use, drugiego Unity, commit/push; gameplay/full clear/FPS NOT RUN.

## Wyjście z lochu — granica lasu, interakcja i obowiązkowa walka — 2026-10-23 — READY_FOR_SMOKE

Furtka lasu zastępuje fragment drzew NA zewnętrznej krawędzi pokoju; jej miejsce i prześwit są rezerwowane przed generowaniem koron. Punkt podejścia/interakcji jest pół kratki wewnątrz, bez otwierania logicznego ruchu poza mapę. Nowa grafika v2, natywne 128x85/32 PPU, profil Single/Point/FullRect, neutralniejsze materiały i oczyszczona alfa. Wyjścia rejestrowane do oświetlenia po jego inicjalizacji; sortowanie pionowego sprite'a według faktycznego progu. Katakumby zachowują schody.

LPM obejmuje grafikę wyjścia i kolejkuje podejście; E z bliska zachowane, wspólny hover outline. Opuszczenie wyprawy otwiera istniejący ItemActionPopup z pytaniem „Opuścić loch?”, anulowaniem i ponowną walidacją po zatwierdzeniu. Zmiana referencji wyłącznie PF_DungeonExitView przez Editor API. Potencjalne stare TMP etykiety wyjścia są wyłączane przy każdej inicjalizacji; prefab nie ma debugowego napisu.

Nowe generacje z profilem długości rezerwują jeden encounter w każdym pokoju Final w OBECNYM budżecie walk, zamiast dodawać dodatkową walkę. Na ostatnim piętrze bossa rolę tę zachowuje istniejący boss. Wyjście/zstąpienie blokowane do oznaczenia encounterów tego pokoju jako ukończone; powrót na poprzednie piętro bez nowej blokady. Stare pełne snapshoty bez takiej walki pozostają możliwe do zakończenia, bez dopisywania przeciwników lub migracji schematu.

264/264 PASS (148.91 s), po 61/61 testach celowanych; następnie 9/9 po końcowym hardeningu legacy labels i wyjątku asset-audit. Zakres: 48 generacji / sześć wariantów, final guard i dotychczasowe limity liczby walk, E/LPM/outline, anulowanie i ponowna walidacja, legacy labels, oba rendery produkcyjne, bramy/pętle/save/legacy/bossowie, stress i nawigacja. Początkowo dwie regresje NavMesh wskazały zanieczyszczenie EditMode (226 NavMeshData); usunięto 225 osieroconych DontSave danych bez żywego właściciela, a test narożników sprząta także po failure. Runtime nawigacji bez zmian; izolowany retest 9/9, narożniki x16, potem pełny retest PASS.

924 ScriptableObjects bez zmian, jeden prefab z jedną referencją przez API, nowe PNG/metadane przez importer. Self-review i rendery PASS. Ręczny setup niepotrzebny; bez Computer Use, drugiego Unity, commit/push. Gameplay/full clear/FPS NOT RUN. Smoke: NOWA wyprawa dla gwarantowanej walki; brama na granicy zamiast koron, hover/LPM/E, Anuluj/potwierdzenie, brak debugowego napisu w katakumbach. Dowody: ../LocalHandoffs/dungeon-exit-20261023/README.md.

## Katakumby — wejścia poza środkiem ścian — 2026-10-23 — READY_FOR_SMOKE

Wczytane jawne pasma wejściowe w 16 niearenowych szablonach Katakumb oraz wspólne wyrównywanie/kompaktowanie według rzeczywistych progów. Każdy szablon ma wejście poza środkiem; dziewięć ma przesunięte przeciwległe wejścia. Część centralnych otworów i areny pozostają. Bez zmian obrysów, mebli, celów, szerokości drzwi, grafów, pętli/odnóg/bram, budżetów walk/EXP/lootów lub schematu save. Las zachowuje dotychczasowy dryf/pakowanie.

204/204 PASS (210.19 s): podejścia wolne we wszystkich obrotach, kompaktowość, bramy/gate-cut, save/legacy/JSON, 144 stress wyprawy, native render i NavMesh. 48 par ma identyczny graf/role/liczby pokoi i walk; 24 pełne snapshoty Lasu identyczne. W Katakumbach 64,8% końców połączeń poza środkiem (wcześniej 0%), łącznik średnio 2,1–2,4 kratki, maks. 12; powierzchnia -0,7–1,4%, brak rozrostu pustych dojść.

16 assetów DRT_catacombs_* przez API Unity; inne 908 ScriptableObjects bez zmian. Rendery/mapy obejrzane, self-review zakończony. Bez Computer Use, drugiego Unity, scen/prefabów, commit/push; gameplay/full clear/FPS NOT RUN. Ręczny setup niepotrzebny; smoke na NOWEJ wyprawie Katakumb (M, progi, bramy, save/load). Stare pełne snapshoty zachowane. Dowody: ../LocalHandoffs/catacombs-ports-20261023/README.md.

## Las — wydłużone obrysy, boczne wejścia i pakowanie łańcuchów — 2026-10-23 — READY_FOR_SMOKE

Wczytane 15 leśnych obrysów z łukami, L/S, zatokami i autorskimi wejściami poza środkiem. 10/15 niearenowych szablonów ma proporcje >=1,6:1; szerszy zagajnik/obozowiska i arena pozostają. Osadzanie wyrównuje faktyczne porty zamiast środków i kompaktuje łańcuchy względem lokalnych sąsiadów, z dolnymi/górnymi ograniczeniami odstępu i kontrolą kolizji. Zapisane ślady gruntu łączą środki pasm wejściowych, bez wymuszonego huba w centrum. Schema save v5 bez zmian.

203/203 testów PASS (183,74 s), w tym proporcje/porty, istniejące limity kompaktowości oraz maksimum łącznika 16, 144 stress wyprawy, bramy, save/legacy/JSON, obroty wyposażenia, native render/NavMesh. 48 par map zachowuje grafy/role/liczbę pokoi; wszystkie 24 snapshoty Katakumb identyczne. Budżety walk bez zmian; retry osadzania może przelosować dobór/liczbę w dotychczasowym zakresie (w próbie Short/Normal suma walk bez zmiany, Long 121→123). Względem ostatniej kompaktowej wersji Las ma 5–10% więcej pól i 19–23% dłuższą najkrótszą drogę, średni łącznik nadal około 3 kratek, 20–24% połączeń bez łącznika. Nie jest to pomiar czasu clearu.

Scope: 15 assetów DRT_forest_* przez API Unity; inne ScriptableObjects i katakumbowe blueprinty bez zmian. Natywne rendery i rzeczywiste mapy ze ścieżkami obejrzane. Ręczny setup niepotrzebny. Smoke na NOWEJ wyprawie Lasu: kręte wnętrza, boczne wejścia, czytelność pod koronami, bramy/save-load. Stare pełne mapy nie są regenerowane. Bez Computer Use, drugiego Unity, scen/prefabów, commit/push; gameplay/full clear/FPS NOT RUN. Dowody: ../LocalHandoffs/forest-organic-20261023/README.md.

## Las/Katakumby — zwarte sale, rozrzut i pomijanie łączników — 2026-10-23 — READY_FOR_SMOKE

Wczytane: losowane odstępy z możliwością bezpośredniego styku wyrównanych wejść, proste łączniki krótsze niż ich szerokość i mniejszy boczny dryf z miejscem na prawidłowy zakręt. Nie pomijamy krawędzi grafu ani bram. Zmniejszono 15 leśnych i 14 katakumbowych szablonów, zachowując ich funkcje, różnorodność selektora i rozmiary aren finałowych/bossów; wyposażenie/cele przeniesione do mniejszych wnętrz. RNG geometrii odseparowane od doboru szablonów. Brak zmian encounter placementu i budżetów walk/pokoi/odnóg/pętli/EXP/lootów, brak nowego schematu save. Istniejące pełne mapy zachowują poprzedni układ.

Porównanie 48 par (8 seedów × dwa biomy × trzy długości): identyczne grafy, szablony/role i liczby walk; powierzchnia Lasu -40,9–43,9%, Katakumb -22,9–25,0%; najkrótsza droga do wyjścia odpowiednio -39–41% i -20–22%. Łączniki średnio około 3 kratek (90. percentyl 5–6); 7–10% połączeń bez łącznika, w tym 12 z bramą w całej próbie. Pomiary geometrii z otwartymi bramami, bez rekwizytów, nie gameplay/full clear.

202/202 bounded regresji PASS (152,81 s), następnie celowane limity kompaktowości sześciu wariantów; fizyczne gate-cuty, save/legacy/JSON, role odnóg/pętli, 144 stress wyprawy, native render/NavMesh i wyposażenie we wszystkich obrotach. 29 istniejących assetów ProductionRooms zmienionych przez API Unity; inne ScriptableObjects bez zmian. Natywne rendery i obrazy faktycznych map obejrzane. Brak scen/prefabów, Computer Use, drugiego Unity, commit/push. Brak ręcznego setupu; smoke na NOWYCH wyprawach obu biomów, bezpośrednich przejściach/bramach i save/load. Dowody: ../LocalHandoffs/spatial-density-20261023/README.md.

## Las/Katakumby — produkcyjny authoring pokoi i łączników — 2026-10-23 — READY_FOR_SMOKE

Wczytane: 16 szablonów Lasu, 17 Katakumb i dwa warianty aren bossów. Nowe wyprawy używają jawnych ProductionTemplates; poprzednie pokoje są poza tą pulą, lecz pozostają do snapshotów i fixture'ów. Czyste obrysy, konkretne porty, prawdziwe łączniki bez szyjek i przesunięcia Tetris; dobór po pełnym grafie tras, zagęszczanie geometrii według rozmiarów sal. Las ma nieregularne polany/przesieki, niewielkie boczne przesunięcia i grunt łączący wejścia/cele bez centrum-gwiazdy. Katakumby mają skalę krypt/galerii/sal, korytarze proste/L/T, funkcjonalne wyposażenie i preferencje pięter. Budżety pokoi/odnóg/pętli/walk/EXP/obozów oraz zasady bram zachowane. Łączniki są geometrią, nie dodatkowymi liczonymi pokojami.

Snapshot v5 przechowuje łączniki i ślady gruntu; odczyt v1–4 zachowany, stare układy nie są regenerowane. Wspólne lookupy rozróżniają pulę generacji i wszystkie historyczne referencje. Kotwy posiłków/przejścia i dekoracje aren nie liczą łącznika jako wnętrza sali. Weryfikacja ujawniła i naprawiła zależność voxelizacji NavMesh od dalekiego Y, wyciek NavMesh z fixture'ów EditMode, dynamiczny ogień Enta widoczny poza wiedzą fog oraz duplikaty ID skrzyń galerii audytowej.

195/195 testów PASS, 0 failure/skip (165,25 s), z nowymi 48 wyprawami, 144 stress wyprawami, obrotami wszystkich szablonów, fizycznymi gate-cutami, save/JSON/legacy, bossami i replay natywnej prezentacji/nawigacji. Test ruchu po narożnikach powtarzany 16 razy. Natywne rendery i schematy obu biomów/wszystkich długości/pięter obejrzane. Scope: 35 nowych assetów pokojów i dziewięć istniejących katalogów przez API Unity (cztery aktywne, pięć Tactical tylko domyślne puste pola). Bez zapisanych scen/prefabów, Computer Use, drugiego Unity, commit/push. Self-review zakończony; ręczny setup niepotrzebny. Gameplay/full clear/FPS NOT RUN. Dowody: ../LocalHandoffs/biome-room-authoring-20261023/README.md.

Smoke: rozpocząć nowe wyprawy obu biomów; sprawdzić wejścia, M, pętle/odnogi, bramy i zapis/wczytanie. Istniejące wyprawy pozostają niezmienione.

## Wyprawy — nieregularny graf tras przed geometrią — 2026-10-23

Wszystkie piętra są planowane przed wyborem szablonów i rozmieszczeniem pierwszego pokoju. Ramiona pętli są osobno dobierane, preferowane nierówne: Short 2–4, Normal 3–5, Long 4–7 pokojów wewnętrznych, plus wspólne F/J. Suma długości ramion musi być parzysta na ortogonalnej siatce bez dodatkowych korytarzy. Dostępny bazowy budżet ogranicza pary; minimum może wzrosnąć tylko wewnątrz dotychczasowego zakresu, a bardzo ciasny profil może wymusić równe ramiona. Pętle mają zamknięte nieregularne przebiegi z wcięciami, minimum 6 zakrętów dla 8/10 pokojów lub 8 dla większych, zachowaną przestrzeń między ramionami i wolne zewnętrzne kierunki wejść/odnóg. Sześciopokojowa pętla na tej siatce musi być prostokątna. Osadzanie korzysta z istniejących zgodnych rodzin wymiarów/szablonów/przejść; nie dodaje arbitralnych korytarzy. Retry nie skraca planu ani nie zmienia późniejszych pięter.

Bramy są mostami między sekcjami, wspólnymi dla obu tras; wymagane odnogi są dostępne przed własną bramą. Długi Las ma dwie pętle, inne aktualne warianty jedną na piętro. Bazowe pule pokojów i budżety walk/EXP/obozów/questów zachowane; aktualna liczba odnóg zależy od długości (poniżej). Legacy/demo bez rozszerzonego profilu nadal używają dawnego generatora. Stare pełne snapshoty zachowane, schema v4 bez nowej wersji. Tylko sześć ofert dostało przez authoring zmianę Short max 3→4; pozostałe assety identyczne. Dowody i ograniczenia: ../LocalHandoffs/irregular-loops-20261023/README.md.

## Wyprawy — dłuższe odnogi i alternatywne drogi — 2026-10-23

Nowa generacja sześciu ofert ma na CAŁĄ wyprawę: krótka 1–3, normalna 2–4, długa 3–5 odnóg (aktualizacja 2026-10-23). W Katakumbach ta pula jest rozdzielana między piętra; pozostaje co najmniej jedna odnoga progresji na piętro. Każda ma dodatkowe pokoje dojścia: krótka 2–3, normalna 2–5, długa 3–8, a na końcu osobny istniejący pokój celu. Na każdym piętrze przynajmniej jedna pętla łączy odcinki głównej trasy; nie omija bramy ani dojścia do bocznego celu. Główna pula i encountery nie są zmniejszane na rzecz nowych dojść. Budżet walk liczy 30–40% pokojów bez IsBranchTransitRoom; same walki mogą być także w odnodze. Rola zapisywana w pełnym snapshocie v4, wersje 1–3 odtwarzane bez przebudowy. Profile z zerowym zakresem dojść zachowują poprzedni generator (story/demo/legacy). Owner: DungeonGeneratorService + DungeonLengthProfile; aktualne zakresy w BALANCE_EXPEDITION_LENGTHS.md. Dowody: ../LocalHandoffs/expedition-branches-20261023/README.md.

## Skrócone wyprawy / nagrody zwykłych przeciwników — 2026-10-23

Aktualne zakresy są zapisane w sześciu DungeonDefinition i w zaktualizowanym akapicie polityki długości poniżej; tabela i rozdzielenie od historycznych symulacji w BALANCE_EXPEDITION_LENGTHS.md. Redukcja pokoi o połowę, zwykły combat EXP ×2, boss ×1, obstawa ×2; brak kompensacji dropu, złota lub zużycia zasobów. Questy i przydział obozów bez zmian. Rozpoczęte mapy z pełnym snapshotem nie są przebudowywane.

Owner mnożnika to CombatRewardSystem.CalculateEnemyExperience, po dotychczasowej karze poziomu, przed sumowaniem/pasywami. CharacterProgressionSystem i bazowa formuła questów nie są mnożone. CombatEnemyReward.IsBoss jest pochodną canonical ScoutCombatSystem.IsBoss(dungeon, encounter, enemyId), kopiowaną przez DungeonCombatMembershipSystem i rekonstruowaną przez restore/cache. DTO i save schema nie zawierają nowego flagowego stanu. Nagrody z ExperienceLevel=0 zachowują legacy fixed-XP; posiłki/przywołania nie dostają nowych nagród.

## Las — cienka solidna przegroda / seed do reprodukcji — 2026-10-22

Przegroda Full z przechodnią kratką po obu stronach nadal blokuje ruch; jej grafika w lesie musi istnieć mimo braku pustej komórki na pień. DungeonFloorMapView tworzy małe drzewa na samej krawędzi i rejestruje je w dotychczasowym systemie koron/głębi. Chronione korzenie zajmują wąski pas przy ścianie, nie cały obszar przechodniej kratki. Bramy progresji są wyłączone z tej reguły. Passable interior TreeTrunk ze starszych układów nadal nie tworzą grafiki ani fizycznej przeszkody. To nie przywraca przypadkowych drzew w polanach i nie zmienia topologii mapy.

Konsolowe seed bez argumentu jest odczytem aktualnej/zapamiętanej wyprawy z GameSession.ActiveDungeon, niezależnym od DebugSeed. Wypisuje seed, DungeonId, Length, DungeonLevel, CurrentFloorIndex i ostatnią zapisaną PlayerPosition. ActiveDungeon może pozostać w sesji podczas wizyty w mieście; pozycja jest więc jawnie opisana jako ostatnia pozycja drużyny w wyprawie. Brak wyprawy jest zgłaszany osobno od ewentualnego seeda debugowego. seed <number> ustawia generator dla nowych debug generacji; akceptuje pełny zakres int, w tym zero/ujemne. Zapytanie nie resetuje RNG i nie regeneruje bieżącej mapy.

Pozostałe zgłoszone migotanie cieni przy nieruchomej scenie pozostaje niewyjaśnione. Test stacjonarny i niepotwierdzony zapis live nie są dowodem naprawy. Powtórne zgłoszenie powinno zawierać seed/parametry wyprawy oraz miejsce; diagnostyczny recorder zapisuje te dane po kolejnym jawnym żądaniu.

## Eksploracja — duża mapa odkrytego terenu — 2026-10-22

M przełącza duży podgląd istniejącej minimapy podczas eksploracji lasu/katakumb; M/Esc/X zamyka. Skala i środek wynikają z odkrytych oraz bieżąco widocznych pól aktualnego ViewedLevel, z marginesem i zachowaniem proporcji. Podgląd jest projekcją danych Fog, nie kamerą świata ani nowym obliczeniem LOS. Ukryte odnogi nie wpływają na kadr; przeciwnicy nadal wymagają bieżącej widoczności i braku niewidzialności. togglefog jest jawnym debug wyjątkiem.

Otwarta mapa przejmuje wejście eksploracji i kamery, lecz nie pauzuje czasu. Przejście do walki ją zamyka. Nie otwiera się podczas pisania/konsoli/menu kontekstowego. Zamknięcie odtwarza poprzedni layout i zwinięcie, z ochroną przed wykonaniem drugiej akcji tym samym Esc/kliknięciem. DungeonMinimapView ma jawne runtime powiązanie z ExplorationPartyController, bez wyszukiwania obiektów i nowego singletonu. Nie ma zmiany save schema ani konfiguracji prefabów/scen.

Tekstura podglądu ma maksymalnie 1024 px na dłuższym boku; powstaje ponownie tylko po zmianie proporcji. Rysowanie terenu/upload następuje po rzeczywistej zmianie wiedzy, widoczności, wysokości lub zawartości, nie co klatkę ani po identycznym fog ticku. Zmiana kadru korzysta z historycznej wiedzy, nie pełnej nieodkrytej mapy.

## Przygotowanie nawigacji — cache geometrii — 2026-10-22

StaticGeometryRevision mapy dotyczy dodania powierzchni/ściany oraz zmiany typu ściany. Stan drzwi i bram zmienia tylko dotychczasowy TraversalRevision. Cache numerycznych źródeł podłogi/ścian jest własnością kontrolera i współdzielony przez różne promienie agentów na danym poziomie; uwzględnia map identity, poziom, projekcję i grubość ścian. Zmiana drzwi/bramy dopisuje tylko aktualne dynamiczne blokery. Nie ma nowego zapisu w DTO/save.

Widok mapy indeksuje swoje collidery podczas setupu i lokalnej wymiany ścian. Numeryczny snapshot stałego terenu jest przygotowany przy pełnej budowie geometrii; zwykłe odświeżenie dekoracji go nie kasuje. Native brama invaliduje tylko wymieniane ściany; las odczytuje aktualny enabled swoich barier. Przesunięcie root transform albo jawny refresh odbudowuje bounds. Zewnętrzne collidery cache'ują referencje, ale ich enabled/active/bounds odczytywane są na nowo. Nieudana trasa odświeża teren oraz pełny physics overlap i w razie zmiany planuje bake w tle. Walidacja fizyczna każdego odcinka pozostaje aktywna, co chroni przed nowymi lub poruszonymi propsami.

Pomiar przygotowania Long seed 171026: las 77.06→6.12–8.01 ms, katakumby 36.19→2.77–3.23 ms; to nie całkowity frame time. 214 odrębnych testów PASS, 5 jawnych dodatkowych audytów/capture skipped. Szczegóły i dowody: ../LocalHandoffs/navigation-preparation-20261022/README.md.

## Bramy progresji — lokalna aktualizacja i nawigacja — 2026-10-22

Normalne otwarcie/zamknięcie istniejącej bramy nie przebudowuje całego DungeonFloorMapView. Las zachowuje drzewa/cache odkrycia i przełącza fizyczną barierę oraz jej widok/pamięć; native katakumby odtwarzają tylko ściany przy zmienionych narożnikach, zachowując odległe renderery i głębokość. Zmiana samego stanu bramy nie zmienia powierzchni podłogi. Nowa lub częściowo przegrupowana leśna bariera zachowuje pełny fallback setupu. Logika stanu bramy i save schema bez zmian.

NavMesh po zmianach runtime budowany asynchronicznie; poprzednia siatka jest usuwana z zapytań, a oczekująca praca anulowana przy kolejnej invalidacji/teardown. Do zakończenia pracy dostępny jest dotychczasowy walidowany logiczny pathfinding. Początkowe siatki przygotowywane przy konfiguracji piętra. Zbieranie źródeł nadal na main thread: pomiar Long seed 171026 76.79 ms las/24.67 ms katakumby. Nie jest to rozwiązanie wszystkich spike'ów eksploracji. 148 testów PASS; 5 dodatkowych jawnych audytów/capture pominiętych; rendery lokalnej bramy zgodne z pełną przebudową. Szczegóły: ../LocalHandoffs/progression-interaction-stall-20261022/README.md.

## Las — regresja kosztu mgły na mocno odkrytej mapie — 2026-10-22 — READY_FOR_SMOKE

Potwierdzono wzrost kosztu nowego filtru ForestKnowledge: każda zmiana widoczności ponownie malowała margines wokół całej historii odkrycia. Dodano cache pamięci, osobną lokalną warstwę widoczności, gotowe wagi/maskę gruntu i animowanie tylko aktywnych pikseli. Pamięć malowana jest dla nowych źródeł; skan starego zbioru potrzebny tylko przy wzroście liczby odkrytych komórek. Reset po debug/map/floor/geometry/depth/teardown. Bez zmian wyglądu, transmisji .35, głębokości lasu, LOS, odkrywania, shadera i save schema.

Kontrolowany pomiar samego filtru w EditMode: mediana 128 odkrytych kratek 7.30→1.28 ms, 1024: 33.96→1.28 ms, 6144: 180.96→1.25 ms (maksimum 209.44→1.41 ms). To nie pomiar całej klatki/FPS. 57 odrębnych testów PASS: niezależny poprzedni algorytm zgodny dla 50 625 pikseli/25 stanów, przejściowa alfa, licznik pracy po rozgrzaniu, behind-tree/deep-woods, produkcyjne GPU renderingi dla 2 seedów, jednolita transmisja oraz dotychczasowe fog/transition testy. Aktualne rendery obejrzane. Import/self-review/scoped diff-check zakończone; bez serialized setupu, Computer Use, ręcznego Refresh, commit/push. Pozostaje powtórzenie przejazdu użytkownika w Play Mode. Dowody i ograniczenia pomiaru: ../LocalHandoffs/forest-fog-spikes-20261022/README.md.

## Konsola — noaggro do testów eksploracji — 2026-10-22 — READY_FOR_SMOKE

Dodano noaggro [on|off]: brak argumentu przełącza, on blokuje automatyczne aggro, off przywraca normalne zachowanie. Stan należy do bieżącej GameSession, działa między lokacjami i nie wchodzi do save DTO. Nowa sesja domyślnie ma normalne aggro. Komenda jest w help i autouzupełnianiu; błędne argumenty nie zmieniają stanu.

W lochach noaggro pomija wykrywanie rozpoczynające/dołączające walki, zeruje narastającą świadomość wolnych wrogów i ukrywa jej wskaźniki. Zatrzymuje alarmy, dołączanie oraz oczekujące scalenia; zwalnia ich MembershipChangePending. Patrole nie zatrzymują się, aby obserwować drużynę. Geometria LOS/FOV, mgła, trasy patroli, trwające walki i ręczne rozpoczęcie walki pozostają aktywne. To narzędzie testowe, nie optymalizacja normalnej eksploracji.

88/88 testów EditMode PASS: konsola/schema/sugestie i integracja wykrywania 74, patrole/drzwi/persistence patroli 14. Nowe testy pokrywają toggle/on/off, idempotencję, brak sesji, błędne argumenty, help/sugestie, wyczyszczenie wykrycia, wznowienie zapytań po off i usunięcie oczekujących alarmów/scalenia bez zakończenia walk. Import automatyczny i self-review/scoped diff-check zakończone. Bez serialized setupu, Computer Use, ręcznego Refresh i commit/push. Dowody: ../LocalHandoffs/noaggro-command-20261022/.

## Furia — tura przerwy i ochrona podczas CC — 2026-10-22 — READY_FOR_SMOKE

Pierwsza pełna własna tura bez ofensywy jest bezpłatna; na końcu drugiej i kolejnych ubywa 10 furii. Użycie skilla ofensywnego resetuje licznik, również przy pudle, pełnym pasku i blokadzie generacji. Sam pasywny przyrost go nie resetuje. Ogłuszenie, zamrożenie, paraliż i sen zatrzymują licznik bez resetu. Unieruchomienie chroni tylko wtedy, gdy nie było legalnej ofensywy na żywego przeciwnika; sprawdzane są wspólne reguły targetowania, koszt/AP, LOS i blokady skilla. Okazja dostępna przed wydaniem AP zostaje zapamiętana. Sam dystans bez CC nie chroni. Poza walką nadal -10 co 6 sekund.

Licznik jest lokalny dla BattleUnit.WarriorState, bez zmiany save schema. Rozliczenie następuje przed końcowoturnowym wygasaniem statusów. Zaktualizowano produkcyjny oraz fallback glosariusza i opis GameDefine, zachowując ID/wartość. Import automatyczny zakończony. 179/179 testów EditMode PASS: zasoby, eksploracja, glosariusz, timeline, produkcyjny wojownik i statusy. Nowe regresje obejmują zabicie celu + podejście, CC i jego końcowe wygaśnięcie, root z atakiem bliskim/dalekim, LOS/koszt/niewidzialność/mobilność, zapamiętaną okazję, cap/blokadę generacji i pasywny przyrost. Self-review zakończony; brak ręcznego setupu Editora, Computer Use i commit/push. Dowody: ../LocalHandoffs/rage-grace-cc-20261022/.

## Las — ograniczenie presji dystansowych encounterów — 2026-10-22 — READY_FOR_SMOKE

Na prośbę użytkownika zmieniono dwa produkcyjne składy: Elfi zwiadowcy z 3 łuczników na kłusownika + druida + łucznika; Bandycki patrol z 2 rzezimieszków + 2 strzelców na 3 rzezimieszków + 1 strzelca. Zaktualizowano odpowiednie dwa assety EncountersBalanced i źródło reauthoringu EncounterBalanceSetup.ForestRosters. Druid otrzymał też zgodne z jego definicją ClassData, zamiast pozostawionej klasy łucznika. Zachowano liczebność, ID slotów i układ spawnów; wszystkie trzy pule dostępnych wypraw korzystają z tych samych poprawionych definicji. Pozostałe encountery i statystyki bez zmian.

Import poza Play Mode zakończony. 2/2 celowanych testów PASS: konfiguracja produkcyjnych pul oraz integracyjny test obu składów, poprawnych klas (w tym many druida), reauthoringu na kopii i generacji z legalnymi, odrębnymi polami spawnów. Nie uruchamiano pełnego ApplyAll ani szerokiego reauthoringu contentu. Self-review zakończony, bez ręcznego setupu Unity, Computer Use, commit/push. Dowody: ../LocalHandoffs/forest-roster-pressure-20261022/.

Dyskusja furii: bieżąca reguła odejmuje 10 na początku kolejnej tury, jeżeli od poprzedniego startu nie zaznaczono generacji furii; wykonywana jest przed obsługą pominięcia tury przez twarde CC. Nie ma wyjątku dla ogłuszenia/zamrożenia/paraliżu/snu. Rekomendacja do decyzji użytkownika: pełna tura odebrana przez takie CC powinna wstrzymywać decay; efekty pozostawiające możliwość ataku nie powinny dostawać wyjątku. Nie wdrożono zmiany reguł zasobu.
## Las — usunięcie pni z przechodniego obrzeża i łagodniejsza przezroczystość — 2026-10-22 — READY_FOR_SMOKE

Po kolejnym smoke użytkownik wskazał trzy pnie wewnątrz polany i zbyt zanikające dolne korony. Pominiętym źródłem były markery TreeTrunk tworzone niezależnie przez DungeonFloorMapBuilder.AddVegetationFringe, na krawędziach pomiędzy dwiema przechodnimi kratkami. Usunięto ich generowanie, pozostawiając te same przechodnie pola obrzeża. Widok pomija takie wewnętrzne markery w starszych układach: nie tworzy grafiki ani kolizji, a wyłączona ceglana ściana prefabu nie wraca. Usunięto nieużywaną funkcję BuildIndividualTrunk. Zapis nadal rozpoznaje dotychczasowy enum i format; nie ma migracji/usuwania danych ani zmiany seedów/zajętości podłogi. Zewnętrzne drzewa graniczne zachowują rezerwacje poza przechodnią mapą.

Łagodna alfa koron polan/przejść podniesiona z 130/255 do 160/255. Aktorzy zachowują 90/255, kontur kontaktu, chronione korzenie i czasy fade. Bieżący import zakończony. 53/53 testów logiki i renderu PASS, następnie 2/2 testów renderu/kontaktu PASS po dodaniu kontrolowanego porównania alf (54 unikalne przypadki). Regresje sprawdzają brak nowych markerów na trzech seedach, brak grafiki i fizycznych przeszkód w układzie ze starym markerem, dostępność kieszeni przez native navigation, wszystkie zarejestrowane dojrzałe korony na zewnątrz przechodniej mapy oraz wcześniejsze GPU/LOS/kontury i priorytety fade.

Obejrzano polanę i blind_glade z prawdziwą drużyną i normalnym LOS, bez RevealMap; dodatkowo kontrolowane pary alfa 130/160 na identycznej scenie po usunięciu wewnętrznych drzew. Korony przy 160 są pełniejsze. Starsze obrazy previous-alpha-130 zawierają również stare pnie i są wyłącznie historycznym porównaniem. Self-review zakończony, brak ręcznej konfiguracji Editora, Computer Use, commit/push. Dowody: ../LocalHandoffs/forest-interior-tree-removal-20261022/.
## Las — korekta niewidocznego wygaszania i pustego pasa za drzewami — 2026-10-22 — READY_FOR_SMOKE

Poprzedni smoke odrzucony przez użytkownika: efekt koron niewidoczny, za pierwszym rzędem nadal widać pusty grunt. Zapytania przejść i dolnej krawędzi sprawdzają teraz przecięcie rzeczywistego konturu liści z powierzchnią widocznej kratki (98% jej wymiarów), zamiast wyłącznie środka kratki. Chronione korzenie są wycinane z tego zapytania. Nie zwiększono zasięgu kontaktu bohaterów. Łagodna alfa przejść/polan zmieniona z 175/255 na 130/255; mocny kontakt aktorów nadal 90/255, czasy przejścia zachowane.

Wypełniające małe drzewa są generowane w wolnych miejscach również na dalszych głębokościach, zamiast kończyć się na drugim rzędzie. Zachowano rezerwacje poza przechodnią mapą, spacing i natywną skalę. Ograniczono dodatkowe przyciemnienie RGB głębokich drzew do łagodnego zakresu 65–100%; zanikanie lasu w mroku nadal zapewnia wspólny filtr mgły. To zapobiega znikaniu sylwetek drzew przy nadal widocznym ciągłym podłożu. Bez zmiany ambientu, palety, LOS i odkrywania.

58/58 celowanych testów PASS. Nowe regresje: liście tylko przy brzegu kratki, wypełnienie głębi po czterech stronach i brak podwójnego wygaszania RGB. Zachowane native-contour/GPU/actor-priority/zero-allocation/LOS testy. Uaktualniono stary test, który mylił ciągły backdrop z lokalnym patchem podłoża. Obejrzano A/B wygaszania long_glade i blind_glade z prawdziwą drużyną i normalnym LOS, bez RevealMap; różnica odpowiednio ponad 116 tys. i 249 tys. pikseli. Obejrzano też normalny filtr z czterech stron wygenerowanego lochu oraz izolowanej polany 8x8, bez sąsiednich pokoi. Izolowana polana ma 61 głębokich rezerwacji drzew; cztery kratki za granicą filtr ma alfa ok. 0,502 we wszystkich kierunkach. Sąsiednie rzeczywiste nieodkryte podłogi nadal pozostają czarne. Import i self-review zakończone. Brak ręcznej konfiguracji Editora, Computer Use, commit/push. Dowody: ../LocalHandoffs/forest-canopy-readability-recheck-20261022/.
## Las — łagodne wygaszanie dolnych koron polan i wnęk — 2026-10-22 — READY_FOR_SMOKE

Rozszerzono istniejące wygaszanie 175/255 na wygenerowane drzewa graniczne, których rzeczywisty kontur korony nachodzi od południa na aktualnie widoczną podłogę polany lub wnęki. Wybór dotyczy lokalnej dolnej krawędzi każdego obszaru, nie globalnego minimum Y mapy; nie wymaga rozpoznania wąskiego przejścia. Korzenie pozostają chronione. Bohaterowie i widoczni przeciwnicy zachowują mocniejszy cel 90/255 i pierwszeństwo. Zapamiętany/nieodkryty grunt nie uruchamia efektu; odkrywanie, LOS, oświetlenie i geometria nie są zmieniane.

48/48 celowanych testów PASS: dolna krawędź szerokiej polany i osobnej wnęki, pamięć/nieodkrycie, zachowanie górnych i odległych drzew, priorytet bohatera, płynność, brak alokacji rozgrzanego zapytania, istniejące native-contour/GPU/regresje przejść i LOS. Obejrzano produkcyjne rendery przed/100ms/po dla long_glade i blind_glade oraz dodatkowe ujęcia z prawdziwym bohaterem i aktualnym LOS. A/B izoluje nową regułę na w pełni odsłoniętym terenie; stare reguły przejść/bohaterów działają w obu wariantach. Przy 4–5 nakładających się koronach efekt 175 pozostaje celowo łagodny; nie usuwa całej roślinności i nie gwarantuje pełnego odsłonięcia podłogi. Import i self-review zakończone; brak ręcznego Editor setupu. Bez Computer Use i commit/push. Dowody: ../LocalHandoffs/forest-foreground-canopy-20261022/.
## Las — kontakt bohatera z konturem korony zamiast prostokąta grafiki — 2026-10-22 — READY_FOR_SMOKE

Zgłoszenie: korona wygaszała się, gdy Jerzy stał daleko od widocznych liści. Prawdziwe grafiki drzew są importowane jako FullRect (4 wierzchołki); dotychczasowy test kontaktu na siatce obejmował cały przezroczysty prostokąt. Syntetyczne testy TightMesh nie wykrywały tej różnicy. Cache koron korzysta teraz z istniejących importowanych konturów sylwetki (GetPhysicsShape), współdzielonych na gatunek, z dotychczasową siatką jako fallback dla sprite’ów bez konturów. Nie dodaje colliderów, nie zmienia fizyki, importu grafiki ani czytelności tekstur. Test punktu i małego obszaru podtrzymującego używają tego samego konturu, z zachowaniem flipów i chronionych korzeni.

Usunięto też podbicie łagodnego celu przejścia 175/255 do mocnego 90/255 przez sam fakt istnienia stanu fade. Margines podtrzymania bohatera dotyczy tylko wcześniejszego mocnego kontaktu. Punkt wejścia nadal jest przy stopach bohatera, czasy fade i wartości alfa zachowane.

41/41 celowanych testów PASS: sześć prawdziwych sprite’ów normalnie/flip, istniejące regresje aktora i przejścia, nowy przypadek priorytetu, GPU, zero alokacji rozgrzanych zapytań, produkcyjne A/B prostokąt→kontur oraz LOS. Diagnostyka przed zmianą: 5457 przezroczystych próbek traktowanych jako kontakt w każdej orientacji. Importowany kontur ma drobną aproksymację liści, zmierzona granica odchyleń 5–7 px przy 32 PPU; test ogranicza ją do maks. 8 px (1/4 kratki), bez deklaracji maski alfa idealnej co do piksela. Obejrzano rendery rzeczywistego bohatera na przechodnim polu poza koroną: poprzedni prostokąt wygaszał drzewo, kontur pozostawia je pełne. Import/self-review zakończone, bez Computer Use, serialized setupu i commit/push. Dowody: ../LocalHandoffs/forest-canopy-contact-20261022/.

## Las — delikatne wygaszanie koron nad widocznymi przejściami — 2026-10-22 — READY_FOR_SMOKE

Wąskie przejścia (do 3 kratek szerokości, z kontynuacją wzdłuż przejścia) w aktualnym LOS wygaszają faktycznie nachodzące korony do alfy 175/255. Selekcja korzysta z istniejącej siatki koron i tight mesh; chronione korzenie, przezroczyste narożniki i drzewa poza kamerą nie uruchamiają efektu. Szerokie polany oraz zapamiętane/nieodkryte przejścia nie są źródłem wygaszania. Bohaterowie/widoczni przeciwnicy zachowują mocniejszy cel 90/255, który ma pierwszeństwo. Po odejściu bohatera korona wraca do 175, jeśli nadal osłania widoczne przejście; po utracie LOS płynnie do 1. Istniejące czasy wejścia/wyjścia i shader alfy bez zmian.

41/41 testów PASS: rozpoznawanie szerokości, pamięć/nieodkrycie, rzeczywisty mesh liści, priorytet bohatera i płynny powrót, invalidacja po zmianie ściany, zero alokacji rozgrzanego zapytania, dwa GPU dla 175/255, dotychczasowe regresje bohaterów/koron, LOS i głębia lasu. Render produkcyjny przed/100ms/po obejrzany. Podgląd izoluje drogę na w pełni odsłoniętej mapie, stan mgły jest testowany osobno. Celowo wybrano trudny przypadek z 7 nakładającymi się koronami: efekt 175 pozostaje subtelny, wartość próbna do oceny użytkownika. Cache topologii jest przypisany do widoku i odświeżany przez TraversalRevision, bez zmian save/assetów/scen/prefabów. Import i self-review zakończone, bez Computer Use/commit/push. Dowody: ../LocalHandoffs/forest-passage-canopy-20261022/.

## Las — dalsze warstwy drzew za pasem brzegowym — 2026-10-22 — READY_FOR_SMOKE

Screenshot z czerwoną linią pokazywał wizualny koniec lasu tuż za pierwszym pasem drzew. Porównano tę samą scenę z filtrem i bez niego: geometria zawierała 399 dalszych zarezerwowanych pni, ale filtr odkrycia przechodził w pełną czerń już po 3 kratkach, podczas gdy drzewa generowano do 7. Filtr dekoracyjnego lasu używa teraz tej samej głębokości co geometria (wspólne ForestBoundaryDepthInCells), więc dalsze sylwetki przechodzą stopniowo w mrok zamiast znikać za pierwszym rzędem. Bez dodawania drzew na polanach, nowych obiektów i zmian serialized state.

29/29 celowanych testów PASS: cztery strony polany na renderach produkcyjnych, cztery testy dekoracyjnej głębokości/pamięci i nieodkrytej podłogi, cztery LOS za drzewem, jednolite przyciemnienie całej scenerii oraz regresje koron. Obejrzano cztery ujęcia po poprawce i zachodni A/B z filtrem/bez. Znane rzeczywiste pola zachowują swój stan mgły; promień odkrywania, LOS, wykrywanie wrogów, alfa zapamiętania 0.65, światło i wygaszanie koron niezmienione. Na południu próbka 4 kratki poza wybranym brzegiem trafia w inne rzeczywiste nieodkryte pole i prawidłowo pozostaje czarna. Import i self-review zakończone; bez Computer Use, ręcznego Refresh, commit/push. Pozostaje gameplay smoke. Dowody: ../LocalHandoffs/forest-background-depth-20261022/.

## Las — usunięcie dużych drzew ze środka polan — 2026-10-22 — READY_FOR_SMOKE

Usunięto dwie kotwy tree.0/tree.1 z szablonu long_glade przez istniejący authoring Unity. To pełne obiekty przeszkód 2×2, więc brak ich instancji usuwa również fizyczne/logiczne blokowanie ruchu i LOS. Authoring nie dodaje ponownie drzew wewnętrznych, także przy późniejszym imporcie grafiki. Drzewa obrzeży, definicja/grafika drzewa do testów i przyszłego użycia, inni przeciwnicy oraz pozostałe rekwizyty zachowane; bez zamienników. Istniejące zapisane wyprawy zachowują snapshot obiektów — zmiana dotyczy nowych wypraw.

7/7 celowanych testów PASS: dziewięć generacji lasu (3 długości × 3 seedy) bez drzew wewnętrznych i z pozostałymi przeszkodami, cztery przypadki zawartości wypraw/bossów, dwa testy renderu lasu/katakumb. Obejrzany render wnętrza lasu. Diff assetu obejmuje wyłącznie usunięcie dwóch kotew (24 linie); brak zmian scen/prefabów, save migracji, wygaszania koron, światła lub mgły. Import i self-review zakończone, bez Computer Use/commit/push. Dowody: ../LocalHandoffs/forest-remove-interior-trees-20261022/.

## Las — usunięcie nierównego, podwójnego przyciemnienia mgły — 2026-10-22 — READY_FOR_SMOKE

Screenshot użytkownika: ścieżka i obiekty za ciemne, ściółka obok jaśniejsza. Poprzednie zwiększenie alfy nie rozwiązywało przyczyny. Stary tint pamięci 0.55 nadal przyciemniał obiekty i kafle, a shader gruntu stosował go do ścieżki, omijając zwykłą ściółkę. Nad tym działał już wspólny filtr mgły. Usunięto drugi tint w lesie dla kafli, dekoracji, przeszkód/obiektów, bram i ciał. Wspólna polityka jasności pamięci zachowuje 0.55 w katakumbach. Alfa wspólnego filtru 0.65, ambient, shader podnoszenia cieni, paleta, LOS, odkrywanie, widoczność wrogów i wygaszanie koron bez zmian.

Nowy test z rzeczywistym LateUpdate 150 widoków obiektów odtworzył błąd (31 451 pikseli i 211 kafli dodatkowo przyciemnionych). Po naprawie: 0 zmian statycznej scenerii po odjęciu filtru, 42 353 próbki GPU z jednakową transmisją bez odstępstw. Animowane emitery i ich kontakty wyłączone wyłącznie z dowodu identyczności statycznych pikseli; zachowane w oglądanym renderze mieszanej widoczności. Finalne 33/33 testów PASS, rendery obejrzane; self-review i import zakończone. Bez Computer Use, serialized setupu, commit/push. Pozostaje gameplay smoke. Dowody: ../LocalHandoffs/forest-fog-uniformity-20261022/README.md.

## Las — mocniejszy kontrast zapamiętanego terenu — 2026-10-22 — READY_FOR_SMOKE

Alfa czarnego filtru nad zapamiętanym lasem zwiększona z 35% do 65% (transmisja liniowa 0.65 → 0.35). Bieżąca widoczność i teren nieodkryty zachowują dotychczasowe wartości. Bez zmian LOS, odkrywania, wykrywania przeciwników, światła i wygaszania koron.

Import zakończony; 6/6 celowanych testów EditMode PASS: cztery przypadki mgły za drzewem 1×1/2×2 i dwa testy renderu produkcyjnego lasu/katakumb. GPU zapamiętanego białego wzorca: 0.827451 → 0.627451, widoczny sąsiad nadal 1. Obejrzano normalny i zapamiętany widok wejścia oraz zapamiętane wnętrze lasu: wyraźniejsze przygaszenie, zachowane detale. Katakumby bez regresji. Self-review zakończony; bez ręcznego setupu, Computer Use, commit/push. Pozostaje ocena w grze. Dowody: ../LocalHandoffs/forest-fog-contrast-20261022/.

## Las — zgodność renderu mgły z linią wzroku — 2026-10-22 — READY_FOR_SMOKE

Przyczyną fałszywie odsłoniętej ścieżki za drzewem był3-kratkowy margines maski, który przenosił jasność widocznej kratki na sąsiednie ukryte pola. Margines dotyczy teraz wyłącznie dekoracyjnego lasu poza grywalnym gruntem. Każda grywalna kratka zachowuje własny stan Fog.Get — ten sam, którego używa widoczność przeciwników. Zapamiętywanie mapy, LOS, wykrywanie przeciwników, wartości zaciemnienia, paleta i wygaszanie koron niezmienione. Dodano zwalnianie tekstury filtru w istniejącym ReleaseSoftFog.

90/90 focused testów PASS. Nowe4przypadki: drzewo1×1/2×2, pole za nim nieodkryte/zapamiętane, a pole obok widoczne; sprawdzono cały grunt maski, GPU i zgodność z IsCurrentlySeen. Obejście drzewa odsłania pole; powrót przywraca stan zapamiętany bez utraty odkrycia. Obejrzane maskiGPU i rendery produkcyjne127/171026. Pierwszy test cleanupu wymagał jawnego wywołania ReleaseSoftFog w fixture EditMode (bez polegania na callbacku MonoBehaviour). Import zakończony, self-review bez dalszych uwag; brak ręcznego Editor setupu/Computer Use/commit/push. Pozostaje smoke na przykładzie użytkownika. Dowody: ../LocalHandoffs/forest-los-fog-20261022/.
## Las — usunięcie sztucznej obwódki postaci — 2026-10-22 — READY_FOR_SMOKE

ForestPropSeparation nie dodaje już zewnętrznego szarobeżowego konturu do profilu aktora. Zachowane oryginalne piksele/alfa sprite'a, dotychczasowy mnożnik jasności1.16 i osobny system zaznaczenia/hover. Profile rekwizytów/roślin, światło, mgła i wygaszanie koron zachowane. GPU na czterech rzeczywistych sprite'ach bohaterów, pełna alfa i0.4:0zmian alfy wobec źródłowej sylwetki; kontrola dodatnia wykrywa9088pikseli obwódki na profilu rekwizytu. 24/24 testów PASS (GPU konturu, ton światła i korony), rendery obejrzane; self-review zakończony. Import odebrany, bez Computer Use/serialized setupu/commit/push. Pomocnik testowy otrzymał opcjonalny jednorazowy odczyt stanu Editora do diagnozy oczekującego importu. Pozostaje gameplay smoke wyglądu postaci. Dowody: ../LocalHandoffs/forest-actor-contours-20261022/.
## Las — filtr odkrycia i płynność światła — 2026-10-22 — READY_FOR_SMOKE

Poprzednie READY_FOR_SMOKE odrzucono po gameplay: tło omijało wizualne zaciemnienie nieodkrycia. Dodano należący do widoku mapy filtr wiedzy nad leśną scenerią: nieodkryte zaciemnione, zapamiętane przygaszone, widoczne czytelne; miękka granica i animowane przejście, togglefog wyłącza filtr. Aktualna linia wzroku poza promieniem trwałego odkrycia pozostaje odsłonięta. Filtr respektuje granicę pasma HUD i zwalnia teksturę/sprite przy przebudowie mapy. Ambient0.34/detail0.30, paleta i wygaszanie koron zachowane; bez serialized wiring/save zmian.

80/80 focused testów PASS dla filtru, następnie2/2 po korekcie dalekiej linii wzroku. Dwa seedy127/171026,32kroki kamery/FOV/flip: maski GPU cache identyczne z pełnym przeliczeniem. Wykonano24-sekundowy zapis82klatek z gry użytkownika (bez Computer Use/profilera); użytkownik potwierdził brak dalszego zaniku cieni. Zgłosił natomiast migotanie światła. Potwierdzony problem: sinus2.5Hz próbkowany5Hz daje naprzemienne skoki energii. Las otrzymał łagodne ciągłe zmiany0.65/1.1Hz, amplituda0.031 zamiast0.108; same uniforms energii aktualizują się co klatkę, geometria i selekcja źródeł nadal5Hz. Katakumby i shader płomienia bez zmian. Końcowy przebieg81/81 PASS: GPU cache vs pełny render, zmiany energii między tickami bez przebudowy masek, zakres/płynność energii, fog/togglefog/teardown i regresje koron. Zweryfikowano finalne rendery. Utkwione pobieranie katalogu ominięto jawną listą testów w pomocniku, nadal przez Unity Test Runner. Self-review zakończony; bez ręcznego Editor setupu. Do smoke pozostaje nowa płynność światła podczas gry. Dowody: ../LocalHandoffs/forest-discovery-regression-20261022/.
## Las — stabilność cieni i granic mgły — 2026-10-22 — READY_FOR_SMOKE

Usunięto konflikt rysowania mgły i kontaktów (las: podłoga→mgła→cienie→outline→rekwizyt). Cienie zapamiętanych przeszkód korzystają z tej samej widoczności co ich grafika, a statyczne dekoracje nie znikają po utracie bieżącego FOV. Kontakt na leśnej ściółce nie jest przycinany do widzenia jednostek. Nieodkryte pola nadal nie rysują drogi/obiektów; jedna warstwa neutralnej ściółki wypełnia czarne dziury. Statyczna ściółka ma spójne światło niezależnie od wiedzy; przyciemnienie zapamiętania pozostaje na ścieżkach/rekwizytach. Mesh mgły ma własne wejście shaderowe, wspólny tint i siatkę próbkowania32PPU. Zabezpieczono dzielenie przez zero przy odkrywaniu. Zachowane ambient0.34/detail0.30, migotanie pochodni, paleta i wygaszanie koron.

114/114 odrębnych focused testów PASS, w tym finalne3/3 po domknięciu dziedziczenia warstwy tła. Dwa produkcyjne seedy171026/127,12klatek ruchu/FOV, obejrzane rendery granic i różnic GPU. W teście bez dróg/kontaktów0niezgodnych pikseli ponad2/255 między podłożem znanym i tłem. Brak ujawniania kanałów dróg/kontaktów; reuse/destroy i warstwa tła sprawdzone. Historyczny benchmark ContactShadowCostTests ma nieaktualny wzorzec trasy2026-10-18 i nie przeszedł kontroli hash przed pomiarem; nie zmieniano go ani nie wyłączano, nie jest dowodem aktualnegoFPS. Bez Computer Use, ręcznego Refresh, serialized setupu, zmian save/statystyk/pathfindingu lub commit/push. Import zakończony; pozostaje gameplay smoke użytkownika. Raport: ../LocalHandoffs/forest-fog-shadow-stability-20261022/README.md.

## Nocny klimat lasu — korekta jasności — 2026-10-22 — READY_FOR_SMOKE

Po uwadze użytkownika poprzedni profil0.62/detail1 uznano za zbyt dzienny. Aktualnie ambient0.34, siła podnoszenia cieni0.30. Średnia jasność poprzednio ciemnych detali: wejście33.00→16.97, wnętrze33.87→17.65; nadal około1.8× pierwotnej wersji. Parametry są sterowane kodem bez serializacji, aby istniejący Editor nie zachowywał starej jasności po reloadzie. Shader, paleta, pochodnie, mgła i wygaszanie koron bez zmian względem poprzedniej implementacji.

Import zakończony,24/24 testów PASS (GPU, dwa ujęcia produkcyjne obu biomów, regresje koron). Dwa rendery przed/po obejrzane: mrok poza pochodniami i lepsza niż pierwotnie czytelność drzew/ścieżek. Dodany limit rozjaśnienia w teście względem pierwotnej sceny. Katakumby identyczne piksel w piksel. Bez Computer Use, ręcznego Refresh, serialized setupu lub commit/push. Pozostaje ocena użytkownika w grze. Dowody: ../LocalHandoffs/forest-night-readability-20261022/README.md. Ta korekta zastępuje wcześniejsze ustawienie0.62/detail1.

## Czytelność cieni w lesie — 2026-10-22 — READY_FOR_SMOKE

Las ma osobne światło rozproszone 0.62 (katakumby nadal 0.22) i łagodne podniesienie ciemnych tonów przez wspólny mnożnik RGB. Zachowane grafiki, paleta, czerń, alfa koron, odkrywanie terenu i lokalne pochodnie; bez światła przy bohaterze. Profil obejmuje grunt, drzewa, aktorów, rekwizyty i metal pochodni, z pominięciem samych płomieni. Pamiętany teren zachowuje przyciemnienie mgły; nieodkryty teren nadal podlega dotychczasowym regułom.

69/69 odrębnych testów EditMode PASS: 66 integracyjnych/regresji + 3 GPU. Dwa ujęcia produkcyjnego lasu obejrzane przed/po; średnia jasność próbek wcześniej ciemnych detali około 3.5× większa. Katakumby w dwóch ujęciach identyczne piksel w piksel przy zmianie profilu lasu. GPU potwierdza proporcje RGB w liniowej przestrzeni, zachowanie czerni, alfy i jasnego wzorca. Dwa pliki wygaszania i funkcja alfy w shaderze niezmienione. Import w istniejącym Unity zakończony; bez Computer Use, ręcznego Refresh, setupu serialized state lub commit/push. Smoke użytkownika: las w eksploracji/walce, obszar bez pochodni i przy pochodni, znany teren poza widocznością oraz fade koron. Bez pomiaru FPS i ręcznego gameplay smoke. Dowody: ../LocalHandoffs/forest-shadow-readability-20261022/README.md.

## Wyśrodkowanie ikon przedmiotów — 2026-10-22 — READY_FOR_SMOKE

Naprawiono nierówne marginesy odziedziczone po starych grafikach:269PNG przesuniętych całymi pikselami (93bazowe/kopie +176wariantów bransolet/naszyjników). Bez zmiany skali, koloru, alfy, kształtu lub utraty widocznych pikseli. Wszystkie558ikon ekwipunku mieszczą środek obrysu w tolerancji0.5px od środka32×32. 40pomocniczych warstw biżuterii przesunięto zgodnie;17warstw runtime zachowano z rejestracją baz. 575meta/GUID i biblioteka ikon bez zmian, UI/prefaby bez zmian.

Wczytane w istniejącym Unity,13/13testów PASS,15próbek GPU zgodnych z PNG,12kompozycji runtime bez zmian. Obejrzano porównania i93ikony bazowe. Raport, źródła i backup: ../LocalHandoffs/item-icon-centering-20261022/README.md. Historyczne output/ niezmienione; aktualny plan w LocalHandoffs/active-item-art-import/import-plan.json. Bez Computer Use, Ctrl+R lub drugiego Editora. Pozostał smoke użytkownika w ekwipunku, handlu i craftingu. Nie commitowano/pushowano.

## Galduryty — znaki według efektów — 2026-10-22 — READY_FOR_SMOKE

Naprawiono prezentację: klucz ikony zawiera teraz kolor:EffectType zamiast samego koloru. 50 unikalnych natywnych znaków 5×7, pigment według wszystkich 10 proszków, oryginalna kolejność i powtórzenia, układy 1–6. Rozróżnienie efektów tego samego koloru; siła/stopień nadal współdzielą znak. Subtelne nacięcie bez białego obrysu i bez dziur w alfie. Stare klucze kolorowe obsługiwane dla bezpośrednich wywołań. Brak zmian serialized biblioteki, wymagania ręcznego wiring lub migracji save; statystyki i pule efektów bez zmian.

Wczytane w istniejącym Unity, 12 testów ikon + 3 testy tooltipów PASS. Obejrzano 106 renderów wszystkich efektów, pojedynczych i kompaktowych. 1276 plików PNG/meta VS22 oraz biblioteka ikon niezmienione. Bez Computer Use, drugiego Editora lub cyklicznego Refresh. Raport, mapowanie efektów i plansze: ../LocalHandoffs/galduryte-effect-runes-20261022/README.md. Pozostał smoke użytkownika: różne efekty tego samego proszku, kolory Pink/White/Gold/Amber, 6 efektów. Nie commitowano/pushowano.

## Import ikon przedmiotów C — 2026-10-22 — READY_FOR_SMOKE

Za zgodą użytkownika podmieniono i zaimportowano do istniejącego Unity 575 tekstur: 206 bazowych PNG/kopii zgodności (203 unikalne), 352 warianty biżuterii, 17 warstw składania. 569 plików zmienionych, 6 już identycznych. 575/575 meta i GUID bez zmian; ItemIconLibrary.asset bajtowo identyczna, referencje zachowane. 63 nieużywane stare warianty galdurytu niezmienione. Jedna zbiorcza prośba o import, bez Computer Use, Ctrl+R lub drugiego Editora.

8/8 VS22ItemArtTests PASS w otwartym Editorze; sprawdzone ustawienia wszystkich 575 tekstur i dane warstw runtime. Obejrzane 23 rendery Unity: 11 statycznych GPU identycznych z PNG, 6 mieszanek z odchyleniem <=1/255, 6 galdurytów zgodnych z runtime (w tym oryginalna alfa tuszu paneli). Dokumentacja, backup, plan i dowody: ../LocalHandoffs/item-art-import-20261022/README.md. Pozostał użytkowy smoke ekwipunku/craftingu/mieszanek/run; manualnego wiring nie potrzeba. Brak zmian gameplay, scen, prefabów, definicji przedmiotów i konfiguracji biblioteki. Nie commitowano ani nie pushowano.

## Biżuteria — 352 warianty w stylu C — 2026-10-22 — ART_READY_FOR_REVIEW

4 typy × 8 metali × 11 kamieni, po 88 kombinacji. Techniczny eksport Aseprite Lua z 4 zaakceptowanych baz i 11 kamieni przerysowanych wcześniej przez image_gen; bez nowych generacji. Odtworzone kolory metali, dopasowanie jasności osobno dla kształtów, zarejestrowane gniazda i palety nowych kamieni. 352 PNG 32×32 + 352 dwuwarstwowe Aseprite; dodatkowo 32 warstwy metali, 44 kamieni i 4 maski. 352/352 QA PASS, brak duplikatów, końcowe plansze wszystkich kombinacji ocenione. 638/638 istniejących PNG VS22 niezmienionych. Bez importu/Refresh/Computer Use, kodu lub serialized state; gameplay smoke NOT RUN.

Paczka, metoda, prompty źródłowe i porównania: ../output/item-jewelry-variants-restyle-v1/README.md. ZIP: ../output/item-jewelry-variants-restyle-v1/jewelry-variants-restyle-v1.zip. Łącznie przygotowane 203 bazowe ikony i 352 warianty biżuterii. Nazwy wariantów zgodne z obecnymi plikami/kluczami biblioteki, podmiana i import nadal niewykonane. Nie przebudowywano nieużywanych starych wariantów galdurytu.

## Ostatnie 7 baz — styl C — 2026-10-21 — ART_READY_FOR_REVIEW

7 indywidualnych edycji przez wbudowany image_gen: bomba, mikstura, galduryt, bransoleta, kolczyki, naszyjnik i pierścień. Eksport Aseprite 32×32, alfa 0/255, <=24kolory; zachowana dokładna natywna zajętość pod maski/gniazda/runy. 21 warstw pomocniczych (w tym niezmienione 6 masek wag i 6 paneli efektów) oraz 24 offline próby składania. Końcowe plansze ocenione. Wszystkie istniejące PNG VS22 niezmienione. Bez importu/Refresh/Computer Use, zmian kodu lub serialized state; gameplay smoke NOT RUN.

Paczka, prompty, porównania i ograniczenia: ../output/item-neutral-bases-restyle-v1/README.md. ZIP: ../output/item-neutral-bases-restyle-v1/neutral-bases-restyle-v1.zip. Łącznie 203/203 bazowe ikony przygotowane do oceny. Nie oznacza to przebudowy wszystkich 352 wariantów biżuterii: nowe neutralne bazy z istniejącymi kamieniami sprawdzono w 12 próbach rejestracji. Przy późniejszej integracji mikstur/bomb wymagane również companion shading/overlay, a galdurytu baza modularna; sama podmiana neutralnych PNG nie aktualizuje runtime.

## Wyposażenie i użytkowe — 19 ikon w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Kontynuacja po kamieniach: 4 ikony broni, 4 pancerze i 11 przedmiotów użytkowych/materiałów. 19 indywidualnych edycji przez wbudowany image_gen. Eksport Aseprite 32×32, alfa 0/255, do 24 kolorów bez ditheringu. 19/19 kontroli PASS; końcowe plansze 1×/2×/3× ocenione. Wszystkie 26 pozostałych PNG Assets niezmienionych. Bez importu/Refresh/Computer Use i zmian serialized state. Brak ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, prompty i porównania: ../output/item-equipment-utility-restyle-v1/README.md. ZIP: ../output/item-equipment-utility-restyle-v1/equipment-utility-icons-restyle-v1.zip. Łącznie 196/203 bazowych ikon przygotowanych. 7 neutralnych baz (bomba, mikstura, galduryt, bransoleta, kolczyki, naszyjnik, pierścień) odłożonych do skoordynowanego przeglądu z warstwami modularnymi. Warianty biżuterii i grafika modularna pozostają osobnym zakresem. Ta paczka NIE kończy całego katalogu.

## Kamienie i proszki — 21 ikon w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Kontynuacja po akceptacji drewna: 11 indywidualnych kamieni i10 wariantów proszku ze wspólnej nowej sylwetki, 12 rysunków przez wbudowany image_gen. Trzy kopie plików proszków pod drugimi nazwami zachowane identycznie bajtowo: 21 unikalnych ikon, 24 PNG i24 Aseprite. Eksport32×32/alfa0–255/do24 kolorów (proszki do16), bez ditheringu. 21/21 unikalnych ikon i3/3 kopie PASS; końcowe plansze1×/2×/3× ocenione. 24/24 PNG w Assets niezmienionych. Bez importu/Refresh/Computer Use, zmian kodu, statystyk, definicji, GUID lub meta. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty i porównania: ../output/item-gems-restyle-v1/README.md. ZIP: ../output/item-gems-restyle-v1/gems-icons-restyle-v1.zip. Łącznie przygotowano177/203 bazowych ikon;26 pozostaje do przeglądu. Biżuteria i warstwy modularne osobno.

## Drewno — 24 ikony w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Kontynuacja po akceptacji tekstyliów: 8 bali drewna, 8 desek i 8 kijów. 24 indywidualne edycje przez wbudowany image_gen; odrębne sylwetki, kora i kolory gatunków zachowane. Eksport Aseprite 32×32, alfa 0/255, do 24 kolorów, bez ditheringu. 24/24 kontroli PASS, w tym ciągłość kijów; końcowe plansze 1×/2×/3× ocenione. 24/24 PNG w Assets niezmienionych. Bez importu/Refresh/Computer Use, zmian kodu, statystyk, definicji, GUID lub meta. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty i porównania: ../output/item-wood-restyle-v1/README.md. ZIP: ../output/item-wood-restyle-v1/wood-icons-restyle-v1.zip. Łącznie przygotowano 156/203 bazowych ikon; 47 pozostaje do przeglądu. Biżuteria i warstwy modularne osobno.

## Tekstylia — 24 ikony w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Kontynuacja po akceptacji skór: 8 włókien, 8 nici i 8 tkanin. 24 indywidualne edycje przez wbudowany image_gen oraz 3 dodatkowe korekty splotu (owcza tkanina v2/v3 i juta v2); odrębne sylwetki i kolory materiałów zachowane. Eksport Aseprite 32×32, alfa 0/255, do 24 kolorów, bez ditheringu. 24/24 kontroli PASS; końcowe plansze 1×/2×/3× ocenione. 24/24 PNG w Assets niezmienionych. Bez importu/Refresh/Computer Use, zmian kodu, statystyk, definicji, GUID lub meta. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty i porównania: ../output/item-textile-restyle-v1/README.md. ZIP: ../output/item-textile-restyle-v1/textile-icons-restyle-v1.zip. Łącznie przygotowano 132/203 bazowych ikon; 71 pozostaje do przeglądu. Biżuteria i warstwy modularne osobno.

## Skóry — 24 ikony w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Użytkownik zaakceptował paczkę kowalską i zlecił kolejne ikony. Osobna paczka: 8 skór surowych, 8 garbowanych i 8 pasków. 24 indywidualne rysunki przez wbudowany image_gen, zachowane odrębne sylwetki i materiały. Eksport Aseprite 32×32, alfa 0/255, maksymalnie 24 kolory, bez ditheringu. 24/24 kontroli PASS; końcowe plansze 1×/2×/3× ocenione. 24/24 PNG w Assets niezmienionych. Bez importu/Refresh/Computer Use i zmian runtime, statystyk, definicji, GUID lub meta. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty i porównania: ../output/item-leather-restyle-v1/README.md. ZIP: ../output/item-leather-restyle-v1/leather-icons-restyle-v1.zip. Łącznie z alchemią i kowalstwem przygotowano 108/203 bazowych ikon; 95 pozostaje do przeglądu. Biżuteria i warstwy modularne osobno.

## Kowalstwo — 48 ikon w stylu C — 2026-10-21 — ART_READY_FOR_REVIEW

Użytkownik zaakceptował paczkę alchemii i zlecił kolejne ikony. Przygotowano osobno 48 ikon kowalskich: 8 surowców i 5 rodzin po 8 metali. 13 nowych rysunków przez wbudowany image_gen, z nich 40 spójnych wariantów istniejących kolorów metali. Eksport Aseprite 32×32, alfa 0/255, do 24 kolorów (warianty do 16), bez ditheringu. 48/48 kontroli PASS, maski identyczne wewnątrz rodzin. Końcowe plansze 1×/2×/3× ocenione. 48/48 obecnych PNG Unity niezmienionych. Bez importu/Refresh/Computer Use i zmian runtime, statystyk, definicji lub metadanych. Brak wymaganego ręcznego Editor wiring; import/gameplay smoke NOT RUN.

Paczka, pełne prompty, pochodzenie i porównania: ../output/item-smithing-restyle-v1/README.md. ZIP: ../output/item-smithing-restyle-v1/smithing-icons-restyle-v1.zip. Łącznie z alchemią przygotowano 84/203 bazowych ikon, 119 pozostaje do przeglądu; biżuteria i warstwy modularne osobno.

## Kierunek ikon przedmiotów — 2026-10-21

Użytkownik wskazał wariant C (nowe uproszczone źródło image_gen + wspólny eksport) jako wyraźnie lepszy od samego przeskalowania B. Zachować tożsamość przedmiotu, dominujące kolory materiału i orientację; upraszczać mikrorefleksy/fakturę w większe grupy cieni. Przy pierwszej paczce nie zmieniać rozmiaru przedmiotu na płótnie wyłącznie dla korzystniejszego porównania. Same kości nie wymagają tak daleko idącego uproszczenia jak zaszumione prochy.

Przygotowane36 ikon alchemicznych w ../output/item-alchemy-restyle-v1:32 nowe edycje+4 zaakceptowane próby, final32×32/alfa0–255/do24 kolorów, plansze1×/2×/3×. Wykryty i poprawiony dryf koloru4 prochów oraz słaby kontrast2 prochów strzelniczych. Status ART_READY_FOR_REVIEW; PNG projektu nadal niepodmienione, żadnych zmian runtime/asset definitions/metadata. Pełne prompty i pochodzenie w README/manifest.json/generation-provenance.json. Pozostałe167 bazowych ikon wymagają osobnego przeglądu; nie zakładamy, że wszystkie trzeba regenerować.

## Wspólne obliczanie odporności — 2026-10-21

Character.Resistances to pochodny snapshot, BaseResistances to surowa baza. Nie wolno dodawać GetModifier do snapshotu przy obliczaniu gameplay. ModifyResistanceSystem.GetEffectiveResistance sumuje bazę, aktywne ResistanceModifierPayload specific/All, bonus ognia maga, aktywne sockety galdurytowe i legacy słownik; dodatkowa tolerancja dotyczy tylko aplikacji statusu. Clamp wykonywany po pełnej sumie. Statusy, przesunięcia i ticki DoT korzystają ze wspólnego resolvera; redukcja obrażeń z galdurytów pozostaje osobną mechaniką.

Lifecycle odświeża statystyki również dla samych modyfikatorów odporności. Celność synchronizowana niezależnie od zasobu. Brakująca mana maga liczona z bieżącego MaxResource z identycznym zaokrągleniem jak ResourcePool. Fizyczna penetracja Analizy w wspólnym ApplyDefense dla wykonania i preview, naliczana raz. Save schema bez zmian; odczyt przelicza bazę definicji i odtworzone efekty/EQ.

650/650 testów PASS;3 JSON roundtrip nie utrwalają bonusu w bazie. Po poprawce te same budżetowe pułapki naL48 średnio9.30/12.98/3.77%HP (kolce/gaz/pieczęć), zamiast9.19/12.64/3.77. To pełny conditional koszt bez leczenia/oczyszczenia; bez zmiany parametrów pułapek. Szczegóły: ../LocalHandoffs/stat-calculation-fixes-20261021/REVIEW.txt. Zastępuje poprzednie ostrzeżenie o nienaprawionej podwójnej odporności.

## Audyt pułapek eksploracji i niespójność odporności — 2026-10-21

Pułapki środowiskowe skalują raw hit/tick poziomem wyprawy przezbase*(1+.065*cumulativeGrowth), niezależnie od mocy alchemicznej. Kolce/sidła:uderzeniefizyczne+3ticki bleed;gaz/purchawki:5ticków poison bez uderzenia;pieczęć/korzenie:magiczne uderzenie+4turyweakening. Produkcyjne3 pary biomów mają równe parametry. Koszt HP legalnych budżetowych bohaterów naL48:średnio9.19/12.64/3.77%,mag16.73/22.37/4.38%. To pełny czas po aplikacji, bez leczenia/oczyszczenia; expecteduwzględniający statuschance zapisany osobno.

ZNANY NIENAPRAWIONY PROBLEM:CharacterStatPipelineSystem.RefreshResistances dodaje persistent ResistanceModifierPayload do Character.Resistances, po czymStatusEffectSystem.GetResistance iDamageOverTimePassiveEffectProcessor ponownie dodają go przezModifyResistanceSystem.GetModifier. Potwierdzone na projekcji bransoletyT40:+7pp daje faktycznie+14pp;Alisonbaza25%,character32%,resolver39%,154rawtick=>188HP zamiast209 przy32%. Nie zmieniać balansu na podstawie samego tooltipowego oporu bez uwzględnienia tego problemu. Użytkownik zlecił audyt bez zmian; kod/assetów nie poprawiano.

Główny model:12 historii ekonomicznychloot25, firstreachlevel, stopniowe zakupy/reforge/upgrade, boss30%co4levele,2 komplety biżuterii/4postacie. Typ bransolety stały:Wladimir/Alisonpoison,Jerzy/Reimondbleed. Wyższe tiery nadalprojekcje danych projektowych, nie gwarantowana oferta całego runtime. Rozszerzono model doL50,2880 stanówL1–48 reprodukuje poprzedni model. Bez potworów w wynikach końcowych. Raport: ../LocalHandoffs/trap-health-audit-20261021/REPORT.txt.

## Wdrożony balans alchemii — 2026-10-21

Zastępuje poniższy historyczny audyt: bazowa celność fizycznych bomb100 przeciw unikowi, bez celności wyposażonej broni. Pozostają osobne bonusy szansy trafienia/Hawkeye, a magiczne zachowują zwykłą gwarancję trafienia. Power(level) doL30 zgodne z dotychczasową referencją; powyżej344.5625+19.1875*d+1.17921875*d², d=level-30, limitL50=1200. Wybuch nadal odwraca model obrony; DoT liniowyceil(power*statusMultiplier), potem istniejące odporności i dozwolone bonusy klasowe. Nie używać przelicznika odwracającego pancerz do DoT, który już omija obronę.

Wspólna wydajność składników obrażeń natychmiastowych w tej samej grupie celów:1=>100%,2=>75%,3=>60%, niezależnie od mieszania typów. Scalanie powtarzanych efektów pozostaje; wybór typu nadal zależy od obrony/uniku. Przykłady:.225 siły dla2P,.1125+.1125 dla1P1M,.18+.09 dla2P1M. Efekty dodatkowe i DoT zachowują dotychczasowe reguły powtórzeń/czasu. Nie obiecywać identycznego DPS fizycznych/magicznych na wszystkich celach.

Runtime bomby ze starego zapisu odbudowują akcję według nowych zasad bez migracji. Już nałożone DoT zachowują zapisany totalDamage do wygaśnięcia. Zwykłe skille nadalAverageAttack. Tooltip/AI alchemicznego DoT używają tego samego helpera co nakładanie. 266/266 testów PASS,991 serialized plików identycznych; raport: ../LocalHandoffs/alchemy-balance-20261021/REVIEW.txt.

## Moc alchemiczna — audyt bez zmiany balansu — 2026-10-21

Bezpośrednie obrażenia bomb bazują na poziomie rzucającego i zapisanej tabeli ATK referencyjnego, nie wyposażonej broni. Kalibracja40%/60% normalnego trafienia dotyczy obrony równej referencji, nie gwarantowanego udziału w HP rzeczywistych wrogów. Craft wymaga trzech różnych materiałów; obecnie są tylko dwa dające fizyczne i dwa magiczne obrażenia. Nie używać nielegalnych receptur3P/3M w ocenie balansu. L45–50 referencja rośnie tylko5.75%, więc po obronie średnie obrażenia maleją; mieszane1P1M z tym samym wolnym miejscem na efekt wyprzedza2P o około40%. Fizyczne nadal używają celności broni; zmiana na stałą bazę100 nie została wdrożona w tym audycie. DoT bomb nadal opiera się na AverageAttack, co wymaga osobnej decyzji. Pomiary neutralne bez SP/kryta/DoT i bez wag encounterów; raport: ../LocalHandoffs/alchemy-power-audit-20261021/REVIEW.txt. 3276 podglądów i180 aplikacji obrażeń zgodnych,991 serialized plików bez zmian.

## Wspólna głębia NPC i drużyny w mieście — 2026-10-21

ArungardCityDepthPreview obejmuje drużynę i NPC z jawnych referencji ExplorationSandboxBootstrap. Przy starcie ArungardPresentationRuntimeSetup dopina brakujące root SortingGroups oraz rejestruje wszystkie bindingi, również nieaktywne postacie drużyny. Niższe stopy na ekranie oznaczają rysowanie przed wyższymi. Zostawić wewnętrzne body/workstation/foreground ordery; nie używać stałego globalnego orderu dla NPC. Rejestracja idempotentna, bez skanowania sceny co klatkę; sortowanie nadal w istniejącym LateUpdate miasta. Kod nie zapisuje zmian scen ani prefabów. 22/22 testy PASS; rendery przed/za Kapłanem sprawdzone. Raport: ../LocalHandoffs/city-npc-depth-20261021/REVIEW.txt.

## Prezentacja zasobów i ticków eksploracji — 2026-10-21

Tymczasowo nie pokazywać dodatkowego tooltipa przy hoverze paska zasobu (mana/furia/impet). Pozostawić wspólny glosariusz i istniejący serialized wiring; InitializeResource jedynie wycisza to źródło, zwykłe tooltipy nadal działają. Ta decyzja zastępuje wcześniejsze wymaganie dodatkowej karty paska.

Otwarty tooltip czasowego efektu poza walką pokazuje aktualne pozostałe sekundy również bez zmiany hoveru i po przypięciu. Aktualizować tylko zmieniony licznik. Radialne czarne przyciemnienie55% na ikonie odsłania się od godziny12 zgodnie z ruchem wskazówek przez wspólny sześciusekundowy tick postaci; reset po ticku, bez niezależnego zegara UI. Tylko czasowe efekty eksploracji, bez sekundowego odliczania w walce. 188/188 testów PASS; raport: ../LocalHandoffs/resource-status-tooltip-20261021/REVIEW.txt.

## Impet z ruchu i rozszerzenie +40% na bossów — 2026-10-21

Aktualna reguła impetu: kolejne pełne kratki rzeczywistego ruchu dają2,4,6… niezależnie od kosztu AP. W walce licznik istniejącego MovementStepsThisTurn resetuje się na początku własnej tury; tura bez ruchu odbiera10. Poza walką limit50, 6 sekund bez ruchu odbiera10 i resetuje serię; następne6 sekund odbiera kolejne10. Krótkie zatrzymanie zachowuje serię, ułamkowa odległość jest kumulowana. Przenosić zdobytą pulę gracza do walki; profile startowe NPC, połowa generacji ponad100, progi szybkości, odzysk sztyletów i blokady zachowane. Bez naliczania za teleport/setup/animację walki. Stan serii i bezruchu przejściowy per CharacterInstance, nie nowy globalny zegar.

+40% zaokrąglonych baz obejmuje teraz też Króla Umarłych, Szkieletowego Pogromcę, Enta i Herszta oraz ich fazy; wcześniejsza notatka o wyłączeniu bossów jest historyczna. Growth i offsety bez zmian; przywołany rycerz3HP zachowuje mechaniczną pulę. 773/773 testów PASS. Raport: ../LocalHandoffs/boss-base-and-exploration-momentum-20261021/REVIEW.txt.

## Baza zwykłych przeciwników +40% — 2026-10-21

Zaakceptowany po porównaniu wykresów wariant dotyczy14 zwykłych typów obu lochów. EnemyScalingBalanceSetup.OrdinaryBaseStatMultiplier=1.4 jest nakładany na wcześniejszą zaokrągloną bazę .44HP/.63ATK. Growth/early/late i offsety zachowane; nie przeliczać kompensacji tak, aby skasowała ten dodatek naL48. Przyrost pozostaje stałą liczbą punktów na wszystkich poziomach. Po zaokrągleniu średnioL1 +40%, L48 około+0.71%HP/+0.83%ATK. Bossowie/fazy i3HP rycerz nieobjęte. Utrzymać idempotencję importu i domyślne dane testowego kandydata zgodne z produkcją; dawne eksperymenty historyczne jawnie korzystają z mnożnika1.0.

633/633 testów regresji PASS,14/889 assetów zmienione tylko w3 bazach. Zapisane bieżące HP są zachowywane bez darmowego leczenia; do oceny pełnego zdrowia nowe spawny/wyprawa. Raport: ../LocalHandoffs/enemy-base-plus40-20261021/REVIEW.txt.

## Aktualność testów HP/ATK i Enta — 2026-10-21 — VERIFIED

28 wcześniejszych porażek rozstrzygnięte: stare bazy sprzed kalibracji i testowe puleHP, nie potwierdzone defekty runtime. Testy fazy Enta wyprowadzają próg50% z rzeczywistego maxHP; nie zakładają700HP z dawnej bazy1400. Testy mnożników obrażeń/leczenia mają jawne puleHP, aby mierzyć mnożnik bez przypadkowego overkill/overheal. Korekta wyłącznie testowa;534/534 zakresu glosariusz/AI/NPC/resources/damage/save PASS. Raport: ../LocalHandoffs/legacy-test-audit-20261021/REVIEW.txt.

## Glosariusz i wycena AI — 2026-10-21 — READY_FOR_SMOKE

Pełne zasady zasobów pozostają w glosariuszu; paski pokazują tylko stan i link do właściwego hasła. Glosariusz rozróżnia regenerację zasobu od regeneracji HP i zawiera aktualny AP-zależny odzysk many bohaterów oraz odrębne reguły NPC/eksploracji. Produkcyjne 87 wpisów nie ma pustych opisów; zachowane ikony. Resource bar hover jest podpięty w produkcyjnych prefabach przez Editor API.

Lista AI zakończona 16/16. Wycena deterministyczna i ograniczona: prawdopodobieństwo dobicia zamiast uznawania średniej za pewny kill, brak overheal, ochrona względem widocznego zagrożenia, lokalny focus i ruch z AP na akcję, pułapki względem podejścia. Brak odczytu niewidocznych przeciwników podczas oceny zagrożenia. Zachowane fazy bossów, zasady i koszty. All/IncludesCaster obowiązuje w runtime; niewidzialni sojusznicy mogą otrzymywać efekty grupowe. To heurystyka, nie pełna symulacja czy gwarancja optimum.

264/264 testy głównego zakresu PASS; szerszy zestaw 505/533 z 28 wcześniejszymi błędami, bez nowych. Gameplay/FPS NOT RUN. Dowody: ../LocalHandoffs/glossary-ai-completion-20261021/REVIEW.txt.

## Mana bohaterów — 2026-10-21 — READY_FOR_SMOKE

Mag i paladyn odzyskują naturalną manę raz na końcu własnej tury: round(regeneracja × 2 × clamp(pozostałe AP / maksymalne AP, 0, 1)). 100%/50%/25%/0% pozostałych AP daje 200%/100%/50%/0% regeneracji. Tick odbywa się przed wygasaniem efektów końca tury, więc blokada odzysku działa również na ostatniej turze. Bez naturalnego ticka na początku tury. NPC i sojusznicze przywołania AI zachowują dotychczasowy tick początkowy. Poza walką zwykła regeneracja co 6 sekund, bez mnożnika AP. Osobne efekty odzysku z EQ/skilli pozostają osobne.

Bazowa regeneracja maga 15 (poprzednio 20), paladyna nadal 15. Czarnoksiężnik: −0,5% kosztów many za SP, nadal +1% obrażeń przeciw pojedynczemu celowi. Odzyskiwanie Many: +0,25%/+0,5% regeneracji za każdy brakujący 1% many, maksymalnie +25%/+50%. Talent przelicza brakującą manę po wydatkach, przed tickiem. Koszty bazowe skilli/AP, różdżki, Ekonomia, limity i start zasobu pozostają bez zmian. Bez nowego pola save; odczyt przelicza bazę ze współczesnej definicji postaci.

275/275 odrębnych synchronicznych EditMode PASS, w tym rzeczywisty Fireball i Heal, pełna/częściowa/pusta pula AP, blokady, overflow, brak podwójnego ticka, eksploracja, UI i save/load. Kompilacja Systems/Views/Editor/EditModeTests i self-review PASS. Automatycznie wczytane w otwartym Unity, bez ręcznego Refresh/Computer Use. Manual gameplay i pełne wyprawy NOT RUN. Dowody: ../LocalHandoffs/mana-package-20261021/REVIEW.txt.

## Sztylet na ziemi bez animacji — 2026-10-20 — READY_FOR_SMOKE

Całkowicie usunięto animację lądowania, także poprzedni ruch skokowy. Sztylet od razu leży w pozycji docelowej; hover jest dostępny natychmiast. Bez zmian pozy rzucającej postaci, obrażeń, zużycia/podnoszenia, fog, grafiki i skali. 60/60 testów PASS; obejrzane pięć identycznych stanów renderu. Wczytane automatycznie, bez ręcznej konfiguracji, Refresh i Computer Use. Dowody: ../LocalHandoffs/knife-static-20261020/REVIEW.md. Manualny gameplay/FPS NOT RUN, bez commit/push. Zastępuje wcześniejszą zasadę lądowania w trzech pozycjach.

## Prezentacja akcji walki — 2026-10-20 — READY_FOR_SMOKE

Naprawione: nazwa/poza skilla ujawniającego przeciwnika (również Materializacja upiora), pierwszy cast/hit po wejściu do walki i wiązanie HUD przed openingiem, kasowanie pozy przez zerowy krok oraz zbyt wczesne usuwanie reakcji po śmiertelnym trafieniu. Lądowanie sztyletu: 0,24 s, trzy utrzymywane pozycje bez płynnego łuku i obrotu. Bez zmian kosztów/obrażeń/niewidzialności/AI/save. Poprawione wspólne sprzątanie rendererów oraz legalne zasoby dwóch fixture'ów testowych.

532/532 odrębne testy PASS; finalne rendery czterech bohaterów cast/hit, śmiertelnego trafienia i etapów sztyletu obejrzane. Audyt ośmiu kierunków i melee/ranged bez brakujących mapowań. Wczytane automatycznie w istniejącym Unity, bez ręcznej konfiguracji, Refresh, Computer Use, serialized edits i commit/push. Manualny gameplay/FPS NOT RUN. Smoke: pierwszy skill, śmiertelny hit, Materializacja/atak z ukrycia, rzucanie/odzysk sztyletu. Dowody i szczegóły: ../LocalHandoffs/combat-action-presentation-20261020/REVIEW.md.

## Pasek wytrzymałości przeszkód — 2026-10-20 — READY_FOR_SMOKE

Fill ograniczony do wnętrza istniejącej ramki (wcięcie i maska), większy pasek z czytelną etykietą i ciemnym tłem. Pozycja nad górą grafiki przeszkody z uwzględnieniem przezroczystego marginesu, zamiast stałego offsetu od komórki. 95/95 odrębnych testów PASS; końcowe 5 przypadków renderu ponowione PASS. Obejrzane porównania przed/po dla 1/3, 2/3 i 3/3 oraz dwóch skal Canvas. Wczytane automatycznie w istniejącym Unity, bez ręcznej konfiguracji/Refresh/Computer Use. Murki i rozmieszczenie filarów bez zmian. Smoke: hover i niszczenie filaru/gruzu, różne wartości i zoom. Dowody: ../LocalHandoffs/obstacle-health-20261020/REVIEW.md. Bez commit/push.

## Niskie murki — dedykowany arkusz sprite'ów — 2026-10-20

Aktualnym źródłem niskich murków jest Resources/CatacombsAudit/catacombs-low-wall-sheet-v1.png, z pełnym litym kamieniem, fazowaną nakrywą, ciemniejszym licem i bocznym powrotem. Poprzednie wycinki wysokiej ściany/generowana faktura/ramki nie były zaakceptowanym wyglądem i nie są już używane w low-wall. Kod składa gotowe UV sprite'y arkusza; nie tworzy nowych tekstur/faktury w runtime. Pion ma prosty powtarzany fragment12x16 i osobne końce; pozioma nakrywa jest skrócona perspektywicznie do6 px, lico12 px i front18 px. Wspólny tint0,75/0,85/1 dopasowuje materiał do katakumb. Zachowano kontakt1 px alpha70/255 bez dublowania, głębię, fog i przycięcia styku z pełną ścianą. Gameplayowe korzenie/collidery i logiczna mapa bez zmian, wygaszanie koron bez zmian.

Finalne rendery GPU obu kierunków/końców, wszystkich L/T/krzyża, podłogi, ścian i postaci po obu stronach ocenione po korektach, także przy pochodniach. To kontrolowany harness, nie pomiar FPS lub zapis żywej gry. Dowody i prompty: ../LocalHandoffs/low-wall-sprites-20261020/REVIEW.md. Poprzedni balkon poza końcową korektą.

## Drzwi — interakcja w walce — 2026-10-20

Aktywny bohater w legalnym zasięgu może używać LPM po widocznym spricie zamkniętych drzwi lub otwartego skrzydła; hover pokazuje istniejący outline. Wspólny owner inputu obsługuje również E, a klik drzwi nie przechodzi do ruchu. Koszt i zakończenie tury pozostają w CombatExecutionDriver/CombatInteractionSystem. Podczas celowania skillem drzwi nie przejmują inputu. Bez darmowego przejścia do interakcji eksploracyjnej.

## Niewidzialność — pełne ukrywanie przeciwników — 2026-10-20 — READY_FOR_SMOKE

Niewidzialny wróg nie ma sprite’a, akcentów, hoveru/inspekcji ani markera minimapy; Known ukrywa portret/HP/szczegóły w kolejce. Kamera nie odczytuje ukrytej pozycji podczas ruchu lub kadrowania. Sojusznicy pozostają widoczni z alfą45%, bez jej narastania; poprawny powrót po ujawnieniu i po zwolnieniu combat binding. Reguły targetingu/AI/break/obrażeń bez zmian: kierowane w teren wzory nadal trafiają niewidzialne cele, ale UI nie podświetla ich pól ani podglądu jednostki. Nie dodano skilla wykrywania. Widoki respetkują mgłę/elevation i permanent invisibility bossa. Wygaszanie koron/drzewa bez zmian runtime.

227/227 testów prezentacji + 67/67 statusów/glosariusza PASS. Dodatkowo 173/175 regresji NPC: dwa stare oczekiwania bazowego HP (500 vs200 i350 vs154), niezwiązane z niewidzialnością; statystyk nie zmieniano. Zaktualizowany wyłącznie stary test statycznych drzew do wcześniejszej reguły braku znikania w fog. Izolowane piksele GPU i obrazy PASS, self-review/whitespace PASS. Import automatyczny, bez Refresh/setupu/migracji, drugiego Unity, agentów lub serialized edits. Manual gameplay/FPS NOT RUN, bez commit/push. Smoke: niewidzialny bandyta/upiór, brak hoveru/minimapy/śledzenia, AoE w teren, ujawnienie; własny Zwiadowca pozostaje półprzezroczysty. Dowody/ograniczenia: `../LocalHandoffs/invisibility-20261020/REVIEW.md`.
## Generowanie lochów — 2026-10-20 — READY_FOR_SMOKE

Skrócono dobór połączeń ścian katakumb przez indeks narożników zamiast porównań wszystkich par. Generator ponownie używa krawędzi, poziomów i walidacji szablonu w obrębie jednego Generate; pamięć wyników nie przechodzi między wyprawami. Zachowano RNG, layout, zawartość, precyzję nawigacji, warstwy i wygaszanie koron.

Te same długie wyprawy, seedy 171026 i 6, L10, produkcyjny harness w istniejącym Unity: Katakumby 15,89 → 5,63 s oraz 24,60 → 7,51 s; Las 8,96 → 6,60 s oraz 8,92 → 7,64 s. Cała próba obejmuje kontrolę manifestu i zapytania testowe; nie jest pomiarem FPS. Generator tworzy wszystkie piętra, widoki pierwszego. Zgodność manifestów i dostępności próbkowanych tras przed/po; 152 odrębne testy EditMode PASS, self-review/whitespace PASS. Import automatyczny, bez ręcznego Refresh/drugiego Unity/agentów i serialized edits. Brak manualnego setupu/migracji; bez commit/push. Smoke: nowa długa wyprawa Katakumb, ściany/narożniki, drzwi i ruch, następnie Las. Dowody i ograniczenia: `../LocalHandoffs/dungeon-generation-20261020/REVIEW.md`.
## Otwarcie walki bez PPM — aktualizacja 2026-10-20

Zastępuje wcześniejsze wejście PPM → Przygotuj atak w PT-15: w lochu pasek aktywnych skilli lidera jest stale widoczny w eksploracji. Klik ikony lub 1–0 uruchamia wybór celu; Esc/PPM anuluje celowanie i wraca do ruchu, pasek zostaje. Menu i zmiana lidera anulują preview. Kolejka tur/jej ramka i przyciski bojowe pozostają ukryte poza walką. Sam pasek nie tworzy pełnej siatki/sesji walki; preview powstaje na żądanie. Akcja nadal musi legalnie rozpocząć walkę: zwykła walidacja, koszty pierwszej tury, normalne dołączanie, alarm i osobne AV wykrycia. Dowolny aktywny skill zachowany; nie dodano darmowego rzucania umiejętności poza walką. Aktualny cel skilla określa źródło otwieranej walki. Nie zmieniono balansu ani statusów. Implementacja i 220 testów PASS; gameplay smoke/FPS NOT RUN.

Korekta zleceń 2026-10-20: nowe tablice obejmują oba dostępne typy lochu przez preferowanie najmniej reprezentowanego typu. Cel „ukończ” z aktualnych wypraw nie przekracza poziomu aktywnej drużyny. Nazwy „oczyść” bez mało/średnio/dużo; liczby celów i nagrody bez zmian. Stare nieprzyjęte oferty powyżej poziomu i pełne tablice jednego typu naprawiane raz, bez zmiany przyjętych zleceń/nagród ani odnawiania odebranych miejsc przed kolejnym ukończeniem lochu.

## Aktualne zasady tablic — 2026-10-20

Ten wpis zastępuje starsze opisy trzech stałych zleceń Katakumb i nieznanej z góry nagrody ×1–3. Tablica wypraw ma 6 unikalnych kombinacji rzeczywistego poziomu, DungeonTheme i DungeonLength; boss ma pierwszeństwo, kolejka bossów nie jest tracona przy usuwaniu duplikatów. Poziomy względne nadal reagują na skład drużyny.

Zlecenia mają 3 miejsca łącznie, obejmujące przyjęte i ukończone czekające na nagrodę. Losowe rodzaje: „oczyść” 8–12 / 16–24 / 24–36 przeciwników wskazanego typu lochu oraz „ukończ” konkretną długość i typ obecne na aktualnej liście wypraw w momencie losowania. Poziom „oczyść” pochodzi ze średniej aktywnej drużyny; „ukończ” z wybranej oferty. Zachowana minimalna granica poziomu: równy lub wyższy. Nagrody XP/złoto/honor są mnożone odpowiednio ×1/×2/×3 przy generowaniu i widoczne jako gotowe liczby; przedmiot bonusowy pojedynczy. Brak etykiety „Nagroda ×1–3” i brak ponownego mnożenia po wyjściu.

Tylko udane ukończenie lochu uruchamia nowy cykl nieprzyjętych/zwolnionych miejsc. Aktywne i oczekujące na odbiór zostają. Odebrana nagroda zwalnia miejsce do następnego cyklu; ponowne otwarcie/porzucenie nie losuje od nowa. Parametry i nagrody są zapisywane. Stare przyjęte zlecenia i nagrody zachowują cele oraz postęp; nowe parametry są runtime stanem slotu, nie modyfikują ScriptableObjects. Future quest/objective types nie są jeszcze implementowane.


## Status dokumentu

### PT-06 — korekta wczesnego zagrożenia — 2026-10-18

Aktualna kalibracja zwykłych NPC zastępuje poprzednie wczesne wartości: baza HP ×0,44 zamiast ×0,40 oryginalnej bazy (około +10%), ATK ×0,63 zamiast ×0,60 (około +5%), wczesny przyrost HP/ATK ×1 zamiast ×0,6. Mocniejsze bazy odrzucono po symulacjach całych wypraw, ponieważ na L1 wyczerpywały drużynę przed końcem serii. Wzmocnienie płynnie wygasa według dotychczasowego q: płaski offset kompensuje dodatkową bazę, a wczesna różnica przyrostu ma wagę C×(1−q). Bieżące wartości L48 i L50 pozostają identyczne; końcowy przyrost HP×2/ATK×2,5 bez zmian. Globalne zakresy/współczynniki wzrostu 1/2/3/5/9 pozostają w tym passie bez zmian, ale użytkownik dopuścił ich dalsze strojenie.

14 zwykłych definicji zmienionych przez Editor API. Bohaterowie, drzewka, wyposażenie, odporności, obrony, zasoby, nagrody oraz własne statystyki bossów/faz i rycerz 3HP chronione. Zwykłe przywoływane drzewa korzystają z krzywej zwykłych NPC. Opcjonalne płaskie offsety w CharacterGrowthData mają domyślnie zero; format save bez zmian, restore nie leczy zapisanych wrogów. Pełny efekt nowych maksymalnych HP dotyczy nowo tworzonych przeciwników.

READY_FOR_SMOKE: 137/137 testów końcowych + 4/4 sweep; 144 wyprawy / 1421 walk przed/po na tych samych trasach i seedach. L1 full clear 7/8 → 6/8 (końcowe HP 46% → 34%), scouting 7/8 → 5/8 (51% → 31%); nowe seedy 8/8 → 7/8. L5–50 wszystkie planowane serie ukończone. 16 par L48/L50 ma identyczne działania, HP, zasoby i wyniki. Legalne drzewka, najwyższa legalna jakość wyposażenia, Common, bez ulepszeń; realne ograniczenia obozów/leczenia/zasobów/XP, brak darmowego resetu HP. Stała arena i osobna polityka bohaterów; nie jest to test wszystkich buildów/geometrii ani estymacja stabilnego win rate. Wysokie progi materiałów nadal z read-only referencji arkusza, produkcyjnego importu części nie rozszerzano. Bez ręcznego Refresh, skryptów Node, agentów, commit/push. Dowody: `LocalHandoffs/early-enemy-threat-20261018/HANDOFF.md`, `verification.json`, `tests-final.xml`, `tests-grid.xml` i raporty przed/po. Manual gameplay/FPS NOT RUN.

### Otwarte uwagi z playtestu — 2026-10-15

Pełna lista15 zapisanych tematów PT-01–PT-15 znajduje się na początku [KANBAN.md](KANBAN.md), w sekcji „Uwagi z playtestu”. Obejmuje poprawki VFX/lasu/QoL, balans, lądowanie sztyletu i inicjowanie walki z zaskoczeniem przy normalnym dołączaniu obu stron. PT-01 zaimplementowane w passie VFX6, PT-11 w passie lasu, PT-09 w passie camping kitu/tooltipów, PT-10 w passie dwukliku, PT-12 w passie skrzyń oraz PT-13 w passie modeli kart i PT-07 w passie licznika sztyletów oraz PT-08 w passie XP/punktów rozwoju i PT-14 w passie lądowania sztyletu oraz PT-05 w passie nazewnictwa odporności na kontrolę i PT-03 w passie nagrody za odzysk sztyletów oraz PT-02 w passie Magicznej Tarczy i PT-04 w passie zasobów przeciwników — READY_FOR_SMOKE, manual smoke NOT RUN. PT-15 również zaimplementowane — READY_FOR_SMOKE; PT-06 po symulacjach całych wypraw —518/518 testów PASS, READY_FOR_SMOKE. Wszystkie15 tematów mają implementację; manualny playtest nadal NOT RUN; PT-06 ma automatyczną ocenę pełnymi symulacjami. Regresja skali z PT-11 skorygowana na żądanie użytkownika: autorskie0,7, bez powiększania aktorów;144/144 testów PASS, manual smoke NOT RUN. Przed wyborem kolejnej karty sprawdzić tę sekcję.



## Furia — zysk za użycie skilla ofensywnego — 2026-10-16

READY_FOR_SMOKE.590 aktualnych odrębnych testów PASS;14 wypraw/95 walk bez limitów,11/14 ukończone. Wyniki obejmują porażki, nie wymagają gwarantowanej wygranej. Manual gameplay NOT RUN; bez ręcznego Refresh/commit/push.

Obowiązująca decyzja użytkownika: wojownik i wszyscy przeciwnicy z furią otrzymują bazowo10 furii **raz za użycie skilla ofensywnego**, niezależnie od liczby celów, efektów i trafień, również przy pudle/uniku. Koszt jest sprawdzany i pobierany przed zyskiem; przyszłe10 nie finansuje niedostępnego skilla. Klasyfikacja ofensywna korzysta ze wspólnego resolvera efektów kierowanych na przeciwników, więc obejmuje również ofensywną kontrolę. Sam buff/przyspieszenie/przygotowanie na rzucającym nie generuje furii. Późniejsze trafienia opóźnionego ataku, obrażenia okresowe, kontrataki i automatyczne kopie nie naliczają ponownie bazowej furii. Jawne bonusy talentów/ekwipunku zachowane.

SkillExecutionSystem jest jedynym ownerem bazowego zysku podczas zatwierdzonego wykonania. CombatDamageSystem nie generuje zasobu. Zachowane limity, blokady regeneracji, decay oraz feedback rzeczywistego zysku; użycie ofensywne przy pełnej furii nadal chroni przed najbliższym decay. Bez zmian schematu save, many/impetu, startów zasobów i kosztów. Wykonać Zadanie nadal50 furii/0 PA bez blokady dodatkowej tury, ale obszar nie mnoży już zysku. Stary identyfikator konfiguracji combat.rage.per_hit zachowany dla kompatybilności, znaczenie to teraz zysk za skill. Szczegóły/dowody: LocalHandoffs/rage-per-skill-pass-20261016/HANDOFF.md.

## PT-06 — Skalowanie i attrition wyprawy

**Aktualny pass encounterów, 2026-10-16 — READY_FOR_SMOKE:** Las ma siedem składów 2/2/3/3/3/3/4 osób (3 wilki; 2 rzezimieszków; 2 rzezimieszków+2 strzelców; 3 łuczników; druid+2 kłusowników; kłusownik+2 łuczników; 2 drzewa). Katakumby były punktem odniesienia. Średnia trudność ma być zbliżona między motywami, a rozrzut między łatwymi i trudnymi składami wewnątrz motywu jest zamierzony. Bez nowych korekt statystyk/skilli/zasobów/bossów. Nowe `.balanced` definicje są w pulach prototypu i trzech wariantów Lasu; oryginały pozostają w katalogu dla aktywnych snapshotów. Nowe składy pojawiają się w nowej wyprawie.

200 unikalnych testów PASS, 560 pojedynczych walk + 72 serie/894 walki, razem 1454 bez limitów. Krótkie full cleary L1: oba lochy 5/6, L48 oba 6/6. W przenoszonych stanach krótkich serii (full/scouted, L1/L48 po sześć seedów) presja obrażeń + netto utrata HP między walkami daje Las/Katakumby 1,0446; w kontrolowanych pojedynczych walkach 1,121, średnie końcowe HP na poziom różni się maksymalnie o około 1,2 pp. To podobne średnie w próbce, nie identyczne wyniki każdego seeda/builda. Dłuższe i bossowe serie mają skromną próbę; bossowie nie byli retunowani. Manual gameplay/FPS NOT RUN. Pełny raport: `LocalHandoffs/encounter-balance-pass-20261016/HANDOFF.md`.

Model XP po tej zmianie: krótkie full cleary + jeden standardowy quest to około 19 wypraw do L20 w obu lochach, scouted około 19,2 Katakumb/20,6 Lasu. Bez zmian wzorów XP. Starsze liczby o łatwiejszym Lesie i 22 wyprawach opisują poprzedni etap. Scouting w symulatorze respektuje graf/bramy, ale nie omijanie aktorów wewnątrz pokojów; robocze 20–30% gameplayowego unikania nie jest gwarancją dla każdej mapy. Cache uwzględnia aktualne pule/składy i dane scoutingu; odziedziczone pary nie mogą zafałszować nowych pomiarów.

**Interpretacja wyników — dopisek użytkownika:** obecne symulacje są full clearami. Scouting i wybór ścieżki mogą pozwolić ominąć około 20–30% encounterów (założenie do weryfikacji na geometrii, nie pomiar). Dalszy balans rozdziela full clear i trasę obejmującą około 70–80% walk, zachowując wymagane przejścia/bossa. Nie osłabiać automatycznie Katakumb tylko dlatego, że startowa drużyna przegrywa długi full clear. Omijanie walk obniża też XP/łup; podane tempo awansów dotyczy pełnego czyszczenia z questem.

**Obowiązująca aktualizacja długości i odnóg — 2026-10-23:** bazowa pula krótka/normalna/długa to 14–17/28–34/42–51 pokojów. Dodatkowo 2–4 odnogi na całą wyprawę, z 2–3/2–5/3–8 pokojami dojścia przed każdym celem. Katakumby rozdzielają odnogi między 1/2/3 piętra (długie mają minimum 3 cele); Las pozostaje na jednym piętrze. Z dojściami: Las 18–29/32–54/48–83, Katakumby 18–29/32–54/51–83 pokojów. Każde piętro ma dodatkowe połączenie między odcinkami głównej drogi, bez obejścia bram. Walki: Las 5–6/9–11/14–17; Katakumby 5–6/10–12/15–18. Gęstość 30–40% dotyczy bazowej puli bez dodatkowych pokoi dojścia, jeden boss wliczony. Zwykli przeciwnicy dają ×2 dotychczasowego EXP po karze za poziom; boss ×1, zwykła obstawa ×2. Questy, tabele dropu, złoto, obozy 0/1/2 i ×1/×2/×3 nagrody questu ukończenia pozostają bez zmian. Nowe pokoje używają istniejącej zawartości, bez automatycznego podnoszenia budżetu walk. Długość/seed oferty są trwałe; istniejący snapshot zachowuje dawny układ. Pełny zapis v4 przechowuje rolę pokoju dojścia i nadal odczytuje v1–3.

Bazy NPC: HP ×0,4, ATK ×0,6 (całkowite), brakująca baza dodana do pełnego późnego przyrostu /199. L50 zachowany, L48 nieznacznie niższy; Widmowy Rycerz 3 HP. Bohaterowie, drzewka, zasoby i wcześniejsze mnożniki przyrostów bez nowych zmian. Krótka bazuje na dawnych około 10 walkach/31 wrogach i około 19 wyprawach do L20 z jednym standardowym questem. Las ma mniejsze grupy, więc tempo XP oraz trudność pozostają różne.

READY_FOR_SMOKE: 232 unikalne testy PASS, 36 wygenerowanych układów, 32 unikalne serie/449 walk, 30 ukończonych. Krótkie L1 8/8; krótkie L10/28/48/50 16/16; normalne/długie L1/48 6/8. Startowe Katakumby przegrały po 17/22 i 20/32 walk z wykorzystanymi obozami; Las łatwiejszy. Testy używają produkcyjnej mechaniki, osobnej polityki bohaterów, stałej areny, realnej progresji i ograniczonych obozów; nie inwestują zdobytych w środku serii punktów. Manual gameplay/render/FPS NOT RUN. Wdrożone przez Editor API bez manualnego wiring/commit/push, wyłącznie sekwencyjne Unity batch z dwoma workerami. Szczegóły: `BALANCE_EXPEDITION_LENGTHS.md` i `LocalHandoffs/dungeon-length-pass-20261016/HANDOFF.md`.

### Poprzedni etap PT-06 — zapis historyczny, przed długościami i zmianą baz

PT-06 — READY_FOR_SMOKE, EXPEDITION SIMULATIONS PASS (92 wypraw/523 walk; 518/518 testów PASS). Baseline48,199 przyrostu, bohaterowie bez zmian. Zwykłe NPC: HP/ATK×0,6 wczesnego przyrostu, płynnie do HP×2/ATK×2,5 na48; bossowie osobno. Wczesna korekta ma dokładnie0 wpływu na48/50. HP/zasoby/statusy przenoszone między encounterami; rzeczywiste zestawy obozowe jeden/piętro,40% HP/many, bez darmowego leczenia. Uwzględniono uzdrowienie/regenerację paladyna i bandaż wojownika. Stare wyniki bez tego leczenia odrzucone. Korekta po decyzji użytkownika: Wykonać Zadanie kosztuje50 furii zamiast20 i może ponawiać przyspieszenie w dodatkowej turze, jeśli boss ponownie uzbiera zasób; usunięto blokadę dodatkowej tury. Po globalnej korekcie furii obszarówki zwracają10 za cały skill, więc dwa skille zwracają20. Korekta50 furii:90/90 testów PASS,6 wypraw/65 walk bez limitów, baseline48 4/4 i ulepszenia48 2/2 ukończone; identyczny stan przed bossem. Dowody:LocalHandoffs/enemy-scaling-pass-20261016/slayer-rage-cost/HANDOFF.md.23 definicje bonusów + koszt/tooltip Zadania, oryginalne pola bazowe/przyrostów, chronione statystyki/arkusz i zasady zasobów zachowane. Testy/import w otwartym Unity bez ręcznego Refresh. Kontrolowana arena12×10 i generowane prawdziwe składy/liczby; nie jest to test wszystkich geometrii/buildów aniFPS. Gear48/50 z read-only fixture arkusza; produkcyjny import wyższych progów nie był rozszerzany. Pełne wyniki:LocalHandoffs/enemy-scaling-pass-20261016/HANDOFF.md,verification.json,final-expedition-results.json. Manual gameplay NOT RUN, nie jest wymaganym dowodem kalibracji. Bez commit/push.

## PT-15 — Gracz otwiera walkę dowolnym skillem — 2026-10-16 — READY_FOR_SMOKE

Zatwierdzone przez użytkownika: wybór **dowolnego aktywnego skilla** przed walką i **osobny pasek wykrycia każdego przeciwnika**. Wejście: PPM na widocznym, wolnym przeciwniku → **Przygotuj atak**. Otwiera dotychczasowy HUD skilli i wybór celów (obszary, łańcuchy, kilka celów/trybów, obrót oraz dodatkowe wskazania mobilności/obiektów korzystają z tego samego kontrolera). Skill musi należeć do aktywnego zestawu, mieć legalny cel/zasięg/LOS oraz zwykłe koszty; brak darmowego ataku. Podgląd korzysta z drivera wyłącznie do targetowania, nie rejestruje walki i nie tyka tur, statusów, bossów, kosztów ani obrażeń. Zwykłe wykrywanie/patrole nadal działają; normalna walka przerywa przygotowanie. Esc anuluje wybór lub przygotowanie. Hipotetyczna kolejka tur oraz przyciski końca tury/ucieczki/przedmiotów są ukryte podczas przygotowania.

Zatwierdzenie używa normalnego ownera aktywacji walki, alarmu w pokoju i widoczności bojowej. Bez sztucznego ograniczenia do atakującego i celu; odlegli/oddzieleni sojusznicy oraz późniejsze posiłki dołączają według zwykłych zasad. Istniejący resolver rozdziela wspólne pola towarzyszy i respektuje rezerwacje innych walk. Jednostkowe wskazania celu są przemapowane na wynik ustawienia, a skill ponownie sprawdzany przed rejestracją i kosztami. Ewentualne dodatkowe cele obszaru rozszerzają początkową grupę w ograniczonym, rosnącym procesie; brak szukania co klatkę.

**p jest zapisane po poprawnej walidacji, przed uruchomieniem tury i rozpatrzeniem skilla.** DodatkoweAV danego przeciwnika = bazoweAV×(1−p), doliczone raz do jego początkowego pozostałegoAV. Atakujący rozpoczyna własną pierwszą turę, płaci zwykłePA/zasób/noże i zachowuje resztęPA. Pudło nadal rozpoczyna walkę. Bez trwałej zmiany szybkości, statusu do ponownego nakładania czy bonusu dla późniejszych dołączeń; merge zachowuje pozostałeAV. Zmiana fazy również nie daje ponownie zaskoczenia. Jeśli efekty samego początku tury uniemożliwią wcześniej legalny skill, walka pozostaje normalnie rozpoczęta i gracz wybiera dostępną akcję; nie dostaje darmowego ciosu.

Kompilacja/self-review PASS; końcowe **433/433 EditMode PASS**, w tym26 nowych przypadków (84 w fixture seamless). Scena testowa potwierdza przygotowanie/anulowanie oraz4 sojuszników+2 przeciwników, osobnepaski, zwykłe koszty, zachowanie nagród i różne pola przy nakładających się followerach. Sprawdzone także zaawansowane tryby targetowania, brak skutków w preview, nielegalne cele/zasięg/LOS/PA/mana/wymagania, pudło i brak powtarzania bonusu przy późniejszych dołączeniach. Wszystkie importy/testy w otwartym Editorze, bez ręcznych Refresh. Manual smoke/FPS NOT RUN; bez zmian artu/skali/PPU/scen/prefabów/schematu save, bez commita/pusha. Standardowe zasoby0/200,100/100,0/50 zachowane.

Dowody: `LocalHandoffs/combat-opening-pass-20261016/{verification.json,final-tests.xml,scoped.diff,HANDOFF.md}`. PT-06 zakończony osobnym passem skalowania i symulacji całych wypraw; aktualny wynik znajduje się w sekcji PT-06 powyżej.

## PT-04 — Standardowy start zasobów — 2026-10-16 — READY_FOR_SMOKE

**Obowiązująca decyzja użytkownika: impet0/200, mana100/100, furia0/50.** Starty50 impetu i60 many zostały wycofane. Dotyczy wszystkich23 definicji przeciwników, w tym obu faz bossów. Profile klas mają teraz standardowy start: mana100%, pozostałe0%. Te same wartości obowiązują przy tworzeniu oraz pierwszym dołączeniu do walki. Zapisany bieżący stan i zmiana fazy/rebind nie są traktowane jako nowe otwarcie walki i nie doładowują zasobu.

Zachowano typy zasobów, limity, regenerację, zatwierdzone koszty furii (połowa, zaokrąglanie w górę do5), bonus szybkości impetu ponad100 oraz zwykłe zasady generacji. Zmieniono wyłącznie początkowy procent w4 assetach klas przez Editor API i odpowiadające oczekiwania testów. Brak zmian artu/skali/PPU, scen czy prefabów.

Weryfikacja438/438 EditMode PASS: profile wszystkich23 definicji na poziomie10, rzeczywiste wejście do walki po eksploracji, AI, zasady zasobów, bossowie, save/load i bonus szybkości. Kompilacja i audyt zakresu PASS. Wszystko w istniejącym Editorze, bez ręcznego Refresh; manual smoke NOT RUN. Dowody: `LocalHandoffs/standard-enemy-resources-20261016/{tests.xml,verification.json,enemy-resource-profiles.json,scoped.diff,HANDOFF.md}`. Bez commita/pusha.

## Historia PT-04 — Korekta limitu impetu i bonus szybkości — 2026-10-16 — READY_FOR_SMOKE

Użytkownik wskazał, że limit100 wyłącza mechanikę szybkości ponad100 impetu. Poprawiono wszystkich8 przeciwników z impetem (w tym obie fazy Przywódcy) na **start50 / limit200**. Start klasy zmieniono na25%, żeby zachować zatwierdzone50 przy tworzeniu i pierwszym dołączeniu do walki. Furia0/50, mana60/100, regeneracja i koszty bez zmian.

Audyt wykrył dodatkowo, że udokumentowany bonus szybkości nie był wykonywany w runtime. Wspólny pipeline statystyk daje teraz **+4 szybkości za każde pełne25 impetu ponad100**:125→+4,150→+8,175→+12,200→+16. Ruch i odzysk aktualizują bonus; wydanie, wyssanie i reset zasobu go obniżają. Obsłużone źródła: reguły ruchu, odzysk/skille/sztylety, drain i upkeep skilli, obiekty przywołane, reset eksploracji/wejście do walki, mikstury, odzyskanie towarzysza i konsola. Bonus jest wyliczany ponownie po wczytaniu save/stat pipeline, nie zapisuje się jako osobny efekt. Odświeżanie po zmianie zasobu następuje tylko przy przekroczeniu progu szybkości, bez skanowania co klatkę. Szybkość wpływa na zwykłe obliczenia PA/AV przyszłych tur, bez zwrotu bieżących PA i bez zmiany aktualnie zaplanowanego AV.

Weryfikacja: końcowe114/114 testów PASS (63 profile/bonus,51 Scout), w tym17 nowych przypadków. Łącznie358 odrębnych przypadków z najnowszym PASS:244 niezmienione przypadki z pierwszego przebiegu oraz114 końcowych; źródło każdego w latest-test-results.json. Pierwsze3 failure dotyczyły konfiguracji testów Scout: ręczna celność poza Stats oraz tonik mieszany z bonusem impetu. Poprawiono fixture, bez osłabiania kontraktu obrażeń/toniku. Importy i testy wykonano w otwartym Editorze bez ręcznych Refresh. Audyt dysku potwierdza8 definicji limit200 i2 klasy start25%. Manual smoke NOT RUN.

Dowody: `LocalHandoffs/momentum-cap-correction-20261016/{verification.json,initial-tests.xml,final-tests.xml,latest-test-results.json,import-audit.json,scoped.diff,HANDOFF.md}`. Smoke: sprawdzić start50/200, ruch124→125, spadek szybkości po skillu oraz powtarzane odświeżanie bez kumulacji bonusu. Bez zmiany grafiki/skali/PPU, bez commita/pusha.

## Historia PT-04 — Zasoby przeciwników i start walki — 2026-10-16 — READY_FOR_SMOKE

Użytkownik zatwierdził role i wartości; doprecyzował zaokrąglanie po obniżce do **wielokrotności5 w górę**. Furia0/50: kościany wojownik, obrońca, ghoul, umarlak, widmowy rycerz i obie fazy Szkieleta Zabójcy. Impet50/200: łucznicy katakumb/lasu, strzelec, rzezimieszek, kłusownik, wilk i obie fazy Przywódcy. Mana60/100: okultysta, upiór, druid, drzewo, obie fazy enta i Umarłego Króla. Mana regeneruje15 na początku własnej tury (pierwsza regeneracja60→75); furia/impet mają regenerację statystyczną0 i dotychczasowe reguły generowania z ataków/ruchu. Koszt furii = ceil(staryKoszt/10)×5, np.35→20,55→30; koszt0 pozostaje0. Pozostałe koszty bez zmian.

Dodano `CharacterClassData.StartingResourcePercent` z domyślnym-1 zachowującym stare reguły graczy. Trzy nowe warianty klas przeciwników (katakumby: furia/impet, las: impet) i dwa istniejące profile many. `CharacterInstanceFactory` stosuje profil po skalowaniu. Przy pierwszym dołączeniu przeciwnika `ScoutKnifeSystem.ResetForCombat` stosuje start raz, korzystając z istniejącego HasInitializedCombatResources — eksploracja nie zeruje w ten sposób otwarcia impetu i nie zmienia go w pełną manę. Rebind/zmiana fazy nie doładowują; gracze są pomijani. Obie fazy bossa zachowują ten sam zasób.

`EnemyResourceProfilesSetup` aktualizuje wąsko23 definicje/67 skilli startowych, klasy, ich rejestrację i referencje16 encounterów. Tylko zasoby/koszty; brak zmian HP, obrażeń, odporności, PA, składu/liczby encounterów, artu/PPU/skali/scen/prefabów. Audyt77 istniejących assetów +3 nowych klas: bez zmian istniejących wartości poza zatwierdzonymi polami zasobów/referencjami; Unity dopisało domyślne pola i znormalizowało zapis6 nazw bez zmiany tekstu. Aktualizacja idempotentna, nie dzieli kosztów ponownie; zapisuje tylko dotknięte assety. Podpięta do istniejącego authoringu lasu/katakumb/bossów.

AI: przeciwnik dystansowy z impetem może raz na turę wykonać do2 bezpiecznych kroków na pozycji strzeleckiej, płacąc normalny koszt i zachowując PA na podstawowy atak. Nie dostaje darmowego zasobu. Wybór wartościowego skilla specjalnego ma pierwszeństwo przed tym ruchem; blokada zysku/taunt/stacjonarna jednostka wyłączają regeneracyjne przestawienie. Niedobór droższego skilla nie zostaje zablokowany samą dostępnością mniej przydatnego tańszego. Planowanie współbieżne kooperacyjnie w istniejącym budżecie, do8 sprawdzeń krawędzi; brak nowych skanów/Update per-frame. Symulacja potwierdza ruch, odbudowę impetu i użycie skilla z odzyskanego zasobu.

Kompatybilność: zachowano stare wspólne klasy many w katalogu. Stare jawne referencje encounterów/saves są mapowane wyłącznie na nowy wariant zasobu tej samej rodziny przeciwników; stare Mana nie staje się darmową pełną furią/impetem, przy migracji przyjmowany jest zatwierdzony start. Aktualne zapisy zachowują stan zasobu. Bez zmiany wersji/DTO save. Reguły i stany początkowe postaci gracza bez zmian.

Weryfikacja: kompilacja Core/Data/Runtime/Systems/Views/Editor/EditModeTests PASS; **421 odrębnych aktualnych przypadków z najnowszym wynikiem PASS**, w tym46 nowych. Ostatni szeroki przebieg420/421, jedyny błąd dotyczył nadmiernie szczegółowej asercji długiej symulacji wymagającej konkretnego buffa zamiast użytecznego skilla. Finalny EnemyResourceProfileTests46/46 PASS; reszta375 przypadków z szerokiego przebiegu PASS. Obejmuje80 AI,58 seamless,51 zwiadowcy, bossów, mobki, save/restore i reguły zasobów. Wcześniejsze stare asercje pełnej many zastąpiono zatwierdzonymi profilami; mechaniczne testy skilli jawnie fundowane. Test par wilków używa transient kopii definicji z samą parą wilków i bez bossa zamiast rozszerzonego mieszanego prototypu; nadal wymaga dokładnie2 wolnych, patrolujących spawnów dla10 seedów. Test pudła deterministycznie używa gwarantowanego uniku (wysoki Dodge pozostawiał minimalną szansę trafienia). Generator/content nie został zmieniony dla testów.

Dowody: `../LocalHandoffs/enemy-resources-pass-20261015/verification.json`, `latest-test-results.json`, `admission-regression-tests.xml`, `resource-final-tests.xml`, `enemy-resource-profiles.json`, `asset-audit.json`, `scoped.diff`. Self-review/diff-check PASS. Manual smoke/FPS NOT RUN. Importy/testy w otwartym Editorze bez ręcznego Refresh, drugiego procesu lub otwierania/zapisywania scen. Menu Godmist → Verification → Enemy resource profiles (keep Editor open). Bez commit/push. Pozostałe2 tematy: PT-06 i PT-15.


## PT-02 — Magiczna Tarcza Alison: siła i czas — 2026-10-15 — READY_FOR_SMOKE

Użytkownik zatwierdził: **250% średniego ataku,3 własne tury odbiorcy, ponowne rzucenie zastępuje poprzednią Magiczną Tarczę i odnawia czas; inne źródła pozostają**. Produkcyjny `SD_MageShield` ma mnożnik2,5 i duration3. Koszt40 many/90% AP, celowanie i pozostałe właściwości są zachowane. Autorski setup zaktualizowany; istniejący asset zmieniła w otwartym Editorze wyłącznie wąska metoda `VS09MageSetup.ApplyShieldPlaytestBalance`, bez ponownego generowania całej klasy.

`ShieldLayer.RemainingTurns` przechowuje czas przy konkretnej warstwie. Zera zachowują bezterminowość innych źródeł. `StatusEffectSystem.ProcessTurnEnd` odejmuje turę tylko odbiorcy; po trzecim zakończeniu usuwa pozostałą wartość tarczy. Bieżąca tura rzucającego na siebie liczy się zgodnie ze standardowym odliczaniem buffów na końcu tury. Pochłonięta warstwa znika od razu. Recast usuwa tylko źródło `skill.mage.shield`, również przy innym rzucającym. Stun/freeze/paraliż nie zatrzymują odliczania własnych zakończeń tur. Feedback wygaśnięcia pokazuje faktyczną pozostałą wartość; oznaczenie self-source zapobiega fałszywej pozie trafienia.

Poza walką odlicza istniejący tick eksploracji co6s (miasto i loch), bez nowego Update/timera. Jednostki walczące są pomijane przez ownera eksploracji, a powieleni occupants z tą samą postacią liczeni raz. Wejście do kolejnej walki nie odnawia czasu. Tooltip podaje250%, czas i regułę zastępowania.

Save: dodano opcjonalne pole `ShieldLayerSaveData.RemainingTurns` (default0), bez podnoszenia wersji. Aktualny zapis zachowuje pozostały czas/wartość. Stare zapisy z bezterminowymi/zdublowanymi Magicznymi Tarczami dostają3 tury i jedną ostatnią warstwę; zachowuje się jej pozostałą wartość, bez ponownego wyliczania siły bez danych pierwotnego rzucającego. Pozostałe stare tarcze nie zmieniają czasu. Ujemny czas jest odrzucany.

Weryfikacja: kompilacja Core/Data/Runtime/Systems/Views/Editor/EditModeTests PASS; **167/167 testów PASS**, w tym18 nowych. Zestawy: MageShieldLifecycle15, ShieldPool6, MageContent15, StatusEffectSystem28, CombatRecovery35, SaveSnapshot5, Restore19, CombatStatusPresentation31, CombatTooltipGlossary8, ExplorationResource5. Pierwszy166/167: test celowo uszkodzonego zapisu nie oczekiwał komunikatu odrzucenia; dodano precyzyjny LogAssert.Expect, retest15/15, finalny167/167. Self-review i audyt assetu PASS. Unity dopisało tylko domyślne pola obecnego schematu oprócz zatwierdzonych zmian. Dowody: `../LocalHandoffs/mage-shield-pass-20261015/verification.json`, `final-live-tests.xml`, `asset-audit.json`, `scoped.diff`.

Manual smoke NOT RUN. Sprawdzić siłę, odliczanie3 tur odbiorcy, recast bez kumulacji i wygasanie w eksploracji. Importy/testy automatycznie w otwartym Editorze, bez ręcznego Ctrl+R/drugiego procesu; menu Godmist → Verification → Mage shield playtest pass (keep Editor open). Bez artu/PPU/scen/prefabów/commit/push. Pozostałe3 tematy: PT-04, PT-06, PT-15.


## PT-03 — Impet za odzyskanie sztyletów — 2026-10-15 — READY_FOR_SMOKE

Wybrano **+8 impetu** w zakresie5–10 pozostawionym przez użytkownika do uznania asystenta. Nagroda jest płaska, raz za faktyczny odzysk: wejście na jedno pole zbiera własne sztylety z tego pola i daje jedną nagrodę bez mnożenia przez liczbę wpisów/stosów; kolejne pole to osobny odzysk. Odzysk skillem daje jedną nagrodę po opłaceniu zwykłych kosztów. Wyjęcie wbitego sztyletu również daje jedną nagrodę po jego usunięciu z celu. Reset zapasu na początku walki, pusta/powtórna próba, obcy sztylet i pełny zapas nie dają impetu.

Wspólny helper `ScoutCombatSystem.RewardKnifeRecovery` używa istniejącego `CombatRecoverySystem`: tylko zasób Momentum, limit zasobu, aktywne blokady zysku, floating feedback faktycznej ilości (np.+3 przy197/200, brak przy200/200). Powyżej100 impetu bonus nadal wynosi8, ponieważ jest płaskim zwrotem, nie generacją z ruchu. Dotychczasowe koszty, talenty zniżek i zasady generacji z ruchu są zachowane. Nie istnieje osobna bezkosztowa akcja upuszczania: sztylety powstają z rzeczywiście zużytego zapasu podczas akcji, a wyjęcie wymaga wcześniejszego wbicia. Przy okazji naprawiono utratę reszty stosu podczas podnoszenia z ziemi przy częściowo pełnym zapasie.

Opis nagrody znajduje się we wspólnych tooltipach skilli odzysku i przy liczniku sztyletów. Brak zmian artu/PPU/skali świata, serialized assetów/scen/prefabów i schematu save; brak nowych skanów per-frame. Import/testy w otwartym Editorze przez istniejący runner, bez ręcznego Ctrl+R i bez drugiego procesu.

Weryfikacja: kompilacja PASS; **203 odrębne przypadki z najnowszym wynikiem PASS**, w tym15 nowych. Pierwszy przebieg201/203 ujawnił dwie stare asercje kosztu Haka bez nowej nagrody; zmieniono wyłącznie oczekiwany wynik udanego odzysku. Retest całego ScoutContentTests51/51 PASS; nieudane próby nadal nie zmieniają stanu. Pozostałe fixture: ScoutMechanics29, ScoutKnifeHud13, ScoutKnifeLanding17, CombatRecoverySystem35, SeamlessDungeonCombat58 PASS. Self-review/diff-check PASS. Dowody: `../LocalHandoffs/knife-recovery-reward-pass-20261015/verification.json`, `latest-test-results.json`, `first-live-tests.xml`, `scout-content-retest.xml`.

Manual smoke NOT RUN. Sprawdzić +8 z ziemi, przez Hak i wyjęcie; limit zasobu; resztę stosu przy prawie pełnym zapasie. Menu: Godmist → Verification → Knife recovery reward (keep Editor open). Pozostałe4 tematy: PT-02, PT-04, PT-06, PT-15. Bez commit/push.


## PT-05 — Spójna nazwa odporności na kontrolę — 2026-10-15 — READY_FOR_SMOKE

Przyjęto istniejącą nazwę z karty postaci: „Odporność na kontrolę”, skrót w kolumnie odporności „Kontrola”. Wspólny formatter tooltipów skilli, statusów i biżuterii używa teraz „kontrolę”; galduryty pokazują „Szansa na kontrolę” / „Odporność na kontrolę”, a inspektor konsoli „Kontrola”. Glosariusz wyjaśnia dokładny zakres `StatusEffectIds.UsesSuppressionResistance`: ogłuszenie, zamrożenie, sen, paraliż, unieruchomienie, wychłodzenie i prowokacja. Nie rozszerzono kategorii o zwykłe spowolnienie.

Poprawiono autorski opis Miażdżącego Ciosu; istniejący asset aktualizuje w otwartym Editorze wąska, idempotentna metoda `VS09PaladinSetup.RefreshControlResistanceWording`, która dotyka tylko `TooltipDescription`. Pełna konfiguracja skilla nie jest uruchamiana ponownie. Unity przy zapisie dopisało domyślne pola aktualnego schematu; audyt potwierdził, że wszystkie istniejące wartości poza opisem są identyczne. Enumy, ID, pola save, wartości odporności i zasady walki pozostają bez zmian. Import/testy obsługuje istniejący request runner; użytkownik nie musi używać Ctrl+R ani zamykać Unity.

Weryfikacja: kompilacja Runtime/Systems/Views/Editor/EditModeTests PASS; testy glosariusza8, treści przedmiotów7 i paladyna17 — **32/32 PASS** w otwartym Editorze. Self-review PASS. Dowody i smoke: `../LocalHandoffs/control-resistance-naming-pass-20261015/`. Manual smoke NOT RUN. Bez commit/push.


## PT-14 — Lądowanie rzeczywistego sztyletu — READY_FOR_SMOKE (2026-10-15)

Fizyczny sztylet, który faktycznie trafił do CombatSession.MapObjects, otrzymuje jednorazowe, NonSerialized LandingOrigin/LandingFromHit. FinishKnifeAction bierze informację o trafieniu z rzeczywistego wyniku efektu obrażeń (Succeeded), nie z cache rolli follow-upów. Upuszczenia po śmierci również mają punkt wyjścia. Zachowanie sztyletu talentem, darmowy cios w zwarciu i pełne wbicie w cel nie tworzą dodatkowej kopii na ziemi. Nie zmieniono wyboru miejsca dropu, liczby/ceny sztyletów, celności, obrażeń, AP, zasobów ani zasad odzyskiwania.

CombatUnitViewSpawner używa tego samego SpriteRenderera i istniejącej grafiki catacombs-ground-dagger-v2. Przez0,42s sztylet pokonuje krótki łuk, obraca się i zatrzymuje dokładnie na istniejącym polu pickup'u z GroundDaggerPresentationOffset. Trafienie zaczyna na wysokości kontaktu0,65; pudło nisko nad ziemią0,12, bez sugerowania trafienia w postać. Po końcu rotacja jest identity, skala nadal0,45 i grafika/32PPU bez zmian. To animacja fizycznego przedmiotu, odrębna od statycznych VFX skilla; nie przedłuża ani nie blokuje tury.

Odzyskanie w trakcie lotu nadal działa według istniejącej mechaniki i usuwa widok oraz stan animacji. Outline z hovera/HUD zaczyna działać po lądowaniu. Hint jest konsumowany raz; istniejące przedmioty, ponowne utworzenie widoku i odkrycie mgły nie odtwarzają starego lotu. Ukryty początek/cel pomija animację, a utrata widoczności w trakcie przerywa ją i przywraca docelową pozycję. ClearViews/usunięcie map object sprzątają dictionary. Bez nowych rendererów ruchu, colliderów, scen scans, bitmap lub komponentu Update na każdy sztylet.

Regresja ujawniła cache grafiki zwracający wpis bez sprawdzenia żywego sprite'a/tekstury i bez ponownego potwierdzenia profilu Point. Load w istniejącym DungeonCatacombsObjectVisuals odtwarza teraz martwy wpis oraz utrzymuje Point/Clamp także na cache hit. Dwa osobne testy wymuszają zniszczony sprite i Bilinear; brak zastępczej lub rozmytej grafiki. Nie edytowano PNG, metadanych importu, PPU ani skali postaci0,7.

Weryfikacja:153/153 testów PASS w otwartym Editorze, w tym17 nowych. Rzeczywisty skill: trafienie/pudło i koszt; zachowane/darmowe/wbite sztylety, śmierć celu, metadata NonSerialized, łuk/rotacja/dokładny koniec, odzysk w trakcie, hover/reuse, stare przedmioty, odtworzenie cache i widoku, mgła oraz regresja treści/mechanik/HUD zwiadowcy, odzyskiwania i walk łączonych. Render pięciu etapów ze źródłową grafiką sprawdzony wizualnie: PASS. Kompilacja offline Runtime/Systems/Views/EditModeTests i końcowa kompilacja live PASS. Self-review PASS. Manualny smoke/FPS pełnej gry NOT RUN.

Pierwszy offline preflight poprawił wymagany floorIndex fixture przed importem. Pierwszy live:10/15; poprawiono parametry opcjonalnego Bind w reflection, założenie testu o leniwym Awake outline'u i źródło faktycznego trafienia. Następnie15/15. Szerszy run150/151 ujawnił cache; po poprawce i dwóch nowych przypadkach153/153 PASS. Wszystkie importy i retesty wykonano automatycznie przez istniejący request-based Assets Refresh; nie proszono użytkownika o Ctrl+R ani zamknięcie Unity.

Dowody: `../LocalHandoffs/knife-landing-pass-20261015/verification.json`, `final-live-tests.xml` i `knife-landing-stages.png`. Bez zmian serialized scen/prefabów/assetów, schematu save, balansu lub ręcznego wiring. Bez commit/push. Menu testów: Godmist → Verification → Knife landing (keep Editor open). PT-01, PT-07, PT-08, PT-09, PT-10, PT-11, PT-12, PT-13 i PT-14 READY_FOR_SMOKE; pozostałe6 tematów: PT-02–PT-06 oraz PT-15.


## PT-08 — Feedback XP, awansu i punktów rozwoju — READY_FOR_SMOKE (2026-10-15)

CharacterProgressionSystem publikuje po rzeczywistym dodatnim AddExperience nieutrwalany event CharacterInstance.ExperienceGained(amount, levels), po zastosowaniu poziomu, punktów i przyrostu statystyk. Wspólny punkt obejmuje walki, questy i konsolę; bez zmiany obliczeń nagród lub progów. Zero/ujemne XP, maksymalny poziom, wczytanie postaci i samo ustawienie poziomu nie tworzą fałszywych komunikatów. Event ma NonSerialized; bez zmiany schematu save.

Rail zbiera nagrody jednej postaci z tej samej klatki w jeden floating text „+N XP”, z „Zdobyto poziom!” przy awansie i ×N przy wielu poziomach. Korzysta z istniejącego CombatFloatingFeedbackView, fontu Godmist i jawnie zarządzanego materiału outline. Około2,5s czasu unscaled, bez przechwytywania kliknięć. Kotwica nad aktualnie widoczną postacią śledzi kamerę; brak dostępnego/widocznego obiektu świata daje bezpieczny fallback obok portretu. Rebind/disable odpinają subskrypcje i czyszczą pending; przebudowa railu najpierw emituje już przyznane XP, żeby go nie zgubić.

Portret ma w prawym górnym rogu czytelne, ciemne Godmist „+N” z faktycznej AvailableSkillPoints. Zero ukrywa wskaźnik. Klik otwiera bezpośrednio istniejący rozwój właściwej postaci, bez dodatkowej karty ekwipunku; jedno okno jest ponownie używane. Zachowana blokada rozwoju uczestnika walki i walidacja przy kliknięciu, także przy starym stanie przycisku. Hover wskaźnika nie konkuruje z rich tooltipem portretu. W karcie przycisk pokazuje „Rozwój (+N)”; otwarta karta aktualizuje również XP/poziom po nagrodzie i punkty po inwestycji.

Weryfikacja w istniejącym Editorze:118/118 testów PASS, w tym16 nowych. Nagrody, wielokrotny awans, brak duplikacji nagród zwycięstwa, zmiana postaci, disable, przebudowa railu, klik/ponowne użycie rozwoju i blokada walki, stan karty, lifetime/materiał i NonSerialized event. Regresja istniejącej progresji, questów, nagród walk, drzewek, railu/kontekstowego menu i kart. Render GPU sprawdzony: wskaźnik czytelny, floating text nie zasłania nazwy/portretu. Self-review PASS; pełny manual smoke w mieście/lesie/walce NOT RUN.

Pierwszy import ujawnił błędną nazwę metody integracji (Show zamiast Spawn); naprawiono, a offline preflight Runtime/Systems/Views/EditModeTests przeszedł bez nadpisywania DLL Editora. Pierwsze testy:13/15 PASS; poprawiono brak CombatId w fixture oraz istniejące Clear drzewka używające Destroy w EditMode (runtime dalej Destroy, EditMode DestroyImmediate). Następnie15/15 i końcowe118/118 PASS. Ostatni dodatkowy przypadek chroni XP przed utratą przy przebudowie railu.

Do istniejącego LiveVfxVerification dodano jawny request `../LocalHandoffs/unity-refresh.request`: poza Play Mode i kompilacją wywołuje Assets Refresh przed odkrywaniem testów. Nie zmienia preferencji, scen ani assetów serialized i nie uruchamia drugiego procesu. Pierwsze wczytanie helpera wymagało użytkownika; kolejne importy i retesty wykonano automatycznie, potwierdzone unity-refresh.state.txt. Nowy run-tests.ps1 przygotowuje obie prośby. Bez commit/push, nowych grafik, importu/PPU/skali świata lub ręcznego wiring.

Dowody: `../LocalHandoffs/progression-feedback-pass-20261015/verification.json`, `final-live-tests.xml`, `progression-feedback.png`. PT-01, PT-07, PT-08, PT-09, PT-10, PT-11, PT-12 i PT-13 READY_FOR_SMOKE; pozostałe7 tematów otwarte: PT-02–PT-06 oraz PT-14–PT-15.


## PT-07 — Jeden licznik sztyletów Jerzego w HUD — READY_FOR_SMOKE (2026-10-15)

CombatActiveUnitView tworzy jeden licznik „Sztylety / dostępne/limit” pod portretem aktywnego sojuszniczego zwiadowcy, obok PA. Czyta bieżące AvailableKnives/MaxKnives, także limit z talentów, bez wpisania5 na stałe. Zero ma czerwony kolor. Font/materiał z nazwy jednostki i istniejąca ramka Godmist; root80×48, pos8/-112, ignoreLayout, bez poszerzania panelu. Ukryty przy innej klasie, przeciwniku lub nieznanej jednostce. CombatSkillBarView usuwa powtarzany zapas pod skillami; KnifeCost, tooltip kosztu, blokady i mechanika odzyskiwania bez zmian.

Hover licznika podświetla własne sztylety leżące na ziemi w aktywnej sesji. CombatUnitViewSpawner składa niezależny hover licznika i kursora świata; sprzątnięcie jednego nie usuwa drugiego. Zachowane IsPositionVisible, forceRenderingOff, enabled i aktywność renderera; bez ujawniania fog, cudzych lub wbitych sztyletów. Zmiana aktywnej jednostki/sesji, wyjście kursorem, disable/rebind i usunięcie sztyletu sprzątają stan. Reuse istniejących outline'ów, bez scene scans ani nowych bitmap/odczytu tekstur per-frame. Sprzątanie rendererów sztyletów używa istniejącego DestroyPresentationObject, także w EditMode.

Weryfikacja:135 odrębnych testów PASS według najnowszego wyniku każdego przypadku (122 wcześniejszych +13 nowych). Nowe testy: limit/aktualizacja, klasy/widoczność, hover ownership, ukryte renderery, suma dwóch hoverów, reuse, zmiana aktywnej jednostki, usunięcie sztyletu, brak powielania zapasu i zachowanie blokad skilli. Izolowany render GPU właściwego HUD sprawdzony wizualnie: PASS. Początkowe failure nowych testów wynikały z błędnego pola sterownika „driver” zamiast „focused” i nieaktywnego domyślnie prefabu HUD; poprawiono konfigurację testów, bez zmiany produkcyjnych reguł. Self-review PASS. Manualny smoke/FPS pełnej gry NOT RUN.

Dowody: `../LocalHandoffs/scout-knife-hud-pass-20261015/verification.json`, `latest-test-results.json`, źródłowe XML i `scout-knife-hud.png`. Bez zmian balansu/PT-03, PNG/importu/PPU, skali świata, save lub serialized assetów/scen/prefabów. Brak ręcznego wiring; istniejąca scena DungeonSandbox ma referencję spawner→HUD. Assets → Refresh poza Play Mode; Unity pozostaje otwarte. Menu testów: Godmist → Verification → Scout knife HUD (keep Editor open). Bez commit/push. PT-01, PT-07, PT-09, PT-10, PT-11, PT-12 i PT-13 READY_FOR_SMOKE; pozostałe8 tematów otwarte.

## PT-13 — Aktualne modele postaci w kartach — READY_FOR_SMOKE (2026-10-15)

Potwierdzona przyczyna: CharacterInfoWindowView odczytywała VisualSprite, czyli stare frontowe ilustracje VS22. Karta wyposażenia pobiera teraz pierwszą klatkę frontowego idle z CharacterAnimationResolver, zgodnie z aktualnym modelem świata. Bez pakietu animacji zachowany fallback: pierwsza dostępna klatka IdleAnimationFrames, następnie VisualSprite. Brak grafiki czyści poprzedni obraz. Podgląd pozostaje statyczny i preserveAspect, z teksturami Point.

Zachowano układ i rozmiar karty, twarze PortraitSprite/UiPortraitSprite oraz przywróconą skalę świata 0,7. Bez zmian PNG, PPU, importu, transformów, serialized scen/prefabów, gameplayu lub save schema.

Weryfikacja w otwartym Editorze: 78/78 testów PASS (21 karta, 21 akcje przedmiotów, 18 camping/tooltipy, 17 prezentacja lasu, 1 zgodność źródła modelu). Testy porównują czterech bohaterów z rzeczywistym prefabricatem eksploracji po wejściu w idle, sprawdzają skalę/layout, fallback i czyszczenie. Cztery izolowane rendery GPU rzeczywistej karty sprawdzone wizualnie: PASS. Self-review PASS. Pełny manual smoke w grze NOT RUN.

Szerszy początkowy przebieg ujawnił niezależne, stare oczekiwanie audytu VS22 dla ikony skill.enemy.catacombs.dead_king_guard: oczekuje VS22 zamiast istniejącej ikony Króla v2. Assetów Króla ani tego audytu nie zmieniano; initial-live-tests.xml zachowuje failure. Wynik 78/78 dotyczy jawnego zakresu PT-13 i regresji UI, nie całego audytu grafiki.

Dowody: `../LocalHandoffs/character-card-model-pass-20261015/verification.json`, `final-live-tests.xml` i cztery pliki *-card.png. Assets → Refresh wykonane; bez ręcznego wiring i zamykania Unity. Testy można uruchomić przez Godmist → Verification → Character card models (keep Editor open), poza Play Mode. Bez commit/push. PT-01, PT-09, PT-10, PT-11, PT-12 i PT-13 READY_FOR_SMOKE; pozostałe 9 tematów otwarte.

### PT-11 — Korekta regresji skali postaci — READY_FOR_SMOKE (2026-10-15)

Użytkownik odrzucił powiększenie drużyny względem miasta/NPC. Cofnięto błędny kierunek wcześniejszego passa: postacie są punktem odniesienia, a ciała i narośle należy dopasować do nich. Nie zwiększać aktorów ani świata tylko po to, by uzyskać natywny piksel32PPU w world units.

ExplorationCharacterView nie kompensuje już skali rodzica odwrotnością lossyScale. PF_ExplorationCharacter zachowuje root0,7 i autorską skalę dziecka. CharacterView nie narzuca skali1 sprite'om32PPU: PF_BattleUnit zachowuje Body0,7, także dla narośli leczącej i trującej. Dzięki temu postacie nie rosną przy wejściu do walki. Dedykowane ciała32PPU mają skalę0,7, zgodną z żywymi aktorami. Zachowano wcześniejszą korektę samego rysunku wilka i narośli. Bez zmian PNG/PPU/importu, serialized prefabów/scen, rozmiarów colliderów żywych postaci, ruchu, statystyk lub save schema. NPC i otoczenie nie są powiększane.

Weryfikacja w otwartym Editorze:144/144 testów PASS. Rzeczywiste prefaby: czterech bohaterów, wilk, obie narośle; skala0,7 i brak narastania przy ponownym Bind/ConfigureVisual. Kolizje/root bez zmian. Regresja ciał, animacji/póz, downed indicators, awareness i druida. Początkową błędną ścieżkę testu wilka zastąpiono istniejącą stałą WolfPath. Testy odpalone na starym DLL nie były przyjęte jako wynik; przed końcowym dispatch potwierdzono nową kompilację. Powtarzanie testów ujawniło zniszczone sprite'y w cache stacji NPC: TryResolveNpcStation dostał ten sam guard żywego sprite'a/tekstury co istniejący TryLoadSprite, z dwoma testami odtwarzania. Nie zmienia to skali/grafik NPC.

Self-review PASS. Dowody: `../LocalHandoffs/actor-scale-restore-20261015/verification.json` i `final-live-tests.xml`. Nie trzeba zamykać Unity. Po Assets → Refresh uruchomić świeży Play Mode: już istniejące obiekty runtime mogły zachować powiększone Transformy sprzed poprawki. Manualny smoke porównania miasta/NPC/lasu/walki NOT RUN. Bez commit/push. To korekta PT-11; liczba pozostałych tematów backlogu bez zmian.

## PT-12 — Skrzynie lochów: outline i LPM — READY_FOR_SMOKE (2026-10-15)

DungeonChestView obsługuje LPM na widocznej grafice skrzyni. Klik w zasięgu otwiera istniejące okno łupu, a dalszy zleca podejście istniejącej ExplorationPartyController. Przejmuje primary click przed zwykłym ruchem (-90 jak wejście do lochu), respektuje UI/console/text input oraz priorytet interakcji jednostek. E pozostaje alternatywą. Loot nie jest automatycznie zbierany; klucze/lockpicki są zużywane dopiero przez dotychczasowy DungeonChestInteractionService przy rzeczywistym otwarciu. Brak klucza lub dostępu pokazuje istniejący krótki floating feedback, bez nowego tooltipu.

Podejście wybiera legalny cel w zasięgu z HasDirectAccess i widoczną parterową powierzchnią, używając istniejącego pathfindingu/NavMesh. Brak otwierania przez ściany, z innych pięter, w walce, martwym liderem albo na nieaktywnym/ukrytym obiekcie. Brak zbindowanej mapy odrzuca rozkaz bez NullReferenceException. CancelMovement, nowy rozkaz, zmiana lidera/piętra, keyboard override i invalidacja celu sprzątają pendingChest. Osiągnięcie końca trasy zachowuje cel wyłącznie do następnego interaction tick, potem otwiera raz i sprząta rozkaz. Akcja używa autorytatywnej Session kontrolera drużyny, tak jak reszta jego obsługi.

Hover używa istniejącego CombatUnitVisualStateView i shadera sylwetki. Osiem rendererów powstaje raz przy pierwszym hoverze, bez scene scans lub odczytu tekstury w runtime. Szerokość odpowiada jednemu natywnemu pikselowi sprite'a; stabilny cyjan zamiast pulsowania do czerni. Zachowane kolor sprite'a, fog/elevation, lokalny sort i aktualna klatka. Wyłączenie/ukrycie czyści outline; zapamiętana skrzynia poza bieżącą widocznością nie jest interaktywna. Nowy setter pulse speed pozostawia dotychczasowe ustawienia pozostałych unit/NPC bez zmian.

Test odmowy ujawnił istniejące automatyczne tworzenie materiału przez TextMeshPro outlineColor w EditMode. DungeonLootFloatingText otrzymał jawnie zarządzany materiał fontu z OUTLINE_ON, bez mutacji współdzielonego assetu. Ponowna Initialize i OnDestroy zwalniają poprzedni materiał. Regresja dotychczasowych ciał/loot feedback PASS.

Weryfikacja: 161/161 aktualnych unikatowych testów PASS;23 nowe przypadki skrzyń, interakcje świata, pathfinder/NavMesh, placement, kontenery lasu, zbieranie łupu i ciała przeciwników. Licznik bierze najnowszy pełny przebieg każdej fixture. Początkowe failure: typ Vector2/Vector3 w reflection teście, dwie skrzynie w tej samej komórce fixture, material warning TMP, niewywoływany automatycznie lifecycle testowy OnDestroy i brak TilemapRenderer w fixture pamięci mgły — rozwiązane, bez ignorowania logów. GPU render właściwego prefabu i self-review PASS; pełny manual smoke NOT RUN.

Dowody: `../LocalHandoffs/chest-primary-pass-20261015/verification.json`; podgląd: `../LocalHandoffs/chest-primary-pass-20261015/chest-hover.png`. Assets → Refresh poza Play Mode wystarczy; bez ręcznego wiring lub zamykania Editora. Testy live: Godmist → Verification → Chest primary pass. Bez zmian balansu/save schema albo commit/push. PT-01, PT-09, PT-10, PT-11 i PT-12 READY_FOR_SMOKE; pozostałe10 tematów otwarte.

## PT-10 — Dwuklik przedmiotów w karcie postaci — READY_FOR_SMOKE (2026-10-15)

W aktualnym UIW ekwipunku pojedynczy LPM wybiera przycisk bez zakładania/zużywania przedmiotu. Dwuklik zakłada sprzęt, zdejmuje go ze slotu, otwiera torbę, używa zgodnego z eksploracją consumable albo wywołuje istniejącą akcję obozu. Slot założonego sprzętu zachowuje inspekcję pojedynczym kliknięciem. PPM zachowuje menu; Submit/Enter na zaznaczonym przycisku wykonuje akcję domyślną. Usunięto natychmiastową mutację z inventory Button.onClick, żeby pierwszy klik dwukliku nie wykonywał operacji i nie przebudowywał listy przed drugim.

Dispatch należy do istniejącej CharacterInfoWindowView i używa dotychczasowych ownerów: EquipmentService, ConsumableService, LootBagService oraz CampAction. Sprawdzane są własność konkretnej instancji, aktualny stan walki, aktywność widoku/okna/przycisku i interactable. Wymagania sprzętu, miejsce/masa, charges, combat-only bomby, powrót do miasta i warunki obozu nadal walidują istniejące serwisy. clickCount==2, a nie >=2, zapobiega drugiej aktywacji trzecim kliknięciem. Drag nie aktywuje przedmiotu. Stary wiersz/slot nie może użyć przeniesionego przedmiotu ani zdjąć nowego wyposażenia w jego miejsce.

Uzasadnione wyjątki zachowane: handel LPM = transakcja, schowek LPM = transfer, łup LPM = podniesienie; ich własne serie kliknięć, Shift/stack, quantity, PPM i drag pozostają bez zmian. Consumable wymagające walki/targetu nie są automatycznie używane na przypadkowej jednostce.

Weryfikacja: 223/223 aktualnych unikatowych testów PASS (221/221, następnie targeted akcje21/21 z dwoma dodatkowymi przypadkami aktywności).21 nowych testów na rzeczywistym prefabie karty i ExecuteEvents dla Button+view, z mutacjami inventory/charges/equipment. Regresja: CharacterCardPresentation, CampingTooltipLifecycle, DungeonCamp, EquipmentService, TradeService, ItemTooltipPresentation, CraftingUpgradingConsumablesAcceptance. Początkowy compile failure nowych testów wynikał z overloadu NUnit Does.Not.Contain; poprawiono na Has.No.Member. Self-review PASS; manual smoke NOT RUN.

Dowody: `../LocalHandoffs/item-default-action-pass-20261015/verification.json`. Bez nowych usług/timerów, zmian balansu/save schema lub ręcznej konfiguracji serialized assetów/scen/prefabów. Assets → Refresh wystarczy. Testy można też wywołać przez Godmist → Verification → Item default action pass poza Play Mode, bez zamykania Editora. Bez commit/push. PT-01, PT-09, PT-10 i PT-11 READY_FOR_SMOKE; pozostałe11 tematów otwarte.

## PT-09 — Camping kit i lifecycle tooltipów — READY_FOR_SMOKE (2026-10-15)

Usunięto tooltip sukcesu obozu z obu wejść: menu przedmiotu w karcie postaci i menu podłogi. Dotychczas komunikat był przypisany do całego okna/mapy, więc nie otrzymywał zwykłego pointer-exit. Odmowa nadal ma widoczny powód: istniejący zamykany notice przedmiotu albo dismissible menu podłogi; warunki są ponownie sprawdzane przez istniejący owner obozu. Zużycie zestawu, leczenie, zasoby i autosave zachowują swoje reguły.

Źródła kart przedmiotów i slotów wyposażenia invalidują tooltip na OnDisable. Karta postaci sprząta własne źródła przy wyłączeniu lub Initialize/rebind. Wspólny guard tooltipu rozpoznaje także wyłączony Behaviour przy aktywnym GameObject; dotychczas patrzył tylko na activeInHierarchy. Wykorzystano istniejące InvalidateSource/HideForSourceWithin i lifecycle nested/pin; wyłączenie starego źródła nie zamyka niezwiązanej karty. Bez nowych usług, timerów, skanowania inventory per-frame lub ręcznego wiring.

Weryfikacja: 169/169 unikatowych testów PASS (pierwszy przebieg168/168, dodatkowy test rzeczywistego obozu z menu podłogi1/1).18 nowych przypadków oraz DungeonCamp, CharacterCardPresentation, ItemTooltipPresentation i RichUnitTooltipLifecycle. Rzeczywiste prefaby UI, zużycie klikniętego zestawu, sukces obu wejść, odmowa bez zużycia, pending/visible/pinned, disabled Behaviour, destroyed source, rebind/close i zachowanie niezwiązanej przypiętej karty. Self-review PASS. Manual smoke NOT RUN.

Dowody: `../LocalHandoffs/camping-tooltip-pass-20261015/verification.json`. Nie wymaga zmian serialized assetów/scen/prefabów ani migracji zapisu. Można zastosować przez zwykły Assets → Refresh; menu Godmist → Verification → Camping tooltip pass uruchamia testy w otwartym Editorze poza Play Mode. Bez commit/push. PT-01, PT-09 i PT-11 READY_FOR_SMOKE; pozostałe12 tematów otwarte.

## PT-11 — Las: czytelność, skala, granice i koszt oświetlenia — READY_FOR_SMOKE (2026-10-15)

Pass obejmuje całą kartę PT-11. Pełne sprite’y drużyny, wrogów, świeżych summonów i ciał otrzymują czytelny order ponad zasłaniającym drzewem/przeszkodą z istniejącego indeksu komórki. Usunięto runtime półprzezroczystej kopii przeciwnika, jej materiał, odczyty czterech tekstur koron i dodatkowy renderer. Bez wygaszania samych postaci, scenowych skanów ani nowych ownerów. Fog/elevation/invisibility nadal należą do dotychczasowych widoków; podniesiony sprite nie wymusza widoczności. Ciała zachowują ground lighting, ale jego przebieg nie resetuje już ich czytelnego orderu. Aktor ma lane ponad ciałem.

Najbliższe pnie pozostają na rzeczywistej granicy kolizji, bez wizualnego odsuwania całej krawędzi od enemy homes/Enta. Niski boundary scrub usunięty z generowanej granicy; krzaki zostają dekoracją wnętrza. Zachowane logiczne ściany, fizyczne collidery, mgła, podłoże pod lasem i reguły ruchu. Preview PA miało już oddzielny sortAtRoot overlay32000; rzeczywisty render GPU potwierdził numer nad przeszkodą mimo niskiej grupy rodzica, bez dokładania drugiego UI.

**Skorygowane po zgłoszeniu użytkownika:** wcześniejsze wymuszenie skali1/odwrotnej kompensacji było błędne i powiększało aktorów względem miasta. Przywrócono autorską skalę0,7 postaci w eksploracji i walce; do niej dopasowano ciała i narośle, bez powiększania NPC/otoczenia lub zmiany PPU. Aktualne dowody w sekcji korekty regresji skali powyżej. Zwłoki wilka zmniejszone w samym rysunku do długości odpowiadającej żywemu bokowi; narośle zmniejszone do maks20×22 widocznych pikseli i zakotwiczone korzeniami przy ziemi. Pozostaje64×64 canvas/32PPU/Point, bez doraźnego przestawiania PPU.3 kontrolowane edycje built-in image_gen ze shipped referencjami, źródło/native kontrolowane po każdej, stary pipeline BOX/alfa150/paleta/no dither/Aseprite.3 exact roundtrip,4 PNG kopie (w tym brakująca wspólna runtime kopia zwłok), stare źródła zachowane w backup. Prompty/podgląd: `../output/forest-playtest-pass-v1/`.

Potwierdzony koszt: ApplyGroundLighting aktualizował MPB całej dekoracji co0,2s niezależnie od kamery. Dodano culling po już przygotowanych granicach z istniejącym zapasem na ruch; poza nimi nie ma alokowania ani wgrywania MPB. Analogiczny filtr dla wall props. Cadence5Hz i cache masek bez zmian. Kontrolowany benchmark4096 props/64 w kadrze: mediana 18.40→1.86ms (89.9% mniej, 9.9× szybciej), rejestracje4096→64. To CPU tego przebiegu, nie FPS całej gry. Pełny runtime FPS/p95 i akceptacja wyglądu: NOT RUN, wymagany smoke.

429/429 aktualnych unikatowych testów PASS po targeted retest. Obejmują światło/cache/cadence, widoczność/render4 typów sprite’a, skalę/import/bounds, AP overlay, navigation/spawn/forced displacement, ciała oraz wszystkie dotychczasowe VFX. Pierwsze failure nowych testów: brak ścian w fixture, brak UpdateAllSortingGroups przed synchronicznym Camera.Render, niewyczyszczony Camera.targetTexture, błędna nazwa ConfigureVisual i użycie pełnego canvas bounds dla niekatalogowej narośli. Poprawione setupy bez ignorowania logów. Rzeczywisty test prefabów wykrył root0,7 — poprawiona wyłącznie prezentacja childa. Starszy test ghosta zastąpiony obecnym kontraktem pełnego sprite’a. ForestArtBindings zsynchronizowane z dziewięcioma już istniejącymi portretamiVS22 i trzema zmienionymi hashami artu, bez cofania zaimportowanych portretów do klatek idle.

Dowody: `../LocalHandoffs/forest-playtest-pass-20261015/verification.json`. Bez balansu/statów/AI, save schema, serialized scen/prefabów lub zmian zasad nawigacji. Bez commit/push. PT-01 i PT-11 READY_FOR_SMOKE; pozostałe13 tematów nadal otwarte.

### VFX — pass12: Przywódca Bandytów i Ent — READY_FOR_SMOKE (2026-10-15)

Domknięte11 współczesnych definicji: Rozkaz Wzmacniający, Bandycka Mina, Pułapka na Niedźwiedzie, Ogniowa Dysza, Ukryte Przebicie oraz Twarda Kora, Zamach Gałęzią, Spaczona Pięść, Splugawione Korzenie, Zapłon, Piekielne Sanktuarium. Audyt mapowań71/71 współczesnych przeciwników oraz45 bazowych skilli klas. Nie jest to deklaracja pełnego pokrycia każdej gałęzi/talentu/proca ani19 innych definicji legacy/test.

7 nowych PNG64×64 /4 Aseprite /5 klipów. Stara metoda built-in image_gen ze shipped referencją, kontrola źródła i native po każdej generacji. Pierwszy ogień odrzucony za bloom, poprawiona wersja importowana. Premultiplied BOX, alfa150, wspólna paleta256/no dithering, baza trzech płomieni zarejestrowana niezależnie od ruchu iskier.7 exact RGBA/timing roundtrip;110 wcześniejszych klatek SHA bez zmian. Point/RGBA32/Clamp/no mipmaps/32PPU, bez ręcznych zmian serialized assets.

Statyczne wykonanie do wspólnego końca pozy2s; odrębne statusy animowane. Kora: jeden sygnał po compound buffie. Korzenie: tylko udany status. Gałąź/Pięść: neutralne drewniane kontakty po trafieniu, prawdziwy obszar i odrzut. Zapłon/Dysza: dokładna linia/stożek i rotacja, również puste pola, Burn osobno. Ukryte Przebicie: jedna ofiara, bez fikcyjnego obszaru na sąsiadach.

Rozkaz używa oryginalnej zaakceptowanej grafiki nad głową, dopiero po udanym wzmocnieniu; cast intent nie pokazuje sukcesu, compound effects nie dublują znaku. Ukryte miny/sidła bez nowej podpowiedzi przy stawianiu. Aktywacja/detonacja: jeden ActionId, dokładne3×3, kontakty wyłącznie na prawdziwych ofiarach. Zachowane istniejące MapObjectTriggered; driver nie renderuje drugiego centralnego wybuchu. Brak trwałego ognia po minie.

Sanktuarium zachowuje losowe Fields i bezpieczne refuges, warning→fire na następnej turze, trzy tury i once-per-victim-turn. Istniejący renderer markera fire odtwarza cztery niskie, ciepłe klatki; brak dodatkowych rendererów i per-frame skanów. Kontakt na rzeczywistym trafieniu, snapshot przed obrażeniami. Znaczniki nadal sprząta istniejący owner; nie dublowano pól, timerów ani gameplayu.

Weryfikacja: 392/392 aktualnych unikatowych testów PASS, bez nierozwiązanych failure; compile/import oraz focused VFX, boss gameplay, contextual hit, destructible/art/presentation regression. Dowody: `../LocalHandoffs/target-vfx-pass12-20261015/verification.json`. Początkowy helper testu używał nieistniejącego pola baseAccuracy; poprawiono na SkillAccuracy. Starsze testy dopasowano do uzgodnionego pełnego deadline i outcome-gated Rozkazu, bez ignorowania logów lub zmiany zasad. Szczegóły kolejnych retestów zapisane w XML.

Podgląd/prompty: `../output/target-vfx-pass12-v1/review.html`. Import/menu live bez zamykania Editora i bez ręcznego wiring. Manual smoke/FPS NOT RUN. Self-review zakończony. Bez zmian balansu/save/loadout/scen/prefabów.14 tematów PT02–15 nadal otwarte. Bez commit/push.

### VFX — pass11: Król i Szkielet Zabójca — READY_FOR_SMOKE (2026-10-15)

11 pozostałych definicji Katakumb: Królewskie Cięcie, Rozkaz Króla, Straż, Runy Zemsty, Widmowa Fala, Widmowe Ostrze, Krzyk Cienia, Cień Destrukcji oraz Nieugięta Obrona, Wykonać Zadanie i Młot Zniszczenia.13 nowych PNG64×64 /4 Aseprite /8 klipów. Built-in image_gen ze shipped referencją, kontrola źródła i native po każdej generacji. Pierwsze hammer-charge odrzucone za bloom; import korzysta wyłącznie z poprawionej wersji.13 exact RGBA/timing roundtrip;97 wcześniejszych klatek SHA bez zmian. Point/RGBA32/no mipmaps/32PPU; wspólna rejestracja głównych klastrów czterech klatek, bez przesuwania całości przez ruch drobin.

Wykonanie pozostaje statyczne do wspólnego końca pozy. Rozkaz, Obrona i Przygotowanie Młota mają osobne autorskie4-klatkowe pulsy1,2s co4,8s przy opacity0,7, w istniejącym limicie dwóch motywów poniżej hard-control/DoT. Rozkaz odczytuje aktywne źródła oznaczenia, znika po przeniesieniu/wygaśnięciu. Obrona/Hammer odczytują istniejące liczniki i znikają po przerwaniu/zakończeniu/śmierci; brak nowych gameplayowych timerów.

Cięcie/Fala korzystają z rzeczywistego kształtu i rotacji, także pustych pól. Kontakt fizyczny respektuje hit/krew/tarczę; Fala i Ostrze mają widmowy kontakt, ich debuffy osobny wynik. Krzyk pokazuje jeden kontakt dźwięku na odbiorcę niezależnie od obu rolli statusów. Ostrze oznacza wyłącznie rzeczywiste lądowanie po teleportacji. Straż pokazuje wszystkich naprawdę przywołanych rycerzy, również dwóch dodanych przez ownera.

Opóźniony Młot i Cień tworzą osobną korelację prezentacji przy faktycznym rozstrzygnięciu; driver współdzieli2s deadline z pozą uderzenia. Młot zachowuje istniejącą pozę destruction-impact, Cień używa istniejącej pozy castowania przy detonacji. Każdy faktyczny Hit ma opóźnienie prezentacji według indeksu; pudło/prewencja bez kontaktu. Pozycja kontaktu snapshotowana przed obrażeniami, więc reakcja/ruch nie przenosi błysku na nowe pole. Uderzenie ziemi osobno, tylko w rozstrzygniętym obszarze. Duży Cień ma dokładny obrys wyłącznie dark fields; safe fields bez wybuchu. Istniejące markery zapowiedzi i ich cleanup pozostają ownerem pola. Przerwany Młot bez późnych kontaktów, bez zmiany warunku10% HP lub countdown.

Runy zachowują istniejące assety, bramki HP, turę aktywacji, dobór ofiary i rozbrajanie. Przy utworzeniu SkillUsed z ActionId poprzedza pięć błysków w faktycznych pozycjach. Rozbrojenie ma krótki błysk pola, czasowe trafienie kontakt na rzeczywistej ofierze tylko przy dodatniej wartości ataku. Bez drugiej stałej obwódki. Testy używają istniejącego zgodnego ownera run z realnym skillem jawnie załadowanym jak w wcześniejszych testach; nie zmieniano dostępności/loadoutu bossa.

361 aktualnych unikalnych testów PASS: VFX, kontekstowe trafienia, integracja póz, legacy runy, Król v2 i Zabójca. Pierwszy przebieg355/360: stary test run zbierał ostatni dowolny feedback zamiast SkillUsed; trzy nowe testy timingu nie ustawiały PF_BattleUnit; setup Task omijał prawdziwe przejście fazy i kolejkę tur. Poprawiono setup/assertion kontraktu, bez ignorowania logów lub zmian gameplayu. Retest VFX/CatacombsBoss283/284; diagnostyka ostatniego przypadku wykazała phase=True/gate=True, ale active=False: dummy ModifyActionValue wywołuje ReorderParticipants, a test nie inicjalizował timeline. Dodano normalne InitializeTurnOrder; końcowy targeted1/1 PASS. Self-review dodał snapshot pre-damage i regression test ruchu celu, sprawdzone w pełnym targeted VFX. Wcześniej naprawiono błąd kompilacji testu próbującego przypisać read-only ActiveSkillLoadout, używając istniejącego TryInitialize.

Dowody: `../LocalHandoffs/target-vfx-pass11-20261015/verification.json`. Podgląd/prompty: `../output/target-vfx-pass11-v1/review.html`. Import/menu live bez zamykania Editora lub ręcznego wiring; batch przy nieaktywnym Editorze. Manual smoke/FPS NOT RUN. Bez scen/prefabów/serialized Unity assets, save schema, balansu i nowych usług/skanów per-frame. PT-02–PT-15 nadal otwarte, bez commit/push.

Audyt mapowań: 60/71 współczesnych definicji przeciwników; pozostaje11 skilli Przywódcy i Enta.45 bazowych skilli klas bez zmian. To licznik mapowań, nie pełny manual/runtime audyt każdej gałęzi, talentu/proca lub legacy; osobne wizualne rozszerzenia końcowej kontry Obrony nie były dodawane.

### VFX — pass10: natura, trucizna, flara i pułapka — READY_FOR_SMOKE (2026-10-15)

13 kolejnych skilli: Ancient Whip, Vomit, Tree Dark/Binding Roots i Plague Growth, Druid Nature Wrath, dwa przywołania narośli i oba pulsy, Poacher Poison Bag/Bear Trap oraz Shooter Flare.5 nowych PNG64×64 /5 Aseprite /8 rejestracji klipów. Stara metoda built-in image_gen ze shipped referencją, kontrola źródła i native po każdej generacji, Point/RGBA32/no mipmaps/32PPU.5 exact RGBA/timing roundtrip,92 wcześniejsze klatki SHA bez zmian.

Obszar wykonania osobno od rzeczywistej aplikacji efektu. Oliwkowy rozbryzg nie tworzy trwałej kałuży; istniejące Poison/Immobilize/Bleed animują się tylko po faktycznym statusie. Worek zachowuje rzeczywisty obszar2×2, także puste pola, z wyłączeniem niechodliwej podłogi. Duży zasięg plagi pokazuje dokładny obrys, również przy otworach, w istniejącym limicie128 rendererów. Plaga przedłuża istniejące zatrucie; świeży cel nie dostaje wymyślonego poison. Gniew Natury zachowuje wymaganie własnych żywych narośli. Kontakt Ciemnych Korzeni osobno od opcjonalnego zatrucia, Zniewalające Korzenie tylko po rzeczywistym unieruchomieniu, niezależnie od debuffa obrony.

Narośl: błysk wyłącznie po udanym przywołaniu, w pozycji faktycznie zwróconego summona; brak udawanego natychmiastowego leczenia. Puls leczniczy pokazuje obszar i sygnały faktycznie odzyskanego HP. Flara zachowuje sumę odbiorców dwóch trybów All (wrogowie/sojusznicy), bez wymyślonego koła. Jeden błysk nad mierzoną głową każdego widocznego odbiorcy, niezależnie od rolli debuffa/stun; oczyszczenie tylko po rzeczywistym usunięciu stun. Ukryte cele nie dostają beaconów.

Bicz pokazuje pył tylko w wykonanych polach odrzutu i neutralny kontakt po rzeczywistym ruchu. Zatrzymanie przed przeszkodą nie pokazuje żądanego końca ani fałszywej krwi. Pułapka nie dodaje VFX przy stawianiu. Aktualny asset ma WorldSprite, ale nie ma TriggerSprite ani flagi HiddenUntilTriggered; zachowano jego obecne zasady widoczności. Po prawdziwej aktywacji publikowany jest istniejący MapObjectTriggered, a brakującą klatkę aktywacji uzupełnia runtime klip trap-snap. Statyczne szczęki0,5s, bez pozy castowania. Inne triggery nadal0,18s. Bez ręcznego wiring lub zmian serialized assetu.

VFX castowania nadal jednoklatkowe, do wspólnego deadline pozy; statusy osobno animowane. Zachowane mgła, niewidzialność i budżety. Brak nowych usług/skanów per-frame, zmian balansu, movement/status/damage rules lub schematu save. PT-02–PT-15 nadal otwarte.

297 aktualnych unikalnych testów PASS, obejmujących fixture VFX, kontekstowe trafienia, Katakumby, przeszkody, druida i kłusownika. Pierwszy przebieg292/294: test niechodliwej podłogi błędnie używał niejawnej pełnej siatki, a test pułapki zakładał nieobecne pola prezentacji w realnym assetcie. Poprawiono setup siatki oraz rzeczywisty brak aktywacyjnego VFX, dodając test obu istniejących wariantów widoczności. Dodano też weryfikację dużej plagi/otworów/limitu i nowego importu; poprawiono błąd kompilacji negowanego NUnit Contains dla GridPosition. Targeted retest zakończony PASS. Dowody: `../LocalHandoffs/target-vfx-pass10-20261015/verification.json`. Manual smoke/FPS NOT RUN. Import/menu live bez zamykania Editora; batch wykonano przy nieaktywnym Editorze. Podgląd/prompty: `../output/target-vfx-pass10-v1/review.html`.

Audyt mapowań: 49/71 współczesnych definicji przeciwników, pozostaje22 skilli bossów (Król, Szkielet Zabójca, Przywódca i Ent). To licznik mapowań, nie pełny manual/runtime audyt wszystkich gałęzi, talentów, proców i legacy.45 bazowych skilli klas bez zmian. Bez commit/push.

### VFX — pass9: wsparcie i materializacja przeciwników — READY_FOR_SMOKE (2026-10-15)

11 kolejnych skilli: Battle Fervor, Bone Fervor, Bone Guardian, Blood Scent, Quiver of Envy, Life Drain, Dematerialize, Materialize, Archer Hawkeye, Bandit Ambush i Wolf Lick Wounds.3 nowe PNG64×64 /3 Aseprite /5 statycznych rejestracji klipów oraz reuse Rally/Tonic/DeathWish/Heal/Smoke. Stara metoda built-in image_gen ze shipped referencją, kontrola źródła i native po KAŻDEJ generacji, Point/RGBA32/no mipmaps/32PPU.3 exact RGBA/timing roundtrip,89 wcześniejszych klatek SHA bez zmian.

Wieloskładnikowe buffy: jeden sygnał na faktycznego odbiorcę. Bone Guardian: odbiorca i obrońca tylko po udanym założeniu więzi; self-target nie udaje ochrony. Nie dodano nowych stałych więzi ani osobnego VFX proca przekierowania obrażeń. Lizanie Ran i leczenie sojuszników przez Life Drain wymagają rzeczywistego odzyskania HP. Pełne HP/pełna absorpcja nie udają leczenia. Life Drain zachowuje rzeczywisty kontakt i oddzielne leczenie, ten sam presentation ActionId i delay trafienia.

Dematerialize/Ambush: jednorazowy ślad w ostatniej widocznej pozycji. Ślad nie śledzi niewidzialnej postaci; ponowne znikanie już ukrytego aktora nie daje beacona. Materialize zachowuje oryginalną kolejność efektów (leczenie15% maksymalnego HP, potem usunięcie niewidzialności). Buforowany jest wyłącznie sygnał faktycznego leczenia, publikowany po rozstrzygnięciu skilla i odsłonięciu celu. Widmowy kontakt wymaga rzeczywistej zmiany widoczności. Skill nadal wymaga niewidzialnego castera. Bez zmian warunków skilli lub gameplayu.

Jednoklatkowe VFX wykonania do wspólnego końca pozy castowania; istniejące statusy nadal animują się osobno. Zachowane fog/invisibility gates i limit128. Brak nowych skanów per-frame, usług, serialized Unity state, schematu save lub balansu.

257 unikalnych testów PASS po targeted retest całej fixture VFX. Pierwszy przebieg255/257 ujawnił brak sygnału leczenia Materialize: błędnie zakładany typ HealMissingHealthPercent zamiast rzeczywistego HealPercent. Drugi failure dotyczył błędnego testu castowania Materialize przez już widocznego castera; istniejący asset celowo wymaga invisibility. Naprawiono hook i test zgodnie z assetem, bez zmiany tego wymagania. Dowody: `../LocalHandoffs/target-vfx-pass9-20261015/verification.json`. Manual smoke/FPS NOT RUN. Import/menu live wspierają otwarty Editor, bez zamykania i ręcznego wiring; obecny przebieg batch uruchomiono przy nieaktywnym Editorze. Podgląd/prompty: `../output/target-vfx-pass9-v1/review.html`.

Audyt mapowań: 36/71 współczesnych skilli przeciwników; dalsze35 definicji obejmuje naturę/truciznę, flarę/ujawnienie i mechaniki bossów. To licznik mapowań, nie pełny runtime/manual audyt każdej gałęzi efektu, talentu/proca lub legacy. Bazowe45 skilli klas bez zmian. PT-02–PT-15 nadal otwarte. Bez commit/push.

### VFX — pass8: kontakty i doskoki przeciwników — READY_FOR_SMOKE (2026-10-15)

15 współczesnych skilli Katakumb/Lasu i legacy `skill.enemy.bone_sword`: maczuga/miecz/cięcie krzyżowe, rozszarpanie/ugryzienia, trzy noże, trzy doskoki, Dziki Ryk, Widmowe Cięcie i Starożytna Inkantacja.3 nowe PNG64×64 /3 Aseprite /4 statyczne klipy, plus reuse istniejących kontaktów/pyłu. Stara metoda image_gen ze shipped referencją, kontrola źródła/native po każdej generacji, Point/RGBA32/no mipmaps/32PPU.3 exact RGBA/timing roundtrip,86 wcześniejszych klatek SHA bez zmian.

Kontakty po faktycznym trafieniu, krew wyłącznie po utracie HP i na cielesnym celu. Istniejący selektor materiału rozpoznaje teraz również wysłane szkielety, upiora i drzewo o ID bez słów skeleton/ghost. Zmiana tylko wyglądu kontaktu, bez odporności na krwawienie. Cross Cut zachowuje każde trafienie i kolejność; kontrola/DoT pozostają osobnymi statusami.

Doskoki pokazują wyłącznie rzeczywiste zatrzymanie po rozstrzygnięciu blokad; success bez ruchu nie tworzy lądowania, częściowy ruch nie udaje dotarcia do celu. Brak beacona niewidzialnego właściciela. Kontakt Widmowego Doskoku osobno od lądowania. Dziki Ryk: dokładny obrys zasięgu, także puste pola graniczne, i jeden sygnał na rzeczywistego widocznego odbiorcę niezależnie od obu rolli statusu. Pełne wypełnienie przekroczyło128 sprite’ów przy zaokrąglanym Radial6; obrys zachowuje zasięg w istniejącym budżecie. Cele mają priorytet nad podłożem. Owner chwilowych efektów odzyskuje wygasłe sloty przy osiągnięciu limitu, jeśli kolejny cast zaczął się w Update przed poprzednim cleanup w LateUpdate. Brak nowego skanu na każdej klatce lub zwiększania limitu.

237 unikalnych testów PASS po naprawie limitu obszaru i targeted retest VFX201/201. Weryfikacja batch przy zamkniętym Editorze; import/menu live wspierają otwarty Editor bez zamykania i bez wiring. Brak zmian balansu, movement/damage rules, scen, serialized Unity state i save schema. Manual smoke/FPS NOT RUN. Podgląd/prompty: `../output/target-vfx-pass8-v1/review.html`; dowody: `../LocalHandoffs/target-vfx-pass8-20261015/verification.json`.

Audyt mapowań: 25/71 współczesnych skilli przeciwników ma mapowanie VFX; dalsze46 definicje obejmują wsparcie, naturę/truciznę, niewidzialność/ujawnienie i specjalne mechaniki bossów. To nie jest pełny runtime/manual audyt każdej gałęzi efektu, talentu lub proc. Bazowe45 skilli klas pozostają z poprzednich passów. PT-02–PT-15 nadal otwarte. Bez commit/push.

### VFX — pass7: Tonik i przełączniki — READY_FOR_SMOKE (2026-10-15)

4 skille: Pragnienie Śmierci, Ofiara, Wycieńczające Zaklęcia i Tonik Pobudzający.13 nowych PNG64×64 /4 Aseprite /7 klipów. Trzy czteroklatkowe subtelne pulsy z przerwami (1,2s aktywne, okres6s albo4,8s), trzy statyczne aliasy najczytelniejszej klatki oraz osobny Tonik. Wykonanie nadal jednoklatkowe do wspólnego końca pozy castowania.

Pętle czytają rzeczywisty stan przełącznika, a Ofiara również faktyczny buff. Współdzielą dwumotywowy budżet, po kontroli/DoT/oznaczeniach. Wygaśnięcie po wyłączeniu, nieopłaconym podtrzymaniu, śmierci, ukryciu i unbindzie. Tonik pokazuje początkowy boost i dopiero rzeczywisty opóźniony spadek szybkości; recast/cancel bez fałszywego sygnału. Wycieńczenie celu tylko po sukcesie rzeczywistego modifiera. Kontekst trafienia niesie czas prezentacji i parent presentation id; gameplay action ids i reguły są bez zmian. Brak zmian balansu/save schema/scen.

Stara metoda image_gen ze shipped referencją → premultiplied BOX/alfa150/wspólna paleta/no dithering → Aseprite, kontrola każdej generacji. Pierwsza za cienka wersja Pragnienia odrzucona. Point/RGBA32/no mipmaps/32PPU,13 exact RGBA/timing roundtrip,73 poprzednie klatki SHA bez zmian. 280 unikalnych testów PASS po targeted retest jednego scenariusza testowego z brakującym zasobem. Weryfikacja batch przy zamkniętym Editorze; import/menu live wspierają otwarty Editor bez zamykania lub wiring. Manual smoke NOT RUN. Podgląd/prompty/metoda: `../output/target-vfx-pass7-v1/review.html`; wyniki: `../LocalHandoffs/target-vfx-pass7-20261015/verification.json`.

Domknięto mapowanie bazowych45 skilli klasowych z planu (coverage.json zawiera audyt mapowań). Nie oznacza to pełnego runtime/manual pokrycia wszystkich talentów/proców ani wszystkich135 definicji. Kolejny zakres VFX: pozostałe rodziny skilli przeciwników oraz legacy/proc. PT-02–PT-15 nadal otwarte. Bez commit/push.

### VFX — pass6: pełny czas castowania, oznaczenia i przemiany zasobów — READY_FOR_SMOKE (2026-10-15)

PT-01 zaimplementowane do smoke: VFX wykonania skilla, warstwy przy pozie i target-head utrzymują się do wspólnego końca pozy. ActionId wiąże cast z celami; pasywne kopie dzielą prezentację, zachowując osobne gameplay action ids. Opóźnione trafienia zachowują kolejność, identyczne statyczne kontakty nie mnożą rendererów. Tick leczenia/pola i aktywacja pułapek mają osobny krótki lifecycle.

6 skilli: Rozkaz Unicestwienia, Słaby Punkt, Przejęcie Mocy, Przemiana Witalności, Rytuał Odnowy i Święta Transfuzja.10 nowych PNG64×64 /4 Aseprite /6 klipów; dwa czteroklatkowe pulsy oznaczeń oraz statyczne kontakty. Oznaczenia czytają OrderTurns/WeakPointTurns, zachowują dwumotywowy budżet i priorytet kontroli/DoT. Ukrycie po śmierci, niewidzialności, wygaśnięciu i unbindzie. Koszt HP oraz odzysk zasobu/leczenie mają osobne sygnały tylko po faktycznej zmianie. Rytuał sygnalizuje rzeczywiste ticki leczenia. Brak zmian balansu i save schema.

Stara metoda image_gen ze shipped referencją → premultiplied BOX/alfa150/wspólna paleta/no dithering → Aseprite; kontrola po każdej generacji. Point/RGBA32/no mipmaps/32PPU. 208 unikalnych testów PASS (wyniki pełnego przebiegu i targeted retest),10 exact RGBA/timing roundtrip,63 poprzednie klatki SHA bez zmian. Weryfikacja batch przy zamkniętym Editorze; import i menu live wspierają otwarty Editor bez zamykania i bez wiring. Manual smoke NOT RUN. Podgląd/metoda: `../output/target-vfx-pass6-v1/review.html`; dowody: `../LocalHandoffs/target-vfx-pass6-20261015/verification.json`.

Aktualizacja pass7: death_wish, tonic, offering, tiring zostały zaimplementowane i czekają na smoke; dalszy zakres przeciwników/proców/legacy z planu135 pozostaje otwarty. Pozostałe14 tematów playtestu PT-02–PT-15 nie były implementowane. Bez commit/push.

### VFX — pass5: pola, aury i przemieszczanie — READY_FOR_SMOKE (2026-10-15)

8 skilli: Młyniec/Okrzyk, Fala Przyciągająca/Pole Osądu, Ognista Aura, Hak/Zamiana/Krok w Cień.10 nowych klatek64×64 /4 Aseprite /5 klipów. Dwie animowane pętle rzeczywistego pola/aury; wykonanie skilli nadal jednoklatkowe. Stara metoda ze shipped referencjami i kontrolą po każdej generacji; pierwszy zbyt cienki okrzyk odrzucony. Point/RGBA32/no mipmaps/32PPU, bez ręcznego wiring.

Obszary używają gameplay geometrii i widoczności, również pustych pól; lokalny refresh co0,2s lub po ruchu/zasięgu, reuse i budżet128 persistent rendererów osobno od128 chwilowych. Cleanup po końcu sesji/wygaśnięciu/wyłączeniu. Kontakt pola tylko przy ticku, Młyniec per realne trafienie z przerwami przy pudłach. Hak zapisuje wykonane kroki zamiast żądanej trasy; odzyskanie sztyletu bez fałszywych obrażeń. Zamiana ma dwa punkty po sukcesie, Shadow tylko dym startu bez beacona niewidzialnego właściciela. Brak zmian reguł/saved state.

Import i weryfikacja w istniejącym otwartym Unity, bez drugiego procesu lub wymuszania Play Mode.147 unikalnych testów PASS po diagnozie dwóch błędnych założeń testowych, finalny pełny przebieg147/147 i końcowe rejestracje5/5;53 wcześniejsze klatki SHA bez zmian. Testy coroutine i manual smoke NOT RUN. Podgląd/metoda: `../output/target-vfx-pass5-v1/review.html`. Dowody/smoke: `../LocalHandoffs/target-vfx-pass5-20261015/HANDOFF.md`. Pełny plan135 nadal wymaga dalszych passów. Bez commit/push.

### VFX — pass4: wsparcie i święta magia — READY_FOR_SMOKE (2026-10-15)

11 kolejnych skilli: tarcza/skupienie Maga; błogosławieństwo, regeneracja, blask, Wyrok, Miażdżący Cios i Oślepiające Światło Paladyna; ochrona/gotowość/Bandaż Wojownika.4 nowe PNG64×64 /4 jednoklatkowe Aseprite,6 statycznych klipów; stara metoda z kontrolą źródła/native po każdej generacji. Point/RGBA32/no mipmaps/32PPU; bez ręcznego wiring.

Efekty wsparcia po sukcesie; Bandaż deduplikuje leczenie/oczyszczenie, brak zmian nie daje błysku. Regeneracja sygnalizuje nałożenie i faktyczny odzysk HP, także po opuszczeniu walki przez healera. Zwykłe Heal zachowuje poprzednie zachowanie. Ochronny Blask oznacza unik, nie tarczę HP. Kontakty ataków wymagają trafienia, oślepienie sukcesu debuffu; oba zachowują kolejność rzeczywistych celów łańcucha. Statusy i wyjątki Freeze/Paralysis bez zmian.

163/163 testów PASS,0 failure/skipped, Unity EXIT0;49 wcześniejszych klatek SHA bez zmian. Podgląd/prompty/metoda: `../output/target-vfx-pass4-v1/review.html`. Dowody/smoke: `../LocalHandoffs/target-vfx-pass4-20261015/HANDOFF.md`. Manual smoke NOT RUN. Pełny plan135 nadal wymaga dalszych passów. Bez commit/push.

### VFX — pass 3: energia i ataki dystansowe — READY_FOR_SMOKE (2026-10-15)

14 kolejnych skilli maga/zwiadowcy oraz dystansowych przeciwników Lasu/Katakumb.6 nowych PNG64×64 /6 jednoklatkowych Aseprite,7 statycznych klipów. Źródła i native sprawdzone po każdej generacji; shipped referencje, BOX/alfa150/shared256/no dither. Point/RGBA32/no mipmaps/32PPU przez istniejącego importera, bez ręcznego wiring.

Dokładne podłoże z geometrii gameplayu (trzy promienie/linia/prostokąt/obszar wokół wybranego pola), również puste pola. Kontakt per rzeczywiste trafienie, tarcze/kości zamiast krwi; seria i Rykoszet mają osobne statyczne okna. Dym sygnalizuje sukces osłabienia nad głową. Mgła, niewidzialność i cleanup zachowane; statyczne freeze/paralysis i dotychczasowe pętle statusów pozostają bez zmian.

126 unikalnych testów PASS po retestach,0 nierozwiązanych failure, Unity EXIT0;43 wcześniejsze klatki SHA bez zmian. Podgląd: `../output/target-vfx-pass3-v1/review.html`. Dowody/smoke: `../LocalHandoffs/target-vfx-pass3-20261015/HANDOFF.md`. Manual smoke NOT RUN. Pełny plan135 definicji nadal wymaga kolejnych passów. Bez commit/push.

### Reguła prezentacji VFX — 2026-10-15

Efekty pojawiające się przy wykonaniu skilla są statycznymi one-frame freeze’ami utrzymywanymi przez krótki czas. Efekty aktywnych statusów, np. stun, poison, immobilize, bleed i burn, są animowanymi pętlami. Wyjątki: freeze i paraliż utrzymują nieruchomą pozę postaci oraz statyczny overlay mrozu/kamienia przez czas statusu. Zdjęcie statusu przywraca animację, a śmierć ma pierwszeństwo. Reguła obowiązuje też kolejne passy.

### VFX — pass 2 — 2026-10-15

READY_FOR_SMOKE:5 nowych prezentacji statusów (stun/poison/immobilize animowane; freeze/paralysis statyczne z blokadą pozy) oraz statyczny kontakt obuchowy.15 nowych klatek64×64 /6 Aseprite;6 generacji sprawdzonych źródłowo i native, stare referencje/pipeline, import Resources Point/RGBA32/no mipmaps/32PPU.1752 zmierzone sylwetki idle/walk kotwiczą głowę i overlay do widocznych pikseli. Oba ownerzy widoku korzystają z istniejącego lifecycle. Najwyżej2 motywy statusów, opcjonalnie1 dodatkowy renderer koloru hard statusu; maksymalnie3 renderery na widok.86 unikalnych testów PASS,28 wcześniejszych klatek zachowane, bez nowego stanu save i bez ręcznego wiring. Podgląd `../output/target-vfx-pass2-v1/review.html`; dowody i smoke `../LocalHandoffs/target-vfx-pass2-20261015/HANDOFF.md`. Manual smoke NOT RUN.

### VFX celów i statusów — pierwszy pass — 2026-10-15

READY_FOR_SMOKE: leczenie paladyna, cięcia Wojownika/Przywódcy, pojedynczy kontakt Kuli Ognia, rzeczywisty obszar Wstrząsów i animowane Burn/Bleed. Feedback wykonania jest oddzielony od pozy castowania i od aktywnego statusu. Krew wymaga udanego kontaktu oraz obrażeń HP celu z krwią; pełna tarcza i cele bez krwi mają odpryski. Puste pola rzeczywistego obszaru również dostają efekt; pozycja trafienia jest zapisana przed zmianami efektu. Statusem zarządza istniejący lifecycle, wspólny player prezentuje go przy widoku walki i eksploracji. Efekty respektują ukrycie i cleanup, bez persistence; najwyżej128 chwilowych rendererów oraz2 statusowe na widok. 28 nowych klatek /7 animacji/Aseprite, stara metoda, import Resources przez Unity API. 78 unikalnych testów PASS i1684 wcześniejsze pliki grafiki SHA bez zmian. Podgląd `../output/target-vfx-pass1-v1/review.html`; dowody/smoke `../LocalHandoffs/target-vfx-pass1-20261015/HANDOFF.md`. Pozostała lista135 to kolejne passy, manual smoke NOT RUN.

### Przywódca Bandytów — grafiki podpięte — 2026-10-15

READY_FOR_SMOKE:271byte-exact importów całego zatwierdzonego zestawu, poprzezUnity Editor API. CharacterAnimationResolver/CombatPoseResolver obsługująbandit-leader-p1/p2;128motion/72pozy/29VFX/2ciała/6przejście/UI128/32. Osobne czasyVFX180ms i kotwy efektów nad celem; defaultowe starekatalogi zachowują poprzednie zachowanie. Pułapki mają tylko krótki feedbackpo aktywacji, cleanup bezdetonacji; krótkiconceal nie jest stałym beaconem.91unikalnych testów PASS po retestach (w tymGPU3/3),271kopii/metas i1684źródeł SHAzweryfikowane. Manual smoke encountera pozostaje doużytkownika: `combat encounter.forest.bandit_leader`; dowody `../LocalHandoffs/bandit-leader-art-integration-20261015`. Bez zmian regułfazy/obstawy/statów/łupów.

### Przywódca Bandytów — UI gotowe do oceny — 2026-10-15

`../output/bandit-leader-ui-v1`:2portrety obu faz128×128 +6ikon skilli i2pasywy32×32,10Aseprite; ART_READY_FOR_REVIEW_NOT_IMPORTED. Stare shipped referencje portretu i ikon, image_gen/BOX/shared256/no dither;3generacje z oceną źródeł i native, poprawiony gest Rozkazu.10/10 roundtripów PASS,1601 wcześniejszych plików SHA zachowane. Identyfikatory powiązań w ui-mapping.json, bez zmian reguł/statusów/importu. Następny etap to podpięcie całego zestawu grafik Przywódcy oraz smoke w Unity.

### Przywódca Bandytów — przejście i ciała — 2026-10-15

Paczka `../output/bandit-leader-finish-v1` jest ART_READY_FOR_REVIEW_NOT_IMPORTED:6klatek S po200ms (dokładne idleendpointy +4noweklatki),2 osobne leżące ciała,3Aseprite,32PPU/scale1. Identyczny miotacz/strój/buty przez odsłonięcie twarzy i dobycie szabli.8/8 roundtripów i timingu PASS;1540 wcześniejszych plików SHA zachowane. Przy imporcie przejście wiązać z istniejącą następną turą po śmierci obstawy, nie progiemHP. Logiczny facing pozostaje bez zmian; klipS ma charakter prezentacyjny. Portrety/ikony i import pozostają kolejnymi etapami.

### Combat summon presentation — 2026-10-15

- Runtime summoned map objects register with dungeon ground lighting; spawned character units register with upright actor lighting. Both registrations move/clean up with their owning view.
- Authored32PPU character/summoned-object sprites render at scale1; the old0.7 multiplier is retained only for legacy art. Forest ground shading samples the world on a32-pixel grid.
- Combat pose caches must recreate destroyed Unity sprites rather than return a stale actor/VFX wrapper. Skill→borrowed view integration preserves the freeze-frame across animation and presentation refresh.

Ten plik jest głównym źródłem prawdy dla zasad gameplayowych i wymagań projektowych Godmist.

Przy implementacji należy czytać wyłącznie sekcje istotne dla bieżącego zadania,
a nie cały dokument.

Sekcje opisujące aktualny stan implementacji są pomocnicze i mogą się z czasem
zdezaktualizować. W przypadku konfliktu dotyczącego tego, co aktualnie istnieje
w projekcie, źródłem prawdy jest bieżące repozytorium.

Nie należy wymyślać brakujących zasad gameplayowych. Jeżeli zasada wymagana
do implementacji nie jest określona w tym pliku ani innych właściwych dokumentach,
należy zgłosić brak głównemu agentowi.

# Godmist - Project Context

## Bandit Leader art — separate VFX — 2026-10-15

`../output/bandit-leader-vfx-v1` is ART_READY_FOR_REVIEW:29 held VFX +2 revealed-only world props,31PNG/31Aseprite180ms. Directional fire/cut/strike ×8; shared order/placement/trap-snap/mine-burst/brief conceal. Existing bear trap reused byte-exact; new mine. Established image_gen/shippedVFX/pose guides, BOX/alpha150/shared256/no dither/exact mirrors/Aseprite.10 generations reviewed, thrustS/N corrected to actual visible blade trajectory.31 exact roundtrips PASS;36 measured composite bindings +2 explicit world-footprint offsets;1203 earlier files unchanged. Not full animated sequences. No Unity import/Resources/meta/gameplay changes; conceal must not become a persistent locator and traps stay hidden until triggered. Transition/corpse, portraits/icons and native integration/manual smoke remain.

## Bandit Leader art — held action poses — 2026-10-15

`../output/bandit-leader-poses-v1` is ART_READY_FOR_REVIEW:72 actor-only held skill/reaction culminations (9 gestures ×8 directions),72 editable single-frame Aseprite files250ms. PhaseI trap/mine share crouch, plus order/hurt/dodge; phaseII cut/fire/strike/hurt/dodge. Established image_gen/reference/BOX/alpha150/frozen96 method, physical standing body~58px at32PPU preserved;128canvas/pivot(64,24) adds margins to prior96canvas/pivot(48,8). Standing calibration is excluded from output; crouches are not enlarged.45 authored and27 exact mirrored poses;72 exact roundtrips PASS,807 earlier files unchanged.18 generations recorded/reviewed, corrections cover nozzle handedness/direction/diameter, thrust facing and alpha artefacts. These are held poses, not full action animations. No Unity import/Resources/meta/gameplay changes. SeparateVFX, transition/corpse, portraits/icons and native import/manual smoke remain.

## Forest rendering regression fix — 2026-10-15

Forest prop separation is part of the sprite's lit colour, never an unlit overlay added after lighting. Opaque pixels skip neighbour-alpha reads; empty sprite/feathered-underlay pixels are discarded before lighting, and empty ordinary underlay pixels before forest-surface sampling. Prepared lighting coverage has hysteresis across the 5 Hz updates: small pans keep shadow UVs and contact meshes stable; scheduled near-edge refill retains movement reserve and escaping teleports/large zooms refresh immediately. Dynamic lighting cadence remains unchanged. 99 unique focused EditMode tests PASS; runtime FPS remains unmeasured pending manual smoke. Evidence: `../LocalHandoffs/forest-render-performance-20261015`.

## Purpose

This file stores the current design context for the diploma-engineering project so the game assumptions can be reused while designing UML and Unity classes.

## Current High-Level Summary

- Project type: singleplayer dungeon crawler RPG with turn-based combat.
- Project goal: support academic documentation such as UML, functional and non-functional scope, and real-world domain description.
- Likely implementation context: Unity.
- Main gameplay loop: explore dungeons, fight enemies, develop characters, collect equipment, complete quests, defeat bosses.
- Exploration is top-down 2D and broadly inspired by Baldur's Gate style party exploration, but adapted to a 2D top-down format.
- Dungeon generation is mixed:
  - mission/story dungeons may be predefined
  - ordinary dungeons may be procedurally/randomly generated
- The overall game goal is story progression and character improvement through quests, dungeon exploration, leveling, skill trees, equipment upgrades, and NPC trading.

## Known Story and Character Assumptions

- The main character is `Jerzy`.
- Jerzy starts at level `1`.
- Jerzy starts as `Scout` (`Zwiadowca`).
- Jerzy's initial sandbox/stat tuning should use approximately `60` Speed; this is a character-specific tuning value and may be fine-tuned later.
- The player does not create a character from scratch.
- The player starts immediately in the city hub `Arungard` for the first version.
- Jerzy is the main character, but can be removed from the active party and replaced by another character.
- Jerzy is required in quests where he participates in dialogue or is explicitly required for a mission dungeon.
- The same "required for quest" rule can also apply to side characters.
- Jerzy always remains a Scout and cannot change class.
- Companions are recruited during the game, mainly through quests.
- AI-controlled allies may also appear.
- Some companions may be generic mercenaries.
- Generic mercenaries are available only when the active party is not full or when a companion is unavailable and needs recovery.
- In special story cases, a companion may leave permanently, but is replaced by another character of the same class.
- Mercenaries have persistent IDs, but return to the inn when no longer needed, for example when the original companion returns and the party is full.

## Known Controllable Unit Types

- Main hero
- Companion
- AI ally
- Enemy
- NPC outside combat
- Third-party combat groups may exist and can be friendly, hostile, or mixed toward the other sides.
- Outside combat, NPCs may be human-like social/trade entities or monsters, but in combat they resolve into the same combat-character concept.
- The player directly controls companions in combat.
- Other allied units may exist in battle and be controlled by AI.

## Known Playable Classes

- Warrior (`Wojownik`)
- Scout (`Zwiadowca`)
- Mage (`Czarodziej`)
- Paladin (`Paladyn`)

## Known Class Resources

- Warrior -> Rage
- Scout -> Momentum (`Ped`)
- Mage -> Mana
- Paladin -> Mana

Resources are used for skills. Characters also have resource regeneration as a stat.

### Resource Rules

- All resources use `currentValue` and `maxValue`.
- `Rage`
  - Generated by direct attacks, including misses and attacks dodged by the target. Damage-over-time ticks do not generate base Rage.
  - Starts at `0`.
  - Each successful hit, miss or dodged attack grants the attacking unit `10` base Rage until maximum Rage is reached. A miss also prevents next-turn decay, including at maximum Rage; resource-generation blocks still apply. Talent bonuses triggered by damage still require damage. Taking damage does not generate base Rage.
  - If no Rage was gained during the previous turn, Rage starts dropping by `10` per turn.
  - Used to cast skills.
  - If a skill costs more than `maxRage`, it can still be used when current Rage is exactly at max.
  - In that case the character gains a temporary weakening effect lasting `1` turn per each missing `15` Rage.
  - The weakening reduces damage by `1%` per each missing `5` Rage.
  - Each point of Rage reduces skill accuracy by `0.2`.
  - Warrior base max Rage is `50`, though some enemies may have more.
  - Outside combat, Rage decays over time.
- `Momentum` (`Ped`)
  - Starts at `0`.
  - Maximum Momentum is `200`.
  - Above `100`, gain efficiency becomes `50%`.
  - Above `100`, every `25` Momentum increases speed by `4`.
  - Used to cast skills.
  - Generated by movement.
  - Every moved tile increases Momentum by `2` for each spent action point.
  - Does not decay outside combat.
  - Does not regenerate outside combat.
- `Mana`
  - Maximum Mana depends on class and equipment.
  - For example, mage wands may increase it.
  - Used to cast skills.
  - Player heroes regenerate at turn end according to unspent AP: round(Regeneration × 2 × clamp01(CurrentAP / MaxAP)).
  - NPCs and friendly AI summons retain their fixed start-of-turn regeneration.
  - Outside combat, regenerates normally every 6 seconds, without an AP multiplier.
- Only Rage may go below the normal maximum-cost rule when using over-cap skills.
- Maximum resource values depend on class, passives, and equipment.
- A shared `ResourcePool` model is viable, with per-resource behavior delegated to resource rules.
- `ResourceRules` / `ResourceRulesManager` is a preferred direction for resource-specific logic.
- Entering town resets Momentum to `0`, Mana to `max`, and Rage to `0`.

## Known Progression Assumptions

- Character levels range from `1` to `50`.
- Experience is awarded after combat or quests.
- On level up, the character gains a skill-tree progression point.
- Experience is tracked separately for each character.
- Reserve characters do not gain experience.
- A character may gain multiple levels at once.
- Stats grow automatically based on class.
- Level up does not include manual stat point allocation.
- Automatic stat growth is incremental. Each class/enemy archetype defines a base growth amount per stat, multiplied by the level-band coefficient `k`: levels `2-10` use `k = 1`, `11-20` use `k = 2`, `21-30` use `k = 3`, `31-40` use `k = 5`, and `41-50` use `k = 9`. For example, warrior `MaxHealth` growth of `12.5` gains `12.5` per level in levels `2-10`, then `25` per level in levels `11-20`.
- Known experience formula from the previous conversation:

`round(12 * (L^1.5) + 12)`

## Known Character Stats

- HP / Max HP
- Attack as min-max range
- Physical defense
- Magical defense
- Dodge
- Critical chance
- Critical damage
- Speed
- Resource regeneration
- Accuracy
- Honor

`Honor` is an out-of-combat stat that affects prices and whether NPCs are willing to trade. It decreases on escape and increases from combat and quests.
Honor is not clamped to −100…100. Approved category bands (lower inclusive, upper exclusive):
- below −100: Wygnaniec
- −100…−75: Haniebny
- −75…−50: Niechlubny
- −50…−20: Podejrzany
- −20…40: Rekrut
- 40…100: Najemnik
- 100…150: Bojownik
- 150…200: Rycerz
- 200 and above: Przywódca

Priest companion recovery uses the speaking character's honor tier, without loyalty.
Tier thresholds and total percentage discounts are exposed in Game Defines. Discounts default to 0% until balance is approved.
Recovery costs ceil(300 × 4^((character level − 1)/10)); newly rolled enemy gold uses baseline ceil(10 × 4^((dungeon level − 1)/10)), with an independent uniform integer roll from ceil(50% baseline) through floor(150% baseline). Previously rolled saved rewards remain unchanged.
Main-character death or an active party with nobody alive causes campaign defeat; a living active member outside the room can still rescue downed members. Dead non-main companions remain recoverable at the priest, preserving equipment and progression. StoryLocked members remain locked. Recovery restores full HP/resources and uses an active slot if available, otherwise the bench.
Campaign defeat offers the latest valid current-campaign save, another valid current-campaign save, or the main menu. No valid saves disables both load choices. Saving after defeat is blocked.

### Stat and Combat Formula Rules

- Attack damage is rolled from the `minAttack-maxAttack` range.
- Damage reduction formula:

`damage = a^2 / (a + o)`

where:
- `a` = attacker attack value
- `o` = target defense value

- Physical defense reduces only physical damage.
- Magical defense reduces only magical damage.
- Neither defense reduces inevitable damage.
- Hit chance formula:

`hitChance = c^2 / (c + u)`

where:
- `c` = accuracy
- `u` = dodge

- Accuracy is the average of weapon accuracy and skill accuracy.
- Dodge is compared against accuracy.
- `Accuracy` is not really a persistent stat; it is a calculated value from `weaponAccuracy` and `skillAccuracy`.
- `SkillAccuracy` is used only by `DealDamage` effects. Other effects do not use hit chance; their application is governed by their own resistance/effect-chance rules where applicable.
- Critical chance and critical damage primarily affect only damage types that can crit.
- Exceptions exist through passives and active abilities, for example magical/direct attacks that can crit or skills that always crit.
- `CritDamage` is a multiplier. For example, `1.5` means a critical hit deals `50%` more damage than a normal hit.
- `CritChance` is stored as a normalized value from `0` to `1`; for example, `0.25` means `25%`.
- Until equipment is implemented, `WeaponAccuracy` is stored as a temporary effective value on `CharacterInstance`. It will later be derived from the equipped weapon.
- There is no special hard-coded basic attack. Every attack is a skill with damage effects.
- Damage effects normally roll the character's `MinAttack-MaxAttack` range. A skill may instead use `MinAttack`, `MaxAttack`, or one of these sources multiplied by a configured multiplier.
- Bleed and other damaging periodic status effects use the caster's average attack, not a flat `BaseValue` or fixed periodic-damage payload.

### Damage Types

- `Physical`
  - Does not always hit.
  - Can critically hit.
  - Reduced by physical defense.
- `Magical`
  - Always hits.
  - Cannot critically hit.
  - Reduced by magical defense.
- `Inevitable`
  - Always hits.
  - Can critically hit.
  - Not reduced by any defense.
- `Direct`
  - Always hits.
  - Cannot critically hit.
  - Not reduced by any defense.

### Resistances

- Bleeding resistance
- Poison resistance
- Fire resistance
- Weakening resistance
- Limitation resistance
  - stun
  - immobilize
  - sleep
  - paralysis
- Forced-movement resistance
  - knockback
  - charge
  - pull
  - retreat

Resistances are better modeled as a dedicated resistance set rather than ordinary flat stats.

### Enemy Type Categories

- Confirmed enemy-type categories currently include:
  - undead
  - beast
  - demon
  - humanoid

### Speed and Turn Order Rules

- Turn order uses an `AV` style algorithm.
- Formula:

`AV = 10000 / speed`

- Combat loop:
  - reduce every participant AV by `1`
  - when someone's AV reaches `0`, that unit acts
  - after that action, recalculate the unit AV from speed again
- Speed also determines action points in a turn:

`AP = speed * const`

- `const` remains a balancing value for later tuning.
- A provisional placeholder value can be `0.25`.

## Known Skills and Progression Assumptions

- Skills may come from class/start setup.
- Skills may come from skill trees.
- Skills may come from unique equipment such as weapons, armor, and jewelry.
- Passive abilities may also come from equipment independently of skill trees.
- Each character has three branching development paths.
- Skill trees mostly provide passives, sometimes conditional passives, and more rarely active skills.
- Active skills can be changed outside combat.
- Each class has exactly `3` skill trees.
- Each skill tree contains around `4-6` active skills and the rest are passives.
- A character normally has `5` active skill slots.
- Skill counts may be increased by abilities and necklaces.
- One slot is always occupied by the basic attack.
- Basic attack is also a skill, and each class has its own basic attack.
- Skills granted by equipment count toward the active skill limit.
- Active skills can be changed anywhere outside combat.
- Each class has a unique set of skill trees.
- The 3 skill trees of a class are independent from each other.
- Nodes may require one or many prerequisite nodes.
- Every node belongs to a specific tree level.
- Tree levels `1` and `2` are available from the beginning.
- Skill tree layout has `7` levels: levels `1..5` each contain `5` nodes, level `6` contains `3` centered nodes, and level `7` contains one centered capstone.
- Tree levels `1` and `2` are available from the beginning; levels `3` and `4` require `10` points invested in that tree, levels `5` and `6` require `20`, and the level `7` capstone requires `30`.
- A whole skill tree allows at most `50` invested points. The total rank capacity of a level is authored per node and is expected to average roughly `8..10` points.
- Nodes may hold `1..3` invested points.
- Nodes that unlock active skills always allow exactly `1` invested point.
- Skill Tree Workspace UX shows the whole currently selected tree at once: it must not use a Scroll View. All seven levels fit in the floating window and prerequisite connections are drawn between their node icons.
- Tier gates are communicated by a visible enclosure around levels `3-4`, `5-6`, and `7`, labelled respectively `0/10`, `0/20`, and `0/30` from the current invested points in that tree. A gate enclosure disappears once its threshold is met; it is not a text separator between rows.
- A node may:
  - unlock a new active skill
  - grant a passive effect
  - grant a conditional passive effect
  - modify an existing skill
- Every invested point in a tree also grants a per-tree passive bonus that scales linearly with invested points.
- Example per-point passive:
  - `Brawler`: `+1% max rage`
  - `+2%` of spent rage is converted into health per invested point
- Respec is not currently planned, but may be added later.

## Known Downed / Death System Assumptions

- A character may become downed.
- A downed character can be revived during combat.
- There are `3` turns to revive a downed character.
- Timed statuses continue to expire during each skipped downed turn (approved 2026-10-19). This includes buffs, debuffs, control and DoT; paralysis does not freeze these downed clocks. Persistent/combat-scoped effects retain their existing lifetime.
- A missed DoT tick at 0 HP consumes its share of the remaining damage budget without damage, hit reactions, passive rewards, RNG or an extra death-timer penalty. Direct attacks on downed targets retain their existing rules.
- Timed shield layers also expire. Regeneration/sleep timers elapse without performing their healing/resource actions or automatically reviving the downed character.
- Revival preserves only the remaining statuses/ticks: no automatic cleanse or catch-up damage. Outside combat, downed status clocks use the normal 6-second exploration interval, without counting down to permanent death.
- `Downed` prolongs combat only while at least one player-side unit remains `Alive` and can revive allies. If no
  player-side unit is `Alive`, combat immediately resolves as `Defeat`; it does not wait for every downed unit to
  exhaust its individual death timer.
- Every hit on a downed target accelerates death by one additional turn.
- Reviving a downed target automatically ends the acting unit's turn.
- If the main character dies, or the entire party dies, the game returns to a save.
- If a companion dies, they may be bought back in town.
- Some quest-important characters may be recovered for free.
- If the player escapes from combat, downed companions are abandoned.
- After combat, all allied downed units automatically stand up.
- Some skills or monster passives can cause immediate death instead of the downed state.
- Enemies can also become downed, not only player units.
- Neutral characters and allies can also be revived.
- Automatic post-combat revival applies to allies after combat.
- Outside combat, lethal damage (including DoT and traps) leaves a character Downed. A living party member must approach and interact to stand them up; there is no automatic exploration revival. Post-victory recovery remains automatic.
- A character may be alive but still unavailable for story reasons.
- Recovered companions return ready to use.
- Character state should be modeled with two dimensions:
  - `LifeState`, for example `Alive / Downed / Dead`
  - `AvailabilityState`, for example `Available / Abandoned / StoryLocked / Recoverable`

## Architecture Direction For Unity

- `ScriptableObject` = static definitions/configuration.
- Regular C# classes = runtime/domain logic.
- `MonoBehaviour` = scene representation, input/output, Unity integration.
- Save data should likely be serialized to JSON.
- Each character visible in the game has a scene object with a `CharacterView`-style script/component.

## Section Workflow Agreed Previously

We work section by section:

1. Characters, classes, stats, and resources
2. Turn-based combat and board/grid
3. Skills, effects, passives, and status effects
4. Items, equipment, loot, and rarity
5. Weapons, armor, parts, galdurytes, and upgrading
6. Crafting
7. NPCs, trade, and storage
8. Quests and story progression
9. Dungeons, exploration, and generation
10. Save system and JSON data
11. Unity layer, scenes, UI, and managers

## Newly Confirmed Party Rules

- Active party size is `5`.
- Bench size is effectively unlimited.
- Up to `5` companion-party units may participate from the player's side in a combat.
- This cap does not count enemies, neutrals, or non-companion allies.
- Outside combat, the player controls one active character and can switch to another character.
- Outside-combat switching currently has no special utility system such as trap disarming or perception-style gating.
- In combat, the player controls only the player character and companions.
- Combat supports multiple teams, with inter-team relations such as allied, neutral, or hostile.
- If a party member dies or becomes unavailable in a dungeon, the player chooses whether to continue weakened or leave for safety.
- Bench-only companions exist only in bench/save state and are not instantiated on screen.
- Any character may revive members of teams toward which that character's relation is `member` or `neutral`.
- Combat-side team membership and world/party membership are distinct concepts.

## Newly Confirmed Class Rules

- Character classes are fixed and do not change.
- Companions also have fixed classes.
- Class-based stat growth is controlled by a dedicated incremental formula.
- Classes do not currently have unique mechanics beyond their resource systems.

## Combat Character Assumptions

- Enemies have level, skills, stats, and resource pools.
- Enemies do not have equipment.
- Enemies do have drop tables.
- All combat-capable beings should still fit one shared entity model.
- Non-playable ordinary characters usually do not have equipment or inventory.
- Mercenaries do use normal character inventory/equipment logic.
- Traders may have inventory for commerce purposes.

## Skill System Assumptions

- Every skill has an AP cost, though it may be `0`.
- Every skill has a resource cost, though it may be `0`.
- Skills contain a collection of effects.
- Damage type belongs to effect definitions such as `deal_damage`, not to the skill root directly.
- Basic attack is just a normal skill.
- Equipment passives work the same way as other passive effects, including conditional behavior.
- Skills can apply multiple effects at once.
- Future effect execution must support a shared resolved value for multiple effects of one skill. For example, a heal that targets both caster and selected ally should roll its random healing amount once and reuse that result for both effects, rather than rolling independently per target mode. The planned data-level link is an optional shared resolution/value ID on the relevant effects; this is not implemented in the current combat runtime.
- Modifiers are mainly associated with skill trees and equipment.
- Potion effects and active skill effects should generally be modeled as status effects.
- Skills should include:
  - `skillId`
  - `name`
  - `description`
  - `icon`
  - `actionCost`
  - `resourceCost`
  - `targetTypes[]`
  - `effects[]`
- `SkillData.actionCost` is expressed as a percentage of the acting unit's `MaxAP`, not as an absolute AP value.
- Skill AP cost is calculated from the current `MaxAP`; for example, a skill with `actionCost = 55` consumes `55%` of the caster's MaxAP.
- A zero-percent action cost is valid.
- Each target type includes absolute range and optionally relative range.
- Skills do not use cooldowns as a general rule.
- Limiting factors are mainly resource cost and action cost.
- Some enemy skills may be condition-gated.
- Skills may require extra conditions such as passive stacks or other runtime requirements.
- Scout knife-throwing skills require enough available knives/daggers and recovery interaction on the map.
- Some skills have delayed execution or split parts such as phase A and phase B after some turns.
- Only toggle-type skills are effectively limited to once per turn.
- Skills can also be obtained through story progression.
- Item-granted skills disappear when the item is removed.
- Item-granted skills do not become permanently unlocked just because they were once granted.

### Targeting and Range

- Skills may use absolute range relative to the caster.
- Skills may also use both absolute and relative range, where relative means relative to the target.
- Range shapes may vary, for example square, radial, or ring.
- `relativeRange` exists only when a skill selects an additional reference point beyond the caster, such as a unit, tile, or future obstacle/object.
- In the current targeting `v1`, this applies only to `AreaAroundTile` (the selected tile). `Single` resolves only the selected unit. Future target types such as `Chain` or an area around a chosen object/unit may introduce their own relative range rules once their runtime targeting exists.
- `Caster` is a `TargetType`, not a `TargetingType`. It resolves the caster automatically, has no `TargetingType`, no absolute range, and no relative range.
- `TargetingType` filters eligible units only for target types that can select or affect a relation group: `Enemies`, `Allies`, or `All`.
- For `AreaAroundCaster`, `absoluteRange` directly describes the effect area around the caster; no separate target point or relative range is involved.
- `AreaAroundCaster` and `All` do not execute immediately when their skill button is selected. Selecting the skill enters targeting mode and shows its highlights; a subsequent click anywhere on the battle grid confirms and executes the skill. Clicks outside the grid do not confirm it.
- Do not add an `includeCaster` exception to targeting. A skill that affects both the caster and allies should use separate target modes (for example index `0` for allies and index `1` for caster), with equivalent effects assigned to each target-mode index.
- Skill targeting UI should distinguish the absolute casting range from eligible targets: the general absolute range is visible after selecting a skill, while the relative area of an area-on-tile skill is previewed only while hovering a concrete selectable tile.
- Units eligible under a skill's `TargetingType` are highlighted individually; selectable tile centers must not be rendered as if they were targets.
- For `AreaAroundTile`, relative range is previewed around the currently hovered absolute-range tile, and units currently affected by that preview use a distinct highlight color.
- A relative `Square` uses side length `maxRange - minRange + 1`. An odd side length is centered on the selected reference point; an even side length is directional and extends away from the caster. For example, caster `(0,0)` and reference `(5,5)` with `0..1` produce `(5,5)` through `(6,6)`; caster `(7,7)` produces `(4,4)` through `(5,5)`.
- If an axis has no caster-to-reference direction for an even relative square, the current temporary deterministic fallback is the positive axis.
- Targeting patterns may include:
  - single enemy/ally/target
  - self
  - all enemies/allies/targets
  - area around caster
  - line of enemies/allies/targets
  - chain/bounce sequences
  - `n` enemies/allies/targets
  - area around a tile
  - other custom shapes such as straight-to-end, diagonal, X-shape, nature element, obstacle, etc.

## Section 3 - Effects / Passives / Statuses

### Effect Types

- Planned effect types include:
  - `deal_damage`
  - `apply_status`
  - `modify_stat`
  - `modify_var`
  - `heal`
  - `remove_status`
  - `charge`
  - `push`
  - `pull`
  - `fallback`
  - `teleport`
  - `shield`
  - `create_object`
  - `summon`
  - `revive`
  - `restore_resource`
  - `drain_resource`
- The most important effect types for the initial architecture are:
  - `deal_damage`
  - `heal`
  - `modify_stat`
  - `apply_status`
  - `modify_var`
- Effects may have conditions.
- Effects can target different target modes through their `targetModeIndex`.
- `SkillTargetModeData` is identified by its index in `SkillData.TargetModes`; selection resolves modes sequentially in that list order.
- Each `SkillEffectData.targetModeIndex` must point to an existing target-mode index in the skill.
- Effects execute globally in their listed order, only for the targets resolved under their own `targetModeIndex`.
- Multiple manually selected target modes are chosen sequentially in target-mode list order. `Caster` modes resolve automatically; `All` and `AreaAroundCaster` still require a grid click as confirmation.
- Effect execution order follows the order in the effect list/algorithm.
- Damage hit check applies to damage rather than to all effect types.
- Status-like and displacement-like effects use effect/status chance rules rather than weapon hit chance.
- The overall behavior is closer to an effect algorithm than a simple flat list.
- Current combat-sandbox rule for recovery effects:
  - `Heal` and `RestoreResource` temporarily use a flat `baseValue` on the effect.
  - `Heal` restores HP up to the target's maximum health.
  - `Heal` may target a `Downed` character, restoring HP, changing its life state to `Alive`, and clearing its downed-turn counter.
  - `Heal` never affects a `Dead` character.
  - `RestoreResource` restores the target's own `ResourcePool` up to its maximum value.
  - Stat-based recovery scaling remains a later design task; `baseValue` is deliberately temporary.

### Passives / Modifiers / PassiveEffects

- Passives should be unified architecturally with modifiers/status-like systems instead of split into many unrelated systems.
- Passive effects may:
  - be always active
  - be conditionally active
  - have duration
  - not have duration
- Passive effects can listen to events and activate when a condition/listener becomes true.
- Skill-tree and item passives should use the same broad passive-effect mechanism.
- Skill trees may modify existing skills or items, for example:
  - `skill_ap_cost_decrease(id: "fireball", 0.2)`
- The project direction is to merge modifiers into `PassiveEffect` style behavior rather than keep a separate modifier system.
- `Status` should be modeled as a timed passive effect, for example `TimedPassiveEffect`.
- Preferred hierarchy direction:
  - `PassiveEffect`
  - `TimedPassiveEffect`
  - `ConditionalPassiveEffect`
  - `TriggeredPassiveEffect`
- Example status IDs:
  - `stun`
  - `bleed`
- Timed passive effects/statuses may carry extra payload kwargs such as `damage: 34`.
- Timed passive effects do not always need stacks.
- Timed passive effects do not always need payload data.
- `ModifyStat` used by an active skill uses `durationTurns` as follows:
  - `durationTurns > 0` creates a timed runtime passive effect.
  - `durationTurns = 0` creates a combat-scoped non-timed passive effect; it remains active until that combat ends.
- A reapplication of the same runtime `ModifyStat` effect refreshes its duration/scope and replaces its modifier values; values never accumulate from repeated application.
- Combat-scoped runtime passive effects need an explicit end-of-combat marker so that future persistent passives from equipment or skill trees are not removed with them.
- Runtime `PassiveEffect` lifetime is explicit:
  - `Timed` -- status or timed effect; it expires after its remaining duration.
  - `CombatScoped` -- active-skill effect with no duration; it expires when the combat ends.
  - `Persistent` -- future equipment or skill-tree passive; it survives combat.
- Runtime effects retain structured sources instead of only character IDs. A source records its type, source ID and originating character ID.
- Reapplying an effect merges distinct sources. Effects created by a skill use a source ID in the form `<skillId>.effect.<effectIndex>`.

### Statuses

- Confirmed/planned statuses include:
  - bleeding
  - poison
  - burning
  - stun
  - freeze
  - chill
  - sleep
  - invisibility
  - taunt
  - immobilize
- Statuses may be positive, negative, or neutral.
- UI groups active components from the same skill and caster or the same consumable instance into one status entry with a shared tooltip (2026-10-10). Runtime effects, payloads, stacks and expiration remain independent. Different sources stay separate even if their display names match. Mixed durations are listed per component; the icon does not imply one shared duration. Persistent passives remain outside the combat-status list.
- This category is mostly relevant for UI rather than deep engine separation.
- A status has:
  - `statusId`
  - source
  - duration
  - optionally stacks
  - optionally additional payload values such as bleeding damage
- Stacking/refresh behavior depends on status type.
- Example:
  - bleeding adds duration and adds damage into the total bleeding pool
  - stronger hard-control statuses such as stun refresh to the higher remaining duration rather than stacking freely
- Statuses should be merged into the broader passive-effect architecture rather than modeled as a fully separate conceptual system.
- Technical status/effect categories such as `DoT`, `HardCC`, `SoftCC`, `Buff`, `Debuff`, and `Utility` are acceptable and can be introduced.

### Damage / Hit / Crit Ordering

- Damage resolution order is:
  - hit
  - crit
  - damage type
  - reduction
- Some attacks/skills may hit multiple times.
- Not every offensive effect uses normal hit checking.
- Status application does not use ordinary hit checking; it uses effect/status chance.
- Effects may depend conditionally on prior effects, for example:
  - deal damage
  - if hit, apply bleed
  - if crit, push target

### Runtime / Save Direction For Effects

- Skill runtime state can stay attached to the character script/state rather than needing full per-skill object instances in every case.
- Statuses can stay in a list on the character.
- Passives can stay attached on the character in a similar way.
- Statuses and passive effects should preferably live on `CharacterInstance`.
- Cooldowns are not needed because the system does not use them.
- Passive effects should be part of save data when persistent/relevant.

### Unity Data Direction For Effects

- `PassiveEffect` is the preferred shared concept for passives/modifiers and partially for statuses.
- The design direction is more algorithmic/composable than rigidly field-based.
- Skills should support true branching logic, not only simple linear effect lists.
- Skill branching can be treated as conditions plus effect nodes; this is conceptually equivalent to conditional chained effects.
- A simple `condition + effect nodes` branching structure is preferred unless future complexity demands a more formal graph.
- The current sandbox `v1` may use conditions referring to the immediately previous effect for quick validation.
- Target architecture: a conditional effect should reference a specific earlier effect by stable `effectId` or by effect index, rather than only "the previous effect".
- Contextual hit dependencies (2026-10-14): contact payloads of weapons, bites and projectiles reference the actual damage effect against the same target. A shield-absorbed hit still counts; a miss does not. Do not gate one payload on a different payload's status/resistance success, and do not reroll accuracy for an already hit-dependent payload.
- Area/environment effects keep their own chance/resistance rolls independently of damage accuracy: Slayer Tremors, Ent Ignition, Plague Growth extension and Dead King's wave chill. Standalone controls, buffs, healing, movement, summons and objects are reviewed by context, never globally gated just because a skill has damage.
- Kick and Ancient Whip have no damage component: their negative effects share the existing per-target accuracy roll, then resolve their own application/movement resistance. A wall preventing displacement does not invalidate the contact. Modifier chances are rolled once for ModifyStat/ModifyVar/ModifyResistance. Reviewed authoring rules live in `ContextualSkillHitSetup`; active content factories preserve them.
- `effectId` is the preferred durable reference because inserting or reordering effects does not silently change the dependency; an index may remain as a convenience for simple authored sequences.
- A skill may expose multiple target types, and different effects may bind to different target selectors, for example:
  - `all_allies` for `effect[0]`
  - `caster` for `effect[1]`
- `create_object` should create map objects such as traps, vines, or totems.
- `summon` should create a unit.
- `CreateObject` / `Summon` v1 rules:
  - Initial examples are a shaman totem and a necromancer skeleton.
  - A created object may be placed on a unit's tile. In that case it must not block movement or line of sight.
  - Object placement uses only an absolute range around the caster; it does not use a separate destination target mode.
  - Objects have a duration, can be destroyed, and may disappear when their creator dies.
  - Object HP is normally expressed as a number of received `DealDamage` hits, but one skill may later apply multiple hits.
  - Summons are full `BattleUnit` instances created from an existing `CharacterDefinition`.
  - A summon inherits its caster's side and team relations, enters the turn order with full AV, and acts like a normal unit.
  - A summon lasts for a skill-defined duration counted at the end of the summon's turns, and disappears when its caster reaches `Dead`.
  - A skill-defined summon limit is supported. Reaching it removes the oldest summon of that caster before adding the new one.
  - A summon never grants XP, gold, loot, or persistent encounter progress; expiry removes it and all attached runtime state.
  - The initial implementation is limited to object and summon creation; totem behavior such as healing, buffing, or dealing damage belongs to later cards.
  - Each received `DealDamage` hit decreases an object's hit-based HP by that effect's hit count, rather than by raw damage.
  - Created objects have a skill-defined active-object limit. Reaching it removes the oldest object created by that caster before adding the new one.
  - `percentMaxHealth` is interpreted against the summoned character definition's maximum health. Other summon stats remain at their normal definition-derived values.
- Shield is an absorb-before-health resource.
- Shield should be stored as a list of shield layers/sources.
- UI may display the sum of shield values.
- When damage is taken, shield layers are consumed one by one from the end of the list until incoming damage is exhausted.
- Shield is often granted through passive effects.
- Some passive effects allow shield stacking.
- Some shield effects are temporary flat shields.
- Some shield effects are permanent or semi-permanent and based on percentage HP rules.
- `modify_stat` affects stats such as HP/speed/defense.
- `modify_var` affects non-stat gameplay variables such as damage dealt/received percentages and similar values.
- Toggle skills apply passive effects while active.
- Toggle skills may or may not have recurring upkeep costs.
- Scout thrown knives/daggers use a separate mechanic specific to that class and only for the player.
- Delayed phase-B skill behavior should be modeled as a scheduled event/effect.
- Passive effects may also operate outside combat.

## Status and Effect Assumptions

- Statuses should support duration in turns.
- Statuses should support stacks.
- Statuses should support source tracking.
- Statuses should support type/category.
- Some statuses such as bleeding or regeneration also need numeric payload values like damage or regeneration amount.
- Status application chance is modified by resistance.
- Resistances also modify damage: each 1% resistance below 50% reduces damage by 1%, and each 1% above 50% increases damage by 2%.
- The exact status application formula has been specified by the project notes and should be preserved.
- The Rage overcap weakening effect should be modeled as a weakening status effect, not a generic timed modifier.

### Confirmed Bleed Rules

- Bleed is a damage effect with its own `DamageType.Bleed`.
- Bleed damage is not reduced by physical defense, magical defense, or any other defense.
- Bleed damage does not pass through shields.
- Bleed cannot critically hit.
- Bleed is applied using the status/effect chance formula:
  - `P = max(0, 1 - 2r) * (1 - s) / (3 - s) + min(1, 2 - 2r) * s`
  - `s` is the base effect chance.
  - `r` is the target's bleed resistance normalized to `0..1`; the displayed/gameplay resistance range is `0..100%`.
- Bleed resistance changes damage with a linear multiplier:
  - `0%` resistance means `2.0x` damage.
  - `50%` resistance means `1.0x` damage.
  - `100%` resistance means `0.5x` damage.
  - The values between these points are interpolated linearly.
- Bleed is processed at the beginning of the affected unit's turn, after regeneration and before the unit can act.
- A duration of `N` means `N` future turn-start ticks.
- Applying Bleed does not cause an immediate tick.
- Reapplying Bleed replaces the remaining duration with the new effect duration and adds the new total damage to the existing total damage.
- The combined total damage is distributed over the new remaining duration.
  - Example: `34 damage/tick for 2 turns` plus `26 damage/tick for 5 turns` becomes `198 total damage over 5 turns`, approximately `40 damage/tick`.
- There is no maximum duration or maximum total Bleed damage.
- When Bleed reduces a character to `0 HP`, the character is handled by the normal downed flow and does not receive further damage while downed.
- Bleed may operate outside combat, but the time/event mechanism responsible for outside-combat ticks is separate from the combat-turn tick.
- The runtime representation should store total Bleed damage rather than treating the generic `payloadValue` as an unexplained Bleed-specific field.
- The approved status-chance balance curve uses `baseChance = 0` as a guaranteed failure.
- For resistances up to `50%`, the status chance is calculated as `s + (1 - 2r) * (1 - s) * B(s)`.
- For resistances above `50%`, the status chance is calculated as `(2 - 2r) * s`.
- `B(s)` is linearly interpolated through these approved points:
  - `(0, 0)`
  - `(0.01, 0.0404)`
  - `(0.03, 0.0722)`
  - `(0.25, 0.3333)`
  - `(0.50, 0.40)`
  - `(1.00, 0)`
  - The approved Bleed damage multiplier is piecewise linear:
    - for `r <= 0.5`: `2 - 2r`
    - for `r > 0.5`: `1.5 - r`

### Confirmed Crowd-Control Status Rules

- `Stun` is a hard CC: the affected unit loses its entire turn and cannot move,
  use skills or items, interact, revive, or attempt escape.
- `Freeze` is a hard CC. The first damaging hit received by the frozen unit ends
  Freeze and increases that hit's damage by `50%` for every remaining Freeze turn.
  Only a direct `DealDamage` effect can trigger this. The bonus applies to every
  damage type and can combine with a critical hit.
- `Sleep` is a hard CC and regenerates a percentage of MaxHP on every affected
  turn; this percentage depends on the applying skill. The sleeping unit can
  voluntarily end Sleep by choosing movement instead of ending its turn; that
  movement is then performed normally. Sleep regeneration occurs at the end of
  the affected unit's turn, not at turn start, and rounds healing up. Reapplying
  Sleep replaces duration and updates the regeneration percentage to the latest
  applying skill's value.
- `Paralysis` is a hard CC that turns the target effectively to stone: it loses
  its turn and cannot be targeted by any ability, including area effects. It is
  completely immune while the status lasts. Paralysis also pauses every other
  status effect, including DoTs: they neither tick nor lose duration until
  Paralysis ends. This pause applies only to timed status effects, never to
  persistent or combat-scoped passive effects from equipment or skill trees.
- `Immobilize` blocks self-initiated movement: normal movement, Charge, Dash,
  Teleport, and Fallback. It does not prevent Push or Pull because those are
  initiated by another unit.
- `Chill` (`Zmrożenie`) is a separate status that limits Speed to a maximum of
  `20`; it must not increase a naturally lower Speed. The value is represented
  in code by the named `StatusEffectSystem.ChillMaximumSpeed` constant. It uses Suppression
  Resistance and reapplication uses `MaxDurationAndValues`.
- Stun, Freeze, Sleep, and Paralysis are hard CC, use the suppression resistance
  group, and reapply with `ReplaceDuration`.
- `Regeneration` ticks at the end of the affected unit's turn. Its value can be either flat HP or a percentage of MaxHP, depending on the applying skill. Its reapplication follows the DoT rule: replace remaining duration and add total healing, then distribute the combined total over the new duration.
- `Taunt` restricts offensive skills so that they may target only the character that most recently applied Taunt. Reapplying Taunt uses `ReplaceDuration` and the latest source replaces the previous taunter. Defensive and utility skills remain legal. AI under Taunt should prefer legal offensive skills against the current taunter over defensive or utility skills.
- `Invisibility` uses `ReplaceDuration`. It ends immediately after its owner successfully executes an offensive skill against an enemy, regardless of whether the damage effect hits.
- `Invisibility` removes the unit from `All` targeting and from direct target selection. It can still be hit by tile/pattern-based effects such as line, X-shape, area around tile, selected tile, and similar shapes; opponents must guess its position for these effects. Its expiry/break rules remain to be specified.

## Save Rules

- Character progression in save data should be stored by `characterId`.
- Save data may also store trader inventory and money.
- Save data should store dungeon information, party position, and ally states.
- Bench characters keep their current HP from when they were left.
- Bench characters continue to regenerate or consume resources normally according to game rules.
- Active status effects, including duration and stacks, are also part of save data.
- Bench simulation may use a simplified recalculation rather than full continuous simulation.
- Save data should also include:
  - current active leader outside combat
  - active party composition
  - recruited/bench companions
  - NPC loyalties
  - honor per character
  - gold
  - stash
  - unlocked services/NPC availability as implied by loyalty/progression
  - world state / ending flags
- Character save data should include everything needed to reconstruct that character fully.
- Saving during combat is not needed because the player cannot save during combat.
- Unidentified galdurytes should save their already-rolled hidden effects and only hide them from the player until identification.
- Every `ItemInstance` should be saved as its own entry with its own `instanceId`.
- Stack grouping should exist only at the UI/inventory presentation layer, not as the core save representation.
- Socketed galdurytes should be stored inside the owning item data as embedded references/entries.
- Dungeon persistence should use `seed + deltas` rather than a full raw dungeon dump.
- Quest save data and world save data should remain separate concepts.

## Inventory and Equipment Assumptions

- Items should be modeled as `ItemDefinition : ScriptableObject`.
- Equipped items should be modeled as item instances, not just references to item definitions.
- `ItemInstance` should be a normal runtime/save class.
- Every item should have its own `instanceId`, even if UI groups them into aggregate counts.
- Item instances may differ by upgrades, enchantments, parts, and rarity.
- `ItemDefinition` should contain static fields such as:
  - id
  - name
  - description
  - icon
  - type
  - rarity
  - base value
  - stackability
  - weight
- Some item-definition behavior can be implied by item type, for example armor is non-stackable by default.
- `ItemInstance` fields depend on item type.
- Example mutable armor fields may include:
  - 3 parts
  - defense
  - HP
  - dodge
  - quality
  - upgrade level
  - list of galdurytes
  - galduryte capacity
  - cost
  - required level
  - required class
  - uniqueness
- Traders have:
  - a rotating inventory that changes after completing a dungeon
  - an inventory of items bought from the player
- Inventory is weight-limited rather than slot-limited.
- Character carry capacity should be deliberately limited: it creates the expedition decision of what can be carried out of a dungeon and what must be left behind or collected after returning.
- Carry capacity may be increased by build choices (for example unique/passive effects, jewelry, or temporary potions), not primarily through a permanent gold-purchase progression.
- Most items are weightless except weapons, armor, and galdurytes.
- The city hub has an infinite-capacity storage stash/chest.
- Stackable items occupy one inventory entry.
- Default item-grid order is item type, current rarity descending, Polish display name ascending, then the tooltip value of one item descending. Stack quantity and total stack value do not affect ordering. Equal visible keys use stable item/variant metadata; only non-stackable items use instance ID as the final tie-breaker. This is presentation ordering and never reorders the underlying inventory/save list.
- If stacked items have metadata, the UI should allow opening the item icon and seeing the list of underlying items of that type.
- Stack size has no hard limit.
- Materials, keys, and valuables are stackable.
- Crafting uses a shared inventory pool.
- The stash can store everything.
- Runtime equipment should store direct references to `ItemInstance`.

## Item and Rarity Assumptions

- Confirmed item types include:
  - weapons
  - armor
  - jewelry
  - materials
    - crafting
    - alchemical
    - rune-making
  - potions
  - galdurytes for weapons and armor
  - lootbags
  - keys
  - quest items
  - sell-only valuables
- Item categorization should use a rigid `ItemType`.
- One item belongs to exactly one category/type.
- Lootbags can drop materials or weapons depending on type.
- Confirmed rarity tiers:
  - trash
  - broken
  - damaged
  - common
  - uncommon
  - rare
  - ancient
  - legendary
  - mythic
  - godly
- `trash`, `broken`, and `damaged` are restricted to certain equipment categories such as starting weapons/armor or degraded gear.
- Armor is not split into multiple worn body slots in the current design.
- Each character has exactly:
  - 1 weapon slot
  - 1 armor slot
  - Jewelry slots are:
    - necklace
    - ring
    - bracelet
    - earrings
  - All classes use the same slot model.
  - Equipment may require class and level.
  - If requirements are not met, the item cannot be equipped.
  - For weapons and armor, rarity regulates some stat ranges/quality.
  - For galdurytes, rarity affects the number and quality of effects.
  - Rarity is usually fixed by definition, except for random-drop categories such as weapons, armor, and galdurytes.
- Equipment may require class and level.
- If requirements are not met, the item cannot be equipped.
- For weapons and armor, rarity regulates some stat ranges/quality.
- For galdurytes, rarity affects the number and quality of effects.
- Rarity is usually fixed by definition, except for random-drop categories such as weapons, armor, and galdurytes.
- Equipment quality includes:
  - normal (`+0` item level adjustment, no explicit quality label)
  - poor (`-3`)
  - excellent (`+3`)
  - masterpiece (`+5`)
- `Masterpiece` quality denotes a unique item; unique status does not need a separate boolean flag in the model.
- Quality is rolled randomly for dropped/generated equipment.
- Crafted equipment can choose quality.
- Quality cannot be changed later.

## Equipment Generation Assumptions

- Weapons and armor are generated from parts.
- Each level bracket (`5, 15, 25, 35, 45`) has four variants of the same part with different stat profiles.
- Parts define one or two core values and modify the rest by approximately `+/-20%`.
- Weapons use 3 parts.
- Armor also uses 3 parts.
- Equipment crafting lets the player choose desired parts.
- Different parts consume different materials, for example iron or copper.
- Parts are predefined, and their IDs depend on material.
- Weapon parts are:
  - `head`
  - `binder`
  - `handle`
- For standard weapons:
  - `head` defines attack and crit damage and modifies crit chance/accuracy by `+/-20%`
  - `binder` defines crit chance and modifies the other three by `+/-20%`
  - `handle` defines accuracy and modifies the other three by `+/-20%`
- Mage weapons are an exception:
  - instead of accuracy they use bonus max mana
  - instead of crit chance they use mana regeneration
- Armor parts are:
  - `plate`
  - `binder`
  - `base`
- For armor:
  - `plate` defines defenses and modifies max HP/dodge by `+/-20%`
  - `binder` defines max HP and modifies the other three by `+/-20%`
  - `base` defines dodge and modifies the other three by `+/-20%`
- Final item stats are the combined result of all parts.
- Item level is determined by the highest part level bracket (`5, 15, 25, 35, 45`) adjusted by quality:
  - `-3` for poor quality
  - `+3` for excellent quality
  - `+5` for masterpiece quality
- Quality is separate from rarity for equippable items.
- Quality sets item level and gives flat per-level modifiers to all stats.

## Weapon and Armor Class Assumptions

- Weapon types depend on class:
  - warrior: longsword
  - scout: sword-and-dagger weapon setup represented as a single weapon item/slot
  - mage: wand
  - paladin: hammer
- Weapon type affects which class can equip it and which parts are available.
- Weapon type does not directly affect skills or resource interactions.
- Armor naming/themes also depend on class:
  - warrior: chainmail
  - scout: tunic
  - mage: robe
  - paladin: armor
- These are class-themed item families rather than a separate armor-type system.

## Galduryte Assumptions

- Galdurytes are a rune-like socketable system for weapons and armor.
- Galdurytes are crafted from runic stone and crystal powder.
- Their effects and strengths are semi-random.
- Each powder type has 1-4 possible effects.
- Weapon galdurytes and armor galdurytes have different effect pools.
- Weapons and armor can host galdurytes.
- Socket count ranges from 1 to 5 depending on upgrade level.
- Upgrade progression is `0-100%`, granting one slot every `20%`.
- Galdurytes are inserted into sockets and grant passives while the item is worn.
- Once inserted, a galduryte is not recoverable.
- Replacing a galduryte is possible only by removing/destroying the previous one.
- Socket availability depends only on upgrade progress.
- Jewelry has exactly 1 galduryte socket.
- The same galduryte can be used in weapon, armor, or jewelry.
- Galdurytes have level thresholds `11`, `21`, and `41`.
- An item can host a galduryte only if:
  - item level is higher than galduryte level
  - item rarity is not lower than galduryte rarity
- Galdurytes may have effects such as:
  - bonus damage
  - crit chance / crit damage
  - hit chance
  - status/effect chance
  - DOT on hit
  - heal on hit
  - resource gain on hit
  - movement speed on hit
  - bonus drop chance
  - bonus resource gain
  - bonus damage versus enemy type
  - stun/freeze/slow on hit
  - bonus XP gain
  - bonus gold gain
  - defense penetration
  - max HP / dodge / defense / speed / AP bonuses
  - resistance bonuses
  - reduced incoming damage
  - max resource bonuses
  - reduced resource consumption
  - passive HP/resource regeneration
- Galduryte effect-tier distribution depends on rarity:
  - common: `50% D`, `50% C`
  - uncommon: `25% D`, `50% C`, `25% B`
  - rare: `50% C`, `25% B`, `25% A`
  - ancient: `25% C`, `50% B`, `25% A`
  - legendary: `50% B`, `25% A`, `25% S`
  - mythic: `25% B`, `50% A`, `25% S`
  - godlike: `50% A`, `50% S`
- Number of galduryte effects depends on level:
  - `11-20`: 2-3 effects
  - `21-40`: 3-4 effects
  - `41-50`: 5-6 effects
- Crafting can guarantee one effect group depending on the used powder color.

## Potion Assumptions

- Potions use 3 herbal ingredients plus a catalyst.
- The catalyst is created from alcohol and combinations of herbs.
- Potions are permanent/reusable and refilled with alcohol.
- Potions and bombs use charges instead of disappearing on use.
- Charges are restored with alcohol or gunpowder-like refill resources depending on item type.
- Other consumables may still exist, and true consumables disappear on use.
- In combat, carried consumables are shared across player participants, including downed carriers. Using another participant's item does not transfer it: the caster pays AP and owns the per-type turn limit; the carrier's item loses its charge. Items in storage, enemy inventories and characters outside the combat are excluded. Exploration use rules remain unchanged (2026-10-11).

## Jewelry Assumptions

- Jewelry is crafted from metal and crystals such as diamonds.
- Jewelry primarily modifies stats.
- Jewelry has rarity based on used materials.
- Jewelry may be randomly generated from different metal and decorative/crystal component combinations.
- Jewelry may grant passive effects, especially if unique.
- Jewelry is not built from the same part system as weapons/armor.
- Jewelry stats come from material type plus crystal type.

## Unique Equipment Assumptions

- Unique weapons, armor, and jewelry can drop from bosses or be granted as quest rewards.
- Every unique item has `Masterpiece` quality.
- Unique equipment may grant passive effects or active skills.
- Unique equipment is still generated from random parts, so the "same" unique can differ between runs/drops.
- Unique passives are independent from the generated parts.
- Uniques can still be upgraded, reforged, and infused.
- Uniques can also host galdurytes.
- Unique item identity and name are fixed in advance.
- Parts only change stats, not unique identity.
- In the data model, uniqueness should be derived from `quality = Masterpiece` plus unique identity data, rather than a separate `isUnique` field.

## Crafting Assumptions

- Crafting is performed by NPC specialists.
- Craftable item categories depend on NPC type:
  - blacksmith: weapons, armor, crafting goods
  - alchemist: potions, alchemical goods
  - rune-maker: rune goods, galdurytes, jewelry
- Crafting allows choosing exact parts for weapons and armor.
- Crafted gear can be as good as dungeon loot, except that uniques remain special.
- Materials influence resulting stats.

## Section 6 - Crafting Assumptions

### General Crafting Model

- Crafting uses a mix of rigid recipes and rule-based assembly depending on item type.
- The player does not need to learn recipes.
- All recipes are available from the start.
- Crafting always succeeds.

### Blacksmith

- Weapons and armor are crafted from the ingredients required for all 3 selected parts.
- The player can choose:
  - parts
  - quality
  - name
- The blacksmith can:
  - craft
  - upgrade
  - reforge
  - infuse
- Crafting goods / crafting materials also include semi-finished artisan components such as metal chains and other items in the "crafting" category.

### Alchemist

- Potions are crafted from:
  - alcohol
  - 3 materials
  - optionally a catalyst (powder from herbs)
- Each ingredient is responsible for 1 effect.
- Ingredient order does not matter.
- A catalyst is optional.
- Bombs use the same system but consume gunpowder instead of alcohol.
- The alchemist can also create alchemical ingredients, which are effectively catalysts.

### Rune-Maker

- Galdurytes are crafted from:
  - a galduryte/runic stone
  - powder
- Jewelry is crafted from:
  - metal ingots
  - a gem/crystal
- The rune-maker can:
  - identify galdurytes
  - create galdurytes
  - insert galdurytes
  - extract/remove galdurytes
- Galdurytes dropped by enemies or rewarded from quests may have unknown effects and must be identified for money.

### Materials and Consumption

- All required crafting ingredients are consumed.
- A sewing needle for threads is an example of a tool that is not consumed.
- Charge refilling is a separate system from crafting.
- Crafting consumes only shared crafting materials and does not need to inspect combat/equipped inventories.

### Crafting UI / Workflow

- Crafting UI should show:
  - how many of each ingredient the player has
  - resulting stats
  - quality
  - possible effects for galdurytes
  - resulting effects for potions
- The player manually chooses everything.
- There should also be a randomize option, for example:
  - random materials for weapon crafting after selecting level and quality
  - random potion materials after selecting desired effects or even fully random
- Crafting is available only through NPCs.

### Crafting Progression / Gating

- Crafting is not heavily restricted by design.
- For simplicity, higher item tiers are hidden until someone in the party reaches the required level milestone:
  - before level 10: no tier 2+
  - before level 20: no tier 3+
  - and so on
- Additional quest-based unlocks may be added later, but are not core right now.
- NPC crafting services are unlocked based on loyalty level rather than all being available from the start.
- Each NPC has its own loyalty level.
- Loyalty scales with the amount of gold spent at that NPC.
- Loyalty unlocks:
  - new services
  - discounts
  - special changes/improvements, for example in upgrading

### Open Crafting Shape Decisions

- Crafting can create `ItemInstance` directly rather than going through a separate result object.
- Crafted items cannot be unique.
- Uniques come only from quest rewards or lootbags.
- Lootbags from bosses drop uniques more often than ordinary lootbags.
- Potions and bombs are simply items whose effect list depends on ingredients.
- Bomb effects may include damage, debuffs, or DOT application.

## Upgrade / Reforge / Infusion Assumptions

- Upgrading is performed by the blacksmith.
- The player chooses upgrade progress from `0%` to `100%`.
- Upgrade progress remains `0-100%`. The equipment primary-stat multiplier is:
  - weapons: `1 + 0.5 × progress` (up to +50% equipment ATK)
  - armor: `1 + 1.5 × progress` (up to +150% equipment physical and magical defense)
- Reforging scales the equipment critical-damage bonus by `1 + 0.5 × (rarityMultiplier - 1)`. The character's base critical multiplier is unchanged. Other rarity scalings remain unchanged: HP/critical chance use the full rarity multiplier, accuracy/dodge use 30% of its bonus, resources/regeneration use 60%.
- Existing equipment is recalculated from its stored baseline. Legacy payloads without a baseline recover primary stats using their original +100% upgrade rule before applying the current coefficients.
- Upgrade also increases:
  - socket count (`+1` every `20%`)
  - item price/value
- Upgrade cost is derived from:
  - selected upgrade chance
  - selected upgrade amount
  - base item price
- Upgrade success chance can be chosen with a slider from `0` to `99`.
- On failure:
  - money is lost
  - upgrade progress drops by a random `10-20%`
- Reforging changes rarity in exchange for money and a piece of material.
- Reforging does not create unique items.
- Infusion raises equipment by effectively moving its parts one tier higher.
- Example: part ID `0` becomes part ID `16` because there are 4 parts per tier and 4 tiers of offset between brackets.
- Infusion recalculates stats because stats come directly from parts.
- Infusion has no hard limit, but requires an appropriate soul tier gathered from monsters of matching level.
- The purpose of infusion is to let a favored unique stay relevant through the whole game.

## Galduryte Data Direction

- Galdurytes may be modeled directly as random instances rather than strongly defined fixed templates, because each galduryt is inherently random.

## Durability Assumptions

- Equipment and galdurytes do not use durability.

## Crafting UX Assumptions

- When crafting a weapon or armor piece, the player can preview the resulting stats and item level before confirming.

## Team Relation Assumptions

- Team hostility/neutrality/alliance should behave symmetrically.
- Relations are not only between teams as abstract objects.
- Each character may hold its own relation toward each team.
- Team membership is effectively derived from having `member` relation to one or more teams.

## Section 2 - Combat and Grid Assumptions

### Combat Start

- Combat starts when entering a combat-trigger range.
- Combat trigger shape depends on the room/space, for example a wide rectangle in a corridor or a square in a room.
- Combat happens on the same map, which is divided into tiles.
- In Combat v2, a combat grid must be derived from the current dungeon-map area rather than from an independent fixed arena in `EncounterDefinition`.
- The combat area inherits the real local geometry: room dimensions, walls, pillars/high obstacles, walkable cells, doors, and other relevant combat map objects.
- A fight may span multiple adjacent smaller rooms when their doors or open passages make them part of the active combat space. Closed doors remain blocking map objects until opened.
- Example: an encounter started in a `4x12` room with a central pillar uses that same `4x12` tactical area and retains the pillar as an obstacle.
- Hidden encounters and dialogue-started fights are possible.
- Starting positions depend on the point of contact.

### Combat Participants

- Only active party members enter combat from the player side.
- Bench characters are effectively absent from the field and remain in city/home/inn context.
- More than two sides can participate in one combat.
- A character's relation to teams can change during combat.
- Team alignment changes happen through changing attitude/relation toward a team.

### Grid and Obstacles

- Combat uses a square grid.
- Each tile may contain only one unit.
- Special map objects exist, for example:
  - traps
  - chests
  - doors
  - levers
  - altars
  - destructible objects such as barrels
- These are separate objects standing on tiles.
- Such objects may have types such as obstacle, trap, chest, door, etc.
- Obstacles can block skill range / line of effect depending on obstacle type.
- For example, a tall tree or wall may block range, but a rock may not.
- Height is ignored for simplicity; combat is effectively flat.

### Movement

- Moving costs AP.
- A character may move multiple tiles at once.
- Each additional moved tile costs 1 more AP than the previous one.
- Normal movement cannot be performed diagonally. Reaching a diagonally adjacent tile requires one horizontal and one vertical step.
- Example: moving 5 tiles costs 15 AP.
- Terrain may increase movement cost progression, for example `2 -> 3 -> 4 -> 5`.
- Terrain may also decrease movement cost progression, for example `0 -> 1 -> 2 -> 3`.
- Units cannot move through allies.
- Units cannot move through enemies.
- Units cannot end movement on an occupied tile.
- Position-changing effects such as teleport/dash/push/pull/charge are not treated as normal movement and do not consume AP by default.
- If movement is interrupted by the player, the character finishes moving to the next tile and then stops.
- Teleport can never place a unit on an occupied tile.
- Pull can never place a unit on an occupied tile.
- Dash and charge are treated as the same kind of displacement behavior.
- Dash/charge stop on walls and obstacles and cannot pass through them.
- Push and charge may slam a target into an obstacle.
- In that case the target position does not change, but the target receives stun.

### Turn Order and AP

- The previously defined AV system remains in use.
- `AP = speed * const`
- The exact value of `const` is still undecided.
- Unused AP can be carried over only up to `120%` of max AP.
- Manual end turn is available from the start of a turn.
- Some skills can enable out-of-turn reactions.
- Reactions normally cost no AP.
- Reaction-style buffs may instead consume their own stacks, for example "counterattack the next 2 people".
- A unit may react multiple times if the skill/effect allows it.
- Start-of-turn effect order is:
  - regeneration
  - then damage
- Downed characters do not process these start-of-turn effects because they are at `0 HP`.

### Combat Actions

- Core combat actions:
  - movement
  - skill
  - item
  - revive
  - escape
  - interact
  - end turn
- Revive is an action triggered by clicking a downed person.
- Manual revive has a maximum range of `1`, including diagonals (the same tile or any of its eight neighbours). Different levels/stair positions must have an actual adjacent traversal connection. During exploration, the helper approaches before reviving when the target is farther away.
- Interact is used on an interactable object.
- Defend does not exist as a separate action.
- Item use costs `20%` of MaxAP.
- Interact costs `20%` of MaxAP.
- Loot becomes available after an encounter is resolved. The player may collect it
  from defeated enemies during exploration, without an AP cost. Looting bodies during
  combat is outside the current scope; if introduced later, each individual item claim
  costs `20%` of the collector's MaxAP.
- The current post-combat `Odbierz loot` / pending-loot flow is temporary. The
  intended gameplay model is physical item collection from defeated enemies'
  bodies during exploration after the encounter has ended.
- Revive costs no AP but automatically ends the turn.
- Seamless dungeon escape is an explicit action for the active player-controlled character, costs `0 AP`, and performs no random roll. Other participants remain in combat.
- Losing enemy contact enables escape; it never automatically removes a player from combat. Every living enemy must finish the configured number of full turns without seeing the character. Reacquired visibility resets progress and blocks escape immediately.
- `Ucieknij` / `X` is a fixed icon button next to consumables `Q` and end turn `E`. A short live tooltip shows the current blocker or `0 AP` when ready. Click/shortcut revalidate eligibility, action presentation, crowd control and membership state.
- Legacy standalone sandbox escape retains its separate `60%` preparation chance, `+10%` on failure, and turn-ending attempt. This probabilistic model does not apply to seamless dungeon combat.
- Any character may use items from characters belonging to the same team, not only the player.

### Targeting and Friendly Fire

- Whether line of sight matters depends on the skill/effect definition and obstacle rules.
- Obstacles can block targeting or range depending on their properties.
- Projectile attacks can be blocked by an ally standing on the line.
- Whether friendly fire exists depends on the skill.
- Some skills affect everyone in range.
- `TargetingType.All` includes enemies, allies, and neutral units. Neutral units are valid targets for all skills; the selected skill's targeting/type rules determine whether they are included.
- Some skills affect only enemies, only allies, or also the caster.
- Some target types allow targeting empty tiles, for example area, semi-area, or custom-shape targeting.
- Melee/ranged tags make sense for skills, but are not directly tied to numeric range.
- Melee skills can still have range greater than 1, even 4 or more.
- Ranged skills behave worse if an enemy stands adjacent to the caster, for example lower damage, weaker effect, or lower hit chance.
- This adjacent-enemy penalty should be an optional per-skill parameter rather than a mandatory global ranged rule.

### End of Combat

- Combat ends when there are no longer two units hostile toward the player.
- The rest of unresolved combat is simulated after that point.
- If remaining teams are hostile to each other but no longer hostile to the player, the player's combat ends and the rest is simulated.
- Neutral units may remain on the field.
- If the main character goes down/dies in the condition that should end the run, combat ends immediately.
- Downed enemies do not count as resolved for combat-end purposes.
- Combat should end only when enemy-side relevant units are dead or escaped rather than merely downed.
- If the player escapes, the rest of the combat is also simulated.
- Experience rewards are granted to all active-party characters that are alive or downed when combat ends; dead characters receive no experience. On escape, the party keeps experience already earned, but uncollected gold and loot from the encounter are lost.

### AI

- AI personalities and combat roles are planned.
- Roles such as DPS may influence action selection frequency.
- AI allies use the same broad system as enemies.
- AI only knows what it can see.
- Trees/walls and similar blockers may create fog-of-war limitations.
- AI chooses actions with a scoring system.
- The best currently scored action is preferred.
- Personality and role modify the scoring weights.
- Personality and role are separate concepts/configurations.

### Unity Combat Architecture Direction

- A combat/session manager such as `CombatSession` or `BattleManager` is appropriate.
- A battle wrapper around runtime character data is appropriate because combat adds fields such as AP and AV.
- A grid model such as `BattleGrid` is appropriate.
- `BattleUnit` should act as a wrapper around `CharacterInstance`.
- `BattleUnit` should contain a reference to the underlying character plus combat-only data such as AP, AV, position, team relations, and temporary combat flags.
- `BattleUnit` may track whether the unit already reacted during the current relevant window.
- Counterattack readiness is better modeled as a modifier or status/buff effect.
- Seamless escape readiness comes from current enemy contact state, not the legacy `IsReadyToEscape` preparation flag. The active character leaves only after explicit `Ucieknij` / `X`; the remaining units keep fighting, with existing outcome/reward rules still applied when combat resolves.
- The separate standalone sandbox still uses preparation flags and its legacy abandonment outcome; it is not the production dungeon escape model.

## Destructible Dungeon Obstacles v1 — approved

- Only authored `Obstacle` objects marked destructible; existing objects default to indestructible. Walls and doors are outside v1.
- Durability is a number of `DealDamage` hits, not raw damage. Each applicable damage effect removes its `DamageHitCount`, including area effects and enemy attacks, respecting skill range/occlusion.
- Exploration: right-click a visible obstacle and choose Destroy from the UIW context menu. Left-click remains movement. A red health-style bar shows remaining/max hits when hovered, during the menu or destruction order, only while currently visible. The interaction approaches the obstacle and repeatedly strikes for one durability point; movement, switching the controlled character or entering combat interrupts it.
- Charging into an obstacle or pushing a unit into it removes one durability point. The displacement stops at the collision even if the obstacle breaks. The same action's direct damage and collision must not charge the same hit twice.
- Destroyed obstacles stop blocking movement and LoS and disappear. v1 grants no XP/loot and leaves no rubble. Partial durability and destruction persist; no mid-combat saving.
- Future, not v1: per-object remains (e.g. rubble as difficult terrain), gold/valuables inside urns.

## Combat Map Object Direction

- Modeling map objects such as chests, doors, traps, levers, altars, and destructibles behind a shared interface/base type is acceptable, but the exact abstraction is still flexible.
- Preferred direction: `IInteractable` plus separate concrete classes rather than one heavy shared object hierarchy.
- `CombatMapObject` should be a lightweight polymorphic base type. Door-only state belongs to `DoorCombatMapObject`; lever-only state belongs to `LeverCombatMapObject`; future chests, traps, and obstacles follow the same pattern.
- Doors have a reversible open/closed state. A closed door blocks movement; an open door does not.
- Door interaction modes are:
  - `DirectOnly`: can only be toggled by direct interaction with the door.
  - `DirectAndLever`: can be toggled directly or by its linked lever.
  - `LeverOnly`: can only be toggled by its linked lever.
- Levers also have a reversible state and toggle their linked doors. A lever must not become permanently inactive after one use.

## World / Dungeon Assumptions

- Dungeons and city exploration use a 2D map with rooms and corridors.
- The usual dungeon goal is reaching the last room and exiting from the far side.
- Dungeons may have one or more floors depending on size.
- Floors may contain puzzles, traps, and parkour sections.
- Some of these may be mandatory if they block the only path or are required to open progression, for example via a lever.
- The end of a dungeon may or may not contain a boss.
- Optional rooms may exist.
- An optional room is not part of the required `Start -> Final` route. For generator v1 it is a one-connection side branch; later it may also create an optional loop.
- Room templates use rectangular `SizeInCells` for the first generator. Irregular footprints such as L/T-shaped rooms are deferred until after the playable dungeon loop, then represented as a cell footprint rather than only a rectangle.
- Rooms may contain chests, puzzle rewards, or rewards for defeating monsters.
- Dungeon-object placement v1 may choose any valid non-passage cell in an eligible room. It is intentionally only a
  vertical-slice rule.
- Later, a `DungeonRoomTemplate` should define semantic content anchors/areas for objects and encounters, for example
  a chest at the far end of a narrow corridor, an obstacle on a chosen choke point, a trap sequence, or a guarded
  reward. The generator then selects from those anchors instead of treating every valid cell as equivalent.
- Separate rooms may touch physically while remaining separated by a wall; adjacency alone does not create a passage.
- Battlefield shape depends on the current dungeon room/corridor.
- Example: a narrow corridor yields a narrow battlefield; a forest dungeon may include stones and trees as obstacles.
- Some quest dungeons may be 90% procedural but guarantee specific rooms.
- Dungeons have themes.
- Themes affect enemies, obstacles, room content, and loot.
- Themes also define the preferred procedural layout style. The generator must support both direct room-to-room passages and routed corridors; a graph connection does not automatically mean a corridor.
  - catacombs and temples: more ordered layouts, with doors usually centred on room walls and many rooms connected by narrow corridors.
  - forests and deserts: more organic/chaotic layouts, with larger positional offsets, long narrow spaces, and less regular connections between rooms.
  - a room may have multiple direct exits on the same wall when their door positions do not overlap.
  - this must be data-driven through a reusable layout profile referenced by `DungeonDefinition`, rather than hard-coded directly from `DungeonTheme`; a themed or story dungeon may override the default profile.
- Example themes/enemy pools include:
  - catacombs: undead, skeletons
  - forest: bandits, elves, wolves, infected trees
  - elven ruins: demons / possessed elves
  - docks: pirates, phoenix, parrot, shark-man
  - desert: awazars, scorpions, tornado
  - temple: monks of Malad, priests, knights, defilers, mummies, necromancers, defensive statue
  - mountains: trolls, rocs, Skullbreaker tribals, random travelers
  - swamp: carnivorous plants, alligators, drowned ones, mosquitoes
  - story/fabular dungeons: enemy set depends on the dungeon
- A corridor is represented as a normal `DungeonRoomType.Corridor` room with a narrow template. It is not a separately generated route/entity between two rooms; every generated graph connection is a direct doorway or open passage between adjacent rooms.
- Dungeons do not necessarily have only one exit; they may have multiple exits.
- Procedural dungeons generate:
  - room count in a range
  - layout
  - room contents
  - enemies
  - effectively most things
- `DungeonDefinition.EncounterDefinitions` is the candidate pool of regular encounter variants for a dungeon.
- The generator decides which rooms receive an encounter and whether a regular encounter may repeat.
- An optional final boss encounter is a separate guaranteed assignment, not an ordinary random-pool entry.
- Some generated elements are still scripted by difficulty/rules, for example:
  - boss near the end
  - two required puzzles to open passage
- There is always a path to at least one exit.
- Boss presence depends on dungeon difficulty.
- Dungeons generate fully at once rather than chunk-by-chunk.
- Outside combat, party members physically follow the leader.
- Switching active character changes the leader.
- In dialogue, the speaking character is whichever character would naturally speak in that conversation.
- Hidden rooms may exist behind optional levers or puzzles.
- Rooms may contain multiple content types at once, for example combat plus a lever.
- Room content is drawn from pools based on room type and theme.
- Traps trigger on stepping on a tile or activating an object, depending on trap type.
- Traps are not disarmable.
- Parkour includes jumping over gaps or lava.
- Puzzles may be optional or mandatory depending on whether they gate the final doors/path.
- Puzzle categories can include:
  - logical: activation order, symbols, patterns, clue reading like Silent Hill style
  - sequential/multi-step
  - story/fabular, mainly in story dungeons
  - simple environmental lever interactions, though these are treated as simpler interactions rather than full puzzles
- Defeated enemies disappear permanently from the dungeon instance.
- Enemies do not respawn within the same dungeon run.
- If the player escapes combat, enemies remain on the map in that place.
- The player can leave a dungeon by going back through an exit or by using a return-to-town scroll.
- If the return scroll is used in combat, everyone must stand within range 2 of the caster or be abandoned.
- Floors are separate maps within one dungeon.
- Floor state is saved and the player can return between floors while inside the dungeon instance.
- Leaving/abandoning a procedural dungeon normally discards that run. Approved exception: a return-to-town scroll preserves the existing dungeon instance for resuming; the player must finish or abandon it before selecting a new expedition.
- Predefined/story dungeons are exceptions, but they also do not preserve killed enemies or opened chests in the same way.
- Quest/story dungeons are usually procedural with guaranteed predefined rooms such as:
  - a specific boss room
  - a story dialogue room
  - a room with a required item
- Quest/story dungeons may still contain random elements.

## Dungeon Selector v2 — approved rules

- Show six expedition offers as markers on an area map, with a side detail panel and the active party plus departure action below (Darkest Dungeon 1 reference).
- One offer uses the rounded average level of the active party. Five others use nonzero offsets of 1–3 levels, with at least one easier and one harder offer where the 1–50 level range permits.
- Each offer has its own seed. Closing the UI and save/load preserve offers. Active-party changes update unselected offer levels without rerolling seeds.
- Replace the offer set after completing or abandoning the expedition. A return scroll preserves the run; offer Resume instead of starting another run until completion or abandonment.
- Show theme and level; show size when its model exists. Never show the seed in player UI.

## Dungeon Data Direction

- Arungard should be treated as a normal location but outside the procedural dungeon generator flow.
- Dungeon history/save should remember at least:
  - layout
  - which rooms were visited
  - which combats occurred
  - what loot was obtained
- Corridors do not need to be a separate structural type; they can be treated as rooms.
- A dedicated `DungeonGeneratorService` is the recommended direction.

## Section 11 - Unity Layer / Scenes / UI / Managers

### Scenes

- Main planned scenes:
  - `MainMenu`
  - `Arungard`
  - `Dungeon`
  - `Loading`
- Because combat happens on the same map, these core scenes are sufficient.
- A single dungeon scene/system can be reused while loading different dungeon data/layouts.

### Layering Rules

- Architectural rule remains:
  - `MonoBehaviour` = presentation / scene / input
  - regular C# classes = game logic and runtime state
  - `ScriptableObject` = definitions
- This separation should be preserved consistently.

### World / Combat State

- Dungeon/world management should switch game state between exploration and combat on the same map.
- UI should react to global mode/state changes.
- A global game mode/state enum is desirable to simplify flow, UI, and input handling.

### UI

- Main UI categories include:
  - exploration HUD
  - combat HUD
  - inventory
  - equipment
  - stash
  - crafting
  - shop
  - dialogue
  - quest journal
  - map
  - party management
  - save/load
  - settings
- UI should mainly use separate overlay windows/screens rather than fully replacing everything.
- Related inventory/equipment/stash/shop/crafting screens should look similar even if internal code sharing is still undecided.

### Input

- The game should support both mouse and keyboard.
- Combat is mainly mouse-driven.
- Skills can also be triggered through hotkeys such as `1-5` or more if the character supports more active skill slots.

### Loading / Persistence

- Loading screens are useful if loading becomes long enough to justify them.
- Whether async loading is needed should be decided based on optimization/performance reality.
- Persistent managers across scenes are acceptable if architecturally justified.

### Communication Style

- The overall system should be event-driven.
- Systems such as quests should listen to gameplay events like:
  - enemy killed
  - item obtained
  - npc talked to
  - dungeon completed

### Tools / Editor Support

- Debug tools are desired.
- Custom editors / inspectors are desired.
- Dungeon generator preview should be available in the editor.

### Architectural Style

- Preferred style:
  - smaller services
  - high granularity
  - each service should have one clear responsibility
- General direction confirmed:
  - `ScriptableObject` for definitions
  - runtime C# for state and logic
  - `MonoBehaviour` for presentation/input/integration
  - managers/services for coordination
- Prefer smaller, granular services with one clear responsibility each.

## Quest Assumptions

- Quests may start from NPCs.
- Quests may also start from picking up story items, dungeon items, or random drops.
- Quests include both main quests and side quests.

## Save Assumptions

- The game supports progress saving.
- Saves are automatic in situations such as entering or leaving a dungeon.
- Manual saves are also available.
- Save UX should be closer to a Skyrim-style chronological save list than fixed numbered slots.
- A campaign may optionally keep a bounded history, for example around 50 saves.
- Global settings should live outside campaign save files.
- A save should preferably be stored as one large save file rather than many split JSON files.
- Save files do not need to be human-readable for the player.
- Save data includes:
  - character stats and equipment
  - quests
  - NPC inventories
  - settings
  - dungeon history
- Autosaves should also happen after quest completion and at certain prescripted moments.
- Autosaves should rotate several recent entries depending on settings.
- Final save DTO naming can be decided during architecture work, but dedicated classes such as `GameSaveData`, `CharacterSaveData`, `DungeonSaveData`, `QuestSaveData`, and `WorldSaveData` are expected.

## Section 7 - NPC / Trade / Stash Assumptions

### NPC Roles

- NPCs can have multiple role labels/tags.
- Example labels:
  - `blacksmith`
  - `trader`
  - `questgiver`
- `questgiver` is effectively automatic when an NPC has quests attached.
- A blacksmith role allows crafting artisan goods, weapons, armor, plus reforging, upgrading, and infusion.
- NPCs outside the city could theoretically trade or give quests, but currently the planned NPC set is city-focused.

### Trade Rules

- Each trader sells a rotating set of specific items.
- The rotating offer refreshes after completing a dungeon.
- Traders can buy everything from the player.
- Buy price from the player is typically:
  - `1/2` value for relevant items
  - `1/4` value for irrelevant items, for example weapons sold to a non-blacksmith
- Prices depend on:
  - honor
  - loyalty
  - item type
  - rarity
  - upgrade state
  - and similar item properties
- Base pricing rules are otherwise fixed.

### NPC Treasury and Guild Payroll

- Trader and service NPCs have their own persistent gold balance used to pay for items bought from the player.
- An NPC has no maximum gold limit. Its balance may accumulate indefinitely; there is no `maximumGold` field in the economy model.
- NPC economy state includes at least current gold, cumulative gold received, cumulative gold spent, and progress toward the next guild payout.
- After a configured number of fully completed dungeons, the NPC receives a guild payroll payment. The payment is calculated from the campaign level, represented by the highest level of the active party at the payout trigger.
- A payout is added to the current balance and does not reset or replace the existing balance.
- Buying an item from an NPC increases that NPC's balance. Selling to an NPC is blocked when the NPC cannot afford the transaction.
- Starting gold, payout cadence, base amount, and campaign-level scaling are configuration/defines rather than hard-coded trade UI rules.
- NPC treasury and payout progress must be persisted with the NPC runtime state.
- Treasury and loyalty are separate systems. Loyalty may later modify economy parameters, but it has no implicit effect until explicitly defined.

### Shop Inventory

- The rotating offer is fully replaced on refresh.
- There is no permanent stock section beyond the rotating offer.
- Buyback items are separate and do not disappear; this is effectively a buyback system.
- Shop logic should likely be a specialized type distinct from ordinary inventory because of rotating stock and buyback behavior.
- `ShopInventory` can be modeled as inheriting from or extending general inventory behavior.

### NPC Loyalty

- Every NPC has its own loyalty level.
- Loyalty has 15 levels.
- The gold needed to advance from the current loyalty level to the next is:

`CEILING(POW(4; level/3 + 4))`

- This cost applies per transition from one level to the next, not as a total cumulative requirement.
- Loyalty does not decrease.
- Loyalty and honor affect prices independently.
- Loyalty unlocks services rather than dialogue/story content.
- Discount thresholds should unlock at the same loyalty levels across NPCs.
- Other loyalty rewards/services can vary per NPC.

### Mercenaries

- Mercenaries are treated more like recruitable companions than ordinary NPCs.
- Recruiting them is free, but only possible under defined conditions.
- If dismissed/unneeded, they return to the inn and can be recruited again.
- They have their own inventory/equipment/progression but do not start with gear; the player must invest in them.

### Stash

- The stash is shared for the whole party.
- It is available in the city.
- It has infinite capacity.
- The stash has neither a weight limit nor slot or stack limits; it is the long-term collection and crafting buffer, not a second carry-capacity constraint.
- Everything can be stored there, including quest items.
- Characters can equip items directly from the stash.

### Dialogue / Interaction

- NPCs use a dialogue system.
- Trade/crafting/quest interaction can exist as branches/options within dialogue.
- Low honor can block conversation and trade.

### NPC Interaction With Items

- Quest NPCs can require item delivery from the player's inventory.
- There are no dedicated healer/storage-keeper NPC archetypes as separate required types right now.

### Arungard Hub

- Arungard contains all major services in the first version.
- Approved presentation decision (2026-10-07): expeditions are accessed at the city gate, rather than a local descent into the Catacombs. The gate is the general departure point for available expedition destinations; preserve the existing expedition selector and active-run resume rules. The new city layout remains a design proposal until reviewed.
- Additional hubs/cities may appear later.
- NPC position/availability/offer changes are not currently expected to shift heavily with story progression.

### NPC Data Direction

- NPC loyalty should be stored in save data.
- Recommended technical direction:
  - `NpcDefinition : ScriptableObject`
  - `NpcInstance`
  - optional role/module data such as trader, blacksmith, and quest-giver capabilities
- NPC role data as modular role objects/data is the preferred direction.
- Buyback has no time limit.

## Section 8 - Quests / Story Progression Assumptions

### Quest Types

- Quest types include:
  - main
  - companion
  - faction
  - mini (side)
  - radiant
- Companion quests are side quests centered around a specific companion's story.
- Companion quests may range from a simple story to a full arc / quest series.
- Quests can include city-only steps and dungeon-completion steps.

### Quest Start

- Quests may start from:
  - NPC interaction
  - entering a location
  - killing a specific enemy
  - picking up an item
  - a drop
- Hidden quests are possible.
- Quests cannot be rejected, but can be hidden in the journal.

### Quest Objectives

- Objective types include:
  - talk to NPC
  - deliver item
  - kill specific enemy
  - kill a specific dungeon type / dungeon objective target
  - interact with an object in a location
  - complete/clear a dungeon
- A quest is structured as stages.
- Each stage may contain one or more objectives.
- Example: talk to 3 different NPCs, then advance to another talk objective.
- Optional objectives also exist.
- Quest progression may branch based on dialogue choices or on which NPC receives the result of a quest.

### Quest States

- Quest states include:
  - unavailable
  - available
  - active
  - completed
  - failed
  - turned_in
- Quests can fail permanently.
- Quests can be blocked by missing characters, story decisions, or similar gating conditions.

### Quest Requirements / Gating

- Quests may require:
  - a specific character in the party
  - a specific talking character's honor
  - completion of another quest
- Quests can block other quests.
- Dialogue/story choices can unlock one branch and block another.

### Quest Rewards

- Quest rewards may include:
  - experience
  - gold
  - items
  - unique items
  - companions
  - honor
  - NPC loyalty
  - another quest
  - a unique dungeon
- Some rewards may be random, for example items.
- Rewards may depend on how the quest was completed.

### Companion Quest Effects

- Quests can:
  - recruit a new companion
  - temporarily remove a companion
  - change character availability
  - change relations between characters
- Companion quests are not currently planned to affect gameplay systems heavily beyond availability/rewards/story.
- If a required companion dies, the quest is blocked until that companion is recovered.

### Story / World Progression

- Main story progression is a chain of quests.
- The chain can branch and later merge back together.
- The world/city can change after important quests.
- Unlocking new dungeons and services is driven more by level/progression than by quests alone.
- The overall main path is mostly linear, with light branching and potentially very different endings based on important story choices.

### Quest Tracking

- Save/runtime should track quest progress so far.
- Quest items should generally be checked by `itemId` and quest conditions rather than having hardwired quest ownership in the item itself.
- Quests should support their own internal flags/conditions for branching and hidden state.

### Quest Data Direction

- Recommended technical direction:
  - `QuestDefinition : ScriptableObject`
  - `QuestInstance` for runtime/save state
  - objective definitions plus quest flags/conditions
  - `QuestManager + WorldState` for story progression and world changes

## Loot Assumptions

- Ordinary enemy loot may include:
  - lootbags
  - gold
  - experience
  - materials
  - quest items when applicable
  - sometimes valuables
- Equipment and potions come from lootbags rather than directly from standard enemy drops.
- Loot should preferably be determined when the dungeon is generated, to reduce save-scumming.
- Chests use the same broad loot-table system as enemies.
- Bosses do not necessarily use a different loot-table mechanic, but they drop different things.

## Quest Item / Key Assumptions

- Quest items cannot be sold or discarded.
- Quest items may be placed in the stash.
- Keys are normal inventory items.
- Sell-only valuables exist purely for sale.

## Open Technical Decisions

- Character definitions such as Jerzy, companions, and enemies should be stored as `CharacterDefinition : ScriptableObject`.
- Class definitions such as Warrior/Scout/Mage/Paladin should be stored as `CharacterClassData : ScriptableObject`.

## Confirmed Character Template Direction

- The direct `BaseStats` currently placed on `CharacterDefinition` are temporary bootstrap data for the first exploration/combat loop.
- The intended final model is composition through reusable `CharacterTemplateDefinition : ScriptableObject` assets, for example a `Rogue Template`.
- A character definition should reference a template instead of duplicating its full baseline statline.
- Templates will use the benchmark and progression data from `design/Statystyki Postaci.xlsx` as their source material.
- `CharacterDefinition` remains the place for the character's identity and future individual differences, such as unique skills or explicit stat overrides.

## Additional Imported Design References

The `design/` folder contains additional high-level and content-side references that should be treated as supplementary context rather than as overrides of previously confirmed decisions. If any spreadsheet/docx content conflicts with already confirmed rules in this file, the previously confirmed rules remain authoritative.

### Imported reference files currently observed

- `Fabułka.docx`
- `Galduryty.xlsx`
- `Lojalność NPC.xlsx`
- `Opisy mechanik.docx`
- `Pomysły.xlsx`
- `Przeciwnicy NPC.xlsx`
- `Spis innych przedmiotów.xlsx`
- `Statystyki Postaci.xlsx`
- `Umiejętności aktywne.xlsx`
- `Ważne wzory.docx`
- `Ważne wzory(1).docx`
- `Wyposażenie.xlsx`

## Additional World / Lore Context From Imported Files

- The story material references a mythic creation narrative with:
  - `Wszechmogący`
  - `Światłość`
  - `Ciemność`
- Named divine / primordial figures found in story notes:
  - `Tywyllych`
  - `Dimonerhin`
  - `Sidriel`
  - `Mist`
- Named playable-world locations/factions mentioned in the imported story draft:
  - `Arungard`
  - `Khilram`
  - `Shellania`
  - `Żelazne Imperium`
  - `Thaalonia`
  - `Las Everdun`
  - `Karczma Lisi Ogon`
- The story draft strongly suggests a cult / prophecy thread connected to forest and divine lore.
- Named story/cult/NPC draft references include, among others:
  - `Reimond`
  - `Wladimir`
  - `Krzysztof` / `Krzysiek`
  - `Henryk`
  - `Ehrelyon`
  - `Ghorn`
- The imported story text includes examples of dialogue choices that can influence near-term gameplay outcomes, for example whether a guard accompanies the player into an early fight.

## Additional NPC Loyalty Detail From Imported Files

The loyalty spreadsheet appears to provide a more detailed per-NPC-service reward ladder. This supplements, but does not replace, the already confirmed loyalty model.

### Shared loyalty behavior still consistent with earlier assumptions

- Loyalty levels are per NPC.
- Loyalty unlocks discounts and service improvements.
- Merchant/service NPC roles include:
  - blacksmith
  - alchemist/chemist
  - enchanter/runeforger-like magic crafter

### Additional detailed loyalty milestones seen in the sheet

- Level `1`
  - allows trading in common materials
- Level `2`
  - service costs reduced by `1%`
- Level `3`
  - blacksmith: crafted equipment has `5%` better primary stats
  - alchemist: crafted potions/bombs reduce alcohol-poisoning effect strength by `25%` if it occurs
  - enchanter: all crafted and identified galdurytes gain `5%` stronger effects
- Level `4`
  - service costs reduced by `3%`
- Level `5`
  - allows trading in uncommon materials
- Level `6`
  - blacksmith: crafted equipment starts with `25%` upgrade modifier
  - alchemist: refilling potions/bombs can grant a chance to refill `2` charges
  - enchanter: adding/removing galdurytes gains `10%` better success chance
- Level `7`
  - service costs reduced by `5%`
- Level `8`
  - blacksmith/alchemist: allows trading in rare materials
  - enchanter: identified galdurytes gain an additional effect
- Level `9`
  - service costs reduced by `7%`
- Level `10`
  - allows trading in ancient materials
- Level `11`
  - blacksmith: upgrading already-upgraded equipment becomes cheaper proportionally to current modifier
  - alchemist: all created potions/bombs become `5%` stronger
  - enchanter: all galduryte effects become `20%` stronger
- Level `12`
  - service costs reduced by `9%`
- Level `13`
  - allows trading in legendary materials
- Level `14`
  - blacksmith: created equipment has at least rarity/uniqueness `4`
  - alchemist: refilling potions/bombs becomes free and has `33%` chance not to consume alcohol/powder
  - enchanter: created galdurytes have at least rarity/uniqueness `4`
- Level `15`
  - blacksmith: reforging cannot decrease uniqueness
  - alchemist: potions/bombs may use one additional catalyst
  - enchanter: the first socketed galduryte does not consume socket capacity

### Additional role summaries from the loyalty sheet

- Blacksmith service scope reference:
  - create equipment
  - upgrade equipment
  - trade equipment
  - reforge equipment
  - create artisan/crafting ingredients
  - trade artisan/crafting ingredients
- Alchemist service scope reference:
  - create potions
  - trade potions
  - trade alchemical materials
  - refill potions
- Enchanter service scope reference:
  - create galdurytes
  - add galdurytes
  - remove galdurytes
  - destroy galdurytes
  - trade galdurytes
  - trade magical ingredients
  - create magical ingredients

## Additional Class / Stat Reference Data From Imported Files

The spreadsheet `Statystyki Postaci.xlsx` appears to provide benchmark ranges for class progression rather than replacing the previously confirmed stat model.

### Referenced class benchmark columns

- Warrior
- Scout
- Mage
- Paladin
- Cultist

### Additional enemy / template class reference

- `Cultist` appears explicitly as a reference statline in the imported class-stat sheet.
- This suggests cultists are a named enemy/NPC combat archetype, even if they do not need to be modeled as a normal player class.

### Example benchmark tendencies from the stat sheet

- Paladin has the highest HP and strong dual defenses in the imported stat reference.
- Scout has the highest dodge and speed reference values.
- Mage has the highest magical attack tendency and high fire resistance in the imported stat reference.
- Warrior has strong physical durability and solid anti-bleed / anti-weakening references.
- Cultist has a balanced hostile stat profile with notable magical defense and speed.

### Additional reference speed values found in the stat sheet

- Warrior speed reference: `40`
- Scout speed reference: `55`
- Mage speed reference: `45`
- Paladin speed reference: `35`
- Cultist speed reference: `50`

## Additional Skill Content Reference From Imported Files

The spreadsheet `Umiejętności aktywne.xlsx` contains concrete skill content examples. These should be treated as initial content references rather than as final locked numbers.

### Confirmed / referenced warrior tree names found in the sheet

- `Wojownik`
- `Dowódca`
- `Awanturnik`
- `Zbrojny`

### Confirmed / referenced scout tree names found in the sheet

- `Zwiadowca`
- `Skrytobójca`
- `Tancerz Ostrzy`
- `Strażnik`

### Confirmed / referenced mage tree names found in the sheet

- `Czarodziej`
- `Czarnoksiężnik`
- `Mag Ognia`
- `Mag Lodu`

### Confirmed / referenced paladin tree names found in the sheet

- `Paladyn`
- `Duchowny`
- `Obrońca`
- `Krzyżowiec`
- `Kapłan`
- `Szaman`
- `Druid`
- `Kapłan Run`

The sheet may include more class-grouping labels than the final class-tree architecture, so previously confirmed system structure remains authoritative. However, these names are useful as content/theme references.

### Concrete skill examples seen in imported sheets

- Warrior examples:
  - `Cięcie`
  - `Kopnięcie`
  - `Okrzyk Wojenny`
  - `Prawy Sierpowy`
  - `Bandaż Polowy`
  - `Rozkaz Unicestwienia`
  - `Unieruchomienie`
  - `Blok`
  - `Rozkaz Przegrupowania`
  - `Przedarcie`
  - `Krwawiące Zniszczenie`
  - `Krzyk`
  - `Młyniec`
  - `Pragnienie Śmierci`
  - `Heroiczna Furia`
  - `Postawa Defensywna`
  - `Cios Rękojeścią`
  - `Przewidywanie`
  - `Odnowienie Energii`
  - `Żelazna Ostoja`
- Scout examples:
  - `Rzut Sztyletem`
  - `Wymiana`
  - `Chmura Dymu`
  - `Linka z Hakiem`
    - Zatwierdzona i wdrożona zmiana bazowa (2026-10-09): koszt 25 PP i 25% PA; przeciw przeciwnikowi 80% szansy na przyciągnięcie o 3 pola.
    - Bazowy odzysk własnego sztyletu do ekwipunku bez losowania: oba składniki kosztu o 20% niższe, czyli 20 PP i 20% PA. Sprawny Odzysk w T4:2 Tancerza Ostrzy (jedna ranga za 1 punkt) zwiększa zniżkę do 60% (10 PP i 10% PA). Zastępuje Mocną Wątrobę, bez wymagania Siły Naparu, z zachowaniem standardowego progu T4. Nowe ID to `node.scout.blade_dancer.efficient_recovery`; wczytywanie migruje stare ID `node.scout.blade_dancer.strong_liver` bez podwojenia inwestycji, a następny zapis używa nowego ID; usunięto skrócenie kary Toniku (pozostaje 3 tury). Bez dodatkowego limitu raz na turę, nowego skilla do dobywania ani premii do ruchu. Szczegóły i status: `VS07_SCOUT_CONTENT.md`.
  - `Celny Rzut`
  - `Rykoszet`
  - `Słaby Punkt`
  - `Wojownik Cieni`
  - `Przewrót w Przód`
  - `Niesamowite Skupienie`
  - `Zatruty Sztylet`
  - `Szybkie Ciosy`
  - `Tonik Pobudzający`
  - `Wzmacniający Jad`
  - `Klątwa Schorowanych`
  - `Refleks Zabójcy`
  - `Kontuzjowanie`
  - `Prowokacja`
  - `Kontratak`
  - `Pociągnięcie za Sznurki`
- Mage examples:
  - `Kula Energii`
  - `Kula Ognia`
  - `Skupienie`
  - `Magiczna Tarcza`
  - `Męczące Czary`
  - `Ofiara`
  - `Skupienie Magii`
  - `Poświęcenie`
  - `Roztrojenie Energii`
  - `Rytuał`
  - `Teleportacja`
  - `Egzaltacja`

### Additional skill-mechanic references from the sheet

- Active skill examples confirm many previously discussed mechanics as concrete content:
  - shield values based on max health or attack
  - invisibility
  - taunt/provoke
  - guaranteed critical hit effects
  - movement effects such as swap, pull, charge
  - linear targeting
  - area enemy targeting
  - self-sacrifice / ally sacrifice mechanics
  - free-repeat attacks or delayed follow-up behavior
- Some skills explicitly consume or reference daggers/knives in the UI notation, reinforcing the scout knife-recovery mechanic already discussed.

## Additional Galduryte Content Reference From Imported Files

The imported galduryte sheet adds more concrete pool/effect references beyond the already confirmed system rules.

### Additional powder-color / effect-pool references

- Red powder pool:
  - general damage bonus
  - physical damage bonus
  - magical damage bonus
- Blue powder pool:
  - critical chance bonus
  - critical damage bonus
- Purple powder pool:
  - hit chance bonus
  - effect chance / status-chance style bonuses
- Lemon/yellow pool:
  - bleed on hit chance
  - poison on hit chance
  - burn on hit chance
- Emerald pool:
  - heal on hit
  - resource restore on hit
  - movement acceleration / AV bonus on hit
- Black pool:
  - item drop chance bonus
  - resource generation bonus
- Pink pool:
  - damage vs undead
  - damage vs humans
  - damage vs beasts
  - damage vs demons
- White pool:
  - stun on hit chance
  - freeze on hit chance
  - slow on hit chance
- Gold pool:
  - experience gain bonus
  - gold gain bonus
- Amber pool:
  - physical defense penetration
  - magical defense penetration

### Additional referenced galduryte rules

- Galdurytes give passive effects while equipped in a valid weapon/armor slot.
- They can be added and removed by the enchanter for a fee.
- Dropped galdurytes are unidentified and must be researched/identified at the enchanter.
- Crafted galdurytes are immediately identified.
- The sheet explicitly states armor and weapons receive a galduryte slot every `20%` upgrade step.
- The sheet references socketing only into:
  - weapon
  - chest armor / breastplate
- The sheet explicitly notes removal failure may produce a broken galduryte that no longer grants effects.

## Additional Equipment / Material Reference From Imported Files

- The equipment sheet provides explicit example part/material tables for class weapons and armors.
- Imported examples reinforce that:
  - warrior weapon archetype is `Długi Miecz`
  - warrior armor archetype is `Kolczuga`
- The equipment/material references include explicit named materials such as:
  - iron
  - nickel
  - copper
  - tin
  - steel
  - invar
  - meteoric iron
- The imported materials/loot sheets also reference additional higher-tier materials and resources, for example:
  - cobalt
  - gold ore
  - nefolite
  - adamantium
  - cifilm
  - orichalcum
  - many themed woods, hides, fibers, stones, and alchemical ingredients

## Additional Loot / Resource-Bag Reference From Imported Files

- The imported item sheets reference named themed lootbags/resource bags, for example:
  - bony/catacomb bag
  - forest bag
  - marine/dock bag
  - desert bag
  - temple bag
  - mountain bag
  - swamp bag
  - demonic bag
- Lootbag contents appear to vary by level brackets and dungeon theme.
- The imported item sheets reinforce that:
  - materials are strongly theme-linked
  - alcohol tiers scale with progression
  - different biome bags pull from different pools of ores, skins, woods, fibers, herbs, and magical reagents

## Current Unity Implementation State

- A Unity project exists in `Godmist Unity`.
- The current implementation focus is a combat sandbox vertical slice.

### Current Combat Sandbox Runtime

- The sandbox currently has working runtime foundations for:
  - `CharacterDefinition`
  - `CharacterClassData`
  - `CharacterInstance`
  - `CharacterStats`
  - `ResourcePool`
  - `ResistanceSet`
  - `ShieldPool`
  - `GridPosition`
  - `BattleGrid`
  - `BattleUnit`
    - `CombatSession`
- The sandbox also has a first presentation prefab for combat units:
  - `Assets/_Project/Scripts/Views/Characters/CharacterView.cs`
  - `Assets/_Project/Prefabs/Characters/PF_BattleUnit.prefab`
  - the prefab contains a colored body sprite, a world-space name label, and the `CharacterView` component
- The prefab is spawned and synchronized by a dedicated view component; `CombatSandboxBootstrap` remains responsible for the runtime session rather than owning the visual instances.
- `CombatUnitViewSpawner` now instantiates one view per runtime `BattleUnit`, binds it to `CharacterView`, synchronizes grid-to-world positions, and removes views for units removed from the session.
- Combat movement now rejects target cells already occupied by another `BattleUnit`.
- The sandbox debug shortcut for removing an enemy selects the first enemy from the session instead of requiring the enemy to be the active unit.
- The first skill execution slice is working: a single enemy target can receive a `DealDamage` effect, AP and resource costs are validated and consumed, defeated units are removed, and player-facing messages are separated from Unity debug diagnostics.
- The second test skill is working with a `Push` effect: it moves a target away from the caster while respecting grid bounds and occupied cells.
- The sandbox already supports:
  - combat session bootstrap
  - basic grid movement
  - simple turn order
  - simplified AP v1
  - end turn
  - victory condition
  - defeat condition
  - simple enemy AI: move toward target and perform a basic attack
  - debug runtime panel
  - scrollable battle log

### Current CombatSandbox Scene HUD Hierarchy

- The existing `Canvas` hierarchy in the combat sandbox is already organized as:
  - `CombatHUD`
    - `TopBar`
      - `EncounterInfo`
      - `TurnOrderPanel`
    - `LeftPanel`
      - `PartyPanel`
    - `BottomPanel`
      - `ActiveUnitPanel`
      - `SkillBar`
      - `ActionButtons`
    - `RightPanel`
      - `ContextPanel`
    - `Overlay`
      - `TooltipLayer`
      - `FloatingTextLayer`
      - `ModalLayer`
  - `DebugHUD`
- Additional temporary runtime/debug UI modules currently exist and should be interpreted relative to this hierarchy:
  - battle log belongs semantically to `CombatHUD`
  - runtime stat/debug panel belongs semantically to `DebugHUD`

### Important Implementation Note

- When extending combat HUD work, do not redesign the HUD container structure from scratch.
- New HUD tasks should bind runtime data into the existing scene hierarchy first, especially:
  - `EncounterInfo`
  - `TurnOrderPanel`
  - `ActiveUnitPanel`
- `SkillBar`, `PartyPanel`, `ActionButtons`, `ContextPanel`, and overlay layers may remain placeholders until their dedicated cards are implemented.

### Current Unity Asset Convention

- Code lives under `Assets/_Project/Scripts/...`.
- `ScriptableObject` assets are stored separately under `Assets/_Project/ScriptableObjects/...`.
- Current confirmed asset folders include:
  - `Assets/_Project/ScriptableObjects/Characters`
  - `Assets/_Project/ScriptableObjects/Classes`
  - `Assets/_Project/ScriptableObjects/Skills`
- Confirmed examples already present in the project:
  - `CD_Jerzy.asset`
  - `CCD_Scout.asset`
- When adding new gameplay data assets such as skills, classes, or character definitions, prefer the matching `ScriptableObjects/...` folder rather than storing assets alongside scripts.


### World unit hover and inspection — approved Dungeon v3 UX
- Exploration and combat share hover highlighting plus a compact name/current-max HP tooltip. No persistent world name labels on party members/enemies.
- Right-click opens the existing UIW context menu with Inspect; left-click retains movement, party selection and combat targeting. Right-click during active skill targeting cancels targeting instead of opening a menu.
- City NPC left-click and right-click Talk approach a reachable point inside the existing interaction radius through town pathfinding, then request dialogue. New movement orders or changing the leader cancel pending dialogue. NPC hover shows identity; NPCs have no fabricated combat HP. The existing E interaction remains available.
- Fog-hidden units cannot be hovered or inspected. Losing visibility or switching mode/focused combat clears the world popup/inspect. No new knowledge or persistence model.


### Vertical Slice v1 — katalog VS-01

Aktualne źródło danych contentu: `design/content_pass_1.xlsx` w katalogu repozytorium (8 arkuszy). Potwierdzenie użytkownika: Awanturnik zamiast Zbrojnego jest zamierzonym wyborem; Kapłan to nowa nazwa dawnego Duchownego. Przedmioty i składniki z innych lochów będą możliwe do uzyskania w ofercie NPC, bez gwarancji dostępności. Nie rozszerza to slice’a o inne lochy ani nie wymaga gwarantowanego stocku każdego składnika.

Punktowa weryfikacja poprawionej wersji arkusza: Zapasowe Sztylety mają 2 rangi; Roztrojenie Energii ma 70%, ulepszenie 85%/100%; Wyczerpanie wymaga T2:1; lootbag używa Kosarza pospolitego. Ektoplazma i Pazur ghula mają Lv 21 i zgodnie z późniejszą decyzją użytkownika są poza passem 1–20; dotyczy to całej grupy materiałów przeciwników od L21.

Decyzje użytkownika z 2026-09-16. Poniższy zakres zastępuje wcześniejszą roboczą propozycję 3 postaci, poziomów 1–12 i 1–2 theme. Nie stanowi potwierdzenia kompletności assetów ani zamknięcia VS-01.

| Postać | Klasa | Wybrane drzewka |
| --- | --- | --- |
| Jerzy | Scout / Zwiadowca | Skrytobójca + Tancerz Ostrzy |
| Wladimir | Wojownik | Dowódca + Awanturnik |
| Alison | Czarodziejka | Czarnoksiężnik + Mag Ognia |
| Reimond | Paladyn | Kapłan + Krzyżowiec |

- Wszystkie cztery postacie dostępne od początku. Fabularne odblokowywanie jest poza obecnym zakresem, ponieważ fabuła nie jest jeszcze przygotowana.
- Po dwa drzewka na klasę, tylko pierwsze cztery tiery, z docelowymi aktywnymi umiejętnościami i pasywkami tych części drzewek. Nie zmienia to docelowego modelu trzech drzewek i siedmiu tierów.
- Każda klasa zaczyna z pięcioma umiejętnościami, wliczając autoatak, aby wypełnić docelowe sloty. Dodatkowe aktywne wynikają z wybranych drzewek; bez narzucania sztucznej liczby dwóch aktywnych na każde drzewko.
- Content do około poziomu 20. Limit postaci pozostaje 50; poziom 20 nie zatrzymuje zdobywania XP. Wyższe tiery drzewek nie należą do content passu VS v1; zachowanie po przekroczeniu zakresu musi być czytelne i nie udostępniać niedziałającego contentu.
- Dwa przedziały materiałów i wyposażenia: 1–10 oraz 11–20. Konkretne pule mają wynikać z materiałów projektu, a nie wcześniejszych niezatwierdzonych propozycji agenta.
- Więcej klejnotów do biżuterii oraz około 50–70% docelowej puli efektów galdurytów. Jest to jawnie zatwierdzone rozszerzenie contentu; konkretną listę, podstawę liczenia pokrycia i brakujące mechaniki trzeba ustalić przed authoringiem.
- Consumables: przygotować składniki dostępne w Katakumbach w obu przedziałach poziomów i ich receptury. Efekty mikstur wynikają ze składników. Nie ograniczać katalogu do z góry wymyślonej listy gotowych mikstur.
- Jedyny theme: Katakumby. Bez podestów/balkonów w tym passie; karta kotew przestrzennych z wysokością nie jest jego zależnością. Istniejących assetów testowych wysokości nie usuwać.
- Produkcyjne pokoje oraz pełna konfiguracja Layout Profile i definicji dungeonu Katakumb. Użytkownik zatwierdził 16 produkcyjnych szablonów 2026-09-22; roboczy podział i kandydaci reuse w VS01_CONTENT_CATALOG.md.
- Wybrać siedem najważniejszych spośród dziewięciu mobów Katakumb na podstawie materiałów źródłowych; przygotować różne składy encounterów.
- Boss: Duch Umarłego Króla, powtarzalny w wariantach poziomu 10 i 20. Użytkownik potwierdził, że wcześniejsze określenie „nekromanta” oznaczało tego samego zamierzonego bossa i wynikało z niedokładnie zapamiętanej nazwy. Nie każda wyprawa ma bossa na końcu. Specjalne wyposażenie bossa i jego warianty są opisane w content_pass_1.xlsx; ostateczne pokrycie wykonawcze ustalić w katalogu.
- Dostępność bossa wynika z Danger motywu lochu (0–100%): Low 0–39%, Medium 40–69%, High 70–99%, 100% odblokowuje wyprawę bossową aż do jej ukończenia. Od poziomu 8 ukończenie wyprawy w motywie obniża jego Danger o 30 punktów, a wyprawa w innym motywie podnosi go losowo o 10–20 punktów. Przy osiągnięciu przez dowolną postać poziomu 10, 20 itd. Danger jednego losowego dostępnego motywu rośnie do 100%; wybór jest trwały w kampanii. Po ukończeniu wyprawy z bossem Danger jego motywu wraca do 0%. W pierwszym passie jedynym dostępnym motywem są Katakumby.
- Przewidziana misja ukończenia wyprawy z bossem na końcu; warunki i nagrody misji pozostają do ustalenia przed zależną implementacją. Rozwijać istniejącego ownera questów.
- Lootbagi Katakumb z docelowymi tematycznymi tabelami dla obu przedziałów, zamiast przypadkowych testowych przedmiotów. Spiąć źródła materiałów, receptury, nagrody i gospodarkę.
- Tempo: wyprawa orientacyjnie 20–30 minut, ale bez stałego przelicznika jedna wyprawa = jeden poziom. Obowiązują późniejsze wzory XP i misji z BALANCE_DECISIONS_PENDING.md; rzeczywiste tempo zmierzyć na gotowym contencie w VS-18.
- Powtarzalność bossa oznacza ponowne nagrody w nowej wyprawie, nie wielokrotne naliczanie nagrody za tę samą walkę po save/load. Brak zapisu w środku walki pozostaje zasadą.

Stan punktowej inspekcji przed authoringiem (nie runtime acceptance): istnieją definicje czterech klas i bazowe wyposażenie; Wladimir_Test ma pustą referencję ClassData. Scout ma trzy assety drzewek, lecz BladeDancer/Guardian zawierają skopiowane węzły Assassin i test1/test2; nie traktować ich jako kompletnych odmiennych drzewek. Katakumby nadal korzystają z testowych encounterów. Istniejące definicje, ID i lokalne zmiany użytkownika zachować i rozwijać.

Imienna checklista VS-01 przygotowana 2026-09-22 w VS01_CONTENT_CATALOG.md: startowe skille, wszystkie węzły T1–T4, siedem mobów i boss, pule materiałów/części/składników/klejnotów/efektów, unikaty, lootbagi i 16 pokojów. Katalog gotowy do akceptacji dokumentacyjnej, authoring nadal w kartach VS-06–17. Kolejny krok to punktowe review VS-02 pod wymienione zależności.

### Dungeon v3 scope closure — approved 2026-09-16
- Finish v3 with exploration mouse-wheel zoom consistent with combat, performance measurements and fixes for material issues, code review, and integrated acceptance/UI smoke.
- Move new logical/dexterity puzzles, new exploration trap behaviours, jumping/parkour, and special authored ambush/hidden-encounter mechanics to Dungeon v4. Detailed rules are not approved by this scheduling decision.
- Existing levers/progression conditions, traps, FOV/Fog, patrols and encounters remain part of v3 regression coverage.
- Complete elevation support across spatial anchors/objects before authoring multi-level content, outside v3 closure. Later encounter composition authoring remains deferred.
- Major visual refinement of platforms/balconies is deferred; current accepted presentation remains the v3 baseline. Scouting/sneak/detection expansion retain their prior v4 assignment.


### Exploration zoom — v3 acceptance stage
- Mouse wheel zoom in exploration and combat uses orthographic size 2.5–10 (200–50%, 100%=5), exponential wheel steps and smooth interpolation. UI scrolling does not issue world zoom.
- First entry to seamless combat preserves the current zoom; returning to exploration retains combat's current zoom rather than restoring a stale exploration value. Existing combat sessions keep their remembered views. Home still explicitly frames the combat arena.


### 2026-10-12 — produkcyjne oferty Lasu
Las jest dostępny na tablicy ofert obok Katakumb: dungeon.forest.expedition bez bossa oraz dungeon.forest.boss_expedition z Entem na poziomach 10/20, przy 100 Danger lub kamieniu milowym. Korzysta ze wspólnych zasad Danger Level i zapisu. dungeon.forest.prototype pozostaje tylko do testów. Zastępuje wcześniejsze stwierdzenie o dostępności wyłącznie Katakumb.

### Szkielet Zabójca — wyprawy i torba — 2026-10-14

W Katakumbach dostępni są Król oraz Szkielet Zabójca na poziomach10/20. Przy odblokowaniu bossowej okazji losowany jest jeden boss na wybranym poziomie; decyzja jest zapisywana aż do ukończenia wyprawy. Zachowujemy jeden bossowy slot na tablicy sześciu ofert. Odświeżenie tablicy ani zapis/wczytanie nie losują ponownie bossa. Dawne zapisane okazje pozostają bez zmian. Torba Szkieleta Zabójcy zachowuje zwykłe pule aktualnej torby Króla, ale zamiast Królewskiej ektoplazmy daje Kość Zabójcy; po korekcie użytkownika ma WŁASNĄ pulę unikatów zamiast unikatów Króla. Własna wyprawa: dungeon.catacombs.skeleton_slayer_expedition; własna torba: item.lootbag.catacombs.skeleton_slayer; materiał: item.material.skeleton_slayer_bone. Oferty: LocalHandoffs/skeleton-slayer-offers-loot-20261014/HANDOFF.md. Aktualne unikaty: LocalHandoffs/approved-boss-uniques-20261014/HANDOFF.md.

### Unikaty Szkieleta Zabójcy i Płaszcz Przemytnika — 2026-10-14

Pula Szkieleta: Kolczuga Furiata (+25/+30 maks. rage), Żebrowana Tunika, Szaty Runiczne (+30%/+45% maks. many do HP), Młot Zabójcy (15%/18% szansy ogłuszenia na2 tury po trafieniu). Szansa unikatów30%, pule zwykłego lootu bez zmiany. Żebrowana Tunika zastępuje roboczą nazwę „Pancerz z Żeber Zabójcy”: 25/30/35/40/50% redukcji pierwszego bezpośredniego fizycznego trafienia między początkami własnych tur; pudła/uniki/DOT/całkowite pochłonięcie bez utraty HP nie zużywają ochrony. Płaszcz Przemytnika dla czarodzieja: 30/35/40/45/55% redukcji fizycznych ataków dystansowych. Progi obu pancerzy10/20/30/40/50, redukcje mnożnikowe. Płaszcz zarejestrowany; drop oczekuje na wdrożenie Przywódcy Bandytów (brak encounteru/torby). Nie trafia do zwykłych bandytów ani Enta. Istniejące ikony klas/slotów; osobne grafiki unikatów pozostają do przygotowania. Wcześniejsza wspólna pula Króla jest nieaktualna.


### Przywódca Bandytów 2.0 — gameplay i wyprawa — 2026-10-14

Art follow-up 2026-10-15 (motion):128 klatekPNG/32 klipy Aseprite idle/chodu,
4 klatki na pętlę i8kierunków obu faz w `../output/bandit-leader-motion-v1`.
ART_READY_FOR_REVIEW;96canvas/32PPU/pivot(48,8)/frozen96, idle300ms,
walk180ms. Bez ostrej statycznej00 w generowanej pętli. Poprawione kroki
i skok sylwetki;128/128 roundtripów/32klipy PASS,163 pliki baz/kierunków
bez zmian. Resources/meta/gameplay niezmienione; pozy/VFX, przemiana/ciało,
portrety/ikony i import wciąż pozostają. Galeria pause/step i GIF-y w paczce.

Art follow-up 2026-10-15: komplet16 statycznych baz kierunkowych obu faz
ART_READY_FOR_REVIEW w `../output/bandit-leader-directions-v1` (16PNG/16Aseprite).
Pięć widoków autorskich + trzy dokładne odbicia na fazę; zaakceptowane S
zachowane byte-exact. Ciało58px/96canvas/32PPU/pivot(48,8), zamrożona96
z baz;16/16 walidacji PASS. Nowe widoki sprawdzone w źródle i native,
poprawiono zbyt frontalnySE i błędną stopę wP2N. Resources/meta/gameplay
niezmienione. Idle/chód i dalszy art/import jeszcze pozostają.

Art follow-up 2026-10-14: ART_READY_FOR_REVIEW dla dwóch statycznych baz S
obu faz w `../output/bandit-leader-base-v1` (PNG i Aseprite). Ludzki dowódca
z krótkim burgundowym płaszczem, skórzaną zbroją, szablą i dyszą ogniową.
FazaI kaptur/schowana broń; fazaII odsłonięta twarz/dobyta szabla, te same
buty i wyposażenie. Stara metoda image_gen + shipped referencje,
BOX/alfa150/wspólna96/Aseprite;2 bazy zwalidowane. Runtime wciąż placeholder;
kierunki/animacje/pozy/VFX/portrety/ikony/import pozostają.

READY_FOR_SMOKE dla etapu mechanik. Wdrożono obie fazy, sześć skilli, pasywy, AI, ukryte pułapki 3×3, przejście po pokonaniu własnej obstawy na początku następnej własnej tury i detonację. Obstawa: 2 Rzezimieszków + 1 Strzelec. Rozkaz przyspiesza następną turę o 50% AV; Ukryte Przebicie jest jednym ciosem ×5. Oddzielna wyprawa Lasu 10/20 i torba; 100 Danger losuje jeden slot Ent/Przywódca z utrzymaniem wyboru po save/load. Własne cztery unikaty, w tym działający już drop Płaszcza Przemytnika. Gruboskórność Kłusownika: odnowienie 5 własnych tur, dowolna liczba aktywacji. Krew przywódcy zastępuje roboczy Sygnet: 6258 złota, starożytny; w puli 11–20 ×1/75%.

Kompilacja PASS;317 odrębnych przypadków EditMode PASS po regresji i końcowym retestcie41/41. Powtarzalność24 assetów PASS,16 źródeł Enta i5 Króla bez zmian. Ręczne wiring nie jest wymagane. Manualny smoke/PlayMode/FPS NOT RUN. Brak dedykowanego artu Przywódcy: obie fazy mają zastępczą prezentację Rzezimieszka; skille/portrety/pozy/VFX/ciała/animacja przemiany i finalne ikony unikatów czekają na art pass. Handoff i dowody: LocalHandoffs/bandit-leader-gameplay-20261014/HANDOFF.md i validation.json. Bez nowych grafik, zmian generatora/rendererów lasu, formatu save, CU, commit/push.


### Sprzęt jako nagroda z odnóg — 2026-10-23

Regularni przeciwnicy Katakumb i Lasu nie losują broni ani pancerzy z tabel biomu; zachowane surowce, złoto, łup gatunkowy i galduryty. Osobne nagrody bossów/unikaty pozostają. W nowych regularnych wyprawach skrzynia/skrytka jest gwarantowana wyłącznie w pokoju celu odnogi (Optional), nigdy w pokoju tranzytowym lub zwykłym. Każdy cel ma jedną skrzynię. Bazowa oczekiwana suma broni/pancerzy ze wszystkich skrzyń całej wyprawy wynosi 5, dla obu biomów, obu przedziałów poziomów i każdej długości. To średnia, nie gwarancja pięciu przedmiotów w każdym lochu ani pięć na skrzynię. Szanse sprzętu są skalowane liczbą celów odnóg wszystkich pięter; dotychczasowy bonus szansy łupu nadal działa. Pozostałe pule skrzyń bez zmian. Pełny zapis zachowuje istniejące obiekty i wylosowaną zawartość; nowy układ i budżet wymagają nowej wyprawy.
