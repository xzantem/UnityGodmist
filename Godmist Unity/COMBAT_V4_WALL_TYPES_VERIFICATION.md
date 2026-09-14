# Combat v4 — niskie murki i balustrady

Status: DONE.

- PASS: 119/119 EditMode, TEMP/Godmist_CombatV4_Verification/walls-verified.xml.
- Zestaw: DungeonBattleGridFactoryTests 6, DungeonRoomFootprintTests 7, DungeonRoomTemplateEditorValidatorTests 17, DungeonStructuralAnchorTests 11, SeamlessDungeonCombatTests 49, SimpleCombatAiSystemTests 29.
- Pokrycie: rodzaje krawędzi i LOS w obu kierunkach; +2 AP (trzeci krok 5 AP), niewystarczające AP, tańsza ścieżka w podglądzie, koszt AI, wyjście z pokoju i pustka, blokowanie przemieszczeń i ukośnego narożnika, legalność teleportu, O-footprint, cztery obroty, zewnętrzne krawędzie, wykluczenie drzwi, domyślny typ legacy i round-trip serializacji.
- PASS: kompilacja Unity 6000.4.0f1.
- PASS: zapis prefabów i odczyt referencji DungeonSandbox oraz CombatSandbox; walidacja demo pokoju, collidery i render obu orientacji.
- Log: TEMP/wall-types-final-verify.log, WALL_TYPES_VERIFIED.
- Podgląd: TEMP/godmist-wall-types.png — pełna ściana, murek, balustrada.
- PASS: final self-review, git diff --check kodu/testów.
- PASS: manualny smoke w grze — zaakceptowany przez użytkownika.
- NOT RUN: end-to-end odczyt starego zapisu z nową wersją.
- Schemat zapisu gry bez zmian; geometria korzysta z szablonów, brak pola WallType zachowuje Full=0.
- Dane i widoki wykorzystują istniejące krawędzie strukturalne, runtime mapy, kontekst i siatkę walki. Nie dodano drugiej produkcyjnej mapy ani budowania geometrii prefabów przez runtime.
- Tymczasowe narzędzia Editor usunięte. DTC_Catacombs_V1 i pozostałe lokalne zmiany użytkownika zachowane; demo pokoju nie zostało automatycznie dopisane do katalogu.
