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
        /// <summary>A tap of the right button is the heavy attack; held this long with a bow, it draws the bow instead.</summary>
        private const float AimHoldSeconds = 0.22f;

        public bool Aiming => BowUsable && Input.BowAimHeld && _aimHeldSince >= 0f && Time.time - _aimHeldSince >= AimHoldSeconds;
        private bool BowUsable => Character.Bow != null && Character.Bow.IsUsable;

        /// <summary>Seconds after a heavy attack starts at which each of its strikes lands (set by the world from the animator).</summary>
        public System.Func<float[]> HeavyHitTimes;
        /// <summary>How long the heavy attack animation runs, in seconds (set with <see cref="HeavyHitTimes"/>).</summary>
        public System.Func<float> HeavyDuration;
        /// <summary>Weapon drawn: a few seconds after an attack, while guarding, or with a target locked.</summary>
        public bool InCombatStance => Time.time < _stanceUntil || Guarding || (Targeting != null && Targeting.Target != null);
        public int Combo { get; private set; }

        // ---------- Sword on the waist and the basic combo (bone-for-bone clips) ----------

        /// <summary>The hero's clips; with a draw clip the sword lives in the waist sheath and is drawn and put away for real.</summary>
        public CharacterRig Rig;
        /// <summary>Seconds into the draw clip when the hand pulls the hilt out of the sheath, and into the sheathe clip when it lets go.</summary>
        public System.Func<float> DrawGrabTime, SheatheReleaseTime;
        /// <summary>Seconds with the sword out and nothing happening before it goes back in the sheath by itself.</summary>
        [SerializeField] private float sheatheAfterSeconds = 6f;

        public enum SwordState { Sheathed, Drawing, Drawn, Sheathing }
        public enum ClipAction { None, Draw, Sheathe, Attack1, Attack2 }

        public SwordState Sword { get; private set; } = SwordState.Sheathed;
        /// <summary>The full-body clip combat is playing (the presenter shows it), when it started, and a count that changes with each start.</summary>
        public ClipAction CurrentClip { get; private set; }
        public float ClipStart { get; private set; }
        public int ClipSerial { get; private set; }
        public bool ClipRunning => CurrentClip != ClipAction.None && Time.time - ClipStart < _clipLength;
        /// <summary>True while the sword is visibly in the hand (from the grab of a draw to the release of a sheathe).</summary>
        public bool SwordInHand
        {
            get
            {
                switch (Sword)
                {
                    case SwordState.Drawn: return true;
                    case SwordState.Drawing: return Time.time - ClipStart >= (DrawGrabTime?.Invoke() ?? 0f);
                    case SwordState.Sheathing: return Time.time - ClipStart < (SheatheReleaseTime?.Invoke() ?? 0f);
                    default: return false;
                }
            }
        }

        /// <summary>The sword really goes in and out of the sheath (the rig has the draw and sheathe clips).</summary>
        public bool UsesSheath => Rig != null && Rig.DrawSword != null && Rig.SheatheSword != null;

        private float _clipLength;
        private bool _attackQueued, _heavyQueued, _comboQueued;
        private float _lastCombat = -100f;

        private float _nextAttack;
        private float _lastSwing = -10f;
        private readonly List<(float at, float multiplier, bool last)> _pendingHits = new List<(float, float, bool)>();
        private float _aimHeldSince = -1f;
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

            for (var i = _pendingHits.Count - 1; i >= 0; i--)
            {
                if (Time.time < _pendingHits[i].at) continue;
                var (_, power, last) = _pendingHits[i];
                _pendingHits.RemoveAt(i);
                ResolveSwing(power, last);
            }

            UpdateSword();
            if (Motor.Current == PlayerMotor.State.Climbing || Motor.Current == PlayerMotor.State.Swimming) return;

            // Right button: without a bow it is the heavy attack at once; with one, a tap is the heavy attack and
            // holding it draws the bow.
            if (Input.HeavyPressed)
            {
                if (BowUsable) _aimHeldSince = Time.time;
                else if (!Guarding) HeavySwing();
            }

            if (Input.HeavyReleased && _aimHeldSince >= 0f)
            {
                if (Time.time - _aimHeldSince < AimHoldSeconds && !Guarding) HeavySwing();
                _aimHeldSince = -1f;
            }

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
            var weapon = Character.Weapon;
            if (weapon != null && !weapon.IsUsable) weapon = null;
            if (weapon != null && UsesSheath)
            {
                _lastCombat = Time.time;
                // Sword in the sheath: draw it first; the swing follows the draw.
                if (Sword != SwordState.Drawn)
                {
                    _attackQueued = true;
                    if (Sword == SwordState.Sheathed && Motor.IsGrounded) StartDraw();
                    return;
                }

                if (weapon.Class == WeaponClass.OneHanded && Rig.BasicAttack1 != null && Rig.BasicAttack2 != null)
                {
                    BasicSwing();
                    return;
                }
            }

            if (Time.time < _nextAttack) return;
            var cls = weapon?.Class ?? WeaponClass.OneHanded;
            var cooldown = cls == WeaponClass.TwoHanded ? 0.7f : cls == WeaponClass.Spear ? 0.3f : 0.38f;
            if (Character.CounterActive) cooldown *= 0.3f; // Flurry Rush.
            _nextAttack = Time.time + cooldown;
            _lastSwing = Time.time;
            Combo++;
            _pendingHits.Add((Time.time + swingDelay, 1f, Combo % (weapon?.Definition.ComboHits ?? 3) == 0));
            // The last swing of a one-handed combo (double damage) is the captured sword kick.
            var finisher = cls == WeaponClass.OneHanded && Combo % (weapon?.Definition.ComboHits ?? 3) == 0;
            Act(cls == WeaponClass.TwoHanded ? ActionKind.SlashHeavy : cls == WeaponClass.Spear ? ActionKind.Thrust
                : finisher ? ActionKind.Kick : ActionKind.SlashLight);
            FaceTarget();
        }

        /// <summary>
        /// Heavy attack: the captured sword-and-shield combo, every strike of it landing in turn (the last one as
        /// the end of a combo). Locks the weapon for its whole duration.
        /// </summary>
        private void HeavySwing()
        {
            var weapon = Character.Weapon;
            if (weapon != null && !weapon.IsUsable) weapon = null;
            if (weapon != null && UsesSheath)
            {
                _lastCombat = Time.time;
                if (Sword != SwordState.Drawn)
                {
                    _heavyQueued = true;
                    if (Sword == SwordState.Sheathed && Motor.IsGrounded) StartDraw();
                    return;
                }

                if (ClipRunning) return;
            }

            if (Time.time < _nextAttack) return;
            if (weapon != null && weapon.Class == WeaponClass.Spear) return; // no heavy with a spear yet
            var times = HeavyHitTimes?.Invoke();
            if (times == null || times.Length == 0) times = new[] { swingDelay + 0.3f };
            var duration = HeavyDuration?.Invoke() ?? times[times.Length - 1] + 0.3f;
            _nextAttack = Time.time + duration;
            _lastSwing = Time.time;
            Combo = 0;
            for (var i = 0; i < times.Length; i++) _pendingHits.Add((Time.time + times[i], 1.2f, i == times.Length - 1));
            Act(ActionKind.SlashHeavy);
            FaceTarget();
        }

        /// <summary>
        /// Left button with a one-handed sword: the first swing of the basic combo; pressing again while a swing plays
        /// chains the next one when it ends (first, second, first...). Each strike of a clip lands at the moment its
        /// right hand moves fastest; the last strike of the second swing is the combo's finisher.
        /// </summary>
        private void BasicSwing()
        {
            if ((CurrentClip == ClipAction.Attack1 || CurrentClip == ClipAction.Attack2) && ClipRunning)
            {
                _comboQueued = true;
                return;
            }

            if (ClipRunning) return;
            StartBasic(ClipAction.Attack1);
        }

        private void StartBasic(ClipAction step)
        {
            var clip = step == ClipAction.Attack1 ? Rig.BasicAttack1 : Rig.BasicAttack2;
            StartClip(step, clip.Duration);
            _comboQueued = false;
            _lastSwing = Time.time;
            _lastCombat = Time.time;
            _stanceUntil = Time.time + 4f;
            Combo = step == ClipAction.Attack1 ? 1 : 2;
            var strikes = clip.Strikes.Count > 0 ? clip.Strikes : new List<float> { clip.Duration * 0.4f };
            for (var i = 0; i < strikes.Count; i++)
                _pendingHits.Add((Time.time + strikes[i], 1f, step == ClipAction.Attack2 && i == strikes.Count - 1));
            _nextAttack = Time.time + clip.Duration;
            FaceTarget();
        }

        private void StartClip(ClipAction action, float length)
        {
            CurrentClip = action;
            ClipStart = Time.time;
            _clipLength = length;
            ClipSerial++;
        }

        private void StartDraw()
        {
            Sword = SwordState.Drawing;
            StartClip(ClipAction.Draw, Rig.DrawSword.Duration);
        }

        private void StartSheathe()
        {
            Sword = SwordState.Sheathing;
            _attackQueued = _heavyQueued = false;
            StartClip(ClipAction.Sheathe, Rig.SheatheSword.Duration);
        }

        /// <summary>
        /// The sword's way in and out of the sheath: a target locked draws it; the sheathe button or some quiet seconds
        /// standing put it back; each clip that ends hands over to what was waiting (a swing after a draw, the next
        /// swing of the combo).
        /// </summary>
        private void UpdateSword()
        {
            if (!UsesSheath)
            {
                Sword = InCombatStance ? SwordState.Drawn : SwordState.Sheathed;
                return;
            }

            var weapon = Character.Weapon;
            var hasSword = weapon != null && weapon.IsUsable;
            if (!hasSword && Sword != SwordState.Sheathed)
            {
                Sword = SwordState.Sheathed;
                CurrentClip = ClipAction.None;
                _attackQueued = _heavyQueued = _comboQueued = false;
            }

            var locked = Targeting != null && Targeting.Target != null;
            if (locked || Guarding) _lastCombat = Time.time;

            // Leaving the ground (a jump, a fall) cuts a clip short.
            if (CurrentClip != ClipAction.None && !Motor.IsGrounded) EndClip(true);
            if (CurrentClip != ClipAction.None && !ClipRunning) EndClip(false);

            if (CurrentClip != ClipAction.None || !hasSword || !Motor.IsGrounded) return;
            if (Sword == SwordState.Sheathed && (locked || _attackQueued || _heavyQueued))
            {
                StartDraw();
                return;
            }

            if (Sword != SwordState.Drawn) return;
            var still = new Vector3(Motor.Velocity.x, 0f, Motor.Velocity.z).sqrMagnitude < 0.04f;
            if ((Input.SheathePressed && !locked && !Guarding)
                || (still && !locked && !Guarding && Time.time - _lastCombat > sheatheAfterSeconds))
                StartSheathe();
        }

        private void EndClip(bool cut)
        {
            var action = CurrentClip;
            var inHand = SwordInHand;
            CurrentClip = ClipAction.None;
            switch (action)
            {
                case ClipAction.Draw:
                    Sword = !cut || inHand ? SwordState.Drawn : SwordState.Sheathed;
                    var attack = _attackQueued;
                    var heavy = _heavyQueued;
                    _attackQueued = _heavyQueued = false;
                    if (Sword == SwordState.Drawn && Motor.IsGrounded)
                    {
                        if (attack) Swing();
                        else if (heavy) HeavySwing();
                    }

                    break;
                case ClipAction.Sheathe:
                    Sword = !cut || !inHand ? SwordState.Sheathed : SwordState.Drawn;
                    break;
                case ClipAction.Attack1:
                case ClipAction.Attack2:
                    var next = !cut && _comboQueued;
                    _comboQueued = false;
                    if (next) StartBasic(action == ClipAction.Attack1 ? ClipAction.Attack2 : ClipAction.Attack1);
                    break;
            }
        }

        private void FaceTarget()
        {
            if (Targeting == null || Targeting.Target == null) return;
            var to = Targeting.Target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }

        private void ResolveSwing(float power, bool lastOfCombo)
        {
            var weapon = Character.Weapon;
            if (weapon != null && !weapon.IsUsable) weapon = null;
            var cls = weapon?.Class ?? WeaponClass.OneHanded;
            var reach = cls == WeaponClass.Spear ? 2.8f : cls == WeaponClass.TwoHanded ? 2.2f : 1.7f;

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
                hit = weapon.Strike(power, Character.Stats.Get(StatId.AttackPercent), lastOfCombo);
            }
            else
            {
                hit = new HitResult { Damage = DamageRules.Dealt((int)unarmedAttack, power, Character.Stats.Get(StatId.AttackPercent)) };
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
