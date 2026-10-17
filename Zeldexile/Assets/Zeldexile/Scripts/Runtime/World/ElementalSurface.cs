using System;
using System.Collections;
using UnityEngine;
using Zeldexile.Domain;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.World
{
    /// <summary>
    /// A piece of the world that reacts to elements: grass burns and spreads, water freezes or
    /// gets electrified, metal conducts lightning, ice melts.
    /// </summary>
    public sealed class ElementalSurface : MonoBehaviour
    {
        public SurfaceKind Kind = SurfaceKind.Stone;

        [Header("Grass fire")]
        [SerializeField] private float burnDuration = 6f;
        [SerializeField] private float spreadDelay = 0.9f;
        [SerializeField] private float spreadRadius = 2.3f;
        [SerializeField] private float regrowAfter = 45f;
        [SerializeField] private float updraftLift = 9f;
        [SerializeField] private float updraftHeight = 34f;

        /// <summary>Raised when lightning reaches a metal surface.</summary>
        public event Action<ElementalSurface> Energized;

        private static readonly Collider[] Buffer = new Collider[32];
        private Renderer[] _renderers;
        private UpdraftZone _updraft;
        private GameObject _flames;

        private void Awake()
        {
            Repaint();
        }

        public void ApplyElement(ElementalImpact impact, Vector3 point)
        {
            var reaction = ElementalReactions.React(Kind, impact.Element);

            if (reaction.Ignites) Ignite();
            if (reaction.FansFlames) FanFlames(impact.Direction);
            if (reaction.CreatesIcePlatform) FreezeAt(point, impact.Radius);
            if (reaction.Electrifies && TryGetComponent<WaterVolume>(out var water)) water.Electrify(impact.Damage);
            if (reaction.Conducts) Energized?.Invoke(this);

            if (Kind == SurfaceKind.BurningGrass && reaction.NewSurface == SurfaceKind.Grass) Extinguish();
            if (Kind == SurfaceKind.Ice && reaction.NewSurface == SurfaceKind.Water &&
                TryGetComponent<IcePlatform>(out var platform))
                platform.Melt();
        }

        public void Ignite()
        {
            if (Kind != SurfaceKind.Grass) return;
            // Wet grass does not catch fire: wait for the rain to stop (or for it to dry).
            if (IsWet())
            {
                Effects.Flash(transform.position + Vector3.up * 0.4f, 0.5f, new Color(0.7f, 0.7f, 0.72f, 0.6f));
                return;
            }

            Kind = SurfaceKind.BurningGrass;
            Repaint();
            StartCoroutine(Burn());
        }

        private IEnumerator Burn()
        {
            var top = GetComponent<Collider>().bounds.max.y;
            _updraft = UpdraftZone.Create(new Vector3(transform.position.x, top, transform.position.z),
                spreadRadius * 0.75f, updraftHeight, updraftLift);
            _flames = Effects.Flames(transform, top);

            var elapsed = 0f;
            var spread = false;
            var tick = 0f;
            while (Kind == SurfaceKind.BurningGrass && elapsed < burnDuration)
            {
                // Rain puts fires out three times faster.
                elapsed += Time.deltaTime * (IsWet() ? 3f : 1f);
                tick += Time.deltaTime;

                if (!spread && elapsed >= spreadDelay)
                {
                    spread = true;
                    SpreadTo(transform.position, spreadRadius);
                }

                if (tick >= 0.5f)
                {
                    tick = 0f;
                    ImpactResolver.Resolve(new ElementalImpact
                    {
                        Element = Element.Fire, Position = new Vector3(transform.position.x, top + 0.5f, transform.position.z),
                        Radius = 1.2f, Damage = 2f, SkipSurfaces = true
                    });
                }

                yield return null;
            }

            if (Kind != SurfaceKind.BurningGrass) yield break;
            Kind = SurfaceKind.Ash;
            Repaint();
            ClearFire(1.5f);
            yield return new WaitForSeconds(regrowAfter);
            if (Kind == SurfaceKind.Ash)
            {
                Kind = SurfaceKind.Grass;
                Repaint();
            }
        }

        private static bool IsWet()
        {
            var atmosphere = AtmosphereController.Current;
            return atmosphere != null && atmosphere.Wetness > 0.35f;
        }

        private void Extinguish()
        {
            StopAllCoroutines();
            Kind = SurfaceKind.Grass;
            Repaint();
            ClearFire(0f);
        }

        private void ClearFire(float updraftLinger)
        {
            if (_updraft != null) Destroy(_updraft.gameObject, updraftLinger);
            if (_flames != null) Destroy(_flames);
            _updraft = null;
            _flames = null;
        }

        /// <summary>Wind throws the flames forward: ignites grass in a line along the wind.</summary>
        private void FanFlames(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();
            for (var step = 1; step <= 3; step++)
                SpreadTo(transform.position + direction * (spreadRadius * 1.4f * step), spreadRadius);
        }

        private static void SpreadTo(Vector3 center, float radius)
        {
            var count = Physics.OverlapSphereNonAlloc(center, radius, Buffer, Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var other = Buffer[i].GetComponent<ElementalSurface>();
                if (other != null && other.Kind == SurfaceKind.Grass) other.Ignite();
            }
        }

        private void FreezeAt(Vector3 point, float radius)
        {
            var top = GetComponent<Collider>().bounds.max.y;
            IcePlatform.Spawn(new Vector3(point.x, top, point.z), Mathf.Max(1.2f, radius));
        }

        /// <summary>Colors the surface for its state. Covers models made of several meshes (grass tufts).</summary>
        public void Repaint()
        {
            if (Kind == SurfaceKind.Metal) return;
            if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in _renderers)
                if (r != null && r.enabled)
                    Palette.Paint(r, Palette.ForSurface(Kind));
        }
    }
}
