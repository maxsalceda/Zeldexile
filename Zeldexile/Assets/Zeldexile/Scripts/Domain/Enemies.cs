using System;
using System.Collections.Generic;

namespace Zeldexile.Domain
{
    /// <summary>Few families, many variations: tier (color), element, weapon and context.</summary>
    public enum EnemyFamily
    {
        Grunt,
        Brute,
        Agile,
        Scout,
        Elite,
        Giant,
        Golem,
        Sentinel
    }

    /// <summary>Red → blue → black → silver: HP ×3 per step; the tier also sets the weapon carried.</summary>
    public enum EnemyTier
    {
        Red,
        Blue,
        Black,
        Silver
    }

    public static class EnemyStats
    {
        private static readonly Dictionary<EnemyFamily, int[]> Health = new Dictionary<EnemyFamily, int[]>
        {
            { EnemyFamily.Grunt, new[] { 12, 40, 130, 400 } },
            { EnemyFamily.Brute, new[] { 45, 120, 300, 900 } },
            { EnemyFamily.Agile, new[] { 40, 100, 240, 720 } },
            { EnemyFamily.Scout, new[] { 10, 80, 1200, 2400 } },
            { EnemyFamily.Elite, new[] { 1500, 2250, 3000, 3000 } },
            { EnemyFamily.Giant, new[] { 500, 700, 900, 900 } },
            { EnemyFamily.Golem, new[] { 250, 600, 750, 750 } },
            { EnemyFamily.Sentinel, new[] { 1200, 1200, 1200, 1200 } }
        };

        private static readonly Dictionary<EnemyFamily, int[]> Attack = new Dictionary<EnemyFamily, int[]>
        {
            { EnemyFamily.Grunt, new[] { 4, 10, 20, 36 } },
            { EnemyFamily.Brute, new[] { 8, 16, 28, 48 } },
            { EnemyFamily.Agile, new[] { 6, 12, 24, 40 } },
            { EnemyFamily.Scout, new[] { 4, 12, 24, 40 } },
            { EnemyFamily.Elite, new[] { 24, 36, 48, 48 } },
            { EnemyFamily.Giant, new[] { 20, 26, 32, 32 } },
            { EnemyFamily.Golem, new[] { 16, 24, 24, 24 } },
            { EnemyFamily.Sentinel, new[] { 60, 60, 60, 60 } }
        };

        public static int HealthOf(EnemyFamily family, EnemyTier tier) => Health[family][(int)tier];
        public static int AttackOf(EnemyFamily family, EnemyTier tier) => Attack[family][(int)tier];

        /// <summary>Weapon tier a creature carries: red 0 or 1, blue 2, black 3, silver 4.</summary>
        public static int WeaponTierOf(EnemyTier tier, bool better = false) =>
            tier == EnemyTier.Red ? (better ? 1 : 0) : (int)tier + 1;

        /// <summary>Points toward the world's difficulty scale for one kill.</summary>
        public static int ScalePoints(EnemyFamily family)
        {
            switch (family)
            {
                case EnemyFamily.Grunt: return 1;
                case EnemyFamily.Brute:
                case EnemyFamily.Agile:
                case EnemyFamily.Scout: return 2;
                case EnemyFamily.Giant:
                case EnemyFamily.Golem: return 6;
                case EnemyFamily.Sentinel: return 8;
                default: return 10;
            }
        }

        public static string FamilyName(EnemyFamily family)
        {
            switch (family)
            {
                case EnemyFamily.Grunt: return "Cascudo";
                case EnemyFamily.Brute: return "Brutamontes";
                case EnemyFamily.Agile: return "Lagartão";
                case EnemyFamily.Scout: return "Vigia";
                case EnemyFamily.Elite: return "Juba de Ferro";
                case EnemyFamily.Giant: return "Gigante";
                case EnemyFamily.Golem: return "Golem de pedra";
                default: return "Sentinela";
            }
        }

        public static string TierName(EnemyTier tier)
        {
            switch (tier)
            {
                case EnemyTier.Blue: return "azul";
                case EnemyTier.Black: return "negro";
                case EnemyTier.Silver: return "prateado";
                default: return "vermelho";
            }
        }

        public static string DisplayName(EnemyFamily family, EnemyTier tier) =>
            tier == EnemyTier.Red ? FamilyName(family) : $"{FamilyName(family)} {TierName(tier)}";
    }

    /// <summary>
    /// Difficulty scale: kills add points (each species counts up to 10 kills); thresholds promote
    /// enemy tiers and the weapons they carry. There is no player level: the world hardens because
    /// the player beat it.
    /// </summary>
    public sealed class WorldLevel
    {
        public const int KillCap = 10;
        public const int BlueThreshold = 60;
        public const int ModifierThreshold = 120;
        public const int BlackThreshold = 180;
        public const int SilverThreshold = 400;

        private readonly Dictionary<EnemyFamily, int> _kills = new Dictionary<EnemyFamily, int>();

        public int Points { get; private set; }

        public void RegisterKill(EnemyFamily family)
        {
            _kills.TryGetValue(family, out var count);
            if (count >= KillCap) return;
            _kills[family] = count + 1;
            Points += EnemyStats.ScalePoints(family);
        }

        public int Kills(EnemyFamily family) => _kills.TryGetValue(family, out var c) ? c : 0;

        public static EnemyTier TierFor(int points)
        {
            if (points >= SilverThreshold) return EnemyTier.Silver;
            if (points >= BlackThreshold) return EnemyTier.Black;
            if (points >= BlueThreshold) return EnemyTier.Blue;
            return EnemyTier.Red;
        }

        /// <summary>Spawn tier: the creature's own tier, or the world's if that is higher.</summary>
        public EnemyTier Promote(EnemyTier baseTier)
        {
            var world = TierFor(Points);
            return (int)world > (int)baseTier ? world : baseTier;
        }

        public bool ModifiersUnlocked => Points >= ModifierThreshold;
        public bool YellowModifiersUnlocked => Points >= SilverThreshold;
    }
}
