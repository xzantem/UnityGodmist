`KANBAN.md` jest głównym źródłem prawdy dla aktualnego stanu prac,
priorytetów i wyboru kolejnej karty. Starsze roadmapy mają charakter referencyjny.

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

## Backlog

### Milestone: Combat v3
- `[P1] Combat v3 - czytelnosc feedbacku i sekwencjonowanie akcji v1` - `UI / UX`, `Combat`
  - Floating combat feedback powinien byc powiazany z momentem rozstrzygniecia akcji i pozycja jednostki w tym momencie, a nie z poczatkiem kolejnego ruchu; komunikaty maja byc grupowane lub rozsuwane tak, aby pozostawaly czytelne.
  - Tura przeciwnika ma byc prezentowana jako sekwencja pojedynczych decyzji i rezultatow z krotkimi przerwami, zamiast wykonywania calej petli AI w jednej klatce; gracz powinien widziec ruch, uzycie skilla i efekt przed kolejna akcja.
- `[P1] Combat v3 - rzeczywista sciezka ruchu AI i preview kosztu v1` - `Combat`, `UI / UX`
  - Ruch przeciwnika ma korzystac z wyznaczonej sciezki tile-by-tile i wizualnie przechodzic przez kolejne pola, z zachowaniem blokad i kosztu AP, zamiast przesuwania widoku bezposrednio z poczatku do konca.
  - Preview ma pokazywac planowana sciezke oraz jej odleglosc/koszt dla nastepnej tury przeciwnika, wykorzystujac istniejacy renderer feedbacku planszy.
- `[P1] Combat v3 - rozszerzyc targetowanie` - `Combat`, `Core / Architecture`, `Content / Data`
  - Dodac ksztalty m.in. line, diagonal line i X.
- `[P1] Combat v3 - penetracja oraz DealDamage z penetracja` - `Combat`, `Core / Architecture`, `Content / Data`, `Design / Systems`
  - Oddzielna penetracja fizyczna i magiczna oraz jej zastosowanie w `DealDamage`.
- `[P1] Combat v3 - tooltipy, inspect i feedback bojowy v1` - `UI / UX`, `Combat`, `Core / Architecture`
  - Tooltip skilla pokazuje wyliczony z aktualnych statystyk zakres obrazen, szanse trafienia, przewidywane obrazenia po obronie celu oraz komplet efektow i czasow trwania.
  - Dodac widoczne buffy, nie tylko statusy; full inspect wybranej jednostki; preview zasiegu ruchu przeciwnika w jego nastepnej turze; feedback dla nowych efektow Combat v3 (buff, proc, aura, redirect, blokada regeneracji).
- `[P2] Zrefaktoryzowac payloady PassiveEffectRuntime v1` - `Core / Architecture`, `Combat`, `Design / Systems`
  - Zastapic pola specyficzne dla wybranych efektow, np. `TotalDamage` i `StatusHealPercent`, typowanymi payloadami runtime.
  - Zaplanowac migracje save/load, debug inspectora, lifecycle i testow przed dodaniem kolejnych niestandardowych statusow.

### Milestone: Dungeon Content and Layout v2

Nastepny milestone po `Combat v3`. Nie przebudowuje istniejacego loopu wejscie -> eksploracja -> wyjscie; rozwija go o content, decyzje i alternatywne trasy.

- `[P1] Dodac semantic content anchors i recznie projektowane sytuacje pokoi v2` - `World / Dungeon / Exploration`, `Content / Data`, `Design / Systems`
  - Room template definiuje semantyczne kotwice dla encounterow, skrzyn, przeszkod, pulapek, dzwigni i nagrod zamiast losowego wyboru dowolnej poprawnej komorki.
- `[P1] Dodac petle grafu lochu i wymagane odnogi progresji v2` - `World / Dungeon / Exploration`, `Core / Architecture`, `Design / Systems`
  - Generator dopuszcza alternatywne trasy i petle, nie tylko jednokierunkowe optional branches. Obsluguje bramkowanie progresji, np. klucz albo dzwignia w wymaganej odnodze otwierajace droge do finalu.
- `[P1] Dodac pulapki, przeszkody, klucze/dzwignie i proste zagadki lochowe v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`, `UI / UX`
  - Pierwszy pionowy pass interaktywnych obiektow lochu, wlacznie z ryzykiem/nagroda i czytelnym feedbackiem; bez osobnego frameworku zlozonych zagadek.
- `[P1] Dodac lootbagi oraz theme i level-bracket loot tables v1` - `Items / Inventory / Economy`, `World / Dungeon / Exploration`, `Content / Data`
  - Zwykly loot z przeciwnikow i skrzyn korzysta z lootbagow; ich zawartosc zalezy od motywu lochu oraz przedzialu poziomu. Equipment i potiony nie dropia bezposrednio ze standardowych przeciwnikow.
- `[P1] Dodac boss encountery i boss lootbagi v1` - `Combat`, `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `Content / Data`
  - Boss jest gwarantowany albo zalezy od trudnosci i trafia blisko konca lochu. Boss lootbag ma wyzsza szanse na unikaty. Pierwszy pass nie tworzy nowego frameworku faz bossa, skryptow areny ani specjalnego AI.
- `[P1] Dodac testowy content bizuterii do lootbagow v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX`
  - Dodac gotowe definicje pierscieni, naszyjnikow, bransolet i kolczykow jako bezposrednie dropy z lootbagow, w tym 1-2 unikaty bossowe. Nie wprowadzac ich do generatora trzyczesciowego, ktory pozostaje dla broni i pancerzy.
- `[P1] Dungeon Content and Layout v2 - acceptance pass` - `World / Dungeon / Exploration`, `Combat`, `Items / Inventory / Economy`, `Tools / Editor / Debug`
  - Zweryfikowac deterministyczny layout z petlami i gate'ami, persistence obiektow/lootbagow/bossow oraz pelny przeplyw lootbag -> inventory -> equipment.
- `[P1] Zachowac stan czesciowo pokonanego encountera po ucieczce v1` - `Bug`, `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`

### Milestone: Crafting, Upgrading and Consumables v1

Planowany po `Dungeon Content and Layout v2`. NPC, dialogi, questy i handel maja juz fundamenty v1; ten milestone rozszerza konkretne uslugi miejskie zamiast budowac ogolne `NPC v2` od zera.

- `[P1] Dodac crafting broni i pancerzy z czesci oraz materialow v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`
  - Kowal tworzy equipment z trzech wybranych czesci i kosztu materialow. Pokazac preview wyniku przed potwierdzeniem; zachowac istniejacy generator runtime jako ownera payloadu.
- `[P1] Dodac ulepszanie quality i rarity equipmentu u kowala v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Design / Systems`, `UI / UX`
  - Uzywa ustalonych kosztow i szans zmiany unikalnosci; pokazuje koszt, ryzyko i wynik przed potwierdzeniem.
- `[P1] Dodac storage w Arungardzie v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `UI / UX`, `Core / Architecture`
  - Trwaly magazyn kampanii do odkladania itemow poza inventory postaci.
- `[P1] Dodac consumables: potiony, bomby i return scroll v1` - `Items / Inventory / Economy`, `Combat`, `World / Dungeon / Exploration`, `Content / Data`
  - Pierwsze dzialajace itemy zuzywalne, ich uzycie, ograniczenia i persistence. Return scroll realizuje ustalone wyjscie do miasta.
- `[P2] Dodac crafting bizuterii oraz galduryty/runy v2` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `Design / Systems`
  - Osobny model po pierwszym passie dropow bizuterii; nie zakladac automatycznie modelu trzech czesci dla bizuterii.
- `[P1] Crafting, Upgrading and Consumables v1 - acceptance pass` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Combat`, `Tools / Editor / Debug`
  - Pokryc crafting, upgrade, storage, consumables i save/load, wlacznie z zachowaniem instanceId oraz walidacja kosztow.

## Ready

## In Progress

## Playable / Test

## Done

### Milestone: Combat v3 — Closed

- `[P1] Combat v3 - dzialalnosc skilli toggle` - `Combat`, `Core / Architecture`, `UI / UX`, `Content / Data`
  - Toggle ma aktywacje ograniczona do raz na ture, darmowe wylaczenie, upkeep zasobu oraz natychmiastowe usuwanie efektow po wylaczeniu lub smierci wlasciciela.
- `[P1] Combat v3 - mechanika sztyletow Scouta` - `Combat`, `Content / Data`, `Design / Systems`
  - Scout ma limit `5` sztyletow resetowany na poczatku walki; skille z `Knife Cost` zuzywaja sztylety, upuszczaja je obok celu, a wejscie na pole odzyskuje zasob; licznik jest widoczny na pasku skilli.

- `[P1] Combat v3 - rozszerzyc execution pipeline skilli` - `Combat`, `Core / Architecture`, `Content / Data`, `Design / Systems`
  - Efekty: modyfikator AP na jedna nastepna ture; przyspieszenie/spowolnienie ruchu przez zmiane AV o `x% * maxAV`; blokada wszystkich dodatnich gainow zasobu; OnHit `StatusEffect`; LifeSteal i ResourceDrain; `TimesDamage`; `BonusDamageMultiplierChance` z osobnym rzutem dla kazdego hitu.
  - Dodano `UniqueBuffTypes`: gwarantowany crit nastepnego damage, gwarantowany unik nastepnej obrony, zawsze maksymalny damage oraz rozszerzalny model kolejnych unikalnych buffow.
  - Obsluga przejmowania czesci obrazen przeciwnika, opcjonalnie z mitygacja po przejeciu; kosztu zdrowia z przeliczeniem na odzysk zasobu; tarczy od procentu average attack; `ModifyResistance`; AuraBuff/AuraStatus.
  - Warunkowe buffy: zalezne od sasiednich sojusznikow, progu wydanego zasobu, pozostajacej tarczy i innych warunkow runtime. Charge zatrzymuje sie na koncu ruchu.

### Milestone: UI Workspace v1 — Closed

- `[P2] Dodac glossary i linki do UI Workspace tooltip v1` - `UI / UX`, `Combat`, `Core / Architecture`
  - Wspolny `UIWorkspaceTooltipView` obsluguje nazwy i ikony statusow z glossary, linki w tresci oraz zagniezdzone tooltipy z timerem blokady i grace period.

- `[P2] Ujednolicic stack count i zloto w slotach UI Workspace v1` - `UI / UX`, `Items / Inventory / Economy`, `Core / Architecture`
  - `PF_UIW_ItemSlot` ma opcjonalny `CountLabel` dla stackowalnych itemow, a zloto z ciala jest pierwszym wpisem glownego gridu lootu. Odbior itemu dziala pojedynczym kliknieciem lub drag-and-drop; zloto pozostaje wartoscia runtime, nie `ItemInstance`. Ciala sa tworzone tylko na walkable cells pokoju encountera i zawsze otrzymuja collider interakcji.

- `[P1] UI Workspace v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`, `Items / Inventory / Economy`, `UI / UX`
  - Zweryfikowano focus, z-order, drag, resize, clamp, transfery, tooltipy, loadout, drzewka oraz persistence; smoke test zaliczony.
- `[P1] Podpiac generowany equipment do LootTable i tooltipow v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX`
  - LootTable generuje testowy equipment, a tooltip itemu pokazuje statystyki, wymagania i porownanie z zalozonym itemem.
- `[P1] Dodac floating loot window ciala v1` - `UI / UX`, `Items / Inventory / Economy`, `World / Dungeon / Exploration`, `Core / Architecture`
  - Okno lootu ciala pokazuje itemy oraz gold w osobnym item slocie. Umozliwia drag-and-drop do otwartego Character Info z walidacja wagi i zachowaniem `instanceId`; double-click odbiera item aktywna postacia. Zakres obejmuje tylko eksploracje po walce: bez lootowania cial w combat i bez kosztu AP. Ownerem mutacji pozostaje system lootu/inventory.

- `[P1] Przeniesc drzewka umiejetnosci do UI Workspace i dopracowac UX v2` - `UI / UX`, `Design / Systems`, `Core / Architecture`
  - Zastapic panel drzewek floating window zgodnym z design systemem, otwieranym buttonem z Character Info. Dodac tooltip node'a, czytelne requirementy i powody blokady, disabled states oraz pelne drzewko widoczne jednoczesnie bez scrolla. Rysowac polaczenia prerequisite'ow; progi tierow pokazywac jako obramowania grup `3-4` (`0/10`), `5-6` (`0/20`) i `7` (`0/30`), znikajace po spelnieniu warunku.

- `[P1] Przeniesc skill loadout do UI Workspace v1` - `UI / UX`, `Combat`, `Core / Architecture`
  - Zastapic modal loadoutu niezaleznym floating window zgodnym z design systemem, otwieranym buttonem z Character Info; zachowac drag-and-drop, pinned basic attack, walidacje slotow i blokade zmian podczas walki.

- `[P1] Dodac wspolne tooltipy UI Workspace v1` - `UI / UX`, `Core / Architecture`, `Items / Inventory / Economy`, `Combat`
  - Tooltipy przy hoverze itemow, skilli i pojec systemowych maja uzywac wspolnego template'u, renderowac sie nad oknami i pokazywac statystyki/wymagania itemu zamiast osobnego inspect panelu.

- `[P1] Dodac transfer drag-and-drop miedzy Character Info v1` - `UI / UX`, `Items / Inventory / Economy`, `Core / Architecture`
  - Umozliwic jednoczesne otwarcie Character Info kilku czlonkow druzyny oraz transfer itemow drag-and-drop miedzy ich inventory z walidacja wagi i bez zmiany `instanceId`; widoki wysylaja intencje, a ownerem transferu pozostaje system inventory.

- `[P1] Dodac Character Info floating window v1` - `UI / UX`, `Core / Architecture`, `Design / Systems`
  - Niezalezne okno per postac otwierane kliknieciem portretu z party railu i innych UI. Centrum zajmuje portret; wokol niego sa kwadratowe sloty equipmentu z ikonami. Lewa kolumna pokazuje name/class, level/XP, HP i resource, runtime combat stats, resistances, Honor i dostepnosc. Prawa kolumna to inventory z wyszukiwarka, itemami oraz laczna waga i gold na dole. Ma odswiezac dane po zmianie inventory/equipment i wspolistniec z innymi oknami.

- `[P1] Dodac fundament floating windows v1` - `UI / UX`, `Core / Architecture`
  - Wspolny window chrome zgodny z design systemem: drag przez titlebar, resize, focus, z-order, clamp do ekranu, close i routing inputu; bez migracji istniejacych paneli w tej karcie.

- `[P1] Ustandaryzowac design system i template paneli v1` - `UI / UX`, `Core / Architecture`, `Tools / Editor / Debug`
  - Zdefiniowac wspolne palette kolorow, typografie, spacing, obramowki, hover/disabled states, ikony i prefab/template UGUI dla paneli. Nowe okna maja korzystac z tych samych tokenow i komponentow zamiast tworzyc lokalne, niespojne style.
  - Utworzyc utrzymywany katalog template'ow: nazwa, przeznaczenie, warianty, hierarchia, wymagane komponenty, przyklady uzycia i ograniczenia. Katalog ma okreslac, kiedy reuse'owac istniejacy template, kiedy zlozyc panel z kilku template'ow, a kiedy uzasadnione jest dodanie nowego.

- `[P1] Rewards and Progression v2 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`, `Combat`, `Items / Inventory / Economy`, `UI / UX`
  - Pokryc testami XP/Honor/escape, growth, drzewka i loadout, persistencje cial i lootow oraz deterministyczne generowanie equipmentu.

- `[P1] Dodac generowanie broni i pancerzy z czesci v1` - `Items / Inventory / Economy`, `Content / Data`, `Design / Systems`
  - Generator buduje bron albo pancerz z trzech czesci, zachowuje quality, rarity, level, wymagania i payload po save/load; bez craftingu, upgrade'ow i galdurytow.
- `[P1] Dodac runtime oraz zapis inwestycji w skill tree v1` - `Core / Architecture`, `Content / Data`, `Design / Systems`
  - Zdefiniowac trzy drzewka, node'y, prerequisite'y, limity punktow i odblokowania poziomow drzewka; bez wykonywania efektow node'ow.
- `[P1] Dodac trwale ciala i interakcje z lootem w lochu v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`, `Core / Architecture`
  - Ciala zachowuja nieodebrany loot po walce i save/load; aktualnie sterowana postac podnosi znalezione itemy kliknieciem, a gold trafia do puli kampanii.
- `[P1] Rozbic combat rewards na indywidualny loot przeciwnikow v1` - `Combat`, `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `Core / Architecture`
  - XP i Honor naliczac automatycznie za victory (`+1 Honor` za pokonanego przeciwnika dla aktywnej postaci); gold i itemy przypisac do konkretnych cial. Escape nie daje nagrod i obniza Honor o `5`.
- `[P1] Dodac modal loadoutu i zamiane aktywnych skilli v1` - `UI / UX`, `Combat`
  - Poza walka umozliwic drag-and-drop skillow miedzy slotami oraz natychmiast odswiezyc HUD kolejnej walki.
- `[P1] Dodac skalowalny model loadoutu aktywnych skilli v1` - `Core / Architecture`, `Combat`, `Design / Systems`
  - Walidowac limit `5 + modyfikatory`, nie dopuszczac duplikatow i zachowac obligatoryjny basic attack w loadoucie.
- `[P1] Dodac UI progresji i wydawania punktow v1` - `UI / UX`, `Core / Architecture`, `Design / Systems`
  - Poza walka pokazac XP, level, dostepne punkty i trzy drzewka; inwestycja zmienia tylko stan progresji do czasu implementacji efektow node'ow.
- `[P1] Dodac GrowthData klas i automatyczny wzrost statystyk v1` - `Core / Architecture`, `Content / Data`, `Design / Systems`
  - Oprzec wzrost o dane klas z `Statystyki Postaci.xlsx`, level bandy oraz przeliczanie po wielokrotnym level-upie i restore save.
- `[P1] Dodac status indicators i tooltipy v1` - `UI / UX`, `Combat`
- `[P2] Dodac wizualne stany obiektow i jednostek v1` - `UI / UX`, `Combat`, `World / Dungeon / Exploration`
- `[P1] Przebudowac feedback ruchu i targetowania v1` - `UI / UX`, `Combat`
- `[P1] Rozszerzyc Floating Combat Feedback v2` - `UI / UX`, `Combat`
- `[P1] Przebudowac istniejacy CombatHUD do ukladu docelowego` - `UI / UX`, `Combat`
- `[P2] Dodac personality AI v1` - `Combat`, `Content / Data`
- `[P2] Dodac role AI v1` - `Combat`, `Content / Data`
- `[P2] Dodac reakcje oraz podstawowe counterattack v1` - `Combat`, `Content / Data`
- `[P1] Dodac scoring dostepnych skilli AI v1` - `Combat`
- `[P2] Dodac scheduled effects i wieloturowe skille v1` - `Combat`, `Content / Data`
- `[P2] Dodac CreateObject i Summon v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Dodac zrodla i wygasanie modifierow v2` - `Combat`, `Core / Architecture`
- `[P1] Dodac Poison i Burn v1` - `Combat`, `Content / Data`
- `[P1] Przeniesc Stun i Immobilize do PassiveEffectRuntime oraz dodac Sleep, Freeze i Paralysis v1` - `Combat`, `Content / Data`
- `[P2] Dodac Taunt i Invisibility v1` - `Combat`, `Content / Data`
- `[P1] Dodac Pull i Fallback v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac Charge i Teleport v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac runtime ModifyVar v2` - `Combat`, `Core / Architecture`
- `[P1] Dodac runtime ModifyStat v2` - `Combat`, `Core / Architecture`
- `[P1] Przeniesc Bleed do PassiveEffectRuntime v2` - `Combat`, `Core / Architecture`
- `[P1] Zrefaktoryzowac aplikowanie i usuwanie statusow pod PassiveEffectRuntime v2` - `Combat`, `Core / Architecture`
- `[P1] Zrefaktoryzowac warunki efektow na effectId/index v2` - `Combat`, `Core / Architecture`, `Content / Data`
- `[P1] Dodac lifecycle passive effectow v2` - `Combat`, `Core / Architecture`
- `[P1] Ujednolicic PassiveEffectRuntime i TimedPassiveEffect dla statusow oraz modifierow v2` - `Combat`, `Core / Architecture`
- `[P2] Dopracowac inspector SkillData v2` - `Tools / Editor / Debug`, `Combat`, `Content / Data`
- `[P2] Dodac podglad runtime jednostki w narzedziach debugowych v1` - `Tools / Editor / Debug`, `Combat`
- `[P2] Dodac autouzupelnianie i sugestie Dev Console v1` - `Tools / Editor / Debug`, `UI / UX`
- `[P1] Dodac walidatory danych combatowych v1` - `Tools / Editor / Debug`, `Combat`, `Content / Data`
- `[P1] Dodac Combat Scenario Runner v1` - `Tools / Editor / Debug`, `Combat`
- `[P1] Rozszerzyc Dev Console o komendy gameplayowe v2` - `Tools / Editor / Debug`, `Combat`, `Core / Architecture`, `Items / Inventory / Economy`
- `[P1] Rozszerzyc Dev Console o komendy globalne i targetowanie po characterId v1` - `Tools / Editor / Debug`, `Combat`, `Core / Architecture`
- `[P1] Dodac Dev Console v1` - `Tools / Editor / Debug`, `Combat`
- `[P1] Dodac generowanie drzwi z kotwic pomieszczen v1` - `World / Dungeon / Exploration`, `Content / Data`, `Combat`
- `[P1] Dodac strukturalne kotwice pomieszczen v1` - `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`
- `[P1] Dodac wykrywanie, poscig i rozpoczecie walki przez encounter v1` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`
- `[P1] Dodac DungeonCombatContext i snapshot obszaru walki v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Zbudowac BattleGrid z DungeonCombatContext v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dodac spawn uczestnikow walki w obszarze lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Wyswietlic obszar lochu jako scene walki v1` - `Combat`, `World / Dungeon / Exploration`, `UI / UX`
- `[P1] Wyswietlic encountery jako przeciwnikow na mapie lochu v1` - `Combat`, `World / Dungeon / Exploration`, `Content / Data`
- `[P1] Polaczyc wynik walki lochowej z DungeonInstance v1` - `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
- `[P1] Dodac testy Dungeon Combat Integration v1` - `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Dodac interakcje z drzwiami w walce lochowej v1` - `Combat`, `World / Dungeon / Exploration`
- `[P1] Dungeon Combat Integration v1 - acceptance pass` - `Combat`, `World / Dungeon / Exploration`, `Tools / Editor / Debug`
- `[P1] Arungard, NPC and Quests v1 - acceptance pass` - `NPC / Quest / Dialogue`, `Tools / Editor / Debug`
- `[P2] Dodac okno handlu NPC v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `UI / UX`
- `[P2] Dodac TradeService i bazowe ceny v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`
- `[P2] Dodac TraderRoleData oraz prosta oferte NPC v1` - `NPC / Quest / Dialogue`, `Items / Inventory / Economy`, `Content / Data`
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

- `[P1] Dodac GameSaveData DTO i JSON serialization v1` - `Core / Architecture` - Done
- `[P1] Dodac snapshoty runtime dla postaci, inventory i itemow v1` - `Core / Architecture`, `Items / Inventory / Economy` - Done
- `[P1] Zapisywac questy, world flags oraz stan NPC v1` - `Core / Architecture`, `NPC / Quest / Dialogue` - Done
- `[P1] Dodac GameContentCatalog v1` - `Core / Architecture`, `Content / Data` - Done
- `[P1] Dodac API restore dla Quest i Dungeon runtime v1` - `Core / Architecture`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration` - Done
- `[P1] Dodac GameSession restore factory v1` - `Core / Architecture`, `Content / Data`, `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration` - Done
- `[P1] Dodac SaveManager oraz manual save/load v1` - `Core / Architecture`, `UI / UX` - Done
- `[P1] Zapisywac party, inventory, quest/world flags i dungeon deltas v1` - `Core / Architecture`, `World / Dungeon / Exploration` - Done
- `[P2] Dodac autosave po wejsciu/wyjsciu z lochu i po ukonczeniu questa v1` - `Core / Architecture` - Done
- `[P1] Dodac CampaignId i strukture katalogow zapisow v1` - `Core / Architecture` - Done
- `[P1] Dodac liste oraz zarzadzanie zapisami kampanii v1` - `Core / Architecture`, `UI / UX` - Done
- `[P1] Dodac menu pauzy i UI zapisow v1` - `UI / UX`, `Core / Architecture` - Done
- `[P1] Save and Load v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture` - Done

### Milestone: Dungeon Combat Integration v1

Zamkniety: zobacz sekcje `Done`.


### Combat Core v2
- `[P1] Combat Core v2 - acceptance pass` - `Combat`, `Tools / Editor / Debug`, `UI / UX`

## Sprint: Combat Sandbox - domkniecie vertical slice

Zamkniety: zobacz sekcje `Done`.

## Git checkpoints

- Commit po zamknieciu jednej karty: kod, assety, testy i zmiana dokumentacji stanowia jeden spojny commit.
- Nie commitujemy pojedynczych checklist itemow ani niedzialajacego stanu posredniego.
- Po zakonczeniu milestone'u wykonujemy acceptance pass, push na GitHub i tag wersji.
- Aktualny checkpoint: `v0.1.0-combat-sandbox` po uporzadkowaniu lokalnej nazwy galezi na `main`.
- Kolejny checkpoint: `v0.2.0-gameplay-loop` po zamknieciu Gameplay Loop v1.
