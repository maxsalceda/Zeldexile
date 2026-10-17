using System.Collections.Generic;
using UnityEngine;
using Zeldexile.World;

namespace Zeldexile.Characters
{
    /// <summary>Colors and silhouette options for a rigged low-poly character.</summary>
    public struct RigStyle
    {
        public Color Skin;
        public Color Hair;
        public Color Tunic;
        public Color Pants;
        public Color Boots;
        public Color Accent;
        public float Scale;
        public bool Shell;
        public bool Horns;
        public bool Ponytail;
        /// <summary>Blender model under Resources/Models/Characters. Empty uses the procedural fallback.</summary>
        public string Model;
        /// <summary>
        /// Skinned model with a Mixamo skeleton under Resources/Models/Skinned (texture "&lt;name&gt;_cor"), posed through
        /// <see cref="SkinnedRigDriver"/>. Takes precedence over <see cref="Model"/>, which stays as the fallback.
        /// </summary>
        public string SkinnedModel;
        /// <summary>Captured walk and run loops for the skinned model (Resources/Models/Skinned/Animacoes).</summary>
        public string WalkClip;
        public string RunClip;

        public Color ColorFor(string slot)
        {
            switch (slot)
            {
                case "skin": return Skin;
                case "hair": return Hair;
                case "tunic": return Tunic;
                case "pants": return Pants;
                case "boots": return Boots;
                case "accent": return Accent;
                case "metal": return new Color(0.75f, 0.75f, 0.78f);
                case "steel": return new Color(0.80f, 0.82f, 0.88f);
                case "dark": return new Color(0.08f, 0.07f, 0.10f);
                case "cream": return new Color(0.86f, 0.79f, 0.68f);
                case "leather": return new Color(0.45f, 0.29f, 0.18f);
                case "wood": return new Color(0.58f, 0.37f, 0.22f);
                case "wooddark": return new Color(0.40f, 0.25f, 0.15f);
                case "shirt": return new Color(0.20f, 0.20f, 0.23f);
                case "glove": return new Color(0.22f, 0.20f, 0.20f);
                case "brass": return new Color(0.85f, 0.66f, 0.28f);
                case "grip": return new Color(0.25f, 0.18f, 0.14f);
                case "kneepad": return new Color(0.50f, 0.48f, 0.50f);
                case "seam": return Pants * 0.72f;
                case "iris": return new Color(0.62f, 0.36f, 0.16f);
                case "sole": return new Color(0.18f, 0.13f, 0.10f);
                case "bag": return new Color(0.68f, 0.47f, 0.30f);
                case "nose": return Skin * 0.92f;
                default: return Color.white;
            }
        }

        /// <summary>
        /// The hero: the original young adventurer of output/imagegen/aventureiro-anime-seis-poses.png.
        /// Warm tan skin, auburn hair, cropped teal jacket with ochre trim, charcoal shirt, slate trousers,
        /// brown boots. Colours are the reference's base areas, not its highlights or shadows.
        /// </summary>
        public static RigStyle Hero => new RigStyle
        {
            Skin = new Color(0.93f, 0.70f, 0.52f),
            Hair = new Color(0.50f, 0.22f, 0.15f),
            Tunic = new Color(0.18f, 0.42f, 0.48f),
            Pants = new Color(0.30f, 0.31f, 0.37f),
            Boots = new Color(0.50f, 0.32f, 0.20f),
            Accent = new Color(0.80f, 0.62f, 0.28f),
            Scale = 1f,
            // heroi_anime: the smooth export of the NPR hero (Tools/Blender/build_hero_game.py --anime), drawn with
            // Zeldexile/ToonOutlineSmooth. "heroi_npr" is its faceted low-poly export; "heroi" the build_models.py hero.
            Model = "heroi_anime",
            // heroi_meshy: the Meshy AI export of the same hero (sword and shield are part of its mesh).
            SkinnedModel = "heroi_meshy",
            WalkClip = "heroi_meshy_andar",
            RunClip = "heroi_meshy_correr"
        };

        /// <summary>A creature style tinted by its tier: red, blue, black or silver.</summary>
        public static RigStyle ForTier(Zeldexile.Domain.EnemyTier tier)
        {
            var style = Cascudo;
            var tint = Palette.ForTier(tier);
            style.Skin = Color.Lerp(style.Skin, tint, 0.75f);
            style.Hair = Color.Lerp(style.Hair, tint * 0.6f, 0.6f);
            style.Tunic = Color.Lerp(style.Tunic, tint * 0.8f, 0.4f);
            return style;
        }

        public static RigStyle Cascudo => new RigStyle
        {
            Skin = new Color(0.55f, 0.66f, 0.30f),
            Hair = new Color(0.30f, 0.36f, 0.16f),
            Tunic = new Color(0.62f, 0.30f, 0.22f),
            Pants = new Color(0.40f, 0.30f, 0.24f),
            Boots = new Color(0.22f, 0.18f, 0.15f),
            Accent = new Color(0.92f, 0.86f, 0.70f),
            Scale = 1.1f,
            Shell = true,
            Horns = true,
            Model = "cascudo"
        };
    }

    public enum GearPart
    {
        None,
        SwordHand,
        SwordBack,
        ShieldHand,
        ShieldBack,
        BowHand,
        BowBack
    }

    /// <summary>
    /// The character's skeleton, 26 bones of rigid segments (each mesh of the model belongs to one bone):
    /// <code>
    /// Quadril (root, centre of gravity)
    ///  + Tronco (lower spine: waist, belt) - Bolsa (secondary, swings)
    ///  |  + Tórax (chest) - Planador
    ///  |     + Pescoço - Cabeça - Mandíbula
    ///  |     + Clavícula E/D - Braço E/D (shoulder) - Cotovelo E/D - Pulso E/D - Dedos E/D
    ///  + Perna E/D (hip) - Joelho E/D - Tornozelo E/D - Ponta do pé E/D
    /// </code>
    /// The animator only rotates bones (and moves the root). Bones a model lacks stay null; the animator
    /// then gives their share of the motion to the parent. A Blender model with these names replaces the
    /// generated meshes.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        public Transform Hips;
        /// <summary>Lower spine (waist). <see cref="Chest"/> carries the upper body.</summary>
        public Transform Torso;
        public Transform Chest;
        public Transform Neck;
        public Transform Head;
        public Transform Jaw;
        public Transform ClavicleL;
        public Transform ClavicleR;
        public Transform ArmL;
        public Transform ArmR;
        /// <summary>Lower-limb bones: forearm (shield) and shin. Straight at identity.</summary>
        public Transform ElbowL;
        public Transform ElbowR;
        /// <summary>Hand and anything held in it.</summary>
        public Transform WristL;
        public Transform WristR;
        public Transform FingersL;
        public Transform FingersR;
        public Transform LegL;
        public Transform LegR;
        public Transform KneeL;
        public Transform KneeR;
        public Transform AnkleL;
        public Transform AnkleR;
        public Transform ToesL;
        public Transform ToesR;
        /// <summary>Secondary bone: the satchel on the right hip.</summary>
        public Transform Bag;
        public Transform Glider;
        /// <summary>Captured gait loops; when set, the animator walks and runs with them instead of its own curves.</summary>
        public MotionClip Walk;
        public MotionClip Run;

        public float HipsHeight { get; internal set; }

        /// <summary>Height of the soles at rest, in the rig's local space: where the ground is.</summary>
        public float GroundY { get; private set; }

        /// <summary>
        /// Points that can touch the ground, each in its bone's local space: heel and sole (ankle), ball and
        /// toe tip (toes), front of the knee. The animator lowers the hips until the lowest one meets the ground.
        /// </summary>
        public readonly List<(Transform bone, Vector3 local)> Contacts = new List<(Transform, Vector3)>();

        public static CharacterRig Build(Transform parent, RigStyle style)
        {
            if (!string.IsNullOrEmpty(style.SkinnedModel))
            {
                var skinned = SkinnedHero.Build(parent, style);
                if (skinned != null) return skinned;
            }

            if (!string.IsNullOrEmpty(style.Model))
            {
                var fromModel = FromModel(parent, style);
                if (fromModel != null) return fromModel;
            }

            return BuildProcedural(parent, style);
        }

        /// <summary>
        /// Uses the Blender model. Joints are rebuilt with identity rotation at the imported joint
        /// positions, so the animator poses work the same whatever axis conversion the import applied.
        /// </summary>
        private static CharacterRig FromModel(Transform parent, RigStyle style)
        {
            var root = new GameObject("Visual");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * style.Scale;
            var model = ModelLibrary.Spawn("Characters/" + style.Model, root.transform, style.ColorFor);
            if (model == null)
            {
                Object.Destroy(root);
                return null;
            }

            var rig = root.AddComponent<CharacterRig>();
            var m = model.transform;
            rig.Hips = Rejoint(m, "Quadril", root.transform);
            rig.Torso = Rejoint(m, "Tronco", rig.Hips);
            rig.Bag = Optional(m, "Bolsa", rig.Torso);
            rig.Chest = Optional(m, "Tórax", rig.Torso);
            var upper = rig.Chest != null ? rig.Chest : rig.Torso;
            rig.Neck = Optional(m, "Pescoço", upper);
            rig.Head = Rejoint(m, "Cabeça", rig.Neck != null ? rig.Neck : upper);
            rig.Jaw = Optional(m, "Mandíbula", rig.Head);
            rig.ClavicleL = Optional(m, "Clavícula E", upper);
            rig.ClavicleR = Optional(m, "Clavícula D", upper);
            rig.ArmL = Rejoint(m, "Braço E", rig.ClavicleL != null ? rig.ClavicleL : upper);
            rig.ArmR = Rejoint(m, "Braço D", rig.ClavicleR != null ? rig.ClavicleR : upper);
            rig.ElbowL = Optional(m, "Cotovelo E", rig.ArmL);
            rig.ElbowR = Optional(m, "Cotovelo D", rig.ArmR);
            rig.WristL = Optional(m, "Pulso E", rig.ElbowL);
            rig.WristR = Optional(m, "Pulso D", rig.ElbowR);
            rig.FingersL = Optional(m, "Dedos E", rig.WristL);
            rig.FingersR = Optional(m, "Dedos D", rig.WristR);
            rig.LegL = Rejoint(m, "Perna E", rig.Hips);
            rig.LegR = Rejoint(m, "Perna D", rig.Hips);
            rig.KneeL = Optional(m, "Joelho E", rig.LegL);
            rig.KneeR = Optional(m, "Joelho D", rig.LegR);
            rig.AnkleL = Optional(m, "Tornozelo E", rig.KneeL);
            rig.AnkleR = Optional(m, "Tornozelo D", rig.KneeR);
            rig.ToesL = Optional(m, "Ponta do pé E", rig.AnkleL);
            rig.ToesR = Optional(m, "Ponta do pé D", rig.AnkleR);
            rig.Glider = Rejoint(m, "Planador", upper);
            rig.HipsHeight = rig.Hips.localPosition.y;
            rig.Glider.gameObject.SetActive(false);
            Object.Destroy(model);
            HideHandGear(rig);
            rig.FindContacts();
            return rig;
        }

        /// <summary>A bone the model may not have (older exports): null when absent or when its parent is.</summary>
        private static Transform Optional(Transform model, string name, Transform newParent)
        {
            if (newParent == null || model.Find(name) == null) return null;
            return Rejoint(model, name, newParent);
        }

        /// <summary>
        /// At rest (all bones at identity) the lowest drawn point is the ground. Contact points then sit on the
        /// sole under the heel, the arch, the ball and the toe tip, and on the front of the knee.
        /// </summary>
        /// <param name="groundY">The sole height when the caller measured it (skinned bounds are loose).</param>
        internal void FindContacts(float? groundY = null)
        {
            Contacts.Clear();
            var lowest = float.MaxValue;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                lowest = Mathf.Min(lowest, transform.InverseTransformPoint(r.bounds.min).y);
            }

            GroundY = groundY ?? (lowest == float.MaxValue ? 0f : lowest);
            foreach (var (knee, ankle, toes) in new[] { (KneeL, AnkleL, ToesL), (KneeR, AnkleR, ToesR) })
            {
                var foot = ankle != null ? ankle : knee;
                if (foot == null) continue;
                var heel = transform.InverseTransformPoint(foot.position);
                AddContact(foot, new Vector3(heel.x, GroundY, heel.z - 0.08f));
                AddContact(foot, new Vector3(heel.x, GroundY, heel.z + 0.08f));
                if (toes != null)
                {
                    var ball = transform.InverseTransformPoint(toes.position);
                    AddContact(toes, new Vector3(ball.x, GroundY, ball.z + 0.09f));
                }

                if (knee != null && knee != foot)
                {
                    var k = transform.InverseTransformPoint(knee.position);
                    AddContact(knee, new Vector3(k.x, k.y - 0.03f, k.z + 0.1f));
                }
            }
        }

        private void AddContact(Transform bone, Vector3 rigLocal) =>
            Contacts.Add((bone, bone.InverseTransformPoint(transform.TransformPoint(rigLocal))));

        /// <summary>How far the lowest contact point is above the ground, in the rig's local space.</summary>
        public float LowestContactHeight()
        {
            var lowest = float.MaxValue;
            foreach (var (bone, local) in Contacts)
                lowest = Mathf.Min(lowest, transform.InverseTransformPoint(bone.TransformPoint(local)).y);
            return lowest == float.MaxValue ? 0f : lowest - GroundY;
        }

        /// <summary>
        /// Which piece of gear a mesh belongs to, from its name ("Joint~Part@slot"). Each item exists twice in
        /// the model: stowed ("... Costas ...") and in the hands. Exactly one of the two is drawn at a time.
        /// </summary>
        public static GearPart Classify(string meshName)
        {
            var tilde = meshName.IndexOf('~');
            if (tilde < 0) return GearPart.None;
            var part = meshName.Substring(tilde + 1);
            var stowed = part.Contains("Costas");
            if (part.StartsWith("Arma")) return stowed ? GearPart.SwordBack : GearPart.SwordHand;
            if (part.StartsWith("Escudo")) return stowed ? GearPart.ShieldBack : GearPart.ShieldHand;
            if (part.StartsWith("Arco")) return stowed ? GearPart.BowBack : GearPart.BowHand;
            return GearPart.None;
        }

        public static bool IsHandGear(GearPart part) =>
            part == GearPart.SwordHand || part == GearPart.ShieldHand || part == GearPart.BowHand;

        /// <summary>Gear held in the hands starts hidden; HeroPresenter shows it when drawn, raised or aimed.</summary>
        internal static void HideHandGear(CharacterRig rig)
        {
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
                if (IsHandGear(Classify(r.name)))
                    r.enabled = false;
        }

        /// <summary>
        /// The model is exported flat: joints are empties, and each mesh is named "Joint~Part@slot".
        /// Builds the joint at the empty's position and moves that joint's meshes under it.
        /// </summary>
        private static Transform Rejoint(Transform model, string name, Transform newParent)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(newParent, false);
            var source = model.Find(name);
            if (source == null) return joint;

            joint.position = source.position;
            joint.localRotation = Quaternion.identity;
            var prefix = name + "~";
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith(prefix))
                    r.transform.SetParent(joint, true);
            return joint;
        }

        /// <summary>Fallback when the Blender models are missing: the same joints with generated meshes.</summary>
        private static CharacterRig BuildProcedural(Transform parent, RigStyle style)
        {
            var root = new GameObject("Visual");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * style.Scale;
            var rig = root.AddComponent<CharacterRig>();

            var skin = Palette.Outlined(style.Skin);
            var hair = Palette.Outlined(style.Hair);
            var tunic = Palette.Outlined(style.Tunic);
            var pants = Palette.Outlined(style.Pants);
            var boots = Palette.Outlined(style.Boots);
            var accent = Palette.Outlined(style.Accent);
            var dark = Palette.Solid(new Color(0.08f, 0.07f, 0.1f));
            var down = Quaternion.Euler(180f, 0f, 0f);

            rig.HipsHeight = 0.9f;
            rig.Hips = Joint("Quadril", root.transform, new Vector3(0f, rig.HipsHeight, 0f));
            LowPoly.Part("Pelve", rig.Hips, LowPoly.Prism(6, 0.85f), pants, new Vector3(0f, -0.1f, 0f), new Vector3(0.36f, 0.18f, 0.26f));

            rig.Torso = Joint("Tronco", rig.Hips, new Vector3(0f, 0.06f, 0f));
            LowPoly.Part("Peito", rig.Torso, LowPoly.Prism(6, 1.18f), tunic, Vector3.zero, new Vector3(0.38f, 0.5f, 0.27f));
            LowPoly.Part("Cinto", rig.Torso, LowPoly.Prism(6), accent, new Vector3(0f, -0.02f, 0f), new Vector3(0.41f, 0.07f, 0.3f));
            LowPoly.Part("Gola", rig.Torso, LowPoly.Prism(5, 0.8f), accent, new Vector3(0f, 0.44f, 0f), new Vector3(0.3f, 0.09f, 0.24f));

            rig.Head = Joint("Cabeça", rig.Torso, new Vector3(0f, 0.52f, 0f));
            LowPoly.Part("Rosto", rig.Head, LowPoly.Ico(1), skin, new Vector3(0f, 0.19f, 0f), new Vector3(0.34f, 0.36f, 0.33f));
            LowPoly.Part("Olho E", rig.Head, LowPoly.Prism(4), dark, new Vector3(-0.07f, 0.2f, 0.16f), new Vector3(0.045f, 0.07f, 0.03f));
            LowPoly.Part("Olho D", rig.Head, LowPoly.Prism(4), dark, new Vector3(0.07f, 0.2f, 0.16f), new Vector3(0.045f, 0.07f, 0.03f));
            if (!style.Horns)
            {
                LowPoly.Part("Cabelo", rig.Head, LowPoly.Ico(0), hair, new Vector3(0f, 0.29f, -0.04f), new Vector3(0.39f, 0.3f, 0.38f));
                LowPoly.Part("Franja", rig.Head, LowPoly.Prism(3, 0f), hair, new Vector3(0.05f, 0.3f, 0.14f), new Vector3(0.22f, 0.16f, 0.1f), Quaternion.Euler(120f, 0f, 0f));
            }

            if (style.Ponytail)
                LowPoly.Part("Rabo de cavalo", rig.Head, LowPoly.Prism(5, 0f), hair, new Vector3(0f, 0.3f, -0.17f), new Vector3(0.13f, 0.38f, 0.13f), Quaternion.Euler(-150f, 0f, 0f));

            if (style.Horns)
            {
                LowPoly.Part("Chifre E", rig.Head, LowPoly.Prism(5, 0f), Palette.Outlined(style.Accent), new Vector3(-0.12f, 0.33f, 0.02f), new Vector3(0.08f, 0.22f, 0.08f), Quaternion.Euler(-20f, 0f, 25f));
                LowPoly.Part("Chifre D", rig.Head, LowPoly.Prism(5, 0f), Palette.Outlined(style.Accent), new Vector3(0.12f, 0.33f, 0.02f), new Vector3(0.08f, 0.22f, 0.08f), Quaternion.Euler(-20f, 0f, -25f));
            }

            if (style.Shell)
                LowPoly.Part("Casco", rig.Torso, LowPoly.Ico(0, 0.12f, 7), hair, new Vector3(0f, 0.3f, -0.14f), new Vector3(0.62f, 0.55f, 0.45f));

            rig.ArmL = Arm("Braço E", rig.Torso, -1f, tunic, skin, down);
            rig.ArmR = Arm("Braço D", rig.Torso, 1f, tunic, skin, down);
            rig.LegL = Leg("Perna E", rig.Hips, -1f, pants, boots, down);
            rig.LegR = Leg("Perna D", rig.Hips, 1f, pants, boots, down);
            rig.ElbowL = Joint("Cotovelo E", rig.ArmL, new Vector3(0f, -0.28f, 0f));
            rig.ElbowR = Joint("Cotovelo D", rig.ArmR, new Vector3(0f, -0.28f, 0f));
            rig.KneeL = Joint("Joelho E", rig.LegL, new Vector3(0f, -0.44f, 0f));
            rig.KneeR = Joint("Joelho D", rig.LegR, new Vector3(0f, -0.44f, 0f));

            rig.Glider = Joint("Planador", rig.Torso, new Vector3(0f, 1.05f, 0.05f));
            LowPoly.Part("Asa", rig.Glider, LowPoly.Glider(), Palette.Solid(style.Accent), Vector3.zero, new Vector3(2.1f, 0.6f, 1.1f));
            LowPoly.Part("Haste", rig.Glider, LowPoly.Prism(4), boots, new Vector3(0f, -0.5f, 0f), new Vector3(0.04f, 0.5f, 0.04f));
            rig.Glider.gameObject.SetActive(false);

            return rig;
        }

        private static Transform Arm(string name, Transform torso, float side, Material sleeve, Material skin, Quaternion down)
        {
            var joint = Joint(name, torso, new Vector3(0.25f * side, 0.43f, 0f));
            LowPoly.Part("Manga", joint, LowPoly.Prism(5, 0.8f), sleeve, Vector3.zero, new Vector3(0.13f, 0.3f, 0.13f), down);
            LowPoly.Part("Antebraço", joint, LowPoly.Prism(5, 0.85f), skin, new Vector3(0f, -0.28f, 0f), new Vector3(0.1f, 0.28f, 0.1f), down);
            LowPoly.Part("Mão", joint, LowPoly.Ico(0), skin, new Vector3(0f, -0.6f, 0f), new Vector3(0.12f, 0.12f, 0.12f));
            return joint;
        }

        private static Transform Leg(string name, Transform hips, float side, Material pants, Material boots, Quaternion down)
        {
            var joint = Joint(name, hips, new Vector3(0.11f * side, -0.08f, 0f));
            LowPoly.Part("Coxa", joint, LowPoly.Prism(5, 0.8f), pants, Vector3.zero, new Vector3(0.16f, 0.72f, 0.16f), down);
            LowPoly.Part("Bota", joint, LowPoly.Prism(6, 0.8f), boots, new Vector3(0f, -0.82f, 0.04f), new Vector3(0.17f, 0.13f, 0.25f));
            return joint;
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }
    }
}
