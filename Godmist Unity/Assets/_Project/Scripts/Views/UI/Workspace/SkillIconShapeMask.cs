using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI.Workspace
{
    /// <summary>Opaque stepped silhouette used by UGUI Mask to clip full-slot skill icons.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillIconShapeMask : MaskableGraphic
    {
        [SerializeField, Min(0)] private float cornerCut = 2;

        public void SetCornerCut(float cut)
        {
            cornerCut = Mathf.Max(0, cut);
            color = Color.white;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            var cut = Mathf.Min(cornerCut, Mathf.Min(rect.width, rect.height) * .5f);
            const float step = 2;
            for (var y = rect.yMin; y < rect.yMax; y += step)
            {
                var top = Mathf.Min(y + step, rect.yMax);
                var edge = Mathf.Min(y - rect.yMin, rect.yMax - top);
                var inset = Mathf.Ceil(Mathf.Max(0, cut - edge) / step) * step;
                var left = rect.xMin + inset;
                var right = rect.xMax - inset;
                if (right <= left) continue;
                var start = vertices.currentVertCount;
                vertices.AddVert(new Vector3(left, y), Color.white, Vector2.zero);
                vertices.AddVert(new Vector3(left, top), Color.white, Vector2.zero);
                vertices.AddVert(new Vector3(right, top), Color.white, Vector2.zero);
                vertices.AddVert(new Vector3(right, y), Color.white, Vector2.zero);
                vertices.AddTriangle(start, start + 1, start + 2);
                vertices.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
