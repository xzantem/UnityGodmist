using UnityEngine;

namespace Godmist.Views.Combat
{
    public class CombatCameraFramer : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField, Min(0f)] private float padding = 2f;
        [SerializeField, Min(1f)] private float minimumOrthographicSize = 5f;

        [Header("HUD Safe Area")]
        [SerializeField, Range(0f, 0.45f)] private float leftHudFraction;
        [SerializeField, Range(0f, 0.45f)] private float rightHudFraction;
        [SerializeField, Range(0f, 0.45f)] private float bottomHudFraction = 0.22f;
        [SerializeField, Range(0f, 0.45f)] private float topHudFraction = 0.12f;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = GetComponent<Camera>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        public void Frame(Bounds bounds)
        {
            if (worldCamera == null) return;

            var safeWidthFraction = Mathf.Max(
                0.1f,
                1f - leftHudFraction - rightHudFraction);

            var safeHeightFraction = Mathf.Max(
                0.1f,
                1f - bottomHudFraction - topHudFraction);

            var aspect = Mathf.Max(0.01f, worldCamera.aspect);
            var requiredHalfHeight = bounds.extents.y + padding;
            var requiredHalfWidth = bounds.extents.x + padding;

            worldCamera.orthographicSize = Mathf.Max(
                minimumOrthographicSize,
                requiredHalfHeight / safeHeightFraction,
                requiredHalfWidth / (aspect * safeWidthFraction));

            var safeViewportCenter = new Vector2(
                leftHudFraction + safeWidthFraction * 0.5f,
                bottomHudFraction + safeHeightFraction * 0.5f);

            var visibleHalfHeight = worldCamera.orthographicSize;
            var visibleHalfWidth = visibleHalfHeight * aspect;

            var cameraPosition = worldCamera.transform.position;

            worldCamera.transform.position = new Vector3(
                bounds.center.x
                    - (safeViewportCenter.x - 0.5f) * visibleHalfWidth * 2f,
                bounds.center.y
                    - (safeViewportCenter.y - 0.5f) * visibleHalfHeight * 2f,
                cameraPosition.z);
        }
    }
}