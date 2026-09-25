using UnityEngine;

namespace CRG.Core
{
    // Pointy-top hexagons in the XZ plane, addressed with axial coordinates (x = q, y = r).
    // Rows grow towards -Z so that direction i lies at 60 * i degrees from +X
    // (E = 0, NE = 60, NW = 120, W = 180, SW = 240, SE = 300), with +Z as north.
    public class HexTopology : IGridTopology
    {
        public const int East = 0;
        public const int NorthEast = 1;
        public const int NorthWest = 2;
        public const int West = 3;
        public const int SouthWest = 4;
        public const int SouthEast = 5;

        private const int EdgeCount = 6;
        private const float WorldPositionXCoefficient = 1.7320508075688772f;
        private const float WorldPositionXHalfCoefficient = 0.8660254037844386f;
        private const float WorldPositionZCoefficient = 1.5f;
        private const float InverseWorldPositionXCoefficient = 0.5773502691896257f;
        private const float InverseWorldPositionXSecondaryCoefficient = 0.3333333333333333f;
        private const float InverseWorldPositionZCoefficient = 0.6666666666666666f;

        private static readonly int[] DirectionX = { 1, 1, 0, -1, -1, 0 };
        private static readonly int[] DirectionY = { 0, -1, -1, 0, 1, 1 };

        public GridType Type => GridType.Hexagon;

        // An interior half wall (depth t/2) meeting an exterior wall (depth t) at a 120 degree corner.
        public float ThickWallCornerInsetPerThickness => 0.8660254037844386f;

        public int GetEdgeCount(CellCoord cell)
        {
            return EdgeCount;
        }

        public CellCoord GetNeighbor(CellCoord cell, int edge)
        {
            return new CellCoord(cell.x + DirectionX[edge], cell.y + DirectionY[edge]);
        }

        public int GetNeighborEdge(CellCoord cell, int edge)
        {
            return (edge + 3) % EdgeCount;
        }

        public Vector3 GetCellCenter(CellCoord cell, float cellSize)
        {
            float worldX = cellSize * (WorldPositionXCoefficient * cell.x + WorldPositionXHalfCoefficient * cell.y);
            float worldZ = -cellSize * (WorldPositionZCoefficient * cell.y);
            return new Vector3(worldX, 0, worldZ);
        }

        public Vector3 GetCornerOffset(CellCoord cell, int corner, float cellSize)
        {
            return HexMath.GetHexVertex(corner, cellSize);
        }

        public Vector3 GetEdgeNormal(CellCoord cell, int edge)
        {
            return HexMath.GetEdgeNormal(edge);
        }

        public CellCoord GetCellAt(Vector3 position, float cellSize)
        {
            float fractionalQ = (InverseWorldPositionXCoefficient * position.x + InverseWorldPositionXSecondaryCoefficient * position.z) / cellSize;
            float fractionalR = -(InverseWorldPositionZCoefficient * position.z) / cellSize;
            float fractionalS = -fractionalQ - fractionalR;

            int q = Mathf.RoundToInt(fractionalQ);
            int r = Mathf.RoundToInt(fractionalR);
            int s = Mathf.RoundToInt(fractionalS);

            float qDifference = Mathf.Abs(q - fractionalQ);
            float rDifference = Mathf.Abs(r - fractionalR);
            float sDifference = Mathf.Abs(s - fractionalS);

            if (qDifference > rDifference && qDifference > sDifference)
            {
                q = -r - s;
            }
            else if (rDifference > sDifference)
            {
                r = -q - s;
            }

            return new CellCoord(q, r);
        }

        public int GetDistance(CellCoord a, CellCoord b)
        {
            int dq = a.x - b.x;
            int dr = a.y - b.y;
            return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(-dq - dr)) / 2;
        }
    }
}
