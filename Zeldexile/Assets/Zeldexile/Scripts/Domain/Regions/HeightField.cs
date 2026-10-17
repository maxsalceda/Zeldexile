using System;

namespace Zeldexile.Domain.Regions
{
    /// <summary>
    /// The region's terrain baked on its grid (one sample per grid corner). Heights read back here match the
    /// rendered mesh exactly: the same triangles, with the diagonal alternating by world cell parity.
    /// Neighboring sectors and regions share their edge samples, so seams have no gaps.
    /// </summary>
    public sealed class HeightField
    {
        public readonly RegionData Data;
        public readonly float Cell;
        /// <summary>Grid corners per row (cells + 1).</summary>
        public readonly int CountU;
        public readonly int CountV;
        public readonly TerrainSample[] Samples;
        /// <summary>World cell index of the first corner (for the diagonal parity shared by every region).</summary>
        private readonly int _parityU, _parityV;

        public HeightField(RegionTerrain terrain)
        {
            Data = terrain.Data;
            Cell = Data.cellSize;
            CountU = (int)Math.Round(Data.sizeU / Cell) + 1;
            CountV = (int)Math.Round(Data.sizeV / Cell) + 1;
            _parityU = (int)Math.Round(Data.originX / Cell);
            _parityV = (int)Math.Round(Data.originZ / Cell);
            Samples = new TerrainSample[CountU * CountV];
            // Rows are independent (the terrain model is read-only once built): bake them on every core.
            System.Threading.Tasks.Parallel.For(0, CountV, j =>
            {
                for (var i = 0; i < CountU; i++)
                    Samples[j * CountU + i] = terrain.Sample(i * Cell, j * Cell);
            });
        }

        public ref TerrainSample At(int i, int j) => ref Samples[Clamp(j, CountV) * CountU + Clamp(i, CountU)];

        /// <summary>True when the triangle split of cell (i, j) runs from its (0,0) corner to its (1,1) corner.</summary>
        public bool MainDiagonal(int i, int j) => (((i + _parityU) + (j + _parityV)) & 1) == 0;

        public bool Contains(float u, float v) => u >= 0f && v >= 0f && u <= Data.sizeU && v <= Data.sizeV;

        /// <summary>Ground height above the lake at (u, v), on the rendered triangles.</summary>
        public float Height(float u, float v)
        {
            Locate(u, v, out var i, out var j, out var fx, out var fz);
            var a = At(i, j).H;
            var b = At(i, j + 1).H;
            var c = At(i + 1, j + 1).H;
            var d = At(i + 1, j).H;
            if (MainDiagonal(i, j))
                return fz > fx ? a + (c - b) * fx + (b - a) * fz : a + (d - a) * fx + (c - d) * fz;
            return fx + fz < 1f ? a + (d - a) * fx + (b - a) * fz : c + (b - c) * (1f - fx) + (d - c) * (1f - fz);
        }

        /// <summary>Upward component of the ground normal (1 flat, 0 vertical), from the cell's triangle.</summary>
        public float NormalY(float u, float v)
        {
            const float e = 0.6f;
            var dx = (Height(u + e, v) - Height(u - e, v)) / (2f * e);
            var dz = (Height(u, v + e) - Height(u, v - e)) / (2f * e);
            return 1f / (float)Math.Sqrt(1f + dx * dx + dz * dz);
        }

        /// <summary>The nearest baked sample (cover, water, path data).</summary>
        public ref TerrainSample Nearest(float u, float v) =>
            ref At((int)Math.Round(u / Cell), (int)Math.Round(v / Cell));

        public float WorldY(float u, float v) => Data.lakeLevel + Height(u, v);

        private void Locate(float u, float v, out int i, out int j, out float fx, out float fz)
        {
            var gx = RegionMath.Clamp(u / Cell, 0f, CountU - 1.0001f);
            var gz = RegionMath.Clamp(v / Cell, 0f, CountV - 1.0001f);
            i = (int)Math.Floor(gx);
            j = (int)Math.Floor(gz);
            fx = gx - i;
            fz = gz - j;
        }

        private static int Clamp(int x, int count) => x < 0 ? 0 : x >= count ? count - 1 : x;
    }

    /// <summary>Dominant land cover of each cell, counted once, for the region's coverage report.</summary>
    public enum Cover
    {
        Meadow,
        Forest,
        Orchard,
        Water,
        Rock
    }

    public static class CoverStats
    {
        public static Cover Classify(in TerrainSample s, float normalY)
        {
            if (s.Water != WaterKind.None) return Cover.Water;
            if (normalY < 0.72f || s.Cliff > 0.5f || s.Rock > 0.55f || s.RockyShore > 0.6f && s.LakeDistance < 25f) return Cover.Rock;
            var orchard = Math.Max(s.OrangeOrchard, s.PearOrchard);
            var forest = Math.Max(s.Forest, s.SparseForest * 0.85f) * (1f - s.Clearing);
            if (orchard > 0.45f && orchard >= forest) return Cover.Orchard;
            if (forest > 0.45f) return Cover.Forest;
            return Cover.Meadow;
        }

        /// <summary>Share of the region (0..1) under each cover, indexed by (int)Cover.</summary>
        public static float[] Measure(HeightField field)
        {
            var counts = new float[5];
            var total = 0f;
            for (var j = 0; j < field.CountV - 1; j++)
            for (var i = 0; i < field.CountU - 1; i++)
            {
                var u = (i + 0.5f) * field.Cell;
                var v = (j + 0.5f) * field.Cell;
                counts[(int)Classify(field.Nearest(u, v), field.NormalY(u, v))]++;
                total++;
            }

            for (var k = 0; k < counts.Length; k++) counts[k] /= total;
            return counts;
        }
    }
}
