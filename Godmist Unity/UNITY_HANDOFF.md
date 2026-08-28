# Unity handoff

Karta: `Rozszerzyc DungeonRoomEditor o legalne krawedzie przejsc i resize footprintu v1`

Kod jest przygotowany do recznego smoke testu w Unity Editorze. Nie uruchamialem Unity Test Runnera ani test mirroru.

## Otworzenie edytora

1. Wybierz room template, np. `Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates/DRT_Catacombs_Normal_Small.asset`.
2. W Inspectorze kliknij `Open Dungeon Room Editor` albo otworz `Godmist -> Dungeons -> Dungeon Room Editor`.

## Legalne krawedzie przejsc

1. W trybie `Footprint` utworz prosty pokoj L albo C, usuwajac wybrane komorki podlogi.
2. Przelacz `Edit mode` na `Boundary edges`.
3. Sprawdz, ze edytor pokazuje krawedzie zewnetrzne na zielono albo czerwono.
4. Kliknij pojedyncza krawedz w zwyklej czesci pokoju i pojedyncza krawedz we wkleslej wnece.
5. Sprawdz licznik `Allowed edges` i ponownie kliknij krawedz, aby ja wylaczyc.
6. Uzyj `Enable all legal edges`, a potem `Disable all edges`.
7. Zapisz template, zamknij edytor i otworz go ponownie.

Oczekiwany rezultat:

- kazdy odcinek komorki podlogi sasiadujacy z pustka otwarta na zewnatrz footprintu jest osobnym kandydatem przejscia;
- wklesle krawedzie L albo C sa widoczne i edytowalne;
- zamkniety otwor w ksztalcie O nie jest traktowany jako zewnetrzna krawedz;
- stan legalnosci krawedzi zachowuje sie po ponownym otwarciu assetu;
- walidator blokuje nieprawidlowa lub zduplikowana krawedz.

## Resize footprintu

1. W sekcji `Resize` uzyj po kolei `Up +`, `Down +`, `Left +` oraz `Right +`.
2. Sprawdz, ze rozmiar zmienia sie o jedna komorke, a istniejace pustki pozostaja pustkami.
3. Dodaj semantic i structural anchor, powieksz pokoj z kazdej strony i sprawdz, ze anchory zachowuja swoje polozenie wzgledem footprintu.
4. Sprobuj zmniejszyc pokoj od strony zawierajacej anchor.

Oczekiwany rezultat:

- resize nie usuwa ani nie przesuwa anchorow w nieprzewidziany sposob;
- zmniejszenie usuwajace anchor jest zablokowane komunikatem;
- nowe krawedzie powstale przez powiekszenie pozostaja domyslnie wylaczone;
- zapis poprawnego template'u dziala, a zapis niepoprawnego pozostaje zablokowany.

## Sprawdzenie generatora

1. Zostaw w room template tylko jeden lub kilka legalnych odcinkow na zewnetrznej granicy albo we wkleslej wnece.
2. Wygeneruj dungeon z aktywnym `DungeonDefinition`.
3. Sprawdz, ze przejscia sa tworzone tylko na wlaczonych odcinkach i ze ich szerokosc wymaga ciaglego zestawu wlaczonych krawedzi.

## Testy automatyczne

Do recznego uruchomienia w Unity Test Runnerze:

- `DungeonRoomTemplateEditorValidatorTests` — footprint, legalne krawedzie, wklesla krawedz, zamkniety otwor i walidacja anchorow;
- dotychczasowe testy `DungeonRoomFootprintTests`, `DungeonSemanticAnchorTests` i testy generatora.
