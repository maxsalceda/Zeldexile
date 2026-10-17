using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Combat;
using Zeldexile.Domain;

namespace Zeldexile.World
{
    public struct ElementalImpact
    {
        public Element Element;
        public Vector3 Position;
        public float Radius;
        public float Damage;
        public int Chains;
        public float ChainRange;
        public float Force;
        public Vector3 Direction;
        public GameObject Owner;
        /// <summary>True for ambient damage (burning grass) that must not react with the world again.</summary>
        public bool SkipSurfaces;
        /// <summary>Damage multiplier against creatures that have not noticed the attacker (0 means 1).</summary>
        public float SneakMultiplier;
        /// <summary>Lightning, fire and falls ignore armor.</summary>
        public bool FixedDamage;
    }

    /// <summary>Creatures that can be caught unaware (sneak attacks).</summary>
    public interface IAwareness
    {
        bool IsAware { get; }
    }

    public interface IKnockbackReceiver
    {
        void Knockback(Vector3 impulse);
    }

    /// <summary>
    /// Applies one impact to everything it touches: creatures take damage, surfaces react.
    /// This is the single path for rune, arrow and weather effects, so a rule in combat is always
    /// the same rule in the world.
    /// </summary>
    public static class ImpactResolver
    {
        private static readonly Collider[] Buffer = new Collider[128];

        /// <summary>Raised for visuals: an impact was resolved.</summary>
        public static event Action<ElementalImpact> Resolved;

        /// <summary>Raised when a creature takes damage (damage numbers, hit-stop). bool = sneak or critical hit.</summary>
        public static event Action<Health, float, ElementalImpact, bool> Damaged;

        /// <summary>Raised for visuals: lightning jumped from one point to another.</summary>
        public static event Action<Vector3, Vector3> Chained;

        /// <summary>Lets melee hits report damage through the same event the HUD and game feel listen to.</summary>
        public static void NotifyDamaged(Health target, float amount, ElementalImpact impact, bool special) =>
            Damaged?.Invoke(target, amount, impact, special);

        public static void Resolve(ElementalImpact impact)
        {
            var hitHealth = new HashSet<Health>();
            var hitSurfaces = new HashSet<ElementalSurface>();
            ApplyArea(impact, hitHealth, hitSurfaces);
            if (impact.Element == Element.Lightning && impact.Chains > 0)
                Chain(impact, hitHealth, hitSurfaces);
            Resolved?.Invoke(impact);
        }

        /// <summary>Cone in front of the caster (wind gust).</summary>
        public static void ResolveCone(ElementalImpact impact, float range, float halfAngle)
        {
            var hitHealth = new HashSet<Health>();
            var hitSurfaces = new HashSet<ElementalSurface>();
            var count = Physics.OverlapSphereNonAlloc(impact.Position, range, Buffer, Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var col = Buffer[i];
                if (IsOwner(col, impact.Owner)) continue;
                var to = col.bounds.center - impact.Position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(impact.Direction, to) > halfAngle) continue;
                ApplyTo(col, impact, hitHealth, hitSurfaces);
            }

            Resolved?.Invoke(impact);
        }

        private static void ApplyArea(ElementalImpact impact, HashSet<Health> hitHealth,
            HashSet<ElementalSurface> hitSurfaces)
        {
            var count = Physics.OverlapSphereNonAlloc(impact.Position, impact.Radius, Buffer, Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var col = Buffer[i];
                if (IsOwner(col, impact.Owner)) continue;
                ApplyTo(col, impact, hitHealth, hitSurfaces);
            }
        }

        /// <summary>True when the impact hit at least one living creature.</summary>
        public static bool ApplyTo(Collider col, ElementalImpact impact, HashSet<Health> hitHealth,
            HashSet<ElementalSurface> hitSurfaces)
        {
            var hitCreature = false;
            var health = col.GetComponentInParent<Health>();
            if (health != null && hitHealth.Add(health))
            {
                var damage = impact.Damage;
                var sneak = impact.SneakMultiplier > 1f && health.TryGetComponent<IAwareness>(out var awareness) && !awareness.IsAware;
                if (sneak) damage *= impact.SneakMultiplier;
                if (health.IsFrozen && impact.Element != Element.Ice) damage *= DamageRules.FrozenMultiplier;
                var before = health.Current + health.Temporary;
                health.TakeDamage(damage, impact.Element, impact.FixedDamage);
                var after = health.Current + health.Temporary;
                hitCreature = !health.IsDead || after < before;
                if (after < before) Damaged?.Invoke(health, before - after, impact, sneak);
                if (impact.Force > 0f)
                {
                    var dir = health.transform.position - impact.Position;
                    if (impact.Direction.sqrMagnitude > 0.01f) dir = impact.Direction;
                    dir.y = 0f;
                    if (health.TryGetComponent<IKnockbackReceiver>(out var receiver))
                        receiver.Knockback(dir.normalized * impact.Force + Vector3.up * impact.Force * 0.2f);
                }
            }

            if (impact.SkipSurfaces || impact.Element == Element.None) return hitCreature;
            var surface = col.GetComponentInParent<ElementalSurface>();
            if (surface != null && hitSurfaces.Add(surface))
                surface.ApplyElement(impact, col.ClosestPoint(impact.Position));
            return hitCreature;
        }

        /// <summary>Lightning jumps to the nearest creature or metal it has not hit yet.</summary>
        private static void Chain(ElementalImpact impact, HashSet<Health> hitHealth,
            HashSet<ElementalSurface> hitSurfaces)
        {
            var from = impact.Position;
            for (var jump = 0; jump < impact.Chains; jump++)
            {
                var count = Physics.OverlapSphereNonAlloc(from, impact.ChainRange, Buffer, Physics.AllLayers,
                    QueryTriggerInteraction.Collide);
                Collider best = null;
                var bestDistance = float.MaxValue;
                for (var i = 0; i < count; i++)
                {
                    var col = Buffer[i];
                    if (IsOwner(col, impact.Owner)) continue;
                    var health = col.GetComponentInParent<Health>();
                    var surface = col.GetComponentInParent<ElementalSurface>();
                    var isTarget = (health != null && !health.IsDead && !hitHealth.Contains(health)) ||
                                   (surface != null && surface.Kind == SurfaceKind.Metal &&
                                    !hitSurfaces.Contains(surface));
                    if (!isTarget) continue;
                    var d = (col.bounds.center - from).sqrMagnitude;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = col;
                    }
                }

                if (best == null) return;
                var to = best.bounds.center;
                Chained?.Invoke(from, to);
                var hop = impact;
                hop.Position = to;
                hop.Force = 0f;
                ApplyTo(best, hop, hitHealth, hitSurfaces);
                from = to;
            }
        }

        private static bool IsOwner(Collider col, GameObject owner) =>
            owner != null && col.transform.IsChildOf(owner.transform);
    }
}
