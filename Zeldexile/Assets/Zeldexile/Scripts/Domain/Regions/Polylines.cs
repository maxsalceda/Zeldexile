using System;
using System.Collections.Generic;

namespace Zeldexile.Domain.Regions
{
    /// <summary>A polyline with arc length, so values can be interpolated along it (stream beds, path heights).</summary>
    public sealed class Polyline
    {
        public readonly P2[] Points;
        /// <summary>Arc length at each point.</summary>
        public readonly float[] Distance;
        public readonly bool Closed;

        public Polyline(P2[] points, bool closed = false)
        {
            if (points == null || points.Length < 2) throw new ArgumentException("Polyline needs at least two points.");
            Closed = closed;
            Points = closed ? Append(points, points[0]) : points;
            Distance = new float[Points.Length];
            for (var i = 1; i < Points.Length; i++) Distance[i] = Distance[i - 1] + P2.Distance(Points[i - 1], Points[i]);
        }

        public float Length => Distance[Distance.Length - 1];
        public int SegmentCount => Points.Length - 1;

        /// <summary>Point and unit direction at arc length s.</summary>
        public P2 At(float s, out P2 direction)
        {
            var i = SegmentAt(s);
            var len = Distance[i + 1] - Distance[i];
            var t = len < 1e-6f ? 0f : (s - Distance[i]) / len;
            direction = (Points[i + 1] - Points[i]).Normalized;
            return P2.Lerp(Points[i], Points[i + 1], RegionMath.Clamp01(t));
        }

        public int SegmentAt(float s)
        {
            if (s <= 0f) return 0;
            for (var i = 0; i < Points.Length - 1; i++)
                if (s <= Distance[i + 1]) return i;
            return Points.Length - 2;
        }

        /// <summary>Distance from p to the polyline and the arc length of the closest point.</summary>
        public float Nearest(P2 p, out float s)
        {
            var best = float.MaxValue;
            s = 0f;
            for (var i = 0; i < Points.Length - 1; i++)
            {
                var d = RegionMath.SegmentDistance(p, Points[i], Points[i + 1], out var t);
                if (d >= best) continue;
                best = d;
                s = Distance[i] + (Distance[i + 1] - Distance[i]) * t;
            }

            return best;
        }

        private static P2[] Append(P2[] points, P2 last)
        {
            var result = new P2[points.Length + 1];
            Array.Copy(points, result, points.Length);
            result[points.Length] = last;
            return result;
        }
    }

    /// <summary>
    /// Bucket grid over the segments of many polylines, so "nearest line within r meters" is cheap for every
    /// terrain sample. Each bucket lists the segments whose padded bounds touch it.
    /// </summary>
    public sealed class SegmentIndex
    {
        public readonly struct Hit
        {
            public readonly int Line;
            public readonly float Distance;
            /// <summary>Arc length along the line.</summary>
            public readonly float S;

            public Hit(int line, float distance, float s)
            {
                Line = line;
                Distance = distance;
                S = s;
            }
        }

        private readonly List<Polyline> _lines;
        private readonly float _bucket;
        private readonly float _reach;
        private readonly Dictionary<long, List<(int line, int seg)>> _buckets = new Dictionary<long, List<(int, int)>>();

        public SegmentIndex(List<Polyline> lines, float reach, float bucket = 32f)
        {
            _lines = lines;
            _reach = reach;
            _bucket = bucket;
            for (var l = 0; l < lines.Count; l++)
            {
                var pts = lines[l].Points;
                for (var i = 0; i < pts.Length - 1; i++)
                {
                    var minU = Math.Min(pts[i].u, pts[i + 1].u) - reach;
                    var maxU = Math.Max(pts[i].u, pts[i + 1].u) + reach;
                    var minV = Math.Min(pts[i].v, pts[i + 1].v) - reach;
                    var maxV = Math.Max(pts[i].v, pts[i + 1].v) + reach;
                    for (var bz = Cell(minV); bz <= Cell(maxV); bz++)
                    for (var bx = Cell(minU); bx <= Cell(maxU); bx++)
                    {
                        var key = Key(bx, bz);
                        if (!_buckets.TryGetValue(key, out var list)) _buckets[key] = list = new List<(int, int)>();
                        list.Add((l, i));
                    }
                }
            }
        }

        public float Reach => _reach;

        /// <summary>For every line within reach of p, the closest point on it (one hit per line).</summary>
        public void Query(P2 p, List<Hit> hits)
        {
            hits.Clear();
            if (!_buckets.TryGetValue(Key(Cell(p.u), Cell(p.v)), out var list)) return;
            foreach (var (line, seg) in list)
            {
                var pl = _lines[line];
                var d = RegionMath.SegmentDistance(p, pl.Points[seg], pl.Points[seg + 1], out var t);
                if (d > _reach) continue;
                var s = pl.Distance[seg] + (pl.Distance[seg + 1] - pl.Distance[seg]) * t;
                var found = false;
                for (var h = 0; h < hits.Count; h++)
                {
                    if (hits[h].Line != line) continue;
                    found = true;
                    if (d < hits[h].Distance) hits[h] = new Hit(line, d, s);
                    break;
                }

                if (!found) hits.Add(new Hit(line, d, s));
            }
        }

        /// <summary>Every segment within reach of p (several per line: both legs of a hairpin are seen).</summary>
        public void QuerySegments(P2 p, List<Hit> hits)
        {
            hits.Clear();
            if (!_buckets.TryGetValue(Key(Cell(p.u), Cell(p.v)), out var list)) return;
            foreach (var (line, seg) in list)
            {
                var pl = _lines[line];
                var d = RegionMath.SegmentDistance(p, pl.Points[seg], pl.Points[seg + 1], out var t);
                if (d > _reach) continue;
                hits.Add(new Hit(line, d, pl.Distance[seg] + (pl.Distance[seg + 1] - pl.Distance[seg]) * t));
            }
        }

        private int Cell(float x) => (int)Math.Floor(x / _bucket);
        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }

    /// <summary>Signed distance to a closed outline (negative inside).</summary>
    public static class Polygon
    {
        public static float SignedDistance(P2[] outline, P2 p)
        {
            var best = float.MaxValue;
            var inside = false;
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            {
                var a = outline[j];
                var b = outline[i];
                best = Math.Min(best, RegionMath.SegmentDistance(p, a, b, out _));
                if ((b.v > p.v) != (a.v > p.v) && p.u < (a.u - b.u) * (p.v - b.v) / (a.v - b.v) + b.u) inside = !inside;
            }

            return inside ? -best : best;
        }

        public static float Area(P2[] outline)
        {
            var area = 0f;
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
                area += P2.Cross(outline[j], outline[i]);
            return Math.Abs(area) * 0.5f;
        }
    }
}
