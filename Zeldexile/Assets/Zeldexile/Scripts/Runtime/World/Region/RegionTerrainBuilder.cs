using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain.Regions;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Builds the playable ground of a region, one flat-shaded mesh + collider per sector (200 m), straight from
    /// the baked height field. Sectors share their edge samples and the triangle split follows world cell parity,
    /// so there are no seams between sectors or with a neighbor region built the same way.
    /// </summary>
    public static class RegionTerrainBuilder
    {
        public static Transform Build(Transform parent, HeightField field, WorldGrid grid, Material material)
        {
            var root = new GameObject("Terreno").transform;
            root.SetParent(parent, false);
            var data = field.Data;
            var cellsPerSector = Mathf.RoundToInt(grid.SectorSize / field.Cell);
            var sectorsU = Mathf.CeilToInt(data.sizeU / grid.SectorSize);
            var sectorsV = Mathf.CeilToInt(data.sizeV / grid.SectorSize);
            for (var sv = 0; sv < sectorsV; sv++)
            for (var su = 0; su < sectorsU; su++)
            {
                var coord = grid.SectorAt(data.originX + (su + 0.5f) * grid.SectorSize, data.originZ + (sv + 0.5f) * grid.SectorSize);
                var i0 = su * cellsPerSector;
                var j0 = sv * cellsPerSector;
                var countI = Mathf.Min(cellsPerSector, field.CountU - 1 - i0);
                var countJ = Mathf.Min(cellsPerSector, field.CountV - 1 - j0);
                if (countI <= 0 || countJ <= 0) continue;
                var mesh = BuildSector(field, i0, j0, countI, countJ, coord);
                var go = new GameObject($"Setor {coord.Id}");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(data.originX, data.lakeLevel, data.originZ);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.isStatic = true;
            }

            return root;
        }

        private static Mesh BuildSector(HeightField field, int i0, int j0, int countI, int countJ, SectorCoord coord)
        {
            var cell = field.Cell;
            var vertices = new List<Vector3>(countI * countJ * 6);
            var colors = new List<Color>(countI * countJ * 6);
            for (var j = j0; j < j0 + countJ; j++)
            for (var i = i0; i < i0 + countI; i++)
            {
                var a = Corner(field, i, j);
                var b = Corner(field, i, j + 1);
                var c = Corner(field, i + 1, j + 1);
                var d = Corner(field, i + 1, j);
                if (field.MainDiagonal(i, j))
                {
                    Face(field, vertices, colors, a, b, c);
                    Face(field, vertices, colors, a, c, d);
                }
                else
                {
                    Face(field, vertices, colors, a, b, d);
                    Face(field, vertices, colors, b, c, d);
                }
            }

            var mesh = new Mesh { name = "Terreno " + coord.Id };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            var indices = new int[vertices.Count];
            for (var k = 0; k < indices.Length; k++) indices[k] = k;
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Corner position relative to the region origin at lake level.</summary>
        private static Vector3 Corner(HeightField field, int i, int j) =>
            new Vector3(i * field.Cell, field.At(i, j).H, j * field.Cell);

        private static void Face(HeightField field, List<Vector3> vertices, List<Color> colors, Vector3 a, Vector3 b, Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            var center = (a + b + c) / 3f;
            var hash = Mathf.Abs(Mathf.Sin(center.x * 12.9898f + center.z * 78.233f) * 43758.5453f) % 1f;
            var sample = field.Nearest(center.x, center.z);
            var color = NaturePalette.Ground(sample, center.y, normal.y, hash);
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
        }
    }
}
