using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Godmist.Views.Lighting
{
    // A textured volume lattice, cached per animation frame. Interior depth is
    // selected by alpha in the mask shader; no hand-authored body is required.
    public static class SilhouetteVolumeMesh
    {
        public const float DepthToWidth = .35f;
        // Texture pixels do not change this lattice. Separate animation frames
        // can share it only when every geometric/UV input is exactly equal.
        internal readonly struct GeometryKey : IEquatable<GeometryKey>
        {
            public readonly Bounds Bounds;
            public readonly float PixelsPerUnit;
            public readonly Vector2 A, B, C, UvA, UvB, UvC;
            public GeometryKey(Sprite sprite)
            {
                Bounds = sprite.bounds; PixelsPerUnit = sprite.pixelsPerUnit;
                var vertices = sprite.vertices; var uv = sprite.uv; var triangles = sprite.triangles;
                int a = triangles[0], b = triangles[1], c = triangles[2];
                A = vertices[a]; B = vertices[b]; C = vertices[c];
                UvA = uv[a]; UvB = uv[b]; UvC = uv[c];
            }
            public bool Equals(GeometryKey other) => Bounds.Equals(other.Bounds)
                && PixelsPerUnit.Equals(other.PixelsPerUnit) && A.Equals(other.A)
                && B.Equals(other.B) && C.Equals(other.C) && UvA.Equals(other.UvA)
                && UvB.Equals(other.UvB) && UvC.Equals(other.UvC);
            public override bool Equals(object obj) => obj is GeometryKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Bounds, PixelsPerUnit, A, B, C, UvA, UvB, UvC);
        }

        public static Mesh Build(Sprite sprite) => Build(new GeometryKey(sprite));

        internal static Mesh Build(GeometryKey geometry)
        {
            const int layers = 7;
            var bounds = geometry.Bounds;
            int nx = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x * geometry.PixelsPerUnit / 4));
            int ny = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y * geometry.PixelsPerUnit / 4));
            int plane = (nx + 1) * (ny + 1);
            var vertices = new Vector3[plane * layers];
            var uv = new Vector2[vertices.Length];
            Vector2 ab = geometry.B - geometry.A, ac = geometry.C - geometry.A;
            var uvAb = geometry.UvB - geometry.UvA; var uvAc = geometry.UvC - geometry.UvA;
            float det = ab.x * ac.y - ab.y * ac.x;
            for (int layer = 0; layer < layers; layer++)
            for (int y = 0; y <= ny; y++)
            for (int x = 0; x <= nx; x++)
            {
                int index = layer * plane + y * (nx + 1) + x;
                var point = new Vector2(Mathf.Lerp(bounds.min.x, bounds.max.x, (float)x/nx),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (float)y/ny));
                vertices[index] = new Vector3(point.x, point.y, -1f + 2f*layer/(layers-1));
                Vector2 d = point - geometry.A;
                float u = (d.x * ac.y - d.y * ac.x) / det;
                float v = (ab.x * d.y - ab.y * d.x) / det;
                uv[index] = geometry.UvA + u*uvAb + v*uvAc;
            }
            var indices = new int[6 * (layers * nx * ny + (layers - 1) * ((ny + 1) * nx + ny * (nx + 1)))];
            int nextIndex = 0;
            void Quad(int i, int j, int k, int l)
            { indices[nextIndex++] = i; indices[nextIndex++] = j; indices[nextIndex++] = k;
                indices[nextIndex++] = i; indices[nextIndex++] = k; indices[nextIndex++] = l; }
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
