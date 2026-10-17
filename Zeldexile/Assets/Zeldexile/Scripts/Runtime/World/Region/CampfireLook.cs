using System;
using UnityEngine;
using Zeldexile.Characters;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// The look of a camp fire in the valley: a ring of pale stones, a small tipi of logs, living flames and a warm
    /// light, and an iron pot on a tripod. Gameplay (warmth, cooking) stays on Campfire and CookingPot.
    /// </summary>
    public static class CampfireLook
    {
        public static void Build(Transform fire, InstancedNature nature, Func<string, InstancedNature.Kind> kindOf)
        {
            var at = fire.position;
            var stone = kindOf("rocha_1");
            for (var i = 0; i < 8; i++)
            {
                var a = i / 8f * Mathf.PI * 2f;
                var p = at + new Vector3(Mathf.Cos(a) * 0.78f, -0.05f, Mathf.Sin(a) * 0.78f);
                nature.Add(stone, Matrix4x4.TRS(p, Quaternion.Euler(0f, i * 47f, 0f), Vector3.one * (0.26f + (i % 3) * 0.03f)), 0.4f);
            }

            var log = kindOf("tronco_caido_0");
            for (var i = 0; i < 3; i++)
            {
                var yaw = i * 120f + 15f;
                var r = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 28f);
                nature.Add(log, Matrix4x4.TRS(at + Quaternion.Euler(0f, yaw, 0f) * new Vector3(-0.28f, -0.05f, 0f), r, new Vector3(0.16f, 0.35f, 0.35f)), 0.6f);
            }

            Flames(fire, at + Vector3.up * 0.15f);

            var light = new GameObject("Luz da fogueira").AddComponent<Light>();
            light.transform.SetParent(fire, false);
            light.transform.localPosition = Vector3.up * 0.8f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.28f);
            light.range = 8f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<FireFlicker>();

            // Iron pot hanging from a tripod of sticks.
            var iron = Palette.Outlined(new Color(0.22f, 0.22f, 0.25f));
            var wood = Palette.Outlined(NaturePalette.Slot("bark"));
            LowPoly.Part("Panela", fire, LowPoly.Prism(8, 0.82f), iron, new Vector3(0f, 0.42f, 0f), new Vector3(0.62f, 0.38f, 0.62f));
            LowPoly.Part("Borda", fire, LowPoly.Prism(8), iron, new Vector3(0f, 0.8f, 0f), new Vector3(0.56f, 0.05f, 0.56f));
            for (var i = 0; i < 3; i++)
            {
                var a = i / 3f * Mathf.PI * 2f + 0.5f;
                var foot = new Vector3(Mathf.Cos(a) * 0.95f, 0f, Mathf.Sin(a) * 0.95f);
                var top = new Vector3(0f, 1.55f, 0f);
                var dir = top - foot;
                LowPoly.Part("Tripé", fire, LowPoly.Prism(5), wood, foot, new Vector3(0.06f, dir.magnitude, 0.06f), Quaternion.FromToRotation(Vector3.up, dir));
            }
        }

        private static void Flames(Transform parent, Vector3 position)
        {
            var go = new GameObject("Chamas");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(-90f, 0f, 0f));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.62f, 0.15f, 0.9f), new Color(1f, 0.85f, 0.3f, 0.9f));
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 30f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.22f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = WaterSpray.SoftMaterial(Color.white);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        private sealed class FireFlicker : MonoBehaviour
        {
            private Light _light;
            private float _base;
            private void Awake()
            {
                _light = GetComponent<Light>();
                _base = _light.intensity;
            }

            // The glow only matters when the sun is low: by day it would wash out the ground around the fire.
            private void Update()
            {
                var sky = Zeldexile.World.Atmosphere.AtmosphereController.Current;
                var night = sky == null ? 1f : Mathf.Lerp(0.08f, 1f, Mathf.InverseLerp(0.25f, -0.05f, sky.SunHeight));
                _light.intensity = _base * night * (0.8f + Mathf.PerlinNoise(Time.time * 7f, 0.3f) * 0.4f);
            }
        }
    }
}
