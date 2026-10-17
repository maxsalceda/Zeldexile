using System;

namespace Zeldexile.Domain.Regions
{
    // Region description loaded from JSON (Resources/World/Regions/*.json). Plain public fields, so both
    // Unity's JsonUtility and other tools read them. Horizontal positions are region-local meters:
    // U from west to east, V from south to north, (0, 0) at the region's south-west corner.
    // Heights ("h", "bed", "top") are meters above the region's lake surface (lakeLevel in world Y).

    [Serializable]
    public sealed class RegionData
    {
        public string id;
        public string name;
        public int seed;
        /// <summary>World position (X, Z) of the region's U = 0, V = 0 corner. Multiple of the sector size.</summary>
        public float originX;
        public float originZ;
        public float sizeU = 1000f;
        public float sizeV = 1000f;
        /// <summary>World Y of the reference surface (the main lake).</summary>
        public float lakeLevel = 20f;
        /// <summary>Terrain grid spacing in meters. Must divide the sector size.</summary>
        public float cellSize = 2.5f;

        public ReliefData relief = new ReliefData();
        public HillData[] hills = new HillData[0];
        public PlateauData[] plateaus = new PlateauData[0];
        public LakeData lake;
        public StreamData[] streams = new StreamData[0];
        public PathData[] paths = new PathData[0];
        public BridgeData[] bridges = new BridgeData[0];
        public AreaData[] areas = new AreaData[0];
        public PoiData[] pois = new PoiData[0];
        public ConnectionData[] connections = new ConnectionData[0];
        public SpawnData spawn = new SpawnData();
        public ShotData[] shots = new ShotData[0];
    }

    [Serializable]
    public sealed class ReliefData
    {
        /// <summary>Height of the open ground where no hill rises.</summary>
        public float baseHeight = 8f;
        /// <summary>Large rolling ground: amplitude (m) and wavelength (m).</summary>
        public float rollAmplitude = 5f;
        public float rollWavelength = 260f;
        /// <summary>Medium undulation of the fields.</summary>
        public float undulationAmplitude = 1.6f;
        public float undulationWavelength = 70f;
        /// <summary>Small irregularities (bumps under roots, mounds).</summary>
        public float detailAmplitude = 0.35f;
        public float detailWavelength = 9f;
        public float maxHeight = 130f;
        /// <summary>Width of the band along an edge that blends into a neighbor region's edge.</summary>
        public float edgeBand = 60f;
    }

    /// <summary>A rounded hill. Overlapping hills add up; warp makes the outline irregular.</summary>
    [Serializable]
    public sealed class HillData
    {
        public string id;
        public float u;
        public float v;
        public float radius = 100f;
        public float height = 20f;
        /// <summary>Length of the hill along its angle relative to its width (1 = round).</summary>
        public float stretch = 1f;
        /// <summary>Direction of the long axis, degrees from +U toward +V.</summary>
        public float angle;
        /// <summary>1 = soft dome, above 1 = narrower top, below 1 = broader shoulders.</summary>
        public float sharpness = 1f;
        public float warp = 0.18f;
    }

    /// <summary>A flat-topped rock mass (butte) with cliff faces, the pale limestone landmark of the valley.</summary>
    [Serializable]
    public sealed class PlateauData
    {
        public string id;
        public float u;
        public float v;
        public float ru = 40f;
        public float rv = 30f;
        public float angle;
        public float top = 60f;
        /// <summary>Horizontal width of the cliff face, meters.</summary>
        public float falloff = 8f;
        public float warp = 6f;
        /// <summary>Terrace steps on the cliff (0 = smooth face).</summary>
        public int ledges = 2;
    }

    [Serializable]
    public sealed class LakeData
    {
        public string id = "lago";
        public string name = "Lago";
        /// <summary>Shore outline, counterclockwise. The water surface is h = 0.</summary>
        public P2[] shore = new P2[0];
        public float maxDepth = 7f;
        /// <summary>Distance from the shore over which the bottom stays shallow (wadeable).</summary>
        public float shelfWidth = 14f;
        public float shelfDepth = 1.1f;
        /// <summary>Distance inland over which the ground rises from the water to the regular terrain.</summary>
        public float shoreBand = 60f;
        public float shoreWarp = 3f;
        public ShoreZone[] zones = new ShoreZone[0];
    }

    /// <summary>Character of a stretch of shore: "praia" (sand), "rochosa" (steep rock), "arborizada", "juncos".</summary>
    [Serializable]
    public sealed class ShoreZone
    {
        public string kind;
        public float u;
        public float v;
        public float radius = 60f;
    }

    /// <summary>
    /// A stream or river. Its bed follows the ground under the centerline (never rising downstream), sunk by
    /// <see cref="incision"/>; steep reaches turn into small cascades, and points with a "fall" get a waterfall.
    /// </summary>
    [Serializable]
    public sealed class StreamData
    {
        public string id;
        public string name;
        /// <summary>"riacho" or "rio" (only changes defaults and labels).</summary>
        public string kind = "riacho";
        /// <summary>Centerline from source to mouth.</summary>
        public StreamPoint[] points = new StreamPoint[0];
        /// <summary>Base width of the banks that blend the channel into the ground.</summary>
        public float bankWidth = 5f;
        /// <summary>How far the water surface sits below the surrounding ground.</summary>
        public float incision = 1f;
        /// <summary>Height of the automatic cascade steps on steep reaches (0 = none).</summary>
        public float cascadeStep = 1.4f;
        /// <summary>Starts in the lake: the water surface begins at lake level and only falls from there.</summary>
        public bool leavesLake;
        /// <summary>Ends in the lake: the water surface never drops below lake level.</summary>
        public bool feedsLake;
        /// <summary>"lago" when it ends in the lake, another stream's id, or an edge ("leste") when it leaves the region.</summary>
        public string mouth = "lago";
    }

    [Serializable]
    public sealed class StreamPoint
    {
        public float u;
        public float v;
        public float width = 4f;
        /// <summary>Water depth over the bed.</summary>
        public float depth = 0.5f;
        /// <summary>Waterfall at this point: the bed drops this many meters here (0 = none).</summary>
        public float fall;
    }

    [Serializable]
    public sealed class PathData
    {
        public string id;
        public string name;
        /// <summary>"principal", "secundaria" or "acesso".</summary>
        public string kind = "principal";
        public float width = 4f;
        public P2[] points = new P2[0];
        public bool loop;
        /// <summary>Steepest rise per meter the path is graded to (0.2 = 11 degrees).</summary>
        public float maxGrade = 0.2f;
    }

    [Serializable]
    public sealed class BridgeData
    {
        public string id;
        public string name;
        public float u0;
        public float v0;
        public float u1;
        public float v1;
        /// <summary>Usable deck width.</summary>
        public float width = 3f;
        /// <summary>Minimum gap between the water and the underside of the deck.</summary>
        public float clearance = 1.1f;
    }

    /// <summary>
    /// Land cover painted over the relief: "bosque", "bosque_ralo", "pomar_laranja", "pomar_pera",
    /// "prado_florido", "rochedo", "clareira". An ellipse with a soft, noisy edge.
    /// </summary>
    [Serializable]
    public sealed class AreaData
    {
        public string id;
        public string kind;
        public float u;
        public float v;
        public float ru = 80f;
        public float rv = 80f;
        public float angle;
        public float strength = 1f;
        /// <summary>Width of the soft edge as a fraction of the radius.</summary>
        public float falloff = 0.45f;
    }

    [Serializable]
    public sealed class PoiData
    {
        public string id;
        public string name;
        /// <summary>"mirante", "arvore_anciã", "clareira", "enseada", "ponte", "cachoeira", "rocha", "raizes", "peninsula", "prado".</summary>
        public string kind;
        public float u;
        public float v;
        public string note;
    }

    /// <summary>Where a future neighbor region connects. Height is filled in from the built terrain.</summary>
    [Serializable]
    public sealed class ConnectionData
    {
        public string id;
        public string name;
        /// <summary>"norte", "sul", "leste" or "oeste".</summary>
        public string edge;
        public float u;
        public float v;
        /// <summary>Direction of travel out of the region, degrees from +U toward +V.</summary>
        public float direction;
        public float width;
        public string terrain;
        public string vegetation;
        public string material;
        public string water;
        public string restrictions;
    }

    [Serializable]
    public sealed class SpawnData
    {
        public float u = 500f;
        public float v = 110f;
        /// <summary>Facing, degrees clockwise from north (Unity yaw).</summary>
        public float yaw;
    }

    /// <summary>A documented camera position for captures and visual comparisons.</summary>
    [Serializable]
    public sealed class ShotData
    {
        public string id;
        public string name;
        public float u;
        public float v;
        /// <summary>Camera height above the ground.</summary>
        public float above = 2f;
        public float yaw;
        public float pitch = 8f;
        public float fov = 55f;
    }
}
