using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain;
using Zeldexile.World;

namespace Zeldexile.Combat
{
    /// <summary>Creatures and objects that stop a hit before it lands (the player's shield).</summary>
    public interface IDamageInterceptor
    {
        /// <summary>Returns true when the hit was handled; <paramref name="through"/> is the damage that still lands.</summary>
        bool TryIntercept(int attack, Vector3 from, out int through, out bool perfect);
    }

    /// <summary>Flies straight, then resolves one elemental impact where it hits or where its range ends.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        private const float CastRadius = 0.2f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private SkillStats _stats;
        private Vector3 _direction;
        private GameObject _owner;
        private float _travelled;

        /// <summary>Raised once when the projectile ends: where, and whether it hit a living creature.</summary>
        public event Action<Vector3, bool> Ended;

        public static Projectile Launch(SkillStats stats, Vector3 origin, Vector3 direction, GameObject owner)
        {
            var go = Effects.Primitive(PrimitiveType.Sphere, null, Palette.Glow(Palette.ForElement(stats.Element)));
            go.name = "Projétil";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.35f;
            var p = go.AddComponent<Projectile>();
            p._stats = stats;
            p._direction = direction.normalized;
            p._owner = owner;
            if (p._direction.sqrMagnitude > 0.001f) go.transform.rotation = Quaternion.LookRotation(p._direction);
            return p;
        }

        /// <summary>Arrows and thrown weapons: a solid shape instead of the glowing orb.</summary>
        public void Visual(Color color, Vector3 size)
        {
            var renderer = GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = Palette.Outlined(color);
            var filter = GetComponent<MeshFilter>();
            if (filter != null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                filter.sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                Destroy(cube);
            }

            transform.localScale = size;
        }

        private void Update()
        {
            var step = _stats.Speed * Time.deltaTime;
            var count = Physics.SphereCastNonAlloc(transform.position, CastRadius, _direction, Hits, step,
                Physics.AllLayers, QueryTriggerInteraction.Collide);

            var bestDistance = float.MaxValue;
            var found = false;
            var point = Vector3.zero;
            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (_owner != null && hit.collider.transform.IsChildOf(_owner.transform)) continue;
                if (hit.collider.GetComponent<Projectile>() != null) continue;
                if (hit.collider.GetComponent<MaterialPickup>() != null || hit.collider.GetComponent<WeaponPickup>() != null) continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    // SphereCast reports distance 0 and point zero when it starts inside a collider.
                    point = hit.distance <= 0f ? transform.position : hit.point;
                    found = true;
                }
            }

            if (found)
            {
                Explode(point);
                return;
            }

            transform.position += _direction * step;
            _travelled += step;
            if (_travelled >= _stats.Range) Explode(transform.position);
        }

        private void Explode(Vector3 point)
        {
            var impact = new ElementalImpact
            {
                Element = _stats.Element,
                Position = point,
                Radius = _stats.Radius,
                Damage = _stats.Damage,
                Chains = _stats.Chains,
                ChainRange = _stats.ChainRange,
                Force = _stats.Force,
                Direction = _direction,
                Owner = _owner,
                SneakMultiplier = _stats.SneakMultiplier
            };
            var before = CountAlive(point, _stats.Radius);
            ImpactResolver.Resolve(impact);
            var hitCreature = before > 0;
            Ended?.Invoke(point, hitCreature);
            Destroy(gameObject);
        }

        private static readonly Collider[] Buffer = new Collider[32];

        private int CountAlive(Vector3 point, float radius)
        {
            var n = Physics.OverlapSphereNonAlloc(point, Mathf.Max(0.3f, radius), Buffer, Physics.AllLayers, QueryTriggerInteraction.Collide);
            var seen = new HashSet<Health>();
            for (var i = 0; i < n; i++)
            {
                if (_owner != null && Buffer[i].transform.IsChildOf(_owner.transform)) continue;
                var h = Buffer[i].GetComponentInParent<Health>();
                if (h != null && !h.IsDead) seen.Add(h);
            }

            return seen.Count;
        }
    }
}
