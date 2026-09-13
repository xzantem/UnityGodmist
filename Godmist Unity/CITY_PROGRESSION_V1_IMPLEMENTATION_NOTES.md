# City Progression and Specialist Services v1 — stan implementacji

Ten plik jest jawną listą rzeczy, których nie wolno traktować jako ukończonych
tylko dlatego, że są opisane w tooltipie lojalności.

## Karta 2 — zaimplementowane

- osobny poziom lojalności i skumulowane wydatki dla NPC,
- zapis i odczyt stanu lojalności,
- rabaty usług według progów z `GameDefines`,
- blokady handlu materiałami według poziomu lojalności,
- tooltip progresji lojalności,
- odświeżanie nagłówka i otwartego tooltipa po wydaniu golda.

## Efekty widoczne w tooltipie, ale jeszcze bez działania w gameplayu

### Kowal

- poziom 3 — tworzony ekwipunek ma o 5% lepsze statystyki główne,
- poziom 6 — nowy ekwipunek zaczyna z 25% ulepszenia,
- poziom 11 — niższy koszt ponownego ulepszania,
- poziom 14 — tworzony ekwipunek ma co najmniej 4 unikalności,
- poziom 15 — przekuwanie ekwipunku nie może obniżyć unikalności.

### Alchemik

- poziom 3 — słabszy efekt zatrucia alkoholem przy tworzeniu potionów/bomb,
- poziom 6 — szansa na napełnienie 2 ładunków,
- poziom 11 — stworzone potiony/bomby są o 5% silniejsze,
- poziom 14 — darmowe napełnianie i 33% szansy na brak zużycia alkoholu/proszku,
- poziom 15 — możliwość użycia jednego dodatkowego katalizatora.

Mechanika roli Alchemika, jej usług i powyższych efektów nie jest jeszcze
zaimplementowana.

### Enchanter / Rune-forger

- poziom 3 — galduryty mają o 5% silniejsze efekty,
- poziom 6 — lepsza szansa powodzenia dodawania/usuwania galdurytów,
- poziom 8 — zidentyfikowane galduryty otrzymują dodatkowy efekt,
- poziom 11 — efekty galdurytów są o 20% silniejsze,
- poziom 14 — tworzone galduryty mają co najmniej 4 unikalności,
- poziom 15 — pierwszy osadzony galduryt nie zużywa pojemności socketu.

Mechanika roli Enchantera/Rune-forgera, galdurytów i tych efektów nie jest
jeszcze zaimplementowana.

## Ważne rozróżnienie

Obecne etykiety nagród są informacją projektową w UI, a nie dowodem działania
efektu. Przy implementacji kolejnych kart należy usunąć ten status dopiero po
podłączeniu efektu do właściwego systemu runtime, UI, save/load i testów.

## Karta 3 — zaimplementowane

- osobny, trwały portfel golda każdego NPC: saldo, suma otrzymanego i wydanego golda,
- początkowe saldo równe jednej wypłacie; domyślna baza wypłaty NPC to `250`,
- wypłata skaluje się data-driven z poziomem kampanii: `baseAmount * 4^((level - 1) * 0.1)`,
- postęp wypłaty wynosi `40% * (ukończone piętra / liczba pięter)`; przy wyjściu z nieukończonego lochu liczone są tylko nowo ukończone piętra, a nadwyżka po wypłacie przepada,
- wypłata trafia do wszystkich zarejestrowanych NPC i używa najwyższego poziomu aktywnej drużyny,
- zakup od NPC zwiększa jego saldo, ale nie nalicza lojalności przy zakupie z buybacku,
- sprzedaż bez pełnego pokrycia wymaga potwierdzenia i wypłaca aktualne saldo NPC; cena przedmiotu w buybacku pozostaje normalną ceną zakupu,
- saldo, historia transakcji, postęp wypłaty i stan naliczonych pięter są zapisywane i odtwarzane,
- aktywne okno handlu pokazuje saldo NPC oraz przyczynę braku możliwości sprzedaży.

## Karta 3 — rzeczy nadal poza zakresem / do smoke testu

- potwierdzenie częściowej sprzedaży jest runtime'owym UI w aktywnym `TradeServiceWindowView`; nie zmieniano prefabów ani scen,
- część efektów lojalności Kowala, Alchemika i Enchantera/Rune-forgera nadal jest tylko opisana w tooltipie — lista powyżej pozostaje źródłem prawdy,
- nie dodano jeszcze ról i usług Alchemika, craftingu biżuterii ani systemu galdurytów/run; należą do kolejnych kart milestone'u,
- pełny build wygenerowanego `Assembly-CSharp.csproj` jest blokowany przez istniejący, nieobecny plik `Assets/_Project/Tests/EditMode/CombatDamageSystemTests.cs`; osobne assembly źródłowe i testowe budują się poprawnie.
