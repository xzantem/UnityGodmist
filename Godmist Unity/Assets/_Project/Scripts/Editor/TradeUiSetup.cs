using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Editor
{
    /// <summary>Native, prefab-only authoring of the trade presentation and shared service rail.</summary>
    public static class TradeUiSetup
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_NpcServicesWindow.prefab";
        public const string SlotPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_TradeItemSlot.prefab";
        static UIWorkspaceStylePalette palette;
        static readonly Color Cream = new Color32(232, 222, 191, 255);
        static readonly Color Separator = new Color(.6f, .59f, .47f, .4f);
        static readonly Color LoyaltyFillTint = new Color32(140, 145, 150, 255);

        [MenuItem("Godmist/Content/UI/Improve Loyalty Counter Contrast")]
        public static void ConfigureLoyaltyContrast()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var shell = root.GetComponentInChildren<NpcServicesWindowView>(true);
                ConfigureLoyaltyContrast(Get<Slider>(Get<NpcLoyaltyHeaderView>(shell, "loyaltyHeader"), "progressSlider"));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void ConfigureLoyaltyContrast(Slider slider)
        {
            // This header has a local darker gold tint; the shared slider style
            // must not restore a white tint on enable or during prefab validation.
            var style = slider.GetComponent<UIWorkspaceSliderStyleApplier>();
            if (style != null) Wire(style, "fillImage", null);
            slider.fillRect.GetComponent<Image>().color = LoyaltyFillTint;
            var glint = slider.fillRect.GetComponentInChildren<UIWorkspaceProgressGlint>(true);
            if (glint != null) glint.GetComponent<Image>().color = LoyaltyFillTint;
        }

        [MenuItem("Godmist/Content/UI/Contain Loyalty Header Fill")]
        public static void ConfigureLoyaltyFill()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var shell = root.GetComponentInChildren<NpcServicesWindowView>(true);
                ConfigureLoyaltyFill(Get<Slider>(Get<NpcLoyaltyHeaderView>(shell, "loyaltyHeader"), "progressSlider"));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void ConfigureLoyaltyFill(Slider slider)
        {
            // Keep the gold inside the sliced frame's straight inner track.
            // Clip its moving glint too, especially while the fill is narrower than the glint.
            var area = (RectTransform)slider.fillRect.parent;
            Rect(area, Vector2.zero, Vector2.one, new Vector2(10, 3), new Vector2(-10, -3));
            var mask = area.GetComponent<RectMask2D>() ?? area.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            mask.softness = Vector2Int.zero;
            ConfigureLoyaltyContrast(slider);
        }

        [MenuItem("Godmist/Content/UI/Keep Loyalty In Service Header")]
        public static void ConfigureLoyaltyHeaderPosition()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var shell = root.GetComponentInChildren<NpcServicesWindowView>(true);
                var host = Get<Transform>(shell, "loyaltyServiceHost");
                var oldTradeHost = Get<Transform>(shell, "loyaltyTradeHost");
                var loyalty = Get<NpcLoyaltyHeaderView>(shell, "loyaltyHeader");
                loyalty.transform.SetParent(host, false);
                Rect(loyalty.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                if (oldTradeHost != host) oldTradeHost.gameObject.SetActive(false);
                Wire(shell, "loyaltyTradeHost", host);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Godmist/Content/UI/Restyle Trade Window")]
        public static void Configure()
        {
            palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Window.prefab").GetComponent<UIWorkspaceStyleApplier>().Palette;
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var shell = root.GetComponentInChildren<NpcServicesWindowView>(true);
                var trade = Get<TradeServiceWindowView>(shell, "tradeService");
                var content = Get<GameObject>(trade, "serviceRoot").transform.parent;
                Rect(content, Vector2.zero, Vector2.one, new Vector2(84, 18), new Vector2(-22, -70));
                var window = root.GetComponent<UIWorkspaceWindow>();
                var ws = new SerializedObject(window);
                ws.FindProperty("minimumSize").vector2Value = new Vector2(1020, 640);
                ws.FindProperty("maximumSize").vector2Value = new Vector2(1500, 1000);
                ws.ApplyModifiedPropertiesWithoutUndo();
                ((RectTransform)root.transform).sizeDelta = new Vector2(1160, 760);
                var title = Get<TextMeshProUGUI>(shell, "titleText");
                Typography(title, 28); title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 28;
                Rect(title.transform, Vector2.zero, Vector2.one, new Vector2(22, 6), new Vector2(-324, -6));

                var tabs = Get<Transform>(shell, "tabsContent");
                Rect(tabs, Vector2.zero, new Vector2(0, 1), new Vector2(16, 18), new Vector2(72, -82));
                var group = tabs.GetComponent<VerticalLayoutGroup>();
                group.spacing = 8; group.childAlignment = TextAnchor.UpperCenter;
                group.childControlWidth = group.childControlHeight = true;
                group.childForceExpandWidth = group.childForceExpandHeight = false;
                var tab = Get<NpcServiceTabView>(shell, "tabTemplate");
                var tabButton = Get<Button>(tab, "button"); Art(tabButton, "icon_button");
                var tabLayout = tab.GetComponent<LayoutElement>() ?? tab.gameObject.AddComponent<LayoutElement>();
                tabLayout.minWidth = tabLayout.preferredWidth = tabLayout.minHeight = tabLayout.preferredHeight = 56;
                tabLayout.flexibleWidth = tabLayout.flexibleHeight = 0;
                var icon = Get<Image>(tab, "icon"); Rect(icon.transform, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10)); icon.preserveAspect = true;
                var selected = Get<Image>(tab, "selectedBorder");
                selected.sprite = palette.GetArtwork("slot_selected"); selected.type = Image.Type.Sliced; selected.color = Color.white; selected.raycastTarget = false;
                Wire(tab, "palette", palette);
                var rail = Node(root.transform, "ServiceRailDivider", typeof(Image));
                Rect(rail, Vector2.zero, new Vector2(0, 1), new Vector2(79, 22), new Vector2(80, -76)); Line(rail);

                var serviceRoot = Get<GameObject>(trade, "serviceRoot");
                Rect(serviceRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var tradeGroup = serviceRoot.GetComponent<VerticalLayoutGroup>(); tradeGroup.spacing = 12;
                var message = Get<TextMeshProUGUI>(trade, "messageText"); Typography(message, 18); message.text = string.Empty;
                var messageLayout = message.GetComponent<LayoutElement>() ?? message.gameObject.AddComponent<LayoutElement>();
                messageLayout.minHeight = messageLayout.preferredHeight = 28; messageLayout.flexibleHeight = 0;
                Get<TextMeshProUGUI>(trade, "goldText").gameObject.SetActive(false);

                var player = Get<RectTransform>(trade, "playerDropArea");
                var trader = Get<RectTransform>(trade, "traderDropArea");
                var columns = player.parent.GetComponent<HorizontalLayoutGroup>(); columns.spacing = 24;
                foreach (var panel in new[] { player, trader })
                {
                    DisableStyles(panel.gameObject, false);
                    panel.GetComponent<Image>().color = new Color32(36, 34, 39, 255);
                    var layout = panel.GetComponent<LayoutElement>(); layout.minWidth = 0; layout.preferredWidth = 0; layout.flexibleWidth = 1;
                    var panelGroup = panel.GetComponent<VerticalLayoutGroup>(); panelGroup.padding = new RectOffset(10, 10, 8, 10); panelGroup.spacing = 12;
                    var scroll = panel.GetComponentInChildren<ScrollRect>(true);
                    foreach (var background in scroll.GetComponents<Image>()) background.enabled = false;
                    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                    var viewportImage = scroll.viewport.GetComponent<Image>();
                    if (viewportImage != null) { DisableStyles(viewportImage.gameObject, false); viewportImage.color = new Color32(31, 29, 34, 255); }
                }
                foreach (var field in new[] { "playerHeaderText", "traderHeaderText" })
                {
                    var header = Get<TextMeshProUGUI>(trade, field); Typography(header, 23);
                    header.alignment = TextAlignmentOptions.TopLeft; header.margin = new Vector4(0, 2, 140, 40);
                    var layout = header.GetComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = 82; layout.flexibleHeight = 0;
                    var gold = header.transform.Find("GoldDisplay");
                    Rect(gold, new Vector2(1, 1), Vector2.one, new Vector2(-132, -32), Vector2.zero);
                    var image = gold.Find("GoldIcon").GetComponent<Image>();
                    image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png"); image.color = Color.white; image.preserveAspect = true;
                    Rect(image.transform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, -16), new Vector2(32, 16));
                    var amount = gold.Find("GoldAmount").GetComponent<TextMeshProUGUI>(); Typography(amount, 23); amount.alignment = TextAlignmentOptions.MidlineRight;
                    Rect(amount.transform, Vector2.zero, Vector2.one, new Vector2(38, 0), Vector2.zero);
                    var divider = Node(header.transform, "HeaderDivider", typeof(Image));
                    Rect(divider, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1)); Line(divider);
                }
                var headerMerchant = Get<TextMeshProUGUI>(trade, "traderHeaderText");
                var tradeHost = Node(headerMerchant.transform, "TradeLoyaltyHost");
                Rect(tradeHost, Vector2.zero, new Vector2(1, 0), new Vector2(0, 12), new Vector2(0, 42));
                tradeHost.gameObject.SetActive(false);
                var otherHost = Node(root.transform.Find("Header"), "ServiceLoyaltyHost");
                Rect(otherHost, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-308, -16), new Vector2(-62, 16));
                var loyalty = Get<NpcLoyaltyHeaderView>(shell, "loyaltyHeader");
                loyalty.transform.SetParent(otherHost, false); Rect(loyalty.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var oldText = loyalty.GetComponent<TextMeshProUGUI>(); if (oldText != null) { oldText.text = string.Empty; oldText.raycastTarget = true; }
                var label = Get<TextMeshProUGUI>(loyalty, "titleText"); Typography(label, 17); label.text = "Lojalność"; label.alignment = TextAlignmentOptions.MidlineLeft;
                Rect(label.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(92, 0));
                var row = Get<RectTransform>(loyalty, "progressRow"); Rect(row, Vector2.zero, Vector2.one, new Vector2(96, 0), Vector2.zero);
                var level = Get<TextMeshProUGUI>(loyalty, "levelText"); Typography(level, 22); level.alignment = TextAlignmentOptions.Center;
                Rect(level.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(28, 0));
                var slider = Get<Slider>(loyalty, "progressSlider"); Rect(slider.transform, Vector2.zero, Vector2.one, new Vector2(36, 3), new Vector2(0, -3));
                ConfigureLoyaltyFill(slider);
                var value = Get<TextMeshProUGUI>(loyalty, "progressText"); Typography(value, 15); value.alignment = TextAlignmentOptions.Center;
                Wire(shell, "loyaltyTradeHost", otherHost); Wire(shell, "loyaltyServiceHost", otherHost);
                Wire(trade, "goldIconSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/ResourceRewards/gold-32.png"));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var slot = PrefabUtility.LoadPrefabContents(SlotPath);
            try
            {
                // Trade clicks transact immediately; keyboard focus must not imply a persistent item selection.
                var button = slot.GetComponent<Button>(); var state = button.spriteState;
                state.selectedSprite = button.GetComponent<Image>().sprite; button.spriteState = state;
                PrefabUtility.SaveAsPrefabAsset(slot, SlotPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(slot); }
            AssetDatabase.SaveAssets();
        }
        static T Get<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        static void Wire(Object owner, string field, Object value) { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        static void DisableStyles(GameObject go, bool children) { foreach (var a in children ? go.GetComponentsInChildren<UIWorkspaceStyleApplier>(true) : go.GetComponents<UIWorkspaceStyleApplier>()) a.enabled = false; }
        static void Typography(TextMeshProUGUI t, int size) { DisableStyles(t.gameObject, false); t.font = palette.Font; t.fontSize = size; t.enableAutoSizing = false; t.color = Cream; }
        static void Line(RectTransform t) { var image = t.GetComponent<Image>(); image.color = Separator; image.raycastTarget = false; }
        static void Rect(Transform t, Vector2 min, Vector2 max, Vector2 low, Vector2 high) { var r = (RectTransform)t; r.anchorMin = min; r.anchorMax = max; r.offsetMin = low; r.offsetMax = high; }
        static RectTransform Node(Transform parent, string name, params System.Type[] components)
        {
            var existing = parent.Find(name) as RectTransform; if (existing != null) return existing;
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
            foreach (var type in components) go.AddComponent(type); return (RectTransform)go.transform;
        }
        static void Art(Button b, string prefix)
        {
            DisableStyles(b.gameObject, false); var image = b.GetComponent<Image>(); image.sprite = palette.GetArtwork(prefix + "_normal"); image.type = Image.Type.Sliced; image.color = Color.white;
            b.targetGraphic = image; b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { highlightedSprite = palette.GetArtwork(prefix + "_hover"), pressedSprite = palette.GetArtwork(prefix + "_pressed"), selectedSprite = image.sprite, disabledSprite = palette.GetArtwork(prefix + "_disabled") };
        }
    }
}
