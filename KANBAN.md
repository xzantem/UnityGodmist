# Godmist - Kanban

Ten plik jest naszym roboczym, tekstowym Kanbanem i punktem odniesienia do synchronizacji pracy.

Założenia:

- nie tworzymy na razie osobnego `Iceboxa`,
- kiedy uznam, że jakaś karta powinna zmienić kolumnę, dam Ci o tym znać,
- przy okazji będę też aktualizował ten plik, żeby nasz „wewnętrzny” Kanban był zgodny z tym, co masz na boardzie.

Aktualne kolumny:

- `Backlog`
- `Ready`
- `In Progress`
- `Playable / Test`
- `Done`

Priorytety:

- `[P1]` - ważne teraz
- `[P2]` - ważne później
- `[P3]` - fajnie mieć, ale nie na pierwszy etap

## Labelki

- `Core / Architecture`
- `Combat`
- `World / Dungeon / Exploration`
- `Items / Inventory / Economy`
- `NPC / Quest / Dialogue`
- `UI / UX`
- `Tools / Editor / Debug`
- `Content / Data`
- `Design / Systems`
- `Bug`

## Ready




## In Progress

## Done
- `[P1] Dodac pierwszy quest "Zbadaj Katakumby" v1` - `Content / Data`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac dziennik zadan i sledzone zadanie v1` - `NPC / Quest / Dialogue`, `UI / UX`
- `[P1] Dodac akcje wyborow dialogowych v1` - `NPC / Quest / Dialogue`, `Core / Architecture`, `Content / Data`
- `[P1] Dodac odbieranie nagrod questowych i world flags v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`
- `[P1] Dodac cele questa: rozmowa i wyjscie z konkretnego lochu v1` - `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac QuestService oraz eventy postepu v1` - `NPC / Quest / Dialogue`, `Core / Architecture`
- `[P1] Dodac QuestDefinition, QuestInstance i cele etapow v1` - `NPC / Quest / Dialogue`, `Content / Data`
- `[P1] Dodac dialogi NPC v1` - `NPC / Quest / Dialogue`, `UI / UX`
- `[P1] Dodac DialogueDefinition oraz DialogueService v1` - `NPC / Quest / Dialogue`, `Content / Data`, `Core / Architecture`
- `[P1] Dodac interakcje z NPC i rozpoczecie rozmowy v1` - `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Dodac NpcDefinition, NpcInstance i NpcView v1` - `NPC / Quest / Dialogue`, `Content / Data`
- `[P1] Dodac Arungard jako statyczna lokacje i powrot z lochu v1` - `World / Dungeon / Exploration`
- `[P1] Dodac punkt wejscia do lochu powiazany z DungeonDefinition v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Procedural Dungeon Loop v1 - acceptance pass` - `World / Dungeon / Exploration`, `Combat`, `Items / Inventory / Economy`, `Tools / Editor / Debug`
- `[P2] Dodac podstawowe obiekty lochu: skrzynia i przeszkoda v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`
- `[P1] Powrot do aktywnego lochu po walce v1` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`
- `[P1] Dodac generowanie encounterow i triggerow walki w lochu v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`
- `[P1] Dodac grywalna mape podlogi lochu: podlogi, sciany, drzwi i kolizje v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `UI / UX`
- `[P1] Dodac wejscie do lochu, spawn druzyny i wyjscie z lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Utworzyc scene DungeonSandbox i widok wygenerowanego lochu v1` - `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Dodac DungeonGeneratorService dla seedowanego layoutu v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `Tools / Editor / Debug`
- `[P1] Dodac katalog szablonow pokoi i korytarzy v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dodac runtime graph pokoi, polaczen i pieter lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Rozszerzyc DungeonDefinition o konfiguracje proceduralnego lochu v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Rewards and Progression v1 - acceptance pass` - `Items / Inventory / Economy`, `Combat`, `Tools / Editor / Debug`
- `[P1] Dodac combat rewards i LootTable v1` - `Items / Inventory / Economy`, `Combat`
- `[P2] Dodac Inventory i Equipment UI v1` - `UI / UX`, `Items / Inventory / Economy`
- `[P1] Dodac pipeline statystyk z equipment i passive effects v1` - `Core / Architecture`, `Items / Inventory / Economy`, `Combat`
- `[P1] Dodac EquipmentSet oraz walidacje slotow i wymagan v1` - `Items / Inventory / Economy`
- `[P1] Dodac ItemDefinition, ItemInstance i Inventory v1` - `Items / Inventory / Economy`, `Content / Data`
- `[P1] Dodac zdobywanie XP, level i skill points v1` - `Core / Architecture`, `Design / Systems`
- `[P1] Gameplay Loop v1 - acceptance pass` - `Core / Architecture`, `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Polaczyc wynik walki ze stanem lochu i druzyny v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac EncounterDefinition, trigger walki i CombatSessionFactory v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac DungeonDefinition i DungeonInstance dla lochu statycznego v1` - `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Utworzyc ExplorationSandbox z ruchem po statycznej mapie v1` - `World / Dungeon / Exploration`
- `[P1] Dodac Party runtime oraz przelaczanie lidera poza walka v1` - `Core / Architecture`, `UI / UX`
- `[P1] Zrefaktoryzowac GameBootstrap i SceneLoader v1` - `Core / Architecture`
- `[P1] Wprowadzic GameSession i GameModeState v1` - `Core / Architecture`
- `[P1] Combat Sandbox - acceptance pass` — `Combat`, `Tools / Editor / Debug`
- `[P2] Dodać line of sight skilli v1` — `Combat`, `World / Dungeon / Exploration`
- `[P2] Dodać Escape v1` — `Combat`
- `[P2] Dodać interakcję z obiektami bojowymi v1` — `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodać ShieldPool z warstwami tarczy v1` — `Combat`
- `[P2] Dodać używanie skilli przez AI v1` — `Combat`
- `[P2] Dodać hard CC statusy v1: Stun i Immobilize` — `Combat`
- `[P1] Dodać wykonanie wielu TargetMode i targetRef v1` — `Combat`
- `[P1] Dodać Heal i RestoreResource effect v1` — `Combat`
- `[P1] Dodać hit chance, crit i podstawowe DamageType v1` — `Combat`, `Design / Systems`
- `[P1] Dodać ResourceRules i generację zasobów v1` — `Combat`, `Design / Systems`
- `[P2] Dopracować zasięgi i kształty targetowania skilli` — `Combat`
- `[P1] Dodać warunkowe gałęzie efektów skilli v1` — `Combat`
- `[P1] Dodać docelową kolejkę tur opartą o AV` — `Combat`
- `[P1] Dodać pełniejsze targetowanie skilli v1` — `Combat`
- `[P1] Dodać obsługę Downed i Dead` — `Combat`
- `[P1] Dodać akcję Revive v1 w CombatSandbox` — `Combat`
- `[P1] Dodać testy StatusEffectSystem i kumulacji Bleed v1` — `Combat`
- `[P1] Dodać testy reguł obrażeń i odporności` — `Combat`
- `[P1] Dodać wspólny CombatDamageSystem v1` — `Combat`
- `[P2] Dodać ApplyStatus effect v1` — `Combat`
- `[P2] DoprecyzowaÄ‡ uproszczone zasady statusu Bleed v1` â€” `Design / Systems`
- `[P1] DodaÄ‡ debug button Start Combat` â€” `Tools / Editor / Debug`
- `[P1] PrzygotowaÄ‡ bazowy bootstrap i game flow` â€” `Core / Architecture`
- `[P1] UtworzyÄ‡ scenÄ™ Bootstrap` â€” `Core / Architecture`
- `[P2] DodaÄ‡ inspect celu v1` â€” `UI / UX`
- `[P2] DodaÄ‡ highlight celu umiejÄ™tnoÅ›ci` â€” `UI / UX`
- `[P1] Dodać CombatSession` — `Combat`
- `[P1] Dodać BattleUnit` — `Combat`
- `[P1] Dodać BattleGrid` — `Combat`
- `[P1] Dodać GridPosition` — `Combat`
- `[P1] Podpiąć ResistanceSet i ShieldPool do CharacterInstance` — `Core / Architecture`
- `[P1] Dodać ShieldPool` — `Core / Architecture`
- `[P1] Dodać ResistanceSet` — `Core / Architecture`
- `[P1] Dodać CharacterInstance` — `Core / Architecture`
- `[P1] Dodać ResourcePool` — `Core / Architecture`
- `[P1] Dodać CharacterStats` — `Core / Architecture`
- `[P1] Dodać klasę Stat` — `Core / Architecture`
- `[P1] Dodać CharacterClassData` — `Content / Data`
- `[P1] Dodać CharacterView v1` — `Core / Architecture`
- `[P1] Dodać podstawowy prefab jednostki bojowej` — `World / Dungeon / Exploration`
- `[P1] Podłączyć prefab jednostki do CombatSession i synchronizować widok z BattleUnit` — `Combat`
- `[P1] Dodać execution pipeline skilli v1` — `Combat`
- `[P1] Dodać drugi skill testowy z prostym dodatkiem, np. bleed albo push` — `Combat`
- `[P2] Dodać status runtime v1` — `Combat`
- `[P1] Dodać prosty restart walki testowej` — `Tools / Editor / Debug`
- `[P2] Dodać 2-3 przeszkody na planszy testowej` — `World / Dungeon / Exploration`
- `[P2] Dodać blokowanie ruchu przez przeszkody` — `Combat`
- `[P2] Doprecyzować minimalny ruleset ruchu v1` — `Design / Systems`
- `[P2] Doprecyzować AP economy dla vertical slice` — `Design / Systems`
- `[P1] Dodać CharacterDefinition` — `Content / Data`

- `[P1] Ustawić strukturę folderów projektu Unity` — `Core / Architecture`
- `[P1] Ustawić assembly definitions i zależności między modułami` — `Core / Architecture`
- `[P1] Utworzyć scenę CombatSandbox` — `Combat`
- `[P1] Spisać konwencje ID, nazw i podziału Data / Runtime / View / System` — `Design / Systems`
- `[P1] Dodać bazowe enumy i typy wspólne` — `Core / Architecture`
- `[P2] Dodać klikany ruch po gridzie i highlight osiągalnych pól` — `UI / UX`

## Backlog

### Milestone: Playable Dungeon Loop v1

Zamkniety: zobacz sekcje `Done`.

### Pozniej: rozbudowa danych lochow
- `[P2] Dodac semantyczne content anchors do DungeonRoomTemplate` - `World / Dungeon / Exploration`, `Content / Data`, `Design / Systems`
  - Szablon pokoju definiuje dozwolone punkty/obszary dla skrzyn, przeszkod, pulapek i encounterow.
  - Pozwala tworzyc recznie zaprojektowane sytuacje, np. nagrode na koncu waskiego przejscia za przeszkodami lub walka.

### Milestone: Save and Load v1

- `[P1] Dodac GameSaveData DTO i JSON serialization v1` - `Core / Architecture`
- `[P1] Dodac SaveManager oraz manual save/load v1` - `Core / Architecture`, `UI / UX`
- `[P1] Zapisywac party, inventory, quest/world flags i dungeon deltas v1` - `Core / Architecture`, `World / Dungeon / Exploration`
- `[P2] Dodac autosave po wejsciu/wyjsciu z lochu i po ukonczeniu questa v1` - `Core / Architecture`
- `[P1] Save and Load v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`

### Milestone: Arungard, NPC and Quests v1

- `[P1] Dodac pierwszy quest "Zbadaj Katakumby" v1` - `Content / Data`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P2] Dodac TraderRoleData oraz prosta oferte NPC v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Content / Data`
- `[P2] Dodac TradeService i bazowe ceny v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`
- `[P2] Dodac okno handlu NPC v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `UI / UX`
- `[P1] Arungard, NPC and Quests v1 - acceptance pass` - `NPC / Quest / Dialogue`, `Tools / Editor / Debug`

### Combat Core v2

- `[P1] Ujednolicic PassiveEffectRuntime i TimedPassiveEffect dla statusow oraz modifierow v1` - `Combat`, `Core / Architecture`
- `[P1] Dodac pipeline warunkow efektow po effect ID/index v1` - `Combat`
- `[P2] Dodac Poison, Fire i podstawowe buff/debuff statusy v1` - `Combat`, `Content / Data`
- `[P2] Dodac scheduled effects i wieloturowe skille v1` - `Combat`
- `[P2] Dodac reakcje oraz podstawowe counterattack v1` - `Combat`
- `[P2] Rozszerzyc AI o role, personality i scoring v1` - `Combat`

## Sprint: Combat Sandbox - domkniecie vertical slice

Zamkniety: zobacz sekcje `Done`.

## Git checkpoints

- Commit po zamknieciu jednej karty: kod, assety, testy i zmiana dokumentacji stanowia jeden spojny commit.
- Nie commitujemy pojedynczych checklist itemow ani niedzialajacego stanu posredniego.
- Po zakonczeniu milestone'u wykonujemy acceptance pass, push na GitHub i tag wersji.
- Aktualny checkpoint: `v0.1.0-combat-sandbox` po uporzadkowaniu lokalnej nazwy galezi na `main`.
- Kolejny checkpoint: `v0.2.0-gameplay-loop` po zamknieciu Gameplay Loop v1.

## PÃ³Åºniej, ale nadal bez osobnego Iceboxa

JeÅ›li na razie nie chcesz robiÄ‡ `Iceboxa`, to te rzeczy moÅ¼esz trzymaÄ‡ po prostu na dole `Backlogu` jako dalszy etap projektu:

- `[P3] DodaÄ‡ scenÄ™ miasta` â€” `World / Dungeon / Exploration`
- `[P3] DodaÄ‡ przejÅ›cie miasto -> loch` â€” `World / Dungeon / Exploration`
- `[P3] DodaÄ‡ proceduralne generowanie lochÃ³w` â€” `World / Dungeon / Exploration`
- `[P3] DodaÄ‡ system questÃ³w v1` â€” `NPC / Quest / Dialogue`
- `[P3] DodaÄ‡ NPC i dialogi` â€” `NPC / Quest / Dialogue`
- `[P3] DodaÄ‡ handel z NPC` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ crafting u NPC` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ peÅ‚ne inventory` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ equipment set` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ stash druÅ¼yny` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ loot table system` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ rarity tier system dla itemÃ³w` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ weapon i armor parts` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ jewelry system` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ galduryty` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ identyfikacjÄ™ galdurytÃ³w` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ socketing galdurytÃ³w` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ upgrade equipment` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ infuzjÄ™ wyposaÅ¼enia` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ przekuwanie wyposaÅ¼enia` â€” `Items / Inventory / Economy`
- `[P3] DodaÄ‡ save/load system v1` â€” `Core / Architecture`
- `[P3] DodaÄ‡ autosave` â€” `Core / Architecture`
- `[P3] DodaÄ‡ loyalty system NPC` â€” `NPC / Quest / Dialogue`
- `[P3] DodaÄ‡ honor system postaci` â€” `Design / Systems`
- `[P3] DodaÄ‡ kilka klas grywalnych zamiast jednej` â€” `Content / Data`
- `[P3] DodaÄ‡ peÅ‚ne drzewka skilli` â€” `Design / Systems`
- `[P3] DodaÄ‡ passive effects runtime` â€” `Combat`
- `[P3] DodaÄ‡ summon/create object skills` â€” `Combat`
- `[P3] DodaÄ‡ push/pull/charge/fallback/teleport` â€” `Combat`
- `[P3] DodaÄ‡ escape flow peÅ‚nej wersji` â€” `Combat`
- `[P3] DodaÄ‡ bardziej zÅ‚oÅ¼one AI role i personality` â€” `Combat`
- `[P3] DodaÄ‡ inspect panel postaci w walce` â€” `UI / UX`
- `[P3] DodaÄ‡ peÅ‚ny quest journal` â€” `UI / UX`
- `[P3] DodaÄ‡ ekran postaÄ‡ / ekwipunek` â€” `UI / UX`
- `[P3] DodaÄ‡ ekran NPC interaction` â€” `UI / UX`
- `[P3] DodaÄ‡ exploration HUD` â€” `UI / UX`
- `[P3] DodaÄ‡ audio feedback i polish VFX` â€” `UI / UX`

## Jak tego uÅ¼ywaÄ‡

Na start:

1. wrzuÄ‡ sekcjÄ™ `Ready` do kolumny `Ready`,
2. resztÄ™ wrzuÄ‡ do `Backlog`,
3. z `In Progress` trzymaj maksymalnie 1 duÅ¼Ä… rzecz albo 2-3 maÅ‚e,
4. wszystko, co jest juÅ¼ zakodowane, ale wymaga sprawdzenia w grze, przerzucaj do `Playable / Test`.
