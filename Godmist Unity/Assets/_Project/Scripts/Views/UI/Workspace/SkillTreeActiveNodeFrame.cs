using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Stepped octagonal frame for nodes that grant an active skill.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillTreeActiveNodeFrame : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float cornerCut = 16f;
        [SerializeField, Min(1f)] private float borderWidth = 2f;
        [SerializeField, Min(1f)] private float pixelStep = 2f;
        [SerializeField] private bool fillSurface;
        [SerializeField] private bool cornerSegmentsOnly;
        [SerializeField] private Color surfaceColor = new(.105f, .095f, .12f, 1f);

        public void Configure(bool surface, bool corners, float width)
        {
            fillSurface = surface;
            cornerSegmentsOnly = corners;
            borderWidth = Mathf.Max(1f, width);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            var step = Mathf.Max(1f, pixelStep);
            var cut = Mathf.Min(cornerCut, Mathf.Min(rect.width, rect.height) * .5f);
            for (var y = rect.yMin; y < rect.yMax; y += step)
            {
                var top = Mathf.Min(y + step, rect.yMax);
                var fromEdge = Mathf.Min(y - rect.yMin, rect.yMax - top);
                var inset = Mathf.Ceil(Mathf.Max(0f, cut - fromEdge) / step) * step;
                var left = rect.xMin + inset;
                var right = rect.xMax - inset;
                if (right <= left) continue;
                var horizontalEdge = fromEdge < borderWidth;
                if (cornerSegmentsOnly && fromEdge > cut + step) continue;
                if (fillSurface) AddQuad(vertices, left, right, y, top, surfaceColor);
                if (horizontalEdge && !cornerSegmentsOnly)
                    AddQuad(vertices, left, right, y, top, color);
                else
                {
                    var edgeWidth = horizontalEdge ? Mathf.Max(borderWidth, step * 3f) : borderWidth;
                    AddQuad(vertices, left, Mathf.Min(left + edgeWidth, right), y, top, color);
                    AddQuad(vertices, Mathf.Max(left, right - edgeWidth), right, y, top, color);
                }
            }
        }

        private static void AddQuad(VertexHelper vertices, float left, float right, float bottom, float top, Color tint)
        {
            var start = vertices.currentVertCount;
            vertices.AddVert(new Vector3(left, bottom), tint, Vector2.zero);
            vertices.AddVert(new Vector3(left, top), tint, Vector2.zero);
            vertices.AddVert(new Vector3(right, top), tint, Vector2.zero);
            vertices.AddVert(new Vector3(right, bottom), tint, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }
    }
}
