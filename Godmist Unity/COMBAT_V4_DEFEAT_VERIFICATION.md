# Combat v4 — reperkusje porażki: weryfikacja

Status: DONE. Użytkownik zaakceptował smoke i końcowy UI oraz polecił commit/push. Poniższe wpisy NOT RUN opisują historyczne etapy przed acceptance.

- PASS: Unity 6000.4.0f1 — kompilacja source project, walidacja Game Defines.
- PASS: 89/89 EditMode w izolowanym projekcie w TEMP/Godmist_CombatV4_Verification; wynik defeat-honor.xml.
- Zestaw: CampaignDefeatAndRecoveryTests, SeamlessDungeonCombatTests, CombatRewardSystemTests, DungeonCombatOutcomeSystemTests, DungeonEncounterRewardPreparationSystemTests, NpcLoyaltyServiceTests, SaveManagerTests, GameSessionSaveSnapshotFactoryTests.
- PASS: referencje nowego UI i skrypty sceny bez missing references; przyciski porażki po layout pass pod Canvas mają 536×48.
- PASS: self-review — zachowanie blokady StoryLocked, zabezpieczenie zapisu po porażce, honor bez clampu, cleanup walk/input, listener cleanup oraz powrót do istniejącej listy zapisów.
- NOT RUN: manualny smoke użytkownika, wizualny PlayMode UI pass.

Wykryte i rozwiązane podczas weryfikacji:
- Test konfiguracji rabatu początkowo FAIL: niepełne wpisy testowe były odrzucane przez walidator Game Defines. Test aktualizuje teraz poprawne, istniejące definicje; granice dziewięciu tierów objęte 18 przypadkami.
- Pomiar przycisków bez Canvas początkowo FAIL: po umieszczeniu prefabowego okna w Canvas i przeliczeniu layoutu weryfikacja PASS.
- StoreRecoverableCompanions zachowuje StoryLocked; test regresyjny PASS.

Serialized setup wykonany za zgodą użytkownika. Tymczasowy helper Editor został usunięty.
Smoke checklist: lokalny UNITY_HANDOFF.md.

## Poprawki po smoke

- FAIL użytkownika: ogólny komunikat bez przyczyny i brak usług kapłana.
- Poprawiono komunikat na podstawie tej samej klasyfikacji, która kończy kampanię.
- Naprawiono null w roli kapłana; PriestRoleData przeniesiono do własnego pliku, utworzono i podpięto PR_Arungard_Priest.
- PASS: 38/38 CampaignDefeatAndRecoveryTests (defeat-smoke-fixes.xml), kompilacja Unity, odczyt zapisanego assetu roli w kolejnym procesie Unity (Godmist_Priest_Verify.log).
- NOT RUN: ponowny smoke usług kapłana i wizualnego komunikatu.

## Honor debug i rozrzut złota

- PASS: 51/51 testów komend konsoli i sugestii (honor-command-final.xml); sethonor ustawia dokładną wartość, również ujemną.
- PASS: 50/50 testów nagród i odzyskiwania (gold-noise.xml); równomierny rozrzut 50–150%, seed lochu i zachowane pre-rolled rewards.
- NOT RUN: smoke nowych komend i losowania w Editorze.
- UI kapłana: przekazano propozycje układu; bez zmian serialized UI w tym pass.

## UI pass kapłana

- PASS: kompilacja i zapis prefabu; walidacja wszystkich referencji CompanionRecoveryWindowView/RowView po ponownym uruchomieniu Unity.
- PASS: przeliczenie layoutu pod Canvas — wiersz 1004×116, scroll 1004×300 (Godmist_Recovery_UI_Verify.log).
- Rozmiar wspólnego okna zachowany. Nowe dane rozmówcy, honor/rabat, status, portret, rezultat odzyskania i powód blokady. Przypisano ikonę zakładki, poprawiono lokalny kontrast.
- Tymczasowy helper usunięty.
- NOT RUN: wizualny smoke użytkownika.

## Uproszczenie UI i party rail

- Naprawa: CharacterInfoPartyRailView reaguje na PartyRosterChanged, usuwa subskrypcję starej sesji i przy OnDestroy.
- Krótszy tekst panelu, usunięte ellipsis i zbędny komunikat gotowości, większy napis przycisku, zmiana ikony.
- PASS: source Unity compile i serialized prefab write (Godmist_Recovery_Compact.log).
- NOT RUN: wizualny smoke i odświeżenie rail w PlayMode.

## Cena, tooltip i hierarchia wiersza

- PASS: 43/43 CampaignDefeatAndRecoveryTests po wydzieleniu GetBaseCost.
- PASS: kompilacja source Unity, ponowny odczyt prefabu i komplet referencji.
- PASS: walidacja disabled dla 243 złota i enabled dla 300 złota; wspólny blok ceny/przycisku 310 px, cena 142 px, odstęp 8 px, przycisk 160 px. Log Godmist_Recovery_ActionFinal.log.
- Naprawiono wykryty podczas walidacji niezapisany CanvasGroup na nested prefabie: grupa jest teraz zapisana na dedykowanym rodzicu przycisku. Tymczasowy helper usunięty.
- NOT RUN: wizualny smoke oraz hover tooltipa w PlayMode.
