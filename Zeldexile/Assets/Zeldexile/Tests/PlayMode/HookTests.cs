using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zeldexile.Combat;
using Zeldexile.Domain;
using Zeldexile.Gameplay;
using Zeldexile.Player;
using Zeldexile.World;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.Tests
{
    /// <summary>Boots the prototype island and checks the rules of the world: the same for runes, weapons and creatures.</summary>
    public sealed class HookTests
    {
        private GameObject _world;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = new GameObject("Zeldexile");
            _world.AddComponent<PrototypeWorld>();
            yield return null;
            // Let physics run so colliders created this frame are visible to queries.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Only the objects the prototype creates; the test runner keeps its own root objects.
            var ours = new[]
            {
                "Zeldexile", "Mundo", "Jogador", "Câmera", "HUD", "Efeitos", "Loot", "Sol", "Corrente de ar",
                "Plataforma de gelo", "Projétil", "Arco", "Atmosfera", "Game feel", "Grama densa", "Clima", "Raio"
            };
            foreach (var go in Object.FindObjectsByType<GameObject>())
                if (go != null && go.transform.parent == null &&
                    (ours.Contains(go.name) || go.name.StartsWith("Arma:") || go.name.StartsWith("Material:")))
                    Object.Destroy(go);
            yield return null;
        }

        [Test]
        public void WorldBootsWithHeartsRunesAndAStartingKit()
        {
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.Runes.Select(r => r.Id), Is.EquivalentTo(new[] { "brasa", "geada", "faisca", "lufada" }));
            Assert.That(player.Health.Max, Is.EqualTo(12f), "Three hearts.");
            Assert.That(player.Stamina.Max, Is.EqualTo(1000f), "One wheel.");
            Assert.That(player.Weapon, Is.Not.Null);
            Assert.That(player.Shield, Is.Not.Null);
            Assert.That(player.Bow, Is.Null, "Bows come from the world.");
            Assert.That(Object.FindObjectsByType<WeaponPickup>().Length, Is.GreaterThanOrEqualTo(4));
            Assert.That(Object.FindAnyObjectByType<CookingPot>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<GoddessStatue>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LightningJumpsThroughMetalAndOpensTheGate()
        {
            var gate = Object.FindAnyObjectByType<PowerGate>();
            var first = gate.Conductors[0].transform.position + Vector3.up;

            var single = SkillBuilder.Build(RuneCatalog.Faisca, null);
            single.Chains = 0;
            ImpactResolver.Resolve(Spark(first, single));
            yield return null;
            Assert.That(gate.Conductors.Count(c => c.IsPowered), Is.EqualTo(1), "Without jumps only one post is hit.");
            Assert.That(gate.IsOpen, Is.False);

            ImpactResolver.Resolve(Spark(first, SkillBuilder.Build(RuneCatalog.Faisca, null)));
            yield return null;
            Assert.That(gate.IsOpen, Is.True, "The rune jumps through all three posts on its own.");
        }

        [UnityTest]
        public IEnumerator FireOnGrassSpreadsAndMakesUpdraft()
        {
            var grass = Object.FindObjectsByType<ElementalSurface>().First(s => s.Kind == SurfaceKind.Grass);
            var p = grass.transform.position;
            ImpactResolver.Resolve(new ElementalImpact { Element = Element.Fire, Position = p, Radius = 0.5f, Damage = 1f });
            yield return null;

            Assert.That(grass.Kind, Is.EqualTo(SurfaceKind.BurningGrass));
            Assert.That(UpdraftZone.LiftAt(p + Vector3.up * 5f), Is.GreaterThan(0f));

            yield return new WaitForSeconds(1.2f);
            var burning = Object.FindObjectsByType<ElementalSurface>().Count(s => s.Kind == SurfaceKind.BurningGrass);
            Assert.That(burning, Is.GreaterThan(1), "Fire should spread to neighboring grass.");
        }

        [UnityTest]
        public IEnumerator OnlyThreeIcePillarsStandAtOnce()
        {
            var water = Object.FindAnyObjectByType<WaterVolume>();
            var stats = SkillBuilder.Build(RuneCatalog.Geada, null);
            for (var i = 0; i < 4; i++)
            {
                var point = new Vector3(24f + i * 4f, water.SurfaceY, 10f);
                ImpactResolver.Resolve(new ElementalImpact
                    { Element = Element.Ice, Position = point, Radius = stats.Radius, Damage = stats.Damage });
                yield return null;
            }

            yield return null;
            Assert.That(Object.FindObjectsByType<IcePlatform>().Length, Is.EqualTo(IcePlatform.MaxActive));
        }

        [Test]
        public void WeaponsWearOutAndTheNextOneIsDrawn()
        {
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            var stick = player.Weapon;
            var spare = new WeaponInstance(WeaponCatalog.ForTier(WeaponClass.OneHanded, 1));
            player.GiveWeapon(spare);
            Assert.That(player.Weapon, Is.SameAs(stick), "Picking up a spare does not swap the weapon in hand.");

            HitResult last = default;
            for (var i = 0; i < stick.MaxDurability; i++) last = stick.Strike();
            Assert.That(last.Broke, Is.True);
            player.OnWeaponBroke(stick);
            Assert.That(player.Weapon, Is.SameAs(spare));
            Assert.That(player.Gear.Items(GearKind.Weapon), Has.No.Member(stick));
        }

        [UnityTest]
        public IEnumerator CreaturesDropTheirWeaponAndReturnOnTheBloodMoon()
        {
            var enemy = EnemyBrain.Active.First(e => e.Family == EnemyFamily.Grunt);
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            var pickupsBefore = Object.FindObjectsByType<WeaponPickup>().Length;
            var pointsBefore = player.World.Points;

            enemy.Health.TakeDamage(99999f, Element.None, fixedDamage: true);
            yield return null;
            Assert.That(enemy.Health.IsDead, Is.True);
            Assert.That(Object.FindObjectsByType<WeaponPickup>().Length, Is.EqualTo(pickupsBefore + 1), "The carried weapon falls to the ground.");
            Assert.That(player.World.Points, Is.GreaterThan(pointsBefore), "Kills harden the world.");

            // Just before the blood-moon midnight (day 5 starts); the clock crosses it in a few frames.
            AtmosphereController.Current.Clock.Set(4, 23.999f);
            var waited = 0f;
            while (enemy.Health.IsDead && waited < 3f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.That(enemy.Health.IsDead, Is.False, "The blood moon brings creatures back.");
        }

        [Test]
        public void DefenseAndFoodFollowTheSameRulesAsCreatures()
        {
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(player.Defense, Is.EqualTo(2), "Old shirt and trousers: 1 + 1.");
            var before = player.Health.Current;
            player.Health.TakeDamage(10f, Element.None);
            Assert.That(before - player.Health.Current, Is.EqualTo(8f), "10 attack minus 2 defense.");

            var dish = player.Cook(new[] { MaterialCatalog.Find("maca"), MaterialCatalog.Find("maca") }, critical: false);
            Assert.That(dish.HealPoints, Is.EqualTo(8));
            player.Eat(dish);
            Assert.That(player.Health.Current, Is.EqualTo(before));
        }

        private static ElementalImpact Spark(Vector3 at, SkillStats s) => new ElementalImpact
        {
            Element = Element.Lightning, Position = at, Radius = s.Radius, Damage = s.Damage,
            Chains = s.Chains, ChainRange = s.ChainRange
        };
    }
}
