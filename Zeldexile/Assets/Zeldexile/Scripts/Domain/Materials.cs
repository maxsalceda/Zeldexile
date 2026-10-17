using System;
using System.Collections.Generic;

namespace Zeldexile.Domain
{
    public enum MaterialCategory
    {
        Fruit,
        Mushroom,
        Herb,
        Meat,
        Critter,
        MonsterPart,
        Ore,
        Wood,
        DragonPart
    }

    /// <summary>One effect per dish; two different effects in the same pot cancel each other.</summary>
    public enum FoodEffect
    {
        None,
        Hearty,
        Energizing,
        Enduring,
        Spicy,
        Chilly,
        Electro,
        Fireproof,
        Hasty,
        Sneaky,
        Mighty,
        Tough
    }

    /// <summary>
    /// A material carries three numbers: raw healing (quarter hearts), effect potency and a duration bonus.
    /// Every material has at least two uses (cooking + selling, elixir + armor upgrade).
    /// </summary>
    public sealed class MaterialDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly MaterialCategory Category;
        public readonly int HealPoints;
        public readonly FoodEffect Effect;
        public readonly int Potency;
        public readonly int DurationBonusSeconds;
        public readonly int SellPrice;

        public MaterialDefinition(string id, string name, MaterialCategory category, int healPoints, int sellPrice,
            FoodEffect effect = FoodEffect.None, int potency = 0, int durationBonusSeconds = 0)
        {
            Id = id;
            Name = name;
            Category = category;
            HealPoints = healPoints;
            SellPrice = sellPrice;
            Effect = effect;
            Potency = potency;
            DurationBonusSeconds = durationBonusSeconds;
        }

        public bool IsFood => Category == MaterialCategory.Fruit || Category == MaterialCategory.Mushroom ||
                              Category == MaterialCategory.Herb || Category == MaterialCategory.Meat;
        public bool IsCritter => Category == MaterialCategory.Critter;
        public bool IsMonsterPart => Category == MaterialCategory.MonsterPart;
        public bool Inedible => Category == MaterialCategory.Ore || Category == MaterialCategory.Wood;
    }

    public static class MaterialCatalog
    {
        public static readonly IReadOnlyList<MaterialDefinition> All = new[]
        {
            // Fruits and vegetables.
            new MaterialDefinition("maca", "Maçã silvestre", MaterialCategory.Fruit, 2, 3),
            new MaterialDefinition("laranja_vale", "Laranja do vale", MaterialCategory.Fruit, 2, 3),
            new MaterialDefinition("pera_dourada", "Pera dourada", MaterialCategory.Fruit, 2, 4),
            new MaterialDefinition("melancia", "Melancia gelada", MaterialCategory.Fruit, 2, 4, FoodEffect.Chilly, 1),
            new MaterialDefinition("pimenta", "Pimenta brasa", MaterialCategory.Fruit, 2, 4, FoodEffect.Spicy, 1),
            new MaterialDefinition("fruta_raio", "Fruta-raio", MaterialCategory.Fruit, 2, 4, FoodEffect.Electro, 1),
            new MaterialDefinition("banana", "Banana vigorosa", MaterialCategory.Fruit, 2, 5, FoodEffect.Mighty, 1),
            new MaterialDefinition("duriao", "Durião robusto", MaterialCategory.Fruit, 16, 15, FoodEffect.Hearty, 1),
            new MaterialDefinition("cenoura", "Cenoura resistente", MaterialCategory.Fruit, 4, 5, FoodEffect.Enduring, 1),
            new MaterialDefinition("abobora", "Abóbora fortificada", MaterialCategory.Fruit, 4, 5, FoodEffect.Tough, 1),
            // Mushrooms.
            new MaterialDefinition("cogumelo", "Cogumelo comum", MaterialCategory.Mushroom, 2, 3),
            new MaterialDefinition("cogumelo_folego", "Cogumelo-fôlego", MaterialCategory.Mushroom, 2, 4, FoodEffect.Energizing, 1),
            new MaterialDefinition("cogumelo_rapido", "Cogumelo-rápido", MaterialCategory.Mushroom, 2, 4, FoodEffect.Hasty, 1),
            new MaterialDefinition("cogumelo_silencio", "Cogumelo-silêncio", MaterialCategory.Mushroom, 2, 4, FoodEffect.Sneaky, 1),
            new MaterialDefinition("cogumelo_sol", "Cogumelo-sol", MaterialCategory.Mushroom, 2, 4, FoodEffect.Spicy, 1),
            new MaterialDefinition("cogumelo_ferro", "Cogumelo ferroso", MaterialCategory.Mushroom, 2, 4, FoodEffect.Tough, 1),
            // Herbs and flowers.
            new MaterialDefinition("erva", "Erva da serra", MaterialCategory.Herb, 2, 2),
            new MaterialDefinition("flor_gelada", "Flor gelada", MaterialCategory.Herb, 1, 6, FoodEffect.Chilly, 2),
            new MaterialDefinition("flor_quente", "Flor quente", MaterialCategory.Herb, 1, 6, FoodEffect.Spicy, 2),
            new MaterialDefinition("flor_silente", "Flor silente", MaterialCategory.Herb, 1, 8, FoodEffect.Sneaky, 2),
            // Meat and fish.
            new MaterialDefinition("carne", "Carne crua", MaterialCategory.Meat, 4, 10),
            new MaterialDefinition("carne_primeira", "Carne de primeira", MaterialCategory.Meat, 6, 20),
            new MaterialDefinition("carne_gourmet", "Carne gourmet", MaterialCategory.Meat, 12, 35),
            new MaterialDefinition("peixe", "Peixe do lago", MaterialCategory.Meat, 4, 8),
            new MaterialDefinition("peixe_robusto", "Salmão robusto", MaterialCategory.Meat, 8, 20, FoodEffect.Hearty, 1),
            // Critters (elixirs only).
            new MaterialDefinition("lagarto_brasa", "Lagarto-brasa", MaterialCategory.Critter, 0, 5, FoodEffect.Fireproof, 1),
            new MaterialDefinition("grilo", "Grilo saltador", MaterialCategory.Critter, 0, 4, FoodEffect.Energizing, 1),
            new MaterialDefinition("sapo", "Sapo incansável", MaterialCategory.Critter, 0, 6, FoodEffect.Enduring, 1),
            new MaterialDefinition("borboleta_gelada", "Borboleta gelada", MaterialCategory.Critter, 0, 4, FoodEffect.Chilly, 1),
            new MaterialDefinition("besouro", "Besouro vigoroso", MaterialCategory.Critter, 0, 6, FoodEffect.Mighty, 1),
            new MaterialDefinition("lagarto_veloz", "Lagarto veloz", MaterialCategory.Critter, 0, 4, FoodEffect.Hasty, 1),
            // Monster parts (elixirs, armor upgrades, trade).
            new MaterialDefinition("chifre_cascudo", "Chifre de Cascudo", MaterialCategory.MonsterPart, 0, 5, durationBonusSeconds: 30),
            new MaterialDefinition("presa_cascudo", "Presa de Cascudo", MaterialCategory.MonsterPart, 0, 15, durationBonusSeconds: 60),
            new MaterialDefinition("visceras_cascudo", "Vísceras de Cascudo", MaterialCategory.MonsterPart, 0, 40, durationBonusSeconds: 120),
            new MaterialDefinition("chifre_brutamontes", "Chifre de Brutamontes", MaterialCategory.MonsterPart, 0, 15, durationBonusSeconds: 60),
            new MaterialDefinition("cauda_lagartao", "Cauda de Lagartão", MaterialCategory.MonsterPart, 0, 15, durationBonusSeconds: 60),
            new MaterialDefinition("casco_elite", "Casco de Juba de Ferro", MaterialCategory.MonsterPart, 0, 150, durationBonusSeconds: 180),
            new MaterialDefinition("nucleo_ancestral", "Núcleo ancestral", MaterialCategory.MonsterPart, 0, 100, durationBonusSeconds: 120),
            new MaterialDefinition("parafuso_ancestral", "Parafuso ancestral", MaterialCategory.MonsterPart, 0, 30, durationBonusSeconds: 60),
            // Ores and wood (never food).
            new MaterialDefinition("pederneira", "Pederneira", MaterialCategory.Ore, 0, 5),
            new MaterialDefinition("ambar", "Âmbar", MaterialCategory.Ore, 0, 20),
            new MaterialDefinition("topazio", "Topázio", MaterialCategory.Ore, 0, 120),
            new MaterialDefinition("rubi", "Rubi", MaterialCategory.Ore, 0, 150),
            new MaterialDefinition("safira", "Safira", MaterialCategory.Ore, 0, 160),
            new MaterialDefinition("diamante", "Diamante", MaterialCategory.Ore, 0, 300),
            new MaterialDefinition("lenha", "Lenha", MaterialCategory.Wood, 0, 2),
            // Dragon parts: guaranteed long duration.
            new MaterialDefinition("escama_dragao", "Escama de dragão", MaterialCategory.DragonPart, 0, 150, durationBonusSeconds: 300),
        };

        private static readonly Dictionary<string, MaterialDefinition> ById = new Dictionary<string, MaterialDefinition>();

        static MaterialCatalog()
        {
            foreach (var m in All) ById[m.Id] = m;
        }

        public static MaterialDefinition Find(string id) => ById.TryGetValue(id, out var m) ? m : null;

        public static string CategoryName(MaterialCategory category)
        {
            switch (category)
            {
                case MaterialCategory.Fruit: return "Frutas e vegetais";
                case MaterialCategory.Mushroom: return "Cogumelos";
                case MaterialCategory.Herb: return "Ervas e flores";
                case MaterialCategory.Meat: return "Carnes e peixes";
                case MaterialCategory.Critter: return "Criaturinhas";
                case MaterialCategory.MonsterPart: return "Partes de monstro";
                case MaterialCategory.Ore: return "Minérios";
                case MaterialCategory.Wood: return "Madeira";
                default: return "Partes de dragão";
            }
        }
    }

    public enum DishKind
    {
        Food,
        Elixir,
        Dubious,
        RockHard
    }

    /// <summary>What comes out of the pot.</summary>
    public sealed class CookedDish
    {
        public string Name;
        public DishKind Kind;
        /// <summary>Quarter hearts restored; -1 restores everything.</summary>
        public int HealPoints;
        public int TemporaryHeartPoints;
        public float StaminaRestoreUnits;
        public float TemporaryStaminaUnits;
        public FoodEffect Effect;
        public int Level;
        public int DurationSeconds;
        public bool Critical;

        public string Describe()
        {
            var heal = HealPoints < 0 ? "cura total" : HealPoints > 0 ? $"{HealPoints / 4f:0.##} coração(ões)" : "sem cura";
            var text = heal;
            if (TemporaryHeartPoints > 0) text += $", +{TemporaryHeartPoints / 4f:0.##} amarelos";
            if (StaminaRestoreUnits > 0f) text += $", +{StaminaRestoreUnits / StaminaModel.WheelUnits:0.##} roda";
            if (TemporaryStaminaUnits > 0f) text += $", +{TemporaryStaminaUnits / StaminaModel.WheelUnits:0.##} roda amarela";
            if (Effect != FoodEffect.None && DurationSeconds > 0)
                text += $", {Cooking.EffectName(Effect)} {Level} por {DurationSeconds / 60}:{DurationSeconds % 60:00}";
            return text;
        }
    }

    /// <summary>
    /// The pot: up to 5 ingredients, healing doubled by cooking, one effect per dish, levels by summed
    /// potency, durations by effect type. Critter + monster part = elixir; food + monster part = dubious;
    /// ore or wood = rock-hard.
    /// </summary>
    public static class Cooking
    {
        public const int MaxIngredients = 5;
        public const int MaxDurationSeconds = 1800;
        public const int CriticalBonusSeconds = 300;
        public const int DubiousHeal = 4;
        public const int RockHardHeal = 1;
        public const float CriticalChance = 0.1f;

        public static CookedDish Cook(IReadOnlyList<MaterialDefinition> ingredients, bool critical = false)
        {
            if (ingredients == null || ingredients.Count == 0 || ingredients.Count > MaxIngredients) return null;

            var hasFood = false;
            var hasCritter = false;
            var hasMonsterPart = false;
            var hasInedible = false;
            foreach (var m in ingredients)
            {
                hasFood |= m.IsFood;
                hasCritter |= m.IsCritter;
                hasMonsterPart |= m.IsMonsterPart;
                hasInedible |= m.Inedible;
            }

            if (hasInedible)
                return new CookedDish { Name = "Comida dura como pedra", Kind = DishKind.RockHard, HealPoints = RockHardHeal };
            if ((hasFood && hasMonsterPart) || (hasFood && hasCritter) || (hasCritter && !hasMonsterPart) || (hasMonsterPart && !hasCritter))
                return new CookedDish { Name = "Comida duvidosa", Kind = DishKind.Dubious, HealPoints = DubiousHeal };

            var isElixir = hasCritter && hasMonsterPart;
            var dish = new CookedDish { Kind = isElixir ? DishKind.Elixir : DishKind.Food, Critical = critical };

            // One effect only: two different effects cancel each other.
            var effect = FoodEffect.None;
            var potency = 0;
            var effectIngredients = 0;
            var durationBonus = 0;
            var heal = 0;
            var hearty = 0;
            foreach (var m in ingredients)
            {
                heal += m.HealPoints;
                durationBonus += m.DurationBonusSeconds;
                if (m.Effect == FoodEffect.None) continue;
                if (effect == FoodEffect.None) effect = m.Effect;
                else if (effect != m.Effect)
                {
                    effect = FoodEffect.None;
                    potency = 0;
                    effectIngredients = 0;
                    break;
                }

                potency += m.Potency;
                effectIngredients++;
                if (m.Effect == FoodEffect.Hearty) hearty += m.Potency;
            }

            dish.Effect = effect;
            dish.HealPoints = isElixir ? 0 : heal * 2;
            if (critical && !isElixir) dish.HealPoints += 12;

            switch (effect)
            {
                case FoodEffect.None:
                    break;
                case FoodEffect.Hearty:
                    dish.HealPoints = -1;
                    dish.TemporaryHeartPoints = Math.Min(LifeModel.MaxTemporaryHearts, hearty) * LifeModel.PointsPerHeart;
                    break;
                case FoodEffect.Energizing:
                    dish.Level = LevelFor(potency, 3);
                    dish.StaminaRestoreUnits = StaminaModel.WheelUnits * (dish.Level == 1 ? 0.5f : dish.Level == 2 ? 1f : 2f);
                    break;
                case FoodEffect.Enduring:
                    dish.Level = LevelFor(potency, 3);
                    dish.TemporaryStaminaUnits = StaminaModel.WheelUnits * (dish.Level == 1 ? 0.5f : dish.Level == 2 ? 1f : 2f);
                    break;
                default:
                    dish.Level = LevelFor(potency, MaxLevel(effect));
                    if (critical && dish.Level < MaxLevel(effect)) dish.Level++;
                    var duration = BaseDurationSeconds(effect) + Math.Max(0, effectIngredients - 1) * 30 + durationBonus;
                    if (critical) duration += CriticalBonusSeconds;
                    dish.DurationSeconds = Math.Min(MaxDurationSeconds, duration);
                    break;
            }

            dish.Name = NameFor(dish, ingredients);
            return dish;
        }

        /// <summary>Potency 1 to 2 gives level 1, 3 to 4 level 2, 5 or more level 3 (five of a kind is the maximum).</summary>
        public static int LevelFor(int potency, int max)
        {
            var level = potency >= 5 ? 3 : potency >= 3 ? 2 : potency >= 1 ? 1 : 0;
            return Math.Min(max, level);
        }

        public static int MaxLevel(FoodEffect effect)
        {
            switch (effect)
            {
                case FoodEffect.Spicy:
                case FoodEffect.Chilly: return 2;
                case FoodEffect.Fireproof: return 1;
                default: return 3;
            }
        }

        public static int BaseDurationSeconds(FoodEffect effect)
        {
            switch (effect)
            {
                case FoodEffect.Spicy:
                case FoodEffect.Chilly:
                case FoodEffect.Electro:
                case FoodEffect.Fireproof: return 150;
                case FoodEffect.Sneaky: return 120;
                case FoodEffect.Hasty: return 60;
                case FoodEffect.Mighty:
                case FoodEffect.Tough: return 50;
                default: return 0;
            }
        }

        public static string EffectName(FoodEffect effect)
        {
            switch (effect)
            {
                case FoodEffect.Hearty: return "Robusto";
                case FoodEffect.Energizing: return "Energizante";
                case FoodEffect.Enduring: return "Resistente";
                case FoodEffect.Spicy: return "Picante";
                case FoodEffect.Chilly: return "Gelado";
                case FoodEffect.Electro: return "Isolante";
                case FoodEffect.Fireproof: return "Ignífugo";
                case FoodEffect.Hasty: return "Veloz";
                case FoodEffect.Sneaky: return "Furtivo";
                case FoodEffect.Mighty: return "Potente";
                case FoodEffect.Tough: return "Resistente";
                default: return "";
            }
        }

        private static string NameFor(CookedDish dish, IReadOnlyList<MaterialDefinition> ingredients)
        {
            var hasMeat = false;
            var hasMushroom = false;
            var hasFruit = false;
            foreach (var m in ingredients)
            {
                hasMeat |= m.Category == MaterialCategory.Meat;
                hasMushroom |= m.Category == MaterialCategory.Mushroom;
                hasFruit |= m.Category == MaterialCategory.Fruit;
            }

            var baseName = dish.Kind == DishKind.Elixir ? "Elixir"
                : hasMeat && hasMushroom ? "Espetinho de carne com cogumelo"
                : hasMeat ? "Carne grelhada"
                : hasMushroom ? "Cogumelos salteados"
                : hasFruit ? "Frutas cozidas"
                : "Ervas refogadas";
            var effect = dish.Effect == FoodEffect.None ? "" : " " + EffectName(dish.Effect).ToLowerInvariant();
            return dish.Kind == DishKind.Elixir ? baseName + effect : baseName + effect;
        }

        /// <summary>Blood moon window: 23:30 to 00:15 guarantees a critical cook.</summary>
        public static bool InBloodMoonWindow(float hour, bool bloodMoonNight) =>
            bloodMoonNight && (hour >= 23.5f || hour < 0.25f);
    }
}
