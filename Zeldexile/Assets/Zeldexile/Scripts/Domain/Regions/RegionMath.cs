using System;

namespace Zeldexile.Domain.Regions
{
    /// <summary>A point or direction on the horizontal plane, in meters (U = west to east, V = south to north).</summary>
    [Serializable]
    public struct P2
    {
        public float u;
        public float v;

        public P2(float u, float v)
        {
            this.u = u;
            this.v = v;
        }

        public float Length => (float)Math.Sqrt(u * u + v * v);
        public P2 Normalized => Length > 1e-6f ? this / Length : new P2(0f, 0f);
        /// <summary>Rotated 90 degrees counterclockwise (left of the direction of travel).</summary>
        public P2 Left => new P2(-v, u);

        public static P2 operator +(P2 a, P2 b) => new P2(a.u + b.u, a.v + b.v);
        public static P2 operator -(P2 a, P2 b) => new P2(a.u - b.u, a.v - b.v);
        public static P2 operator *(P2 a, float k) => new P2(a.u * k, a.v * k);
        public static P2 operator /(P2 a, float k) => new P2(a.u / k, a.v / k);
        public static float Dot(P2 a, P2 b) => a.u * b.u + a.v * b.v;
        public static float Cross(P2 a, P2 b) => a.u * b.v - a.v * b.u;
        public static float Distance(P2 a, P2 b) => (a - b).Length;
        public static P2 Lerp(P2 a, P2 b, float t) => new P2(a.u + (b.u - a.u) * t, a.v + (b.v - a.v) * t);

        public override string ToString() => $"({u:0.#}, {v:0.#})";
    }

    /// <summary>Scalar helpers and deterministic noise. Pure C#, so heights never depend on the engine version.</summary>
    public static class RegionMath
    {
        public static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        public static float Clamp(float x, float a, float b) => x < a ? a : x > b ? b : x;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float x) => Math.Abs(b - a) < 1e-9f ? 0f : Clamp01((x - a) / (b - a));

        /// <summary>0 below a, 1 above b, smooth in between (b may be less than a for a falling edge).</summary>
        public static float SmoothStep(float a, float b, float x)
        {
            var t = Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Smooth maximum: like max, with a rounded blend of width k.</summary>
        public static float SmoothMax(float a, float b, float k)
        {
            var h = Clamp01(0.5f + 0.5f * (a - b) / k);
            return Lerp(b, a, h) + k * h * (1f - h);
        }

        /// <summary>Stable 32-bit hash of integers (seed, cells, layer ids).</summary>
        public static uint Hash(int a, int b = 0, int c = 0, int d = 0)
        {
            unchecked
            {
                var h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h = (h ^ (uint)d) * 16777619u;
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                h *= 0x297a2d39u;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>Stable hash of a string id (FNV-1a), for seeds named in data.</summary>
        public static int HashString(string s)
        {
            unchecked
            {
                var h = 2166136261u;
                foreach (var ch in s ?? string.Empty) h = (h ^ ch) * 16777619u;
                return (int)h;
            }
        }

        public static float Hash01(int a, int b = 0, int c = 0, int d = 0) => (Hash(a, b, c, d) & 0xFFFFFF) / 16777216f;

        /// <summary>Gradient noise in [-1, 1], continuous everywhere (including negative coordinates).</summary>
        public static float Noise(float x, float y, int seed)
        {
            var x0 = (int)Math.Floor(x);
            var y0 = (int)Math.Floor(y);
            var fx = x - x0;
            var fy = y - y0;
            var u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            var v = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            var a = Grad(x0, y0, seed, fx, fy);
            var b = Grad(x0 + 1, y0, seed, fx - 1f, fy);
            var c = Grad(x0, y0 + 1, seed, fx, fy - 1f);
            var d = Grad(x0 + 1, y0 + 1, seed, fx - 1f, fy - 1f);
            return Lerp(Lerp(a, b, u), Lerp(c, d, u), v) * 1.42f;
        }

        /// <summary>Fractal noise in about [-1, 1].</summary>
        public static float Fbm(float x, float y, int seed, int octaves)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 101) * amp;
                norm += amp;
                amp *= 0.5f;
                x *= 2.03f;
                y *= 2.03f;
            }

            return sum / norm;
        }

        private static float Grad(int ix, int iy, int seed, float dx, float dy)
        {
            var angle = Hash(ix, iy, seed) * (2.0 * Math.PI / 4294967296.0);
            return (float)(Math.Cos(angle) * dx + Math.Sin(angle) * dy);
        }

        /// <summary>Distance from p to segment ab, with t = position along it (0..1).</summary>
        public static float SegmentDistance(P2 p, P2 a, P2 b, out float t)
        {
            var ab = b - a;
            var len2 = P2.Dot(ab, ab);
            t = len2 < 1e-9f ? 0f : Clamp01(P2.Dot(p - a, ab) / len2);
            return P2.Distance(p, a + ab * t);
        }
    }
}
