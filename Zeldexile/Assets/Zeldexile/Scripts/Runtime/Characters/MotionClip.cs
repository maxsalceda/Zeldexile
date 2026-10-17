using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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

        /// <summary>How far the clip turns, in degrees (positive = to the right). Removed from the poses.</summary>
        public float TurnDegrees { get; private set; }

        /// <summary>Ground speed of the hips at each sample (m/s, hero size), from the clip's root motion.</summary>
        private float[] _hipSpeed;

        /// <summary>How far the hips rise from the first sample to the last (m, hero size): a climb loop's cycle height.</summary>
        public float VerticalTravel { get; private set; }

        /// <summary>Height of the lowest foot above its lowest point in the clip, at each sample (m, hero size).</summary>
        private float[] _feetHeight;

        /// <summary>Height of the hips at each sample (m, hero size, same origin as the feet).</summary>
        private float[] _hipsHeight;

        /// <summary>
        /// A jump clip's phases, in clip progress: when both feet leave the ground, the hips' highest point, and
        /// when a foot touches down again. Falls back to thirds when the feet never clearly leave the ground.
        /// </summary>
        public (float takeoff, float apex, float landing) AirPhases()
        {
            var n = _feetHeight.Length;
            int takeoff = -1, landing = -1;
            for (var i = 0; i < n; i++)
                if (_feetHeight[i] > 0.08f)
                {
                    if (takeoff < 0) takeoff = i;
                    landing = i;
                }

            if (takeoff < 0 || landing - takeoff < 2) return (0.25f, 0.45f, 0.65f);
            var apex = takeoff;
            for (var i = takeoff; i <= landing; i++)
                if (_hipsHeight[i] > _hipsHeight[apex]) apex = i;
            return ((float)takeoff / n, (float)apex / n, (float)(landing + 1) / n);
        }

        /// <summary>
        /// A start-to-walk clip: from its first frame to where the hips reach their steady pace (plus a step's
        /// settle), in clip progress.
        /// </summary>
        public (float start, float end) StartWindow()
        {
            var n = _hipSpeed.Length;
            var steady = Steady(n * 2 / 3, n);
            var end = n - 1;
            for (var i = 0; i < n; i++)
                if (_hipSpeed[i] >= steady * 0.85f) { end = i; break; }
            return (0f, Mathf.Min(1f, (float)end / n + 0.25f / Duration));
        }

        /// <summary>
        /// A walk-to-stop clip: from the last moment at full pace to where the hips have settled, in clip progress.
        /// </summary>
        public (float start, float end) StopWindow()
        {
            var n = _hipSpeed.Length;
            var steady = Steady(0, n / 3);
            var begin = 0;
            for (var i = 0; i < n; i++)
                if (_hipSpeed[i] >= steady * 0.85f) begin = i;
                else if (begin > 0 && i > begin + 3) break;
            var end = n - 1;
            for (var i = begin; i < n; i++)
                if (_hipSpeed[i] < steady * 0.1f) { end = i; break; }
            return (Mathf.Max(0f, (float)begin / n - 0.15f / Duration), Mathf.Min(1f, (float)end / n + 0.5f / Duration));
        }

        private float Steady(int from, int to)
        {
            var values = new List<float>();
            for (var i = Mathf.Max(0, from); i < Mathf.Min(_hipSpeed.Length, to); i++) values.Add(_hipSpeed[i]);
            if (values.Count == 0) return 0f;
            values.Sort();
            return values[values.Count / 2];
        }

        /// <summary>The same pose seen in a mirror: left and right swap, rotations reflect across the body's midplane.</summary>
        public static CharacterAnimator.Pose Mirror(CharacterAnimator.Pose p)
        {
            Quaternion M(Quaternion q) =>
                q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f ? q : new Quaternion(q.x, -q.y, -q.z, q.w);
            return new CharacterAnimator.Pose
            {
                Hips = M(p.Hips), Torso = M(p.Torso), Head = M(p.Head), Jaw = M(p.Jaw),
                ArmL = M(p.ArmR), ArmR = M(p.ArmL), ElbowL = M(p.ElbowR), ElbowR = M(p.ElbowL),
                WristL = M(p.WristR), WristR = M(p.WristL), FingersL = M(p.FingersR), FingersR = M(p.FingersL),
                LegL = M(p.LegR), LegR = M(p.LegL), KneeL = M(p.KneeR), KneeR = M(p.KneeL),
                AnkleL = M(p.AnkleR), AnkleR = M(p.AnkleL), ToesL = M(p.ToesR), ToesR = M(p.ToesL),
                FootFreeL = p.FootFreeR, FootFreeR = p.FootFreeL,
                HipsOffsetY = p.HipsOffsetY, HipsOffsetZ = p.HipsOffsetZ, ShowGlider = p.ShowGlider
            };
        }

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
            // AnimationClip.SampleAnimation only moves bones in the Editor; a built game needs the Playables API.
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var graph = PlayableGraph.Create("Amostra de clipe");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
            var bones = SkinnedHero.CanonicalBones(go.transform);
            string[] needed = { "Hips", "Spine", "Neck", "Head", "LeftArm", "LeftForeArm", "LeftHand", "RightArm",
                "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg",
                "RightFoot", "RightToeBase" };
            foreach (var n in needed)
                if (!bones.ContainsKey(n))
                {
                    graph.Destroy();
                    Object.Destroy(go);
                    return null;
                }

            var count = Mathf.Max(8, Mathf.RoundToInt(clip.length * SamplesPerSecond));
            var positions = new List<Dictionary<string, Vector3>>();
            for (var i = 0; i < count; i++)
            {
                playable.SetTime(clip.length * i / count);
                graph.Evaluate();
                var frame = new Dictionary<string, Vector3>();
                // In world units: a clip FBX without a mesh imports its scaled armature node as the root.
                foreach (var pair in bones)
                    frame[pair.Key] = Quaternion.Inverse(go.transform.rotation) * (pair.Value.position - go.transform.position);
                positions.Add(frame);
            }

            graph.Destroy();
            Object.Destroy(go);

            // The clip's frame: up from the hips to the neck, right from the left thigh to the right one, averaged.
            Vector3 up = Vector3.zero, right = Vector3.zero;
            foreach (var p in positions)
            {
                up += p["Neck"] - p["Hips"];
                right += p["RightUpLeg"] - p["LeftUpLeg"];
            }

            // The import already stands clips up (Y); a leaning gait (the crouched walk) would tilt the body-based
            // up and stand the trunk upright. Only a clip lying far off Y falls back to the body's own up.
            if (Vector3.Angle(up, Vector3.up) < 60f) up = Vector3.up;
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

            // Turning clips (and Mixamo's root motion) rotate the whole body over time; the game turns the hero
            // itself, so the trend of the heading is taken out, leaving the steps, the lean and the pelvis sway.
            var headings = new float[count];
            for (var i = 0; i < count; i++)
            {
                var r = positions[i]["RightUpLeg"] - positions[i]["LeftUpLeg"];
                var h = Mathf.Atan2(-r.z, r.x) * Mathf.Rad2Deg;
                headings[i] = i == 0 ? h : headings[i - 1] + Mathf.DeltaAngle(headings[i - 1], h);
            }

            float meanT = 0f, meanH = 0f;
            for (var i = 0; i < count; i++)
            {
                meanT += i;
                meanH += headings[i];
            }

            meanT /= count;
            meanH /= count;
            float cov = 0f, varT = 0f;
            for (var i = 0; i < count; i++)
            {
                cov += (i - meanT) * (headings[i] - meanH);
                varT += (i - meanT) * (i - meanT);
            }

            var slope = varT > 0f ? cov / varT : 0f;
            for (var i = 0; i < count; i++)
            {
                var trend = Quaternion.AngleAxis(-(meanH + slope * (i - meanT)), Vector3.up);
                var pivot = positions[i]["Hips"];
                foreach (var key in new List<string>(positions[i].Keys))
                    positions[i][key] = pivot + trend * (positions[i][key] - pivot);
            }

            var motion = new MotionClip { Duration = clip.length, _frames = new CharacterAnimator.Pose[count] };
            motion.TurnDegrees = slope * count;
            for (var i = 0; i < count; i++) motion._frames[i] = Rebuild(positions[i], bones.ContainsKey("HeadTop_End"));

            // Size: the standing height from the bone lengths (the same whatever the pose: a climb or a crouch never
            // stands up straight), legs plus trunk plus head; speed: how fast the planted foot slides back.
            var first = positions[0];
            float Len(string a, string b) => first.ContainsKey(a) && first.ContainsKey(b) ? (first[a] - first[b]).magnitude : 0f;
            // Bone lengths only (no constants: clip units vary): thigh, shin, half the foot, and the pelvis drop.
            var legs = Len("LeftUpLeg", "LeftLeg") + Len("LeftLeg", "LeftFoot") + Len("LeftFoot", "LeftToeBase") * 0.5f + Len("Hips", "LeftUpLeg") * 0.6f;
            var trunk = Len("Hips", "Spine") + (first.ContainsKey("Spine1") ? Len("Spine", "Spine1") + Len("Spine1", "Spine2") : 0f)
                        + Len(first.ContainsKey("Spine2") ? "Spine2" : "Spine", "Neck") + Len("Neck", "Head")
                        + (first.ContainsKey("HeadTop_End") ? Len("Head", "HeadTop_End") : Len("Neck", "Head"));
            var height = (legs + trunk) * 0.97f;
            var scale = SkinnedHero.TargetHeight / Mathf.Max(0.01f, height);
            var slides = new List<float>();
            var lowestLeft = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var a = positions[i];
                var b = positions[(i + 1) % count];
                var leftDown = a["LeftToeBase"].y < a["RightToeBase"].y;
                var foot = leftDown ? "LeftToeBase" : "RightToeBase";
                // Relative to the hips: clips with root motion move the hips over a planted foot.
                slides.Add(-((b[foot].z - b["Hips"].z) - (a[foot].z - a["Hips"].z)) * count / clip.length);
                // Left heel strike: the left foot furthest ahead of the hips.
                var ahead = a["LeftFoot"].z - a["Hips"].z;
                if (-ahead < lowestLeft)
                {
                    lowestLeft = -ahead;
                    motion.LeftStrike = (float)i / count;
                }
            }

            slides.Sort();
            motion.Speed = Mathf.Abs(slides[slides.Count / 2]) * scale;
            motion._hipSpeed = new float[count];
            for (var i = 1; i < count; i++)
            {
                var d = positions[i]["Hips"] - positions[i - 1]["Hips"];
                d.y = 0f;
                motion._hipSpeed[i] = d.magnitude * count / clip.length * scale;
            }

            motion._hipSpeed[0] = motion._hipSpeed[Mathf.Min(1, count - 1)];
            motion.VerticalTravel = (positions[count - 1]["Hips"].y - positions[0]["Hips"].y) * scale;
            motion._feetHeight = new float[count];
            motion._hipsHeight = new float[count];
            var lowestFoot = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var p = positions[i];
                motion._feetHeight[i] = Mathf.Min(Mathf.Min(p["LeftToeBase"].y, p["RightToeBase"].y), Mathf.Min(p["LeftFoot"].y, p["RightFoot"].y));
                lowestFoot = Mathf.Min(lowestFoot, motion._feetHeight[i]);
            }

            for (var i = 0; i < count; i++)
            {
                motion._feetHeight[i] = (motion._feetHeight[i] - lowestFoot) * scale;
                motion._hipsHeight[i] = (positions[i]["Hips"].y - lowestFoot) * scale;
            }
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
