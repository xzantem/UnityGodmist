using UnityEngine;
using UnityEngine.UI;

namespace Godmist.Views.UI
{
    /// <summary>A border-only focus indicator; never fills or obscures the control's content.</summary>
    public sealed class ControllerFocusGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            var width = Mathf.Min(1.5f, rect.width * .5f, rect.height * .5f);
            AddStrip(vertices, rect.xMin, rect.yMin, rect.xMax, rect.yMin + width);
            AddStrip(vertices, rect.xMin, rect.yMax - width, rect.xMax, rect.yMax);
            AddStrip(vertices, rect.xMin, rect.yMin + width, rect.xMin + width, rect.yMax - width);
            AddStrip(vertices, rect.xMax - width, rect.yMin + width, rect.xMax, rect.yMax - width);
        }

        private void AddStrip(VertexHelper vertices, float left, float bottom, float right, float top)
        {
            var start = vertices.currentVertCount;
            vertices.AddVert(new Vector3(left, bottom), color, Vector2.zero);
            vertices.AddVert(new Vector3(left, top), color, Vector2.zero);
            vertices.AddVert(new Vector3(right, top), color, Vector2.zero);
            vertices.AddVert(new Vector3(right, bottom), color, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start + 2, start + 3, start);
        }
    }
}
