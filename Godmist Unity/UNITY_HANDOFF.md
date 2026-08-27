# Unity handoff

Karta: `Dodac DungeonRoomEditor v1`

Edytor sluzy do projektowania i walidacji `DungeonRoomTemplateDefinition`. Nie wymaga zmiany sceny, prefabow ani serialized references w scenie.

## Inne kroki Editor

### Otworzenie istniejacego template'u

1. W oknie Project otworz:

   `Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates/DRT_Catacombs_Normal_Small.asset`

2. W Inspectorze kliknij `Open Dungeon Room Editor`.

Alternatywnie wybierz asset i otworz menu:

`Godmist -> Dungeons -> Dungeon Room Editor`

W oknie edytora mozna wskazac asset w polu obok przycisku `New`.

### Utworzenie nowego template'u

1. W oknie `Dungeon Room Editor` kliknij `New`.
2. Wybierz folder:

   `Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates`

3. Podaj nazwe assetu i zatwierdz.

Nowy asset jest tworzony z poprawnymi wartosciami poczatkowymi:

- `Id` na podstawie nazwy assetu;
- `Display Name` na podstawie nazwy assetu;
- `Room Type = Normal`;
- `Size In Cells = 8 x 8`;
- pusty `Floor Cell Offsets`, czyli pelny prostokat.

### Edycja footprintu

1. Wybierz tryb `Footprint`.
2. Kliknij komorke, aby przelaczyc ja miedzy podloga i pustka.
3. Przeciagaj lewym przyciskiem myszy, aby malowac kolejne komorki tym samym stanem.

Pusty `Floor Cell Offsets` oznacza pelny prostokat. Po pierwszej zmianie lista jest zapisywana jawnie. Gdy wszystkie komorki znowu sa podloga, edytor kompresuje footprint z powrotem do pustej listy.

### Dodawanie structural anchoru

1. Wybierz tryb `Structural anchors`.
2. W sekcji `Preview visibility` wlacz albo wylacz `Structural` zgodnie z potrzeba.
3. Ustaw `New type` na `Wall` albo `Door`.
4. Ustaw `Direction`.
5. Kliknij pierwsza z dwoch sasiednich komorek podlogi.

Edytor doda anchor laczacy kliknieta komorke z komorka wskazana przez kierunek. Klikniecie istniejacego anchoru wybiera go do edycji w panelu po prawej.

PPM na structural anchorze usuwa go. Usuwanie dziala zgodnie z aktywnym trybem edycji.

`Door` anchor jest wewnetrzna krawedzia pokoju. Nie sluzy do recznego definiowania przejsc na zewnetrznej granicy pokoju.

### Dodawanie semantic anchoru

1. Wybierz tryb `Semantic anchors`.
2. W sekcji `Preview visibility` wlacz albo wylacz `Semantic` zgodnie z potrzeba.
3. Ustaw `New type`.
4. Kliknij komorke podlogi.
5. W panelu po prawej ustaw:

   - `Id`;
   - `Type`;
   - `Local Cell`;
   - `Area Size`;
   - `Spawn Chance`;
   - `Is Required`;
   - `Requires Anchor Id`.

Nowy anchor zaczyna z obszarem `1 x 1`, szansa `1` i bez zaleznosci.

PPM na semantic anchorze usuwa go. Przelaczanie widocznosci zmienia tylko podglad i nie usuwa ani nie modyfikuje danych anchorow.

## Smoke test

### A. Otwarcie i podglad

1. Otworz `DRT_Catacombs_Normal_Small.asset` przez Inspector.
2. Kliknij `Open Dungeon Room Editor`.
3. Sprawdz, ze widoczna jest siatka odpowiadajaca rozmiarowi `12 x 12`.
4. Sprawdz, ze pelny prostokat jest pokazany jako podloga.
5. Sprawdz, ze istnieja wizualne markery aktualnych structural i semantic anchorow.
6. W sekcji `Preview visibility` wylacz `Structural`, a potem `Semantic`.

Oczekiwany rezultat:

- otwiera sie osobne okno `Dungeon Room Editor`;
- footprint jest pokazany jako statyczna siatka 2D;
- anchory sa widoczne na tej samej siatce;
- mozna niezaleznie ukryc structural i semantic anchory, gdy ich markery nachodza na siebie;
- nie jest potrzebny `DungeonRoomView`, scena ani prefab.

### B. Edycja footprintu

1. Przejdz do trybu `Footprint`.
2. Kliknij jedna komorke pelnego prostokata, aby zmienic ja w pustke.
3. Przeciagaj po kilku kolejnych komorkach, aby utworzyc wieksza pustke.

Oczekiwany rezultat:

- komorki zmieniaja kolor miedzy podloga i pustka;
- pustka jest natychmiast widoczna w podgladzie;
- `Floor Cell Offsets` przestaje byc puste i zawiera jawna liste pozostalych komorek;
- kolejnosc zapisanych komorek jest stabilna.

### C. Structural anchor

1. Wroc do trybu `Footprint` i przywroc podloge na komorce `(2,2)` oraz `(3,2)`, jesli zostaly usuniete.
2. Przejdz do `Structural anchors`.
3. Ustaw `New type = Wall` oraz `Direction = Right`.
4. Kliknij komorke `(2,2)`.
5. Kliknij dodany marker i sprawdz panel po prawej.
6. Zmien typ na `Door`, zapisz i ponownie zaznacz anchor.
7. Kliknij ten anchor PPM.

Oczekiwany rezultat:

- powstaje jeden anchor na krawedzi `(2,2)` - `(3,2)`;
- marker pokazuje typ i krawedz;
- typ oraz kierunek mozna zmieniac z panelu;
- klikniecie tej samej krawedzi nie tworzy duplikatu;
- `Remove selected anchor` usuwa wybrany anchor.
- PPM usuwa anchor bez potrzeby otwierania panelu po prawej.

### D. Semantic anchor

1. Przejdz do `Semantic anchors`.
2. Ustaw `New type = Chest`.
3. Kliknij komorke `(4,4)`.
4. Ustaw w panelu:

   - `Id = chest.editor.test`;
   - `Area Size = (1,1)`;
   - `Spawn Chance = 1`;
   - `Is Required = wlaczone`;
   - `Requires Anchor Id = puste`.

5. Zmien `Area Size` na `(2,1)` i sprawdz podglad obszaru.
6. Uzyj `Remove selected anchor` i sprawdz, ze marker znika.
7. Dodaj ponownie anchor i usun go PPM.

Oczekiwany rezultat:

- semantic anchor ma unikalne domyslne Id po dodaniu;
- mozna edytowac wszystkie obecne pola danych;
- obszar anchoru jest widoczny jako jeden zaznaczony obszar na siatce.
- PPM usuwa semantic anchor.

### E. Walidacja blokujaca zapis

1. Dodaj semantic anchor na komorce `(4,4)`.
2. Ustaw mu `Area Size = (2,2)`.
3. Usun z footprintu co najmniej jedna komorke objeta tym obszarem.
4. Sprawdz sekcje `Validation`.
5. Kliknij `Save` albo uzyj `Ctrl+S`.

Oczekiwany rezultat:

- sekcja `Validation` pokazuje blad o obszarze poza footprintem;
- przycisk `Save` nie zapisuje assetu;
- `Ctrl+S` rowniez nie zapisuje niepoprawnego room template;
- w Console pojawia sie blad wskazujacy, ze zapis zostal zablokowany.

Nastepnie napraw footprint albo obszar anchoru. Gdy lista bledow jest pusta, zapis powinien zadzialac.

### F. Walidacja spojnosci footprintu i zaleznosci

Sprawdz dodatkowo dwa przypadki:

1. Utworz footprint z dwoch oddzielonych grup komorek.
2. Ustaw `Requires Anchor Id` na nieistniejace Id albo utworz cykl zaleznosci A -> B -> A.

Oczekiwany rezultat:

- edytor zglasza blad o niepolaczonym footprintcie;
- edytor zglasza blad o nieistniejacej zaleznosci albo cyklu;
- asset nie moze zostac zapisany, dopoki wszystkie bledy nie zostana usuniete.

### G. Ponowne otwarcie assetu

1. Napraw wszystkie bledy.
2. Kliknij `Save`.
3. Zamknij okno edytora.
4. Otworz ten sam asset ponownie.

Oczekiwany rezultat:

- footprint, structural anchory i semantic anchory zachowuja zapisane wartosci;
- podglad po ponownym otwarciu odpowiada zapisanym danym.

## Testy automatyczne

Dodano testy EditMode dla walidatora:

- poprawnego pelnego prostokata;
- rozlaczonego footprintu;
- semantic anchoru wychodzacego poza footprint;
- cyklu zaleznosci semantic anchorow.

Uruchom je recznie w Unity Test Runnerze jako `DungeonRoomTemplateEditorValidatorTests`. Nie uruchamialem test runnera ani nie tworzylem test mirroru.
