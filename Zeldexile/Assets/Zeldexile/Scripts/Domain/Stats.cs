using System;
using System.Collections.Generic;

namespace Zeldexile.Domain
{
    /// <summary>
    /// Effects that armor and food give the character. Every one answers a pressure of the world
    /// (cold, heat, fire, shock, noise, water, walls); there is no generic power stat.
    /// Percent stats are whole numbers (20 = +20%). Resistance stats are levels (1 or 2).
    /// </summary>
    public enum StatId
    {
        ColdResistance,
        HeatResistance,
        FlameGuard,
        ShockResistance,
        /// <summary>Percent less noise while moving.</summary>
        NoiseReduction,
        MoveSpeedPercent,
        ClimbSpeedPercent,
        ClimbCostReduction,
        SwimSpeedPercent,
        SwimCostReduction,
        AttackPercent,
        DefenseBonus,
        SentinelDamageReduction,
        NightSpeedPercent,
        Unfreezable,
        Fireproof,
        AncientProficiency
    }

    public sealed class StatBlock
    {
        private readonly Dictionary<StatId, float> _values = new Dictionary<StatId, float>();

        public float Get(StatId id) => _values.TryGetValue(id, out var v) ? v : 0f;

        public void Add(StatId id, float value) => _values[id] = Get(id) + value;

        public void Add(StatBlock other)
        {
            foreach (var pair in other._values) Add(pair.Key, pair.Value);
        }

        public void Clear() => _values.Clear();

        /// <summary>Multiplier from a percent stat: 25 → 1.25. Never below 0.1.</summary>
        public float Multiplier(StatId id) => Math.Max(0.1f, 1f + Get(id) / 100f);

        /// <summary>Cost factor from a reduction stat: 30 → 0.7. Never below 0.2.</summary>
        public float CostFactor(StatId id) => Math.Max(0.2f, 1f - Get(id) / 100f);

        public int Level(StatId id) => (int)Math.Round(Get(id));

        public static string Describe(StatId id, float value)
        {
            switch (id)
            {
                case StatId.ColdResistance: return $"Resistência ao frio nível {value:0}";
                case StatId.HeatResistance: return $"Resistência ao calor nível {value:0}";
                case StatId.FlameGuard: return $"Proteção contra chamas nível {value:0}";
                case StatId.ShockResistance: return $"Resistência a choque nível {value:0}";
                case StatId.NoiseReduction: return $"{value:0}% menos ruído";
                case StatId.MoveSpeedPercent: return $"+{value:0}% de velocidade";
                case StatId.ClimbSpeedPercent: return $"+{value:0}% de escalada";
                case StatId.ClimbCostReduction: return $"{value:0}% menos stamina ao escalar";
                case StatId.SwimSpeedPercent: return $"+{value:0}% de nado";
                case StatId.SwimCostReduction: return $"{value:0}% menos stamina no dash aquático";
                case StatId.AttackPercent: return $"+{value:0}% de ataque";
                case StatId.DefenseBonus: return $"+{value:0} de defesa";
                case StatId.SentinelDamageReduction: return $"{value:0}% menos dano de sentinelas";
                case StatId.NightSpeedPercent: return $"+{value:0}% de velocidade à noite";
                case StatId.Unfreezable: return "Não congela";
                case StatId.Fireproof: return "Imune a pegar fogo";
                case StatId.AncientProficiency: return "Armas ancestrais +50% de dano e durabilidade";
                default: return id + " " + value;
            }
        }
    }

    [Serializable]
    public struct StatModifier
    {
        public StatId Stat;
        public float Value;

        public StatModifier(StatId stat, float value)
        {
            Stat = stat;
            Value = value;
        }
    }
}
