using UnityEngine;

namespace CRG.Core
{
    // Equilateral triangles in the XZ plane, with +Z as north. The lattice has basis vectors a = (size, 0) and
    // b = (size / 2, size * sqrt(3) / 2); the rhombus at p = x * a + y * b holds two cells:
    // - variant 0, pointing up:   corners p, p + a, p + b;
    // - variant 1, pointing down: corners p + a, p + a + b, p + b.
    // Corners are counter-clockwise and edge i runs from corner i - 1 to corner i, so
    // up edges are 0 = left, 1 = bottom, 2 = diagonal; down edges are 0 = diagonal, 1 = right, 2 = top.
    // Every neighbor has the opposite variant.
    public class TriangleTopology : IGridTopology
    {
        public const int Up = 0;
        public const int Down = 1;

        private const int EdgeCount = 3;
        private const float HalfSqrt3 = 0.8660254037844386f;
        private const float InverseHalfSqrt3 = 1.1547005383792515f;

        // Corner positions in units of size, relative to the rhombus origin p.
        private static readonly float[,] UpCorners = { { 0f, 0f }, { 1f, 0f }, { 0.5f, HalfSqrt3 } };
        private static readonly float[,] DownCorners = { { 1f, 0f }, { 1.5f, HalfSqrt3 }, { 0.5f, HalfSqrt3 } };

        // Outward edge normals (x, z).
        private static readonly float[,] UpNormals = { { -HalfSqrt3, 0.5f }, { 0f, -1f }, { HalfSqrt3, 0.5f } };
        private static readonly float[,] DownNormals = { { -HalfSqrt3, -0.5f }, { HalfSqrt3, -0.5f }, { 0f, 1f } };

        public GridType Type => GridType.Triangle;

        // Worst case at a 60 degree corner: an exterior wall (depth t) whose neighbor across an open edge carries a
        // shallower wall ends its full-depth face at the bisector, t / tan(30) = 1.732 t along the edge (a miter
        // with an interior half wall reaches only (t + t/2 * cos 60) / sin 60 = 1.443 t).
        public float ThickWallCornerInsetPerThickness => 1.7320508075688772f;

        public int GetEdgeCount(CellCoord cell)
        {
            return EdgeCount;
        }

        public CellCoord GetNeighbor(CellCoord cell, int edge)
        {
            if (cell.variant == Up)
            {
                switch (edge)
                {
                    case 0: return new CellCoord(cell.x - 1, cell.y, Down);
                    case 1: return new CellCoord(cell.x, cell.y - 1, Down);
                    default: return new CellCoord(cell.x, cell.y, Down);
                }
            }

            switch (edge)
            {
                case 0: return new CellCoord(cell.x, cell.y, Up);
                case 1: return new CellCoord(cell.x + 1, cell.y, Up);
                default: return new CellCoord(cell.x, cell.y + 1, Up);
            }
        }

        public int GetNeighborEdge(CellCoord cell, int edge)
        {
            // Up left/bottom/diagonal meet down right/top/diagonal.
            return cell.variant == Up ? (edge + 1) % EdgeCount : (edge + 2) % EdgeCount;
        }

        public Vector3 GetCellCenter(CellCoord cell, float cellSize)
        {
            float[,] corners = cell.variant == Up ? UpCorners : DownCorners;
            float cx = (corners[0, 0] + corners[1, 0] + corners[2, 0]) / 3f;
            float cz = (corners[0, 1] + corners[1, 1] + corners[2, 1]) / 3f;
            return RhombusOrigin(cell, cellSize) + new Vector3(cx * cellSize, 0f, cz * cellSize);
        }

        public Vector3 GetCornerOffset(CellCoord cell, int corner, float cellSize)
        {
            float[,] corners = cell.variant == Up ? UpCorners : DownCorners;
            Vector3 cornerPosition = RhombusOrigin(cell, cellSize) + new Vector3(corners[corner, 0] * cellSize, 0f, corners[corner, 1] * cellSize);
            return cornerPosition - GetCellCenter(cell, cellSize);
        }

        public Vector3 GetEdgeNormal(CellCoord cell, int edge)
        {
            float[,] normals = cell.variant == Up ? UpNormals : DownNormals;
            return new Vector3(normals[edge, 0], 0f, normals[edge, 1]);
        }

        public CellCoord GetCellAt(Vector3 position, float cellSize)
        {
            float latticeY = position.z * InverseHalfSqrt3 / cellSize;
            float latticeX = position.x / cellSize - latticeY * 0.5f;

            int x = Mathf.FloorToInt(latticeX);
            int y = Mathf.FloorToInt(latticeY);
            float u = latticeX - x;
            float v = latticeY - y;

            return new CellCoord(x, y, u + v < 1f ? Up : Down);
        }

        // Each cell lies between three families of parallel grid lines; crossing an edge moves across exactly
        // one line, so the step count is the sum of the index differences.
        public int GetDistance(CellCoord a, CellCoord b)
        {
            int da = a.x - b.x;
            int db = a.y - b.y;
            int dc = (a.x + a.y + a.variant) - (b.x + b.y + b.variant);
            return Mathf.Abs(da) + Mathf.Abs(db) + Mathf.Abs(dc);
        }

        private static Vector3 RhombusOrigin(CellCoord cell, float cellSize)
        {
            return new Vector3((cell.x + cell.y * 0.5f) * cellSize, 0f, cell.y * HalfSqrt3 * cellSize);
        }
    }
}
