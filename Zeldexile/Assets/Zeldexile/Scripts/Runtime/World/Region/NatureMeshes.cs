using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Characters;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Turns a Blender nature model (one mesh per part, named "...@slot") into a single mesh for GPU instancing:
    /// vertex color = slot color (alpha = wind sway), TEXCOORD1 = the part's center and size, used by the outline
    /// so it grows evenly around each part. Meshes are cached by model name.
    /// </summary>
    public static class NatureMeshes
    {
        public const string Folder = "Nature/";

        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>The merged mesh for Resources/Models/Nature/{name}, or null when the model is missing.</summary>
        public static Mesh Get(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var prefab = ModelLibrary.Load(Folder + name);
            Mesh mesh = null;
            if (prefab != null) mesh = Merge(prefab, name);
            Cache[name] = mesh;
            return mesh;
        }

        private static Mesh Merge(GameObject prefab, string name)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var centers = new List<Vector4>();
            var triangles = new List<int>();
            var root = prefab.transform;
            var rootInverse = root.worldToLocalMatrix;

            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                if (source == null || !source.isReadable) continue;
                var slot = ModelLibrary.SlotOf(filter.name);
                var color = NaturePalette.Slot(slot);
                color.a = NaturePalette.Sway(slot);
                var toRoot = rootInverse * filter.transform.localToWorldMatrix;
                var center = toRoot.MultiplyPoint3x4(source.bounds.center);
                var size = source.bounds.extents.magnitude;
                // A tiny per-part shade shift keeps neighboring blobs of the same slot apart.
                var shift = 1f + (Mathf.Abs(Mathf.Sin(center.x * 12.9f + center.y * 78.2f + center.z * 37.7f) * 43758.5f) % 1f - 0.5f) * 0.08f;
                color = new Color(color.r * shift, color.g * shift, color.b * shift, color.a);

                var start = vertices.Count;
                var v = source.vertices;
                var n = source.normals;
                for (var i = 0; i < v.Length; i++)
                {
                    vertices.Add(toRoot.MultiplyPoint3x4(v[i]));
                    normals.Add(n.Length == v.Length ? toRoot.MultiplyVector(n[i]).normalized : Vector3.up);
                    colors.Add(color);
                    centers.Add(new Vector4(center.x, center.y, center.z, Mathf.Clamp01(size / 0.35f)));
                }

                foreach (var t in source.triangles) triangles.Add(start + t);
            }

            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = "Natureza " + name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(1, centers);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false); // stays readable: rocks reuse it as their MeshCollider
            return mesh;
        }
    }
}
