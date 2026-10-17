using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Draws thousands of nature props with GPU instancing, grouped in square cells so whole cells are culled by
    /// distance and frustum. Near cells use the detailed mesh with a contour; far cells switch to the low mesh.
    /// Small plants stop drawing beyond their own short distance. Nothing here is a GameObject per prop.
    /// </summary>
    public sealed class InstancedNature : MonoBehaviour
    {
        /// <summary>A model and how it is drawn.</summary>
        public sealed class Kind
        {
            public string Name;
            public Mesh Near;
            public Mesh Far;
            public bool Outline = true;
            public bool CastShadows = true;
            /// <summary>Beyond this, the far mesh (or nothing, without one).</summary>
            public float LodDistance = 140f;
            public float DrawDistance = 1500f;
            public float OutlineDistance = 90f;
            public float WindStrength = 1f;
        }

        private sealed class Batch
        {
            public Kind Kind;
            public Bounds Bounds;
            public readonly List<Matrix4x4> Matrices = new List<Matrix4x4>();
        }

        public const float CellSize = 50f;

        private readonly List<Kind> _kinds = new List<Kind>();
        private readonly Dictionary<(Kind, int, int), Batch> _lookup = new Dictionary<(Kind, int, int), Batch>();
        private readonly List<Batch> _batches = new List<Batch>();
        private Material _outlined;
        private Material _plain;
        private Material _outlinedStill;
        private Material _plainStill;

        public int InstanceCount { get; private set; }
        public int BatchCount => _batches.Count;
        /// <summary>Instances and draw calls submitted in the last frame (for the performance report).</summary>
        public int DrawnInstances { get; private set; }
        public int DrawnBatches { get; private set; }

        private static readonly int WindId = Shader.PropertyToID("_WindStrength");

        private void Awake()
        {
            _outlined = Make("Materials/FolhagemContorno", "Zeldexile/FoliageOutline", 1f);
            _plain = Make("Materials/Folhagem", "Zeldexile/Foliage", 1f);
            _outlinedStill = Make("Materials/FolhagemContorno", "Zeldexile/FoliageOutline", 0f);
            _plainStill = Make("Materials/Folhagem", "Zeldexile/Foliage", 0f);
        }

        private static Material Make(string saved, string shaderName, float wind)
        {
            // Saved materials keep the instanced shader variants in builds (see RegionSceneBuilder).
            var asset = Resources.Load<Material>(saved);
            var shader = Shader.Find(shaderName);
            var m = asset != null ? new Material(asset) : shader != null ? new Material(shader) : null;
            if (m == null) return null;
            m.enableInstancing = true;
            m.SetFloat(WindId, wind);
            return m;
        }

        public Kind AddKind(Kind kind)
        {
            _kinds.Add(kind);
            return kind;
        }

        public void Add(Kind kind, Matrix4x4 matrix, float radius)
        {
            var p = matrix.GetColumn(3);
            var key = (kind, Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize));
            if (!_lookup.TryGetValue(key, out var batch))
            {
                batch = new Batch { Kind = kind, Bounds = new Bounds(p, Vector3.zero) };
                _lookup[key] = batch;
                _batches.Add(batch);
            }

            batch.Matrices.Add(matrix);
            var extent = Vector3.one * radius;
            batch.Bounds.Encapsulate(new Bounds((Vector3)p + Vector3.up * radius, extent * 2f));
            InstanceCount++;
        }

        // Update (not LateUpdate) so captures rendered from a coroutine in the same frame include the props.
        private void Update()
        {
            var cam = Camera.main;
            if (cam == null || _plain == null) return;
            Draw(cam, false);
        }

        /// <summary>Submits every visible batch for a camera. exclusive: only that camera draws them (captures).</summary>
        public void Draw(Camera cam, bool exclusive)
        {
            var eye = cam.transform.position;
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var shadowRange = QualitySettings.shadowDistance + CellSize;
            DrawnInstances = 0;
            DrawnBatches = 0;
            foreach (var b in _batches)
            {
                var k = b.Kind;
                var d2 = b.Bounds.SqrDistance(eye);
                if (d2 > k.DrawDistance * k.DrawDistance) continue;
                var castsShadow = k.CastShadows && d2 < shadowRange * shadowRange;
                if (!castsShadow && !GeometryUtility.TestPlanesAABB(planes, b.Bounds)) continue;
                var near = d2 < k.LodDistance * k.LodDistance || k.Far == null;
                var mesh = near ? k.Near : k.Far;
                if (mesh == null) continue;
                var outline = near && k.Outline && d2 < k.OutlineDistance * k.OutlineDistance;
                var still = k.WindStrength <= 0f;
                var material = outline ? (still ? _outlinedStill : _outlined) : (still ? _plainStill : _plain);
                var rp = new RenderParams(material)
                {
                    worldBounds = b.Bounds,
                    shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    layer = gameObject.layer,
                    camera = exclusive ? cam : null
                };
                Graphics.RenderMeshInstanced(rp, mesh, 0, b.Matrices);
                DrawnInstances += b.Matrices.Count;
                DrawnBatches++;
            }
        }
    }
}
