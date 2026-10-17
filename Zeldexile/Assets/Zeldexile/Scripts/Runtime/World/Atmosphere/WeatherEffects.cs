using System;
using UnityEngine;
using Zeldexile.Domain;
using Random = UnityEngine.Random;

namespace Zeldexile.World.Atmosphere
{
    /// <summary>
    /// Rain particles around the camera and lightning during storms. Lightning prefers metal:
    /// if the target carries metal gear, sparks warn first and the bolt strikes the target.
    /// Every bolt is a real lightning impact, so it powers copper and hurts what it hits.
    /// </summary>
    public sealed class WeatherEffects : MonoBehaviour
    {
        public AtmosphereController Atmosphere;
        public Transform Target;
        public Func<bool> TargetCarriesMetal;
        public Action<string> Log;

        [SerializeField] private float minStrikeInterval = 7f;
        [SerializeField] private float maxStrikeInterval = 16f;
        [SerializeField] private float warningSeconds = 5f;
        [SerializeField] private float strikeDamage = 20f; // 5 hearts, ignores armor.

        private ParticleSystem _rain;
        private float _nextStrike;
        private float _warningUntil = -1f;
        private float _nextSpark;

        private void Start()
        {
            _rain = CreateRain();
            _nextStrike = Time.time + maxStrikeInterval;
        }

        private void Update()
        {
            if (Atmosphere == null) return;
            var cam = Camera.main;
            if (cam != null) _rain.transform.position = cam.transform.position + Vector3.up * 14f + cam.transform.forward * 6f;

            var emission = _rain.emission;
            emission.rateOverTime = 2600f * Atmosphere.RainIntensity;

            if (!Atmosphere.IsStorm || Target == null)
            {
                _warningUntil = -1f;
                return;
            }

            // Metal gear: sparks crackle for a moment, then the bolt hits the carrier.
            if (_warningUntil > 0f)
            {
                if (Time.time >= _nextSpark)
                {
                    _nextSpark = Time.time + 0.18f;
                    Effects.Flash(Target.position + Vector3.up * 1.6f + Random.insideUnitSphere * 0.4f, 0.25f,
                        Palette.ForElement(Element.Lightning));
                }

                if (Time.time >= _warningUntil)
                {
                    _warningUntil = -1f;
                    if (TargetCarriesMetal != null && TargetCarriesMetal()) Strike(Target.position, true);
                }

                return;
            }

            if (Time.time < _nextStrike) return;
            _nextStrike = Time.time + Random.Range(minStrikeInterval, maxStrikeInterval);

            if (TargetCarriesMetal != null && TargetCarriesMetal() && Random.value < 0.6f)
            {
                _warningUntil = Time.time + warningSeconds;
                Log?.Invoke("Seu equipamento de metal está faiscando! Tire-o ou fuja.");
                return;
            }

            var offset = Random.insideUnitCircle.normalized * Random.Range(14f, 45f);
            var ground = Target.position + new Vector3(offset.x, 0f, offset.y);
            if (Physics.Raycast(ground + Vector3.up * 80f, Vector3.down, out var hit, 200f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                ground = hit.point;
            Strike(ground, false);
        }

        private void Strike(Vector3 point, bool onTarget)
        {
            Bolt(point);
            Atmosphere.Flash(1f);
            ImpactResolver.Resolve(new ElementalImpact
            {
                Element = Element.Lightning, Position = point + Vector3.up * 0.5f, Radius = 3f,
                Damage = strikeDamage, Chains = 2, ChainRange = 8f, FixedDamage = true
            });
            if (onTarget) Log?.Invoke("Um raio caiu em você!");
        }

        private static void Bolt(Vector3 ground)
        {
            var go = new GameObject("Raio");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Palette.Glow(new Color(0.95f, 0.97f, 1f, 1f));
            line.widthMultiplier = 0.35f;
            const int segments = 12;
            line.positionCount = segments + 1;
            var top = ground + Vector3.up * 70f + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var jitter = i == 0 || i == segments ? Vector3.zero : new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                line.SetPosition(i, Vector3.Lerp(top, ground, t) + jitter);
            }

            Destroy(go, 0.25f);
            Effects.Flash(ground + Vector3.up, 3f, Palette.ForElement(Element.Lightning));
        }

        private ParticleSystem CreateRain()
        {
            var go = new GameObject("Chuva");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 26f;
            main.startSize = 0.035f;
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.75f, 0.82f, 0.95f, 0.55f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 36f, 1f); // thin along Z, which the rotation turns into Y
            shape.rotation = new Vector3(90f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 5f;
            renderer.velocityScale = 0.04f;
            renderer.sharedMaterial = Palette.Glow(Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
            return ps;
        }
    }
}
