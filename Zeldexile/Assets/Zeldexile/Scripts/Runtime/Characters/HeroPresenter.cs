using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain;
using Zeldexile.Player;

namespace Zeldexile.Characters
{
    /// <summary>
    /// What the hero visibly carries. The model has every piece of gear twice: stowed on the back and held
    /// in the hands. Each frame this shows one or the other from the pouch and the combat state (sword drawn
    /// in a fight, shield on the arm while guarding, bow in hand while aiming), scales the blade to the
    /// weapon class, and feeds the held stances to the animator.
    /// </summary>
    public sealed class HeroPresenter : MonoBehaviour
    {
        public PlayerCharacter Character;
        public PlayerCombat Combat;
        public CharacterAnimator Animator;
        public CharacterRig Rig;
        /// <summary>True while a menu that kneels the hero is open (cooking, prayer).</summary>
        public System.Func<bool> Kneeling;
        /// <summary>The motor's crouch toggle; the animator walks crouched while it holds.</summary>
        public System.Func<bool> Crouching;
        /// <summary>The motor's sprint (run button held on the ground): the sprint clip plays while it holds.</summary>
        public System.Func<bool> Sprinting;

        /// <summary>Seconds into the draw clip when the hand takes the hilt out of the sheath (the sword moves to the hand).</summary>
        public float DrawGrab { get; private set; }
        /// <summary>Seconds into the sheathe clip when the hand lets go of the hilt in the sheath.</summary>
        public float SheatheRelease { get; private set; }

        private readonly Dictionary<GearPart, List<Renderer>> _parts = new Dictionary<GearPart, List<Renderer>>();
        private Transform _bladeRoot;
        /// <summary>The joint the held sword hangs on, and the place on the hips where it rests in its sheath.</summary>
        private Transform _gripParent, _sheath;
        private int _clipSerial = -1;

        private void Start()
        {
            if (Rig == null) return;
            foreach (var r in Rig.GetComponentsInChildren<Renderer>(true))
            {
                var part = CharacterRig.Classify(r.name);
                if (part == GearPart.None) continue;
                if (!_parts.TryGetValue(part, out var list)) _parts[part] = list = new List<Renderer>();
                list.Add(r);
            }

            // The blade scales by weapon class around the fist, so every sword part shares one pivot.
            if (_parts.TryGetValue(GearPart.SwordHand, out var sword) && sword.Count > 0)
            {
                // Pivot at the grip (the fist), on the forearm bone that carries the sword.
                var grip = sword.Find(r => r.name.Contains("Arma Punho")) ?? sword[0];
                _bladeRoot = new GameObject("Empunhadura").transform;
                _bladeRoot.SetParent(grip.transform.parent, false);
                _bladeRoot.position = grip.transform.position;
                foreach (var r in sword) r.transform.SetParent(_bladeRoot, true);
                _gripParent = _bladeRoot.parent;
                PlaceSheath();
            }
        }

        /// <summary>
        /// The sheath on the waist is where the draw clip takes the sword from: at the moment its right hand slows to
        /// grip the hilt (the slowest point of the middle of the draw), the held sword's pose, carried by the hips.
        /// Swapping the sword between hand and sheath at that moment moves nothing. The sheathe clip lets go when its
        /// hand comes back closest to that same grip.
        /// </summary>
        private void PlaceSheath()
        {
            var draw = Rig.DrawSword;
            var sheathe = Rig.SheatheSword;
            var direct = Rig.Direct;
            if (draw == null || sheathe == null || direct == null || _gripParent == null) return;
            var skeleton = direct.Skeleton;
            var hand = skeleton.IndexOf("RightHand");
            if (hand < 0) return;
            var handBone = skeleton.Bones[hand];
            var hipsBone = skeleton.Bones[skeleton.Hips];
            // The held sword's joint relative to the hand bone, as rigged (both still at rest here).
            var grip = handBone.worldToLocalMatrix * _gripParent.localToWorldMatrix;

            var rotations = new Quaternion[skeleton.Bones.Count];
            Matrix4x4 HandOnHips(DirectClip clip, float time)
            {
                for (var i = 0; i < rotations.Length; i++) rotations[i] = skeleton.Bones[i].localRotation;
                clip.Sample(time, false, rotations, out var hips);
                return clip.BoneInModel(skeleton.Hips, rotations, hips).inverse * clip.BoneInModel(hand, rotations, hips);
            }

            DrawGrab = Slowest(draw, 0.25f, 0.6f);
            var held = HandOnHips(draw, DrawGrab);
            var place = held * grip;
            // In the sheath the hilt stays where the hand takes it and the blade hangs along the hip, back and down
            // (the hero still stands at rest here, facing his forward): the held sword turned about its hilt.
            var axis = BladeAxis(out var hilt);
            if (axis.sqrMagnitude > 1e-12f)
            {
                var hang = hipsBone.InverseTransformDirection(transform.TransformDirection(new Vector3(0f, -0.75f, -1f)));
                var pivot = place.MultiplyPoint3x4(hilt);
                var turn = Quaternion.FromToRotation(place.MultiplyVector(axis), hang);
                place = Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(turn) * Matrix4x4.Translate(-pivot) * place;
            }

            _sheath = new GameObject("Bainha").transform;
            _sheath.SetParent(hipsBone, false);
            _sheath.localPosition = place.GetColumn(3);
            _sheath.localRotation = place.rotation;
            _sheath.localScale = place.lossyScale;

            var best = float.MaxValue;
            var target = (Vector3)held.GetColumn(3);
            for (var t = sheathe.Duration * 0.5f; t <= sheathe.Duration; t += DirectClip.TrackStep)
            {
                var d = ((Vector3)HandOnHips(sheathe, t).GetColumn(3) - target).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                SheatheRelease = t;
            }
        }

        /// <summary>
        /// The held sword's blade, from the hilt (the grip pivot) toward the middle of the sword's meshes, and the hilt
        /// itself, both in the grip joint's space.
        /// </summary>
        private Vector3 BladeAxis(out Vector3 hilt)
        {
            hilt = _gripParent.InverseTransformPoint(_bladeRoot.position);
            if (!_parts.TryGetValue(GearPart.SwordHand, out var sword) || sword.Count == 0) return Vector3.zero;
            // From the meshes, not Renderer.bounds: the hand gear starts hidden, and hidden renderers report no bounds.
            Bounds? box = null;
            foreach (var r in sword)
            {
                var mesh = r.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;
                var b = mesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = _gripParent.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (box == null) box = new Bounds(p, Vector3.zero);
                    else
                    {
                        var grown = box.Value;
                        grown.Encapsulate(p);
                        box = grown;
                    }
                }
            }

            return box.HasValue ? box.Value.center - hilt : Vector3.zero;
        }

        /// <summary>When the right hand moves slowest within a part of a clip (fractions of its length).</summary>
        private static float Slowest(DirectClip clip, float from, float to)
        {
            var track = clip.RightHandTrack;
            if (track == null || track.Length < 3) return clip.Duration * (from + to) * 0.5f;
            var a = Mathf.Max(1, Mathf.FloorToInt(from * (track.Length - 1)));
            var b = Mathf.Min(track.Length - 2, Mathf.CeilToInt(to * (track.Length - 1)));
            var best = float.MaxValue;
            var at = a;
            for (var i = a; i <= b; i++)
            {
                var speed = (track[i + 1] - track[i - 1]).sqrMagnitude;
                if (speed >= best) continue;
                best = speed;
                at = i;
            }

            return at * DirectClip.TrackStep;
        }

        private void Update()
        {
            if (Character == null) return;
            var guarding = Combat != null && Combat.Guarding;
            var aiming = Combat != null && Combat.Aiming;
            var kneeling = Kneeling != null && Kneeling();
            var sheath = _sheath != null && Combat != null && Combat.UsesSheath;
            // With the waist sheath the sword is in the hand only between the draw's grab and the sheathe's release.
            var drawn = Combat != null && (sheath ? Combat.SwordInHand : Combat.InCombatStance && !aiming && !kneeling);

            var hasWeapon = Character.Weapon != null && Character.Weapon.IsUsable;
            var hasShield = Character.Shield != null && Character.Shield.IsUsable;
            var hasBow = Character.Bow != null && Character.Bow.IsUsable;

            if (sheath)
            {
                Show(GearPart.SwordHand, hasWeapon);
                Show(GearPart.SwordBack, false);
                var parent = drawn ? _gripParent : _sheath;
                if (_bladeRoot.parent != parent) _bladeRoot.SetParent(parent, false);
            }
            else
            {
                Show(GearPart.SwordHand, hasWeapon && drawn);
                Show(GearPart.SwordBack, hasWeapon && !drawn);
            }

            Show(GearPart.ShieldHand, hasShield && (guarding || drawn));
            Show(GearPart.ShieldBack, hasShield && !(guarding || drawn));
            Show(GearPart.BowHand, hasBow && aiming);
            Show(GearPart.BowBack, hasBow && !aiming);

            if (_bladeRoot != null && hasWeapon)
            {
                // The same blade mesh reads as sword, greatsword or spear by its proportions.
                switch (Character.Weapon.Class)
                {
                    case WeaponClass.TwoHanded: _bladeRoot.localScale = new Vector3(1.6f, 1.35f, 1.35f); break;
                    case WeaponClass.Spear: _bladeRoot.localScale = new Vector3(0.5f, 1.8f, 1.8f); break;
                    default: _bladeRoot.localScale = Vector3.one; break;
                }
            }

            if (Animator != null)
            {
                Animator.Guarding = guarding;
                Animator.Aiming = aiming;
                Animator.Kneeling = kneeling;
                Animator.Crouching = Crouching != null && Crouching();
                Animator.Armed = hasWeapon && drawn;
                Animator.ShieldInHand = hasShield && (guarding || drawn);
            }

            PlayClips();
        }

        /// <summary>
        /// The bone-for-bone clips: combat's full-body clip (draw, sheathe, a basic combo swing) from the moment it
        /// started, else the sprint loop while the run button sprints on the ground, else back to the procedural body.
        /// </summary>
        private void PlayClips()
        {
            var direct = Rig != null ? Rig.Direct : null;
            if (direct == null || Combat == null) return;
            if (Combat.ClipRunning)
            {
                if (_clipSerial == Combat.ClipSerial) return;
                _clipSerial = Combat.ClipSerial;
                DirectClip clip = null;
                switch (Combat.CurrentClip)
                {
                    case PlayerCombat.ClipAction.Draw: clip = Rig.DrawSword; break;
                    case PlayerCombat.ClipAction.Sheathe: clip = Rig.SheatheSword; break;
                    case PlayerCombat.ClipAction.Attack1: clip = Rig.BasicAttack1; break;
                    case PlayerCombat.ClipAction.Attack2: clip = Rig.BasicAttack2; break;
                }

                direct.Play(clip, false, 0.12f, Time.time - Combat.ClipStart);
                return;
            }

            _clipSerial = -1;
            var source = Animator != null ? Animator.Source : null;
            var speed = source != null ? new Vector3(source.Velocity.x, 0f, source.Velocity.z).magnitude : 0f;
            var sprint = Rig.Sprint != null && Sprinting != null && Sprinting() && source != null
                         && source.Locomotion == Locomotion.Grounded && speed > 3f;
            if (sprint)
            {
                if (direct.Current != Rig.Sprint || !direct.Playing) direct.Play(Rig.Sprint, true, 0.2f);
            }
            else if (direct.Playing) direct.Stop(0.2f);
        }

        private void Show(GearPart part, bool visible)
        {
            if (!_parts.TryGetValue(part, out var renderers)) return;
            foreach (var r in renderers)
                if (r != null && r.enabled != visible)
                    r.enabled = visible;
        }
    }
}
