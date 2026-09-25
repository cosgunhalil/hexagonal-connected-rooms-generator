using System;
using System.Collections.Generic;
using UnityEngine;

namespace CRG.Core
{
    [Serializable]
    public struct AxialCoord : IEquatable<AxialCoord>
    {
        private const int HashPrimeMultiplier = 397;
        private const float WorldPositionXCoefficient = 1.7320508075688772f;
        private const float WorldPositionXHalfCoefficient = 0.8660254037844386f;
        private const float WorldPositionZCoefficient = 1.5f;
        private const float InverseWorldPositionXCoefficient = 0.5773502691896257f;
        private const float InverseWorldPositionXSecondaryCoefficient = 0.3333333333333333f;
        private const float InverseWorldPositionZCoefficient = 0.6666666666666666f;

        public int columnIndex;
        public int rowIndex;

        public AxialCoord(int columnIndex, int rowIndex)
        {
            this.columnIndex = columnIndex;
            this.rowIndex = rowIndex;
        }

        public int cubeCoordinateZ => -columnIndex - rowIndex;

        public AxialCoord GetNeighbor(int direction)
        {
            AxialCoord directionVector = HexDirection.GetDirectionVector(direction);
            return new AxialCoord(columnIndex + directionVector.columnIndex, rowIndex + directionVector.rowIndex);
        }

        public IEnumerable<AxialCoord> GetAllNeighbors()
        {
            for (int i = 0; i < 6; i++)
            {
                yield return GetNeighbor(i);
            }
        }

        public int GetSharedEdgeDirection(AxialCoord neighbor)
        {
            for (int i = 0; i < 6; i++)
            {
                if (GetNeighbor(i).Equals(neighbor))
                    return i;
            }
            return -1;
        }

        public int DistanceTo(AxialCoord other)
        {
            return (Mathf.Abs(columnIndex - other.columnIndex) + 
                    Mathf.Abs(rowIndex - other.rowIndex) + 
                    Mathf.Abs(cubeCoordinateZ - other.cubeCoordinateZ)) / 2;
        }

        public Vector3 ToCubeCoordinates()
        {
            return new Vector3(columnIndex, rowIndex, cubeCoordinateZ);
        }

        public static AxialCoord FromCubeCoordinates(int cubeX, int cubeY, int cubeZ)
        {
            if (cubeX + cubeY + cubeZ != 0)
                throw new ArgumentException("Cube coordinates must sum to zero");
            return new AxialCoord(cubeX, cubeY);
        }

        // Pointy-top layout. Rows grow towards -Z so that HexDirection i lies at 60 * i degrees
        // from +X in the XZ plane (E = 0, NE = 60, NW = 120, ...), with +Z as north.
        public Vector3 ToWorldPosition(float hexSize)
        {
            float worldX = hexSize * (WorldPositionXCoefficient * columnIndex + WorldPositionXHalfCoefficient * rowIndex);
            float worldZ = -hexSize * (WorldPositionZCoefficient * rowIndex);
            return new Vector3(worldX, 0, worldZ);
        }

        public static AxialCoord FromWorldPosition(Vector3 worldPosition, float hexSize)
        {
            float fractionalColumn = (InverseWorldPositionXCoefficient * worldPosition.x + InverseWorldPositionXSecondaryCoefficient * worldPosition.z) / hexSize;
            float fractionalRow = -(InverseWorldPositionZCoefficient * worldPosition.z) / hexSize;
            return RoundToAxial(fractionalColumn, fractionalRow);
        }

        private static AxialCoord RoundToAxial(float fractionalColumn, float fractionalRow)
        {
            float fractionalZ = -fractionalColumn - fractionalRow;
            
            int roundedColumn = Mathf.RoundToInt(fractionalColumn);
            int roundedRow = Mathf.RoundToInt(fractionalRow);
            int roundedZ = Mathf.RoundToInt(fractionalZ);

            float columnDifference = Mathf.Abs(roundedColumn - fractionalColumn);
            float rowDifference = Mathf.Abs(roundedRow - fractionalRow);
            float zDifference = Mathf.Abs(roundedZ - fractionalZ);

            if (columnDifference > rowDifference && columnDifference > zDifference)
            {
                roundedColumn = -roundedRow - roundedZ;
            }
            else if (rowDifference > zDifference)
            {
                roundedRow = -roundedColumn - roundedZ;
            }

            return new AxialCoord(roundedColumn, roundedRow);
        }

        public bool Equals(AxialCoord other)
        {
            return columnIndex == other.columnIndex && rowIndex == other.rowIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is AxialCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (columnIndex * HashPrimeMultiplier) ^ rowIndex;
            }
        }

        public static bool operator ==(AxialCoord left, AxialCoord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AxialCoord left, AxialCoord right)
        {
            return !left.Equals(right);
        }

        public static AxialCoord operator +(AxialCoord a, AxialCoord b)
        {
            return new AxialCoord(a.columnIndex + b.columnIndex, a.rowIndex + b.rowIndex);
        }

        public static AxialCoord operator -(AxialCoord a, AxialCoord b)
        {
            return new AxialCoord(a.columnIndex - b.columnIndex, a.rowIndex - b.rowIndex);
        }

        public static AxialCoord operator *(AxialCoord a, int scale)
        {
            return new AxialCoord(a.columnIndex * scale, a.rowIndex * scale);
        }

        public override string ToString()
        {
            return $"Axial({columnIndex}, {rowIndex})";
        }
    }
}
