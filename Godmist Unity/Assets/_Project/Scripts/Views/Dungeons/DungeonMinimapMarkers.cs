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
        public void ClearMarkers() => markers.Clear();
        public void AddMarker(Vector2 position, Color markerColor) => markers.Add((position, markerColor));
        public void Refresh() => SetVerticesDirty();
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var bounds = rectTransform.rect;
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
    }
}
