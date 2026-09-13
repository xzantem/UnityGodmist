# Combat v4 — teren taktyczny i koszty przemieszczania v1

Status: DONE. Końcowy smoke użytkownika PASS; poprzednie wpisy NOT RUN dokumentują stan wcześniejszych etapów.

## Zakres

- DifficultTerrainCells w szablonie pokoju, obracane przez istniejący DungeonFloorMapBuilder. Ta sama informacja przechodzi przez DungeonCombatContextFactory i DungeonBattleGridFactory.
- +1 AP za wejście na trudne pole, sumowane z dopłatą +2 za niski murek. Nie zmienia numeru kolejnego kroku. Ruch specjalny zachowuje dotychczasowy koszt i blokady.
- Gracz: zagrożenie → koszt AP → liczba zakrętów; dopuszcza śmiertelną drogę, jeżeli brak bezpiecznej. AI: zagrożenie → AP, odrzuca przewidywane powalenie. Planowanie bez mutacji stanu pułapek.
- Wspólny ExplorationCharacterView spowalnia eksploracyjny ruch o połowę na trudnym podłożu. Drużyna i przeciwnicy korzystają z tego samego mechanizmu. Animacja ruchu bojowego nie otrzymuje tego spowolnienia.
- Przejście przez niski murek: interpolacja liniowa przez 0,6 s, blokada kolejnych poleceń ruchu, przywrócenie fizyki i sterowania. Kontrola walkability i zajętego pola docelowego. Rozpoczęcie/dołączenie do walki czeka na zakończenie przejścia. Pełna ściana, balustrada i pustka zachowują blokady.
- Zapisane kolory podłoża i kosztownych odcinków; per-cell wypełnienie trudnego terenu. Czerwone ostrzeżenie pułapki ma pierwszeństwo.

## Testy

PASS: 108/108 EditMode w `%TEMP%/terrain-final.xml`, Unity 6000.4.0f1. Zestawy TacticalTerrainTests, TacticalObstacleTests, DungeonTrapCombatTests, SimpleCombatAiSystemTests, DungeonBattleGridFactoryTests, SeamlessDungeonCombatTests.
PASS: walidacja szablonu i zbudowanie 6 trudnych pól we wszystkich obrotach 0/90/180/270; `%TEMP%/terrain-demo.log`, TERRAIN_DEMO_PASS.
PASS: git diff --check dla kodu.

Nowe testy: sumowanie kosztu terenu i murka bez wpływu na następny krok; bezpieczne obejście tańszej pułapki; dostępność śmiertelnej jedynej drogi gracza; minimalizacja zakrętów; brak blokady przemieszczeń specjalnych przez teren; spowolnienie eksploracyjnego widoku; blokada wejścia na zajęty cel murka i blokada inputu podczas przejścia.

Wcześniejsze niepowodzenia: poprawiona składnia asercji NUnit; regresja blokująca ruch borrowed combat view została wykryta przez istniejący test i naprawiona przez ograniczenie logiki terenu do eksploracji. Końcowy retest PASS.

## Serialized state i self-review

Osobne demo: DD_Catacombs_Terrain_Demo, DTC_Terrain_Demo, DRT_CombatV4_Terrain_Demo. GC_MainContentCatalog ma dodatkowy wpis demo ponad zachowane lokalne wpisy użytkownika. DungeonSandbox: tylko dwa nowe pola kolorów. Produkcyjne definicje lochu i katalog DTC_Catacombs_V1 bez zmian tej karty. Brak nowych pól save i zapisu w walce; stare szablony mają pusty teren. Tymczasowy Editor helper usunięty po walidacji.

Sprawdzono: wspólnego ownera ruchu, brak osobnego kontrolera animacji, bezpieczny powrót sterowania po murku, oddzielenie prezentacji bojowej od spowolnienia eksploracji, sumowanie AP i kolejność wyboru trasy. Brak modyfikacji niezwiązanych zmian użytkownika.

NOT RUN: interaktywny smoke wyglądu podłoża/highlightów, pościgu przez murki, zmiany aktywnej postaci, powtórnych walk i wydajności na dużych arenach. Kroki w lokalnym UNITY_HANDOFF.md. Docelowe grafiki terenu pozostają do content passu; demo używa koloru istniejącej posadzki.

PASS: dodatkowy końcowy test najtańszej bezpiecznej trasy z terenem, razem TacticalTerrainTests 8/8 (`%TEMP%/terrain-cost.xml`). Łącznie pokryto 109 różnych testów w regresji i uzupełnieniu.

Poprawka po smoke: RebuildHighlights otrzymuje siatkę bieżącej sesji bezpośrednio, zamiast odczytywać cachedSession przed inicjalizacją cache. Usuwa NullReferenceException na pierwszym highlight i użycie starej siatki przy zmianie sesji. Kompilacja Views.csproj PASS (0 błędów/ostrzeżeń). Ponowny smoke pierwszego wejścia w walkę i przełączania sesji: NOT RUN.

## Poprawka prezentacji po uwagach użytkownika

Subtelne wypełnienie trudnych pól, etykiety +1 AP z zapisanego prefabu oraz roboczy kafelek uszkodzonej posadzki. Całkowity koszt ścieżki bez zmiany położenia. Runtime wyłącznie dobiera kafelek do danych pola i instancjonuje/usuwa etykiety zasięgu z istniejącego prefabu.
PASS: Editor zapisał prefab i Tile, TERRAIN_PRESENTATION_SETUP_PASS; odczyt GUID/fileID i podpięć sceny; dotnet build Views.csproj, 0 błędów i ostrzeżeń; diff check kodu. Tymczasowy helper usunięty. Pierwszy helper trafił do asmdef bez referencji TMP, przeniesienie do standardowego Assets/Editor rozwiązało kompilację.
NOT RUN: wizualny smoke etykiet/tekstury i ich czytelności w grze po tej poprawce. Nie ponawiano suite gameplay dla zmiany prezentacyjnej.

## Gradient kosztów po kolejnej uwadze użytkownika

Zastąpiono osobny kolor trudnego terenu przyciemnianiem bazowego koloru według dopłaty AP. Pola używają dopłaty podłoża, odcinki ścieżki — podłoża + murka. Alpha bez zmiany; czerwony danger ma pierwszeństwo. Usunięto etykiety +1 AP, prefab i wiring. Bez tooltipu. PASS: build Views.csproj 0 warnings/errors, brak pozostałych odwołań etykiet, diff check. NOT RUN: smoke wizualny tego passu.

Aktualny gradient (zastępuje przyciemnianie): bazowy żółty → brązowo-rudy RGBA(0.65, 0.27, 0.09, 1), liniowo według dopłaty AP / 3, z zachowaniem alpha. +0 pozostaje bazowe; +3 i więcej osiąga kolor końcowy. Pola oraz odcinki ścieżki używają wspólnej reguły; czerwone ostrzeżenie pułapek bez zmian. Parametry zapisane w DungeonSandbox przy zamkniętym Unity. PASS: odczyt zapisanych parametrów; kompilacja kodu PASS z poprzedniego kroku. Wizualny smoke nowego gradientu: NOT RUN.

Poprawka widoczności ścieżki: DungeonSandbox Path Sorting Order = 7 (pułapki 6, postacie eksploracji 10). Dotyczy linii i jej kosztu; fill/outline bez zmian. Kompilacja Views.csproj PASS, 0 błędów/ostrzeżeń; smoke NOT RUN. Zgłoszenie powrotu przez murek pozostaje w diagnozie — potrzebne doprecyzowanie trybu i sterowania.

Poprawka powrotu przez murek: po doprecyzowaniu użytkownika usunięto sprawdzanie zajęcia/rezerwacji pola przez postacie eksploracji. Towarzysz po drugiej stronie nie blokuje przejścia. Sprawdzanie blokujących obiektów oraz walkability pozostaje; reguły zajętości walki nie zostały zmienione. Wcześniejszy opis blokady przez postać jest nieaktualny. Kompilacja Views.csproj PASS. Test rozszerzono o natychmiastowy powrót bez opuszczania pola lądowania.

Retest poprawki murka: TacticalTerrainTests 8/8 PASS (`%TEMP%/terrain-return.xml`), w tym powrót bez opuszczenia pola. Smoke gracza z towarzyszem: NOT RUN.

Gradient pól: obrys zasięgu otrzymuje teraz kolor według dopłaty podłoża pola, tak samo jak istniejące wypełnienie. Obejmuje jednostkę aktywną i oglądaną. Linia ścieżki bez zmian. Kompilacja Views.csproj PASS, 0 błędów/ostrzeżeń. Smoke widoczności obrysów: NOT RUN.

## Aktualny styl: osobne kwadraty i delikatny zasięg

Zapisane w DungeonSandbox: zwykłe wypełnienie alpha 0.025; trudny teren alpha 0.1 z dotychczasowym gradientem kosztów. Cienki obrys zewnętrzny 0.025, wewnętrzne krawędzie pojedynczych kwadratów 0.012 / alpha 0.18. Wspólna wewnętrzna krawędź jest rysowana tylko raz. Kontur i ścieżka mają delikatną szerszą warstwę koloru (halo alpha 0.12, szerokość ×3). Ścieżka cieńsza: 0.035, nadal nad pułapkami. Aktywna jednostka ma jaśniejszą ramkę; cel ruchu ramkę i fill alpha 0.1. Brak napisów +1 AP i tooltipu; całkowity koszt bez zmiany położenia.

Nowe krawędzie i halo należą do istniejących kolekcji renderera i są sprzątane razem z warstwą; wypełnienie celu jest czyszczone przy zmianie preview. PASS: kompilacja Views.csproj, 0 warnings/errors. Wizualny smoke: NOT RUN. Sprawdź czytelność siatki przy oddaleniu, widoczność tekstur/pułapek oraz usuwanie wszystkich oznaczeń po przełączeniu do eksploracji.

Retest stylu siatki: TacticalTerrainTests + SeamlessDungeonCombatTests, 57/57 PASS (`%TEMP%/terrain-grid-style.xml`).
