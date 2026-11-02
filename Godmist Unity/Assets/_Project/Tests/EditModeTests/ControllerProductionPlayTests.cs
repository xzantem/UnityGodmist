using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godmist.Views.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Godmist.Tests.EditMode
{
    /// <summary>Real Bootstrap wiring, real UGUI and action processing; never creates/loads a campaign or writes a save.</summary>
    public sealed class ControllerProductionPlayTests
    {
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalHandoffs/controller-rework-20261102"));
        private Gamepad pad;
        private Gamepad previousPad;
        private GamepadState desiredState;
        private Vector2 observedPointer;
        private Mouse verificationMouse;
        private Keyboard verificationKeyboard;
        private bool sendMouse, sendKeyboard;
        private readonly List<InputDevice> disabledDevices = new();
        private InputSettings.EditorInputBehaviorInPlayMode oldBehavior;
        private InputSettings.BackgroundBehavior oldBackgroundBehavior;
        private bool oldBackground, configured;

        [Serializable] private sealed class PreviousScenes { public string[] paths; public bool[] loaded, active; public bool gameMaximized; }

        [UnitySetUp]
        public IEnumerator EnterMenu()
        {
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty), Is.False,
                "Integration tests must not replace unsaved user scenes.");
            Directory.CreateDirectory(Output);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            var game = EditorWindow.focusedWindow;
            Assert.That(game?.GetType().Name, Is.EqualTo("GameView"));
            File.WriteAllText(Path.Combine(Output, "play-test-scenes.json"), JsonUtility.ToJson(new PreviousScenes
                { paths = setup.Select(s => s.path).ToArray(), loaded = setup.Select(s => s.isLoaded).ToArray(), active = setup.Select(s => s.isActive).ToArray(), gameMaximized = game.maximized }));
            game.maximized = true;
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Bootstrap/Bootstrap.unity");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            yield return new EnterPlayMode();
            oldBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            oldBackground = Application.runInBackground;
            configured = true;
            // Match player input delivery; keep the actual project action asset and scene setup.
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            previousPad = Gamepad.current;
            // Isolate this scripted input sequence from concurrent desktop activity. Devices
            // are disabled only in this Editor's Input System and restored in TearDown.
            foreach (var device in InputSystem.devices.ToArray())
                if (device.enabled && device is Mouse or Keyboard or Gamepad)
                { disabledDevices.Add(device); InputSystem.DisableDevice(device); }
            pad = InputSystem.AddDevice<Gamepad>("Godmist verification pad");
            InputSystem.onBeforeUpdate += FeedPad;
            Application.onBeforeRender += TracePlayer;
            desiredState = new GamepadState { leftStick = Vector2.up * .8f };
            for (var i = 0; i < 6; i++) yield return null;
            Assert.That(ControllerInput.UiActive, Is.True, "The authored Bootstrap controller must receive real player-loop input.");
            desiredState = new GamepadState();
            for (var i = 0; i < 3; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator LeaveMenu()
        {
            if (configured)
            {
                InputSystem.onBeforeUpdate -= FeedPad;
                Application.onBeforeRender -= TracePlayer;
                if (pad is { added: true }) InputSystem.RemoveDevice(pad);
                if (verificationMouse is { added: true }) InputSystem.RemoveDevice(verificationMouse);
                if (verificationKeyboard is { added: true }) InputSystem.RemoveDevice(verificationKeyboard);
                foreach (var device in disabledDevices) if (device.added) InputSystem.EnableDevice(device);
                disabledDevices.Clear();
                if (previousPad is { added: true }) previousPad.MakeCurrent();
                InputSystem.settings.editorInputBehaviorInPlayMode = oldBehavior;
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
                Application.runInBackground = oldBackground;
                configured = false;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
            var previous = JsonUtility.FromJson<PreviousScenes>(File.ReadAllText(Path.Combine(Output, "play-test-scenes.json")));
            // The test runner also restores its own scene after ExitPlayMode. Restore the user's
            // scenes explicitly through the scene API, without relying on an intermediate setup.
            UnityEngine.SceneManagement.Scene activeScene = default;
            for (var i = 0; i < previous.paths.Length; i++)
            {
                var scene = EditorSceneManager.OpenScene(previous.paths[i], i == 0 ? OpenSceneMode.Single
                    : previous.loaded[i] ? OpenSceneMode.Additive : OpenSceneMode.AdditiveWithoutLoading);
                if (previous.active[i]) activeScene = scene;
            }
            if (activeScene.IsValid()) SceneManager.SetActiveScene(activeScene);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            if (EditorWindow.focusedWindow?.GetType().Name == "GameView") EditorWindow.focusedWindow.maximized = previous.gameMaximized;
        }

        private static T ObjectField<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object
            => (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;

        private static T View<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();

        private IEnumerator Send(GamepadState state, int frames = 3)
        {
            desiredState = state;
            for (var i = 0; i < frames; i++)
            {
                yield return null;
            }
        }

        private void FeedPad()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            if (pad is { added: true }) InputSystem.QueueStateEvent(pad, desiredState);
            if (sendMouse)
            {
                sendMouse = false;
                InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = new Vector2(20, 20), delta = new Vector2(20, 20) });
            }
            if (sendKeyboard)
            {
                sendKeyboard = false;
                InputSystem.QueueStateEvent(verificationKeyboard, new KeyboardState(Key.LeftShift));
            }
        }

        private void TracePlayer()
        {
            observedPointer = ControllerInput.PointerPosition;
        }

        private IEnumerator Press(GamepadButton button)
        {
            yield return Send(new GamepadState().WithButton(button));
            yield return Send(new GamepadState());
        }

        private IEnumerator Capture(string name)
        {
            var path = Path.Combine(Output, name + ".png");
            if (File.Exists(path)) File.Move(path, Path.Combine(Output, name + "-previous-" + DateTime.UtcNow.Ticks + ".png"));
            ScreenCapture.CaptureScreenshot(path);
            for (var i = 0; i < 30 && !File.Exists(path); i++) yield return null;
            Assert.That(File.Exists(path), Is.True, "Game View must render the integration screenshot.");
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(1000));
        }

        [UnityTest]
        public IEnumerator SettingsOpensOnceWithHeldAAndBRestoresItsMenuFocus()
        {
            var menu = View<MainMenuView>();
            var settingsButton = ObjectField<Button>(menu, "settingsButton");
            var settings = View<MainMenuSettingsView>();
            var events = EventSystem.current;
            for (var i = 0; i < 12 && events.currentSelectedGameObject != settingsButton.gameObject; i++)
                yield return Press(GamepadButton.DpadDown);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(settingsButton.gameObject), "Menu must be navigable with the D-pad.");
            yield return Capture("menu-controller");
            var clicks = 0;
            settingsButton.onClick.AddListener(() => clicks++);
            yield return Send(new GamepadState().WithButton(GamepadButton.South), 8);
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(clicks, Is.EqualTo(1), "A must not be submitted by both the controller and native UI module.");
            Assert.That(ControllerInput.WorldActive, Is.False);
            var panel = ObjectField<GameObject>(settings, "panelRoot");
            Assert.That(events.currentSelectedGameObject.transform.IsChildOf(panel.transform), Is.True);
            var cursorSpeed = ObjectField<Slider>(settings,"controllerCursorSlider");
            var cameraSpeed = ObjectField<Slider>(settings,"controllerCameraSlider");
            Assert.That(cursorSpeed,Is.Not.Null); Assert.That(cameraSpeed,Is.Not.Null);
            var savedCursorSpeed = ControllerPreferences.CursorSpeed;
            var savedCameraSpeed = ControllerPreferences.CameraSpeed;
            cursorSpeed.value = savedCursorSpeed < 1.9f ? savedCursorSpeed+.1f : savedCursorSpeed-.1f;
            Assert.That(cameraSpeed.value,Is.EqualTo(savedCameraSpeed),"Cursor and camera have independent settings.");
            Assert.That(ObjectField<Button>(settings,"applyButton").interactable,Is.True);
            yield return Capture("settings-controller");
            yield return Send(new GamepadState());
            yield return Press(GamepadButton.East);
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(ControllerPreferences.CursorSpeed,Is.EqualTo(savedCursorSpeed),"Cancel does not persist cursor tuning.");
            Assert.That(ControllerPreferences.CameraSpeed,Is.EqualTo(savedCameraSpeed));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(settingsButton.gameObject));
            Assert.That(clicks, Is.EqualTo(1));
            InputSystem.RemoveDevice(pad);
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(ControllerInput.Active, Is.False);
        }

        [UnityTest]
        public IEnumerator RealSaveNameFieldOpensKeyboardAndCancelRollsBackWithoutSaving()
        {
            var saves = View<SaveLoadPanelView>();
            var keyboard = View<ControllerKeyboardView>();
            Assert.That(keyboard.gameObject.activeSelf, Is.False, "The keyboard must be closed before opening the form.");
            Assert.That(ObjectField<ControllerKeyboardView>(View<ControllerInputView>(), "keyboardView"), Is.SameAs(keyboard));
            // Same public form setup used by the pause menu; no Save/Load action is invoked.
            saves.Open(SaveLoadPanelMode.Save);
            Assert.That(keyboard.gameObject.activeSelf, Is.False, "Opening Save must not open the keyboard.");
            var field = ObjectField<TMP_InputField>(saves, "saveNameInput");
            Assert.That(field.gameObject.activeInHierarchy && field.interactable, Is.True);
            EventSystem.current.SetSelectedGameObject(field.gameObject);
            yield return Press(GamepadButton.South);
            Assert.That(keyboard.gameObject.activeInHierarchy, Is.True);
            Assert.That(field.enabled, Is.False, "The on-screen keyboard owns editing while open.");
            var firstKey = ObjectField<Button>(keyboard, "firstKey");
            Assert.That(firstKey.isActiveAndEnabled, Is.True, "The first authored key must be active.");
            yield return Capture("keyboard-open-controller");
            Assert.That(field.text, Is.Empty, "Opening the keyboard must not type the opening A press.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(firstKey.gameObject));
            yield return Press(GamepadButton.South);
            Assert.That(field.text, Is.EqualTo("1"));
            yield return Capture("keyboard-controller");
            yield return Press(GamepadButton.East);
            Assert.That(keyboard.gameObject.activeInHierarchy, Is.False);
            Assert.That(field.enabled, Is.True);
            Assert.That(field.text, Is.Empty);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(field.gameObject));
            saves.Close();
        }

        [UnityTest]
        public IEnumerator MouseAndKeyboardTakeOverAndPadCanReturnWithoutLosingFocus()
        {
            var events = EventSystem.current;
            var original = events.currentSelectedGameObject;
            verificationMouse = InputSystem.AddDevice<Mouse>("Godmist verification mouse");
            for (var i = 0; i < 3; i++) yield return null;
            sendMouse = true;
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(ControllerInput.Active, Is.False);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(original));
            Assert.That(events.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule module && module.submit.action.enabled, Is.True);
            yield return Press(GamepadButton.RightStick);
            Assert.That(ControllerInput.UiActive, Is.True);
            verificationKeyboard = InputSystem.AddDevice<Keyboard>("Godmist verification keyboard");
            for (var i = 0; i < 3; i++) yield return null;
            sendKeyboard = true;
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(ControllerInput.Active, Is.False);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(original));
            yield return Press(GamepadButton.RightStick);
            Assert.That(ControllerInput.UiActive, Is.True);
        }

        [UnityTest]
        public IEnumerator FreeCameraKeepsLeftStickMovementAndAutomaticHoverStaysHidden()
        {
            var bootstrap = Godmist.Views.Game.GameBootstrap.Instance;
            Assert.That(bootstrap.TryStartNewGame(out var message), Is.True, message);
            var deadline = Time.realtimeSinceStartup + 30;
            while (bootstrap.IsSceneLoading && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(bootstrap.IsSceneLoading, Is.False);
            for (var i = 0; i < 3; i++) yield return null;
            var party = View<Godmist.Views.Exploration.ExplorationPartyController>();
            var camera = View<Godmist.Views.Exploration.ExplorationCameraFollow>();
            var inspections = View<Godmist.Views.Combat.CombatUnitInspectController>();
            var tooltip = ObjectField<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>(inspections, "worldTooltip");
            var leader = party.ActiveLeaderTransform.GetComponent<Godmist.Views.Exploration.ExplorationCharacterView>();
            Assert.That(leader, Is.Not.Null);
            var hoverSource = (object)leader.CombatUnit ?? leader;

            // Right-stick panning unlocks the camera. Do not use L3 to wake/recenter it.
            yield return Send(new GamepadState { rightStick = Vector2.right * .8f }, 8);
            Assert.That(camera.IsFollowingLeader, Is.False);

            var start = party.ActiveLeaderTransform.position;
            yield return Send(new GamepadState { leftStick = Vector2.left * .8f }, 20);
            Assert.That(Vector3.Distance(start, party.ActiveLeaderTransform.position), Is.GreaterThan(.05f), "Left stick must move immediately while the camera remains unlocked.");
            Assert.That(camera.IsFollowingLeader, Is.False);
            Assert.That(tooltip.IsVisibleOrPendingFor(hoverSource), Is.False);
            Assert.That(ObjectField<RectTransform>(View<ControllerInputView>(), "cursor").gameObject.activeSelf, Is.False);
            yield return Send(new GamepadState());
            yield return Capture("free-camera-moving-controller");

            // Explicit precise-pointer hover remains available on the real character.
            var destination = (Vector2)Camera.main.WorldToScreenPoint(leader.transform.position);
            yield return Press(GamepadButton.LeftStick);
            deadline = Time.realtimeSinceStartup + 8;
            while (Vector2.Distance(observedPointer, destination) > 3 && Time.realtimeSinceStartup < deadline)
            {
                var offset = destination - observedPointer;
                yield return Send(new GamepadState { leftStick = offset.normalized * Mathf.Clamp(offset.magnitude / 60f, .3f, .7f) }, 1);
            }
            yield return Send(new GamepadState());
            deadline = Time.unscaledTime + 1;
            while (!tooltip.IsVisibleOrPendingFor(hoverSource) && Time.unscaledTime < deadline) yield return null;
            Assert.That(tooltip.IsVisibleOrPendingFor(hoverSource), Is.True, "L3 cursor mode must allow deliberate world inspection.");
            yield return Press(GamepadButton.LeftStick);
            yield return Send(new GamepadState { leftStick = Vector2.left * .8f }, 8);
            Assert.That(tooltip.IsVisibleOrPendingFor(hoverSource), Is.False, "Releasing precise mode must clear both pending and visible automatic hover.");
            Assert.That(camera.IsFollowingLeader, Is.False);
            yield return Press(GamepadButton.RightStick);
            Assert.That(camera.IsFollowingLeader, Is.True, "R3 must restore camera follow independently of L3 cursor mode.");

        }

        [UnityTest, Order(0)]
        public IEnumerator TownMovementInventoryContextAndCombatSkillSelectionUseAuthoredViews()
        {
            var bootstrap = Godmist.Views.Game.GameBootstrap.Instance;
            Assert.That(bootstrap.TryStartNewGame(out var message), Is.True, message);
            var deadline = Time.realtimeSinceStartup + 30;
            while (bootstrap.IsSceneLoading && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(bootstrap.IsSceneLoading, Is.False);
            for (var i = 0; i < 3; i++) yield return null;
            var party = View<Godmist.Views.Exploration.ExplorationPartyController>();
            TownStage("loaded", party);
            Assert.That(party.ActiveLeaderTransform, Is.Not.Null);
            yield return Press(GamepadButton.RightStick);
            var initialLeader = party.Session.PlayerParty.ActiveLeaderId;
            yield return Press(GamepadButton.RightShoulder);
            Assert.That(party.Session.PlayerParty.ActiveLeaderId, Is.Not.EqualTo(initialLeader), "RB selects the next party member in exploration.");
            yield return Press(GamepadButton.LeftShoulder);
            Assert.That(party.Session.PlayerParty.ActiveLeaderId, Is.EqualTo(initialLeader), "LB returns to the previous member.");
            var start = party.ActiveLeaderTransform.position;
            yield return Send(new GamepadState { leftStick = Vector2.left * .8f }, 20);
            TownStage("moved", party);
            Assert.That(Vector3.Distance(start, party.ActiveLeaderTransform.position), Is.GreaterThan(.05f), "Real exploration movement must read the pad.");
            yield return Send(new GamepadState());
            yield return Capture("town-controller");
            // Arrange proximity to an actual authored NPC; all interaction and UI execution remains production code.
            var city = View<Godmist.Views.Exploration.ExplorationSandboxBootstrap>();
            var authoredNpcs = city.NpcBindings.Select(binding => binding.NpcView).Where(view => view != null).ToArray();
            File.WriteAllLines(Path.Combine(Output, "nearby-setup.txt"), authoredNpcs.Select(view =>
                $"{view.DisplayName}: activeSelf={view.gameObject.activeSelf}; hierarchy={view.gameObject.activeInHierarchy}; enabled={view.enabled}; scene={view.gameObject.scene.name}"));
            Assert.That(authoredNpcs.Length, Is.GreaterThan(0), "The production city must expose its authored NPC bindings.");
            var npc = authoredNpcs.First();
            Assert.That(npc.isActiveAndEnabled, Is.True, File.ReadAllText(Path.Combine(Output, "nearby-setup.txt")));
            party.CancelMovement();
            var savedPosition = party.ActiveLeaderTransform.position;
            party.ActiveLeaderTransform.position = npc.transform.position + Vector3.down * .5f;
            yield return Send(new GamepadState());
            Assert.That(npc.CanInteract(), Is.True);
            Assert.That(View<ControllerInputView>().InteractionPrompt, Does.Contain(npc.DisplayName));
            yield return Capture("interaction-target-controller");
            yield return Send(new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(ControllerInput.UiActive, Is.True, "Held A must open nearby interaction selection.");
            Assert.That(party.Session.CurrentMode, Is.EqualTo(Godmist.Core.Enums.GameModeState.Exploration), "Held A must not also begin a dialogue.");
            var nearby = ObjectField<Godmist.Views.UI.Workspace.PartyRailMenuView>(View<ControllerInputView>(), "selectorMenu");
            Assert.That(nearby.GetComponentsInChildren<TMP_Text>().Any(label => label.text == npc.DisplayName), Is.True);
            yield return Capture("nearby-interactions-controller");
            yield return Press(GamepadButton.East);
            party.ActiveLeaderTransform.position = savedPosition;
            yield return Send(new GamepadState());

            yield return Send(new GamepadState { leftTrigger = 1 });
            yield return Send(new GamepadState());
            Assert.That(ControllerInput.UiActive, Is.True);
            yield return Capture("party-selector-controller");
            yield return Press(GamepadButton.East);
            Assert.That(ControllerInput.WorldActive, Is.True);
            yield return Send(new GamepadState { leftTrigger = 1 });
            yield return Send(new GamepadState());
            yield return Press(GamepadButton.South); // LT selects a subject, then shows its panels.
            TownStage("panels opened", party);
            Assert.That(ControllerInput.UiActive, Is.True);
            yield return Capture("panels-selector-controller");
            start = party.ActiveLeaderTransform.position;
            yield return Send(new GamepadState { leftStick = Vector2.right * .8f }, 12);
            Assert.That(Vector3.Distance(start, party.ActiveLeaderTransform.position), Is.LessThan(.01f), "UI navigation must not move the party.");
            yield return Send(new GamepadState());
            var controllerWheel = View<ControllerWheelView>();
            Assert.That(controllerWheel.IsOpen, Is.True);
            yield return Send(new GamepadState { leftStick = Vector2.up * .8f });
            yield return Send(new GamepadState());
            Assert.That(controllerWheel.SelectedId, Is.EqualTo("Inventory"));
            yield return Press(GamepadButton.South);
            var card = View<Godmist.Views.UI.Workspace.CharacterInfoWindowView>();
            Assert.That(card.gameObject.activeInHierarchy, Is.True);
            yield return Capture("inventory-controller");
            var window = card.GetComponent<Godmist.Views.UI.Workspace.UIWorkspaceWindow>();
            var handle = card.GetComponentInChildren<Godmist.Views.UI.Workspace.UIWorkspaceWindowDragHandle>();
            var origin = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)handle.transform).TransformPoint(((RectTransform)handle.transform).rect.center));
            yield return MovePointerTo(origin, false);
            var windowStart = window.RectTransform.anchoredPosition;
            yield return Send(new GamepadState().WithButton(GamepadButton.South));
            yield return MovePointerTo(origin + new Vector2(30, 15), true);
            yield return Send(new GamepadState());
            Assert.That(Vector2.Distance(windowStart, window.RectTransform.anchoredPosition), Is.GreaterThan(5), "Precise A drag must reach the real UGUI drag handle.");
            yield return Press(GamepadButton.East); // First B exits the optional cursor.
            yield return Press(GamepadButton.East); // Next B closes the card.
            Assert.That(window.IsOpen, Is.False);
            Assert.That(ControllerInput.WorldActive, Is.True);

            var operation = SceneManager.LoadSceneAsync("CombatSandbox");
            while (!operation.isDone) yield return null;
            yield return Send(new GamepadState { leftStick = Vector2.right * .8f });
            yield return Send(new GamepadState());
            var combat = View<Godmist.Views.Combat.CombatSandboxBootstrap>();
            Assert.That(combat.Session?.ActiveUnit?.IsPlayerControlled, Is.True);
            var actor = combat.Session.ActiveUnit;
            var actorStart = actor.Position;
            var ap = actor.CurrentActionPoints;
            var cursorStart = observedPointer;
            yield return Send(new GamepadState { leftStick = Vector2.right * .8f });
            yield return Send(new GamepadState());
            Assert.That(Vector2.Distance(cursorStart, observedPointer), Is.GreaterThan(2));
            Assert.That(actor.Position, Is.EqualTo(actorStart));
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(ap));
            var worldCamera = Camera.main;
            var combatSpawner = View<Godmist.Views.Combat.CombatUnitViewSpawner>();
            // The standalone sandbox has no DungeonCombatCoordinator. Use its same public focus
            // contract with the actual grid bounds; do not bypass framing, constraints or input.
            Assert.That(combatSpawner.TryGetWorldPosition(new Godmist.Runtime.Combat.GridPosition(0,0),out var gridMin),Is.True);
            Assert.That(combatSpawner.TryGetWorldPosition(new Godmist.Runtime.Combat.GridPosition(combat.Session.Grid.Width-1,combat.Session.Grid.Height-1),out var gridMax),Is.True);
            var cameraBounds = new Bounds((gridMin+gridMax)*.5f,gridMax-gridMin+Vector3.one);
            combat.CameraFramer.Focus(combat.Session,cameraBounds,cameraBounds,unit=>combatSpawner.TryGetWorldPosition(unit,out var point)?point:Vector3.zero);
            combat.CameraFramer.Zoom(7);
            for(var i=0;i<60;i++) yield return null;
            Assert.That(combat.CameraFramer.CanAcceptManualInput,Is.True);
            var selectedWorldPoint = (Vector2)worldCamera.ScreenToWorldPoint(observedPointer);
            var beforeCamera = worldCamera.transform.position;
            yield return Send(new GamepadState { rightStick = Vector2.up * .8f }, 8);
            yield return Send(new GamepadState());
            Assert.That(Vector3.Distance(beforeCamera,worldCamera.transform.position),Is.GreaterThan(.01f));
            Assert.That(Vector2.Distance(selectedWorldPoint,worldCamera.ScreenToWorldPoint(observedPointer)),Is.LessThan(.01f),"Panning must retain the selected world cell.");
            var zoomBefore = worldCamera.orthographicSize;
            yield return Send(new GamepadState().WithButton(GamepadButton.DpadUp),8);
            yield return Send(new GamepadState());
            Assert.That(Mathf.Abs(zoomBefore-worldCamera.orthographicSize),Is.GreaterThan(.001f));
            Assert.That(Vector2.Distance(selectedWorldPoint,worldCamera.ScreenToWorldPoint(observedPointer)),Is.LessThan(.01f),"Zooming must retain the selected world cell.");
            yield return Capture("world-cell-camera-controller");
            yield return Press(GamepadButton.DpadRight);
            var firstTarget = observedPointer;
            yield return Press(GamepadButton.DpadRight);
            Assert.That(Vector2.Distance(firstTarget, observedPointer), Is.GreaterThan(2), "D-pad must cycle real visible combat targets.");
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(ap));
            yield return Press(GamepadButton.North);
            Assert.That(View<ControllerInputView>().HasWindowFocus,Is.True,"A short Y opens inspection, never ends the turn.");
            Assert.That(combat.Session.ActiveUnit,Is.SameAs(actor));
            yield return Capture("target-inspection-controller");
            yield return Press(GamepadButton.East);
            Assert.That(ControllerInput.WorldActive,Is.True);
            yield return Send(new GamepadState { rightTrigger = 1 });
            yield return Send(new GamepadState());
            Assert.That(ControllerInput.UiActive, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject.GetComponentInParent<ControllerWheelView>(), Is.Not.Null);
            yield return Capture("combat-skills-controller");
            var radial = View<ControllerWheelView>();
            var radialTooltip = ObjectField<Godmist.Views.UI.Workspace.UIWorkspaceTooltipView>(View<CombatSkillBarView>(),"tooltipView");
            yield return Press(GamepadButton.North);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(radialTooltip.IsVisibleOrPendingFor(radial),Is.True,"Y opens details of the selected radial skill.");
            yield return Capture("radial-skill-details-controller");
            yield return Send(new GamepadState { leftStick=Vector2.right*.8f });
            yield return Send(new GamepadState());
            Assert.That(radialTooltip.IsVisibleOrPendingFor(radial),Is.False,"Changing selection must close old skill details.");
            yield return Send(new GamepadState { leftStick=Vector2.up*.8f });
            yield return Send(new GamepadState());
            yield return Press(GamepadButton.South);
            var targeting = View<Godmist.Views.Combat.CombatTargetSelectionController>();
            Assert.That(targeting.SelectedSkill, Is.Not.Null);
            Assert.That(ControllerInput.WorldActive, Is.True);
            yield return Capture("combat-target-controller");
            var heldY = new GamepadState().WithButton(GamepadButton.North);
            yield return Send(heldY);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(combat.Session.ActiveUnit, Is.SameAs(actor), "Y while targeting cannot end the turn.");
            yield return Send(heldY.WithButton(GamepadButton.East));
            yield return Send(heldY);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(targeting.SelectedSkill, Is.Null);
            Assert.That(combat.Session.ActiveUnit, Is.SameAs(actor), "Carrying Y through target cancellation must not end the turn.");
            Assert.That(actor.CurrentActionPoints, Is.EqualTo(ap), "Choosing and cancelling a skill must not spend AP.");
            yield return Send(new GamepadState());
            var inputView = View<ControllerInputView>();
            var lastSkillId = View<CombatSkillBarView>().ControllerSlots.First().ControllerSkill.SkillId;
            yield return Send(new GamepadState { rightTrigger = 1 });
            yield return Send(new GamepadState());
            Assert.That(View<ControllerWheelView>().SelectedId,Is.EqualTo(lastSkillId),"RT remembers the last chosen skill for this character.");
            yield return Press(GamepadButton.East);
            var targetSelector = View<Godmist.Views.Combat.CombatTargetSelectionController>();
            actor.CurrentActionPoints = 0;
            yield return Send(new GamepadState());
            yield return Send(new GamepadState { rightTrigger = 1 });
            yield return Send(new GamepadState());
            yield return Capture("disabled-action-controller");
            yield return Press(GamepadButton.South);
            Assert.That(View<ControllerWheelView>().IsOpen,Is.True,"A cannot execute an unavailable action.");
            Assert.That(targetSelector.SelectedSkill,Is.Null);
            yield return Press(GamepadButton.East);
            actor.CurrentActionPoints = ap;
            yield return Send(new GamepadState());
            yield return Send(new GamepadState { rightTrigger = 1 });
            yield return Send(new GamepadState());
            var actionWheel = View<ControllerWheelView>();
            var bar = View<CombatSkillBarView>();
            var itemIndex = bar.ControllerSlots.Count(slot => slot != null && slot.isActiveAndEnabled && slot.ControllerSkill != null);
            for (var i = 0; i < itemIndex / 8; i++) yield return Press(GamepadButton.RightShoulder);
            var angle = (itemIndex % 8) * Mathf.PI * 2 / actionWheel.PageEntryCount;
            yield return Send(new GamepadState { leftStick = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * .8f });
            yield return Send(new GamepadState());
            Assert.That(actionWheel.SelectedId, Is.EqualTo("items"));
            yield return Press(GamepadButton.South);
            var combatActions = ObjectField<CombatActionButtonsView>(View<ControllerInputView>(), "combatActions");
            Assert.That(combatActions.IsConsumableSelectionOpen, Is.True, "RT → Przedmioty must open the window owned by the authored combat action view.");
            var input = View<ControllerInputView>();
            Assert.That(input.HasWindowFocus, Is.True);
            var hint = combatActions.GetComponentInParent<Canvas>().GetComponentsInChildren<TMP_Text>(true)
                .Single(label => label.name == "Contextual Input Hints");
            Assert.That(hint.gameObject.activeSelf, Is.False, "Input hints must not cover the open item picker.");
            yield return Capture("combat-items-controller");
            yield return Press(GamepadButton.East);
            Assert.That(ControllerInput.WorldActive, Is.True);
            var turn = combat.Session.ActiveTurnIndex;
            yield return Send(heldY);
            yield return new WaitForSecondsRealtime(.8f);
            yield return Send(new GamepadState());
            Assert.That(combat.Session.ActiveUnit != actor || combat.Session.ActiveTurnIndex != turn, Is.True,
                "A fresh held Y in neutral combat must invoke the production end-turn action.");
            // Reach an actual player's caster-only skill through the normal turn flow.
            // The Scout's initial five-slot loadout has none; do not substitute a fake skill.
            CombatSkillSlotView selfSlot = null;
            var casterDeadline = Time.realtimeSinceStartup + 45;
            while (selfSlot == null && Time.realtimeSinceStartup < casterDeadline)
            {
                yield return Send(new GamepadState());
                selfSlot = View<CombatSkillBarView>().ControllerSlots.FirstOrDefault(slot => slot.ControllerAvailable
                    && slot.ControllerSkill != null && slot.ControllerSkill.TargetModes.Count > 0
                    && slot.ControllerSkill.TargetModes.All(mode=>mode.TargetType==Godmist.Core.Enums.TargetType.Caster));
                if (selfSlot != null) break;
                if (combat.CanEndTurn())
                {
                    yield return Send(heldY);
                    yield return new WaitForSecondsRealtime(.8f);
                    yield return Send(new GamepadState());
                }
                else yield return null;
            }
            Assert.That(selfSlot,Is.Not.Null,"The real party turn flow must reach an available caster-only action.");
            var selfSkill = selfSlot.ControllerSkill;
            var caster = combat.Session.ActiveUnit;
            var casterAp = caster.CurrentActionPoints;
            var wasActive = caster.ActiveToggleSkillIds.Contains(selfSkill.SkillId);
            yield return ChooseWheelSkill(selfSkill.SkillId);
            Assert.That(targetSelector.SelectedSkill,Is.SameAs(selfSkill));
            Assert.That(targetSelector.RequiresControllerConfirmation,Is.True);
            Assert.That(caster.CurrentActionPoints,Is.EqualTo(casterAp));
            Assert.That(caster.ActiveToggleSkillIds.Contains(selfSkill.SkillId),Is.EqualTo(wasActive));
            yield return Send(new GamepadState(),3);
            var confirmationHint = View<CombatHudView>().GetComponentInParent<Canvas>().GetComponentsInChildren<TMP_Text>(true)
                .Single(label=>label.name=="Contextual Input Hints");
            Assert.That(confirmationHint.text,Does.Contain("A – użyj umiejętności"));
            yield return Capture("self-skill-confirmation-controller");
            yield return Press(GamepadButton.East);
            Assert.That(targetSelector.SelectedSkill,Is.Null);
            Assert.That(caster.CurrentActionPoints,Is.EqualTo(casterAp));
            yield return ChooseWheelSkill(selfSkill.SkillId);
            Assert.That(targetSelector.RequiresControllerConfirmation,Is.True);
            yield return Press(GamepadButton.South);
            Assert.That(targetSelector.SelectedSkill,Is.Null);
            if (selfSkill.IsToggle) Assert.That(caster.ActiveToggleSkillIds.Contains(selfSkill.SkillId),Is.Not.EqualTo(wasActive),"Only the second A changes the toggle state.");
            else Assert.That(caster.CurrentActionPoints,Is.LessThan(casterAp),"Only the second A spends the caster-only skill cost.");
            if (selfSkill.IsToggle && !wasActive)
            {
                yield return ChooseWheelSkill(selfSkill.SkillId);
                Assert.That(targetSelector.RequiresControllerConfirmation,Is.True);
                Assert.That(caster.ActiveToggleSkillIds.Contains(selfSkill.SkillId),Is.True,"Selecting an active toggle must not immediately switch it off.");
                yield return Press(GamepadButton.East);
                Assert.That(caster.ActiveToggleSkillIds.Contains(selfSkill.SkillId),Is.True);
            }

        }

        private IEnumerator ChooseWheelSkill(string id)
        {
            var slots = View<CombatSkillBarView>().ControllerSlots.Where(slot=>slot!=null && slot.isActiveAndEnabled && slot.ControllerSkill!=null).ToArray();
            var index = Array.FindIndex(slots,slot=>slot.ControllerSkill.SkillId==id);
            Assert.That(index,Is.GreaterThanOrEqualTo(0));
            yield return Send(new GamepadState { rightTrigger=1 });
            yield return Send(new GamepadState());
            // The wheel may reopen on the last page; use its actual current selection to choose a page.
            var wheel = View<ControllerWheelView>();
            var currentIndex = Array.FindIndex(slots,slot=>slot.ControllerSkill.SkillId==wheel.SelectedId);
            var currentPage = Mathf.Max(0,currentIndex)/8;
            for (var i=currentPage;i<index/8;i++) yield return Press(GamepadButton.RightShoulder);
            for (var i=currentPage;i>index/8;i--) yield return Press(GamepadButton.LeftShoulder);
            var angle=(index%8)*Mathf.PI*2/wheel.PageEntryCount;
            yield return Send(new GamepadState { leftStick=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*.8f });
            yield return Send(new GamepadState());
            Assert.That(wheel.SelectedId,Is.EqualTo(id));
            yield return Press(GamepadButton.South);
        }

        private IEnumerator MovePointerTo(Vector2 destination, bool dragging)
        {
            if (!ControllerInput.PrecisePointer) yield return Press(GamepadButton.LeftStick);
            var deadline = Time.realtimeSinceStartup + 8;
            while (Vector2.Distance(observedPointer, destination) > 6 && Time.realtimeSinceStartup < deadline)
            {
                var offset = destination - observedPointer;
                var speed = Mathf.Clamp(offset.magnitude / 60f, .35f, .8f);
                var state = new GamepadState { leftStick = offset.normalized * speed };
                if (dragging) state = state.WithButton(GamepadButton.South);
                yield return Send(state, 1);
            }
            var held = new GamepadState();
            if (dragging) held = held.WithButton(GamepadButton.South);
            yield return Send(held);
            Assert.That(Vector2.Distance(observedPointer, destination), Is.LessThan(9), "Precise cursor must reach the real control using the left stick.");
        }

        private static void TownStage(string stage, Godmist.Views.Exploration.ExplorationPartyController party)
        {
            File.AppendAllText(Path.Combine(Output, "town-stages.txt"),
                $"{Time.frameCount} {stage}: scene={SceneManager.GetActiveScene().name}, party={party != null}, "
                + $"leaderId={party?.Session?.PlayerParty?.ActiveLeaderId}, leader={party?.ActiveLeaderTransform?.name}, "
                + $"mode={party?.Session?.CurrentMode}, ui={ControllerInput.UiActive}, world={ControllerInput.WorldActive}\n");
        }
    }

}
