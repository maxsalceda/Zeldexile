using System;
using System.Collections.Generic;

namespace Zeldexile.Domain.Regions
{
    /// <summary>One placed object: which layer and model variant, where, and how it is turned and scaled.</summary>
    public struct ScatterInstance
    {
        public int Layer;
        public int Variant;
        public float U;
        public float V;
        /// <summary>Ground height above the lake at the instance.</summary>
        public float H;
        /// <summary>Degrees around the vertical.</summary>
        public float Yaw;
        public float Scale;
        /// <summary>Tilt in degrees (logs lie along the slope, rocks lean), around Yaw's right axis.</summary>
        public float Tilt;
        /// <summary>How far the base sits below the ground (rocks look embedded).</summary>
        public float Sink;
        /// <summary>Jitter cell of the layer grid. With the region and layer ids it makes a stable object id.</summary>
        public int CellU;
        public int CellV;
    }

    /// <summary>A family of objects scattered by rules over the region.</summary>
    public sealed class ScatterLayer
    {
        public string Id;
        /// <summary>Spacing of the jitter grid (one candidate per cell).</summary>
        public float Spacing;
        public int Variants = 1;
        public float MinScale = 1f;
        public float MaxScale = 1f;
        /// <summary>Footprint radius at scale 1, for the shared occupancy of solid objects (0 = not solid).</summary>
        public float Radius;
        /// <summary>Clear distance kept from path edges.</summary>
        public float PathClearance = 1f;
        public float MinNormalY = 0.75f;
        public float MaxTilt;
        public float SinkMin;
        public float SinkMax;
        public bool AllowInWater;
        /// <summary>Probability (0..1) of keeping the candidate at this spot.</summary>
        public Func<TerrainSample, float, float> Density;
        /// <summary>Optional: chooses the variant from the spot (e.g. orchard fruit kind).</summary>
        public Func<TerrainSample, int, int> PickVariant;
    }

    /// <summary>
    /// The scattering rules of the start region: trees, orchards, bushes, rocks, logs, branches, ferns, reeds and
    /// flowers, each placed from altitude, slope, moisture, distance to water and paths, and the cover areas.
    /// Deterministic: the same region data always produces the same objects, and nothing depends on other regions.
    /// </summary>
    public static class ScatterCatalog
    {
        public const string BroadTree = "arvore_copa";
        public const string Cypress = "cipreste";
        public const string OrangeTree = "laranjeira";
        public const string PearTree = "pereira";
        public const string Bush = "arbusto";
        public const string Boulder = "rocha_grande";
        public const string Rock = "rocha";
        public const string Pebble = "seixo";
        public const string Log = "tronco_caido";
        public const string Branch = "galho";
        public const string Stump = "toco";
        public const string Fern = "samambaia";
        public const string Reed = "junco";
        public const string FlowerWhite = "flor_branca";
        public const string FlowerYellow = "flor_amarela";

        /// <summary>Layers in placement order: big solids first, so smaller ones fill around them.</summary>
        public static ScatterLayer[] Default()
        {
            return new[]
            {
                new ScatterLayer
                {
                    Id = Boulder, Spacing = 20f, Variants = 3, MinScale = 2.6f, MaxScale = 5.5f, Radius = 0.85f,
                    PathClearance = 2.5f, MinNormalY = 0f, MaxTilt = 12f, SinkMin = 0.25f, SinkMax = 0.45f,
                    Density = (s, ny) => Dry(s, 2f) * Math.Max(Math.Max(s.Rock * 0.75f, s.RockyShore * 0.5f * Near(s.LakeDistance, 2f, 30f)),
                        Math.Max(s.Cliff * 0.9f, (1f - ny) * 0.9f + s.Falls * 0.6f))
                },
                new ScatterLayer
                {
                    Id = BroadTree, Spacing = 8.5f, Variants = 3, MinScale = 0.8f, MaxScale = 1.35f, Radius = 1.2f,
                    PathClearance = 2.2f, MinNormalY = 0.8f,
                    Density = (s, ny) => Dry(s, 3f) * Shade(s) * (1f - Orchard(s)) * (1f - s.Clearing) *
                        Math.Max(s.Forest * 0.78f, Math.Max(s.SparseForest * 0.3f, Math.Max(s.WoodedShore * 0.45f * Near(s.LakeDistance, 4f, 40f), 0.012f)))
                },
                new ScatterLayer
                {
                    Id = Cypress, Spacing = 7f, Variants = 2, MinScale = 0.8f, MaxScale = 1.3f, Radius = 0.8f,
                    PathClearance = 1.8f, MinNormalY = 0.75f,
                    Density = (s, ny) => Dry(s, 3f) * (1f - s.Clearing) * (1f - Orchard(s)) * Clump(s, 0.55f) *
                        Math.Max(s.Forest * 0.25f, Math.Max(s.SparseForest * 0.3f, Math.Max(s.Rock * 0.25f, 0.5f * Near(s.LakeDistance, 8f, 60f) * (1f - s.Beach))))
                },
                new ScatterLayer
                {
                    Id = OrangeTree, Spacing = 9f, Variants = 4, MinScale = 0.85f, MaxScale = 1.2f, Radius = 1f,
                    PathClearance = 1.8f, MinNormalY = 0.82f,
                    Density = (s, ny) => Dry(s, 4f) * (1f - s.Clearing) * Math.Max(Smooth(s.OrangeOrchard, 0.25f, 0.6f) * 0.82f,
                        0.035f * (1f - s.Forest) * Near(s.PathDistance, 3f, 14f))
                },
                new ScatterLayer
                {
                    Id = PearTree, Spacing = 9f, Variants = 4, MinScale = 0.85f, MaxScale = 1.2f, Radius = 1f,
                    PathClearance = 1.8f, MinNormalY = 0.82f,
                    Density = (s, ny) => Dry(s, 4f) * (1f - s.Clearing) * Math.Max(Smooth(s.PearOrchard, 0.25f, 0.6f) * 0.82f,
                        0.025f * (1f - s.Forest) * Near(s.PathDistance, 3f, 14f))
                },
                new ScatterLayer
                {
                    Id = Log, Spacing = 24f, Variants = 2, MinScale = 0.8f, MaxScale = 1.3f, Radius = 1.6f,
                    PathClearance = 2f, MinNormalY = 0.82f, MaxTilt = 4f, SinkMin = 0.05f, SinkMax = 0.15f,
                    Density = (s, ny) => Dry(s, 1.5f) * Math.Max(s.Forest * 0.45f, Math.Max(s.SparseForest * 0.25f, 0.35f * Near(s.StreamDistance, 1.5f, 10f) * Math.Max(s.Forest, s.SparseForest)))
                },
                new ScatterLayer
                {
                    Id = Stump, Spacing = 26f, Variants = 1, MinScale = 0.8f, MaxScale = 1.3f, Radius = 0.5f,
                    PathClearance = 1.5f, MinNormalY = 0.8f, SinkMin = 0.05f, SinkMax = 0.1f,
                    Density = (s, ny) => Dry(s, 2f) * Math.Max(s.Forest * 0.35f, s.SparseForest * 0.2f)
                },
                new ScatterLayer
                {
                    Id = Rock, Spacing = 7f, Variants = 3, MinScale = 0.7f, MaxScale = 1.8f, Radius = 0.7f,
                    PathClearance = 1.2f, MinNormalY = 0.35f, MaxTilt = 18f, SinkMin = 0.12f, SinkMax = 0.3f,
                    Density = (s, ny) => (s.Water == WaterKind.None ? 1f : 0.5f) * Math.Max(Math.Max(s.Rock * 0.55f, s.Falls * 0.7f),
                        Math.Max(0.45f * Near(s.StreamDistance, -0.5f, 4f), Math.Max(s.RockyShore * 0.5f * Near(s.LakeDistance, -2f, 12f), 0.03f + (1f - ny) * 0.6f)))
                },
                new ScatterLayer
                {
                    Id = Bush, Spacing = 4.5f, Variants = 3, MinScale = 0.7f, MaxScale = 1.4f, Radius = 0.7f,
                    PathClearance = 1f, MinNormalY = 0.7f,
                    Density = (s, ny) => Dry(s, 1.5f) * (1f - s.Clearing * 0.7f) * Math.Max(Edge(s.Forest) * 0.6f,
                        Math.Max(s.SparseForest * 0.18f, Math.Max(s.Rock * 0.2f, Math.Max(0.3f * Near(s.StreamDistance, 1f, 8f), 0.025f))))
                },
                new ScatterLayer
                {
                    Id = Pebble, Spacing = 2.6f, Variants = 3, MinScale = 0.25f, MaxScale = 0.7f, PathClearance = -0.6f,
                    MinNormalY = 0.4f, MaxTilt = 25f, SinkMin = 0.04f, SinkMax = 0.12f, AllowInWater = true,
                    Density = (s, ny) => Math.Max(Math.Max(s.Rock * 0.25f, 0.3f * Near(s.StreamDistance, -1.5f, 3f)),
                        Math.Max(0.25f * Near(Math.Abs(s.LakeDistance), 0f, 6f) * (s.Beach + s.RockyShore), Math.Max(s.Path * 0.06f, 0.008f)))
                },
                new ScatterLayer
                {
                    Id = Branch, Spacing = 5f, Variants = 3, MinScale = 0.7f, MaxScale = 1.3f, PathClearance = 0.4f,
                    MinNormalY = 0.75f, MaxTilt = 6f,
                    Density = (s, ny) => Dry(s, 1f) * Math.Max(s.Forest * 0.3f, Math.Max(s.SparseForest * 0.15f, Orchard(s) * 0.05f))
                },
                new ScatterLayer
                {
                    Id = Fern, Spacing = 2.4f, Variants = 2, MinScale = 0.7f, MaxScale = 1.3f, PathClearance = 0.4f,
                    MinNormalY = 0.6f,
                    Density = (s, ny) => Dry(s, 0.6f) * Math.Max(s.Forest * s.Moisture * 0.7f, 0.45f * Near(s.StreamDistance, 0.6f, 6f) * (0.4f + s.Forest + s.SparseForest)) * Clump(s, 0.35f)
                },
                new ScatterLayer
                {
                    Id = Reed, Spacing = 1.5f, Variants = 2, MinScale = 0.7f, MaxScale = 1.25f, PathClearance = 0.6f,
                    MinNormalY = 0.6f, AllowInWater = true,
                    Density = (s, ny) => Math.Max(
                        (0.15f + s.Reeds * 0.75f) * Band(s.LakeDistance, -4f, -0.5f, 1.5f) * (1f - s.Beach * 0.8f) * (1f - s.RockyShore),
                        0.32f * Band(s.StreamDistance, -1.2f, 0f, 1.2f)) * Clump(s, 0.3f)
                },
                new ScatterLayer
                {
                    Id = FlowerWhite, Spacing = 1.7f, Variants = 2, MinScale = 0.7f, MaxScale = 1.2f, PathClearance = 0.2f,
                    MinNormalY = 0.82f,
                    Density = (s, ny) => Dry(s, 0.5f) * Meadow(s) * (s.Flowers * 0.55f + 0.05f) * Clump(s, 0.4f)
                },
                new ScatterLayer
                {
                    Id = FlowerYellow, Spacing = 1.9f, Variants = 2, MinScale = 0.7f, MaxScale = 1.2f, PathClearance = 0.2f,
                    MinNormalY = 0.82f,
                    Density = (s, ny) => Dry(s, 0.5f) * Meadow(s) * (s.Flowers * 0.4f + 0.04f + Orchard(s) * 0.05f) * Clump(s, 0.55f)
                },
            };
        }

        private static float Dry(TerrainSample s, float fromWater) =>
            s.Water != WaterKind.None ? 0f : RegionMath.SmoothStep(0f, fromWater, Math.Min(s.LakeDistance, s.StreamDistance));

        private static float Orchard(TerrainSample s) => Math.Max(s.OrangeOrchard, s.PearOrchard);
        private static float Shade(TerrainSample s) => 1f - s.Cliff * 0.8f;
        private static float Meadow(TerrainSample s) => (1f - s.Forest * 0.85f) * (1f - s.Rock * 0.7f) * (1f - s.Cliff);
        private static float Edge(float forest) => RegionMath.Clamp01(forest * (1f - forest) * 4f);
        private static float Smooth(float x, float a, float b) => RegionMath.SmoothStep(a, b, x);
        /// <summary>1 at distance a or less, falling to 0 at b.</summary>
        private static float Near(float d, float a, float b) => 1f - RegionMath.SmoothStep(a, b, d);
        /// <summary>1 between lo and mid, fading to 0 at hi; 0 below lo.</summary>
        private static float Band(float d, float lo, float mid, float hi) =>
            d < lo ? 0f : d < mid ? RegionMath.SmoothStep(lo, mid, d) : 1f - RegionMath.SmoothStep(mid, hi, d);
        /// <summary>Groups small plants into patches using the low-frequency variation.</summary>
        private static float Clump(TerrainSample s, float threshold) => RegionMath.SmoothStep(threshold - 0.15f, threshold + 0.15f, s.Variation);
    }

    /// <summary>Runs the scatter layers over a region.</summary>
    public sealed class Scatterer
    {
        /// <summary>A spot kept free of solid objects (spawn, a landmark tree, a camp).</summary>
        public struct Reserve
        {
            public P2 Center;
            public float Radius;
        }

        private readonly HeightField _field;
        private readonly RegionTerrain _terrain;
        private readonly ScatterLayer[] _layers;
        private readonly List<Reserve> _reserved;
        private readonly Dictionary<long, List<(P2 p, float r)>> _solids = new Dictionary<long, List<(P2, float)>>();
        private const float SolidCell = 6f;

        public IReadOnlyList<ScatterLayer> Layers => _layers;

        public Scatterer(RegionTerrain terrain, HeightField field, ScatterLayer[] layers, List<Reserve> reserved)
        {
            _terrain = terrain;
            _field = field;
            _layers = layers;
            _reserved = reserved ?? new List<Reserve>();
        }

        /// <summary>Every instance of every layer, in a fixed order.</summary>
        public List<ScatterInstance> Run()
        {
            var result = new List<ScatterInstance>();
            var data = _field.Data;
            for (var l = 0; l < _layers.Length; l++)
            {
                var layer = _layers[l];
                var salt = RegionMath.HashString(layer.Id);
                var cellsU = (int)Math.Ceiling(data.sizeU / layer.Spacing);
                var cellsV = (int)Math.Ceiling(data.sizeV / layer.Spacing);
                for (var cv = 0; cv < cellsV; cv++)
                for (var cu = 0; cu < cellsU; cu++)
                {
                    var r0 = RegionMath.Hash01(data.seed, salt, cu, cv);
                    var r1 = RegionMath.Hash01(data.seed, salt + 1, cu, cv);
                    var u = (cu + 0.1f + r0 * 0.8f) * layer.Spacing;
                    var v = (cv + 0.1f + r1 * 0.8f) * layer.Spacing;
                    // Keep solids off the outermost meters: a future neighbor owns the other side of the seam.
                    if (u < 1f || v < 1f || u > data.sizeU - 1f || v > data.sizeV - 1f) continue;
                    if (layer.Radius > 0f && (u < 2.5f || v < 2.5f || u > data.sizeU - 2.5f || v > data.sizeV - 2.5f)) continue;

                    ref var s = ref _field.Nearest(u, v);
                    if (!layer.AllowInWater && s.Water != WaterKind.None) continue;
                    var ny = _field.NormalY(u, v);
                    if (ny < layer.MinNormalY) continue;
                    // Paths stay clear of the whole footprint of solid objects, not just their centers.
                    var scale = RegionMath.Lerp(layer.MinScale, layer.MaxScale, RegionMath.Hash01(data.seed, salt + 3, cu, cv));
                    var footprint = layer.Radius * scale;
                    if (s.PathDistance < PathHalfWidth(s.PathKind) + layer.PathClearance + footprint) continue;
                    if (NearBridge(u, v, (layer.Radius > 0f ? 6f : 2.5f) + footprint)) continue;

                    var density = layer.Density(s, ny);
                    if (RegionMath.Hash01(data.seed, salt + 2, cu, cv) >= density) continue;

                    var p = new P2(u, v);
                    if (layer.Radius > 0f)
                    {
                        if (Reserved(p, footprint) || !FreeOfSolids(p, footprint)) continue;
                        AddSolid(p, footprint);
                    }

                    var variant = (int)(RegionMath.Hash01(data.seed, salt + 4, cu, cv) * layer.Variants) % layer.Variants;
                    if (layer.PickVariant != null) variant = layer.PickVariant(s, variant);
                    result.Add(new ScatterInstance
                    {
                        Layer = l,
                        Variant = variant,
                        U = u,
                        V = v,
                        H = _field.Height(u, v),
                        Yaw = RegionMath.Hash01(data.seed, salt + 5, cu, cv) * 360f,
                        Scale = scale,
                        Tilt = (RegionMath.Hash01(data.seed, salt + 6, cu, cv) * 2f - 1f) * layer.MaxTilt,
                        Sink = RegionMath.Lerp(layer.SinkMin, layer.SinkMax, RegionMath.Hash01(data.seed, salt + 7, cu, cv)) * scale,
                        CellU = cu,
                        CellV = cv
                    });
                }
            }

            return result;
        }

        /// <summary>Stable id of an instance: region, layer and grid cell. Survives any change to other layers or regions.</summary>
        public string IdOf(ScatterInstance i) => $"{_field.Data.id}/{_layers[i.Layer].Id}/{i.CellU}_{i.CellV}";

        public static float PathHalfWidth(PathKind kind)
        {
            switch (kind)
            {
                case PathKind.Main: return 2.2f;
                case PathKind.Secondary: return 1.4f;
                case PathKind.Access: return 1.1f;
                default: return 0f;
            }
        }

        private bool NearBridge(float u, float v, float clearance)
        {
            var p = new P2(u, v);
            foreach (var b in _terrain.Bridges)
                if (RegionMath.SegmentDistance(p, b.A, b.B, out _) < b.Data.width * 0.5f + clearance)
                    return true;
            return false;
        }

        private bool Reserved(P2 p, float radius)
        {
            foreach (var r in _reserved)
                if (P2.Distance(p, r.Center) < r.Radius + radius)
                    return true;
            return false;
        }

        private bool FreeOfSolids(P2 p, float radius)
        {
            var cx = (int)Math.Floor(p.u / SolidCell);
            var cz = (int)Math.Floor(p.v / SolidCell);
            for (var z = cz - 2; z <= cz + 2; z++)
            for (var x = cx - 2; x <= cx + 2; x++)
            {
                if (!_solids.TryGetValue(Key(x, z), out var list)) continue;
                foreach (var (q, r) in list)
                    if (P2.Distance(p, q) < r + radius)
                        return false;
            }

            return true;
        }

        private void AddSolid(P2 p, float radius)
        {
            var key = Key((int)Math.Floor(p.u / SolidCell), (int)Math.Floor(p.v / SolidCell));
            if (!_solids.TryGetValue(key, out var list)) _solids[key] = list = new List<(P2, float)>();
            list.Add((p, radius));
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
