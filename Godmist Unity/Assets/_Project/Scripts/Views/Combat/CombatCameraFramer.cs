using System;
using System.Collections.Generic;
using Godmist.Runtime.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;
using UnityEngine;

namespace Godmist.Views.Combat
{
    public class CombatCameraFramer : MonoBehaviour
    {
        // Orthographic size is inversely proportional to zoom: 100% = size 5.
        private const float MinimumZoomSize = 2.5f; // 200%
        private const float MaximumZoomSize = 10f; // 50%
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(0f)] private float padding = 2f;
        [SerializeField, Min(1f)] private float minimumOrthographicSize = 5f;
        [Header("Controls")]
        [SerializeField, Min(0.1f)] private float keyboardPanSpeed = 15f;
        [SerializeField, Min(0.01f)] private float zoomSpeed = 0.15f;
        [SerializeField, Min(0.1f)] private float transitionSpeed = 8f;
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
        private Bounds arenaBounds, floorBounds;
        private ViewState target;
        private bool transitioning, dragging;
        private Vector3 previousMouse;
        private float lastAspect;
        public CombatSession FocusedSession => session;
        public int RememberedSessionCount => views.Count;

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

        public void Focus(CombatSession next, Bounds arena, Bounds floor, Func<BattleUnit, Vector3> positionResolver)
        {
            if (worldCamera == null) return;
            if (session != null) views[session] = Current;
            session = next;
            unitPosition = positionResolver;
            dragging = false;
            transitioning = false;
            if (next == null) return;
            arenaBounds = arena;
            floorBounds = floor;
            lastAspect = Aspect;
            if (views.TryGetValue(next, out var remembered)) MoveTo(remembered);
            else ShowArena();
        }

        public void Forget(CombatSession ended)
        {
            if (ended == null) return;
            if (session == ended) { session = null; unitPosition = null; transitioning = dragging = false; }
            views.Remove(ended);
        }

        public void ShowArena()
        {
            if (session == null) return;
            var zoom = FitZoom(arenaBounds);
            MoveTo(new ViewState { Position = Centered(arenaBounds.center, zoom), Zoom = zoom });
        }

        public void FocusActiveUnit()
        {
            if (session?.ActiveUnit == null || unitPosition == null || CanPresentUnit?.Invoke(session.ActiveUnit) == false) return;
            MoveTo(new ViewState { Position = Centered(unitPosition(session.ActiveUnit), worldCamera.orthographicSize), Zoom = worldCamera.orthographicSize });
        }

        public void NotifyTurnBegan(CombatSession source, BattleUnit unit)
        {
            if (source != session || unit == null || unitPosition == null || CanPresentUnit?.Invoke(unit) == false) return;
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
        private void MoveTo(ViewState state) { target = Constrain(state); transitioning = true; }
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
            transitioning = false;
            var state = Current;
            state.Zoom *= Mathf.Exp(-steps * zoomSpeed);
            Apply(Constrain(state));
        }
        public void TickTransition(float deltaTime)
        {
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
                Apply(Constrain(Current));
                if (transitioning) target = Constrain(target);
            }
            if (DevConsoleView.IsOpen || UIWorkspaceInputRouter.HasFocusedTextInput()) { dragging = false; return; }
            if (Input.GetKeyDown(KeyCode.F)) FocusActiveUnit();
            if (Input.GetKeyDown(KeyCode.Home)) ShowArena();
            var direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (direction.sqrMagnitude > 0f) Pan(direction.normalized * (keyboardPanSpeed * Time.unscaledDeltaTime));
            if (UIWorkspaceInputRouter.IsPointerOverUi()) { dragging = false; return; }
            if (Input.mouseScrollDelta.y != 0f) Zoom(Input.mouseScrollDelta.y);
            if (Input.GetMouseButtonDown(2)) { previousMouse = Input.mousePosition; dragging = true; }
            if (!Input.GetMouseButton(2)) dragging = false;
            if (dragging)
            {
                var delta = previousMouse - Input.mousePosition;
                var scale = worldCamera.orthographicSize * 2f / Mathf.Max(1f, worldCamera.pixelHeight);
                if (delta.sqrMagnitude > 0f) Pan((Vector2)delta * scale);
                previousMouse = Input.mousePosition;
            }
        }
        private void LateUpdate() => TickTransition(Time.unscaledDeltaTime);
        private void OnDisable() { dragging = false; transitioning = false; }
        private void OnDestroy() { views.Clear(); session = null; unitPosition = null; }
    }
}
