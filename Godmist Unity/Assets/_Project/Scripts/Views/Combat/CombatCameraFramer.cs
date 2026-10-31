using System;
using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using Godmist.Views.Exploration;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public class CombatCameraFramer : MonoBehaviour
    {
        // Orthographic size is inversely proportional to zoom: 100% = size 5.
        public const float MinimumZoomSize = 2.5f; // 200%
        public const float MaximumZoomSize = 10f; // 50%
        public const float DefaultZoomSpeed = .15f;
        public const float DefaultTransitionSpeed = 8f;
        public static float ScrollZoom(float size, float steps, float speed = DefaultZoomSpeed) =>
            Mathf.Clamp(size * Mathf.Exp(-steps * speed), MinimumZoomSize, MaximumZoomSize);
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(0f)] private float padding = 2f;
        [SerializeField, Min(1f)] private float minimumOrthographicSize = 5f;
        [Header("Controls")]
        [SerializeField, Min(0.1f)] private float keyboardPanSpeed = 15f;
        [SerializeField, Min(0.01f)] private float zoomSpeed = DefaultZoomSpeed;
        [SerializeField, Min(0.1f)] private float transitionSpeed = DefaultTransitionSpeed;
        [Header("HUD Safe Area")]
        [SerializeField, Range(0f, 0.45f)] private float leftHudFraction;
        [SerializeField, Range(0f, 0.45f)] private float rightHudFraction;
        [SerializeField, Range(0f, 0.45f)] private float bottomHudFraction = 0.22f;
        [SerializeField, Range(0f, 0.45f)] private float topHudFraction = 0.12f;

        private struct ViewState { public Vector3 Position; public float Zoom; }
        private readonly Dictionary<CombatSession, ViewState> views = new();
        private CombatSession session;
        private Func<BattleUnit, Vector3> unitPosition;
        public Func<BattleUnit, bool> CanPresentUnit { get; set; }
        private bool CanPresent(BattleUnit unit) => unit != null
            && !Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(unit)
            && CanPresentUnit?.Invoke(unit) != false;
        private Bounds arenaBounds, floorBounds;
        private ViewState target;
        private ViewState? actionRestore;
        private CombatSession actionSession;
        private BattleUnit actionUnit;
        private Func<Bounds> actionBounds;
        private Func<Bounds> participantBounds;
        private CombatSession movementSession;
        private BattleUnit movementUnit;
        private Func<Vector3> movementPosition;
        private float movementZoom;
        public Func<Camera, Rect> ActionSafeViewport { get; set; }
        private bool transitioning, dragging;
        private Vector3 previousMouse;
        private float lastAspect;
        public CombatSession FocusedSession => session;
        public int RememberedSessionCount => views.Count;
        public bool CanAcceptManualInput =>
            isActiveAndEnabled
            && session != null
            && worldCamera != null
            && !actionRestore.HasValue
            && movementPosition == null;

        private void Awake()
        {
            if (worldCamera == null) worldCamera = GetComponent<Camera>();
        }

        private Vector2 SafeSize => new(Mathf.Max(0.1f, 1f - leftHudFraction - rightHudFraction),
            Mathf.Max(0.1f, 1f - bottomHudFraction - topHudFraction));
        private Vector2 SafeCenter => new(leftHudFraction + SafeSize.x * 0.5f, bottomHudFraction + SafeSize.y * 0.5f);
        private float Aspect => Mathf.Max(0.01f, worldCamera.aspect);
        private ViewState Current => new() { Position = worldCamera.transform.position, Zoom = worldCamera.orthographicSize };
        private Vector3 Centered(Vector3 center, float zoom) => new(
            center.x - (SafeCenter.x - 0.5f) * zoom * 2f * Aspect,
            center.y - (SafeCenter.y - 0.5f) * zoom * 2f, worldCamera.transform.position.z);
        private float FitZoom(Bounds bounds) => Mathf.Clamp(Mathf.Max(minimumOrthographicSize,
            (bounds.extents.y + padding) / SafeSize.y, (bounds.extents.x + padding) / (Aspect * SafeSize.x)),
            MinimumZoomSize, MaximumZoomSize);

        public void Frame(Bounds bounds)
        {
            if (worldCamera == null) return;
            var zoom = FitZoom(bounds);
            Apply(new ViewState { Position = Centered(bounds.center, zoom), Zoom = zoom });
        }

        public void Focus(CombatSession next, Bounds arena, Bounds floor, Func<BattleUnit, Vector3> positionResolver, Func<Bounds> participantBoundsResolver = null)
        {
            if (worldCamera == null) return;
            if (session != null) views[session] = actionRestore ?? Current;
            actionRestore = null;
            actionSession = null;
            actionUnit = null;
            actionBounds = null;
            movementSession = null; movementPosition = null; movementUnit = null;
            session = next;
            participantBounds = participantBoundsResolver;
            unitPosition = positionResolver;
            dragging = false;
            transitioning = false;
            if (next == null) return;
            arenaBounds = arena;
            floorBounds = floor;
            lastAspect = Aspect;
            if (views.TryGetValue(next, out var remembered)) MoveTo(remembered);
            else if (next.IsSeamless && participantBounds == null) MoveTo(new ViewState { Position = Centered(arena.center, Current.Zoom), Zoom = Current.Zoom });
            else ShowArena();
        }

        public void Forget(CombatSession ended)
        {
            if (ended == null) return;
            if (actionSession == ended) { actionRestore = null; actionSession = null; actionUnit = null; actionBounds = null; }
            if (session == ended) { session = null; unitPosition = null; participantBounds = null; transitioning = dragging = false; }
            if (movementSession == ended) { movementSession = null; movementPosition = null; movementUnit = null; }
            views.Remove(ended);
        }

        public void ShowArena()
        {
            if (actionRestore.HasValue) return;
            if (session == null) return;
            if (participantBounds != null)
            {
                arenaBounds = participantBounds();
                var area = new Rect(leftHudFraction, bottomHudFraction, SafeSize.x, SafeSize.y);
                if (ActionSafeViewport != null)
                {
                    var hud = ActionSafeViewport(worldCamera);
                    area = Rect.MinMaxRect(Mathf.Max(area.xMin, hud.xMin), Mathf.Max(area.yMin, hud.yMin),
                        Mathf.Min(area.xMax, hud.xMax), Mathf.Min(area.yMax, hud.yMax));
                }
                var zoom = Mathf.Clamp(Mathf.Max(minimumOrthographicSize,
                    (arenaBounds.extents.y + .75f) / Mathf.Max(.1f, area.height),
                    (arenaBounds.extents.x + .75f) / (Aspect * Mathf.Max(.1f, area.width))), MinimumZoomSize, MaximumZoomSize);
                target = new ViewState { Position = new Vector3(
                    arenaBounds.center.x - (area.center.x - .5f) * zoom * 2 * Aspect,
                    arenaBounds.center.y - (area.center.y - .5f) * zoom * 2, worldCamera.transform.position.z), Zoom = zoom };
                transitioning = true;
            }
            else
            {
                var zoom = FitZoom(arenaBounds);
                MoveTo(new ViewState { Position = Centered(arenaBounds.center, zoom), Zoom = zoom });
            }
        }

        public void FocusUnit(BattleUnit unit)
        {
            if (actionRestore.HasValue) return;
            if (unit == null || unitPosition == null || !CanPresent(unit)) return;
            MoveTo(new ViewState { Position = Centered(unitPosition(unit), worldCamera.orthographicSize), Zoom = worldCamera.orthographicSize });
        }

        // Fog-only movement keeps its existing follow behavior. Invisibility
        // must never move the camera toward a hidden unit's current position.
        public void BeginMovementFocus(CombatSession source, Func<Vector3> position, BattleUnit unit = null)
        {
            if (source == null || source != session || worldCamera == null || position == null
                || Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(unit)) return;
            movementSession = source;
            movementUnit = unit;
            movementPosition = position;
            movementZoom = actionRestore?.Zoom ?? (transitioning ? target.Zoom : Current.Zoom);
            RefreshMovementFrame();
        }

        public void EndMovementFocus(CombatSession source)
        {
            if (source != movementSession) return;
            RefreshMovementFrame();
            movementSession = null;
            movementPosition = null;
            movementUnit = null;
        }

        private void RefreshMovementFrame()
        {
            if (movementPosition == null || actionRestore.HasValue
                || Godmist.Systems.Combat.StatusEffectSystem.IsInvisibleToPlayer(movementUnit)) return;
            target = new ViewState { Position = Centered(movementPosition(), movementZoom), Zoom = movementZoom };
            transitioning = true;
        }

        public void BeginActionFocus(CombatSession source, BattleUnit unit)
        {
            if (source != session || unit == null || worldCamera == null || unitPosition == null
                || !CanPresent(unit)) return;
            actionRestore ??= transitioning ? target : Current;
            actionSession = source;
            actionUnit = unit;
            actionBounds = null;
            dragging = false;
            MoveTo(new ViewState { Position = Centered(unitPosition(unit), Current.Zoom), Zoom = Current.Zoom });
        }

        public void EndActionFocus(CombatSession source)
        {
            if (source != actionSession || !actionRestore.HasValue) return;
            var restore = actionRestore.Value;
            actionRestore = null;
            actionSession = null;
            actionUnit = null;
            actionBounds = null;
            if (session == source) MoveTo(restore);
        }

        public void BeginActionFocus(CombatSession source, BattleUnit unit, Func<Bounds> bounds)
        {
            if (source != session || unit == null || worldCamera == null || unitPosition == null
                || !CanPresent(unit)) return;
            BeginActionFocus(source, unit);
            if (actionSession != source || actionUnit != unit) return;
            actionBounds = bounds;
            RefreshActionFrame();
        }

        private void RefreshActionFrame()
        {
            if (actionBounds == null) return;
            var area = new Rect(leftHudFraction, bottomHudFraction, SafeSize.x, SafeSize.y);
            if (ActionSafeViewport != null)
            {
                var hud = ActionSafeViewport(worldCamera);
                area = Rect.MinMaxRect(Mathf.Max(area.xMin, hud.xMin), Mathf.Max(area.yMin, hud.yMin),
                    Mathf.Min(area.xMax, hud.xMax), Mathf.Min(area.yMax, hud.yMax));
            }
            // Floating text is laid out in screen pixels. Keep a screen-space
            // margin even when a distant target requires a much wider world view.
            var horizontalInset = Mathf.Min(48f / Mathf.Max(1, worldCamera.pixelWidth), area.width * .08f);
            var topInset = Mathf.Min(64f / Mathf.Max(1, worldCamera.pixelHeight), area.height * .12f);
            var bottomInset = Mathf.Min(12f / Mathf.Max(1, worldCamera.pixelHeight), area.height * .025f);
            area = Rect.MinMaxRect(area.xMin + horizontalInset, area.yMin + bottomInset,
                area.xMax - horizontalInset, area.yMax - topInset);
            var bounds = actionBounds();
            // Fit the action instead of retaining an unnecessarily distant user
            // view. Long-range attacks can still exceed the manual zoom limit;
            // EndActionFocus restores the user's previous position and zoom.
            var zoom = Mathf.Max(MinimumZoomSize,
                bounds.extents.y / Mathf.Max(.1f, area.height),
                bounds.extents.x / (Aspect * Mathf.Max(.1f, area.width)));
            var center = area.center;
            // Floor/manual-pan constraints must not push a framed silhouette
            // back under the HUD at a room boundary.
            target = new ViewState { Position = new Vector3(
                bounds.center.x - (center.x - .5f) * zoom * 2 * Aspect,
                bounds.center.y - (center.y - .5f) * zoom * 2, worldCamera.transform.position.z), Zoom = zoom };
            transitioning = true;
        }

        public void FocusActiveUnit()
        {
            if (actionRestore.HasValue) return;
            if (session?.ActiveUnit == null || unitPosition == null || !CanPresent(session.ActiveUnit)) return;
            MoveTo(new ViewState { Position = Centered(unitPosition(session.ActiveUnit), worldCamera.orthographicSize), Zoom = worldCamera.orthographicSize });
        }

        public void NotifyTurnBegan(CombatSession source, BattleUnit unit)
        {
            if (actionRestore.HasValue || source != session || unit == null || unitPosition == null || !CanPresent(unit)) return;
            var state = transitioning ? target : Current;
            var position = unitPosition(unit);
            var x = (position.x - state.Position.x) / (state.Zoom * 2f * Aspect) + 0.5f;
            var y = (position.y - state.Position.y) / (state.Zoom * 2f) + 0.5f;
            if (x < leftHudFraction || x > 1f - rightHudFraction || y < bottomHudFraction || y > 1f - topHudFraction)
                MoveTo(new ViewState { Position = Centered(position, state.Zoom), Zoom = state.Zoom });
        }

        private ViewState Constrain(ViewState state)
        {
            state.Zoom = Mathf.Clamp(state.Zoom, MinimumZoomSize, MaximumZoomSize);
            var half = new Vector2(state.Zoom * Aspect * SafeSize.x, state.Zoom * SafeSize.y);
            var offset = new Vector2((SafeCenter.x - 0.5f) * state.Zoom * 2f * Aspect, (SafeCenter.y - 0.5f) * state.Zoom * 2f);
            var center = (Vector2)state.Position + offset;
            center.x = ClampAxis(center.x, floorBounds.min.x - padding, floorBounds.max.x + padding, half.x);
            center.y = ClampAxis(center.y, floorBounds.min.y - padding, floorBounds.max.y + padding, half.y);
            state.Position = new Vector3(center.x - offset.x, center.y - offset.y, worldCamera.transform.position.z);
            return state;
        }
        private static float ClampAxis(float value, float min, float max, float half) =>
            max - min <= 2f * half ? (min + max) * 0.5f : Mathf.Clamp(value, min + half, max - half);
        private void MoveTo(ViewState state) { target = participantBounds != null ? state : Constrain(state); transitioning = true; }
        private void Apply(ViewState state)
        {
            worldCamera.orthographicSize = state.Zoom;
            worldCamera.transform.position = state.Position;
        }

        public void Pan(Vector2 offset)
        {
            if (session == null) return;
            transitioning = false;
            var state = Current;
            state.Position += (Vector3)offset;
            Apply(Constrain(state));
        }
        public void Zoom(float steps)
        {
            if (session == null) return;
            var state = Current;
            state.Zoom = ScrollZoom(transitioning ? target.Zoom : state.Zoom, steps, zoomSpeed);
            MoveTo(state);
        }
        public void TickTransition(float deltaTime)
        {
            // Charge and teleport commit after SkillUsed. Follow the existing view
            // throughout the pose instead of retaining its pre-action position.
            if (actionRestore.HasValue && actionUnit != null && unitPosition != null
                && CanPresent(actionUnit))
            {
                if (actionBounds != null) RefreshActionFrame();
                else MoveTo(new ViewState { Position = Centered(unitPosition(actionUnit), Current.Zoom), Zoom = Current.Zoom });
            }
            RefreshMovementFrame();
            if (session == null || !transitioning) return;
            var blend = 1f - Mathf.Exp(-transitionSpeed * Mathf.Max(0f, deltaTime));
            Apply(new ViewState { Position = Vector3.Lerp(worldCamera.transform.position, target.Position, blend),
                Zoom = Mathf.Lerp(worldCamera.orthographicSize, target.Zoom, blend) });
            if (Vector3.Distance(worldCamera.transform.position, target.Position) < 0.01f
                && Mathf.Abs(worldCamera.orthographicSize - target.Zoom) < 0.01f)
            { Apply(target); transitioning = false; }
        }
        private void Update()
        {
            if (session == null || worldCamera == null) return;
            if (!Mathf.Approximately(lastAspect, Aspect))
            {
                lastAspect = Aspect;
                if (participantBounds != null) ShowArena();
                else { Apply(Constrain(Current)); if (transitioning) target = Constrain(target); }
            }
            if (actionRestore.HasValue || movementPosition != null || DevConsoleView.IsOpen || UIWorkspaceInputRouter.HasFocusedTextInput()) { dragging = false; return; }
            if (Input.GetKeyDown(KeyCode.F)) FocusActiveUnit();
            if (Godmist.Views.UI.ControllerInput.Pressed(Godmist.Views.UI.ControllerInput.WorldMap, "Center")) FocusActiveUnit();
            if (Godmist.Views.UI.ControllerInput.Zoom != 0) Zoom(Godmist.Views.UI.ControllerInput.Zoom * Time.unscaledDeltaTime * 5);
            if (Input.GetKeyDown(KeyCode.Home)) ShowArena();
            var direction = Vector2.ClampMagnitude(ExplorationKeyboardInput.ReadMovement() + Godmist.Views.UI.ControllerInput.Camera, 1f);
            if (direction.sqrMagnitude > 0f) Pan(direction.normalized * (keyboardPanSpeed * Time.unscaledDeltaTime));
            if (UIWorkspaceInputRouter.IsPointerOverUi()) { dragging = false; return; }
            if (Input.mouseScrollDelta.y != 0f) Zoom(Input.mouseScrollDelta.y);
            if (Input.GetMouseButtonDown(2)) { previousMouse = Godmist.Views.UI.ControllerInput.PointerPosition; dragging = true; }
            if (!Input.GetMouseButton(2)) dragging = false;
            if (dragging)
            {
                var delta = previousMouse - Godmist.Views.UI.ControllerInput.PointerPosition;
                var scale = worldCamera.orthographicSize * 2f / Mathf.Max(1f, worldCamera.pixelHeight);
                if (delta.sqrMagnitude > 0f) Pan((Vector2)delta * scale);
                previousMouse = Godmist.Views.UI.ControllerInput.PointerPosition;
            }
        }
        private void LateUpdate() => TickTransition(Time.unscaledDeltaTime);
        private void OnDisable()
        {
            if (actionRestore.HasValue && worldCamera != null) Apply(actionRestore.Value);
            actionRestore = null; actionSession = null; actionUnit = null; actionBounds = null;
            movementSession = null; movementPosition = null; movementUnit = null; dragging = false; transitioning = false;
        }
        private void OnDestroy() { views.Clear(); session = null; unitPosition = null; participantBounds = null; }
    }
}
