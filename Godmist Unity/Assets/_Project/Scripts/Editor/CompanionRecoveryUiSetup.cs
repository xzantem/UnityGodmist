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
    /// <summary>Native authoring confined to the priest companion recovery tab.</summary>
    public static class CompanionRecoveryUiSetup
    {
        private static UIWorkspaceStylePalette palette;
        [MenuItem("Godmist/Content/UI/Configure Companion Recovery")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(TradeUiSetup.PrefabPath);
            try
            {
                palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                var shell = root.GetComponentInChildren<NpcServicesWindowView>(true);
                var view = Ref<CompanionRecoveryWindowView>(shell, "companionRecoveryService");
                var body = Ref<GameObject>(view, "serviceRoot").transform;
                Disable(body); body.GetComponent<Image>().enabled = false;
                Rect(body, Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-16, -12));
                var layout = body.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(); layout.spacing = 12;
                layout.childControlHeight = layout.childControlWidth = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                var top = body.Find("TopBar"); Element(top, -1, 36, 1, 0);
                var topLayout = top.GetComponent<HorizontalLayoutGroup>(); topLayout.padding = new RectOffset(); topLayout.spacing = 10;
                topLayout.childControlWidth = topLayout.childControlHeight = true;
                topLayout.childForceExpandWidth = topLayout.childForceExpandHeight = false; topLayout.childAlignment = TextAnchor.MiddleLeft;
                var heading = top.Find("Heading").GetComponent<TMP_Text>(); Text(heading, 23); heading.text = "Towarzysze";
                Element(heading.transform, -1, 32, 0, 0);
                var count = Node(top, "CompanionCount", typeof(TextMeshProUGUI), typeof(LayoutElement));
                Text(count.GetComponent<TMP_Text>(), 18); count.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Left;
                count.GetComponent<TMP_Text>().color = new Color32(190, 184, 161, 255);
                Element(count, 32, 32, 0, 0); count.SetSiblingIndex(1); Wire(view, "countText", count.GetComponent<TMP_Text>());
                var spacer = Node(top, "HeaderSpacer", typeof(LayoutElement)); Element(spacer, -1, 32, 1, 0); spacer.SetSiblingIndex(2);
                var wallet = Ref<TextMeshProUGUI>(view, "walletText"); Text(wallet, 21); wallet.alignment = TextAlignmentOptions.Right;
                var walletGroup = wallet.transform.parent; Element(walletGroup, 208, 32, 0, 0);
                walletGroup.GetComponent<HorizontalLayoutGroup>().spacing = 8;
                walletGroup.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                walletGroup.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                walletGroup.GetComponent<HorizontalLayoutGroup>().childControlWidth = walletGroup.GetComponent<HorizontalLayoutGroup>().childControlHeight = true;
                walletGroup.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
                Element(wallet.transform, 180, 32, 0, 0); wallet.overflowMode = TextOverflowModes.Overflow;
                wallet.textWrappingMode = TextWrappingModes.NoWrap; wallet.enableAutoSizing = true; wallet.fontSizeMin = 16; wallet.fontSizeMax = 21;
                var walletCoin = walletGroup.Find("GoldIcon").GetComponent<Image>(); Coin(walletCoin); Element(walletCoin.transform, 20, 20, 0, 0);
                var scroll = body.Find("CompanionScroll").GetComponent<ScrollRect>(); Wire(view, "rowsScroll", scroll);
                Disable(scroll.transform); if (scroll.GetComponent<Image>() != null) scroll.GetComponent<Image>().enabled = false;
                Element(scroll.transform, -1, 150, 1, 1); scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
                scroll.viewport.GetComponent<Image>().color = Color.clear;
                var source = root.GetComponentInChildren<MaterialCraftingWindowView>(true).GetComponentsInChildren<ScrollRect>(true)
                    .First(s => s.verticalScrollbar != null).verticalScrollbar;
                var bar = scroll.verticalScrollbar;
                if (bar == null) { bar = Object.Instantiate(source, scroll.transform, false); bar.name = "Scrollbar"; }
                Rect(bar.transform, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
                scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                Rect(scroll.viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14, 0));
                var rows = Ref<Transform>(view, "rowsRoot");
                Rect(rows, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero); ((RectTransform)rows).pivot = new Vector2(.5f, 1);
                var rowsLayout = rows.GetComponent<VerticalLayoutGroup>(); rowsLayout.padding = new RectOffset(); rowsLayout.spacing = 8;
                rowsLayout.childControlWidth = rowsLayout.childControlHeight = true; rowsLayout.childForceExpandWidth = true; rowsLayout.childForceExpandHeight = false;
                var fit = rows.GetComponent<ContentSizeFitter>() ?? rows.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var status = Ref<TextMeshProUGUI>(view, "messageText"); Text(status, 18); Element(status.transform, -1, 26, 1, 0); status.transform.SetSiblingIndex(top.GetSiblingIndex() + 1);
                var row = Ref<CompanionRecoveryRowView>(view, "rowTemplate"); Element(row.transform, -1, 104, 1, 0);
                Disable(row.transform); row.GetComponent<Image>().color = new Color32(34, 33, 38, 255);
                var rowLayout = row.GetComponent<HorizontalLayoutGroup>(); rowLayout.padding = new RectOffset(12, 12, 12, 12); rowLayout.spacing = 16;
                rowLayout.childAlignment = TextAnchor.MiddleLeft; rowLayout.childForceExpandHeight = false; rowLayout.childForceExpandWidth = false;
                rowLayout.childControlWidth = rowLayout.childControlHeight = true;
                var portrait = Ref<Image>(row, "portrait"); Disable(portrait.transform); portrait.color = Color.white; portrait.preserveAspect = true; portrait.raycastTarget = false;
                var portraitFrame = Node(row.transform, "PortraitFrame", typeof(Image), typeof(LayoutElement));
                portraitFrame.SetAsFirstSibling(); Element(portraitFrame, 80, 80, 0, 0);
                portraitFrame.GetComponent<Image>().sprite = palette.GetArtwork("item_frame"); portraitFrame.GetComponent<Image>().type = Image.Type.Sliced; portraitFrame.GetComponent<Image>().raycastTarget = false;
                portrait.transform.SetParent(portraitFrame, false); var portraitElement = portrait.GetComponent<LayoutElement>(); if (portraitElement != null) portraitElement.ignoreLayout = true;
                Rect(portrait.transform, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
                var name = Ref<TextMeshProUGUI>(row, "title"); var level = Ref<TextMeshProUGUI>(row, "levelText"); var state = Ref<TextMeshProUGUI>(row, "statusText");
                var info = row.transform.Find("CharacterInfo"); Element(info, 180, 80, 1, 0);
                var infoLayout = info.GetComponent<VerticalLayoutGroup>(); infoLayout.padding = new RectOffset(); infoLayout.spacing = 2;
                infoLayout.childForceExpandHeight = false; infoLayout.childForceExpandWidth = true;
                infoLayout.childControlHeight = infoLayout.childControlWidth = true; infoLayout.childAlignment = TextAnchor.MiddleLeft;
                var nameLine = info.Find("NameLine"); Element(nameLine, -1, 28, 1, 0); nameLine.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset();
                nameLine.SetAsFirstSibling(); level.transform.SetParent(info, false); level.transform.SetSiblingIndex(1); state.transform.SetAsLastSibling();
                Text(name, 23); name.overflowMode = TextOverflowModes.Ellipsis; Element(name.transform, -1, 28, 1, 0);
                Text(level, 17); Element(level.transform, -1, 22, 1, 0);
                Text(state, 17); state.color = new Color32(190, 184, 161, 255); state.overflowMode = TextOverflowModes.Ellipsis; Element(state.transform, -1, 22, 1, 0);
                var action = row.transform.Find("Action"); Element(action, 310, 80, 0, 0);
                var actionLayout = action.GetComponent<HorizontalLayoutGroup>(); actionLayout.padding = new RectOffset(); actionLayout.spacing = 16;
                actionLayout.childControlWidth = actionLayout.childControlHeight = true;
                actionLayout.childAlignment = TextAnchor.MiddleRight; actionLayout.childForceExpandHeight = actionLayout.childForceExpandWidth = false;
                var price = Ref<TextMeshProUGUI>(row, "price"); Text(price, 21); price.alignment = TextAlignmentOptions.Right;
                price.overflowMode = TextOverflowModes.Overflow; Element(price.transform, 110, 32, 0, 0);
                price.enableAutoSizing = true; price.fontSizeMin = 12; price.fontSizeMax = 21;
                price.textWrappingMode = TextWrappingModes.NoWrap; price.raycastTarget = true;
                var priceGroup = price.transform.parent; Element(priceGroup, 138, 40, 0, 0); priceGroup.GetComponent<HorizontalLayoutGroup>().spacing = 6;
                priceGroup.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                priceGroup.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
                priceGroup.GetComponent<HorizontalLayoutGroup>().childControlWidth = priceGroup.GetComponent<HorizontalLayoutGroup>().childControlHeight = true;
                priceGroup.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
                var coin = priceGroup.Find("GoldIcon").GetComponent<Image>(); Coin(coin); Element(coin.transform, 20, 20, 0, 0);
                var button = Ref<Button>(row, "recoverButton"); Element(button.transform.parent, 156, 40, 0, 0);
                Disable(button.transform); var image = button.GetComponent<Image>(); image.sprite = palette.GetArtwork("tab_normal"); image.type = Image.Type.Sliced; image.color = Color.white;
                button.targetGraphic = image; button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState { highlightedSprite = palette.GetArtwork("tab_hover"), selectedSprite = image.sprite,
                    pressedSprite = palette.GetArtwork("tab_pressed"), disabledSprite = palette.GetArtwork("tab_disabled") };
                button.transform.Find("Border")?.gameObject.SetActive(false);
                Text(button.GetComponentInChildren<TMP_Text>(true), 21);
                PrefabUtility.SaveAsPrefabAsset(root, TradeUiSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
        private static void Coin(Image icon) { Disable(icon.transform); icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png"); icon.color = Color.white; icon.preserveAspect = true; icon.raycastTarget = false; }
        private static void Disable(Transform t)
        {
            foreach (var style in t.GetComponents<UIWorkspaceStyleApplier>())
            {
                // OnValidate still runs on disabled ExecuteAlways components during prefab authoring.
                // Clear only this tab's local targets so it cannot overwrite the authored values.
                foreach (var target in new[] { "surfaceImage", "borderImage", "text", "selectable" }) Wire(style, target, null);
                style.enabled = false;
            }
        }
        private static void Text(TMP_Text text, int size) { Disable(text.transform); text.font = palette.Font; text.fontSize = size; text.enableAutoSizing = false; text.color = new Color32(223,215,184,255); text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; }
        private static void Element(Transform t, float w, float h, float fw, float fh) { var e = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>(); e.minWidth = e.preferredWidth = w; e.minHeight = e.preferredHeight = h; e.flexibleWidth = fw; e.flexibleHeight = fh; }
        private static RectTransform Node(Transform parent, string name, params Type[] types) { var node = parent.Find(name) as RectTransform; if (node != null) return node; var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); foreach (var type in types) go.AddComponent(type); return (RectTransform)go.transform; }
        private static void Rect(Transform t, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi) { var r = (RectTransform)t; r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f,.5f); r.offsetMin = lo; r.offsetMax = hi; }
        private static T Ref<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void Wire(Object owner, string field, Object value) { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
