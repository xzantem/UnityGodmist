using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public abstract class DungeonObjectView : MonoBehaviour
    {
        private DungeonFloorMapView elevationView;
        private Renderer[] elevationRenderers;
        public bool IsElevationVisible => elevationView == null || DungeonObject == null
            || !elevationView.IsSurfaceOccluded(new DungeonSurfacePosition(DungeonObject.Position));
        public void BindElevationView(DungeonFloorMapView view)
        { elevationView = view; elevationRenderers = GetComponentsInChildren<Renderer>(true); }
        protected virtual void LateUpdate()
        {
            if (elevationRenderers == null) return;
            foreach (var renderer in elevationRenderers)
                if (renderer != null) renderer.forceRenderingOff = !IsElevationVisible;
        }

        public string InstanceId { get; private set; } = string.Empty;

        protected DungeonObjectInstance DungeonObject { get; private set; }

        protected bool TryBind(
            DungeonObjectInstance dungeonObject,
            Vector3 worldPosition)
        {
            if (dungeonObject == null) return false;

            DungeonObject = dungeonObject;
            InstanceId = dungeonObject.InstanceId;
            transform.position = worldPosition;

            return true;
        }

        public static T CreateFallback<T>(Transform parent, string objectName)
            where T : DungeonObjectView
        {
            var gameObject = new GameObject(objectName);

            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateFallbackSprite();
            renderer.sortingOrder = 6;
            renderer.color = Color.magenta;

            return gameObject.AddComponent<T>();
        }

        private static Sprite CreateFallbackSprite()
        {
            var texture = new Texture2D(1, 1)
            {
                name = "DungeonObjectFallbackTexture"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
        }
    }
}
