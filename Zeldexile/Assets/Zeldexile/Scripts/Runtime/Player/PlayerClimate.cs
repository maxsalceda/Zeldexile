using UnityEngine;
using Zeldexile.Domain;
using Zeldexile.World;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.Player
{
    /// <summary>
    /// Body temperature from time of day, altitude, rain and nearby fire. Cold and heat take one quarter
    /// heart every 5 s (level 1) or 2.5 s (level 2); armor and food of the same level cancel the damage.
    /// Three answers to every pressure: equipment, a dish, or behavior (fire, shade, time of day).
    /// </summary>
    public sealed class PlayerClimate : MonoBehaviour
    {
        public PlayerCharacter Character;

        [SerializeField] private float fireRadius = 5f;

        /// <summary>World Y counted as altitude zero, and how strongly height cools the air there. The island uses
        /// 0 and 1; a large region sets its valley floor and a gentler lapse so its hills are cool, not deadly.</summary>
        public float AltitudeReference;
        public float LapseScale = 1f;

        private static readonly Collider[] Buffer = new Collider[32];
        private float _nextTick;

        public float Celsius { get; private set; } = 20f;
        /// <summary>Seconds between damage ticks, 0 when safe.</summary>
        public float DamageInterval { get; private set; }
        public bool NearFire { get; private set; }
        public int ColdLevel => Temperature.ColdLevel(Celsius);
        public int HeatLevel => Temperature.HeatLevel(Celsius);
        public bool InDanger => DamageInterval > 0f;

        private void Update()
        {
            var atmosphere = AtmosphereController.Current;
            if (atmosphere == null || Character == null) return;

            var fire = FireProximity();
            NearFire = fire > 0f;
            Celsius = Temperature.At(atmosphere.Clock.Hour, (transform.position.y - AltitudeReference) * LapseScale, atmosphere.CurrentWeather, fire);
            DamageInterval = Temperature.DamageInterval(Celsius,
                Character.Stats.Level(StatId.ColdResistance), Character.Stats.Level(StatId.HeatResistance));

            if (DamageInterval <= 0f)
            {
                _nextTick = Time.time + Temperature.Level1TickSeconds;
                return;
            }

            if (Time.time < _nextTick) return;
            _nextTick = Time.time + DamageInterval;
            Character.Health.TakeDamage(1f, ColdLevel > 0 ? Element.Ice : Element.Fire, fixedDamage: true);
        }

        private float FireProximity()
        {
            var best = 0f;
            var count = Physics.OverlapSphereNonAlloc(transform.position, fireRadius, Buffer, Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var surface = Buffer[i].GetComponent<ElementalSurface>();
                var campfire = Buffer[i].GetComponent<Campfire>();
                if ((surface == null || surface.Kind != SurfaceKind.BurningGrass) && (campfire == null || !campfire.IsLit)) continue;
                var d = Vector3.Distance(transform.position, Buffer[i].bounds.center);
                best = Mathf.Max(best, 1f - d / fireRadius);
            }

            return best;
        }
    }
}
