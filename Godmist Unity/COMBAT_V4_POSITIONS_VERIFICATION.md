# Combat v4 — pozycje przy wejściu i wyjściu

Status: DONE — smoke użytkownika PASS, commit/push zatwierdzone.

- PASS: 47/47 EditMode, TEMP/Godmist_CombatV4_Verification/positions-verified.xml.
- Zestaw: SeamlessDungeonCombatTests 37, DungeonCombatOutcomeSystemTests 4, DungeonCombatSpawnResolverTests 4, DungeonCombatStateSyncSystemTests 2.
- Nowe przypadki: rezerwowanie oryginalnych pozycji przed konfliktami, zewnętrzne rezerwacje, deterministyczność, blokada krawędzi/pola, pełny pokój, równoczesne dołączanie, integracja factory z konfliktem i atomowym odrzuceniem, snap i oddanie widoku po niedokończonej animacji.
- PASS: kompilacja kodu w Unity 6000.4.0f1.
- PASS: zapis i ponowne wczytanie DungeonSandbox z wariantem PF_UIW_CombatPlacementNotice; referencje, nieblokowanie inputu, niezależność od HUD, szerokość tekstu.
- Log UI: TEMP/combat-placement-ui-check.log, PLACEMENT_NOTICE_VERIFIED.
- Wcześniejsze błędy weryfikacji usunięte: pomocnik Editor wymagał assembly z TMP; negacja kolekcji w NUnit wymagała Has.No.Member; fixture kolizji wskazywała pole poza swoim trójpolowym footprintem. Końcowy przebieg PASS.
- PASS: final self-review i git diff --check.
- PASS: manualny smoke użytkownika oraz wizualny odbiór komunikatu w grze.
- Bez zmian save schema i zasad AV. Scena zmieniona tylko o komunikat i referencje; pozostałe lokalne zmiany zachowane.
- Tymczasowe narzędzia Editor usunięte.
