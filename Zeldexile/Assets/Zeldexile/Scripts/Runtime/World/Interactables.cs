using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain;
using Zeldexile.Player;

namespace Zeldexile.World
{
    /// <summary>Something the player can use with the interact button when close enough.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> Active = new List<Interactable>();

        public float Range = 2.6f;

        public abstract string Prompt { get; }
        public abstract void Interact(PlayerCharacter player);

        protected virtual void OnEnable() => Active.Add(this);
        protected virtual void OnDisable() => Active.Remove(this);

        public static Interactable Nearest(Vector3 position)
        {
            Interactable best = null;
            var bestDistance = float.MaxValue;
            foreach (var i in Active)
            {
                if (i == null || !i.isActiveAndEnabled) continue;
                var d = Vector3.Distance(position, i.transform.position);
                if (d > i.Range || d >= bestDistance) continue;
                bestDistance = d;
                best = i;
            }

            return best;
        }
    }

    /// <summary>A weapon, bow or shield lying in the world. Blood moons bring back the ones that were taken.</summary>
    public sealed class WeaponPickup : Interactable
    {
        public WeaponInstance Weapon;
        /// <summary>Set for weapons placed by the world (not drops), so a blood moon can respawn them.</summary>
        public bool Respawns;

        private WeaponInstance _original;
        private Renderer[] _renderers;
        private Collider _collider;

        public bool Taken { get; private set; }

        public override string Prompt => Weapon == null ? "" : $"Pegar {Weapon.Name}";

        public static WeaponPickup Spawn(WeaponInstance weapon, Vector3 position, bool respawns = false)
        {
            var go = new GameObject("Arma: " + weapon.Name);
            go.transform.position = position;
            var size = weapon.Class == WeaponClass.Shield ? new Vector3(0.6f, 0.7f, 0.12f)
                : weapon.Class == WeaponClass.Bow ? new Vector3(0.1f, 1.1f, 0.25f)
                : weapon.Class == WeaponClass.TwoHanded ? new Vector3(0.22f, 1.5f, 0.12f)
                : weapon.Class == WeaponClass.Spear ? new Vector3(0.08f, 2.0f, 0.08f)
                : new Vector3(0.12f, 1.0f, 0.1f);
            var visual = Effects.Primitive(PrimitiveType.Cube, go.transform, Palette.Outlined(Palette.WeaponColor(weapon.Class)));
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            visual.transform.localScale = size;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.6f;
            col.isTrigger = true;
            var pickup = go.AddComponent<WeaponPickup>();
            pickup.Weapon = weapon;
            pickup.Respawns = respawns;
            pickup.Range = 2.4f;
            return pickup;
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _collider = GetComponent<Collider>();
            _original = Weapon;
        }

        public override void Interact(PlayerCharacter player)
        {
            if (Taken || Weapon == null) return;
            if (!player.GiveWeapon(Weapon)) return;
            Taken = true;
            SetVisible(false);
            if (!Respawns) Destroy(gameObject, 0.1f);
        }

        /// <summary>Blood moon: the world weapon is back, fresh.</summary>
        public void Respawn()
        {
            if (!Respawns || !Taken || _original == null) return;
            Weapon = new WeaponInstance(_original.Definition) { Modifier = _original.Modifier, ModifierValue = _original.ModifierValue };
            Taken = false;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in _renderers) r.enabled = visible;
            if (_collider != null) _collider.enabled = visible;
            enabled = visible;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
        }
    }

    /// <summary>A material on the ground. Picked up by walking over it.</summary>
    public sealed class MaterialPickup : MonoBehaviour
    {
        public MaterialDefinition Material;
        public int Amount = 1;

        private static readonly List<MaterialPickup> Active = new List<MaterialPickup>();

        public static MaterialPickup Spawn(MaterialDefinition material, Vector3 position, int amount = 1)
        {
            var go = new GameObject("Material: " + material.Name);
            go.transform.position = position;
            var visual = Effects.Primitive(PrimitiveType.Sphere, go.transform, Palette.Outlined(Palette.MaterialColor(material.Category)));
            visual.transform.localPosition = Vector3.up * 0.25f;
            visual.transform.localScale = Vector3.one * 0.35f;
            var pickup = go.AddComponent<MaterialPickup>();
            pickup.Material = material;
            pickup.Amount = amount;
            return pickup;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>Called by the player each frame: collects everything within reach.</summary>
        public static void CollectAround(PlayerCharacter player, float radius)
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var p = Active[i];
                if (p == null) continue;
                if ((p.transform.position - player.transform.position).sqrMagnitude > radius * radius) continue;
                player.AddMaterial(p.Material, p.Amount);
                Destroy(p.gameObject);
            }
        }
    }

    /// <summary>A lit fire: warms the air around it and, with a pot, lets the player cook.</summary>
    public sealed class Campfire : MonoBehaviour
    {
        public bool IsLit = true;
        public bool HasPot = true;
    }

    /// <summary>Cooking pot over a fire. Opens the cooking screen.</summary>
    public sealed class CookingPot : Interactable
    {
        public static event Action<CookingPot> Opened;

        public override string Prompt => "Cozinhar";

        public override void Interact(PlayerCharacter player) => Opened?.Invoke(this);
    }

    /// <summary>Goddess statue: trades 4 Spirit Orbs for a Heart Container or a Stamina Vessel.</summary>
    public sealed class GoddessStatue : Interactable
    {
        public static event Action<GoddessStatue> Opened;

        public override string Prompt => "Rezar";

        public override void Interact(PlayerCharacter player) => Opened?.Invoke(this);
    }

    /// <summary>A sleeping spot: skips to morning or night (and lets the clock pass).</summary>
    public sealed class RestSpot : Interactable
    {
        public static event Action<RestSpot> Opened;

        public override string Prompt => "Descansar";

        public override void Interact(PlayerCharacter player) => Opened?.Invoke(this);
    }

    /// <summary>Finds the nearest interactable each frame, shows its prompt and uses it on the button.</summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public PlayerInputReader Input;
        public PlayerCharacter Character;

        public Interactable Current { get; private set; }

        /// <summary>Raised when the player uses something (drives the pick-up animation).</summary>
        public event Action Interacted;

        private void Update()
        {
            if (Character == null) return;
            MaterialPickup.CollectAround(Character, 1.3f);
            Current = Interactable.Nearest(transform.position);
            if (Current != null && Input != null && Input.InteractPressed)
            {
                Current.Interact(Character);
                Interacted?.Invoke();
            }
        }
    }
}
