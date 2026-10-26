using System;
using System.Linq;
using Godmist.Core.Enums;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Editor
{
    /// <summary>Scoped native authoring of one alchemy tab; preserves the service shell and other tabs.</summary>
    public static class AlchemyCraftingUiSetup
    {
        public const string PrefabPath = TradeUiSetup.PrefabPath;
        private static UIWorkspaceStylePalette palette;
        private static Scrollbar scrollbarSource;

        [MenuItem("Godmist/Content/UI/Configure Potion Crafting")]
        public static void Configure()
        {
            Configure(ConsumableUseType.Potion);
        }

        [MenuItem("Godmist/Content/UI/Configure Bomb Crafting")]
        public static void ConfigureBombs()
        {
            Configure(ConsumableUseType.Bomb);
        }

        private static void Configure(ConsumableUseType type)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                scrollbarSource = root.GetComponentInChildren<MaterialCraftingWindowView>(true)
                    .GetComponentsInChildren<ScrollRect>(true).First(scroll => scroll.verticalScrollbar != null).verticalScrollbar;
                var bomb = type == ConsumableUseType.Bomb;
                var view = Ref<AlchemyCraftingWindowView>(root.GetComponentInChildren<NpcServicesWindowView>(true),
                    bomb ? "bombCraftingService" : "potionCraftingService");
                var body = Ref<GameObject>(view, "serviceRoot").transform;
                Rect(body, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
                body.GetComponent<HorizontalLayoutGroup>().spacing = 28;
                var left = body.Find("AssemblyColumn"); var right = body.Find("SummaryColumn");
                Element(left, 432, -1, 0, 1); left.GetComponent<LayoutElement>().preferredWidth = 500;
                Element(right, 330, -1, 1, 1);
                left.GetComponent<VerticalLayoutGroup>().spacing = 12;
                right.GetComponent<VerticalLayoutGroup>().spacing = 8;

                var ingredients = left.Find("BaseSection"); Section(ingredients, "Składniki", 160, 1);
                var count = Node(ingredients, "IngredientCount", typeof(TextMeshProUGUI)); Text(count.GetComponent<TMP_Text>(), 18);
                count.GetComponent<TMP_Text>().color = new Color32(215, 184, 102, 255);
                count.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Right;
                Rect(count, new Vector2(1, 1), Vector2.one, new Vector2(-90, -28), Vector2.zero);
                Wire(view, "ingredientCountText", count.GetComponent<TMP_Text>());
                var grid = Ref<Transform>(view, "ingredientContent");
                var scroll = Scroll(ingredients.Find("Content"), "IngredientScroll", grid);
                Wire(view, "ingredientScroll", scroll);
                var layout = grid.GetComponent<GridLayoutGroup>(); layout.cellSize = new Vector2(72, 72);
                layout.spacing = new Vector2(10, 10); layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = 5; layout.childAlignment = TextAnchor.UpperLeft;
                layout.padding = new RectOffset();
                var ingredient = Ref<BlacksmithCraftingSlotView>(view, "ingredientSlotTemplate"); Slot(ingredient);
                var amount = Ref<TextMeshProUGUI>(ingredient, "countText"); Text(amount, 16);
                Rect(amount.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 3), new Vector2(-5, 22));

                var settings = left.Find("SettingsSection"); Section(settings, bomb ? "Proch i katalizator" : "Alkohol i katalizator", 80, 0);
                var medium = Ref<TMP_Dropdown>(view, "mediumDropdown"); var catalyst = Ref<TMP_Dropdown>(view, "catalystDropdown");
                Dropdown(medium); Dropdown(catalyst);
                Rect(medium.transform, Vector2.zero, new Vector2(.5f, 0), Vector2.zero, new Vector2(-6, 40));
                Rect(catalyst.transform, new Vector2(.5f, 0), new Vector2(1, 0), new Vector2(6, 0), new Vector2(0, 40));
                var names = left.Find("NameSection"); Section(names, "Nazwa", 80, 0);
                var input = Ref<TMP_InputField>(view, "customNameInput"); Disable(input.gameObject);
                Rect(input.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 40));
                Text(input.textComponent, 19); Text((TMP_Text)input.placeholder, 18);
                ((TMP_Text)input.placeholder).text = bomb ? "Nazwa bomby (opcjonalnie)…" : "Nazwa mikstury (opcjonalnie)…";

                var divider = Node(right, "ColumnDivider", typeof(Image), typeof(LayoutElement));
                divider.GetComponent<LayoutElement>().ignoreLayout = true;
                Rect(divider, Vector2.zero, new Vector2(0, 1), new Vector2(-14, 0), new Vector2(-13, 0));
                divider.GetComponent<Image>().color = new Color32(107, 109, 87, 255); divider.GetComponent<Image>().raycastTarget = false;
                var preview = right.Find("PreviewSection"); Section(preview, "Podgląd", bomb ? 166 : 146, 0);
                var previewLayout = preview.Find("Content").GetComponent<VerticalLayoutGroup>();
                if (previewLayout != null) { previewLayout.padding = new RectOffset(); previewLayout.spacing = 0; }
                var result = Ref<BlacksmithCraftingSlotView>(view, "previewTooltipSlot"); Slot(result);
                Element(result.transform, 80, 80, 0, 0);
                var header = result.transform.parent; Element(header, -1, bomb ? 130 : 110, 1, 0);
                var name = Ref<TextMeshProUGUI>(view, "previewNameText"); Text(name, 23);
                Element(name.transform, -1, bomb ? -1 : 78, 1, 0); name.overflowMode = TextOverflowModes.Ellipsis;
                if (bomb)
                {
                    header.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                    name.transform.parent.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                }
                var inheritedInput = name.GetComponent<TMP_InputField>(); if (inheritedInput != null) inheritedInput.enabled = false;
                var info = Ref<TextMeshProUGUI>(view, "previewSummaryText"); Text(info, 17); Element(info.transform, -1, bomb ? 44 : 24, 1, 0);

                var effects = Node(right, "EffectsSection", typeof(LayoutElement)); effects.SetSiblingIndex(preview.GetSiblingIndex() + 1);
                Element(effects, -1, 108, 1, 0);
                var title = Node(effects, "SectionTitle", typeof(TextMeshProUGUI)); Text(title.GetComponent<TMP_Text>(), 23); title.GetComponent<TMP_Text>().text = "Efekty";
                Rect(title, new Vector2(0, 1), Vector2.one, new Vector2(0, -28), Vector2.zero);
                var effectHost = Node(effects, "Content"); Rect(effectHost, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -32));
                var text = Ref<TextMeshProUGUI>(view, "previewEffectsText")
                    ?? Node(effectHost, "EffectSummary", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                Text(text, 18);
                Scroll(effectHost, "EffectsScroll", text.transform); Wire(view, "previewEffectsText", text);

                var materials = right.Find("MaterialsSection"); Section(materials, "Materiały", 160, 1);
                var materialGrid = Ref<Transform>(view, "materialRequirementsContent");
                Scroll(materials.Find("Content"), "MaterialsScroll", materialGrid);
                var costs = materialGrid.GetComponent<GridLayoutGroup>(); costs.cellSize = new Vector2(88, 120);
                costs.spacing = new Vector2(8, 12); costs.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                costs.constraintCount = 3; costs.childAlignment = TextAnchor.UpperLeft;
                costs.padding = new RectOffset();
                var material = Ref<BlacksmithCraftingSlotView>(view, "materialSlotTemplate"); Slot(material);
                var materialButton = Ref<Button>(material, "button");
                Rect(materialButton.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-36, -72), new Vector2(36, 0));
                var owned = Ref<TextMeshProUGUI>(material, "countText"); owned.transform.SetParent(material.transform, false); Text(owned, 18);
                owned.alignment = TextAlignmentOptions.Center;
                Rect(owned.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -94), new Vector2(0, -74));
                var buy = Ref<Button>(material, "buyButton"); Button(buy);
                Rect(buy.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 24));
                var coin = Node(buy.transform, "Icon", typeof(Image)).GetComponent<Image>(); Disable(coin.gameObject);
                coin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");
                coin.gameObject.SetActive(true); coin.color = Color.white; coin.preserveAspect = true; coin.raycastTarget = false;
                Rect(coin.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(8, -8), new Vector2(24, 8));
                var price = Ref<TextMeshProUGUI>(material, "buyPriceText"); Text(price, 18);
                Rect(price.transform, Vector2.zero, Vector2.one, new Vector2(28, 2), new Vector2(-4, -2));
                var status = Ref<TextMeshProUGUI>(view, "statusText"); Text(status, 17); Element(status.transform, -1, 24, 1, 0);
                var craft = Ref<Button>(view, "craftButton"); Button(craft); Element(craft.transform, -1, 44, 1, 0);
                craft.transform.Find("Border")?.gameObject.SetActive(false); Text(craft.GetComponentInChildren<TMP_Text>(true), 21);
                craft.GetComponentInChildren<TMP_Text>(true).text = bomb ? "Wytwórz" : "Uwarz";
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Godmist/Content/UI/Configure Potion and Bomb Refilling")]
        public static void ConfigureRefills()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                scrollbarSource = root.GetComponentInChildren<MaterialCraftingWindowView>(true)
                    .GetComponentsInChildren<ScrollRect>(true).First(scroll => scroll.verticalScrollbar != null).verticalScrollbar;
                var shell = root.GetComponentInChildren<NpcServicesWindowView>(true);
                foreach (var bomb in new[] { false, true })
                {
                    var view = Ref<PotionRefillWindowView>(shell, bomb ? "bombRefillService" : "potionRefillService");
                    var body = Ref<GameObject>(view, "serviceRoot").transform.Find("CraftingBody");
                    Rect(body, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
                    body.GetComponent<HorizontalLayoutGroup>().spacing = 28;
                    var left = body.Find("AssemblyColumn"); var right = body.Find("SummaryColumn");
                    Element(left, 432, -1, 0, 1); left.GetComponent<LayoutElement>().preferredWidth = 500;
                    Element(right, 330, -1, 1, 1);
                    left.GetComponent<VerticalLayoutGroup>().spacing = 12;
                    right.GetComponent<VerticalLayoutGroup>().spacing = 8;
                    var items = left.Find("BaseSection"); Section(items, bomb ? "Bomby" : "Mikstury", 160, 1);
                    Disable(items.Find("Content").gameObject);
                    if (items.Find("Content").GetComponent<Image>() != null) items.Find("Content").GetComponent<Image>().enabled = false;
                    var count = Node(items, "ConsumableCount", typeof(TextMeshProUGUI)); Text(count.GetComponent<TMP_Text>(), 18);
                    count.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Right;
                    Rect(count, new Vector2(1, 1), Vector2.one, new Vector2(-90, -28), Vector2.zero);
                    Wire(view, "consumableCountText", count.GetComponent<TMP_Text>());
                    var grid = Ref<Transform>(view, "recipeGrid");
                    var itemScroll = Scroll(items.Find("Content"), "ConsumableScroll", grid);
                    Wire(view, "consumableScroll", itemScroll);
                    foreach (Transform child in items.Find("Content"))
                        if (child != itemScroll.transform && child.name != "EmptyText") child.gameObject.SetActive(false);
                    var layout = grid.GetComponent<GridLayoutGroup>(); layout.cellSize = new Vector2(72, 72);
                    layout.spacing = new Vector2(10, 10); layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    layout.constraintCount = 5; layout.childAlignment = TextAnchor.UpperLeft; layout.padding = new RectOffset();
                    Disable(grid.gameObject); if (grid.GetComponent<Image>() != null) grid.GetComponent<Image>().enabled = false;
                    grid.Find("Border")?.gameObject.SetActive(false);
                    var empty = Node(items.Find("Content"), "EmptyText", typeof(TextMeshProUGUI)); Text(empty.GetComponent<TMP_Text>(), 18);
                    Rect(empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); Wire(view, "consumableEmptyText", empty.GetComponent<TMP_Text>());
                    var item = Ref<BlacksmithCraftingSlotView>(view, "recipeSlotTemplate"); Slot(item);
                    var amount = Ref<TextMeshProUGUI>(item, "countText"); Text(amount, 16);
                    Rect(amount.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 3), new Vector2(-5, 22));
                    var divider = Node(right, "ColumnDivider", typeof(Image), typeof(LayoutElement));
                    divider.GetComponent<LayoutElement>().ignoreLayout = true;
                    Rect(divider, Vector2.zero, new Vector2(0, 1), new Vector2(-14, 0), new Vector2(-13, 0));
                    divider.GetComponent<Image>().color = new Color32(107, 109, 87, 255); divider.GetComponent<Image>().raycastTarget = false;
                    var preview = right.Find("PreviewSection"); Section(preview, "Podgląd", 146, 0);
                    Wire(view, "previewSection", preview.gameObject);
                    var previewLayout = preview.Find("Content").GetComponent<VerticalLayoutGroup>();
                    if (previewLayout != null) { previewLayout.padding = new RectOffset(); previewLayout.spacing = 0; }
                    var result = Ref<BlacksmithCraftingSlotView>(view, "previewTooltipSlot"); Slot(result); Element(result.transform, 80, 80, 0, 0);
                    var header = result.transform.parent; Element(header, -1, 114, 1, 0);
                    header.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                    var name = Ref<TextMeshProUGUI>(view, "previewNameText"); Text(name, 23);
                    name.transform.parent.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                    Element(name.transform, -1, -1, 1, 0); name.overflowMode = TextOverflowModes.Ellipsis;
                    var inheritedInput = name.GetComponent<TMP_InputField>(); if (inheritedInput != null) inheritedInput.enabled = false;
                    var info = Ref<TextMeshProUGUI>(view, "previewSummaryText"); Text(info, 17); Element(info.transform, -1, 44, 1, 0);
                    var effects = Node(right, "EffectsSection", typeof(LayoutElement)); effects.SetSiblingIndex(preview.GetSiblingIndex() + 1);
                    Element(effects, -1, 108, 1, 0);
                    Wire(view, "effectsSection", effects.gameObject);
                    var title = Node(effects, "SectionTitle", typeof(TextMeshProUGUI)); Text(title.GetComponent<TMP_Text>(), 23); title.GetComponent<TMP_Text>().text = "Efekty";
                    Rect(title, new Vector2(0, 1), Vector2.one, new Vector2(0, -28), Vector2.zero);
                    var effectHost = Node(effects, "Content"); Rect(effectHost, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -32));
                    var text = Ref<TextMeshProUGUI>(view, "previewEffectsText")
                        ?? Node(effectHost, "EffectSummary", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); Text(text, 18);
                    Scroll(effectHost, "EffectsScroll", text.transform); Wire(view, "previewEffectsText", text);
                    var materials = left.Find("MaterialsSection") ?? right.Find("MaterialsSection");
                    materials.SetParent(right, false); materials.SetSiblingIndex(effects.GetSiblingIndex() + 1);
                    Section(materials, bomb ? "Proch strzelniczy" : "Alkohol", 160, 1);
                    Wire(view, "mediumSection", materials.gameObject);
                    var emptySpacer = Node(right, "EmptySummarySpacer", typeof(LayoutElement));
                    Element(emptySpacer, -1, 0, 1, 1); emptySpacer.gameObject.SetActive(false);
                    Wire(view, "emptySummarySpacer", emptySpacer.gameObject);
                    var materialGrid = Ref<Transform>(view, "materialsGrid"); Scroll(materials.Find("Content"), "MaterialsScroll", materialGrid);
                    var costs = materialGrid.GetComponent<GridLayoutGroup>(); costs.cellSize = new Vector2(88, 120);
                    costs.spacing = new Vector2(8, 12); costs.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    costs.constraintCount = 3; costs.childAlignment = TextAnchor.UpperLeft; costs.padding = new RectOffset();
                    var mediumEmpty = Ref<TextMeshProUGUI>(view, "mediumEmptyText"); mediumEmpty.transform.SetParent(materials.Find("Content"), false);
                    Text(mediumEmpty, 18); Rect(mediumEmpty.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    var material = Ref<BlacksmithCraftingSlotView>(view, "materialSlotTemplate"); Slot(material);
                    var materialButton = Ref<Button>(material, "button");
                    Rect(materialButton.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-36, -72), new Vector2(36, 0));
                    var owned = Ref<TextMeshProUGUI>(material, "countText"); owned.transform.SetParent(material.transform, false); Text(owned, 18);
                    owned.alignment = TextAlignmentOptions.Center;
                    Rect(owned.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -94), new Vector2(0, -74));
                    var buy = Ref<Button>(material, "buyButton"); Button(buy);
                    Rect(buy.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 24));
                    var coin = Node(buy.transform, "Icon", typeof(Image)).GetComponent<Image>(); Disable(coin.gameObject);
                    coin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");
                    coin.gameObject.SetActive(true); coin.color = Color.white; coin.preserveAspect = true; coin.raycastTarget = false;
                    Rect(coin.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(8, -8), new Vector2(24, 8));
                    var price = Ref<TextMeshProUGUI>(material, "buyPriceText"); Text(price, 18);
                    Rect(price.transform, Vector2.zero, Vector2.one, new Vector2(28, 2), new Vector2(-4, -2));
                    var status = Ref<TextMeshProUGUI>(view, "statusText"); Text(status, 17); Element(status.transform, -1, 24, 1, 0);
                    var craft = Ref<Button>(view, "craftButton"); Button(craft); Element(craft.transform, -1, 44, 1, 0);
                    craft.transform.SetAsLastSibling(); craft.transform.Find("Border")?.gameObject.SetActive(false);
                    Text(craft.GetComponentInChildren<TMP_Text>(true), 21); craft.GetComponentInChildren<TMP_Text>(true).text = "Napełnij";
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static ScrollRect Scroll(Transform host, string name, Transform content)
        {
            var node = Node(host, name, typeof(ScrollRect)); Rect(node, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var viewport = Node(node, "Viewport", typeof(Image), typeof(RectMask2D));
            viewport.GetComponent<Image>().color = Color.clear;
            Rect(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14, 0));
            content.SetParent(viewport, false); Rect(content, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            ((RectTransform)content).pivot = new Vector2(.5f, 1);
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = node.GetComponent<ScrollRect>(); scroll.content = (RectTransform)content; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            var bar = node.Find("Scrollbar")?.GetComponent<Scrollbar>();
            if (bar == null) { bar = Object.Instantiate(scrollbarSource, node, false); bar.name = "Scrollbar"; }
            Rect(bar.transform, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        private static void Section(Transform section, string title, float min, float flex)
        {
            Disable(section.gameObject); var image = section.GetComponent<Image>(); if (image != null) image.enabled = false;
            section.Find("Border")?.gameObject.SetActive(false); Element(section, -1, min, 1, flex);
            var heading = section.Find("SectionTitle").GetComponent<TMP_Text>(); Text(heading, 23); heading.text = title;
            Rect(heading.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -28), Vector2.zero);
            var content = section.Find("Content"); if (content != null) Rect(content, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -32));
        }
        private static void Slot(BlacksmithCraftingSlotView slot)
        {
            var button = Ref<Button>(slot, "button"); Disable(button.gameObject); button.transition = Selectable.Transition.None;
            var surface = button.GetComponent<Image>(); surface.sprite = palette.GetArtwork("item_frame"); surface.type = Image.Type.Sliced; surface.color = Color.white;
            var border = Ref<Image>(slot, "border"); Disable(border.gameObject); Rect(border.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); border.raycastTarget = false;
            var icon = Ref<Image>(slot, "icon"); Rect(icon.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-32, -32), new Vector2(32, 32)); icon.preserveAspect = true; icon.raycastTarget = false;
        }
        private static void Dropdown(TMP_Dropdown dropdown)
        {
            Text(dropdown.captionText, 18); dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis; Text(dropdown.itemText, 18);
            var template = dropdown.template; template.sizeDelta = new Vector2(template.sizeDelta.x, 196);
            var item = dropdown.itemText.transform.parent.GetComponent<RectTransform>(); item.sizeDelta = new Vector2(item.sizeDelta.x, 36);
            item.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 36);
            Rect(dropdown.itemText.transform, Vector2.zero, Vector2.one, new Vector2(26, 2), new Vector2(-6, -2));
            var background = item.Find("Item Background").GetComponent<Image>(); Disable(background.gameObject);
            background.sprite = palette.GetArtwork("tab_normal"); background.type = Image.Type.Sliced; background.color = Color.white;
        }
        private static void Button(Button button)
        {
            Disable(button.gameObject); var image = button.GetComponent<Image>(); image.sprite = palette.GetArtwork("tab_normal"); image.type = Image.Type.Sliced; image.color = Color.white;
            button.targetGraphic = image; image.overrideSprite = null; button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = palette.GetArtwork("tab_hover"), selectedSprite = image.sprite,
                pressedSprite = palette.GetArtwork("tab_pressed"), disabledSprite = palette.GetArtwork("tab_disabled") };
        }
        private static void Disable(GameObject go) { foreach (var style in go.GetComponents<UIWorkspaceStyleApplier>()) style.enabled = false; }
        private static void Text(TMP_Text text, int size) { Disable(text.gameObject); text.font = palette.Font; text.fontSize = size; text.enableAutoSizing = false; text.color = new Color32(223,215,184,255); text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; }
        private static void Element(Transform t, float w, float h, float fw, float fh) { var e = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>(); e.minWidth = e.preferredWidth = w; e.minHeight = e.preferredHeight = h; e.flexibleWidth = fw; e.flexibleHeight = fh; }
        private static RectTransform Node(Transform parent, string name, params Type[] types) { var node = parent.Find(name) as RectTransform; if (node != null) return node; var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); foreach (var type in types) go.AddComponent(type); return (RectTransform)go.transform; }
        private static void Rect(Transform t, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi) { var r = (RectTransform)t; r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f,.5f); r.offsetMin = lo; r.offsetMax = hi; }
        private static T Ref<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void Wire(Object owner, string field, Object value) { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
