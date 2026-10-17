using UnityEngine;
using Zeldexile.Combat;
using Zeldexile.Domain;
using Zeldexile.Player;
using Zeldexile.World;

namespace Zeldexile.Gameplay
{
    /// <summary>
    /// What creatures leave behind: the weapon they carried (with a modifier once the world has hardened)
    /// and fixed parts by family and tier. Kills also feed the difficulty scale.
    /// </summary>
    public sealed class LootDropper : MonoBehaviour
    {
        public PlayerCharacter Player;

        private readonly System.Random _random = new System.Random();

        private void OnEnable() => EnemyBrain.Killed += OnKilled;
        private void OnDisable() => EnemyBrain.Killed -= OnKilled;

        private void OnKilled(EnemyBrain enemy)
        {
            if (Player == null) return;
            Player.World.RegisterKill(enemy.Family);
            var at = enemy.transform.position;

            if (enemy.Weapon != null && !enemy.Weapon.IsBroken)
            {
                if (Player.World.ModifiersUnlocked && enemy.Weapon.Modifier == WeaponModifier.None && _random.NextDouble() < 0.5)
                    WeaponCatalog.RollModifier(enemy.Weapon, _random, Player.World.YellowModifiersUnlocked);
                WeaponPickup.Spawn(enemy.Weapon, at + Vector3.right * 0.7f);
                enemy.Weapon = null;
            }

            foreach (var part in PartsFor(enemy.Family, enemy.Tier))
                MaterialPickup.Spawn(part, at + new Vector3((float)_random.NextDouble() - 0.5f, 0f, (float)_random.NextDouble() - 0.5f));
        }

        private System.Collections.Generic.IEnumerable<MaterialDefinition> PartsFor(EnemyFamily family, EnemyTier tier)
        {
            switch (family)
            {
                case EnemyFamily.Grunt:
                    yield return MaterialCatalog.Find("chifre_cascudo");
                    if (tier >= EnemyTier.Blue && _random.NextDouble() < 0.6) yield return MaterialCatalog.Find("presa_cascudo");
                    if (tier >= EnemyTier.Black && _random.NextDouble() < 0.4) yield return MaterialCatalog.Find("visceras_cascudo");
                    break;
                case EnemyFamily.Brute:
                    yield return MaterialCatalog.Find("chifre_brutamontes");
                    if (tier >= EnemyTier.Blue) yield return MaterialCatalog.Find("chifre_brutamontes");
                    break;
                case EnemyFamily.Agile:
                    yield return MaterialCatalog.Find("cauda_lagartao");
                    break;
                case EnemyFamily.Elite:
                    yield return MaterialCatalog.Find("casco_elite");
                    yield return MaterialCatalog.Find("casco_elite");
                    break;
                case EnemyFamily.Sentinel:
                case EnemyFamily.Scout:
                    yield return MaterialCatalog.Find("parafuso_ancestral");
                    if (_random.NextDouble() < 0.3) yield return MaterialCatalog.Find("nucleo_ancestral");
                    break;
                default:
                    yield return MaterialCatalog.Find("ambar");
                    break;
            }
        }
    }

    /// <summary>
    /// Reward at the end of a Trial: a Spirit Orb (the shrine reward), a weapon of the trial's tier and
    /// a material. Opened with the interact button.
    /// </summary>
    public sealed class TrialChest : Interactable
    {
        public PlayerCharacter Player;
        public string TrialName = "Prova";
        public int WeaponTier = 2;
        public WeaponClass WeaponClass = WeaponClass.OneHanded;
        public string MaterialId = "ambar";

        public bool Opened { get; private set; }

        public override string Prompt => Opened ? "" : $"Abrir baú ({TrialName})";

        public override void Interact(PlayerCharacter player)
        {
            if (Opened) return;
            Opened = true;
            player.AddLog($"{TrialName} concluída!");
            player.AddSpiritOrb();
            var weapon = new WeaponInstance(WeaponCatalog.ForTier(WeaponClass, WeaponTier));
            if (!player.GiveWeapon(weapon)) WeaponPickup.Spawn(weapon, transform.position + Vector3.forward * 1.2f);
            var material = MaterialCatalog.Find(MaterialId);
            if (material != null) player.AddMaterial(material, 2);
            transform.localScale = new Vector3(transform.localScale.x, transform.localScale.y * 0.4f, transform.localScale.z);
            enabled = false;
        }
    }
}
