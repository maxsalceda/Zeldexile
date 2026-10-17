using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zeldexile.World.Terrain
{
    /// <summary>
    /// Dense decorative grass drawn with GPU instancing: clumps of one-triangle blades, grouped in cells
    /// and drawn only near the camera. Wind, trampling and distance fade happen in the Grass shader.
    /// </summary>
    public sealed class GrassField : MonoBehaviour
    {
        public Transform Player;

        [SerializeField] private float cellSize = 16f;
        [SerializeField] private float clumpsPerSquareMeter = 1.9f;
        [SerializeField] private float drawDistance = 70f;
        [SerializeField] private int bladesPerClump = 9;

        private readonly List<Cell> _cells = new List<Cell>();
        private Mesh _clump;
        private Material _material;
        private RenderParams _params;

        private static readonly int PlayerPosId = Shader.PropertyToID("_ZxPlayerPos");
        private static readonly int DrawDistanceId = Shader.PropertyToID("_DrawDistance");

        private sealed class Cell
        {
            public Vector3 Center;
            public readonly List<Matrix4x4> Instances = new List<Matrix4x4>();
        }

        public int InstanceCount { get; private set; }

        /// <summary>Scatters clumps over the area where allowed(x, z) says grass grows.</summary>
        public void Build(Rect area, Func<float, float, float> height, Func<float, float, bool> allowed, int seed)
        {
            _clump = BuildClump(bladesPerClump, seed);
            if (!SystemInfo.supportsInstancing) return;
            // A saved material keeps the instanced shader variant in builds.
            var saved = Resources.Load<Material>("Materials/Grama");
            var shader = Shader.Find("Zeldexile/Grass");
            if (saved == null && shader == null) return;
            _material = saved != null ? new Material(saved) : new Material(shader);
            _material.enableInstancing = true;
            _material.SetFloat(DrawDistanceId, drawDistance);
            _params = new RenderParams(_material)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0
            };

            var random = new System.Random(seed);
            var perCell = Mathf.RoundToInt(cellSize * cellSize * clumpsPerSquareMeter);
            for (var cz = area.yMin; cz < area.yMax; cz += cellSize)
            for (var cx = area.xMin; cx < area.xMax; cx += cellSize)
            {
                var cell = new Cell();
                for (var i = 0; i < perCell; i++)
                {
                    var x = cx + (float)random.NextDouble() * cellSize;
                    var z = cz + (float)random.NextDouble() * cellSize;
                    if (!allowed(x, z)) continue;
                    var y = height(x, z) - 0.04f;
                    var scale = 0.6f + (float)random.NextDouble() * 0.45f;
                    var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    cell.Instances.Add(Matrix4x4.TRS(new Vector3(x, y, z), rotation, new Vector3(scale, scale * (0.8f + (float)random.NextDouble() * 0.5f), scale)));
                }

                if (cell.Instances.Count == 0) continue;
                cell.Center = new Vector3(cx + cellSize / 2f, height(cx + cellSize / 2f, cz + cellSize / 2f), cz + cellSize / 2f);
                _cells.Add(cell);
                InstanceCount += cell.Instances.Count;
            }

            var bounds = new Bounds(new Vector3(area.center.x, 0f, area.center.y), new Vector3(area.width, 200f, area.height));
            _params.worldBounds = bounds;
        }

        private void Update()
        {
            if (_material == null) return;
            if (Player != null) Shader.SetGlobalVector(PlayerPosId, Player.position);

            var cam = Camera.main;
            if (cam == null) return;
            var camPos = cam.transform.position;
            var reach = drawDistance + cellSize;
            foreach (var cell in _cells)
            {
                if ((cell.Center - camPos).sqrMagnitude > reach * reach) continue;
                Graphics.RenderMeshInstanced(_params, _clump, 0, cell.Instances);
            }
        }

        /// <summary>A clump of thin triangular blades. uv.y is 0 at the root and 1 at the tip (drives sway and color).</summary>
        public static Mesh BuildClump(int blades, int seed)
        {
            var random = new System.Random(seed + 77);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var b = 0; b < blades; b++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var radius = (float)random.NextDouble() * 0.35f;
                var root = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var facing = (float)random.NextDouble() * Mathf.PI;
                var side = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing)) * 0.05f;
                var height = 0.22f + (float)random.NextDouble() * 0.22f;
                var lean = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 0.18f;

                var i = vertices.Count;
                vertices.Add(root - side);
                vertices.Add(root + side);
                vertices.Add(root + lean + Vector3.up * height);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0.5f, 1f));
                triangles.Add(i);
                triangles.Add(i + 2);
                triangles.Add(i + 1);
            }

            var mesh = new Mesh { name = "Tufo de grama" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
