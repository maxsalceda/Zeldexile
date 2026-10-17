using System;

namespace Zeldexile.Domain
{
    /// <summary>
    /// The stamina wheel: 1000 units per wheel, 1 wheel at the start, up to 3 wheels through Stamina
    /// Vessels (200 units, 4 Spirit Orbs each). Temporary (yellow) stamina from Enduring food is spent
    /// first and never regenerates. Emptying the wheel exhausts the character until it is full again.
    /// </summary>
    public sealed class StaminaModel
    {
        public const float WheelUnits = 1000f;
        public const float VesselUnits = 200f;
        public const int MaxWheels = 3;
        public const int MaxVessels = 10;
        public const float MaxTemporaryUnits = 2f * WheelUnits;

        public int Vessels { get; private set; }
        public float Max => WheelUnits + Vessels * VesselUnits;
        public float Current { get; private set; }
        public float Temporary { get; private set; }
        public float RegenPerSecond { get; set; } = 300f;
        public float RegenDelay { get; set; } = 0.5f;
        public bool Exhausted { get; private set; }

        private float _sinceLastSpend;

        public StaminaModel(int vessels = 0)
        {
            Vessels = Math.Max(0, Math.Min(MaxVessels, vessels));
            Current = Max;
        }

        public float Wheels => Max / WheelUnits;
        public float Normalized => Current / Max;
        public bool IsFull => Current >= Max && Temporary <= 0f;
        public bool CanSpend => !Exhausted && (Current > 0f || Temporary > 0f);

        public bool AddVessel()
        {
            if (Vessels >= MaxVessels) return false;
            Vessels++;
            Current = Max;
            return true;
        }

        public bool RemoveVessel()
        {
            if (Vessels <= 0) return false;
            Vessels--;
            Current = Math.Min(Current, Max);
            return true;
        }

        /// <summary>Energizing food: refills part of the wheel.</summary>
        public void Restore(float units)
        {
            if (units <= 0f) return;
            Current = Math.Min(Max, Current + units);
            if (Current >= Max) Exhausted = false;
        }

        /// <summary>Enduring food: yellow stamina above the maximum.</summary>
        public void AddTemporary(float units)
        {
            if (units <= 0f) return;
            Temporary = Math.Min(MaxTemporaryUnits, Temporary + units);
            Exhausted = false;
        }

        /// <summary>Spends an amount (rate × dt or a one-off cost). Returns false when the character ran out.</summary>
        public bool Drain(float amount)
        {
            if (!CanSpend) return false;
            _sinceLastSpend = 0f;
            if (Temporary > 0f)
            {
                var fromTemporary = Math.Min(Temporary, amount);
                Temporary -= fromTemporary;
                amount -= fromTemporary;
                if (amount <= 0f) return true;
            }

            Current -= amount;
            if (Current <= 0f)
            {
                Current = 0f;
                Exhausted = true;
                return false;
            }

            return true;
        }

        public void Tick(float dt)
        {
            _sinceLastSpend += dt;
            if (_sinceLastSpend < RegenDelay) return;
            Current = Math.Min(Max, Current + RegenPerSecond * dt);
            if (Exhausted && Current >= Max) Exhausted = false;
        }

        public void Refill()
        {
            Current = Max;
            Exhausted = false;
        }
    }
}
