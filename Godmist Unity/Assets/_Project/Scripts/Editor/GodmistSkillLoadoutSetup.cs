using System;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Native, scoped authoring of the skills workspace variant.</summary>
public static class GodmistSkillLoadoutSetup
{
    const string Path = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_SkillLoadoutWindow Variant.prefab";
    static UIWorkspaceStylePalette palette;
    static void Wire(UnityEngine.Object owner, string field, UnityEngine.Object value)
    { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Stretch(RectTransform r, float l = 0, float b = 0, float right = 0, float top = 0)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-right, -top); }
    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    { r.anchorMin = r.anchorMax = anchor; r.pivot = pivot ?? new Vector2(.5f, .5f); r.sizeDelta = size; r.anchoredPosition = pos; }
    static void Top(RectTransform r, float y, float h, float inset = 0)
    { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.sizeDelta = new Vector2(-2 * inset, h); r.anchoredPosition = new Vector2(0, -y); }
    static RectTransform Node(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name, typeof(RectTransform)).transform; t.SetParent(parent, false); }
        t.gameObject.layer = parent.gameObject.layer;
        return (RectTransform)t;
    }
    static void Paint(Image i, string name, bool hollow = false)
    { i.sprite = palette.GetArtwork(name) ?? throw new Exception("Missing art " + name); i.overrideSprite = null; i.color = Color.white; i.type = i.sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple; i.fillCenter = !hollow; }
    static Image Art(Transform p, string name, string art, bool hollow = false)
    { var r = Node(p, name); var i = r.GetComponent<Image>() ?? r.gameObject.AddComponent<Image>(); Paint(i, art, hollow); i.raycastTarget = false; return i; }
    static void TextStyle(TMP_Text t, float size = 18, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    { t.font = palette.Font; t.fontSharedMaterial = palette.Font.material; t.fontSize = size; t.color = new Color(.91f, .875f, .78f); t.enableAutoSizing = false; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.alignment = alignment; t.raycastTarget = false; t.margin = Vector4.zero; t.fontStyle = FontStyles.Normal; }
    static TextMeshProUGUI Label(Transform p, string name, string text, float size = 18)
    { var r = Node(p, name); var t = r.GetComponent<TextMeshProUGUI>() ?? r.gameObject.AddComponent<TextMeshProUGUI>(); TextStyle(t, size); t.text = text; return t; }
    static void RemoveLayout(Transform t)
    {
        foreach (var c in t.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in t.GetComponents<ContentSizeFitter>()) UnityEngine.Object.DestroyImmediate(c);
        foreach (var c in t.GetComponents<LayoutElement>()) UnityEngine.Object.DestroyImmediate(c);
    }
    static void ButtonStyle(Button button, string caption)
    {
        Paint(button.GetComponent<Image>(), "button_normal"); button.targetGraphic = button.GetComponent<Image>(); button.transition = Selectable.Transition.ColorTint; button.spriteState = default;
        button.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(1.12f, 1.12f, 1.12f), selectedColor = new Color(1.12f, 1.12f, 1.12f), pressedColor = new Color(.75f, .75f, .75f), disabledColor = new Color(.5f, .5f, .5f), colorMultiplier = 1, fadeDuration = 0 };
        var l = Label(button.transform, "Label", caption, 20); TextStyle(l, 20, TextAlignmentOptions.Center); Stretch(l.rectTransform, 8, 2, 8, 2);
    }
    static Button NewButton(Transform p, string name, string text)
    { var i = Art(p, name, "button_normal"); i.raycastTarget = true; var b = i.GetComponent<Button>() ?? i.gameObject.AddComponent<Button>(); ButtonStyle(b, text); return b; }
    static void Card(Button button, bool library)
    {
        var root = (RectTransform)button.transform; var view = button.GetComponent<SkillLoadoutCardView>();
        Paint(button.GetComponent<Image>(), "item_frame"); button.targetGraphic = button.GetComponent<Image>(); button.transition = Selectable.Transition.None; button.spriteState = default;
        var border = root.Find("Border").GetComponent<Image>(); Paint(border, "item_frame", true); Stretch(border.rectTransform); border.raycastTarget = false;
        var icon = (Image)new SerializedObject(view).FindProperty("icon").objectReferenceValue;
        var iconMask = GodmistSkillTreeSetup.ConfigureIconMask(root, ref icon); Wire(view,"iconMask",iconMask);
        var old = root.Find("CountLabel"); if (old != null) old.gameObject.SetActive(false);
        var overlay = root.Find("Overlay").GetComponent<Image>(); overlay.enabled = false;
        var badge = Label(root, "SlotState", "", 14); TextStyle(badge, 14, TextAlignmentOptions.TopRight); Place(badge.rectTransform, Vector2.one, new Vector2(-5, -3), new Vector2(48, 18), Vector2.one);
        var fallback = Label(root, "Fallback", "+", 34); TextStyle(fallback, 34, TextAlignmentOptions.Center); Stretch(fallback.rectTransform, 5, 5, 5, 5);
        var selection = Art(root, "SelectionBorder", "slot_selected", true); Stretch(selection.rectTransform); selection.enabled = false;
        var feedback = Art(root, "FeedbackBorder", "slot_hover", true); Stretch(feedback.rectTransform); feedback.enabled = false;
        Wire(view, "button", button); Wire(view, "background", button.GetComponent<Image>()); Wire(view, "borderImage", border); Wire(view, "icon", icon); Wire(view, "fallbackGlyph", fallback); Wire(view, "slotStateText", badge); Wire(view, "selectionBorder", selection); Wire(view, "feedbackBorder", feedback); Wire(view, "sourceCanvasGroup", button.GetComponent<CanvasGroup>()); Wire(view, "lockIcon", null);
        var so = new SerializedObject(view); so.FindProperty("compactHover").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
        if (!library) { var le = root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>(); le.minWidth = le.preferredWidth = 88; le.minHeight = le.preferredHeight = 88; le.flexibleWidth = le.flexibleHeight = 0; }
    }
    static ScrollRect Scroll(Transform parent, string name, bool horizontal)
    {
        var r = Node(parent, name); var s = r.GetComponent<ScrollRect>() ?? r.gameObject.AddComponent<ScrollRect>();
        var vp = Node(r, "Viewport"); Stretch(vp); var im = vp.GetComponent<Image>() ?? vp.gameObject.AddComponent<Image>(); im.sprite = null; im.color = Color.white;
        var mask = vp.GetComponent<Mask>() ?? vp.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
        s.viewport = vp; s.horizontal = horizontal; s.vertical = !horizontal; s.movementType = ScrollRect.MovementType.Clamped; s.inertia = false; s.scrollSensitivity = 24;
        return s;
    }
    [MenuItem("Godmist/UI/Restyle skills icon masks")]
    public static void ConfigureSkillIconMasks()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            foreach(var card in root.GetComponentsInChildren<SkillLoadoutCardView>(true))
            {
                var icon=(Image)new SerializedObject(card).FindProperty("icon").objectReferenceValue;
                Wire(card,"iconMask",GodmistSkillTreeSetup.ConfigureIconMask(card.transform,ref icon));
                Wire(card,"icon",icon);
            }
            PrefabUtility.SaveAsPrefabAsset(root,Path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    static void Scrollbar(ScrollRect s, bool horizontal)
    {
        var image = Art(s.transform, horizontal ? "HorizontalScrollbar" : "VerticalScrollbar", "scrollbar_track"); image.raycastTarget = true;
        var r = image.rectTransform;
        if (horizontal) { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.sizeDelta = new Vector2(0, 8); r.anchoredPosition = Vector2.zero; }
        else { r.anchorMin = new Vector2(1, 0); r.anchorMax = Vector2.one; r.pivot = new Vector2(1, .5f); r.sizeDelta = new Vector2(8, 0); r.anchoredPosition = Vector2.zero; }
        var bar = r.GetComponent<Scrollbar>() ?? r.gameObject.AddComponent<Scrollbar>(); var area = Node(r, "SlidingArea"); Stretch(area, 2, 2, 2, 2);
        var handle = Art(area, "Handle", "scrollbar_thumb_normal"); Stretch(handle.rectTransform); handle.raycastTarget = true; bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = horizontal ? UnityEngine.UI.Scrollbar.Direction.LeftToRight : UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        if (horizontal) { s.horizontalScrollbar = bar; s.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; Stretch(s.viewport, 0, 12, 0, 0); }
        else { s.verticalScrollbar = bar; s.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; Stretch(s.viewport, 0, 0, 14, 0); }
    }
    [MenuItem("Godmist/UI/Restyle skills workspace")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            foreach (var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true)) { style.enabled = false; Wire(style, "palette", null); }
            var view = root.GetComponent<SkillLoadoutModalView>(); var window = root.GetComponent<UIWorkspaceWindow>();
            var so = new SerializedObject(window); so.FindProperty("minimumSize").vector2Value = new Vector2(960, 650); so.FindProperty("maximumSize").vector2Value = new Vector2(1400, 900); so.ApplyModifiedPropertiesWithoutUndo();
            Place((RectTransform)root.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1120, 720));
            FitWindowSurface(root); Paint(root.transform.Find("Border").GetComponent<Image>(), "tooltip_frame", true);
            var header = (RectTransform)root.transform.Find("Header"); Top(header, 0, 48); Paint(header.GetComponent<Image>(), "button_normal");
            var title = header.Find("Title").GetComponent<TextMeshProUGUI>(); TextStyle(title, 27, TextAlignmentOptions.MidlineLeft); Stretch(title.rectTransform, 20, 0, 62, 0); title.text = "Umiejętności"; title.enabled = true;
            var close = header.Find("CloseButton").GetComponent<Button>(); ButtonStyle(close, "×"); Place((RectTransform)close.transform, new Vector2(1, .5f), new Vector2(-10, 0), new Vector2(36, 36), new Vector2(1, .5f));
            var content = (RectTransform)root.transform.Find("Content"); RemoveLayout(content); Stretch(content, 18, 18, 18, 64);
            var equipped = (RectTransform)content.Find("EquippedSection"); RemoveLayout(equipped); Top(equipped, 0, 156); Paint(equipped.GetComponent<Image>(), "surface_solid"); Paint(equipped.Find("Border").GetComponent<Image>(), "tooltip_frame", true);
            var row = (RectTransform)equipped.Find("Content"); RemoveLayout(row); Stretch(row, 12, 10, 12, 10);
            var basic = (RectTransform)row.Find("BasicColumn"); RemoveLayout(basic); basic.anchorMin = Vector2.zero; basic.anchorMax = new Vector2(0, 1); basic.pivot = new Vector2(0, .5f); basic.sizeDelta = new Vector2(148, 0); basic.anchoredPosition = Vector2.zero;
            var bl = basic.Find("BasicLabel").GetComponent<TextMeshProUGUI>(); RemoveLayout(bl.transform); TextStyle(bl, 18); bl.text = "Atak podstawowy"; Top(bl.rectTransform, 0, 24);
            var bs = basic.Find("BasicSlot").GetComponent<Button>(); Card(bs, false); Place((RectTransform)bs.transform, new Vector2(.5f, 1), new Vector2(0, -27), new Vector2(88, 88), new Vector2(.5f, 1));
            var active = (RectTransform)row.Find("ActiveColumn"); RemoveLayout(active); Stretch(active, 170, 0, 0, 0);
            var al = active.Find("ActiveLabel").GetComponent<TextMeshProUGUI>(); RemoveLayout(al.transform); TextStyle(al, 18); Top(al.rectTransform, 0, 24);
            var slots = (RectTransform)(active.Find("ActiveSlotsRoot") ?? active.Find("ActiveScroll/Viewport/ActiveSlotsRoot")); RemoveLayout(slots);
            var activeScroll = Scroll(active, "ActiveScroll", true); Stretch((RectTransform)activeScroll.transform, 0, 0, 0, 27); Scrollbar(activeScroll, true);
            slots.SetParent(activeScroll.viewport, false); slots.anchorMin = slots.anchorMax = new Vector2(0, 1); slots.pivot = new Vector2(0, 1); slots.anchoredPosition = Vector2.zero; slots.sizeDelta = new Vector2(0, 88);
            var horizontal = slots.gameObject.AddComponent<HorizontalLayoutGroup>(); horizontal.spacing = 12; horizontal.childControlHeight = horizontal.childControlWidth = true; horizontal.childForceExpandHeight = horizontal.childForceExpandWidth = false; horizontal.childAlignment = TextAnchor.UpperLeft;
            var fit = slots.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; activeScroll.content = slots;
            var at = slots.Find("ActiveSlotTemplate").GetComponent<Button>(); Card(at, false); at.gameObject.SetActive(false); Wire(view, "activeSlotsScrollRect", activeScroll);
            var separator = content.Find("SectionSeparator").GetComponent<Image>(); RemoveLayout(separator.transform); Paint(separator, "separator_horizontal"); Top(separator.rectTransform, 170, 2);
            var library = Node(content, "Library"); library.anchorMin = Vector2.zero; library.anchorMax = new Vector2(0, 1); library.pivot = new Vector2(0, .5f); library.sizeDelta = new Vector2(330, -186); library.anchoredPosition = new Vector2(0, -93);
            var count = (content.Find("AvailableCountText") ?? library.Find("AvailableCountText")).GetComponent<TextMeshProUGUI>(); RemoveLayout(count.transform); count.transform.SetParent(library, false); TextStyle(count, 20); Top(count.rectTransform, 0, 30);
            var available = (content.Find("AvailableScroll") ?? library.Find("AvailableScroll")).GetComponent<ScrollRect>(); RemoveLayout(available.transform); available.transform.SetParent(library, false); Stretch((RectTransform)available.transform, 0, 30, 0, 40); available.GetComponent<Image>().sprite = null; available.GetComponent<Image>().color = Color.clear; available.horizontal = false; available.vertical = true; available.inertia = false; available.movementType = ScrollRect.MovementType.Clamped; available.scrollSensitivity = 24;
            var oldScrollbar = available.transform.Find("Scrollbar Vertical"); if (oldScrollbar != null) oldScrollbar.gameObject.SetActive(false); available.verticalScrollbar = null; Scrollbar(available, false);
            var vpImage = available.viewport.GetComponent<Image>(); vpImage.sprite = null; vpImage.color = Color.white; available.viewport.GetComponent<Mask>().showMaskGraphic = false;
            var grid = available.content.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(88, 88); grid.spacing = new Vector2(12, 12); grid.padding = new RectOffset(4, 4, 4, 4); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3; grid.childAlignment = TextAnchor.UpperLeft;
            available.content.anchorMin = new Vector2(0, 1); available.content.anchorMax = Vector2.one; available.content.pivot = new Vector2(.5f, 1); available.content.sizeDelta = Vector2.zero; available.content.anchoredPosition = Vector2.zero;
            var af = available.content.GetComponent<ContentSizeFitter>(); af.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; af.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var template = available.content.Find("AvailableSkillTemplate").GetComponent<Button>(); Card(template, true); template.gameObject.SetActive(false);
            var empty = Label(available.viewport, "Empty", "Brak poznanych umiejętności", 18); Top(empty.rectTransform, 12, 60); empty.gameObject.SetActive(false); Wire(view, "emptyAvailableState", empty);
            var details = Node(content, "SkillDetails"); Stretch(details, 354, 0, 0, 186);
            var divider = Art(content, "DetailsDivider", "separator_vertical"); divider.rectTransform.anchorMin = new Vector2(0, 0); divider.rectTransform.anchorMax = new Vector2(0, 1); divider.rectTransform.pivot = new Vector2(.5f, .5f); divider.rectTransform.sizeDelta = new Vector2(2, -186); divider.rectTransform.anchoredPosition = new Vector2(340, -93);
            var di = Art(details, "Icon", "item_frame"); di.sprite = null; di.preserveAspect = true; Place(di.rectTransform, new Vector2(0, 1), new Vector2(0, 0), new Vector2(64, 64), new Vector2(0, 1));
            var dt = Label(details, "Title", "Wybierz umiejętność", 26); Top(dt.rectTransform, 0, 36); dt.rectTransform.offsetMin = new Vector2(80, dt.rectTransform.offsetMin.y);
            var state = Label(details, "State", "", 16); Top(state.rectTransform, 40, 25); state.rectTransform.offsetMin = new Vector2(80, state.rectTransform.offsetMin.y);
            var cost = Label(details, "Cost", "", 22); Top(cost.rectTransform, 80, 64);
            var ds = Scroll(details, "BodyScroll", false); Stretch((RectTransform)ds.transform, 0, 68, 0, 152); Scrollbar(ds, false);
            var body = Label(ds.viewport, "Body", "", 22); body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one; body.rectTransform.pivot = new Vector2(.5f, 1); body.rectTransform.sizeDelta = Vector2.zero; body.rectTransform.anchoredPosition = Vector2.zero; body.lineSpacing = 4; body.paragraphSpacing = 10;
            var bf = body.GetComponent<ContentSizeFitter>() ?? body.gameObject.AddComponent<ContentSizeFitter>(); bf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; ds.content = body.rectTransform;
            var assign = NewButton(details, "Assign", "Przypisz"); Place((RectTransform)assign.transform, new Vector2(1, 0), Vector2.zero, new Vector2(250, 42), new Vector2(1, 0));
            var remove = NewButton(details, "Remove", "Usuń z zestawu"); Place((RectTransform)remove.transform, new Vector2(1, 0), Vector2.zero, new Vector2(250, 42), new Vector2(1, 0));
            Wire(view, "detailsIcon", di); Wire(view, "detailsTitle", dt); Wire(view, "detailsState", state); Wire(view, "detailsCost", cost); Wire(view, "detailsBody", body); Wire(view, "detailsScroll", ds); Wire(view, "assignButton", assign); Wire(view, "removeButton", remove); Wire(view, "assignButtonText", assign.GetComponentInChildren<TextMeshProUGUI>());
            var ghost = root.transform.Find("DragLayer/DragGhostTemplate"); Paint(ghost.GetComponent<Image>(), "item_frame"); Paint(ghost.Find("Border").GetComponent<Image>(), "item_frame", true); Stretch((RectTransform)ghost.Find("Border")); Place((RectTransform)ghost.Find("Icon"), new Vector2(.5f, .5f), Vector2.zero, new Vector2(64, 64));
            RemoveHints(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    static void RemoveHints(Transform root)
    {
        foreach (var path in new[] { "Content/Library/Hint", "Content/SkillDetails/FooterHint" })
        {
            var hint = root.Find(path);
            if (hint != null) UnityEngine.Object.DestroyImmediate(hint.gameObject);
        }
    }

    [MenuItem("Godmist/UI/Remove skills control hints")]
    public static void RemoveControlHints()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            RemoveHints(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Godmist/UI/Fit skills window surface to frame")]
    public static void FitWindowSurfaceToFrame()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>("Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            FitWindowSurface(root);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    static void FitWindowSurface(GameObject root)
    {
        // Keep the surface inside the frame rather than filling its transparent corners.
        root.GetComponent<Image>().enabled = false;
        var surface = Art(root.transform, "WindowSurface", "surface_solid");
        surface.raycastTarget = true;
        Stretch(surface.rectTransform, 5, 5, 5, 5);
        surface.transform.SetAsFirstSibling();
    }

    [MenuItem("Godmist/UI/Improve skills text readability")]
    public static void ImproveReadability()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var details = root.transform.Find("Content/SkillDetails");
            details.Find("Cost").GetComponent<TMP_Text>().fontSize = 22;
            var body = details.Find("BodyScroll/Viewport/Body").GetComponent<TMP_Text>();
            body.fontSize = 22;
            body.lineSpacing = 4;
            body.paragraphSpacing = 10;
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
}
