using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Combat;
using Zeldexile.Domain;

namespace Zeldexile.Player
{
    /// <summary>
    /// Everything the character carries and is: hearts, stamina wheels, the weapon/bow/shield pouches,
    /// worn armor, materials, cooked food, Spirit Orbs, Korok seeds and the four runes.
    /// Progression is horizontal: more options, never a level.
    /// </summary>
    public sealed class PlayerCharacter : MonoBehaviour
    {
        public Health Health;

        public LifeModel Life { get; } = new LifeModel();
        public StaminaModel Stamina { get; } = new StaminaModel();
        public StatBlock Stats { get; } = new StatBlock();
        public GearInventory Gear { get; } = new GearInventory();
        public WorldLevel World { get; } = new WorldLevel();

        public WeaponInstance Weapon { get; private set; }
        public WeaponInstance Bow { get; private set; }
        public WeaponInstance Shield { get; private set; }
        public ArmorPiece Head { get; private set; }
        public ArmorPiece Body { get; private set; }
        public ArmorPiece Legs { get; private set; }
        public List<ArmorPiece> Wardrobe { get; } = new List<ArmorPiece>();
        public Dictionary<MaterialDefinition, int> Materials { get; } = new Dictionary<MaterialDefinition, int>();
        public List<CookedDish> Food { get; } = new List<CookedDish>();

        public int Rupees { get; private set; }
        public int KorokSeeds { get; private set; }
        public int SpiritOrbs { get; private set; }
        public int Arrows { get; private set; }

        public FoodEffect ActiveEffect { get; private set; }
        public int ActiveEffectLevel { get; private set; }
        public float EffectRemaining => Mathf.Max(0f, _effectUntil - Time.time);

        public IReadOnlyList<RuneDefinition> Runes => RuneCatalog.Actives;
        public int Defense { get; private set; }

        /// <summary>Short log of recent events for the HUD.</summary>
        public List<string> Log { get; } = new List<string>();

        public event Action Changed;
        /// <summary>Raised with a message worth a banner (weapon broke, blood moon).</summary>
        public event Action<string> Announced;

        private float _counterUntil;
        private float _effectUntil;

        /// <summary>Short window after a perfect dodge (unscaled time, so slow motion does not stretch it).</summary>
        public bool CounterActive => Time.unscaledTime < _counterUntil;

        public void StartCounter(float seconds) => _counterUntil = Time.unscaledTime + seconds;

        /// <summary>Metal in hand or on the body attracts lightning.</summary>
        public bool CarriesMetal =>
            (Weapon != null && Weapon.Definition.IsMetal) || (Shield != null && Shield.Definition.IsMetal) ||
            (Head != null && Head.Set.IsMetal) || (Body != null && Body.Set.IsMetal) || (Legs != null && Legs.Set.IsMetal);

        private void Awake()
        {
            if (Health == null) Health = GetComponent<Health>();

            // Starting kit: a stick, a pot lid, old clothes and a few things to cook.
            GiveWeapon(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, 0)), equip: true, silent: true);
            GiveWeapon(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.Shield, 0)), equip: true, silent: true);
            Body = new ArmorPiece(ArmorCatalog.OldClothes, ArmorSlot.Body);
            Legs = new ArmorPiece(ArmorCatalog.OldClothes, ArmorSlot.Legs);
            AddMaterial(MaterialCatalog.Find("maca"), 3, silent: true);
            AddMaterial(MaterialCatalog.Find("cogumelo"), 2, silent: true);
            AddMaterial(MaterialCatalog.Find("lenha"), 1, silent: true);
            AddMaterial(MaterialCatalog.Find("pederneira"), 1, silent: true);
            Arrows = 10;

            Recalculate(refill: true);
        }

        private void Update()
        {
            if (ActiveEffect != FoodEffect.None && Time.time >= _effectUntil)
            {
                ActiveEffect = FoodEffect.None;
                ActiveEffectLevel = 0;
                AddLog("O efeito da comida passou.");
                Recalculate();
            }

            foreach (var kind in new[] { GearKind.Weapon, GearKind.Bow, GearKind.Shield })
                foreach (var w in Gear.Items(kind))
                    w.TickRecharge(Time.deltaTime);
        }

        public void Recalculate(bool refill = false)
        {
            Stats.Clear();
            Armor.AddStats(Stats, Head, Body, Legs);
            AddFoodStats();

            Defense = Armor.TotalDefense(Head, Body, Legs) + (int)Stats.Get(StatId.DefenseBonus);
            if (Health != null)
            {
                Health.SetMax(Life.Max, refill);
                Health.Defense = Defense;
                Health.Fireproof = Stats.Get(StatId.Fireproof) > 0f;
                Health.Unfreezable = Stats.Get(StatId.Unfreezable) > 0f;
            }

            if (refill) Stamina.Refill();
            Changed?.Invoke();
        }

        private void AddFoodStats()
        {
            var level = ActiveEffectLevel;
            switch (ActiveEffect)
            {
                case FoodEffect.Spicy: Stats.Add(StatId.ColdResistance, level); break;
                case FoodEffect.Chilly: Stats.Add(StatId.HeatResistance, level); break;
                case FoodEffect.Electro: Stats.Add(StatId.ShockResistance, level); break;
                case FoodEffect.Fireproof: Stats.Add(StatId.Fireproof, 1f); break;
                case FoodEffect.Hasty: Stats.Add(StatId.MoveSpeedPercent, DamageRules.HastyPercent(level)); break;
                case FoodEffect.Sneaky: Stats.Add(StatId.NoiseReduction, DamageRules.SneakyNoiseReduction(level)); break;
                case FoodEffect.Mighty: Stats.Add(StatId.AttackPercent, DamageRules.MightyPercent(level)); break;
                case FoodEffect.Tough: Stats.Add(StatId.DefenseBonus, DamageRules.ToughBonus(level)); break;
            }
        }

        // ---------- Gear ----------

        /// <summary>Puts a weapon in its pouch. Returns false when the pouch is full.</summary>
        public bool GiveWeapon(WeaponInstance weapon, bool equip = false, bool silent = false)
        {
            if (!Gear.TryAdd(weapon))
            {
                if (!silent) AddLog($"Bolsa de {GearInventory.KindName(weapon.Kind).ToLowerInvariant()} cheia.");
                return false;
            }

            if (!silent) AddLog($"Pegou: {weapon.Name}" + (weapon.Modifier != WeaponModifier.None ? $" ({weapon.ModifierText})" : ""));
            if (equip || SlotFor(weapon.Kind) == null) Equip(weapon);
            else Changed?.Invoke();
            return true;
        }

        public WeaponInstance SlotFor(GearKind kind) => kind == GearKind.Bow ? Bow : kind == GearKind.Shield ? Shield : Weapon;

        public void Equip(WeaponInstance weapon)
        {
            if (weapon == null || !Gear.Contains(weapon)) return;
            switch (weapon.Kind)
            {
                case GearKind.Bow: Bow = weapon; break;
                case GearKind.Shield: Shield = weapon; break;
                default: Weapon = weapon; break;
            }

            Changed?.Invoke();
        }

        public void Unequip(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Bow: Bow = null; break;
                case GearKind.Shield: Shield = null; break;
                default: Weapon = null; break;
            }

            Changed?.Invoke();
        }

        /// <summary>Removes a weapon from the pouch (broken, thrown or dropped) and hands the next one if it was in use.</summary>
        public void RemoveWeapon(WeaponInstance weapon, string reason = null)
        {
            if (!Gear.Remove(weapon)) return;
            if (reason != null) AddLog(reason);
            if (Weapon == weapon) Weapon = NextUsable(GearKind.Weapon);
            if (Bow == weapon) Bow = NextUsable(GearKind.Bow);
            if (Shield == weapon) Shield = NextUsable(GearKind.Shield);
            Changed?.Invoke();
        }

        private WeaponInstance NextUsable(GearKind kind)
        {
            foreach (var w in Gear.Items(kind)) if (w.IsUsable) return w;
            return null;
        }

        public void OnWeaponBroke(WeaponInstance weapon)
        {
            Announced?.Invoke($"{weapon.Name} quebrou!");
            RemoveWeapon(weapon, $"{weapon.Name} quebrou.");
        }

        public bool UpgradePouch(GearKind kind)
        {
            var seeds = KorokSeeds;
            if (!Gear.Upgrade(kind, ref seeds)) return false;
            KorokSeeds = seeds;
            AddLog($"Bolsa de {GearInventory.KindName(kind).ToLowerInvariant()}: +1 espaço.");
            Changed?.Invoke();
            return true;
        }

        public void AddArrows(int count)
        {
            Arrows += count;
            AddLog($"+{count} flecha(s)");
            Changed?.Invoke();
        }

        public bool SpendArrow()
        {
            if (Arrows <= 0) return false;
            Arrows--;
            return true;
        }

        // ---------- Armor ----------

        public void GiveArmor(ArmorPiece piece, bool wear = false)
        {
            Wardrobe.Add(piece);
            AddLog($"Pegou: {piece.Name}");
            if (wear) Wear(piece);
            else Changed?.Invoke();
        }

        public ArmorPiece Worn(ArmorSlot slot) => slot == ArmorSlot.Head ? Head : slot == ArmorSlot.Body ? Body : Legs;

        public void Wear(ArmorPiece piece)
        {
            if (piece == null) return;
            var current = Worn(piece.Slot);
            if (current != null && !Wardrobe.Contains(current)) Wardrobe.Add(current);
            Wardrobe.Remove(piece);
            switch (piece.Slot)
            {
                case ArmorSlot.Head: Head = piece; break;
                case ArmorSlot.Body: Body = piece; break;
                default: Legs = piece; break;
            }

            Recalculate();
        }

        public void TakeOff(ArmorSlot slot)
        {
            var current = Worn(slot);
            if (current == null) return;
            Wardrobe.Add(current);
            switch (slot)
            {
                case ArmorSlot.Head: Head = null; break;
                case ArmorSlot.Body: Body = null; break;
                default: Legs = null; break;
            }

            Recalculate();
        }

        // ---------- Materials, cooking and eating ----------

        public int Count(MaterialDefinition material) => Materials.TryGetValue(material, out var c) ? c : 0;

        public void AddMaterial(MaterialDefinition material, int amount, bool silent = false)
        {
            if (material == null || amount <= 0) return;
            Materials[material] = Count(material) + amount;
            if (!silent) AddLog($"+{amount} {material.Name}");
            Changed?.Invoke();
        }

        public bool TakeMaterial(MaterialDefinition material, int amount)
        {
            if (Count(material) < amount) return false;
            Materials[material] -= amount;
            if (Materials[material] <= 0) Materials.Remove(material);
            return true;
        }

        /// <summary>Cooks the chosen materials (consumed) and stores the dish.</summary>
        public CookedDish Cook(IReadOnlyList<MaterialDefinition> ingredients, bool critical)
        {
            var dish = Cooking.Cook(ingredients, critical);
            if (dish == null) return null;
            foreach (var m in ingredients)
                if (!TakeMaterial(m, 1))
                    return null;
            Food.Add(dish);
            AddLog($"Cozinhou: {dish.Name}" + (critical ? " (sucesso crítico!)" : ""));
            Changed?.Invoke();
            return dish;
        }

        public void Eat(CookedDish dish)
        {
            if (dish == null || !Food.Remove(dish)) return;
            if (dish.HealPoints < 0) Health.HealFully();
            else Health.Heal(dish.HealPoints);
            if (dish.TemporaryHeartPoints > 0) Health.AddTemporary(dish.TemporaryHeartPoints);
            if (dish.StaminaRestoreUnits > 0f) Stamina.Restore(dish.StaminaRestoreUnits);
            if (dish.TemporaryStaminaUnits > 0f) Stamina.AddTemporary(dish.TemporaryStaminaUnits);
            if (dish.DurationSeconds > 0 && dish.Effect != FoodEffect.None)
            {
                ActiveEffect = dish.Effect;
                ActiveEffectLevel = dish.Level;
                _effectUntil = Time.time + dish.DurationSeconds;
            }

            AddLog($"Comeu: {dish.Name}");
            Recalculate();
        }

        // ---------- Progression ----------

        public void AddSpiritOrb()
        {
            SpiritOrbs++;
            AddLog("Recebeu um Orbe Espiritual.");
            Changed?.Invoke();
        }

        public bool CanPray => SpiritOrbs >= LifeModel.OrbsPerUpgrade;

        /// <summary>Goddess statue: 4 orbs for a Heart Container or a Stamina Vessel.</summary>
        public bool Pray(bool heart)
        {
            if (!CanPray) return false;
            var ok = heart ? Life.AddContainer() : Stamina.AddVessel();
            if (!ok) return false;
            SpiritOrbs -= LifeModel.OrbsPerUpgrade;
            AddLog(heart ? "Um coração a mais." : "A roda de stamina cresceu.");
            Recalculate(refill: true);
            return true;
        }

        public void AddKorokSeeds(int amount)
        {
            KorokSeeds += amount;
            AddLog($"+{amount} semente(s) Korok");
            Changed?.Invoke();
        }

        public void AddRupees(int amount)
        {
            Rupees = Mathf.Max(0, Rupees + amount);
            AddLog(amount >= 0 ? $"+{amount} rupees" : $"{amount} rupees");
            Changed?.Invoke();
        }

        public void AddLog(string line)
        {
            Log.Add(line);
            if (Log.Count > 6) Log.RemoveAt(0);
        }

        public void Announce(string message) => Announced?.Invoke(message);
    }
}
