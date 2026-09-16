# Dungeon Exploration v3 — acceptance

Data: 2026-09-16. Branch: milestone/dungeon-exploration-v3-threats-traversal-v1.
Stan: DONE na polecenie użytkownika 2026-09-16, po akceptacji smoke końcowej obwódki wejścia. Commit/push zatwierdzone. Poniższe sekcje zachowują wyniki i ograniczenia poszczególnych etapów. Profiler rzeczywistej rozgrywki nadal NOT RUN.

## Automatyczna weryfikacja

- PASS: 1127/1127 EditMode, 0 FAIL, 0 skipped. Aktualny kod oraz zawartość Assets/_Project skopiowane do osobnego projektu testowego. Wynik: `.codex/dungeon-v3-acceptance.xml`, log obok. Uruchomienie 2026-09-16 czasu lokalnego; XML zapisuje UTC 2026-09-15 23:50.
- PASS: dodatkowe 3/3 scenariusze pomiarowe FOV, `.codex/acceptance-fov-benchmark.xml`. To pomiary CPU, nie test płynności gry.
- PASS: diff --check dla zmienionych skryptów i testów. Nie normalizowano lokalnych dokumentów ani zmian assetów użytkownika.
- PASS: wcześniejsza kierunkowa weryfikacja wejścia LPM, 34/34; pełna regresja zawiera te testy.

## Review integracji

Przejrzano zmiany wejścia LPM: priorytet jednostek/UI, wspólny helper podejścia do NPC i wejścia, anulowanie oczekującej interakcji przez nowy ruch i zmianę lidera, wykorzystanie istniejącego selektora. Bez dodatkowego runtime właściciela mapy i bez zmian persistence lub prefabów.

Punktowo sprawdzono cache Fog/FOV, filtrowanie hover/menu przez widoczność i sprzątanie przy zmianie trybu, subskrypcje koordynatora oraz routing towarzyszy. Nie znaleziono potwierdzonego nowego blockera w sprawdzonych miejscach. To review krytycznych integracji, nie deklaracja ręcznego przejrzenia każdej linii całego milestone’u.

Testy pełnej regresji obejmują dotychczasowe zestawy generatora/kotew, RNG, selector/save-load, wysokości, seamless combat, kontakt/uczestnictwo, Fog, patroli, grup/ruchu, niszczenia, minimapy, UI i lifecycle. Zielone EditMode nie zastępuje scenariuszy wykonywanych w Play Mode.

## Pomiary CPU

Unity 6000.4.0f1, Editor batchmode, kopia projektu na tej samej maszynie. Główne Unity pozostawało otwarte; wyniki są orientacyjne i zależą od obciążenia.

| Scenariusz | Wynik |
| --- | --- |
| Fog: 400 pól | pojedyncza próbka 2,77 ms |
| Widoczność strukturalna: 3600 pól ze ścianami | indeks 13,88 ms; referencyjne przeglądanie ścian 37,41 ms |
| Minimap: 50 odświeżeń terenu, 40000 pól | 27,18 ms łącznie, ok. 0,54 ms/odświeżenie; bez uploadu GPU |
| Zasięg skilla: 10 obliczeń na gridzie 300×300 | ograniczony obszar 1,14 ms łącznie; pełny grid referencyjnie 231,97 ms |
| FOV: 1 wróg | mediana 0,75 ms, p95 1,19 ms |
| FOV: 4 wrogów | mediana 3,41 ms, p95 4,52 ms |
| FOV: 8 wrogów | mediana 6,57 ms, p95 8,13 ms |

FOV: syntetyczna mapa 60×60, ściany co 10 pól z otworami, zasięg 10, stożek 90°, 5 rozgrzewek i 50 próbek. Każda próbka liczy FOV wszystkich wrogów od nowa. Brak obiektów, renderowania i cache koordynatora; nie jest to koszt całej obsługi Ctrl. Harness znajdował się wyłącznie w kopii testowej; jego źródło zachowano w `.codex/AcceptanceFovBenchmark.cs`.

Wniosek: zakres skilla i CPU minimapy mają mały koszt w tych scenariuszach. Fog i FOV mogą skumulować koszt w jednej klatce. Koordynator posiada cache i interwał 0,1 s, ale sama częstotliwość nie usuwa szczytów czasu klatki. Nie przeprowadzono spekulacyjnej przebudowy ani zmiany zasad widzenia.

## Pozostała bramka — Play Mode / UI / profiler

NOT RUN w tym acceptance:
1. Wejście LPM z bliska i daleka, anulowanie przez ruch/zmianę lidera, brak kliknięcia przez UI; rozmowa z NPC po wspólnym helperze.
2. Jeden ciąg: selector → loch → rozdzielenie grup → dwie walki i posiłki → utrata/odzyskanie kontaktu → zakończenie → ponowne swobodne poruszanie i schody.
3. Zniszczenie przeszkody, łup/ciała, dźwignie i pułapki; powrót zwojem oraz save/load bez odtworzenia nagród i bez przelosowania układu/ofert.
4. UI pass: party rail, menu/inspect, minimapa, HP przeszkody, Ctrl i Fog przy zmianie poziomu, brak informacji o niewidocznych wrogach, zoom nad UI.
5. Profiler rzeczywistej sceny: porównać Ctrl wyłączone/włączone przy kilku widocznych patrolach, ruch lidera/ostre zakręty, zmianę pokoju. Zapisać Main Thread, GC Alloc i szczyty czasu klatki; oddzielić pierwsze użycie od rozgrzanego przebiegu. Dotychczasowe benchmarki nie zastępują tego pomiaru.

Sporadyczne zacięcia towarzysza na zakrętach i niewielki spadek FPS pod Ctrl były wcześniej zaakceptowane jako polish, ale nie uznajemy ich za naprawione. Finalne acceptance/UI i profiler: NOT RUN. Nowych potwierdzonych awarii: brak. Karta nie jest DONE.

## Uzgodnione granice

Scouting/sneak, nowe zagadki i pułapki, skoki oraz specjalne zasadzki: Dungeon v4. Rozszerzenie przestrzennych kotew o wysokość: przed wielopoziomowym content passem. Zaawansowane AI/targetowanie i inventory/hotbar: osobny zakres Combat v5. Większy wizualny polish podestów/balkonów później. Tych zakresów nie dodawano do obecnego diffu.

## Korekta po review użytkownika — 2026-09-16

Użytkownik potwierdził „reszta jest okej” i zlecił automatyczne otwarcie selektora oraz wskazówki przy wejściu. Dodano otwarcie raz na podejście, napis, hover i tooltip. Końcowy retest tej zmiany: 41/41 PASS (`.codex/entrance-cue-tests.xml`). Wynik 1127/1127 powyżej dotyczy stanu sprzed tej korekty. Smoke nowej korekty NOT RUN; wcześniejszy zakres zaakceptowany przez użytkownika. Pomiar profilerem nadal NOT RUN.

## Zamknięcie

Po smoke użytkownik zaakceptował obwódkę zamiast zmiany koloru całego wejścia i polecił zamknięcie karty, commit oraz push. Obwódka używa istniejącego CombatUnitVisualStateView i zapisanej referencji w Arungardzie. Finalny retest: 21/21 PASS (`.codex/entrance-outline-tests.xml`); kompilacja i self-review PASS. Nie uruchamiano ponownie pełnych 1127 testów po tych lokalnych poprawkach. Pozostałe lokalne zmiany użytkownika, w tym seed wejścia 2, nie należą do commitu. Pomiary rzeczywistych klatek i większy polish pozostają poza zakończonym smoke; nie oznaczono ich jako PASS.
