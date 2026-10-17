using System;
using System.Collections.Generic;

namespace Zeldexile.Domain.Regions
{
    public enum WaterKind : byte
    {
        None,
        Lake,
        Stream
    }

    public enum PathKind : byte
    {
        None,
        Main,
        Secondary,
        Access
    }

    /// <summary>Everything the builders need to know about one spot of the region.</summary>
    public struct TerrainSample
    {
        /// <summary>Ground height above the lake surface (meters).</summary>
        public float H;
        public WaterKind Water;
        /// <summary>Water surface height above the lake surface (0 for the lake), when Water is not None.</summary>
        public float WaterSurface;
        /// <summary>Signed distance to the lake shore (negative in the lake).</summary>
        public float LakeDistance;
        /// <summary>Distance from the edge of the nearest stream channel (negative inside it).</summary>
        public float StreamDistance;
        /// <summary>0..1 where a stream falls steeply (rock and spray around waterfalls).</summary>
        public float Falls;
        public float Beach, RockyShore, Reeds, WoodedShore;
        /// <summary>0..1 trodden path surface.</summary>
        public float Path;
        public PathKind PathKind;
        /// <summary>Distance from the centerline of the nearest path (large when none is near).</summary>
        public float PathDistance;
        public float Forest, SparseForest, OrangeOrchard, PearOrchard, Flowers, Rock, Clearing;
        /// <summary>0..1 on the cliff faces of plateaus.</summary>
        public float Cliff;
        public float Moisture;
        /// <summary>Low-frequency 0..1 noise for color and density variation.</summary>
        public float Variation;
    }

    /// <summary>
    /// The relief, water and land cover of one region, computed from its data:
    /// rolling ground and hills, limestone plateaus, the lake basin and its shores, stream channels with falls,
    /// graded paths with bridge ramps, and soft cover areas. Deterministic for a given RegionData.
    /// Heights are relative to the region's lake surface (world Y = lakeLevel + H).
    /// </summary>
    public sealed class RegionTerrain
    {
        private sealed class StreamLine
        {
            public StreamData Data;
            public Polyline Line;
            /// <summary>Water surface every ProfileStep meters, never rising downstream.</summary>
            public float[] Surface;
        }

        private sealed class PathLine
        {
            public PathData Data;
            public Polyline Line;
            public PathKind Kind;
            /// <summary>Graded centerline height every ProfileStep meters.</summary>
            public float[] Profile;
        }

        public sealed class BridgeSpan
        {
            public BridgeData Data;
            public P2 A, B;
            /// <summary>Deck top height above the lake surface.</summary>
            public float Deck;
            public float WaterSurface;
            public string StreamId;
        }

        private const float ProfileStep = 2f;

        public RegionData Data { get; }
        public IReadOnlyList<BridgeSpan> Bridges => _bridges;

        private readonly int _seed;
        private readonly List<StreamLine> _streams = new List<StreamLine>();
        private readonly List<PathLine> _paths = new List<PathLine>();
        private readonly List<BridgeSpan> _bridges = new List<BridgeSpan>();
        private readonly SegmentIndex _streamIndex;
        private readonly SegmentIndex _pathIndex;
        private readonly Func<float, float, float?> _neighborEdgeY;
        // One hit list per thread: the height field bakes rows in parallel.
        [ThreadStatic] private static List<SegmentIndex.Hit> _threadHits;
        private static List<SegmentIndex.Hit> Hits => _threadHits ??= new List<SegmentIndex.Hit>();

        /// <param name="neighborEdgeY">
        /// Edge contract: world Y of an existing neighbor region at a point on this region's border (world X, Z),
        /// or null where there is no neighbor. This region's border blends into it, so the seam is exact.
        /// </param>
        public RegionTerrain(RegionData data, Func<float, float, float?> neighborEdgeY = null)
        {
            Data = data;
            _seed = data.seed;
            _neighborEdgeY = neighborEdgeY;

            foreach (var s in data.streams)
            {
                var pts = new P2[s.points.Length];
                for (var i = 0; i < pts.Length; i++) pts[i] = new P2(s.points[i].u, s.points[i].v);
                _streams.Add(new StreamLine { Data = s, Line = new Polyline(pts) });
            }

            foreach (var st in _streams) BuildStreamProfile(st);
            _streamIndex = new SegmentIndex(_streams.ConvertAll(s => s.Line), 40f);

            foreach (var p in data.paths)
                _paths.Add(new PathLine { Data = p, Line = new Polyline(p.points, p.loop), Kind = KindOf(p.kind) });
            _pathIndex = new SegmentIndex(_paths.ConvertAll(p => p.Line), 16f);

            BuildBridges();
            foreach (var p in _paths) BuildProfile(p);
        }

        public static PathKind KindOf(string kind)
        {
            switch (kind)
            {
                case "principal": return PathKind.Main;
                case "secundaria": return PathKind.Secondary;
                default: return PathKind.Access;
            }
        }

        public float WorldY(float h) => Data.lakeLevel + h;

        // ------------------------------------------------------------------ sampling

        public TerrainSample Sample(float u, float v)
        {
            var s = new TerrainSample { PathDistance = 999f, StreamDistance = 999f };
            var p = new P2(u, v);
            s.H = Relief(p, out s.Cliff);
            s.Variation = RegionMath.Fbm(u / 55f, v / 55f, _seed + 401, 2) * 0.5f + 0.5f;
            Cover(p, ref s);
            ApplyLake(p, ref s);
            ApplyStreams(p, ref s, out var channel);
            ApplyPaths(p, ref s, channel);
            ApplyNeighborEdges(p, ref s);

            var toWater = Math.Min(s.LakeDistance < 0f ? 0f : s.LakeDistance, Math.Max(0f, s.StreamDistance));
            s.Moisture = RegionMath.Clamp01(Math.Max(1f - RegionMath.SmoothStep(0f, 45f, toWater), s.Forest * 0.55f) - s.Cliff * 0.5f);
            return s;
        }

        /// <summary>Ground before water and paths: rolling base, hills, small detail, then plateaus.</summary>
        private float Relief(P2 p, out float cliff)
        {
            var r = Data.relief;
            var h = r.baseHeight
                    + r.rollAmplitude * RegionMath.Fbm(p.u / r.rollWavelength, p.v / r.rollWavelength, _seed + 11, 3)
                    + r.undulationAmplitude * RegionMath.Fbm(p.u / r.undulationWavelength, p.v / r.undulationWavelength, _seed + 23, 2)
                    + r.detailAmplitude * RegionMath.Fbm(p.u / r.detailWavelength, p.v / r.detailWavelength, _seed + 37, 2);

            for (var i = 0; i < Data.hills.Length; i++)
            {
                var hill = Data.hills[i];
                var d = Ellipse(p, hill.u, hill.v, hill.radius * Math.Max(0.05f, hill.stretch), hill.radius, hill.angle);
                d *= 1f + hill.warp * RegionMath.Fbm(p.u / 140f, p.v / 140f, _seed + 1000 + i * 17, 2);
                if (d >= 1f) continue;
                var f = 1f - RegionMath.SmoothStep(0f, 1f, d);
                h += hill.height * (float)Math.Pow(f, hill.sharpness);
            }

            // Soft ceiling, so stacked hills never spike.
            if (h > r.maxHeight * 0.85f) h = r.maxHeight * 0.85f + (h - r.maxHeight * 0.85f) * 0.35f;

            cliff = 0f;
            for (var i = 0; i < Data.plateaus.Length; i++)
            {
                var pl = Data.plateaus[i];
                var dn = Ellipse(p, pl.u, pl.v, pl.ru, pl.rv, pl.angle);
                var sd = (dn - 1f) * Math.Min(pl.ru, pl.rv) + pl.warp * RegionMath.Fbm(p.u / 35f, p.v / 35f, _seed + 2000 + i * 31, 2);
                if (sd > 0f) continue;
                var t = 1f - RegionMath.SmoothStep(-pl.falloff, 0f, sd);
                var stepped = Ledges(t, pl.ledges);
                var top = pl.top + 0.6f * RegionMath.Fbm(p.u / 18f, p.v / 18f, _seed + 2100 + i, 2);
                var lifted = RegionMath.Lerp(h, top, stepped);
                if (lifted > h)
                {
                    h = lifted;
                    cliff = Math.Max(cliff, t > 0.02f && t < 0.98f ? 1f : 0f);
                }
            }

            return h;
        }

        private static float Ledges(float t, int ledges)
        {
            if (ledges <= 0) return t;
            var k = ledges + 1;
            var x = t * k;
            var i = (float)Math.Floor(x);
            return Math.Min(1f, (i + RegionMath.SmoothStep(0.25f, 0.75f, x - i)) / k);
        }

        /// <summary>Normalized distance inside a rotated ellipse (1 on its outline).</summary>
        private static float Ellipse(P2 p, float cu, float cv, float ru, float rv, float angleDeg)
        {
            var a = angleDeg * (float)Math.PI / 180f;
            var c = (float)Math.Cos(a);
            var s = (float)Math.Sin(a);
            var du = p.u - cu;
            var dv = p.v - cv;
            var x = (du * c + dv * s) / ru;
            var y = (-du * s + dv * c) / rv;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private void Cover(P2 p, ref TerrainSample s)
        {
            for (var i = 0; i < Data.areas.Length; i++)
            {
                var a = Data.areas[i];
                var dn = Ellipse(p, a.u, a.v, a.ru, a.rv, a.angle);
                dn *= 1f + 0.22f * RegionMath.Fbm(p.u / 60f, p.v / 60f, _seed + 3000 + i * 7, 2);
                if (dn >= 1f) continue;
                var m = a.strength * (1f - RegionMath.SmoothStep(1f - a.falloff, 1f, dn));
                switch (a.kind)
                {
                    case "bosque": s.Forest = Math.Max(s.Forest, m); break;
                    case "bosque_ralo": s.SparseForest = Math.Max(s.SparseForest, m); break;
                    case "pomar_laranja": s.OrangeOrchard = Math.Max(s.OrangeOrchard, m); break;
                    case "pomar_pera": s.PearOrchard = Math.Max(s.PearOrchard, m); break;
                    case "prado_florido": s.Flowers = Math.Max(s.Flowers, m); break;
                    case "rochedo": s.Rock = Math.Max(s.Rock, m); break;
                    case "clareira": s.Clearing = Math.Max(s.Clearing, m); break;
                }
            }
        }

        // ------------------------------------------------------------------ lake

        private void ApplyLake(P2 p, ref TerrainSample s)
        {
            var lake = Data.lake;
            if (lake == null || lake.shore.Length < 3)
            {
                s.LakeDistance = 999f;
                return;
            }

            var sd = Polygon.SignedDistance(lake.shore, p);
            if (sd > lake.shoreBand + 30f)
            {
                s.LakeDistance = sd;
                return;
            }

            sd += lake.shoreWarp * RegionMath.Fbm(p.u / 38f, p.v / 38f, _seed + 51, 2);
            s.LakeDistance = sd;
            foreach (var z in lake.zones)
            {
                var w = 1f - RegionMath.SmoothStep(z.radius * 0.55f, z.radius, P2.Distance(p, new P2(z.u, z.v)));
                switch (z.kind)
                {
                    case "praia": s.Beach = Math.Max(s.Beach, w); break;
                    case "rochosa": s.RockyShore = Math.Max(s.RockyShore, w); break;
                    case "juncos": s.Reeds = Math.Max(s.Reeds, w); break;
                    case "arborizada": s.WoodedShore = Math.Max(s.WoodedShore, w); break;
                }
            }

            var rocky = s.RockyShore;
            var beach = s.Beach * (1f - rocky);
            if (sd < 0f)
            {
                var inside = -sd;
                var shelf = RegionMath.Lerp(lake.shelfWidth * (1f + beach * 0.8f), 3f, rocky);
                var depth = inside < shelf
                    ? lake.shelfDepth * inside / shelf
                    : lake.shelfDepth + (lake.maxDepth - lake.shelfDepth) * RegionMath.SmoothStep(shelf, shelf + 70f, inside);
                depth += 0.25f * RegionMath.Fbm(p.u / 12f, p.v / 12f, _seed + 61, 2) * RegionMath.SmoothStep(0f, 4f, inside);
                s.H = Math.Min(s.H, -Math.Max(0.05f, depth));
                s.Water = WaterKind.Lake;
                s.WaterSurface = 0f;
                return;
            }

            // Shore: from just above the water up to the regular ground. Sand rises gently, rock rises fast.
            var rise = RegionMath.Lerp(RegionMath.Lerp(0.11f, 0.045f, beach), 0.9f, rocky);
            var profile = 0.12f + sd * rise;
            var blend = RegionMath.SmoothStep(0f, lake.shoreBand * (1f - 0.6f * rocky), sd);
            s.H = Math.Max(0.1f + sd * 0.02f, RegionMath.Lerp(Math.Min(profile, s.H), s.H, blend));
        }

        // ------------------------------------------------------------------ streams

        /// <summary>Carves every stream; channel = 0..1 how much this spot belongs to a channel (paths stay off it).</summary>
        private void ApplyStreams(P2 p, ref TerrainSample s, out float channel)
        {
            channel = 0f;
            var hits = Hits;
            _streamIndex.Query(p, hits);
            foreach (var hit in hits)
            {
                var st = _streams[hit.Line];
                StreamAt(st, hit.S, out var bed, out var width, out var depth, out var slope);
                var half = width * 0.5f;
                var ws = bed + depth;
                var d = hit.Distance;
                var bank = st.Data.bankWidth + 0.45f * Math.Max(0f, s.H - ws);
                if (d > half + bank) continue;

                var target = d < half
                    ? bed + (depth + 0.28f) * (d / half) * (d / half)
                    : ws + 0.28f + (d - half) * 0.1f;
                var t = RegionMath.SmoothStep(half, half + bank, d);
                var inLake = s.Water == WaterKind.Lake;
                if (s.H > target) s.H = RegionMath.Lerp(target, s.H, t);
                else if (!inLake && d < half + 1.5f && s.LakeDistance > 2f) s.H = target;

                var edge = d - half;
                if (edge < s.StreamDistance) s.StreamDistance = edge;
                channel = Math.Max(channel, 1f - RegionMath.SmoothStep(half + 1.2f, half + 3.2f, d));
                s.Falls = Math.Max(s.Falls, RegionMath.SmoothStep(0.25f, 0.7f, slope) * (1f - RegionMath.SmoothStep(half, half + 9f, d)));
                // Water where the bowl dips under the surface (the lake already counts as water).
                if (!inLake && s.H < ws - 0.02f && d < half)
                {
                    s.Water = WaterKind.Stream;
                    s.WaterSurface = ws;
                }
            }
        }

        /// <summary>Bed height, width, depth and bed steepness at arc length s of a stream.</summary>
        private static void StreamAt(StreamLine st, float s, out float bed, out float width, out float depth, out float slope)
        {
            WidthAt(st, s, out width, out depth);
            var surface = SurfaceAt(st, s);
            bed = surface - depth;
            slope = (SurfaceAt(st, s - 2f) - SurfaceAt(st, s + 2f)) / 4f;
        }

        private static void WidthAt(StreamLine st, float s, out float width, out float depth)
        {
            var pts = st.Data.points;
            var i = st.Line.SegmentAt(s);
            var len = st.Line.Distance[i + 1] - st.Line.Distance[i];
            var t = len < 1e-4f ? 0f : RegionMath.Clamp01((s - st.Line.Distance[i]) / len);
            width = RegionMath.Lerp(pts[i].width, pts[i + 1].width, t);
            depth = RegionMath.Lerp(pts[i].depth, pts[i + 1].depth, t);
        }

        private static float SurfaceAt(StreamLine st, float s)
        {
            var f = RegionMath.Clamp(s, 0f, st.Line.Length) / ProfileStep;
            var i = (int)Math.Floor(f);
            if (i >= st.Surface.Length - 1) return st.Surface[st.Surface.Length - 1];
            return RegionMath.Lerp(st.Surface[i], st.Surface[i + 1], f - i);
        }

        /// <summary>
        /// Water surface along a stream: the lowest ground across the channel, sunk by the incision, never rising
        /// downstream; falls drop it at their points, and steep reaches settle into pools and small cascades.
        /// </summary>
        private void BuildStreamProfile(StreamLine st)
        {
            var d = st.Data;
            var count = (int)Math.Ceiling(st.Line.Length / ProfileStep) + 1;
            var surface = new float[count];
            var exitS = 0f;
            for (var i = 0; i < count; i++)
            {
                var s = Math.Min(i * ProfileStep, st.Line.Length);
                var p = st.Line.At(s, out var dir);
                WidthAt(st, s, out var width, out _);
                var side = dir.Left * (width * 0.5f + 4f);
                var ground = Math.Min(GroundBeforeStreams(p, out var lakeSd), Math.Min(GroundBeforeStreams(p + side, out _), GroundBeforeStreams(p - side, out _)));
                surface[i] = ground - d.incision;
                if (d.leavesLake)
                {
                    // Out of the lake the surface eases down from lake level instead of stepping.
                    if (lakeSd < 0f) exitS = s;
                    surface[i] = lakeSd < 0f ? 0f : Math.Min(0f, Math.Max(surface[i], -0.025f * (s - exitS)));
                }

                if (i > 0) surface[i] = Math.Min(surface[i], surface[i - 1]);

                // A fall lowers the level at its point; the running minimum carries it downstream
                // only until the natural bed catches up.
                foreach (var pt in d.points)
                {
                    if (pt.fall <= 0f) continue;
                    var at = st.Line.Nearest(new P2(pt.u, pt.v), out var fallS);
                    if (at < 0.5f && s >= fallS && s < fallS + ProfileStep) surface[i] -= pt.fall;
                }
            }

            // Steep reaches: hold the level (pool), then drop a step (cascade).
            if (d.cascadeStep > 0.1f)
            {
                var stepped = new float[count];
                for (var i = 0; i < count; i++)
                {
                    var a = surface[Math.Max(0, i - 6)];
                    var b = surface[Math.Min(count - 1, i + 6)];
                    var slope = (a - b) / (ProfileStep * (Math.Min(count - 1, i + 6) - Math.Max(0, i - 6)) + 1e-3f);
                    var q = (float)Math.Floor(surface[i] / d.cascadeStep) * d.cascadeStep;
                    stepped[i] = RegionMath.Lerp(surface[i], q, RegionMath.SmoothStep(0.05f, 0.12f, slope));
                }

                for (var i = 1; i < count; i++) stepped[i] = Math.Min(stepped[i], stepped[i - 1]);
                surface = stepped;
            }

            if (d.feedsLake)
                for (var i = 0; i < count; i++) surface[i] = Math.Max(surface[i], 0f);

            // A tributary meets the stream it feeds at that stream's surface.
            var host = _streams.Find(o => o.Data.id == d.mouth && o.Surface != null);
            if (host != null)
            {
                var end = st.Line.Points[st.Line.Points.Length - 1];
                host.Line.Nearest(end, out var hs);
                var floor = SurfaceAt(host, hs);
                for (var i = 0; i < count; i++) surface[i] = Math.Max(surface[i], floor);
            }

            st.Surface = surface;
        }

        /// <summary>Ground with relief and lake only (what a stream cuts into).</summary>
        private float GroundBeforeStreams(P2 p, out float lakeDistance)
        {
            var s = new TerrainSample { PathDistance = 999f, StreamDistance = 999f };
            s.H = Relief(p, out _);
            ApplyLake(p, ref s);
            lakeDistance = s.LakeDistance;
            return s.H;
        }

        /// <summary>Water surface of a stream at arc length s (used for its visible surface).</summary>
        public float StreamSurface(int stream, float s, out float width, out float slope)
        {
            StreamAt(_streams[stream], s, out var bed, out width, out var depth, out slope);
            return bed + depth;
        }

        public int StreamCount => _streams.Count;
        public Polyline StreamLineOf(int i) => _streams[i].Line;
        public StreamData StreamDataOf(int i) => _streams[i].Data;

        // ------------------------------------------------------------------ paths and bridges

        private void ApplyPaths(P2 p, ref TerrainSample s, float channel)
        {
            if (s.Water == WaterKind.Lake && s.LakeDistance < -1f) return;
            var hits = Hits;
            _pathIndex.QuerySegments(p, hits);
            // Every nearby stretch proposes a height; the strongest wins, and near hairpins and junctions, where
            // stretches tie, their heights blend into one landing instead of a step.
            float maxWeight = 0f, sum = 0f, weights = 0f;
            var onBridge = OnBridge(p);
            var ramp = NearBridgeEnd(p);
            foreach (var hit in hits)
            {
                var pl = _paths[hit.Line];
                var half = PathHalfWidth(hit.Line, hit.S);
                var target = ProfileAt(pl, hit.S);
                // Bridge approaches are widened into a firm ramp as wide as the deck.
                var graded = Math.Max(half, ramp);
                var shoulder = 2.5f + 1.1f * Math.Abs(s.H - target);
                var w = 1f - RegionMath.SmoothStep(graded, graded + shoulder, hit.Distance);
                if (onBridge) w = 0f;
                w *= 1f - channel;
                if (w > 0f)
                {
                    var k = w * w * w * w;
                    sum += target * k;
                    weights += k;
                    maxWeight = Math.Max(maxWeight, w);
                }

                if (hit.Distance < s.PathDistance)
                {
                    s.PathDistance = hit.Distance;
                    var edgeNoise = 0.35f * RegionMath.Noise(p.u / 2.3f, p.v / 2.3f, _seed + 83);
                    var surface = 1f - RegionMath.SmoothStep(half * 0.7f, half * 1.12f + edgeNoise, hit.Distance);
                    if (surface > s.Path)
                    {
                        s.Path = surface;
                        s.PathKind = pl.Kind;
                    }
                }
            }

            if (maxWeight > 0f) s.H = RegionMath.Lerp(s.H, sum / weights, maxWeight);
            if (s.Water != WaterKind.None) s.Path *= 0.2f;
        }

        /// <summary>Half-width of the graded ground near a bridge end (0 away from bridges).</summary>
        private float NearBridgeEnd(P2 p)
        {
            var best = 0f;
            foreach (var b in _bridges)
            {
                var d = Math.Min(P2.Distance(p, b.A), P2.Distance(p, b.B));
                if (d < 7f) best = Math.Max(best, (b.Data.width * 0.5f + 1.2f) * (1f - RegionMath.SmoothStep(3f, 7f, d)));
            }

            return best;
        }

        private bool OnBridge(P2 p)
        {
            foreach (var b in _bridges)
            {
                // The span over the water stays ungraded; its first and last meters are solid ground (abutments).
                var d = RegionMath.SegmentDistance(p, b.A, b.B, out var t);
                var len = P2.Distance(b.A, b.B);
                if (d < b.Data.width * 0.5f + 0.6f && t * len > 3.5f && (1f - t) * len > 3.5f) return true;
            }

            return false;
        }

        private static float ProfileAt(PathLine pl, float s)
        {
            var f = s / ProfileStep;
            var i = (int)Math.Floor(f);
            if (i >= pl.Profile.Length - 1) return pl.Profile[pl.Profile.Length - 1];
            if (i < 0) return pl.Profile[0];
            return RegionMath.Lerp(pl.Profile[i], pl.Profile[i + 1], f - i);
        }

        /// <summary>Half the trodden width of a path at arc length s: it widens and narrows a little along the way.</summary>
        public float PathHalfWidth(int path, float s) =>
            _paths[path].Data.width * 0.5f * (1f + 0.16f * RegionMath.Noise(s / 22f, path * 3.7f, _seed + 71));

        /// <summary>Graded centerline height of a path at arc length s.</summary>
        public float PathHeight(int path, float s) => ProfileAt(_paths[path], s);
        public int PathCount => _paths.Count;
        public Polyline PathLineOf(int i) => _paths[i].Line;
        public PathData PathDataOf(int i) => _paths[i].Data;

        /// <summary>Ground before paths, used to grade them.</summary>
        public float GroundBeforePaths(P2 p)
        {
            var s = new TerrainSample { PathDistance = 999f, StreamDistance = 999f };
            s.H = Relief(p, out _);
            ApplyLake(p, ref s);
            ApplyStreams(p, ref s, out _);
            return s.H;
        }

        private void BuildBridges()
        {
            foreach (var b in Data.bridges)
            {
                var span = new BridgeSpan { Data = b, A = new P2(b.u0, b.v0), B = new P2(b.u1, b.v1) };
                // The stream under the middle of the bridge sets the clearance.
                var mid = P2.Lerp(span.A, span.B, 0.5f);
                var ws = -1f;
                for (var i = 0; i < _streams.Count; i++)
                {
                    var d = _streams[i].Line.Nearest(mid, out var at);
                    if (d > 12f) continue;
                    ws = StreamSurface(i, at, out _, out _);
                    span.StreamId = _streams[i].Data.id;
                    break;
                }

                span.WaterSurface = ws;
                var ends = (GroundBeforePaths(span.A) + GroundBeforePaths(span.B)) * 0.5f;
                span.Deck = Math.Max(ws + b.clearance + 0.35f, ends);
                _bridges.Add(span);
            }
        }

        private void BuildProfile(PathLine pl)
        {
            var count = (int)Math.Ceiling(pl.Line.Length / ProfileStep) + 1;
            var raw = new float[count];
            var fixedAt = new bool[count];
            var fixedH = new float[count];
            for (var i = 0; i < count; i++)
            {
                var pt = pl.Line.At(Math.Min(i * ProfileStep, pl.Line.Length), out _);
                raw[i] = Math.Max(GroundBeforePaths(pt), 0.25f);
                foreach (var b in _bridges)
                {
                    var d = RegionMath.SegmentDistance(pt, b.A, b.B, out var t);
                    // Over the span and a few meters past each end, the path is the deck.
                    var len = P2.Distance(b.A, b.B);
                    var extra = 3f / Math.Max(1f, len);
                    if (d < 2.5f && t > -extra && t < 1f + extra)
                    {
                        fixedAt[i] = true;
                        fixedH[i] = b.Deck;
                    }
                }

                // Where it meets a path graded before it, it takes that path's height, so junctions have no step.
                if (fixedAt[i]) continue;
                foreach (var other in _paths)
                {
                    if (other == pl || other.Profile == null) continue;
                    var d = other.Line.Nearest(pt, out var os);
                    if (d > other.Data.width * 0.5f + 1.5f) continue;
                    fixedAt[i] = true;
                    fixedH[i] = ProfileAt(other, os);
                    break;
                }
            }

            // Smooth out bumps (about 12 m on each side), then hold the deck heights and limit the grade.
            var smooth = new float[count];
            const int window = 6;
            for (var i = 0; i < count; i++)
            {
                float sum = 0f, wsum = 0f;
                for (var k = -window; k <= window; k++)
                {
                    var j = pl.Data.loop ? (i + k + count) % count : Math.Max(0, Math.Min(count - 1, i + k));
                    var w = window + 1 - Math.Abs(k);
                    sum += raw[j] * w;
                    wsum += w;
                }

                smooth[i] = fixedAt[i] ? fixedH[i] : sum / wsum;
            }

            var g = pl.Data.maxGrade * ProfileStep;
            for (var pass = 0; pass < 3; pass++)
            {
                for (var i = 1; i < count; i++)
                    if (!fixedAt[i]) smooth[i] = RegionMath.Clamp(smooth[i], smooth[i - 1] - g, smooth[i - 1] + g);
                for (var i = count - 2; i >= 0; i--)
                    if (!fixedAt[i]) smooth[i] = RegionMath.Clamp(smooth[i], smooth[i + 1] - g, smooth[i + 1] + g);
            }

            pl.Profile = smooth;
        }

        // ------------------------------------------------------------------ neighbors

        private void ApplyNeighborEdges(P2 p, ref TerrainSample s)
        {
            if (_neighborEdgeY == null) return;
            var band = Data.relief.edgeBand;
            Blend(ref s, p.u, Data.originX, Data.originZ + p.v, band);                     // west edge
            Blend(ref s, Data.sizeU - p.u, Data.originX + Data.sizeU, Data.originZ + p.v, band); // east
            Blend(ref s, p.v, Data.originX + p.u, Data.originZ, band);                    // south
            Blend(ref s, Data.sizeV - p.v, Data.originX + p.u, Data.originZ + Data.sizeV, band); // north
        }

        /// <summary>Pulls the ground toward the neighbor's height at the closest point of a shared edge.</summary>
        private void Blend(ref TerrainSample s, float distance, float edgeX, float edgeZ, float band)
        {
            if (distance > band) return;
            var y = _neighborEdgeY(edgeX, edgeZ);
            if (!y.HasValue) return;
            var t = RegionMath.SmoothStep(0f, band, distance);
            s.H = RegionMath.Lerp(y.Value - Data.lakeLevel, s.H, t);
        }
    }
}
