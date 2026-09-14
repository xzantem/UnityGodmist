# Combat v4 — przeszkody taktyczne v1

Status: DONE.

- PASS: 96/96 EditMode w pełnym projekcie Unity 6000.4.0f1. Raport: TEMP/obstacles-source.xml; log: TEMP/obstacles-source.log.
- Zestaw: TacticalObstacleTests, SeamlessDungeonCombatTests, DungeonCombatIntegrationTests, DungeonBattleGridFactoryTests, SimpleCombatAiSystemTests, CombatLineOfSightServiceTests.
- Pokrycie nowych testów: faktyczne definicje trzech wariantów, niezależne flagi ruchu/LOS w obu kierunkach, obejście przeszkody, dash/charge i push/pull, teleport na pole i za przeszkodę, niemutujący preview; walidacja brakujących/duplikowanych przypisań i collidera, ostrzeżenie dla nieblokującej przeszkody.
- PASS: ponowny odczyt zapisanej sceny i przypisań prefabów. TEMP/obstacles-final-verify.log i TEMP/obstacles-demo-verified.log, marker OBSTACLES_VERIFY_PASS.
- PASS: istniejący generator i placement demo, seed 4205: 2 sarkofagi, 3 stosy gruzu, 3 filary; OBSTACLES_DEMO_PASS. TEMP/godmist-obstacles.png — obejrzany render prefabów.
- PASS: self-review i git diff --check kodu. Prefaby mają statyczne elementy; runtime tylko wybiera prefab i ustawia stan collidera. Bez nowego modelu przeszkód/niszczenia ani duplikowania mapy.
- PASS: manualny smoke użytkownika; funkcjonalność i tymczasowe grafiki zaakceptowane.
- NOT RUN: pełny save/load istniejącego zapisu. Produkcyjna pula i identyfikatory pozostały bez zmian; demo ma osobne ID. Aktualizację produkcyjnej puli powiązano z kartą persistence.
- Poprzedni przebieg w izolowanym projekcie: 89/92 PASS, 3 FAIL z powodu niezaładowanych assetów definicji. Końcowy przebieg w pełnym projekcie obejmuje te same testy i kończy się 96/96 PASS.
- Tymczasowy CombatObstacleSetup.cs i meta usunięte. Brak commit/push przed smoke.

## Scope przy commitowaniu

Kod: DungeonObjectSpawner.cs, DungeonObstacleView.cs, ContentValidator.cs; nowe TacticalObstacleTests.cs i meta.
Nowe prefaby: PF_DungeonSarcophagus, PF_DungeonRubble, PF_DungeonPillar wraz z meta.
Nowe dane: DO_Obstacle_Sarcophagus/Rubble/Pillar, DRT_CombatV4_Obstacles_Demo, DTC_Obstacles_Demo, DD_Catacombs_Obstacles_Demo wraz z meta.
DungeonSandbox.unity: wyłącznie 7 linii mapowania obstaclePrefabs.
GC_MainContentCatalog.asset: wyłącznie jeden wpis DD_Catacombs_Obstacles_Demo w DungeonDefinitions; pozostały diff jest użytkownika. Lokalna kopia stanu katalogu sprzed dopisania: TEMP/godmist-obstacles-catalog-before.asset.
KANBAN.md: bieżąca karta, nowa karta niszczenia Combat v5 i zależność aktualizacji puli od persistence. Pozostałe lokalne zmiany zachować.
UNITY_HANDOFF.md pozostaje lokalny.
