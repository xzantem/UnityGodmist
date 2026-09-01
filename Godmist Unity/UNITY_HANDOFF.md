# Unity handoff — Campaign Storage v1

## Prefab okna

Utwórz `PF_UIW_CampaignStorageWindow` jako wariant `PF_UIW_Window`. Root ma `UIWorkspaceWindow` i `CampaignStorageWindowView`.

W `Content` utwórz tylko jeden `PF_UIW_Section/Titled` o nazwie `StashPanel`; `SectionTitle` = `Magazyn Arungardu`. W jego `Content` dodaj `Scroll View`; na `Viewport/Content` ustaw `GridLayoutGroup`: Cell Size `64 x 64`, Spacing `8 x 8`, Constraint `Fixed Column Count`, Count `5`, Child Alignment `Upper Left`.

W gridzie utwórz inactive `ItemTemplate` jako kopię `PF_UIW_ItemSlot`. Dodaj na nim `CampaignStorageItemView`: `Icon` = child `Icon`, `Count Text` = child `CountLabel`. `CampaignStorageDropTarget` dodaj na `Scroll View/Viewport`, nie na `Viewport/Content`: `Viewport` ma istniejący komponent `Image`, który odbiera raycast. Ustaw `Accepts Stash Items` on.

Na **rootcie `PF_UIW_CampaignStorageWindow`**, na tym samym GameObjectcie co komponent `UIWorkspaceWindow`, dodaj `CampaignStorageWindowView`. W jego Inspectorze przypisz dokładnie:

- `Window`: przeciągnij komponent `UIWorkspaceWindow` z tego samego root GameObjectu.
- `Stash Items Root`: przeciągnij `Viewport/Content` z `StashPanel/Content/Scroll View`. To jest Transform mający `GridLayoutGroup`; nie przypisuj `StashPanel`, `Scroll View` ani `Viewport`.
- `Item Template`: przeciągnij `ItemTemplate/CampaignStorageItemView` z tego samego `Viewport/Content`. Template pozostaje inactive w prefabie.
- `Status Text`: w `StashPanel/Content` najpierw dodaj `VerticalLayoutGroup`: Padding `Left 0, Right 0, Top 0, Bottom 0`; Spacing `6`; Child Alignment `Upper Left`; `Control Child Size Width` on, `Height` on; `Child Force Expand Width` on, `Height` off. `Scroll View` i `StatusText` są jedynymi bezpośrednimi dziećmi tego `Content`.
  - Na `Scroll View` dodaj `LayoutElement`: `Flexible Height` `1`; pozostałe pola LayoutElement zostaw na `0`.
  - Utwórz `StatusText` przez `UI > Text - TextMeshPro` jako drugie dziecko `StashPanel/Content`; ustaw pusty tekst, `Alignment` = `Middle Left`, dodaj `UIWorkspaceStyleApplier` ze stylem `Caption` oraz `LayoutElement` z `Preferred Height` `22`, `Flexible Height` `0`.
  - Nie ustawiaj ręcznie anchory, pozycję ani Width/Height `StatusText`: `VerticalLayoutGroup` kontroluje jego RectTransform. Następnie przeciągnij komponent `TextMeshProUGUI` z `StatusText` do pola `Status Text`.
- `Drag Ghost Template`: zostaw puste. Jest opcjonalne i nie jest potrzebne do transferu ani dropów.

## Skrzynia Arungardu

W scenie dodaj `CampaignStorageChest` z widocznym sprite'em/colliderem używanym przez interakcje. Dodaj `CampaignStorageChestView`; przypisz istniejący `ExplorationPartyController` oraz prefab `PF_UIW_CampaignStorageWindow`. Pozostaw `Interaction Radius` `1.25`, klawisz `E`.

## Weryfikacja

E przy skrzyni otwiera tylko stash. Otwórz okno postaci z party rail: drag między jego inventory a stashem przenosi cały stos, Ctrl+drag jedną sztukę. PPM w inventory postaci odkłada item do stashu; PPM w stashu pobiera go do aktywnego lidera. Sprawdź save/load i zachowanie `instanceId`.
