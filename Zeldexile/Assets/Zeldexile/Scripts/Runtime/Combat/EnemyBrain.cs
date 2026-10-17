using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Characters;
using Zeldexile.Domain;
using Zeldexile.World;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.Combat
{
    /// <summary>
    /// A creature of one family and tier. It carries a real weapon (its attack is the weapon's attack),
    /// notices the player by sight and sound, sleeps at night, telegraphs its swing (readable, perfectly
    /// dodgeable, blockable), drops its weapon and parts when it dies, and comes back on the Blood Moon.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public sealed class EnemyBrain : MonoBehaviour, IKnockbackReceiver, IAnimationSource, IAwareness
    {
        public static readonly List<EnemyBrain> Active = new List<EnemyBrain>();

        public EnemyFamily Family = EnemyFamily.Grunt;
        public EnemyTier Tier = EnemyTier.Red;
        public Transform Target;
        public WeaponInstance Weapon;

        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float aggroRange = 14f;
        [SerializeField] private float viewAngle = 65f;
        [SerializeField] private float hearingRange = 3.5f;
        [SerializeField] private float forgetSeconds = 8f;
        [SerializeField] private float attackRange = 1.8f;
        [SerializeField] private float attackCooldown = 1.6f;
        [SerializeField] private float windupSeconds = 0.6f;

        /// <summary>Raised on death, before the creature hides itself.</summary>
        public static event Action<EnemyBrain> Killed;

        /// <summary>Raised when the wind-up starts (drives the swing animation).</summary>
        public event Action Attacked;

        private CharacterController _controller;
        private Health _health;
        private Health _targetHealth;
        private IDamageInterceptor _targetShield;
        private Vector3 _home;
        private Vector3 _knockback;
        private float _verticalSpeed;
        private float _nextAttack;
        private float _awareUntil;
        private float _stunnedUntil;
        private float _windupEnds = -1f;
        private Renderer[] _renderers;
        private int _attack;

        public Health Health => _health;
        public Vector3 Velocity { get; private set; }
        public float MaxGroundSpeed => moveSpeed * 1.4f;
        public Locomotion Locomotion => _controller.isGrounded ? Locomotion.Grounded : Locomotion.Airborne;
        public bool IsAware => Time.time < _awareUntil;
        public bool IsWindingUp => _windupEnds > 0f;
        public float WindupRemaining => IsWindingUp ? _windupEnds - Time.time : 0f;
        public bool IsStunned => Time.time < _stunnedUntil;
        public bool IsSleeping { get; private set; }
        public string DisplayName => EnemyStats.DisplayName(Family, Tier);
        public int Attack => _attack;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _health = GetComponent<Health>();
            _renderers = GetComponentsInChildren<Renderer>();
            _home = transform.position;
            _health.Died += OnDied;
            _health.Damaged += OnDamaged;
        }

        private void Start()
        {
            if (AtmosphereController.Current != null) AtmosphereController.Current.BloodMoonRose += Revive;
        }

        private void OnDestroy()
        {
            if (AtmosphereController.Current != null) AtmosphereController.Current.BloodMoonRose -= Revive;
        }

        /// <summary>Sets family, tier, health and the carried weapon. Call right after creation.</summary>
        public void Setup(EnemyFamily family, EnemyTier tier, WeaponInstance weapon)
        {
            Family = family;
            Tier = tier;
            Weapon = weapon;
            _attack = weapon != null ? weapon.Attack : EnemyStats.AttackOf(family, tier);
            if (_health == null) _health = GetComponent<Health>();
            _health.SetMax(EnemyStats.HealthOf(family, tier), true);
            var scale = family == EnemyFamily.Brute ? 1.35f : family == EnemyFamily.Agile ? 0.95f : 1.1f;
            transform.localScale = Vector3.one;
            var visual = transform.Find("Visual");
            if (visual != null) visual.localScale = Vector3.one * scale;
            moveSpeed = family == EnemyFamily.Agile ? 4.6f : family == EnemyFamily.Brute ? 2.6f : 3.2f;
            attackRange = family == EnemyFamily.Brute ? 2.4f : weapon != null && weapon.Class == WeaponClass.Spear ? 2.6f : 1.8f;
            windupSeconds = family == EnemyFamily.Brute ? 0.9f : family == EnemyFamily.Agile ? 0.4f : 0.6f;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public void Knockback(Vector3 impulse) => _knockback += impulse;

        /// <summary>A perfect guard or a shock leaves the creature open.</summary>
        public void Stun(float seconds)
        {
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + seconds);
            _windupEnds = -1f;
        }

        private void OnDamaged(Health _, float amount, Element element)
        {
            _awareUntil = Time.time + forgetSeconds;
            IsSleeping = false;
            if (element == Element.Lightning)
            {
                Stun(2f);
                DropWeapon(transform.position + transform.right * 0.8f);
            }
        }

        private void OnDied(Health _)
        {
            Killed?.Invoke(this);
            SetVisible(false);
            _windupEnds = -1f;
            _awareUntil = 0f;
        }

        /// <summary>Blood moon: back at home, at full health, with a fresh weapon.</summary>
        public void Revive()
        {
            if (!_health.IsDead) return;
            transform.position = _home;
            _health.Revive();
            if (Weapon == null) Weapon = new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, EnemyStats.WeaponTierOf(Tier)));
            else if (Weapon.IsBroken) Weapon = new WeaponInstance(Weapon.Definition);
            _attack = Weapon.Attack;
            SetVisible(true);
        }

        /// <summary>Lightning knocks the weapon out of its hand onto the ground.</summary>
        public void DropWeapon(Vector3 where)
        {
            if (Weapon == null) return;
            WeaponPickup.Spawn(Weapon, where);
            Weapon = null;
            _attack = Mathf.Max(1, EnemyStats.AttackOf(Family, Tier) / 4);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in _renderers) r.enabled = visible;
            _controller.enabled = visible;
        }

        private void Update()
        {
            if (_health.IsDead) return;

            var dt = Time.deltaTime;
            var move = Vector3.zero;
            var atmosphere = AtmosphereController.Current;
            var night = atmosphere != null && atmosphere.Clock.IsCreatureNight;
            IsSleeping = night && !IsAware && Family != EnemyFamily.Sentinel;

            if (!_health.IsFrozen && !IsStunned && Target != null)
            {
                if (_targetHealth == null)
                {
                    _targetHealth = Target.GetComponent<Health>();
                    _targetShield = Target.GetComponent<IDamageInterceptor>();
                }

                var toTarget = Target.position - transform.position;
                toTarget.y = 0f;
                var distance = toTarget.magnitude;
                var sameLevel = Mathf.Abs(Target.position.y - transform.position.y) < 4f;

                // Senses: a view cone and hearing. Asleep, both shrink; rain halves hearing.
                var view = IsSleeping ? 0f : viewAngle;
                var hearing = hearingRange * (IsSleeping ? 0.5f : 1f) * (atmosphere != null && atmosphere.IsRaining ? 0.5f : 1f);
                if (sameLevel && distance < aggroRange &&
                    (distance < hearing || (view > 0f && Vector3.Angle(transform.forward, toTarget) < view)))
                {
                    _awareUntil = Time.time + forgetSeconds;
                    IsSleeping = false;
                }

                if (IsWindingUp)
                {
                    // Committed to the swing: turn toward the target, then strike when the wind-up ends.
                    if (distance > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toTarget), 1f - Mathf.Exp(-6f * dt));
                    if (Time.time >= _windupEnds)
                    {
                        _windupEnds = -1f;
                        if (distance <= attackRange * 1.25f && sameLevel && _targetHealth != null) Strike();
                    }
                }
                else if (IsAware && sameLevel)
                {
                    if (distance > attackRange * 0.8f) move = toTarget / distance * moveSpeed;
                    if (distance <= attackRange && Time.time >= _nextAttack)
                    {
                        _nextAttack = Time.time + attackCooldown + windupSeconds;
                        _windupEnds = Time.time + windupSeconds;
                        Attacked?.Invoke();
                    }
                }
                else
                {
                    var toHome = _home - transform.position;
                    toHome.y = 0f;
                    if (toHome.sqrMagnitude > 1f) move = toHome.normalized * moveSpeed * 0.5f;
                }

                if (move.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(move, Vector3.up);
            }

            Velocity = move + _knockback;
            _verticalSpeed = _controller.isGrounded ? -2f : _verticalSpeed - 22f * dt;
            _controller.Move((move + _knockback + Vector3.up * _verticalSpeed) * dt);
            _knockback = Vector3.Lerp(_knockback, Vector3.zero, 1f - Mathf.Exp(-5f * dt));

            if (transform.position.y < -30f)
            {
                _controller.enabled = false;
                transform.position = _home;
                _controller.enabled = true;
            }
        }

        /// <summary>The swing lands: the shield may take it first; the weapon wears like the player's.</summary>
        private void Strike()
        {
            var attack = _attack;
            if (_targetShield != null && _targetShield.TryIntercept(attack, transform.position, out var through, out var perfect))
            {
                if (perfect)
                {
                    Stun(2f);
                    return;
                }

                attack = through;
            }

            if (attack > 0) _targetHealth.TakeDamage(attack, Element.None);

            if (Weapon != null)
            {
                var hit = Weapon.Strike();
                if (hit.Broke)
                {
                    Weapon = null;
                    _attack = Mathf.Max(1, EnemyStats.AttackOf(Family, Tier) / 4);
                }
            }
        }
    }
}
