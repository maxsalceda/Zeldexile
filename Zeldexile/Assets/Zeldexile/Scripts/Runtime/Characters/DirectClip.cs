using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Zeldexile.Characters
{
    /// <summary>
    /// The hero's skinned skeleton as the clips see it: every bone by its Mixamo name (no "mixamorig:" prefix), its
    /// parent, and its rest pose from &lt;model&gt;_repouso.json (written by Tools/Blender/export_rest.py).
    /// </summary>
    public sealed class HeroSkeleton
    {
        public Transform Root;
        public readonly List<string> Names = new List<string>();
        public readonly List<Transform> Bones = new List<Transform>();
        public readonly List<int> Parents = new List<int>();
        public RestPose Rest;
        public int Hips = -1;
        /// <summary>The hips' rest height in the model root's space (Unity units).</summary>
        public float HipsHeight;

        public int IndexOf(string name) => Names.IndexOf(name);

        /// <summary>The bones of <paramref name="model"/> (an instance of the hero FBX) in parent-first order.</summary>
        public static HeroSkeleton Read(Transform model, string restAsset)
        {
            var rest = RestPose.Load(restAsset);
            if (rest == null) return null;
            var skeleton = new HeroSkeleton { Root = model, Rest = rest };
            var byName = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                var bare = RestPose.Bare(t.name);
                if (rest.Has(bare) && !byName.ContainsKey(bare)) byName[bare] = t;
            }

            foreach (var name in rest.Order)
            {
                if (!byName.TryGetValue(name, out var t)) continue;
                skeleton.Names.Add(name);
                skeleton.Bones.Add(t);
                var parent = rest.Parent(name);
                skeleton.Parents.Add(string.IsNullOrEmpty(parent) ? -1 : skeleton.Names.IndexOf(parent));
            }

            skeleton.Hips = skeleton.IndexOf("Hips");
            if (skeleton.Hips < 0) return null;
            skeleton.HipsHeight = skeleton.Bones[skeleton.Hips].localPosition.y;
            return skeleton;
        }
    }

    /// <summary>A skeleton's rest (bind) pose: each bone's rotation and offset relative to its parent, in Unity's convention.</summary>
    public sealed class RestPose
    {
        [System.Serializable]
        private sealed class BoneData
        {
            public string name;
            public string parent;
            public float[] rot;
            public float[] pos;
        }

        [System.Serializable]
        private sealed class FileData
        {
            public BoneData[] bones;
        }

        public readonly List<string> Order = new List<string>();
        private readonly Dictionary<string, (string parent, Quaternion rot, Vector3 pos)> _bones =
            new Dictionary<string, (string, Quaternion, Vector3)>();

        public bool Has(string name) => _bones.ContainsKey(name);
        public string Parent(string name) => _bones[name].parent;
        public Quaternion Rotation(string name) => _bones[name].rot;
        public Vector3 Offset(string name) => _bones[name].pos;

        public static string Bare(string name)
        {
            var colon = name.LastIndexOf(':');
            return colon < 0 ? name : name.Substring(colon + 1);
        }

        /// <summary>Resources path of the JSON without extension, e.g. "Models/Skinned/Animacoes/heroi_correr_rapido_repouso".</summary>
        public static RestPose Load(string resource)
        {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null) return null;
            var data = JsonUtility.FromJson<FileData>(asset.text);
            if (data?.bones == null || data.bones.Length == 0) return null;
            var pose = new RestPose();
            foreach (var b in data.bones)
            {
                pose.Order.Add(b.name);
                pose._bones[b.name] = (b.parent, new Quaternion(b.rot[0], b.rot[1], b.rot[2], b.rot[3]).normalized,
                    new Vector3(b.pos[0], b.pos[1], b.pos[2]));
            }

            return pose;
        }

        /// <summary>Rest rotation of every bone in the space of the skeleton's root (the hips' parent).</summary>
        public Dictionary<string, Quaternion> Absolute()
        {
            var result = new Dictionary<string, Quaternion>();
            foreach (var name in Order)
            {
                var parent = Parent(name);
                var up = !string.IsNullOrEmpty(parent) && result.TryGetValue(parent, out var p) ? p : Quaternion.identity;
                result[name] = up * Rotation(name);
            }

            return result;
        }

        /// <summary>
        /// Where a bone points at rest, in its own space: toward its child along the limb (the neck for the chest, the
        /// knuckles between the index and the ring finger for the hand). Null for the hips and the end bones.
        /// </summary>
        public Vector3? Aim(string name)
        {
            if (name == "Hips") return null;
            if (name == "Spine2" && Has("Neck")) return Offset("Neck");
            if (name.EndsWith("Hand") && !name.Contains("Hand" + "Thumb"))
            {
                string index = name + "Index1", ring = name + "Ring1";
                if (Has(index) && Has(ring)) return (Offset(index) + Offset(ring)) * 0.5f;
            }

            string only = null;
            foreach (var other in Order)
            {
                if (Parent(other) != name) continue;
                if (only != null) return null;
                only = other;
            }

            return only == null ? (Vector3?)null : Offset(only);
        }
    }

    /// <summary>
    /// A captured clip played bone for bone on the hero's own skeleton, the way it was animated: every joint the clip
    /// moves (spine segments, shoulders, twist, fingers) points where the clip's points.
    /// <para>
    /// The clip runs live on a hidden copy of its own FBX (Playables; the built game cannot use SampleAnimation) and
    /// each bone's local rotation is carried over through a fixed correction between the two rest poses: both
    /// skeletons are first brought to the clip's T-pose (each hero bone swung, twist kept, until it points where the
    /// clip's bone points at rest), so a clip made on a skeleton of other proportions or another rest pose (A or T)
    /// still lays every bone along the same direction, with the same twist. The hips move as the clip's, scaled by
    /// the ratio of the two hip heights.
    /// </para>
    /// <para>
    /// The one thing that is not the clip's own motion: hero bones the clip's skeleton lacks (the Mixamo 49-bone rig
    /// has no middle or little finger) follow their neighbours (middle: halfway between index and ring; little: ring).
    /// </para>
    /// </summary>
    public sealed class DirectClip
    {
        /// <summary>Folder under Resources/Models/Skinned with the animation FBXs.</summary>
        public const string Folder = "Animacoes";

        public string Name { get; private set; }
        public float Duration { get; private set; }
        /// <summary>Seconds into the clip where the right hand moves fastest (the strikes of an attack), 0.3 s apart at least.</summary>
        public readonly List<float> Strikes = new List<float>();
        /// <summary>The right hand's path in the hero's model space, sampled every <see cref="TrackStep"/> seconds.</summary>
        public Vector3[] RightHandTrack { get; private set; }
        public const float TrackStep = 1f / 60f;

        private HeroSkeleton _hero;
        private GameObject _source;
        private PlayableGraph _graph;
        private AnimationClipPlayable _playable;
        private float _scale;
        private Vector3 _drift;

        /// <summary>Per hero bone: how its rotation comes from the clip.</summary>
        private struct Link
        {
            /// <summary>Clip bone that drives it (or the first proxy), and a second proxy for a blended finger.</summary>
            public Transform Source, Source2;
            public Quaternion Pre, Post;
            /// <summary>Hero bone the clip lacks: rest rotation times the proxies' change from their rest.</summary>
            public bool Proxy;
            public Quaternion ProxyRest, ProxyRest2;
        }

        private Link[] _links;

        /// <summary>The clip "Resources/Models/Skinned/Animacoes/&lt;name&gt;" bound to the hero; its hidden copy lives under <paramref name="holder"/>.</summary>
        public static DirectClip Load(string name, HeroSkeleton hero, Transform holder)
        {
            if (string.IsNullOrEmpty(name) || hero == null) return null;
            var path = "Models/Skinned/" + Folder + "/" + name;
            var prefab = Resources.Load<GameObject>(path);
            AnimationClip clip = null;
            foreach (var c in Resources.LoadAll<AnimationClip>(path))
                if (!c.name.StartsWith("__preview__") && (clip == null || c.length > clip.length)) clip = c;
            var rest = RestPose.Load(path + "_repouso");
            if (prefab == null || clip == null || rest == null) return null;
            var direct = new DirectClip { Name = name, Duration = clip.length, _hero = hero };
            if (!direct.Bind(prefab, clip, rest, holder))
            {
                direct.Dispose();
                return null;
            }

            direct.Analyse();
            return direct;
        }

        private bool Bind(GameObject prefab, AnimationClip clip, RestPose rest, Transform holder)
        {
            _source = Object.Instantiate(prefab, holder, false);
            _source.name = "Clipe " + Name;
            foreach (var r in _source.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var animator = _source.GetComponent<Animator>();
            if (animator == null) animator = _source.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create("Clipe " + Name);
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _playable = AnimationClipPlayable.Create(_graph, clip);
            _playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(_graph, "Pose", animator).SetSourcePlayable(_playable);

            var sourceBones = new Dictionary<string, Transform>();
            foreach (var t in _source.GetComponentsInChildren<Transform>(true))
            {
                var bare = RestPose.Bare(t.name);
                if (rest.Has(bare) && !sourceBones.ContainsKey(bare)) sourceBones[bare] = t;
            }

            if (!sourceBones.ContainsKey("Hips")) return false;

            // Both skeletons in the clip's T-pose, in their roots' space: the clip's is its rest; the hero's is its own
            // rest with each bone swung, parents first, to point where the clip's bone points.
            var heroRest = _hero.Rest;
            var clipT = rest.Absolute();
            var heroT = new Dictionary<string, Quaternion>();
            foreach (var name in heroRest.Order)
            {
                var parent = heroRest.Parent(name);
                var up = !string.IsNullOrEmpty(parent) && heroT.TryGetValue(parent, out var p) ? p : Quaternion.identity;
                var r = up * heroRest.Rotation(name);
                if (rest.Has(name))
                {
                    var heroAim = heroRest.Aim(name);
                    var clipAim = rest.Aim(name);
                    if (heroAim.HasValue && clipAim.HasValue && heroAim.Value.sqrMagnitude > 1e-12f && clipAim.Value.sqrMagnitude > 1e-12f)
                        r = Quaternion.FromToRotation(r * heroAim.Value, clipT[name] * clipAim.Value) * r;
                }

                heroT[name] = r;
            }

            Quaternion Correction(string name) =>
                string.IsNullOrEmpty(name) || !clipT.ContainsKey(name) || !heroT.ContainsKey(name)
                    ? Quaternion.identity
                    : Quaternion.Inverse(clipT[name]) * heroT[name];

            _links = new Link[_hero.Names.Count];
            for (var i = 0; i < _hero.Names.Count; i++)
            {
                var name = _hero.Names[i];
                var parent = heroRest.Parent(name);
                if (sourceBones.TryGetValue(name, out var src))
                {
                    _links[i] = new Link { Source = src, Pre = Quaternion.Inverse(Correction(parent)), Post = Correction(name) };
                    continue;
                }

                // A finger the clip's hand does not have: rest pose (in the T-pose) times its neighbours' bend.
                var (a, b) = Proxies(name);
                if (a == null || !sourceBones.TryGetValue(a, out var pa)) continue;
                sourceBones.TryGetValue(b ?? "", out var pb);
                var parentT = !string.IsNullOrEmpty(parent) && heroT.ContainsKey(parent) ? heroT[parent] : Quaternion.identity;
                _links[i] = new Link
                {
                    Proxy = true,
                    Source = pa,
                    Source2 = pb,
                    Pre = Quaternion.Inverse(parentT) * heroT[name],
                    ProxyRest = Quaternion.Inverse(rest.Rotation(a)),
                    ProxyRest2 = pb != null ? Quaternion.Inverse(rest.Rotation(b)) : Quaternion.identity
                };
            }

            // Hips: the clip's height over the hero's, from their rest heights in each FBX's own units.
            var unit = UnitScale(rest, sourceBones);
            var clipHips = rest.Offset("Hips").y * unit;
            _scale = clipHips > 1e-9f ? _hero.HipsHeight / clipHips : 1f;
            return true;
        }

        /// <summary>FBX units per rest-pose unit of the clip's file, from a bone offset the clip never changes.</summary>
        private static float UnitScale(RestPose rest, Dictionary<string, Transform> bones)
        {
            foreach (var name in new[] { "Spine1", "Spine2", "Spine", "LeftUpLeg" })
            {
                if (!bones.TryGetValue(name, out var t) || !rest.Has(name)) continue;
                var r = rest.Offset(name).magnitude;
                if (r > 1e-9f) return t.localPosition.magnitude / r;
            }

            return 1f;
        }

        private static (string, string) Proxies(string name)
        {
            foreach (var side in new[] { "Left", "Right" })
            for (var k = 1; k <= 4; k++)
            {
                if (name == side + "HandMiddle" + k) return (side + "HandIndex" + k, side + "HandRing" + k);
                if (name == side + "HandPinky" + k) return (side + "HandRing" + k, null);
            }

            return (null, null);
        }

        public void Dispose()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_source != null) Object.Destroy(_source);
        }

        /// <summary>Poses the hidden clip at <paramref name="time"/> seconds (clamped, or wrapped when looping).</summary>
        private void Evaluate(float time, bool loop)
        {
            var t = loop ? Mathf.Repeat(time, Duration) : Mathf.Clamp(time, 0f, Duration);
            _playable.SetTime(t);
            _graph.Evaluate();
        }

        /// <summary>
        /// The hero's pose at <paramref name="time"/>: each bone's local rotation (indexed like <see cref="HeroSkeleton.Bones"/>;
        /// left unchanged for a bone the clip cannot drive) and the hips' local position. A looping clip has its
        /// overall forward drift removed so it plays in place; everything else is the clip's.
        /// </summary>
        public void Sample(float time, bool loop, Quaternion[] rotations, out Vector3 hips)
        {
            Evaluate(time, loop);
            for (var i = 0; i < _links.Length; i++)
            {
                var link = _links[i];
                if (link.Source == null) continue;
                if (!link.Proxy)
                {
                    rotations[i] = link.Pre * link.Source.localRotation * link.Post;
                    continue;
                }

                var bend = link.ProxyRest * link.Source.localRotation;
                if (link.Source2 != null) bend = Quaternion.Slerp(bend, link.ProxyRest2 * link.Source2.localRotation, 0.5f);
                rotations[i] = link.Pre * bend;
            }

            var hipsSource = _links[_hero.Hips].Source;
            var position = hipsSource.localPosition;
            if (loop) position -= _drift * (Mathf.Repeat(time, Duration) / Duration);
            hips = position * _scale;
        }

        /// <summary>Strike times and the right hand's path, from the pose on the hero's own skeleton.</summary>
        private void Analyse()
        {
            Evaluate(0f, false);
            var start = _links[_hero.Hips].Source.localPosition;
            Evaluate(Duration, false);
            var end = _links[_hero.Hips].Source.localPosition;
            _drift = new Vector3(end.x - start.x, 0f, end.z - start.z);

            var hand = _hero.IndexOf("RightHand");
            if (hand < 0) return;
            var n = Mathf.Max(2, Mathf.CeilToInt(Duration / TrackStep) + 1);
            var track = new Vector3[n];
            var rotations = new Quaternion[_hero.Names.Count];
            for (var i = 0; i < rotations.Length; i++) rotations[i] = _hero.Bones[i].localRotation;
            for (var f = 0; f < n; f++)
            {
                Sample(Mathf.Min(Duration, f * TrackStep), false, rotations, out var hips);
                track[f] = ModelSpace(hand, rotations, hips);
            }

            RightHandTrack = track;
            FindStrikes(track);
        }

        /// <summary>A bone's position in the hero model's space for a set of local rotations (rest offsets).</summary>
        private Vector3 ModelSpace(int bone, Quaternion[] rotations, Vector3 hips) => BoneInModel(bone, rotations, hips).GetColumn(3);

        /// <summary>
        /// A bone's pose in the hero model's space (the hips' parent) for a set of local rotations and hips position,
        /// with every other bone at its rest offset from its parent.
        /// </summary>
        public Matrix4x4 BoneInModel(int bone, Quaternion[] rotations, Vector3 hips)
        {
            var chain = new List<int>();
            for (var i = bone; i >= 0; i = _hero.Parents[i]) chain.Insert(0, i);
            var m = Matrix4x4.TRS(hips, rotations[chain[0]], Vector3.one);
            for (var k = 1; k < chain.Count; k++)
                m *= Matrix4x4.TRS(_hero.Bones[chain[k]].localPosition, rotations[chain[k]], _hero.Bones[chain[k]].localScale);
            return m;
        }

        private void FindStrikes(Vector3[] track)
        {
            var n = track.Length;
            if (n < 6) return;
            var speed = new float[n];
            for (var i = 1; i < n; i++) speed[i] = (track[i] - track[i - 1]).magnitude / TrackStep;
            var smooth = new float[n];
            for (var i = 0; i < n; i++)
            {
                float sum = 0f;
                var count = 0;
                for (var k = Mathf.Max(0, i - 2); k <= Mathf.Min(n - 1, i + 2); k++)
                {
                    sum += speed[k];
                    count++;
                }

                smooth[i] = sum / count;
            }

            var max = 0f;
            foreach (var v in smooth) max = Mathf.Max(max, v);
            if (max <= 0f) return;
            var gap = Mathf.CeilToInt(0.3f / TrackStep);
            var peaks = new List<int>();
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

            foreach (var p in peaks) Strikes.Add(p * TrackStep);
        }

        /// <summary>
        /// The moment the right hand is closest to <paramref name="point"/> (model space) within a part of the clip
        /// (0..1): where a draw grips the hilt in its sheath, or a sheathe lets go of it.
        /// </summary>
        public float ClosestApproach(Vector3 point, float from = 0f, float to = 1f)
        {
            if (RightHandTrack == null) return Duration * 0.5f;
            var best = float.MaxValue;
            var at = 0;
            var a = Mathf.FloorToInt(from * (RightHandTrack.Length - 1));
            var b = Mathf.CeilToInt(to * (RightHandTrack.Length - 1));
            for (var i = a; i <= b; i++)
            {
                var d = (RightHandTrack[i] - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    at = i;
                }
            }

            return Mathf.Min(Duration, at * TrackStep);
        }

        /// <summary>Hero's right hand in model space at a time of the clip.</summary>
        public Vector3 RightHandAt(float time)
        {
            if (RightHandTrack == null || RightHandTrack.Length == 0) return Vector3.zero;
            var f = Mathf.Clamp(time / TrackStep, 0f, RightHandTrack.Length - 1);
            var i = Mathf.FloorToInt(f);
            var j = Mathf.Min(RightHandTrack.Length - 1, i + 1);
            return Vector3.Lerp(RightHandTrack[i], RightHandTrack[j], f - i);
        }

        /// <summary>Clip bone world rotation relative to the clip root after posing at a time (tests compare it with the hero's).</summary>
        public Quaternion SourceRotation(string bone, float time)
        {
            Evaluate(time, false);
            foreach (var t in _source.GetComponentsInChildren<Transform>(true))
                if (RestPose.Bare(t.name) == bone)
                    return Quaternion.Inverse(_source.transform.rotation) * t.rotation;
            return Quaternion.identity;
        }

        /// <summary>Clip bone position relative to the clip root after posing at a time.</summary>
        public Vector3 SourcePosition(string bone, float time)
        {
            Evaluate(time, false);
            foreach (var t in _source.GetComponentsInChildren<Transform>(true))
                if (RestPose.Bare(t.name) == bone)
                    return Quaternion.Inverse(_source.transform.rotation) * (t.position - _source.transform.position);
            return Vector3.zero;
        }

        /// <summary>The correction between the clip's bone and the hero's (hero = clip * correction), for tests.</summary>
        public Quaternion Correction(int heroBone) => _links[heroBone].Proxy ? Quaternion.identity : _links[heroBone].Post;

        public bool Drives(int heroBone) => _links[heroBone].Source != null && !_links[heroBone].Proxy;

        public float HipsScale => _scale;
    }
}
