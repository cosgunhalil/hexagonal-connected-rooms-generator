using UnityEngine;

namespace CRG.Core
{
    public class HexDirection
    {
        public const int East = 0;
        public const int NorthEast = 1;
        public const int NorthWest = 2;
        public const int West = 3;
        public const int SouthWest = 4;
        public const int SouthEast = 5;

        private static readonly AxialCoord[] directionVectors = new AxialCoord[]
        {
            new AxialCoord(1, 0),   // East
            new AxialCoord(1, -1),  // NorthEast
            new AxialCoord(0, -1),  // NorthWest
            new AxialCoord(-1, 0),  // West
            new AxialCoord(-1, 1),  // SouthWest
            new AxialCoord(0, 1)    // SouthEast
        };

        public static AxialCoord GetDirectionVector(int direction)
        {
            if (direction < 0 || direction > 5)
                throw new System.ArgumentOutOfRangeException(nameof(direction), "Direction must be between 0 and 5");
            
            return directionVectors[direction];
        }

        public static bool IsValidDirection(int direction)
        {
            return direction >= 0 && direction <= 5;
        }

        public static int GetOppositeDirection(int direction)
        {
            if (direction < 0 || direction > 5)
                throw new System.ArgumentOutOfRangeException(nameof(direction), "Direction must be between 0 and 5");
            
            return (direction + 3) % 6;
        }
    }
}
