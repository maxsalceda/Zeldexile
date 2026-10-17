using System;
using UnityEngine;
using Zeldexile.Domain;

namespace Zeldexile.Combat
{
    /// <summary>
    /// Life in quarter-heart points, plus the elemental statuses: burning (damage over time), frozen
    /// (cannot act, next hit ×3) and shocked (drops the guard). Temporary (yellow) points are lost first.
    /// Defense reduces every hit to at least one point, for creatures and the player alike.
    /// </summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private float max = 12f;

        public float Max => max;
        public float Current { get; private set; }
        public float Temporary { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsFrozen => Time.time < _frozenUntil;
        public bool IsBurning => Time.time < _burningUntil;
        public bool IsShocked => Time.time < _shockedUntil;
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        /// <summary>Armor points subtracted from every hit that is not fixed damage.</summary>
        public int Defense { get; set; }
        /// <summary>Fireproof armor or elixir: never catches fire.</summary>
        public bool Fireproof { get; set; }
        /// <summary>Unfreezable set bonus.</summary>
        public bool Unfreezable { get; set; }

        public event Action<Health, float, Element> Damaged;
        public event Action<Health> Died;

        private float _frozenUntil;
        private float _burningUntil;
        private float _shockedUntil;
        private float _invulnerableUntil;
        private float _burnTick;

        /// <summary>Ignore all damage for a moment (dodge frames).</summary>
        public void MakeInvulnerable(float seconds) => _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);

        private void Awake() => Current = max;

        public void SetMax(float value, bool refill)
        {
            var ratio = max > 0f ? Current / max : 1f;
            max = Mathf.Max(1f, value);
            Current = refill ? max : Mathf.Min(max, Mathf.Round(max * ratio));
        }

        public void Heal(float points)
        {
            if (IsDead || points <= 0f) return;
            Current = Mathf.Min(max, Current + points);
        }

        public void HealFully() => Current = max;

        public void AddTemporary(float points)
        {
            if (points <= 0f) return;
            Temporary = Mathf.Min(LifeModel.MaxTemporaryHearts * LifeModel.PointsPerHeart, Temporary + points);
        }

        public void Revive()
        {
            IsDead = false;
            Current = max;
            Temporary = 0f;
            _frozenUntil = _burningUntil = _shockedUntil = 0f;
        }

        /// <summary>
        /// Applies a hit. Defense is subtracted unless <paramref name="fixedDamage"/> (fire, falls, cold,
        /// lasers). Elements add their status: ice freezes, fire burns, lightning shocks.
        /// </summary>
        public void TakeDamage(float amount, Element element, bool fixedDamage = false)
        {
            if (IsDead || amount <= 0f || IsInvulnerable) return;
            if (!fixedDamage && Defense > 0) amount = DamageRules.Taken(Mathf.RoundToInt(amount), Defense);
            amount = Mathf.Max(DamageRules.MinimumDamage, Mathf.Round(amount));

            var fromTemporary = Mathf.Min(Temporary, amount);
            Temporary -= fromTemporary;
            Current = Mathf.Max(0f, Current - (amount - fromTemporary));
            Damaged?.Invoke(this, amount, element);

            if (element == Element.Ice && !Unfreezable) _frozenUntil = Time.time + 1.2f;
            if (element == Element.Lightning) _shockedUntil = Time.time + 2f;
            if (element == Element.Fire && !Fireproof) _burningUntil = Mathf.Max(_burningUntil, Time.time + 3f);
            if (element == Element.Fire) _frozenUntil = 0f;

            if (Current <= 0f && Temporary <= 0f)
            {
                IsDead = true;
                Died?.Invoke(this);
            }
        }

        private void Update()
        {
            if (IsDead || !IsBurning) return;
            // Burning: one quarter heart every half second, ignoring armor.
            _burnTick += Time.deltaTime;
            if (_burnTick < 0.5f) return;
            _burnTick = 0f;
            var fromTemporary = Mathf.Min(Temporary, 1f);
            Temporary -= fromTemporary;
            Current = Mathf.Max(0f, Current - (1f - fromTemporary));
            if (Current <= 0f && Temporary <= 0f)
            {
                IsDead = true;
                Died?.Invoke(this);
            }
        }
    }
}
