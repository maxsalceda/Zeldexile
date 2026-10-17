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
            Model = "heroi"
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
    /// Joint hierarchy of a character. The animator only rotates these joints, so a Blender model
    /// with the same joints can replace the generated meshes later.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        public Transform Hips;
        public Transform Torso;
        public Transform Head;
        public Transform ArmL;
        public Transform ArmR;
        public Transform LegL;
        public Transform LegR;
        public Transform Glider;

        public float HipsHeight { get; private set; }

        public static CharacterRig Build(Transform parent, RigStyle style)
        {
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
            rig.Hips = Rejoint(model.transform, "Quadril", root.transform);
            rig.Torso = Rejoint(model.transform, "Tronco", rig.Hips);
            rig.Head = Rejoint(model.transform, "Cabeça", rig.Torso);
            rig.ArmL = Rejoint(model.transform, "Braço E", rig.Torso);
            rig.ArmR = Rejoint(model.transform, "Braço D", rig.Torso);
            rig.LegL = Rejoint(model.transform, "Perna E", rig.Hips);
            rig.LegR = Rejoint(model.transform, "Perna D", rig.Hips);
            rig.Glider = Rejoint(model.transform, "Planador", rig.Torso);
            rig.HipsHeight = rig.Hips.localPosition.y;
            rig.Glider.gameObject.SetActive(false);
            Object.Destroy(model);
            HideHandGear(rig);
            return rig;
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
        private static void HideHandGear(CharacterRig rig)
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
