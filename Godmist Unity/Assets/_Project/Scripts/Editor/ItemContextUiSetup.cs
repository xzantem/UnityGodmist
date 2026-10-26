using System;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Godmist.Editor
{
    /// <summary>Scoped Editor authoring for item popups and their two owning windows.</summary>
    public static class ItemContextUiSetup
    {
        private const string Root = "Assets/_Project/Prefabs/UI/Workspace/";
        public const string PopupPath = Root + "PF_UIW_ItemActionPopup.prefab";
        [MenuItem("Godmist/Content/UI/Match Trade And Inventory Item Icons")]
        public static void MatchTradeItemIcons()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var root = PrefabUtility.LoadPrefabContents(TradeUiSetup.SlotPath);
            try
            {
                var icon = root.transform.Find("Icon").GetComponent<Image>();
                // Character inventory and equipment slots use the same 64px icon area in 72px cells.
                Place(icon.rectTransform, 0, 0, 64, 64);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(root, TradeUiSetup.SlotPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static UIWorkspaceStylePalette palette;
        private static TMP_SpriteAsset resourceSprites;
        private static readonly Color Cream = new Color32(223, 215, 184, 255);
        private static T Ref<T>(Object owner, string field) where T : Object =>
            new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;
        private static void Wire(Object owner, string field, Object value)
        { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static RectTransform Node(Transform parent, string name, params Type[] types)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
            foreach (var type in types) go.AddComponent(type); return (RectTransform)go.transform;
        }
        private static void Stretch(RectTransform rect, float l = 0, float b = 0, float r = 0, float t = 0)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(l,b); rect.offsetMax = new Vector2(-r,-t); }
        private static void Place(RectTransform rect, float x, float y, float width, float height, Vector2? anchor = null)
        { rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(.5f,.5f); rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = new Vector2(x,y); rect.sizeDelta = new Vector2(width,height); }
        private static Image Paint(RectTransform rect, string art, bool hollow = false)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = palette.GetArtwork(art) ?? throw new Exception("Missing UI art: " + art);
            image.type = image.sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white; image.fillCenter = !hollow; image.raycastTarget = !hollow; return image;
        }
        private static TextMeshProUGUI Text(Transform parent, string name, string value, int size = 20)
        {
            var text = Node(parent, name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.font = palette.Font; text.fontSharedMaterial = palette.Font.material; text.spriteAsset = resourceSprites;
            text.fontSize = size; text.color = Cream; text.text = value; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
        private static Button Button(Transform parent, string name, string label, float x, float y, float w, float h)
        {
            var rect = Node(parent,name,typeof(Image),typeof(Button)); Place(rect,x,y,w,h);
            var button = rect.GetComponent<Button>(); button.targetGraphic = Paint(rect,"button_normal");
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = palette.GetArtwork("button_hover"),
                pressedSprite = palette.GetArtwork("button_pressed"), selectedSprite = palette.GetArtwork("button_hover"),
                disabledSprite = palette.GetArtwork("button_disabled") };
            var text = Text(rect,"Label",label); Stretch(text.rectTransform,8,2,8,2); text.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private static RectTransform Modal(Transform parent, string name, float w, float h, out GameObject overlay)
        {
            var shade = Node(parent,name,typeof(Image)); Stretch(shade); shade.GetComponent<Image>().color = new Color(0,0,0,.45f);
            overlay = shade.gameObject; var panel = Node(shade,"Panel",typeof(Image)); Place(panel,0,0,w,h); Paint(panel,"surface_solid");
            var frame = Node(panel,"Frame",typeof(Image)); Stretch(frame); Paint(frame,"tooltip_frame",true);
            return panel;
        }
        [MenuItem("Godmist/Content/UI/Configure Item Context Menus")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
            resourceSprites = Ref<TMP_Text>(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"PF_UIW_CharacterInfoWindow Variant.prefab")
                .GetComponent<CharacterInfoWindowView>(),"itemDetailsMeta").spriteAsset;
            var root = new GameObject("PF_UIW_ItemActionPopup",typeof(RectTransform),typeof(ItemActionPopupView));
            root.layer = 5; root.SetActive(false); Stretch((RectTransform)root.transform);
            try
            {
                var view = root.GetComponent<ItemActionPopupView>();
                var menu = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"PF_UIW_PartyContextMenu.prefab"),root.transform);
                Stretch((RectTransform)menu.transform); menu.SetActive(false); var menuView = menu.GetComponent<PartyRailMenuView>();
                var menuPanel = Ref<RectTransform>(menuView,"panel"); menuPanel.sizeDelta = new Vector2(364,menuPanel.sizeDelta.y);
                Wire(view,"menu",menuView);
                var panel = Modal(root.transform,"QuantityOverlay",500,332,out var overlay); Wire(view,"quantityPanel",overlay);
                var title = Text(panel,"Title","Wybierz ilość",25); Place(title.rectTransform,0,130,450,36); Wire(view,"quantityTitle",title);
                var hint = Text(panel,"Hint","Przedmiot",20); Place(hint.rectTransform,0,75,450,68); Wire(view,"quantityHint",hint);
                var inputRect = Node(panel,"Quantity",typeof(Image),typeof(TMP_InputField)); Place(inputRect,-35,3,160,38); Paint(inputRect,"input_normal");
                var textArea = Node(inputRect,"TextArea",typeof(RectMask2D)); Stretch(textArea,8,2,8,2);
                var countText = Text(textArea,"Text","1",23); Stretch(countText.rectTransform); countText.alignment = TextAlignmentOptions.Center;
                var input = inputRect.GetComponent<TMP_InputField>(); input.textViewport = textArea; input.textComponent = countText;
                input.targetGraphic = inputRect.GetComponent<Image>(); input.contentType = TMP_InputField.ContentType.IntegerNumber;
                input.characterLimit = 9; input.lineType = TMP_InputField.LineType.SingleLine; input.text = "1"; Wire(view,"quantityInput",input);
                Wire(view,"minusButton",Button(panel,"Minus","−",-142,3,42,38));
                Wire(view,"plusButton",Button(panel,"Plus","+",72,3,42,38));
                Wire(view,"maxButton",Button(panel,"Max","Wszystko",162,3,128,38));
                var error = Text(panel,"Error","",17); error.color = new Color32(224,108,117,255); Place(error.rectTransform,0,-58,450,68); Wire(view,"quantityError",error);
                Wire(view,"cancelButton",Button(panel,"Cancel","Anuluj",-116,-127,218,38));
                Wire(view,"confirmButton",Button(panel,"Confirm","Potwierdź",116,-127,218,38)); overlay.SetActive(false);
                var comparison = Modal(root.transform,"ComparisonOverlay",960,600,out var comparisonOverlay); Wire(view,"comparisonPanel",comparisonOverlay);
                MakeCard(comparison,view,"candidate",-232); MakeCard(comparison,view,"equipped",232);
                Wire(view,"closeComparisonButton",Button(comparison,"Close","Zamknij",0,-264,280,40)); comparisonOverlay.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,PopupPath);
            }
            finally { Object.DestroyImmediate(root); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath).GetComponent<ItemActionPopupView>();
            Edit(Root+"PF_UIW_CharacterInfoWindow Variant.prefab", characterRoot =>
            {
                var view = characterRoot.GetComponent<CharacterInfoWindowView>(); Wire(view,"itemActionPopupPrefab",prefab);
                var template = Ref<CharacterInfoItemView>(view,"inventoryItemTemplate"); Badge(template);
                foreach (var slot in characterRoot.GetComponentsInChildren<CharacterInfoEquipmentSlotView>(true)) Badge(slot);
            });
            Edit(Root+"PF_UIW_NpcServicesWindow.prefab", npcRoot => Wire(npcRoot.GetComponentInChildren<TradeServiceWindowView>(true),"itemActionPopupPrefab",prefab));
            Edit(Root+"PF_UIW_TradeItemSlot.prefab", tradeRoot => Badge(tradeRoot.GetComponent<TradeItemSlotView>()));
            AssetDatabase.SaveAssets();
        }
        private static void Badge(Component view)
        {
            var rect = view.transform.Find("SaleProtectionBadge") as RectTransform ?? Node(view.transform,"SaleProtectionBadge",typeof(Image));
            Place(rect,11,-11,16,16,new Vector2(0,1)); var image = Paint(rect,"badge_locked"); image.raycastTarget = false;
            rect.SetAsLastSibling(); rect.gameObject.SetActive(false); Wire(view,"saleProtectionBadge",image);
        }
        private static void MakeCard(Transform parent, ItemActionPopupView view, string prefix, float x)
        {
            var card = Node(parent,prefix+"Card"); Place(card,x,14,438,518);
            var icon = Node(card,"Icon",typeof(Image)).GetComponent<Image>(); Place(icon.rectTransform,-184,218,62,62);
            icon.raycastTarget = false; icon.preserveAspect = true; Wire(view,prefix+"Icon",icon);
            var title = Text(card,"Title","Przedmiot",23); Place(title.rectTransform,30,218,348,66); Wire(view,prefix+"Title",title);
            var scrollRect = Node(card,"Details",typeof(ScrollRect)); Place(scrollRect,0,-34,428,436);
            var scroll = scrollRect.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            var viewport = Node(scrollRect,"Viewport",typeof(Image),typeof(RectMask2D)); Stretch(viewport,0,0,12,0);
            viewport.GetComponent<Image>().color = Color.clear;
            var body = Text(viewport,"Body","",20); var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0,1); bodyRect.anchorMax = Vector2.one; bodyRect.pivot = new Vector2(.5f,1);
            bodyRect.sizeDelta = Vector2.zero; bodyRect.anchoredPosition = Vector2.zero;
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var divider = Node(body.transform,"Divider",typeof(Image)).GetComponent<Image>(); Paint(divider.rectTransform,"separator_horizontal"); divider.raycastTarget = false;
            var dividerView = body.gameObject.AddComponent<ItemDetailsDividerView>(); Wire(dividerView,"detailsText",body); Wire(dividerView,"divider",divider);
            scroll.viewport = viewport; scroll.content = bodyRect; Wire(view,prefix+"Body",body); Wire(view,prefix+"Scroll",scroll);
            var scrollbar = Node(scrollRect,"Scrollbar",typeof(Image),typeof(Scrollbar));
            scrollbar.anchorMin = new Vector2(1,0); scrollbar.anchorMax = Vector2.one; scrollbar.sizeDelta = new Vector2(8,0); scrollbar.anchoredPosition = new Vector2(-4,0);
            Paint(scrollbar,"scrollbar_track"); var handle = Node(scrollbar,"Handle",typeof(Image)); Stretch(handle); var handleImage = Paint(handle,"scrollbar_thumb_normal");
            var bar = scrollbar.GetComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop; bar.handleRect = handle; bar.targetGraphic = handleImage;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
        private static void Edit(string path, Action<GameObject> action)
        { var root = PrefabUtility.LoadPrefabContents(path); try { action(root); PrefabUtility.SaveAsPrefabAsset(root,path); } finally { PrefabUtility.UnloadPrefabContents(root); } }
    }
}
