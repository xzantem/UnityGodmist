using System;
using System.Linq;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Editor
{
    /// <summary>Authors the jewelry tab without modifying the service shell or other services.</summary>
    public static class JewelryCraftingUiSetup
    {
        private static UIWorkspaceStylePalette palette;
        private static Scrollbar scrollbarSource;
        [MenuItem("Godmist/Content/UI/Configure Jewelry Crafting")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(TradeUiSetup.PrefabPath);
            try
            {
                palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                scrollbarSource = root.GetComponentInChildren<MaterialCraftingWindowView>(true)
                    .GetComponentsInChildren<ScrollRect>(true).First(scroll => scroll.verticalScrollbar != null).verticalScrollbar;
                var view = root.GetComponentInChildren<JewelryCraftingWindowView>(true);
                var body = Ref<GameObject>(view, "serviceRoot").transform;
                var templates = Node(body, "JewelryTemplates", typeof(LayoutElement));
                templates.GetComponent<LayoutElement>().ignoreLayout = true;
                templates.gameObject.SetActive(false);
                foreach (var field in new[] { "jewelrySlotTemplate", "ingredientSlotTemplate", "materialSlotTemplate" })
                {
                    var source = Ref<BlacksmithCraftingSlotView>(view, field);
                    if (source.transform.parent == templates) continue;
                    var clone = Object.Instantiate(source, templates, false);
                    clone.name = field; clone.gameObject.SetActive(false); Wire(view, field, clone);
                }

                Rect(body, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
                body.GetComponent<HorizontalLayoutGroup>().spacing = 28;
                var left = body.Find("AssemblyColumn"); var right = body.Find("SummaryColumn");
                Element(left, 432, -1, 0, 1); left.GetComponent<LayoutElement>().preferredWidth = 500;
                Element(right, 330, -1, 1, 1);
                left.GetComponent<VerticalLayoutGroup>().spacing = 12;
                right.GetComponent<VerticalLayoutGroup>().spacing = 8;
                var types = left.Find("BaseSection"); Section(types, "Biżuteria", 120, 0);
                var typeGrid = Ref<Transform>(view, "jewelryContent").GetComponent<GridLayoutGroup>();
                Grid(typeGrid, 4, new Vector2(72,72)); Slot(Ref<BlacksmithCraftingSlotView>(view,"jewelrySlotTemplate"));
                var ingredients = left.Find("PartsSection"); Section(ingredients, "Materiały", 200, 1);
                var columns = ingredients.Find("Content/PartColumns");
                var columnLayout = columns.GetComponent<HorizontalLayoutGroup>();
                columnLayout.spacing = 16; columnLayout.padding = new RectOffset();
                columnLayout.childControlWidth = columnLayout.childControlHeight = true;
                columnLayout.childForceExpandWidth = columnLayout.childForceExpandHeight = true;
                foreach (Transform stage in columns) stage.gameObject.SetActive(false);
                foreach (var field in new[] { "metalContent", "gemContent" })
                {
                    var grid = Ref<Transform>(view, field); var stage = grid.parent.parent.parent;
                    stage.gameObject.SetActive(true); Element(stage, 180, -1, 1, 1);
                    var label = stage.Find("StageLabel").GetComponent<TMP_Text>(); Text(label, 19);
                    label.text = field == "metalContent" ? "Metal" : "Klejnot";
                    label.alignment = TextAlignmentOptions.Left; Element(label.transform, -1, 28, 1, 0); label.transform.SetAsFirstSibling();
                    Rect(label.transform, new Vector2(0,1), Vector2.one, new Vector2(0,-28), Vector2.zero);
                    // Keep existing scroll identities; rebuild its clipping and layout through Editor APIs.
                    var scroll = grid.GetComponentInParent<ScrollRect>(true); Disable(scroll.gameObject); Element(scroll.transform, -1, 160, 1, 1);
                    if (scroll.GetComponent<Image>() != null) scroll.GetComponent<Image>().enabled = false;
                    scroll.transform.Find("Border")?.gameObject.SetActive(false);
                    Rect(scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0,-32));
                    var viewport = (RectTransform)grid.parent; scroll.viewport = viewport; scroll.content = (RectTransform)grid; var mask = viewport.GetComponent<Mask>(); if (mask != null) mask.enabled = false;
                    if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
                    Disable(viewport.gameObject); if (viewport.GetComponent<Image>() != null) viewport.GetComponent<Image>().enabled = false;
                    Rect(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14,0));
                    Rect(grid, new Vector2(0,1), Vector2.one, Vector2.zero, Vector2.zero); ((RectTransform)grid).pivot = new Vector2(.5f,1);
                    var fitter = grid.GetComponent<ContentSizeFitter>() ?? grid.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    Grid(grid.GetComponent<GridLayoutGroup>(),2,new Vector2(72,72));
                    scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
                    Rect(scroll.verticalScrollbar.transform, new Vector2(1,0), Vector2.one,new Vector2(-10,0),Vector2.zero);
                    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                    Wire(view, field == "metalContent" ? "metalScroll" : "gemScroll", scroll);
                }
                Slot(Ref<BlacksmithCraftingSlotView>(view,"ingredientSlotTemplate"));
                var names = left.Find("SettingsSection"); Section(names,"Nazwa",80,0);
                var input = Ref<TMP_InputField>(view,"customNameInput"); Disable(input.gameObject);
                Rect(input.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,40));
                Text(input.textComponent,19);Text((TMP_Text)input.placeholder,18);
                ((TMP_Text)input.placeholder).text = "Nazwa biżuterii (opcjonalnie)…";
                var divider = Node(right,"ColumnDivider",typeof(Image),typeof(LayoutElement)); divider.GetComponent<LayoutElement>().ignoreLayout = true;
                Rect(divider,Vector2.zero,new Vector2(0,1),new Vector2(-14,0),new Vector2(-13,0));
                divider.GetComponent<Image>().color = new Color32(107,109,87,255);divider.GetComponent<Image>().raycastTarget = false;
                var preview = right.Find("PreviewSection"); preview.SetAsFirstSibling(); Section(preview,"Podgląd",132,0);
                var previewLayout = preview.Find("Content").GetComponent<VerticalLayoutGroup>(); previewLayout.padding = new RectOffset(); previewLayout.spacing = 0;
                var result = Ref<BlacksmithCraftingSlotView>(view,"previewTooltipSlot"); Slot(result);Element(result.transform,80,80,0,0);
                var header = result.transform.parent;Element(header,-1,100,1,0);header.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                var name = Ref<TextMeshProUGUI>(view,"previewNameText");Text(name,23);Element(name.transform,-1,-1,1,0);name.overflowMode = TextOverflowModes.Ellipsis;
                var infoLayout = name.transform.parent.GetComponent<VerticalLayoutGroup>();
                infoLayout.childControlWidth = infoLayout.childControlHeight = true;
                infoLayout.childForceExpandWidth = true; infoLayout.childForceExpandHeight = false;
                infoLayout.childAlignment = TextAnchor.UpperLeft; infoLayout.spacing = 4; infoLayout.padding = new RectOffset();
                var inheritedInput = name.GetComponent<TMP_InputField>();if(inheritedInput != null) inheritedInput.enabled = false;
                var info = Ref<TextMeshProUGUI>(view,"previewSummaryText");Text(info,17);Element(info.transform,-1,44,1,0);
                var effects = Node(right,"EffectsSection",typeof(LayoutElement));effects.SetSiblingIndex(1);Element(effects,-1,108,1,0);
                var title = Node(effects,"SectionTitle",typeof(TextMeshProUGUI));Text(title.GetComponent<TMP_Text>(),23);title.GetComponent<TMP_Text>().text = "Statystyki i efekty";
                Rect(title,new Vector2(0,1),Vector2.one,new Vector2(0,-28),Vector2.zero);
                var host = Node(effects,"Content");Rect(host,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,-32));
                var effectText = Ref<TextMeshProUGUI>(view,"previewEffectsText") ?? Node(host,"EffectSummary",typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                Text(effectText,18);Scroll(host,"EffectsScroll",effectText.transform);Wire(view,"previewEffectsText",effectText);
                var materials = right.Find("MaterialsSection");materials.SetSiblingIndex(2);Section(materials,"Koszt wytworzenia",160,1);
                var costs = Ref<Transform>(view,"materialsContent");Scroll(materials.Find("Content"),"MaterialsScroll",costs);
                Grid(costs.GetComponent<GridLayoutGroup>(),3,new Vector2(88,120));
                var material = Ref<BlacksmithCraftingSlotView>(view,"materialSlotTemplate");Slot(material);
                var materialButton = Ref<Button>(material,"button");Rect(materialButton.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-36,-72),new Vector2(36,0));
                var owned = Ref<TextMeshProUGUI>(material,"countText");owned.transform.SetParent(material.transform,false);Text(owned,18);owned.alignment = TextAlignmentOptions.Center;
                Rect(owned.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-94),new Vector2(0,-74));
                var buy = Ref<Button>(material,"buyButton");Button(buy);Rect(buy.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,24));
                var coin = Node(buy.transform,"Icon",typeof(Image)).GetComponent<Image>();Disable(coin.gameObject);
                coin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");coin.gameObject.SetActive(true);coin.color = Color.white;coin.preserveAspect = true;coin.raycastTarget = false;
                Rect(coin.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(8,-8),new Vector2(24,8));
                var price = Ref<TextMeshProUGUI>(material,"buyPriceText");Text(price,18);Rect(price.transform,Vector2.zero,Vector2.one,new Vector2(28,2),new Vector2(-4,-2));
                var status = Ref<TextMeshProUGUI>(view,"statusText");Text(status,17);Element(status.transform,-1,24,1,0);status.transform.SetAsLastSibling();
                var craft = Ref<Button>(view,"craftButton");Button(craft);Element(craft.transform,-1,44,1,0);craft.transform.SetAsLastSibling();
                craft.transform.Find("Border")?.gameObject.SetActive(false);Text(craft.GetComponentInChildren<TMP_Text>(true),21);craft.GetComponentInChildren<TMP_Text>(true).text = "Wytwórz";
                PrefabUtility.SaveAsPrefabAsset(root,TradeUiSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        private static void Grid(GridLayoutGroup grid,int columns,Vector2 cell)
        {
            grid.cellSize = cell; grid.spacing = new Vector2(10,10);grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;grid.childAlignment = TextAnchor.UpperLeft;grid.padding = new RectOffset();
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
        private static void Disable(GameObject go)
        {
            foreach (var style in go.GetComponents<UIWorkspaceStyleApplier>())
            {
                // OnValidate still runs on disabled ExecuteAlways components during prefab authoring.
                // Clear only this tab's local targets so it cannot overwrite the authored values.
                foreach (var target in new[] { "surfaceImage", "borderImage", "text", "selectable" }) Wire(style, target, null);
                style.enabled = false;
            }
        }
        private static void Text(TMP_Text text, int size) { Disable(text.gameObject); text.font = palette.Font; text.fontSize = size; text.enableAutoSizing = false; text.color = new Color32(223,215,184,255); text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; }
        private static void Element(Transform t, float w, float h, float fw, float fh) { var e = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>(); e.minWidth = e.preferredWidth = w; e.minHeight = e.preferredHeight = h; e.flexibleWidth = fw; e.flexibleHeight = fh; }
        private static RectTransform Node(Transform parent, string name, params Type[] types) { var node = parent.Find(name) as RectTransform; if (node != null) return node; var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); foreach (var type in types) go.AddComponent(type); return (RectTransform)go.transform; }
        private static void Rect(Transform t, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi) { var r = (RectTransform)t; r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f,.5f); r.offsetMin = lo; r.offsetMax = hi; }
        private static T Ref<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void Wire(Object owner, string field, Object value) { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
