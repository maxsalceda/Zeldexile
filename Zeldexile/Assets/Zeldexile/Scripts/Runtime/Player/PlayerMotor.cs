using UnityEngine;
using Zeldexile.Characters;
using Zeldexile.Combat;
using Zeldexile.Domain;
using Zeldexile.World;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.Player
{
    /// <summary>
    /// Traversal: walk, sprint, jump, climb any steep surface, glide, swim, dodge. Stamina (1000 units a
    /// wheel) limits sprint, climb, glide, swim and aerial aiming, and only refills on the ground.
    /// Climbing up costs full rate, sideways or down half, hanging still is free; rain makes walls slippery.
    /// Falls hurt from 6 m and kill from 30 m, unless the glider is open or the landing is deep water.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour, IAnimationSource
    {
        public enum State
        {
            Grounded,
            Airborne,
            Climbing,
            Gliding,
            Swimming
        }

        public PlayerInputReader Input;
        public ThirdPersonCamera CameraRig;
        public PlayerCharacter Character;
        public PlayerTargeting Targeting;

        [Header("Ground")]
        /// <summary>The pace of the hero's captured walk (Meshy clip, about 1.5 m/s): the feet stay planted.</summary>
        [SerializeField] private float walkSpeed = 1.6f;
        /// <summary>Steering speed in the air: jumps keep the reach they had before the walk slowed down.</summary>
        [SerializeField] private float airSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.3f;
        [SerializeField] private float gravity = -22f;

        [Header("Climb")]
        [SerializeField] private float climbSpeed = 2f;
        [SerializeField] private float maxClimbNormalY = 0.55f;

        [Header("Glide")]
        [SerializeField] private float glideSpeed = 7f;
        [SerializeField] private float glideFallSpeed = 1.8f;

        [Header("Stamina per second (wheel = 1000)")]
        [SerializeField] private float sprintCost = 110f;
        [SerializeField] private float climbCost = 95f;
        [SerializeField] private float climbIdleCost = 0f;
        [SerializeField] private float rainClimbFactor = 1.2f;
        [SerializeField] private float rainSlipStamina = 100f;
        [SerializeField] private float focusCost = 250f;
        [SerializeField] private float glideCost = 25f;
        [SerializeField] private float climbJumpCost = 150f;
        [SerializeField] private float swimCost = 130f;
        [SerializeField] private float swimDashCost = 200f;

        [Header("Swim")]
        [SerializeField] private float swimSpeed = 2.4f;
        [SerializeField] private float drownDamage = 4f;

        [Header("Dodge")]
        [SerializeField] private float dodgeSpeed = 9f;
        [SerializeField] private float dodgeInvulnerable = 0.35f;
        [SerializeField] private float perfectWindow = 0.35f;
        [SerializeField] private float counterSeconds = 2.2f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private Vector3 _external;
        private Vector3 _wallNormal;
        private float _airTime;
        private float _regrabCooldown;
        private Vector3 _spawnPoint;
        private float _nextSlip;
        private float _dodgeCooldown;
        private float _fallStartY;
        private bool _fallTracked;
        private float _swimDashUntil;

        /// <summary>True while time is slowed for aiming in the air.</summary>
        public bool Focusing { get; private set; }

        /// <summary>Raised after a perfect dodge (the HUD and the combat react to it).</summary>
        public event System.Action PerfectDodged;
        /// <summary>Raised on touching down, with the height fallen in meters.</summary>
        public event System.Action<float> Landed;
        public event System.Action ClimbJumped;
        public event System.Action SwimDashed;

        public State Current { get; private set; } = State.Grounded;
        public Vector3 Velocity => _velocity;
        public float MaxGroundSpeed => sprintSpeed;
        public bool IsGrounded => Current == State.Grounded;

        public Locomotion Locomotion
        {
            get
            {
                switch (Current)
                {
                    case State.Airborne: return Locomotion.Airborne;
                    case State.Climbing: return Locomotion.Climbing;
                    case State.Gliding: return Locomotion.Gliding;
                    case State.Swimming: return Locomotion.Swimming;
                    default: return Locomotion.Grounded;
                }
            }
        }

        private StaminaModel Stamina => Character.Stamina;
        private StatBlock Stats => Character.Stats;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _spawnPoint = transform.position;
        }

        public void SetSpawn(Vector3 point) => _spawnPoint = point;

        public void AddImpulse(Vector3 impulse) => _external += impulse;

        public void Respawn()
        {
            _controller.enabled = false;
            transform.position = _spawnPoint;
            _controller.enabled = true;
            _velocity = Vector3.zero;
            _external = Vector3.zero;
            _fallTracked = false;
            Current = State.Airborne;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (Current == State.Grounded || Current == State.Swimming) Stamina.Tick(dt);
            _regrabCooldown -= dt;
            _dodgeCooldown -= dt;
            UpdateFocus();

            var moveDir = CameraRelative(Input.Move);
            if (Current != State.Swimming && Current != State.Climbing)
            {
                var water = WaterVolume.At(transform.position + Vector3.up * 0.5f);
                if (water != null && transform.position.y < water.SurfaceY - 1.2f)
                {
                    _fallTracked = false; // Deep water takes any fall.
                    Current = State.Swimming;
                }
            }

            switch (Current)
            {
                case State.Grounded: TickGrounded(moveDir, dt); break;
                case State.Airborne: TickAirborne(moveDir, dt); break;
                case State.Climbing: TickClimbing(dt); break;
                case State.Gliding: TickGliding(moveDir, dt); break;
                case State.Swimming: TickSwimming(moveDir, dt); break;
            }

            _external = Vector3.Lerp(_external, Vector3.zero, 1f - Mathf.Exp(-3f * dt));
            if (transform.position.y < -30f) Respawn();
        }

        private Vector3 CameraRelative(Vector2 input)
        {
            var yaw = CameraRig != null ? CameraRig.Yaw : 0f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            return rot * new Vector3(input.x, 0f, input.y);
        }

        private float SpeedFactor()
        {
            var factor = Stats.Multiplier(StatId.MoveSpeedPercent);
            var atmosphere = AtmosphereController.Current;
            if (atmosphere != null && atmosphere.Clock.IsCreatureNight) factor *= Stats.Multiplier(StatId.NightSpeedPercent);
            if (Stamina.Exhausted) factor *= 0.85f;
            return factor;
        }

        private void TickGrounded(Vector3 moveDir, float dt)
        {
            if (!_controller.isGrounded)
            {
                _airTime += dt;
                if (_airTime > 0.15f)
                {
                    BeginFall();
                    Current = State.Airborne;
                    return;
                }
            }
            else
            {
                _airTime = 0f;
            }

            var speed = walkSpeed;
            if (Input.Sprint && moveDir.sqrMagnitude > 0.01f && Stamina.Drain(sprintCost * dt))
                speed = sprintSpeed;
            speed *= SpeedFactor();

            var horizontal = moveDir * speed;
            _velocity = new Vector3(horizontal.x, -2f, horizontal.z);

            var locked = Targeting != null && Targeting.Target != null;
            if (Input.DodgePressed || (locked && Input.JumpPressed && !Input.GuardHeld))
            {
                Dodge(moveDir, locked);
            }
            else if (Input.JumpPressed && !Input.GuardHeld)
            {
                _velocity.y = Mathf.Sqrt(2f * jumpHeight * -gravity);
                BeginFall();
                Current = State.Airborne;
                _airTime = 0f;
            }
            else if (TryStartClimb(moveDir))
            {
                return;
            }

            if (locked)
            {
                // Strafe around the target, always facing it.
                var to = Targeting.Target.transform.position - transform.position;
                to.y = 0f;
                Face(to.normalized, dt);
            }
            else
            {
                Face(moveDir, dt);
            }

            _controller.Move((_velocity + _external) * dt);
        }

        /// <summary>
        /// Side hop (or back flip with no input). Dodging just before a telegraphed swing lands is a
        /// perfect dodge: time slows and the Flurry Rush window opens.
        /// </summary>
        private void Dodge(Vector3 moveDir, bool locked)
        {
            if (_dodgeCooldown > 0f) return;
            _dodgeCooldown = 0.45f;
            var dir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : -transform.forward;
            _external += dir * dodgeSpeed;
            _velocity.y = 3.5f;
            BeginFall();
            Current = State.Airborne;
            _airTime = 0f;
            Character.Health.MakeInvulnerable(dodgeInvulnerable);

            foreach (var enemy in EnemyBrain.Active)
            {
                if (!enemy.IsWindingUp || enemy.WindupRemaining > perfectWindow) continue;
                if ((enemy.transform.position - transform.position).sqrMagnitude > 4.5f * 4.5f) continue;
                Character.Health.MakeInvulnerable(1f);
                Character.StartCounter(counterSeconds);
                GameFeel.SlowMotion(0.25f, counterSeconds * 0.6f);
                PerfectDodged?.Invoke();
                break;
            }
        }

        /// <summary>Drawing the bow in the air slows time while stamina lasts.</summary>
        private void UpdateFocus()
        {
            var airborne = Current == State.Airborne || Current == State.Gliding;
            var wants = airborne && Input.FocusHeld && Character.Bow != null && Stamina.CanSpend && HeightAboveGround() > 1.5f;
            if (wants && !Stamina.Drain(focusCost * Time.unscaledDeltaTime)) wants = false;
            if (wants != Focusing)
            {
                Focusing = wants;
                GameFeel.SetFocus(wants);
            }
        }

        private float HeightAboveGround()
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 50f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.distance - 0.2f
                : 50f;
        }

        private void BeginFall()
        {
            if (_fallTracked) return;
            _fallTracked = true;
            _fallStartY = transform.position.y;
        }

        private void TickAirborne(Vector3 moveDir, float dt)
        {
            _airTime += dt;
            _velocity.y += gravity * dt;
            if (_fallTracked) _fallStartY = Mathf.Max(_fallStartY, transform.position.y);

            var horizontal = new Vector3(_velocity.x, 0f, _velocity.z);
            var target = moveDir * Mathf.Max(airSpeed, horizontal.magnitude);
            horizontal = Vector3.MoveTowards(horizontal, target, 10f * dt);
            _velocity = new Vector3(horizontal.x, _velocity.y, horizontal.z);

            if (Input.JumpPressed && _airTime > 0.1f && Stamina.CanSpend)
            {
                Current = State.Gliding;
                return;
            }

            if (TryStartClimb(moveDir)) return;

            Face(moveDir, dt);
            var flags = _controller.Move((_velocity + _external) * dt);
            if ((flags & CollisionFlags.Below) != 0 && _velocity.y < 0f) Land();
        }

        private void TickGliding(Vector3 moveDir, float dt)
        {
            var lift = UpdraftZone.LiftAt(transform.position);
            _velocity.y = lift > 0f
                ? Mathf.MoveTowards(_velocity.y, lift, 18f * dt)
                : Mathf.MoveTowards(_velocity.y, -glideFallSpeed, 12f * dt);

            var forward = moveDir.sqrMagnitude > 0.01f ? moveDir : transform.forward * 0.6f;
            var horizontal = Vector3.MoveTowards(new Vector3(_velocity.x, 0f, _velocity.z), forward * glideSpeed, 6f * dt);
            _velocity = new Vector3(horizontal.x, _velocity.y, horizontal.z);

            // Gliding resets the fall: landing under an open glider never hurts.
            _fallTracked = false;

            var cost = glideCost * (lift > 0f ? 0.5f : 1f);
            if (Input.JumpPressed || !Stamina.Drain(cost * dt))
            {
                BeginFall();
                Current = State.Airborne;
                _airTime = 1f;
                return;
            }

            if (TryStartClimb(moveDir)) return;

            Face(moveDir, dt);
            var flags = _controller.Move((_velocity + _external) * dt);
            if ((flags & CollisionFlags.Below) != 0) Land();
        }

        private void TickClimbing(float dt)
        {
            var chest = transform.position + Vector3.up * 1.1f;
            var feet = transform.position + Vector3.up * 0.3f;
            var reach = _controller.radius + 0.6f;
            _fallTracked = false;

            if (!FindWall(chest, -_wallNormal, reach, out var hit))
            {
                // No wall at chest height: either a ledge to pull up on, or the wall ended.
                if (FindWall(feet, -_wallNormal, reach, out _)) Vault();
                else Detach(Vector3.zero);
                return;
            }

            _wallNormal = hit.normal;
            var move = Input.Move;
            var right = Vector3.Cross(Vector3.up, -_wallNormal).normalized;
            var speed = climbSpeed * Stats.Multiplier(StatId.ClimbSpeedPercent);
            var moving = move.sqrMagnitude > 0.01f;

            var direction = move.y > 0.1f ? 1f : 0.5f;
            var raining = AtmosphereController.Current != null && AtmosphereController.Current.IsRaining;
            var cost = (moving ? climbCost * direction : climbIdleCost) * Stats.CostFactor(StatId.ClimbCostReduction) *
                       (raining ? rainClimbFactor : 1f);
            if (!Stamina.Drain(cost * dt))
            {
                BeginFall();
                Detach(_wallNormal * 1.5f);
                return;
            }

            // Rain: wet rock makes the climber slip down now and then while going up.
            if (raining && move.y > 0.1f && Time.time >= _nextSlip)
            {
                if (_nextSlip > 0f)
                {
                    _controller.Move(Vector3.down * 1.1f);
                    Stamina.Drain(rainSlipStamina);
                    GameFeel.AddTrauma(0.2f);
                }

                _nextSlip = Time.time + Random.Range(2f, 4f);
            }
            else if (!raining || move.y <= 0.1f)
            {
                _nextSlip = 0f;
            }

            if (Input.JumpPressed)
            {
                if (moving && move.y > 0.5f && Stamina.Drain(climbJumpCost * Stats.CostFactor(StatId.ClimbCostReduction)))
                {
                    // Climb jump: a fast burst upward, still on the wall.
                    _controller.Move(Vector3.up * 1.4f);
                    ClimbJumped?.Invoke();
                    return;
                }

                BeginFall();
                Detach(_wallNormal * 4f + Vector3.up * 3f);
                return;
            }

            if (move.y < -0.1f && _controller.isGrounded)
            {
                Current = State.Grounded;
                _regrabCooldown = 0.4f;
                return;
            }

            _velocity = (Vector3.up * move.y + right * move.x) * speed;
            transform.rotation = Quaternion.LookRotation(-_wallNormal, Vector3.up);
            _controller.Move((_velocity - _wallNormal * 1.5f) * dt);
        }

        private void TickSwimming(Vector3 moveDir, float dt)
        {
            var water = WaterVolume.At(transform.position + Vector3.up * 1f);
            if (water == null)
            {
                Current = State.Airborne;
                return;
            }

            var moving = moveDir.sqrMagnitude > 0.01f;
            if (moving && !Stamina.Drain(swimCost * dt))
            {
                // Out of stamina in deep water: the character goes under and wakes up at the spawn.
                Character.Health.TakeDamage(drownDamage, Element.None, fixedDamage: true);
                Character.AddLog("Você afundou. Voltando à margem.");
                Respawn();
                return;
            }

            // Swim dash: a burst forward for a one-off cost.
            var speed = swimSpeed * Stats.Multiplier(StatId.SwimSpeedPercent);
            if (moving && Input.Sprint && Time.time >= _swimDashUntil &&
                Stamina.Drain(swimDashCost * Stats.CostFactor(StatId.SwimCostReduction)))
            {
                _swimDashUntil = Time.time + 0.7f;
                SwimDashed?.Invoke();
            }
            if (Time.time < _swimDashUntil) speed *= 2.2f;

            if (TryStartClimb(moveDir)) return;

            var floatY = water.SurfaceY - 1.4f;
            var vertical = Mathf.Clamp((floatY - transform.position.y) * 4f, -3f, 3f);
            _velocity = moveDir * speed + Vector3.up * vertical;
            Face(moveDir, dt);
            _controller.Move((_velocity + _external) * dt);
        }

        private bool TryStartClimb(Vector3 moveDir)
        {
            if (_regrabCooldown > 0f || moveDir.sqrMagnitude < 0.01f || !Stamina.CanSpend) return false;
            var chest = transform.position + Vector3.up * 1.1f;
            if (!FindWall(chest, moveDir.normalized, _controller.radius + 0.25f, out var hit)) return false;
            if (Vector3.Dot(moveDir.normalized, -hit.normal) < 0.5f) return false;

            _wallNormal = hit.normal;
            _velocity = Vector3.zero;
            _fallTracked = false;
            Current = State.Climbing;
            return true;
        }

        private bool FindWall(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                hit = default;
                return false;
            }

            return Physics.Raycast(origin, direction.normalized, out hit, distance, Physics.DefaultRaycastLayers,
                       QueryTriggerInteraction.Ignore) &&
                   Mathf.Abs(hit.normal.y) < maxClimbNormalY;
        }

        private void Vault()
        {
            _controller.Move(Vector3.up * 1.3f);
            _controller.Move(-_wallNormal * 0.8f);
            _velocity = Vector3.zero;
            Current = State.Airborne;
            _airTime = 0f;
            _regrabCooldown = 0.3f;
        }

        private void Detach(Vector3 push)
        {
            _velocity = push;
            Current = State.Airborne;
            _airTime = 0.2f;
            _regrabCooldown = 0.5f;
        }

        private void Land()
        {
            _velocity.y = -2f;
            _airTime = 0f;
            Current = State.Grounded;
            if (!_fallTracked) return;
            _fallTracked = false;
            var meters = _fallStartY - transform.position.y;
            Landed?.Invoke(meters);
            var damage = DamageRules.FallDamage(meters);
            if (damage <= 0) return;
            GameFeel.AddTrauma(0.4f);
            if (damage == int.MaxValue)
            {
                Character.AddLog("Queda fatal.");
                Character.Health.TakeDamage(9999f, Element.None, fixedDamage: true);
                return;
            }

            Character.AddLog($"Queda de {meters:0} m.");
            Character.Health.TakeDamage(damage, Element.None, fixedDamage: true);
        }

        private void Face(Vector3 moveDir, float dt)
        {
            if (moveDir.sqrMagnitude < 0.01f) return;
            var target = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-12f * dt));
        }
    }
}
