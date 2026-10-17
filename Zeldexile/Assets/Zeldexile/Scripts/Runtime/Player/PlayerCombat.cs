using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Characters;
using Zeldexile.Combat;
using Zeldexile.Domain;
using Zeldexile.World;

namespace Zeldexile.Player
{
    /// <summary>
    /// Weapons in hand: melee swings that spend durability, throwing, the bow, and the shield
    /// (block and perfect guard). Every rule here is the one creatures follow too.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour, IDamageInterceptor
    {
        public PlayerInputReader Input;
        public PlayerCharacter Character;
        public PlayerMotor Motor;
        public PlayerCaster Caster;
        public PlayerTargeting Targeting;

        [SerializeField] private float swingDelay = 0.12f;
        [SerializeField] private float comboResetSeconds = 0.9f;
        [SerializeField] private float parryWindow = 0.25f;
        [SerializeField] private float arrowSpeed = 45f;
        [SerializeField] private float throwSpeed = 22f;
        [SerializeField] private float unarmedAttack = 1f;

        private static readonly Collider[] Buffer = new Collider[32];

        /// <summary>Raised when an action starts, with its kind (drives the matching animation).</summary>
        public event Action<ActionKind> Acted;

        public bool Guarding => Input.GuardHeld && Character.Shield != null && Character.Shield.IsUsable && Motor.IsGrounded;
        public bool Aiming => Input.BowAimHeld && Character.Bow != null && Character.Bow.IsUsable;
        /// <summary>Weapon drawn: a few seconds after an attack, while guarding, or with a target locked.</summary>
        public bool InCombatStance => Time.time < _stanceUntil || Guarding || (Targeting != null && Targeting.Target != null);
        public int Combo { get; private set; }

        private float _nextAttack;
        private float _lastSwing = -10f;
        private float _pendingHit = -1f;
        private float _parryUntil = -1f;
        private float _nextShot;
        private float _stanceUntil = -10f;

        private void Act(ActionKind kind)
        {
            if (kind != ActionKind.Shoot) _stanceUntil = Time.time + 4f;
            Acted?.Invoke(kind);
        }

        private void Update()
        {
            if (Character == null || Character.Health.IsDead) return;
            if (Time.time - _lastSwing > comboResetSeconds) Combo = 0;

            if (Guarding && Input.JumpPressed) _parryUntil = Time.time + parryWindow;

            if (_pendingHit > 0f && Time.time >= _pendingHit)
            {
                _pendingHit = -1f;
                ResolveSwing();
            }

            if (Motor.Current == PlayerMotor.State.Climbing || Motor.Current == PlayerMotor.State.Swimming) return;

            if (Aiming)
            {
                if (Input.AttackPressed) Shoot();
                return;
            }

            if (Input.ThrowPressed) Throw();
            else if (Input.AttackPressed && !Guarding) Swing();
        }

        // ---------- Melee ----------

        private void Swing()
        {
            if (Time.time < _nextAttack) return;
            var weapon = Character.Weapon;
            if (weapon != null && !weapon.IsUsable) weapon = null;
            var cls = weapon?.Class ?? WeaponClass.OneHanded;
            var cooldown = cls == WeaponClass.TwoHanded ? 0.7f : cls == WeaponClass.Spear ? 0.3f : 0.38f;
            if (Character.CounterActive) cooldown *= 0.3f; // Flurry Rush.
            _nextAttack = Time.time + cooldown;
            _lastSwing = Time.time;
            Combo++;
            _pendingHit = Time.time + swingDelay;
            // The last swing of a one-handed combo (double damage) is the captured sword kick.
            var finisher = cls == WeaponClass.OneHanded && Combo % (weapon?.Definition.ComboHits ?? 3) == 0;
            Act(cls == WeaponClass.TwoHanded ? ActionKind.SlashHeavy : cls == WeaponClass.Spear ? ActionKind.Thrust
                : finisher ? ActionKind.Kick : ActionKind.SlashLight);

            if (Targeting != null && Targeting.Target != null)
            {
                var to = Targeting.Target.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }
        }

        private void ResolveSwing()
        {
            var weapon = Character.Weapon;
            if (weapon != null && !weapon.IsUsable) weapon = null;
            var cls = weapon?.Class ?? WeaponClass.OneHanded;
            var reach = cls == WeaponClass.Spear ? 2.8f : cls == WeaponClass.TwoHanded ? 2.2f : 1.7f;
            var comboHits = weapon?.Definition.ComboHits ?? 3;
            var lastOfCombo = Combo % comboHits == 0;

            var center = transform.position + Vector3.up * 1f + transform.forward * reach * 0.55f;
            var count = Physics.OverlapSphereNonAlloc(center, reach * 0.7f, Buffer, Physics.AllLayers, QueryTriggerInteraction.Collide);
            var targets = new List<Health>();
            for (var i = 0; i < count; i++)
            {
                if (Buffer[i].transform.IsChildOf(transform)) continue;
                var health = Buffer[i].GetComponentInParent<Health>();
                if (health == null || health.IsDead || targets.Contains(health)) continue;
                targets.Add(health);
            }

            if (targets.Count == 0)
            {
                // Swinging at the world: wood and grass still react to the weapon's element later; for now nothing.
                return;
            }

            // One durability per swing that connected, whatever it hit.
            HitResult hit;
            if (weapon != null)
            {
                hit = weapon.Strike(1f, Character.Stats.Get(StatId.AttackPercent), lastOfCombo);
            }
            else
            {
                hit = new HitResult { Damage = DamageRules.Dealt((int)unarmedAttack, 1f, Character.Stats.Get(StatId.AttackPercent)) };
            }

            foreach (var target in targets)
            {
                var multiplier = 1f;
                var sneak = target.TryGetComponent<IAwareness>(out var awareness) && !awareness.IsAware;
                if (sneak) multiplier *= DamageRules.SneakMultiplier;
                if (target.IsFrozen) multiplier *= DamageRules.FrozenMultiplier;
                var damage = Mathf.Max(1f, Mathf.Round(hit.Damage * multiplier));
                var before = target.Current + target.Temporary;
                target.TakeDamage(damage, Element.None);
                var dealt = before - (target.Current + target.Temporary);
                if (dealt > 0f)
                {
                    ImpactResolver.NotifyDamaged(target, dealt, new ElementalImpact { Element = Element.None, Position = center, Owner = gameObject }, sneak || hit.Critical);
                    if (target.TryGetComponent<IKnockbackReceiver>(out var receiver))
                    {
                        var dir = target.transform.position - transform.position;
                        dir.y = 0f;
                        receiver.Knockback(dir.normalized * (cls == WeaponClass.TwoHanded ? 7f : 3.5f) + Vector3.up * 1f);
                    }
                }
            }

            if (hit.Broke && weapon != null) Character.OnWeaponBroke(weapon);
        }

        // ---------- Throw ----------

        private void Throw()
        {
            var weapon = Character.Weapon;
            if (weapon == null || weapon.Definition.Anchor || !weapon.IsUsable) return;
            var hit = weapon.Throw(Character.Stats.Get(StatId.AttackPercent));
            Character.RemoveWeapon(weapon);
            Act(ActionKind.Throw);

            var origin = transform.position + Vector3.up * 1.3f;
            var aim = Caster != null ? Caster.AimPoint(18f * weapon.ThrowRangeMultiplier) : origin + transform.forward * 18f;
            var direction = (aim - origin).normalized;
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);

            var stats = new SkillStats
            {
                Element = Element.None, Shape = SkillShape.Projectile, Damage = hit.Damage, Radius = 0.7f,
                Speed = throwSpeed, Range = 18f * weapon.ThrowRangeMultiplier, Force = 4f, SneakMultiplier = DamageRules.SneakMultiplier
            };
            var projectile = Projectile.Launch(stats, origin + flat.normalized * 0.6f, direction, gameObject);
            projectile.Visual(Palette.WeaponColor(weapon.Class), new Vector3(0.15f, 0.15f, 0.9f));
            // A thrown weapon that hits nothing alive lands on the ground, slightly worn.
            projectile.Ended += (point, hitCreature) =>
            {
                if (hitCreature) return;
                var dropped = new WeaponInstance(weapon.Definition) { Modifier = weapon.Modifier, ModifierValue = weapon.ModifierValue };
                dropped.Wear(dropped.MaxDurability - Mathf.Max(1f, weapon.MaxDurability * 0.3f));
                WeaponPickup.Spawn(dropped, point + Vector3.up * 0.2f);
            };
        }

        // ---------- Bow ----------

        private void Shoot()
        {
            var bow = Character.Bow;
            if (bow == null || !bow.IsUsable || Time.time < _nextShot) return;
            if (!Character.SpendArrow())
            {
                Character.AddLog("Sem flechas.");
                return;
            }

            _nextShot = Time.time + (bow.Modifier == WeaponModifier.QuickShot ? 0.25f : 0.5f);
            Act(ActionKind.Shoot);
            var origin = transform.position + Vector3.up * 1.4f;
            var aim = Caster != null ? Caster.AimPoint(60f) : origin + transform.forward * 60f;
            var direction = (aim - origin).normalized;
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);

            var damage = DamageRules.Dealt(bow.Attack, 1f, Character.Stats.Get(StatId.AttackPercent));
            var stats = new SkillStats
            {
                Element = Element.None, Shape = SkillShape.Projectile, Damage = damage, Radius = 0.5f,
                Speed = arrowSpeed, Range = 60f, Force = 1f, SneakMultiplier = DamageRules.SneakMultiplier
            };
            var arrows = bow.Projectiles;
            for (var i = 0; i < arrows; i++)
            {
                var offset = arrows == 1 ? 0f : Mathf.Lerp(-6f, 6f, i / (float)(arrows - 1));
                var dir = Quaternion.AngleAxis(offset, Vector3.up) * direction;
                Projectile.Launch(stats, origin + flat.normalized * 0.5f, dir, gameObject)
                    .Visual(new Color(0.75f, 0.6f, 0.4f), new Vector3(0.08f, 0.08f, 0.8f));
            }

            // Bows wear one point per shot and break like any weapon.
            bow.Wear(1f);
            if (bow.IsBroken) Character.OnWeaponBroke(bow);
        }

        // ---------- Shield ----------

        /// <summary>
        /// A creature's hit arrives here first. With the shield up and the attacker in front, a perfect
        /// guard (jump pressed just before) deflects everything and stuns; a normal block absorbs up to
        /// the shield's guard value and costs durability.
        /// </summary>
        public bool TryIntercept(int attack, Vector3 from, out int through, out bool perfect)
        {
            through = attack;
            perfect = false;
            if (!Guarding) return false;
            var to = from - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, to.normalized) < 0.3f) return false;

            if (Time.time <= _parryUntil)
            {
                perfect = true;
                through = 0;
                _parryUntil = -1f;
                GameFeel.HitStop(0.08f);
                GameFeel.AddTrauma(0.15f);
                Character.AddLog("Defesa perfeita!");
                Act(ActionKind.Parry);
                return true;
            }

            var shield = Character.Shield;
            through = shield.Block(attack, out var broke);
            GameFeel.AddTrauma(0.1f);
            if (broke) Character.OnWeaponBroke(shield);
            return true;
        }
    }
}
