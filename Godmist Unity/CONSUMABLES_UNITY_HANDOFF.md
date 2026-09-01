# Unity handoff

Karta: `Consumables v1`

## CombatSandbox — przycisk i okno consumables

### `ConsumablesButton`

- Parent: `ActionButtons` — istniejący obiekt z komponentem `CombatActionButtonsView`.
- Typ: Button

#### RectTransform

- Ustaw rozmiar i pozycję jako trzeci przycisk w istniejącym układzie `ActionButtons`, obok `EscapeButton` i `EndTurnButton`.

#### Dodatkowe komponenty

- Ustaw ikonę dowolnego placeholdera przedmiotu; tekst przycisku pozostaw pusty.

#### Referencje

Na `CombatActionButtonsView` przypisz:

- `Sandbox Bootstrap` → komponent `CombatSandboxBootstrap` z istniejącego obiektu bootstrapa sceny (ten sam obiekt wskazany przez `SkillBar/CombatSkillBarView`).
- `Target Selection Controller` → istniejący `CombatTargetSelectionController` (ten sam, który wskazuje `SkillBar/CombatSkillBarView`).
- `Consumables Button` → `ActionButtons/ConsumablesButton/Button`.
- `Consumable Selection Prefab` → asset `PF_UIW_CombatConsumableSelectionWindow` z Project, tak jak `CombatUnitInspectController/Inspect Window Prefab`.

### `ConsumableSelectionWindow` — wariant `PF_UIW_Window`

- Utwórz wariant prefab `PF_UIW_Window` o nazwie `PF_UIW_CombatConsumableSelectionWindow`.
- Zachowaj root `UIWorkspaceWindow`, istniejący chrome okna oraz przycisk `X`.
- Usuń z `Content` wszystkie przykładowe dzieci odziedziczone po bazowym wariancie.
- Nie dodawaj headera ani tytułu. Okno zawiera wyłącznie grid itemów.
- Na root wariantu dodaj `CombatConsumableSelectionView`.

#### RectTransform rootu

- Anchor Min: `(0.5, 0.5)`
- Anchor Max: `(0.5, 0.5)`
- Pivot: `(0.5, 0.5)`
- Pos X: `0`
- Pos Y: `150`
- Width: `420`
- Height: `160`

### `ContentScroll`

- Parent: `Content` wariantu `PF_UIW_Window`.
- Typ: Scroll View.

#### RectTransform

- Anchor Min: `(0, 0)`
- Anchor Max: `(1, 1)`
- Pivot: `(0.5, 0.5)`
- Left: `12`
- Right: `12`
- Top: `12`
- Bottom: `12`

- W `Scroll View` wyłącz poziomy scrollbar; pionowy scrollbar pozostaw włączony.
- Nie dodawaj `ContentSizeFitter` do rootu okna ani do `ContentScroll`.

### `Content`

- Parent: `ContentScroll/Viewport`.
- Typ: Empty.

#### RectTransform

- Anchor Min: `(0, 0)`
- Anchor Max: `(1, 1)`
- Pivot: `(0.5, 0.5)`
- Left: `0`
- Right: `0`
- Top: `0`
- Bottom: `0`

#### Dodatkowe komponenty

- Dodaj `GridLayoutGroup`.
- `Cell Size`: `64 x 64` (wartość startowa; skrypt ustawia ją również runtime).
- `Spacing`: `8 x 8` (wartość startowa; skrypt ustawia ją również runtime).
- `Constraint`: `Fixed Column Count`.
- `Constraint Count`: `8` (wartość maksymalna; skrypt zmniejsza ją dla mniejszej liczby itemów).
- `Start Axis`: `Horizontal`.
- `Start Corner`: `Upper Left`.
- `Child Alignment`: `Upper Left`.
- `Child Force Expand`: off.
- Dodaj `ContentSizeFitter`.
- `Horizontal Fit`: `Unconstrained`.
- `Vertical Fit`: `Preferred Size`.

### `ItemTemplate`

- Parent: `ContentScroll/Viewport/Content`.
- Typ: Button.
- Utwórz jako kopię istniejącego `SkillBar/SkillSlotTemplate`, aby zachować jego dziecko `Icon`, overlay i komponent `CombatSkillSlotView`.
- Przenieś kopię do `ConsumableSelectionWindow/ContentScroll/Viewport/Content`.
- Nazwij ją `ItemTemplate`.
- Pozostaw inactive.
- Nie dodawaj ręcznych listenerów do przycisku; `CombatConsumableSelectionView` ustawia listener podczas tworzenia runtime kopii.

### Komponent `CombatConsumableSelectionView`

Dodaj komponent na root `ConsumableSelectionWindow` i przypisz:

- `Sandbox Bootstrap` → istniejący komponent `CombatSandboxBootstrap`.
- `Target Selection Controller` → istniejący komponent `CombatTargetSelectionController`.
- `Content Root` → `ConsumableSelectionWindow/ContentScroll/Viewport/Content` (Transform z `GridLayoutGroup` i `ContentSizeFitter`).
- `Window` → komponent `UIWorkspaceWindow` z rootu tego samego wariantu.
- `Item Template` → `ConsumableSelectionWindow/ContentScroll/Viewport/Content/ItemTemplate` (komponent `CombatSkillSlotView`).
- `Tooltip View` → istniejący `UIWorkspaceTooltipView` z combat UI.
- `Maximum Columns`: `8`.
- `Maximum Rows`: `8`.
- `Slot Size`: `(64, 64)`.
- `Slot Spacing`: `(8, 8)`.
- `Window Padding`: `(24, 24)`.

Nie dodawaj wariantu jako instancji do sceny. `CombatActionButtonsView` instancjonuje prefab przy pierwszym kliknięciu, pod rodzicem wybranym tak samo jak `CombatUnitInspectController`: najpierw parent aktywnego tooltipu, potem `UIWorkspaceWindowManager.WorkspaceRoot`, a na końcu Canvas combat. Kolejne kliknięcia używają tej samej instancji. `CombatConsumableSelectionView` otwiera ją przez `UIWorkspaceWindow.Open()` i zamyka przez `UIWorkspaceWindow.Close()` przed rozpoczęciem targetowania.

## Definicje consumables

Dla każdego przedmiotu utwórz asset przez `Create > Godmist > Items > Consumable Item Definition`.

Wspólne pola dla potionów:

- `Item Type`: `Potion`.
- `Use Type`: `Potion`.
- `Max Charges`: `5`.
- `Is Stackable`: off.
- `Base Rarity`: `Common`.

Wspólne pola dla bomb:

- `Item Type`: `Tool`.
- `Use Type`: `Bomb`.
- `Max Charges`: `5`.
- `Is Stackable`: off.
- `Base Rarity`: `Common`.

Wspólne pola dla jednorazowych utility:

- `Item Type`: `Tool`.
- `Use Type`: `Utility`.
- `Max Charges`: `1`.
- `Is Stackable`: on.
- `Base Rarity`: `Common`.

### Runtime effects

Nie ustawiaj efektów w `ConsumableItemDefinition`. Generator runtime wypełnia `ConsumableItemInstance.RuntimeEffects`.

### Jednorazowe utility

| Przedmiot | Runtime effect |
| --- | --- | --- |
| Bandaż | `RemoveStatus`, `Bleed` |
| Antidotum | `RemoveStatus`, `Poison` |
| Sucha szmatka | `RemoveStatus`, `Burn` |
| Return scroll | `ReturnToArungard` |

Return scroll działa wyłącznie poza walką: PPM w inventory postaci w lochu przenosi do Arungardu, zachowuje `ActiveDungeon` i nie raportuje ukończenia lochu/questa.

## Runtime action

Nie twórz osobnego `SkillData` assetu dla consumables. Podczas użycia system buduje transient action z `RuntimeEffects`.

Transient action otrzymuje koszt `20`, czyli `ceil(20% maksymalnego AP)`, oraz target/effects z runtime payloadu.

Pasek pozwala aktywnej jednostce użyć maksymalnie jednego `Potion` i jednej `Bomb` na turę. Limit nie dotyczy `Utility`.

Dla runtime efektów potionu generator ustawia target zgodnie z efektem. Dla bomby generator ustawia `Area Around Tile`, zasięg `5` i promień `3`.

Po utworzeniu wszystkich CID dodaj je do `GC_MainContentCatalog > Item Definitions`, aby `createitem <id>` mógł je tworzyć przez debug console.

## Smoke test

1. W `CombatSandbox` aktywna postać mająca potion i bombę widzi je na `ConsumableBar`; licznik ma postać `5/5`.
2. Użycie potionu pobiera `ceil(20% max AP)`, zmniejsza charge do `4/5` i blokuje tylko kolejne potiony tej jednostki w tej turze.
3. Użycie bomby niezależnie blokuje tylko kolejne bomby tej jednostki w tej turze; wybór pola pokazuje zasięg `5` i obszar promienia `3`.
4. Po następnej turze tej samej jednostki oba limity są odblokowane.
5. PPM na `Odnowa zdrowia` poza walką leczy i zmniejsza charge. PPM na return scroll w lochu wraca do Arungardu bez ukończenia lochu. PPM na return scroll poza lochem oraz w walce nie zużywa scrolla.
6. Save/load zachowuje aktualne charge’e refillable consumables.
