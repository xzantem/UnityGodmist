# Combat v4 — nawigacja przeciwników i drzwi v1

Status: DONE — smoke zaakceptowany, commit/push zatwierdzone.

- PASS: kompilacja w izolowanym projekcie Unity 6000.4.0f1.
- PASS: 65/65 EditMode; TEMP/Godmist_CombatV4_Verification/ai-navigation-verified.xml.
- Zakres: SimpleCombatAiSystemTests 29, CombatInteractionSystemTests 5, DungeonCombatStateSyncSystemTests 2, SeamlessDungeonCombatTests 29.
- Dodano 15 przypadków: wieloturowe obejście ze zwiększeniem odległości, ściana między sąsiadami, koszt i osobna akcja otwierania drzwi, zajęty cel za drzwiami, brak AP, LeverOnly zamknięte/otwarte, ponowne planowanie po zmianie zajętości/drzwi/przeszkód, niedostępny cel, granica pokoju, wcześniejsze kroki tury, role dystansowe i zbiorczy feedback częściowego ruchu.
- Wcześniejszy przebieg: 64/65 PASS, 1 FAIL w konfiguracji testu dystansowego — ustawiono blokadę ruchu, pominięto osobne źródło blokady LOS. Dodano reprezentację fizycznej ściany i asercję braku LOS przed działaniem AI; powtórny przebieg 65/65 PASS.
- PASS: final self-review oraz git diff --check kodu/testów.
- PASS: manualny smoke i odbiór prezentacji przez użytkownika.
- Brak zmian serialized state, save schema, eksploracyjnego pościgu oraz FOV.
- DoorCombatMapObject blokuje krawędź, nie pole. CombatExecutionDriver przekazuje AI istniejący CombatInteractionSystem, zachowując callback synchronizacji/prezentacji.
- Ruch zwraca rzeczywiście wykonany odcinek, również gdy kończy się AP; dotychczasowy interfejs i zbiorczy feedback pozostają dostępne.
