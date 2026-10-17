using UnityEngine;
using Zeldexile.Gameplay;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// The edge of a region while its neighbors do not exist yet: tall invisible walls (built by RegionWorld) stop
    /// the player, a short message says the way goes on later, and anyone who somehow falls below the world is
    /// put back at the entrance instead of falling forever.
    /// </summary>
    public sealed class RegionLimits : MonoBehaviour
    {
        public RegionWorld World;

        [SerializeField] private float warnDistance = 10f;
        [SerializeField] private float warnEverySeconds = 25f;
        private float _nextWarning;

        private void Update()
        {
            if (World == null || World.Player == null || World.Motor == null) return;
            var p = World.Player.transform.position - World.Origin;
            var data = World.Data;

            if (p.y < -60f || p.x < -20f || p.z < -20f || p.x > data.sizeU + 20f || p.z > data.sizeV + 20f)
            {
                World.Motor.SetSpawn(World.SpawnPoint);
                World.Motor.Respawn();
                World.Player.AddLog("Você voltou à entrada do vale.");
                return;
            }

            var edge = Mathf.Min(Mathf.Min(p.x, data.sizeU - p.x), Mathf.Min(p.z, data.sizeV - p.z));
            if (edge < warnDistance && Time.time >= _nextWarning)
            {
                _nextWarning = Time.time + warnEverySeconds;
                World.Player.AddLog("O caminho segue além daqui, mas ainda não está aberto.");
            }
        }
    }

    /// <summary>White spray at the foot of a waterfall.</summary>
    public static class WaterSpray
    {
        public static GameObject Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("Borrifo");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startColor = new Color(1f, 1f, 1f, 0.55f);
            main.gravityModifier = 0.25f;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 22f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 1.2f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SoftMaterial(new Color(1f, 1f, 1f, 0.7f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return go;
        }

        private static Material _material;

        /// <summary>Soft round particles (no texture needed).</summary>
        public static Material SoftMaterial(Color color)
        {
            if (_material == null)
            {
                var shader = Shader.Find("Zeldexile/SoftParticle");
                _material = shader != null ? new Material(shader) : Palette.Glow(color);
            }

            return _material;
        }
    }
}
