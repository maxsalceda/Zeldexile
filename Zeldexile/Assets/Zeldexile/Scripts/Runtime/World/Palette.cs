using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain;

namespace Zeldexile.World
{
    /// <summary>
    /// The functional color code: every material reads at a glance (metal shines, wood is matte brown,
    /// explosive is red, ancient tech is cyan), and every creature tier has its color.
    /// Flat toon materials cached by color; final art replaces this without touching logic.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Grass = new Color(0.42f, 0.66f, 0.30f);
        public static readonly Color BurningGrass = new Color(0.95f, 0.45f, 0.12f);
        public static readonly Color Ash = new Color(0.25f, 0.24f, 0.23f);
        public static readonly Color Water = new Color(0.20f, 0.48f, 0.78f, 0.65f);
        public static readonly Color Ice = new Color(0.78f, 0.92f, 1f);
        public static readonly Color Metal = new Color(0.80f, 0.48f, 0.27f);
        public static readonly Color MetalPowered = new Color(1f, 0.95f, 0.45f);
        public static readonly Color Stone = new Color(0.58f, 0.55f, 0.50f);
        public static readonly Color Cliff = new Color(0.50f, 0.45f, 0.40f);
        public static readonly Color Ground = new Color(0.47f, 0.60f, 0.34f);
        public static readonly Color Player = new Color(0.20f, 0.45f, 0.85f);
        public static readonly Color Enemy = new Color(0.75f, 0.22f, 0.20f);
        public static readonly Color Chest = new Color(0.65f, 0.45f, 0.20f);
        public static readonly Color Wood = new Color(0.58f, 0.38f, 0.20f);
        public static readonly Color Steel = new Color(0.72f, 0.74f, 0.80f);
        /// <summary>Ancient technology: active cyan, dormant orange.</summary>
        public static readonly Color AncientActive = new Color(0.35f, 0.95f, 1f);
        public static readonly Color AncientDormant = new Color(1f, 0.55f, 0.15f);
        public static readonly Color Statue = new Color(0.82f, 0.80f, 0.72f);

        private static readonly Dictionary<Color, Material> Opaque = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> Transparent = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> Outline = new Dictionary<Color, Material>();

        public static Color ForElement(Element element)
        {
            switch (element)
            {
                case Element.Fire: return new Color(1f, 0.45f, 0.1f);
                case Element.Ice: return new Color(0.6f, 0.9f, 1f);
                case Element.Lightning: return new Color(1f, 0.95f, 0.4f);
                case Element.Wind: return new Color(0.85f, 1f, 0.9f);
                default: return Color.white;
            }
        }

        public static Color ForSurface(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Grass: return Grass;
                case SurfaceKind.BurningGrass: return BurningGrass;
                case SurfaceKind.Ash: return Ash;
                case SurfaceKind.Water: return Water;
                case SurfaceKind.Ice: return Ice;
                case SurfaceKind.Metal: return Metal;
                default: return Stone;
            }
        }

        /// <summary>Tier color of a creature: red, blue, black, silver.</summary>
        public static Color ForTier(EnemyTier tier)
        {
            switch (tier)
            {
                case EnemyTier.Blue: return new Color(0.25f, 0.45f, 0.85f);
                case EnemyTier.Black: return new Color(0.16f, 0.14f, 0.18f);
                case EnemyTier.Silver: return new Color(0.78f, 0.80f, 0.86f);
                default: return new Color(0.78f, 0.26f, 0.20f);
            }
        }

        public static Color WeaponColor(WeaponClass cls)
        {
            switch (cls)
            {
                case WeaponClass.Bow: return Wood;
                case WeaponClass.Shield: return new Color(0.55f, 0.50f, 0.42f);
                case WeaponClass.Spear: return new Color(0.62f, 0.52f, 0.36f);
                case WeaponClass.TwoHanded: return new Color(0.60f, 0.62f, 0.68f);
                default: return Steel;
            }
        }

        public static Color MaterialColor(MaterialCategory category)
        {
            switch (category)
            {
                case MaterialCategory.Fruit: return new Color(0.90f, 0.30f, 0.25f);
                case MaterialCategory.Mushroom: return new Color(0.85f, 0.70f, 0.45f);
                case MaterialCategory.Herb: return new Color(0.40f, 0.80f, 0.40f);
                case MaterialCategory.Meat: return new Color(0.75f, 0.35f, 0.35f);
                case MaterialCategory.Critter: return new Color(0.60f, 0.85f, 0.55f);
                case MaterialCategory.MonsterPart: return new Color(0.45f, 0.30f, 0.50f);
                case MaterialCategory.Ore: return new Color(0.55f, 0.75f, 0.95f);
                case MaterialCategory.Wood: return Wood;
                default: return new Color(1f, 0.85f, 0.3f);
            }
        }

        public static Material Solid(Color color)
        {
            if (Opaque.TryGetValue(color, out var m) && m != null) return m;
            m = new Material(FindShader("Zeldexile/Toon", "Standard")) { color = color };
            Opaque[color] = m;
            return m;
        }

        /// <summary>Toon with a dark contour. For characters and props, not for large terrain blocks.</summary>
        public static Material Outlined(Color color)
        {
            if (Outline.TryGetValue(color, out var m) && m != null) return m;
            m = new Material(FindShader("Zeldexile/ToonOutline", "Standard")) { color = color };
            Outline[color] = m;
            return m;
        }

        /// <summary>Unlit, alpha-blended. Used for water, glows and effects.</summary>
        public static Material Glow(Color color)
        {
            if (Transparent.TryGetValue(color, out var m) && m != null) return m;
            m = new Material(FindShader("Sprites/Default")) { color = color };
            Transparent[color] = m;
            return m;
        }

        /// <summary>Recolors a renderer, keeping the contour if it already had one.</summary>
        public static void Paint(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            var outlined = renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "Zeldexile/ToonOutline";
            renderer.sharedMaterial = color.a < 1f ? Glow(color) : outlined ? Outlined(color) : Solid(color);
        }

        private static Shader FindShader(string name, string fallback = null)
        {
            var shader = Shader.Find(name);
            if (shader == null && fallback != null) shader = Shader.Find(fallback);
            return shader != null ? shader : Shader.Find("Hidden/InternalErrorShader");
        }
    }
}
