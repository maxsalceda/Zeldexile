using System;

namespace Zeldexile.Domain
{
    /// <summary>
    /// Life in quarter-heart points. The character starts with 3 hearts and can reach 20 through
    /// Heart Containers (4 Spirit Orbs each). Temporary (yellow) hearts from Hearty food sit on top
    /// of the maximum, are lost first and never regenerate.
    /// </summary>
    public sealed class LifeModel
    {
        public const int PointsPerHeart = 4;
        public const int StartHearts = 3;
        public const int MaxHearts = 20;
        public const int MaxTemporaryHearts = 20;
        public const int OrbsPerUpgrade = 4;

        public int Containers { get; private set; }
        public int Max => Containers * PointsPerHeart;
        public int Current { get; private set; }
        public int Temporary { get; private set; }
        public bool IsDead => Current <= 0 && Temporary <= 0;

        public float Hearts => Current / (float)PointsPerHeart;

        public LifeModel(int hearts = StartHearts)
        {
            Containers = Math.Max(1, Math.Min(MaxHearts, hearts));
            Current = Max;
        }

        /// <summary>Adds one Heart Container (also fills every heart, as the reference game does).</summary>
        public bool AddContainer()
        {
            if (Containers >= MaxHearts) return false;
            Containers++;
            Current = Max;
            return true;
        }

        /// <summary>Removes one container (the Horned Statue trade). Keeps at least 3 hearts.</summary>
        public bool RemoveContainer()
        {
            if (Containers <= StartHearts) return false;
            Containers--;
            Current = Math.Min(Current, Max);
            return true;
        }

        /// <summary>Takes points, yellow hearts first. Returns the points actually lost.</summary>
        public int Damage(int points)
        {
            if (points <= 0 || IsDead) return 0;
            var lost = 0;
            var fromTemporary = Math.Min(Temporary, points);
            Temporary -= fromTemporary;
            lost += fromTemporary;
            points -= fromTemporary;
            var fromCurrent = Math.Min(Current, points);
            Current -= fromCurrent;
            lost += fromCurrent;
            return lost;
        }

        public void Heal(int points)
        {
            if (points <= 0) return;
            Current = Math.Min(Max, Current + points);
        }

        public void HealFully() => Current = Max;

        public void AddTemporary(int points)
        {
            if (points <= 0) return;
            Temporary = Math.Min(MaxTemporaryHearts * PointsPerHeart, Temporary + points);
        }

        public void Revive()
        {
            Current = Max;
            Temporary = 0;
        }
    }
}
