# Combat v4 — content i scenariusze taktyczne

Status: DONE. Smoke użytkownika PASS; drugi pass zaakceptowany.

## Uruchomienie
Otwórz grę standardowo przez Bootstrap i wczytaj drużynę. W konsoli developerskiej wpisz `seed 404`, następnie jedną z komend:

| Scenariusz | Komenda | Co sprawdzić |
| --- | --- | --- |
| Korytarz grobowy (14×6) | `goto dungeon.catacombs.tactical.corridor` | Kolce i sarkofagi, obejście pułapek, wejście grupy w przewężenie |
| Sala filarów (12×12) | `goto dungeon.catacombs.tactical.pillars` | Grupy filarów, LOS, wybór drogi po obu stronach |
| Krypta L (12×12, ramiona szerokości 5) | `goto dungeon.catacombs.tactical.cryptl` | Załamanie LOS, gaz, trudny teren i bezpieczne obejście |
| Sala rytualna (18×16) | `goto dungeon.catacombs.tactical.ritual` | Pieczęcie, filary, gruz, duża arena i kamera |
| Komnata przejść (10×10) | `goto dungeon.catacombs.tactical.passages` | Drzwi między pokojami, dołączenie i wyjście członka drużyny, kolejna walka na tym samym piętrze |

Scenariusze to normalne lochy z krótką sekwencją pokoi, nie osobna scena walki. Od wejścia przejdź do pokoju encountera. Korytarz grobowy jest wąskim pokojem typu Normal, aby mógł zawierać normalny encounter; zwykłe łączniki typu Corridor pozostają bez zmiany.

Każdy scenariusz korzysta z tego samego szablonu pokoju, który dodano do DTC_Catacombs_V1. W normalnym lochu: `goto dungeon.catacombs.procedural.v1`. Nowe pokoje losują się obok istniejących. Scenariusze debugowe ograniczają pulę do jednego wariantu taktycznego plus start, final i łączniki. Poziom 1 i istniejące encountery; efekty pułapek niezmienione.

`seed` działa na NOWE generowanie. Powrót do już rozpoczętego lochu nie resetuje jego stanu. Aby dokładnie powtórzyć test, wczytaj zapis sprzed pierwszego wejścia do danego scenariusza albo rozpocznij nową kampanię; ustaw ponownie `seed 404`. Nie zapisuj nad właściwym zapisem kampanii podczas debugowania.

## Smoke
1. Uruchom pięć scenariuszy; sprawdź przejścia i możliwość ustawienia drużyny.
2. Sprawdź obejście pułapek bez obowiązkowych obrażeń, ruch wokół przeszkód, LOS i koszt trudnego terenu.
3. W komnacie przejść: wyprowadź i ponownie wprowadź postać do walki, następnie rozegraj kolejną walkę bez przeładowania piętra.
4. Obejrzyj salę rytualną przy różnych zoomach; sprawdź widoczność obiektów oraz highlightów.
5. W nowo wygenerowanych normalnych katakumbach sprawdź mieszankę starych i nowych pokoi. Stare zapisane lochy nie zmieniają geometrii.

## Techniczne
- Opcjonalne `ObjectDefinition` w kotwie semantycznej przypina istniejącą definicję z puli lochu. Null zachowuje stare losowanie. Walidator sprawdza typ; placement odrzuca niezgodną pulę zamiast zastępować obiekt.
- Nie zmieniono formatu save ani zasad walki. Wygenerowane instancje nadal zapisują istniejące identyfikatory definicji i stan.
- Zachowano lokalne wpisy użytkownika w katalogach DTC_Catacombs_V1 i GC_MainContentCatalog.

## Weryfikacja
- PASS: regresja generatora/walidatora/przeszkód/pułapek 55/55 (`%TEMP%/combat-content-regression.xml`).
- PASS: końcowy content 9/9 (`%TEMP%/combat-content-final.xml`): pięć wariantów × trzy seedy, powtarzalność obiektów, zgodność kotew, spójność bezpiecznych pól, wiele encounterów; trzy normalne proceduralne lochy; błędna pula obiektów.
- PASS: Views build — 0 warnings/errors. Pierwsza próba zbiegła się ze sprzątaniem Unity Temp i zgłosiła DirectoryNotFoundException; ponowienie po zakończeniu Editora PASS.
- PASS: self-review. NOT RUN: smoke użytkownika / wizualny pass w grze.

## Drugi pass rozmieszczenia
- Korytarz: dwie przesunięte grupy sarkofagów, kolce przy skrótach, mijanka i trudniejszy teren.
- Filary: krótkie, przesunięte bariery bliżej środka zamiast czterech narożnych punktów.
- Krypta L: sarkofagi przy zakręcie, gaz i teren rozłożone wzdłuż obu ramion.
- Komnata przejść: dwie grupy osłon i kolce przy krótszych obejściach.
- Sala rytualna: osłony i pieczęcie rozdzielone na dwie strefy; centrum pozostaje bez przeszkód i pułapek ze względu na preferowaną pozycję encountera.
- Do oglądania zmian potrzebne NOWE wygenerowanie lochu (zapis sprzed wejścia / nowa kampania). Istniejące instancje zachowują poprzedni układ.
- Smoke wizualny drugiego passu: NOT RUN.
- PASS: testy contentu po zmianie układów 9/9 (`%TEMP%/combat-content-pass2.xml`); drożność bezpiecznych pól zachowana. Self-review PASS.
