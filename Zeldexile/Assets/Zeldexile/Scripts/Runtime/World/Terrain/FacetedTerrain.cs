using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zeldexile.World.Terrain
{
    /// <summary>
    /// Builds flat-shaded low-poly terrain from a height function, split into chunks
    /// (one mesh + collider each), so later the world can load chunks near the player.
    /// Each triangle gets its own color from its height and slope.
    /// </summary>
    public static class FacetedTerrain
    {
        public static Transform Build(Transform parent, Func<float, float, float> height,
            Func<Vector3, float, float, Color> faceColor, Rect bounds, float cellSize, int cellsPerChunk, Material material)
        {
            var root = new GameObject("Terreno").transform;
            root.SetParent(parent, false);

            var cellsX = Mathf.CeilToInt(bounds.width / cellSize);
            var cellsZ = Mathf.CeilToInt(bounds.height / cellSize);
            for (var cz = 0; cz < cellsZ; cz += cellsPerChunk)
            for (var cx = 0; cx < cellsX; cx += cellsPerChunk)
            {
                var mesh = BuildChunk(height, faceColor, bounds.min, cellSize, cx, cz,
                    Mathf.Min(cellsPerChunk, cellsX - cx), Mathf.Min(cellsPerChunk, cellsZ - cz));
                var chunk = new GameObject($"Pedaço {cx / cellsPerChunk},{cz / cellsPerChunk}");
                chunk.transform.SetParent(root, false);
                chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                chunk.AddComponent<MeshRenderer>().sharedMaterial = material;
                chunk.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            return root;
        }

        private static Mesh BuildChunk(Func<float, float, float> height, Func<Vector3, float, float, Color> faceColor,
            Vector2 origin, float cell, int startX, int startZ, int countX, int countZ)
        {
            // Sample heights once per grid corner.
            var h = new float[countX + 1, countZ + 1];
            for (var x = 0; x <= countX; x++)
            for (var z = 0; z <= countZ; z++)
                h[x, z] = height(origin.x + (startX + x) * cell, origin.y + (startZ + z) * cell);

            var vertices = new List<Vector3>(countX * countZ * 6);
            var colors = new List<Color>(countX * countZ * 6);
            for (var x = 0; x < countX; x++)
            for (var z = 0; z < countZ; z++)
            {
                var wx = origin.x + (startX + x) * cell;
                var wz = origin.y + (startZ + z) * cell;
                var a = new Vector3(wx, h[x, z], wz);
                var b = new Vector3(wx, h[x, z + 1], wz + cell);
                var c = new Vector3(wx + cell, h[x + 1, z + 1], wz + cell);
                var d = new Vector3(wx + cell, h[x + 1, z], wz);

                // Alternate the diagonal so slopes read as facets instead of stripes.
                if (((startX + x) + (startZ + z)) % 2 == 0)
                {
                    AddFace(vertices, colors, faceColor, a, b, c);
                    AddFace(vertices, colors, faceColor, a, c, d);
                }
                else
                {
                    AddFace(vertices, colors, faceColor, a, b, d);
                    AddFace(vertices, colors, faceColor, b, c, d);
                }
            }

            var mesh = new Mesh { name = "Terreno" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            var indices = new int[vertices.Count];
            for (var i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFace(List<Vector3> vertices, List<Color> colors,
            Func<Vector3, float, float, Color> faceColor, Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            var center = (a + b + c) / 3f;
            // Per-face variation from a hash of its position: gives the hand-painted facet look.
            var variation = Mathf.Abs(Mathf.Sin(center.x * 12.9898f + center.z * 78.233f) * 43758.5453f) % 1f;
            var color = faceColor(center, normal.y, variation);
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
        }
    }
}
