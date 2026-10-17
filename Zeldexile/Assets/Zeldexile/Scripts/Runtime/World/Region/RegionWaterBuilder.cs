using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain;
using Zeldexile.Domain.Regions;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Visible and gameplay water of a region: the lake surface (only over the basin, so it never shows through
    /// the ground) with a swimmable volume, and a flowing ribbon for every stream, steep where the bed falls.
    /// </summary>
    public static class RegionWaterBuilder
    {
        public sealed class Result
        {
            public GameObject LakeSurface;
            public WaterVolume LakeVolume;
            public readonly List<GameObject> Streams = new List<GameObject>();
            /// <summary>Base of every waterfall (world space), for spray and sound.</summary>
            public readonly List<Vector3> FallBases = new List<Vector3>();
        }

        public static Result Build(Transform parent, RegionTerrain terrain, HeightField field)
        {
            var result = new Result();
            var data = terrain.Data;
            var origin = new Vector3(data.originX, data.lakeLevel, data.originZ);
            var root = new GameObject("Água").transform;
            root.SetParent(parent, false);

            if (data.lake != null && data.lake.shore.Length >= 3) BuildLake(root, data, field, origin, result);
            for (var i = 0; i < terrain.StreamCount; i++) BuildStream(root, terrain, field, i, origin, result);
            return result;
        }

        private static void BuildLake(Transform root, RegionData data, HeightField field, Vector3 origin, Result result)
        {
            float minU = float.MaxValue, minV = float.MaxValue, maxU = float.MinValue, maxV = float.MinValue;
            foreach (var p in data.lake.shore)
            {
                minU = Mathf.Min(minU, p.u);
                minV = Mathf.Min(minV, p.v);
                maxU = Mathf.Max(maxU, p.u);
                maxV = Mathf.Max(maxV, p.v);
            }

            // Grid over the basin; a quad is kept only where the ground dips near or below the surface.
            const float cell = 3f;
            const float margin = 18f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var index = new Dictionary<(int, int), int>();
            int Vertex(int x, int z, float u0, float v0)
            {
                if (index.TryGetValue((x, z), out var k)) return k;
                k = vertices.Count;
                vertices.Add(new Vector3(u0 + x * cell, 0f, v0 + z * cell));
                index[(x, z)] = k;
                return k;
            }

            var startU = minU - margin;
            var startV = minV - margin;
            var nx = Mathf.CeilToInt((maxU - minU + 2 * margin) / cell);
            var nz = Mathf.CeilToInt((maxV - minV + 2 * margin) / cell);
            for (var z = 0; z < nz; z++)
            for (var x = 0; x < nx; x++)
            {
                var u = startU + (x + 0.5f) * cell;
                var v = startV + (z + 0.5f) * cell;
                if (!field.Contains(u, v)) continue;
                ref var s = ref field.Nearest(u, v);
                if (s.LakeDistance > 12f) continue;
                // Where a river leaves the lake its bed drops below the lake surface: that is river, not lake.
                if (s.LakeDistance > 1f && s.StreamDistance < 4f) continue;
                var low = Mathf.Min(Mathf.Min(field.Height(u - cell * 0.5f, v - cell * 0.5f), field.Height(u + cell * 0.5f, v - cell * 0.5f)),
                    Mathf.Min(field.Height(u - cell * 0.5f, v + cell * 0.5f), field.Height(u + cell * 0.5f, v + cell * 0.5f)));
                if (low > 0.25f) continue;
                var a = Vertex(x, z, startU, startV);
                var b = Vertex(x, z + 1, startU, startV);
                var c = Vertex(x + 1, z + 1, startU, startV);
                var d = Vertex(x + 1, z, startU, startV);
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }

            var mesh = new Mesh { name = "Lago" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.up * 2f);
            var go = new GameObject(data.lake.name);
            go.transform.SetParent(root, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LakeMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            result.LakeSurface = go;

            // Swimmable volume: the top of the box is the surface. Shallow shores stay walkable (the motor only
            // swims where the water is deeper than the player's chest).
            var volume = new GameObject("Volume do lago");
            volume.transform.SetParent(root, false);
            var bottom = -data.lake.maxDepth - 2f;
            volume.transform.position = origin + new Vector3((minU + maxU) / 2f, bottom / 2f, (minV + maxV) / 2f);
            volume.AddComponent<BoxCollider>().size = new Vector3(maxU - minU + 8f, -bottom, maxV - minV + 8f);
            result.LakeVolume = volume.AddComponent<WaterVolume>();
            volume.AddComponent<ElementalSurface>().Kind = SurfaceKind.Water;
        }

        private static void BuildStream(Transform root, RegionTerrain terrain, HeightField field, int index, Vector3 origin, Result result)
        {
            var line = terrain.StreamLineOf(index);
            var data = terrain.StreamDataOf(index);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            var lastFall = -100f;
            const float step = 1f;
            var started = false;
            for (var s = 0f; s <= line.Length + 0.01f; s += step)
            {
                var p = line.At(Mathf.Min(s, line.Length), out var dir);
                // Inside the lake the lake surface takes over.
                if (field.Contains(p.u, p.v) && field.Nearest(p.u, p.v).LakeDistance < -3f)
                {
                    if (started) break;
                    continue;
                }

                var y = terrain.StreamSurface(index, s, out var width, out var slope);
                var side = dir.Left * (width * 0.5f * 0.97f);
                var steep = Mathf.InverseLerp(0.08f, 0.6f, slope);
                var k = vertices.Count;
                vertices.Add(new Vector3(p.u + side.u, y, p.v + side.v));
                vertices.Add(new Vector3(p.u - side.u, y, p.v - side.v));
                uvs.Add(new Vector2(0f, s));
                uvs.Add(new Vector2(1f, s));
                colors.Add(new Color(steep, 0f, 0f, 1f));
                colors.Add(new Color(steep, 0f, 0f, 1f));
                if (started) triangles.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 });
                started = true;

                if (slope > 0.6f && s - lastFall > 6f)
                {
                    lastFall = s;
                    // The base of the fall: a little downstream, at the pool surface.
                    var below = line.At(Mathf.Min(s + 3f, line.Length), out _);
                    var by = terrain.StreamSurface(index, s + 3f, out _, out _);
                    result.FallBases.Add(origin + new Vector3(below.u, by, below.v));
                }
            }

            if (vertices.Count < 4) return;
            var mesh = new Mesh { name = data.name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + Vector3.up);
            var go = new GameObject(data.name);
            go.transform.SetParent(root, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = StreamMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            result.Streams.Add(go);
        }

        private static Material _lake;
        private static Material _stream;

        public static Material LakeMaterial()
        {
            if (_lake != null) return _lake;
            _lake = new Material(Shader.Find("Zeldexile/Water"));
            _lake.SetColor("_Shallow", new Color(0.45f, 0.86f, 0.88f, 0.42f));
            _lake.SetColor("_Deep", new Color(0.11f, 0.45f, 0.72f, 0.9f));
            _lake.SetFloat("_DepthRange", 4.5f);
            _lake.SetFloat("_WaveHeight", 0.1f);
            _lake.SetFloat("_WaveScale", 0.22f);
            _lake.SetFloat("_WaveSpeed", 0.8f);
            return _lake;
        }

        public static Material StreamMaterial()
        {
            if (_stream != null) return _stream;
            var saved = Resources.Load<Material>("Materials/Riacho");
            _stream = saved != null ? new Material(saved) : new Material(Shader.Find("Zeldexile/Stream"));
            // Foam only along the very edge of the banks; the saved material keeps older values otherwise.
            _stream.SetFloat("_FoamWidth", 0.06f);
            return _stream;
        }
    }
}
