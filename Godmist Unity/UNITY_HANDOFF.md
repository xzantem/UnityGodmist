# Unity handoff

Karta: `Dodac runtime oraz zapis inwestycji w skill tree v1`

## Referencje

W `Assets/_Project/Scenes/City/Arungard.unity` referencja
`ExplorationPartyController.skillTreeProgressionPanel` jest juz przypisana.
Nie dodawaj wyszukiwania panelu po scenie ani dodatkowego wiring'u.

Testowe drzewka dotyczace Scouta maja globalne pasywy skalowane za kazdy punkt
wydany w konkretne drzewko:

- `ST_Scout_Assassin`: `+0.2%` flat Crit Chance za punkt.
- `ST_Scout_BladeDancer`: `+0.5%` Min Attack i Max Attack za punkt.
- `ST_Scout_Guardian`: `+0.5%` Physical Defense za punkt.

To sa wartosci testowe; docelowe dane dla pozostalych klas i mnozniki obrazen
specyficznych dla rodzaju ataku sa poza zakresem tej karty.

Stare lub skopiowane node'y z zapisanym `MaxRanks = 0` sa traktowane przez
runtime jako `1` i obecne testowe assety zostaly zmigrowane do wartosci `1`.

## Wymagana konfiguracja UI w scenie

Scena: `Assets/_Project/Scenes/City/Arungard.unity`.
Modyfikowany root: `SkillTreeProgressionPanel`.

Docelowa hierarchia (pozostale istniejace childy pozostaw bez zmian):

```text
SkillTreeProgressionPanel
|- Header                         (istniejacy; bez zmian)
|- TreeTabs                       (istniejacy; zmien RectTransform)
|  `- TreeTabTemplate             (istniejacy; zmien Layout Element)
|- SelectedTreeHeader             (nowy: Image)
|  `- SelectedTreeName            (nowy: TextMeshProUGUI)
|- GlobalPassivePanel             (nowy: Image)
|  `- GlobalPassiveText           (nowy: TextMeshProUGUI)
`- TreeContent                    (istniejacy; bez zmian)
```

### `TreeTabs` (istniejacy direct child `SkillTreeProgressionPanel`)

- `RectTransform`: `Anchor Min = (0, 1)`, `Anchor Max = (0, 1)`,
  `Pivot = (0, 1)`, `Anchored Position = (16, -122)`,
  `Size Delta = (800, 36)`.
- Zachowaj istniejacy `HorizontalLayoutGroup`; `Padding = (0, 0, 0, 0)`,
  `Spacing = 12`, `Child Alignment = Upper Left`, wszystkie `Child Force
  Expand` i `Child Control Size` wylaczone.

### `TreeTabTemplate` (istniejacy child `TreeTabs`)

- Na komponencie `LayoutElement`: `Preferred Width = 180`,
  `Preferred Height = 36`.
- Nie zmieniaj stanu inactive template ani komponentu `Button`.

### `SelectedTreeHeader` (nowy direct child `SkillTreeProgressionPanel`)

- Utworz `UI > Image` o dokladnej nazwie `SelectedTreeHeader`.
- `RectTransform`: `Anchor Min = (0, 1)`, `Anchor Max = (0, 1)`,
  `Pivot = (0, 1)`, `Anchored Position = (16, -174)`,
  `Size Delta = (800, 46)`.
- `Image`: kolor `RGBA = (0.10, 0.15, 0.21, 0.96)`, `Raycast Target = off`.
- Nie dodawaj `Layout Group`, `Content Size Fitter` ani `Button`.

### `SelectedTreeName` (nowy child `SelectedTreeHeader`)

- Utworz `UI > Text - TextMeshPro` o dokladnej nazwie `SelectedTreeName`.
- `RectTransform`: `Anchor Min = (0, 0)`, `Anchor Max = (1, 1)`,
  `Pivot = (0.5, 0.5)`, w inspectorze `Left = 14`, `Right = 14`,
  `Top = 6`, `Bottom = 6` (serialized: `offsetMin = (14, 6)`,
  `offsetMax = (-14, -6)`).
- `TextMeshProUGUI`: skopiuj font, material i kolor z `CharacterNameText`;
  `Font Size = 26`, `Auto Size = on`, `Min = 18`, `Max = 26`,
  `Alignment = Midline Left`, `Word Wrapping = off`, `Overflow = Overflow`,
  `Raycast Target = off`.
- Tekst runtime ma jeden wiersz: `<nazwa drzewka> | Wydano: N/50`.

### `GlobalPassivePanel` (nowy direct child `SkillTreeProgressionPanel`)

- Utworz `UI > Image` o dokladnej nazwie `GlobalPassivePanel`.
- `RectTransform`: `Anchor Min = (1, 1)`, `Anchor Max = (1, 1)`,
  `Pivot = (1, 1)`, `Anchored Position = (-16, -174)`,
  `Size Delta = (390, 46)`.
- `Image`: kolor `RGBA = (0.10, 0.15, 0.21, 0.96)`, `Raycast Target = off`.
- Nie dodawaj `Layout Group`, `Content Size Fitter` ani `Button`.

### `GlobalPassiveText` (nowy child `GlobalPassivePanel`)

- Utworz `UI > Text - TextMeshPro` o dokladnej nazwie `GlobalPassiveText`.
- `RectTransform`: `Anchor Min = (0, 0)`, `Anchor Max = (1, 1)`,
  `Pivot = (0.5, 0.5)`, w inspectorze `Left = 14`, `Right = 14`,
  `Top = 6`, `Bottom = 6` (serialized: `offsetMin = (14, 6)`,
  `offsetMax = (-14, -6)`).
- `TextMeshProUGUI`: skopiuj font, material i kolor z `CharacterNameText`;
  `Font Size = 15`, `Auto Size = on`, `Min = 11`, `Max = 15`,
  `Alignment = Midline Left`, `Word Wrapping = off`, `Overflow = Overflow`,
  `Raycast Target = off`.
- Tekst runtime ma dwa wiersze: nazwa pasywy i `Aktualnie: <efekt>`.

### `SkillTreeProgressionPanelView` (komponent na `SkillTreeProgressionPanel`)

Przypisz cztery nowe referencje:

- `Selected Tree Header` -> `SelectedTreeHeader` (`RectTransform`),
- `Selected Tree Name Text` -> `SelectedTreeName` (`TextMeshProUGUI`),
- `Global Passive Panel` -> `GlobalPassivePanel` (`RectTransform`),
- `Global Passive Text` -> `GlobalPassiveText` (`TextMeshProUGUI`).

Gdy wszystkie cztery sa przypisane, runtime nie zmienia ich pozycji ani
rozmiarow. Do czasu konfiguracji zachowuje tymczasowy fallback runtime,
aby panel pozostawal grywalny.

## Wymagana konfiguracja content catalog dla quicksave

Ten krok jest wymagany, aby odtworzyc istniejacy quicksave kampanii
`be96f5ef08ba4992a14007e01752968d`.

Otworz `Assets/_Project/ScriptableObjects/World/GC_MainContentCatalog.asset`.
Na komponencie `GameContentCatalog`, w liscie `Skill Definitions`, dodaj
ponizsze istniejace assety (nie usuwaj obecnych wpisow):

- `Assets/_Project/ScriptableObjects/Skills/SD_BattleCry.asset`
  (`skill.battle_cry`),
- `Assets/_Project/ScriptableObjects/Skills/SD_Haste.asset`
  (`skill.haste`),
- `Assets/_Project/ScriptableObjects/Skills/SD_Counterattack.asset`
  (`skill.counterattack`),
- `Assets/_Project/ScriptableObjects/Skills/SD_Teleport.asset`
  (`skill.teleport`),
- `Assets/_Project/ScriptableObjects/Skills/SD_Charge.asset`
  (`skill.charge`),
- `Assets/_Project/ScriptableObjects/Skills/SD_CreateTestTotem.asset`
  (`skill.test_totem`),
- `Assets/_Project/ScriptableObjects/Skills/SD_DelayedStrike_Test.asset`
  (`skill.delayed_strike_test`).

Kazdy skill zapisany w `CharacterSaveData.SkillIds` musi istniec w tej liscie.
Restore celowo odrzuca zapis, gdy nie moze zrekonstruowac nawet jednego
skillId, aby nie tworzyc postaci z niepelna lista umiejetnosci.

## Konfiguracja bazowych odpornosci postaci

`CharacterDefinition` ma nowe pole `Base Resistances`. Jest ono per postać;
wartości są znormalizowane w zakresie `0..1` (`0.5` oznacza `50%`).

1. Otwórz odpowiedni asset `CD_*` w `Assets/_Project/ScriptableObjects/Characters/`.
2. Rozwiń `Base Resistances` i ustaw: `Bleed`, `Poison`, `Fire`, `Weakening`,
   `Suppression` (wspólna odporność na stun, immobilize, sleep i paralysis) oraz
   `Move` (wspólna odporność na forced movement).
3. Nowa gra użyje tych wartości natychmiast. Po wczytaniu starego quicksave'a
   (utworzonego przed tą zmianą) runtime również bierze dane bazowe z assetu;
   po następnym `F5` zapis utrwali bieżący `ResistanceSet`, aby przyszłe
   modyfikacje runtime mogły zostać zachowane.

Arkusz `design/Statystyki Postaci.xlsx` dostarcza wartości dla Bleed, Poison,
Fire i Weakening. Wartość `Odporność na EOR` wpisz identycznie do obu pól:
`Suppression` oraz `Move`.

## Smoke test

1. Uruchom `Arungard` poza walka postacia Scout i nacisnij `P`.
2. Sprawdz, ze zakladki sa nad headerem, a bezposrednio nad kartami po lewej
   widac jednoliniowa nazwe wybranego drzewka i `Wydano: N/50`. Po prawej
   widac nazwe oraz aktualny efekt jego globalnej pasywy.
3. Zdobadz co najmniej jeden punkt umiejetnosci, zainwestuj go w dostepny node
   i sprawdz odswiezenie licznika punktow, rangi node'a oraz wartosci pasywy.
4. Dla Assassin po pieciu punktach sprawdz `+1% CritChance`; dla BladeDancer
   `+2.5% MinAttack` oraz `MaxAttack`; dla Guardian `+2.5% PhysicalDefense`.
5. Zapisz gre, wczytaj zapis i ponownie otworz panel.

Oczekiwany rezultat:

- globalna pasywa zalezy tylko od punktow wydanych w swoje drzewko;
- przejscie z poziomu 1 na 2 daje 2 punkty; lacznie od poziomu 1 do 50
  postac otrzymuje 50 punktow;
- inwestycja natychmiast odswieza statystyki postaci;
- prerequisite'y oraz progi poziomow drzewka blokuja niedostepne node'y;
- inwestycje i wynikajace z nich pasywy pozostaja po save/load;
- `P` nie otwiera panelu podczas walki i zamyka go przy ponownym nacisnieciu.

## Rewards and Progression v2 — pełny smoke test milestone'u

Poniższy scenariusz jest testem akceptacyjnym całego milestone'u. Nie wymaga
żadnych kolejnych zmian w scenie ani w prefabach. Wykonaj go na kopii
quicksave'a albo od nowej sesji, ponieważ celowo zmienia XP, ekwipunek i stan
walki.

### 1. Start, content catalog i podstawowy save/load

1. Uruchom scenę `Arungard` i wczytaj quicksave klawiszem `F9`.
2. Nie może pojawić się komunikat `Nie udało się odtworzyć GameSession z zapisu`.
3. Naciśnij `F5`, a następnie `F9`; po wczytaniu aktywna drużyna, pozycja oraz
   stan ekwipunku mają pozostać spójne.

### 2. Level growth i punkty umiejętności

1. Na świeżej postaci poziomu 1 otwórz dev console i użyj `xp 24` (próg dla
   poziomu 2). Gdy testujesz istniejącą postać, użyj `characters`, a następnie
   `xp <characterId> <liczba>`.
2. Sprawdź, że przejście z poziomu 1 na 2 daje dokładnie `2` punkty umiejętności.
3. Awansuj testowo do poziomu 50 i sprawdź, że łączna pula zdobytych punktów
   (wydane + dostępne) wynosi `50`, nie `49`.
4. Sprawdź, że wzrost maksymalnego HP podnosi również bieżące HP tylko przy
   realnym awansie; po `F5`/`F9` bieżące HP nie może zostać samoistnie uleczone.
5. Po ustawieniu `Base Resistances` użyj `unit <characterId>` i potwierdź, że
   Console pokazuje skonfigurowane wartości. Wykonaj `F5`/`F9` i sprawdź je
   ponownie.

### 3. Drzewko umiejętności i globalna pasywa

1. Poza walką naciśnij `P`.
2. Zakładki drzewek muszą być nad headerem. Pod nimi, nad kartami po lewej,
   ma być jednoliniowy header `<nazwa drzewka> | Wydano: N/50`; po prawej
   panel globalnej pasywy z nazwą oraz `Aktualnie: <efekt>`.
3. Zainwestuj punkt w dostępny node. Sprawdź natychmiastowe odświeżenie:
   puli punktów, rangi node'a, licznika konkretnego drzewka i statystyki.
4. Sprawdź blokady prerequisite'ów, maksymalnych rang oraz progów 10/20/30
   punktów wydanych w *tym samym* drzewku. Punkty z drugiego drzewka nie mogą
   odblokować jego kolejnego tieru.
5. Dla Scouta sprawdź po pięciu punktach: Assassin `+1% CritChance`,
   BladeDancer `+2.5% MinAttack` i `MaxAttack`, Guardian `+2.5% PhysicalDefense`.
6. Zapisz (`F5`), wczytaj (`F9`) i zweryfikuj ponownie rangi, liczniki drzewek
   oraz wynikające z nich statystyki. Podczas walki `P` nie może otworzyć panelu.

### 4. Active skill loadout

1. Poza walką naciśnij `L`. Ruch drużyny ma być zablokowany, a `P` ma zamykać
   loadout przed otwarciem drzewka.
2. Basic attack pozostaje zawsze w slocie `0` i nie może zostać usunięty ani
   przeniesiony.
3. Przenieś dostępny skill do aktywnego slotu kliknięciem, usuń go z powrotem,
   a następnie wykonaj drag-and-drop: available → active, active → available
   oraz active slot 1 → active slot 2.
4. Ghost przeciąganego skilla ma mieć strukturę i tło karty; jego ikona ma być
   wycentrowana na kursorze. Zielony feedback available panelu ma działać także
   wtedy, gdy kursor przechodzi bezpośrednio nad inną ikoną available.
5. Zamknij modal przyciskiem `X`, zapisz (`F5`) i wczytaj (`F9`). Kolejność
   aktywnych skilli oraz basic attack w slocie 0 muszą zostać zachowane.

### 5. Equipment generation i payload itemu

1. W dev console wpisz `createitem equipment.scout.sword_dagger.test` oraz
   `createitem equipment.scout.tunic.test`.
2. Skopiuj zwrócone `instanceId` i dla każdego użyj `inspectitem <instanceId>`.
3. Dla equipmentu wynik musi zawierać niepuste `PartIds` i `StatModifiers`,
   `RequiredClassId: class.scout`, wymagany poziom wynikający z najwyższego
   level bracket części, quality tylko `Poor`/`Normal`/`Excellent` oraz losową
   rarity.
4. Zapisz (`F5`) i wczytaj (`F9`), po czym użyj `inspectitem` ponownie. Wszystkie
   pola payloadu (w szczególności `PartIds`, quality, rarity, level i modyfikatory)
   muszą być identyczne.

### 6. Rewards, escape i ciała przeciwników

1. Wejdź w normalny encounter lochu z co najmniej dwoma aktywnymi członkami
   drużyny. W jednej walce doprowadź jedną postać do stanu downed, ale zakończ
   walkę zwycięstwem.
2. Po powrocie do eksploracji tylko żywi i downed aktywni członkowie drużyny
   otrzymują XP oraz honor; postać martwa lub nieaktywna nie może ich otrzymać.
3. Przy pokonanym przeciwniku z nagrodą ma powstać ciało: używa jego `DeadSprite`,
   jest obrócone o 90 stopni, ciemnoczerwone, renderowane pod graczem i nie ma
   obramówki bez hovera. Po najechaniu w zasięgu interakcji pojawia się żółta
   obramówka; klik odbiera najpierw item, potem złoto, oraz pokazuje czytelny
   floating text. Ciało znika dopiero po opróżnieniu.
4. Zapisz przy nieopróżnionym ciele (`F5`) i wczytaj (`F9`). Pozycja ciała,
   złoto i dokładne item instance IDs muszą przetrwać reload; uprzednio odebrane
   nagrody nie mogą wrócić.
5. W osobnym encounterze użyj `E`, aby wykonać escape. Zwycięstwo nie może
   zostać naliczone, ciało nie może powstać, a każdy żywy lub downed aktywny
   członek drużyny musi dostać jednorazowo `-5` honoru — również ten, który po
   escape zostaje porzucony. Ponowne odświeżenie/wyjście z walki nie może naliczyć
   kary drugi raz.

### Kryterium zaliczenia

Milestone jest zaliczony, gdy wszystkie sześć sekcji przejdzie bez wyjątków w
Console, bez utraty danych po `F5`/`F9` i bez rozjazdu między UI a stanem runtime.
Brak osobnego UI listy przedmiotów w ciele jest oczekiwany: to zakres kolejnego
milestone'u lootbagów; obecnie klik odbiera kolejne itemy sekwencyjnie.
