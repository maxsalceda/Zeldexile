using System.Collections.Generic;
using UnityEngine;

namespace Zeldexile.World
{
    /// <summary>A column of hot air. Gliding inside it lifts the character.</summary>
    public sealed class UpdraftZone : MonoBehaviour
    {
        private static readonly List<UpdraftZone> Active = new List<UpdraftZone>();

        public float Radius = 2f;
        public float Height = 30f;
        public float Lift = 9f;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public static UpdraftZone Create(Vector3 basePosition, float radius, float height, float lift)
        {
            var go = new GameObject("Corrente de ar");
            go.transform.position = basePosition;
            var zone = go.AddComponent<UpdraftZone>();
            zone.Radius = radius;
            zone.Height = height;
            zone.Lift = lift;
            Effects.UpdraftColumn(go.transform, radius, height);
            return zone;
        }

        /// <summary>Strongest lift at a point, or 0 outside every column.</summary>
        public static float LiftAt(Vector3 position)
        {
            var best = 0f;
            foreach (var zone in Active)
            {
                var p = zone.transform.position;
                if (position.y < p.y - 1f || position.y > p.y + zone.Height) continue;
                var dx = position.x - p.x;
                var dz = position.z - p.z;
                if (dx * dx + dz * dz > zone.Radius * zone.Radius) continue;
                best = Mathf.Max(best, zone.Lift);
            }

            return best;
        }
    }
}
