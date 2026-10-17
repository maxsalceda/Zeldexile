using System.Collections.Generic;
using UnityEngine;

namespace Zeldexile.Characters
{
    /// <summary>
    /// Flat-shaded low-poly meshes built in code (every triangle has its own vertices, so faces stay faceted).
    /// Placeholders in the final art style; Blender models replace them later.
    /// </summary>
    public static class LowPoly
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// Prism along +Y from 0 to 1, radius 0.5 at the bottom and 0.5 × topScale at the top.
        /// topScale 0 makes a cone.
        /// </summary>
        public static Mesh Prism(int sides, float topScale = 1f)
        {
            var key = $"prism{sides}_{topScale:0.###}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var tris = new List<Vector3>();
            for (var i = 0; i < sides; i++)
            {
                var a0 = Mathf.PI * 2f * i / sides;
                var a1 = Mathf.PI * 2f * (i + 1) / sides;
                var b0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f;
                var b1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f;
                var t0 = new Vector3(b0.x * topScale, 1f, b0.z * topScale);
                var t1 = new Vector3(b1.x * topScale, 1f, b1.z * topScale);

                // Side.
                if (topScale > 0f) tris.AddRange(new[] { b0, t0, t1 });
                tris.AddRange(new[] { b0, t1, b1 });

                // Caps.
                tris.AddRange(new[] { Vector3.zero, b0, b1 });
                if (topScale > 0f) tris.AddRange(new[] { Vector3.up, t1, t0 });
            }

            return Cache[key] = Build(key, tris);
        }

        /// <summary>Icosahedron of radius 0.5, optionally subdivided once. jitter &gt; 0 makes rocks.</summary>
        public static Mesh Ico(int subdivisions = 0, float jitter = 0f, int seed = 0)
        {
            var key = $"ico{subdivisions}_{jitter:0.###}_{seed}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            for (var s = 0; s < subdivisions; s++)
            {
                var mid = new Dictionary<long, int>();
                var next = new List<int>();
                for (var i = 0; i < f.Count; i += 3)
                {
                    var a = f[i];
                    var b = f[i + 1];
                    var c = f[i + 2];
                    var ab = Midpoint(v, mid, a, b);
                    var bc = Midpoint(v, mid, b, c);
                    var ca = Midpoint(v, mid, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }

                f = next;
            }

            var random = new System.Random(seed);
            for (var i = 0; i < v.Count; i++)
            {
                var scale = 0.5f * (1f + (float)(random.NextDouble() * 2.0 - 1.0) * jitter);
                v[i] = v[i].normalized * scale;
            }

            var tris = new List<Vector3>();
            foreach (var index in f) tris.Add(v[index]);
            return Cache[key] = Build(key, tris);
        }

        private static int Midpoint(List<Vector3> v, Dictionary<long, int> cache, int a, int b)
        {
            var key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (cache.TryGetValue(key, out var index)) return index;
            v.Add((v[a] + v[b]) * 0.5f);
            return cache[key] = v.Count - 1;
        }

        /// <summary>Thin wing shape for the glider, spanning x [-0.5, 0.5] and z [-0.5, 0.5].</summary>
        public static Mesh Glider()
        {
            const string key = "glider";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var nose = new Vector3(0f, 0.08f, 0.5f);
            var left = new Vector3(-0.5f, 0f, -0.25f);
            var right = new Vector3(0.5f, 0f, -0.25f);
            var tail = new Vector3(0f, 0.04f, -0.5f);
            var tris = new List<Vector3>
            {
                nose, right, tail, nose, tail, left, // top
                nose, tail, right, nose, left, tail  // bottom
            };
            return Cache[key] = Build(key, tris, fixWinding: false);
        }

        private static Mesh Build(string name, List<Vector3> triangleVertices, bool fixWinding = true)
        {
            var mesh = new Mesh { name = name };
            var indices = new int[triangleVertices.Count];
            for (var i = 0; i < indices.Length; i++) indices[i] = i;

            // Make winding face outward from the centroid so the convex generators can be sloppy about order.
            var center = Vector3.zero;
            foreach (var p in triangleVertices) center += p;
            center /= Mathf.Max(1, triangleVertices.Count);
            for (var i = 0; fixWinding && i < triangleVertices.Count; i += 3)
            {
                var a = triangleVertices[i];
                var b = triangleVertices[i + 1];
                var c = triangleVertices[i + 2];
                var normal = Vector3.Cross(b - a, c - a);
                var faceCenter = (a + b + c) / 3f;
                var outward = faceCenter - center;
                if (outward.sqrMagnitude > 1e-6f && Vector3.Dot(normal, outward) < 0f)
                {
                    triangleVertices[i + 1] = c;
                    triangleVertices[i + 2] = b;
                }
            }

            mesh.SetVertices(triangleVertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject Part(string name, Transform parent, Mesh mesh, Material material,
            Vector3 localPosition, Vector3 localScale, Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
