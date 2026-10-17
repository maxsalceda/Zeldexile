using UnityEngine;
using Zeldexile.Domain;

namespace Zeldexile.World.Atmosphere
{
    /// <summary>
    /// Time of day and weather in one place. Every frame it moves the sun (or moon), and writes
    /// the shared shader globals read by the toon, water, grass and sky shaders (Atmosphere.cginc).
    /// One real second is one game minute; weather re-rolls every 4 game hours.
    /// </summary>
    public sealed class AtmosphereController : MonoBehaviour
    {
        /// <summary>The active controller, so world rules (fire in rain, slipping) can ask about the weather.</summary>
        public static AtmosphereController Current { get; private set; }

        public Light Sun;
        [SerializeField] private float startHour = 9f;
        [SerializeField] private float transitionSeconds = 12f;

        public GameClock Clock { get; private set; }
        public WeatherTable Weather { get; private set; }
        public WeatherKind CurrentWeather { get; private set; }

        /// <summary>0 dry .. 1 soaked. Rises fast in rain, dries slowly after.</summary>
        public float Wetness { get; private set; }
        public float RainIntensity { get; private set; }
        public bool IsRaining => CurrentWeather == WeatherKind.Rain || CurrentWeather == WeatherKind.Storm;
        public bool IsStorm => CurrentWeather == WeatherKind.Storm;
        public float SunHeight { get; private set; }

        public event System.Action<WeatherKind> WeatherChanged;
        /// <summary>Raised at a blood-moon midnight: every creature and world weapon is back.</summary>
        public event System.Action BloodMoonRose;
        /// <summary>The night leading into a blood moon (red sky, guaranteed critical cooking near midnight).</summary>
        public bool IsBloodMoonNight => BloodMoon.IsBloodMoonNight(Clock);

        private float _cloudCover = 0.3f;
        private float _darken = 1f;
        private float _fogNear = 70f;
        private float _fogFar = 280f;
        private float _flash;
        private Material _sky;
        private bool _forced;

        private static readonly int ZenithId = Shader.PropertyToID("_ZxSkyZenith");
        private static readonly int HorizonId = Shader.PropertyToID("_ZxSkyHorizon");
        private static readonly int GroundId = Shader.PropertyToID("_ZxSkyGround");
        private static readonly int FogColorId = Shader.PropertyToID("_ZxFogColor");
        private static readonly int FogParamsId = Shader.PropertyToID("_ZxFogParams");
        private static readonly int AerialId = Shader.PropertyToID("_ZxAerialTint");
        private static readonly int SunColorId = Shader.PropertyToID("_ZxSunColor");
        private static readonly int SunDirId = Shader.PropertyToID("_ZxSunDir");
        private static readonly int AmbientId = Shader.PropertyToID("_ZxAmbient");
        private static readonly int CloudId = Shader.PropertyToID("_ZxCloudParams");
        private static readonly int WindId = Shader.PropertyToID("_ZxWind");
        private static readonly int WetId = Shader.PropertyToID("_ZxWetness");
        private static readonly int FlashId = Shader.PropertyToID("_ZxFlash");

        private void Awake()
        {
            Current = this;
            Clock = new GameClock(startHour);
            Weather = WeatherTable.Temperate(new System.Random());
            Weather.At(Clock.TotalHours);
            // Every session starts under a clear sky; later slots roll normally.
            Weather.Force(WeatherKind.Clear);
            CurrentWeather = WeatherKind.Clear;
            var skyShader = Shader.Find("Zeldexile/Sky");
            if (skyShader != null)
            {
                _sky = new Material(skyShader);
                RenderSettings.skybox = _sky;
            }

            RenderSettings.fog = false; // Our shaders do their own sky-colored fog.
            Apply(1f, true);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>Debug and tests: jump to an hour and/or force the weather.</summary>
        public void Set(float? hour, WeatherKind? weather)
        {
            if (hour.HasValue) Clock.SetHour(hour.Value);
            if (weather.HasValue)
            {
                _forced = true;
                Weather.Force(weather.Value);
                CurrentWeather = weather.Value;
            }

            Apply(1f, true);
        }

        public void Flash(float amount) => _flash = Mathf.Max(_flash, amount);

        private void Update()
        {
            var before = Clock.TotalHours;
            Clock.Advance(Time.deltaTime);
            if (BloodMoon.Crossed(before, Clock.TotalHours)) BloodMoonRose?.Invoke();
            var rolled = _forced ? CurrentWeather : Weather.At(Clock.TotalHours);
            if (rolled != CurrentWeather)
            {
                CurrentWeather = rolled;
                WeatherChanged?.Invoke(rolled);
            }

            Apply(Time.deltaTime, false);
        }

        private void Apply(float dt, bool instant)
        {
            // Weather targets.
            float cover, darken, fogNear, fogFar, rain;
            switch (CurrentWeather)
            {
                case WeatherKind.Cloudy: cover = 0.58f; darken = 0.82f; fogNear = 55f; fogFar = 230f; rain = 0f; break;
                case WeatherKind.Rain: cover = 0.82f; darken = 0.6f; fogNear = 25f; fogFar = 150f; rain = 0.7f; break;
                case WeatherKind.Storm: cover = 0.93f; darken = 0.55f; fogNear = 18f; fogFar = 120f; rain = 1f; break;
                default: cover = 0.3f; darken = 1f; fogNear = 70f; fogFar = 280f; rain = 0f; break;
            }

            var k = instant ? 1f : Mathf.Clamp01(dt / transitionSeconds);
            _cloudCover = Mathf.Lerp(_cloudCover, cover, k);
            _darken = Mathf.Lerp(_darken, darken, k);
            _fogNear = Mathf.Lerp(_fogNear, fogNear, k);
            _fogFar = Mathf.Lerp(_fogFar, fogFar, k);
            RainIntensity = Mathf.Lerp(RainIntensity, rain, k);
            Wetness = instant ? (rain > 0f ? 1f : 0f)
                : rain > 0f ? Mathf.MoveTowards(Wetness, 1f, dt / 20f) : Mathf.MoveTowards(Wetness, 0f, dt / 90f);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 4f);

            // Sun path: cycle 0 = sunrise (east), 0.25 = noon, 0.5 = sunset, 0.75 = midnight.
            var cycle = Clock.SunCycle;
            var angle = cycle * 360f;
            SunHeight = Mathf.Sin(cycle * Mathf.PI * 2f);
            var sunRotation = Quaternion.Euler(angle, -60f, 0f);
            var toSun = -(sunRotation * Vector3.forward);
            var isDay = SunHeight > -0.05f;

            var zenith = Tri(new Color(0.02f, 0.035f, 0.09f), new Color(0.24f, 0.32f, 0.58f), new Color(0.24f, 0.50f, 0.90f));
            var horizon = Tri(new Color(0.05f, 0.08f, 0.16f), new Color(0.98f, 0.62f, 0.42f), new Color(0.70f, 0.84f, 0.97f));
            var sunColor = Tri(new Color(0.55f, 0.65f, 0.95f), new Color(1f, 0.62f, 0.38f), new Color(1f, 0.97f, 0.90f));
            var ambient = Tri(new Color(0.32f, 0.36f, 0.55f), new Color(0.85f, 0.80f, 0.85f), Color.white);
            var intensity = SunHeight > 0f ? Mathf.Lerp(0.65f, 1.05f, Mathf.Clamp01(SunHeight / 0.4f)) : 0.3f;

            // Overcast: gray the sky, dim the light.
            var gray = new Color(0.55f, 0.58f, 0.62f) * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(SunHeight + 0.4f));
            var overcast = 1f - _darken;
            zenith = Color.Lerp(zenith, gray * 0.85f, overcast * 1.2f);
            horizon = Color.Lerp(horizon, gray, overcast * 1.2f);
            intensity *= _darken;

            if (Sun != null)
            {
                // At night the same light becomes the moon, from the opposite direction. The elevation is
                // kept between 12 and 168 degrees so shadows never stretch across the whole map.
                var elevation = Mathf.Repeat(isDay ? angle : angle + 180f, 360f);
                elevation = Mathf.Clamp(elevation > 180f ? 12f : elevation, 12f, 168f);
                Sun.transform.rotation = Quaternion.Euler(elevation, -60f, 0f);
                Sun.color = Color.Lerp(sunColor, Color.white, 0.1f);
                Sun.intensity = intensity + _flash * 1.5f;
                Sun.shadowStrength = Mathf.Lerp(0.5f, 0.9f, _darken);
            }

            if (IsBloodMoonNight)
            {
                var red = new Color(0.55f, 0.08f, 0.10f);
                zenith = Color.Lerp(zenith, red * 0.6f, 0.6f);
                horizon = Color.Lerp(horizon, red, 0.6f);
                sunColor = Color.Lerp(sunColor, new Color(1f, 0.35f, 0.3f), 0.7f);
            }

            var fogColor = Color.Lerp(horizon, zenith, 0.25f);
            Shader.SetGlobalColor(ZenithId, zenith);
            Shader.SetGlobalColor(HorizonId, new Color(horizon.r, horizon.g, horizon.b, 1f));
            Shader.SetGlobalColor(GroundId, Color.Lerp(horizon, new Color(0.2f, 0.22f, 0.2f), 0.5f));
            Shader.SetGlobalColor(FogColorId, fogColor);
            Shader.SetGlobalVector(FogParamsId, new Vector4(_fogNear, _fogFar, 0.012f, 0.92f));
            Shader.SetGlobalColor(AerialId, new Color(0.45f, 0.58f, 0.82f, Mathf.Lerp(0.55f, 0.2f, overcast)));
            Shader.SetGlobalColor(SunColorId, sunColor);
            Shader.SetGlobalVector(SunDirId, new Vector4(toSun.x, toSun.y, toSun.z, isDay ? 1f : 0f));
            Shader.SetGlobalColor(AmbientId, new Color(ambient.r * Mathf.Lerp(0.8f, 1f, _darken), ambient.g * Mathf.Lerp(0.8f, 1f, _darken), ambient.b, 1f));
            Shader.SetGlobalVector(CloudId, new Vector4(_cloudCover, 0.012f, 1f, isDay ? Mathf.Lerp(0.25f, 0.1f, overcast) : 0f));
            Shader.SetGlobalVector(WindId, new Vector4(0.8f, 0.6f, Mathf.Lerp(0.4f, 1f, RainIntensity), 0f));
            Shader.SetGlobalFloat(WetId, Wetness);
            Shader.SetGlobalFloat(FlashId, _flash);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = zenith;
            RenderSettings.ambientEquatorColor = horizon;
            RenderSettings.ambientGroundColor = gray * 0.4f;

            Color Tri(Color night, Color dawn, Color day) =>
                SunHeight < 0f
                    ? Color.Lerp(night, dawn, 1f - Mathf.Clamp01(-SunHeight / 0.25f))
                    : Color.Lerp(dawn, day, Mathf.Clamp01(SunHeight / 0.35f));
        }
    }
}
