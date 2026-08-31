# Unity handoff

Karta: `[P2] Crafting półproduktów materiałów v1`

Efekt końcowy: jedno okno `Usługi Kowala` z pionowymi ikonami po lewej. Pokazuje equipment crafting i półprodukty. Obecny sklep pozostaje osobnym `TradePanelView` w tej karcie.

## 1. Utwórz dane CID

### Surowce bazowe

Utwórz zwykłe `ItemDefinition` dla rudy żelaza, skóry kozy, drewna modrzewiowego i włókna bawełny. Dla każdego ustaw:

- `Item Type`: `Material`
- `Material Discipline`: `Smithing`
- `Is Stackable`: włączone
- `Base Value`: ręcznie ustalana źródłowa cena ekonomiczna.

### Craftowane itemy

Utwórz `CraftableItemDefinition` z prefiksem `CID_`. CID sam jest wynikiem oraz receptą — nie wskazuje oddzielnego `ItemDefinition` jako result.

Na każdym CID półproduktu ustaw:

- `Item Type`: `Material`
- `Is Stackable`: włączone
- `Crafting Discipline`: `Smithing`
- `Output Amount` i `Ingredients` według recepty.

Nie ustawiaj `Base Value`: CID pokazuje wyłącznie `Calculated Base Value`. Jest to suma cen składników podzielona przez `Output Amount`; półprodukty w składnikach wyceniają się rekurencyjnie.

- `CID_IronIngot`: wynik ×1; ruda żelaza ×2.
- `CID_LargeIronIngot`: wynik ×1; ruda żelaza ×6.
- `CID_IronChain`: wynik ×1; `CID_IronIngot` ×1.
- `CID_IronPlate`: wynik ×1; `CID_IronIngot` ×2.
- `CID_TannedGoatLeather`: wynik ×1; skóra kozy ×1.
- `CID_GoatLeatherStrips`: wynik ×2; `CID_TannedGoatLeather` ×1.
- `CID_LarchPlank`: wynik ×2; drewno modrzewiowe ×1.
- `CID_LarchStick`: wynik ×8; `CID_LarchPlank` ×3.
- `CID_WeakNeedle`: wynik ×3; `CID_IronIngot` ×1.
- `CID_CottonThread`: wynik ×2; włókno bawełny ×1.
- `CID_CottonCloth`: wynik ×3; `CID_CottonThread` ×8 oraz `CID_WeakNeedle` ×1.

W `BRD_Arungard_Blacksmith > Material Recipes` dodaj te CID w podanej kolejności. W `TRD_Arungard_Trader > Initial Offer` dodaj tylko składniki, które kowal ma sprzedawać.

## 2. Zbuduj root okna usług

1. Zduplikuj `PF_UIW_BlacksmithCraftingWindow` jako `PF_UIW_NpcServicesWindow`.
2. Otwórz nowy prefab w Prefab Mode.
3. Zachowaj root, `UIWorkspaceWindow`, Header i przycisk X bez zmian.
4. Na root dodaj `NpcServicesWindowView`.
5. Znajdź najwyższy GameObject zawierający cały obecny crafting equipmentu: `1. Wybierz bazę`, `2. Złóż części`, `3. Nazwa i jakość`, `Wynik`, `Materiały`, `Wytwórz`. Nazwij go `EquipmentServiceRoot`.
6. Na istniejącym `BlacksmithCraftingWindowView` ustaw `Service Root` → `EquipmentServiceRoot`. `Window` nadal wskazuje rootowy `UIWorkspaceWindow`.

## 3. Dodaj zakładki

### `ServiceTabs`

- Parent: `PF_UIW_NpcServicesWindow`
- Typ: Empty

#### RectTransform

- Anchor Min: `(0, 0)`
- Anchor Max: `(0, 1)`
- Pivot: `(1, 0.5)`
- Pos X: `-8`
- Width: `56`
- Top: `58`
- Bottom: `12`

#### Komponenty

- `Vertical Layout Group`: `Child Alignment = Upper Center`, `Spacing = 8`, `Control Child Size Width = true`, `Control Child Size Height = true`, `Child Force Expand Width = true`, `Child Force Expand Height = false`.

`ServiceTabs` celowo wystaje poza lewą krawędź rootowego okna. Nie umieszczaj go pod `Content` i nie dodawaj maski; ma zachowywać się jak zakładki huba z drzewka.

### `TabTemplate`

Pod `ServiceTabs` dodaj jeden prefab variant `PF_UIW_ItemSlot`, nazwij go `TabTemplate` i wyłącz GameObject w prefabie. Kod klonuje go dla każdej aktualnie dostępnej usługi.

Na `TabTemplate` dodaj `NpcServiceTabView` i ustaw:

- `Button` → rootowy `Button` template’u.
- `Icon` → `TabTemplate/Icon`.
- `Selected Border` → `TabTemplate/Border`.
- `Layout Element > Preferred Width`: `48`.
- `Layout Element > Preferred Height`: `48`.

Przesuń `EquipmentServiceRoot` w prawo: jego lewy margines ma wynosić `80`. Dzieci sterowanych Layout Group nie przesuwaj ręcznie.

## 4. Zbuduj panel półproduktów

Nie buduj prawej strony drugi raz. Będzie identyczną kopią obecnego equipment craftingu.

1. Pod `Content` zduplikuj cały `EquipmentServiceRoot` jako sibling i nazwij kopię `MaterialCraftingServiceRoot`.
2. Nie zmieniaj RectTransform kopii. Ma dokładnie zajmować ten sam obszar `Content`, co equipment crafting.
3. Na `MaterialCraftingServiceRoot` usuń komponent `BlacksmithCraftingWindowView`; dodaj `MaterialCraftingWindowView`.
4. W kopii zachowaj całą prawą kolumnę bez zmian: sekcję `Wynik`, sekcję `Materiały`, tekst statusu oraz przycisk `Wytwórz`. To jest gotowa `ActionColumn` dla CID.
5. W lewej kolumnie usuń trzy sekcje specyficzne dla equipmentu: `1. Wybierz bazę`, `2. Złóż części` i `3. Nazwa i jakość`.
6. Zduplikuj dawną sekcję `1. Wybierz bazę` **przed jej usunięciem** i użyj kopii jako jedynej lewej sekcji. Nazwij ją `RecipeSection`; tytuł ustaw na `Wybierz receptę`.
7. `BaseSection` nie ma Scroll View. W `RecipeSection/Content` usuń odziedziczony `BaseGrid` oraz wszystkie jego sloty.
8. Do `RecipeSection/Content` dodaj wariant `PF_UIW_ScrollView` nazwany `RecipeScroll`. Jego RectTransform wypełnia `RecipeSection/Content`; `RecipeScroll/Viewport/Content` nazwij `RecipeContent`.
9. Na `RecipeContent` dodaj `Grid Layout Group` z dokładnie tymi ustawieniami:
   - `Padding`: Left `0`, Right `0`, Top `0`, Bottom `0`.
   - `Child Alignment`: `Upper Left`.
   - `Start Corner`: `Upper Left`.
   - `Start Axis`: `Horizontal`.
   - `Cell Size`: X `100`, Y `100`.
   - `Spacing`: X `8`, Y `8`.
   - `Constraint`: `Fixed Column Count`.
   - `Constraint Count`: `4`.
10. Na `RecipeContent` dodaj `Content Size Fitter`: `Horizontal Fit = Unconstrained`, `Vertical Fit = Preferred Size`.
11. Do `RecipeContent` dodaj jeden `BlacksmithCraftingSlotView`, nazwij go `RecipeSlotTemplate` i wyłącz GameObject. `RecipeScroll` zostaje, bo lista CID będzie rosnąć.
9. W prawej sekcji `Materiały` zachowaj obecny `MaterialsContent` i jego `Grid Layout Group`; nie umieszczaj materiałów w Scroll View. Zostaw jeden ukryty `BlacksmithCraftingSlotView` jako `MaterialSlotTemplate`, wraz z przyciskiem zakupu pod slotem.
10. Wyłącz cały `MaterialCraftingServiceRoot` w prefabie.

Na `MaterialCraftingWindowView` ustaw:

- `Service Root` → `MaterialCraftingServiceRoot`.
- `Recipe Grid` → `RecipeContent` (dokładnie obiekt z `Grid Layout Group`, wewnątrz `RecipeScroll/Viewport/Content`).
- `Materials Grid` → `MaterialsContent` (dokładnie obiekt z `Grid Layout Group` w sekcji `Materiały`, nie root sekcji ani Scroll View).
- `Recipe Slot Template` → `RecipeSlotTemplate`.
- `Material Slot Template` → `MaterialSlotTemplate`.
- `Preview Tooltip Slot` → istniejący slot ikony w sekcji `Wynik`.
- `Preview Name Text` → istniejący tekst nazwy wyniku.
- `Preview Summary Text` → istniejący tekst pod nazwą wyniku.
- `Status Text` → istniejący tekst statusu.
- `Craft Button` → istniejący przycisk `Wytwórz`.

## 5. Podepnij rootowy komponent

Na root `PF_UIW_NpcServicesWindow > NpcServicesWindowView` ustaw:

- `Window` → rootowy `UIWorkspaceWindow`.
- `Title Text` → tytuł Headera.
- `Tabs Content` → `ServiceTabs`.
- `Tab Template` → `ServiceTabs/TabTemplate`.
- `Tab Definitions`, rozmiar `2`:
  - element `0`: `Kind = Equipment Crafting`, ikona equipmentu;
  - element `1`: `Kind = Material Crafting`, ikona materiałów.
- `Equipment Crafting Service` → istniejący `BlacksmithCraftingWindowView`.
- `Material Crafting Service` → `MaterialCraftingServiceRoot/MaterialCraftingWindowView`.

## 6. Podepnij dialog dopiero na końcu

W `Arungard`, na `NpcDialogueController`:

1. Zachowaj istniejący przycisk handlu i jego referencje `Trade Button` oraz `Trade Panel`.
2. Przygotuj osobny przycisk `Usługi kowala`.
3. `Services Button` → przycisk `Usługi kowala`.
4. `Services Window Prefab` → `PF_UIW_NpcServicesWindow`.

## Smoke test

1. Poczekaj na kompilację Unity i wygenerowanie `.meta` nowych skryptów.
2. Zwykły handlarz: istniejący przycisk handlu nadal otwiera obecny `TradePanel`.
3. Kowal: `Usługi kowala` otwiera jedno okno z dwiema ikonami po lewej.
4. Przełącz equipment → półprodukty bez zamykania okna.
5. Craftuj CID ze składników rozdzielonych między aktywnych członków party.
6. Sprawdź zakup brakującego składnika od kowala.
7. Zamknij okno X: gra wraca do eksploracji.
