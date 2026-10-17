using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Zeldexile.Domain.Regions;
using Zeldexile.World.Terrain;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Dense instanced grass for a large region, generated cell by cell only around the camera and dropped behind
    /// it, so a square kilometer never holds millions of instances in memory. Each cell is seeded from its world
    /// coordinates: the same spot always grows the same grass. Same shader as the island grass (wind, trampling).
    /// </summary>
    public sealed class StreamedGrass : MonoBehaviour
    {
        public Transform Player;

        [SerializeField] private float cellSize = 16f;
        [SerializeField] private float clumpsPerSquareMeter = 1.7f;
        [SerializeField] private float drawDistance = 75f;
        [SerializeField] private int bladesPerClump = 10;

        private sealed class Cell
        {
            public Bounds Bounds;
            public readonly List<Matrix4x4> Instances = new List<Matrix4x4>();
        }

        private HeightField _field;
        private Vector3 _origin;
        private int _seed;
        private Mesh _clump;
        private Material _material;
        private readonly Dictionary<(int, int), Cell> _cells = new Dictionary<(int, int), Cell>();
        private readonly Stack<Cell> _pool = new Stack<Cell>();
        private readonly List<(int, int)> _drop = new List<(int, int)>();

        private static readonly int PlayerPosId = Shader.PropertyToID("_ZxPlayerPos");
        private static readonly int DrawDistanceId = Shader.PropertyToID("_DrawDistance");

        public int LiveInstances { get; private set; }

        public void Setup(HeightField field, int seed)
        {
            _field = field;
            _seed = seed;
            _origin = new Vector3(field.Data.originX, field.Data.lakeLevel, field.Data.originZ);
            _clump = GrassField.BuildClump(bladesPerClump, seed);
            if (!SystemInfo.supportsInstancing) return;
            var saved = Resources.Load<Material>("Materials/Grama");
            var shader = Shader.Find("Zeldexile/Grass");
            if (saved == null && shader == null) return;
            _material = saved != null ? new Material(saved) : new Material(shader);
            _material.enableInstancing = true;
            _material.SetFloat(DrawDistanceId, drawDistance);
            _material.SetColor("_BaseColor", new Color(0.24f, 0.47f, 0.15f));
            _material.SetColor("_TipColor", new Color(0.60f, 0.81f, 0.30f));
        }

        private void Update()
        {
            var cam = Camera.main;
            if (cam == null || _material == null) return;
            if (Player != null) Shader.SetGlobalVector(PlayerPosId, Player.position);
            // A few new cells per frame while moving keeps the frame time flat; the rest follow next frames.
            Refresh(cam.transform.position, 6);
            Draw(cam, false);
        }

        /// <summary>Creates cells that came into range (nearest first, at most maxNew) and recycles those that left it.</summary>
        public void Refresh(Vector3 eye, int maxNew = int.MaxValue)
        {
            var reach = drawDistance + cellSize;
            var cx0 = Mathf.FloorToInt((eye.x - reach) / cellSize);
            var cx1 = Mathf.FloorToInt((eye.x + reach) / cellSize);
            var cz0 = Mathf.FloorToInt((eye.z - reach) / cellSize);
            var cz1 = Mathf.FloorToInt((eye.z + reach) / cellSize);

            _drop.Clear();
            foreach (var pair in _cells)
            {
                var (x, z) = pair.Key;
                if (x < cx0 || x > cx1 || z < cz0 || z > cz1) _drop.Add(pair.Key);
            }

            foreach (var key in _drop)
            {
                LiveInstances -= _cells[key].Instances.Count;
                _pool.Push(_cells[key]);
                _cells.Remove(key);
            }

            _wanted.Clear();
            for (var z = cz0; z <= cz1; z++)
            for (var x = cx0; x <= cx1; x++)
            {
                if (_cells.ContainsKey((x, z))) continue;
                var center = new Vector2((x + 0.5f) * cellSize, (z + 0.5f) * cellSize);
                var d2 = (center - new Vector2(eye.x, eye.z)).sqrMagnitude;
                if (d2 <= reach * reach) _wanted.Add((d2, x, z));
            }

            _wanted.Sort((a, b) => a.d2.CompareTo(b.d2));
            for (var k = 0; k < _wanted.Count && k < maxNew; k++)
            {
                var (_, x, z) = _wanted[k];
                var cell = _pool.Count > 0 ? _pool.Pop() : new Cell();
                Grow(cell, x, z);
                _cells[(x, z)] = cell;
                LiveInstances += cell.Instances.Count;
            }
        }

        private readonly List<(float d2, int x, int z)> _wanted = new List<(float, int, int)>();

        private void Grow(Cell cell, int cx, int cz)
        {
            cell.Instances.Clear();
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var candidates = Mathf.RoundToInt(cellSize * cellSize * clumpsPerSquareMeter);
            for (var i = 0; i < candidates; i++)
            {
                var wx = (cx + RegionMath.Hash01(_seed, cx, cz, i * 4)) * cellSize;
                var wz = (cz + RegionMath.Hash01(_seed, cx, cz, i * 4 + 1)) * cellSize;
                var u = wx - _origin.x;
                var v = wz - _origin.z;
                if (!_field.Contains(u, v)) continue;
                ref var s = ref _field.Nearest(u, v);
                var density = Density(s);
                if (density <= 0f || RegionMath.Hash01(_seed, cx, cz, i * 4 + 2) > density) continue;
                if (_field.NormalY(u, v) < 0.82f) continue;
                var y = _origin.y + _field.Height(u, v) - 0.04f;
                var r = RegionMath.Hash01(_seed, cx, cz, i * 4 + 3);
                // Taller in open meadows, shorter by paths and under trees.
                var tall = Mathf.Lerp(0.85f, 1.5f, (1f - s.Path) * (1f - s.Forest * 0.5f) * Mathf.Clamp01(s.Variation * 1.3f));
                var scale = tall * (0.75f + r * 0.4f);
                var rotation = Quaternion.Euler(0f, r * 360f * 7.3f, 0f);
                cell.Instances.Add(Matrix4x4.TRS(new Vector3(wx, y, wz), rotation, new Vector3(scale, scale * (0.85f + r * 0.45f), scale)));
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            var c = new Vector3((cx + 0.5f) * cellSize, (minY + maxY) * 0.5f, (cz + 0.5f) * cellSize);
            cell.Bounds = cell.Instances.Count == 0 ? new Bounds(c, Vector3.zero) : new Bounds(c, new Vector3(cellSize + 1f, maxY - minY + 2f, cellSize + 1f));
        }

        /// <summary>Where grass grows and how thick: open dry ground, thinner under trees, none on paths or rock.</summary>
        public static float Density(in TerrainSample s)
        {
            if (s.Water != WaterKind.None || s.Cliff > 0.3f) return 0f;
            if (s.LakeDistance < 1.5f || s.StreamDistance < 0.4f) return 0f;
            if (s.Beach > 0.5f && s.LakeDistance < 14f) return 0f;
            var d = 1f - RegionMath.SmoothStep(0.15f, 0.55f, s.Path);
            d *= 1f - s.Rock * 0.6f;
            d *= 1f - s.Forest * (1f - s.Clearing) * 0.55f;
            return d;
        }

        public void Draw(Camera cam, bool exclusive)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var eye = cam.transform.position;
            var reach = drawDistance + cellSize;
            foreach (var cell in _cells.Values)
            {
                if (cell.Instances.Count == 0) continue;
                if (cell.Bounds.SqrDistance(eye) > reach * reach) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, cell.Bounds)) continue;
                var rp = new RenderParams(_material)
                {
                    worldBounds = cell.Bounds,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    layer = gameObject.layer,
                    camera = exclusive ? cam : null
                };
                Graphics.RenderMeshInstanced(rp, _clump, 0, cell.Instances);
            }
        }
    }
}
