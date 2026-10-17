using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Combat;
using Zeldexile.Domain;

namespace Zeldexile.World
{
    /// <summary>Swimmable water. Its collider is a trigger; the top of the box is the water surface.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class WaterVolume : MonoBehaviour
    {
        private static readonly List<WaterVolume> Active = new List<WaterVolume>();
        private static readonly Collider[] Buffer = new Collider[64];

        private BoxCollider _box;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public float SurfaceY => _box.bounds.max.y;

        public bool Contains(Vector3 point)
        {
            var b = _box.bounds;
            return point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z &&
                   point.y >= b.min.y && point.y <= b.max.y;
        }

        /// <summary>Returns the water containing the point, if any.</summary>
        public static WaterVolume At(Vector3 point)
        {
            foreach (var water in Active)
                if (water.Contains(point))
                    return water;
            return null;
        }

        /// <summary>Lightning in water hurts every creature in it.</summary>
        public void Electrify(float damage)
        {
            var b = _box.bounds;
            var count = Physics.OverlapBoxNonAlloc(b.center, b.extents, Buffer, Quaternion.identity, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            var hit = new HashSet<Health>();
            for (var i = 0; i < count; i++)
            {
                var health = Buffer[i].GetComponentInParent<Health>();
                if (health != null && hit.Add(health)) health.TakeDamage(damage * 1.5f, Element.Lightning);
            }

            Effects.Flash(new Vector3(b.center.x, SurfaceY, b.center.z), Mathf.Max(b.extents.x, b.extents.z),
                Palette.ForElement(Element.Lightning));
        }
    }
}
