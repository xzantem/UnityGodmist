using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public abstract class DungeonObjectView : MonoBehaviour
    {
        private DungeonFloorMapView elevationView;
        private Renderer[] elevationRenderers;
        private readonly System.Collections.Generic.Dictionary<SpriteRenderer, Color> baseColors = new();
        public bool IsElevationVisible => elevationView == null || DungeonObject == null
            || (!elevationView.IsSurfaceOccluded(new DungeonSurfacePosition(DungeonObject.Position)) && elevationView.IsObjectCurrentlySeen(DungeonObject));
        protected bool PresentationState(bool current) => elevationView?.FogFloor == null || DungeonObject == null
            || elevationView.FogFloor.Fog.DebugFogDisabled
            ? current : elevationView.KnownObject(DungeonObject)?.State ?? false;
        protected virtual void RefreshFogVisual() { }
        public void BindElevationView(DungeonFloorMapView view)
        { elevationView = view; elevationRenderers = GetComponentsInChildren<Renderer>(true); }
        protected virtual void LateUpdate()
        {
            if (elevationRenderers == null) return;
            foreach (var item in elevationRenderers)
                if (item is SpriteRenderer sprite)
                {
                    if (!baseColors.TryGetValue(sprite, out var color)) baseColors.Add(sprite, color = sprite.color);
                    sprite.color = color;
                }
            RefreshFogVisual();
            var remembered = elevationView?.FogFloor != null && DungeonObject != null && !elevationView.IsObjectCurrentlySeen(DungeonObject);
            var known = DungeonObject == null ? null : elevationView?.KnownObject(DungeonObject);
            var show = elevationView?.FogFloor == null || elevationView.FogFloor.Fog.DebugFogDisabled
                ? DungeonObject?.IsActive == true && IsElevationVisible
                : DungeonObject != null && known?.Active == true
                    && (elevationView.IsObjectCurrentlySeen(DungeonObject)
                        || (DungeonObject is DoorDungeonObjectInstance door
                            ? elevationView.EdgeKnowledge(new DungeonSurfaceEdge(door.Edge)) != DungeonKnowledge.Unexplored
                            : elevationView.Knowledge(new DungeonTraversalPosition(new DungeonSurfacePosition(DungeonObject.Position))) != DungeonKnowledge.Unexplored))
                    && !elevationView.IsSurfaceOccluded(new DungeonSurfacePosition(DungeonObject.Position));
            foreach (var renderer in elevationRenderers)
                if (renderer != null)
                {
                    renderer.forceRenderingOff = !show;
                    if (remembered && renderer is SpriteRenderer sprite) sprite.color = DungeonFloorMapView.Dim(sprite.color);
                }
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
