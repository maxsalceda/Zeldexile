using System;

namespace Zeldexile.Domain
{
    /// <summary>
    /// Damage in quarter-heart points. The same rules apply to the player and to creatures.
    /// </summary>
    public static class DamageRules
    {
        public const int MinimumDamage = 1;
        public const float SneakMultiplier = 8f;
        public const float FrozenMultiplier = 3f;
        public const float LastHitMultiplier = 2f;
        public const float HeadshotMultiplier = 2f;
        public const float ThrowDamagedMultiplier = 2f;
        public const float FallSafeMeters = 6f;
        public const float FallLethalMeters = 30f;

        /// <summary>Points lost by a defender: attack minus defense, never below one quarter heart.</summary>
        public static int Taken(int attack, int defense) => Math.Max(MinimumDamage, attack - Math.Max(0, defense));

        /// <summary>Points dealt by an attacker: weapon attack × situation multiplier × Mighty bonus.</summary>
        public static int Dealt(int attack, float multiplier, float attackPercent = 0f) =>
            Math.Max(MinimumDamage, (int)Math.Round(attack * multiplier * (1f + attackPercent / 100f)));

        /// <summary>Fall damage: none up to 6 m, one heart to 12 m, a heart per 3 m beyond, death from 30 m.</summary>
        public static int FallDamage(float meters)
        {
            if (meters <= FallSafeMeters) return 0;
            if (meters >= FallLethalMeters) return int.MaxValue;
            if (meters <= 12f) return LifeModel.PointsPerHeart;
            return LifeModel.PointsPerHeart + LifeModel.PointsPerHeart * (int)Math.Ceiling((meters - 12f) / 3f);
        }

        public static float MightyPercent(int level) => level <= 0 ? 0f : level == 1 ? 20f : level == 2 ? 30f : 50f;
        public static int ToughBonus(int level) => level <= 0 ? 0 : level == 1 ? 4 : level == 2 ? 8 : 12;
        public static float HastyPercent(int level) => level <= 0 ? 0f : 10f * Math.Min(3, level);
        public static float SneakyNoiseReduction(int level) => level <= 0 ? 0f : level == 1 ? 30f : level == 2 ? 50f : 70f;
    }
}
