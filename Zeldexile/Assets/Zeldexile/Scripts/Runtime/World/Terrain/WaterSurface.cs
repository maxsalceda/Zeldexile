using UnityEngine;

namespace Zeldexile.World.Terrain
{
    /// <summary>
    /// Visible water: a flat grid the Water shader animates into faceted waves.
    /// Gameplay water (swimming, freezing, lightning) stays on WaterVolume.
    /// </summary>
    public static class WaterSurface
    {
        public static GameObject Build(Transform parent, Rect area, float surfaceY, float cellSize)
        {
            var cellsX = Mathf.CeilToInt(area.width / cellSize);
            var cellsZ = Mathf.CeilToInt(area.height / cellSize);
            var vertices = new Vector3[(cellsX + 1) * (cellsZ + 1)];
            for (var z = 0; z <= cellsZ; z++)
            for (var x = 0; x <= cellsX; x++)
                vertices[z * (cellsX + 1) + x] = new Vector3(area.xMin + x * cellSize, 0f, area.yMin + z * cellSize);

            var triangles = new int[cellsX * cellsZ * 6];
            var t = 0;
            for (var z = 0; z < cellsZ; z++)
            for (var x = 0; x < cellsX; x++)
            {
                var a = z * (cellsX + 1) + x;
                var b = a + cellsX + 1;
                triangles[t++] = a;
                triangles[t++] = b;
                triangles[t++] = b + 1;
                triangles[t++] = a;
                triangles[t++] = b + 1;
                triangles[t++] = a + 1;
            }

            var mesh = new Mesh { name = "Água" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            // Waves move vertices up and down in the shader; keep them inside the culling bounds.
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.up * 2f);

            var go = new GameObject("Superfície da água");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, surfaceY, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Zeldexile/Water"));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
