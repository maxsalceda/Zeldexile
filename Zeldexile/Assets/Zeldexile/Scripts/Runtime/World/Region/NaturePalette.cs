using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain.Regions;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Colors of the valley: model slots (baked into vertex colors of the instanced nature meshes) and the
    /// ground, picked to match the mockups — vivid but controlled greens, warm wood, pale limestone, clear water.
    /// One place to retune the look of the whole region.
    /// </summary>
    public static class NaturePalette
    {
        private static readonly Dictionary<string, Color> Slots = new Dictionary<string, Color>
        {
            { "bark", new Color(0.50f, 0.33f, 0.21f) },
            { "barkdark", new Color(0.36f, 0.24f, 0.16f) },
            { "leaf", new Color(0.38f, 0.66f, 0.21f) },
            { "leafdark", new Color(0.22f, 0.49f, 0.20f) },
            { "leaflight", new Color(0.56f, 0.78f, 0.27f) },
            { "cypress", new Color(0.16f, 0.40f, 0.22f) },
            { "orange", new Color(1.00f, 0.56f, 0.12f) },
            { "pear", new Color(0.96f, 0.80f, 0.24f) },
            { "fruitleaf", new Color(0.30f, 0.58f, 0.20f) },
            { "stone", new Color(0.83f, 0.81f, 0.74f) },
            { "stonelight", new Color(0.91f, 0.89f, 0.82f) },
            { "stonedark", new Color(0.68f, 0.66f, 0.60f) },
            { "moss", new Color(0.44f, 0.60f, 0.23f) },
            { "woodcut", new Color(0.87f, 0.70f, 0.46f) },
            { "fern", new Color(0.30f, 0.60f, 0.24f) },
            { "reed", new Color(0.56f, 0.68f, 0.30f) },
            { "cattail", new Color(0.46f, 0.30f, 0.18f) },
            { "stem", new Color(0.34f, 0.58f, 0.22f) },
            { "petal", new Color(0.98f, 0.98f, 0.95f) },
            { "petalyellow", new Color(1.00f, 0.84f, 0.20f) },
            { "flowercenter", new Color(0.99f, 0.74f, 0.16f) },
            { "wood", new Color(0.66f, 0.44f, 0.24f) },
            { "wooddark", new Color(0.47f, 0.31f, 0.18f) },
            { "rope", new Color(0.82f, 0.72f, 0.52f) },
            { "litter", new Color(0.64f, 0.47f, 0.24f) },
            { "grass", new Color(0.42f, 0.68f, 0.26f) },
        };

        /// <summary>How much each slot moves in the wind (vertex alpha).</summary>
        public static float Sway(string slot)
        {
            switch (slot)
            {
                case "leaf":
                case "leafdark":
                case "leaflight":
                case "cypress":
                case "orange":
                case "pear":
                case "petal":
                case "petalyellow":
                case "flowercenter":
                case "reed":
                case "cattail":
                case "stem":
                case "fern":
                case "fruitleaf":
                    return 1f;
                case "bark":
                    return 0.15f;
                default:
                    return 0f;
            }
        }

        public static Color Slot(string slot) => Slots.TryGetValue(slot, out var c) ? c : new Color(1f, 0f, 1f);

        // ---------------- ground

        private static readonly Color GrassA = new Color(0.40f, 0.66f, 0.22f);
        private static readonly Color GrassB = new Color(0.50f, 0.73f, 0.27f);
        private static readonly Color GrassHigh = new Color(0.55f, 0.70f, 0.32f);
        private static readonly Color GrassDry = new Color(0.62f, 0.70f, 0.30f);
        private static readonly Color ForestFloor = new Color(0.27f, 0.46f, 0.19f);
        private static readonly Color ForestLitter = new Color(0.42f, 0.42f, 0.20f);
        private static readonly Color Dirt = new Color(0.76f, 0.60f, 0.38f);
        private static readonly Color DirtEdge = new Color(0.62f, 0.56f, 0.32f);
        private static readonly Color Sand = new Color(0.90f, 0.83f, 0.60f);
        private static readonly Color SandWet = new Color(0.74f, 0.67f, 0.48f);
        private static readonly Color LakeBedShallow = new Color(0.78f, 0.72f, 0.52f);
        private static readonly Color LakeBedDeep = new Color(0.36f, 0.42f, 0.32f);
        private static readonly Color StreamBed = new Color(0.66f, 0.62f, 0.50f);
        private static readonly Color Rock = new Color(0.80f, 0.78f, 0.71f);
        private static readonly Color RockDark = new Color(0.66f, 0.64f, 0.58f);
        private static readonly Color RockWarm = new Color(0.84f, 0.79f, 0.68f);
        private static readonly Color Mud = new Color(0.48f, 0.44f, 0.30f);

        /// <summary>
        /// Color of one terrain face from the land data at its center, its steepness and a per-face hash.
        /// Paths read as packed earth with grassy, irregular edges; shores turn to sand; cliffs show strata.
        /// </summary>
        public static Color Ground(in TerrainSample s, float h, float normalY, float hash)
        {
            // Water beds.
            if (s.Water == WaterKind.Lake)
            {
                var depth = Mathf.Clamp01(-h / 5f);
                var bed = Color.Lerp(LakeBedShallow, LakeBedDeep, depth);
                return Vary(Color.Lerp(bed, RockDark, s.RockyShore * 0.5f), hash, 0.08f);
            }

            if (s.Water == WaterKind.Stream) return Vary(Color.Lerp(StreamBed, RockDark, hash * 0.5f), hash, 0.1f);

            // Steep faces and cliffs: pale limestone in horizontal bands.
            var steep = Mathf.InverseLerp(0.8f, 0.62f, normalY);
            var rockiness = Mathf.Max(steep, Mathf.Max(s.Cliff, s.Falls * 0.7f));
            var band = Mathf.Repeat(h * 0.55f, 1f) < 0.5f ? Rock : RockWarm;
            var rock = Color.Lerp(band, RockDark, hash * 0.45f);

            // Grass: lighter in the open, warmer on dry heights, darker under trees.
            var grass = Color.Lerp(GrassA, GrassB, Mathf.SmoothStep(0f, 1f, s.Variation));
            grass = Color.Lerp(grass, GrassHigh, Mathf.InverseLerp(30f, 90f, h) * 0.7f);
            grass = Color.Lerp(grass, GrassDry, (1f - s.Moisture) * 0.25f * (1f - s.Forest));
            var shade = Mathf.Max(s.Forest, s.SparseForest * 0.5f) * (1f - s.Clearing);
            var floor = Color.Lerp(ForestFloor, ForestLitter, hash > 0.72f ? 0.6f : 0f);
            grass = Color.Lerp(grass, floor, shade * 0.85f);
            var orchard = Mathf.Max(s.OrangeOrchard, s.PearOrchard);
            grass = Color.Lerp(grass, GrassDry, orchard * 0.12f);

            // Shores: sand on beaches, darker wet sand at the waterline, mud by reeds and streams.
            var nearLake = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 9f, s.LakeDistance));
            var beach = s.Beach * (1f - s.RockyShore) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 16f, s.LakeDistance)));
            var shore = Color.Lerp(Sand, SandWet, 1f - Mathf.InverseLerp(0.2f, 1.2f, h));
            var muddy = Mathf.Max(nearLake * (1f - s.Beach) * 0.55f, (1f - Mathf.InverseLerp(0f, 1.6f, s.StreamDistance)) * 0.6f);

            var c = grass;
            c = Color.Lerp(c, Mud, muddy * (1f - s.RockyShore));
            c = Color.Lerp(c, shore, beach);
            c = Color.Lerp(c, rock, Mathf.Max(rockiness, nearLake * s.RockyShore));

            // Under paths: trampled, yellowed grass. The earth itself is the path strip drawn on top
            // (PathRibbonBuilder), whose smooth edge the terrain faces could not draw.
            if (s.Path > 0.02f)
            {
                var dirt = Color.Lerp(DirtEdge, Dirt, 0.35f);
                c = Color.Lerp(c, dirt, s.Path * 0.7f);
            }

            return Vary(c, hash, 0.045f);
        }

        private static Color Vary(Color c, float hash, float amount)
        {
            var k = 1f + (hash - 0.5f) * 2f * amount;
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }
    }
}
