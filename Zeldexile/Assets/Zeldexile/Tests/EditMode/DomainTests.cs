using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Zeldexile.Domain;

namespace Zeldexile.Tests
{
    public sealed class ReactionTests
    {
        [Test]
        public void FireIgnitesGrass()
        {
            var r = ElementalReactions.React(SurfaceKind.Grass, Element.Fire);
            Assert.That(r.Ignites, Is.True);
            Assert.That(r.NewSurface, Is.EqualTo(SurfaceKind.BurningGrass));
        }

        [Test]
        public void IceOnWaterMakesPlatformAndLightningElectrifies()
        {
            Assert.That(ElementalReactions.React(SurfaceKind.Water, Element.Ice).CreatesIcePlatform, Is.True);
            Assert.That(ElementalReactions.React(SurfaceKind.Water, Element.Lightning).Electrifies, Is.True);
        }

        [Test]
        public void LightningConductsOnlyThroughMetal()
        {
            Assert.That(ElementalReactions.React(SurfaceKind.Metal, Element.Lightning).Conducts, Is.True);
            Assert.That(ElementalReactions.React(SurfaceKind.Stone, Element.Lightning).Conducts, Is.False);
        }

        [Test]
        public void IcePutsOutFireAndFireMeltsIce()
        {
            Assert.That(ElementalReactions.React(SurfaceKind.BurningGrass, Element.Ice).NewSurface, Is.EqualTo(SurfaceKind.Grass));
            Assert.That(ElementalReactions.React(SurfaceKind.Ice, Element.Fire).NewSurface, Is.EqualTo(SurfaceKind.Water));
        }

        [Test]
        public void RunesAreFixedToolsWithChainingLightning()
        {
            Assert.That(RuneCatalog.Actives.Count, Is.EqualTo(4));
            var spark = SkillBuilder.Build(RuneCatalog.Faisca, null);
            Assert.That(spark.Chains, Is.EqualTo(3), "Lightning jumps through nearby metal on its own.");
            Assert.That(spark.SneakMultiplier, Is.EqualTo(DamageRules.SneakMultiplier));
        }
    }

    public sealed class HeartTests
    {
        [Test]
        public void StartsWithThreeHeartsAndGrowsToTwenty()
        {
            var life = new LifeModel();
            Assert.That(life.Max, Is.EqualTo(12));
            for (var i = 0; i < 40; i++) life.AddContainer();
            Assert.That(life.Containers, Is.EqualTo(LifeModel.MaxHearts));
            Assert.That(life.Max, Is.EqualTo(80));
        }

        [Test]
        public void TemporaryHeartsAreLostFirst()
        {
            var life = new LifeModel();
            life.AddTemporary(8);
            life.Damage(10);
            Assert.That(life.Temporary, Is.EqualTo(0));
            Assert.That(life.Current, Is.EqualTo(10));
            life.Heal(100);
            Assert.That(life.Current, Is.EqualTo(12));
        }
    }

    public sealed class StaminaTests
    {
        [Test]
        public void OneWheelIsAThousandUnitsAndVesselsAddAFifth()
        {
            var s = new StaminaModel();
            Assert.That(s.Max, Is.EqualTo(1000f));
            for (var i = 0; i < 20; i++) s.AddVessel();
            Assert.That(s.Vessels, Is.EqualTo(StaminaModel.MaxVessels));
            Assert.That(s.Wheels, Is.EqualTo(3f));
        }

        [Test]
        public void EmptyingStaminaExhaustsUntilFull()
        {
            var s = new StaminaModel { RegenDelay = 0f, RegenPerSecond = 300f };
            Assert.That(s.Drain(1500f), Is.False);
            Assert.That(s.Exhausted, Is.True);
            Assert.That(s.CanSpend, Is.False);

            s.Tick(1f);
            Assert.That(s.Exhausted, Is.True, "Still exhausted while refilling.");
            s.Tick(10f);
            Assert.That(s.Exhausted, Is.False);
            Assert.That(s.Current, Is.EqualTo(1000f));
        }

        [Test]
        public void RegenWaitsForDelayAndRefillsAWheelInAboutThreeSeconds()
        {
            var s = new StaminaModel { RegenDelay = 0.5f };
            s.Drain(900f);
            s.Tick(0.25f);
            Assert.That(s.Current, Is.EqualTo(100f));
            s.Tick(0.25f);
            s.Tick(3f);
            Assert.That(s.Current, Is.EqualTo(1000f));
        }

        [Test]
        public void TemporaryStaminaIsSpentFirstAndNeverRegenerates()
        {
            var s = new StaminaModel { RegenDelay = 0f };
            s.AddTemporary(500f);
            s.Drain(300f);
            Assert.That(s.Temporary, Is.EqualTo(200f));
            Assert.That(s.Current, Is.EqualTo(1000f));
            s.Tick(5f);
            Assert.That(s.Temporary, Is.EqualTo(200f));
        }
    }

    public sealed class WeaponTests
    {
        [Test]
        public void CurveGrowsByTierAndClassMultipliersApply()
        {
            var sword = WeaponCatalog.ForTier(WeaponClass.OneHanded, 2);
            var claymore = WeaponCatalog.ForTier(WeaponClass.TwoHanded, 2);
            var spear = WeaponCatalog.ForTier(WeaponClass.Spear, 2);
            Assert.That(sword.Attack, Is.EqualTo(18));
            Assert.That(claymore.Attack, Is.EqualTo(27));
            Assert.That(spear.Attack, Is.EqualTo(13));
            for (var tier = 1; tier <= 5; tier++)
                Assert.That(WeaponCatalog.ForTier(WeaponClass.OneHanded, tier).Attack,
                    Is.GreaterThan(WeaponCatalog.ForTier(WeaponClass.OneHanded, tier - 1).Attack));
        }

        [Test]
        public void EveryHitWearsTheWeaponAndTheLastHitDealsDouble()
        {
            var weapon = new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, 0)); // 4 attack, 8 durability
            HitResult last = default;
            for (var i = 0; i < 8; i++) last = weapon.Strike();
            Assert.That(weapon.IsBroken, Is.True);
            Assert.That(last.Broke, Is.True);
            Assert.That(last.Damage, Is.EqualTo(8));
            Assert.That(weapon.Strike().Damage, Is.EqualTo(0), "A broken weapon does nothing.");
        }

        [Test]
        public void ThrowingBreaksAndDamagedThrowsAreCritical()
        {
            var fresh = new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, 1));
            Assert.That(fresh.Throw().Damage, Is.EqualTo(10));
            Assert.That(fresh.IsBroken, Is.True);

            var worn = new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, 1));
            worn.Wear(worn.MaxDurability - 2);
            Assert.That(worn.BadlyDamaged, Is.True);
            Assert.That(worn.Throw().Damage, Is.EqualTo(20));
        }

        [Test]
        public void AnchorWeaponsRestInsteadOfBreaking()
        {
            var sword = new WeaponInstance(WeaponCatalog.LegendarySword);
            for (var i = 0; i < 40; i++) sword.Strike();
            Assert.That(sword.IsBroken, Is.False);
            Assert.That(sword.IsResting, Is.True);
            sword.TickRecharge(WeaponInstance.AnchorRechargeSeconds);
            Assert.That(sword.IsUsable, Is.True);
            Assert.That(sword.Durability, Is.EqualTo(40f));
        }

        [Test]
        public void ShieldBlocksUpToItsGuardAndWears()
        {
            var shield = new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.Shield, 1)); // guard 20, durability 20
            Assert.That(shield.Block(12, out _), Is.EqualTo(0));
            Assert.That(shield.Durability, Is.EqualTo(19f));
            Assert.That(shield.Block(30, out _), Is.EqualTo(10));
            Assert.That(shield.Durability, Is.EqualTo(17f));
        }
    }

    public sealed class DamageRuleTests
    {
        [Test]
        public void DefenseSubtractsButNeverBelowAQuarterHeart()
        {
            Assert.That(DamageRules.Taken(36, 9), Is.EqualTo(27));
            Assert.That(DamageRules.Taken(20, 60), Is.EqualTo(1));
        }

        [Test]
        public void FallDamageCurve()
        {
            Assert.That(DamageRules.FallDamage(5f), Is.EqualTo(0));
            Assert.That(DamageRules.FallDamage(10f), Is.EqualTo(4));
            Assert.That(DamageRules.FallDamage(18f), Is.EqualTo(12));
            Assert.That(DamageRules.FallDamage(31f), Is.EqualTo(int.MaxValue));
        }
    }

    public sealed class InventoryTests
    {
        [Test]
        public void PouchesStartSmallAndFillUp()
        {
            var gear = new GearInventory();
            Assert.That(gear.Slots(GearKind.Weapon), Is.EqualTo(6));
            for (var i = 0; i < 6; i++) Assert.That(gear.TryAdd(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, 1))), Is.True);
            Assert.That(gear.TryAdd(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, 1))), Is.False);
            Assert.That(gear.TryAdd(new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.Bow, 1))), Is.True, "Bows have their own pouch.");
        }

        [Test]
        public void SeedCostsAddUpToOneHundredSeventy()
        {
            Assert.That(GearInventory.SumOfAllCosts(), Is.EqualTo(GearInventory.TotalSeedsForMax));
            var gear = new GearInventory();
            var seeds = 1000;
            while (gear.Upgrade(GearKind.Weapon, ref seeds)) { }
            Assert.That(gear.Slots(GearKind.Weapon), Is.EqualTo(16));
            Assert.That(seeds, Is.EqualTo(1000 - 79));
        }
    }

    public sealed class EnemyTests
    {
        [Test]
        public void HealthTriplesPerTierAndKillsHardenTheWorld()
        {
            Assert.That(EnemyStats.HealthOf(EnemyFamily.Grunt, EnemyTier.Red), Is.EqualTo(12));
            Assert.That(EnemyStats.HealthOf(EnemyFamily.Grunt, EnemyTier.Silver), Is.EqualTo(400));
            Assert.That(EnemyStats.HealthOf(EnemyFamily.Elite, EnemyTier.Red), Is.EqualTo(1500));

            var world = new WorldLevel();
            for (var i = 0; i < 50; i++) world.RegisterKill(EnemyFamily.Grunt);
            Assert.That(world.Points, Is.EqualTo(10), "Only the first ten kills of a family count.");
            for (var i = 0; i < 10; i++) world.RegisterKill(EnemyFamily.Elite);
            Assert.That(world.Points, Is.EqualTo(110));
            Assert.That(WorldLevel.TierFor(world.Points), Is.EqualTo(EnemyTier.Blue));
            Assert.That(world.Promote(EnemyTier.Black), Is.EqualTo(EnemyTier.Black), "Never demotes.");
        }

        [Test]
        public void HitsToKillStayInTheIntendedRange()
        {
            // Phase-matched weapon vs the grunt of the same phase: 2 to 12 hits.
            var tiers = new[] { (1, EnemyTier.Red), (2, EnemyTier.Blue), (3, EnemyTier.Black), (4, EnemyTier.Silver) };
            foreach (var (weaponTier, enemyTier) in tiers)
            {
                var attack = WeaponCatalog.ForTier(WeaponClass.OneHanded, weaponTier).Attack;
                var hits = (int)System.Math.Ceiling(EnemyStats.HealthOf(EnemyFamily.Grunt, enemyTier) / (float)attack);
                Assert.That(hits, Is.InRange(2, 12), $"tier {weaponTier} vs {enemyTier}: {hits} hits");
            }
        }
    }

    public sealed class CookingTests
    {
        private static MaterialDefinition M(string id) => MaterialCatalog.Find(id);

        [Test]
        public void CookingDoublesHealingAndFiveIsTheLimit()
        {
            var dish = Cooking.Cook(new[] { M("maca"), M("maca") });
            Assert.That(dish.HealPoints, Is.EqualTo(8));
            Assert.That(Cooking.Cook(new[] { M("maca"), M("maca"), M("maca"), M("maca"), M("maca"), M("maca") }), Is.Null);
        }

        [Test]
        public void TwoDifferentEffectsCancelEachOther()
        {
            var one = Cooking.Cook(new[] { M("pimenta") });
            Assert.That(one.Effect, Is.EqualTo(FoodEffect.Spicy));
            Assert.That(one.Level, Is.EqualTo(1));
            var mixed = Cooking.Cook(new[] { M("pimenta"), M("melancia") });
            Assert.That(mixed.Effect, Is.EqualTo(FoodEffect.None));
        }

        [Test]
        public void PotencyRaisesTheLevelWithinTheEffectsCap()
        {
            var three = Cooking.Cook(new[] { M("pimenta"), M("pimenta"), M("pimenta") });
            Assert.That(three.Level, Is.EqualTo(2));
            var five = Cooking.Cook(new[] { M("pimenta"), M("pimenta"), M("pimenta"), M("pimenta"), M("pimenta") });
            Assert.That(five.Level, Is.EqualTo(2), "Thermal resistances cap at level 2.");
            var mighty = Cooking.Cook(new[] { M("banana"), M("banana"), M("banana"), M("banana"), M("banana"), });
            Assert.That(mighty.Level, Is.EqualTo(3));
            Assert.That(mighty.DurationSeconds, Is.EqualTo(50 + 4 * 30));
        }

        [Test]
        public void ElixirsNeedACritterAndAMonsterPart()
        {
            Assert.That(Cooking.Cook(new[] { M("grilo"), M("chifre_cascudo") }).Kind, Is.EqualTo(DishKind.Elixir));
            Assert.That(Cooking.Cook(new[] { M("grilo") }).Kind, Is.EqualTo(DishKind.Dubious));
            Assert.That(Cooking.Cook(new[] { M("maca"), M("chifre_cascudo") }).Kind, Is.EqualTo(DishKind.Dubious));
            Assert.That(Cooking.Cook(new[] { M("maca"), M("pederneira") }).Kind, Is.EqualTo(DishKind.RockHard));
        }

        [Test]
        public void HeartyHealsFullyAndAddsYellowHearts()
        {
            var dish = Cooking.Cook(new[] { M("duriao"), M("duriao") });
            Assert.That(dish.HealPoints, Is.EqualTo(-1));
            Assert.That(dish.TemporaryHeartPoints, Is.EqualTo(8));
        }

        [Test]
        public void CriticalAddsTimeAndDurationsCapAtThirtyMinutes()
        {
            var dish = Cooking.Cook(new[] { M("pimenta"), M("escama_dragao"), M("escama_dragao"), M("escama_dragao"), M("escama_dragao") }, critical: true);
            Assert.That(dish.DurationSeconds, Is.EqualTo(150 + 4 * 300 + Cooking.CriticalBonusSeconds));
            Assert.That(dish.DurationSeconds, Is.LessThanOrEqualTo(Cooking.MaxDurationSeconds));
            Assert.That(Cooking.InBloodMoonWindow(23.75f, true), Is.True);
            Assert.That(Cooking.InBloodMoonWindow(23.75f, false), Is.False);
        }
    }

    public sealed class ArmorTests
    {
        [Test]
        public void DefenseCurveAndSetBonusAtLevelTwo()
        {
            Assert.That(Armor.DefenseFor(3, 0), Is.EqualTo(3));
            Assert.That(Armor.DefenseFor(3, 4), Is.EqualTo(20));
            var set = ArmorCatalog.Cold;
            var head = new ArmorPiece(set, ArmorSlot.Head, 2);
            var body = new ArmorPiece(set, ArmorSlot.Body, 2);
            var legs = new ArmorPiece(set, ArmorSlot.Legs, 1);
            Assert.That(Armor.SetBonusActive(head, body, legs), Is.False);
            legs.Level = 2;
            Assert.That(Armor.SetBonusActive(head, body, legs), Is.True);

            var stats = new StatBlock();
            Armor.AddStats(stats, head, body, legs);
            Assert.That(stats.Level(StatId.ColdResistance), Is.EqualTo(2), "Three cold pieces cap at level 2.");
            Assert.That(stats.Get(StatId.Unfreezable), Is.EqualTo(1f));
            Assert.That(Armor.TotalDefense(head, body, legs), Is.EqualTo(24));
        }
    }
}
