using System.Collections.Generic;
using UnityEngine;

namespace Zeldexile.Characters
{
    /// <summary>
    /// Plays <see cref="DirectClip"/>s on the skinned hero, bone for bone, over whatever the procedural animator left
    /// on the bones. At full weight the bones are exactly the clip's; the weight only ramps for the short fade into and
    /// out of a clip, and two clips cross-fade the same way. Runs after <see cref="SkinnedRigDriver"/>, then moves the
    /// rig's joints onto the bones so the gear in the hands and the next procedural frame follow the clip's pose.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class DirectLayer : MonoBehaviour
    {
        public HeroSkeleton Skeleton;
        public SkinnedRigDriver Driver;

        private DirectClip _clip;
        private float _time;
        private bool _loop;
        private float _speed = 1f;
        private float _weight;
        private float _target;
        private float _fadeRate = 8f;

        // The clip being faded out when another one starts, kept running until the fade ends.
        private DirectClip _from;
        private float _fromTime;
        private bool _fromLoop;
        private float _fromWeight;
        private float _crossRate = 8f;

        private Quaternion[] _pose, _fromPose, _procedural;
        private readonly List<DirectClip> _owned = new List<DirectClip>();

        public DirectClip Current => _clip;
        /// <summary>Seconds played of the current clip.</summary>
        public float Time => _time;
        /// <summary>How much of the body the clips own now (0 = procedural, 1 = clip only).</summary>
        public float Weight => _weight;
        /// <summary>A one-shot clip still running (not counting its fade out).</summary>
        public bool Playing => _clip != null && _target > 0f && (_loop || _time < _clip.Duration);
        public int Serial { get; private set; }

        public DirectClip Load(string name)
        {
            var holder = transform.Find("Clipes");
            if (holder == null)
            {
                holder = new GameObject("Clipes").transform;
                holder.SetParent(transform, false);
            }

            var clip = DirectClip.Load(name, Skeleton, holder);
            if (clip != null) _owned.Add(clip);
            return clip;
        }

        /// <summary>
        /// Plays <paramref name="clip"/> from <paramref name="start"/> seconds, fading in over <paramref name="fade"/>
        /// seconds (from the procedural pose, or cross-fading from the clip already playing).
        /// </summary>
        public void Play(DirectClip clip, bool loop, float fade = 0.15f, float start = 0f, float speed = 1f)
        {
            if (clip == null) return;
            if (_clip != null && _weight > 0f && _clip != clip)
            {
                _from = _clip;
                _fromTime = _time;
                _fromLoop = _loop;
                _fromWeight = 1f;
                _crossRate = fade > 0f ? 1f / fade : 1000f;
            }

            _clip = clip;
            _time = start;
            _loop = loop;
            _speed = speed;
            _target = 1f;
            _fadeRate = fade > 0f ? 1f / fade : 1000f;
            Serial++;
        }

        /// <summary>Hands the body back to the procedural animator over <paramref name="fade"/> seconds.</summary>
        public void Stop(float fade = 0.15f)
        {
            _target = 0f;
            _fadeRate = fade > 0f ? 1f / fade : 1000f;
        }

        private void OnDestroy()
        {
            foreach (var clip in _owned) clip.Dispose();
        }

        private void LateUpdate()
        {
            if (Skeleton == null) return;
            var dt = UnityEngine.Time.deltaTime;
            if (_clip != null)
            {
                _time += dt * _speed;
                // A one-shot that reached its end holds its last frame while it fades out.
                if (!_loop && _time >= _clip.Duration && _target > 0f) _target = 0f;
            }

            _weight = Mathf.MoveTowards(_weight, _target, _fadeRate * dt);
            if (_weight <= 0f && _target <= 0f)
            {
                _clip = null;
                _from = null;
                return;
            }

            var bones = Skeleton.Bones;
            var n = bones.Count;
            if (_pose == null || _pose.Length != n)
            {
                _pose = new Quaternion[n];
                _fromPose = new Quaternion[n];
                _procedural = new Quaternion[n];
            }

            for (var i = 0; i < n; i++) _procedural[i] = bones[i].localRotation;
            var hipsBone = bones[Skeleton.Hips];
            var proceduralHips = hipsBone.localPosition;

            System.Array.Copy(_procedural, _pose, n);
            _clip.Sample(_time, _loop, _pose, out var hips);
            if (_from != null)
            {
                _fromTime += dt;
                _fromWeight = Mathf.MoveTowards(_fromWeight, 0f, _crossRate * dt);
                if (_fromWeight <= 0f) _from = null;
                else
                {
                    System.Array.Copy(_procedural, _fromPose, n);
                    _from.Sample(_fromTime, _fromLoop, _fromPose, out var fromHips);
                    for (var i = 0; i < n; i++) _pose[i] = Quaternion.Slerp(_pose[i], _fromPose[i], _fromWeight);
                    hips = Vector3.Lerp(hips, fromHips, _fromWeight);
                }
            }

            var w = Mathf.SmoothStep(0f, 1f, _weight);
            for (var i = 0; i < n; i++)
                bones[i].localRotation = w >= 1f ? _pose[i] : Quaternion.Slerp(_procedural[i], _pose[i], w);
            hipsBone.localPosition = w >= 1f ? hips : Vector3.Lerp(proceduralHips, hips, w);

            if (Driver != null) Driver.FollowBones();
        }
    }
}
