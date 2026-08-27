# Unity handoff

Karta: Combat v3 - tooltipy, inspect i feedback bojowy v1

Ta instrukcja jest wyłącznie deltą względem już skonfigurowanego PF_UIW_CombatUnitInspectWindow. Nie zmieniaj sliderów, AP, Scroll View, Viewportu ani list statusów i umiejętności.

## Hierarchy

~~~text
PF_UIW_CombatUnitInspectWindow/Content/Scroll View/Viewport/Content
└── Stats (zastępuje dotychczasowy tekst Stats)
    ├── Stats Left
    └── Stats Right
~~~

## Obiekty

### Stats

Usuń dotychczasowy obiekt Text - TextMeshPro o nazwie Stats. W tym samym miejscu utwórz obiekt o nazwie Stats.

- Parent: PF_UIW_CombatUnitInspectWindow/Content/Scroll View/Viewport/Content
- Typ: Empty

Stats jest bezpośrednim dzieckiem istniejącego Vertical Layout Group na Content. Nie ustawiaj jego RectTransformu.

#### Dodatkowe komponenty

- Horizontal Layout Group:
  - Padding Left: 0.
  - Padding Right: 0.
  - Padding Top: 0.
  - Padding Bottom: 0.
  - Spacing: 12.
  - Child Alignment: Upper Left.
  - Control Child Size > Width: włączone.
  - Control Child Size > Height: włączone.
  - Use Child Scale > Width: wyłączone.
  - Use Child Scale > Height: wyłączone.
  - Child Force Expand > Width: włączone.
  - Child Force Expand > Height: wyłączone.
- Content Size Fitter:
  - Horizontal Fit: Unconstrained.
  - Vertical Fit: Preferred Size.

### Stats Left

- Parent: PF_UIW_CombatUnitInspectWindow/Content/Scroll View/Viewport/Content/Stats
- Typ: Text - TextMeshPro

Stats Left jest dzieckiem Horizontal Layout Group na Stats. Nie ustawiaj jego RectTransformu.

#### Ustaw

- TextMeshProUGUI > Alignment: Left.
- TextMeshProUGUI > Wrapping: włączone.

#### Dodatkowe komponenty

- Layout Element > Flexible Width: 1.
- Content Size Fitter:
  - Horizontal Fit: Unconstrained.
  - Vertical Fit: Preferred Size.
- UIWorkspaceStyleApplier:
  - Palette: Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset.
  - Surface Style: None.
  - Border Style: None.
  - Text Style: Detail.
  - Interaction Style: None.
  - Text: komponent TextMeshProUGUI na Stats Left.

### Stats Right

- Parent: PF_UIW_CombatUnitInspectWindow/Content/Scroll View/Viewport/Content/Stats
- Typ: Text - TextMeshPro

Stats Right jest dzieckiem Horizontal Layout Group na Stats. Nie ustawiaj jego RectTransformu.

#### Ustaw

- TextMeshProUGUI > Alignment: Left.
- TextMeshProUGUI > Wrapping: włączone.

#### Dodatkowe komponenty

- Layout Element > Flexible Width: 1.
- Content Size Fitter:
  - Horizontal Fit: Unconstrained.
  - Vertical Fit: Preferred Size.
- UIWorkspaceStyleApplier:
  - Palette: Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset.
  - Surface Style: None.
  - Border Style: None.
  - Text Style: Detail.
  - Interaction Style: None.
  - Text: komponent TextMeshProUGUI na Stats Right.

## Referencje

Na komponencie CombatUnitInspectWindowView obiektu PF_UIW_CombatUnitInspectWindow przypnij:

- Stats Left Text → PF_UIW_CombatUnitInspectWindow/Content/Scroll View/Viewport/Content/Stats/Stats Left.
- Stats Right Text → PF_UIW_CombatUnitInspectWindow/Content/Scroll View/Viewport/Content/Stats/Stats Right.

## Smoke test

1. Otwórz inspect dowolnej jednostki.
2. Statystyki są w dwóch kolumnach i zawierają odporności.
3. Zmniejsz wysokość okna. Zawartość jest przycięta do Viewportu i przewijana pionowo.
4. Jednostka bez efektów lub umiejętności pokazuje właściwy fallback tekst.

Oczekiwany rezultat:

- Handoff wymaga tylko przebudowy sekcji Stats i dwóch referencji.
