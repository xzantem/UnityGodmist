using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Godmist.Editor
{
    /// <summary>Explicit Editor authoring of the controller bindings and existing UI hierarchy.</summary>
    [InitializeOnLoad]
    public static class ControllerSupportSetup
    {
        public const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        public const string KeyboardCharacters = "1234567890qwertyuiopasdfghjklzxcvbnmąćęłńóśźż-_.";
        private static string Output => Path.GetFullPath("../LocalHandoffs/controller-support-20261031");
        private static bool running;
        static ControllerSupportSetup() => EditorApplication.update += Tick;

        private static void Tick()
        {
            var request = Path.Combine(Output, "setup.request");
            if (running || !File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            running = true;
            try { Apply(); File.WriteAllText(Path.Combine(Output, "setup-state.txt"), "PASS"); }
            catch (Exception error) { File.WriteAllText(Path.Combine(Output, "setup-state.txt"), "FAILED: " + error); Debug.LogException(error); }
            finally { running = false; }
        }

        public static void ConfigureActions(InputActionAsset asset)
        {
            // Migrate only our unshipped draft schema; existing Player/UI actions and their IDs remain intact.
            var draft = asset.FindActionMap(ControllerInput.WorldMap, false);
            if (draft?.FindAction("Confirm", false) != null)
            {
                asset.RemoveActionMap(draft);
                var draftUi = asset.FindActionMap(ControllerInput.UiMap, false);
                if (draftUi != null) asset.RemoveActionMap(draftUi);
            }
            var world = asset.FindActionMap(ControllerInput.WorldMap, false) ?? asset.AddActionMap(ControllerInput.WorldMap);
            var ui = asset.FindActionMap(ControllerInput.UiMap, false) ?? asset.AddActionMap(ControllerInput.UiMap);
            var global = asset.FindActionMap(ControllerInput.GlobalMap, false) ?? asset.AddActionMap(ControllerInput.GlobalMap);
            Add(world, "Move", "<Gamepad>/leftStick", InputActionType.Value, "Vector2");
            Add(world, "PointerMove", "<Gamepad>/leftStick", InputActionType.Value, "Vector2");
            Add(world, "DirectInteraction", "<Gamepad>/buttonSouth");
            Add(world, "Camera", "<Gamepad>/rightStick", InputActionType.Value, "Vector2");
            var zoom = world.FindAction("Zoom", false) ?? world.AddAction("Zoom", InputActionType.Value, expectedControlLayout: "Axis");
            if (zoom.bindings.Any(b => b.path == "<Gamepad>/dpad/right"))
                while (zoom.bindings.Count > 0) zoom.ChangeBinding(0).Erase();
            if (zoom.bindings.Count == 0) zoom.AddCompositeBinding("1DAxis").With("Positive", "<Gamepad>/dpad/up").With("Negative", "<Gamepad>/dpad/down");
            Add(ui, "Scroll", "<Gamepad>/rightStick", InputActionType.Value, "Vector2");
            Add(ui, "PointerMove", "<Gamepad>/leftStick", InputActionType.Value, "Vector2");
            Add(ui, "Navigate", "<Gamepad>/leftStick", InputActionType.Value, "Vector2");
            Add(ui, "Navigate", "<Gamepad>/dpad", InputActionType.Value, "Vector2");
            Add(global, "Confirm", "<Gamepad>/buttonSouth");
            Add(global, "Cancel", "<Gamepad>/buttonEast");
            Add(global, "Context", "<Gamepad>/buttonWest");
            Add(global, "Auxiliary", "<Gamepad>/buttonNorth");
            Add(global, "LeftTrigger", "<Gamepad>/leftTrigger");
            Add(global, "RightTrigger", "<Gamepad>/rightTrigger");
            Add(global, "Next", "<Gamepad>/rightShoulder");
            Add(global, "Previous", "<Gamepad>/leftShoulder");
            BindOnly(global, "Skills", "<Gamepad>/rightShoulder");
            BindOnly(global, "Map", "<Gamepad>/select");
            BindOnly(global, "Center", "<Gamepad>/rightStickPress");
            Add(global, "Cursor", "<Gamepad>/leftStickPress");
            Add(global, "NextTarget", "<Gamepad>/dpad/right");
            Add(global, "PreviousTarget", "<Gamepad>/dpad/left");
            Add(global, "Pause", "<Gamepad>/start");
            BindOnly(global, "Panels", "<Gamepad>/rightTrigger");
            // Native UI Submit must not also receive gamepad A, which the context owner routes once.
            var native = asset.FindActionMap("UI", true);
            Add(native, "KeyboardSubmit", "<Keyboard>/enter");
            Add(native, "KeyboardSubmit", "<Keyboard>/numpadEnter");
            var navigation = native.FindAction("KeyboardNavigate", false)
                ?? native.AddAction("KeyboardNavigate", InputActionType.Value, expectedControlLayout: "Vector2");
            if (navigation.bindings.Count == 0)
            {
                navigation.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
                navigation.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            }
        }

        private static void BindOnly(InputActionMap map, string name, string path)
        {
            var action = map.FindAction(name, false) ?? map.AddAction(name, InputActionType.Button);
            if (action.bindings.Count == 1 && action.bindings[0].path == path) return;
            while (action.bindings.Count > 0) action.ChangeBinding(0).Erase();
            action.AddBinding(path);
        }

        private static void Add(InputActionMap map, string name, string path, InputActionType type = InputActionType.Button, string expected = "Button")
        {
            var action = map.FindAction(name, false) ?? map.AddAction(name, type, expectedControlLayout: expected);
            if (!action.bindings.Any(b => b.path == path)) action.AddBinding(path);
            if (path.EndsWith("Stick", StringComparison.OrdinalIgnoreCase))
            {
                for (var index = 0; index < action.bindings.Count; index++)
                    if (action.bindings[index].path == path)
                    {
                        var binding = action.bindings[index];
                        binding.processors = "stickDeadzone(min=0.25,max=0.95)";
                        action.ChangeBinding(index).To(binding);
                    }
            }
        }

        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            if (Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Unsaved scenes: controller authoring will not overwrite them.");
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (source == null) throw new InvalidOperationException("Existing project actions are missing.");
            var copy = InputActionAsset.FromJson(source.ToJson());
            try
            {
                ConfigureActions(copy);
                // Serialize through the same InputActionAsset API used by Unity's action editor.
                File.WriteAllText(ActionsPath, copy.ToJson());
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            AssetDatabase.ImportAsset(ActionsPath, ImportAssetOptions.ForceUpdate);
            source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (InputSystem.actions != source) InputSystem.actions = source;

            var changedPrefabs = new List<string>();
            foreach (var path in OrderedUiPrefabs())
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureNavigation(root.transform);
                    ConfigureScopes(root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changedPrefabs.Add(path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var original = EditorSceneManager.GetSceneManagerSetup();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            try
            {
                foreach (var path in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var roots = scene.GetRootGameObjects();
                    var eventSystem = roots.SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).Single();
                    var module = eventSystem.GetComponent<InputSystemUIInputModule>();
                    if (module == null) throw new InvalidOperationException("Missing InputSystem UI module: " + path);
                    var canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
                        .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                        .OrderByDescending(c => c.GetComponentsInChildren<Selectable>(true).Length).First();
                    var owner = roots.FirstOrDefault(r => r.name == "GodmistControllerInput") ?? new GameObject("GodmistControllerInput");
                    var view = owner.GetComponent<ControllerInputView>() ?? owner.AddComponent<ControllerInputView>();
                    foreach (var uiCanvas in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
                        .Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray())
                        uiCanvas.transform.SetParent(owner.transform, true);
                    var all = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
                    var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"));
                    Set(view, "actions", source); Set(view, "events", eventSystem); Set(view, "module", module);
                    Set(view, "worldCamera", camera); Set(view, "party", all.OfType<ExplorationPartyController>().FirstOrDefault());
                    Set(view, "combat", all.OfType<CombatSandboxBootstrap>().FirstOrDefault());
                    Set(view, "targeting", all.OfType<CombatTargetSelectionController>().FirstOrDefault());
                    Set(view, "unitViews", all.OfType<CombatUnitViewSpawner>().FirstOrDefault());
                    Set(view, "movement", all.OfType<CombatMovementInputController>().FirstOrDefault());
                    Set(view, "minimap", all.OfType<Godmist.Views.Dungeons.DungeonMinimapView>().FirstOrDefault());
                    Set(view, "skillBar", all.OfType<CombatSkillBarView>().FirstOrDefault());
                    Set(view, "pause", all.OfType<PauseMenuView>().FirstOrDefault());
                    Set(view, "partyRail", all.OfType<CharacterInfoPartyRailView>().FirstOrDefault());
                    Set(view, "journal", all.OfType<QuestJournalPanelView>().FirstOrDefault());
                    Set(view, "combatActions", all.OfType<CombatActionButtonsView>().FirstOrDefault());
                    foreach (var buttons in all.OfType<CombatActionButtonsView>())
                        Set(buttons, "targetSelectionController", all.OfType<CombatTargetSelectionController>().FirstOrDefault());
                    var selector = owner.GetComponentsInChildren<PartyRailMenuView>(true).FirstOrDefault(value => value.name == "ControllerSelectors");
                    if (selector == null)
                    {
                        var template = AssetDatabase.LoadAssetAtPath<PartyRailMenuView>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_PartyContextMenu.prefab");
                        if (template == null) throw new InvalidOperationException("Missing existing party menu prefab.");
                        selector = ((GameObject)PrefabUtility.InstantiatePrefab(template.gameObject, canvas.transform)).GetComponent<PartyRailMenuView>();
                        selector.name = "ControllerSelectors";
                    }
                    selector.gameObject.SetActive(false);
                    Set(view, "selectorMenu", selector);
                    // Use one authored action asset for both the controller and UGUI. Leaving
                    // the module on its package defaults creates a second, independent UI map.
                    module.actionsAsset = source;
                    module.point = UiReference(source, "Point");
                    module.move = UiReference(source, "KeyboardNavigate");
                    module.submit = UiReference(source, "KeyboardSubmit");
                    module.cancel = UiReference(source, "Cancel");
                    module.leftClick = UiReference(source, "Click");
                    module.middleClick = UiReference(source, "MiddleClick");
                    module.rightClick = UiReference(source, "RightClick");
                    module.scrollWheel = UiReference(source, "ScrollWheel");
                    module.trackedDevicePosition = UiReference(source, "TrackedDevicePosition");
                    module.trackedDeviceOrientation = UiReference(source, "TrackedDeviceOrientation");
                    module.pointerBehavior = UIPointerBehavior.SingleUnifiedPointer;
                    ConfigureNavigation(owner.transform);
                    ConfigureScopes(owner.transform);
                    var skillBar = all.OfType<CombatSkillBarView>().FirstOrDefault();
                    if (skillBar != null)
                    {
                        var skillRoot = ObjectField(skillBar, "skillBarRoot");
                        AddScope(skillRoot, view.ReturnToWorld, false);
                        var skillScope = skillRoot.GetComponent<ControllerNavigationScope>(); skillScope.enabled = false;
                        Set(view, "skillScope", skillScope);
                    }
                    ConfigureCursorAndHints(canvas, view);
                    ConfigureKeyboard(canvas, view);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(original); }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Output, "authoring.json"), JsonUtility.ToJson(new Authoring
                { scenes = scenes, prefabs = changedPrefabs.ToArray(), actions = ActionsPath }, true));
        }

        private static InputActionReference UiReference(InputActionAsset asset, string name)
        {
            var action = asset.FindAction("UI/" + name, true);
            return AssetDatabase.LoadAllAssetsAtPath(ActionsPath).OfType<InputActionReference>()
                .Single(r => r.action != null && r.action.id == action.id && (r.hideFlags & HideFlags.HideInHierarchy) == 0);
        }

        private static IEnumerable<string> OrderedUiPrefabs()
        {
            var paths = new HashSet<string>(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" }).Select(AssetDatabase.GUIDToAssetPath));
            var visited = new HashSet<string>();
            var ordered = new List<string>();
            void Visit(string path)
            {
                if (!visited.Add(path)) return;
                foreach (var dependency in AssetDatabase.GetDependencies(path, false).Where(p => p != path && paths.Contains(p)).OrderBy(p => p)) Visit(dependency);
                ordered.Add(path);
            }
            foreach (var path in paths.OrderBy(p => p)) Visit(path);
            return ordered;
        }

        private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(name) ?? throw new InvalidOperationException(target.name + ": missing " + name);
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ConfigureNavigation(Transform root)
        {
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable is Slider slider && !slider.interactable) continue;
                var navigation = selectable.navigation; navigation.mode = Navigation.Mode.Automatic; selectable.navigation = navigation;
            }
            foreach (var behavior in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behavior == null || behavior is ControllerNavigationItem || (behavior is not IPointerClickHandler && behavior is not Selectable)
                    || behavior.transform is not RectTransform || behavior is UIWorkspaceWindow
                    || behavior is PartyRailMenuView || behavior is UIWorkspaceTooltipView) continue;
                var selectable = behavior.GetComponent<Selectable>();
                // Item slots expose click/context actions without a Button. Make those entries navigable.
                if (selectable == null && behavior is IPointerEnterHandler)
                    selectable = behavior.gameObject.AddComponent<Selectable>();
                if (selectable == null) continue;
                if (selectable is Slider slider && !slider.interactable) continue;
                var nav = selectable.navigation;
                nav.mode = Navigation.Mode.Automatic;
                selectable.navigation = nav;
                var item = behavior.GetComponent<ControllerNavigationItem>() ?? behavior.gameObject.AddComponent<ControllerNavigationItem>();
                if (behavior is IPointerClickHandler && behavior is not Selectable && behavior is not ISubmitHandler
                    && behavior is not CharacterInfoPartyPortraitView) Set(item, "pointerHandler", behavior);
                if (behavior is CharacterInfoPartyPortraitView) Set(item, "pointerHandler", null);
                if (behavior.transform.Find("ControllerFocus") == null)
                {
                    var frame = new GameObject("ControllerFocus", typeof(RectTransform), typeof(CanvasRenderer), typeof(ControllerFocusGraphic), typeof(LayoutElement));
                    var rect = (RectTransform)frame.transform;
                    rect.SetParent(behavior.transform, false);
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                    ConfigureFocusGraphic(frame);
                    frame.GetComponent<LayoutElement>().ignoreLayout = true;
                    Set(item, "focusFrame", frame); frame.SetActive(false);
                }
                else
                {
                    var frames = behavior.GetComponentsInChildren<RectTransform>(true).Where(r => r.parent == behavior.transform && r.name == "ControllerFocus").ToArray();
                    var kept = frames.FirstOrDefault(r => PrefabUtility.GetCorrespondingObjectFromSource(r.gameObject) != null) ?? frames[0];
                    foreach (var extra in frames.Where(r => r != kept && PrefabUtility.GetCorrespondingObjectFromSource(r.gameObject) == null)) UnityEngine.Object.DestroyImmediate(extra.gameObject);
                    var layout = kept.GetComponent<LayoutElement>() ?? kept.gameObject.AddComponent<LayoutElement>(); layout.ignoreLayout = true;
                    ConfigureFocusGraphic(kept.gameObject);
                    Set(item, "focusFrame", kept.gameObject);
                    var items = behavior.GetComponents<ControllerNavigationItem>();
                    if (items.Length > 1)
                    {
                        var inherited = items.FirstOrDefault(i => PrefabUtility.GetCorrespondingObjectFromSource(i) != null) ?? items[0];
                        foreach (var extra in items.Where(i => i != inherited && PrefabUtility.GetCorrespondingObjectFromSource(i) == null)) UnityEngine.Object.DestroyImmediate(extra);
                        Set(inherited, "focusFrame", kept.gameObject);
                    }
                }
            }
        }

        private static void ConfigureFocusGraphic(GameObject frame)
        {
            if (frame.TryGetComponent<Outline>(out var outline)) UnityEngine.Object.DestroyImmediate(outline);
            if (frame.TryGetComponent<Image>(out var image)) UnityEngine.Object.DestroyImmediate(image);
            var graphic = frame.GetComponent<ControllerFocusGraphic>() ?? frame.AddComponent<ControllerFocusGraphic>();
            graphic.color = new Color(1, .8f, .25f, 1);
            graphic.raycastTarget = false;
        }

        private static void AddScope(GameObject root, UnityAction cancel, bool modal)
        {
            if (root == null) return;
            var scope = root.GetComponent<ControllerNavigationScope>() ?? root.AddComponent<ControllerNavigationScope>();
            var so = new SerializedObject(scope); so.FindProperty("modal").boolValue = modal; so.ApplyModifiedPropertiesWithoutUndo();
            if (cancel != null && scope.CancelAction.GetPersistentEventCount() == 0) UnityEventTools.AddPersistentListener(scope.CancelAction, cancel);
        }

        private static GameObject ObjectField(UnityEngine.Object owner, string field)
        {
            var value = new SerializedObject(owner).FindProperty(field)?.objectReferenceValue;
            return value is GameObject go ? go : value is Component component ? component.gameObject : null;
        }

        private static void ConfigureScopes(Transform root)
        {
            foreach (var menu in root.GetComponentsInChildren<MainMenuView>(true)) AddScope(menu.gameObject, null, true);
            foreach (var window in root.GetComponentsInChildren<UIWorkspaceWindow>(true)) AddScope(window.gameObject, window.Close, false);
            foreach (var menu in root.GetComponentsInChildren<PartyRailMenuView>(true)) AddScope(menu.gameObject, menu.Back, true);
            foreach (var popup in root.GetComponentsInChildren<ItemActionPopupView>(true))
            {
                AddScope(ObjectField(popup, "quantityPanel"), popup.HideAll, true);
                AddScope(ObjectField(popup, "comparisonPanel"), popup.HideAll, true);
            }
            foreach (var pause in root.GetComponentsInChildren<PauseMenuView>(true)) AddScope(ObjectField(pause, "overlayRoot"), pause.HandleEscape, true);
            foreach (var settings in root.GetComponentsInChildren<MainMenuSettingsView>(true)) AddScope(ObjectField(settings, "panelRoot"), settings.HandleEscape, true);
            foreach (var save in root.GetComponentsInChildren<SaveLoadPanelView>(true)) AddScope(ObjectField(save, "panelRoot"), save.HandleEscape, true);
            foreach (var consumables in root.GetComponentsInChildren<CombatConsumableSelectionView>(true))
            {
                if (consumables.GetComponent<UIWorkspaceWindow>() != null) AddScope(consumables.gameObject, consumables.Close, true);
                else if (consumables.TryGetComponent<ControllerNavigationScope>(out var childScope))
                    UnityEngine.Object.DestroyImmediate(childScope); // The retired duplicate content component does not own a window.
            }
            foreach (var loadout in root.GetComponentsInChildren<SkillLoadoutModalView>(true)) AddScope(ObjectField(loadout, "panelRoot"), loadout.Close, true);
            foreach (var inventory in root.GetComponentsInChildren<InventoryEquipmentPanelView>(true)) AddScope(ObjectField(inventory, "panelRoot"), inventory.Close, false);
            foreach (var trade in root.GetComponentsInChildren<TradePanelView>(true)) AddScope(ObjectField(trade, "panelRoot"), trade.Close, false);
            foreach (var journal in root.GetComponentsInChildren<QuestJournalPanelView>(true)) AddScope(ObjectField(journal, "panelRoot"), journal.Close, false);
            foreach (var dialogue in root.GetComponentsInChildren<Godmist.Views.NPC.NpcDialogueController>(true)) AddScope(ObjectField(dialogue, "panelRoot"), dialogue.CloseForController, true);
            foreach (var minimap in root.GetComponentsInChildren<Godmist.Views.Dungeons.DungeonMinimapView>(true))
            {
                AddScope(minimap.gameObject, minimap.CloseOverviewForController, true);
                var scope = minimap.GetComponent<ControllerNavigationScope>(); scope.enabled = false;
                Set(minimap, "controllerScope", scope);
            }
            foreach (var defeat in root.GetComponentsInChildren<DefeatPanelView>(true)) AddScope(ObjectField(defeat, "panelRoot"), null, true);
        }

        private static void ConfigureCursorAndHints(Canvas canvas, ControllerInputView view)
        {
            var cursor = canvas.transform.Find("ControllerCursor") as RectTransform;
            if (cursor == null)
            {
                var go = new GameObject("ControllerCursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cursor = (RectTransform)go.transform; cursor.SetParent(canvas.transform, false);
                cursor.sizeDelta = new Vector2(12, 12);
                var image = go.GetComponent<Image>(); image.color = new Color(1, .85f, .3f, .9f); image.raycastTarget = false;
            }
            Set(view, "cursor", cursor); cursor.gameObject.SetActive(false);
            if (canvas.GetComponent<ContextualInputHintsView>() == null) canvas.gameObject.AddComponent<ContextualInputHintsView>();
        }

        private static void ConfigureKeyboard(Canvas canvas, ControllerInputView input)
        {
            var existing = canvas.GetComponentInChildren<ControllerKeyboardView>(true);
            if (existing != null)
            {
                ConfigureKeyboardLabels(existing);
                var actual = existing.GetComponentsInChildren<ControllerKeyboardKey>(true)
                    .Select(k => new SerializedObject(k).FindProperty("token").stringValue).ToArray();
                var expected = KeyboardCharacters.Select(c => c.ToString()).Concat(new[] { "shift", "space", "backspace", "clear" }).ToArray();
                if (actual.Length != expected.Length || !new HashSet<string>(actual).SetEquals(expected))
                { UnityEngine.Object.DestroyImmediate(existing.gameObject); existing = null; }
            }
            if (existing != null)
            {
                Set(input, "keyboardView", existing); AddScope(existing.gameObject, existing.Cancel, true);
                Set(existing, "firstKey", existing.GetComponentInChildren<ControllerKeyboardKey>(true)?.GetComponent<Button>());
                return;
            }
            var windowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Window.prefab");
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Workspace/PF_UIW_Button.prefab");
            if (windowPrefab == null || buttonPrefab == null) throw new InvalidOperationException("Existing workspace window/button styles are required.");
            var manager = canvas.GetComponentInChildren<UIWorkspaceWindowManager>(true);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(windowPrefab, manager != null ? manager.WorkspaceRoot : canvas.transform);
            if (manager != null) Set(go.GetComponent<UIWorkspaceWindow>(), "manager", manager);
            go.name = "ControllerKeyboard";
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(620, 470);
            var keyboard = go.AddComponent<ControllerKeyboardView>();
            var title = go.transform.Find("Header/Title")?.GetComponent<TMP_Text>();
            if (title != null) title.text = "Wpisz tekst";
            var content = go.transform.Find("Content") as RectTransform;
            if (content == null) throw new InvalidOperationException("Workspace window Content missing.");
            var layout = new GameObject("KeyboardLayout", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var layoutRect = (RectTransform)layout.transform; layoutRect.SetParent(content, false);
            layoutRect.anchorMin = Vector2.zero; layoutRect.anchorMax = Vector2.one; layoutRect.offsetMin = new Vector2(8, 8); layoutRect.offsetMax = new Vector2(-8, -8);
            var vertical = layout.GetComponent<VerticalLayoutGroup>(); vertical.spacing = 8;
            vertical.childControlWidth = vertical.childControlHeight = true; vertical.childForceExpandWidth = true; vertical.childForceExpandHeight = false;
            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
            previewGo.transform.SetParent(layout.transform, false); previewGo.GetComponent<LayoutElement>().preferredHeight = 46;
            var preview = previewGo.GetComponent<TextMeshProUGUI>();
            preview.font = title != null ? title.font : canvas.GetComponentInChildren<TMP_Text>(true).font;
            preview.fontSize = 22; preview.raycastTarget = false;
            Set(keyboard, "preview", preview);
            var keys = new GameObject("Keys", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            keys.transform.SetParent(layout.transform, false); keys.GetComponent<LayoutElement>().preferredHeight = 300;
            var grid = keys.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(52, 38); grid.spacing = new Vector2(5, 5);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 10; grid.childAlignment = TextAnchor.UpperCenter;
            void Key(string token, string label)
            {
                var buttonGo = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, keys.transform);
                buttonGo.name = "Key " + token;
                var key = buttonGo.AddComponent<ControllerKeyboardKey>();
                var text = buttonGo.GetComponentInChildren<TMP_Text>(true); text.text = label; text.fontSize = 17;
                var icon = buttonGo.transform.Find("Icon"); if (icon != null) icon.gameObject.SetActive(false);
                Set(key, "keyboard", keyboard); Set(key, "label", text);
                var serialized = new SerializedObject(key); serialized.FindProperty("token").stringValue = token; serialized.ApplyModifiedPropertiesWithoutUndo();
                UnityEventTools.AddPersistentListener(buttonGo.GetComponent<Button>().onClick, key.Enter);
            }
            foreach (var character in KeyboardCharacters) Key(character.ToString(), character.ToString());
            Set(keyboard, "firstKey", keys.GetComponentInChildren<ControllerKeyboardKey>(true).GetComponent<Button>());
            Key("shift", "Aa"); Key("space", "Sp"); Key("backspace", "Usuń"); Key("clear", "Clr");
            var footer = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            footer.transform.SetParent(layout.transform, false); footer.GetComponent<LayoutElement>().preferredHeight = 42;
            var horizontal = footer.GetComponent<HorizontalLayoutGroup>(); horizontal.spacing = 8; horizontal.childForceExpandWidth = true; horizontal.childControlHeight = true;
            foreach (var accept in new[] { false, true })
            {
                var buttonGo = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, footer.transform);
                buttonGo.GetComponentInChildren<TMP_Text>(true).text = accept ? "Gotowe" : "Anuluj";
                var icon = buttonGo.transform.Find("Icon"); if (icon != null) icon.gameObject.SetActive(false);
                UnityEventTools.AddPersistentListener(buttonGo.GetComponent<Button>().onClick, accept ? keyboard.Accept : keyboard.Cancel);
            }
            var scope = go.GetComponent<ControllerNavigationScope>() ?? go.AddComponent<ControllerNavigationScope>();
            while (scope.CancelAction.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(scope.CancelAction, 0);
            UnityEventTools.AddPersistentListener(scope.CancelAction, keyboard.Cancel);
            var scopeSo = new SerializedObject(scope); scopeSo.FindProperty("modal").boolValue = true; scopeSo.ApplyModifiedPropertiesWithoutUndo();
            Set(input, "keyboardView", keyboard); go.SetActive(false);
            ConfigureKeyboardLabels(keyboard);
        }

        private static void ConfigureKeyboardLabels(ControllerKeyboardView keyboard)
        {
            var title = keyboard.transform.Find("Header/Title")?.GetComponent<TMP_Text>();
            if (title == null) throw new InvalidOperationException("Keyboard window header is missing.");
            title.text = "Wpisz tekst";
            var style = title.GetComponent<UIWorkspaceStyleApplier>();
            if (style != null)
            {
                var data = new SerializedObject(style);
                data.FindProperty("textStyle").enumValueIndex = (int)UIWorkspaceTextStyle.Heading;
                data.ApplyModifiedPropertiesWithoutUndo(); style.Apply();
            }
            foreach (var key in keyboard.GetComponentsInChildren<ControllerKeyboardKey>(true))
            {
                var label = key.GetComponentInChildren<TMP_Text>(true);
                label.rectTransform.offsetMin = new Vector2(3, 0);
                label.rectTransform.offsetMax = new Vector2(-3, 0);
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        [Serializable] private sealed class Authoring { public string actions; public string[] scenes, prefabs; }
    }
}
