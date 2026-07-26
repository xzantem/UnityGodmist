# Godmist - roadmap implementacji

## Cel pierwszego etapu

Nie zaczynamy od całej gry naraz. Celem pierwszego etapu jest zbudowanie grywalnego rdzenia, który potwierdzi, że fundament techniczny i gameplayowy działa.

Pierwszy milestone powinien pozwalać na:

- wejście do testowej sceny,
- postawienie kilku postaci na gridzie,
- rozpoczęcie walki,
- wykonywanie tur,
- ruch,
- użycie 1-2 skilli,
- zadawanie obrażeń,
- zakończenie walki.

To jest pierwszy vertical slice: combat foundation.

## MVP techniczne

### Co ma działać

- 1 scena testowa walki,
- 1 grywalna postać,
- 1-2 przeciwników,
- square grid,
- kolejka tur,
- AP,
- 1 resource, np. Momentum albo Rage,
- ruch po polach,
- 2 umiejętności:
  - prosty atak single target,
  - jeden skill z dodatkowym efektem, np. bleed albo push,
- HP i śmierć lub downed w wersji uproszczonej,
- prosty UI walki,
- koniec walki po zabiciu przeciwników.

### Czego jeszcze nie robić

- miasta,
- lochów proceduralnych,
- questów,
- craftingu,
- handlu,
- pełnego inventory,
- galdurytów,
- pełnego save/load,
- pełnego AI,
- wszystkich klas,
- rozbudowanych tooltipów.

## Proponowane milestone'y

### Milestone 1: Fundament projektu

- struktura folderów,
- assembly definitions,
- podstawowe sceny,
- bazowy bootstrap i game flow,
- wspólne enumy i ID policy.

### Milestone 2: Runtime postaci

- `CharacterDefinition`,
- `CharacterClassData`,
- `CharacterInstance`,
- `CharacterStats`,
- `Stat`,
- `ResourcePool`,
- `ResistanceSet`,
- `ShieldPool`.

### Milestone 3: Fundament walki

- `BattleUnit`,
- `CombatSession`,
- `BattleGrid`,
- `GridPosition`,
- kolejka tur,
- AP,
- zmiana aktywnej jednostki,
- end turn.

### Milestone 4: Skille i efekty

- `SkillData`,
- targeting,
- `SkillEffectData`,
- prosty execution pipeline,
- `DealDamage`,
- `ApplyStatus`.

### Milestone 5: Minimalne UI

- panel aktywnej postaci,
- skill bar,
- kolejka tur,
- przycisk zakończenia tury,
- podstawowy inspect celu.

### Milestone 6: Minimalne AI i win/lose

- wróg wykonuje prostą turę,
- victory / defeat,
- restart test fight.

## Struktura folderów w Unity

```text
Assets/
  _Project/
    Art/
      Icons/
      Sprites/
      UI/
    Audio/
    Prefabs/
      Characters/
      Combat/
      UI/
      World/
    Scenes/
      Bootstrap/
      Menu/
      City/
      Dungeon/
      Combat/
      Sandbox/
    Scripts/
      Core/
      Data/
      Runtime/
      Systems/
      UI/
      Views/
      Editor/
    ScriptableObjects/
      Characters/
      Classes/
      Skills/
      Statuses/
      Items/
      NPC/
      Quests/
      Dungeons/
    UI/
      UXML-or-Prefabs/
    Resources/
    Addressables/
```

Jeśli chcesz bardziej kodowy podział, to w `Scripts/`:

```text
Scripts/
  Core/
    Game/
    Events/
    Utilities/
    Serialization/
  Data/
    Characters/
    Combat/
    Skills/
    Items/
    Quests/
    World/
  Runtime/
    Characters/
    Combat/
    Skills/
    Items/
    Quests/
    World/
  Systems/
    Combat/
    Dungeon/
    Inventory/
    Quests/
    SaveLoad/
    NPC/
  Views/
    Characters/
    Combat/
    UI/
    World/
  Editor/
```

## Assembly Definitions

Proponowane assembly:

- `Godmist.Core`,
- `Godmist.Data`,
- `Godmist.Runtime`,
- `Godmist.Systems`,
- `Godmist.Views`,
- `Godmist.UI`,
- `Godmist.Editor`.

Proponowane zależności:

- `Core` -> nic albo minimum,
- `Data` -> `Core`,
- `Runtime` -> `Core`, `Data`,
- `Systems` -> `Core`, `Data`, `Runtime`,
- `Views` -> `Core`, `Runtime`, `Systems`,
- `UI` -> `Core`, `Runtime`, `Systems`,
- `Editor` -> wszystko potrzebne edytorowo.

## Zasady architektoniczne

### Definicje

Statyczne dane. Najlepiej `ScriptableObject`, np. `CharacterDefinition`, `SkillData`.

### Runtime

Aktualny stan gry. Zwykłe klasy C#, np. `CharacterInstance`, `BattleUnit`.

### Systems

Logika operacyjna, np. `CombatService`, `TurnOrderService`.

### Views

`MonoBehaviour`, czyli scena, input, animacje, prezentacja.

Przykładowe zasady:

- `CharacterView` nie liczy obrażeń,
- `SkillButtonView` nie decyduje, czy skill można użyć,
- `CombatService` nie powinien zależeć od konkretnego panelu UI.

## Minimalny model danych na start

### Data

- `CharacterDefinition`,
- `CharacterClassData`,
- `SkillData`,
- `StatusEffectData`.

### Runtime

- `CharacterInstance`,
- `BattleUnit`,
- `CombatSession`,
- `ResourcePool`,
- `PassiveEffectRuntime` albo prostszy `StatusRuntime`.

### Combat support

- `BattleGrid`,
- `GridCellState`,
- `GridPosition`.

## Pierwszy vertical slice combat

### Postać gracza

- Jerzy / zwiadowca,
- HP,
- AP,
- Momentum,
- 2 skille:
  - `Knife Toss`,
  - `Dash` albo prosty `Attack`.

### Przeciwnicy

- 2 szkielety albo bandyci,
- proste AI:
  - jeśli w zasięgu, atakuj,
  - jeśli nie, podejdź.

### Scena

- mała plansza 8x8 albo 10x10,
- kilka przeszkód,
- start combat od razu po wejściu do sceny.

### UI

- aktywna postać,
- HP/AP/resource,
- skill buttons,
- end turn,
- turn order.

To wystarczy, żeby poczuć realny postęp.

## Kolejność implementacji klas

1. `Stat`
2. `CharacterStats`
3. `ResourcePool`
4. `CharacterDefinition`
5. `CharacterClassData`
6. `CharacterInstance`
7. `GridPosition`
8. `BattleGrid`
9. `BattleUnit`
10. `CombatSession`
11. `SkillData`
12. `SkillEffectData`
13. `CombatService`
14. `SkillExecutionService`
15. `CharacterView`
16. `CombatHudView`

Najpierw fundament danych, potem walka, potem widok.

## Plan na pierwszy tydzień

### Dzień 1

- utworzenie projektu / folderów / scen,
- bazowe enumy,
- polityka ID,
- pierwszy bootstrap.

### Dzień 2

- `CharacterDefinition`,
- `CharacterClassData`,
- `CharacterInstance`,
- staty i resource.

### Dzień 3

- `BattleGrid`,
- `GridPosition`,
- ruch po gridzie.

### Dzień 4

- `BattleUnit`,
- `CombatSession`,
- tury i AP.

### Dzień 5

- `SkillData`,
- prosty single-target damage skill.

### Dzień 6

- minimalne UI,
- end turn,
- aktywna jednostka.

### Dzień 7

- prosty przeciwnik AI,
- victory / defeat,
- cleanup.

## Definition of done dla pierwszego etapu

Etap uznajemy za zamknięty, jeśli:

- można odpalić scenę walki,
- tury działają,
- ruch działa,
- skill działa,
- obrażenia działają,
- przeciwnik wykonuje turę,
- walka się kończy,
- UI pokazuje najważniejsze rzeczy,
- kod nie jest dramatycznie splątany.

Nie musisz mieć jeszcze:

- pięknych animacji,
- pełnego inspect panelu,
- wszystkich statusów,
- save/load,
- contentu.

## Czego pilnować od początku

- nie wrzucać logiki do UI,
- nie wrzucać logiki do `CharacterView`,
- nie robić 50 skilli na start,
- nie robić pełnego inventory przed combat core,
- nie robić proceduralnych lochów przed działającą walką,
- nie robić wszystkiego przez `Update()`,
- robić debug przyciski i testowe sceny.

## Rekomendacja końcowa

Najlepszy start dla tego projektu to Combat Sandbox:

- runtime postaci,
- grid,
- battle unit,
- tury,
- 2 skille,
- prosty HUD.

To da najszybszy efekt i od razu zweryfikuje architekturę.
