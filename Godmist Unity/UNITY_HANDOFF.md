# Unity handoff

Karta: `[P1] Dodac anchory pojedynczych przeciwnikow w pokojach v1`

Implementacja jest gotowa do recznego smoke testu w Unity Editorze. Nie uruchamialem Unity Test Runnera ani `Godmist Unity Test Mirror`.

## Cel karty

Room template moze teraz definiowac pozycje pojedynczych przeciwnikow nalezacych do encounteru. Anchor nie wskazuje typu przeciwnika ani konkretnego assetu encounteru. Wskazuje indeks wpisu na liscie `EncounterDefinition.Enemies`.

Zakres obejmuje tylko poczatkowe rozmieszczenie przeciwnikow w eksploracji. Nie zmienia logiki FOV, poscigu, rozpoczecia walki ani pozniejszego ruchu przeciwnikow.

## Przygotowanie danych

1. Otworz `EncounterDefinition`, ktory jest uzywany przez aktywny `DungeonDefinition`.
2. Sprawdz kolejnosc wpisow na jego liscie `Enemies`. Indeks zaczyna sie od zera: pierwszy wpis ma indeks `0`, drugi `1` itd.
3. Otworz room template uzywany przez jeden z pokojow, w ktorym moze pojawic sie encounter.
4. W sekcji semantic anchors dodaj anchor i ustaw:
   - `Type` = `Enemy`;
   - `Local Cell` = komorka, w ktorej ma stac konkretny przeciwnik;
   - `Encounter enemy index` = indeks wpisu z `EncounterDefinition.Enemies`.
5. Marker typu `Enemy` jest oznaczony na siatce litera `E`. Anchor zajmuje zawsze jedna komorke. Pola `Area Size`, `Spawn Chance`, `Is Required`, `Requires Anchor Id` i `Activation group` nie steruja anchorami przeciwnikow.
6. Zapisz asset. Walidacja room template'u blokuje zapis, jezeli anchor ma pusty cel, nieprawidlowy rozmiar, zduplikowany cel, zduplikowana komorke albo kolizje z innym semantic anchorem.

## Test 1: dwa konkretne cele

1. Przygotuj encounter z co najmniej dwoma wpisami o roznych `InstanceId`.
2. W room template ustaw dwa anchory `Enemy` na dwoch roznych komorkach, przypisujac im odpowiednio indeks `0` i `1`.
3. Upewnij sie, ze ten template jest w aktywnym `DungeonRoomTemplateCatalog`, a encounter w aktywnym `DungeonDefinition`.
4. Uruchom dungeon z ustalonym seedem i wejdz do pokoju zawierajacego encounter.

Oczekiwany rezultat:

- pierwszy przeciwnik z listy `Enemies` stoi dokladnie na komorce pierwszego anchora;
- drugi przeciwnik z listy `Enemies` stoi dokladnie na komorce drugiego anchora;
- pozycja nie jest wybierana na podstawie typu przeciwnika ani konkretnego `InstanceId`;
- obie komorki pozostaja zarezerwowane dla przeciwnikow i nie pojawi sie na nich skrzynia, pulapka, obstacle, lever ani condition;
- zachowanie FOV, poscigu i rozpoczecia walki pozostaje takie jak przed ta karta.

## Test 2: wiecej przeciwnikow niz anchorow

1. Zostaw w template tylko jeden albo dwa anchory `Enemy`.
2. Uzyj encounteru z wieksza liczba poprawnych, zywych wpisow przeciwnikow.
3. Uruchom dungeon.

Oczekiwany rezultat:

- przeciwnicy z pasujacymi indeksami sa ustawieni na swoich anchorach;
- pozostali przeciwnicy sa rozmieszczani przez dotychczasowy fallback w poprawnych komorkach pokoju;
- fallback nie nadpisuje pozycji przypisanych anchorami i nie stawia przeciwnika poza podloga pokoju.

## Test 3: brakujacy albo pokonany cel

Sprawdz osobno:

- anchor wskazujacy indeks wiekszy lub rowny liczbie wpisow w encounterze;
- anchor wskazujacy wpis oznaczony jako pokonany;
- encounter bez zadnego pasujacego anchora.

Oczekiwany rezultat:

- brakujacy cel jest ignorowany bez wyjatku;
- pokonany przeciwnik nie jest respawnowany;
- pozostali przeciwnicy nadal korzystaja z normalnego fallbacku.

## Test 4: rotacja room template'u

1. Dla template'u dopusc 0, 90, 180 i 270 stopni.
2. Uruchom dungeon kilka razy z seedem, ktory ustawia rozne rotacje tego pokoju.

Oczekiwany rezultat:

- anchor pozostaje w tej samej pozycji lokalnej template'u;
- jego pozycja w swiecie obraca sie razem z pokojem;
- przeciwnik nadal trafia do konkretnego `InstanceId`, a nie do konkretnej pozycji z poprzedniej rotacji.

## Test 5: walidacja blokujaca zapis

Sprawdz, ze zapis room template'u jest blokowany dla kazdego przypadku:

- ujemny `Encounter enemy index`;
- dwa anchory wskazujace ten sam indeks;
- dwa anchory wskazujace te sama komorke;
- `Area Size` inne niz `1 x 1`;
- anchor `Enemy` nachodzacy na inny semantic anchor.

Po poprawieniu danych asset powinien dac sie zapisac bez bledu walidacji.

## Test regresji

Sprawdz, ze:

- encounter bez anchorow przeciwnikow nadal dziala jak dotychczas;
- pojedynczy anchor nie zmienia liczby przeciwnikow w encounterze;
- dwa pokoje uzywajace tego samego room template'u nie dziela pozycji ani rezerwacji;
- semantic anchory skrzyn, pulapek, obstacle, leverow i conditionow nadal dzialaja;
- kotwy przeciwnikow nie sa traktowane jako obiekty do placementu.

## Testy automatyczne do recznego uruchomienia

Po smoke tescie uruchom w Unity Test Runnerze:

- `DungeonRoomTemplateEditorValidatorTests`;
- `DungeonEncounterPlacementSystemTests`;
- `DungeonSemanticAnchorTests`;
- `DungeonObjectPlacementSystemTests`;
- pozostale testy dungeon runtime.

Jesli pojawi sie regresja, podaj seed, `EncounterDefinition.Id`, indeks przeciwnika, room template oraz screenshot z pozycjami anchorow i przeciwnikow.
