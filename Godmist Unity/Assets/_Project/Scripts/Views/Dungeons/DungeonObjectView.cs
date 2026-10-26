using Godmist.Runtime.Dungeons;
using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public abstract class DungeonObjectView : MonoBehaviour
    {
        private DungeonFloorMapView elevationView;
        private Renderer[] elevationRenderers;
        private readonly System.Collections.Generic.Dictionary<SpriteRenderer, Color> baseColors = new();
        private UnityEngine.Rendering.SortingGroup depthSortingGroup;
        private bool depthSortingGroupCreated;
        private bool depthSortingGroupStateCaptured;
        private bool depthSortingGroupOriginalEnabled;
        private int depthSortingGroupOriginalLayerID;
        private int depthSortingGroupOriginalOrder;
        private bool cameraPresentationCulled;
        private bool unknownPresentationHidden;
        public int HiddenPresentationSkipCount { get; private set; }
        public bool IsElevationVisible => elevationView == null || DungeonObject == null
            || (!elevationView.IsSurfaceOccluded(new DungeonSurfacePosition(DungeonObject.Position)) && elevationView.IsObjectCurrentlySeen(DungeonObject));
        public bool IsLightingPresentationVisible => IsElevationVisible
            || DungeonObject is ObstacleDungeonObjectInstance && elevationView?.FogFloor != null
                && elevationView.KnownObject(DungeonObject)?.Active == true
                && !elevationView.IsSurfaceOccluded(new DungeonSurfacePosition(DungeonObject.Position));
        protected bool PresentationState(bool current) => elevationView?.FogFloor == null || DungeonObject == null
            || elevationView.FogFloor.Fog.DebugFogDisabled
            ? current : elevationView.KnownObject(DungeonObject)?.State ?? false;
        protected virtual void RefreshFogVisual() { }
        internal void ReserveDepthBaseline(DungeonFloorMapView view) => ResolveDepthSortingOrder(view);
        public void BindElevationView(DungeonFloorMapView view)
        {
            elevationView = view;
            elevationRenderers = GetComponentsInChildren<Renderer>(true);
            unknownPresentationHidden = false;
            RefreshDepthSorting();
        }
        private static readonly Unity.Profiling.ProfilerMarker ObjectLateSample = new("Godmist.Dungeon.ObjectLate");
        protected virtual void LateUpdate()
        {
            using var sample = ObjectLateSample.Auto();
            if (elevationRenderers == null)
            {
                RefreshDepthSorting();
                return;
            }
            // No camera, tint or sorting work is needed after unknown/inactive
            // fog knowledge has already hidden every renderer. Read knowledge
            // live: discovery, debug fog and external renderer changes wake it.
            bool unknown = elevationView?.FogFloor != null && DungeonObject != null
                && !elevationView.FogFloor.Fog.DebugFogDisabled
                && elevationView.KnownObject(DungeonObject)?.Active != true;
            if (unknown && unknownPresentationHidden)
            {
                bool hidden = true;
                foreach (var renderer in elevationRenderers)
                    if (renderer != null && !renderer.forceRenderingOff) { hidden = false; break; }
                if (hidden) { HiddenPresentationSkipCount++; return; }
            }
            if (elevationView != null)
            {
                var inCamera = elevationView.AreAnyInPresentationCamera(elevationRenderers);
                if (!inCamera)
                {
                    if (!cameraPresentationCulled)
                        foreach (var renderer in elevationRenderers)
                            if (renderer != null && !renderer.forceRenderingOff) renderer.forceRenderingOff = true;
                    if (depthSortingGroup != null && depthSortingGroup.enabled) depthSortingGroup.enabled = false;
                    cameraPresentationCulled = true;
                    unknownPresentationHidden = unknown;
                    return;
                }
            }
            cameraPresentationCulled = false;
            foreach (var item in elevationRenderers)
                if (item is SpriteRenderer sprite)
                {
                    if (!baseColors.TryGetValue(sprite, out var color)) baseColors.Add(sprite, color = sprite.color);
                    if (sprite.color != color) sprite.color = color;
                }
            RefreshFogVisual();
            var remembered = elevationView?.FogFloor != null && DungeonObject != null && !elevationView.IsObjectCurrentlySeen(DungeonObject);
            var known = DungeonObject == null ? null : elevationView?.KnownObject(DungeonObject);
            var show = elevationView?.FogFloor == null || elevationView.FogFloor.Fog.DebugFogDisabled
                ? DungeonObject?.IsActive == true && IsElevationVisible
                : DungeonObject != null && known?.Active == true
                    && (DungeonObject is ObstacleDungeonObjectInstance || elevationView.IsObjectCurrentlySeen(DungeonObject)
                        || (DungeonObject is DoorDungeonObjectInstance door
                            ? elevationView.EdgeKnowledge(new DungeonSurfaceEdge(door.Edge)) != DungeonKnowledge.Unexplored
                            : elevationView.Knowledge(new DungeonTraversalPosition(new DungeonSurfacePosition(DungeonObject.Position))) != DungeonKnowledge.Unexplored))
                    && !elevationView.IsSurfaceOccluded(new DungeonSurfacePosition(DungeonObject.Position));
            foreach (var renderer in elevationRenderers)
                if (renderer != null)
                {
                    if (renderer is SpriteRenderer sprite && elevationView != null)
                        elevationView.PresentFogSprite(sprite, sprite.color, show,
                            remembered ? elevationView.RememberedFogBrightness : 1);
                    else renderer.forceRenderingOff = !show;
                }

            // Derived object views may adjust their child renderer orders while
            // updating their presentation. The root group owns only the
            // map-relative band, so those local body/outline relationships stay
            // intact while the group is refreshed after the fog pass.
            RefreshDepthSorting();
            unknownPresentationHidden = unknown && !show;
        }

        private void RefreshDepthSorting()
        {
            if (elevationView == null || elevationRenderers == null || elevationRenderers.Length == 0)
            {
                RestoreDepthSortingGroup();
                return;
            }

            EnsureDepthSortingGroup();
            var anyShown = false;
            foreach (var renderer in elevationRenderers)
                if (renderer != null && renderer.enabled && !renderer.forceRenderingOff) { anyShown = true; break; }
            if (!anyShown) { if (depthSortingGroup.enabled) depthSortingGroup.enabled = false; return; }
            var layerID = elevationRenderers[0] != null
                ? elevationRenderers[0].sortingLayerID
                : depthSortingGroupOriginalLayerID;
            if (!depthSortingGroup.enabled) depthSortingGroup.enabled = true;
            if (depthSortingGroup.sortingLayerID != layerID) depthSortingGroup.sortingLayerID = layerID;
            var order = ResolveDepthSortingOrder(elevationView);
            if (depthSortingGroup.sortingOrder != order) depthSortingGroup.sortingOrder = order;
        }

        protected virtual int ResolveDepthSortingOrder(DungeonFloorMapView view)
            => view.GetDungeonObjectDepthSortingOrder();

        private void EnsureDepthSortingGroup()
        {
            if (depthSortingGroup == null)
            {
                depthSortingGroup = GetComponent<UnityEngine.Rendering.SortingGroup>();
                if (depthSortingGroup == null)
                {
                    depthSortingGroup = gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    depthSortingGroupCreated = true;
                }
            }

            if (depthSortingGroupStateCaptured)
                return;

            depthSortingGroupOriginalEnabled = depthSortingGroup.enabled;
            depthSortingGroupOriginalLayerID = depthSortingGroup.sortingLayerID;
            depthSortingGroupOriginalOrder = depthSortingGroup.sortingOrder;
            depthSortingGroupStateCaptured = true;
        }

        private void RestoreDepthSortingGroup()
        {
            if (depthSortingGroup == null || !depthSortingGroupStateCaptured)
                return;

            if (depthSortingGroupCreated)
            {
                depthSortingGroup.enabled = false;
                return;
            }

            depthSortingGroup.enabled = depthSortingGroupOriginalEnabled;
            depthSortingGroup.sortingLayerID = depthSortingGroupOriginalLayerID;
            depthSortingGroup.sortingOrder = depthSortingGroupOriginalOrder;
        }

        private void OnDestroy()
        {
            RestoreDepthSortingGroup();
        }

        public string InstanceId { get; private set; } = string.Empty;

        protected DungeonObjectInstance DungeonObject { get; private set; }
        public string ObjectInstanceId => DungeonObject?.InstanceId;

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
