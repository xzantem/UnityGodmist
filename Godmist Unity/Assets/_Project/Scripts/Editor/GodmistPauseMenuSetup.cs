using System;
using System.Linq;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class GodmistMainMenuSetup
{
    public static readonly string[] PauseScenes = {
        "Assets/_Project/Scenes/City/Arungard.unity",
        "Assets/_Project/Scenes/Sandbox/DungeonSandbox.unity"
    };

    [MenuItem("Godmist/UI/Configure pause menu")]
    public static void ConfigurePauseMenu()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        palette = AssetDatabase.LoadAssetAtPath<UIWorkspaceStylePalette>(
            "Assets/_Project/ScriptableObjects/UI/Workspace/UIWorkspaceStylePalette.asset");
        var bootstrap = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var settingsSource = bootstrap.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MainMenuSettingsView>(true)).Single();
        var savesSource = bootstrap.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<SaveLoadPanelView>(true)).Single();
        ConfigureSaveAction(savesSource);
        foreach (var path in PauseScenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var pause = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PauseMenuView>(true)).Single();
            AuthorPause(pause, settingsSource, savesSource);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("GODMIST_PAUSE_MENU_CONFIGURED");
    }

    private static void AuthorPause(PauseMenuView pause, MainMenuSettingsView source, SaveLoadPanelView savesSource)
    {
        var overlay = Reference<GameObject>(pause, "overlayRoot").transform;
        var canvasRoot = overlay.GetComponentInParent<Canvas>().rootCanvas.transform;
        overlay.SetParent(canvasRoot, false);
        overlay.SetAsLastSibling(); WithoutLayout(overlay); Stretch(overlay);
        var veil = overlay.GetComponent<Image>();
        if (veil == null) veil = overlay.gameObject.AddComponent<Image>();
        veil.sprite = null; veil.color = new Color(0, 0, 0, .68f); veil.raycastTarget = true;
        var canvas = overlay.GetComponent<Canvas>();
        if (canvas == null) canvas = overlay.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true; canvas.sortingOrder = 1000;
        if (overlay.GetComponent<GraphicRaycaster>() == null) overlay.gameObject.AddComponent<GraphicRaycaster>();

        var panel = Reference<GameObject>(pause, "menuPanelRoot").transform;
        panel.SetParent(overlay, false); WithoutLayout(panel);
        Fixed(panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(448, 476));
        var surface = panel.GetComponent<Image>();
        if (surface == null) surface = panel.gameObject.AddComponent<Image>();
        Paint(surface, "surface_solid"); surface.raycastTarget = true;
        var title = Reference<GameObject>(pause, "gameTitleRoot").transform;
        title.SetParent(panel, false); WithoutLayout(title); Top(title, 20, 64, 28, 28);
        foreach (var label in title.GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = palette.Font; label.fontSize = 54; label.text = "Godmist";
            label.color = palette.GetTextColor(UIWorkspaceTextStyle.Body);
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        }
        var heading = Text(panel, "PauseHeading", "Pauza", 26);
        Top(heading.transform, 96, 36, 28, 28); heading.alignment = TextAlignmentOptions.Center;
        var fields = new[] { "resumeButton", "saveGameButton", "loadGameButton", "settingsButton", "quitGameButton" };
        var labels = new[] { "Powrót do gry", "Zapisz grę", "Wczytaj grę", "Ustawienia", "Wyjdź z gry" };
        for (var i = 0; i < fields.Length; i++)
        {
            var control = Reference<Button>(pause, fields[i]);
            control.transform.SetParent(panel, false); WithoutLayout(control.transform);
            Top(control.transform, 148 + i * 60, 48, 28, 28); Button(control, "button");
            var label = control.GetComponentInChildren<TMP_Text>(true);
            label.text = labels[i]; Stretch(label.transform, 12, 0, 12, 0);
            label.font = palette.Font; label.fontSize = 22;
            label.color = palette.GetTextColor(UIWorkspaceTextStyle.Body);
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            var navigation = control.navigation; navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = Reference<Button>(pause, fields[(i + fields.Length - 1) % fields.Length]);
            navigation.selectOnDown = Reference<Button>(pause, fields[(i + 1) % fields.Length]);
            navigation.selectOnLeft = navigation.selectOnRight = null; control.navigation = navigation;
        }
        var message = Reference<TextMeshProUGUI>(pause, "messageText");
        message.text = string.Empty; message.gameObject.SetActive(false);
        foreach (var oldHeading in panel.GetComponentsInChildren<TMP_Text>(true))
            if (oldHeading != heading && oldHeading != message && !oldHeading.transform.IsChildOf(title)
                && !fields.Any(field => oldHeading.transform.IsChildOf(Reference<Button>(pause, field).transform)))
                oldHeading.gameObject.SetActive(false);
        var frame = Image(panel, "PauseFrame", "window_frame");
        Stretch(frame.transform); frame.fillCenter = false; frame.transform.SetAsLastSibling();

        var settings = Reference<MainMenuSettingsView>(pause, "settingsPanel");
        if (settings == null)
        {
            var host = new GameObject("PauseSettingsController"); host.transform.SetParent(pause.transform, false);
            settings = host.AddComponent<MainMenuSettingsView>();
            var original = Reference<GameObject>(source, "panelRoot").transform;
            var clone = UnityEngine.Object.Instantiate(original.gameObject, overlay, false);
            clone.name = "PauseSettingsPanel";
            var settingFields = new[] { "panelRoot", "volumeSlider", "resolutionDropdown", "fullscreenToggle",
                "applyButton", "closeButton", "messageText", "volumeValueText", "confirmationRoot",
                "confirmationMessageText", "keepDisplayButton", "revertDisplayButton" };
            foreach (var field in settingFields)
            {
                var referenced = Reference<UnityEngine.Object>(source, field);
                var node = referenced is GameObject go ? go.transform : (referenced as Component)?.transform;
                if (node == null || (node != original && !node.IsChildOf(original)))
                    throw new InvalidOperationException("Settings reference outside template: " + field);
                var relative = AnimationUtility.CalculateTransformPath(node, original);
                var target = string.IsNullOrEmpty(relative) ? clone.transform : clone.transform.Find(relative);
                if (target == null) throw new InvalidOperationException("Missing cloned settings control: " + relative);
                Assign(settings, field, referenced is GameObject ? target.gameObject : target.GetComponent(referenced.GetType()));
            }
            var data = new SerializedObject(settings); var sourceData = new SerializedObject(source);
            foreach (var field in new[] { "applyDisabledTextColor", "volumeFillColor" })
                data.FindProperty(field).colorValue = sourceData.FindProperty(field).colorValue;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assign(pause, "settingsPanel", settings);
        }
        var settingsRoot = Reference<GameObject>(settings, "panelRoot").transform;
        Fixed(settingsRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(660, 540));
        if (settings.GetMissingReferences().Length > 0) throw new InvalidOperationException("Incomplete pause settings wiring.");
        settingsRoot.gameObject.SetActive(false);
        var saves = Reference<SaveLoadPanelView>(pause, "saveLoadPanel");
        var savesRoot = Reference<GameObject>(saves, "panelRoot").transform;
        if (savesRoot.name != "PauseSaveLoadPanel")
        {
            // Retain the controller identity used by pause/defeat; remap its view to the shared browser.
            savesRoot.gameObject.SetActive(false);
            var original = Reference<GameObject>(savesSource, "panelRoot").transform;
            var clone = UnityEngine.Object.Instantiate(original.gameObject, overlay, false);
            clone.name = "PauseSaveLoadPanel";
            var browserFields = new[] { "panelRoot", "closeButton", "titleText", "messageText", "saveListContent",
                "saveEntryPrefab", "emptyListText", "detailsText", "saveNameGroup", "saveNameInput", "saveButton",
                "loadButton", "deleteButton", "saveScroll", "previewCatalog", "campaignDropdown", "historyButton",
                "campaignStatusText", "previewLocationText", "previewDateText", "partyHeadingText" };
            foreach (var field in browserFields)
                Assign(saves, field, RemapBrowserReference(Reference<UnityEngine.Object>(savesSource, field), original, clone.transform));
            var data = new SerializedObject(saves); var sourceData = new SerializedObject(savesSource);
            foreach (var field in new[] { "partyPortraits", "partyCaptionTexts" })
            {
                var from = sourceData.FindProperty(field); var to = data.FindProperty(field); to.arraySize = from.arraySize;
                for (var i = 0; i < from.arraySize; i++)
                    to.GetArrayElementAtIndex(i).objectReferenceValue = RemapBrowserReference(
                        from.GetArrayElementAtIndex(i).objectReferenceValue, original, clone.transform);
            }
            data.FindProperty("keyboardNavigation").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            savesRoot = clone.transform;
        }
        ConfigureSaveAction(saves);
        var shortcutHelp = savesRoot.Find("GothicKeyboardHelp");
        if (shortcutHelp != null) shortcutHelp.gameObject.SetActive(false);
        Fixed(savesRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1040, 650));
        savesRoot.gameObject.SetActive(false);
        overlay.gameObject.SetActive(false);
    }
    private static UnityEngine.Object RemapBrowserReference(UnityEngine.Object value, Transform original, Transform clone)
    {
        if (value == null || AssetDatabase.Contains(value)) return value;
        var node = value is GameObject go ? go.transform : (value as Component)?.transform;
        if (node == null || (node != original && !node.IsChildOf(original)))
            throw new InvalidOperationException("Save browser reference outside template: " + value.name);
        var path = AnimationUtility.CalculateTransformPath(node, original);
        var target = string.IsNullOrEmpty(path) ? clone : clone.Find(path);
        if (target == null) throw new InvalidOperationException("Missing cloned browser control: " + path);
        return value is GameObject ? target.gameObject : target.GetComponent(value.GetType());
    }

    private static void ConfigureSaveAction(SaveLoadPanelView saves)
    {
        var panel = Reference<GameObject>(saves, "panelRoot").transform;
        var group = Reference<GameObject>(saves, "saveNameGroup").transform;
        group.SetParent(panel, false); WithoutLayout(group);
        var rect = (RectTransform)group; rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
        rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(280, 24); rect.sizeDelta = new Vector2(736, 44);
        var input = Reference<TMP_InputField>(saves, "saveNameInput");
        input.transform.SetParent(group, false); WithoutLayout(input.transform); Stretch(input.transform, 0, 0, 244, 0);
        var background = input.GetComponent<Image>(); Paint(background, "input_normal"); background.raycastTarget = true;
        input.targetGraphic = background; input.transition = Selectable.Transition.SpriteSwap;
        input.spriteState = new SpriteState { highlightedSprite = Art("input_focused"), pressedSprite = Art("input_focused"),
            selectedSprite = Art("input_focused"), disabledSprite = Art("input_disabled") };
        var colors = input.colors; colors.normalColor = colors.highlightedColor = colors.pressedColor = colors.selectedColor = colors.disabledColor = Color.white; input.colors = colors;
        Stretch(input.textViewport, 14, 5, 14, 5);
        foreach (var label in input.GetComponentsInChildren<TMP_Text>(true))
        {
            label.font = palette.Font; label.fontSize = 20; label.color = palette.GetTextColor(UIWorkspaceTextStyle.Body);
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontStyle = FontStyles.Normal;
        }
        if (input.placeholder is TMP_Text hint) { hint.text = "Nazwa zapisu (opcjonalnie)"; hint.color = new Color(.65f, .64f, .57f); }
        var save = Reference<Button>(saves, "saveButton"); save.transform.SetParent(group, false);
        WithoutLayout(save.transform); Fixed(save.transform, new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(220, 44));
        Button(save, "button");
        var caption = save.GetComponentInChildren<TMP_Text>(true); caption.text = "Zapisz";
        caption.font = palette.Font; caption.fontSize = 22; caption.color = palette.GetTextColor(UIWorkspaceTextStyle.Body);
        caption.alignment = TextAlignmentOptions.Center; caption.raycastTarget = false; Stretch(caption.transform, 12, 0, 12, 0);
    }

}
