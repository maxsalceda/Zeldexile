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

        private readonly Dictionary<GearPart, List<Renderer>> _parts = new Dictionary<GearPart, List<Renderer>>();
        private Transform _bladeRoot;

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
                var arm = sword[0].transform.parent;
                _bladeRoot = new GameObject("Empunhadura").transform;
                _bladeRoot.SetParent(arm, false);
                _bladeRoot.localPosition = new Vector3(0f, -0.62f, 0f);
                foreach (var r in sword) r.transform.SetParent(_bladeRoot, true);
            }
        }

        private void Update()
        {
            if (Character == null) return;
            var guarding = Combat != null && Combat.Guarding;
            var aiming = Combat != null && Combat.Aiming;
            var kneeling = Kneeling != null && Kneeling();
            var drawn = Combat != null && Combat.InCombatStance && !aiming && !kneeling;

            var hasWeapon = Character.Weapon != null && Character.Weapon.IsUsable;
            var hasShield = Character.Shield != null && Character.Shield.IsUsable;
            var hasBow = Character.Bow != null && Character.Bow.IsUsable;

            Show(GearPart.SwordHand, hasWeapon && drawn);
            Show(GearPart.SwordBack, hasWeapon && !drawn);
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
                Animator.Armed = hasWeapon && drawn;
                Animator.ShieldInHand = hasShield && (guarding || drawn);
            }
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
