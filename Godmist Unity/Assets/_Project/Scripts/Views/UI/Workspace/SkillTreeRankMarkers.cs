using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>One white faceted pixel gem per rank, tinted green when acquired.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillTreeRankMarkers : MaskableGraphic
    {
        [SerializeField, Min(1)] private int maximumRanks = 1;
        [SerializeField, Min(0)] private int investedRanks;
        [SerializeField] private Color emptyColor = Color.white;

        public void SetRanks(int invested, int maximum, Color filled, Color empty)
        {
            maximumRanks = Mathf.Max(1, maximum);
            investedRanks = Mathf.Clamp(invested, 0, maximumRanks);
            color = filled;
            emptyColor = empty;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            const float size = 14f, gap = 4f, step = 2f;
            var width = maximumRanks * size + (maximumRanks - 1) * gap;
            var left = Mathf.Round(rect.center.x - width * .5f);
            var bottom = Mathf.Round(rect.center.y - size * .5f);
            for (var rank = 0; rank < maximumRanks; rank++)
            {
                var tint = rank < investedRanks ? color : emptyColor;
                var start = left + rank * (size + gap);
                // Draw the thin silhouette first so faces cannot be covered by the next outline strip.
                for (var row = 0; row < 7; row++)
                {
                    var inset = Mathf.Abs(3 - row) * step;
                    var y = bottom + row * step;
                    AddQuad(vertices, start + inset - 1, start + size - inset + 1,
                        y - 1, y + step + 1, new Color(0, 0, 0, .85f));
                }
                for (var row = 0; row < 7; row++)
                {
                    var inset = Mathf.Abs(3 - row) * step;
                    var l = start + inset;
                    var r = start + size - inset;
                    var y = bottom + row * step;
                    if (r - l <= step * 2)
                        AddQuad(vertices, l, r, y, y + step, Shade(tint, row > 3 ? .95f : .6f));
                    else
                    {
                        // A single white gem design; all faces receive the same rank tint.
                        var middle = (l + r) * .5f;
                        AddQuad(vertices, l, l + step, y, y + step, Shade(tint, .65f));
                        AddQuad(vertices, l + step, middle, y, y + step, Shade(tint, row < 3 ? .85f : 1f));
                        AddQuad(vertices, middle, r - step, y, y + step, Shade(tint, row < 3 ? .6f : .8f));
                        AddQuad(vertices, r - step, r, y, y + step, Shade(tint, .5f));
                    }
                }
            }
        }

        private static Color Shade(Color tint, float brightness) =>
            new(tint.r * brightness, tint.g * brightness, tint.b * brightness, tint.a);

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
