using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain.Regions;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Horizon around a region: a coarse, non-walkable ring of land that continues the region's edges and rises
    /// into distant hills and mountains, plus scattered low-detail trees near the edges. Purely visual and kept apart
    /// from the playable ground ("Cenário distante"): delete or replace it when a neighbor region is built.
    /// It is not part of the region's area.
    /// </summary>
    public static class FarScenery
    {
        public const float Reach = 1700f;
        private const float Cell = 25f;

        public static GameObject Build(Transform parent, HeightField field, InstancedNature nature, InstancedNature.Kind[] trees)
        {
            var data = field.Data;
            var root = new GameObject("Cenário distante");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(data.originX, data.lakeLevel, data.originZ);

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var n = Mathf.CeilToInt((data.sizeU + 2f * Reach) / Cell);
            var m = Mathf.CeilToInt((data.sizeV + 2f * Reach) / Cell);
            var heights = new float[n + 1, m + 1];
            for (var i = 0; i <= n; i++)
            for (var j = 0; j <= m; j++)
                heights[i, j] = Height(field, -Reach + i * Cell, -Reach + j * Cell);

            for (var i = 0; i < n; i++)
            for (var j = 0; j < m; j++)
            {
                var u = -Reach + i * Cell;
                var v = -Reach + j * Cell;
                // Leave the region itself (its own terrain is there), with a one-cell tuck under its edge.
                if (u >= Cell && v >= Cell && u + Cell <= data.sizeU - Cell && v + Cell <= data.sizeV - Cell) continue;
                var a = new Vector3(u, heights[i, j], v);
                var b = new Vector3(u, heights[i, j + 1], v + Cell);
                var c = new Vector3(u + Cell, heights[i + 1, j + 1], v + Cell);
                var d = new Vector3(u + Cell, heights[i + 1, j], v);
                Face(vertices, colors, a, b, c);
                Face(vertices, colors, a, c, d);
            }

            var mesh = new Mesh { name = "Horizonte", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            var idx = new int[vertices.Count];
            for (var k = 0; k < idx.Length; k++) idx[k] = k;
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = root.AddComponent<MeshRenderer>();
            r.sharedMaterial = Palette.Solid(Color.white);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Low trees continuing the woods past the edges (seeded, so the horizon never changes between runs).
            if (trees != null && trees.Length > 0)
            {
                var origin = root.transform.position;
                for (var k = 0; k < 2600; k++)
                {
                    var u = -420f + RegionMath.Hash01(data.seed, 77, k, 1) * (data.sizeU + 840f);
                    var v = -420f + RegionMath.Hash01(data.seed, 77, k, 2) * (data.sizeV + 840f);
                    var outside = Mathf.Max(Mathf.Max(-u, u - data.sizeU), Mathf.Max(-v, v - data.sizeV));
                    if (outside < 6f) continue;
                    var forest = RegionMath.Fbm(u / 160f, v / 160f, data.seed + 5, 2);
                    if (forest < -0.05f + outside / 2000f) continue;
                    var h = Height(field, u, v);
                    var kind = trees[k % trees.Length];
                    var s = 0.9f + RegionMath.Hash01(data.seed, 77, k, 3) * 0.6f;
                    nature.Add(kind, Matrix4x4.TRS(origin + new Vector3(u, h - 0.3f, v), Quaternion.Euler(0f, k * 47f, 0f), Vector3.one * s), 8f * s);
                }
            }

            return root;
        }

        /// <summary>Continues the nearest edge height, then rises into hills and ranges far away.</summary>
        public static float Height(HeightField field, float u, float v)
        {
            var data = field.Data;
            var cu = Mathf.Clamp(u, 0f, data.sizeU);
            var cv = Mathf.Clamp(v, 0f, data.sizeV);
            var outside = Mathf.Sqrt((u - cu) * (u - cu) + (v - cv) * (v - cv));
            var edge = field.Height(cu, cv);
            if (outside <= 0f) return edge - 3f;
            var rolling = 6f * RegionMath.Fbm(u / 180f, v / 180f, data.seed + 91, 3);
            var ridges = 1f - Mathf.Abs(RegionMath.Fbm(u / 520f, v / 520f, data.seed + 93, 3));
            var mountains = Mathf.Pow(Mathf.Max(0f, ridges), 2.2f) * 230f + 40f * RegionMath.Fbm(u / 260f, v / 260f, data.seed + 95, 2);
            var t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 900f, outside));
            // Near the edge it follows the region (minus a little, tucked under its border), then lifts away.
            return Mathf.Lerp(edge - 0.5f + rolling * Mathf.InverseLerp(0f, 120f, outside), Mathf.Max(edge, 10f) + mountains, t);
        }

        private static void Face(List<Vector3> vertices, List<Color> colors, Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            var center = (a + b + c) / 3f;
            var hash = Mathf.Abs(Mathf.Sin(center.x * 12.9898f + center.z * 78.233f) * 43758.5453f) % 1f;
            var green = Color.Lerp(new Color(0.30f, 0.52f, 0.22f), new Color(0.40f, 0.62f, 0.25f), hash);
            var rock = Color.Lerp(new Color(0.74f, 0.72f, 0.66f), new Color(0.84f, 0.82f, 0.75f), hash);
            var snowline = Mathf.InverseLerp(170f, 230f, center.y);
            var c0 = Color.Lerp(green, rock, Mathf.Max(Mathf.InverseLerp(0.82f, 0.6f, normal.y), Mathf.InverseLerp(80f, 160f, center.y)));
            c0 = Color.Lerp(c0, new Color(0.93f, 0.95f, 0.97f), snowline * 0.8f);
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(c0);
            colors.Add(c0);
            colors.Add(c0);
        }
    }
}
