using System;
using UnityEngine;
using Zeldexile.Combat;
using Zeldexile.Domain;
using Zeldexile.World;

namespace Zeldexile.Player
{
    /// <summary>Casts the selected rune toward the point under the screen center. All four runes are always there.</summary>
    public sealed class PlayerCaster : MonoBehaviour
    {
        public PlayerInputReader Input;
        public PlayerCharacter Character;
        public PlayerMotor Motor;
        public Camera ViewCamera;
        public PlayerTargeting Targeting;

        private float[] _readyAt = new float[0];

        /// <summary>Raised every time a rune is cast (drives the cast animation).</summary>
        public event Action Casted;

        public int Selected { get; private set; }

        public RuneDefinition SelectedRune => Character.Runes[Mathf.Clamp(Selected, 0, Character.Runes.Count - 1)];

        public float CooldownRemaining(int slot) =>
            slot < _readyAt.Length ? Mathf.Max(0f, _readyAt[slot] - Time.time) : 0f;

        public SkillStats StatsFor(int slot)
        {
            var stats = SkillBuilder.Build(Character.Runes[slot], Character.Stats);
            if (Character.CounterActive) stats.Cooldown *= 0.3f;
            return stats;
        }

        private void Update()
        {
            var runes = Character.Runes;
            if (_readyAt.Length != runes.Count) _readyAt = new float[runes.Count];
            if (runes.Count == 0) return;

            if (Input.SkillSlotPressed >= 0 && Input.SkillSlotPressed < runes.Count) Selected = Input.SkillSlotPressed;
            Selected = Mathf.Clamp(Selected, 0, runes.Count - 1);

            // No casting while hanging on a wall or with the shield up.
            if (!Input.RunePressed || Motor.Current == PlayerMotor.State.Climbing || Input.GuardHeld) return;
            if (Time.time < _readyAt[Selected]) return;

            var stats = StatsFor(Selected);
            _readyAt[Selected] = Time.time + stats.Cooldown;
            Cast(stats);
            Casted?.Invoke();
        }

        private void Cast(SkillStats stats)
        {
            var origin = transform.position + Vector3.up * 1.3f;
            var aimPoint = AimPoint(stats.Range);
            var direction = (aimPoint - origin).normalized;

            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            origin += flat.normalized * 0.6f;

            if (stats.Shape == SkillShape.Cone)
            {
                ImpactResolver.ResolveCone(new ElementalImpact
                {
                    Element = stats.Element, Position = origin, Radius = stats.Radius, Damage = stats.Damage,
                    Force = stats.Force, Direction = flat.normalized, Owner = gameObject,
                    SneakMultiplier = stats.SneakMultiplier
                }, stats.Range, 35f + stats.Radius * 3f);
                Effects.Flash(origin + flat.normalized * 2f, stats.Radius * 0.6f, Palette.ForElement(stats.Element));
                return;
            }

            var count = stats.Projectiles;
            var spread = stats.SpreadDegrees;
            for (var i = 0; i < count; i++)
            {
                var offset = count == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (float)(count - 1));
                var dir = Quaternion.AngleAxis(offset, Vector3.up) * direction;
                Projectile.Launch(stats, origin, dir, gameObject);
            }
        }

        public Vector3 AimPoint(float range)
        {
            if (Targeting != null && Targeting.Target != null)
                return Targeting.Target.transform.position + Vector3.up * 1f;

            var ray = ViewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // The player is on the Ignore Raycast layer, so the aim ray never stops on the character.
            if (Physics.Raycast(ray, out var hit, range + 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                return hit.point;
            return ray.origin + ray.direction * range;
        }
    }
}
