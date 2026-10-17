using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Zeldexile.Characters;
using Zeldexile.Domain;
using Zeldexile.Domain.Regions;
using Zeldexile.Player;
using Zeldexile.World;
using Zeldexile.World.Region;
using Debug = UnityEngine.Debug;

namespace Zeldexile.Gameplay
{
    /// <summary>
    /// Builds an open-world region from its data (Resources/World/Regions/{RegionId}.json) when the scene starts:
    /// relief, lake and streams, graded paths, bridges, scattered nature, grass, fruit and branches to collect, a camp
    /// at the entrance, the horizon and the region limits; then the player at the entrance. Everything is seeded, so
    /// every run builds the same valley. The island prototype keeps its own world (PrototypeWorld).
    /// </summary>
    public sealed class RegionWorld : MonoBehaviour
    {
        public string RegionId = "vale_inicial";

        public static RegionWorld Current { get; private set; }

        public WorldGrid Grid { get; private set; }
        public WorldAtlas Atlas { get; private set; }
        public RegionData Data { get; private set; }
        public RegionTerrain Terrain { get; private set; }
        public HeightField Field { get; private set; }
        public List<ScatterInstance> Scatter { get; private set; }
        public Scatterer Scatterer { get; private set; }
        public InstancedNature Nature { get; private set; }
        public StreamedGrass Grass { get; private set; }
        public RegionWaterBuilder.Result Water { get; private set; }
        public List<BridgeBuilder.Built> Bridges { get; private set; }
        public PlayerCharacter Player { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public Vector3 Origin { get; private set; }
        public Vector3 SpawnPoint { get; private set; }
        /// <summary>Build time of each stage, for the performance report.</summary>
        public readonly List<(string stage, long ms)> Timings = new List<(string, long)>();
        public int ColliderCount { get; private set; }
        public int PickupCount { get; private set; }

        private Transform _root;
        private readonly Dictionary<string, InstancedNature.Kind> _kinds = new Dictionary<string, InstancedNature.Kind>();
        private Stopwatch _clock;

        private void Awake()
        {
            Current = this;
            _clock = Stopwatch.StartNew();
            _root = new GameObject("Região").transform;

            Grid = new WorldGrid();
            Atlas = RegionLibrary.LoadAtlas(Grid);
            Data = Atlas.Find(RegionId) ?? RegionLibrary.Load(RegionId);
            Origin = new Vector3(Data.originX, Data.lakeLevel, Data.originZ);
            Terrain = new RegionTerrain(Data);
            Field = new HeightField(Terrain);
            Mark("relevo");

            SpawnPoint = WorldAt(Data.spawn.u, Data.spawn.v, 0.15f);
            BuildPlayer();
            Mark("jogador");

            RegionTerrainBuilder.Build(_root, Field, Grid, Palette.Solid(Color.white));
            PathRibbonBuilder.Build(_root, Terrain, Field, Palette.Solid(Color.white));
            Mark("terreno e trilhas");
            Water = RegionWaterBuilder.Build(_root, Terrain, Field);
            Mark("água");

            Nature = new GameObject("Natureza instanciada").AddComponent<InstancedNature>();
            Nature.transform.SetParent(_root, false);
            Bridges = BridgeBuilder.Build(_root, Terrain, Field, Nature, Kind);
            Mark("pontes");

            BuildScatter();
            Mark("vegetação e rochas");
            BuildLandmarks();
            BuildCamp();
            Mark("marcos");

            Grass = new GameObject("Grama").AddComponent<StreamedGrass>();
            Grass.transform.SetParent(_root, false);
            Grass.Player = Player != null ? Player.transform : null;
            Grass.Setup(Field, Data.seed);
            FarScenery.Build(_root, Field, Nature, new[] { Kind("arvore_copa_0_lod"), Kind("arvore_copa_1_lod"), Kind("cipreste_0_lod") });
            BuildLimits();
            BuildFallSpray();
            Mark("horizonte e limites");

            Debug.Log($"Zeldexile: região '{Data.id}' pronta em {_clock.ElapsedMilliseconds} ms — {Nature.InstanceCount} objetos instanciados, {ColliderCount} colisores, {PickupCount} coletáveis.");
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Mark(string stage)
        {
            Timings.Add((stage, _clock.ElapsedMilliseconds));
        }

        /// <summary>World position of a region spot, on the ground, plus a lift.</summary>
        public Vector3 WorldAt(float u, float v, float lift = 0f) => Origin + new Vector3(u, Field.Height(u, v) + lift, v);

        // ---------------------------------------------------------------- player

        private void BuildPlayer()
        {
            var host = new GameObject("Zeldexile");
            host.SetActive(false);
            var world = host.AddComponent<PrototypeWorld>();
            world.PlayerOnly = true;
            world.SpawnPoint = SpawnPoint;
            host.SetActive(true);
            Player = world.Player;
            if (Player == null) return;
            Player.transform.rotation = Quaternion.Euler(0f, Data.spawn.yaw, 0f);
            Motor = Player.GetComponent<PlayerMotor>();
            if (world.Atmosphere != null) world.Atmosphere.FogDistanceScale = 3.4f;
            var cam = Camera.main;
            if (cam != null) cam.farClipPlane = 3000f;
        }

        // ---------------------------------------------------------------- nature

        /// <summary>The instanced kind for a model of Resources/Models/Nature (cached).</summary>
        public InstancedNature.Kind Kind(string model)
        {
            if (_kinds.TryGetValue(model, out var kind)) return kind;
            kind = new InstancedNature.Kind { Name = model, Near = NatureMeshes.Get(model) };
            var lod = model.EndsWith("_lod");
            if (!lod) kind.Far = NatureMeshes.Get(model + "_lod");
            var family = model.Split('_')[0];
            switch (family)
            {
                case "arvore":
                case "cipreste":
                case "laranjeira":
                case "pereira":
                    kind.LodDistance = 150f;
                    kind.DrawDistance = 3000f;
                    kind.OutlineDistance = 110f;
                    break;
                case "arbusto":
                    kind.LodDistance = 90f;
                    kind.DrawDistance = 380f;
                    kind.OutlineDistance = 60f;
                    break;
                case "rocha":
                    kind.LodDistance = 9999f;
                    kind.DrawDistance = model.StartsWith("rocha_grande") ? 2000f : 450f;
                    kind.OutlineDistance = model.StartsWith("rocha_grande") ? 160f : 70f;
                    kind.WindStrength = 0f;
                    break;
                case "tronco":
                case "toco":
                    kind.DrawDistance = 380f;
                    kind.OutlineDistance = 70f;
                    kind.WindStrength = 0f;
                    break;
                case "ponte":
                    kind.DrawDistance = 1200f;
                    kind.OutlineDistance = 90f;
                    kind.WindStrength = 0f;
                    break;
                case "seixo":
                    kind.DrawDistance = 90f;
                    kind.Outline = false;
                    kind.CastShadows = false;
                    kind.WindStrength = 0f;
                    break;
                case "galho":
                case "folhas":
                    kind.DrawDistance = 80f;
                    kind.Outline = false;
                    kind.CastShadows = false;
                    kind.WindStrength = 0f;
                    break;
                case "junco":
                    kind.DrawDistance = 140f;
                    kind.Outline = false;
                    kind.CastShadows = false;
                    break;
                default: // samambaia, flores
                    kind.DrawDistance = 90f;
                    kind.Outline = false;
                    kind.CastShadows = false;
                    break;
            }

            if (lod)
            {
                kind.Outline = false;
                kind.DrawDistance = 3000f;
                kind.LodDistance = 9999f;
            }

            _kinds[model] = kind;
            return Nature.AddKind(kind);
        }

        private static string ModelOf(string layer, int variant)
        {
            switch (layer)
            {
                case ScatterCatalog.Stump: return "toco";
                case ScatterCatalog.FlowerWhite: return "flor_branca_" + variant;
                case ScatterCatalog.FlowerYellow: return "flor_amarela_" + variant;
                case ScatterCatalog.Boulder: return "rocha_grande_" + variant;
                default: return layer + "_" + variant;
            }
        }

        private void BuildScatter()
        {
            var reserved = new List<Scatterer.Reserve>
            {
                new Scatterer.Reserve { Center = new P2(Data.spawn.u, Data.spawn.v), Radius = 16f },
            };
            foreach (var poi in Data.pois)
            {
                var radius = poi.kind == "arvore_ancia" ? 16f : poi.kind == "mirante" ? 9f : poi.kind == "clareira" ? 10f : 0f;
                if (radius > 0f) reserved.Add(new Scatterer.Reserve { Center = new P2(poi.u, poi.v), Radius = radius });
            }

            var layers = ScatterCatalog.Default();
            Scatterer = new Scatterer(Terrain, Field, layers, reserved);
            Scatter = Scatterer.Run();

            var colliders = new GameObject("Colisões").transform;
            colliders.SetParent(_root, false);
            var pickups = new GameObject("Coletáveis").transform;
            pickups.SetParent(_root, false);
            var orange = MaterialCatalog.Find("laranja_vale");
            var pear = MaterialCatalog.Find("pera_dourada");
            var wood = MaterialCatalog.Find("lenha");

            foreach (var inst in Scatter)
            {
                var layer = layers[inst.Layer].Id;
                var model = ModelOf(layer, inst.Variant);
                var position = Origin + new Vector3(inst.U, inst.H - inst.Sink, inst.V);
                var rotation = Orientation(layer, inst);
                var id = Scatterer.IdOf(inst);
                var hash = RegionMath.Hash01(Data.seed, RegionMath.HashString(layer), inst.CellU, inst.CellV);

                // Some branches lie loose: those are firewood to pick up instead of scenery.
                if (layer == ScatterCatalog.Branch && hash < 0.12f && wood != null)
                {
                    SpawnPickup(pickups, id, wood, "galho_" + inst.Variant, position + Vector3.up * 0.05f, rotation, inst.Scale);
                    continue;
                }

                var kind = Kind(model);
                if (kind.Near == null) continue;
                Nature.Add(kind, Matrix4x4.TRS(position, rotation, Vector3.one * inst.Scale), RadiusOf(layer) * inst.Scale);
                AddCollider(colliders, layer, inst, id, position, rotation, kind);

                // Ripe fruit fallen under some orchard trees.
                if ((layer == ScatterCatalog.OrangeTree || layer == ScatterCatalog.PearTree) && hash < 0.3f)
                {
                    var count = hash < 0.1f ? 2 : 1;
                    for (var k = 0; k < count; k++)
                    {
                        var a = RegionMath.Hash01(Data.seed, 31, inst.CellU, inst.CellV + k) * Mathf.PI * 2f;
                        var r = 1.1f + RegionMath.Hash01(Data.seed, 37, inst.CellU, inst.CellV + k) * 1.6f;
                        var u = inst.U + Mathf.Cos(a) * r;
                        var v = inst.V + Mathf.Sin(a) * r;
                        var isOrange = layer == ScatterCatalog.OrangeTree;
                        SpawnPickup(pickups, $"{id}/fruta{k}", isOrange ? orange : pear, isOrange ? "fruta_laranja" : "fruta_pera",
                            WorldAt(u, v, -0.02f), Quaternion.Euler(0f, a * 57f, 0f), 1f);
                    }
                }
            }
        }

        private Quaternion Orientation(string layer, ScatterInstance inst)
        {
            var yaw = Quaternion.Euler(0f, inst.Yaw, 0f);
            if (layer == ScatterCatalog.Log || layer == ScatterCatalog.Branch || layer == ScatterCatalog.Rock ||
                layer == ScatterCatalog.Boulder || layer == ScatterCatalog.Pebble)
            {
                // Lie along the ground (rocks only partly), then lean a little.
                var normal = GroundNormal(inst.U, inst.V);
                var follow = layer == ScatterCatalog.Log || layer == ScatterCatalog.Branch ? 1f : 0.5f;
                var align = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), follow);
                return align * yaw * Quaternion.Euler(inst.Tilt, 0f, inst.Tilt * 0.5f);
            }

            return yaw;
        }

        public Vector3 GroundNormal(float u, float v)
        {
            const float e = 0.8f;
            var dx = (Field.Height(u + e, v) - Field.Height(u - e, v)) / (2f * e);
            var dz = (Field.Height(u, v + e) - Field.Height(u, v - e)) / (2f * e);
            return new Vector3(-dx, 1f, -dz).normalized;
        }

        private static float RadiusOf(string layer)
        {
            switch (layer)
            {
                case ScatterCatalog.BroadTree: return 7f;
                case ScatterCatalog.Cypress: return 5.5f;
                case ScatterCatalog.OrangeTree:
                case ScatterCatalog.PearTree: return 3.6f;
                case ScatterCatalog.Boulder: return 1.2f;
                case ScatterCatalog.Log: return 3f;
                case ScatterCatalog.Reed: return 1f;
                default: return 1.2f;
            }
        }

        private void AddCollider(Transform parent, string layer, ScatterInstance inst, string id, Vector3 position, Quaternion rotation, InstancedNature.Kind kind)
        {
            GameObject go = null;
            switch (layer)
            {
                case ScatterCatalog.BroadTree:
                case ScatterCatalog.Cypress:
                case ScatterCatalog.OrangeTree:
                case ScatterCatalog.PearTree:
                {
                    // Trunk only: the crown stays open to the camera and to gliding.
                    var radius = layer == ScatterCatalog.BroadTree ? new[] { 0.42f, 0.5f, 0.62f }[inst.Variant % 3]
                        : layer == ScatterCatalog.Cypress ? 0.2f : new[] { 0.14f, 0.21f, 0.23f, 0.2f }[inst.Variant % 4];
                    go = new GameObject(id);
                    var cap = go.AddComponent<CapsuleCollider>();
                    cap.radius = radius * inst.Scale;
                    cap.height = (layer == ScatterCatalog.BroadTree ? 5f : 2.6f) * inst.Scale;
                    cap.center = Vector3.up * cap.height * 0.5f;
                    break;
                }
                case ScatterCatalog.Boulder:
                case ScatterCatalog.Rock:
                    go = new GameObject(id);
                    go.AddComponent<MeshCollider>().sharedMesh = kind.Near;
                    break;
                case ScatterCatalog.Log:
                {
                    go = new GameObject(id);
                    var cap = go.AddComponent<CapsuleCollider>();
                    cap.direction = 0;
                    cap.radius = (inst.Variant == 0 ? 0.28f : 0.36f) * inst.Scale;
                    cap.height = (inst.Variant == 0 ? 4.2f : 5.6f) * inst.Scale;
                    cap.center = new Vector3(0f, cap.radius * 0.85f, 0f);
                    break;
                }
                case ScatterCatalog.Stump:
                {
                    go = new GameObject(id);
                    var cap = go.AddComponent<CapsuleCollider>();
                    cap.radius = 0.4f * inst.Scale;
                    cap.height = 0.9f * inst.Scale;
                    cap.center = Vector3.up * 0.3f * inst.Scale;
                    break;
                }
            }

            if (go == null) return;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            if (layer == ScatterCatalog.Boulder || layer == ScatterCatalog.Rock) go.transform.localScale = Vector3.one * inst.Scale;
            go.isStatic = true;
            ColliderCount++;
        }

        /// <summary>A collectable with a Blender model as its look. Far ones stop drawing (LOD cull).</summary>
        private void SpawnPickup(Transform parent, string id, MaterialDefinition material, string model, Vector3 position, Quaternion rotation, float scale)
        {
            if (material == null) return;
            var pickup = MaterialPickup.Spawn(material, position);
            pickup.name = id;
            pickup.transform.SetParent(parent, true);
            for (var i = pickup.transform.childCount - 1; i >= 0; i--) Destroy(pickup.transform.GetChild(i).gameObject);
            var look = ModelLibrary.Spawn(NatureMeshes.Folder + model, pickup.transform, NaturePalette.Slot);
            if (look != null)
            {
                look.transform.localRotation = rotation;
                look.transform.localScale = Vector3.one * scale;
                var lod = pickup.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.02f, look.GetComponentsInChildren<Renderer>()) });
                lod.size = 2f;
            }

            PickupCount++;
        }

        // ---------------------------------------------------------------- landmarks and camp

        private void BuildLandmarks()
        {
            foreach (var poi in Data.pois)
            {
                if (poi.kind != "arvore_ancia") continue;
                var kind = Kind("arvore_ancia");
                var p = WorldAt(poi.u, poi.v, -0.3f);
                Nature.Add(kind, Matrix4x4.TRS(p, Quaternion.Euler(0f, 30f, 0f), Vector3.one), 12f);
                var trunk = new GameObject(poi.id).AddComponent<CapsuleCollider>();
                trunk.transform.SetParent(_root, false);
                trunk.transform.position = p;
                trunk.radius = 1.1f;
                trunk.height = 7f;
                trunk.center = Vector3.up * 3.5f;
                ColliderCount++;
                var orange = MaterialCatalog.Find("laranja_vale");
                for (var k = 0; k < 5; k++)
                {
                    var a = k * 1.3f + 0.4f;
                    SpawnPickup(_root, $"{Data.id}/{poi.id}/fruta{k}", orange, "fruta_laranja",
                        WorldAt(poi.u + Mathf.Cos(a) * (2.4f + k * 0.4f), poi.v + Mathf.Sin(a) * (2.4f + k * 0.4f), -0.02f), Quaternion.identity, 1f);
                }
            }
        }

        /// <summary>Campfire with a cooking pot beside the entrance path, two logs to sit on.</summary>
        private void BuildCamp()
        {
            var u = Data.spawn.u + 7f;
            var v = Data.spawn.v + 6f;
            var fireBase = WorldAt(u, v);
            var fire = new GameObject("Fogueira da entrada");
            fire.transform.SetParent(_root, false);
            fire.transform.position = fireBase;
            var trigger = fire.AddComponent<SphereCollider>();
            trigger.radius = 1.2f;
            trigger.isTrigger = true;
            fire.AddComponent<Campfire>();
            CampfireLook.Build(fire.transform, Nature, Kind);
            fire.AddComponent<CookingPot>().Range = 2.8f;

            var log = Kind("tronco_caido_0");
            foreach (var (du, dv, yaw) in new[] { (2.6f, 0.4f, 80f), (-0.6f, 2.7f, 175f) })
            {
                var p = WorldAt(u + du, v + dv, -0.05f);
                var r = Quaternion.Euler(0f, yaw, 0f);
                Nature.Add(log, Matrix4x4.TRS(p, r, Vector3.one * 0.55f), 2f);
                var seat = new GameObject("Assento").AddComponent<CapsuleCollider>();
                seat.transform.SetParent(_root, false);
                seat.transform.SetPositionAndRotation(p, r);
                seat.direction = 0;
                seat.radius = 0.16f;
                seat.height = 2.3f;
                seat.center = Vector3.up * 0.14f;
                ColliderCount++;
            }
        }

        // ---------------------------------------------------------------- limits and effects

        private void BuildLimits()
        {
            var limits = new GameObject("Limites da região").transform;
            limits.SetParent(_root, false);
            const float height = 600f;
            const float thick = 4f;
            var sizeU = Data.sizeU;
            var sizeV = Data.sizeV;
            Wall(limits, new Vector3(sizeU / 2f, 0f, -thick / 2f), new Vector3(sizeU + 2 * thick, height, thick));
            Wall(limits, new Vector3(sizeU / 2f, 0f, sizeV + thick / 2f), new Vector3(sizeU + 2 * thick, height, thick));
            Wall(limits, new Vector3(-thick / 2f, 0f, sizeV / 2f), new Vector3(thick, height, sizeV + 2 * thick));
            Wall(limits, new Vector3(sizeU + thick / 2f, 0f, sizeV / 2f), new Vector3(thick, height, sizeV + 2 * thick));
            var guard = limits.gameObject.AddComponent<RegionLimits>();
            guard.World = this;
        }

        private void Wall(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Limite");
            go.transform.SetParent(parent, false);
            go.transform.position = Origin + center + Vector3.up * (size.y / 2f - 120f);
            go.AddComponent<BoxCollider>().size = size;
            ColliderCount++;
        }

        private void BuildFallSpray()
        {
            if (Water == null) return;
            foreach (var p in Water.FallBases) WaterSpray.Create(_root, p);
        }

        /// <summary>Renders a capture camera with the instanced props and grass included.</summary>
        public void RenderFor(Camera cam)
        {
            if (Nature != null) Nature.Draw(cam, true);
            if (Grass != null)
            {
                Grass.Refresh(cam.transform.position);
                Grass.Draw(cam, true);
            }

            cam.Render();
        }
    }
}
