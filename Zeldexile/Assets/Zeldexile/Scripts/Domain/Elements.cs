namespace Zeldexile.Domain
{
    public enum Element
    {
        None,
        Fire,
        Ice,
        Lightning,
        Wind
    }

    public enum SurfaceKind
    {
        None,
        Grass,
        BurningGrass,
        Ash,
        Water,
        Ice,
        Metal,
        Stone
    }

    /// <summary>What happens when an element touches a surface. Pure data, no Unity types.</summary>
    public readonly struct Reaction
    {
        public readonly SurfaceKind NewSurface;
        public readonly bool Ignites;
        public readonly bool CreatesIcePlatform;
        public readonly bool Conducts;
        public readonly bool Electrifies;
        public readonly bool FansFlames;

        public Reaction(SurfaceKind newSurface, bool ignites = false, bool createsIcePlatform = false,
            bool conducts = false, bool electrifies = false, bool fansFlames = false)
        {
            NewSurface = newSurface;
            Ignites = ignites;
            CreatesIcePlatform = createsIcePlatform;
            Conducts = conducts;
            Electrifies = electrifies;
            FansFlames = fansFlames;
        }

        public bool ChangesSurface(SurfaceKind current) => NewSurface != current;
    }

    /// <summary>The world chemistry table (GDD section 4).</summary>
    public static class ElementalReactions
    {
        public static Reaction React(SurfaceKind surface, Element element)
        {
            switch (surface)
            {
                case SurfaceKind.Grass:
                    return element == Element.Fire
                        ? new Reaction(SurfaceKind.BurningGrass, ignites: true)
                        : new Reaction(surface);
                case SurfaceKind.BurningGrass:
                    if (element == Element.Ice) return new Reaction(SurfaceKind.Grass);
                    if (element == Element.Wind) return new Reaction(surface, fansFlames: true);
                    return new Reaction(surface);
                case SurfaceKind.Water:
                    if (element == Element.Ice) return new Reaction(surface, createsIcePlatform: true);
                    if (element == Element.Lightning) return new Reaction(surface, electrifies: true);
                    return new Reaction(surface);
                case SurfaceKind.Ice:
                    return element == Element.Fire
                        ? new Reaction(SurfaceKind.Water)
                        : new Reaction(surface);
                case SurfaceKind.Metal:
                    return element == Element.Lightning
                        ? new Reaction(surface, conducts: true)
                        : new Reaction(surface);
                default:
                    return new Reaction(surface);
            }
        }

        public static string DisplayName(Element element)
        {
            switch (element)
            {
                case Element.Fire: return "Fogo";
                case Element.Ice: return "Gelo";
                case Element.Lightning: return "Raio";
                case Element.Wind: return "Vento";
                default: return "Físico";
            }
        }
    }
}
