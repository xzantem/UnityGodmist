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
        [SerializeField] private bool facetedGem;
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
            if (facetedGem) PopulateGem(helper);
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

        // Same stepped silhouette and faces as the development-tree rank gems.
        // The existing region clipping below keeps fractional AP and cost previews exact.
        private void PopulateGem(VertexHelper helper)
        {
            helper.Clear();
            var rect = ((RectTransform)transform).rect;
            var size = Mathf.Min(rect.width, rect.height) - 2f;
            if (size <= 0f) return;
            var step = size / 7f;
            var left = rect.center.x - size * .5f;
            var bottom = rect.center.y - size * .5f;
            for (var row = 0; row < 7; row++)
            {
                var inset = Mathf.Abs(3 - row) * step;
                var y = bottom + row * step;
                Quad(helper, left + inset - 1, left + size - inset + 1, y - 1, y + step + 1, Color.black);
            }
            for (var row = 0; row < 7; row++)
            {
                var inset = Mathf.Abs(3 - row) * step;
                var l = left + inset; var r = left + size - inset;
                var y = bottom + row * step;
                if (r - l <= step * 2f)
                    Face(l, r, row > 3 ? .95f : .6f);
                else
                {
                    var middle = (l + r) * .5f;
                    Face(l, l + step, .65f);
                    Face(l + step, middle, row < 3 ? .85f : 1f);
                    Face(middle, r - step, row < 3 ? .6f : .8f);
                    Face(r - step, r, .5f);
                }
                void Face(float x1, float x2, float shade) =>
                    Quad(helper, x1, x2, y, y + step, new Color(shade, shade, shade, 1f));
            }
        }

        private static void Quad(VertexHelper helper, float l, float r, float b, float t, Color color)
        {
            var start = helper.currentVertCount;
            helper.AddVert(new Vector3(l, b), color, Vector2.zero);
            helper.AddVert(new Vector3(l, t), color, Vector2.zero);
            helper.AddVert(new Vector3(r, t), color, Vector2.zero);
            helper.AddVert(new Vector3(r, b), color, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
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
