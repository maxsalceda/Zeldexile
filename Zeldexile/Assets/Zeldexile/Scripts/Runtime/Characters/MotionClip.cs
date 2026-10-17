using System.Collections.Generic;
using UnityEngine;

namespace Zeldexile.Characters
{
    /// <summary>
    /// A captured loop (the Meshy walk) turned into <see cref="CharacterAnimator.Pose"/> frames, so the animator
    /// can play it like any procedural pose and still layer stances and actions over it.
    /// <para>
    /// The clip is sampled once on a hidden copy of its model. Each frame is read as bone positions only and
    /// rebuilt on the rig's identity-rest joints: the hips, spine, neck and head from their up and right axes,
    /// every limb segment by swinging its rest direction onto the clip's. The clip's own bone axes (and its broken
    /// Meshy bind pose) never matter.
    /// </para>
    /// </summary>
    public sealed class MotionClip
    {
        /// <summary>Folder under Resources/Models/Skinned with the animation FBXs.</summary>
        public const string Folder = "Animacoes";

        private const float SamplesPerSecond = 30f;

        private static readonly Dictionary<string, MotionClip> Cache = new Dictionary<string, MotionClip>();

        private CharacterAnimator.Pose[] _frames;

        /// <summary>Length of one loop, in seconds.</summary>
        public float Duration { get; private set; }

        /// <summary>Ground speed of the loop for a hero <see cref="SkinnedHero.TargetHeight"/> tall, in m/s.</summary>
        public float Speed { get; private set; }

        /// <summary>Distance covered by one loop (two steps), in meters.</summary>
        public float CycleDistance => Speed * Duration;

        /// <summary>Loop progress (0..1) where the left heel strikes: phase 0 of the animator's own gait.</summary>
        public float LeftStrike { get; private set; }

        /// <summary>
        /// The swings of an attack clip, in clip progress (0..1): each runs from the wind-up before a peak of the
        /// right hand's speed to the follow-through after it. Empty for a clip with no fast hand motion.
        /// </summary>
        public readonly List<(float start, float peak, float end)> Strikes = new List<(float, float, float)>();

        /// <summary>The clip "Resources/Models/Skinned/Animacoes/&lt;name&gt;", sampled once; null when missing.</summary>
        public static MotionClip Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;
            MotionClip result = null;
            var path = "Models/Skinned/" + Folder + "/" + name;
            var prefab = Resources.Load<GameObject>(path);
            AnimationClip clip = null;
            foreach (var c in Resources.LoadAll<AnimationClip>(path))
                if (!c.name.StartsWith("__preview__") && (clip == null || c.length > clip.length)) clip = c;
            if (prefab != null && clip != null && clip.length > 0.1f) result = Sample(prefab, clip);
            Cache[name] = result;
            return result;
        }

        /// <summary>The pose at a point of the loop (0..1, wraps), blended between the two nearest frames.</summary>
        public CharacterAnimator.Pose Sample(float progress)
        {
            progress -= Mathf.Floor(progress);
            var f = progress * _frames.Length;
            var i = Mathf.FloorToInt(f) % _frames.Length;
            return CharacterAnimator.Mix(_frames[i], _frames[(i + 1) % _frames.Length], f - Mathf.Floor(f));
        }

        private static MotionClip Sample(GameObject prefab, AnimationClip clip)
        {
            var go = Object.Instantiate(prefab);
            go.name = "Amostra de " + clip.name;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            var bones = SkinnedHero.CanonicalBones(go.transform);
            string[] needed = { "Hips", "Spine", "Neck", "Head", "LeftArm", "LeftForeArm", "LeftHand", "RightArm",
                "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg",
                "RightFoot", "RightToeBase" };
            foreach (var n in needed)
                if (!bones.ContainsKey(n))
                {
                    Object.Destroy(go);
                    return null;
                }

            var count = Mathf.Max(8, Mathf.RoundToInt(clip.length * SamplesPerSecond));
            var positions = new List<Dictionary<string, Vector3>>();
            for (var i = 0; i < count; i++)
            {
                clip.SampleAnimation(go, clip.length * i / count);
                var frame = new Dictionary<string, Vector3>();
                // In world units: a clip FBX without a mesh imports its scaled armature node as the root.
                foreach (var pair in bones)
                    frame[pair.Key] = Quaternion.Inverse(go.transform.rotation) * (pair.Value.position - go.transform.position);
                positions.Add(frame);
            }

            Object.Destroy(go);

            // The clip's frame: up from the hips to the neck, right from the left thigh to the right one, averaged.
            Vector3 up = Vector3.zero, right = Vector3.zero;
            foreach (var p in positions)
            {
                up += p["Neck"] - p["Hips"];
                right += p["RightUpLeg"] - p["LeftUpLeg"];
            }

            var facing = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            var toRig = Quaternion.Inverse(facing);
            foreach (var p in positions)
                foreach (var key in new List<string>(p.Keys))
                    p[key] = toRig * p[key];

            // A clip FBX without a mesh can come in mirrored (negative scale on its root): its toes then point
            // back. Reflecting front to back restores the handedness while left stays left.
            var toes = 0f;
            foreach (var p in positions) toes += p["LeftToeBase"].z - p["LeftFoot"].z + p["RightToeBase"].z - p["RightFoot"].z;
            if (toes < 0f)
                foreach (var p in positions)
                    foreach (var key in new List<string>(p.Keys))
                        p[key] = Vector3.Scale(p[key], new Vector3(1f, 1f, -1f));

            var motion = new MotionClip { Duration = clip.length, _frames = new CharacterAnimator.Pose[count] };
            for (var i = 0; i < count; i++) motion._frames[i] = Rebuild(positions[i], bones.ContainsKey("HeadTop_End"));

            // Size: the standing height of the first frame; speed: how fast the planted foot slides back.
            var first = positions[0];
            var top = first.ContainsKey("HeadTop_End") ? first["HeadTop_End"].y : first["Head"].y;
            var height = (top - Mathf.Min(first["LeftToeBase"].y, first["RightToeBase"].y)) * 1.05f;
            var scale = SkinnedHero.TargetHeight / Mathf.Max(0.01f, height);
            var slides = new List<float>();
            var lowestLeft = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var a = positions[i];
                var b = positions[(i + 1) % count];
                var leftDown = a["LeftToeBase"].y < a["RightToeBase"].y;
                var foot = leftDown ? "LeftToeBase" : "RightToeBase";
                slides.Add(-(b[foot].z - a[foot].z) * count / clip.length);
                // Left heel strike: the left foot furthest ahead.
                if (-a["LeftFoot"].z < lowestLeft)
                {
                    lowestLeft = -a["LeftFoot"].z;
                    motion.LeftStrike = (float)i / count;
                }
            }

            slides.Sort();
            motion.Speed = Mathf.Abs(slides[slides.Count / 2]) * scale;
            FindStrikes(motion, positions, clip.length);
            return motion;
        }

        /// <summary>
        /// Peaks of the right hand's speed (smoothed) above half the fastest one, at least 0.3 s apart. Each swing
        /// starts 0.4 s before its peak (the wind-up) and ends 0.35 s after it, never past the middle of the gap
        /// to its neighbours.
        /// </summary>
        private static void FindStrikes(MotionClip motion, List<Dictionary<string, Vector3>> positions, float length)
        {
            var n = positions.Count;
            if (!positions[0].ContainsKey("RightHand") || n < 6) return;
            var dt = length / n;
            var speed = new float[n];
            for (var i = 1; i < n; i++) speed[i] = (positions[i]["RightHand"] - positions[i - 1]["RightHand"]).magnitude / dt;
            var smooth = new float[n];
            for (var i = 0; i < n; i++)
                smooth[i] = (speed[Mathf.Max(0, i - 1)] + speed[i] + speed[Mathf.Min(n - 1, i + 1)]) / 3f;
            var max = 0f;
            foreach (var v in smooth) max = Mathf.Max(max, v);
            if (max <= 0f) return;

            var peaks = new List<int>();
            var gap = Mathf.CeilToInt(0.3f / dt);
            for (var i = 1; i < n - 1; i++)
            {
                if (smooth[i] < max * 0.5f || smooth[i] < smooth[i - 1] || smooth[i] < smooth[i + 1]) continue;
                if (peaks.Count > 0 && i - peaks[peaks.Count - 1] < gap)
                {
                    if (smooth[i] > smooth[peaks[peaks.Count - 1]]) peaks[peaks.Count - 1] = i;
                    continue;
                }

                peaks.Add(i);
            }

            for (var k = 0; k < peaks.Count; k++)
            {
                var t = (float)peaks[k] / n;
                var before = k > 0 ? (peaks[k - 1] + peaks[k]) * 0.5f / n : 0f;
                var after = k + 1 < peaks.Count ? (peaks[k] + peaks[k + 1]) * 0.5f / n : 1f;
                motion.Strikes.Add((Mathf.Max(before, t - 0.4f / length), t, Mathf.Min(after, t + 0.35f / length)));
            }
        }

        private static Vector3 Rest(Vector3 dir) => dir.normalized;

        /// <summary>
        /// One frame on the rig: absolute orientations of the trunk pieces, then each limb segment swung from its
        /// parent so that it points where the clip's does. Output rotations follow the Pose conventions.
        /// </summary>
        private static CharacterAnimator.Pose Rebuild(Dictionary<string, Vector3> p, bool hasHeadTop)
        {
            Quaternion Frame(Vector3 up, Vector3 right) => Quaternion.LookRotation(Vector3.Cross(right, up), up);
            Quaternion Swing(Quaternion parent, Vector3 rest, Vector3 to) =>
                Quaternion.FromToRotation(parent * Rest(rest), to) * parent;

            var hipsRight = p["RightUpLeg"] - p["LeftUpLeg"];
            var chestRight = p["RightArm"] - p["LeftArm"];
            var chestBone = p.ContainsKey("Spine2") ? "Spine2" : "Spine";
            var hips = Frame(p["Spine"] - p["Hips"], hipsRight);
            var chest = Frame(p["Neck"] - p[chestBone], chestRight);
            var headUp = (hasHeadTop ? p["HeadTop_End"] : p["Head"] + (p["Head"] - p["Neck"])) - p["Head"];
            var head = Frame(headUp, chestRight);

            var armL = Swing(chest, new Vector3(-0.12f, -1f, 0f), p["LeftForeArm"] - p["LeftArm"]);
            var elbowL = Swing(armL, new Vector3(-0.08f, -1f, 0.08f), p["LeftHand"] - p["LeftForeArm"]);
            var armR = Swing(chest, new Vector3(0.12f, -1f, 0f), p["RightForeArm"] - p["RightArm"]);
            var elbowR = Swing(armR, new Vector3(0.08f, -1f, 0.08f), p["RightHand"] - p["RightForeArm"]);

            var legL = Swing(hips, Vector3.down, p["LeftLeg"] - p["LeftUpLeg"]);
            var kneeL = Swing(legL, Vector3.down, p["LeftFoot"] - p["LeftLeg"]);
            var ankleL = Swing(kneeL, new Vector3(0f, -0.45f, 1f), p["LeftToeBase"] - p["LeftFoot"]);
            var legR = Swing(hips, Vector3.down, p["RightLeg"] - p["RightUpLeg"]);
            var kneeR = Swing(legR, Vector3.down, p["RightFoot"] - p["RightLeg"]);
            var ankleR = Swing(kneeR, new Vector3(0f, -0.45f, 1f), p["RightToeBase"] - p["RightFoot"]);
            var toesL = p.ContainsKey("LeftToe_End") ? Swing(ankleL, Vector3.forward, p["LeftToe_End"] - p["LeftToeBase"]) : ankleL;
            var toesR = p.ContainsKey("RightToe_End") ? Swing(ankleR, Vector3.forward, p["RightToe_End"] - p["RightToeBase"]) : ankleR;

            // Hands: aimed down the middle finger when the clip has fingers; the curl is the bend from the back of
            // the hand to the fingertips, shared over the three phalanges as the rig's finger joint expects.
            var wristL = p.ContainsKey("LeftHandMiddle1") ? Swing(elbowL, new Vector3(-0.08f, -1f, 0.08f), p["LeftHandMiddle1"] - p["LeftHand"]) : elbowL;
            var wristR = p.ContainsKey("RightHandMiddle1") ? Swing(elbowR, new Vector3(0.08f, -1f, 0.08f), p["RightHandMiddle1"] - p["RightHand"]) : elbowR;
            float Curl(string side)
            {
                float total = 0f;
                var count = 0;
                foreach (var f in new[] { "Index", "Middle", "Ring" })
                {
                    string a = side + "Hand" + f + "1", b = side + "Hand" + f + "3", c = side + "Hand" + f + "4";
                    if (!p.ContainsKey(a) || !p.ContainsKey(b)) continue;
                    var tip = p.ContainsKey(c) ? p[c] - p[b] : p[b] - p[a];
                    total += Vector3.Angle(p[a] - p[side + "Hand"], tip);
                    count++;
                }

                return count == 0 ? 18f : total / count / 2.7f;
            }

            Quaternion Local(Quaternion parent, Quaternion child) => Quaternion.Inverse(parent) * child;
            return new CharacterAnimator.Pose
            {
                Hips = hips,
                Torso = Local(hips, chest),
                Head = Local(chest, head),
                ArmL = Local(chest, armL),
                ArmR = Local(chest, armR),
                ElbowL = Local(armL, elbowL),
                ElbowR = Local(armR, elbowR),
                WristL = Local(elbowL, wristL),
                WristR = Local(elbowR, wristR),
                FingersL = Quaternion.Euler(-Curl("Left"), 0f, 0f),
                FingersR = Quaternion.Euler(-Curl("Right"), 0f, 0f),
                LegL = Local(hips, legL),
                LegR = Local(hips, legR),
                KneeL = Local(legL, kneeL),
                KneeR = Local(legR, kneeR),
                AnkleL = Local(kneeL, ankleL),
                AnkleR = Local(kneeR, ankleR),
                ToesL = Local(ankleL, toesL),
                ToesR = Local(ankleR, toesR),
                // The clip's feet already roll and plant themselves.
                FootFreeL = 1f,
                FootFreeR = 1f
            };
        }
    }
}
