using System;
using UnityEngine.Serialization;

namespace CRG.Core
{
    // Grid-independent cell address. What x and y mean is up to the grid topology (axial q/r for hexagons).
    // variant tells apart cells that share a lattice position in tilings with several cell shapes; it is 0
    // for grids with a single cell shape.
    [Serializable]
    public struct CellCoord : IEquatable<CellCoord>
    {
        private const int HashPrimeMultiplier = 397;
        private const int VariantHashMultiplier = 7919;

        [FormerlySerializedAs("columnIndex")]
        public int x;

        [FormerlySerializedAs("rowIndex")]
        public int y;

        public int variant;

        public CellCoord(int x, int y, int variant = 0)
        {
            this.x = x;
            this.y = y;
            this.variant = variant;
        }

        public bool Equals(CellCoord other)
        {
            return x == other.x && y == other.y && variant == other.variant;
        }

        public override bool Equals(object obj)
        {
            return obj is CellCoord other && Equals(other);
        }

        // Must stay identical to the former AxialCoord hash for variant 0: generation iterates hash sets,
        // so a different hash would change the levels existing seeds produce.
        public override int GetHashCode()
        {
            unchecked
            {
                return (x * HashPrimeMultiplier) ^ y ^ (variant * VariantHashMultiplier);
            }
        }

        public static bool operator ==(CellCoord left, CellCoord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CellCoord left, CellCoord right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return variant == 0 ? $"Cell({x}, {y})" : $"Cell({x}, {y}, {variant})";
        }
    }
}
