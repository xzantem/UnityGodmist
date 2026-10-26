using System;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Editor
{
    /// <summary>Authors only the stash variant; shared window/slot prefabs remain untouched.</summary>
    public static class CampaignStorageUiSetup
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_CampaignStorageWindow.prefab";

        [MenuItem("Godmist/Content/UI/Configure Campaign Storage")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponent<CampaignStorageWindowView>();
                var palette = root.GetComponent<UIWorkspaceStyleApplier>().Palette;
                var title = root.transform.Find("Header/Title").GetComponent<TextMeshProUGUI>();
                Text(title, palette, 24);
                title.text = "Skrytka";
                title.alignment = TextAlignmentOptions.MidlineLeft;
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.overflowMode = TextOverflowModes.Ellipsis;
                Rect(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 4), new Vector2(-56, -4));
                Wire(view, "titleText", title);

                var grid = Ref<Transform>(view, "stashItemsRoot").GetComponent<GridLayoutGroup>();
                var scroll = grid.GetComponentInParent<ScrollRect>();
                var body = root.transform.Find("Content");
                var layout = body.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(8, 8, 8, 0);
                layout.spacing = 12;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var heading = body.Find("TransferPanels/StashPanel/StashHeading") as RectTransform
                    ?? Node(body, "StashHeading", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                heading.SetAsFirstSibling();
                Element(heading, -1, 28, 1, 0);
                var row = heading.GetComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(); row.spacing = 12;
                row.childAlignment = TextAnchor.MiddleLeft;
                row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = false;
                var label = Node(heading, "Label", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                Text(label, palette, 21); label.text = "Skrytka";
                label.alignment = TextAlignmentOptions.MidlineLeft;
                Element(label.transform, -1, 28, 1, 0);
                var count = Node(heading, "Count", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                Text(count, palette, 17); count.text = "0";
                count.alignment = TextAlignmentOptions.MidlineRight;
                count.textWrappingMode = TextWrappingModes.NoWrap;
                Element(count.transform, 96, 28, 0, 0); Wire(view, "itemCountText", count);

                Element(scroll.transform, -1, 120, 1, 1);
                scroll.transform.Find("Border")?.gameObject.SetActive(false);
                var surface = scroll.GetComponent<Image>();
                if (surface != null) { DisableStyle(surface.gameObject); surface.color = Color.clear; }
                var viewport = scroll.viewport;
                Rect(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-18, 0));
                var viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null) { DisableStyle(viewport.gameObject); viewportImage.color = Color.clear; viewportImage.raycastTarget = true; }
                // The inherited stencil Mask clips everything when its graphic is transparent.
                // The stash already has a RectMask2D, which does not depend on image alpha.
                var stencilMask = viewport.GetComponent<Mask>();
                if (stencilMask != null) stencilMask.enabled = false;
                var rectMask = viewport.GetComponent<RectMask2D>() ?? viewport.gameObject.AddComponent<RectMask2D>();
                rectMask.enabled = true;
                scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 24;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                scroll.verticalScrollbarSpacing = 0;
                if (scroll.verticalScrollbar != null)
                    Rect((RectTransform)scroll.verticalScrollbar.transform, new Vector2(1, 0), Vector2.one, new Vector2(-10, 0), Vector2.zero);
                Rect((RectTransform)grid.transform, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
                ((RectTransform)grid.transform).pivot = new Vector2(.5f, 1);
                var fitter = grid.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.enabled = false; // The view owns responsive content height.
                grid.padding = new RectOffset(0, 0, 0, 4);
                grid.cellSize = new Vector2(64, 64); grid.spacing = new Vector2(10, 10);
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;

                var empty = Node(viewport, "EmptyText", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                Text(empty, palette, 18); empty.text = "Skrytka jest pusta.";
                empty.alignment = TextAlignmentOptions.TopLeft;
                Rect(empty.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Wire(view, "emptyText", empty);

                var status = Ref<TextMeshProUGUI>(view, "statusText");
                Text(status, palette, 17); status.text = string.Empty;
                Element(status.transform, -1, 24, 1, 0); status.gameObject.SetActive(false);
                status.transform.SetAsLastSibling();

                var template = Ref<CampaignStorageItemView>(view, "itemTemplate");
                var background = template.GetComponent<Image>(); DisableStyle(template.gameObject);
                background.enabled = true; background.sprite = palette.GetArtwork("item_frame");
                background.overrideSprite = null; background.type = Image.Type.Sliced;
                background.color = Color.white; background.raycastTarget = true;
                background.preserveAspect = false; background.fillCenter = true;
                template.transform.Find("Border")?.gameObject.SetActive(false);
                template.transform.Find("Overlay")?.gameObject.SetActive(false);
                var button = template.GetComponent<Button>();
                button.targetGraphic = background; button.transition = Selectable.Transition.SpriteSwap;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.spriteState = new SpriteState {
                    highlightedSprite = palette.GetArtwork("slot_hover"),
                    pressedSprite = palette.GetArtwork("slot_selected"),
                    selectedSprite = background.sprite, disabledSprite = background.sprite
                };
                var icon = template.transform.Find("Icon").GetComponent<Image>();
                DisableStyle(icon.gameObject); icon.color = Color.white;
                icon.preserveAspect = true; icon.raycastTarget = false;
                Rect(icon.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-24, -24), new Vector2(24, 24));
                Wire(template, "icon", icon);
                var amount = template.transform.Find("CountLabel").GetComponent<TextMeshProUGUI>();
                Text(amount, palette, 17); amount.alignment = TextAlignmentOptions.BottomRight;
                amount.textWrappingMode = TextWrappingModes.NoWrap;
                amount.enableAutoSizing = true; amount.fontSizeMin = 10; amount.fontSizeMax = 17;
                Rect(amount.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(5, 5), new Vector2(-5, 24));
                amount.transform.SetAsLastSibling(); Wire(template, "countText", amount);
                template.gameObject.SetActive(false);
                // Reuse the complete styled slot as the drag ghost, including its Icon child.
                Wire(view, "dragGhostTemplate", template.gameObject);

                ConfigureTransfer(root, view, body, heading, scroll, template, palette);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("GODMIST_STASH_UI_CONFIGURED");
        }

        private static void ConfigureTransfer(GameObject root, CampaignStorageWindowView view,
            Transform body, RectTransform heading, ScrollRect stashScroll, CampaignStorageItemView template,
            UIWorkspaceStylePalette palette)
        {
            // Both panels use the same authored scroll/slot geometry. No runtime hierarchy construction.
            var panels = Node(body, "TransferPanels", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            Element(panels, -1, 160, 1, 1);
            var horizontal = panels.GetComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(); horizontal.spacing = 12;
            horizontal.childControlWidth = horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false; horizontal.childForceExpandHeight = true;
            var inventoryPanel = Node(panels, "InventoryPanel", typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var stashPanel = Node(panels, "StashPanel", typeof(VerticalLayoutGroup), typeof(LayoutElement));
            foreach (var panel in new[] { inventoryPanel, stashPanel })
            {
                Element(panel, 0, -1, 1, 1);
                var vertical = panel.GetComponent<VerticalLayoutGroup>();
                vertical.padding = new RectOffset(); vertical.spacing = 12;
                vertical.childControlWidth = vertical.childControlHeight = true;
                vertical.childForceExpandWidth = true; vertical.childForceExpandHeight = false;
            }
            var divider = Node(panels, "Divider", typeof(Image), typeof(LayoutElement));
            divider.SetSiblingIndex(1); Element(divider, 1, -1, 0, 1);
            divider.GetComponent<Image>().color = new Color32(111, 110, 87, 180);
            divider.GetComponent<Image>().raycastTarget = false;
            stashScroll.transform.SetParent(stashPanel, false);
            heading.SetParent(stashPanel, false); heading.SetAsFirstSibling();

            var inventoryScroll = inventoryPanel.Find("InventoryScroll")?.GetComponent<ScrollRect>();
            if (inventoryScroll == null)
            {
                inventoryScroll = UnityEngine.Object.Instantiate(stashScroll, inventoryPanel);
                inventoryScroll.name = "InventoryScroll";
                var clonedTemplate = inventoryScroll.GetComponentInChildren<CampaignStorageItemView>(true);
                if (clonedTemplate != null) UnityEngine.Object.DestroyImmediate(clonedTemplate.gameObject);
            }
            var inventoryHeading = inventoryPanel.Find("InventoryHeading") as RectTransform;
            if (inventoryHeading == null)
            {
                inventoryHeading = UnityEngine.Object.Instantiate(heading, inventoryPanel);
                inventoryHeading.name = "InventoryHeading";
            }
            inventoryHeading.SetAsFirstSibling();
            var inventoryHeader = inventoryHeading.Find("Label").GetComponent<TextMeshProUGUI>();
            inventoryHeader.text = "Plecak"; inventoryHeader.fontSize = 21;
            inventoryHeader.textWrappingMode = TextWrappingModes.NoWrap;
            inventoryHeader.overflowMode = TextOverflowModes.Ellipsis;
            Element(inventoryHeading.Find("Count"), 44, 28, 0, 0);
            Element(heading.Find("Count"), 44, 28, 0, 0);
            Wire(view, "inventoryHeaderText", inventoryHeader);
            Wire(view, "inventoryCountText", inventoryHeading.Find("Count").GetComponent<TextMeshProUGUI>());
            Wire(view, "inventoryItemsRoot", inventoryScroll.content);
            var inventoryEmpty = inventoryScroll.viewport.Find("EmptyText").GetComponent<TextMeshProUGUI>();
            inventoryEmpty.text = "Plecak jest pusty."; Wire(view, "inventoryEmptyText", inventoryEmpty);
            foreach (var pair in new[] { (stashScroll, true), (inventoryScroll, false) })
            {
                var target = pair.Item1.viewport.GetComponent<CampaignStorageDropTarget>()
                    ?? pair.Item1.viewport.gameObject.AddComponent<CampaignStorageDropTarget>();
                var serialized = new SerializedObject(target);
                serialized.FindProperty("acceptsStashItems").boolValue = pair.Item2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var portraits = Node(body, "CharacterSelector", typeof(ScrollRect), typeof(LayoutElement));
            portraits.SetAsFirstSibling(); Element(portraits, -1, 64, 1, 0);
            var viewport = Node(portraits, "Viewport", typeof(Image), typeof(RectMask2D));
            viewport.GetComponent<Image>().color = Color.clear; viewport.GetComponent<Image>().raycastTarget = true;
            Rect(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var content = Node(viewport, "Content", typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            Rect(content, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0, .5f);
            var row = content.GetComponent<HorizontalLayoutGroup>(); row.spacing = 10;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var selector = portraits.GetComponent<ScrollRect>();
            selector.content = content; selector.viewport = viewport;
            selector.horizontal = true; selector.vertical = false;
            selector.movementType = ScrollRect.MovementType.Clamped; selector.scrollSensitivity = 24;
            var portrait = Node(content, "CharacterTemplate", typeof(Image), typeof(Button), typeof(LayoutElement));
            Element(portrait, 64, 64, 0, 0);
            var frame = portrait.GetComponent<Image>(); frame.sprite = palette.GetArtwork("item_frame");
            frame.type = Image.Type.Sliced; frame.color = Color.white;
            var button = portrait.GetComponent<Button>(); button.targetGraphic = frame;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = template.GetComponent<Button>().spriteState;
            var icon = Node(portrait, "Icon", typeof(Image));
            Rect(icon, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            icon.GetComponent<Image>().preserveAspect = true; icon.GetComponent<Image>().raycastTarget = false;
            portrait.gameObject.SetActive(false);
            Wire(view, "characterButtonsRoot", content); Wire(view, "characterButtonTemplate", button);
            Wire(view, "itemActionPopupPrefab", AssetDatabase.LoadAssetAtPath<ItemActionPopupView>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_ItemActionPopup.prefab"));

            var window = root.GetComponent<UIWorkspaceWindow>();
            var windowData = new SerializedObject(window);
            windowData.FindProperty("minimumSize").vector2Value = new Vector2(580, 360);
            windowData.FindProperty("maximumSize").vector2Value = new Vector2(1600, 1000);
            windowData.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 600);
        }

        private static void DisableStyle(GameObject go)
        {
            foreach (var style in go.GetComponents<UIWorkspaceStyleApplier>())
            {
                foreach (var target in new[] { "surfaceImage", "borderImage", "text", "selectable" }) Wire(style, target, null);
                style.enabled = false;
            }
        }
        private static void Text(TMP_Text text, UIWorkspaceStylePalette palette, int size)
        {
            DisableStyle(text.gameObject); text.font = palette.Font; text.fontSize = size;
            text.enableAutoSizing = false; text.color = new Color32(223, 215, 184, 255);
            text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
        }
        private static void Element(Transform t, float w, float h, float fw, float fh)
        {
            var e = t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
            e.minWidth = e.preferredWidth = w; e.minHeight = e.preferredHeight = h;
            e.flexibleWidth = fw; e.flexibleHeight = fh;
        }
        private static RectTransform Node(Transform parent, string name, params Type[] types)
        {
            var node = parent.Find(name) as RectTransform;
            if (node != null) return node;
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            go.transform.SetParent(parent, false);
            foreach (var type in types) go.AddComponent(type);
            return (RectTransform)go.transform;
        }
        private static void Rect(RectTransform r, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
        {
            r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f, .5f);
            r.offsetMin = lo; r.offsetMax = hi;
        }
        private static T Ref<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object =>
            (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void Wire(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
