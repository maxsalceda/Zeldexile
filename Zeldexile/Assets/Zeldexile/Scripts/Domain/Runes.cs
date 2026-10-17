using System;
using System.Collections.Generic;

namespace Zeldexile.Domain
{
    public enum SkillShape
    {
        Projectile,
        Cone
    }

    /// <summary>
    /// Numbers of a rune. The same values drive the combat effect and the world effect, so a rune
    /// never does something in combat without the matching reaction in the world.
    /// </summary>
    public struct SkillStats
    {
        public Element Element;
        public SkillShape Shape;
        public float Damage;
        public float Radius;
        public int Projectiles;
        public float SpreadDegrees;
        public int Chains;
        public float ChainRange;
        public float Speed;
        public float Range;
        public float Force;
        public float Cooldown;
        /// <summary>Damage multiplier against creatures that have not noticed the caster.</summary>
        public float SneakMultiplier;
    }

    /// <summary>
    /// A rune is a permanent tool, all of them given in the opening area and never replaced:
    /// the depth comes from combining them with the world, not from upgrading them.
    /// </summary>
    public sealed class RuneDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly SkillStats Base;

        public RuneDefinition(string id, string name, string description, SkillStats baseStats)
        {
            Id = id;
            Name = name;
            Description = description;
            Base = baseStats;
        }
    }

    public static class SkillBuilder
    {
        /// <summary>Final numbers: the rune's base plus the few things food and armor change.</summary>
        public static SkillStats Build(RuneDefinition rune, StatBlock character)
        {
            var stats = rune.Base;
            if (character != null) stats.Damage *= character.Multiplier(StatId.AttackPercent);
            stats.SneakMultiplier = DamageRules.SneakMultiplier;
            stats.Projectiles = Math.Max(1, stats.Projectiles);
            stats.Chains = Math.Max(0, stats.Chains);
            return stats;
        }
    }

    /// <summary>The four runes. Damage is in quarter-heart points, like weapons.</summary>
    public static class RuneCatalog
    {
        public static readonly RuneDefinition Brasa = new RuneDefinition("brasa", "Brasa",
            "Projétil de fogo. Incendeia grama (corrente de ar quente) e derrete gelo.",
            new SkillStats
            {
                Element = Element.Fire, Shape = SkillShape.Projectile, Damage = 8f, Radius = 1.5f, Projectiles = 1,
                SpreadDegrees = 12f, Speed = 22f, Range = 30f, Force = 1f, Cooldown = 1.2f, ChainRange = 6f
            });

        public static readonly RuneDefinition Geada = new RuneDefinition("geada", "Geada",
            "Projétil de gelo. Congela inimigos e ergue até 3 pilares de gelo sobre a água.",
            new SkillStats
            {
                Element = Element.Ice, Shape = SkillShape.Projectile, Damage = 4f, Radius = 1.6f, Projectiles = 1,
                SpreadDegrees = 14f, Speed = 18f, Range = 30f, Force = 0.5f, Cooldown = 1.5f, ChainRange = 6f
            });

        public static readonly RuneDefinition Faisca = new RuneDefinition("faisca", "Faísca",
            "Projétil de raio. Paralisa, derruba armas de metal e salta por metais próximos.",
            new SkillStats
            {
                Element = Element.Lightning, Shape = SkillShape.Projectile, Damage = 6f, Radius = 0.8f,
                Projectiles = 1, SpreadDegrees = 10f, Speed = 40f, Range = 35f, Force = 0.2f, Cooldown = 2f,
                Chains = 3, ChainRange = 9f
            });

        public static readonly RuneDefinition Lufada = new RuneDefinition("lufada", "Lufada",
            "Rajada de vento em cone. Empurra inimigos e objetos leves e espalha o fogo.",
            new SkillStats
            {
                Element = Element.Wind, Shape = SkillShape.Cone, Damage = 1f, Radius = 4f, Projectiles = 1,
                SpreadDegrees = 0f, Speed = 0f, Range = 7f, Force = 14f, Cooldown = 1f, ChainRange = 0f
            });

        public static readonly IReadOnlyList<RuneDefinition> Actives = new[] { Brasa, Geada, Faisca, Lufada };

        public static RuneDefinition Find(string id)
        {
            foreach (var r in Actives) if (r.Id == id) return r;
            return null;
        }
    }
}
