using System;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Editor
{
    /// <summary>Restyles the existing crafting prefab without replacing its service wiring.</summary>
    public static class BlacksmithCraftingUiSetup
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_BlacksmithCraftingWindow.prefab";
        public const string ServicesPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab";
        static UIWorkspaceStylePalette palette;
        static readonly Color Cream = new Color32(223, 215, 184, 255);

        [MenuItem("Godmist/Content/UI/Configure Equipment Crafting")]
        public static void Configure()
        {
            ConfigurePrefab(PrefabPath);
            ConfigurePrefab(ServicesPath);
            AssetDatabase.SaveAssets();
        }
        static void ConfigurePrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                var view = root.GetComponentInChildren<BlacksmithCraftingWindowView>(true);
                var assemblyRoot = Get<Transform>(view, "equipmentContent").parent;
                while (assemblyRoot != null && assemblyRoot.name != "AssemblyColumn") assemblyRoot = assemblyRoot.parent;
                if (assemblyRoot == null) throw new InvalidOperationException("Crafting assembly column is missing in " + path);
                var body = assemblyRoot.parent;
                Rect(body, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-16, 0));
                var assembly = body.Find("AssemblyColumn"); var summary = body.Find("SummaryColumn");
                var horizontal = body.GetComponent<HorizontalLayoutGroup>(); horizontal.spacing = 28;
                Element(assembly, 500, -1, 0, 1); assembly.GetComponent<LayoutElement>().preferredWidth = 520;
                Element(summary, 330, -1, 1, 1);
                assembly.GetComponent<VerticalLayoutGroup>().spacing = 16;
                summary.GetComponent<VerticalLayoutGroup>().spacing = 16;
                var baseSection = Section(assembly.Find("BaseSection"), "Wyposażenie", 200, 0);
                var partsSection = Section(assembly.Find("PartsSection"), "Części", 190, 1);
                var settings = Section(assembly.Find("SettingsSection"), "Nazwa i jakość", 96, 0);
                var preview = Section(summary.Find("PreviewSection"), "Wynik", 240, 0);
                var materials = Section(summary.Find("MaterialsSection"), "Materiały", 174, 1);
                var divider = Node(summary, "ColumnDivider", typeof(Image), typeof(LayoutElement));
                divider.GetComponent<LayoutElement>().ignoreLayout = true;
                Rect(divider, Vector2.zero, new Vector2(0, 1), new Vector2(-14, 0), new Vector2(-13, 0)); Line(divider);
                var baseGrid = Get<Transform>(view, "equipmentContent").GetComponent<GridLayoutGroup>();
                baseGrid.cellSize = new Vector2(80, 80); baseGrid.spacing = new Vector2(12, 8); baseGrid.childAlignment = TextAnchor.UpperCenter;
                foreach (var stage in partsSection.Find("PartColumns").GetComponentsInChildren<VerticalLayoutGroup>(true))
                {
                    var label = stage.transform.Find("StageLabel").GetComponent<TextMeshProUGUI>(); Text(label, 18);
                    Element(label.transform, -1, 24, 1, 0);
                    var scroll = stage.GetComponentInChildren<ScrollRect>(true);
                    DisableStyles(scroll.gameObject); scroll.GetComponent<Image>().color = new Color32(37, 34, 40, 255);
                    scroll.transform.Find("Border").gameObject.SetActive(false);
                    Rect(scroll.viewport, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-16, -4));
                    var grid = scroll.content.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(64, 64); grid.spacing = new Vector2(8, 8); grid.childAlignment = TextAnchor.UpperCenter;
                }
                var resultHeader = preview.Find("ResultHeader"); Element(resultHeader, -1, 80, 1, 0);
                var resultIcon = Get<BlacksmithCraftingSlotView>(view, "previewTooltipSlot"); Element(resultIcon.transform, 80, 80, 0, 0);
                StyleSlot(resultIcon, 64);
                var name = Get<TextMeshProUGUI>(view, "previewNameText"); Text(name, 25); Element(name.transform, -1, 30, 1, 0);
                name.textWrappingMode = TextWrappingModes.NoWrap; name.overflowMode = TextOverflowModes.Ellipsis;
                var info = Get<TextMeshProUGUI>(view, "previewSummaryText"); Text(info, 17); Element(info.transform, -1, 42, 1, 0);
                var previewLayout = preview.GetComponent<VerticalLayoutGroup>(); previewLayout.spacing = 8; previewLayout.padding = new RectOffset(0, 0, 0, 0);
                var stats = Node(preview, "PreviewStats", typeof(TextMeshProUGUI), typeof(LayoutElement));
                var statsText = stats.GetComponent<TextMeshProUGUI>(); Text(statsText, 17); statsText.text = string.Empty; statsText.alignment = TextAlignmentOptions.TopLeft;
                Element(stats, -1, 120, 1, 0); Wire(view, "previewStatsText", statsText);
                var materialsGrid = Get<Transform>(view, "materialsContent");
                var materialScrollNode = Node(materials, "MaterialsScroll", typeof(ScrollRect));
                Rect(materialScrollNode, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var viewport = Node(materialScrollNode, "Viewport", typeof(Image), typeof(RectMask2D));
                viewport.GetComponent<Image>().color = Color.clear;
                Rect(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14, 0));
                materialsGrid.SetParent(viewport, false);
                var materialScroll = materialScrollNode.GetComponent<ScrollRect>(); materialScroll.content = (RectTransform)materialsGrid; materialScroll.viewport = viewport;
                materialScroll.horizontal = false; materialScroll.vertical = true; materialScroll.movementType = ScrollRect.MovementType.Clamped; materialScroll.scrollSensitivity = 24;
                var materialScrollbar = materialScrollNode.Find("Scrollbar")?.GetComponent<Scrollbar>();
                if (!materialScrollbar)
                {
                    materialScrollbar = Object.Instantiate(partsSection.GetComponentInChildren<ScrollRect>(true).verticalScrollbar, materialScrollNode, false);
                    materialScrollbar.name = "Scrollbar";
                }
                Rect(materialScrollbar.transform, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
                materialScroll.verticalScrollbar = materialScrollbar; materialScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                var fitter = materialsGrid.GetComponent<ContentSizeFitter>(); if (fitter) { fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
                Rect(materialsGrid, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
                ((RectTransform)materialsGrid).pivot = new Vector2(.5f, 1);
                var materialGrid = materialsGrid.GetComponent<GridLayoutGroup>(); materialGrid.constraintCount = 3; materialGrid.cellSize = new Vector2(96, 142); materialGrid.spacing = new Vector2(10, 12); materialGrid.childAlignment = TextAnchor.UpperCenter;
                StyleSlot(Get<BlacksmithCraftingSlotView>(view, "equipmentSlotTemplate"), 64);
                StyleSlot(Get<BlacksmithCraftingSlotView>(view, "partSlotTemplate"), 50);
                var part = Get<BlacksmithCraftingSlotView>(view, "partSlotTemplate");
                var partLevel = Get<TextMeshProUGUI>(part, "countText"); Text(partLevel, 16);
                Rect(partLevel.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-29, 3), new Vector2(-5, 23));
                var material = Get<BlacksmithCraftingSlotView>(view, "materialSlotTemplate");
                var materialSlot = material.transform.Find("MaterialSlotTemplate");
                Rect(materialSlot, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-42, -84), new Vector2(42, 0)); StyleSlot(material, 64);
                var count = Get<TextMeshProUGUI>(material, "countText"); count.transform.SetParent(material.transform, false); Text(count, 18); count.alignment = TextAlignmentOptions.Center;
                Rect(count.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -110), new Vector2(0, -86));
                var buy = Get<Button>(material, "buyButton"); Rect(buy.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 30));
                StyleButton(buy);
                var coin = buy.transform.Find("Icon").GetComponent<Image>(); DisableStyles(coin.gameObject); coin.gameObject.SetActive(true); coin.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png"); coin.color = Color.white; coin.enabled = true; coin.preserveAspect = true;
                Rect(coin.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(8, -8), new Vector2(24, 8));
                var price = Get<TextMeshProUGUI>(material, "buyPriceText"); Text(price, 18); Rect(price.transform, Vector2.zero, Vector2.one, new Vector2(28, 2), new Vector2(-4, -2));
                var status = Get<TextMeshProUGUI>(view, "statusText"); Text(status, 17); Element(status.transform, -1, 36, 1, 0);
                var craft = Get<Button>(view, "craftButton"); StyleButton(craft); Element(craft.transform, -1, 44, 1, 0);
                craft.transform.Find("Border").gameObject.SetActive(false); Text(craft.GetComponentInChildren<TextMeshProUGUI>(true), 21);
                var input = Get<TMP_InputField>(view, "customNameInput");
                Element(input.transform, -1, 40, 1, 0); Text(input.textComponent, 18); Text((TextMeshProUGUI)input.placeholder, 17);
                ((TMP_Text)input.placeholder).text = "Nazwa przedmiotu…";
                StyleQuality(Get<TMP_Dropdown>(view, "qualityDropdown"));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static Transform Section(Transform section, string title, float height, float flex)
        {
            DisableStyles(section.gameObject); section.GetComponent<Image>().enabled = false;
            section.Find("Border").gameObject.SetActive(false); Element(section, -1, height, 1, flex);
            var heading = section.Find("SectionTitle").GetComponent<TextMeshProUGUI>(); Text(heading, 23); heading.text = title;
            Rect(heading.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -28), Vector2.zero);
            var content = section.Find("Content") ?? section.Find("EquipmentContent");
            if (content == null) throw new InvalidOperationException("Section content is missing: " + section.name);
            Rect(content, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -32)); return content;
        }
        static void StyleSlot(BlacksmithCraftingSlotView slot, float iconSize)
        {
            var button = Get<Button>(slot, "button"); DisableStyles(button.gameObject); button.transition = Selectable.Transition.None;
            var surface = button.GetComponent<Image>(); surface.sprite = palette.GetArtwork("item_frame"); surface.type = Image.Type.Sliced; surface.color = Color.white;
            var border = Get<Image>(slot, "border"); DisableStyles(border.gameObject); Rect(border.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); border.raycastTarget = false;
            var icon = Get<Image>(slot, "icon"); Rect(icon.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-iconSize/2, -iconSize/2), new Vector2(iconSize/2, iconSize/2)); icon.preserveAspect = true; icon.raycastTarget = false;
            Get<TextMeshProUGUI>(slot, "countText").gameObject.SetActive(false);
        }
        static void StyleQuality(TMP_Dropdown dropdown)
        {
            Element(dropdown.transform, 170, 40, 0, 0); Text((TextMeshProUGUI)dropdown.captionText, 18);
            Text((TextMeshProUGUI)dropdown.itemText, 18);
            dropdown.itemText.color = Cream;
            var template = dropdown.template; Rect(template, new Vector2(0, 1), Vector2.one, new Vector2(0, 4), new Vector2(0, 126));
            template.pivot = new Vector2(.5f, 0); template.anchoredPosition = new Vector2(0, 4);
            var item = dropdown.itemText.transform.parent.GetComponent<RectTransform>(); item.sizeDelta = new Vector2(item.sizeDelta.x, 36);
            var content = item.parent.GetComponent<RectTransform>(); content.sizeDelta = new Vector2(content.sizeDelta.x, 44);
            var background = item.Find("Item Background").GetComponent<Image>(); DisableStyles(background.gameObject); background.sprite = palette.GetArtwork("button_normal"); background.type = Image.Type.Sliced;
            var toggle = item.GetComponent<Toggle>(); toggle.transition = Selectable.Transition.ColorTint; var colors = toggle.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.12f,1.12f,1.12f); colors.selectedColor = Color.white; toggle.colors = colors;
            Rect(dropdown.itemText.transform, Vector2.zero, Vector2.one, new Vector2(26, 2), new Vector2(-6, -2));
        }
        static void StyleButton(Button button)
        {
            DisableStyles(button.gameObject); var image = button.GetComponent<Image>(); image.sprite = palette.GetArtwork("button_normal"); image.type = Image.Type.Sliced; image.color = Color.white;
            button.transition = Selectable.Transition.SpriteSwap; var state = button.spriteState;
            state.highlightedSprite = palette.GetArtwork("button_hover"); state.selectedSprite = image.sprite; state.pressedSprite = palette.GetArtwork("button_pressed"); state.disabledSprite = palette.GetArtwork("button_disabled"); button.spriteState = state;
        }
        static void DisableStyles(GameObject go) { foreach(var style in go.GetComponents<UIWorkspaceStyleApplier>())style.enabled = false; }
        static void Text(TMP_Text text, int size) { DisableStyles(text.gameObject); text.font = palette.Font; text.fontSize = size; text.color = Cream; text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; }
        static void Element(Transform t, float width, float height, float flexWidth, float flexHeight) { var e=t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>(); e.minWidth=width; e.preferredWidth=width; e.minHeight=height; e.preferredHeight=height; e.flexibleWidth=flexWidth; e.flexibleHeight=flexHeight; }
        static RectTransform Node(Transform parent,string name,params Type[] types) { var t=parent.Find(name) as RectTransform; if(t)return t; var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); foreach(var type in types)go.AddComponent(type); return (RectTransform)go.transform; }
        static void Rect(Transform t,Vector2 min,Vector2 max,Vector2 leftBottom,Vector2 rightTop) { var r=(RectTransform)t; r.anchorMin=min; r.anchorMax=max; r.pivot=new Vector2(.5f,.5f); r.offsetMin=leftBottom; r.offsetMax=rightTop; }
        static void Line(RectTransform t) { var im=t.GetComponent<Image>(); im.color=new Color32(107,109,87,255); im.raycastTarget=false; }
        static T Get<T>(Object owner,string field) where T:Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        static void Wire(Object owner,string field,Object value) { var so=new SerializedObject(owner);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
