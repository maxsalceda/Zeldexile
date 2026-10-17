using System.Linq;
using NUnit.Framework;
using Zeldexile.Domain;

namespace Zeldexile.Tests
{
    public sealed class ClimateTests
    {
        [Test]
        public void ADayLastsTwentyFourRealMinutes()
        {
            var clock = new GameClock(6.0);
            clock.Advance(24f * 60f);
            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.Hour, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void DayAndNightBoundaries()
        {
            Assert.That(new GameClock(5.9).IsDay, Is.False);
            Assert.That(new GameClock(6.0).IsDay, Is.True);
            Assert.That(new GameClock(18.0).IsDay, Is.False);
            Assert.That(new GameClock(18.0).SunCycle, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(new GameClock(22.0).IsCreatureNight, Is.True);
            Assert.That(new GameClock(7.0).IsCreatureNight, Is.False);
        }

        [Test]
        public void BloodMoonEveryFiveDaysAtMidnight()
        {
            Assert.That(BloodMoon.IsBloodMoonDay(5), Is.True);
            Assert.That(BloodMoon.IsBloodMoonDay(4), Is.False);
            Assert.That(BloodMoon.Crossed(4 * 24 + 23.9, 5 * 24 + 0.1), Is.True);
            Assert.That(BloodMoon.Crossed(3 * 24 + 23.9, 4 * 24 + 0.1), Is.False);
            Assert.That(BloodMoon.IsBloodMoonNight(new GameClock(4 * 24 + 23.6)), Is.True);
            Assert.That(BloodMoon.IsBloodMoonNight(new GameClock(4 * 24 + 22.0)), Is.False);
        }

        [Test]
        public void WeatherFollowsWeights()
        {
            var table = WeatherTable.Temperate(new System.Random(1));
            var rolls = Enumerable.Range(0, 20000).Select(_ => table.Roll()).ToList();
            var clear = rolls.Count(r => r == WeatherKind.Clear) / 20000f;
            var storm = rolls.Count(r => r == WeatherKind.Storm) / 20000f;
            Assert.That(clear, Is.EqualTo(0.55f).Within(0.02f));
            Assert.That(storm, Is.EqualTo(0.08f).Within(0.01f));
        }

        [Test]
        public void ForecastShiftsWhenASlotEnds()
        {
            var table = WeatherTable.Temperate(new System.Random(3));
            table.At(0.0);
            var next = table.Forecast(1);
            var current = table.At(WeatherTable.HoursPerSlot + 0.1);
            Assert.That(current, Is.EqualTo(next));
        }

        [Test]
        public void AltitudeNightAndRainMakeItCold()
        {
            var valleyNoon = Temperature.At(14f, 0f, WeatherKind.Clear, 0f);
            var peakNight = Temperature.At(3f, 40f, WeatherKind.Rain, 0f);
            Assert.That(valleyNoon, Is.EqualTo(Temperature.DayBase).Within(0.01f));
            Assert.That(peakNight, Is.LessThan(Temperature.SevereColdThreshold));
            Assert.That(Temperature.DamageInterval(peakNight, 0, 0), Is.EqualTo(Temperature.Level2TickSeconds));
            Assert.That(Temperature.DamageInterval(valleyNoon, 0, 0), Is.EqualTo(0f));
        }

        [Test]
        public void ProtectionLevelsCancelTheSameDangerLevel()
        {
            Assert.That(Temperature.DamageInterval(-5f, 1, 0), Is.EqualTo(0f));
            Assert.That(Temperature.DamageInterval(-15f, 1, 0), Is.EqualTo(Temperature.Level1TickSeconds));
            Assert.That(Temperature.DamageInterval(-15f, 2, 0), Is.EqualTo(0f));
            Assert.That(Temperature.DamageInterval(45f, 0, 0), Is.EqualTo(Temperature.Level1TickSeconds));
            Assert.That(Temperature.DamageInterval(55f, 0, 1), Is.EqualTo(Temperature.Level1TickSeconds));
        }

        [Test]
        public void FireWarms()
        {
            var cold = Temperature.At(3f, 30f, WeatherKind.Clear, 0f);
            var byFire = Temperature.At(3f, 30f, WeatherKind.Clear, 1f);
            Assert.That(byFire, Is.GreaterThan(cold + 20f));
        }
    }
}
