using System;
using System.Collections.Generic;

namespace Zeldexile.Domain
{
    public enum WeaponClass
    {
        OneHanded,
        TwoHanded,
        Spear,
        Bow,
        Shield
    }

    public enum WeaponMaterial
    {
        Wood,
        Metal,
        Bone,
        Stone,
        Ancient
    }

    /// <summary>Bonuses found only on weapons from the world (never on shop weapons).</summary>
    public enum WeaponModifier
    {
        None,
        AttackUp,
        DurabilityUp,
        CriticalHit,
        LongThrow,
        QuickShot,
        MultiShot,
        ShieldGuardUp
    }

    /// <summary>A weapon type: attack, durability and material. Tier 0 is junk, tier 5 ancestral.</summary>
    public sealed class WeaponDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly WeaponClass Class;
        public readonly int Tier;
        public readonly int Attack;
        public readonly int Durability;
        public readonly WeaponMaterial Material;
        /// <summary>Never breaks: loses energy and recharges instead (the legendary sword and shield).</summary>
        public readonly bool Anchor;

        public WeaponDefinition(string id, string name, WeaponClass cls, int tier, int attack, int durability,
            WeaponMaterial material, bool anchor = false)
        {
            Id = id;
            Name = name;
            Class = cls;
            Tier = tier;
            Attack = attack;
            Durability = durability;
            Material = material;
            Anchor = anchor;
        }

        public bool IsMetal => Material == WeaponMaterial.Metal || Material == WeaponMaterial.Ancient;
        public bool IsWood => Material == WeaponMaterial.Wood;

        public GearKind Kind => Class == WeaponClass.Bow ? GearKind.Bow : Class == WeaponClass.Shield ? GearKind.Shield : GearKind.Weapon;

        public int ComboHits => Class == WeaponClass.TwoHanded ? 3 : Class == WeaponClass.Spear ? 5 : 4;
        public int FlurryHits => Class == WeaponClass.TwoHanded ? 4 : Class == WeaponClass.Spear ? 9 : 7;
    }

    public struct HitResult
    {
        public int Damage;
        public bool Broke;
        public bool Critical;
    }

    /// <summary>One weapon in the world or in the inventory: its remaining durability and modifier.</summary>
    public sealed class WeaponInstance
    {
        public const float BadlyDamagedFraction = 0.3f;
        public const float AnchorRechargeSeconds = 480f;

        public readonly WeaponDefinition Definition;
        public WeaponModifier Modifier;
        public int ModifierValue;
        /// <summary>Yellow modifiers are the stronger tier.</summary>
        public bool YellowModifier;

        public float Durability { get; private set; }
        /// <summary>Anchor weapons: seconds until usable again after running out of energy.</summary>
        public float RechargeRemaining { get; private set; }

        public WeaponInstance(WeaponDefinition definition)
        {
            Definition = definition;
            Durability = MaxDurability;
        }

        public string Name => Definition.Name;
        public WeaponClass Class => Definition.Class;
        public GearKind Kind => Definition.Kind;

        public int Attack => Definition.Attack + (Modifier == WeaponModifier.AttackUp ? ModifierValue : 0);
        public int Guard => Definition.Class == WeaponClass.Shield
            ? Definition.Attack + (Modifier == WeaponModifier.ShieldGuardUp ? ModifierValue : 0)
            : 0;
        public int MaxDurability => Definition.Durability + (Modifier == WeaponModifier.DurabilityUp ? ModifierValue : 0);
        public float DurabilityNormalized => Durability / MaxDurability;
        public bool IsBroken => !Definition.Anchor && Durability <= 0f;
        public bool IsResting => Definition.Anchor && RechargeRemaining > 0f;
        public bool IsUsable => !IsBroken && !IsResting;
        public bool BadlyDamaged => !Definition.Anchor && Durability <= MaxDurability * BadlyDamagedFraction;
        public int Projectiles => Modifier == WeaponModifier.MultiShot ? 3 : 1;
        public float ThrowRangeMultiplier => Modifier == WeaponModifier.LongThrow ? 1.5f : 1f;

        public string ModifierText
        {
            get
            {
                switch (Modifier)
                {
                    case WeaponModifier.AttackUp: return $"Ataque +{ModifierValue}";
                    case WeaponModifier.DurabilityUp: return $"Durabilidade +{ModifierValue}";
                    case WeaponModifier.CriticalHit: return "Golpe crítico";
                    case WeaponModifier.LongThrow: return "Arremesso longo";
                    case WeaponModifier.QuickShot: return "Tiro rápido";
                    case WeaponModifier.MultiShot: return "Tiro triplo";
                    case WeaponModifier.ShieldGuardUp: return $"Guarda +{ModifierValue}";
                    default: return "";
                }
            }
        }

        /// <summary>
        /// One hit that connected. Consumes 1 durability; the hit that breaks the weapon deals double damage.
        /// Anchor weapons rest for 8 minutes instead of breaking.
        /// </summary>
        public HitResult Strike(float multiplier = 1f, float attackPercent = 0f, bool lastOfCombo = false)
        {
            var result = new HitResult();
            if (!IsUsable) return result;
            var m = multiplier;
            if (lastOfCombo && Modifier == WeaponModifier.CriticalHit)
            {
                m *= 2f;
                result.Critical = true;
            }

            Durability -= 1f;
            if (Durability <= 0f)
            {
                Durability = 0f;
                m *= DamageRules.LastHitMultiplier;
                result.Critical = true;
                if (Definition.Anchor) RechargeRemaining = AnchorRechargeSeconds;
                else result.Broke = true;
            }

            result.Damage = DamageRules.Dealt(Attack, m, attackPercent);
            return result;
        }

        /// <summary>Thrown at a creature: full damage (double when badly damaged) and the weapon breaks.</summary>
        public HitResult Throw(float attackPercent = 0f)
        {
            var result = new HitResult();
            if (Definition.Anchor || IsBroken) return result;
            var m = BadlyDamaged ? DamageRules.ThrowDamagedMultiplier : 1f;
            result.Critical = BadlyDamaged;
            result.Damage = DamageRules.Dealt(Attack, m, attackPercent);
            Durability = 0f;
            result.Broke = true;
            return result;
        }

        /// <summary>
        /// Shield block. Attacks below the guard value do no damage and cost 1 durability; stronger attacks
        /// get through by the difference and cost 2. A perfect guard costs nothing (handled by the caller).
        /// </summary>
        public int Block(int incomingAttack, out bool broke)
        {
            broke = false;
            if (!IsUsable) return incomingAttack;
            var through = Math.Max(0, incomingAttack - Guard);
            Durability -= through > 0 ? 2f : 1f;
            if (Durability <= 0f)
            {
                Durability = 0f;
                if (Definition.Anchor) RechargeRemaining = AnchorRechargeSeconds;
                else broke = true;
            }

            return through;
        }

        /// <summary>Shield surfing on rock: 1 durability every 3 seconds.</summary>
        public void Wear(float amount)
        {
            if (!IsUsable) return;
            Durability = Math.Max(0f, Durability - amount);
            if (Durability <= 0f && Definition.Anchor) RechargeRemaining = AnchorRechargeSeconds;
        }

        public void TickRecharge(float dt)
        {
            if (RechargeRemaining <= 0f) return;
            RechargeRemaining = Math.Max(0f, RechargeRemaining - dt);
            if (RechargeRemaining <= 0f) Durability = MaxDurability;
        }
    }

    /// <summary>
    /// The weapon curve: attack ≈ ×1.4 and durability ≈ ×1.25 per tier, with class multipliers,
    /// one glass weapon (huge attack, breaks fast) and one anchor per class.
    /// </summary>
    public static class WeaponCatalog
    {
        private static readonly int[] OneHandedAttack = { 4, 10, 18, 26, 36, 45 };
        private static readonly int[] OneHandedDurability = { 8, 20, 26, 32, 40, 50 };
        private static readonly int[] BowAttack = { 4, 8, 14, 22, 30, 40 };
        private static readonly int[] BowDurability = { 20, 30, 40, 55, 70, 90 };
        private static readonly int[] ShieldGuard = { 10, 20, 35, 50, 65, 80 };
        private static readonly int[] ShieldDurability = { 10, 20, 35, 50, 70, 90 };

        private static readonly string[] OneHandedNames =
            { "Galho seco", "Espada de viajante", "Espada de soldado", "Espada de cavaleiro", "Espada real", "Lâmina ancestral" };

        private static readonly string[] TwoHandedNames =
            { "Tronco pesado", "Machado de lenhador", "Montante de soldado", "Montante de cavaleiro", "Montante real", "Serra ancestral" };

        private static readonly string[] SpearNames =
            { "Estaca de madeira", "Lança de viajante", "Lança de soldado", "Lança de cavaleiro", "Lança real", "Lança ancestral" };

        private static readonly string[] BowNames =
            { "Arco de galho", "Arco de viajante", "Arco de soldado", "Arco de cavaleiro", "Arco real", "Arco ancestral" };

        private static readonly string[] ShieldNames =
            { "Tampa de panela", "Escudo de madeira", "Escudo de soldado", "Escudo de cavaleiro", "Escudo real", "Escudo ancestral" };

        public static readonly WeaponDefinition GlassSword =
            new WeaponDefinition("guarda_real", "Espada da guarda real", WeaponClass.OneHanded, 5, 48, 12, WeaponMaterial.Metal);

        public static readonly WeaponDefinition LegendarySword =
            new WeaponDefinition("espada_lendaria", "Espada lendária", WeaponClass.OneHanded, 4, 28, 40, WeaponMaterial.Metal, anchor: true);

        public static readonly WeaponDefinition LegendaryShield =
            new WeaponDefinition("escudo_lendario", "Escudo lendário", WeaponClass.Shield, 5, 80, 600, WeaponMaterial.Metal, anchor: true);

        public static readonly IReadOnlyList<WeaponDefinition> All;
        private static readonly Dictionary<string, WeaponDefinition> ById = new Dictionary<string, WeaponDefinition>();

        static WeaponCatalog()
        {
            var list = new List<WeaponDefinition>();
            for (var tier = 0; tier <= 5; tier++)
            {
                var metal = tier >= 1 ? WeaponMaterial.Metal : WeaponMaterial.Wood;
                if (tier == 5) metal = WeaponMaterial.Ancient;
                list.Add(new WeaponDefinition("espada_" + tier, OneHandedNames[tier], WeaponClass.OneHanded, tier,
                    OneHandedAttack[tier], OneHandedDurability[tier], metal));
                list.Add(new WeaponDefinition("montante_" + tier, TwoHandedNames[tier], WeaponClass.TwoHanded, tier,
                    Round(OneHandedAttack[tier] * 1.5f), Round(OneHandedDurability[tier] * 1.1f), metal));
                list.Add(new WeaponDefinition("lanca_" + tier, SpearNames[tier], WeaponClass.Spear, tier,
                    Round(OneHandedAttack[tier] * 0.7f), Round(OneHandedDurability[tier] * 1.3f),
                    tier <= 1 ? WeaponMaterial.Wood : metal));
                list.Add(new WeaponDefinition("arco_" + tier, BowNames[tier], WeaponClass.Bow, tier,
                    BowAttack[tier], BowDurability[tier], tier == 5 ? WeaponMaterial.Ancient : WeaponMaterial.Wood));
                list.Add(new WeaponDefinition("escudo_" + tier, ShieldNames[tier], WeaponClass.Shield, tier,
                    ShieldGuard[tier], ShieldDurability[tier],
                    tier <= 1 ? WeaponMaterial.Wood : tier == 5 ? WeaponMaterial.Ancient : WeaponMaterial.Metal));
            }

            list.Add(GlassSword);
            list.Add(LegendarySword);
            list.Add(LegendaryShield);
            All = list;
            foreach (var w in list) ById[w.Id] = w;
        }

        private static int Round(float v) => (int)Math.Round(v);

        public static WeaponDefinition Find(string id) => ById.TryGetValue(id, out var w) ? w : null;

        /// <summary>The regular weapon of a class at a tier (0 to 5).</summary>
        public static WeaponDefinition ForTier(WeaponClass cls, int tier)
        {
            tier = Math.Max(0, Math.Min(5, tier));
            string prefix;
            switch (cls)
            {
                case WeaponClass.TwoHanded: prefix = "montante_"; break;
                case WeaponClass.Spear: prefix = "lanca_"; break;
                case WeaponClass.Bow: prefix = "arco_"; break;
                case WeaponClass.Shield: prefix = "escudo_"; break;
                default: prefix = "espada_"; break;
            }

            return Find(prefix + tier);
        }

        public static string ClassName(WeaponClass cls)
        {
            switch (cls)
            {
                case WeaponClass.TwoHanded: return "Duas mãos";
                case WeaponClass.Spear: return "Lança";
                case WeaponClass.Bow: return "Arco";
                case WeaponClass.Shield: return "Escudo";
                default: return "Uma mão";
            }
        }

        /// <summary>Modifier roll for a weapon found in the world, following the reference odds.</summary>
        public static void RollModifier(WeaponInstance weapon, Random random, bool yellow)
        {
            var r = random.NextDouble();
            weapon.YellowModifier = yellow;
            switch (weapon.Class)
            {
                case WeaponClass.Shield:
                    weapon.Modifier = r < 0.5 ? WeaponModifier.DurabilityUp : WeaponModifier.ShieldGuardUp;
                    break;
                case WeaponClass.Bow:
                    weapon.Modifier = r < 0.25 ? WeaponModifier.AttackUp : r < 0.5 ? WeaponModifier.DurabilityUp
                        : r < 0.75 ? WeaponModifier.QuickShot : WeaponModifier.MultiShot;
                    break;
                default:
                    weapon.Modifier = r < 0.25 ? WeaponModifier.AttackUp : r < 0.583 ? WeaponModifier.DurabilityUp
                        : yellow ? WeaponModifier.LongThrow : WeaponModifier.CriticalHit;
                    break;
            }

            switch (weapon.Modifier)
            {
                case WeaponModifier.AttackUp: weapon.ModifierValue = yellow ? random.Next(9, 16) : random.Next(3, 9); break;
                case WeaponModifier.DurabilityUp: weapon.ModifierValue = yellow ? random.Next(11, 21) : random.Next(5, 11); break;
                case WeaponModifier.ShieldGuardUp: weapon.ModifierValue = yellow ? 20 : 10; break;
                default: weapon.ModifierValue = 0; break;
            }
        }
    }
}
