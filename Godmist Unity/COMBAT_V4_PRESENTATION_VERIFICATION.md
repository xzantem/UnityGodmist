# Combat v4 — prezentacja areny i informacje taktyczne v1

Stan: READY_FOR_SMOKE, 2026-09-14. Branch codex/combat-v4-arena-obstacles-transition-v1.

## Zakres

Statusy eksploracyjne przy portretach (3 + overflow, czas wyłącznie w tooltipie), Inspect UIW bez budowania statycznego UI w runtime, ostre krawędzie highlightów i opóźnione informacje o taktycznych właściwościach pola. Istniejący runtime walki i reprezentacja mapy pozostają właścicielami danych.

Przyczyna białego Inspecta: runtime wyłączał Mask przy pozostawionym białym Image i dodawał RectMask2D. Viewport ma teraz zapisany RectMask2D i przezroczysty Image, a wiersze używają zapisanych szablonów UIW.

## Wyniki

- PASS: dotnet build Views.csproj — 0 warnings / 0 errors.
- PASS: 74/74 EditMode: CombatPresentationV4Tests, CombatStatusPresentationTests, CombatGridFeedbackGeometryTests, TacticalTerrainTests, TacticalObstacleTests, SeamlessDungeonCombatTests.
- PASS: po poprawce hoverów powtórzono CombatPresentationV4Tests i CombatStatusPresentationTests — 9/9.
- PASS: render rzeczywistego prefabu Inspect w Unity z jednostką testową; poprawny kontrast, dwie kolumny, sekcje efektów i umiejętności.
- PASS: self-review serialized references, usunięcie tymczasowego narzędzia Editor; diff sceny ograniczony do potrzebnych komponentów i parametrów.
- NOT RUN: interaktywny smoke użytkownika, długie listy efektów/umiejętności i różne rozdzielczości/zoomy w grze.

## Ograniczenia i handoff

Ostre kontury to pierwszy pixel-art pass, nie nowe ręcznie rysowane sprite’y. Docelową czytelność gradientu i tooltipów trzeba potwierdzić w grze. UNITY_HANDOFF.md zawiera kroki smoke. Karta pozostaje In Progress. Bez commita/pusha; niezwiązane zmiany użytkownika zachowane.

## Rozszerzony HUD — drugi pass

- PASS: Views.csproj, 0 ostrzeżeń/błędów.
- PASS: 75/75 EditMode (CombatHudPassTests, CombatPresentationV4Tests, CombatStatusPresentationTests, CombatGridFeedbackGeometryTests, CombatCameraControlTests, SeamlessDungeonCombatTests).
- PASS: render HUD-u w Unity przy 1600×900; poprawiona obwódka kolejki, wspólny dolny panel i poziomy AP. Render korzystał z danych przykładowych, nie zastępuje interaktywnego smoke.
- PASS: self-review — ustawienia statyczne zapisane; usunięte runtime tworzenie licznika noży i dodawanie tooltipów akcji. Zachowana zgodność ich bindings w debug CombatSandbox. Tymczasowy skrypt Editor usunięty.
- NOT RUN: interaktywny smoke aktualnej wersji, inne proporcje ekranu, identyfikacja zgłoszonego pustego okienka podczas gry. Okno przedmiotów otrzymało stan pustej listy, ale pochodzenie okienka ze screena wymaga potwierdzenia.

Bez zmian zasad walki i bez nowego systemu cooldownów. Powtórne portrety kolejki opisują kolejne wystąpienia w Action Value. Karta nadal In Progress, bez commit/push.

## Weryfikacja poprawek UIW

- PASS: build Views.csproj, 0 ostrzeżeń/błędów.
- PASS: 11/11 testów prezentacji, HUD-u i statusów; dodatkowe asercje potwierdzają brak Selectable w wierszach Inspecta i rzeczywisty rozmiar ikon.
- PASS: render Inspecta z jedną umiejętnością w Unity; ikona i wiersz mieszczą się w oknie.
- NOT RUN: ponowny interaktywny smoke po tych poprawkach. Tymczasowy helper Editor usunięty.

## Kompozycje prefabów UIW i wspólne wskaźniki

- Wydzielono prefab HUD-u oraz zagnieżdżone ActionPanel/Timeline/PartyRail. Nowe warianty przycisku ikonowego i slota korzystają z bazowych UIW_Button/UIW_ItemSlot.
- Wspólne CombatVitalsView/PF_UIW_CombatVitals obsługuje oba widoki HP/zasobu/AP. Podgląd kosztu jest prezentacją istniejących kosztów, bez mutacji AP, HP i zasobu.
- Panel celu, powiązanie portret–plansza, wskazanie jednostki Inspecta i czyszczenie wskazania są stanem prezentacji. Nie dodano modelu wspólnych rund.
- PASS: build Views.csproj — 0 ostrzeżeń i błędów.
- PASS: render rzeczywistego HUD-u w Unity na danych przykładowych; wspólne paski, liczby na railu, sloty i przyciski ikonowe.
- Pierwszy zestaw: 77/78 PASS; test callbacku OnDisable wymagał korekty dla EditMode (bez zmiany produkcyjnego callbacku). Końcowy wynik powtórki zapisany niżej.
- Kontrola referencji po wydzieleniu prefabów: PASS; powiązania HUD-u i podwidoków z hostem walki zachowane.
- NOT RUN: interaktywny smoke opisany w UNITY_HANDOFF.md, różne proporcje ekranu oraz porównanie wielu równoczesnych sesji z nowym układem.
- Tymczasowy helper Editor usunięty. Nie wykonano commita/pusha ani zamknięcia karty.

Końcowa powtórka CombatHudPassTests: PASS 5/5. Pozostałe testy zestawu regresyjnego przeszły wcześniej; pierwotny problem dotyczył sposobu wywołania callbacku w teście EditMode. Smoke aktualnego passu: NOT RUN.

## Pass: Inspect drag / skill grid (2026-09-14)
- Naprawiono clamp UIW dla kotwic i pivotów poza środkiem. Testy obu krawędzi dla trzech kombinacji + dotychczasowe resize/focus.
- Authored GridLayoutGroup/ScrollRect/RectMask2D: 5 kolumn, 1–2 widoczne rzędy, przewijanie ponad 10; skrót 0 dla dziesiątego skilla. Testy 5/6/10/12 potwierdzają kompletność, wysokość viewportu i overflow.
- Panel wskazanego celu oddzielony od trwałego Inspecta; odstęp od kolejki; powiększone liczby raila.
- PASS: Views build 0 warnings/errors; final EditMode 18/18 (`%TEMP%/combat-grid-final.xml`).
- NOT RUN: użytkownik smoke aktualnego passu. Bez commit/push. Helper Editora usunięty.

## Zamknięcie
Smoke użytkownika PASS i zgoda na zamknięcie/commit/push: 2026-09-14. Ostatnia poprawka tooltipa zaakceptowana w grze.
