using UnityEngine;

namespace CRG.Core
{
    // Axis-aligned squares in the XZ plane. Cell (x, y) is centered at (x * size, 0, y * size), with +Z as north.
    // Edge i faces direction 90 * i degrees from +X: 0 = East, 1 = North, 2 = West, 3 = South.
    // Corner i sits at 45 + 90 * i degrees, so edge i runs from corner i - 1 to corner i.
    public class SquareTopology : IGridTopology
    {
        public const int East = 0;
        public const int North = 1;
        public const int West = 2;
        public const int South = 3;

        private const int EdgeCount = 4;

        private static readonly int[] DirectionX = { 1, 0, -1, 0 };
        private static readonly int[] DirectionY = { 0, 1, 0, -1 };
        private static readonly float[] CornerX = { 0.5f, -0.5f, -0.5f, 0.5f };
        private static readonly float[] CornerZ = { 0.5f, 0.5f, -0.5f, -0.5f };

        public GridType Type => GridType.Square;

        // An interior half wall (depth t/2) meeting an exterior wall (depth t) at a right angle ends t along the edge.
        public float ThickWallCornerInsetPerThickness => 1f;

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
            return (edge + 2) % EdgeCount;
        }

        public Vector3 GetCellCenter(CellCoord cell, float cellSize)
        {
            return new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);
        }

        public Vector3 GetCornerOffset(CellCoord cell, int corner, float cellSize)
        {
            return new Vector3(CornerX[corner] * cellSize, 0f, CornerZ[corner] * cellSize);
        }

        public Vector3 GetEdgeNormal(CellCoord cell, int edge)
        {
            return new Vector3(DirectionX[edge], 0f, DirectionY[edge]);
        }

        public CellCoord GetCellAt(Vector3 position, float cellSize)
        {
            return new CellCoord(Mathf.RoundToInt(position.x / cellSize), Mathf.RoundToInt(position.z / cellSize));
        }

        public int GetDistance(CellCoord a, CellCoord b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
