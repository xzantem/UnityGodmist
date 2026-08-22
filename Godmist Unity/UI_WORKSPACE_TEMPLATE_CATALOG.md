# UI Workspace — katalog template'ów v1

Ten katalog jest źródłem decyzji o budowaniu nowych okien w UI Workspace. Dotyczy wyłącznie prezentacji UGUI; dane, walidacja i mutacje stanu pozostają w istniejących systemach oraz view-modelach/panelach.

## Tokeny wspólne

Jedynym źródłem kolorów, typografii i wymiarów nowych okien jest asset `UIWorkspaceStylePalette`.

- Wszystkie odległości są wielokrotnością `Spacing Unit` (domyślnie `4`).
- Wewnętrzny padding okna: `16`; sekcji: `12`; standardowa wysokość kontrolki: `36`.
- Tekst: `Title 24`, `Heading 18`, `Body 15`, `Detail 13`, `Caption 11`. Do assetu należy przypisać jeden wspólny font TMP.
- Ikony zachowują proporcje: `24` w kontrolkach, `48` w slotach. Nie skalować ikon rastrowych ponad ich importową rozdzielczość.
- Kolory ról: ciemne granatowe `Window/Header/Section`, `Neutral` dla normalnej ramki, bursztynowy `Selected`, niebieski `Hover`, zielony `Positive`, czerwony `Negative`, żółty `Warning`.
- Stan disabled używa przygaszonego tekstu oraz `Selectable.interactable = false`; nie zastępuje go samą zmianą koloru.
- `UIWorkspaceStyleApplier` jest jedynym komponentem, który kopiuje tokeny do `Image`, `TMP_Text` i `Selectable`. Nie wpisujemy nowych stałych kolorów do viewów.

## Template'y

### `PF_UIW_Window`

**Przeznaczenie:** korzeń każdego niezależnego floating window w następnej karcie.

**Warianty:** `Standard`, `Wide`, `Compact`; wariant zmienia wyłącznie początkowy rozmiar `RectTransform`, nie hierarchię ani tokeny.

**Hierarchia:**

```text
PF_UIW_Window
├── Border
├── Header
│   ├── Title
│   └── CloseButton
└── Content
```

**Wymagane komponenty:** root `Image`, `CanvasGroup`, `UIWorkspaceStyleApplier`; `Header` `Image` + applier; `Title` `TextMeshProUGUI` + applier; `CloseButton` `Button` + `Image` + applier. Card floating-window v1 doda wspólną logikę drag/resize/focus do tego samego rootu.

**Użycie:** Character Info, Inventory, Skill Loadout i Skill Trees. Nie używać dla tooltipów, potwierdzeń ani pojedynczych kart listy.

### `PF_UIW_Section`

**Przeznaczenie:** grupowanie informacji w obrębie `Content` okna.

**Warianty:** `Plain` (sam content), `Titled` (z `SectionTitle`), `Scrollable` (z `Scroll View`).

**Hierarchia:**

```text
PF_UIW_Section
├── Border
├── SectionTitle (tylko Titled)
└── Content
```

**Wymagane komponenty:** root `Image` + applier; `Border` `Image` + applier; opcjonalny tytuł TMP + applier. `Content` może dostać `VerticalLayoutGroup`, `GridLayoutGroup` albo `Scroll Rect` dopiero, gdy tego wymaga jego dane użycie.

**Użycie:** sekcje statystyk Character Info, equipment, lista inventory i lista skilli. Nie zakładać jednej uniwersalnej grupy layoutu na root.

### `PF_UIW_Button`

**Przeznaczenie:** jawna akcja użytkownika.

**Warianty:** `Primary`, `Secondary`, `Danger`, `Icon`.

**Hierarchia:**

```text
PF_UIW_Button
├── Border
├── Icon (opcjonalnie)
└── Label (opcjonalnie)
```

**Wymagane komponenty:** root `Button`, `Image`, `UIWorkspaceStyleApplier`; `Border` `Image` + applier; `Label` TMP + applier. `Icon` ma `Image`, `preserveAspect = true`, `raycastTarget = false`.

**Użycie:** Close, Equip, Unequip, Confirm. Wybór w liście nie jest `Primary`; używa `PF_UIW_ListRow`.

### `PF_UIW_ListRow`

**Przeznaczenie:** selectable element listy, np. item, postać lub skill.

**Warianty:** `Text`, `IconText`, `Detailed`.

**Hierarchia:**

```text
PF_UIW_ListRow
├── Border
├── Icon (opcjonalnie)
└── TextRoot
    ├── Title
    └── Detail (opcjonalnie)
```

**Wymagane komponenty:** root `Button`, `Image`, `LayoutElement`, applier; `Border` Image + applier; teksty TMP + osobne appliers. Stan wybrany renderuje `Selected`; hover renderuje `Hover`; disabled jest nieinteraktywny.

**Użycie:** inwentarz, lista postaci, dostępne skille. Nie używać jako slotu drag-and-drop.

### `PF_UIW_ItemSlot`

**Przeznaczenie:** pojedynczy slot equipmentu lub karta skilla ze strukturą wymaganą również dla drag ghosta.

**Warianty:** `Equipment`, `Skill`, `Empty`, `Locked`.

**Hierarchia:**

```text
PF_UIW_ItemSlot
├── Border
├── Icon
└── Overlay (opcjonalnie: lock, rank, feedback)
```

**Wymagane komponenty:** root `Button` lub `Image` zależnie od interakcji, `CanvasGroup`, `Image`, applier; `Border` Image + applier; `Icon` Image z `preserveAspect`; `Overlay` ma wyłączony raycast, o ile konkretny child nie jest kontrolką.

**Użycie:** aktywne skille, dostępne skille, equipment sloty i drag ghost. Drag ghost ma zachować tę samą hierarchię i wizualne tło, ale `CanvasGroup.blocksRaycasts = false`.

### `PF_UIW_Tooltip`

**Przeznaczenie:** warstwa opisu elementu, zawsze nad aktywnym oknem.

**Warianty:** `Short`, `Detailed`, `Comparison`.

**Hierarchia:**

```text
PF_UIW_Tooltip
├── Border
└── Content
    ├── Title
    └── Body
```

**Wymagane komponenty:** root `Image`, `CanvasGroup`, applier; border Image + applier; TMP teksty + applier. Root nie blokuje pointerów, chyba że kolejna karta jawnie wymaga przypiętego tooltipu.

**Użycie:** skille, item inspect i requirementy drzewek. Nie używać jako modal ani części zwykłego Content.

## Reguła wyboru

1. Użyj istniejącego template'u, jeżeli różni się tylko tekst, ikona, dane lub jeden opisany wariant.
2. Złóż widok z istniejących template'ów, gdy potrzebujesz kilku niezależnych ról, np. `Window + Section + ListRow`.
3. Dodaj nowy template tylko, gdy nowa rola powtarza się w co najmniej dwóch planowanych widokach albo istniejący template wymagałby naruszenia swojej hierarchii/odpowiedzialności.
4. Przed dodaniem nowego template'u dopisz go tutaj wraz z wariantami, hierarchią, komponentami, przykładami i ograniczeniami.
5. Nie kopiuj prefabów istniejących legacy paneli jako punktu startowego. Migracje używają katalogu oraz tokenów, a reguły domenowe pozostają w istniejących systemach.
