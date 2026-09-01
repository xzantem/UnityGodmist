# Unity handoff — okno craftingu kowala (UI Workspace)

## Zasada UX

Okno nie używa tekstowych list-rowów do wyboru. Gracz czyta flow od lewej do prawej: **baza → trzy części → nazwa/jakość → wynik i materiały → craft**. Przedmioty, części i materiały są wyłącznie ikonami w slotach. Nazwa, opis, statystyki oraz koszt są dostępne z istniejącego `UIWorkspaceTooltipView` po hoverze. Tekst stały pozostaje wyłącznie w nagłówkach sekcji, labelach trzech typów części, preview i statusie.

Nie używaj `InventoryEquipmentPanel` ani `PF_UIW_ListRow`.

Każdy nowy `UIWorkspaceStyleApplier` poniżej dostaje `Palette` ustawione na ten sam asset `UIWorkspaceStylePalette`, który jest już przypisany na root `PF_UIW_Window`. Przy każdym obiekcie wymienione są tylko pola, które trzeba zmienić; resztę pozostaw domyślną. Wszystkie inherited appliers z `PF_UIW_Window`, `PF_UIW_Section`, `PF_UIW_ScrollView`, `PF_UIW_ItemSlot` i `PF_UIW_Button` zachowaj bez zmian.

## Zmiany kodu już przygotowane

- `EquipmentPartDefinition` ma teraz `DisplayName`, `Description` i `Icon`. Wypełnij te trzy pola dla każdej craftowalnej części; `DisplayName` może zostać pusty wyłącznie jako tymczasowy fallback do `Id`.
- `EquipmentItemDefinition` pozostaje globalną definicją equipmentu: może reprezentować craftowalną bazę, zwykły loot albo bossowy unikat. Nie dodawaj do niego pola określającego dostępność u kowala ani kolejność menu.
- `BlacksmithRoleData` jest ownerem statycznej oferty tego kowala. Jego lista `ClassOffers` zawiera pary `WeaponBase`, `ArmorBase`; ich wspólna klasa wynika z `RequiredClass` obu definicji. Kolejność elementów listy jest kolejnością kolumn grida.
- `BlacksmithCraftingWindowView` nie używa już `Choice Button Template`. Zamiast niego ma trzy pola: `Equipment Slot Template`, `Part Slot Template`, `Material Slot Template` typu `BlacksmithCraftingSlotView`.
- `BlacksmithCraftingSlotView` ustawia ikonę, highlight wybrania, outline brakującego materiału, badge `posiadane/wymagane`, przycisk kupna i tooltip. Nie dodawaj ręcznych UnityEventów do instancji runtime.
- Preview wyniku używa opcjonalnych pól `Preview Tooltip Slot`, `Preview Name Text` i `Preview Summary Text`. Slot pokazuje wyłącznie ikonę; po prawej są nazwa i jedna linia identyfikacji, a hover nad slotem pokazuje jakość, losowaną rzadkość i statystyki w tooltipie. `Preview Text` pozostaje bezpiecznym fallbackiem, gdy nowe pola nie są podpięte.

## Prefab i pełna hierarchia

Utwórz wariant `PF_UIW_BlacksmithCraftingWindow` z istniejącego `PF_UIW_Window`. Umieść instancję pod `Canvas/UIWorkspaceLayer`.

```text
Canvas
└── UIWorkspaceLayer
    └── PF_UIW_BlacksmithCraftingWindow
        ├── Border                                      [inherited]
        ├── Header                                      [inherited]
        │   ├── Title                                   [inherited]
        │   └── CloseButton                             [inherited]
        ├── Content                                     [inherited]
        │   └── CraftingBody
        │       ├── AssemblyColumn
        │       │   ├── BaseSection                     [PF_UIW_Section/Titled variant]
        │       │   │   └── Content
        │       │   │       └── BaseGrid                [dynamic]
        │       │   ├── PartsSection                    [PF_UIW_Section/Titled variant]
        │       │   │   └── Content
        │       │   │       └── PartColumns
        │       │   │           ├── WeaponHeadStage
        │       │   │           │   ├── StageLabel
        │       │   │           │   └── PartScroll      [PF_UIW_ScrollView/Grid variant]
        │       │   │           │       └── Viewport/Content [dynamic]
        │       │   │           ├── WeaponBinderStage
        │       │   │           │   ├── StageLabel
        │       │   │           │   └── PartScroll/Viewport/Content [dynamic]
        │       │   │           ├── WeaponHandleStage
        │       │   │           │   ├── StageLabel
        │       │   │           │   └── PartScroll/Viewport/Content [dynamic]
        │       │   │           ├── ArmorPlateStage
        │       │   │           │   ├── StageLabel
        │       │   │           │   └── PartScroll/Viewport/Content [dynamic]
        │       │   │           ├── ArmorBinderStage
        │       │   │           │   ├── StageLabel
        │       │   │           │   └── PartScroll/Viewport/Content [dynamic]
        │       │   │           └── ArmorBaseStage
        │       │   │               ├── StageLabel
        │       │   │               └── PartScroll/Viewport/Content [dynamic]
        │       │   └── SettingsSection                 [PF_UIW_Section/Titled variant]
        │       │       └── Content
        │       │           ├── CustomNameInput          [TMP_InputField]
        │       │           └── QualityDropdown          [TMP_Dropdown]
        │       └── SummaryColumn
        │           ├── PreviewSection                  [PF_UIW_Section/Titled variant]
        │           │   └── Content                     [HorizontalLayoutGroup]
        │           │       ├── ResultIconSlot          [instancja PF_UIW_ItemSlot + BlacksmithCraftingSlotView]
        │           │       ├── PreviewNameText          [TextMeshProUGUI]
        │           │       ├── PreviewSummaryText       [TextMeshProUGUI]
        │           │       └── PreviewText              [TextMeshProUGUI, fallback]
        │           ├── MaterialsSection                [PF_UIW_Section/Titled variant]
        │           │   └── Content
        │           │       └── MaterialsScroll          [PF_UIW_ScrollView/Grid variant]
        │           │           └── Viewport/Content    [dynamic]
        │           ├── StatusText                      [TextMeshProUGUI]
        │           └── CraftButton                     [PF_UIW_Button/Primary variant]
        ├── ResizeHandle                                [inherited]
        └── Templates
            ├── EquipmentSlotTemplate                   [instancja PF_UIW_ItemSlot]
            ├── PartSlotTemplate                        [instancja PF_UIW_ItemSlot]
            └── MaterialSlotTemplate                    [instancja PF_UIW_ItemSlot]
```

`WeaponHeadStage`, `WeaponBinderStage`, `WeaponHandleStage`, `ArmorPlateStage`, `ArmorBinderStage` i `ArmorBaseStage` są wszystkie statycznie obecne. Skrypt włącza dokładnie trzy, zależnie od wybranej bazy. Nie ustawiaj ich aktywności jako część konfiguracji prefabu.

## Korzeń i dwa słupy

### `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow`

- **Rodzic:** `Canvas/UIWorkspaceLayer`.
- **Typ bazowy:** prefab variant `PF_UIW_Window`.
- **RectTransform:** Anchor Min `(0.5, 0.5)`, Anchor Max `(0.5, 0.5)`, Pivot `(0.5, 0.5)`, Pos X `0`, Pos Y `0`, Width `1100`, Height `720`.
- **`UIWorkspaceWindow`:** `Minimum Size` `(900, 600)`, `Maximum Size` `(1200, 900)`, `Allow Horizontal Resize` = `true`, `Allow Vertical Resize` = `true`.
- **Zmiany względem bazowego:** dodaj tylko komponent `BlacksmithCraftingWindowView`; zachowaj wszystkie inherited komponenty i referencje `UIWorkspaceWindow`.
- `Header/Title`: tekst `Kuźnia Arungardu`.

### `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody`

- **Rodzic:** `Content`.
- **Typ:** pusty GameObject UI.
- **RectTransform:** Anchor Min `(0, 0)`, Anchor Max `(1, 1)`, Pivot `(0.5, 0.5)`, Left `16`, Right `16`, Top `16`, Bottom `16`.
- **Dodaj `HorizontalLayoutGroup`:** Padding `0`; Spacing `12`; Child Alignment `Upper Center`; Control Child Size Width `true`, Height `true`; Child Force Expand Width `false`, Height `true`.
- Nie dodawaj `ContentSizeFitter`.

### `.../CraftingBody/AssemblyColumn`

- **Rodzic:** `CraftingBody`.
- **Typ:** pusty GameObject UI.
- **RectTransform:** pozostaw Anchor Min `(0.5, 0.5)`, Anchor Max `(0.5, 0.5)`, Pivot `(0.5, 0.5)`; pozycję i rozmiar ustawia `CraftingBody/HorizontalLayoutGroup` z `LayoutElement`, więc nie wpisuj ręcznie Width, Height ani Pos X/Y.
- **Dodaj `LayoutElement`:** Preferred Width `610`, Flexible Width `0`, Flexible Height `1`.
- **Dodaj `VerticalLayoutGroup`:** Padding `0`; Spacing `12`; Child Alignment `Upper Center`; Control Child Size Width `true`, Height `true`; Child Force Expand Width `true`, Height `false`.

### `.../CraftingBody/SummaryColumn`

- **Rodzic:** `CraftingBody`.
- **Typ:** pusty GameObject UI.
- **RectTransform:** pozostaw Anchor Min `(0.5, 0.5)`, Anchor Max `(0.5, 0.5)`, Pivot `(0.5, 0.5)`; pozycję i rozmiar ustawia `CraftingBody/HorizontalLayoutGroup` z `LayoutElement`, więc nie wpisuj ręcznie Width, Height ani Pos X/Y.
- **Dodaj `LayoutElement`:** Min Width `360`, Flexible Width `1`, Flexible Height `1`.
- **Dodaj `VerticalLayoutGroup`:** Padding `0`; Spacing `12`; Child Alignment `Upper Center`; Control Child Size Width `true`, Height `true`; Child Force Expand Width `true`, Height `false`.

## Sekcje po lewej

### `.../AssemblyColumn/BaseSection`

- **Rodzic:** `AssemblyColumn`.
- **Typ bazowy:** `PF_UIW_Section/Titled`.
- **Zmiany:** `SectionTitle` = `1. Wybierz bazę`; root `LayoutElement`: Preferred Height `184`, Flexible Height `0`.
- W `Content` usuń layout odziedziczony przez wariant, jeśli występuje; childem jest tylko `BaseScroll`.

### `.../BaseSection/Content/BaseGrid`

- **Rodzic:** `BaseSection/Content`.
- **Typ:** pusty GameObject UI. Nie używaj `ScrollRect`, `PF_UIW_ScrollView` ani `ContentSizeFitter`.
- **RectTransform:** Anchor Min `(0, 0)`, Anchor Max `(1, 1)`, Pivot `(0.5, 0.5)`, Left `0`, Right `0`, Top `0`, Bottom `0`.
- **Dodaj `GridLayoutGroup`:** Cell Size `(64, 64)`, Spacing `(8, 8)`, Padding Left/Right/Top/Bottom `0`, Start Corner `Upper Left`, Start Axis `Horizontal`, Constraint `Fixed Column Count`, Count `4`, Child Alignment `Upper Center`.
- `BaseGrid` zaczyna puste. Skrypt umieszcza tu instancje `EquipmentSlotTemplate`.
- W `BlacksmithRoleData/ClassOffers` dodaj cztery elementy w kolejności `Warrior`, `Scout`, `Mage`, `Paladin`. Przypisz im odpowiednio pary: `Długi miecz` + `Kolczuga`; `Miecz i sztylet` + `Tunika`; `Różdżka` + `Szata`; `Młot` + `Zbroja płytowa`. W każdej parze oba EID muszą mieć ten sam `RequiredClass`. Widok pokazuje wszystkie `WeaponBase` jako pierwszy rząd i wszystkie `ArmorBase` jako drugi. Nie wpisuj unikatów bossowych ani equipmentu, który nie ma być craftowalny.

### `.../AssemblyColumn/PartsSection`

- **Rodzic:** `AssemblyColumn`.
- **Typ bazowy:** `PF_UIW_Section/Titled`.
- **Zmiany:** `SectionTitle` = `2. Złóż części`; root `LayoutElement`: Preferred Height `276`, Flexible Height `0`.

### `.../PartsSection/Content/PartColumns`

- **Rodzic:** `PartsSection/Content`.
- **Typ:** pusty GameObject UI.
- **RectTransform:** Anchor Min `(0, 0)`, Anchor Max `(1, 1)`, Pivot `(0.5, 0.5)`, Left `0`, Right `0`, Top `0`, Bottom `0`. To nie jest wartość domyślna; ustaw ją ręcznie, aby `PartColumns` wypełniał `PartsSection/Content`.
- **Dodaj `HorizontalLayoutGroup`:** Padding `0`; Spacing `8`; Child Alignment `Upper Center`; Control Child Size Width `true`, Height `true`; Child Force Expand Width `true`, Height `true`.

### Każdy z sześciu `*Stage`

Dotyczy: `WeaponHeadStage`, `WeaponBinderStage`, `WeaponHandleStage`, `ArmorPlateStage`, `ArmorBinderStage`, `ArmorBaseStage`.

- **Rodzic:** `PartColumns`.
- **Typ:** pusty GameObject UI.
- **RectTransform:** pozostaw Anchor Min `(0.5, 0.5)`, Anchor Max `(0.5, 0.5)`, Pivot `(0.5, 0.5)`; pozycję i rozmiar ustawia `PartColumns/HorizontalLayoutGroup` z `LayoutElement`, więc nie wpisuj ręcznie Width, Height ani Pos X/Y.
- **Dodaj `LayoutElement`:** Flexible Width `1`, Flexible Height `1`.
- **Dodaj `VerticalLayoutGroup`:** Padding `0`; Spacing `4`; Child Alignment `Upper Center`; Control Child Size Width `true`, Height `true`; Child Force Expand Width `true`, Height `false`.
- `StageLabel`: `TextMeshProUGUI` + nowy `UIWorkspaceStyleApplier`: `Text Style = Caption`, `Text` = komponent TMP na `StageLabel`. `LayoutElement` Preferred Height `16`; Alignment `Center`; teksty kolejno: `Głowica`, `Łącznik`, `Rękojeść`, `Płyta`, `Łącznik`, `Baza`.
- `PartScroll`: prefab variant `PF_UIW_ScrollView/Grid`; root `LayoutElement` Flexible Height `1`. To jest obowiązkowy osobny `ScrollRect` dla każdego typu części; nie ograniczaj liczby slotów do wysokości sekcji ani nie zastępuj go zawijającą się listą.
- `PartScroll/Viewport/Content`: `GridLayoutGroup` Cell Size `(56, 56)`, Spacing `(6, 6)`, Padding `0`, Constraint `Fixed Column Count`, Count `2`; `ContentSizeFitter` Horizontal `Unconstrained`, Vertical `Preferred Size`; bez statycznych dzieci.

### `.../AssemblyColumn/SettingsSection`

- **Rodzic:** `AssemblyColumn`.
- **Typ bazowy:** `PF_UIW_Section/Titled`.
- **Zmiany:** `SectionTitle` = `3. Nazwa i jakość`; root `LayoutElement`: Preferred Height `112`, Flexible Height `0`.
- `Content`: dodaj `HorizontalLayoutGroup`, Padding `0`, Spacing `8`, Control Child Size Width/Height `true`, Child Force Expand Width `false`, Height `false`.
- `CustomNameInput`: `TMP_InputField`; `LayoutElement` Preferred Height `36`, Flexible Width `1`; placeholder `Nazwa przedmiotu (opcjonalnie)`.
- `QualityDropdown`: `TMP_Dropdown`; `LayoutElement` Preferred Width `176`, Preferred Height `36`, Flexible Width `0`; opcje dokładnie: `Kiepska (-50%)`, `Normalna`, `Doskonała (+100%)`; domyślny indeks `1`.

## Sekcje po prawej

### `.../SummaryColumn/PreviewSection`

- **Rodzic:** `SummaryColumn`.
- **Typ bazowy:** `PF_UIW_Section/Titled`.
- **Zmiany:** `SectionTitle` = `Wynik`; root `LayoutElement`: Preferred Height `112`, Flexible Height `0`.
- `Content`: `HorizontalLayoutGroup`, Padding `0`, Spacing `12`, Control Child Size Width/Height `true`, Child Force Expand Width/Height `false`.
- `ResultIconSlot`: zwykła instancja `PF_UIW_ItemSlot`, pozostaje aktywna i ma `LayoutElement` Preferred Width/Height `80`; dodaj `BlacksmithCraftingSlotView` na root. Podepnij `Button` na root, `Icon` = `Icon/Image`, `Border` = `Border/Image`; `Count Text`, `Buy Button` i `Buy Price Text` pozostaw puste. Nie dodawaj UnityEventów. Kod ustawia ikonę oraz tooltip po zmianie preview.
- `PreviewSummaryText`: `TextMeshProUGUI` z `LayoutElement` Flexible Width `1`, `Text Style = Body`, Alignment `Middle Left`, Horizontal Overflow `Wrap`, Vertical Overflow `Overflow`, tekst startowy pusty. Kod wpisuje jedną linię: rodzaj itemu, wymagana klasa i poziom.
- `PreviewNameText`: `TextMeshProUGUI` z `Text Style = Heading`, Alignment `Middle Left`, tekst startowy pusty. Kod wpisuje nazwę wyniku (`DisplayName`).
- `PreviewText`: pozostaw jako istniejący fallback i podepnij do pola `Preview Text`; przy podpiętym `ResultIconSlot`/`PreviewSummaryText` kod ukrywa go automatycznie. Nie wpisuj ręcznie statystyk ani UnityEventów.

### `.../SummaryColumn/MaterialsSection`

- **Rodzic:** `SummaryColumn`.
- **Typ bazowy:** `PF_UIW_Section/Titled`.
- **Zmiany:** `SectionTitle` = `Materiały party`; root `LayoutElement`: Min Height `176`, Flexible Height `1`.

### `.../MaterialsSection/Content/MaterialsScroll`

- **Rodzic:** `MaterialsSection/Content`.
- **Typ bazowy:** `PF_UIW_ScrollView/Grid`.
- **RectTransform:** Anchor Min `(0, 0)`, Anchor Max `(1, 1)`, Pivot `(0.5, 0.5)`, Left `0`, Right `0`, Top `0`, Bottom `0`.
- **Root `LayoutElement`:** Flexible Width `1`, Flexible Height `1`.
- **Zmiany `Viewport/Content`:** `GridLayoutGroup`: Cell Size `(72, 96)`, Spacing `(8, 8)`, Padding `0`, Start Corner `Upper Left`, Start Axis `Horizontal`, Constraint `Fixed Column Count`, Count `4`; `ContentSizeFitter`: Horizontal `Unconstrained`, Vertical `Preferred Size`.
- `Viewport/Content` zaczyna puste. Skrypt umieszcza tu instancje `MaterialSlotTemplate`.

### `.../SummaryColumn/StatusText`

- **Rodzic:** `SummaryColumn`.
- **Typ:** `TextMeshProUGUI` + nowy `UIWorkspaceStyleApplier`: `Text Style = Detail`, `Text` = komponent TMP na `StatusText`.
- **RectTransform:** pozostaw Anchor Min `(0.5, 0.5)`, Anchor Max `(0.5, 0.5)`, Pivot `(0.5, 0.5)`; pozycję i rozmiar ustawia `SummaryColumn/VerticalLayoutGroup` z `LayoutElement`, więc nie wpisuj ręcznie Width, Height ani Pos X/Y.
- **`LayoutElement`:** Min Height `20`, Preferred Height `20`, Flexible Height `0`.
- Alignment `Middle Left`; Horizontal Overflow `Wrap`; tekst startowy pusty.

### `.../SummaryColumn/CraftButton`

- **Rodzic:** `SummaryColumn`.
- **Typ bazowy:** `PF_UIW_Button/Primary`.
- **Zmiany:** `Label` = `Wytwórz`; root `LayoutElement`: Preferred Height `40`, Flexible Width `1`.
- Nie wpisuj `OnClick`; callback dodaje `BlacksmithCraftingWindowView`.

## Trzy template slotów

W `Templates` utwórz trzy **zwykłe instancje** istniejącego `PF_UIW_ItemSlot`. Nie twórz variantów `Equipment` i nie dodawaj żadnych nowych Image do `Border` ani `Overlay`. Każda instancja jest `SetActive(false)` i nie jest dzieckiem żadnego dynamicznego contentu.

### `.../Templates/EquipmentSlotTemplate`

- **Rodzic:** `Templates`; **źródło:** zwykła instancja `PF_UIW_ItemSlot`; zmień wyłącznie nazwę na `EquipmentSlotTemplate`.
- Zachowaj hierarchy `Border`, `Icon`, `Overlay`, `CountLabel` bez zmian. `CountLabel` pozostaje inactive.
- Na root dodaj `BlacksmithCraftingSlotView`: `Button` = Button na root; `Icon` = `Icon/Image`; `Count Text` = `CountLabel/TMP`; `Border` = `Border/Image`; `Buy Button`, `Buy Price Text` = `None`.
- Skrypt zmienia istniejący `Border` na `Selected` dla wybranej bazy; w pozostałych przypadkach przywraca `Neutral`.

### `.../Templates/PartSlotTemplate`

- **Rodzic:** `Templates`; **źródło:** zwykła instancja `PF_UIW_ItemSlot`; zmień wyłącznie nazwę na `PartSlotTemplate`.
- Konfiguracja i referencje `BlacksmithCraftingSlotView` są identyczne jak dla `EquipmentSlotTemplate`.
- Skrypt zmienia istniejący `Border` na `Selected` dla wybranej części.

### `.../Templates/MaterialSlotTemplate`

- **Rodzic:** `Templates`; **źródło:** zwykła instancja `PF_UIW_ItemSlot`; zmień wyłącznie nazwę na `MaterialSlotTemplate`.
- **Root RectTransform:** Width `72`, Height `96`. Jego parent używa Cell Size `(72, 96)`.
- Zachowaj istniejący `Border`, `Icon`, `Overlay`, `CountLabel`. W `Overlay` dodaj tylko `BuyButton` jako instancję `PF_UIW_Button`; Anchor Min `(0, 0)`, Anchor Max `(1, 0)`, Pivot `(0.5, 0)`, Left `0`, Right `0`, Pos Y `0`, Height `22`; `Label` Alignment `Center`; `BuyButton` = `SetActive(false)`.
- Na root dodaj `BlacksmithCraftingSlotView`: `Button` = Button na root; `Icon` = `Icon/Image`; `Count Text` = `CountLabel/TMP`; `Border` = `Border/Image`; `Buy Button` = `Overlay/BuyButton`; `Buy Price Text` = `Overlay/BuyButton/Label`.
- Skrypt zmienia istniejący `Border` na `Warning`, jeżeli materiału brakuje, w przeciwnym razie na `Neutral`; włącza `BuyButton` tylko przy brakującym materiale oferowanym przez kowala.
- Nie ustawiaj `OnClick` na root ani `BuyButton`.

## Referencje `BlacksmithCraftingWindowView`

Komponent znajduje się na `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow`.

| Pole | Dokładna referencja |
| --- | --- |
| `Window` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow` (`UIWorkspaceWindow`) |
| `Title Text` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Header/Title` |
| `Custom Name Input` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/AssemblyColumn/SettingsSection/Content/CustomNameInput` |
| `Quality Dropdown` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/AssemblyColumn/SettingsSection/Content/QualityDropdown` |
| `Equipment Content` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/AssemblyColumn/BaseSection/Content/BaseGrid` |
| `Materials Content` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/SummaryColumn/MaterialsSection/Content/MaterialsScroll/Viewport/Content` |
| `Equipment Slot Template` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Templates/EquipmentSlotTemplate` |
| `Part Slot Template` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Templates/PartSlotTemplate` |
| `Material Slot Template` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Templates/MaterialSlotTemplate` |
| `Preview Text` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/SummaryColumn/PreviewSection/Content/PreviewText` |
| `Preview Tooltip Slot` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/SummaryColumn/PreviewSection/Content/ResultIconSlot` |
| `Preview Name Text` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/SummaryColumn/PreviewSection/Content/PreviewNameText` |
| `Preview Summary Text` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/SummaryColumn/PreviewSection/Content/PreviewSummaryText` |
| `Status Text` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/SummaryColumn/StatusText` |
| `Craft Button` | `Canvas/UIWorkspaceLayer/PF_UIW_BlacksmithCraftingWindow/Content/CraftingBody/SummaryColumn/CraftButton` |

`Part Selection Views` ma dokładnie sześć elementów w kolejności: `WeaponHead`, `WeaponBinder`, `WeaponHandle`, `ArmorPlate`, `ArmorBinder`, `ArmorBase`. Dla każdego `Root` wskazuje odpowiadający `*Stage`; `Content` wskazuje jego `PartScroll/Viewport/Content`.

## NPC i dane

1. `NpcDialogueController/Crafting Button` wskazuje istniejący przycisk `Craft` dialogu NPC; `Crafting Window Prefab` wskazuje asset `Assets/_Project/Prefabs/UI/Workspace/PF_UIW_BlacksmithCraftingWindow.prefab`. Kontroler sam tworzy pierwszą instancję pod `UIWorkspaceWindowManager/WorkspaceRoot`; nie umieszczaj jej ręcznie w scenie i nie podpinaj UnityEventów.
2. Kowal ma `BlacksmithRoleData`; w jego `ClassOffers` skonfiguruj cztery pary klas opisane wyżej. Aby kupować braki, ma także `TraderRoleData` oraz te materiały w aktualnej ofercie shopu.
3. Każda craftowalna część ma ikonę i listę `CraftingMaterials`. Pusta lista blokuje craft tej części.

## Smoke check

1. Wybór bazy odświeża trzy widoczne stage'e i wyróżnia tylko kliknięty slot.
2. Hover nad bazą, częścią, materiałem oraz `ResultIconSlot` pokazuje istniejący tooltip Workspace; tooltip wyniku zawiera nazwę, jakość, losowaną rzadkość i polsko sformatowane statystyki.
3. Zmiana jakości odświeża badge `posiadane/wymagane` i stan craft buttonu.
4. Brak materiału zmienia jego istniejący `Border` na styl `Warning` i włącza `BuyButton`; częściowy stock pokazuje zakup ograniczony do dostępnej liczby sztuk. Brak złota zostawia przycisk widoczny, ale nieaktywny, z czerwonym borderem; brak stocku także ma czerwony stan bez aktywnego zakupu. Zakup odświeża grid bez zamykania okna.
5. `Wytwórz` jest interaktywny wyłącznie przy komplecie materiałów z całego aktywnego party.
