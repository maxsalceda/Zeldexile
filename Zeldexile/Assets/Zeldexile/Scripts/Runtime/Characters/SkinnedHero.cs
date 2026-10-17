using System.Collections.Generic;
using UnityEngine;
using Zeldexile.World;

namespace Zeldexile.Characters
{
    /// <summary>
    /// A skinned model with a Mixamo skeleton (the Meshy hero) driven by the procedural <see cref="CharacterAnimator"/>.
    /// The rig gets the usual identity-rest joints at the bone positions; <see cref="SkinnedRigDriver"/> copies each
    /// joint's rotation onto its bone every frame, so every pose (walk, run, swings, climb, glide...) works unchanged.
    /// </summary>
    public static class SkinnedHero
    {
        /// <summary>Height of the hero from sole to top of the hair, in meters (the controller is 1.8 m).</summary>
        public const float TargetHeight = 1.75f;

        /// <summary>Rig joint and the bone it drives, by its canonical (Mixamo) name; see <see cref="CanonicalBones"/>.</summary>
        private static readonly (string joint, string bone)[] Map =
        {
            ("Quadril", "Hips"), ("Tronco", "Spine"), ("Tórax", "Spine2"), ("Pescoço", "Neck"), ("Cabeça", "Head"),
            ("Clavícula E", "LeftShoulder"), ("Braço E", "LeftArm"), ("Cotovelo E", "LeftForeArm"), ("Pulso E", "LeftHand"),
            ("Clavícula D", "RightShoulder"), ("Braço D", "RightArm"), ("Cotovelo D", "RightForeArm"), ("Pulso D", "RightHand"),
            ("Perna E", "LeftUpLeg"), ("Joelho E", "LeftLeg"), ("Tornozelo E", "LeftFoot"), ("Ponta do pé E", "LeftToeBase"),
            ("Perna D", "RightUpLeg"), ("Joelho D", "RightLeg"), ("Tornozelo D", "RightFoot"), ("Ponta do pé D", "RightToeBase"),
        };

        public static CharacterRig Build(Transform parent, RigStyle style)
        {
            var prefab = ModelLibrary.Load("Skinned/" + style.SkinnedModel);
            if (prefab == null) return null;

            var root = new GameObject("Visual");
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * style.Scale;
            var model = Object.Instantiate(prefab, root.transform, false);
            model.name = prefab.name;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(animator);

            var bones = CanonicalBones(model.transform);

            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0 || !bones.ContainsKey("Hips") || !bones.ContainsKey("LeftArm") || !bones.ContainsKey("Head"))
            {
                Object.Destroy(root);
                return null;
            }

            // Bind pose (repaired), then face +Z (the character's left on -X), straighten the skeleton into a standing
            // rest pose, and stand the soles on y = 0 at the target height with the hips over the origin.
            var names = new Dictionary<Transform, string>();
            foreach (var pair in bones) names[pair.Value] = pair.Key;
            foreach (var skin in skins)
            {
                RepairBindposes(skin, names);
                ToBindPose(skin);
            }

            var r = root.transform;
            Vector3 P(string bone) => r.InverseTransformPoint(bones[bone].position);
            var feet = bones.ContainsKey("LeftFoot") && bones.ContainsKey("RightFoot") ? (P("LeftFoot") + P("RightFoot")) * 0.5f : P("Hips");
            var up = P("Head") - feet;
            var right = Vector3.ProjectOnPlane(bones.ContainsKey("RightUpLeg") && bones.ContainsKey("LeftUpLeg")
                ? P("RightUpLeg") - P("LeftUpLeg") : P("RightArm") - P("LeftArm"), up);
            var frame = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            model.transform.localRotation = Quaternion.Inverse(frame) * model.transform.localRotation;
            StandUp(bones, r);

            var (low, high) = VerticalExtent(bones, r);
            model.transform.localScale *= TargetHeight / Mathf.Max(0.01f, high - low);
            (low, _) = VerticalExtent(bones, r);
            var hips = P("Hips");
            model.transform.localPosition -= new Vector3(hips.x, low, hips.z);

            var material = TexturedMaterial(style.SkinnedModel);
            foreach (var skin in skins)
            {
                var materials = skin.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) materials[i] = material;
                skin.sharedMaterials = materials;
                skin.updateWhenOffscreen = true;
            }

            var rig = root.AddComponent<CharacterRig>();
            var driver = root.AddComponent<SkinnedRigDriver>();
            Transform Joint(string name, Transform jointParent)
            {
                var index = System.Array.FindIndex(Map, m => m.joint == name);
                if (jointParent == null || index < 0 || !bones.TryGetValue(Map[index].bone, out var bone)) return null;
                var joint = new GameObject(name).transform;
                joint.SetParent(jointParent, false);
                joint.position = bone.position;
                joint.localRotation = Quaternion.identity;
                driver.Add(joint, bone);
                return joint;
            }

            rig.Hips = Joint("Quadril", r);
            rig.Torso = Joint("Tronco", rig.Hips);
            rig.Chest = Joint("Tórax", rig.Torso);
            var upper = rig.Chest != null ? rig.Chest : rig.Torso;
            rig.Neck = Joint("Pescoço", upper);
            rig.Head = Joint("Cabeça", rig.Neck != null ? rig.Neck : upper);
            rig.ClavicleL = Joint("Clavícula E", upper);
            rig.ClavicleR = Joint("Clavícula D", upper);
            rig.ArmL = Joint("Braço E", rig.ClavicleL != null ? rig.ClavicleL : upper);
            rig.ArmR = Joint("Braço D", rig.ClavicleR != null ? rig.ClavicleR : upper);
            rig.ElbowL = Joint("Cotovelo E", rig.ArmL);
            rig.ElbowR = Joint("Cotovelo D", rig.ArmR);
            rig.WristL = Joint("Pulso E", rig.ElbowL);
            rig.WristR = Joint("Pulso D", rig.ElbowR);
            rig.LegL = Joint("Perna E", rig.Hips);
            rig.LegR = Joint("Perna D", rig.Hips);
            rig.KneeL = Joint("Joelho E", rig.LegL);
            rig.KneeR = Joint("Joelho D", rig.LegR);
            rig.AnkleL = Joint("Tornozelo E", rig.KneeL);
            rig.AnkleR = Joint("Tornozelo D", rig.KneeR);
            rig.ToesL = Joint("Ponta do pé E", rig.AnkleL);
            rig.ToesR = Joint("Ponta do pé D", rig.AnkleR);

            rig.Glider = Glider(r, upper, style);
            BorrowGear(rig, style);
            rig.HipsHeight = rig.Hips.localPosition.y;
            rig.FindContacts(0f);
            return rig;
        }

        /// <summary>The bone names the rest of this class uses (Mixamo's), each with the spellings other exports use.</summary>
        private static readonly string[][] BoneNames =
        {
            new[] { "Hips" }, new[] { "Neck" }, new[] { "Head" }, new[] { "HeadTop_End", "head_end", "HeadEnd" },
            new[] { "LeftShoulder" }, new[] { "LeftArm" }, new[] { "LeftForeArm" }, new[] { "LeftHand" },
            new[] { "RightShoulder" }, new[] { "RightArm" }, new[] { "RightForeArm" }, new[] { "RightHand" },
            new[] { "LeftUpLeg" }, new[] { "LeftLeg" }, new[] { "LeftFoot" }, new[] { "LeftToeBase" }, new[] { "LeftToe_End" },
            new[] { "RightUpLeg" }, new[] { "RightLeg" }, new[] { "RightFoot" }, new[] { "RightToeBase" }, new[] { "RightToe_End" },
        };

        /// <summary>
        /// The model's bones under Mixamo's names, whatever the export called them: no "mixamorig:" prefix, any
        /// letter case. The spine is named by position, not by name: Meshy's own rig counts it top-down (Spine02,
        /// Spine01, Spine under the neck) where Mixamo counts bottom-up. The bone above the hips becomes "Spine",
        /// the one carrying the neck and shoulders "Spine2", one in between "Spine1".
        /// </summary>
        private static Dictionary<string, Transform> CanonicalBones(Transform model)
        {
            var raw = new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                var colon = t.name.LastIndexOf(':');
                var bare = colon < 0 ? t.name : t.name.Substring(colon + 1);
                if (!raw.ContainsKey(bare)) raw[bare] = t;
            }

            var bones = new Dictionary<string, Transform>();
            foreach (var spellings in BoneNames)
                foreach (var spelling in spellings)
                    if (raw.TryGetValue(spelling, out var t))
                    {
                        bones[spellings[0]] = t;
                        break;
                    }

            if (bones.TryGetValue("Hips", out var hips) && bones.TryGetValue("Neck", out var neck))
            {
                var chain = new List<Transform>();
                for (var t = neck.parent; t != null && t != hips; t = t.parent) chain.Insert(0, t);
                if (chain.Count > 0 && chain[0].parent == hips)
                {
                    bones["Spine"] = chain[0];
                    if (chain.Count > 1) bones["Spine2"] = chain[chain.Count - 1];
                    if (chain.Count > 2) bones["Spine1"] = chain[1];
                }
            }

            return bones;
        }

        /// <summary>Bone segments used to fit the skeleton onto the mesh: each one against the vertices it moves most.</summary>
        private static readonly (string bone, string child)[] FitSegments =
        {
            ("Hips", "Spine"), ("Spine1", "Spine2"), ("Head", "HeadTop_End"),
            ("LeftArm", "LeftForeArm"), ("LeftForeArm", "LeftHand"), ("RightArm", "RightForeArm"), ("RightForeArm", "RightHand"),
            ("LeftUpLeg", "LeftLeg"), ("LeftLeg", "LeftFoot"), ("RightUpLeg", "RightLeg"), ("RightLeg", "RightFoot"),
        };

        /// <summary>Imported mesh and its copy with repaired bindposes (null when it needed none), shared by every hero.</summary>
        private static readonly Dictionary<Mesh, Mesh> Repaired = new Dictionary<Mesh, Mesh>();

        /// <summary>
        /// Meshy's FBX binds the skin to a skeleton that does not sit on the mesh (same size, but turned 90 degrees and
        /// moved away), so the skin flies off the bones. Fits the skeleton back onto the mesh, comparing each segment's
        /// middle with the centroid of the vertices it drives, and gives the renderer a copy of the mesh with the
        /// corrected bindposes. A model whose skeleton already lies inside its mesh is left alone.
        /// </summary>
        private static void RepairBindposes(SkinnedMeshRenderer skin, Dictionary<Transform, string> names)
        {
            var mesh = skin.sharedMesh;
            if (mesh == null) return;
            if (Repaired.TryGetValue(mesh, out var done))
            {
                if (done != null) skin.sharedMesh = done;
                return;
            }

            Repaired[mesh] = null;
            if (!mesh.isReadable) return;
            var bindposes = mesh.bindposes;
            var bones = skin.bones;
            if (bindposes.Length != bones.Length) return;
            var index = new Dictionary<string, int>();
            for (var i = 0; i < bones.Length; i++)
                if (bones[i] != null && names.TryGetValue(bones[i], out var name))
                    index[name] = i;

            Vector3 Bind(int i) => bindposes[i].inverse.GetColumn(3);
            if (!index.TryGetValue("Hips", out var hipsIndex) || !index.TryGetValue("Head", out var headIndex)) return;
            var area = mesh.bounds;
            area.Expand(area.size.magnitude * 0.25f);
            if (area.Contains(Bind(hipsIndex)) && area.Contains(Bind(headIndex))) return;

            var sums = new Vector3[bones.Length];
            var counts = new int[bones.Length];
            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            for (var v = 0; v < vertices.Length && v < weights.Length; v++)
            {
                if (weights[v].weight0 < 0.5f) continue;
                sums[weights[v].boneIndex0] += vertices[v];
                counts[weights[v].boneIndex0]++;
            }

            var skeleton = new Dictionary<string, Vector3>();
            var flesh = new Dictionary<string, Vector3>();
            foreach (var (bone, child) in FitSegments)
            {
                if (!index.TryGetValue(bone, out var b) || !index.TryGetValue(child, out var c) || counts[b] < 20) continue;
                skeleton[bone] = (Bind(b) + Bind(c)) * 0.5f;
                flesh[bone] = sums[b] / counts[b];
            }

            if (!skeleton.ContainsKey("Head") || !skeleton.ContainsKey("LeftUpLeg") || !skeleton.ContainsKey("RightUpLeg")) return;
            Vector3 Legs(Dictionary<string, Vector3> d) => (d["LeftUpLeg"] + d["RightUpLeg"]) * 0.5f;
            var upS = skeleton["Head"] - Legs(skeleton);
            var upM = flesh["Head"] - Legs(flesh);
            var rightS = skeleton["RightUpLeg"] - skeleton["LeftUpLeg"];
            var rightM = flesh["RightUpLeg"] - flesh["LeftUpLeg"];
            var turn = Quaternion.LookRotation(Vector3.Cross(rightM, upM), upM)
                       * Quaternion.Inverse(Quaternion.LookRotation(Vector3.Cross(rightS, upS), upS));
            var scale = upM.magnitude / Mathf.Max(1e-9f, upS.magnitude);
            Vector3 meanS = Vector3.zero, meanM = Vector3.zero;
            foreach (var key in skeleton.Keys)
            {
                meanS += skeleton[key];
                meanM += flesh[key];
            }

            meanS /= skeleton.Count;
            meanM /= skeleton.Count;
            var fit = Matrix4x4.TRS(meanM - turn * (meanS * scale), turn, Vector3.one * scale);
            var unfit = fit.inverse;
            for (var i = 0; i < bindposes.Length; i++) bindposes[i] = bindposes[i] * unfit;
            var repaired = Object.Instantiate(mesh);
            repaired.name = mesh.name;
            repaired.bindposes = bindposes;
            Repaired[mesh] = repaired;
            skin.sharedMesh = repaired;
        }

        /// <summary>Puts every bone where the mesh was bound to it (position and rotation), parents first.</summary>
        private static void ToBindPose(SkinnedMeshRenderer skin)
        {
            var bindposes = skin.sharedMesh != null ? skin.sharedMesh.bindposes : null;
            var bones = skin.bones;
            if (bindposes == null || bindposes.Length != bones.Length) return;
            var order = new List<int>();
            for (var i = 0; i < bones.Length; i++) if (bones[i] != null) order.Add(i);
            order.Sort((x, y) => Depth(bones[x]).CompareTo(Depth(bones[y])));
            foreach (var i in order)
            {
                var world = skin.transform.localToWorldMatrix * bindposes[i].inverse;
                bones[i].SetPositionAndRotation(world.GetColumn(3), world.rotation);
            }
        }

        private static int Depth(Transform t)
        {
            var depth = 0;
            for (; t != null; t = t.parent) depth++;
            return depth;
        }

        /// <summary>Each bone of a chain, parent first, and the direction its child should lie in at rest.</summary>
        private static readonly (string bone, string child, Vector3 dir)[] Rest =
        {
            ("Hips", "Spine", Vector3.up), ("Spine", "Spine1", Vector3.up), ("Spine1", "Spine2", Vector3.up),
            ("Spine2", "Neck", Vector3.up), ("Neck", "Head", Vector3.up), ("Head", "HeadTop_End", Vector3.up),
            ("LeftUpLeg", "LeftLeg", Vector3.down), ("LeftLeg", "LeftFoot", Vector3.down),
            ("LeftFoot", "LeftToeBase", new Vector3(0f, -0.45f, 1f)), ("LeftToeBase", "LeftToe_End", Vector3.forward),
            ("RightUpLeg", "RightLeg", Vector3.down), ("RightLeg", "RightFoot", Vector3.down),
            ("RightFoot", "RightToeBase", new Vector3(0f, -0.45f, 1f)), ("RightToeBase", "RightToe_End", Vector3.forward),
            ("LeftShoulder", "LeftArm", Vector3.left), ("LeftArm", "LeftForeArm", new Vector3(-0.12f, -1f, 0f)),
            ("LeftForeArm", "LeftHand", new Vector3(-0.08f, -1f, 0.08f)),
            ("RightShoulder", "RightArm", Vector3.right), ("RightArm", "RightForeArm", new Vector3(0.12f, -1f, 0f)),
            ("RightForeArm", "RightHand", new Vector3(0.08f, -1f, 0.08f)),
        };

        /// <summary>
        /// The bind pose holds the arms out (A-pose). Rotates each bone so the next one lies where a hero standing
        /// with arms down has it (the animator's identity pose), keeping each bone's twist.
        /// </summary>
        private static void StandUp(Dictionary<string, Transform> bones, Transform root)
        {
            foreach (var (name, childName, dir) in Rest)
            {
                if (!bones.TryGetValue(name, out var bone) || !bones.TryGetValue(childName, out var child)) continue;
                var current = child.position - bone.position;
                if (current.sqrMagnitude < 1e-12f) continue;
                bone.rotation = Quaternion.FromToRotation(current, root.TransformDirection(dir)) * bone.rotation;
            }
        }

        /// <summary>
        /// Sole and top of the hair in the root's space, from the bones (the Meshy export nests scales that make the
        /// mesh bounds unreliable): the toe base sits just above the sole, the head-top end just under the hair.
        /// </summary>
        private static (float low, float high) VerticalExtent(Dictionary<string, Transform> bones, Transform root)
        {
            float Y(string bone) => bones.TryGetValue(bone, out var t) ? root.InverseTransformPoint(t.position).y : float.NaN;
            var toe = Mathf.Min(Y("LeftToeBase"), Y("RightToeBase"));
            if (float.IsNaN(toe)) toe = Mathf.Min(Y("LeftFoot"), Y("RightFoot"));
            var top = bones.ContainsKey("HeadTop_End") ? Y("HeadTop_End") : Y("Head");
            if (float.IsNaN(toe) || float.IsNaN(top) || top <= toe) return (0f, 1f);
            var span = top - toe;
            return (toe - span * 0.02f, top + span * 0.03f);
        }

        /// <summary>Joints the skinned rig lacks, and the joint their meshes ride on instead.</summary>
        private static readonly Dictionary<string, string> GearFallback = new Dictionary<string, string>
        {
            { "Dedos E", "Pulso E" }, { "Dedos D", "Pulso D" }, { "Bolsa", "Tronco" }, { "Mandíbula", "Cabeça" },
        };

        /// <summary>
        /// The Meshy hero has no gear of its own. Takes the sword, shield and bow meshes (in hand and stowed) of the
        /// Blender hero (<see cref="RigStyle.Model"/>) and hangs each on the same joint here, at the same offset from
        /// it; HeroPresenter then shows and hides them as usual.
        /// </summary>
        private static void BorrowGear(CharacterRig rig, RigStyle style)
        {
            if (string.IsNullOrEmpty(style.Model)) return;
            // Before spawning the source: its joint empties carry the same names.
            var joints = new Dictionary<string, Transform>();
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) joints[t.name] = t;
            var source = ModelLibrary.Spawn("Characters/" + style.Model, rig.transform, style.ColorFor);
            if (source == null) return;
            var m = source.transform;
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                if (CharacterRig.Classify(renderer.name) == GearPart.None) continue;
                var name = renderer.name.Substring(0, renderer.name.IndexOf('~'));
                if (!joints.ContainsKey(name) && GearFallback.TryGetValue(name, out var fallback)) name = fallback;
                var anchor = m.Find(name);
                if (!joints.TryGetValue(name, out var joint) || anchor == null) continue;
                // Both rigs have identity-rotation joints under the Visual root, so the offset carries over as is.
                var offset = rig.transform.InverseTransformVector(renderer.transform.position - anchor.position);
                var rotation = Quaternion.Inverse(rig.transform.rotation) * renderer.transform.rotation;
                var scale = renderer.transform.lossyScale;
                renderer.transform.SetParent(joint, false);
                renderer.transform.localPosition = offset;
                renderer.transform.localRotation = rotation;
                renderer.transform.localScale = new Vector3(scale.x / joint.lossyScale.x, scale.y / joint.lossyScale.y, scale.z / joint.lossyScale.z);
            }

            Object.Destroy(source);
            CharacterRig.HideHandGear(rig);
        }

        /// <summary>The paraglider of the procedural rig, folded above the shoulders until the animator opens it.</summary>
        private static Transform Glider(Transform root, Transform upper, RigStyle style)
        {
            var glider = new GameObject("Planador").transform;
            glider.SetParent(upper, false);
            // Same place as on the procedural rig: about 2 m up, just behind the head.
            glider.position = root.TransformPoint(new Vector3(0f, TargetHeight + 0.25f, 0.05f));
            LowPoly.Part("Asa", glider, LowPoly.Glider(), Palette.Solid(style.Accent), Vector3.zero, new Vector3(2.1f, 0.6f, 1.1f));
            LowPoly.Part("Haste", glider, LowPoly.Prism(4), Palette.Outlined(style.Boots), new Vector3(0f, -0.5f, 0f), new Vector3(0.04f, 0.5f, 0.04f));
            glider.gameObject.SetActive(false);
            return glider;
        }

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        private static Material TexturedMaterial(string model)
        {
            if (Materials.TryGetValue(model, out var m) && m != null) return m;
            var shader = Shader.Find("Zeldexile/ToonOutlineTextured");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { mainTexture = Resources.Load<Texture2D>("Models/Skinned/" + model + "_cor") };
            Materials[model] = m;
            return m;
        }
    }

    /// <summary>
    /// Runs after <see cref="CharacterAnimator"/>: each bone takes its joint's rotation times the bind offset between
    /// them, and the hips bone follows the hips joint's position (the animator moves the hips up and down).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class SkinnedRigDriver : MonoBehaviour
    {
        private readonly List<(Transform joint, Transform bone, Quaternion offset)> _pairs = new List<(Transform, Transform, Quaternion)>();
        private Transform _hipsJoint;
        private Transform _hipsBone;

        public void Add(Transform joint, Transform bone)
        {
            _pairs.Add((joint, bone, Quaternion.Inverse(joint.rotation) * bone.rotation));
            if (_hipsJoint == null)
            {
                _hipsJoint = joint;
                _hipsBone = bone;
            }
        }

        private void LateUpdate()
        {
            if (_hipsBone != null) _hipsBone.position = _hipsJoint.position;
            foreach (var (joint, bone, offset) in _pairs)
                bone.rotation = joint.rotation * offset;
        }
    }
}
