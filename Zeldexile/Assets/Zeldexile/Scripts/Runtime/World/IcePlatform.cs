using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain;

namespace Zeldexile.World
{
    /// <summary>
    /// Walkable disc of ice created when cold hits water. Melts with time or fire. Only three can stand
    /// at once: raising a fourth breaks the oldest.
    /// </summary>
    public sealed class IcePlatform : MonoBehaviour
    {
        public const int MaxActive = 3;

        private static readonly List<IcePlatform> Active = new List<IcePlatform>();

        [SerializeField] private float lifetime = 14f;
        private float _age;
        private Vector3 _fullScale;
        private bool _melting;

        public static int Count => Active.Count;

        public static IcePlatform Spawn(Vector3 waterTop, float radius)
        {
            while (Active.Count >= MaxActive)
            {
                var oldest = Active[0];
                Active.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Plataforma de gelo";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = waterTop + Vector3.up * 0.05f;
            go.transform.localScale = new Vector3(radius * 2f, 0.2f, radius * 2f);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            col.convex = true;
            var surface = go.AddComponent<ElementalSurface>();
            surface.Kind = SurfaceKind.Ice;
            surface.Repaint();
            return go.AddComponent<IcePlatform>();
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Start() => _fullScale = transform.localScale;

        public void Melt()
        {
            if (_melting) return;
            _melting = true;
            _age = Mathf.Max(_age, lifetime - 1f);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            var remaining = lifetime - _age;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (remaining < 2f)
            {
                var k = Mathf.Max(0.05f, remaining / 2f);
                transform.localScale = new Vector3(_fullScale.x * k, _fullScale.y, _fullScale.z * k);
            }
        }
    }
}
