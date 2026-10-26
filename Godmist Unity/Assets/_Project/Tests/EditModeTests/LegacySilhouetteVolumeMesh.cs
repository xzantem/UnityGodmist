using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Godmist.Tests.EditMode
{
    // A textured volume lattice, cached per animation frame. Interior depth is
    // selected by alpha in the mask shader; no hand-authored body is required.
    internal static class LegacySilhouetteVolumeMesh
    {
        public const float DepthToWidth = .35f;
        public static Mesh Build(Sprite sprite)
        {
            const int layers = 7;
            var bounds = sprite.bounds;
            int nx = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x * sprite.pixelsPerUnit / 4));
            int ny = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y * sprite.pixelsPerUnit / 4));
            int plane = (nx + 1) * (ny + 1);
            var vertices = new Vector3[plane * layers];
            var uv = new Vector2[vertices.Length];
            var source = sprite.vertices; var sourceUV = sprite.uv; var tris = sprite.triangles;
            int a = tris[0], b = tris[1], c = tris[2];
            Vector2 ab = source[b] - source[a], ac = source[c] - source[a];
            float det = ab.x * ac.y - ab.y * ac.x;
            for (int layer = 0; layer < layers; layer++)
            for (int y = 0; y <= ny; y++)
            for (int x = 0; x <= nx; x++)
            {
                int index = layer * plane + y * (nx + 1) + x;
                var point = new Vector2(Mathf.Lerp(bounds.min.x, bounds.max.x, (float)x/nx),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (float)y/ny));
                vertices[index] = new Vector3(point.x, point.y, -1f + 2f*layer/(layers-1));
                Vector2 d = point - source[a];
                float u = (d.x * ac.y - d.y * ac.x) / det;
                float v = (ab.x * d.y - ab.y * d.x) / det;
                uv[index] = sourceUV[a] + u*(sourceUV[b]-sourceUV[a]) + v*(sourceUV[c]-sourceUV[a]);
            }
            var indices = new List<int>();
            void Quad(int i, int j, int k, int l)
            { indices.Add(i); indices.Add(j); indices.Add(k); indices.Add(i); indices.Add(k); indices.Add(l); }
            // Caps and interior cell faces make the volume render even when
            // its front plane is exactly edge-on to the projected light.
            for (int layer=0; layer<layers; layer++)
            for (int y=0; y<ny; y++)
            for (int x=0; x<nx; x++)
            {
                int i=layer*plane+y*(nx+1)+x;
                Quad(i,i+1,i+nx+2,i+nx+1);
            }
            for (int layer=0; layer<layers-1; layer++)
            {
                for (int y=0; y<=ny; y++) for (int x=0; x<nx; x++)
                { int i=layer*plane+y*(nx+1)+x; Quad(i,i+1,i+1+plane,i+plane); }
                for (int y=0; y<ny; y++) for (int x=0; x<=nx; x++)
                { int i=layer*plane+y*(nx+1)+x; Quad(i,i+nx+1,i+nx+1+plane,i+plane); }
            }
            var mesh = new Mesh { name="Animated silhouette volume", hideFlags=HideFlags.DontSave,
                indexFormat=vertices.Length>65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices=vertices; mesh.uv=uv; mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
