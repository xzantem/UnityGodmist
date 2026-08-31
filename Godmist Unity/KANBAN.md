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
- `In Progress`
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

### Milestone: Dungeon Content and Layout v2

Nastepny milestone po `Combat v3`. Nie przebudowuje istniejacego loopu wejscie -> eksploracja -> wyjscie; rozwija go o content, decyzje i alternatywne trasy.

### Milestone: Crafting, Upgrading and Consumables v1

Planowany po `Dungeon Content and Layout v2`. NPC, dialogi, questy i handel maja juz fundamenty v1; ten milestone rozszerza konkretne uslugi miejskie zamiast budowac ogolne `NPC v2` od zera.

- `[P1] Dodac ulepszanie quality i rarity equipmentu u kowala v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Design / Systems`, `UI / UX`
  - Uzywa ustalonych kosztow i szans zmiany unikalnosci; pokazuje koszt, ryzyko i wynik przed potwierdzeniem.
- `[P1] Dodac consumables: potiony, bomby i return scroll v1` - `Items / Inventory / Economy`, `Combat`, `World / Dungeon / Exploration`, `Content / Data`
  - Pierwsze dzialajace itemy zuzywalne, ich uzycie, ograniczenia i persistence. Return scroll realizuje ustalone wyjscie do miasta.
- `[P2] Dodac crafting bizuterii oraz galduryty/runy v2` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `Design / Systems`
  - Osobny model po pierwszym passie dropow bizuterii; nie zakladac automatycznie modelu trzech czesci dla bizuterii.
- `[P1] Crafting, Upgrading and Consumables v1 - acceptance pass` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Combat`, `Tools / Editor / Debug`
  - Pokryc crafting, upgrade, storage, consumables i save/load, wlacznie z zachowaniem instanceId oraz walidacja kosztow.

### Milestone: Developer Workflow and Content Authoring v1

Planowany bezpośrednio po `Crafting, Upgrading and Consumables v1`. Skraca iterację contentową: rozwija konsolę developerską, tworzenie danych i walidację assetów. Nie dodaje gameplayu, ale pozwala szybciej budować późniejsze skille, itemy, przeciwników, loot table i crafting z arkuszy.

- `[P1] Rozszerzyć nawigację rekomendacji konsoli v1` - `Tools / Editor / Debug`, `UI / UX`
  - Konsola pokazuje nadal pięć pozycji naraz, ale scroll kółkiem lub klawiszami góra/dół porusza się po pełnej liście legalnych rekomendacji. Zmiana komendy lub argumentu resetuje zaznaczenie; Enter wybiera aktualną pozycję.
- `[P1] Dodać schematy komend i opcjonalne argumenty konsoli v1` - `Tools / Editor / Debug`, `Core / Architecture`, `UI / UX`
  - Każda komenda deklaruje wymagane i opcjonalne argumenty, walidację oraz kontekstowe sugestie. Przykład: `createitem <itemId> [amount] [characterId]`. `help` wyświetla jednoznaczną składnię i przykłady.
- `[P1] Rozszerzyć zestaw komend content/debug v1` - `Tools / Editor / Debug`, `Items / Inventory / Economy`, `NPC / Quest / Dialogue`
  - Ujednolicić istniejące komendy oraz dodać te potrzebne do testowania contentu: przekazanie itemów do wybranej postaci, złoto, XP, inspekcja inventory, stan questa/flag i wygodne uruchamianie testowego encountera. Każda zmiana runtime ma czytelny raport zwrotny.
- `[P1] Dodać Content Authoring Window dla itemów i equipmentu v1` - `Tools / Editor / Debug`, `Content / Data`, `Items / Inventory / Economy`
  - Jedno okno Editorowe z wyszukiwaniem, filtrowaniem i walidacją dla itemów, baz equipmentu, części oraz materiałów. Ułatwia tworzenie/duplikację assetów, ustawienie klasy, bracketu, statystyk, receptury i przypisanie do katalogu contentu.
- `[P1] Dodać import-preview danych z arkuszy contentowych v1` - `Tools / Editor / Debug`, `Content / Data`
  - Narzędzie czyta wybrany arkusz lub eksport CSV, pokazuje preview planowanych zmian i waliduje je przed zapisem. Nie nadpisuje assetów automatycznie: pokazuje tworzone, aktualizowane oraz błędne rekordy.
- `[P1] Dodać walidator contentu i raport referencji v1` - `Tools / Editor / Debug`, `Content / Data`
  - Jedno polecenie Editorowe sprawdza duplikaty ID, brakujące referencje katalogu, niezgodną klasę bazy/części, złe receptury oraz martwe assety. Błędy prowadzą bezpośrednio do assetu.
- `[P1] Dodać Game Defines Window v1` - `Tools / Editor / Debug`, `Core / Architecture`, `Design / Systems`, `Content / Data`
  - Jeden jawny katalog parametrów gameplayowych dostępny z Unity Editora: ekonomia, combat, itemy, crafting, upgrade/reforge, progresja oraz eksploracja. Każdy parametr ma nazwę, opis, jednostkę, zakres i wartość domyślną; przykłady: AP na punkt Speed, mnożnik ceny reforgingu i krzywe kosztu upgrade'u.
  - Runtime odczytuje konfigurację przez jeden serwis/asset konfiguracji zamiast rozproszonych magic numbers. Narzędzie waliduje zakresy, brakujące definicje i nie pozwala zapisać nielegalnych wartości; aktualizacja parametrów nie miesza się z mutable save state.
- `[P1] Developer Workflow and Content Authoring v1 — acceptance pass` - `Tools / Editor / Debug`, `Content / Data`, `UI / UX`
  - Ręczna weryfikacja pełnego flow: rekord w arkuszu → preview importu → asset → walidacja → `createitem` dla wybranej postaci → test w grze.

### Milestone: Combat Passives and Affixes v1

Planowany po `Crafting, Upgrading and Consumables v1`. Wspolny model dla pasywek drzewek skilli, klejnotow i unikatowego equipmentu; rozwija warunkowe efekty walki bez mieszania ich z systemem craftingu.

- `[P1] Ujednolicic framework pasywek dla equipmentu i drzewek skilli v1` - `Combat`, `Items / Inventory / Economy`, `Core / Architecture`, `UI / UX`
  - Wspolna definicja zrodla, warunkow i efektow pasywki dla drzewek skilli, klejnotow oraz unikatowego equipmentu. Obsluzyc stale modyfikatory statystyk i odpornosci, modyfikatory obrazen, XP oraz zlota, z pelnym persistence i tooltipem. Zachowac zgodnosc z istniejacymi globalnymi pasywkami drzewek; migracje wykonywac etapowo.
- `[P1] Dodac tagi przeciwnikow i warunkowe modyfikatory v1` - `Combat`, `Content / Data`, `Core / Architecture`
  - Dodac tagi `Human`, `Beast`, `Undead` i `Demon` na definicjach przeciwnikow oraz warunki pasywek dla typu celu i theme lochu. Uzyc ich m.in. dla obrazen przeciwko okreslonym przeciwnikom i bonusow terenowych klejnotow.
- `[P1] Dodac unikatowe dropy bossow broni i pancerzy per klasa v1` - `Combat`, `Items / Inventory / Economy`, `Content / Data`
  - Boss ma `30%` szansy na dowolny unikat broni albo pancerza: po `7,5%` na kazda z czterech klas. Unikat losuje normalne czesci equipmentu, ale otrzymuje specjalna pasywke przypisana bossowi zamiast pasywy generycznej.
- `[P1] Dodac pierwszy pass wykonawczy pasywek node'ow drzewek skilli v1` - `Combat`, `Skills / Progression`, `Content / Data`
  - Wykorzystac wspolny framework dla reprezentatywnych node'ow Scouta: warunki po ruchu, krytyku, uniku/chybieniu, statusie celu i aktywnym buffie oraz modyfikacje parametrow konkretnej umiejetnosci.
- `[P2] Rozszerzyc content klejnotow i unikatow bossowych v1` - `Items / Inventory / Economy`, `Combat`, `Content / Data`
  - Dodac pozostale standardowe klejnoty z arkusza, bossowe pasywy dla kolejnych theme'ow oraz ich zrodla/dropy. Zachowac model metal + klejnot dla zwyklej bizuterii i losowy metal + pasywa bossa dla unikatu.
- `[P1] Combat Passives and Affixes v1 - acceptance pass` - `Combat`, `Items / Inventory / Economy`, `Skills / Progression`, `Tools / Editor / Debug`
  - Zweryfikowac warunki, stackowanie, tooltipy, save/load oraz wspoldzialanie pasywek z drzewek, bizuterii i unikatowego equipmentu.

### Milestone: City Progression and Specialist Services v1

Planowany po `Combat Passives and Affixes v1`. Domyka miejski loop progresji oraz rozwija specjalistyczne uslugi NPC bez mieszania ich z kolejnym passsem walki lub lochow.

- Loyalty NPC, progi uslug, rabaty i trwały progress wydatkow.
- Alchemik, rune-maker i pozostale planowane uslugi specjalistyczne.
- Crafting półproduktów kowalskich oraz rozróżnienie zastosowań materiałów dla specjalistów.
- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

### Milestone: Combat v4 — Arena, Obstacles and Transition v1

Planowany po `City Progression and Specialist Services v1`. Rozwija prezentację i taktykę areny walki oraz poprawia przejście eksploracja → walka, nie rozszerzając jeszcze zachowań przeciwników w lochu.

- Pułapki bojowe, zewnętrzne ściany areny i inne przeszkody taktyczne.
- Scrollowana kamera oraz czytelna prezentacja większych aren.
- Lepsza integracja wejścia z eksploracji do walki, w tym ustalone przenoszenie pozycji i formacji.
- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

### Milestone: Dungeon Exploration v3 — Threats and Traversal v1

Planowany po `Combat v4 — Arena, Obstacles and Transition v1`. Dodaje aktywne zagrożenia i możliwości przemieszczania w lochu, wykorzystując ustalone zachowania walki i miasta.

- Indywidualne rozmieszczenie przeciwników, FOV, pościg oraz zasadzki i niewidzialne encountery.
- Pathfinding sterowany myszką, skakanie oraz nowe typy pułapek eksploracyjnych.
- Prawdziwe zagadki logiczne i zręcznościowe z czytelnym stanem, persistence i konsekwencjami.
- Objętościowo: przed rozpoczęciem zapewnić porównywalny zakres pracy, contentu, testów i acceptance passu względem poprzednich milestone'ów. Jeżeli sam zakres systemowy okaże się zbyt mały, dołączyć pasujący content tej kategorii zamiast sztucznie mnożyć karty.

## In Progress

### Milestone: Crafting, Upgrading and Consumables v1

- `[P2] Przenieść handel NPC do UI Workspace Services v1` - `NPC / Quest / Dialogue`, `UI / UX`, `Items / Inventory / Economy`
  - Zastąpić legacy `TradePanelView` zakładką sklepu we wspólnym UI Workspace usług NPC. Karta osobna po craftingu półproduktów; obejmuje dopiero wtedy migrację istniejącego handlu, buybacku i jego smoke test.

### Karty do decyzji o przypisaniu do milestone'u

- `[Do decyzji] Dodac indywidualne rozmieszczenie przeciwnikow, FOV i poscig w lochu v2` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`, `Design / Systems`
  - Przeciwnicy sa rozmieszczani indywidualnie albo wedlug autorskiego ukladu pokoju; wejscie gracza w FOV przeciwnika uruchamia poscig, a walka zaczyna sie po jego dogonieniu. Bez jednego wspolnego anchoru dla calej grupy.
  - Przypisanie do milestone'u oraz priorytet do ustalenia.
- `[Do decyzji] Dodac pathfinding ruchu sterowanego myszka w eksploracji lochu v1` - `World / Dungeon / Exploration`, `UI / UX`, `Core / Architecture`
  - Klikniecie walkable celu wyznacza sciezke po mapie lochu i prowadzi druzyne po kolejnych komorkach z uwzglednieniem przeszkod, scian i zamknietych przejsc.
  - Przypisanie do milestone'u oraz priorytet do ustalenia; istnieje starsza karta o klikanym ruchu po gridzie, ktora nalezy traktowac jako jej poprzednia wersje.
- `[Do decyzji] Rozszerzyc blokowanie layoutu pokoi z bounding boxa na rzeczywisty footprint v2` - `World / Dungeon / Exploration`, `Core / Architecture`, `Design / Systems`
  - Generator ma dopuszczac lepsze upakowanie nieregularnych pokoi, blokujac rzeczywiste zajete komorki zamiast calego prostokatnego bounding boxa; uwzglednic dziury, sasiedztwo pokoi i kompatybilne przejscia.
  - Karta odlozona poza pierwszy pass nieregularnych footprintow; przypisanie do milestone'u oraz priorytet do ustalenia.

## Done

### Milestone: Crafting, Upgrading and Consumables v1

- `[P1] Dodac storage w Arungardzie v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `UI / UX`, `Core / Architecture`
  - Zrealizowano trwały, bezlimitowy magazyn kampanii w Arungardzie wraz z persistence save/load. Skrzynia otwiera osobne okno UI Workspace; obsługuje drag-and-drop całych stosów, Ctrl+drag jednej sztuki, PPM do pobrania/odłożenia i bezpośrednie zakładanie equipmentu przez aktywnego lidera.

- `[P2] Dodac crafting półproduktów materiałów v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`, `Core / Architecture`
  - Zrealizowano ogólny `CraftableItemDefinition`, z recepturami na stackowalne półprodukty oraz `MaterialDiscipline` (`Smithing`, `Alchemy`, `Runic`), zachowując wspólny `ItemType.Material`. Kowal v1 korzysta wyłącznie z materiałów `Smithing`.
  - Dodano startowy content kowalski: sztabki żelaza, łańcuchy, blachy, wyprawioną skórę i paski, deski i patyki modrzewiowe, igły, nitki oraz tkaninę bawełnianą — zgodnie z ustalonymi recepturami.
  - Zrealizowano rozszerzalne UI Workspace `Usługi NPC` z zakładkami craftingu equipmentu i półproduktów. Koszty agregują inventory aktywnej drużyny, wynik trafia do lidera, a brakujące składniki można kupić z oferty kowala.

- `[P1] Dodac crafting broni i pancerzy z czesci oraz materialow v1` - `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `Content / Data`, `UI / UX`
  - Zrealizowano craft u kowala z wyborem bazy i trzech czesci, materialami agregowanymi z aktywnej druzyny, preview, zakupem brakujacego materialu z oferty kowala oraz utrwaleniem wybranej jakosci i nazwy. Generator runtime pozostaje ownerem payloadu.

### Milestone: Dungeon Content and Layout v2

- `[P1] Dungeon Content and Layout v2 - acceptance pass` - `World / Dungeon / Exploration`, `Combat`, `Items / Inventory / Economy`, `Tools / Editor / Debug`
  - Zaliczone: layout z petlami i gate'ami, nieregularne footprinty, ciala i persistence, lootbagi, boss, biżuteria oraz przejscia miedzy pietrami przeszly review kodu i reczna weryfikacje w Unity.

- `[P1] Dodac przejscia miedzy pietrami lochu v1` - `World / Dungeon / Exploration`, `Core / Architecture`, `UI / UX`
  - Zrealizowano dwukierunkowe przejscia miedzy pietrami, zachowanie formacji oraz stanu pieter. Tylko Final ostatniego pietra konczy loch i wymaga pokonania przypisanego bossa.

- `[P1] Zachowac stan czesciowo pokonanego encountera po ucieczce v1` - `Bug`, `Combat`, `World / Dungeon / Exploration`, `Core / Architecture`
  - Zrealizowane wczesniej: pokonani przeciwnicy, ich ciala oraz stan encountera sa zachowane po ucieczce i save/load.

- `[P1] Dodac testowy content bizuterii do lootbagow v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX`
  - Zrealizowano model metal + kamien, cztery sloty, persistence oraz testowe dropy zwykle i cztery niezalezne 5% dropy bossa Katakumb. Generator trzech czesci pozostaje wylacznie dla broni i pancerzy.

- `[P1] Dodac UI skrzynek oraz zamki, klucze i lockpicking v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`, `Content / Data`

- `[P1] Dodac boss encountery i boss lootbagi v1` - `Combat`, `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `Content / Data`
  - Boss jest gwarantowany albo zalezy od trudnosci i trafia blisko konca lochu. Boss lootbag ma wyzsza szanse na unikaty. Pierwszy pass nie tworzy nowego frameworku faz bossa, skryptow areny ani specjalnego AI.

- `[P1] Dodac lootbagi oraz theme i level-bracket loot tables v1` - `Items / Inventory / Economy`, `World / Dungeon / Exploration`, `Content / Data`
  - Zwykly loot z przeciwnikow i skrzyn korzysta z lootbagow; ich zawartosc zalezy od motywu lochu oraz przedzialu poziomu. Equipment i potiony nie dropia bezposrednio ze standardowych przeciwnikow.

- `[P1] Dodac anchory pojedynczych przeciwnikow w pokojach v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`

- `[P1] Zablokowac interakcje z obiektami przez sciane v1` - `World / Dungeon / Exploration`, `UI / UX`, `Core / Architecture`

- `[P1] Dodac rotacje instancji pokoi v1` - `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`

- `[P1] Dodac petle grafu lochu i wymagane odnogi progresji v2` - `World / Dungeon / Exploration`, `Core / Architecture`, `Design / Systems`

- `[P2] Dodac DungeonRoomEditor v1` - `Tools / Editor / Debug`, `World / Dungeon / Exploration`, `Content / Data`

- `[P1] Dopracowac persistence cial przeciwnikow: pozycja smierci v2` - `World / Dungeon / Exploration`, `Combat`, `Core / Architecture`

- `[P1] Dodac semantic content anchors i recznie projektowane sytuacje pokoi v2` - `World / Dungeon / Exploration`, `Content / Data`, `Design / Systems`

- `[P1] Dodac nieregularne ksztalty pokoi lochu v2` - `World / Dungeon / Exploration`, `Content / Data`, `Design / Systems`

- `[P1] Dodac pulapki, przeszkody, klucze/dzwignie i proste zagadki lochowe v1` - `World / Dungeon / Exploration`, `Combat`, `Content / Data`, `UI / UX`

- `[P2] Rozszerzyc DungeonRoomEditor o legalne krawedzie przejsc i resize footprintu v1` - `Tools / Editor / Debug`, `World / Dungeon / Exploration`, `Content / Data`, `Core / Architecture`

### Milestone: Save and Load v1

- `[P1] Dodac GameSaveData DTO i JSON serialization v1` - `Core / Architecture`
- `[P1] Dodac snapshoty runtime dla postaci, inventory i itemow v1` - `Core / Architecture`, `Items / Inventory / Economy`
- `[P1] Zapisywac questy, world flags oraz stan NPC v1` - `Core / Architecture`, `NPC / Quest / Dialogue`
- `[P1] Dodac GameContentCatalog v1` - `Core / Architecture`, `Content / Data`
- `[P1] Dodac API restore dla Quest i Dungeon runtime v1` - `Core / Architecture`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac GameSession restore factory v1` - `Core / Architecture`, `Content / Data`, `Items / Inventory / Economy`, `NPC / Quest / Dialogue`, `World / Dungeon / Exploration`
- `[P1] Dodac SaveManager oraz manual save/load v1` - `Core / Architecture`, `UI / UX`
- `[P1] Zapisywac party, inventory, quest/world flags i dungeon deltas v1` - `Core / Architecture`, `World / Dungeon / Exploration`
- `[P2] Dodac autosave po wejsciu/wyjsciu z lochu i po ukonczeniu questa v1` - `Core / Architecture`
- `[P1] Dodac CampaignId i strukture katalogow zapisow v1` - `Core / Architecture`
- `[P1] Dodac liste oraz zarzadzanie zapisami kampanii v1` - `Core / Architecture`, `UI / UX`
- `[P1] Dodac menu pauzy i UI zapisow v1` - `UI / UX`, `Core / Architecture`
- `[P1] Save and Load v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`

### Milestone: Combat Core v2 — Closed

- `[P1] Combat Core v2 - acceptance pass` - `Combat`, `Tools / Editor / Debug`, `UI / UX`

### Milestone: Combat v3 — Closed

- `[P1] Combat v3 - acceptance pass` - `Combat`, `UI / UX`, `Core / Architecture`, `Tools / Editor / Debug`

- `[P2] Zrefaktoryzowac payloady PassiveEffectRuntime v1` - `Core / Architecture`, `Combat`, `Design / Systems`

- `[P1] Combat v3 - tooltipy, inspect i feedback bojowy v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P1] Combat v3 - penetracja oraz DealDamage z penetracja` - `Combat`, `Core / Architecture`, `Content / Data`, `Design / Systems`

- `[P1] Combat v3 - rozszerzyc targetowanie` - `Combat`, `Core / Architecture`, `Content / Data`

- `[P1] Combat v3 - rzeczywista sciezka ruchu AI i preview kosztu v1` - `Combat`, `UI / UX`

- `[P1] Combat v3 - czytelnosc feedbacku i sekwencjonowanie akcji v1` - `UI / UX`, `Combat`

- `[P1] Combat v3 - dzialalnosc skilli toggle` - `Combat`, `Core / Architecture`, `UI / UX`, `Content / Data`
- `[P1] Combat v3 - mechanika sztyletow Scouta` - `Combat`, `Content / Data`, `Design / Systems`

- `[P1] Combat v3 - rozszerzyc execution pipeline skilli` - `Combat`, `Core / Architecture`, `Content / Data`, `Design / Systems`

### Milestone: UI Workspace v1 — Closed

- `[P2] Dodac glossary i linki do UI Workspace tooltip v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P2] Ujednolicic stack count i zloto w slotach UI Workspace v1` - `UI / UX`, `Items / Inventory / Economy`, `Core / Architecture`

- `[P1] UI Workspace v1 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`, `Items / Inventory / Economy`, `UI / UX`
- `[P1] Podpiac generowany equipment do LootTable i tooltipow v1` - `Items / Inventory / Economy`, `Content / Data`, `UI / UX`
- `[P1] Dodac floating loot window ciala v1` - `UI / UX`, `Items / Inventory / Economy`, `World / Dungeon / Exploration`, `Core / Architecture`

- `[P1] Przeniesc drzewka umiejetnosci do UI Workspace i dopracowac UX v2` - `UI / UX`, `Design / Systems`, `Core / Architecture`

- `[P1] Przeniesc skill loadout do UI Workspace v1` - `UI / UX`, `Combat`, `Core / Architecture`

- `[P1] Dodac wspolne tooltipy UI Workspace v1` - `UI / UX`, `Core / Architecture`, `Items / Inventory / Economy`, `Combat`

- `[P1] Dodac transfer drag-and-drop miedzy Character Info v1` - `UI / UX`, `Items / Inventory / Economy`, `Core / Architecture`

- `[P1] Dodac Character Info floating window v1` - `UI / UX`, `Core / Architecture`, `Design / Systems`

- `[P1] Dodac fundament floating windows v1` - `UI / UX`, `Core / Architecture`

- `[P1] Ustandaryzowac design system i template paneli v1` - `UI / UX`, `Core / Architecture`, `Tools / Editor / Debug`

- `[P1] Rewards and Progression v2 - acceptance pass` - `Tools / Editor / Debug`, `Core / Architecture`, `Combat`, `Items / Inventory / Economy`, `UI / UX`

- `[P1] Dodac generowanie broni i pancerzy z czesci v1` - `Items / Inventory / Economy`, `Content / Data`, `Design / Systems`
- `[P1] Dodac runtime oraz zapis inwestycji w skill tree v1` - `Core / Architecture`, `Content / Data`, `Design / Systems`
- `[P1] Dodac trwale ciala i interakcje z lootem w lochu v1` - `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `UI / UX`, `Core / Architecture`
- `[P1] Rozbic combat rewards na indywidualny loot przeciwnikow v1` - `Combat`, `World / Dungeon / Exploration`, `Items / Inventory / Economy`, `Core / Architecture`
- `[P1] Dodac modal loadoutu i zamiane aktywnych skilli v1` - `UI / UX`, `Combat`
- `[P1] Dodac skalowalny model loadoutu aktywnych skilli v1` - `Core / Architecture`, `Combat`, `Design / Systems`
- `[P1] Dodac UI progresji i wydawania punktow v1` - `UI / UX`, `Core / Architecture`, `Design / Systems`
- `[P1] Dodac GrowthData klas i automatyczny wzrost statystyk v1` - `Core / Architecture`, `Content / Data`, `Design / Systems`
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
- `[P2] Doprecyzować uproszczone zasady statusu Bleed v1` — `Design / Systems`
- `[P1] Dodać debug button Start Combat` — `Tools / Editor / Debug`
- `[P1] Przygotować bazowy bootstrap i game flow` — `Core / Architecture`
- `[P1] Utworzyć scenę Bootstrap` — `Core / Architecture`
- `[P2] Dodać inspect celu v1` — `UI / UX`
- `[P2] Dodać highlight celu umiejętności` — `UI / UX`
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

## Git checkpoints

- Commit po zamknieciu jednej karty: kod, assety, testy i zmiana dokumentacji stanowia jeden spojny commit.
- Nie commitujemy pojedynczych checklist itemow ani niedzialajacego stanu posredniego.
- Po zakonczeniu milestone'u wykonujemy acceptance pass, push na GitHub i tag wersji.
- Aktualny checkpoint: `v0.1.0-combat-sandbox` po uporzadkowaniu lokalnej nazwy galezi na `main`.
- Kolejny checkpoint: `v0.2.0-gameplay-loop` po zamknieciu Gameplay Loop v1.
