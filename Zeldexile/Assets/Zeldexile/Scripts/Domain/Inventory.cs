using System;
using System.Collections.Generic;

namespace Zeldexile.Domain
{
    public enum GearKind
    {
        Weapon,
        Bow,
        Shield
    }

    /// <summary>
    /// Weapon, bow and shield pouches with a fixed number of slots, expanded with Korok seeds:
    /// weapons 6 → 16, bows 4 → 10, shields 3 → 14 (170 seeds in total).
    /// </summary>
    public sealed class GearInventory
    {
        private static readonly int[] StartSlots = { 6, 4, 3 };
        private static readonly int[] MaxSlots = { 16, 10, 14 };

        private static readonly int[][] SlotCosts =
        {
            new[] { 1, 2, 3, 4, 6, 8, 10, 12, 15, 18 },
            new[] { 1, 2, 3, 4, 6, 8 },
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12 }
        };

        public const int TotalSeedsForMax = 170;

        private readonly int[] _slots = (int[])StartSlots.Clone();
        private readonly List<WeaponInstance>[] _items =
            { new List<WeaponInstance>(), new List<WeaponInstance>(), new List<WeaponInstance>() };

        public int Slots(GearKind kind) => _slots[(int)kind];
        public int MaxSlotsOf(GearKind kind) => MaxSlots[(int)kind];
        public IReadOnlyList<WeaponInstance> Items(GearKind kind) => _items[(int)kind];
        public bool IsFull(GearKind kind) => _items[(int)kind].Count >= _slots[(int)kind];

        public bool TryAdd(WeaponInstance weapon)
        {
            var kind = weapon.Kind;
            if (IsFull(kind)) return false;
            _items[(int)kind].Add(weapon);
            return true;
        }

        public bool Remove(WeaponInstance weapon) => _items[(int)weapon.Kind].Remove(weapon);

        public bool Contains(WeaponInstance weapon) => _items[(int)weapon.Kind].Contains(weapon);

        /// <summary>Seeds for the next slot of a kind, or -1 at the maximum.</summary>
        public int NextUpgradeCost(GearKind kind)
        {
            var bought = _slots[(int)kind] - StartSlots[(int)kind];
            var costs = SlotCosts[(int)kind];
            return bought < costs.Length ? costs[bought] : -1;
        }

        /// <summary>Buys one slot if there are enough seeds; deducts them.</summary>
        public bool Upgrade(GearKind kind, ref int seeds)
        {
            var cost = NextUpgradeCost(kind);
            if (cost < 0 || seeds < cost) return false;
            seeds -= cost;
            _slots[(int)kind]++;
            return true;
        }

        public static string KindName(GearKind kind)
        {
            switch (kind)
            {
                case GearKind.Bow: return "Arcos";
                case GearKind.Shield: return "Escudos";
                default: return "Armas";
            }
        }

        public static int SumOfAllCosts()
        {
            var total = 0;
            foreach (var costs in SlotCosts)
                foreach (var c in costs)
                    total += c;
            return total;
        }
    }
}
