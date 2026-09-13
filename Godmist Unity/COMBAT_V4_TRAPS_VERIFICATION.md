# Combat v4 — pułapki bojowe i integracja z ruchem v1

Status: DONE. Smoke użytkownika: PASS (w tym poprawki feedbacku, balansu i skali).

## Implementacja

DungeonTrapSystem korzysta z istniejących SpikeTrapDungeonObjectInstance i grup aktywacji floor. TrapCombatMapObject wskazuje ten sam obiekt. DungeonCombatCoordinator zbiera jednostki eksploracji i wszystkich sesji; wykonanie kroku ruchu, teleportu, charge i push/pull powiadamia wspólnego ownera. Widok pułapki wyświetla stan, bez osobnego zadawania obrażeń.

Obrażenia wykorzystują CombatDamageSystem, statusy StatusEffectSystem i istniejący lifecycle. Osłabienie ogranicza bezpośrednie obrażenia i nowe snapshoty DoT o 20%; już nałożone DoT zachowują snapshot. Poza walką efekty tykają co 6 sekund aktywnej gry. Częściowy zegar pozostaje na runtime postaci, także przy zmianie sceny; nie dodano pól zapisu ani zapisu w walce. Eksploracyjne pułapki są sprawdzane przy zmianie zajmowanego pola.

AI planuje po zagrożeniu, następnie długości drogi; rozróżnia zużyte grupy i odrzuca przewidywane powalenie. Prediction jest konserwatywne: pełny możliwy DoT, a nie wartość oczekiwana szansy statusu. Podgląd pozostaje read-only, nie wykonuje losowań. Niebezpieczna droga używa istniejącego koloru preview i znaku `!` przy AP.

## Automatyczne wyniki

- PASS: Unity 6000.4.0f1, kompilacja projektu i 105/105 EditMode, 0 FAIL / 0 skipped. Raport lokalny `%TEMP%/traps-reviewed.xml`, log `%TEMP%/traps-reviewed.log`.
- Zestawy: DungeonTrapCombatTests, DungeonTrapAndConditionTests, SimpleCombatAiSystemTests, SeamlessDungeonCombatTests, TacticalObstacleTests, DungeonCombatIntegrationTests, CombatLineOfSightServiceTests, DungeonCombatStateSyncTests, DungeonCombatOutcomeTests.
- Nowe testy: grupowa aktywacja obu stron w różnych sesjach dokładnie raz; 6-sekundowy DoT i wygasanie; przerwanie śmiertelnego ruchu i koszt wykonanego prefiksu; teleport tylko na celu; read-only ryzyko i grupy; obrona/tarcze; osłabienie i 2 tury; pełna odporność; częściowy zegar przy zmianie trybu i ownera; deduplikacja ticku; nieaktywna pułapka; bezpieczne wieloturowe obejście AI.
- PASS: odczyt serialized definicji/prefabów, mapowań minimalnego diffu DungeonSandbox, content validator i rejestracja demo. `%TEMP%/traps-verify.log`: TRAPS_VERIFY_PASS.
- PASS: generator demo seed 4205: 2 pieczęcie, 5 kolców, 3 gazowe pola. TRAPS_DEMO_PASS.
- PASS: git diff --check dla kodu i testów karty.
- Usunięto tymczasowy Editor helper po zapisaniu i walidacji assetów.

## Self-review

Sprawdzono brak podwójnej aktywacji view/runtime, wspólną tożsamość pułapek, callback cleanup, teleport wyłącznie na celu, pełny krok chain charge przed obsługą skutków oraz utrzymanie czasów statusów. Śmierć wroga poza sesją zapisuje pokonanie i ciało przez istniejący mechanizm; pokonanie wszystkich czyści encounter.

Zachowano lokalne zmiany użytkownika. Nowy content trafia do oddzielnego demo; produkcyjnego DD_Catacombs_Procedural i DTC_Catacombs_V1 ta karta nie modyfikuje. Zachowane wartości domyślne istniejących kolców. GC_MainContentCatalog zawiera wyłącznie dodany wpis demo ponad stan użytkownika.

## Weryfikacja manualna

NOT RUN: smoke runtime, wizualny przegląd roboczych sprite'ów, odczucie AI i balans, test obciążenia dużej areny, interaktywne push/pull/charge z pułapkami. Kroki w lokalnym UNITY_HANDOFF.md.

Pełna trwałość obiektów po save/load i włączenie nowej puli do produkcji pozostają w następnej karcie persistence. Minigra rozbrajania poza zakresem.

## Poprawka po smoke: feedback poza walką

Root cause: null feedback bus dla eksploracji, brak pozycji eksploracyjnego BattleUnit w spawnerze, odpinanie wspólnego busa po ostatniej sesji. Naprawiono routing busa i resolver pozycji bez tworzenia dodatkowych widoków jednostek ani edycji prefabów. Callback jest czyszczony przy niszczeniu ownera. Bind zachowuje istniejącą deduplikację subskrypcji.
PASS: dotnet build Views.csproj, 0 warnings/errors. Pierwsza próba --no-restore nie miała project.assets.json; zwykły build z restore zakończył się powodzeniem.
NOT RUN: ponowny smoke feedbacku w Unity i EditMode po tej poprawce (Editor użytkownika otwarty).

## Aktualizacja balansu

Zapisane definicje: kolce 30 + krwawienie 10 przez 3 ticki; gaz 12 przez 5 ticków; pieczęć 40 + osłabienie na 4 tury. PASS: odczyt i asercje wartości assetów. Unity zamknięte podczas edycji. Smoke nowego balansu: NOT RUN. Wzór skalowania pozostał bez zmian.

Aktualizacja współczynnika: DamageGrowthPerLevel = 0.065 we wszystkich trzech definicjach. PASS: asercje zapisu/odczytu wartości. Unity zamknięte. Smoke balansu: NOT RUN.

Końcowe acceptance użytkownika: PASS; wcześniejsze NOT RUN opisują stan w momencie poszczególnych weryfikacji.
