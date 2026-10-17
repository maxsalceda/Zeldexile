using System;
using System.Collections.Generic;

namespace Zeldexile.Domain.Regions
{
    /// <summary>
    /// A logical cell of the world grid, addressed by integer coordinates that may be negative.
    /// Sector (0, 0) starts at the world origin; X grows east (U), Z grows north (V).
    /// The id ("s0_0", "s-1_2") is stable: it names objects and seeds, so adding sectors never renames old ones.
    /// </summary>
    public readonly struct SectorCoord : IEquatable<SectorCoord>
    {
        public readonly int X;
        public readonly int Z;

        public SectorCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public string Id => $"s{X}_{Z}";

        public SectorCoord North => new SectorCoord(X, Z + 1);
        public SectorCoord South => new SectorCoord(X, Z - 1);
        public SectorCoord East => new SectorCoord(X + 1, Z);
        public SectorCoord West => new SectorCoord(X - 1, Z);

        public bool Equals(SectorCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is SectorCoord other && Equals(other);
        public override int GetHashCode() => (X * 73856093) ^ (Z * 19349663);
        public static bool operator ==(SectorCoord a, SectorCoord b) => a.Equals(b);
        public static bool operator !=(SectorCoord a, SectorCoord b) => !a.Equals(b);
        public override string ToString() => Id;

        public static bool TryParse(string id, out SectorCoord coord)
        {
            coord = default;
            if (string.IsNullOrEmpty(id) || id[0] != 's') return false;
            var sep = id.IndexOf('_', 2);
            if (sep < 0) return false;
            if (!int.TryParse(id.Substring(1, sep - 1), out var x) || !int.TryParse(id.Substring(sep + 1), out var z)) return false;
            coord = new SectorCoord(x, z);
            return true;
        }
    }

    /// <summary>
    /// World grid: one sector size for the whole world (200 m by default). Converts world meters to sectors and back.
    /// Nothing here assumes how big the world is.
    /// </summary>
    public sealed class WorldGrid
    {
        public const float DefaultSectorSize = 200f;

        public float SectorSize { get; }

        public WorldGrid(float sectorSize = DefaultSectorSize)
        {
            if (sectorSize <= 0f) throw new ArgumentOutOfRangeException(nameof(sectorSize));
            SectorSize = sectorSize;
        }

        public SectorCoord SectorAt(float x, float z) =>
            new SectorCoord((int)Math.Floor(x / SectorSize), (int)Math.Floor(z / SectorSize));

        public P2 Origin(SectorCoord s) => new P2(s.X * SectorSize, s.Z * SectorSize);
        public P2 Center(SectorCoord s) => new P2((s.X + 0.5f) * SectorSize, (s.Z + 0.5f) * SectorSize);

        /// <summary>Every sector touched by a world-space rectangle (min inclusive, max exclusive).</summary>
        public IEnumerable<SectorCoord> Covering(float minX, float minZ, float maxX, float maxZ)
        {
            var a = SectorAt(minX, minZ);
            var b = SectorAt(maxX - 1e-3f, maxZ - 1e-3f);
            for (var z = a.Z; z <= b.Z; z++)
            for (var x = a.X; x <= b.X; x++)
                yield return new SectorCoord(x, z);
        }
    }

    /// <summary>
    /// Which region owns which sector. Regions register their sector rectangles; adding a region never moves
    /// another one, and nothing assumes a fixed list or world size.
    /// </summary>
    public sealed class WorldAtlas
    {
        private readonly Dictionary<SectorCoord, RegionData> _owners = new Dictionary<SectorCoord, RegionData>();
        private readonly List<RegionData> _regions = new List<RegionData>();

        public WorldGrid Grid { get; }
        public IReadOnlyList<RegionData> Regions => _regions;

        public WorldAtlas(WorldGrid grid) => Grid = grid;

        /// <summary>Registers a region. Throws if it overlaps a sector that already has an owner.</summary>
        public void Add(RegionData region)
        {
            foreach (var s in SectorsOf(region))
                if (_owners.TryGetValue(s, out var other))
                    throw new InvalidOperationException($"Setor {s.Id} já pertence à região '{other.id}' (nova: '{region.id}').");
            foreach (var s in SectorsOf(region)) _owners[s] = region;
            _regions.Add(region);
        }

        public RegionData RegionAt(SectorCoord s) => _owners.TryGetValue(s, out var r) ? r : null;

        public RegionData Find(string id) => _regions.Find(r => r.id == id);

        public IEnumerable<SectorCoord> SectorsOf(RegionData region) =>
            Grid.Covering(region.originX, region.originZ, region.originX + region.sizeU, region.originZ + region.sizeV);

        /// <summary>Sectors adjacent to a region that no region owns yet: where the world can grow.</summary>
        public IEnumerable<SectorCoord> OpenNeighbors(RegionData region)
        {
            var seen = new HashSet<SectorCoord>();
            foreach (var s in SectorsOf(region))
                foreach (var n in new[] { s.North, s.South, s.East, s.West })
                    if (!_owners.ContainsKey(n) && seen.Add(n))
                        yield return n;
        }
    }
}
