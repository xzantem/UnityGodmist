# Unity handoff — delta po ostatniej konfiguracji

Wykonaj tylko poniższe zmiany. Nie twórz ponownie `UpgradeServiceRoot`, `ReforgeServiceRoot`, `EquipmentSection`, `SettingsSection`, gridów, sekcji ani tabów.

## 1. Dwa interaktywne slidery — uchwyt i obsługa wartości 0

Nie zmieniaj wspólnego prefabu `PF_UIW_Slider`. Poniższe kroki wykonaj osobno na jego dwóch instancjach: `SettingsSection/Content/TargetProgressSetting/TargetProgressSlider` i `SettingsSection/Content/SuccessChanceSetting/SuccessChanceSlider`.

Na każdej z tych dwóch instancji:

1. Na root slidera, w komponencie `Slider`:
   - `Interactable`: on.
   - `Target Graphic`: istniejący child `Background` (`Image`).
2. Na `Background/Image`: `Raycast Target`: on.
3. Jako trzecie dziecko root slidera utwórz `UI > Empty Object` o nazwie `Handle Slide Area`.
   - Ma tylko `RectTransform`.
   - Anchors Min `(0,0)`, Max `(1,1)`; Pivot `(0.5,0.5)`.
   - W widocznym w Inspectorze trybie stretch ustaw: `Left 8`, `Right 8`, `Top 0`, `Bottom 0`.
4. Jako dziecko `Handle Slide Area` utwórz `UI > Image` o nazwie `Handle`.
   - `RectTransform`: Anchors Min `(0,0.5)`, Max `(0,0.5)`; Pivot `(0.5,0.5)`.
   - W widocznym w Inspectorze trybie bez stretch ustaw: `Pos X 0`, `Pos Y 0`, `Width 16`, `Height 16`.
   - `Image > Raycast Target`: off.
   - Dodaj `UIWorkspaceStyleApplier`: `Palette` = ta sama paleta, której używa `UIWorkspaceSliderStyleApplier`; `Surface Image` = Image z `Handle`; `Surface Style` = `Raised`.
5. Na **root slidera** (tym samym GameObjectcie, który ma komponent `Slider`) przypisz pole `Handle Rect` do **RectTransform GameObjectu `Handle`**. Nie przypisuj `Handle Slide Area`, `Image` z `Background` ani komponentu `Image` z `Handle`.
6. Na tym samym root `Slider` sprawdź `Direction`: `Left To Right`.
7. Szybka weryfikacja przed wyjściem z prefabu: zmień w Inspectorze `Slider > Value` kolejno na `0` i `1`. `Handle` musi przejść od lewej do prawej krawędzi `Handle Slide Area`. Jeżeli stoi w miejscu, usuń przypisanie `Handle Rect` i przypisz ponownie dokładnie `Handle/RectTransform`.

Nie zmieniaj `Fill Rect`, `Direction`, Min/Max ani `Whole Numbers` żadnego slidera.

## 2. Koszt upgrade/reforge

Na `CostText` w obu rootach (`UpgradeServiceRoot` i `ReforgeServiceRoot`), w istniejącym `UIWorkspaceStyleApplier`:

- `Text Style`: `Heading`.
- `Text Accent Style`: `Warning`.

Na `CostText/TextMeshProUGUI` ustaw `Alignment` na `Middle Center`. Pozostałe ustawienia i RectTransform bez zmian.

## 3. Nowa referencja previewu

Po przeładowaniu skryptów `EquipmentUpgradeWindowView` ma nowe pole `Preview Name Text`.

- Na `UpgradeServiceRoot/EquipmentUpgradeWindowView` przypisz istniejący `PreviewNameText` z `SummaryColumn/PreviewSection`.
- Na `ReforgeServiceRoot/EquipmentUpgradeWindowView` przypisz jego własny `PreviewNameText`.

To wszystko. Pozostałe poprawki — pięć socketów przy 100%, szanse w preview, docelowy rarity reforge i tooltip previewu — są już w kodzie i nie wymagają zmian w Unity.
