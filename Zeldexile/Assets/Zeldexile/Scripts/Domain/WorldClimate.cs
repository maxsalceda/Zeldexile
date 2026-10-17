using System;

namespace Zeldexile.Domain
{
    /// <summary>
    /// In-game time. One real second is one game minute, so a full day lasts 24 real minutes.
    /// Night runs from 21:00 to 05:00 for creatures; the sun cycle itself turns at 06:00 and 18:00.
    /// </summary>
    public sealed class GameClock
    {
        public const float GameMinutesPerRealSecond = 1f;
        public const float DayStartHour = 6f;
        public const float NightStartHour = 18f;
        public const float CreatureNightStart = 21f;
        public const float CreatureNightEnd = 5f;

        /// <summary>Hours since day 0, 00:00.</summary>
        public double TotalHours { get; private set; }

        public GameClock(double startHour) => TotalHours = startHour;

        public float Hour => (float)(TotalHours % 24.0);
        public int Day => (int)(TotalHours / 24.0);
        public bool IsDay => Hour >= DayStartHour && Hour < NightStartHour;
        /// <summary>Deep night, when skeletons rise and creatures sleep.</summary>
        public bool IsCreatureNight => Hour >= CreatureNightStart || Hour < CreatureNightEnd;

        /// <summary>0 at 06:00, 0.5 at 18:00 (sun sets), 1 back at 06:00.</summary>
        public float SunCycle => Mod((Hour - DayStartHour) / 24f, 1f);

        public void Advance(float realSeconds) => TotalHours += realSeconds * GameMinutesPerRealSecond / 60.0;

        public void SetHour(float hour) => TotalHours = Day * 24.0 + hour;

        /// <summary>Jumps to a day and hour (resting, debug and tests).</summary>
        public void Set(int day, float hour) => TotalHours = day * 24.0 + hour;

        public string Display => $"{(int)Hour:00}:{(int)(Hour % 1f * 60f):00}";

        private static float Mod(float a, float b) => (a % b + b) % b;
    }

    /// <summary>
    /// Every 5 game days, at midnight, the Blood Moon brings back every defeated creature and every
    /// weapon picked up from the world. Cooking around that midnight is always a critical success.
    /// </summary>
    public static class BloodMoon
    {
        public const int PeriodDays = 5;

        /// <summary>Day index whose midnight (start of the day) is a blood moon: days 5, 10, 15...</summary>
        public static bool IsBloodMoonDay(int day) => day > 0 && day % PeriodDays == 0;

        /// <summary>True when the clock crossed a blood-moon midnight between two readings.</summary>
        public static bool Crossed(double previousTotalHours, double totalHours)
        {
            var before = (int)(previousTotalHours / 24.0);
            var after = (int)(totalHours / 24.0);
            for (var d = before + 1; d <= after; d++)
                if (IsBloodMoonDay(d))
                    return true;
            return false;
        }

        /// <summary>The night leading into a blood-moon midnight (23:30 to 00:15 is the cooking window).</summary>
        public static bool IsBloodMoonNight(GameClock clock) =>
            (clock.Hour >= 23.5f && IsBloodMoonDay(clock.Day + 1)) || (clock.Hour < 0.25f && IsBloodMoonDay(clock.Day));
    }

    public enum WeatherKind
    {
        Clear,
        Cloudy,
        Rain,
        Storm
    }

    /// <summary>
    /// Weighted weather roll per climate, re-rolled every few game hours. The next slots are rolled ahead,
    /// so the HUD can show a forecast.
    /// </summary>
    public sealed class WeatherTable
    {
        public const float HoursPerSlot = 4f;

        private readonly float[] _weights;
        private readonly Random _random;
        private readonly WeatherKind[] _slots = new WeatherKind[3];
        private int _slotIndex = -1;

        /// <param name="weights">Clear, Cloudy, Rain, Storm.</param>
        public WeatherTable(float[] weights, Random random)
        {
            if (weights.Length != 4) throw new ArgumentException("Four weights: clear, cloudy, rain, storm.");
            _weights = weights;
            _random = random;
        }

        public static WeatherTable Temperate(Random random) => new WeatherTable(new[] { 55f, 25f, 12f, 8f }, random);

        public WeatherKind Roll()
        {
            var total = 0f;
            foreach (var w in _weights) total += w;
            var r = (float)_random.NextDouble() * total;
            for (var i = 0; i < _weights.Length; i++)
            {
                if (r < _weights[i]) return (WeatherKind)i;
                r -= _weights[i];
            }

            return WeatherKind.Clear;
        }

        /// <summary>Weather for the slot containing this time; rolls new slots as time moves on.</summary>
        public WeatherKind At(double totalHours)
        {
            var slot = (int)(totalHours / HoursPerSlot);
            if (_slotIndex < 0)
            {
                _slotIndex = slot;
                for (var i = 0; i < _slots.Length; i++) _slots[i] = Roll();
            }

            while (_slotIndex < slot)
            {
                _slots[0] = _slots[1];
                _slots[1] = _slots[2];
                _slots[2] = Roll();
                _slotIndex++;
            }

            return _slots[0];
        }

        /// <summary>Current and the next two slots (call At first).</summary>
        public WeatherKind Forecast(int ahead) => _slots[Math.Max(0, Math.Min(2, ahead))];

        /// <summary>Forces the current slot (debug and tests).</summary>
        public void Force(WeatherKind kind) => _slots[0] = kind;

        public static string DisplayName(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Cloudy: return "Nublado";
                case WeatherKind.Rain: return "Chuva";
                case WeatherKind.Storm: return "Tempestade";
                default: return "Céu limpo";
            }
        }
    }

    /// <summary>
    /// Air temperature at a point: warm by day, cooler at night, colder with altitude and rain,
    /// warmer next to fire. Cold hurts below 0 °C (level 1) and below -10 °C (level 2); heat hurts
    /// above 40 °C (level 1) and above 50 °C (level 2). Each level needs the same level of protection.
    /// </summary>
    public static class Temperature
    {
        public const float DayBase = 24f;
        public const float NightBase = 11f;
        public const float LapsePerMeter = 0.55f;
        public const float RainChill = 4f;
        public const float FireWarmth = 22f;

        public const float ColdThreshold = 0f;
        public const float SevereColdThreshold = -10f;
        public const float HotThreshold = 40f;
        public const float SevereHotThreshold = 50f;

        /// <summary>Seconds between quarter-heart ticks at each danger level.</summary>
        public const float Level1TickSeconds = 5f;
        public const float Level2TickSeconds = 2.5f;

        public static float At(float hour, float altitude, WeatherKind weather, float fireProximity01)
        {
            // Smooth day curve: coldest at 04:00, warmest at 14:00.
            var t = (float)Math.Cos((hour - 14f) / 24f * Math.PI * 2.0) * 0.5f + 0.5f;
            var baseTemp = NightBase + (DayBase - NightBase) * t;
            var wet = weather == WeatherKind.Rain || weather == WeatherKind.Storm ? RainChill : 0f;
            return baseTemp - Math.Max(0f, altitude) * LapsePerMeter - wet + FireWarmth * Clamp01(fireProximity01);
        }

        /// <summary>Cold danger: 0 safe, 1 below freezing, 2 below -10 °C.</summary>
        public static int ColdLevel(float celsius) => celsius < SevereColdThreshold ? 2 : celsius < ColdThreshold ? 1 : 0;

        /// <summary>Heat danger: 0 safe, 1 above 40 °C, 2 above 50 °C.</summary>
        public static int HeatLevel(float celsius) => celsius > SevereHotThreshold ? 2 : celsius > HotThreshold ? 1 : 0;

        /// <summary>
        /// Seconds between one-point ticks of damage after protection, or 0 when safe. Protection of the
        /// same level as the danger cancels it; one level short leaves level-1 damage.
        /// </summary>
        public static float DamageInterval(float celsius, int coldProtection, int heatProtection)
        {
            var cold = Math.Max(0, ColdLevel(celsius) - Math.Max(0, coldProtection));
            var heat = Math.Max(0, HeatLevel(celsius) - Math.Max(0, heatProtection));
            var level = Math.Max(cold, heat);
            return level >= 2 ? Level2TickSeconds : level == 1 ? Level1TickSeconds : 0f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
