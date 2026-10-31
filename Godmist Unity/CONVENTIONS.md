## Status dokumentu

Ten dokument jest normatywnym źródłem prawdy dla aktualnych konwencji
architektonicznych i kodowych projektu.

Jeżeli `ARCHITECTURE_SKETCH.md` zawiera starszą propozycję sprzeczną
z tym dokumentem, pierwszeństwo ma `CONVENTIONS.md`.

Jeżeli świadomie zmieniamy konwencję lub zasadę architektoniczną,
należy zaktualizować ten dokument razem ze zmianą.

# Godmist - conventions

Ten plik opisuje podstawowe konwencje projektu Unity dla gry `Godmist`.
Jego celem jest utrzymanie spójności architektury, nazewnictwa, identyfikatorów i podziału odpowiedzialności pomiędzy warstwami projektu.

Jeżeli później jakaś decyzja zostanie świadomie zmieniona, należy zaktualizować ten plik, zamiast polegać na ustaleniach „z pamięci”.

## 1. Główna zasada architektoniczna

Projekt jest podzielony na cztery podstawowe warstwy:

- `Data`
- `Runtime`
- `Systems`
- `Views`

Do tego dochodzi warstwa:

- `Core`
- `Editor`

Każda z tych warstw ma własną odpowiedzialność i nie powinna przejmować ról innych warstw.

## 2. Podział odpowiedzialności

### 2.1. Core

Warstwa `Core` zawiera:

- podstawowe enumy,
- wspólne typy,
- bazowe interfejsy,
- utility,
- pomocnicze struktury współdzielone przez resztę projektu.

`Core` nie powinno zależeć od żadnej wyższej warstwy.

### 2.2. Data

Warstwa `Data` zawiera definicje statycznych danych, najczęściej jako `ScriptableObject`.

Przykłady:

- `CharacterDefinition`
- `CharacterClassData`
- `SkillData`
- `ItemDefinition`
- `QuestDefinition`

`Data` opisuje:

- czym coś jest,
- jakie ma bazowe właściwości,
- jakie ma konfiguracje,
- jakie ma reguły zapisane w danych.

`Data` nie przechowuje aktualnego stanu rozgrywki.

### 2.3. Runtime

Warstwa `Runtime` zawiera aktualny stan gry.

Przykłady:

- `CharacterInstance`
- `BattleUnit`
- `CombatSession`
- `ResourcePool`
- `StatusRuntime`

`Runtime` opisuje:

- co teraz istnieje,
- jaki jest aktualny stan postaci,
- jakie ma HP, AP, zasoby, statusy,
- co dzieje się w aktualnej sesji gry.

`Runtime` powinno być zwykłym kodem C#, a nie `MonoBehaviour`, jeśli nie ma ku temu bardzo mocnego powodu.

### 2.4. Systems

Warstwa `Systems` zawiera logikę operacyjną.

Przykłady:

- `CombatService`
- `TurnOrderService`
- `SkillExecutionService`
- `InventoryService`
- `QuestService`
- `DungeonService`
- `SaveLoadService`

`Systems` odpowiada za:

- wykonywanie reguł gry,
- koordynację przebiegu mechanik,
- zmianę runtime state,
- obsługę flow systemów.

`Systems` nie powinno być zależne od UI ani sceny.

#### Baza ataku zależna od poziomu

`CharacterInstanceFactory` dla bazy startowej i `CharacterLevelGrowthSystem` po
pełnym wzroście poziomu wyliczają `Stat.BaseValue` z definicji. Runtime `Stat.BaseValue` dla
MinAttack/MaxAttack jest bezpośrednim wynikiem danych bazowych i przyrostów.
Baza ataku dopuszcza wartości ułamkowe. Dawna korekta przeciwników ×0,85 jest
wliczona w bazę i wszystkie składowe przyrostu w assetach; nie istnieje osobny
mnożnik postaci. Wyposażenie, efekty, premie pochodne i limity działają po bazie.
Odtwarzanie zapisu przelicza bazę z aktualnej definicji, bez mnożenia zachowanych efektów.
Atak przeciwnika edytuje się w CharacterDefinition (Inspector/import danych).
Historyczne generatory Lasu i Katakumb zachowują istniejące dane ATK; nie tworzą
nowych CharacterDefinitions ze starych tabel balansu. Brak takiej definicji jest jawnym błędem setupu.

### 2.5. Views

Warstwa `Views` zawiera:

- `MonoBehaviour`,
- obiekty sceny,
- HUD,
- integrację z inputem,
- warstwę prezentacji,
- połączenie runtime z tym, co gracz widzi.

Przykłady:

- `CharacterView`
- `CombatHudView`
- `GridCellView`
- `SkillButtonView`

`Views` może znać `Runtime` i `Systems`, ale logika gameplayowa nie powinna zależeć od `Views`.

### 2.6. Editor

Warstwa `Editor` zawiera narzędzia edytorowe.

Przykłady:

- custom inspectory,
- walidatory danych,
- debug windows,
- generatory pomocnicze.

Ta warstwa jest tylko dla Unity Editor.

## 3. Zasada kierunku zależności

Zależności mają iść w dół architektury:

- `Core`
- `Data -> Core`
- `Runtime -> Core, Data`
- `Systems -> Core, Data, Runtime`
- `Views -> Core, Data, Runtime, Systems`
- `Editor -> wszystko potrzebne`

Nie robimy zależności odwrotnych, np.:

- `Runtime -> Views`
- `Data -> Systems`
- `Core -> Runtime`

## 4. Konwencje nazewnicze klas

### 4.1. Definicje danych

Dla definicji używamy końcówki:

- `Definition`
- `Data`

Przykłady:

- `CharacterDefinition`
- `CharacterClassData`
- `SkillData`
- `PassiveEffectData`
- `ItemDefinition`

Zasada:

- jeśli coś jest statyczną definicją bytu w grze, powinno mieć nazwę sugerującą definicję danych.

### 4.2. Runtime state

Dla aktualnego stanu używamy końcówki:

- `Instance`
- `Runtime`
- `Session`
- `Pool`
- `State`

Przykłady:

- `CharacterInstance`
- `SkillRuntimeState`
- `QuestInstance`
- `CombatSession`
- `ResourcePool`
- `GridCellState`

### 4.3. Systems

Dla logiki operacyjnej używamy końcówki:

- `Service`
- opcjonalnie `Manager`, jeśli to naprawdę nadrzędny koordynator

Preferowana forma:

- `CombatService`
- `InventoryService`
- `QuestService`

Nie należy nadużywać słowa `Manager`.

### 4.4. Views

Dla obiektów sceny i UI używamy końcówki:

- `View`

Przykłady:

- `CharacterView`
- `CombatHudView`
- `NpcView`
- `TooltipView`

### 4.5. Interfejsy

Dla interfejsów używamy prefiksu:

- `I`

Przykłady:

- `IInteractable`
- `IIdentifiable`
- `ITargetable`

## 5. Konwencje identyfikatorów

### 5.1. Ogólna zasada

Wszystkie logiczne definicje danych powinny mieć stabilne tekstowe ID.

Przykłady:

- `characterId`
- `classId`
- `skillId`
- `itemId`
- `questId`
- `npcId`
- `dungeonId`

ID nie powinny być tłumaczone, lokalizowane ani zależne od wyświetlanej nazwy.

### 5.2. Format ID

Preferowany format:

- małe litery,
- słowa oddzielone kropką albo podkreślnikiem,
- bez spacji,
- bez polskich znaków.

Przykłady:

- `jerzy`
- `class.scout`
- `skill.scout.knife_toss`
- `quest.main.prologue_01`
- `npc.arungard.blacksmith_01`

Najważniejsze jest zachowanie spójności w całym projekcie.

### 5.3. Runtime instance IDs

Definicja i instancja to nie to samo.

Przykład:

- `characterId = jerzy` oznacza typ/postać logiczną,
- runtime/save może dodatkowo mieć osobne `instanceId`, jeśli potrzeba.

Dla itemów instancyjnych:

- każdy item ma osobne `instanceId`,
- nawet jeśli w UI jest stackowany logicznie.

## 6. Konwencje pól

### 6.1. Nazwy pól

Nazwy pól w kodzie:

- camelCase dla pól i parametrów,
- PascalCase dla klas, enumów, właściwości publicznych jeśli używasz properties.

Przykład:

- `characterId`
- `currentHealth`
- `maxValue`
- `resourceType`

### 6.2. Bool

Bool powinny brzmieć jak pytanie o stan.

Przykłady:

- `isMainCharacter`
- `isUnlocked`
- `isToggle`
- `isBlocking`
- `isCompleted`

## 7. Enumy

Enumy trzymamy w `Core`, jeśli są współdzielone przez wiele systemów.

Przykłady:

- `ResourceType`
- `CharacterLifeState`
- `CharacterAvailabilityState`
- `DamageType`
- `TargetType`
- `TeamRelationType`

Nie używamy gołych stringów tam, gdzie stan jest zamkniętym zbiorem wartości.

## 8. ScriptableObject conventions

### 8.1. Co powinno być ScriptableObject

ScriptableObject nadaje się do:

- definicji postaci,
- definicji klas,
- definicji skilli,
- definicji statusów,
- definicji itemów,
- definicji questów,
- definicji lochów,
- innych danych konfiguracyjnych.

### 8.2. Czego nie pakować do ScriptableObject

Nie wrzucamy tam:

- aktualnego HP,
- aktualnego AP,
- aktualnego stanu walki,
- aktualnego postępu questu,
- aktualnego stanu ekwipunku.

To należy do runtime/save state.

## 9. MonoBehaviour conventions

### 9.1. MonoBehaviour tylko tam, gdzie potrzebne

`MonoBehaviour` używamy tylko wtedy, gdy coś naprawdę musi:

- istnieć na scenie,
- reagować na lifecycle Unity,
- mieć transform,
- obsługiwać input,
- sterować widokiem lub UI.

### 9.2. Czego nie robić w MonoBehaviour

Nie wkładamy tam całej logiki:

- liczenia obrażeń,
- rozstrzygania kolejki tur,
- logiki questów,
- save/load,
- definicji systemowych.

## 10. Konwencje scen

Sceny umieszczamy w:

- `Assets/_Project/Scenes/`

Podział scen:

- `Bootstrap`
- `Sandbox`
- `Menu`
- `City`
- `Dungeon`
- `Combat`

Sandboxy służą do testowania systemów w izolacji.

## 11. Konwencje prefabów

Prefaby umieszczamy w:

- `Assets/_Project/Prefabs/`

Podział:

- `Characters`
- `Combat`
- `Dungeon`
- `Interactables`
- `NPC`
- `UI`

Prefab scenowy nie powinien być źródłem prawdy o logice domenowej.

## 12. Konwencje plików UI

UI traktujemy jako warstwę prezentacji.

Zasady:

- HUD i panele nie podejmują decyzji gameplayowych,
- UI pyta systemy o stan i wywołuje akcje przez warstwę logiczną,
- tooltipy i inspect panele są prezentacją danych, a nie ich źródłem.

### Wspólne nazwy statystyk, modyfikatorów i odporności

Źródłem nazw w polskim UI jest `Scripts/Core/Utilities/UiTerms.cs`:
`Stat`, `Variable`, `Resistance`, `Damage`, `Status`, `Galduryte` oraz stałe etykiet.
Nowe widoki i generowane tooltipy korzystają z tego słownika zamiast lokalnych
mapowań i własnych synonimów. Uzgodnione nazwy to m.in. „Szansa krytyczna”,
„Siła leczenia”, „Penetracja fizyczna” i „Penetracja magiczna”. W sekcji
„Odporności” krótkie nazwy wierszy pochodzą z `ResistanceRow` tego samego słownika.

Kontekst zasobu przekazujemy jawnie: postać, jej umiejętności, drzewko i status
o znanym odbiorcy używają many/furii/impetu. Dla statusu liczy się odbiorca,
nie rzucający. Przy nieznanym zasobie celu zachowujemy „zasób”. Mikstury,
galduryty i ogólne przedmioty nadal używają „zasób”; sprzęt przypisany do klasy
może używać nazwy zasobu tej klasy. Nie przenosimy kontekstu oglądającej postaci
na ogólny tooltip przedmiotu.

Nazwy nie określają operacji matematycznej: flat Unik/Celność to punkty,
addytywna zmiana prawdopodobieństwa to `pp`, zmiana względna to `%`.
Regeneracja, generowanie i odzysk zasobu oraz odporność i redukcja obrażeń
pozostają odrębnymi pojęciami. Nie zmieniamy wartości ani zasad przez zmianę nazwy.
`UiTerms.Present` obsługuje historyczne synonimy w istniejących opisach podczas
prezentacji, zachowując dane źródłowe i linki TMP. Nie zastępuje typowanego
słownika przy dodawaniu nowych etykiet. Długie opisy mechanik zmieniamy osobno.

## 13. Konwencje dotyczące debugowania

Debug i narzędzia są częścią procesu produkcyjnego, a nie dodatkiem „na później”.

Preferowane narzędzia:

- debug button start combat,
- debug refill AP/resource,
- debug kill target,
- debug spawn enemy,
- walidator duplikatów ID,
- podgląd runtime state w sandboxie.

## 14. Konwencje dotyczące save/load

- Save state powinien być odseparowany od definicji i od sceny.
- DTO do zapisu powinny być prostymi strukturami danych.
- Save/load nie powinien zależeć od `MonoBehaviour`.
- `instanceId` i logiczne `...Id` muszą być jasno rozróżnione.

## 15. Konwencje komentarzy i organizacji kodu

- Komentarze dodajemy tylko wtedy, gdy naprawdę pomagają zrozumieć intencję.
- Nie komentujemy oczywistości.
- Jedna klasa = jedna główna odpowiedzialność.
- Gdy plik robi zbyt wiele rzeczy, trzeba go rozbić.

## 16. Zasady praktyczne na start

Na początku projektu pilnujemy szczególnie tego:

- nie wrzucać logiki do UI,
- nie wrzucać logiki do `CharacterView`,
- nie robić wielkiego `GameManagera`,
- nie mieszać definicji danych z runtime state,
- nie odkładać save/load „na kiedyś” w architekturze,
- utrzymywać spójne ID i nazewnictwo.

## 17. Formatowanie warunków

- Krótkie warunki `if` zapisujemy w jednej linii, razem z bezpośrednim `return`, jeśli nadal pozostają czytelne.
- Dłuższe warunki można złamać na drugiej linii, ale kolejne operatory logiczne powinny pozostawać przy wyrażeniach, które łączą. Nie rozbijamy każdego warunku na osobną linię bez potrzeby.

Preferowany styl:

```csharp
if (skill == null || session == null || session.ActiveUnit == null
    || !session.ActiveUnit.IsPlayerSide
    || session.Outcome != CombatOutcomeState.Ongoing) return;
```

## 18. Zasada końcowa

Jeśli pojawia się wątpliwość „gdzie coś powinno trafić?”, pytanie pomocnicze brzmi:

- czy to jest definicja?
- czy to jest aktualny stan?
- czy to jest logika wykonania?
- czy to jest tylko prezentacja?

Odpowiedź na to pytanie powinna wskazać właściwą warstwę.
