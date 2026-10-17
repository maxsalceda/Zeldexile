using UnityEngine;
using Zeldexile.Domain;

namespace Zeldexile.World
{
    /// <summary>Placeholder visual effects built from primitives. Swapped for real VFX later.</summary>
    public static class Effects
    {
        public static GameObject Flames(Transform parent, float topY)
        {
            var root = new GameObject("Chamas");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(parent.position.x, topY, parent.position.z);
            for (var i = 0; i < 3; i++)
            {
                var flame = Primitive(PrimitiveType.Cube, root.transform, Palette.Glow(new Color(1f, 0.55f, 0.1f, 0.85f)));
                flame.transform.localPosition = new Vector3(Random.Range(-0.6f, 0.6f), 0.4f, Random.Range(-0.6f, 0.6f));
                flame.transform.localScale = new Vector3(0.35f, 0.8f, 0.35f);
                flame.AddComponent<Flicker>();
            }

            return root;
        }

        public static void UpdraftColumn(Transform parent, float radius, float height)
        {
            for (var i = 0; i < 4; i++)
            {
                var wisp = Primitive(PrimitiveType.Cube, parent, Palette.Glow(new Color(1f, 0.9f, 0.7f, 0.25f)));
                wisp.transform.localScale = new Vector3(0.15f, 1.2f, 0.15f);
                var rise = wisp.AddComponent<Rise>();
                rise.Radius = radius * 0.8f;
                rise.Height = Mathf.Min(height, 14f);
                rise.Phase = i / 4f;
            }
        }

        public static void Flash(Vector3 position, float radius, Color color)
        {
            color.a = 0.45f;
            var go = Primitive(PrimitiveType.Sphere, null, Palette.Glow(color));
            go.transform.position = position;
            go.transform.localScale = Vector3.one * radius * 2f;
            go.AddComponent<Fade>().Duration = 0.25f;
        }

        public static void Arc(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Arco");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Palette.Glow(Palette.ForElement(Element.Lightning));
            line.widthMultiplier = 0.12f;
            line.positionCount = 6;
            for (var i = 0; i < 6; i++)
            {
                var t = i / 5f;
                var jitter = i == 0 || i == 5 ? Vector3.zero : Random.insideUnitSphere * 0.35f;
                line.SetPosition(i, Vector3.Lerp(from, to, t) + jitter);
            }

            Object.Destroy(go, 0.2f);
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent != null) go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private sealed class Flicker : MonoBehaviour
        {
            private float _seed;
            private void Start() => _seed = Random.value * 10f;

            private void Update()
            {
                var s = 0.6f + Mathf.PerlinNoise(_seed, Time.time * 6f) * 0.8f;
                transform.localScale = new Vector3(0.35f, s, 0.35f);
            }
        }

        private sealed class Rise : MonoBehaviour
        {
            public float Radius;
            public float Height;
            public float Phase;

            private void Update()
            {
                var t = Mathf.Repeat(Time.time * 0.5f + Phase, 1f);
                var angle = (Phase + t * 0.3f) * Mathf.PI * 2f;
                transform.localPosition = new Vector3(Mathf.Cos(angle) * Radius, t * Height, Mathf.Sin(angle) * Radius);
            }
        }

        private sealed class Fade : MonoBehaviour
        {
            public float Duration = 0.3f;
            private float _age;
            private Vector3 _scale;

            private void Start() => _scale = transform.localScale;

            private void Update()
            {
                _age += Time.deltaTime;
                transform.localScale = _scale * (1f + _age / Duration * 0.3f);
                if (_age >= Duration) Destroy(gameObject);
            }
        }
    }

    /// <summary>Draws impact flashes and lightning arcs for every resolved impact.</summary>
    public sealed class EffectsPresenter : MonoBehaviour
    {
        private void OnEnable()
        {
            ImpactResolver.Resolved += OnResolved;
            ImpactResolver.Chained += Effects.Arc;
        }

        private void OnDisable()
        {
            ImpactResolver.Resolved -= OnResolved;
            ImpactResolver.Chained -= Effects.Arc;
        }

        private static void OnResolved(ElementalImpact impact)
        {
            if (impact.SkipSurfaces || impact.Radius <= 0f) return;
            Effects.Flash(impact.Position, impact.Radius, Palette.ForElement(impact.Element));
        }
    }
}
