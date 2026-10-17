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
        SwimDash
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

            Pose pose;
            switch (Source.Locomotion)
            {
                case Locomotion.Airborne: pose = Airborne(velocity.y); break;
                case Locomotion.Climbing: pose = Climbing(velocity, dt); break;
                case Locomotion.Gliding: pose = Gliding(); break;
                case Locomotion.Swimming: pose = Swimming(speed, dt); break;
                default: pose = Ground(speed, run, dt); break;
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

        private void ApplyPose(Pose pose, bool grounded, float dt)
        {
            var k = 1f - Mathf.Exp(-blend * dt);
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

        private static Pose Mix(Pose a, Pose b, float t)
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

        private Pose Ground(float speed, float run, float dt)
        {
            if (speed > 0.1f) _phase += speed / strideLength * Mathf.PI * dt;
            var swing = Mathf.Sin(_phase); // > 0: left leg ahead
            var lift = Mathf.Cos(_phase); // > 0 while the left leg travels forward (its knee folds to clear the ground)
            var legAmp = Mathf.Lerp(0f, 42f, run);
            var armAmp = Mathf.Lerp(0f, 40f, run);
            var breathe = Mathf.Sin(Time.time * 2.2f) * (1f - run);
            // Idle: a slow look around, knees soft, arms relaxed with a slight bend.
            var idle = 1f - Mathf.Clamp01(speed / 0.5f);
            var lookAround = Mathf.Sin(Time.time * 0.6f) * 14f * idle;
            var elbowBend = -12f - 70f * run;
            var swingL = Mathf.Max(0f, lift) * run;
            var swingR = Mathf.Max(0f, -lift) * run;
            // The trailing foot rolls onto its ball before it leaves the ground.
            var pushL = Mathf.Max(0f, -swing) * run;
            var pushR = Mathf.Max(0f, swing) * run;

            return new Pose
            {
                Hips = E(0f, swing * 6f * run),
                Torso = E(run * 14f + breathe * 1.5f, -swing * 8f * run),
                Head = E(-run * 8f, lookAround),
                ArmL = E(swing * armAmp, 0f, -6f - idle * 4f),
                ArmR = Armed ? E(-26f - swing * armAmp * 0.35f, -10f, 14f) : E(-swing * armAmp, 0f, 6f + idle * 4f),
                ElbowL = E(ShieldInHand ? -22f : elbowBend),
                ElbowR = Armed ? E(-42f) : E(elbowBend),
                WristL = E(-6f),
                WristR = Armed ? E(-12f) : E(-6f),
                FingersL = E(ShieldInHand ? Grip : Relaxed - 30f * run),
                FingersR = E(Armed ? Grip : Relaxed - 30f * run),
                LegL = E(-swing * legAmp - 3f * idle),
                LegR = E(swing * legAmp - 3f * idle),
                KneeL = E(5f * idle + 8f * run + 75f * swingL),
                KneeR = E(5f * idle + 8f * run + 75f * swingR),
                AnkleL = E(28f * pushL),
                AnkleR = E(28f * pushR),
                ToesL = E(-30f * pushL),
                ToesR = E(-30f * pushR),
                FootFreeL = swingL * 0.8f,
                FootFreeR = swingR * 0.8f
            };
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

            var weight = Mathf.Sin(t * Mathf.PI);
            Pose target;
            switch (CurrentAction)
            {
                case ActionKind.SlashLight: target = SlashLight(t, _actionIndex); break;
                case ActionKind.SlashHeavy: target = SlashHeavy(t); break;
                case ActionKind.Thrust: target = Thrust(t); break;
                case ActionKind.Throw: target = Throw(t); break;
                case ActionKind.Shoot: target = Shoot(t, pose); break;
                case ActionKind.Cast: target = Cast(pose); break;
                case ActionKind.Hurt: target = Hurt(); weight = 1f - t; break;
                case ActionKind.Pickup: target = Pickup(); break;
                case ActionKind.Parry: target = Parry(t); break;
                case ActionKind.Land: target = Land(); weight = 1f - t; break;
                case ActionKind.ClimbJump: target = ClimbJump(); break;
                case ActionKind.SwimDash: target = SwimDash(pose); break;
                default: return;
            }

            target.ShowGlider = pose.ShowGlider;
            pose = Mix(pose, target, Mathf.Clamp01(weight));
        }

        /// <summary>
        /// Wind-up with the elbow folded and the wrist cocked, then the arm extends and the wrist snaps through
        /// the cut. Even swings cut sideways from the right, odd swings come overhead.
        /// </summary>
        private static Pose SlashLight(float t, int index)
        {
            var swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 0.5f)); // 0 = wound up, 1 = through
            if (index % 2 == 0)
            {
                return new Pose
                {
                    Hips = E(0f, Mathf.Lerp(28f, -24f, swing)),
                    Torso = E(12f, Mathf.Lerp(30f, -40f, swing)),
                    Head = E(-4f, Mathf.Lerp(-10f, 18f, swing)),
                    ArmL = E(-24f, 0f, -28f),
                    ArmR = E(Mathf.Lerp(-100f, -70f, swing), Mathf.Lerp(55f, -58f, swing), Mathf.Lerp(20f, -10f, swing)),
                    ElbowL = E(-30f),
                    ElbowR = E(Mathf.Lerp(-80f, -8f, swing)),
                    WristR = E(Mathf.Lerp(25f, -20f, swing)),
                    FingersL = E(Grip),
                    FingersR = E(Grip),
                    LegL = E(-26f),
                    LegR = E(16f),
                    KneeL = E(32f),
                    KneeR = E(14f),
                    AnkleR = E(12f),
                    ToesR = E(-15f)
                };
            }

            return new Pose
            {
                Hips = E(0f, 6f),
                Torso = E(Mathf.Lerp(-12f, 26f, swing), -8f),
                Head = E(Mathf.Lerp(-16f, 8f, swing)),
                ArmL = E(-30f, 0f, -26f),
                ArmR = E(Mathf.Lerp(-175f, -60f, swing), -6f, Mathf.Lerp(8f, 0f, swing)),
                ElbowL = E(-35f),
                ElbowR = E(Mathf.Lerp(-70f, -5f, swing)),
                WristR = E(Mathf.Lerp(30f, -25f, swing)),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(-28f),
                LegR = E(18f),
                KneeL = E(Mathf.Lerp(18f, 44f, swing)),
                KneeR = E(14f),
                AnkleR = E(14f),
                ToesR = E(-18f)
            };
        }

        /// <summary>Both arms raised with the elbows folded and the body arched back, then a shouted chop that drops the whole body.</summary>
        private static Pose SlashHeavy(float t)
        {
            var swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.3f) / 0.4f));
            return new Pose
            {
                Hips = E(0f, 10f),
                Torso = E(Mathf.Lerp(-22f, 34f, swing), Mathf.Lerp(12f, -14f, swing)),
                Head = E(Mathf.Lerp(-20f, 12f, swing)),
                Jaw = E(14f * swing),
                ArmL = E(Mathf.Lerp(-165f, -50f, swing), 18f, -14f),
                ArmR = E(Mathf.Lerp(-170f, -52f, swing), -6f, 10f),
                ElbowL = E(Mathf.Lerp(-75f, -10f, swing)),
                ElbowR = E(Mathf.Lerp(-75f, -10f, swing)),
                WristL = E(Mathf.Lerp(30f, -25f, swing)),
                WristR = E(Mathf.Lerp(30f, -25f, swing)),
                FingersL = E(Grip),
                FingersR = E(Grip),
                LegL = E(-36f),
                LegR = E(24f),
                KneeL = E(Mathf.Lerp(28f, 66f, swing)),
                KneeR = E(Mathf.Lerp(10f, 30f, swing)),
                AnkleR = E(18f),
                ToesR = E(-22f)
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
