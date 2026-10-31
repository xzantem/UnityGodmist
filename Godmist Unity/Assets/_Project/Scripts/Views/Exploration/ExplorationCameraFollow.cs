using UnityEngine;
using Godmist.Views.Combat;
using Godmist.Views.UI;
using Godmist.Views.UI.Workspace;

namespace Godmist.Views.Exploration
{
    public class ExplorationCameraFollow : MonoBehaviour
    {
        private enum CameraMode
        {
            FollowLeader,
            FreeRoam
        }

        [SerializeField] private ExplorationPartyController partyController;
        [SerializeField] private BoxCollider2D cameraBounds;
        [SerializeField] private float followSpeed = 8f;
        [SerializeField] private KeyCode toggleModeKey = KeyCode.F;
        [SerializeField] private Vector3 cameraOffset = new(0f, 0f, -10f);
        [SerializeField] private float keyboardPanSpeed = 10f;
        [SerializeField] private InventoryEquipmentPanelView inventoryEquipmentPanel;

        private Camera worldCamera;
        private CameraMode currentMode = CameraMode.FollowLeader;
        private Vector3 previousMouseWorldPosition;
        private bool isPanning;
        public const float RightDragThreshold = 6f;
        private Vector2 panPressScreen;
        private bool panDragged;
        public bool IsFollowingLeader => currentMode == CameraMode.FollowLeader;

        private bool combatSuspended;
        private float targetZoom;
        private bool zooming;
        public void SetCombatSuspended(bool suspended)
        {
            if (combatSuspended == suspended) return;
            combatSuspended = suspended;
            zooming = false;
            if (worldCamera != null) targetZoom = worldCamera.orthographicSize;
            isPanning = false;
            enabled = !suspended;
        }

        public void Zoom(float steps)
        {
            if (combatSuspended || worldCamera == null || !worldCamera.orthographic || steps == 0f) return;
            targetZoom = CombatCameraFramer.ScrollZoom(zooming ? targetZoom : worldCamera.orthographicSize, steps);
            zooming = true;
        }
        public void TickZoom(float deltaTime)
        {
            if (combatSuspended || !zooming || worldCamera == null) return;
            var blend = 1f - Mathf.Exp(-CombatCameraFramer.DefaultTransitionSpeed * Mathf.Max(0f, deltaTime));
            worldCamera.orthographicSize = Mathf.Lerp(worldCamera.orthographicSize, targetZoom, blend);
            if (Mathf.Abs(worldCamera.orthographicSize-targetZoom) < .01f)
            { worldCamera.orthographicSize = targetZoom; zooming = false; }
            transform.position = ClampToBounds(transform.position);
        }

        private void Awake()
        {
            worldCamera = GetComponent<Camera>();
        }

        public void SetWorldBounds(Bounds worldBounds)
        {
            if (cameraBounds == null)
            {
                return;
            }

            cameraBounds.transform.position = new Vector3(
                worldBounds.center.x,
                worldBounds.center.y,
                cameraBounds.transform.position.z);

            cameraBounds.size = new Vector2(
                worldBounds.size.x,
                worldBounds.size.y);
        }

        public void SetCameraBounds(BoxCollider2D bounds)
        {
            cameraBounds = bounds;
        }

        public void CenterOn(Vector3 position, bool followLeader = false)
        {
            currentMode = followLeader ? CameraMode.FollowLeader : CameraMode.FreeRoam;
            isPanning = false;
            transform.position = ClampToBounds(position + cameraOffset);
        }

        public void SnapToLeader()
        {
            if (partyController == null
                || partyController.ActiveLeaderTransform == null)
            {
                return;
            }

            var targetPosition = partyController.ActiveLeaderTransform.position
                                + cameraOffset;

            transform.position = ClampToBounds(targetPosition);
        }

        private void Update()
        {
            if(partyController!=null && partyController.IsMapOverviewBlockingInput){isPanning=false;return;}
            if (DevConsoleView.IsOpen || UIWorkspaceInputRouter.HasFocusedTextInput()) return;

            if (inventoryEquipmentPanel != null && inventoryEquipmentPanel.IsOpen)
            {
                isPanning = false;
                return;
            }
            if (Input.GetKeyDown(toggleModeKey))
            {
                currentMode = currentMode == CameraMode.FollowLeader
                    ? CameraMode.FreeRoam
                    : CameraMode.FollowLeader;
            }

            if (ControllerInput.Pressed(ControllerInput.WorldMap, "Center") && partyController?.ActiveLeaderTransform != null)
                CenterOn(partyController.ActiveLeaderTransform.position, true);
            if (ControllerInput.Camera.sqrMagnitude > .01f) currentMode = CameraMode.FreeRoam;
            if (!UIWorkspaceInputRouter.IsPointerOverUi()) Zoom(Input.mouseScrollDelta.y + ControllerInput.Zoom * Time.unscaledDeltaTime * 5);
            HandleFreeRoamPanning();
        }

        private static readonly Unity.Profiling.ProfilerMarker CameraLateSample = new("Godmist.Exploration.CameraLate");
        private void LateUpdate()
        {
            using var sample = CameraLateSample.Auto();
            TickZoom(Time.unscaledDeltaTime);
            if (currentMode != CameraMode.FollowLeader) return;
            if (partyController == null || partyController.ActiveLeaderTransform == null) return;

            var targetPosition = partyController.ActiveLeaderTransform.position + cameraOffset;
            var clampedPosition = ClampToBounds(targetPosition);

            transform.position = Vector3.Lerp(
                transform.position,
                clampedPosition,
                followSpeed * Time.deltaTime);
        }

        [SerializeField] private Godmist.Views.Combat.CombatUnitInspectController unitInteractions;
        private void HandleFreeRoamPanning()
        {
            if (currentMode != CameraMode.FreeRoam || worldCamera == null)
            {
                isPanning = false;
                return;
            }

            var keyboardDirection = Vector2.ClampMagnitude(ExplorationKeyboardInput.ReadMovement() + ControllerInput.Camera, 1f);

            if (keyboardDirection.sqrMagnitude > 1f)
            {
                keyboardDirection.Normalize();
            }

            if (keyboardDirection.sqrMagnitude > 0f)
            {
                transform.position = ClampToBounds(
                    transform.position + (Vector3)(keyboardDirection * keyboardPanSpeed * Time.deltaTime));
            }

            if (unitInteractions != null && unitInteractions.ConsumedSecondaryClickThisFrame) { isPanning = false; return; }
            if ((Input.GetMouseButtonDown(1) || Godmist.Views.UI.ControllerInput.Context)
                && !UIWorkspaceInputRouter.IsPointerOverUi()
                && !UIWorkspaceTooltipView.IsPointerOverVisibleTooltip(Godmist.Views.UI.ControllerInput.PointerPosition))
            {
                previousMouseWorldPosition = GetMouseWorldPosition();
                panPressScreen = Godmist.Views.UI.ControllerInput.PointerPosition;
                panDragged = false;
                isPanning = true;
            }

            if (Input.GetMouseButtonUp(1))
            {
                isPanning = false;
            }

            if (!isPanning
                || !Input.GetMouseButton(1)
                || UIWorkspaceInputRouter.IsPointerOverUi()
                || UIWorkspaceTooltipView.IsPointerOverVisibleTooltip(Godmist.Views.UI.ControllerInput.PointerPosition))
            {
                return;
            }

            var currentMouseWorldPosition = GetMouseWorldPosition();
            if (!panDragged)
            {
                if (((Vector2)Godmist.Views.UI.ControllerInput.PointerPosition - panPressScreen).sqrMagnitude <= RightDragThreshold * RightDragThreshold) return;
                panDragged = true;
            }
            var dragOffset = previousMouseWorldPosition - currentMouseWorldPosition;

            transform.position = ClampToBounds(transform.position + dragOffset);
            previousMouseWorldPosition = currentMouseWorldPosition;
        }

        private Vector3 GetMouseWorldPosition()
        {
            var mousePosition = Godmist.Views.UI.ControllerInput.PointerPosition;
            mousePosition.z = -transform.position.z;

            return worldCamera.ScreenToWorldPoint(mousePosition);
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            position.z = cameraOffset.z;

            if (cameraBounds == null || worldCamera == null) return position;

            var bounds = cameraBounds.bounds;
            var halfHeight = worldCamera.orthographicSize;
            var halfWidth = halfHeight * worldCamera.aspect;

            var minX = bounds.min.x + halfWidth;
            var maxX = bounds.max.x - halfWidth;
            var minY = bounds.min.y + halfHeight;
            var maxY = bounds.max.y - halfHeight;

            position.x = minX > maxX
                ? bounds.center.x
                : Mathf.Clamp(position.x, minX, maxX);

            position.y = minY > maxY
                ? bounds.center.y
                : Mathf.Clamp(position.y, minY, maxY);

            return position;
        }
    }
}
