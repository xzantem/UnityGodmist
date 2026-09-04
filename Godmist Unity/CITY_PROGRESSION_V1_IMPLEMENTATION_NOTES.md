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
