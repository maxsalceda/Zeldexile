using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain.Regions;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// The trodden surface of every path, drawn as a thin strip laid on the terrain: smooth curves and a ragged,
    /// grassy edge that the 2.5 m terrain faces alone cannot draw. Packed earth in the middle, darker and greener
    /// toward the edges, wetter near water. Purely visual: the walkable ground is the graded terrain under it.
    /// </summary>
    public static class PathRibbonBuilder
    {
        private static readonly float[] Across = { -1f, -0.6f, 0f, 0.6f, 1f };

        public static GameObject Build(Transform parent, RegionTerrain terrain, HeightField field, Material material)
        {
            var data = terrain.Data;
            var root = new GameObject("Trilhas");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(data.originX, data.lakeLevel, data.originZ);
            for (var i = 0; i < terrain.PathCount; i++)
            {
                var mesh = BuildPath(terrain, field, i);
                if (mesh == null) continue;
                var go = new GameObject(terrain.PathDataOf(i).name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return root;
        }

        private static Mesh BuildPath(RegionTerrain terrain, HeightField field, int index)
        {
            var line = terrain.PathLineOf(index);
            var data = terrain.PathDataOf(index);
            var lift = data.kind == "principal" ? 0.07f : data.kind == "secundaria" ? 0.06f : 0.05f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var open = false;
            const float step = 0.9f;
            var seed = terrain.Data.seed + index * 13;
            for (var s = 0f; s <= line.Length + 0.01f; s += step)
            {
                var p = line.At(Mathf.Min(s, line.Length), out var dir);
                if (OverWater(terrain, field, p))
                {
                    open = false;
                    continue;
                }

                var half = terrain.PathHalfWidth(index, s);
                var left = dir.Left;
                var start = vertices.Count;
                for (var k = 0; k < Across.Length; k++)
                {
                    var a = Across[k];
                    // Ragged edges: the outer vertices wander in and out along the path.
                    var edge = Mathf.Abs(a) > 0.99f ? RegionMath.Noise(s / 1.7f, a * 5f, seed) * 0.32f + RegionMath.Noise(s / 6f, a * 9f, seed + 1) * 0.25f : 0f;
                    var off = a * half * 1.05f + Mathf.Sign(a) * edge;
                    var u = p.u + left.u * off;
                    var v = p.v + left.v * off;
                    if (!field.Contains(u, v))
                    {
                        u = Mathf.Clamp(u, 0f, field.Data.sizeU);
                        v = Mathf.Clamp(v, 0f, field.Data.sizeV);
                    }

                    vertices.Add(new Vector3(u, field.Height(u, v) + lift * (Mathf.Abs(a) > 0.99f ? 0.6f : 1f), v));
                    normals.Add(Normal(field, u, v));
                    colors.Add(SurfaceColor(field, u, v, a, s, seed));
                }

                if (open)
                {
                    var prev = start - Across.Length;
                    for (var k = 0; k < Across.Length - 1; k++)
                    {
                        // Clockwise seen from above (Unity's front face).
                        triangles.Add(prev + k);
                        triangles.Add(start + k + 1);
                        triangles.Add(start + k);
                        triangles.Add(prev + k);
                        triangles.Add(prev + k + 1);
                        triangles.Add(start + k + 1);
                    }
                }

                open = true;
            }

            if (triangles.Count == 0) return null;
            var mesh = new Mesh { name = "Trilha " + data.id };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>On a bridge span or in a stream ford there is no earth to draw.</summary>
        private static bool OverWater(RegionTerrain terrain, HeightField field, P2 p)
        {
            foreach (var b in terrain.Bridges)
            {
                var d = RegionMath.SegmentDistance(p, b.A, b.B, out var t);
                if (d < b.Data.width && t > 0.0f && t < 1f) return true;
            }

            return field.Contains(p.u, p.v) && field.Nearest(p.u, p.v).Water != WaterKind.None;
        }

        private static Vector3 Normal(HeightField field, float u, float v)
        {
            const float e = 0.5f;
            var dx = (field.Height(u + e, v) - field.Height(u - e, v)) / (2f * e);
            var dz = (field.Height(u, v + e) - field.Height(u, v - e)) / (2f * e);
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        private static readonly Color Center = new Color(0.80f, 0.65f, 0.43f);
        private static readonly Color Middle = new Color(0.74f, 0.59f, 0.38f);
        private static readonly Color Edge = new Color(0.56f, 0.58f, 0.30f);
        private static readonly Color Wet = new Color(0.56f, 0.45f, 0.30f);

        private static Color SurfaceColor(HeightField field, float u, float v, float across, float s, int seed)
        {
            var c = Mathf.Abs(across) > 0.99f ? Edge : Mathf.Abs(across) > 0.1f ? Middle : Center;
            // Patches of lighter dust and darker packed earth along the way.
            var patch = RegionMath.Noise(s / 4f, across * 2f, seed + 7) * 0.06f;
            c = new Color(c.r + patch, c.g + patch, c.b + patch * 0.6f);
            ref var sample = ref field.Nearest(u, v);
            var wet = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 5f, Mathf.Min(sample.StreamDistance, sample.LakeDistance)));
            c = Color.Lerp(c, Wet, wet * 0.7f);
            var shade = Mathf.Max(sample.Forest, sample.SparseForest * 0.5f);
            var dark = new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, 1f);
            return Color.Lerp(c, dark, shade);
        }
    }
}
