# CP2-07 — T5 Skrytobójcy i Tancerza Ostrzy

Status: READY_FOR_SMOKE — wszystkie trzy decyzje użytkownika zamknięte 2026-11-02.
Gwarantowany krytyk Przewrotu obejmuje tylko jedno trafienie. Odzyskana Siła
nalicza ładunek za każdy sztylet. Jad pulsuje przy użyciu oraz na początku
dwóch następnych tur właściciela.

CP2-06: smoke użytkownika PASS, commit `3f7db1797`, wypchnięty na
`codex/v0.2.0-content-pass-2`. Wcześniejsze zmiany CP2-03/04 i pozostałe
zmiany użytkownika zachowane poza commitem.

## Źródło i granice

Odczytano aktualny `../design/content_pass_2.xlsx`, arkusz Drzewka, wiersze
46–56. SHA256: `83cb291dd139a4e34497d2d752c8289a9545442d3c714537502c6909566618b7`.
Surowy odczyt: `../LocalHandoffs/cp2-07-scout-t5-20261102/workbook.json`.
Nie zmieniono XLSX.

Zakres: dziesięć węzłów T5 dwóch istniejących drzew, trzy aktywne skille,
dedykowane ikony i redakcyjne opisy każdej rangi, preview/AI, integracja
grafu i testy. Bez osobistych traitów Jerzego i bez wdrażania Strażnika.
T5 wymaga 20 SP wydanych w odpowiednim drzewie. Nowe ID pozostają zgodne
z rejestrem CP2-02; nie nadawać ponownie ID na podstawie nowych nazw.

| Drzewo | Slot | Nazwa | Rangi | Wymaganie runtime | Aktywny skill |
| --- | --- | --- | ---: | --- | --- |
| Skrytobójca | T5:1 | Kondycja | 1 | t5_2 | — |
| Skrytobójca | T5:2 | Przewrót w Przód | 1 | — | skill.scout.assassin.t5_2 |
| Skrytobójca | T5:3 | Przewidywanie | 2 | — | — |
| Skrytobójca | T5:4 | Cios Poniżej Pasa | 1 | — | — |
| Skrytobójca | T5:5 | Cięcie bez Honoru | 1 | pommel_strike | — |
| Tancerz Ostrzy | T5:1 | Jad w Butelce | 1 | — | skill.scout.blade_dancer.t5_1 |
| Tancerz Ostrzy | T5:2 | Odzyskana Siła | 3 | efficient_recovery | — |
| Tancerz Ostrzy | T5:3 | Sokoli Wzrok | 2 | accurate_throw | — |
| Tancerz Ostrzy | T5:4 | Klątwa Schorowanych | 1 | poison_master | skill.scout.blade_dancer.t5_4 |
| Tancerz Ostrzy | T5:5 | Worek na Sztylety | 1 | spare_daggers | — |

ID węzłów: `node.scout.assassin.t5_1` … `t5_5` i
`node.scout.blade_dancer.t5_1` … `t5_5`. Nie zmieniać istniejących nazw/ID
T1–T4: arkusz podaje współrzędne prerequisite, a kod używa trwałych ID.

## Zamknięta decyzja: Przewrót w Przód

Użytkownik wybrał: **krytyk tylko na jedno trafienie; pozostałe proponowane
zasady przyjęte**.

- 80 PP i 80% PA; Kondycja zmienia koszt PA na 40%.
- Wybór wolnego pola w odległości 1–2 jak Krok w Cień, z istniejącą walidacją
  przeszkód i blokadą przy unieruchomieniu.
- Następne użycie ataku otrzymuje jeden gwarantowany krytyk; po jego
  wykorzystaniu pozostałe trafienia mają zwykłą szansę krytyczną. Niewykorzystana
  premia przepada także po pudle tego użycia ataku.
- Gwarantowany unik zatrzymuje jedno najbliższe trafienie bezpośredniego
  ataku. Nie zatrzymuje DoT.
- Premie trwają do zużycia lub końca walki; ponowne użycie odświeża je
  bez kumulacji. Pasywny kontratak nie powinien przypadkowo przejąć premii
  przeznaczonej na użycie następnej umiejętności.

Nie wolno rozszerzyć korekty użytkownika na gwarantowany krytyk całego
wielohitu. Preview powinno rozdzielać pierwsze kwalifikujące trafienie
od kolejnych, a stan musi być kopiowany do odłączonej symulacji AI.

## Pozostałe dwie decyzje — zatwierdzone przez użytkownika

### Odzyskana Siła

Przyjęto: jeden ładunek za każdy faktycznie odzyskany
własny sztylet, niezależnie od źródła odzysku (ziemia, Linka z Hakiem,
wyciągnięcie z przeciwnika). Maksymalnie trzy ładunki; +8/16/25% każdy,
czyli maksymalnie +24/48/75% obrażeń całego następnego użycia ataku.
Zużycie także przy pudle, bez wzmacniania DoT, bez przenoszenia między walkami.
Użytkownik wybrał naliczanie za każdy sztylet.

### Jad w Butelce

Przyjęto: 50 PP, 50% PA, trzy pulsy — od razu po użyciu
oraz na początku dwóch kolejnych tur właściciela. Każdy żywy wróg w odległości
Manhattan ≤2 dostaje osobną próbę zatrucia z bazową szansą 70%, z uwzględnieniem
odporności. Trucizna 40% średniego ataku na turę przez trzy tury, wzmacniana
istniejącymi pasywami trucizny. Wyjście z obszaru i wygaśnięcie aury nie
usuwają już nałożonego zatrucia. Użytkownik zatwierdził tę propozycję.

Nie ma dalszych blokujących pytań projektowych.

## Pozostałe kontrakty wynikające z arkusza i istniejącej gry

- Przewidywanie: 20/40% szansy zamiany chybienia w trafienie; jedna próba
  na chybienie, bez ponownego wykonania ataku. Gwarantowany unik zachowuje
  pierwszeństwo i nie jest zamieniany w trafienie.
- Cios Poniżej Pasa: po bezpośrednim krytycznym trafieniu osobna próba
  ogłuszenia 30% na jedną turę. Standardowa odporność/tolerancja nadal działa.
  Wielohit ma osobne krytyki i próby statusu; DoT nie jest krytykiem ataku.
- Cięcie bez Honoru: podwaja bazową szansę Podcięcia Gardła tylko wobec celu
  już ogłuszonego przy rozstrzyganiu ataku. Zachować próg pięciokrotności
  obrażeń oraz istniejący wyjątek bossa (podwójne obrażenia zamiast zabicia).
  Nie wykonywać ponownego losowania tego samego proca.
- Sokoli Wzrok: +2/4 zasięgu umiejętności dystansowych, bez rozszerzania
  promienia aury, dystansu Przewrotu i zasięgu walki wręcz. Uwzględnić
  wspólną walidację, preview i AI, bez mutacji bazowego SkillData.
- Klątwa Schorowanych: 55 PP, 70% PA; celność 82; zasięg 4, w zwarciu 1;
  losowe obrażenia fizyczne na dystansie, 60% w zwarciu. +40% przeciw celowi
  zatrutemu przed trafieniem; po trafieniu przedłuża istniejące zatrucie
  o jedną turę. Jeden sztylet na dystansie, bez kosztu sztyletu w zwarciu.
  Sama umiejętność nie tworzy nowej trucizny na zdrowym celu.
- Worek na Sztylety: +2 maksymalnej pojemności, addytywnie z Zapasowymi
  Sztyletami. Wielokrotna inicjalizacja walki nie może dodawać premii ponownie.

## Punkty integracji potwierdzone w kodzie

- `ScoutCombatSystem.cs`: odzysk Linką, wyciąganie wbitego sztyletu, knife
  rewards, trwałe ID drzew. Rozróżnić istniejące stałe +8 PP za akcję odzysku
  od nowego licznika Odzyskanej Siły; nie zmieniać bazowej nagrody.
- `ScoutCombatSystem.Content.cs`: obrażenia, krytyk, turn hooks,
  ResolveThroatCut i AfterSkillHit. Rozszerzyć spójny owner skauta.
- `CombatDamageSystem`: istnieją pojedyncze guaranteed critical/dodge
  i rozdzielenie zwykłego chybienia od gwarantowanego uniku. Nowe warunki
  muszą przechodzić przez tę samą ścieżkę co rzeczywiste obrażenia.
- `SkillExecutionSystem`: granica użycia skilla, wielohit, koszt sztyletów,
  teleport, ApplyUniqueBuff, walidacja celu. To granica zużywania banków.
- `CombatAuraSystem` na końcu `SkillExecutionSystem.cs`: obecna aura
  odświeża statusy przy zmianach położenia i usuwa większość statusów
  po opuszczeniu zasięgu. Nie używać jej bezpośrednio dla proponowanej
  pulsującej trucizny: powodowałaby dodatkowe losowania i kasowanie poison.
  Zachować zachowanie istniejących aur, zwłaszcza Ognistej Aury Maga.
- `CombatMovementSystem.InitializeBattle`: bazowa i dodatkowa pojemność
  sztyletów; zachować idempotencję istniejącego ExtraKnifeCapacity.
- `PassiveEffectCombatExecutionSystem.Parameters`: wspólny ResolveMode
  dla zasięgów oraz parametrów kosztu PA. Kondycja i Sokoli Wzrok muszą
  dawać ten sam wynik w UI, walidacji wykonania i planowaniu AI.
- `ScoutTargetState`: lokalny stan walki. Nowe liczniki nie wymagają
  rozszerzania save; zapis w walce pozostaje zablokowany. Kopia AI musi
  kopiować je jawnie i uwzględniać w kluczu planowania.
- `VS07ScoutSetup.Trees`: istniejące 40 węzłów i trzy stabilne mapowania
  prerequisite T4; authoring addytywny na wzór CP2-05, nie pełny rebuild.
- Prezentacja: SkillTreeTooltipCopy, SkillTreeTooltipPresentation,
  ReviewedSkills, HeroCombatStatusText oraz katalog póz Jerzego.

## Kolejność implementacji po zamknięciu decyzji

1. Lokalny stan, pure queries premii, granica castu i poprawne zużywanie
   krytyka/uniku/ładunków. Następnie miss conversion, stun i throat proc.
2. Odzysk/pulsująca trucizna, zasięg, pojemność i Klątwa Schorowanych.
3. Dziesięć dedykowanych ikon: pojedyncze image_gen, zatwierdzony full-canvas
   nearest 32×32 przez Aseprite, źródła/provenance/roundtrip jak CP2-05/06.
4. Addytywny Editor API authoring: dwa istniejące drzewa, trzy nowe SkillData,
   referencje katalogu. Nie zmieniać YAML, GUID ani meta ręcznie.
5. Opisy rang, rozwijane szczegóły i widoczne statusy; istniejące odpowiednie
   pozy/VFX lub jawna informacja o brakującym assetcie, bez martwych ID.
6. Testy mechanik, preview, AI, grafu, UI, zachowania 40 dawnych węzłów,
   idempotencji authoringu oraz legacy SP/rang. Handoff i READY_FOR_SMOKE.

## Minimalna macierz testów

- Przewrót: legalny/nielegalny cel, koszt 80/40% PA, jeden krytyk w wielohicie,
  utrata premii przy samych pudłach, pojedynczy unik, DoT bez konsumpcji,
  ponowne użycie bez stackowania i reset po walce.
- Przewidywanie: obie rangi, brak talentu, zwykłe pudło vs guaranteed dodge,
  dokładnie jedno rozstrzygnięcie kontaktu i zgodny expected damage.
- Cios/Podcięcie: kryt i niekryt, odporność na stun, żywy vs martwy cel,
  ogłuszony vs nieogłuszony, boss i pięciokrotny próg egzekucji.
- Odzysk: kilka noży, pełna pojemność, obcy nóż, ziemia/Linka/wyjęcie,
  cap trzech, atak wielocelowy/wielohit/pudło i niezależność od +8 PP.
- Aura: dokładna liczba pulsów, granica zasięgu, sojusznik/śmierć źródła,
  odporność, bonusy trucizny, wyjście z obszaru, wygaśnięcie i ponowny cast.
- Klątwa: oba dystanse, obrażenia +40% tylko przy poison, trafienie/pudło,
  przedłużenie bez tworzenia poison i prawidłowy koszt/free-melee sztyletu.
- Pojemność/zasięg: rank 0/1/2, idempotencja inicjalizacji, niemutowanie
  definicji, brak rozszerzania wręcz/aury/teleportu.
- AI: odłączony stan i brak konsumowania banków/RNG przez preview.
- UI: dziesięć nowych kart i ikon, zakresy rang i SP, zakup po 20 SP,
  wszystkie prerequisite i zachowanie wcześniejszych inwestycji.

Weryfikacja Unity wyłącznie ukrytym batchem w izolowanej kopii; bez
przejmowania okien i klawiatury użytkownika. Testy CP2-07 wykonano w ukrytej kopii batch; szczegółowe wyniki poniżej.


## Wdrożenie

`ScoutCombatSystem.Tier5.cs` jest właścicielem reguł T5. AttackScope przechwytuje
ładunki i premia krytyka raz na użycie skilla, zachowując niezależność kopii
pasywnych. Krytyk Przewrotu jest konsumowany przy pierwszym trafieniu, a jeżeli
nie wystąpi — znika wraz z końcem użycia ataku. Krytyk i unik są stanami
combat-scoped, bez sztucznego limitu jednej tury.

Odzysk korzysta ze wspólnego RewardKnifeRecovery, zachowując stały zwrot
8 PP za akcję. Ładunki naliczane są od rzeczywistej liczby odzyskanych noży,
bez podwójnego naliczania przy podnoszeniu z ziemi. Trucizna używa istniejącego
systemu statusów i wspólnego przeliczenia siły wszystkich zatruć; osobny licznik
odpowiada tylko za trzy pulsy. Usunięcie aury zatrzymuje przyszłe pulsy, a
nałożona trucizna trwa niezależnie. Przedłużenie dodaje jeden tick obrażeń.

Podglądy są odczytowe: bank nie jest zużywany, a oczekiwane obrażenia
wielohitu liczą jedno gwarantowane trafienie krytyczne i zwykłą szansę kolejnych.
Stan AI kopiuje liczniki i uwzględnia je w kluczu planowania. Nie dodano
żadnego pola save; transient state nie jest serializowany między walkami.

Editor API dopisuje 10 węzłów do dwóch istniejących drzew i trzy SkillData
do głównego katalogu. Zachowano 40 starszych rekordów węzłów, class assets
i stable ID istniejących efektów. Nowe efekty mają jawne stableEffectId,
a zależności Przewrotu od przemieszczenia wskazują stabilne ID `move`.
Ponowny authoring jest bajtowo identyczny. Historyczny pełny Scout setup
przywraca T5, jeżeli dostępna jest paczka authoringu tej karty.

Dedykowane ikony: Tools/Art/ScoutT5Icons20261102, 10 osobnych generacji,
full-canvas nearest 32×32, natywne PNG i edytowalne Aseprite. Dokładne
próbkowanie RGBA i roundtrip Aseprite 10/10 PASS. Sprawdzono wizualnie
review.png w rozmiarze natywnym oraz powiększeniu. Bez nowych animacji postaci:
Przewrót używa istniejącej pozy shadow Jerzego, Jad — tonic, Klątwa — throw/melee.
To reuse istniejących póz, nie dedykowana animacja przewrotu. Nowe ID są
podpięte do katalogu póz i kontaktów VFX; stany mają ikony i krótkie liczniki.

Testy i ich surowe wyniki: ../LocalHandoffs/cp2-07-scout-t5-20261102/.
Pierwszy focused runtime 69/69 PASS. Authoring/idempotencja/40 dawnych
węzłów/prerequisites/3 skille — PASS. Szeroka regresja: 621/623 PASS;
dwa nieaktualne oczekiwania testów poprawione (koszt odzysku nadal 20 PP
i przewijanie pięciu rzędów). Retest mechanik oraz tego testu UI: 138/138 PASS.
Osobny końcowy retest podglądów obejmuje przypadek unikania krytyków,
aby wielohit nie obiecywał trzech gwarantowanych krytyków zamiast jednego.
Raport końcowy i hashe: Tools/ContentPass2/scout-t5-validation.json.
Brak builda gracza, pomiarów FPS i ręcznego smoke w oknie gry. Bez commit/push.

Końcowa kontrola tekstów i mechanik: 92/92 PASS. Łącznie 642 różne przypadki
funkcjonalne mają końcowy wynik PASS. Dwa opcjonalne eksportery screenshotów
pozostają nieweryfikowane w trybie bez grafiki; ich błędy i ograniczenia są
zachowane w raporcie, nie są zaliczane do PASS. Uzupełniono również klikalne
wartości w opisach Przewidywania i Ciosu Rękojeścią Zbrojnego, wykryte przez
wspólny test pełnego pokrycia szczegółami wartości. Gameplay CP2-06 bez zmian.


## Korekta palet — 2026-11-02

Odtworzono źródłowe prompty i rozdzielono palety pasywek od aktywnych skilli.
Szczegóły: Tools/Art/ICON_COLOR_CONVENTIONS.md. Cztery aktywne skille
Zbrojnego mają teraz bordowe tło aktywnego Wojownika; złoto ochrony,
błękit kontroli i zieleń odnowienia pozostają w efektach. T5 Dowódcy
i Awanturnika porównano ze starszymi ikonami — były zgodne, bez zmian.
U skauta poprawiono wszystkie 10 nowych ikon: 4 śliwkowe pasywki Skrytobójcy,
3 pasywki teal Tancerza i 3 aktywne z paletą odpowiednią dla ich działania.
Korekta obejmuje 14 produkcyjnych PNG; zachowano pliki meta/GUID, węzły,
referencje aktywnych skilli, parametry importerów i cały kod gameplayu.
Źródła przed zmianą i prompty edycji zachowano. Odtworzono natywne PNG32
i Aseprite: pełne próbkowanie nearest i roundtrip obu paczek 35/35 PASS.
Porównanie przed/po/wzorzec obejrzano w32×32 i powiększeniu. Raport:
Tools/Art/IconPaletteFix20261102/validation.json. Pozostaje ręczny odbiór
kolorów w grze; wcześniejszy smoke mechanik nie jest powtarzany ani cofany.
Bez commit/push tej korekty.
