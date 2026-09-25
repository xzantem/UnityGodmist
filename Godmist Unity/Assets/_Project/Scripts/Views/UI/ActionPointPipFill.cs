using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    // Keeps the existing pip sprite/shape, including sprite-less Images, and colors
    // three exact horizontal regions: remaining, preview spend and already spent.
    [RequireComponent(typeof(Image))]
    public sealed class ActionPointPipFill : BaseMeshEffect
    {
        private float current = 1f, remaining = 1f;
        private Color available = Color.white, preview = Color.white, spent = Color.gray;

        public void SetFill(float currentFill, float remainingFill, Color availableColor, Color previewColor, Color spentColor)
        {
            currentFill = Mathf.Clamp01(currentFill);
            remainingFill = Mathf.Clamp(remainingFill, 0f, currentFill);
            if (current == currentFill && remaining == remainingFill && available == availableColor
                && preview == previewColor && spent == spentColor) return;
            current = currentFill;
            remaining = remainingFill;
            available = availableColor; preview = previewColor; spent = spentColor;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            var vertices = new List<UIVertex>();
            helper.GetUIVertexStream(vertices);
            if (vertices.Count == 0) return;
            var min = float.PositiveInfinity; var max = float.NegativeInfinity;
            foreach (var vertex in vertices) { min = Mathf.Min(min, vertex.position.x); max = Mathf.Max(max, vertex.position.x); }
            helper.Clear();
            var first = Mathf.Lerp(min, max, remaining);
            var second = Mathf.Lerp(min, max, current);
            for (var i = 0; i < vertices.Count; i += 3)
            {
                var triangle = new List<UIVertex> { vertices[i], vertices[i + 1], vertices[i + 2] };
                AddRegion(helper, triangle, min, first, available);
                AddRegion(helper, triangle, first, second, preview);
                AddRegion(helper, triangle, second, max, spent);
            }
        }

        private static void AddRegion(VertexHelper helper, List<UIVertex> triangle, float left, float right, Color color)
        {
            if (right <= left) return;
            var polygon = Clip(Clip(triangle, left, true), right, false);
            if (polygon.Count < 3) return;
            var start = helper.currentVertCount;
            foreach (var source in polygon) { var vertex = source; vertex.color = (Color)vertex.color * color; helper.AddVert(vertex); }
            for (var i = 1; i < polygon.Count - 1; i++) helper.AddTriangle(start, start + i, start + i + 1);
        }

        private static List<UIVertex> Clip(List<UIVertex> source, float boundary, bool keepRight)
        {
            var result = new List<UIVertex>();
            if (source.Count == 0) return result;
            var previous = source[source.Count - 1];
            var previousInside = keepRight ? previous.position.x >= boundary : previous.position.x <= boundary;
            foreach (var next in source)
            {
                var inside = keepRight ? next.position.x >= boundary : next.position.x <= boundary;
                if (inside != previousInside)
                {
                    var t = (boundary - previous.position.x) / (next.position.x - previous.position.x);
                    var cut = previous;
                    cut.position = Vector3.Lerp(previous.position, next.position, t);
                    cut.uv0 = Vector4.Lerp(previous.uv0, next.uv0, t);
                    cut.uv1 = Vector4.Lerp(previous.uv1, next.uv1, t);
                    cut.color = Color32.Lerp(previous.color, next.color, t);
                    result.Add(cut);
                }
                if (inside) result.Add(next);
                previous = next; previousInside = inside;
            }
            return result;
        }
    }
}
