using System;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class GodmistMainMenuSetup
{
    public const string DungeonSelectorPath = "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_DungeonSelector.prefab";

    [MenuItem("Godmist/UI/Configure expedition selector")]
    public static void ConfigureDungeonSelector()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
            "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var root = PrefabUtility.LoadPrefabContents(DungeonSelectorPath);
        try
        {
            var view = root.GetComponent<DungeonSelectorWindowView>();
            var window = Reference<UIWorkspaceWindow>(view, "window");
            var config = new SerializedObject(window);
            config.FindProperty("minimumSize").vector2Value = new Vector2(1100, 700);
            config.FindProperty("maximumSize").vector2Value = new Vector2(1100, 700);
            config.ApplyModifiedPropertiesWithoutUndo();
            window.ConfigureResizeAxes(false, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(1100, 700);
            var handle = root.transform.Find("ResizeHandle");
            if (handle != null) handle.gameObject.SetActive(false);
            var content = root.transform.Find("Content");
            Stretch(content, 20, 20, 20, 64); WithoutLayout(content);
            var title = root.transform.Find("Header/Title").GetComponent<TMP_Text>();
            Stretch(title.transform, 32, 4, 56, 4);
            ExpeditionText(title, 26); title.text = "Wybierz wyprawę";
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var map = root.GetComponentInChildren<RawImage>(true).transform;
            map.SetParent(content, false);
            ExpeditionArea(map, 0, .70f, 0, 400, 0, 20);
            var markers = new[] { new Vector2(.18f,.74f), new Vector2(.82f,.68f), new Vector2(.48f,.85f),
                new Vector2(.22f,.37f), new Vector2(.55f,.42f), new Vector2(.83f,.20f) };
            var data = new SerializedObject(view);
            for (var i = 0; i < 6; i++)
            {
                var control = (Button)data.FindProperty("offerButtons").GetArrayElementAtIndex(i).objectReferenceValue;
                Fixed(control.transform, markers[i], new Vector2(.5f,.5f), Vector2.zero, new Vector2(200, 68));
                Button(control, "button");
                var label = (TMP_Text)data.FindProperty("offerLabels").GetArrayElementAtIndex(i).objectReferenceValue;
                ConfigureOfferCaption(control, label);
                var border = (Image)data.FindProperty("offerBorders").GetArrayElementAtIndex(i).objectReferenceValue;
                Paint(border, "item_frame"); Stretch(border.transform, -3, -3, -3, -3);
                border.fillCenter = false; border.raycastTarget = false; border.transform.SetAsLastSibling();
            }
            var details = Reference<TMP_Text>(view, "details");
            var detailPanel = details.transform.parent; WithoutLayout(detailPanel);
            ExpeditionArea(detailPanel, .72f, 1, 0, 408, 0, 0);
            Paint(detailPanel.GetComponent<Image>(), "surface_solid");
            Top(details.transform, 82, 220, 0, 0); ExpeditionText(details, 20); details.lineSpacing = 5;
            var heading = Text(detailPanel, "ExpeditionHeading", "", 26); Top(heading.transform, 16, 60);
            heading.overflowMode = TextOverflowModes.Ellipsis;
            Assign(view, "selectionHeading", heading);
            var state = Text(detailPanel, "ExpeditionState", "", 20); Top(state.transform, 320, 66);
            Assign(view, "selectionState", state);
            var divider = Image(content, "ExpeditionDivider", "separator_vertical");
            // Match the continuous divider used by the save browser instead of
            // stretching decorative end caps into a nearly invisible line.
            divider.sprite = null; divider.overrideSprite = null; divider.type = UnityEngine.UI.Image.Type.Simple;
            divider.color = new Color(.28f, .31f, .28f, .65f);
            ExpeditionArea(divider.transform, .71f, .71f, 0, 616, 0, 0);
            ((RectTransform)divider.transform).sizeDelta = new Vector2(2, 616);
            var summary = Reference<TMP_Text>(view, "partySummary");
            WithoutLayout(summary.transform.parent);
            ExpeditionArea(summary.transform.parent, 0, .70f, 406, 40, 0, 20);
            Stretch(summary.transform); ExpeditionText(summary, 22);
            var party = Reference<Transform>(view, "partyRoot");
            ExpeditionArea(party, 0, .70f, 448, 168, 0, 20);
            var layout = party.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16; layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var template = Reference<CombatPartyMemberView>(view, "partyTemplate");
            if (AssetDatabase.Contains(template))
            {
                template = UnityEngine.Object.Instantiate(template, content, false);
                template.name = "ExpeditionPartyTemplate";
                Assign(view, "partyTemplate", template);
            }
            template.gameObject.SetActive(false); WithoutLayout(template.transform);
            Fixed(template.transform, new Vector2(0,1), new Vector2(0,1), Vector2.zero, new Vector2(100,168));
            var element = template.GetComponent<LayoutElement>();
            if (element == null) element = template.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = 100; element.minHeight = element.preferredHeight = 168;
            element.flexibleWidth = element.flexibleHeight = 0;
            var background = template.GetComponent<Image>(); background.sprite = null; background.color = Color.clear;
            var portrait = Reference<Image>(template, "portrait");
            Fixed(portrait.transform, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-40), new Vector2(80,80));
            portrait.preserveAspect = true; portrait.raycastTarget = false;
            var rim = Image(template.transform, "ExpeditionPortraitFrame", "icon_button_normal");
            Fixed(rim.transform, new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-40), new Vector2(80,80));
            rim.fillCenter = false; rim.transform.SetAsLastSibling();
            var name = Text(template.transform, "ExpeditionName", "", 18); Top(name.transform, 0, 38);
            name.alignment = TextAlignmentOptions.Center; name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis; Assign(template, "characterNameText", name);
            var health = Reference<Slider>(template, "healthSlider");
            var resource = Reference<Slider>(template, "resourceSlider");
            Top(health.transform, 124, 20); Top(resource.transform, 148, 20);
            var rail = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyMember.prefab").GetComponent<CombatPartyMemberView>();
            ExpeditionBar(health, Reference<Slider>(rail, "healthSlider"));
            ExpeditionBar(resource, Reference<Slider>(rail, "resourceSlider"));
            foreach (var field in new[] { "healthValueText", "resourceValueText" })
            {
                var text = Reference<TMP_Text>(template, field);
                Stretch(text.transform); ExpeditionText(text, 14); text.alignment = TextAlignmentOptions.Center;
                text.transform.SetAsLastSibling();
            }
            foreach (var field in new[] { "selectedMarker", "turnMarker", "lifeStateMarker" })
            {
                var marker = Reference<Image>(template, field);
                if (marker != null) { Top(marker.transform, 38, 84, 8, 8); marker.raycastTarget = false; }
                if (marker != null && field != "lifeStateMarker")
                {
                    Paint(marker, field == "selectedMarker" ? "slot_selected" : "slot_hover");
                    marker.fillCenter = false;
                }
            }
            var statuses = Reference<CombatStatusCollectionView>(template, "statusCollection");
            if (statuses != null) statuses.gameObject.SetActive(false);
            foreach (var field in new[] { "depart", "abandon" })
            {
                var control = Reference<Button>(view, field); control.transform.SetParent(content, false);
                ExpeditionArea(control.transform, .72f, 1, field == "depart" ? 500 : 556, 44, 0, 0);
                Button(control, "button");
                var caption = Reference<TMP_Text>(view, field == "depart" ? "departLabel" : "abandonLabel");
                Stretch(caption.transform, 12, 0, 12, 0); ExpeditionText(caption, 20); caption.alignment = TextAlignmentOptions.Center;
            }
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.SaveAsPrefabAsset(root, DungeonSelectorPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("GODMIST_EXPEDITION_SELECTOR_CONFIGURED");
    }

    [MenuItem("Godmist/UI/Update expedition offer captions")]
    public static void ConfigureDungeonOfferCaptions()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
            "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var root = PrefabUtility.LoadPrefabContents(DungeonSelectorPath);
        try
        {
            var data = new SerializedObject(root.GetComponent<DungeonSelectorWindowView>());
            for (var i = 0; i < data.FindProperty("offerButtons").arraySize; i++)
                ConfigureOfferCaption(
                    (Button)data.FindProperty("offerButtons").GetArrayElementAtIndex(i).objectReferenceValue,
                    (TMP_Text)data.FindProperty("offerLabels").GetArrayElementAtIndex(i).objectReferenceValue);
            PrefabUtility.SaveAsPrefabAsset(root, DungeonSelectorPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureOfferCaption(Button control, TMP_Text label)
    {
        ((RectTransform)control.transform).sizeDelta = new Vector2(200, 68);
        Stretch(label.transform, 8, 5, 8, 5); ExpeditionText(label, 18);
        label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 18;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void ExpeditionBar(Slider slider, Slider rail)
    {
        slider.interactable = false; slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.transition = Selectable.Transition.None;
        var background = slider.transform.Find("Background").GetComponent<Image>();
        var sourceBackground = rail.transform.Find("Background").GetComponent<Image>();
        ExpeditionBarImage(background, sourceBackground); Stretch(background.transform);
        var fillArea = (RectTransform)slider.transform.Find("Fill Area");
        var sourceArea = (RectTransform)rail.transform.Find("Fill Area");
        fillArea.anchorMin = sourceArea.anchorMin; fillArea.anchorMax = sourceArea.anchorMax;
        fillArea.pivot = sourceArea.pivot; fillArea.offsetMin = sourceArea.offsetMin; fillArea.offsetMax = sourceArea.offsetMax;
        // UGUI owns the fill anchors as values change. Its offsets must stay zero
        // so neither the full nor partial fill can escape the rail's inner track.
        Stretch(slider.fillRect);
        ExpeditionBarImage(slider.fillRect.GetComponent<Image>(), rail.fillRect.GetComponent<Image>());
        var frame = slider.transform.Find("GothicBarFrame").GetComponent<Image>();
        ExpeditionBarImage(frame, rail.transform.Find("GothicBarFrame").GetComponent<Image>());
        Stretch(frame.transform); frame.transform.SetAsLastSibling();
        foreach (var style in slider.GetComponentsInChildren<UIWorkspaceSliderStyleApplier>(true))
        {
            Assign(style, "backgroundImage", null); Assign(style, "fillImage", null); style.enabled = false;
        }
    }

    private static void ExpeditionBarImage(Image target, Image source)
    {
        target.sprite = source.sprite; target.overrideSprite = null; target.color = source.color;
        target.type = source.type; target.fillCenter = source.fillCenter;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.preserveAspect = source.preserveAspect; target.enabled = source.enabled; target.raycastTarget = false;
    }

    private static void ExpeditionText(TMP_Text text, float size)
    {
        foreach (var style in text.GetComponents<UIWorkspaceStyleApplier>())
        {
            Assign(style, "text", null);
            style.enabled = false;
        }
        text.font = palette.Font; text.fontSize = size; text.color = palette.GetTextColor(UIWorkspaceTextStyle.Body);
        text.enableAutoSizing = false; text.fontStyle = FontStyles.Normal; text.raycastTarget = false;
        text.margin = Vector4.zero;
    }

    private static void ExpeditionArea(Transform node, float minX, float maxX, float top, float height, float left, float right)
    {
        var rect = (RectTransform)node; rect.anchorMin = new Vector2(minX,1); rect.anchorMax = new Vector2(maxX,1);
        rect.pivot = new Vector2(.5f,1); rect.anchoredPosition = new Vector2((left-right)*.5f,-top);
        rect.sizeDelta = new Vector2(-left-right,height);
    }
}
