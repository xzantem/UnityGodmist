using UnityEngine;
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
        public bool IsFollowingLeader => currentMode == CameraMode.FollowLeader;

        private bool combatSuspended;
        private float explorationZoom;
        public void SetCombatSuspended(bool suspended)
        {
            if (combatSuspended == suspended) return;
            if (worldCamera != null)
            {
                if (suspended) explorationZoom = worldCamera.orthographicSize;
                else worldCamera.orthographicSize = explorationZoom;
            }
            combatSuspended = suspended;
            isPanning = false;
            enabled = !suspended;
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
            if (DevConsoleView.IsOpen) return;

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

            HandleFreeRoamPanning();
        }

        private void LateUpdate()
        {
            if (currentMode != CameraMode.FollowLeader) return;
            if (partyController == null || partyController.ActiveLeaderTransform == null) return;

            var targetPosition = partyController.ActiveLeaderTransform.position + cameraOffset;
            var clampedPosition = ClampToBounds(targetPosition);

            transform.position = Vector3.Lerp(
                transform.position,
                clampedPosition,
                followSpeed * Time.deltaTime);
        }

        private void HandleFreeRoamPanning()
        {
            if (currentMode != CameraMode.FreeRoam || worldCamera == null)
            {
                isPanning = false;
                return;
            }

            var keyboardDirection = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (keyboardDirection.sqrMagnitude > 1f)
            {
                keyboardDirection.Normalize();
            }

            if (keyboardDirection.sqrMagnitude > 0f)
            {
                transform.position = ClampToBounds(
                    transform.position + (Vector3)(keyboardDirection * keyboardPanSpeed * Time.deltaTime));
            }

            if (Input.GetMouseButtonDown(1) && !UIWorkspaceInputRouter.IsPointerOverUi())
            {
                previousMouseWorldPosition = GetMouseWorldPosition();
                isPanning = true;
            }

            if (Input.GetMouseButtonUp(1))
            {
                isPanning = false;
            }

            if (!isPanning
                || !Input.GetMouseButton(1)
                || UIWorkspaceInputRouter.IsPointerOverUi())
            {
                return;
            }

            var currentMouseWorldPosition = GetMouseWorldPosition();
            var dragOffset = previousMouseWorldPosition - currentMouseWorldPosition;

            transform.position = ClampToBounds(transform.position + dragOffset);
            previousMouseWorldPosition = currentMouseWorldPosition;
        }

        private Vector3 GetMouseWorldPosition()
        {
            var mousePosition = Input.mousePosition;
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
