using UnityEngine;

namespace Zeldexile.World.Terrain
{
    /// <summary>
    /// Height of the prototype island at any (x, z), plus the color of each terrain face.
    /// The trials depend on these numbers:
    ///  - Mesa Alta: 26 m, more than base stamina can climb.
    ///  - Lake: the island is farther from every shore than base stamina can swim.
    ///  - Penhasco: 12 m, climbable.
    /// </summary>
    public static class IslandShape
    {
        public const float MinX = -80f, MaxX = 100f, MinZ = -80f, MaxZ = 80f;

        public const float WaterSurface = -0.4f;
        public const float LakeFloor = -4f;

        public static readonly Rect Mesa = Rect.MinMaxRect(-12f, -52f, 2f, -42f);
        public const float MesaHeight = 26f;
        public static readonly Rect Penhasco = Rect.MinMaxRect(-40f, 20f, -22f, 44f);
        public const float PenhascoHeight = 12f;
        public static readonly Rect Lake = Rect.MinMaxRect(12f, -29f, 68f, 29f);
        public static readonly Vector2 IslandCenter = new Vector2(40f, 0f);
        public const float IslandRadius = 2.5f;
        public const float IslandHeight = 0.6f;
        public static readonly Vector2 HillCenter = new Vector2(-5f, 29f);

        /// <summary>Areas kept nearly flat so trials, grass and buildings sit level.</summary>
        private static readonly Rect[] Flat =
        {
            Rect.MinMaxRect(-10f, -12f, 10f, 8f),   // spawn
            Rect.MinMaxRect(-16f, -54f, 14f, -26f), // grass field and mesa
            Rect.MinMaxRect(8f, -33f, 72f, 33f),    // lake shores
            Rect.MinMaxRect(66f, -20f, 94f, 16f),   // pillar gate
        };

        public static float Height(float x, float z)
        {
            var p = new Vector2(x, z);

            // Rolling ground, calmer near the trial areas.
            var flatten = 0f;
            foreach (var r in Flat) flatten = Mathf.Max(flatten, 1f - Smooth(0f, 10f, SignedDistance(p, r, 2f)));
            var rolling = (Mathf.PerlinNoise(x * 0.018f + 3.1f, z * 0.018f + 7.7f) - 0.45f) * 6f;
            var detail = (Mathf.PerlinNoise(x * 0.11f + 11f, z * 0.11f + 5f) - 0.5f) * 0.6f;
            var h = Mathf.Lerp(rolling + detail, detail * 0.5f, flatten);

            // Gentle hill (walkable).
            var hill = 1f - Smooth(0f, 9f, Vector2.Distance(p, HillCenter));
            h = Mathf.Max(h, 5f * hill * hill * (3f - 2f * hill));

            // Plateaus with cliff faces: full height inside the rect, falling off over a few meters at its edge.
            h = Plateau(h, p, Penhasco, PenhascoHeight, 2.5f, 3f);
            h = Plateau(h, p, Mesa, MesaHeight, 2.5f, 2f);

            // Lake basin and the island in the middle.
            var lake = 1f - Smooth(-3f, 0f, SignedDistance(p, Lake, 10f) + Warp(p, 4f));
            h = Mathf.Lerp(h, LakeFloor, lake);
            var island = 1f - Smooth(IslandRadius - 0.6f, IslandRadius + 1.2f, Vector2.Distance(p, IslandCenter));
            h = Mathf.Lerp(h, IslandHeight, island);

            // Mountains closing the world.
            var edge = Mathf.Min(Mathf.Min(x - MinX, MaxX - x), Mathf.Min(z - MinZ, MaxZ - z));
            var ridge = 1f - Smooth(2f, 16f, edge);
            h += ridge * (24f + Mathf.PerlinNoise(x * 0.07f, z * 0.07f) * 14f);
            return h;
        }

        /// <summary>Face color from its height and steepness (normal.y: 1 = flat, 0 = vertical).</summary>
        public static Color FaceColor(Vector3 center, float normalY, float variation)
        {
            Color c;
            if (normalY < 0.72f) c = Color.Lerp(new Color(0.55f, 0.47f, 0.40f), new Color(0.66f, 0.58f, 0.49f), variation);
            else if (center.y < WaterSurface - 0.3f) c = Color.Lerp(new Color(0.58f, 0.55f, 0.40f), new Color(0.48f, 0.47f, 0.36f), variation);
            else if (center.y < WaterSurface + 0.5f && InLakeArea(center)) c = Color.Lerp(new Color(0.88f, 0.80f, 0.56f), new Color(0.82f, 0.74f, 0.50f), variation);
            else if (center.y > 18f) c = Color.Lerp(new Color(0.53f, 0.70f, 0.33f), new Color(0.62f, 0.74f, 0.38f), variation);
            else c = Color.Lerp(new Color(0.40f, 0.62f, 0.27f), new Color(0.50f, 0.68f, 0.30f), variation);
            return c;
        }

        /// <summary>True where the terrain is green, gentle and dry: where decorative grass grows.</summary>
        public static bool IsGrassy(float x, float z)
        {
            const float e = 1f;
            var h = Height(x, z);
            var dx = Height(x + e, z) - Height(x - e, z);
            var dz = Height(x, z + e) - Height(x, z - e);
            var normalY = 1f / Mathf.Sqrt(1f + (dx * dx + dz * dz) / (4f * e * e));
            if (normalY < 0.85f || h < WaterSurface || h > 16f) return false;
            // Sandy shore around the lake (same rule as FaceColor).
            return !(h < WaterSurface + 0.5f && InLakeArea(new Vector3(x, h, z)));
        }

        private static bool InLakeArea(Vector3 p) => SignedDistance(new Vector2(p.x, p.z), Lake, 10f) < 6f;

        /// <summary>Low-frequency wobble added to shape edges so shores and cliffs are not straight lines.</summary>
        private static float Warp(Vector2 p, float amplitude) =>
            (Mathf.PerlinNoise(p.x * 0.07f + 41f, p.y * 0.07f + 13f) - 0.5f) * 2f * amplitude;

        private static float Plateau(float h, Vector2 p, Rect r, float top, float falloff, float warp)
        {
            var t = 1f - Smooth(-falloff, 0f, SignedDistance(p, r, 1.5f) + Warp(p, warp));
            return Mathf.Max(h, Mathf.Lerp(h, top, t));
        }

        /// <summary>Signed distance to a rounded rectangle (negative inside).</summary>
        public static float SignedDistance(Vector2 p, Rect r, float radius)
        {
            var half = r.size * 0.5f - Vector2.one * radius;
            var d = new Vector2(Mathf.Abs(p.x - r.center.x), Mathf.Abs(p.y - r.center.y)) - half;
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
        }

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
    }
}
