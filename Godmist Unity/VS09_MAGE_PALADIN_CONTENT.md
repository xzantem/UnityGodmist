# VS-09 — Alison i Reimond

Status: DONE. VS-08 zamknięta: f121f9c0. Smoke zaakceptowany; commit/push zlecone przez użytkownika.

## Zakres
Alison: Czarnoksiężnik + Mag Ognia; Reimond: Kapłan + Krzyżowiec. Po pierwsze cztery tiery i po pięć starterów. Dane: design/content_pass_1.xlsx; nie przywracać wcześniejszych wartości z rozmowy.

## Potwierdzone zasady (2026-09-22)
- Ognista Aura: bazowo 3 tury, zachowanie przestrzenne zgodne ze wspólnym systemem aur.
- Rytuał: regeneruje HP, łącznie 30% maksymalnego zdrowia przez 5 tur po poświęceniu 10% HP.
- Moc poza Granice: każda metoda odzysku może osiągnąć 115%/130% nominalnego maksimum many; nadmiar zostaje po walce. Nominalny MaxValue pozostaje podstawą kosztów procentowych.
- Agresywny Ogień: losowanie przy pierwszym ticku; osobno dla każdego wroga w zasięgu, kopie mają połowę siły rodzica i dziedziczą generację. Maksymalnie trzy pokolenia kopii.
- Choleryzm: próg 10% maksymalnego HP przeciwnika; wzmianka o 20% w Sangwinizmie jest nieaktualna.
- Pośpiech: oddaje część sumy Świętej Regeneracji wcześniej, odejmując ją z późniejszych ticków; nie dodaje leczenia.
- Ognista Ofiara: samopodpalenie kopiuje siłę i czas podpalenia właśnie nałożonego na przeciwnika.

## Wdrożony fundament
ResourcePool.RecoveryLimit oddziela limit odzysku od nominalnego maksimum; dodatkowa pojemność dotyczy tylko many. CharacterStatPipelineSystem wylicza ją z rangi node.mage.warlock.beyond_limits (tree.mage.warlock) i używa przy clampowaniu. Limit nie wymaga nowego pola zapisu — jest odtwarzany z inwestycji, bieżąca mana pozostaje w istniejącym zapisie.

Authoring wykonany przez Unity Editor API w izolowanej kopii projektu; zweryfikowane assety i wygenerowane meta przeniesione do głównego projektu. Alison i Reimond mają po 5 starterów i po dwa drzewka po 20 węzłów (tiery 1–4). Runtime obejmuje nowe efekty, interakcje, koszty, opisy i wybór celów. Nowe kampanie korzystają z nowych starterów; istniejące zapisy zachowują zapisane umiejętności.

## Weryfikacja końcowa
153/153 EditMode PASS (`.codex/vs09-acceptance.xml`): osobne testy contentu maga i paladyna oraz regresja statusów, surowców, wykonania/podglądu skilli, lifecycle, restore, wojownika i zwiadowcy. Poprawiono przeliczanie regeneracji brakującej many przy każdym ticku eksploracji i odświeżanie spowolnienia pola przy ruchu. Scoped diff nie dodaje trailing whitespace. Przegląd końcowy zakończony. Ręczny Play Mode / acceptance pozostaje do wykonania według UNITY_HANDOFF.md; brak commit/push.

## Obsługa wyboru wielu sojuszników
Masowe Poświęcenie: LPM zaznacza różnych sojuszników; po osiągnięciu limitu następuje wykonanie. Enter zatwierdza mniejszą liczbę celów. Esc/PPM anuluje bez kosztu. Zasięg, unikalność i limit są ponownie walidowane przed płatnością.

## Weryfikacja fundamentu many
43/43 PASS: zasoby, pipeline statystyk, rozwój poziomu oraz zapis/wczytanie (`.codex/vs09-mana-verified.xml`). Test round-trip JSON potwierdza zachowanie 130/100 MP i odtworzenie limitu z rangi. CharacterLevelGrowthSystem pozostawia clampowanie pipeline zamiast wcześniej obcinać nadmiar do nominalnego maksimum.

- Masowe Poświęcenie: każdy sojusznik wskazywany osobno przez gracza; bez automatycznego dobierania celów.

- Błogosławieństwo: jeden sojusznik, zasięg bezwzględny 7; w3 to pozostałość opisu.
- Pole Osądu: własne tury w kolejce AV, cztery aktywacje. Efekt Wydania Osądu następuje przy naturalnym końcu czasu. Nowe pole zastępuje poprzednie, także na tym samym polu.

## Poprawka targetowania po smoke — 2026-09-22
Zasięgi kołowe i pierścieniowe używają wspólnie floor(sqrt(dx² + dy²) + 0,5), zarówno dla minimum, jak i maksimum. Brak wyjątku dla promienia 1. Przykłady: (1,1) → 1, (2,1) → 2, (2,2) → 3, (3,1) → 3. Większe promienie również korzystają z zaokrąglania; LOS pozostaje niezależny. Highlighty: indeks kolumn wielopoziomowej siatki, indeks pełnych ścian dla LOS i ograniczenie przeszukiwania obszarów do lokalnego prostokąta zamiast całego piętra; brak skanowania dla RangeShape.None. Bez cache wyników LOS, więc dynamiczne przeszkody nadal sprawdzane na bieżąco.
96/96 EditMode PASS (`.codex/vs09-targeting-final.xml`): zasięg po przekątnych, porównanie geometrii obszarów/obrotów, aktualizacje indeksu pięter, ściany pełne/niskie i narożniki, wykonanie skilli oraz cztery klasy. Pomiar samego indeksu: 100 odczytów obszaru 17×17 na mapie 40 tys. pól około 13 ms; nie jest to pomiar całego renderowania UI.
Smoke: Cięcie/Wymiana po przekątnej, zablokowany narożnik nadal blokuje LOS; szybkość pojawienia się highlightów Kuli Ognia i Uzdrowienia oraz przesuwania kursora na docelowym piętrze. Pełny czas UI wymaga sprawdzenia w Play Mode. Bez commit/push.

Reguła radialna po zatwierdzeniu jednolitego zaokrąglania: 103/103 EditMode PASS (`.codex/vs09-radial.xml`), w tym minimum/maksimum pierścienia i granice różnych promieni.

## Odbiór i izolowany commit
2026-09-22: użytkownik zaakceptował smoke i zlecił commit/push. Izolowany zakres commita: 145/145 EditMode PASS (`.codex/vs09-index-final.xml`). Minimalna zależność Rytuału: przeniesienie ułamkowej reszty regeneracji między tickami wraz z save/load, bez pozostałych mechanik VS-18. Test eksploracji pozostaje lokalny w VS-18; commit testuje ten sam wspólny tick zasobu.
