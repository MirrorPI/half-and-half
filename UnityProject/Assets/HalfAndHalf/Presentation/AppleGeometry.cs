using System.Collections.Generic;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // Authored B01 prototype profile, shared by the visible body and its separate mass export.
    public static class AppleGeometry
    {
        public const int Rings = 48;
        public const int Segments = 64;
        public static float Radius(float t) => Mathf.Sin(t) * (1.04f + .06f * Mathf.Cos(t));
        public static float Height(float t) => .92f * Mathf.Cos(t) - .16f * Mathf.Exp(-t * t / .08f);
        public static Mesh Body()
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            for (int r = 0; r <= Rings; r++)
            {
                float t = Mathf.PI * r / Rings;
                for (int s = 0; s <= Segments; s++)
                {
                    float angle = 2 * Mathf.PI * s / Segments;
                    vertices.Add(new Vector3(Radius(t) * Mathf.Cos(angle), Height(t), Radius(t) * Mathf.Sin(angle) * .86f));
                }
            }
            for (int r = 0; r < Rings; r++) for (int s = 0; s < Segments; s++)
            {
                int a = r * (Segments + 1) + s, b = a + Segments + 1;
                indices.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
            var mesh = new Mesh { name = "B01 prototype apple" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Slice(Mesh source, float x, bool left) => MeshSection.Slice(source, x, left);
    }
}
