# Combat v4 — kamera większych aren v1

Status: DONE. Smoke użytkownika PASS.

## Zmiana

Rozwinięty istniejący CombatCameraFramer: MMB drag, WASD/strzałki, zoom kółkiem, F na aktywną jednostkę, Home na arenę. Granice przesuwania obejmują całe piętro. Kadry i zoom są zapamiętywane per CombatSession w pamięci widoku, czyszczone przy zakończeniu walki; nie są nowym stanem save.

Koordynator zachowuje rozłączny ownership kamery eksploracji/walki. Powrót do eksploracji płynnie odtwarza zoom. Początek tury przesuwa kamerę tylko dla oglądanej sesji i jednostki poza bezpiecznym widokiem HUD; bez zmiany zoomu i bez ciągłego śledzenia ruchu. Inne walki nie przejmują kadru. Ręczne przesunięcie lub zoom przerywa płynny dojazd.

Scena DungeonSandbox: zapisane Keyboard Pan Speed 15, Zoom Speed 0.15, Transition Speed 8. Zachowane istniejące referencje kamery, padding i HUD safe area. Bez nowych elementów UI i runtime workaroundów prefabów.

## Weryfikacja

PASS: kompilacja Unity i dotnet build Views.csproj (0 warnings / errors).
PASS: 58/58 EditMode przed zmianami limitów zoomu, CombatCameraControlTests (9) + SeamlessDungeonCombatTests (49). Raport `%TEMP%/camera-final.xml`, log `%TEMP%/camera-final.log`.
PASS: self-review i git diff --check dla scope karty.
PASS: manualny smoke inputu i wizualnej płynności w Game View. Kroki w lokalnym UNITY_HANDOFF.md.

Testy pokrywają granice całego piętra, pamięć i usuwanie kadru, izolację walk, automatyczne skupienie tylko poza widokiem, zachowanie zoomu, brak śledzenia kroków, przerwanie dojazdu, brak zapisu do kamery eksploracji i kadrowanie dla różnych proporcji ekranu oraz istniejące regresje seamless.

Smoke oraz commit/push zatwierdzone przez użytkownika.

Doprecyzowanie zoomu: bezwzględny zakres 50–200% niezależnie od rozmiaru piętra, także dla Home/Frame. Dotychczasowe minimumOrthographicSize pozostaje dolnym rozmiarem automatycznego kadrowania małej areny; nie ogranicza ręcznego przybliżania. Build po zmianie PASS; Poprzedni zakres: CombatCameraControlTests 12/12 PASS; testy nowego zakresu 50–200% NOT RUN (Unity otwarte). Self-review PASS; smoke nowych limitów PASS.
