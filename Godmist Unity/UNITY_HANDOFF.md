# Unity handoff

## Zakres

Ten handoff służy do przetestowania dwóch kart milestone'u `Dungeon Content and Layout v2`:

1. `Dodac semantic content anchors i recznie projektowane sytuacje pokoi v2`.
2. `Dodac nieregularne ksztalty pokoi lochu v2`.

Test wykonujemy w dwóch przebiegach: najpierw semantic anchory na zwykłym prostokątnym template, a potem te same anchory na template z nieregularnym footprintem. Dzięki temu łatwo rozróżnić regresję anchorów od problemu generatora kształtu.

Nie zmieniaj sceny, prefabów ani skryptów ręcznie. Konfiguracja dotyczy wyłącznie wskazanych assetów ScriptableObject.

## Assety używane w teście

Scena:

`Assets/_Project/Scenes/Sandbox/ExplorationSandbox.unity`

Na obiekcie `DungeonEntranceView` scena ma już przypisany:

`Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset`

Seed sceny to `15`.

Katalog template'ów:

`Assets/_Project/ScriptableObjects/Dungeons/DTC_Catacombs_V1.asset`

Template używany w instrukcji:

`Assets/_Project/ScriptableObjects/Dungeons/RoomTemplates/DRT_Catacombs_Normal_Small.asset`

Jego aktualny `Size In Cells` to `12 x 12`. Asset zawiera już zapisany semantic anchor o starym Id `encounter.arena`; w tej wersji jego typ enumu oznacza `Chest`. W przebiegu testowym zmień jego Id na `chest.reward` i ustaw wartości opisane poniżej.

Definicje obiektów debugowych są już dostępne w projekcie:

- `Assets/_Project/ScriptableObjects/Dungeons/Objects/DO_Chest_Debug.asset`
- `Assets/_Project/ScriptableObjects/Dungeons/Objects/DO_Obstacle_Debug.asset`

## Przebieg A — test semantic content anchors bez footprintu

Ten przebieg izoluje poprzednią kartę. Pokój pozostaje pełnym prostokątem.

### 1. Przywróć prostokątny template

Otwórz `DRT_Catacombs_Normal_Small.asset`.

Rozwiń:

`Room Footprint -> Floor Cell Offsets`

Lista `Floor Cell Offsets` musi być pusta. Pusta lista oznacza pełny prostokąt `12 x 12`.

### 2. Skonfiguruj skrzynię

W tym samym assetcie rozwiń:

`Semantic Content Anchors`

Na istniejącym anchorze ustaw:

- `Id`: `chest.reward`
- `Type`: `Chest`
- `Local Cell`: `(4,4)`
- `Area Size`: `(1,1)`
- `Spawn Chance`: `1`
- `Is Required`: włączone
- `Requires Anchor Id`: puste

`Local Cell` jest liczony od lewego-dolnego rogu template'u, a nie od pozycji pokoju w całym lochu.

### 3. Dodaj przeszkodę

Kliknij `+` na liście `Semantic Content Anchors` i ustaw:

- `Id`: `obstacle.choke`
- `Type`: `Obstacle`
- `Local Cell`: `(1,6)`
- `Area Size`: `(2,1)`
- `Spawn Chance`: `1`
- `Is Required`: włączone
- `Requires Anchor Id`: puste

Oba anchory wskazują walkable komórki prostokątnego template'u.

### 4. Ustaw liczbę obiektów na czas testu

Otwórz:

`Assets/_Project/ScriptableObjects/Dungeons/DD_Catacombs_Procedural.asset`

Tymczasowo ustaw:

- `Min Object Count Per Floor`: `0`
- `Max Object Count Per Floor`: `100`

Dzięki temu wszystkie wymagane anchory skrzyni i przeszkody z wylosowanych instancji tego template'u mogą zostać wybrane i nie zostaną ucięte przez niski limit. Po całym teście przywróć wartości `4` i `6`.

Upewnij się, że `Object Definitions` zawiera definicje `DO_Chest_Debug` i `DO_Obstacle_Debug`.

### 5. Uruchom scenę

1. Zapisz assety i zaczekaj, aż Unity zakończy import/kompilację.
2. Otwórz `ExplorationSandbox.unity`.
3. Uruchom Play Mode.
4. Podejdź drużyną do wejścia i naciśnij `E`.

### Oczekiwany rezultat przebiegu A

- W wygenerowanym pokoju używającym `DRT_Catacombs_Normal_Small` pojawia się skrzynia dokładnie na lokalnej komórce `(4,4)`.
- Przeszkoda pojawia się na jednej z komórek `(1,6)` albo `(2,6)`.
- Nie są wybierane losowe pozycje poza skonfigurowanymi anchorami.
- Ponowne wejście do dungeonu z tym samym seedem daje te same pozycje.
- Nie testujemy tutaj `Encounter` anchorów. Encountery nadal działają według obecnej logiki wejścia do pokoju, pościgu i rozpoczęcia walki po dogonieniu gracza.

Jeżeli seed `15` nie wylosuje pokoju `Normal Small`, tymczasowo zmień `seed` na obiekcie `DungeonEntranceView` na inny, np. `16`, i powtórz test. Po teście możesz przywrócić `15`.

## Przebieg B — ten sam test z nieregularnym footprintem

Ten przebieg testuje obie karty jednocześnie.

### 1. Pozostaw anchory z przebiegu A

Nie zmieniaj anchorów `chest.reward` i `obstacle.choke`. Ich komórki muszą pozostać częścią footprintu.

### 2. Wpisz footprint

W `DRT_Catacombs_Normal_Small.asset` rozwiń:

`Room Footprint -> Floor Cell Offsets`

Dodaj dokładnie komórki opisane poniżej. Każdy wpis to osobny element listy. Kolejność wpisów nie ma znaczenia.

To jest połączony, nieregularny kształt typu `L` z dwoma odnogami. Zawiera oba structural anchory istniejące w tym template oraz oba semantic anchory z przebiegu A.

Wpisy według wierszy lokalnego `y`:

- `y = 0`: `x = 0..11`
- `y = 1`: `x = 0, 6`
- `y = 2`: `x = 0, 6`
- `y = 3`: `x = 0..3, 6`
- `y = 4`: `x = 0..6`
- `y = 5`: `x = 0..6`
- `y = 6`: `x = 0..7`
- `y = 7`: `x = 0`
- `y = 8`: `x = 0`
- `y = 9`: `x = 0`
- `y = 10`: `x = 0`
- `y = 11`: `x = 0`

Przykład: zapis `y = 3: x = 0..3, 6` oznacza pięć osobnych elementów: `(0,3)`, `(1,3)`, `(2,3)`, `(3,3)`, `(6,3)`.

Nie dodawaj żadnych innych wpisów. W szczególności pominięte komórki mają pozostać pustką.

### 3. Sprawdź zgodność anchorów

Po wpisaniu footprintu te komórki muszą istnieć:

- skrzynia: `(4,4)`;
- przeszkoda: `(1,6)` i `(2,6)`;
- istniejący structural wall anchor: `(2,3)` oraz `(3,3)`;
- istniejący structural door anchor: `(6,6)` oraz `(7,6)`.

Nie przesuwaj structural anchorów. Jeśli którykolwiek z powyższych punktów nie jest wpisany do footprintu, generator lub resolver słusznie zgłosi błędną konfigurację.

### Oczekiwany rezultat przebiegu B

- Pokój nie jest już prostokątną pełną powierzchnią.
- Wpisane komórki mają podłogę.
- Pominięte komórki są pustką.
- Wokół pustek powstają segmenty ścian.
- Skrzynia nadal pojawia się dokładnie na `(4,4)`.
- Przeszkoda nadal pojawia się na `(1,6)` albo `(2,6)`.
- Żaden semantic anchor nie zostaje przeniesiony do losowej komórki.
- Start, wyjście, encounter i formacja przeciwników nie pojawiają się w pustce footprintu.
- Przejścia między pokojami wykorzystują wyłącznie rzeczywiste komórki podłogi na zewnętrznych krawędziach.
- Gracz może fizycznie dotrzeć do każdej komórki footprintu.
- Kliknięcie w pustkę nie powinno prowadzić drużyny przez pokój. Obecny mouse movement nadal jest ruchem bez pełnego pathfindingu; karta pathfindingu pozostaje osobnym zadaniem.

### Powtórzenie deterministyczne

Uruchom dungeon ponownie z tym samym seedem i sprawdź:

- ten sam układ pokoi;
- ten sam kształt footprintu;
- te same przejścia;
- te same pozycje skrzyni i przeszkody.

## Diagnostyka typowych błędów

### `Kotwa semantyczna ... wskazuje pole poza pokojem`

Anchor wskazuje komórkę pominiętą w footprintcie, wychodzi poza `Size In Cells` albo jego `Area Size` obejmuje pustkę. Sprawdź wszystkie komórki obszaru anchoru.

### `Kotwica drzwi ... wskazuje pole poza pokojem`

Footprint nie zawiera jednej z dwóch komórek istniejącego wewnętrznego structural door anchoru. Przywróć `(6,6)` i `(7,6)`.

### Nie udało się wygenerować poprawnego układu piętra

Wybrany footprint nie ma wystarczająco szerokiej rzeczywistej krawędzi w kierunku, w którym generator próbuje połączyć pokój. Nie dodawaj przejścia ręcznie. Spróbuj innego seeda albo użyj footprintu, który ma podłogę na wymaganych zewnętrznych krawędziach.

### Brak skrzyni lub przeszkody

Sprawdź `Object Definitions`, `Min/Max Object Count Per Floor`, typ anchoru oraz to, czy template z anchorami został wybrany przez generator. W razie potrzeby zmień seed zgodnie z przebiegiem A.

## Przywrócenie assetów po teście

Po zakończeniu testu:

1. Przywróć `Min Object Count Per Floor = 4`.
2. Przywróć `Max Object Count Per Floor = 6`.
3. Jeśli nie chcesz zostawiać testowego kształtu, wyczyść listę `Floor Cell Offsets`, aby template wrócił do pełnego prostokąta.
4. Jeśli zmieniłeś seed sceny, przywróć `15`.

## Testy automatyczne

Nie uruchamiałem test runnera Unity ani nie tworzyłem test mirroru. Po imporcie nowych skryptów możesz ręcznie uruchomić EditMode Tests, szczególnie:

- `DungeonSemanticAnchorTests`;
- `DungeonRoomFootprintTests`;
- `DungeonGeneratorServiceTests`.
