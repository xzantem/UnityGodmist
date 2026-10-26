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
    /// <summary>Authors only the galduryte crafting tab and its dedicated templates.</summary>
    public static class GalduryteCraftingUiSetup
    {
        private static UIWorkspaceStylePalette palette;
        private static Scrollbar scrollbarSource;
        [MenuItem("Godmist/Content/UI/Configure Galduryte Crafting")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(TradeUiSetup.PrefabPath);
            try
            {
                palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                scrollbarSource = root.GetComponentInChildren<MaterialCraftingWindowView>(true)
                    .GetComponentsInChildren<ScrollRect>(true).First(s => s.verticalScrollbar != null).verticalScrollbar;
                var view = root.GetComponentInChildren<GalduryteCraftingWindowView>(true);
                var body = Ref<GameObject>(view,"serviceRoot").transform;
                var templates = Node(body,"GalduryteTemplates",typeof(LayoutElement));
                templates.GetComponent<LayoutElement>().ignoreLayout = true; templates.gameObject.SetActive(false);
                foreach(var field in new[]{"stoneSlotTemplate","powderSlotTemplate","materialSlotTemplate"})
                {
                    var source=Ref<BlacksmithCraftingSlotView>(view,field);
                    if(source.transform.parent==templates)continue;
                    var clone=Object.Instantiate(source,templates,false);clone.name=field;clone.gameObject.SetActive(false);Wire(view,field,clone);
                }
                Rect(body,Vector2.zero,Vector2.one,new Vector2(16,0),new Vector2(-16,0));body.GetComponent<HorizontalLayoutGroup>().spacing=28;
                var left=body.Find("AssemblyColumn");var right=body.Find("SummaryColumn");
                Element(left,432,-1,0,1);left.GetComponent<LayoutElement>().preferredWidth=500;Element(right,330,-1,1,1);
                left.GetComponent<VerticalLayoutGroup>().spacing=12;right.GetComponent<VerticalLayoutGroup>().spacing=8;
                var target=left.Find("SettingsSection");target.SetAsFirstSibling();Section(target,"Przeznaczenie",80,0);
                foreach(Transform child in target.Find("Content"))child.gameObject.SetActive(false);
                foreach(var field in new[]{"weaponButton","armorButton"})
                {
                    var b=Ref<Button>(view,field);b.gameObject.SetActive(true);Button(b);b.transform.Find("Border")?.gameObject.SetActive(false);b.transform.Find("Icon")?.gameObject.SetActive(false);
                    var weapon=field=="weaponButton";Rect(b.transform,new Vector2(weapon?0:.5f,0),new Vector2(weapon?.5f:1,1),new Vector2(weapon?0:6,0),new Vector2(weapon?-6:0,0));Text(b.GetComponentInChildren<TMP_Text>(true),20);
                }
                var stones=left.Find("BaseSection");stones.SetSiblingIndex(1);Section(stones,"Kamień galdurytowy",120,0);
                var stoneGrid=Ref<Transform>(view,"stoneGrid");Grid(stoneGrid.GetComponent<GridLayoutGroup>(),5,new Vector2(72,72));Slot(Ref<BlacksmithCraftingSlotView>(view,"stoneSlotTemplate"));
                var powders=left.Find("PartsSection");powders.SetSiblingIndex(2);Section(powders,"Proszek",180,1);
                var powderGrid=Ref<Transform>(view,"powderGrid");var columns=powders.Find("Content/PartColumns");foreach(Transform stage in columns)stage.gameObject.SetActive(false);
                var powderScroll=Scroll(powders.Find("Content"),"PowderScroll",powderGrid);Wire(view,"powderScroll",powderScroll);
                Grid(powderGrid.GetComponent<GridLayoutGroup>(),5,new Vector2(72,72));Slot(Ref<BlacksmithCraftingSlotView>(view,"powderSlotTemplate"));
                var divider=Node(right,"ColumnDivider",typeof(Image),typeof(LayoutElement));divider.GetComponent<LayoutElement>().ignoreLayout=true;
                Rect(divider,Vector2.zero,new Vector2(0,1),new Vector2(-14,0),new Vector2(-13,0));divider.GetComponent<Image>().color=new Color32(107,109,87,255);divider.GetComponent<Image>().raycastTarget=false;
                var preview=right.Find("PreviewSection");preview.SetAsFirstSibling();Section(preview,"Podgląd",132,0);
                var layout=preview.Find("Content").GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset();layout.spacing=0;
                var result=Ref<BlacksmithCraftingSlotView>(view,"previewTooltipSlot");Slot(result);Element(result.transform,80,80,0,0);
                var header=result.transform.parent;Element(header,-1,100,1,0);header.GetComponent<HorizontalLayoutGroup>().childAlignment=TextAnchor.UpperLeft;
                var name=Ref<TextMeshProUGUI>(view,"previewNameText");Text(name,23);Element(name.transform,-1,-1,1,0);name.overflowMode=TextOverflowModes.Ellipsis;
                var infoLayout=name.transform.parent.GetComponent<VerticalLayoutGroup>();infoLayout.childControlWidth=infoLayout.childControlHeight=true;infoLayout.childForceExpandWidth=true;infoLayout.childForceExpandHeight=false;infoLayout.childAlignment=TextAnchor.UpperLeft;infoLayout.spacing=4;infoLayout.padding=new RectOffset();
                var inheritedInput=name.GetComponent<TMP_InputField>();if(inheritedInput!=null)inheritedInput.enabled=false;
                var info=Ref<TextMeshProUGUI>(view,"previewSummaryText");Text(info,17);Element(info.transform,-1,44,1,0);
                var effects=Node(right,"EffectsSection",typeof(LayoutElement));effects.SetSiblingIndex(1);Element(effects,-1,156,1,0);
                var title=Node(effects,"SectionTitle",typeof(TextMeshProUGUI));Text(title.GetComponent<TMP_Text>(),23);title.GetComponent<TMP_Text>().text="Możliwe efekty";Rect(title,new Vector2(0,1),Vector2.one,new Vector2(0,-28),Vector2.zero);
                var host=Node(effects,"Content");Rect(host,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(0,-32));
                var effectText=Ref<TextMeshProUGUI>(view,"previewEffectsText")??Node(host,"EffectSummary",typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();Text(effectText,18);var effectsScroll=Scroll(host,"EffectsScroll",effectText.transform);Wire(view,"previewEffectsText",effectText);Wire(view,"effectsScroll",effectsScroll);Wire(view,"effectsSectionLayout",effects.GetComponent<LayoutElement>());
                var materials=right.Find("MaterialsSection");materials.SetSiblingIndex(2);Section(materials,"Koszt wytworzenia",154,1);
                var costs=Ref<Transform>(view,"materialsGrid");Scroll(materials.Find("Content"),"MaterialsScroll",costs);Grid(costs.GetComponent<GridLayoutGroup>(),2,new Vector2(88,120));
                var material=Ref<BlacksmithCraftingSlotView>(view,"materialSlotTemplate");Slot(material);
                var materialButton=Ref<Button>(material,"button");Rect(materialButton.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-36,-72),new Vector2(36,0));
                var owned=Ref<TextMeshProUGUI>(material,"countText");owned.transform.SetParent(material.transform,false);Text(owned,18);owned.alignment=TextAlignmentOptions.Center;Rect(owned.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-94),new Vector2(0,-74));
                var buy=Ref<Button>(material,"buyButton");Button(buy);Rect(buy.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,24));
                var coin=Node(buy.transform,"Icon",typeof(Image)).GetComponent<Image>();Disable(coin.gameObject);coin.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png");coin.gameObject.SetActive(true);coin.color=Color.white;coin.preserveAspect=true;coin.raycastTarget=false;Rect(coin.transform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(8,-8),new Vector2(24,8));
                var price=Ref<TextMeshProUGUI>(material,"buyPriceText");Text(price,18);Rect(price.transform,Vector2.zero,Vector2.one,new Vector2(28,2),new Vector2(-4,-2));
                var status=Ref<TextMeshProUGUI>(view,"statusText");Text(status,17);Element(status.transform,-1,24,1,0);status.transform.SetAsLastSibling();
                var craft=Ref<Button>(view,"craftButton");Button(craft);Element(craft.transform,-1,44,1,0);craft.transform.SetAsLastSibling();craft.transform.Find("Border")?.gameObject.SetActive(false);Text(craft.GetComponentInChildren<TMP_Text>(true),21);
                PrefabUtility.SaveAsPrefabAsset(root,TradeUiSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}AssetDatabase.SaveAssets();
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
