using UnityEngine;
using UnityEngine.UI;
namespace Godmist.Views.UI
{
    /// <summary>Dark, muted gold wheel surface using the game's UI colors.</summary>
    public sealed class ControllerWheelGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            for (var i = 0; i < 96; i++)
            {
                var a = i*Mathf.PI*2/96; var b = (i+1)*Mathf.PI*2/96;
                var start = vertices.currentVertCount;
                vertices.AddVert(Vector3.zero,color,Vector2.zero);
                vertices.AddVert(new Vector3(Mathf.Sin(a)*346,Mathf.Cos(a)*265),color,Vector2.zero);
                vertices.AddVert(new Vector3(Mathf.Sin(b)*346,Mathf.Cos(b)*265),color,Vector2.zero);
                vertices.AddTriangle(start,start+1,start+2);
                var rim = new Color(.48f,.42f,.28f,.95f);
                start = vertices.currentVertCount;
                vertices.AddVert(new Vector3(Mathf.Sin(a)*346,Mathf.Cos(a)*265),rim,Vector2.zero);
                vertices.AddVert(new Vector3(Mathf.Sin(b)*346,Mathf.Cos(b)*265),rim,Vector2.zero);
                vertices.AddVert(new Vector3(Mathf.Sin(b)*344,Mathf.Cos(b)*263),rim,Vector2.zero);
                vertices.AddVert(new Vector3(Mathf.Sin(a)*344,Mathf.Cos(a)*263),rim,Vector2.zero);
                vertices.AddTriangle(start,start+1,start+2); vertices.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
