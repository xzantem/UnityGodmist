using System.Collections.Generic;
using System.Linq;
using Godmist.Views.Combat;
using Godmist.Views.Exploration;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    /// <summary>Scene-authored input composition: owns devices, contexts and focus, never gameplay rules.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed partial class ControllerInputView : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private EventSystem events;
        [SerializeField] private InputSystemUIInputModule module;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private ExplorationPartyController party;
        [SerializeField] private CombatSandboxBootstrap combat;
        [SerializeField] private CombatSkillBarView skillBar;
        [SerializeField] private ControllerNavigationScope skillScope;
        [SerializeField] private CombatTargetSelectionController targeting;
        [SerializeField] private CombatUnitViewSpawner unitViews;
        [SerializeField] private CombatMovementInputController movement;
        [SerializeField] private Godmist.Views.Dungeons.DungeonMinimapView minimap;
        [SerializeField] private PauseMenuView pause;
        [SerializeField] private RectTransform cursor;
        [SerializeField] private ControllerKeyboardView keyboardView;
        [SerializeField, Min(.05f)] private float repeatDelay = .16f;
        private sealed class ScopeState
        {
            public ControllerNavigationScope Scope;
            public GameObject ReturnFocus, LastFocus;
            public bool ReturnContext;
            public int OpeningOrder;
        }
        private readonly List<ScopeState> scopes = new();
        private int nextScopeOrder;
        private InputActionMap world, ui, global;
        private Mouse pointer;
        private Mouse physicalMouse;
        private Keyboard keyboard;
        private Gamepad activePad;
        private double mouseTime, keyboardTime, padTime;
        private bool controllerActive, uiRequested;
        private bool precisePointer;
        private readonly ControllerHoldGesture endTurnHold = new();
        private readonly ControllerHoldGesture searchHold = new();
        private bool interactionPress;
        [System.NonSerialized] private Godmist.Runtime.Combat.BattleUnit holdActor;
        private bool originalCursorVisible;
        private bool changedCursor;
        private float nextStep;
        private Vector2 position;
        private Vector2 requestedScroll;
        private Vector2Int previousDirection;
        private Vector2Int previousUiDirection;
        private float nextUiStep;
        private GameObject lastSelected;
        private GameObject rememberedFocus;
        [System.NonSerialized] private Godmist.Runtime.Combat.BattleUnit cursorActor;
        private bool HasGameplay => party?.Session is { IsDefeated: false }
            || combat?.Session?.Outcome == Godmist.Core.Enums.CombatOutcomeState.Ongoing;
        private ScopeState TopState => scopes.Count == 0 ? null : scopes[^1];
        private ControllerNavigationScope TopScope => TopState?.Scope;
        public bool HasWindowFocus => ControllerInput.UiActive && TopScope != null && TopScope.GetComponent<UIWorkspaceWindow>() != null;
        public bool HasSkillBarFocus => ControllerInput.UiActive && skillScope != null && TopScope == skillScope;

        private void OnEnable()
        {
            if (actions == null || events == null || module == null) { enabled = false; return; }
            world = actions.FindActionMap(ControllerInput.WorldMap, true);
            ui = actions.FindActionMap(ControllerInput.UiMap, true);
            global = actions.FindActionMap(ControllerInput.GlobalMap, true);
            physicalMouse = Mouse.current;
            keyboard = Keyboard.current;
            mouseTime = physicalMouse?.lastUpdateTime ?? 0;
            keyboardTime = keyboard?.lastUpdateTime ?? 0;
            padTime = Gamepad.current?.lastUpdateTime ?? 0;
            position = physicalMouse?.position.ReadValue() ?? new Vector2(Screen.width / 2f, Screen.height / 2f);
            originalCursorVisible = Cursor.visible;
            global.Enable(); world.Disable(); ui.Disable();
        }

        private void OnDisable()
        {
            endTurnHold.Reset(); searchHold.Reset(); interactionPress = false;
            selectorMenu?.Hide();
            controllerActive = false;
            world?.Disable(); ui?.Disable(); global?.Disable();
            if (pointer != null) { InputSystem.RemoveDevice(pointer); pointer = null; }
            if (module != null) { module.move?.action?.Enable(); module.submit?.action?.Enable(); }
            if (cursor != null) cursor.gameObject.SetActive(false);
            if (changedCursor) { Cursor.visible = originalCursorVisible; changedCursor = false; }
            ReleaseCurrentFocus();
        }

        public void Register(ControllerNavigationScope scope)
        {
            if (scopes.Any(state => state.Scope == scope)) return;
            scopes.Add(new ScopeState
            {
                Scope = scope, ReturnFocus = events != null ? events.currentSelectedGameObject : null,
                ReturnContext = uiRequested, OpeningOrder = nextScopeOrder++
            });
            if (!controllerActive) return;
            uiRequested = true;
            SetContext(true);
            FocusFirst();
        }

        public void Unregister(ControllerNavigationScope scope)
        {
            var state = scopes.FirstOrDefault(entry => entry.Scope == scope);
            if (state == null) return;
            var wasTop = state == TopState;
            scopes.Remove(state);
            if (!wasTop || !controllerActive) return;
            uiRequested = state.ReturnContext;
            var previous = state.ReturnFocus;
            if (events != null && previous != null && previous.activeInHierarchy) events.SetSelectedGameObject(previous);
            else lastSelected = null;
        }

        private void Update()
        {
            requestedScroll = Vector2.zero;
            global.FindAction("Confirm", true).Enable();
            global.FindAction("Context", true).Enable();
            var pad = Gamepad.current;
            if (controllerActive && (pad == null || activePad is { added: false })) SetActive(false);
            var activityTime = double.NegativeInfinity;
            var controllerWasLast = controllerActive;
            if (pad != null && pad.lastUpdateTime > padTime)
            {
                padTime = pad.lastUpdateTime;
                // Ignore noise and drift: only deliberate movement/button use changes the input scheme.
                if (pad.leftStick.ReadValue().sqrMagnitude > .16f || pad.rightStick.ReadValue().sqrMagnitude > .16f
                    || pad.dpad.ReadValue().sqrMagnitude > .1f || pad.allControls.Any(c => c is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame))
                { activityTime = pad.lastUpdateTime; controllerWasLast = true; }
            }
            if (physicalMouse != null && physicalMouse.added && physicalMouse.lastUpdateTime > mouseTime)
            {
                mouseTime = physicalMouse.lastUpdateTime;
                if (physicalMouse.delta.ReadValue().sqrMagnitude > 4 || physicalMouse.leftButton.wasPressedThisFrame
                    || physicalMouse.rightButton.wasPressedThisFrame || physicalMouse.scroll.ReadValue().sqrMagnitude > 0)
                {
                    if (physicalMouse.lastUpdateTime > activityTime) { activityTime = physicalMouse.lastUpdateTime; controllerWasLast = false; }
                }
            }
            if (keyboard != null && keyboard.added && keyboard.lastUpdateTime > keyboardTime)
            {
                keyboardTime = keyboard.lastUpdateTime;
                if (keyboard.anyKey.wasPressedThisFrame && keyboard.lastUpdateTime > activityTime)
                { activityTime = keyboard.lastUpdateTime; controllerWasLast = false; }
            }
            if (!double.IsNegativeInfinity(activityTime)) SetActive(controllerWasLast);
            if (physicalMouse == null || !physicalMouse.added || !physicalMouse.enabled)
                physicalMouse = InputSystem.devices.OfType<Mouse>().FirstOrDefault(m => m != pointer && m.enabled);
            if (keyboard == null || !keyboard.added || !keyboard.enabled) keyboard = Keyboard.current;
            if (DevConsoleView.IsOpen) { SetActive(false); return; }
            if (!controllerActive) return;
            if (ControllerInput.Pressed(ControllerInput.GlobalMap, "Pause") && pause != null)
            {
                if (pause.IsOpen) pause.HandleEscape(); else pause.Open();
                uiRequested = pause.IsOpen;
            }
            var selectionActive = targeting?.IsSelectingTarget == true;
            if (!selectionActive && HasGameplay && TopScope?.IsModal != true)
            {
                if (ControllerInput.Pressed(ControllerInput.GlobalMap, "LeftTrigger")) OpenPartySelector();
                else if ((!ControllerInput.UiActive || !precisePointer) && ControllerInput.Pressed(ControllerInput.GlobalMap, "Panels")) OpenPanelSelector();
            }
            var useUi = !HasGameplay || uiRequested || TopScope?.IsModal == true;
            SetContext(useUi);
            if (ControllerInput.Pressed(ControllerInput.GlobalMap, "Cursor") && keyboardView?.gameObject.activeInHierarchy != true)
            { precisePointer = !precisePointer; lastSelected = null; }
            ApplyPointerMode(useUi);
            TickEndTurnHold(useUi);
            if (useUi) TickUi(); else { TickWorld(); BlockWorldPointerButtons(); }
            if (cursor != null)
            {
                var worldPointer = ControllerInput.WorldActive
                    && (combat?.Session != null || targeting?.SelectedSkill != null || precisePointer);
                cursor.gameObject.SetActive(worldPointer || useUi && precisePointer);
                cursor.position = position;
            }
            UpdatePointer();
        }

        private void SetActive(bool value)
        {
            if (controllerActive == value) return;
            controllerActive = value;
            if (value)
            {
                activePad = Gamepad.current;
                Cursor.visible = false;
                changedCursor = true;
                if (scopes.Count > 0) uiRequested = true;
                if (pointer == null)
                {
                    pointer = InputSystem.AddDevice<Mouse>("GodmistControllerPointer");
                    InputState.Change(pointer, new MouseState { position = position });
                }
                else InputSystem.EnableDevice(pointer);
                lastSelected = null;
            }
            else
            {
                selectorMenu?.Hide();
                precisePointer = false;
                endTurnHold.Reset(); searchHold.Reset(); interactionPress = false;
                activePad = null;
                world.Disable(); ui.Disable();
                if (pointer != null) InputSystem.DisableDevice(pointer);
                module.move?.action?.Enable(); module.submit?.action?.Enable();
                if (cursor != null) cursor.gameObject.SetActive(false);
                if (changedCursor) { Cursor.visible = originalCursorVisible; changedCursor = false; }
                ReleaseCurrentFocus();
            }
        }

        private void SetContext(bool useUi)
        {
            if (useUi != ui.enabled)
            {
                precisePointer = false;
                endTurnHold.Reset(); searchHold.Reset(); interactionPress = false;
            }
            if (useUi && !ui.enabled && rememberedFocus != null && rememberedFocus.activeInHierarchy && WithinScope(rememberedFocus.transform))
                events.SetSelectedGameObject(rememberedFocus);
            if (!useUi && ui.enabled)
            {
                rememberedFocus = events.currentSelectedGameObject;
                events.SetSelectedGameObject(null);
                lastSelected = null;
                cursorActor = null;
            }
            if (useUi) { world.Disable(); ui.Enable(); module.move?.action?.Disable(); module.submit?.action?.Disable(); }
            else { ui.Disable(); world.Enable(); module.move?.action?.Disable(); module.submit?.action?.Disable(); }
            ApplyPointerMode(useUi);
        }

        private void ApplyPointerMode(bool useUi)
        {
            var action = (useUi ? ui : world).FindAction("PointerMove", true);
            if (precisePointer) action.Enable(); else action.Disable();
            var direct = world.FindAction("DirectInteraction", true);
            if (!useUi && !precisePointer && combat?.Session == null && targeting?.SelectedSkill == null) direct.Enable();
            else direct.Disable();
        }

        private void TickEndTurnHold(bool useUi)
        {
            var actor = combat?.Session?.ActiveUnit;
            if (actor != holdActor) { holdActor = actor; endTurnHold.Reset(); }
            var eligible = !useUi && targeting?.IsSelectingTarget != true && combat?.CanEndTurn() == true;
            if (endTurnHold.Tick(eligible, ControllerInput.Pressed(ControllerInput.GlobalMap, "Auxiliary"),
                ControllerInput.Held(ControllerInput.GlobalMap, "Auxiliary"), Time.unscaledDeltaTime)) combat.RequestEndTurn();
        }

        private void ReleaseCurrentFocus()
        {
            if (events == null) return;
            var selected = events.currentSelectedGameObject;
            if (selected != null) selected.GetComponent<ControllerNavigationItem>()?.ReleaseControllerFocus();
        }

        public void Focus(ControllerNavigationScope scope)
        {
            var state = scopes.FirstOrDefault(entry => entry.Scope == scope);
            if (scope == null || state == null || state == TopState) return;
            if (controllerActive && TopScope?.IsModal == true && !scope.transform.IsChildOf(TopScope.transform)) return;
            scopes.Remove(state); scopes.Add(state);
            if (!controllerActive) return;
            uiRequested = true;
            var selected = state.LastFocus;
            if (selected != null && selected.activeInHierarchy) events.SetSelectedGameObject(selected);
            else FocusFirst();
        }

        private void TickUi()
        {
            var freePointer = precisePointer;
            module.submit?.action?.Disable();
            // Route Submit once ourselves, to either the selected control or the optional precise pointer.
            if (ControllerInput.Pressed(ControllerInput.UiMap, "Cancel"))
            {
                if (freePointer) { precisePointer = false; lastSelected = null; ApplyPointerMode(true); return; }
                if (TopScope != null) TopScope.Cancel();
                else if (HasGameplay) uiRequested = false;
                return;
            }
            if (skillScope != null && TopScope == skillScope)
            {
                if (ControllerInput.Pressed(ControllerInput.UiMap, "Next")) skillBar.PageForController(events, 1);
                else if (ControllerInput.Pressed(ControllerInput.UiMap, "Previous")) skillBar.PageForController(events, -1);
            }
            else if (TopScope?.IsModal != true && (ControllerInput.Pressed(ControllerInput.UiMap, "Next") || ControllerInput.Pressed(ControllerInput.UiMap, "Previous")))
            {
                var windows = scopes.Where(s => s.Scope != null && !s.Scope.IsModal && s.Scope.GetComponent<UIWorkspaceWindow>() != null)
                    .OrderBy(s => s.OpeningOrder).Select(s => s.Scope).ToArray();
                if (windows.Length > 0)
                {
                    var index = System.Array.IndexOf(windows, TopScope);
                    var next = (index + (ControllerInput.Pressed(ControllerInput.UiMap, "Next") ? 1 : windows.Length - 1) + windows.Length) % windows.Length;
                    windows[next].GetComponent<UIWorkspaceWindow>().Focus();
                }
            }
            var selected = events.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy || !WithinScope(selected.transform)) FocusFirst();
            selected = events.currentSelectedGameObject;
            if (!freePointer) Navigate(selected);
            selected = events.currentSelectedGameObject;
            if (selected != null && TopState != null) TopState.LastFocus = selected;
            if (selected != null)
            {
                if (selected != lastSelected && !freePointer)
                {
                    lastSelected = selected;
                    ScrollIntoView(selected.transform);
                    selected.GetComponent<ControllerNavigationItem>()?.OnSelect(new BaseEventData(events));
                    var rect = selected.transform as RectTransform;
                    if (rect != null) position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                }
            }
            if (freePointer)
            {
                position += ControllerInput.Vector(ControllerInput.UiMap, "PointerMove") * (700 * Time.unscaledDeltaTime);
                position.x = Mathf.Clamp(position.x, 0, Screen.width);
                position.y = Mathf.Clamp(position.y, 0, Screen.height);
            }
            if (!freePointer && ControllerInput.Pressed(ControllerInput.UiMap, "Context"))
            {
                var target = selected;
                var data = new PointerEventData(events) { position = position, button = PointerEventData.InputButton.Right, clickCount = 1 };
                if (target != null) ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerClickHandler);
                return;
            }
            if (ControllerInput.Pressed(ControllerInput.UiMap, "Details"))
            { UIWorkspaceTooltipView.TryPinAtCurrentPointer(position); return; }
            if (!freePointer && ControllerInput.Pressed(ControllerInput.UiMap, "Confirm"))
            {
                if (selected != null)
                {
                    if (selected.TryGetComponent<TMPro.TMP_InputField>(out var field) && keyboardView != null) keyboardView.Open(field);
                    else if (selected.TryGetComponent<ControllerNavigationItem>(out var item) && item.UsesPointerSubmit) item.SubmitPointer();
                    else ExecuteEvents.Execute(selected, new BaseEventData(events), ExecuteEvents.submitHandler);
                }
            }
            requestedScroll = freePointer ? Vector2.zero : new Vector2(0, ControllerInput.Vector(ControllerInput.UiMap, "Scroll").y * 60);
        }

        private bool WithinScope(Transform target) => TopScope == null || target == TopScope.transform || target.IsChildOf(TopScope.transform);
        private bool CanNavigate(Selectable candidate)
        {
            if (!candidate.IsActive() || !candidate.IsInteractable() || !WithinScope(candidate.transform)
                || candidate.GetComponentInParent<DevConsoleView>() != null) return false;
            for (var parent = candidate.transform; parent != null; parent = parent.parent)
                if (parent.TryGetComponent<Canvas>(out var parentCanvas) && !parentCanvas.isActiveAndEnabled) return false;
            return true;
        }
        private void FocusFirst()
        {
            foreach (var selectable in Selectable.allSelectablesArray)
            {
                if (!CanNavigate(selectable)) continue;
                events.SetSelectedGameObject(selectable.gameObject);
                return;
            }
            events.SetSelectedGameObject(null);
        }

        private static void ScrollIntoView(Transform target)
        {
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null || scroll.content == null) return;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
            var rect = scroll.viewport.rect;
            var delta = Vector2.zero;
            if (scroll.vertical)
                delta.y = bounds.max.y > rect.yMax ? rect.yMax - bounds.max.y : bounds.min.y < rect.yMin ? rect.yMin - bounds.min.y : 0;
            if (scroll.horizontal)
                delta.x = bounds.max.x > rect.xMax ? rect.xMax - bounds.max.x : bounds.min.x < rect.xMin ? rect.xMin - bounds.min.x : 0;
            scroll.content.anchoredPosition += delta;
            if (delta != Vector2.zero) scroll.StopMovement();
        }

        private void Navigate(GameObject selected)
        {
            var value = ControllerInput.Vector(ControllerInput.UiMap, "Navigate");
            var direction = value.sqrMagnitude > .25f
                ? Mathf.Abs(value.x) > Mathf.Abs(value.y) ? new Vector2Int(value.x > 0 ? 1 : -1, 0) : new Vector2Int(0, value.y > 0 ? 1 : -1)
                : Vector2Int.zero;
            if (direction == Vector2Int.zero) { previousUiDirection = direction; return; }
            if (direction == previousUiDirection && Time.unscaledTime < nextUiStep) return;
            var initial = direction != previousUiDirection;
            previousUiDirection = direction;
            nextUiStep = Time.unscaledTime + (initial ? .35f : .12f);
            if (selected == null) { FocusFirst(); return; }
            var slider = selected.GetComponent<Slider>();
            var scrollbar = selected.GetComponent<Scrollbar>();
            var verticalSlider = slider != null && slider.direction is Slider.Direction.BottomToTop or Slider.Direction.TopToBottom;
            var verticalScrollbar = scrollbar != null && scrollbar.direction is Scrollbar.Direction.BottomToTop or Scrollbar.Direction.TopToBottom;
            if (slider != null && (verticalSlider ? direction.y != 0 : direction.x != 0)
                || scrollbar != null && (verticalScrollbar ? direction.y != 0 : direction.x != 0))
            {
                ExecuteEvents.Execute(selected, new AxisEventData(events)
                { moveVector = direction, moveDir = direction.x > 0 ? MoveDirection.Right : direction.x < 0 ? MoveDirection.Left : direction.y > 0 ? MoveDirection.Up : MoveDirection.Down }, ExecuteEvents.moveHandler);
                return;
            }
            Selectable best = null;
            var score = float.PositiveInfinity;
            var origin = ScreenCenter(selected.transform);
            foreach (var candidate in Selectable.allSelectablesArray)
            {
                if (candidate.gameObject == selected || !CanNavigate(candidate)) continue;
                var offset = ScreenCenter(candidate.transform) - origin;
                var forward = Vector2.Dot(offset, direction);
                if (forward <= 1) continue;
                var side = Mathf.Abs(Vector2.Dot(offset, new Vector2(-direction.y, direction.x)));
                var next = offset.magnitude + side * 2;
                if (next < score) { best = candidate; score = next; }
            }
            if (best != null) events.SetSelectedGameObject(best.gameObject);
        }

        private static Vector2 ScreenCenter(Transform transform) => RectTransformUtility.WorldToScreenPoint(null,
            transform is RectTransform rect ? rect.TransformPoint(rect.rect.center) : transform.position);

        private void TickWorld()
        {
            if (ControllerInput.Pressed(ControllerInput.WorldMap, "Map") && minimap != null)
            {
                minimap.SetOverviewOpen(true);
                if (minimap.IsOverviewOpen) return;
            }
            if (targeting?.IsSelectingTarget != true && (ControllerInput.Pressed(ControllerInput.WorldMap, "Next")
                || ControllerInput.Pressed(ControllerInput.WorldMap, "Previous")) && skillBar != null)
            {
                if (skillScope != null) skillScope.enabled = true;
                uiRequested = true;
                SetContext(true);
                if (!skillBar.FocusForController(events)) { ReturnToWorld(); SetContext(false); }
                return;
            }
            if (worldCamera == null) return;
            var actor = combat?.Session?.ActiveUnit ?? skillBar?.DisplayActor;
            if (actor != null && actor != cursorActor && unitViews != null && unitViews.TryGetWorldPosition(actor, out var actorPosition))
            {
                cursorActor = actor;
                selectedWorldUnit = null;
                position = worldCamera.WorldToScreenPoint(actorPosition);
            }
            if (ControllerInput.Pressed(ControllerInput.WorldMap, "NextTarget")) CycleWorldTarget(1);
            else if (ControllerInput.Pressed(ControllerInput.WorldMap, "PreviousTarget")) CycleWorldTarget(-1);
            if (combat?.Session == null && targeting?.SelectedSkill == null && !precisePointer)
            {
                var available = party?.GetControllerInteractions();
                if (selectedInteraction == null || available == null || !available.Contains(selectedInteraction))
                    selectedInteraction = available?.FirstOrDefault();
                var interaction = selectedInteraction;
                if (interaction is Component target) position = worldCamera.WorldToScreenPoint(target.transform.position);
                else if (party?.ActiveLeaderTransform != null) position = worldCamera.WorldToScreenPoint(party.ActiveLeaderTransform.position);
                BlockWorldPointerButtons();
                var confirm = global.FindAction("Confirm", true);
                if (confirm.WasPressedThisFrame()) { interactionPress = true; pressedInteraction = interaction; }
                if (searchHold.Tick(true, confirm.WasPressedThisFrame(), confirm.IsPressed(), Time.unscaledDeltaTime))
                {
                    interactionPress = false;
                    OpenInteractionSelector();
                }
                else if (interactionPress && confirm.WasReleasedThisFrame())
                {
                    interactionPress = false;
                    if (pressedInteraction is Component pressed && pressed != null && pressedInteraction.CanInteract()) pressedInteraction.Interact();
                    pressedInteraction = null;
                }
                // The direct interaction owns A, not the world-click handlers.
                return;
            }
            if (precisePointer)
            {
                position += ControllerInput.Vector(ControllerInput.WorldMap, "PointerMove") * (600 * Time.unscaledDeltaTime);
                position.x = Mathf.Clamp(position.x, 0, Screen.width);
                position.y = Mathf.Clamp(position.y, 0, Screen.height);
                return;
            }
            var move = ControllerInput.Movement;
            var direction = move.sqrMagnitude > .25f
                ? new Vector2Int(Mathf.Abs(move.x) > .35f ? move.x > 0 ? 1 : -1 : 0,
                    Mathf.Abs(move.y) > .35f ? move.y > 0 ? 1 : -1 : 0)
                : Vector2Int.zero;
            if (direction == Vector2Int.zero) { previousDirection = direction; return; }
            if (direction == previousDirection && Time.unscaledTime < nextStep) return;
            previousDirection = direction;
            nextStep = Time.unscaledTime + repeatDelay;
            var point = worldCamera.ScreenToWorldPoint(position);
            var size = movement != null ? movement.GridCellSize : 1;
            var anchor = Vector3.zero;
            if (actor != null && unitViews != null) unitViews.TryGetWorldPosition(actor, out anchor);
            point.x = anchor.x + Mathf.Round((point.x - anchor.x) / size) * size;
            point.y = anchor.y + Mathf.Round((point.y - anchor.y) / size) * size;
            var next = worldCamera.WorldToScreenPoint(point + (Vector3)((Vector2)direction * size));
            position = new Vector2(Mathf.Clamp(next.x, 0, Screen.width), Mathf.Clamp(next.y, 0, Screen.height));
        }

        public void ReturnToWorld()
        {
            if (skillScope != null) skillScope.enabled = false;
            uiRequested = false;
        }

        private void BlockWorldPointerButtons()
        {
            if (!ControllerInput.WorldActive) return;
            if (world.FindAction("DirectInteraction", true).enabled) return;
            var results = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = position }, results);
            if (!results.Any(r => r.module is GraphicRaycaster)) return;
            global.FindAction("Confirm", true).Disable();
            global.FindAction("Context", true).Disable();
        }

        private void UpdatePointer()
        {
            if (pointer == null) return;
            var precise = ControllerInput.UiActive && precisePointer;
            var delta = position - pointer.position.ReadValue();
            var canStart = CanStartPointerInteraction();
            var state = new MouseState
            {
                position = position, delta = delta,
                scroll = ControllerInput.UiActive ? requestedScroll : Vector2.zero
            };
            state = state.WithButton(MouseButton.Left,
                precise && (pointer.leftButton.isPressed || canStart) && ControllerInput.Held(ControllerInput.UiMap, "Confirm"));
            state = state.WithButton(MouseButton.Right,
                precise && (pointer.rightButton.isPressed || canStart) && ControllerInput.Held(ControllerInput.UiMap, "Context"));
            InputState.Change(pointer, state);
        }

        private bool CanStartPointerInteraction()
        {
            if (TopScope?.IsModal != true) return true;
            var results = new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events) { position = position }, results);
            return results.Count > 0 && WithinScope(results[0].gameObject.transform);
        }
    }
}
