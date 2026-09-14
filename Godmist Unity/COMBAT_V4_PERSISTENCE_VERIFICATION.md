# Combat v4 — trwałość stanu obiektów areny v1

Status: DONE. Smoke użytkownika: PASS. Smoke zaakceptowany; commit/push zatwierdzone przez użytkownika.

## Zakres

- LayoutVersion 1: zapis wygenerowanej geometrii pięter i tożsamości obiektów zamiast ponownego losowania z aktualnej puli. Komórki zapisują przynależność do pokoju i trudny teren; krawędzie zapisują typ ściany.
- Istniejące delty zachowują stan drzwi, pułapek, skrzyń, dźwigni, bram progresji i pokonanych przeciwników. Layout zachowuje grupy aktywacji pułapek i źródła warunków progresji.
- Zachowane pozycje eksploracyjne przeciwników oraz przygotowane, nieodebrane nagrody. Przedmioty nadal korzystają z istniejącego snapshotu itemów i walidacji jednokrotnego przypisania.
- Definicje obiektów wycofywane z pul można zachować w GameContentCatalog.DungeonObjectDefinitions. Dotychczasowe pule pozostają obsługiwane bez dodatkowego wiring. Brak definicji odrzuca odczyt zamiast podmieniać obiekt.
- Zaakceptowana migracja: stare save’y odtwarzają loch dotychczasową metodą, kolejny zapis utrwala wynik. Nowe niekompletne lub nieznane snapshoty nie uruchamiają fallbacku.
- Bez zapisu podczas walki, zmian produkcyjnej puli, scen i prefabów. Snapshot utrwala wygenerowany układ, nie archiwizuje historycznego balansu ani zawartości definicji encounterów.

## Automatyczna weryfikacja

PASS — Unity kompilacja.
PASS — dotnet build Views.csproj, 0 warnings / 0 errors.
PASS — 82/82 EditMode, raport `%TEMP%/persistence-final.xml`.

Zestawy: DungeonLayoutSnapshotTests (10), DungeonRuntimeRestoreTests (2), DungeonCombatOutcomeSystemTests (4), DungeonCombatStateSyncSystemTests (2), DungeonEnemyBodyTests (11), DungeonTrapAndConditionTests (3), DungeonTrapCombatTests (12), GameSaveJsonSerializerTests (6), GameSessionRestoreFactoryTests (18), GameSessionSaveSnapshotFactoryTests (5), SaveManagerTests (9).

Nowe przypadki obejmują zmianę puli i powtórny zapis/odczyt, migrację rzeczywistego starego JSON bez pola wersji layoutu, usunięcie dawnych szablonów z puli po migracji, uszkodzony snapshot, brak definicji, brak delty pułapki, jednokrotne przypisanie lootów oraz częściowo spełnioną bramę progresji.

PASS — final self-review: istniejący owner save/load, brak drugiej reprezentacji stanu runtime, odrzucanie niekompletnego snapshotu, zachowane unrelated zmiany użytkownika.
PASS — manualny smoke. Kroki w lokalnym UNITY_HANDOFF.md.
