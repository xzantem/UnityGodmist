using System;
using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit, scoped authoring of the shared exploration/combat party drawer.</summary>
public static class GodmistPartyRailSetup
{
    const string Root = "Assets/_Project/Prefabs/UI/Workspace/";
    const float StatusIconSize = 24;
    // Four icons, four 3px gaps and the 20px overflow counter.
    const float StatusColumnHeight = 4 * StatusIconSize + 4 * 3 + 20;
    static readonly Color TextColor = new(.91f, .875f, .78f);
    static readonly Color Gold = new(.88f, .67f, .24f);
    static UIWorkspaceStylePalette palette;
    static Sprite Art(string name) => palette.GetArtwork(name)
        ?? throw new InvalidOperationException("Missing UI artwork: " + name);
    static T Ref<T>(UnityEngine.Object owner, string property) where T : UnityEngine.Object
        => new SerializedObject(owner).FindProperty(property).objectReferenceValue as T;
    static void Wire(UnityEngine.Object owner, string property, UnityEngine.Object value)
    {
        var state = new SerializedObject(owner);
        state.FindProperty(property).objectReferenceValue = value;
        state.ApplyModifiedPropertiesWithoutUndo();
    }
    [MenuItem("Godmist/UI/Polish party rail")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
            "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        StatusIcon();
        Edit("PF_UIW_PartyMember", Member);
        Edit("PF_UIW_PartyGroup", Group);
        Edit("PF_UIW_PartyRail", Rail);
        foreach (var name in new[] { "City/Arungard", "Sandbox/DungeonSandbox" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CharacterInfoPartyRailView>(true)).Single();
            // Preserve scene-owned gameplay references; update only drawer presentation.
            Panel(view.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }
    static void Edit(string name, Action<GameObject> configure)
    {
        var root = PrefabUtility.LoadPrefabContents(Root + name + ".prefab");
        try { configure(root); PrefabUtility.SaveAsPrefabAsset(root, Root + name + ".prefab"); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [MenuItem("Godmist/UI/Polish party group connectors")]
    public static void ConfigureRailGrouping()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
            "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        Edit("PF_UIW_PartyGroup", Group);
        Edit("PF_UIW_PartyMember", root =>
        {
            var view = root.GetComponent<CharacterInfoPartyPortraitView>();
            foreach (var field in new[] { "groupLineTop", "groupLineBottom" }) Ref<Image>(view, field).enabled = false;
        });
        Edit("PF_UIW_PartyRail", root => Panel(root.GetComponentInChildren<CharacterInfoPartyRailView>(true).gameObject));
        foreach (var name in new[] { "City/Arungard", "Sandbox/DungeonSandbox" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity");
            var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CharacterInfoPartyRailView>(true)).Single();
            Panel(view.gameObject);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Godmist/UI/Polish party context menu")]
    public static void ConfigureContextMenu()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
            "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        Edit("PF_UIW_PartyMenuAction", root =>
        {
            // OnValidate can run even on a disabled style applier during prefab import.
            foreach (var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))
            { style.enabled = false; Wire(style, "palette", null); }
            var button = root.GetComponent<Button>();
            var background = root.GetComponent<Image>();
            background.sprite = null; background.overrideSprite = null;
            background.type = Image.Type.Simple; background.color = Color.white;
            button.targetGraphic = background; button.transition = Selectable.Transition.ColorTint;
            button.spriteState = default;
            button.colors = new ColorBlock { normalColor = Color.clear,
                highlightedColor = new Color(.28f, .26f, .21f, 1), selectedColor = new Color(.28f, .26f, .21f, 1),
                pressedColor = new Color(.38f, .32f, .20f, 1), disabledColor = Color.clear,
                colorMultiplier = 1, fadeDuration = 0 };
            var label = root.GetComponentInChildren<TextMeshProUGUI>(true);
            label.font = palette.Font; label.fontSize = 20; label.color = TextColor;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(label.rectTransform); label.margin = new Vector4(10, 0, 10, 0);
            var layout = root.GetComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = 36;
            layout.preferredWidth = 276;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image != background) image.enabled = false;
        });
        Edit("PF_UIW_PartyContextMenu", root =>
        {
            foreach (var style in root.GetComponentsInChildren<UIWorkspaceStyleApplier>(true))
            { style.enabled = false; Wire(style, "palette", null); }
            var view = root.GetComponent<PartyRailMenuView>();
            var panel = Ref<RectTransform>(view, "panel");
            panel.sizeDelta = new Vector2(300, panel.sizeDelta.y);
            panel.pivot = new Vector2(0, 1);
            Paint(panel.GetComponent<Image>(), "surface_solid");
            var border = ImageAt(panel, "MenuBorder", "tooltip_frame", true); Stretch(border.rectTransform);
            (border.GetComponent<LayoutElement>() ?? border.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 2;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var title = Ref<TextMeshProUGUI>(view, "header");
            title.font = palette.Font; title.fontSize = 22; title.color = Gold;
            title.alignment = TextAlignmentOptions.MidlineLeft; title.margin = new Vector4(10, 0, 10, 0);
            title.GetComponent<LayoutElement>().preferredHeight = 32;
            var hint = Label(panel, "MenuHint"); hint.fontSize = 17;
            hint.alignment = TextAlignmentOptions.MidlineLeft; hint.textWrappingMode = TextWrappingModes.Normal;
            hint.margin = new Vector4(10, 0, 10, 0);
            var hintLayout = hint.GetComponent<LayoutElement>() ?? hint.gameObject.AddComponent<LayoutElement>();
            hintLayout.preferredHeight = 44; hint.gameObject.SetActive(false); Wire(view, "hintText", hint);
            var separator = ImageAt(panel, "SeparatorTemplate", "separator_horizontal");
            var separatorLayout = separator.GetComponent<LayoutElement>() ?? separator.gameObject.AddComponent<LayoutElement>();
            separatorLayout.minHeight = separatorLayout.preferredHeight = 10;
            separator.gameObject.SetActive(false); Wire(view, "separatorTemplate", separator.rectTransform);
        });
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Godmist/UI/Resize party status icons")]
    public static void ResizePartyStatusIcons()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        StatusIcon();
        Edit("PF_UIW_PartyMember", root =>
        {
            var statuses = Ref<CombatStatusCollectionView>(root.GetComponent<CombatPartyMemberView>(), "statusCollection");
            ((RectTransform)statuses.transform).sizeDelta = new Vector2(30, StatusColumnHeight);
        });
        AssetDatabase.SaveAssets();
    }
    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    static void Stretch(RectTransform rect, float inset = 0)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
    }
    static Image ImageAt(Transform parent, string name, string art, bool hollow = false)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name, typeof(RectTransform), typeof(Image)).transform; child.SetParent(parent, false); }
        child.gameObject.layer = parent.gameObject.layer;
        var image = child.GetComponent<Image>();
        Paint(image, art, hollow); image.raycastTarget = false;
        return image;
    }
    static void Paint(Image image, string art, bool hollow = false)
    {
        image.sprite = Art(art); image.overrideSprite = null; image.color = Color.white;
        image.type = image.sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = !hollow; image.pixelsPerUnitMultiplier = 1;
    }
    static TextMeshProUGUI Label(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).transform; child.SetParent(parent, false); }
        var label = child.GetComponent<TextMeshProUGUI>();
        label.font = palette.Font; label.fontSize = 20; label.color = TextColor;
        label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false; return label;
    }
    static void Member(GameObject root)
    {
        // All changes are overrides on the existing CombatPartyMember variant.
        var view = root.GetComponent<CharacterInfoPartyPortraitView>();
        var presenter = root.GetComponent<CombatPartyMemberView>();
        root.GetComponent<LayoutElement>().preferredWidth = 188;
        root.GetComponent<LayoutElement>().preferredHeight = 204;
        ((RectTransform)root.transform).sizeDelta = new Vector2(188, 204);
        var surface = root.GetComponent<Image>(); Paint(surface, "surface_solid");
        var button = root.GetComponent<Button>(); button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors; colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.3f, 1.25f); colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.7f, .7f, .7f); colors.disabledColor = new Color(.5f, .5f, .5f);
        colors.colorMultiplier = 1; button.colors = colors;
        var portrait = Ref<Image>(view, "portrait");
        // Portraits dominate the card; the separate status column never covers the face.
        Place(portrait.rectTransform, new Vector2(.5f, 1), new Vector2(-14, -94), new Vector2(128, 128));
        portrait.preserveAspect = true; portrait.raycastTarget = true;
        var frame = ImageAt(root.transform, "PortraitFrame", "icon_button_normal", true);
        frame.color = new Color(.72f, .78f, .78f);
        Place(frame.rectTransform, new Vector2(.5f, 1), new Vector2(-14, -94), new Vector2(134, 134));
        var selected = Ref<Image>(presenter, "selectedMarker"); Wire(view, "selectedMarker", selected); Paint(selected, "slot_selected", true);
        Place(selected.rectTransform, new Vector2(.5f, 1), new Vector2(-14, -94), new Vector2(138, 138));
        selected.raycastTarget = false; selected.transform.SetAsLastSibling();
        var turn = Ref<Image>(presenter, "turnMarker");
        Place(turn.rectTransform, new Vector2(.5f, 1), new Vector2(-14, -29), new Vector2(32, 3));
        var name = Label(root.transform, "Name"); name.fontSize = 20;
        Place(name.rectTransform, new Vector2(.5f, 1), new Vector2(-14, -12), new Vector2(144, 28));
        Wire(view, "nameText", name);
        var life = Label(root.transform, "LifeStateText"); life.fontSize = 18;
        Place(life.rectTransform, new Vector2(.5f, 1), new Vector2(-14, -146), new Vector2(128, 22));
        life.color = new Color(1, .8f, .65f); life.gameObject.SetActive(false); Wire(view, "lifeStateText", life);
        var stateMarker = Ref<Image>(presenter, "lifeStateMarker");
        Place(stateMarker.rectTransform, new Vector2(.5f, 1), new Vector2(-14, -146), new Vector2(128, 22));
        stateMarker.color = new Color(.2f, .05f, .05f, .95f); stateMarker.transform.SetSiblingIndex(life.transform.GetSiblingIndex());
        life.transform.SetSiblingIndex(stateMarker.transform.GetSiblingIndex() + 1);
        foreach (var field in new[] { "healthSlider", "resourceSlider" })
        {
            var slider = Ref<Slider>(presenter, field);
            Place((RectTransform)slider.transform, new Vector2(.5f, 1), new Vector2(0, field == "healthSlider" ? -164 : -186), new Vector2(180, field == "healthSlider" ? 20 : 18), new Vector2(.5f, 1));
            slider.interactable = false; slider.navigation = new Navigation { mode = Navigation.Mode.None };
            var background = slider.transform.Find("Background").GetComponent<Image>();
            background.sprite = null; background.color = new Color(.055f, .05f, .065f); background.raycastTarget = false;
            var fillArea = (RectTransform)slider.transform.Find("Fill Area"); Stretch(fillArea, 3);
            var fill = slider.fillRect.GetComponent<Image>(); fill.sprite = Art("fill_runtime"); fill.raycastTarget = false;
            if (field == "healthSlider") Wire(view, "healthFill", fill);
            else Wire(view, "resourceRect", slider.transform);
            var border = slider.transform.Find("GothicBarFrame").GetComponent<Image>();
            Paint(border, "slider_track", true); border.pixelsPerUnitMultiplier = 3; border.raycastTarget = false;
            var value = slider.transform.Find("Value").GetComponent<TextMeshProUGUI>();
            foreach (var style in value.GetComponents<UIWorkspaceStyleApplier>()) style.enabled = false;
            value.fontSize = 20; value.color = TextColor; value.enableAutoSizing = false;
            value.textWrappingMode = TextWrappingModes.NoWrap; value.alignment = TextAlignmentOptions.Center;
            value.raycastTarget = false; value.transform.SetAsLastSibling();
        }
        var statuses = Ref<CombatStatusCollectionView>(presenter, "statusCollection"); Wire(view, "statusCollection", statuses);
        Place((RectTransform)statuses.transform, new Vector2(.5f, 1), new Vector2(73, -94), new Vector2(30, StatusColumnHeight));
        var statusState = new SerializedObject(statuses); statusState.FindProperty("maximumVisibleIcons").intValue = 4; statusState.ApplyModifiedPropertiesWithoutUndo();
        Wire(statuses, "iconTemplate", AssetDatabase.LoadAssetAtPath<GameObject>(Root + "PF_UIW_PartyStatusIcon.prefab").GetComponent<CombatStatusIconView>());
        // Retain the inherited component identity for prefab compatibility.
        statuses.GetComponent<HorizontalLayoutGroup>().enabled = false;
        var column = statuses.transform.Find("StatusColumn") as RectTransform;
        if (column == null)
        {
            var child = new GameObject("StatusColumn", typeof(RectTransform), typeof(VerticalLayoutGroup));
            child.layer = statuses.gameObject.layer; child.transform.SetParent(statuses.transform, false);
            column = (RectTransform)child.transform;
        }
        Stretch(column); Wire(statuses, "iconRoot", column);
        var statusLayout = column.GetComponent<VerticalLayoutGroup>();
        statusLayout.childAlignment = TextAnchor.UpperCenter; statusLayout.spacing = 3;
        statusLayout.childControlHeight = statusLayout.childControlWidth = true;
        statusLayout.childForceExpandHeight = statusLayout.childForceExpandWidth = false;
        // An inherited variant child cannot be reparented. Keep it inactive and
        // provide the counter inside the authored column instead.
        statuses.transform.Find("Overflow").gameObject.SetActive(false);
        var overflow = Label(column, "Overflow"); overflow.fontSize = 18;
        Wire(statuses, "overflowText", overflow);
        var overflowLayout = overflow.GetComponent<LayoutElement>() ?? overflow.gameObject.AddComponent<LayoutElement>();
        overflowLayout.preferredWidth = 30; overflowLayout.preferredHeight = 20;
        foreach (var field in new[] { "groupLineTop", "groupLineBottom" })
        {
            var line = Ref<Image>(view, field); line.color = new Color(.75f, .61f, .34f);
            line.enabled = false;
            // Keep the connector inside the rail's frame instead of beneath its border.
            line.rectTransform.anchoredPosition = new Vector2(2, 0); line.rectTransform.sizeDelta = new Vector2(3, 2);
        }
        foreach (var field in new[] { "insertBefore", "insertAfter" }) Ref<Image>(view, field).color = Gold;
    }
    static void Group(GameObject root)
    {
        root.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = true;
        root.GetComponent<VerticalLayoutGroup>().spacing = 2;
        root.GetComponent<Image>().enabled = false;
        var view = root.GetComponent<PartyRailGroupView>();
        Image Connector(string name, string field)
        {
            var image = ImageAt(root.transform, name, "surface_solid");
            image.sprite = null; image.overrideSprite = null; image.type = Image.Type.Simple;
            image.color = new Color(.48f, .51f, .43f);
            (image.GetComponent<LayoutElement>() ?? image.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
            Wire(view, field, image); return image;
        }
        var line = Connector("GroupConnector", "connector").rectTransform;
        line.anchorMin = new Vector2(0, 0); line.anchorMax = new Vector2(0, 1);
        line.pivot = new Vector2(0, .5f); line.anchoredPosition = new Vector2(2, 8);
        line.sizeDelta = new Vector2(2, -76);
        Place(Connector("GroupConnectorTop", "connectorTop").rectTransform, new Vector2(0, 1),
            new Vector2(2, -30), new Vector2(8, 2), new Vector2(0, .5f));
        Place(Connector("GroupConnectorBottom", "connectorBottom").rectTransform, Vector2.zero,
            new Vector2(2, 46), new Vector2(8, 2), new Vector2(0, .5f));
        var separator = ImageAt(root.transform, "GroupSeparator", "separator_horizontal");
        (separator.GetComponent<LayoutElement>() ?? separator.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
        Place(separator.rectTransform, new Vector2(.5f, 1), new Vector2(0, 5), new Vector2(172, 4));
        Wire(view, "separator", separator);
        view.ConfigurePresentation(2, false);
    }
    static void StatusIcon()
    {
        var path = Root + "PF_UIW_PartyStatusIcon.prefab";
        var exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var root = exists ? PrefabUtility.LoadPrefabContents(path)
            : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/CombatHUD/PF_CombatStatusIcon_Compact.prefab"));
        try
        {
            root.name = "PF_UIW_PartyStatusIcon";
            ((RectTransform)root.transform).sizeDelta = new Vector2(StatusIconSize, StatusIconSize);
            var layout = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            layout.preferredWidth = layout.preferredHeight = StatusIconSize;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
    }
    static void Rail(GameObject root)
    {
        Panel(root.GetComponentInChildren<CharacterInfoPartyRailView>(true).gameObject);
        root.transform.Find("PartyDragGhost").GetComponent<RectTransform>().sizeDelta = new Vector2(128, 128);
        var marker = root.transform.Find("PartyNewGroupMarker").GetComponent<Image>();
        marker.rectTransform.sizeDelta = new Vector2(196, 3); marker.color = Gold;
    }
    static void Panel(GameObject root)
    {
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(208, rect.sizeDelta.y);
        var entries = Ref<RectTransform>(root.GetComponent<CharacterInfoPartyRailView>(), "portraitRoot");
        var layout = entries.GetComponent<VerticalLayoutGroup>();
        // Five separate 204px cards fit the 1080 reference height, including group gaps.
        layout.padding = new RectOffset(10, 10, 8, 12); layout.spacing = 10;
        // Inset the surface so transparent frame corners reveal the world, not a square texture.
        root.GetComponent<Image>().enabled = false;
        var surface = ImageAt(root.transform, "RailSurface", "surface_solid");
        surface.sprite = null; surface.overrideSprite = null; surface.type = Image.Type.Simple;
        surface.color = new Color(.12f, .11f, .13f); surface.raycastTarget = true;
        Stretch(surface.rectTransform, 5);
        (surface.GetComponent<LayoutElement>() ?? surface.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
        surface.transform.SetAsFirstSibling();
        var border = ImageAt(root.transform, "RailBorder", "tooltip_frame", true); Stretch(border.rectTransform);
        var le = border.GetComponent<LayoutElement>() ?? border.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        border.transform.SetAsLastSibling();
        var drawer = root.GetComponent<CombatEdgeDrawer>();
        var drawerState = new SerializedObject(drawer); drawerState.FindProperty("edgeGap").floatValue = 0;
        drawerState.ApplyModifiedPropertiesWithoutUndo();
        var handle = Ref<RectTransform>(drawer, "toggleRect"); handle.sizeDelta = new Vector2(36, 72);
        var hint = handle.GetComponent<CombatSimpleTooltipTrigger>() ?? handle.gameObject.AddComponent<CombatSimpleTooltipTrigger>();
        hint.Initialize(null, "Panel drużyny", "Kliknij, aby zwinąć lub rozwinąć panel drużyny.");
    }
}
