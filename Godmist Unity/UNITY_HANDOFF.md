# Unity handoff

Karta: `Dodac pulapki, przeszkody, klucze/dzwignie i proste zagadki lochowe v1`

Smoke test gameplayu zostal potwierdzony recznie: pelny loch z jedna dzwignia i jednym warunkiem zostal ukonczony, a brama otworzyla sie dopiero po interakcji z oboma. Nie uruchamialem Unity Test Runnera ani test mirroru.

## Konfiguracja contentu

1. Utworz w `Assets/_Project/ScriptableObjects/Dungeons/Objects/` assety:

   - `SpikeTrapDungeonObjectDefinition` — ustaw `Id`, `Display Name`, `Spawn Weight` oraz `Damage`;
   - `LeverDungeonObjectDefinition` — ustaw `Id`, `Display Name`, `Spawn Weight` oraz `Is Reversible`;
   - `ConditionDungeonObjectDefinition` — ustaw `Id`, `Display Name` oraz `Spawn Weight`.

2. Dodaj te definicje do listy `Object Definitions` w aktywnym `DungeonDefinition`.

3. Otworz room template w `DungeonRoomEditorWindow`, wybierz narzedzie `Semantic Anchor` i dodaj:

   - typ `Trap` w pokoju `Normal`, `Optional` albo `Corridor`;
   - typ `Lever` albo `Condition` tylko w template oznaczonym `Can Be Required Progression Branch`;
   - zwykle template-y `Normal` i `Optional` bez tej flagi nie powinny zawierac tych typow anchorow.

4. Dla pulapek, ktore maja aktywowac sie razem, wpisz identyczne `Activation group (traps)` na ich anchorach. Puste pole oznacza niezalezna pulapke.

5. Zapisz room template i upewnij sie, ze walidator nie raportuje bledow.

## Wymagane odnogi progresji

1. W katalogu aktywnego dungeon definition otworz template typu `Optional`,
   ktory ma sluzyc jako odnoga wymagajaca ukonczenia.
2. W inspectorze template wlacz `Can Be Required Progression Branch`.
3. W tym samym template dodaj co najmniej jeden anchor `Lever` albo `Condition`.
   Jesli w wymaganej odnodze znajduje sie kilka takich anchorow, wszystkie
   zostana wymuszone przez generator i wszystkie musza zostac aktywowane.
4. Ustaw `Min/Max Progression Gate Count` na liczbe wymaganych pokoi, np. `2`.
   Generator utworzy dokladnie dwa wymagane pokoje progresji, o ile katalog i
   zakres pokoi opcjonalnych pozwalaja je rozstawic. Liczba dzwigni nie zwieksza
   liczby odnóg.

Klucz nie jest osobnym pickupem mapy. Jest zwyklym `ItemDefinition` z `Item Type = Key`. Aby byl gwarantowanym lootem, dodaj go jako wpis `LootEntryData` w loot table skrzyni albo encountera i ustaw `Drop Chance = 1` oraz ilosc `1..1`. Otwieranie zamkow i lockpicking pozostaja osobna karta UI skrzyn i zamkow.

## Prefaby widoku

`DungeonObjectSpawner` ma trzy nowe referencje:

- `spikeTrapPrefab` typu `DungeonSpikeTrapView`;
- `leverPrefab` typu `DungeonLeverView`;
- `conditionPrefab` typu `DungeonConditionView`.

Mozesz utworzyc proste prefab-y z komponentem odpowiedniego typu oraz opcjonalnym `SpriteRenderer` i przypisac je w obiekcie `DungeonObjectSpawner` w scenie `Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity`.

Brak prefab-u nie blokuje uruchomienia: spawner utworzy widoczny, tymczasowy kwadrat debugowy. Prefab-y sa zalecane, jezeli chcesz sprawdzic docelowa grafike lub collider.

Collider gracza w `PF_ExplorationCharacter` ma ustawiony promien `0.28`, zeby lider mogl przejsc przez jednokratkowe korytarze przy obecnej grubosci scian.

## Oczekiwane zachowanie

- przeszkoda pozostaje statyczna i blokuje ruch zgodnie z dotychczasowa definicja;
- wejscie lidera albo innego aktywnego czlonka druzyny na komorke spike trapa aktywuje go tylko raz;
- aktywacja zadaje staly damage liderowi i kazdemu aktywnemu czlonkowi stojacemu na komorce aktywowanego trapa;
- pulapki w tej samej grupie aktywacji uruchamiaja sie w tym samym momencie, a pulapki z innej grupy pozostaja uzbrojone;
- aktywowany trap zmienia kolor na nieaktywny i nie zadaje obrazen ponownie;
- lever obok aktywnego gracza mozna przelaczac klawiszem `E`;
- generator automatycznie przypisuje wszystkie lever-y i wymagane obiekty `Condition` z pokoju `RequiredBranchRoomId` do jednej bramki; brak takiego obiektu jest bledem konfiguracji zamiast losowego uzupelnienia;
- aktywacja jednego z kilku wymaganych leverow nie otwiera bramki; dopiero aktywacja wszystkich zrodel progresji w pokoju spelnia generyczny `ProgressionConditionId`;
- stan `IsTriggered`, `IsActivated` i `IsSatisfied` jest zapisywany w `DungeonObjectDeltaSaveData` i odtwarzany po loadzie;
- po ponownym wygenerowaniu tego samego lochu obiekty, grupy pulapek i przypisania condition sa deterministyczne.

## Smoke test

1. Ustaw na `DD_Catacombs_Procedural.asset` co najmniej jedna pulapke, lever albo condition w `Object Definitions` oraz odpowiednie anchory w template.
2. Uruchom wejscie do lochu przez istniejacy przeplyw `DungeonSandbox`.
3. Przejdz liderem na anchor `Trap`; sprawdz obrazenia na liderze i czlonkach druzyny stojacych na komorce. Przy grupie pulapek sprawdz, ze wszystkie obiekty z ta sama grupa zmieniaja stan jednoczesnie.
4. Oznacz template optional jako `Can Be Required Progression Branch`, dodaj do niego dwie kotwy `Lever`, ustaw `Min/Max Progression Gate Count = 1`, wygeneruj dungeon i sprawdz, ze oba lever-y sa obecne w wymaganej odnodze.
5. Aktywuj tylko jeden lever i sprawdz, ze bramka nadal jest zamknieta. Aktywuj drugi i sprawdz, ze dopiero wtedy przejscie do finalnego pokoju staje sie otwarte.
6. Wykonaj zapis po aktywacji tylko czesci leverow, przejdz do innej sceny i wczytaj gre. Sprawdz, ze postep czesciowy zostal zachowany, a bramka otwiera sie po aktywacji pozostalych zrodel.
7. W `LootTableData` dodaj `ItemDefinition` typu `Key` z `Drop Chance = 1`; sprawdz, ze skrzynia albo encounter daje ten item jako gwarantowany loot.

## Testy automatyczne

Do recznego uruchomienia w Unity Test Runnerze:

- `DungeonTrapAndConditionTests` — grupowanie i jednorazowa aktywacja trapow oraz restore stanow;
- dotychczasowe testy `DungeonRuntimeRestoreTests`, `DungeonSemanticAnchorTests` i testy generatora.
