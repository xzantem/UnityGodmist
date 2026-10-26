using System.Collections.Generic;
using Godmist.Views.UI.Workspace;
using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.Exploration
{
    /// <summary>
    /// Runtime UGUI presentation for enemy awareness. The fill is clipped to
    /// the diamond polygon itself, so a partially filled indicator never
    /// exposes the rectangular bounds of the old awareness bar.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ExplorationAwarenessIndicator : MaskableGraphic
    {
        private const float BorderInset = 4f;
        private static readonly Color OuterColor = new(.015f, .02f, .025f, .98f);
        private static readonly Color TrackColor = new(.1f, .15f, .21f, 1f);

        private float progress;
        private Color fillColor = Color.yellow;
        private Image gothicFrame;
        private bool hasGothicStyle;
        private float gothicBorderRatio;

        public float Progress => progress;
        public Color FillColor => fillColor;

        public void SetStyle(UIWorkspaceStylePalette palette)
        {
            var artwork = palette != null ? palette.GetArtwork("icon_button_normal") : null;
            hasGothicStyle = artwork != null;
            if (hasGothicStyle)
                gothicBorderRatio = Mathf.Clamp(Mathf.Max(
                    Mathf.Max(artwork.border.x, artwork.border.z) / artwork.rect.width,
                    Mathf.Max(artwork.border.y, artwork.border.w) / artwork.rect.height), .1f, .45f);
            if (hasGothicStyle && gothicFrame == null)
            {
                var frameObject = new GameObject("GodmistAwarenessFrame", typeof(RectTransform), typeof(CanvasRenderer));
                frameObject.transform.SetParent(transform.parent, false);
                gothicFrame = frameObject.AddComponent<Image>();
                gothicFrame.raycastTarget = false;
                // The existing square icon frame becomes the diamond's backing.
                // Its diagonal fits inside the unchanged awareness canvas.
                var frameRect = gothicFrame.rectTransform;
                frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(.5f, .5f);
                frameRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                frameRect.sizeDelta = Vector2.one * (64f / Mathf.Sqrt(2f));
                // Keep the fill above the frame's dark backing, with its geometry
                // inset past the ornament. The exclamation remains the last sibling.
                frameRect.SetSiblingIndex(transform.GetSiblingIndex());
            }
            if (gothicFrame != null)
            {
                gothicFrame.sprite = artwork;
                gothicFrame.type = Image.Type.Simple;
                gothicFrame.color = Color.white;
                gothicFrame.gameObject.SetActive(hasGothicStyle);
            }
            SetVerticesDirty();
        }

        public void SetProgress(float value, Color color)
        {
            progress = Mathf.Clamp01(value);
            fillColor = color;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            var size = Mathf.Min(rect.width, rect.height);
            if (size <= 0f) return;

            var center = rect.center;
            if (!hasGothicStyle)
            {
                var outer = Diamond(center, size * .5f);
                AddPolygon(vertices, outer, OuterColor);
            }

            var inset = hasGothicStyle ? size * gothicBorderRatio : BorderInset;
            var inner = Diamond(center, Mathf.Max(0f, size * .5f - inset));
            if (!hasGothicStyle) AddPolygon(vertices, inner, TrackColor);

            if (progress <= 0f) return;
            var bottom = center.y - Mathf.Max(0f, size * .5f - inset);
            var top = center.y + Mathf.Max(0f, size * .5f - inset);
            var cutoff = Mathf.Lerp(bottom, top, progress);
            var filled = ClipBelow(inner, cutoff);
            var tint = fillColor;
            if (hasGothicStyle) tint.a *= .45f;
            if (filled.Count >= 3) AddPolygon(vertices, filled, tint);
        }

        public static Vector2[] BuildFillPolygon(Rect rect, float value)
        {
            var size = Mathf.Min(rect.width, rect.height);
            if (size <= 0f || value <= 0f) return System.Array.Empty<Vector2>();
            var center = rect.center;
            var innerRadius = Mathf.Max(0f, size * .5f - BorderInset);
            var inner = Diamond(center, innerRadius);
            var cutoff = Mathf.Lerp(center.y - innerRadius, center.y + innerRadius, Mathf.Clamp01(value));
            return ClipBelow(inner, cutoff).ToArray();
        }

        private static Vector2[] Diamond(Vector2 center, float radius)
            => new[]
            {
                new Vector2(center.x, center.y - radius),
                new Vector2(center.x - radius, center.y),
                new Vector2(center.x, center.y + radius),
                new Vector2(center.x + radius, center.y)
            };

        private static List<Vector2> ClipBelow(IReadOnlyList<Vector2> polygon, float cutoff)
        {
            if (polygon == null || polygon.Count == 0) return new List<Vector2>();
            var result = new List<Vector2>(polygon.Count + 2);

            var previous = polygon[polygon.Count - 1];
            var previousInside = previous.y <= cutoff;
            for (var i = 0; i < polygon.Count; i++)
            {
                var next = polygon[i];
                var nextInside = next.y <= cutoff;
                if (previousInside != nextInside)
                {
                    var denominator = next.y - previous.y;
                    var t = Mathf.Abs(denominator) < .00001f ? 0f : (cutoff - previous.y) / denominator;
                    AddDistinct(result, Vector2.Lerp(previous, next, t));
                }
                if (nextInside) AddDistinct(result, next);
                previous = next;
                previousInside = nextInside;
            }
            if (result.Count > 1 && (result[0] - result[result.Count - 1]).sqrMagnitude < .000001f)
                result.RemoveAt(result.Count - 1);
            return result;
        }

        private static void AddDistinct(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude >= .000001f)
                points.Add(point);
        }

        private static void AddPolygon(VertexHelper vertices, IReadOnlyList<Vector2> polygon, Color polygonColor)
        {
            if (polygon == null || polygon.Count < 3) return;
            var start = vertices.currentVertCount;
            for (var i = 0; i < polygon.Count; i++)
                vertices.AddVert(polygon[i], polygonColor, Vector2.zero);
            for (var i = 1; i < polygon.Count - 1; i++)
                vertices.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
