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
    /// Procedural poses for the rig: walk/run cycle, jump, climb, glide, swim, plus held stances (shield up,
    /// bow drawn, kneeling at a pot or statue) and one-shot actions (each weapon class swings its own way,
    /// throw, shoot, cast, flinch, pick up, parry, hard landing, climb jump, swim dash).
    /// Joint convention: rotating +X swings a limb backward, -X forward; +Y turns right; +Z tilts.
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
        /// <summary>Sword in hand: the right arm stays raised so the blade never drags on the ground.</summary>
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

        public ActionKind CurrentAction { get; private set; }
        public float ActionProgress => _actionDuration <= 0f ? 1f : Mathf.Clamp01((Time.time - _actionStart) / _actionDuration);
        public bool ActionPlaying => CurrentAction != ActionKind.None && ActionProgress < 1f;

        private float _phase;
        private float _actionStart = -10f;
        private float _actionDuration;
        private int _actionIndex;
        private float _heldProgress = -1f;

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

            Pose pose;
            switch (Source.Locomotion)
            {
                case Locomotion.Airborne: pose = Airborne(velocity.y); break;
                case Locomotion.Climbing: pose = Climbing(velocity, dt); break;
                case Locomotion.Gliding: pose = Gliding(); break;
                case Locomotion.Swimming: pose = Swimming(speed, dt); break;
                default: pose = Ground(speed, run, dt); break;
            }

            if (Source.Locomotion == Locomotion.Grounded)
            {
                if (Kneeling) pose = Mix(pose, Kneel(), 1f);
                else if (Aiming) pose = Mix(pose, Aim(run), 1f);
                else if (Guarding) pose = Mix(pose, Guard(run), 1f);
            }

            ApplyAction(ref pose);

            var k = 1f - Mathf.Exp(-blend * dt);
            Blend(Rig.Hips, pose.Hips, k);
            Blend(Rig.Torso, pose.Torso, k);
            Blend(Rig.Head, pose.Head, k);
            Blend(Rig.ArmL, pose.ArmL, k);
            Blend(Rig.ArmR, pose.ArmR, k);
            Blend(Rig.LegL, pose.LegL, k);
            Blend(Rig.LegR, pose.LegR, k);

            var hipsPos = new Vector3(0f, Rig.HipsHeight + pose.HipsOffsetY, pose.HipsOffsetZ);
            Rig.Hips.localPosition = Vector3.Lerp(Rig.Hips.localPosition, hipsPos, k);

            if (Rig.Glider.gameObject.activeSelf != pose.ShowGlider) Rig.Glider.gameObject.SetActive(pose.ShowGlider);
        }

        public struct Pose
        {
            public Quaternion Hips, Torso, Head, ArmL, ArmR, LegL, LegR;
            public float HipsOffsetY, HipsOffsetZ;
            public bool ShowGlider;
        }

        private static Quaternion E(float x, float y = 0f, float z = 0f) => Quaternion.Euler(x, y, z);

        private static Pose Mix(Pose a, Pose b, float t)
        {
            return new Pose
            {
                Hips = Quaternion.Slerp(a.Hips, b.Hips, t),
                Torso = Quaternion.Slerp(a.Torso, b.Torso, t),
                Head = Quaternion.Slerp(a.Head, b.Head, t),
                ArmL = Quaternion.Slerp(a.ArmL, b.ArmL, t),
                ArmR = Quaternion.Slerp(a.ArmR, b.ArmR, t),
                LegL = Quaternion.Slerp(a.LegL, b.LegL, t),
                LegR = Quaternion.Slerp(a.LegR, b.LegR, t),
                HipsOffsetY = Mathf.Lerp(a.HipsOffsetY, b.HipsOffsetY, t),
                HipsOffsetZ = Mathf.Lerp(a.HipsOffsetZ, b.HipsOffsetZ, t),
                ShowGlider = a.ShowGlider
            };
        }

        // ---------- locomotion ----------

        private Pose Ground(float speed, float run, float dt)
        {
            if (speed > 0.1f) _phase += speed / strideLength * Mathf.PI * dt;
            var swing = Mathf.Sin(_phase);
            var legAmp = Mathf.Lerp(0f, 50f, run);
            var armAmp = Mathf.Lerp(0f, 45f, run);
            var breathe = Mathf.Sin(Time.time * 2.2f) * (1f - run);
            // Idle: a slow look around and a relaxed, slightly open stance.
            var idle = 1f - Mathf.Clamp01(speed / 0.5f);
            var lookAround = Mathf.Sin(Time.time * 0.6f) * 14f * idle;

            var armR = Armed
                ? E(-42f - swing * armAmp * 0.35f, -12f, 16f)
                : E(-swing * armAmp, 0f, 6f + idle * 4f);

            return new Pose
            {
                Hips = E(0f, swing * 6f * run),
                Torso = E(run * 14f + breathe * 1.5f, -swing * 8f * run),
                Head = E(-run * 8f, lookAround),
                ArmL = E(swing * armAmp, 0f, -6f - idle * 4f),
                ArmR = armR,
                LegL = E(-swing * legAmp),
                LegR = E(swing * legAmp),
                HipsOffsetY = -Mathf.Abs(Mathf.Cos(_phase)) * 0.06f * run + breathe * 0.005f
            };
        }

        private static Pose Airborne(float verticalSpeed)
        {
            var rising = Mathf.Clamp01(verticalSpeed / 6f);
            return new Pose
            {
                Hips = Quaternion.identity,
                Torso = E(8f),
                Head = E(-6f),
                ArmL = E(-30f - rising * 40f, 0f, -35f),
                ArmR = E(-30f - rising * 40f, 0f, 35f),
                LegL = E(-45f),
                LegR = E(10f),
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
                Hips = Quaternion.identity,
                Torso = E(-6f),
                Head = E(-25f),
                ArmL = E(-160f + s * 25f, 0f, -12f),
                ArmR = E(-160f - s * 25f, 0f, 12f),
                LegL = E(-35f - s * 25f),
                LegR = E(-35f + s * 25f),
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
                ArmL = E(-175f, 0f, -14f),
                ArmR = E(-175f, 0f, 14f),
                LegL = E(18f + sway),
                LegR = E(24f - sway),
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
                Torso = E(0f),
                Head = E(-60f),
                ArmL = E(-90f + s * 70f, 0f, -20f),
                ArmR = E(-90f - s * 70f, 0f, 20f),
                LegL = E(s * 25f),
                LegR = E(-s * 25f),
                HipsOffsetY = -0.4f
            };
        }

        // ---------- held stances ----------

        /// <summary>Shield forward on the left arm, sword arm ready, knees bent, weight low.</summary>
        private Pose Guard(float run)
        {
            var step = Mathf.Sin(_phase) * 20f * run;
            return new Pose
            {
                Hips = E(0f, 12f),
                Torso = E(10f, -14f),
                Head = E(-6f, 10f),
                ArmL = ShieldGuardArm,
                ArmR = E(-26f, -10f, 34f),
                LegL = E(-22f + step),
                LegR = E(-22f - step),
                HipsOffsetY = -0.08f
            };
        }

        /// <summary>Bow arm straight ahead, string hand drawn to the cheek, body turned sideways.</summary>
        private Pose Aim(float run)
        {
            var step = Mathf.Sin(_phase) * 18f * run;
            return new Pose
            {
                Hips = E(0f, 20f),
                Torso = E(4f, -22f),
                Head = E(-4f, 18f, -6f),
                ArmL = E(-92f, -6f, 0f),
                ArmR = E(-100f, -42f, 8f),
                LegL = E(-8f + step),
                LegR = E(-8f - step),
                HipsOffsetY = -0.03f
            };
        }

        /// <summary>One knee down, hands forward over the pot or in prayer.</summary>
        private static Pose Kneel()
        {
            var stir = Mathf.Sin(Time.time * 2.5f) * 10f;
            return new Pose
            {
                Hips = Quaternion.identity,
                Torso = E(18f),
                Head = E(22f),
                ArmL = E(-62f + stir, 0f, -18f),
                ArmR = E(-62f - stir, 0f, 18f),
                LegL = E(-95f),
                LegR = E(-18f),
                HipsOffsetY = -0.48f,
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

        /// <summary>Wind-up then follow-through. Even swings cut sideways from the right, odd swings come overhead.</summary>
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
                    ArmR = E(Mathf.Lerp(-110f, -62f, swing), Mathf.Lerp(55f, -58f, swing), Mathf.Lerp(20f, -10f, swing)),
                    LegL = E(-18f),
                    LegR = E(14f),
                    HipsOffsetY = -0.05f
                };
            }

            return new Pose
            {
                Hips = E(0f, 6f),
                Torso = E(Mathf.Lerp(-12f, 26f, swing), -8f),
                Head = E(Mathf.Lerp(-16f, 8f, swing)),
                ArmL = E(-30f, 0f, -26f),
                ArmR = E(Mathf.Lerp(-178f, -48f, swing), -6f, Mathf.Lerp(8f, 0f, swing)),
                LegL = E(-22f),
                LegR = E(18f),
                HipsOffsetY = Mathf.Lerp(0f, -0.08f, swing)
            };
        }

        /// <summary>Both arms raised, body arched back, then a chop that pulls the whole body down.</summary>
        private static Pose SlashHeavy(float t)
        {
            var swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.3f) / 0.4f));
            return new Pose
            {
                Hips = E(0f, 10f),
                Torso = E(Mathf.Lerp(-22f, 34f, swing), Mathf.Lerp(12f, -14f, swing)),
                Head = E(Mathf.Lerp(-20f, 12f, swing)),
                ArmL = E(Mathf.Lerp(-172f, -40f, swing), 18f, -14f),
                ArmR = E(Mathf.Lerp(-178f, -42f, swing), -6f, 10f),
                LegL = E(-30f),
                LegR = E(22f),
                HipsOffsetY = Mathf.Lerp(0.02f, -0.16f, swing)
            };
        }

        /// <summary>Spear pulled to the hip, then driven straight forward with a step.</summary>
        private static Pose Thrust(float t)
        {
            var push = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.1f) / 0.45f));
            return new Pose
            {
                Hips = E(0f, Mathf.Lerp(30f, -12f, push)),
                Torso = E(Mathf.Lerp(0f, 18f, push), Mathf.Lerp(26f, -22f, push)),
                Head = E(0f, Mathf.Lerp(-14f, 10f, push)),
                ArmL = E(-48f, 20f, -22f),
                ArmR = E(Mathf.Lerp(-30f, -96f, push), Mathf.Lerp(28f, -4f, push), 0f),
                LegL = E(Mathf.Lerp(-10f, -46f, push)),
                LegR = E(Mathf.Lerp(0f, 26f, push)),
                HipsOffsetY = Mathf.Lerp(0f, -0.1f, push),
                HipsOffsetZ = Mathf.Lerp(0f, 0.12f, push)
            };
        }

        /// <summary>Arm back over the shoulder, then whipped forward with a twist of the hips.</summary>
        private static Pose Throw(float t)
        {
            var release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.3f) / 0.4f));
            return new Pose
            {
                Hips = E(0f, Mathf.Lerp(34f, -20f, release)),
                Torso = E(Mathf.Lerp(-10f, 24f, release), Mathf.Lerp(36f, -30f, release)),
                Head = E(-6f, Mathf.Lerp(-20f, 12f, release)),
                ArmL = E(-70f, 10f, -20f),
                ArmR = E(Mathf.Lerp(50f, -118f, release), Mathf.Lerp(30f, -10f, release), 14f),
                LegL = E(-30f),
                LegR = E(24f),
                HipsOffsetY = -0.05f
            };
        }

        /// <summary>From the aim: the string hand snaps forward on release.</summary>
        private static Pose Shoot(float t, Pose current)
        {
            var kick = Mathf.Clamp01(t * 2f);
            var pose = current;
            pose.ArmR = E(Mathf.Lerp(-100f, -82f, kick), Mathf.Lerp(-42f, -16f, kick), 6f);
            pose.Head = E(-2f, 16f, -4f);
            return pose;
        }

        /// <summary>Right arm thrust forward with an open hand, shoulders turned into it.</summary>
        private static Pose Cast(Pose current)
        {
            var pose = current;
            pose.ArmR = E(-95f, -10f, 5f);
            pose.Torso = current.Torso * E(0f, -20f, 0f);
            return pose;
        }

        /// <summary>Flinch: chest back, head thrown, arms out.</summary>
        private static Pose Hurt() => new Pose
        {
            Hips = E(-6f),
            Torso = E(-22f, 8f),
            Head = E(-24f, -10f),
            ArmL = E(-40f, 0f, -45f),
            ArmR = E(-40f, 0f, 45f),
            LegL = E(14f),
            LegR = E(-26f),
            HipsOffsetY = -0.04f,
            HipsOffsetZ = -0.08f
        };

        /// <summary>Crouch and reach down with the right hand.</summary>
        private static Pose Pickup() => new Pose
        {
            Hips = E(0f, 8f),
            Torso = E(38f, -10f),
            Head = E(18f),
            ArmL = E(-20f, 0f, -16f),
            ArmR = E(-54f, -14f, 6f),
            LegL = E(-70f),
            LegR = E(-62f),
            HipsOffsetY = -0.36f,
            HipsOffsetZ = 0.06f
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
                ArmL = E(-96f, 30f, -6f),
                ArmR = E(Mathf.Lerp(-30f, -140f, counter), 10f, 24f),
                LegL = E(-30f),
                LegR = E(22f),
                HipsOffsetY = -0.12f
            };
        }

        /// <summary>Squat on landing.</summary>
        private static Pose Land() => new Pose
        {
            Hips = Quaternion.identity,
            Torso = E(24f),
            Head = E(-6f),
            ArmL = E(-20f, 0f, -40f),
            ArmR = E(-20f, 0f, 40f),
            LegL = E(-62f),
            LegR = E(-58f),
            HipsOffsetY = -0.3f
        };

        /// <summary>Arms thrown up the wall, legs tucked.</summary>
        private static Pose ClimbJump() => new Pose
        {
            Hips = Quaternion.identity,
            Torso = E(-10f),
            Head = E(-30f),
            ArmL = E(-178f, 0f, -8f),
            ArmR = E(-178f, 0f, 8f),
            LegL = E(-70f),
            LegR = E(-70f),
            HipsOffsetZ = 0.1f
        };

        /// <summary>Arms straight ahead, legs together: a burst through the water.</summary>
        private static Pose SwimDash(Pose current)
        {
            var pose = current;
            pose.ArmL = E(-170f, 0f, -6f);
            pose.ArmR = E(-170f, 0f, 6f);
            pose.LegL = E(4f);
            pose.LegR = E(-4f);
            pose.Head = E(-70f);
            return pose;
        }

        private static void Blend(Transform joint, Quaternion target, float k) =>
            joint.localRotation = Quaternion.Slerp(joint.localRotation, target, k);
    }
}
