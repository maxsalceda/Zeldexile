using NUnit.Framework;
using UnityEngine;
using Zeldexile.Domain;
using Zeldexile.World.Terrain;

namespace Zeldexile.Tests
{
    /// <summary>
    /// The trials rely on the terrain being out of reach with one stamina wheel.
    /// Numbers match PlayerMotor defaults: 1000 units, climb 2.0 m/s at 95/s, swim 2.4 m/s at 130/s.
    /// </summary>
    public sealed class TerrainRulesTests
    {
        private const float BaseClimbReach = StaminaModel.WheelUnits / 95f * 2.0f;
        private const float BaseSwimReach = StaminaModel.WheelUnits / 130f * 2.4f;

        [Test]
        public void MesaIsTallerThanBaseClimbReach()
        {
            var top = IslandShape.Height(IslandShape.Mesa.center.x, IslandShape.Mesa.center.y);
            var foot = IslandShape.Height(IslandShape.Mesa.center.x, IslandShape.Mesa.yMax + 3f);
            Assert.That(top - foot, Is.GreaterThan(BaseClimbReach + 2f));
        }

        [Test]
        public void IslandIsFartherThanBaseSwimReach()
        {
            // Walk out from the island in every direction; swimming starts where the ground is 1.2 m under the surface.
            var deep = IslandShape.WaterSurface - 1.2f;
            var shortest = float.MaxValue;
            for (var i = 0; i < 72; i++)
            {
                var dir = Quaternion.Euler(0f, i * 5f, 0f) * Vector3.forward;
                var start = -1f;
                for (var r = IslandShape.IslandRadius; r < 60f; r += 0.25f)
                {
                    var x = IslandShape.IslandCenter.x + dir.x * r;
                    var z = IslandShape.IslandCenter.y + dir.z * r;
                    var h = IslandShape.Height(x, z);
                    if (start < 0f && h < deep) start = r;
                    else if (start >= 0f && h >= deep)
                    {
                        shortest = Mathf.Min(shortest, r - start);
                        break;
                    }
                }
            }

            Assert.That(shortest, Is.GreaterThan(BaseSwimReach + 1f), $"Shortest swim is {shortest:0.0} m");
        }

        [Test]
        public void PenhascoIsClimbable()
        {
            var r = IslandShape.Penhasco;
            var top = IslandShape.Height(r.center.x, r.center.y);
            Assert.That(top, Is.LessThan(BaseClimbReach));
        }
    }
}
