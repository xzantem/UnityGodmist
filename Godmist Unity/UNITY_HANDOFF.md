# Unity handoff

Karta: `Dodac rotacje instancji pokoi v1`

Implementacja jest gotowa do recznego smoke testu w Unity Editorze. Nie uruchamialem Unity Test Runnera ani `Godmist Unity Test Mirror`.

## Co zostalo zaimplementowane

- Generator zapisuje na kazdej instancji pokoju rotacje `0`, `90`, `180` albo `270` stopni.
- Rotacja jest deterministyczna dla tego samego seeda i nie wymaga nowego pola w save data.
- Domyslnie kazdy template ma wszystkie cztery dozwolone rotacje.
- Developer moze ograniczyc rotacje pojedynczego template'u przez liste `Allowed Rotation Degrees`.
- Przy obrocie transformowane sa footprint, rozmiar bounding boxa, structural anchory i kierunki scian/drzwi, legalne krawedzie przejsc, semantic anchory oraz obszary encounterow.
- Generator probuje kolejnych dozwolonych rotacji, gdy dana orientacja nie pozwala znalezc poprawnego miejsca.
- Aktywny katalog katakumb korzysta z jednego poziomego template'u korytarza; pionowy wariant nie jest juz referencjonowany przez katalog, bo poziomy template moze obrocic sie o `90` albo `270` stopni.

## 1. Sprawdzenie konfiguracji template'u

1. Otworz asset `Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates/DRT_Catacombs_Normal_Large.asset`.
2. W Inspectorze znajdz sekcje `Allowed rotations`.
3. Sprawdz, ze lista zawiera domyslnie `0`, `90`, `180`, `270`.
4. Sprawdz ten sam field na template'ach `Start`, `Optional`, `Final` oraz `Corridor`.
5. Otworz asset `Assets/_Project/ScriptableObjects/Dungeons/DTC_Catacombs_V1.asset` i sprawdz, ze katalog nie zawiera pionowego wariantu `DRT_Catacombs_Corridor_V`.

## 2. Rotacja asymetrycznego pokoju

Do testu uzyj `DRT_Catacombs_Normal_Large`, bo ma rozmiar `16x12` i latwo rozpoznac zamiane wymiarow.

1. Pozostaw na template'cie wszystkie cztery dozwolone rotacje.
2. Uruchom generowanie aktywnego lochu katakumb kilka razy z roznymi seedami przez obecny workflow wejscia do lochu.
3. Obejrzyj caly layout pietra, najlepiej z widokiem pozwalajacym porownac szerokosc i wysokosc pokojow.
4. Powtorz generowanie z tym samym seedem.

Oczekiwany rezultat:

- pokoje moga pojawiac sie w orientacji bazowej albo po obrocie o `90`, `180` lub `270` stopni;
- pokoj `16x12` po obrocie o `90`/`270` zajmuje bounding box `12x16`;
- przy tym samym seedzie layout, rotacje, przejscia i content sa takie same;
- pokoje nie nachodza na siebie, a wszystkie ich komorki footprintu sa poprawnie zbudowane;
- rotacja nie tworzy pustych, zduplikowanych ani dodatkowych komorek podlogi.

## 3. Ograniczenie rotacji per template

1. Na kopii albo testowym template ustaw `Allowed Rotation Degrees` tylko na `90`.
2. Wygeneruj kilka layoutow.
3. Sprawdz, ze ten template zawsze ma orientacje `90`, a inne template'y nadal korzystaja ze swoich konfiguracji.
4. Przywroc liste `0`, `90`, `180`, `270` po tescie.

Pusta lista jest traktowana kompatybilnie jako wszystkie cztery obroty.

## 4. Footprint i wklesle krawedzie

1. Uzyj template'u z nieregularnym footprintem, np. `DRT_Catacombs_L`.
2. Pozostaw skonfigurowane legalne krawedzie, w tym krawedzie wklesle.
3. Wygeneruj dungeon z kilkoma seedami.
4. Sprawdz pokoje po obrocie o `90`/`270`.

Oczekiwany rezultat:

- ksztalt L obraca sie razem z pokojem, bez prostokatnego dopelnienia pustki;
- wklesle odcinki pozostaja czescia geometrii pokoju;
- przejscie moze powstac tylko na legalnej krawedzi po przeliczeniu jej pozycji i kierunku;
- zamkniete dziury footprintu nie staja sie przypadkowo przejsciami;
- kazda komorka footprintu pozostaje osiagalna zgodnie z dotychczasowa walidacja generatora.

## 5. Structural anchory, drzwi i przejscia

1. Otworz template zawierajacy structural anchory scian albo drzwi.
2. Wygeneruj layout, w ktorym template wystapi w roznych orientacjach.
3. Sprawdz wizualnie sciany wewnetrzne oraz drzwi structural anchorow.
4. Sprawdz przejscia laczace pokoj z reszta layoutu.

Oczekiwany rezultat:

- anchor sciany obraca swoja komorke i kierunek razem z pokojem;
- anchor drzwi nie wskazuje poza footprint ani do niewlasciwego pokoju;
- przejscia sa otwierane w komorkach zgodnych z obrocona geometria;
- nie pojawiaja sie sciany blokujace jedyne legalne przejscie.

## 6. Semantic anchory, obiekty i encountery

1. Uzyj template'u zawierajacego semantic anchory skrzyni, przeszkody, pulapki, levera albo condition.
2. Wygeneruj pokoj w kilku orientacjach.
3. Sprawdz polozenie obiektu wzgledem scian i footprintu.
4. Dla anchoru obszarowego, np. o rozmiarze `2x1`, sprawdz po obrocie, ze obszar staje sie `1x2` i nadal miesci sie w pokoju.
5. Sprawdz encounter w obroconym pokoju oraz jego exploration position.

Oczekiwany rezultat:

- content pozostaje w tej samej pozycji wzglednej do obroconego pokoju;
- anchor nie przenosi sie losowo do innej czesci pokoju;
- obiekty nie spawnuja sie w scianach ani poza footprintem;
- encounter obejmuje obrocony bounding box pokoju, a preferowana komorka znajduje sie na podlodze tego pokoju.

## 7. Korytarz

1. Sprawdz, ze katalog zawiera tylko `DRT_Catacombs_Corridor_H` jako template typu `Corridor`.
2. Wygeneruj kilka layoutow.
3. Sprawdz korytarze laczace pokoje w osi poziomej i pionowej.

Oczekiwany rezultat:

- poziomy template moze zostac obrocony do orientacji pionowej;
- nie ma dwoch niezaleznych, dublujacych sie definicji korytarza;
- polaczenia do korytarzy nadal maja poprawna geometrie i sa przechodnie.

## Testy automatyczne do recznego uruchomienia

W Unity Test Runnerze uruchom:

- `DungeonRoomTransformUtilityTests`;
- dotychczasowe `DungeonRoomFootprintTests`;
- `DungeonSemanticAnchorTests`;
- `DungeonObjectPlacementSystemTests`;
- `DungeonEncounterPlacementSystemTests`;
- testy generatora i reachability.

Po smoke tescie nie oznaczaj karty jako zakonczonej, jesli pojawi sie problem z konkretnym seedem, footprintem, anchorem albo przejsciem — podaj seed i screenshot, a poprawie implementacje przed commitem.
