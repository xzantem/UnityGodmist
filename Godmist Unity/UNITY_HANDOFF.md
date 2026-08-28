# Unity handoff

Karta: `[P1] Dodac UI skrzynek oraz zamki, klucze i lockpicking v1`

Implementacja jest gotowa do recznego smoke testu w Unity Editorze. Nie uruchamialem Unity Test Runnera ani `Godmist Unity Test Mirror`.

## Co zostalo zaimplementowane

- Skrzynia korzysta z tego samego okna lootu co cialo przeciwnika.
- Loot ze skrzyni jest odbierany pojedynczo bezposrednio do inventory aktywnej postaci.
- Skrzynia pozostaje w lochu po otwarciu i po oproznieniu.
- Puste okno pokazuje komunikat `Skrzynia jest pusta.`.
- Skrzynia moze byc otwarta, wymagac konkretnego klucza albo wymagac klucza lub lockpicka.
- Klucz jest dopasowywany po konkretnym `ItemDefinition.Id`, wiec nie otwiera wszystkich skrzyn.
- Zuzywany jest jeden klucz albo jeden lockpick. Przedmioty moga znajdowac sie w inventory dowolnego aktywnego czlonka druzyny.
- Lockpick w v1 zawsze dziala i nie ma statystyk, szansy ani minigry.
- Dodano `ItemType.Tool` oraz testowe assety `ID_TestKey` i `ID_TestLockpick`.
- Stan odblokowania i pozostaly loot skrzyni sa zapisywane i odtwarzane razem ze stanem lochu.

## Przygotowanie testu

### 1. Sprawdz referencje sceny

1. Otworz scene `Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity`.
2. Znajdz obiekt zawierajacy komponent `DungeonObjectSpawner`.
3. Sprawdz, czy pole `Dungeon Loot Window Prefab` wskazuje na `PF_UIW_DungeonLootWindow`.
4. Nie trzeba tworzyc nowego prefabu okna. Skrzynia otrzymuje ta sama referencje automatycznie podczas spawnowania.

### 2. Sprawdz assety testowe

Assety znajduja sie tutaj:

- `Assets/_Project/ScriptableObjects/Items/ID_TestKey.asset`
- `Assets/_Project/ScriptableObjects/Items/ID_TestLockpick.asset`
- `Assets/_Project/ScriptableObjects/Dungeons/Objects/DO_Chest_Debug.asset`

Oba itemy sa dodane do `GC_MainContentCatalog`.

`DO_Chest_Debug` pozostaw domyslnie jako `Unlocked`, aby zwykly test lootu nie blokowal generowanego lochu.

## Test 1: otwarta skrzynia i wspolne okno lootu

1. Otworz `DO_Chest_Debug`.
2. Ustaw `Lock Type` = `Unlocked`.
3. Uruchom dungeon z obiektem skrzyni.
4. Podejdz liderem do skrzyni i nacisnij `E`.

Oczekiwany rezultat:

- otwiera sie `PF_UIW_DungeonLootWindow`;
- tytul okna to `Łup ze skrzyni`;
- loot widoczny jest w tym samym stylu co loot z ciala;
- klikniecie itemu dodaje go do inventory lidera;
- item znika z okna, ale skrzynia pozostaje na mapie;
- po zabraniu wszystkich itemow okno pokazuje `Skrzynia jest pusta.`;
- ponowne nacisniecie `E` otwiera puste okno, a sama skrzynia nadal jest widoczna.

## Test 2: skrzynia wymagajaca konkretnego klucza

1. Otworz `DO_Chest_Debug`.
2. Ustaw `Lock Type` = `KeyRequired`.
3. Ustaw `Required Key` na `ID_TestKey`.
4. Uruchom nowa generacje dungeonu.
5. Podejdz do skrzyni i nacisnij `E` bez klucza.
6. Dodaj klucz aktywnemu liderowi przez dev console:

   `giveitem <characterId> item.key.test.01 1`

   `<characterId>` to ID aktywnego lidera, np. `jerzy`, jezeli jest aktualnie liderem.
7. Ponownie nacisnij `E` przy skrzyni.

Oczekiwany rezultat:

- bez klucza skrzynia nie otwiera lootu i pokazuje komunikat o braku odpowiedniego klucza;
- po dodaniu klucza skrzynia otwiera sie;
- jeden klucz zostaje usuniety z inventory;
- po otwarciu dziala normalne odbieranie lootu;
- inny item typu `Key` o innym `Id` nie otwiera skrzyni.

## Test 3: skrzynia otwierana kluczem albo lockpickiem

1. Otworz `DO_Chest_Debug`.
2. Ustaw `Lock Type` = `KeyOrLockpick`.
3. Ustaw `Required Key` na `ID_TestKey`.
4. Uruchom nowa generacje dungeonu.
5. Najpierw przetestuj otwarcie z kluczem — powinien zostac zuzyty klucz.
6. Wygeneruj dungeon ponownie albo uzyj kolejnej skrzyni.
7. Dodaj aktywnemu liderowi lockpick przez dev console:

   `giveitem <characterId> item.lockpick.test.01 1`

8. Podejdz do skrzyni i nacisnij `E`.

Oczekiwany rezultat:

- skrzynia otwiera sie bez klucza, jezeli aktywna druzyna ma lockpick;
- jeden lockpick zostaje zuzyty;
- otwiera sie wspolne okno lootu;
- nie jest uruchamiana zadna minigra ani test statystyki.

## Test 4: zamkniecie i ponowne otwarcie okna

1. Otworz skrzynie.
2. Zamknij okno przyciskiem `X`.
3. Otworz skrzynie ponownie.
4. Odbierz tylko czesc przedmiotow.
5. Zamknij okno i otworz je jeszcze raz.

Oczekiwany rezultat:

- skrzynia nadal jest interaktywna;
- zabrane itemy nie wracaja;
- niezabrane itemy nadal sa dostepne;
- pojemnosc inventory blokuje odebranie zbyt ciezkiego itemu bez usuwania go ze skrzyni.

## Test 5: zapis stanu skrzyni

1. Otworz zamknieta skrzynie kluczem albo lockpickiem.
2. Zabierz jeden item, pozostawiajac co najmniej jeden item w skrzyni.
3. Wykonaj zapis gry.
4. Wczytaj zapis.
5. Wroc do tej samej skrzyni.

Oczekiwany rezultat:

- skrzynia nadal jest odblokowana;
- zabrany item nie pojawia sie ponownie;
- pozostaly item nadal mozna odebrac;
- po calkowitym oproznieniu skrzynia pozostaje widoczna i pokazuje puste okno.

## Uwagi o danych

- `Lock Type` i `Required Key` sa zapisane na `ChestDungeonObjectDefinition`. Wszystkie instancje korzystajace z tej samej definicji maja ten sam model zamka.
- Jezeli potrzebne sa rozne rodzaje skrzyn w jednym katalogu, utworz osobne assety `ChestDungeonObjectDefinition` z roznymi ustawieniami i dodaj je do `DungeonDefinition.ObjectDefinitions`.
- Skrzynia z `KeyRequired` albo `KeyOrLockpick` bez `Required Key` jest odrzucana przez placement system i nie zostanie wygenerowana.
- Testowe assety nie maja ikon. Brak ikony nie blokuje dzialania, ale w oknie lootu item bedzie bez grafiki.

## Regresja

Sprawdz, ze:

- loot z cial przeciwnikow nadal otwiera sie i odbiera jak wczesniej;
- skrzynie nadal sa rozmieszczane tylko na semantic anchorach `Chest`;
- wejscie w interakcje przez sciane nadal jest blokowane przez `HasDirectAccess`;
- otwarte okno lootu zamyka sie po wejsciu w combat albo po opuszczeniu exploration;
- zwykla skrzynia `Unlocked` nie trafia do `PendingLoot`.

Po smoke tescie uruchom recznie odpowiednie testy Unity, jezeli chcesz potwierdzic regresje zapisu i lootu.
