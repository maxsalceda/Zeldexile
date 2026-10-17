using System;
using System.Collections.Generic;

namespace Zeldexile.Domain
{
    public enum ArmorSlot
    {
        Head,
        Body,
        Legs
    }

    /// <summary>Each set solves one pressure of the world; its set bonus deepens the same pressure.</summary>
    public sealed class ArmorSetDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int BaseDefense;
        public readonly StatModifier Effect;
        public readonly StatModifier SetBonus;
        public readonly string[] PieceNames;
        public readonly int[] Prices;
        public readonly bool IsMetal;

        public ArmorSetDefinition(string id, string name, int baseDefense, StatModifier effect, StatModifier setBonus,
            string[] pieceNames, int[] prices, bool isMetal = false)
        {
            Id = id;
            Name = name;
            BaseDefense = baseDefense;
            Effect = effect;
            SetBonus = setBonus;
            PieceNames = pieceNames;
            Prices = prices;
            IsMetal = isMetal;
        }

        public bool HasSetBonus => SetBonus.Value != 0f;
    }

    /// <summary>One worn or stored piece and its upgrade level (0 to 4).</summary>
    public sealed class ArmorPiece
    {
        public const int MaxLevel = 4;

        public readonly ArmorSetDefinition Set;
        public readonly ArmorSlot Slot;
        public int Level;

        public ArmorPiece(ArmorSetDefinition set, ArmorSlot slot, int level = 0)
        {
            Set = set;
            Slot = slot;
            Level = level;
        }

        public string Name => Set.PieceNames[(int)Slot];
        public int Defense => Armor.DefenseFor(Set.BaseDefense, Level);
        public string Stars => Level == 0 ? "" : new string('★', Level);
    }

    public static class Armor
    {
        public const int SetBonusLevel = 2;

        /// <summary>Defense per piece and level, from the reference curve (3 → 5 → 8 → 12 → 20).</summary>
        public static int DefenseFor(int baseDefense, int level)
        {
            level = Math.Max(0, Math.Min(ArmorPiece.MaxLevel, level));
            switch (baseDefense)
            {
                case 1: return new[] { 1, 2, 3, 4, 6 }[level];
                case 2: return new[] { 2, 4, 6, 9, 14 }[level];
                case 3: return new[] { 3, 5, 8, 12, 20 }[level];
                case 4: return new[] { 4, 7, 12, 18, 28 }[level];
                default: return baseDefense + level * baseDefense;
            }
        }

        /// <summary>Materials for the next level: counts of (common, monster part) by level.</summary>
        public static string UpgradeMaterials(int nextLevel)
        {
            switch (nextLevel)
            {
                case 1: return "3 materiais comuns";
                case 2: return "5 comuns + 3 partes de monstro azul";
                case 3: return "5 raros + 5 partes de monstro negro";
                case 4: return "8 raros + 3 partes de elite";
                default: return "";
            }
        }

        /// <summary>Set bonus active with all three pieces of one set at level 2 or more.</summary>
        public static bool SetBonusActive(ArmorPiece head, ArmorPiece body, ArmorPiece legs)
        {
            if (head == null || body == null || legs == null) return false;
            if (head.Set != body.Set || body.Set != legs.Set || !head.Set.HasSetBonus) return false;
            return head.Level >= SetBonusLevel && body.Level >= SetBonusLevel && legs.Level >= SetBonusLevel;
        }

        public static int TotalDefense(ArmorPiece head, ArmorPiece body, ArmorPiece legs) =>
            (head?.Defense ?? 0) + (body?.Defense ?? 0) + (legs?.Defense ?? 0);

        /// <summary>Stats from the worn pieces: each piece adds its set's effect, the full set its bonus.</summary>
        public static void AddStats(StatBlock block, ArmorPiece head, ArmorPiece body, ArmorPiece legs)
        {
            foreach (var piece in new[] { head, body, legs })
            {
                if (piece == null || piece.Set.Effect.Value == 0f) continue;
                block.Add(piece.Set.Effect.Stat, piece.Set.Effect.Value);
            }

            // Resistances cap at level 2; three pieces of the same cold set still give level 2.
            foreach (var id in new[] { StatId.ColdResistance, StatId.HeatResistance, StatId.FlameGuard })
                if (block.Get(id) > 2f)
                    block.Add(id, 2f - block.Get(id));

            if (SetBonusActive(head, body, legs)) block.Add(head.Set.SetBonus.Stat, head.Set.SetBonus.Value);
        }

        public static string SlotName(ArmorSlot slot)
        {
            switch (slot)
            {
                case ArmorSlot.Head: return "Cabeça";
                case ArmorSlot.Body: return "Corpo";
                default: return "Pernas";
            }
        }
    }

    public static class ArmorCatalog
    {
        private static StatModifier S(StatId id, float v) => new StatModifier(id, v);
        private static readonly StatModifier None = new StatModifier(StatId.DefenseBonus, 0f);

        public static readonly ArmorSetDefinition OldClothes = new ArmorSetDefinition("velha", "Roupa velha", 1, None, None,
            new[] { "Gorro velho", "Camisa velha", "Calça surrada" }, new[] { 0, 0, 0 });

        public static readonly ArmorSetDefinition Basic = new ArmorSetDefinition("basico", "Traje do campo", 3, None, None,
            new[] { "Capuz do campo", "Túnica do campo", "Calça do campo" }, new[] { 60, 120, 90 });

        public static readonly ArmorSetDefinition Stealth = new ArmorSetDefinition("furtivo", "Traje furtivo", 2,
            S(StatId.NoiseReduction, 30f), S(StatId.NightSpeedPercent, 20f),
            new[] { "Máscara furtiva", "Peitoral furtivo", "Meias furtivas" }, new[] { 300, 400, 300 });

        public static readonly ArmorSetDefinition Cold = new ArmorSetDefinition("frio", "Traje de penas", 3,
            S(StatId.ColdResistance, 1f), S(StatId.Unfreezable, 1f),
            new[] { "Cocar de penas", "Túnica de penas", "Calça de penas" }, new[] { 400, 420, 330 });

        public static readonly ArmorSetDefinition Desert = new ArmorSetDefinition("deserto", "Traje do deserto", 3,
            S(StatId.HeatResistance, 1f), S(StatId.ShockResistance, 1f),
            new[] { "Faixa do deserto", "Ombreira do deserto", "Calça do deserto" }, new[] { 400, 800, 600 });

        public static readonly ArmorSetDefinition Fire = new ArmorSetDefinition("fogo", "Traje ígneo", 3,
            S(StatId.FlameGuard, 1f), S(StatId.Fireproof, 1f),
            new[] { "Elmo ígneo", "Couraça ígnea", "Botas ígneas" }, new[] { 400, 360, 500 }, isMetal: true);

        public static readonly ArmorSetDefinition Swim = new ArmorSetDefinition("nado", "Traje das águas", 3,
            S(StatId.SwimSpeedPercent, 30f), S(StatId.SwimCostReduction, 50f),
            new[] { "Elmo das águas", "Couraça das águas", "Grevas das águas" }, new[] { 0, 0, 0 }, isMetal: true);

        public static readonly ArmorSetDefinition Climb = new ArmorSetDefinition("escalada", "Traje do escalador", 3,
            S(StatId.ClimbSpeedPercent, 20f), S(StatId.ClimbCostReduction, 30f),
            new[] { "Bandana do escalador", "Colete do escalador", "Botas do escalador" }, new[] { 0, 0, 0 });

        public static readonly ArmorSetDefinition Ancient = new ArmorSetDefinition("ancestral", "Traje ancestral", 4,
            S(StatId.SentinelDamageReduction, 20f), S(StatId.AncientProficiency, 1f),
            new[] { "Elmo ancestral", "Couraça ancestral", "Grevas ancestrais" }, new[] { 1000, 1200, 1000 }, isMetal: true);

        public static readonly IReadOnlyList<ArmorSetDefinition> All = new[]
            { OldClothes, Basic, Stealth, Cold, Desert, Fire, Swim, Climb, Ancient };

        public static ArmorSetDefinition Find(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return null;
        }
    }
}
