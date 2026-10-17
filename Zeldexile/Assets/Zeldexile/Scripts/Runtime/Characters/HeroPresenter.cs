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

        private readonly Dictionary<string, Renderer[]> _parts = new Dictionary<string, Renderer[]>();
        private Transform _bladeRoot;

        private static readonly string[] SwordBack = { "~Arma Costas", "~Arma Costas Guarda", "~Arma Costas Punho" };
        private static readonly string[] SwordHand = { "~Arma@", "~Arma Guarda", "~Arma Punho" };
        private static readonly string[] ShieldBack = { "~Escudo Costas", "~Escudo Costas Aro", "~Escudo Costas Umbo" };
        private static readonly string[] ShieldHand = { "~Escudo@", "~Escudo Aro", "~Escudo Umbo" };
        private static readonly string[] BowBack = { "~Arco Costas", "~Arco Costas Corda" };
        private static readonly string[] BowHand = { "~Arco@", "~Arco Corda" };
        private static readonly string[] Scabbard = { "~Bainha" };

        private void Start()
        {
            if (Rig == null) return;
            var all = Rig.GetComponentsInChildren<Renderer>(true);
            foreach (var key in new[] { SwordBack, SwordHand, ShieldBack, ShieldHand, BowBack, BowHand, Scabbard })
            foreach (var pattern in key)
            {
                var list = new List<Renderer>();
                foreach (var r in all)
                    if (r.name.Contains(pattern))
                        list.Add(r);
                _parts[pattern] = list.ToArray();
                if (pattern == "~Arma@" && list.Count > 0) _bladeRoot = list[0].transform;
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

            Show(Scabbard, true);
            Show(SwordHand, hasWeapon && drawn);
            Show(SwordBack, hasWeapon && !drawn);
            Show(ShieldHand, hasShield && (guarding || drawn));
            Show(ShieldBack, hasShield && !(guarding || drawn));
            Show(BowHand, hasBow && aiming);
            Show(BowBack, hasBow && !aiming);

            if (_bladeRoot != null && hasWeapon)
            {
                // The same blade mesh reads as sword, greatsword or spear by its proportions.
                switch (Character.Weapon.Class)
                {
                    case WeaponClass.TwoHanded: _bladeRoot.localScale = new Vector3(1.7f, 1.2f, 1.6f); break;
                    case WeaponClass.Spear: _bladeRoot.localScale = new Vector3(0.45f, 1.9f, 0.9f); break;
                    default: _bladeRoot.localScale = Vector3.one; break;
                }
            }

            if (Animator != null)
            {
                Animator.Guarding = guarding;
                Animator.Aiming = aiming;
                Animator.Kneeling = kneeling;
                Animator.Armed = hasWeapon && drawn;
            }
        }

        private void Show(string[] patterns, bool visible)
        {
            foreach (var p in patterns)
                if (_parts.TryGetValue(p, out var renderers))
                    foreach (var r in renderers)
                        if (r != null && r.enabled != visible)
                            r.enabled = visible;
        }
    }
}
