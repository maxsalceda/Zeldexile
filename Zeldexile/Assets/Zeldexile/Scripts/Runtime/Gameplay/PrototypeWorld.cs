using UnityEngine;
using Zeldexile.Characters;
using Zeldexile.Combat;
using Zeldexile.Domain;
using Zeldexile.Player;
using Zeldexile.UI;
using Zeldexile.World;
using Zeldexile.World.Terrain;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.Gameplay
{
    /// <summary>
    /// Builds the prototype island at runtime, so the vertical slice runs with no art.
    /// The camp at the start has a cooking pot, a goddess statue and a few weapons on the ground.
    /// Each trial tests one rule of the world and rewards a Spirit Orb:
    ///  - Mesa Alta: too tall to climb; burn the grass below and ride the hot air up (Brasa).
    ///  - Lago: too wide to swim; raise ice pillars across (Geada, three at a time).
    ///  - Portão dos Três Pilares: power all three posts at once (Faísca jumps through metal).
    /// </summary>
    public sealed class PrototypeWorld : MonoBehaviour
    {
        private Transform _root;
        private PlayerCharacter _player;
        private LootDropper _loot;
        private Light _sun;
        private AtmosphereController _atmosphere;

        /// <summary>
        /// Set before Awake (on an inactive object) by worlds that build their own ground, such as the open-world
        /// regions: only the light, the atmosphere and the player (at SpawnPoint) are built.
        /// </summary>
        public bool PlayerOnly;
        public Vector3 SpawnPoint;

        public PlayerCharacter Player => _player;
        public AtmosphereController Atmosphere => _atmosphere;

        private void Awake()
        {
            _root = new GameObject("Mundo").transform;
            BuildLighting();
            if (PlayerOnly)
            {
                BuildPlayer(SpawnPoint);
                return;
            }

            BuildTerrain();
            var player = BuildPlayer(OnGround(0f, -2f, 0.1f));
            BuildCamp();
            BuildGrassField(new Vector3(-14f, 0f, -40f), 13, 6);
            BuildMesaAlta();
            BuildLake();
            BuildPillarGate();
            BuildEnemies(player.transform);
            BuildScenery();
            BuildDecorativeGrass(player.transform);
        }

        /// <summary>Dense instanced grass over the gentle green ground (not on the trial grass field).</summary>
        private void BuildDecorativeGrass(Transform player)
        {
            var field = new GameObject("Grama densa").AddComponent<GrassField>();
            field.Player = player;
            var trialField = Rect.MinMaxRect(-15f, -41f, 13f, -27f);
            var gate = Rect.MinMaxRect(75f, 2f, 85f, 12f);
            field.Build(Rect.MinMaxRect(IslandShape.MinX + 6f, IslandShape.MinZ + 6f, IslandShape.MaxX - 6f, IslandShape.MaxZ - 6f),
                IslandShape.Height,
                (x, z) => !trialField.Contains(new Vector2(x, z)) && !gate.Contains(new Vector2(x, z)) && IslandShape.IsGrassy(x, z),
                19);
        }

        // ---------- Setup ----------

        private void BuildLighting()
        {
            _sun = FindAnyObjectByType<Light>();
            if (_sun == null) _sun = new GameObject("Sol").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;

            // Time of day, weather, sky and fog all come from the atmosphere controller.
            var go = new GameObject("Atmosfera");
            go.SetActive(false);
            _atmosphere = go.AddComponent<AtmosphereController>();
            _atmosphere.Sun = _sun;
            go.SetActive(true);
            new GameObject("Game feel").AddComponent<GameFeel>();
        }

        private void BuildTerrain()
        {
            var bounds = Rect.MinMaxRect(IslandShape.MinX, IslandShape.MinZ, IslandShape.MaxX, IslandShape.MaxZ);
            FacetedTerrain.Build(_root, IslandShape.Height, IslandShape.FaceColor, bounds, 2f, 24, Palette.Solid(Color.white));

            // The border mountains are the natural frontier; these colliders only stop falls off the mesh edge.
            InvisibleWall(new Vector3(IslandShape.MinX, -10f, IslandShape.MinZ), new Vector3(IslandShape.MaxX, 80f, IslandShape.MinZ + 1f));
            InvisibleWall(new Vector3(IslandShape.MinX, -10f, IslandShape.MaxZ - 1f), new Vector3(IslandShape.MaxX, 80f, IslandShape.MaxZ));
            InvisibleWall(new Vector3(IslandShape.MinX, -10f, IslandShape.MinZ), new Vector3(IslandShape.MinX + 1f, 80f, IslandShape.MaxZ));
            InvisibleWall(new Vector3(IslandShape.MaxX - 1f, -10f, IslandShape.MinZ), new Vector3(IslandShape.MaxX, 80f, IslandShape.MaxZ));
        }

        private void InvisibleWall(Vector3 min, Vector3 max)
        {
            var go = new GameObject("Limite");
            go.transform.SetParent(_root, false);
            go.transform.position = (min + max) / 2f;
            go.AddComponent<BoxCollider>().size = max - min;
        }

        private static Vector3 OnGround(float x, float z, float lift = 0f) =>
            new Vector3(x, IslandShape.Height(x, z) + lift, z);

        private GameObject BuildPlayer(Vector3 position)
        {
            var go = new GameObject("Jogador") { layer = 2 }; // Ignore Raycast: aim and camera casts skip it.
            go.transform.position = position;

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;

            var visual = CharacterRig.Build(go.transform, RigStyle.Hero);

            var health = go.AddComponent<Health>();
            var input = go.AddComponent<PlayerInputReader>();
            _player = go.AddComponent<PlayerCharacter>();
            _player.Health = health;

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Câmera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }

            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            // The water shader reads scene depth for its shore foam and depth color.
            cam.depthTextureMode |= DepthTextureMode.Depth;
            if (!cam.TryGetComponent<ThirdPersonCamera>(out var rig)) rig = cam.gameObject.AddComponent<ThirdPersonCamera>();
            rig.Target = go.transform;
            rig.Input = input;

            var targeting = go.AddComponent<PlayerTargeting>();
            targeting.Input = input;
            targeting.ViewCamera = cam;
            rig.Targeting = targeting;

            var climate = go.AddComponent<PlayerClimate>();
            climate.Character = _player;

            var motor = go.AddComponent<PlayerMotor>();
            motor.Input = input;
            motor.CameraRig = rig;
            motor.Character = _player;
            motor.Targeting = targeting;
            motor.SetSpawn(position);

            var caster = go.AddComponent<PlayerCaster>();
            caster.Input = input;
            caster.Character = _player;
            caster.Motor = motor;
            caster.ViewCamera = cam;
            caster.Targeting = targeting;

            var combat = go.AddComponent<PlayerCombat>();
            combat.Input = input;
            combat.Character = _player;
            combat.Motor = motor;
            combat.Caster = caster;
            combat.Targeting = targeting;

            var interactor = go.AddComponent<PlayerInteractor>();
            interactor.Input = input;
            interactor.Character = _player;

            var animator = go.AddComponent<CharacterAnimator>();
            animator.Rig = visual;
            animator.Source = motor;
            caster.Casted += animator.TriggerCast;
            combat.Acted += kind => animator.Play(kind);
            interactor.Interacted += () => animator.Play(ActionKind.Pickup);
            motor.Landed += meters => { if (meters > 3f) animator.Play(ActionKind.Land); };
            motor.ClimbJumped += () => animator.Play(ActionKind.ClimbJump);
            motor.SwimDashed += () => animator.Play(ActionKind.SwimDash);

            health.Damaged += (_, amount, _) => GameFeel.AddTrauma(Mathf.Clamp(amount / 12f, 0.15f, 0.6f));
            health.Damaged += (_, _, _) => animator.Play(ActionKind.Hurt);
            if (GameFeel.Current != null) GameFeel.Current.Player = go;

            health.Died += _ =>
            {
                _player.AddLog("Você caiu. Voltando ao início.");
                health.Revive();
                motor.Respawn();
            };

            var hud = new GameObject("HUD").AddComponent<GameHud>();
            var presenter = go.AddComponent<HeroPresenter>();
            presenter.Character = _player;
            presenter.Combat = combat;
            presenter.Animator = animator;
            presenter.Rig = visual;
            presenter.Kneeling = () => hud.Kneeling;
            hud.Character = _player;
            hud.Motor = motor;
            hud.Caster = caster;
            hud.Combat = combat;
            hud.Interactor = interactor;
            hud.Input = input;
            hud.ViewCamera = cam;
            hud.Climate = climate;
            hud.Targeting = targeting;

            var weather = new GameObject("Clima").AddComponent<WeatherEffects>();
            weather.Atmosphere = _atmosphere;
            weather.Target = go.transform;
            weather.TargetCarriesMetal = () => _player.CarriesMetal;
            weather.Log = _player.AddLog;
            _atmosphere.BloodMoonRose += () => _player.Announce("A Lua de Sangue renasceu os monstros.");

            new GameObject("Efeitos").AddComponent<EffectsPresenter>();
            _loot = new GameObject("Loot").AddComponent<LootDropper>();
            _loot.Player = _player;
            return go;
        }

        // ---------- Camp ----------

        /// <summary>The starting camp: fire with a pot, goddess statue, weapons on the ground and food around.</summary>
        private void BuildCamp()
        {
            var camp = new GameObject("Acampamento").transform;
            camp.SetParent(_root, false);

            // Campfire with a cooking pot.
            var fireBase = OnGround(3f, 1f);
            var fire = new GameObject("Fogueira");
            fire.transform.SetParent(camp, false);
            fire.transform.position = fireBase;
            var fireCol = fire.AddComponent<SphereCollider>();
            fireCol.radius = 1.2f;
            fireCol.isTrigger = true;
            fire.AddComponent<Campfire>();
            var stones = Effects.Primitive(PrimitiveType.Cylinder, fire.transform, Palette.Outlined(Palette.Stone));
            stones.transform.localPosition = Vector3.up * 0.1f;
            stones.transform.localScale = new Vector3(1.4f, 0.1f, 1.4f);
            Effects.Flames(fire.transform, fireBase.y + 0.2f);
            var pot = Effects.Primitive(PrimitiveType.Sphere, fire.transform, Palette.Outlined(new Color(0.25f, 0.25f, 0.28f)));
            pot.transform.localPosition = Vector3.up * 0.75f;
            pot.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);
            fire.AddComponent<CookingPot>().Range = 2.8f;

            // Goddess statue: prayer trades Spirit Orbs for hearts or stamina.
            var statueBase = OnGround(-4f, 3f);
            var statue = new GameObject("Estátua");
            statue.transform.SetParent(camp, false);
            statue.transform.position = statueBase;
            statue.AddComponent<BoxCollider>().size = new Vector3(0.9f, 2.4f, 0.9f);
            statue.GetComponent<BoxCollider>().center = Vector3.up * 1.2f;
            var pedestal = Effects.Primitive(PrimitiveType.Cube, statue.transform, Palette.Outlined(Palette.Stone));
            pedestal.transform.localPosition = Vector3.up * 0.25f;
            pedestal.transform.localScale = new Vector3(1.2f, 0.5f, 1.2f);
            var figure = Effects.Primitive(PrimitiveType.Capsule, statue.transform, Palette.Outlined(Palette.Statue));
            figure.transform.localPosition = Vector3.up * 1.4f;
            figure.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            statue.AddComponent<GoddessStatue>().Range = 3f;

            // Weapons left in the world (back after a blood moon) and things to eat.
            WeaponPickup.Spawn(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, 1)), OnGround(2f, -5f), respawns: true).transform.SetParent(camp, true);
            WeaponPickup.Spawn(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.Bow, 0)), OnGround(-3f, -5f), respawns: true).transform.SetParent(camp, true);
            WeaponPickup.Spawn(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.Shield, 1)), OnGround(5f, -3f), respawns: true).transform.SetParent(camp, true);
            WeaponPickup.Spawn(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.Spear, 1)), OnGround(-6f, -1f), respawns: true).transform.SetParent(camp, true);

            var random = new System.Random(7);
            string[] food = { "maca", "cogumelo", "pimenta", "melancia", "erva", "carne", "cogumelo_folego", "banana", "duriao", "cenoura", "cogumelo_rapido" };
            for (var i = 0; i < 18; i++)
            {
                var x = (float)(random.NextDouble() * 24.0 - 12.0);
                var z = (float)(random.NextDouble() * 20.0 - 14.0);
                var m = MaterialCatalog.Find(food[random.Next(food.Length)]);
                MaterialPickup.Spawn(m, OnGround(x, z)).transform.SetParent(camp, true);
            }

            // Critters and ore for elixirs and trade, a little farther out.
            MaterialPickup.Spawn(MaterialCatalog.Find("grilo"), OnGround(-20f, -10f)).transform.SetParent(camp, true);
            MaterialPickup.Spawn(MaterialCatalog.Find("lagarto_brasa"), OnGround(-22f, -14f)).transform.SetParent(camp, true);
            MaterialPickup.Spawn(MaterialCatalog.Find("ambar"), OnGround(-30f, 10f)).transform.SetParent(camp, true);
            MaterialPickup.Spawn(MaterialCatalog.Find("pederneira"), OnGround(-28f, 12f)).transform.SetParent(camp, true);
        }

        // ---------- Trials ----------

        /// <summary>A mesa too tall to climb with one wheel. Burning the grass below makes an updraft.</summary>
        private void BuildMesaAlta()
        {
            // The mesa itself is part of the terrain (IslandShape.Mesa).
            Chest("Prova da Mesa Alta", OnGround(IslandShape.Mesa.center.x, IslandShape.Mesa.center.y), 2, WeaponClass.OneHanded, "topazio");
        }

        private void BuildGrassField(Vector3 origin, int columns, int rows)
        {
            const float size = 2f;
            for (var x = 0; x < columns; x++)
            for (var z = 0; z < rows; z++)
            {
                var patch = new GameObject("Grama");
                patch.transform.SetParent(_root, false);
                var px = origin.x + x * size + size / 2f;
                var pz = origin.z + z * size + size / 2f;
                patch.transform.position = OnGround(px, pz, 0.15f);
                var box = patch.AddComponent<BoxCollider>();
                box.size = new Vector3(size * 0.98f, 0.3f, size * 0.98f);
                box.isTrigger = true;
                Visual(patch.transform, "grama", box.size, Palette.Grass, Vector3.zero, ((x * 7 + z * 3) % 4) * 90f);
                var surface = patch.AddComponent<ElementalSurface>();
                surface.Kind = SurfaceKind.Grass;
                surface.Repaint();
            }
        }

        /// <summary>Lake wider than one wheel can swim. Raise ice pillars across, or glide from the hill.</summary>
        private void BuildLake()
        {
            // The basin is part of the terrain; this is the gameplay volume plus the visible surface.
            var area = IslandShape.Lake;
            var bottom = IslandShape.LakeFloor - 0.5f;
            var water = new GameObject("Lago");
            water.transform.SetParent(_root, false);
            water.transform.position = new Vector3(area.center.x, (IslandShape.WaterSurface + bottom) / 2f, area.center.y);
            water.AddComponent<BoxCollider>().size = new Vector3(area.width + 4f, IslandShape.WaterSurface - bottom, area.height + 4f);
            water.AddComponent<WaterVolume>();
            water.AddComponent<ElementalSurface>().Kind = SurfaceKind.Water;

            var visible = new Rect(area.xMin - 4f, area.yMin - 4f, area.width + 8f, area.height + 8f);
            WaterSurface.Build(_root, visible, IslandShape.WaterSurface, 2f);

            // The island is farther from every shore than one wheel can swim.
            Chest("Prova do Lago", OnGround(IslandShape.IslandCenter.x, IslandShape.IslandCenter.y), 2, WeaponClass.Bow, "safira");
        }

        /// <summary>Three metal posts. The gate opens only when all are powered at the same time.</summary>
        private void BuildPillarGate()
        {
            // Walls start a little below the ground so the uneven terrain never shows a gap.
            var center = OnGround(80f, 0f, -0.3f);
            var gateGo = Block("Portão", center + new Vector3(-3f, 0f, 3.5f), center + new Vector3(3f, 4f, 4f), Palette.Stone, "portao");
            Block("Muro esq.", center + new Vector3(-3.5f, 0f, 3.5f), center + new Vector3(-3f, 4f, 10f), Palette.Cliff, "muro_lateral");
            Block("Muro dir.", center + new Vector3(3f, 0f, 3.5f), center + new Vector3(3.5f, 4f, 10f), Palette.Cliff, "muro_lateral", 180f);
            Block("Muro fundo", center + new Vector3(-3.5f, 0f, 10f), center + new Vector3(3.5f, 4f, 10.5f), Palette.Cliff, "muro_fundo");
            Block("Teto", center + new Vector3(-3.5f, 4f, 3.5f), center + new Vector3(3.5f, 4.5f, 10.5f), Palette.Cliff, "teto");
            Chest("Prova dos Três Pilares", OnGround(center.x, center.z + 7f), 3, WeaponClass.TwoHanded, "rubi");

            var gate = gateGo.AddComponent<PowerGate>();
            var posts = new[] { new Vector3(-7f, 0f, -5f), new Vector3(0f, 0f, -9f), new Vector3(7f, 0f, -5f) };
            foreach (var offset in posts)
            {
                var postBase = OnGround(center.x + offset.x, center.z + offset.z, -0.3f);
                var post = Block("Pilar de cobre", postBase + new Vector3(-0.4f, 0f, -0.4f),
                    postBase + new Vector3(0.4f, 3.5f, 0.4f), Palette.Metal, "pilar_cobre");
                post.AddComponent<ElementalSurface>();
                gate.Conductors.Add(post.AddComponent<MetalConductor>());
            }

            gate.Opened += _ => _player.AddLog("O portão se abriu!");
        }

        private void BuildEnemies(Transform target)
        {
            // A camp of red grunts by the grass field, a brute guarding the gate road, an agile blue
            // lizard near the gate and a black grunt on the cliff for whoever climbs it.
            Enemy(OnGround(-6f, -28f, 0.1f), target, EnemyFamily.Grunt, EnemyTier.Red, WeaponClass.OneHanded, 0);
            Enemy(OnGround(0f, -30f, 0.1f), target, EnemyFamily.Grunt, EnemyTier.Red, WeaponClass.OneHanded, 1);
            Enemy(OnGround(-10f, -24f, 0.1f), target, EnemyFamily.Grunt, EnemyTier.Red, WeaponClass.Spear, 1);
            Enemy(OnGround(76f, -20f, 0.1f), target, EnemyFamily.Brute, EnemyTier.Red, WeaponClass.TwoHanded, 1);
            Enemy(OnGround(90f, -18f, 0.1f), target, EnemyFamily.Agile, EnemyTier.Blue, WeaponClass.Spear, 2);
            Enemy(OnGround(-31f, 32f, 0.1f), target, EnemyFamily.Grunt, EnemyTier.Black, WeaponClass.OneHanded, 3);
        }

        // ---------- Builders ----------

        private void Enemy(Vector3 position, Transform target, EnemyFamily family, EnemyTier tier, WeaponClass weaponClass, int weaponTier)
        {
            tier = _player.World.Promote(tier);
            var go = new GameObject(EnemyStats.DisplayName(family, tier));
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.45f;
            controller.center = new Vector3(0f, 0.8f, 0f);
            var visual = CharacterRig.Build(go.transform, RigStyle.ForTier(tier));
            var brain = go.AddComponent<EnemyBrain>();
            var animator = go.AddComponent<CharacterAnimator>();
            animator.Rig = visual;
            animator.Source = brain;
            brain.Attacked += () => animator.Play(
                brain.Weapon == null ? ActionKind.SlashLight
                : brain.Weapon.Class == WeaponClass.TwoHanded ? ActionKind.SlashHeavy
                : brain.Weapon.Class == WeaponClass.Spear ? ActionKind.Thrust : ActionKind.SlashLight);
            go.GetComponent<Health>().Damaged += (_, _, _) => animator.Play(ActionKind.Hurt);
            brain.Target = target;
            brain.Setup(family, tier, new WeaponInstance(WeaponCatalog.ForTier(weaponClass, Mathf.Max(weaponTier, EnemyStats.WeaponTierOf(tier)))));
        }

        private void Chest(string trialName, Vector3 position, int weaponTier, WeaponClass weaponClass, string materialId)
        {
            var go = Block(trialName, position + new Vector3(-0.6f, 0f, -0.4f), position + new Vector3(0.6f, 0.8f, 0.4f), Palette.Chest, "bau", 180f);
            var chest = go.AddComponent<TrialChest>();
            chest.Player = _player;
            chest.TrialName = trialName;
            chest.WeaponTier = weaponTier;
            chest.WeaponClass = weaponClass;
            chest.MaterialId = materialId;
        }

        /// <summary>
        /// Solid piece: an unscaled root with a box collider, dressed with a Blender model
        /// (origin at its bottom center) or, if the model is missing, a plain colored box.
        /// </summary>
        private GameObject Block(string name, Vector3 min, Vector3 max, Color color, string model = null, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = (min + max) / 2f;
            var size = max - min;
            go.AddComponent<BoxCollider>().size = size;
            Visual(go.transform, model, size, color, Vector3.down * size.y / 2f, yaw);
            return go;
        }

        private static void Visual(Transform parent, string model, Vector3 size, Color color, Vector3 localBase, float yaw)
        {
            var visual = model != null ? ModelLibrary.Spawn("Props/" + model, parent, ModelLibrary.PropColor) : null;
            if (visual != null)
            {
                visual.transform.localPosition = localBase;
                visual.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                return;
            }

            var cube = Effects.Primitive(PrimitiveType.Cube, parent, Palette.Solid(color));
            cube.transform.localPosition = localBase + Vector3.up * size.y / 2f;
            cube.transform.localScale = size;
        }

        // ---------- Scenery ----------

        /// <summary>Low-poly trees and rocks, placed with a fixed seed away from the trial areas.</summary>
        private void BuildScenery()
        {
            var random = new System.Random(19);
            var blocked = new[]
            {
                new Rect(8f, -33f, 64f, 66f),     // lake and shores
                new Rect(-16f, -54f, 30f, 28f),   // grass field and mesa
                new Rect(66f, -14f, 28f, 28f),    // pillar gate
                new Rect(-42f, 18f, 22f, 28f),    // cliff
                new Rect(-14f, 20f, 18f, 18f),    // hill
                new Rect(-8f, -10f, 16f, 16f),    // spawn
            };

            var placed = 0;
            for (var attempt = 0; attempt < 600 && placed < 70; attempt++)
            {
                var x = (float)(random.NextDouble() * 170.0 - 76.0);
                var z = (float)(random.NextDouble() * 152.0 - 76.0);
                var spot = new Vector2(x, z);
                var free = true;
                foreach (var r in blocked)
                    if (r.Contains(spot))
                        free = false;
                if (!free) continue;

                placed++;
                // Skip the border mountains.
                if (IslandShape.Height(x, z) > 6f) continue;
                if (random.NextDouble() < 0.7) Tree(OnGround(x, z, -0.2f), random);
                else Rock(OnGround(x, z, -0.3f), random);
            }
        }

        private void Tree(Vector3 position, System.Random random)
        {
            var model = random.NextDouble() < 0.5 ? "pinheiro" : "arvore";
            var scale = 0.8f + (float)random.NextDouble() * 0.6f;
            var go = ModelLibrary.SpawnProp(model, _root, position, (float)random.NextDouble() * 360f, scale);
            if (go == null) return;
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.5f, 0f);
            col.height = 3f;
            col.radius = 0.3f;
            // Fruit trees: an apple at the foot of some of them.
            if (model == "arvore" && random.NextDouble() < 0.5)
                MaterialPickup.Spawn(MaterialCatalog.Find("maca"), position + new Vector3(0.9f, 0.2f, 0.3f)).transform.SetParent(_root, true);
        }

        private void Rock(Vector3 position, System.Random random)
        {
            var size = 1f + (float)random.NextDouble() * 2.5f;
            var go = ModelLibrary.SpawnProp("pedra_" + random.Next(3), _root, position, (float)random.NextDouble() * 360f, size);
            if (go == null) return;
            var col = go.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.25f, 0f);
            col.radius = 0.42f;
            if (random.NextDouble() < 0.25)
                MaterialPickup.Spawn(MaterialCatalog.Find("pederneira"), position + new Vector3(0.7f, 0.2f, 0.4f)).transform.SetParent(_root, true);
        }
    }
}
