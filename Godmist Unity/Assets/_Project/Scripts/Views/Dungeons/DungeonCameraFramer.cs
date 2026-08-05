using UnityEngine;

namespace Godmist.Views.Dungeons
{
    [RequireComponent(typeof(Camera))]
    public class DungeonCameraFramer : MonoBehaviour
    {
        [SerializeField] private float padding = 4f;

        private Camera worldCamera;

        private void Awake()
        {
            worldCamera = GetComponent<Camera>();
        }

        public void Frame(Bounds bounds)
        {
            if (worldCamera == null) return;

            var halfHeight = bounds.extents.y + padding;
            var halfWidth = bounds.extents.x + padding;

            worldCamera.orthographicSize = Mathf.Max(
                halfHeight,
                halfWidth / worldCamera.aspect);

            transform.position = new Vector3(
                bounds.center.x,
                bounds.center.y,
                transform.position.z);
        }
    }
}