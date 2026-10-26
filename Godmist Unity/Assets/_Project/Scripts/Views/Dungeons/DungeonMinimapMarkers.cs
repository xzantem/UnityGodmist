using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.Dungeons
{
    // Dynamic geometry only. The graphic, its bounds and style are authored in the minimap prefab.
    public sealed class DungeonMinimapMarkers : MaskableGraphic
    {
        [SerializeField] private float markerSize = 5;
        private readonly List<(Vector2 Position, Color Color)> markers = new();
        private readonly List<(Vector2 Position, Color Color)> publishedMarkers = new();
        private bool hasPublishedMarkers;
        private float publishedMarkerSize;
        private bool hasCameraFrame, publishedHasCameraFrame;
        private readonly Vector2[] cameraFrame = new Vector2[4];
        private readonly Vector2[] publishedCameraFrame = new Vector2[4];
        private const float CameraFrameWidth = 1.5f;
        public void ClearMarkers() => markers.Clear();
        public void ClearCameraFrame() => hasCameraFrame = false;
        public void SetCameraFrame(Vector2 bottomLeft, Vector2 bottomRight, Vector2 topRight, Vector2 topLeft)
        {
            hasCameraFrame = true;
            cameraFrame[0] = bottomLeft; cameraFrame[1] = bottomRight;
            cameraFrame[2] = topRight; cameraFrame[3] = topLeft;
        }
        public void AddMarker(Vector2 position, Color markerColor) => markers.Add((position, markerColor));
        public void Refresh()
        {
            var changed = !hasPublishedMarkers || publishedMarkerSize != markerSize
                || markers.Count != publishedMarkers.Count || hasCameraFrame != publishedHasCameraFrame;
            for (int i = 0; !changed && hasCameraFrame && i < 4; i++)
                changed = !cameraFrame[i].Equals(publishedCameraFrame[i]);
            for (int i = 0; !changed && i < markers.Count; i++)
                changed = !markers[i].Equals(publishedMarkers[i]);
            if (!changed) return;
            publishedMarkers.Clear(); publishedMarkers.AddRange(markers);
            publishedMarkerSize = markerSize; hasPublishedMarkers = true;
            publishedHasCameraFrame = hasCameraFrame;
            if (hasCameraFrame) System.Array.Copy(cameraFrame, publishedCameraFrame, 4);
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var bounds = rectTransform.rect;
            if (hasCameraFrame)
                for (int i = 0; i < 4; i++) AddCameraEdge(vh, bounds.center + cameraFrame[i], bounds.center + cameraFrame[(i + 1) % 4], bounds);
            foreach (var marker in markers)
            {
                var center = bounds.center + marker.Position;
                var min = Vector2.Max(bounds.min, center - Vector2.one * markerSize * .5f);
                var max = Vector2.Min(bounds.max, center + Vector2.one * markerSize * .5f);
                if (min.x >= max.x || min.y >= max.y) continue;
                var index = vh.currentVertCount;
                vh.AddVert(new Vector3(min.x, min.y), marker.Color, Vector2.zero);
                vh.AddVert(new Vector3(min.x, max.y), marker.Color, Vector2.zero);
                vh.AddVert(new Vector3(max.x, max.y), marker.Color, Vector2.zero);
                vh.AddVert(new Vector3(max.x, min.y), marker.Color, Vector2.zero);
                vh.AddTriangle(index, index+1, index+2); vh.AddTriangle(index, index+2, index+3);
            }
        }

        private static void AddCameraEdge(VertexHelper vh, Vector2 from, Vector2 to, Rect bounds)
        {
            var delta = to - from;
            float first = 0, last = 1;
            // Clip real edges separately: clamping the rectangle first would
            // invent a border when the camera extends beyond the minimap.
            if (!Clip(-delta.x, from.x - bounds.xMin, ref first, ref last)
                || !Clip(delta.x, bounds.xMax - from.x, ref first, ref last)
                || !Clip(-delta.y, from.y - bounds.yMin, ref first, ref last)
                || !Clip(delta.y, bounds.yMax - from.y, ref first, ref last)) return;
            to = from + delta * last; from += delta * first;
            if ((to - from).sqrMagnitude < .000001f) return;
            var normal = new Vector2(-delta.y, delta.x).normalized * (CameraFrameWidth * .5f);
            int index = vh.currentVertCount;
            Add(from - normal); Add(from + normal); Add(to + normal); Add(to - normal);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
            void Add(Vector2 point) => vh.AddVert(Vector2.Min(bounds.max, Vector2.Max(bounds.min, point)), Color.white, Vector2.zero);
        }

        private static bool Clip(float direction, float distance, ref float first, ref float last)
        {
            if (Mathf.Abs(direction) < .000001f) return distance >= 0;
            float ratio = distance / direction;
            if (direction < 0) { if (ratio > last) return false; first = Mathf.Max(first, ratio); }
            else { if (ratio < first) return false; last = Mathf.Min(last, ratio); }
            return true;
        }
    }
}
