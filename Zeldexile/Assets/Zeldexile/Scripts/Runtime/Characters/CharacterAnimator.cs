using UnityEngine;

namespace Zeldexile.Characters
{
    public enum Locomotion
    {
        Grounded,
        Airborne,
        Climbing,
        Gliding,
        Swimming
    }

    /// <summary>One-shot actions layered over the locomotion pose. Each reads differently at a glance.</summary>
    public enum ActionKind
    {
        None,
        /// <summary>One-handed swing; alternates sideways and overhead along the combo.</summary>
        SlashLight,
        /// <summary>Two-handed overhead chop with the whole body.</summary>
        SlashHeavy,
        /// <summary>Spear thrust from the hip.</summary>
        Thrust,
        Throw,
        Shoot,
        Cast,
        Hurt,
        Pickup,
        Parry,
        Land,
        ClimbJump,
        SwimDash,
        /// <summary>Combo finisher with a one-handed sword: the captured sword kick.</summary>
        Kick,
        /// <summary>Pulling up over a ledge at the top of a climb.</summary>
        Vault
    }

    /// <summary>What the animator needs to know about a moving body.</summary>
    public interface IAnimationSource
    {
        Locomotion Locomotion { get; }
        /// <summary>World-space velocity.</summary>
        Vector3 Velocity { get; }
        float MaxGroundSpeed { get; }
    }

    /// <summary>
    /// Procedural poses for the skeleton of <see cref="CharacterRig"/>: walk/run cycle, jump, climb, glide, swim,
    /// held stances (shield up, bow drawn, kneeling) and one-shot actions (each weapon class swings its own
    /// way, throw, shoot, cast, flinch, pick up, parry, hard landing, climb jump, swim dash).
    /// <para>
    /// Bone convention: +X swings a hanging limb backward, -X forward; +Y turns right; +Z tilts.
    /// Elbows bend with -X (forearm comes forward), knees with +X (shin goes back), ankles +X point the toes
    /// down, toes -X bend up, fingers -X close the hand, the jaw opens with +X.
    /// </para>
    /// <para>
    /// Derived motion, so every pose gets it for free: the spine and neck share the torso and head rotations
    /// between their two bones; the clavicles lift and reach with the arm; on the ground the feet stay flat
    /// (unless a pose frees them) and the hips drop until the lowest heel, toe or knee touches the ground;
    /// the satchel swings on a spring behind the motion.
    /// </para>
    /// </summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        public CharacterRig Rig;
        public IAnimationSource Source;

        [SerializeField] private float blend = 14f;
        [SerializeField] private float strideLength = 1.5f;

        /// <summary>Held stances, set every frame by the owner.</summary>
        public bool Guarding { get; set; }
        public bool Aiming { get; set; }
        public bool Kneeling { get; set; }
        /// <summary>Sword in hand: the right forearm stays raised so the blade never drags on the ground.</summary>
        public bool Armed { get; set; }
        /// <summary>Shield on the left forearm: at rest it hangs at the side, face out, as in the reference.</summary>
        public bool ShieldInHand { get; set; }
        /// <summary>Crouched (sneaking): the captured crouched walk replaces the walk.</summary>
        public bool Crouching { get; set; }

        /// <summary>
        /// Left arm brought forward and across the chest. The forearm shield's face (the arm's -X side) then
        /// turns toward the threat, slightly to the left, covering the torso while the face stays visible
        /// (reference pose 3). LookRotation: the arm's local Z goes up, its local Y opposite to the arm.
        /// </summary>
        private static readonly Quaternion ShieldGuardArm =
            Quaternion.LookRotation(Vector3.up, -new Vector3(0.85f, -0.15f, 0.5f));

        /// <summary>Share of the torso rotation taken by the lower spine; the chest takes the rest.</summary>
        private const float LowerSpineShare = 0.45f;
        /// <summary>Share of the head rotation taken by the neck.</summary>
        private const float NeckShare = 0.4f;
        private const float Grip = -75f;
        private const float Relaxed = -18f;

        public ActionKind CurrentAction { get; private set; }
        public float ActionProgress => _actionDuration <= 0f ? 1f : Mathf.Clamp01((Time.time - _actionStart) / _actionDuration);
        public bool ActionPlaying => CurrentAction != ActionKind.None && ActionProgress < 1f;

        private float _phase;
        private float _clipProgress;
        private float _groundSpeed;
        private MotionClip _airClip;
        private bool _wasAirborne;
        private float _lastYaw = float.NaN;
        private float _yawRate;
        private float _turnWeight;
        private float _crouchWeight;
        private float _stillTime;
        private float _walkTime;
        private float _lastSpeed;
        private MotionClip _oneShot;
        private float _oneShotStart;
        private float _oneShotEnd;
        private float _oneShotTime;
        private float _oneShotLength;
        private float _actionStart = -10f;
        private float _actionDuration;
        private int _actionIndex;
        private float _heldProgress = -1f;
        private float _freeHipsY = float.NaN;
        private float _groundWeight = 1f;
        private float _bagAngle;
        private float _bagVelocity;
        private float _lastHipsY = float.NaN;
        private float _lastHipsSpeed;

        /// <summary>Plays a one-shot action. Repeated slashes alternate their direction.</summary>
        public void Play(ActionKind kind, float duration = -1f)
        {
            if (kind == ActionKind.None) return;
            if (kind == CurrentAction && ActionPlaying && kind == ActionKind.SlashLight) _actionIndex++;
            else if (kind != CurrentAction) _actionIndex = 0;
            else _actionIndex++;
            CurrentAction = kind;
            _actionStart = Time.time;
            _actionDuration = duration > 0f ? duration : DefaultDuration(kind);
            // Captured swings carry a real wind-up and follow-through: they get a little more time.
            if (duration <= 0f && HasCapturedSwings && (kind == ActionKind.SlashLight || kind == ActionKind.SlashHeavy))
                _actionDuration *= 1.35f;
            _heldProgress = -1f;
        }

        /// <summary>Right-arm swing kept for callers that only know "an attack happened" (runes).</summary>
        public void TriggerCast() => Play(ActionKind.Cast);

        /// <summary>Previews and tests: freeze an action at a point of its curve (0..1).</summary>
        public void Hold(ActionKind kind, float progress)
        {
            CurrentAction = kind;
            _actionDuration = 1f;
            _heldProgress = Mathf.Clamp01(progress);
        }

        private static float DefaultDuration(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.SlashLight: return 0.34f;
                case ActionKind.SlashHeavy: return 0.62f;
                case ActionKind.Thrust: return 0.3f;
                case ActionKind.Throw: return 0.42f;
                case ActionKind.Shoot: return 0.22f;
                case ActionKind.Cast: return 0.35f;
                case ActionKind.Hurt: return 0.32f;
                case ActionKind.Pickup: return 0.55f;
                case ActionKind.Parry: return 0.28f;
                case ActionKind.Land: return 0.35f;
                case ActionKind.ClimbJump: return 0.3f;
                case ActionKind.Kick: return 1.3f;
                case ActionKind.Vault: return 1.1f;
                case ActionKind.SwimDash: return 0.5f;
                default: return 0.3f;
            }
        }

        private void LateUpdate()
        {
            if (Rig == null || Source == null) return;
            var dt = Time.deltaTime;
            var velocity = Source.Velocity;
            var horizontal = new Vector3(velocity.x, 0f, velocity.z);
            var speed = horizontal.magnitude;
            var run = Mathf.Clamp01(speed / Mathf.Max(0.1f, Source.MaxGroundSpeed));
            var grounded = Source.Locomotion == Locomotion.Grounded;
            _groundSpeed = grounded ? speed : 0f;

            // Jumps: the standing or the running jump clip, chosen at takeoff; touching down plays its landing.
            var airborne = Source.Locomotion == Locomotion.Airborne;
            if (airborne && !_wasAirborne)
                _airClip = speed > 1f ? Rig.RunningJump ?? Rig.Jump : Rig.Jump ?? Rig.RunningJump;
            if (!airborne && _wasAirborne && grounded && _airClip != null)
            {
                var (_, _, landing) = _airClip.AirPhases();
                PlayLocomotion(_airClip, (landing, Mathf.Min(1f, landing + 0.5f / _airClip.Duration)), 1.2f);
            }

            _wasAirborne = airborne;

            Pose pose;
            switch (Source.Locomotion)
            {
                case Locomotion.Airborne: pose = _airClip != null ? CapturedAir(velocity.y) : Airborne(velocity.y); break;
                case Locomotion.Climbing: pose = Climbing(velocity, dt); break;
                case Locomotion.Gliding: pose = Gliding(); break;
                case Locomotion.Swimming: pose = Swimming(speed, dt); break;
                default: pose = Rig.Walk != null || Rig.Run != null ? CapturedGround(speed, horizontal, dt) : Ground(speed, run, dt); break;
            }

            if (grounded)
            {
                if (Kneeling) pose = Mix(pose, Kneel(), 1f);
                else if (Aiming) pose = Mix(pose, Aim(run), 1f);
                else if (Guarding) pose = Mix(pose, Guard(run), 1f);
            }

            ApplyAction(ref pose);
            ApplyPose(pose, grounded, dt);
            Secondary(velocity, dt);

            if (Rig.Glider.gameObject.activeSelf != pose.ShowGlider) Rig.Glider.gameObject.SetActive(pose.ShowGlider);
        }

        /// <summary>Strikes cut through the smoothing: the pose follows the swing curve almost as authored.</summary>
        private static bool IsStrike(ActionKind kind) =>
            kind == ActionKind.SlashLight || kind == ActionKind.SlashHeavy || kind == ActionKind.Thrust || kind == ActionKind.Kick;

        private void ApplyPose(Pose pose, bool grounded, float dt)
        {
            var rate = IsStrike(CurrentAction) ? Mathf.Max(blend, 42f) : blend;
            var k = 1f - Mathf.Exp(-rate * dt);
            Blend(Rig.Hips, N(pose.Hips), k);
            Split(Rig.Torso, Rig.Chest, N(pose.Torso), LowerSpineShare, k);
            Split(Rig.Neck, Rig.Head, N(pose.Head), NeckShare, k);
            Blend(Rig.Jaw, N(pose.Jaw), k);

            Arm(Rig.ClavicleL, Rig.ArmL, N(pose.ArmL), -1f, k);
            Arm(Rig.ClavicleR, Rig.ArmR, N(pose.ArmR), 1f, k);
            Blend(Rig.ElbowL, N(pose.ElbowL), k);
            Blend(Rig.ElbowR, N(pose.ElbowR), k);
            Blend(Rig.WristL, N(pose.WristL), k);
            Blend(Rig.WristR, N(pose.WristR), k);
            Blend(Rig.FingersL, N(pose.FingersL), k);
            Blend(Rig.FingersR, N(pose.FingersR), k);

            Blend(Rig.LegL, N(pose.LegL), k);
            Blend(Rig.LegR, N(pose.LegR), k);
            Blend(Rig.KneeL, N(pose.KneeL), k);
            Blend(Rig.KneeR, N(pose.KneeR), k);
            Blend(Rig.AnkleL, Foot(pose.Hips, pose.LegL, pose.KneeL, pose.AnkleL, grounded ? pose.FootFreeL : 1f), k);
            Blend(Rig.AnkleR, Foot(pose.Hips, pose.LegR, pose.KneeR, pose.AnkleR, grounded ? pose.FootFreeR : 1f), k);
            Blend(Rig.ToesL, N(pose.ToesL), k);
            Blend(Rig.ToesR, N(pose.ToesR), k);

            // Hips: free poses ease toward their authored height; on the ground the legs decide it.
            var freeY = Rig.HipsHeight + pose.HipsOffsetY;
            _freeHipsY = float.IsNaN(_freeHipsY) ? freeY : Mathf.Lerp(_freeHipsY, freeY, k);
            var z = Mathf.Lerp(Rig.Hips.localPosition.z, pose.HipsOffsetZ, k);
            var usesContacts = grounded && Rig.Contacts.Count > 0;
            _groundWeight = Mathf.MoveTowards(_groundWeight, usesContacts ? 1f : 0f, dt * 6f);
            var y = _freeHipsY;
            if (_groundWeight > 0f && Rig.Contacts.Count > 0)
            {
                Rig.Hips.localPosition = new Vector3(0f, Rig.HipsHeight, z);
                var planted = Rig.HipsHeight - Rig.LowestContactHeight();
                y = Mathf.Lerp(_freeHipsY, planted, _groundWeight);
            }

            Rig.Hips.localPosition = new Vector3(0f, y, z);
        }

        /// <summary>Satchel on a damped spring: it trails behind the run and bounces with the hips.</summary>
        private void Secondary(Vector3 velocity, float dt)
        {
            if (Rig.Bag == null || dt <= 0f) return;
            var local = transform.InverseTransformDirection(velocity);
            var hipsY = Rig.Hips.localPosition.y;
            var hipsSpeed = float.IsNaN(_lastHipsY) ? 0f : (hipsY - _lastHipsY) / dt;
            _bagVelocity -= (hipsSpeed - _lastHipsSpeed) * 90f;
            _lastHipsY = hipsY;
            _lastHipsSpeed = hipsSpeed;

            var target = Mathf.Clamp(local.z * 3f, -20f, 30f);
            _bagVelocity += (80f * (target - _bagAngle) - 9f * _bagVelocity) * dt;
            _bagAngle = Mathf.Clamp(_bagAngle + _bagVelocity * dt, -35f, 50f);
            Rig.Bag.localRotation = E(_bagAngle);
        }

        public struct Pose
        {
            public Quaternion Hips, Torso, Head, Jaw;
            public Quaternion ArmL, ArmR, ElbowL, ElbowR, WristL, WristR, FingersL, FingersR;
            public Quaternion LegL, LegR, KneeL, KneeR, AnkleL, AnkleR, ToesL, ToesR;
            /// <summary>
            /// 0 = the foot stays flat on the ground and <see cref="AnkleL"/> is added on top (heel lift);
            /// 1 = the ankle follows <see cref="AnkleL"/> relative to the shin (swinging or kneeling foot).
            /// </summary>
            public float FootFreeL, FootFreeR;
            /// <summary>Used off the ground; on the ground the contacts set the hip height.</summary>
            public float HipsOffsetY;
            public float HipsOffsetZ;
            public bool ShowGlider;
        }

        private static Quaternion E(float x, float y = 0f, float z = 0f) => Quaternion.Euler(x, y, z);

        /// <summary>Unset quaternions in a pose (all zero) mean "no rotation".</summary>
        private static Quaternion N(Quaternion q) =>
            q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f ? Quaternion.identity : q;

        private static Quaternion S(Quaternion a, Quaternion b, float t) => Quaternion.Slerp(N(a), N(b), t);

        internal static Pose Mix(Pose a, Pose b, float t)
        {
            return new Pose
            {
                Hips = S(a.Hips, b.Hips, t),
                Torso = S(a.Torso, b.Torso, t),
                Head = S(a.Head, b.Head, t),
                Jaw = S(a.Jaw, b.Jaw, t),
                ArmL = S(a.ArmL, b.ArmL, t),
                ArmR = S(a.ArmR, b.ArmR, t),
                ElbowL = S(a.ElbowL, b.ElbowL, t),
                ElbowR = S(a.ElbowR, b.ElbowR, t),
                WristL = S(a.WristL, b.WristL, t),
                WristR = S(a.WristR, b.WristR, t),
                FingersL = S(a.FingersL, b.FingersL, t),
                FingersR = S(a.FingersR, b.FingersR, t),
                LegL = S(a.LegL, b.LegL, t),
                LegR = S(a.LegR, b.LegR, t),
                KneeL = S(a.KneeL, b.KneeL, t),
                KneeR = S(a.KneeR, b.KneeR, t),
                AnkleL = S(a.AnkleL, b.AnkleL, t),
                AnkleR = S(a.AnkleR, b.AnkleR, t),
                ToesL = S(a.ToesL, b.ToesL, t),
                ToesR = S(a.ToesR, b.ToesR, t),
                FootFreeL = Mathf.Lerp(a.FootFreeL, b.FootFreeL, t),
                FootFreeR = Mathf.Lerp(a.FootFreeR, b.FootFreeR, t),
                HipsOffsetY = Mathf.Lerp(a.HipsOffsetY, b.HipsOffsetY, t),
                HipsOffsetZ = Mathf.Lerp(a.HipsOffsetZ, b.HipsOffsetZ, t),
                ShowGlider = a.ShowGlider
            };
        }

        // ---------- derived motion ----------

        /// <summary>Two bones in a chain share one rotation (same axis, so together they make exactly the whole).</summary>
        private static void Split(Transform lower, Transform upper, Quaternion whole, float lowerShare, float k)
        {
            if (lower == null || upper == null)
            {
                Blend(upper != null ? upper : lower, whole, k);
                return;
            }

            Blend(lower, Quaternion.Slerp(Quaternion.identity, whole, lowerShare), k);
            Blend(upper, Quaternion.Slerp(Quaternion.identity, whole, 1f - lowerShare), k);
        }

        /// <summary>
        /// The clavicle lifts as the arm rises and reaches forward with it; the shoulder bone then takes the
        /// remaining rotation, so the arm points exactly where the pose says while the shoulder moves.
        /// </summary>
        private static void Arm(Transform clavicle, Transform arm, Quaternion pose, float side, float k)
        {
            if (clavicle == null)
            {
                Blend(arm, pose, k);
                return;
            }

            var dir = pose * Vector3.down;
            var lift = Mathf.Clamp01((dir.y + 0.3f) / 1.3f) * 24f;
            var reach = Mathf.Clamp01(dir.z) * 14f + Mathf.Clamp01(-dir.x * side) * 10f;
            var clav = E(0f, -side * reach, side * lift);
            Blend(clavicle, clav, k);
            Blend(arm, Quaternion.Inverse(clav) * pose, k);
        }

        /// <summary>Planted: undo the hips, thigh and shin rotations so the sole stays level, then add the ankle.</summary>
        private static Quaternion Foot(Quaternion hips, Quaternion leg, Quaternion knee, Quaternion ankle, float free)
        {
            var flat = Quaternion.Inverse(N(hips) * N(leg) * N(knee)) * N(ankle);
            return Quaternion.Slerp(flat, N(ankle), Mathf.Clamp01(free));
        }

        // ---------- locomotion ----------

        // Joint angles over one stride of a leg, from heel strike (0) through push-off and swing back to the next
        // heel strike (1), as (phase, degrees) pairs. Hip: flexion forward. Knee: flexion. Ankle: toes down.
        // Shaped on human gait curves: the stance knee gives a little as the weight lands, the swing knee folds
        // to clear the ground, the ankle pushes off before the foot leaves.
        private static readonly float[] WalkHip = { 0f, 22f, 0.12f, 18f, 0.35f, 2f, 0.55f, -11f, 0.66f, -5f, 0.8f, 18f, 0.9f, 25f };
        private static readonly float[] WalkKnee = { 0f, 4f, 0.12f, 18f, 0.35f, 6f, 0.52f, 22f, 0.68f, 62f, 0.8f, 42f, 0.95f, 4f };
        private static readonly float[] WalkAnkle = { 0f, -4f, 0.1f, 3f, 0.4f, -6f, 0.58f, 16f, 0.66f, 6f, 0.82f, -4f };
        private static readonly float[] RunHip = { 0f, 30f, 0.15f, 14f, 0.33f, -14f, 0.45f, -18f, 0.62f, 12f, 0.8f, 46f, 0.9f, 40f };
        private static readonly float[] RunKnee = { 0f, 22f, 0.12f, 40f, 0.3f, 20f, 0.45f, 64f, 0.62f, 108f, 0.76f, 82f, 0.92f, 30f };
        private static readonly float[] RunAnkle = { 0f, 0f, 0.1f, -10f, 0.3f, 20f, 0.4f, 26f, 0.6f, 2f, 0.85f, -6f };

        /// <summary>Periodic Catmull-Rom through (phase, value) pairs: smooth, so the gait has no corners.</summary>
        private static float Cycle(float[] keys, float phase)
        {
            var n = keys.Length / 2;
            phase -= Mathf.Floor(phase);
            var i = n - 1;
            for (var k = 0; k < n; k++)
                if (keys[k * 2] > phase) { i = k - 1; break; }
            if (i < 0) i = n - 1;

            float T(int k) => keys[((k % n) + n) % n * 2] + Mathf.Floor((float)k / n);
            float V(int k) => keys[((k % n) + n) % n * 2 + 1];
            var t0 = T(i);
            var t1 = T(i + 1);
            var span = t1 - t0;
            var p = phase < t0 ? phase + 1f : phase;
            var u = span > 1e-5f ? (p - t0) / span : 0f;
            var m1 = (V(i + 1) - V(i - 1)) / Mathf.Max(1e-5f, T(i + 1) - T(i - 1)) * span;
            var m2 = (V(i + 2) - V(i)) / Mathf.Max(1e-5f, T(i + 2) - T(i)) * span;
            var u2 = u * u;
            var u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * V(i) + (u3 - 2f * u2 + u) * m1 + (-2f * u3 + 3f * u2) * V(i + 1) + (u3 - u2) * m2;
        }

        /// <summary>How free a foot is from the ground at this stride phase: planted in stance, free from heel-off to heel strike.</summary>
        private static float FootFree(float phase, float liftOff)
        {
            phase -= Mathf.Floor(phase);
            var rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(liftOff - 0.12f, liftOff, phase));
            var land = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.93f, 1f, phase));
            return rise * land;
        }

        private Pose Ground(float speed, float run, float dt)
        {
            // Walk and run are two gaits, not one curve scaled by speed: the run takes over near full speed.
            var moving = Mathf.Clamp01(speed / 1.2f);
            var runW = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.92f, run));
            var step = strideLength * Mathf.Lerp(1f, 1.3f, runW);
            if (speed > 0.1f) _phase += speed / step * Mathf.PI * dt;
            var pL = _phase / (2f * Mathf.PI);
            var pR = pL + 0.5f;

            float Hip(float p) => Mathf.Lerp(Cycle(WalkHip, p), Cycle(RunHip, p), runW) * moving;
            float Knee(float p) => Mathf.Lerp(Cycle(WalkKnee, p), Cycle(RunKnee, p), runW) * moving;
            float Ankle(float p) => Mathf.Lerp(Cycle(WalkAnkle, p), Cycle(RunAnkle, p), runW) * moving;
            var liftOff = Mathf.Lerp(0.6f, 0.38f, runW);

            // Idle: weight shifts slowly from foot to foot, knees soft, a slow look around, breathing.
            var idle = 1f - moving;
            var breathe = Mathf.Sin(Time.time * 2.2f) * idle;
            var shift = Mathf.Sin(Time.time * 0.45f) * idle;
            var lookAround = Mathf.Sin(Time.time * 0.6f) * 14f * idle;

            // Pelvis: turns with the leading leg, drops on the swing side; the chest turns the other way.
            var sway = Mathf.Sin(pL * 2f * Mathf.PI); // > 0 while the left leg is the stance leg
            var turn = Mathf.Cos(pL * 2f * Mathf.PI); // > 0 when the left leg is ahead
            var pelvisYaw = turn * Mathf.Lerp(6f, 9f, runW) * moving;
            var pelvisRoll = -sway * Mathf.Lerp(4f, 3f, runW) * moving + shift * 3f;
            var lean = Mathf.Lerp(4f, 16f, runW) * moving;

            // Arms swing against the legs, a beat late; elbows fold more as the pace rises.
            // The hip curve leans forward (flexion peaks higher than extension): centre it so the arms swing evenly.
            var armAmp = Mathf.Lerp(1.3f, 1.5f, runW) * moving;
            var armCentre = Mathf.Lerp(7f, 14f, runW) * moving;
            var armL = (Hip(pR - 0.04f) - armCentre) * armAmp;
            var armR = (Hip(pL - 0.04f) - armCentre) * armAmp;
            var elbowBase = Mathf.Lerp(-16f, -88f, runW) * moving - 10f * idle;
            var elbowL = elbowBase - Mathf.Max(0f, armL) * 0.5f;
            var elbowR = elbowBase - Mathf.Max(0f, armR) * 0.5f;

            var kneeSoft = 6f * idle;
            return new Pose
            {
                Hips = E(0f, pelvisYaw, pelvisRoll),
                Torso = E(lean + breathe * 1.5f, -pelvisYaw * 1.6f, -pelvisRoll * 0.7f),
                Head = E(-lean * 0.5f, pelvisYaw * 0.6f + lookAround, pelvisRoll * 0.3f),
                ArmL = E(-armL, 0f, -6f - idle * 4f - runW * 6f),
                ArmR = Armed ? E(-26f - armR * 0.35f, -10f, 14f) : E(-armR, 0f, 6f + idle * 4f + runW * 6f),
                ElbowL = E(ShieldInHand ? Mathf.Min(-22f, elbowBase * 0.6f) : elbowL),
                ElbowR = Armed ? E(-42f - runW * 20f) : E(elbowR),
                WristL = E(-6f),
                WristR = Armed ? E(-12f) : E(-6f),
                FingersL = E(ShieldInHand ? Grip : Relaxed - 30f * runW),
                FingersR = E(Armed ? Grip : Relaxed - 30f * runW),
                LegL = E(-Hip(pL) - 3f * idle, 0f, -shift * 2f),
                LegR = E(-Hip(pR) - 3f * idle, 0f, -shift * 2f),
                KneeL = E(Knee(pL) + kneeSoft + Mathf.Max(0f, -shift) * 8f),
                KneeR = E(Knee(pR) + kneeSoft + Mathf.Max(0f, shift) * 8f),
                AnkleL = E(Ankle(pL)),
                AnkleR = E(Ankle(pR)),
                ToesL = E(-28f * moving * Mathf.Clamp01(Ankle(pL) / 16f)),
                ToesR = E(-28f * moving * Mathf.Clamp01(Ankle(pR) / 16f)),
                FootFreeL = FootFree(pL, liftOff) * moving,
                FootFreeR = FootFree(pR, liftOff) * moving
            };
        }

        /// <summary>
        /// Walk and run from the captured loops (<see cref="CharacterRig.Walk"/>, <see cref="CharacterRig.Run"/>).
        /// Each loop plays at the cadence that keeps its planted foot still on the ground, up to 1.9 times its own
        /// pace; between the two speeds the loops cross-fade on a shared, heel-strike-aligned progress. Standing
        /// still, the procedural idle takes over. On top of that:
        /// <list type="bullet">
        /// <item>crouched, the crouched-walk loop replaces the walk (and holds its last step when stopped);</item>
        /// <item>turning while walking blends in the turning loop, mirrored for right turns;</item>
        /// <item>one-shots: the start clip when setting off from standing, the stop clip when halting after a
        /// walk, the about-turn clip when the walk reverses.</item>
        /// </list>
        /// The drawn sword and the forearm shield keep their procedural holds over the clips' arm swing.
        /// </summary>
        private Pose CapturedGround(float speed, Vector3 horizontal, float dt)
        {
            var walk = Rig.Walk ?? Rig.Run;
            var runClip = Rig.Run ?? Rig.Walk;
            var idle = Ground(0f, 0f, dt);
            var moving = Mathf.SmoothStep(0f, 1f, speed / 0.6f);
            var walkTop = walk.Speed * 1.2f;
            var runW = runClip == walk ? 0f
                : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(walkTop, Mathf.Max(walkTop + 0.5f, runClip.Speed * 0.9f), speed));
            _crouchWeight = Mathf.MoveTowards(_crouchWeight, Crouching && Rig.CrouchWalk != null ? 1f : 0f, dt * 5f);
            var crouch = _crouchWeight > 0f ? Rig.CrouchWalk : null;

            var cycle = Mathf.Lerp(walk.CycleDistance, runClip.CycleDistance, runW);
            var loop = Mathf.Lerp(walk.Duration, runClip.Duration, runW);
            if (crouch != null)
            {
                cycle = Mathf.Lerp(cycle, crouch.CycleDistance, _crouchWeight);
                loop = Mathf.Lerp(loop, crouch.Duration, _crouchWeight);
            }

            if (speed > 0.05f) _clipProgress += Mathf.Min(speed / Mathf.Max(0.05f, cycle), 1.9f / loop) * dt;

            Transitions(speed, horizontal, walk, dt);

            var gait = walk.Sample(_clipProgress + walk.LeftStrike);
            if (runW > 0f) gait = Mix(gait, runClip.Sample(_clipProgress + runClip.LeftStrike), runW);

            // Turning: the turning loop leans into the curve; it was captured turning left.
            var turnTarget = Rig.TurnLeft != null
                ? moving * (1f - runW) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 70f, Mathf.Abs(_yawRate)))
                : 0f;
            _turnWeight = Mathf.Lerp(_turnWeight, turnTarget, 1f - Mathf.Exp(-6f * dt));
            if (_turnWeight > 0.01f)
            {
                var turn = Rig.TurnLeft.Sample(_clipProgress + Rig.TurnLeft.LeftStrike);
                var clipTurnsRight = Rig.TurnLeft.TurnDegrees > 0f;
                if (_yawRate > 0f != clipTurnsRight) turn = MotionClip.Mirror(turn);
                gait = Mix(gait, turn, _turnWeight);
            }

            var pose = Mix(idle, gait, moving);
            if (crouch != null) pose = Mix(pose, crouch.Sample(_clipProgress + crouch.LeftStrike), _crouchWeight);

            if (_oneShot != null)
            {
                _oneShotTime += dt;
                var u = _oneShotTime / _oneShotLength;
                if (u >= 1f) _oneShot = null;
                else
                {
                    var w = Mathf.SmoothStep(0f, 1f, u / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, u)));
                    pose = Mix(pose, _oneShot.Sample(Mathf.Lerp(_oneShotStart, _oneShotEnd, u)), w * (1f - _crouchWeight));
                }
            }

            if (Armed)
            {
                pose.ArmR = idle.ArmR;
                pose.ElbowR = idle.ElbowR;
                pose.WristR = idle.WristR;
                pose.FingersR = idle.FingersR;
            }

            if (ShieldInHand)
            {
                pose.ElbowL = idle.ElbowL;
                pose.FingersL = idle.FingersL;
            }

            return pose;
        }

        /// <summary>Watches speed, heading and turning to start the one-shot transition clips.</summary>
        private void Transitions(float speed, Vector3 horizontal, MotionClip walk, float dt)
        {
            var yaw = transform.eulerAngles.y;
            var rate = float.IsNaN(_lastYaw) || dt <= 0f ? 0f : Mathf.DeltaAngle(_lastYaw, yaw) / dt;
            _lastYaw = yaw;
            _yawRate = Mathf.Lerp(_yawRate, rate, 1f - Mathf.Exp(-8f * dt));

            const float still = 0.15f;
            var walkingPace = walk.Speed * 1.6f;
            if (!Crouching)
            {
                if (_lastSpeed < still && speed >= still && _stillTime > 0.3f && speed < walkingPace && Rig.StartWalk != null)
                    PlayLocomotion(Rig.StartWalk, Rig.StartWalk.StartWindow(), 1.15f);
                else if (_lastSpeed >= still && speed < still && _walkTime > 0.5f && _lastSpeed < walkingPace && Rig.StopWalk != null)
                    PlayLocomotion(Rig.StopWalk, Rig.StopWalk.StopWindow(), 1.15f);
                else if (speed > 0.5f && speed < walkingPace && Rig.TurnAround != null && _oneShot != Rig.TurnAround
                         && Vector3.Angle(horizontal, transform.forward) > 135f)
                    PlayLocomotion(Rig.TurnAround, (0f, 0.9f), 1.4f);
            }

            // Setting off cancels a stop; halting cancels a start.
            if (_oneShot == Rig.StopWalk && speed > 0.3f) _oneShot = null;
            if (_oneShot == Rig.StartWalk && speed < 0.05f) _oneShot = null;

            _stillTime = speed < still ? _stillTime + dt : 0f;
            _walkTime = speed >= still ? _walkTime + dt : 0f;
            _lastSpeed = speed;
        }

        private void PlayLocomotion(MotionClip clip, (float start, float end) window, float rate)
        {
            if (window.end <= window.start) return;
            _oneShot = clip;
            _oneShotStart = window.start;
            _oneShotEnd = window.end;
            _oneShotTime = 0f;
            _oneShotLength = (window.end - window.start) * clip.Duration / rate;
        }

        /// <summary>
        /// In the air with a captured jump: rising plays takeoff to apex as the upward speed runs out, falling plays
        /// apex to just before touchdown as the downward speed builds (a long fall holds the pre-landing pose).
        /// </summary>
        private Pose CapturedAir(float verticalSpeed)
        {
            const float launch = 7.5f; // about the motor's jump speed
            var (takeoff, apex, landing) = _airClip.AirPhases();
            var progress = verticalSpeed > 0f
                ? Mathf.Lerp(takeoff, apex, 1f - Mathf.Clamp01(verticalSpeed / launch))
                : Mathf.Lerp(apex, Mathf.Max(apex, landing - 0.02f), Mathf.Clamp01(-verticalSpeed / launch));
            return _airClip.Sample(progress);
        }

        private static Pose Airborne(float verticalSpeed)
        {
            var rising = Mathf.Clamp01(verticalSpeed / 6f);
            return new Pose
            {
                Torso = E(8f),
                Head = E(-6f),
                ArmL = E(-30f - rising * 40f, 0f, -35f),
                ArmR = E(-30f - rising * 40f, 0f, 35f),
                ElbowL = E(-40f),
                ElbowR = E(-40f),
                WristL = E(10f),
                WristR = E(10f),
                FingersL = E(-10f),
                FingersR = E(-10f),
                LegL = E(-55f),
                LegR = E(8f),
                KneeL = E(80f),
                KneeR = E(35f),
                AnkleL = E(-10f),
                AnkleR = E(30f),
                HipsOffsetY = 0.05f
            };
        }

        private Pose Climbing(Vector3 velocity, float dt)
        {
            var climbSpeed = velocity.magnitude;
            if (climbSpeed > 0.05f) _phase += climbSpeed * 3.2f * dt;
            var s = Mathf.Sin(_phase);
            return new Pose
            {
                Torso = E(-6f),
                Head = E(-25f),
                // Reaching hand straight, pulling hand bent; the knee of the stepping leg folds high.
                ArmL = E(-150f + s * 30f, 0f, -14f),
                ArmR = E(-150f - s * 30f, 0f, 14f),
                ElbowL = E(-20f - 45f * Mathf.Max(0f, s)),
                ElbowR = E(-20f - 45f * Mathf.Max(0f, -s)),
                WristL = E(-25f),
                WristR = E(-25f),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(-40f - s * 30f),
                LegR = E(-40f + s * 30f),
                KneeL = E(55f + 35f * Mathf.Max(0f, s)),
                KneeR = E(55f + 35f * Mathf.Max(0f, -s)),
                // Toes on the holds.
                AnkleL = E(-30f),
                AnkleR = E(-30f),
                ToesL = E(-25f),
                ToesR = E(-25f),
                // Pull the body against the wall.
                HipsOffsetZ = 0.12f
            };
        }

        private static Pose Gliding()
        {
            var sway = Mathf.Sin(Time.time * 3f) * 4f;
            return new Pose
            {
                Hips = E(0f, 0f, sway * 0.5f),
                Torso = E(12f),
                Head = E(-18f),
                ArmL = E(-172f, 0f, -14f),
                ArmR = E(-172f, 0f, 14f),
                ElbowL = E(-12f),
                ElbowR = E(-12f),
                WristL = E(15f),
                WristR = E(15f),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(10f + sway),
                LegR = E(16f - sway),
                KneeL = E(30f),
                KneeR = E(45f),
                AnkleL = E(35f),
                AnkleR = E(40f),
                ShowGlider = true
            };
        }

        private Pose Swimming(float speed, float dt)
        {
            _phase += (1.5f + speed * 2f) * dt;
            var s = Mathf.Sin(_phase);
            return new Pose
            {
                Hips = E(70f),
                Head = E(-60f),
                ArmL = E(-90f + s * 70f, 0f, -20f),
                ArmR = E(-90f - s * 70f, 0f, 20f),
                ElbowL = E(-15f - 35f * Mathf.Max(0f, s)),
                ElbowR = E(-15f - 35f * Mathf.Max(0f, -s)),
                WristL = E(-15f * s),
                WristR = E(15f * s),
                LegL = E(s * 22f),
                LegR = E(-s * 22f),
                KneeL = E(15f + 20f * Mathf.Max(0f, s)),
                KneeR = E(15f + 20f * Mathf.Max(0f, -s)),
                // Kicking: pointed toes.
                AnkleL = E(45f),
                AnkleR = E(45f),
                HipsOffsetY = -0.4f
            };
        }

        // ---------- held stances ----------

        /// <summary>Shield across the chest, sword arm cocked, knees bent and weight low (reference pose 3).</summary>
        private Pose Guard(float run)
        {
            var step = Mathf.Sin(_phase) * 18f * run;
            return new Pose
            {
                Hips = E(0f, 12f),
                Torso = E(10f, -14f),
                Head = E(-6f, 10f),
                ArmL = ShieldGuardArm,
                ArmR = E(-22f, -10f, 34f),
                ElbowL = Quaternion.identity,
                ElbowR = E(-55f),
                WristR = E(-15f),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(-32f + step),
                LegR = E(-12f - step),
                KneeL = E(48f),
                KneeR = E(32f)
            };
        }

        /// <summary>Bow arm straight ahead, string hand drawn back to the cheek, body turned sideways.</summary>
        private Pose Aim(float run)
        {
            var step = Mathf.Sin(_phase) * 16f * run;
            return new Pose
            {
                Hips = E(0f, 20f),
                Torso = E(4f, -22f),
                Head = E(-4f, 18f, -6f),
                ArmL = E(-92f, -6f, 0f),
                ArmR = E(-88f, -48f, 6f),
                ElbowL = Quaternion.identity,
                ElbowR = E(-135f),
                WristR = E(10f),
                FingersL = E(Grip),
                FingersR = E(-45f),
                LegL = E(-14f + step),
                LegR = E(10f - step),
                KneeL = E(18f),
                KneeR = E(8f)
            };
        }

        /// <summary>Front foot planted with the knee up, back knee on the ground with the toes tucked, hands over the pot.</summary>
        private static Pose Kneel()
        {
            var stir = Mathf.Sin(Time.time * 2.5f) * 10f;
            return new Pose
            {
                Torso = E(16f),
                Head = E(20f),
                ArmL = E(-48f + stir, 0f, -16f),
                ArmR = E(-48f - stir, 0f, 16f),
                ElbowL = E(-40f),
                ElbowR = E(-40f),
                WristL = E(-15f),
                WristR = E(-15f),
                FingersL = E(-40f),
                FingersR = E(-40f),
                LegL = E(-85f),
                LegR = E(-6f),
                KneeL = E(85f),
                KneeR = E(110f),
                AnkleR = E(-20f),
                ToesR = E(-70f),
                FootFreeR = 1f,
                HipsOffsetZ = 0.05f
            };
        }

        // ---------- one-shot actions ----------

        private void ApplyAction(ref Pose pose)
        {
            if (CurrentAction == ActionKind.None) return;
            var t = _heldProgress >= 0f ? _heldProgress : ActionProgress;
            if (t >= 1f && _heldProgress < 0f)
            {
                CurrentAction = ActionKind.None;
                return;
            }

            var weight = IsStrike(CurrentAction) ? StrikeWeight(t) : Mathf.Sin(t * Mathf.PI);
            Pose target;
            switch (CurrentAction)
            {
                case ActionKind.SlashLight:
                    target = HasCapturedSwings
                        ? CapturedSwing(t, _actionIndex % Mathf.Min(2, Rig.Slash.Strikes.Count), pose)
                        : SlashLight(t, _actionIndex);
                    break;
                case ActionKind.SlashHeavy:
                    target = HasCapturedSwings ? CapturedSwing(t, Rig.Slash.Strikes.Count - 1, pose) : SlashHeavy(t);
                    break;
                case ActionKind.Thrust: target = Thrust(t); break;
                case ActionKind.Throw: target = Throw(t); break;
                case ActionKind.Shoot: target = Shoot(t, pose); break;
                case ActionKind.Cast: target = Cast(pose); break;
                case ActionKind.Hurt: target = Hurt(); weight = 1f - t; break;
                case ActionKind.Pickup: target = Pickup(); break;
                case ActionKind.Parry: target = Parry(t); break;
                case ActionKind.Land: target = Land(); weight = 1f - t; break;
                case ActionKind.ClimbJump: target = ClimbJump(); break;
                case ActionKind.Kick:
                    if (Rig.Kick != null)
                    {
                        target = Rig.Kick.Sample(t * 0.98f);
                        target.FingersR = E(Grip);
                        if (ShieldInHand) target.FingersL = E(Grip);
                    }
                    else target = SlashLight(t, _actionIndex);
                    break;
                case ActionKind.Vault:
                    target = Rig.Vault != null ? Rig.Vault.Sample(t * 0.98f) : ClimbJump();
                    weight = Mathf.SmoothStep(0f, 1f, t / 0.08f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, t)));
                    break;
                case ActionKind.SwimDash: target = SwimDash(pose); break;
                default: return;
            }

            target.ShowGlider = pose.ShowGlider;
            pose = Mix(pose, target, Mathf.Clamp01(weight));
        }

        private bool HasCapturedSwings => Rig != null && Rig.Slash != null && Rig.Slash.Strikes.Count > 0;

        /// <summary>
        /// One swing of the captured sword combo (<see cref="CharacterRig.Slash"/>): its wind-up fills the first 40 %
        /// of the action, the peak lands there, the follow-through takes the rest. While the hero moves, the legs
        /// keep most of the gait so a swing on the run does not freeze them; both hands keep their grip.
        /// </summary>
        private Pose CapturedSwing(float t, int strike, Pose current)
        {
            var (start, peak, end) = Rig.Slash.Strikes[Mathf.Clamp(strike, 0, Rig.Slash.Strikes.Count - 1)];
            var progress = t < 0.4f ? Mathf.Lerp(start, peak, t / 0.4f) : Mathf.Lerp(peak, end, (t - 0.4f) / 0.6f);
            var pose = Rig.Slash.Sample(progress);
            pose.FingersR = E(Grip);
            if (ShieldInHand) pose.FingersL = E(Grip);
            var keepGait = Mathf.Clamp01(_groundSpeed / 1.2f) * 0.75f;
            if (keepGait > 0f)
            {
                var legs = Mix(pose, current, keepGait);
                pose.LegL = legs.LegL;
                pose.LegR = legs.LegR;
                pose.KneeL = legs.KneeL;
                pose.KneeR = legs.KneeR;
                pose.AnkleL = legs.AnkleL;
                pose.AnkleR = legs.AnkleR;
                pose.ToesL = legs.ToesL;
                pose.ToesR = legs.ToesR;
                pose.FootFreeL = legs.FootFreeL;
                pose.FootFreeR = legs.FootFreeR;
            }

            return pose;
        }

        /// <summary>Strikes reach full weight within the first 12 % (the wind-up shows) and release over the last 25 %.</summary>
        private static float StrikeWeight(float t) =>
            Mathf.SmoothStep(0f, 1f, t / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, t)));

        /// <summary>
        /// The swing's timing: 0 while winding up (anticipation), then a fast cut that overshoots a little and
        /// settles at 1 (follow-through). <paramref name="start"/> and <paramref name="end"/> bound the cut.
        /// </summary>
        private static float Cut(float t, float start, float end)
        {
            var u = Mathf.Clamp01((t - start) / (end - start));
            const float back = 1.6f; // ease-out-back: the blade and the body carry past the line, then settle
            var v = u - 1f;
            return 1f + (back + 1f) * v * v * v + back * v * v;
        }

        private static float L(float a, float b, float t) => Mathf.LerpUnclamped(a, b, t);

        /// <summary>
        /// One-handed sword. The body leads: hips, then chest, then shoulder, then the wrist snaps the blade
        /// through. Even swings cut flat from the right, coiling the trunk and bending it into the cut; odd swings
        /// come overhead, arching back and folding the trunk forward over the lead leg.
        /// </summary>
        private static Pose SlashLight(float t, int index)
        {
            var c = Cut(t, 0.26f, 0.46f);
            var lag = Cut(t, 0.3f, 0.52f); // arm and wrist trail the trunk by a few hundredths
            if (index % 2 == 0)
            {
                return new Pose
                {
                    Hips = E(0f, L(32f, -30f, c), L(-3f, 4f, c)),
                    Torso = E(L(4f, 24f, c), L(48f, -58f, c), L(-10f, 16f, c)),
                    Head = E(L(-2f, 6f, c), L(-26f, 30f, c), L(6f, -10f, c)),
                    ArmL = E(L(-50f, -12f, c), L(-20f, 10f, c), L(-24f, -42f, c)),
                    ArmR = E(L(-96f, -78f, lag), L(72f, -72f, lag), L(26f, -16f, lag)),
                    ElbowL = E(L(-60f, -78f, c)),
                    ElbowR = E(L(-100f, -4f, lag)),
                    WristR = E(L(38f, -28f, lag), L(-10f, 14f, lag)),
                    FingersL = E(Grip),
                    FingersR = E(Grip),
                    LegL = E(L(-22f, -42f, c), L(-6f, 4f, c)),
                    LegR = E(L(12f, 28f, c)),
                    KneeL = E(L(30f, 58f, c)),
                    KneeR = E(L(18f, 14f, c)),
                    AnkleR = E(L(8f, 26f, c)),
                    ToesR = E(L(-10f, -28f, c)),
                    FootFreeR = 0.15f,
                    HipsOffsetZ = L(-0.04f, 0.16f, c)
                };
            }

            return new Pose
            {
                Hips = E(L(-4f, 10f, c), L(12f, -8f, c)),
                Torso = E(L(-20f, 42f, c), L(14f, -10f, c), L(-4f, 6f, c)),
                Head = E(L(-18f, 14f, c), L(-6f, 4f, c)),
                ArmL = E(L(-64f, -18f, c), 0f, L(-22f, -36f, c)),
                ArmR = E(L(-172f, -42f, lag), L(-4f, -10f, lag), L(10f, -2f, lag)),
                ElbowL = E(L(-48f, -70f, c)),
                ElbowR = E(L(-104f, -4f, lag)),
                WristR = E(L(42f, -34f, lag)),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(L(-20f, -44f, c)),
                LegR = E(L(12f, 30f, c)),
                KneeL = E(L(16f, 62f, c)),
                KneeR = E(L(10f, 18f, c)),
                AnkleR = E(L(10f, 28f, c)),
                ToesR = E(L(-12f, -30f, c)),
                FootFreeR = 0.15f,
                HipsOffsetZ = L(-0.05f, 0.18f, c)
            };
        }

        /// <summary>
        /// Two-handed overhead chop: a long wind-up with the body arched back and the blade over the head, then
        /// the whole trunk folds forward over a deep lunge and the arms follow it down.
        /// </summary>
        private static Pose SlashHeavy(float t)
        {
            var c = Cut(t, 0.36f, 0.56f);
            var lag = Cut(t, 0.4f, 0.6f);
            return new Pose
            {
                Hips = E(L(-6f, 14f, c), L(16f, -6f, c)),
                Torso = E(L(-28f, 52f, c), L(18f, -14f, c), L(-6f, 6f, c)),
                Head = E(L(-24f, 16f, c)),
                Jaw = E(14f * Mathf.Clamp01(c)),
                ArmL = E(L(-170f, -40f, lag), L(18f, 14f, lag), -14f),
                ArmR = E(L(-176f, -44f, lag), L(-6f, -8f, lag), 10f),
                ElbowL = E(L(-90f, -8f, lag)),
                ElbowR = E(L(-90f, -8f, lag)),
                WristL = E(L(40f, -30f, lag)),
                WristR = E(L(40f, -30f, lag)),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(L(-24f, -50f, c)),
                LegR = E(L(16f, 34f, c)),
                KneeL = E(L(22f, 76f, c)),
                KneeR = E(L(8f, 30f, c)),
                AnkleR = E(L(12f, 30f, c)),
                ToesR = E(L(-14f, -32f, c)),
                FootFreeR = 0.15f,
                HipsOffsetZ = L(-0.06f, 0.22f, c)
            };
        }

        /// <summary>Fist pulled back to the hip with the elbow folded, then driven straight out into a lunge (reference pose 4).</summary>
        private static Pose Thrust(float t)
        {
            var push = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.1f) / 0.45f));
            return new Pose
            {
                Hips = E(0f, Mathf.Lerp(30f, -12f, push)),
                Torso = E(Mathf.Lerp(0f, 16f, push), Mathf.Lerp(26f, -22f, push)),
                Head = E(0f, Mathf.Lerp(-14f, 10f, push)),
                Jaw = E(8f * push),
                ArmL = E(-48f, 20f, -22f),
                ArmR = E(Mathf.Lerp(-20f, -88f, push), Mathf.Lerp(28f, -4f, push), 0f),
                ElbowL = E(-45f),
                ElbowR = E(Mathf.Lerp(-110f, -4f, push)),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(Mathf.Lerp(-12f, -55f, push)),
                LegR = E(Mathf.Lerp(0f, 28f, push)),
                KneeL = E(Mathf.Lerp(15f, 62f, push)),
                KneeR = E(Mathf.Lerp(10f, 6f, push)),
                // Back heel rises: weight on the ball of the trailing foot.
                AnkleR = E(26f * push),
                ToesR = E(-32f * push),
                HipsOffsetZ = Mathf.Lerp(0f, 0.1f, push)
            };
        }

        /// <summary>Arm cocked back over the shoulder with the elbow folded, then whipped forward; the hand opens on release.</summary>
        private static Pose Throw(float t)
        {
            var release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.3f) / 0.4f));
            return new Pose
            {
                Hips = E(0f, Mathf.Lerp(34f, -20f, release)),
                Torso = E(Mathf.Lerp(-10f, 24f, release), Mathf.Lerp(36f, -30f, release)),
                Head = E(-6f, Mathf.Lerp(-20f, 12f, release)),
                Jaw = E(6f * release),
                ArmL = E(-70f, 10f, -20f),
                ArmR = E(Mathf.Lerp(-150f, -110f, release), Mathf.Lerp(30f, -10f, release), 14f),
                ElbowL = E(-20f),
                ElbowR = E(Mathf.Lerp(-120f, -5f, release)),
                WristR = E(Mathf.Lerp(40f, -30f, release)),
                FingersR = E(Mathf.Lerp(Grip, -5f, release)),
                LegL = E(-32f),
                LegR = E(24f),
                KneeL = E(36f),
                KneeR = E(12f),
                AnkleR = E(20f * release),
                ToesR = E(-25f * release)
            };
        }

        /// <summary>From the aim: the drawn string hand opens and springs forward on release.</summary>
        private static Pose Shoot(float t, Pose current)
        {
            var kick = Mathf.Clamp01(t * 2f);
            var pose = current;
            pose.ElbowR = E(Mathf.Lerp(-135f, -90f, kick));
            pose.FingersR = E(Mathf.Lerp(-45f, 0f, kick));
            pose.WristR = E(Mathf.Lerp(10f, -15f, kick));
            pose.Head = E(-2f, 16f, -4f);
            return pose;
        }

        /// <summary>Right arm thrust forward with an open palm, shoulders turned into it.</summary>
        private static Pose Cast(Pose current)
        {
            var pose = current;
            pose.ArmR = E(-95f, -10f, 5f);
            pose.ElbowR = E(-5f);
            pose.WristR = E(35f);
            pose.FingersR = E(5f);
            pose.Torso = N(current.Torso) * E(0f, -20f, 0f);
            return pose;
        }

        /// <summary>Flinch: chest back, head thrown, mouth open, arms out with open hands, knees giving.</summary>
        private static Pose Hurt() => new Pose
        {
            Hips = E(-6f),
            Torso = E(-22f, 8f),
            Head = E(-24f, -10f),
            Jaw = E(16f),
            ArmL = E(-40f, 0f, -45f),
            ArmR = E(-40f, 0f, 45f),
            ElbowL = E(-35f),
            ElbowR = E(-35f),
            WristL = E(20f),
            WristR = E(20f),
            FingersL = E(5f),
            FingersR = E(5f),
            LegL = E(10f),
            LegR = E(-26f),
            KneeL = E(16f),
            KneeR = E(36f),
            HipsOffsetZ = -0.08f
        };

        /// <summary>Deep crouch on bent knees, one forearm resting on the knee, the other hand reaching the ground (reference pose 5).</summary>
        private static Pose Pickup() => new Pose
        {
            Hips = E(0f, 8f),
            Torso = E(30f, -10f),
            Head = E(14f),
            ArmL = E(-45f, 0f, -12f),
            ArmR = E(-50f, -14f, 6f),
            ElbowL = E(-70f),
            ElbowR = E(-15f),
            WristL = E(10f),
            WristR = E(-25f),
            FingersL = E(-30f),
            FingersR = E(-55f),
            LegL = E(-80f),
            LegR = E(-66f),
            KneeL = E(120f),
            KneeR = E(112f),
            // Heels come up a little in a deep squat.
            AnkleL = E(12f),
            AnkleR = E(18f),
            ToesL = E(-14f),
            ToesR = E(-20f),
            HipsOffsetZ = 0.04f
        };

        /// <summary>Shield punched forward, then the sword arm comes up for the counter.</summary>
        private static Pose Parry(float t)
        {
            var counter = Mathf.Clamp01((t - 0.4f) / 0.5f);
            return new Pose
            {
                Hips = E(0f, 16f),
                Torso = E(6f, -24f),
                Head = E(-6f, 14f),
                ArmL = ShieldGuardArm * E(-10f, 0f, 0f),
                ArmR = E(Mathf.Lerp(-25f, -130f, counter), 10f, 24f),
                ElbowL = Quaternion.identity,
                ElbowR = E(Mathf.Lerp(-60f, -20f, counter)),
                WristR = E(Mathf.Lerp(20f, -15f, counter)),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(-38f),
                LegR = E(22f),
                KneeL = E(52f),
                KneeR = E(16f),
                AnkleR = E(16f),
                ToesR = E(-20f)
            };
        }

        /// <summary>Squat on landing: knees and elbows take the impact.</summary>
        private static Pose Land() => new Pose
        {
            Torso = E(26f),
            Head = E(-8f),
            Jaw = E(5f),
            ArmL = E(-25f, 0f, -38f),
            ArmR = E(-25f, 0f, 38f),
            ElbowL = E(-40f),
            ElbowR = E(-40f),
            WristL = E(20f),
            WristR = E(20f),
            LegL = E(-50f),
            LegR = E(-44f),
            KneeL = E(88f),
            KneeR = E(80f)
        };

        /// <summary>Arms thrown up the wall, legs tucked.</summary>
        private static Pose ClimbJump() => new Pose
        {
            Torso = E(-10f),
            Head = E(-30f),
            ArmL = E(-178f, 0f, -8f),
            ArmR = E(-178f, 0f, 8f),
            ElbowL = E(-10f),
            ElbowR = E(-10f),
            WristL = E(-20f),
            WristR = E(-20f),
            FingersL = E(-10f),
            FingersR = E(-10f),
            LegL = E(-60f),
            LegR = E(-50f),
            KneeL = E(100f),
            KneeR = E(90f),
            AnkleL = E(30f),
            AnkleR = E(30f),
            HipsOffsetZ = 0.1f
        };

        /// <summary>Arms straight ahead, legs together and straight with pointed toes: a burst through the water.</summary>
        private static Pose SwimDash(Pose current)
        {
            var pose = current;
            pose.ArmL = E(-170f, 0f, -6f);
            pose.ArmR = E(-170f, 0f, 6f);
            pose.ElbowL = Quaternion.identity;
            pose.ElbowR = Quaternion.identity;
            pose.WristL = Quaternion.identity;
            pose.WristR = Quaternion.identity;
            pose.FingersL = Quaternion.identity;
            pose.FingersR = Quaternion.identity;
            pose.LegL = E(4f);
            pose.LegR = E(-4f);
            pose.KneeL = E(5f);
            pose.KneeR = E(5f);
            pose.AnkleL = E(60f);
            pose.AnkleR = E(60f);
            pose.Head = E(-70f);
            return pose;
        }

        private static void Blend(Transform joint, Quaternion target, float k)
        {
            if (joint != null) joint.localRotation = Quaternion.Slerp(joint.localRotation, target, k);
        }
    }
}
