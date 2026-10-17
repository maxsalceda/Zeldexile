using System;
using System.Collections.Generic;
using UnityEngine;
using Zeldexile.World;
using Object = UnityEngine.Object;

namespace Zeldexile.Characters
{
    /// <summary>
    /// Loads the Blender-built models from Resources/Models and colors each mesh by the "@slot" suffix
    /// of its name, so one model serves many color styles and elemental states.
    /// </summary>
    public static class ModelLibrary
    {
        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        private static readonly Dictionary<string, Color> PropColors = new Dictionary<string, Color>
        {
            { "bark", new Color(0.45f, 0.30f, 0.20f) },
            { "leaf", new Color(0.42f, 0.70f, 0.26f) },
            { "leafdark", new Color(0.22f, 0.50f, 0.30f) },
            { "stone", new Color(0.64f, 0.61f, 0.56f) },
            { "stonedark", new Color(0.50f, 0.48f, 0.46f) },
            { "copper", Palette.Metal },
            { "wood", new Color(0.58f, 0.38f, 0.20f) },
            { "metal", new Color(0.72f, 0.72f, 0.76f) },
            { "accent", new Color(0.95f, 0.78f, 0.25f) },
            { "grass", Palette.Grass },
        };

        /// <summary>Slots that read better without a contour (thin or ground-hugging pieces).</summary>
        private static readonly HashSet<string> NoOutline = new HashSet<string> { "grass", "dark" };

        public static bool Has(string path) => Load(path) != null;

        public static GameObject Load(string path)
        {
            if (Cache.TryGetValue(path, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>("Models/" + path);
            Cache[path] = prefab;
            return prefab;
        }

        /// <summary>Instantiates a model and assigns a material to every mesh from its slot.</summary>
        public static GameObject Spawn(string path, Transform parent, Func<string, Color> colorForSlot)
        {
            var prefab = Load(path);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = prefab.name;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var slot = SlotOf(r.name);
                var color = colorForSlot(slot);
                r.sharedMaterial = NoOutline.Contains(slot) ? Palette.Solid(color) : Palette.Outlined(color);
            }

            return go;
        }

        /// <summary>A prop with the default world colors, placed with its origin (bottom center) at a point.</summary>
        public static GameObject SpawnProp(string path, Transform parent, Vector3 position, float yaw = 0f, float scale = 1f)
        {
            var go = Spawn("Props/" + path, parent, PropColor);
            if (go == null) return null;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        public static Color PropColor(string slot) => PropColors.TryGetValue(slot, out var c) ? c : Color.white;

        /// <summary>"Tronco~Peito@tunic" -> "tunic". Ignores a ".001" suffix if an editor tool adds one.</summary>
        public static string SlotOf(string objectName)
        {
            var at = objectName.LastIndexOf('@');
            if (at < 0) return string.Empty;
            var slot = objectName.Substring(at + 1);
            var dot = slot.IndexOf('.');
            return dot < 0 ? slot : slot.Substring(0, dot);
        }
    }
}
