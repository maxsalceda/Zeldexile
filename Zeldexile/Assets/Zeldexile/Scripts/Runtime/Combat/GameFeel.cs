using UnityEngine;
using Zeldexile.World;

namespace Zeldexile.Combat
{
    /// <summary>
    /// Owns Time.timeScale so effects never fight each other: hit-stop on impacts, slow motion after a
    /// perfect dodge, and aerial focus. Also keeps the camera trauma value (shake = trauma squared).
    /// </summary>
    public sealed class GameFeel : MonoBehaviour
    {
        public static GameFeel Current { get; private set; }

        /// <summary>Hits dealt by this object get hit-stop and a small shake.</summary>
        public GameObject Player;

        public const float HitStopScale = 0.05f;
        public const float FocusScale = 0.3f;

        private float _hitStopUntil;
        private float _slowUntil;
        private float _slowScale = 1f;
        private bool _focus;

        /// <summary>0..1. The camera shakes by Trauma squared.</summary>
        public float Trauma { get; private set; }

        public bool InSlowMotion => Time.unscaledTime < _slowUntil;

        private void Awake() => Current = this;

        private void OnEnable() => ImpactResolver.Damaged += OnDamaged;
        private void OnDisable() => ImpactResolver.Damaged -= OnDamaged;

        private void OnDamaged(Health target, float amount, ElementalImpact impact, bool sneak)
        {
            if (Player == null || impact.Owner != Player || target.gameObject == Player) return;
            HitStop(sneak ? 0.12f : 0.06f);
            AddTrauma(sneak ? 0.25f : 0.12f);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        public static void HitStop(float seconds)
        {
            if (Current == null) return;
            Current._hitStopUntil = Mathf.Max(Current._hitStopUntil, Time.unscaledTime + seconds);
        }

        public static void SlowMotion(float scale, float seconds)
        {
            if (Current == null) return;
            Current._slowScale = scale;
            Current._slowUntil = Time.unscaledTime + seconds;
        }

        public static void SetFocus(bool on)
        {
            if (Current != null) Current._focus = on;
        }

        public static void AddTrauma(float amount)
        {
            if (Current != null) Current.Trauma = Mathf.Clamp01(Current.Trauma + amount);
        }

        private void Update()
        {
            var scale = 1f;
            if (_focus) scale = Mathf.Min(scale, FocusScale);
            if (Time.unscaledTime < _slowUntil) scale = Mathf.Min(scale, _slowScale);
            if (Time.unscaledTime < _hitStopUntil) scale = Mathf.Min(scale, HitStopScale);
            Time.timeScale = scale;
            Time.fixedDeltaTime = 0.02f * scale;
            Trauma = Mathf.MoveTowards(Trauma, 0f, Time.unscaledDeltaTime * 1.4f);
        }
    }
}
