using UnityEngine;

namespace CRG.Core
{
    // Truncated square tiling (4.8.8): regular octagons with flat sides facing E, N, W and S on a square
    // lattice with spacing s = size * (1 + sqrt(2)), and squares rotated by 45 degrees in the gaps. All edges
    // have length size, with +Z as north.
    // - variant 0, octagon (x, y): centered at (x, y) * s. Edge i faces 45 * i degrees (0 = E, 1 = NE, ... 7 = SE);
    //   even edges meet octagons, odd (diagonal) edges meet squares. Corner i sits at 22.5 + 45 * i degrees.
    // - variant 1, square (x, y): centered at (x + 0.5, y + 0.5) * s, between octagons (x, y), (x + 1, y),
    //   (x, y + 1) and (x + 1, y + 1). Edge j faces 45 + 90 * j degrees (0 = NE, 1 = NW, 2 = SW, 3 = SE) and
    //   corner j sits at 90 + 90 * j degrees.
    public class OctagonSquareTopology : IGridTopology
    {
        public const int Octagon = 0;
        public const int Square = 1;

        private const float Spacing = 2.414213562373095f;              // 1 + sqrt(2)
        private const float OctagonCircumradius = 1.3065629648763766f;  // 1 / (2 sin 22.5)
        private const float OctagonApothem = 1.2071067811865475f;       // (1 + sqrt(2)) / 2
        private const float SquareCircumradius = 0.7071067811865476f;   // 1 / sqrt(2)
        private const float Diagonal = 0.7071067811865476f;

        private static readonly int[] OctagonNeighborX = { 1, 0, 0, -1, -1, -1, 0, 0 };
        private static readonly int[] OctagonNeighborY = { 0, 0, 1, 0, 0, -1, -1, -1 };
        private static readonly int[] SquareNeighborX = { 1, 0, 0, 1 };
        private static readonly int[] SquareNeighborY = { 1, 1, 0, 0 };

        private static readonly float[,] OctagonNormals =
        {
            { 1f, 0f }, { Diagonal, Diagonal }, { 0f, 1f }, { -Diagonal, Diagonal },
            { -1f, 0f }, { -Diagonal, -Diagonal }, { 0f, -1f }, { Diagonal, -Diagonal }
        };

        private static readonly float[,] SquareNormals =
        {
            { Diagonal, Diagonal }, { -Diagonal, Diagonal }, { -Diagonal, -Diagonal }, { Diagonal, -Diagonal }
        };

        public GridType Type => GridType.OctagonSquare;

        // Worst case is at the 90 degree square corners: an interior half wall meeting an exterior wall, or an
        // exterior wall chamfered towards a shallower one, both end t along the edge (135 degree octagon
        // corners reach at most 0.914 t).
        public float ThickWallCornerInsetPerThickness => 1f;

        public int GetEdgeCount(CellCoord cell)
        {
            return cell.variant == Octagon ? 8 : 4;
        }

        public CellCoord GetNeighbor(CellCoord cell, int edge)
        {
            if (cell.variant == Octagon)
            {
                int variant = edge % 2 == 0 ? Octagon : Square;
                return new CellCoord(cell.x + OctagonNeighborX[edge], cell.y + OctagonNeighborY[edge], variant);
            }

            return new CellCoord(cell.x + SquareNeighborX[edge], cell.y + SquareNeighborY[edge], Octagon);
        }

        public int GetNeighborEdge(CellCoord cell, int edge)
        {
            if (cell.variant == Square)
                return (2 * edge + 5) % 8;

            // Octagon to octagon faces the opposite side; octagon NE/NW/SW/SE meets square SW/SE/NE/NW.
            return edge % 2 == 0 ? (edge + 4) % 8 : ((edge - 1) / 2 + 2) % 4;
        }

        public Vector3 GetCellCenter(CellCoord cell, float cellSize)
        {
            float offset = cell.variant == Octagon ? 0f : 0.5f;
            return new Vector3((cell.x + offset) * Spacing * cellSize, 0f, (cell.y + offset) * Spacing * cellSize);
        }

        public Vector3 GetCornerOffset(CellCoord cell, int corner, float cellSize)
        {
            float angle;
            float radius;

            if (cell.variant == Octagon)
            {
                angle = (22.5f + 45f * corner) * Mathf.Deg2Rad;
                radius = OctagonCircumradius * cellSize;
            }
            else
            {
                angle = (90f + 90f * corner) * Mathf.Deg2Rad;
                radius = SquareCircumradius * cellSize;
            }

            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        public Vector3 GetEdgeNormal(CellCoord cell, int edge)
        {
            float[,] normals = cell.variant == Octagon ? OctagonNormals : SquareNormals;
            return new Vector3(normals[edge, 0], 0f, normals[edge, 1]);
        }

        // Each lattice point's octagon lies inside its square Voronoi region; whatever is left of that region
        // belongs to the diamond-shaped squares at the region's corners.
        public CellCoord GetCellAt(Vector3 position, float cellSize)
        {
            float spacing = Spacing * cellSize;
            int x = Mathf.RoundToInt(position.x / spacing);
            int y = Mathf.RoundToInt(position.z / spacing);

            float dx = Mathf.Abs(position.x - x * spacing);
            float dz = Mathf.Abs(position.z - y * spacing);
            float apothem = OctagonApothem * cellSize;

            if (dx <= apothem && dz <= apothem && (dx + dz) * Diagonal <= apothem)
                return new CellCoord(x, y, Octagon);

            return new CellCoord(Mathf.FloorToInt(position.x / spacing), Mathf.FloorToInt(position.z / spacing), Square);
        }

        // Octagons are Manhattan-distant (a diagonal move costs two steps through a square); squares reach the
        // rest of the grid through one of their four octagons.
        public int GetDistance(CellCoord a, CellCoord b)
        {
            if (a.variant == Square && b.variant == Square)
            {
                int best = int.MaxValue;
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        int distance = OctagonDistance(a.x + SquareNeighborX[i], a.y + SquareNeighborY[i], b.x + SquareNeighborX[j], b.y + SquareNeighborY[j]);
                        best = Mathf.Min(best, distance);
                    }
                }
                return a == b ? 0 : best + 2;
            }

            if (a.variant == Square || b.variant == Square)
            {
                CellCoord square = a.variant == Square ? a : b;
                CellCoord octagon = a.variant == Square ? b : a;
                int best = int.MaxValue;
                for (int i = 0; i < 4; i++)
                    best = Mathf.Min(best, OctagonDistance(square.x + SquareNeighborX[i], square.y + SquareNeighborY[i], octagon.x, octagon.y));
                return best + 1;
            }

            return OctagonDistance(a.x, a.y, b.x, b.y);
        }

        private static int OctagonDistance(int ax, int ay, int bx, int by)
        {
            return Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);
        }
    }
}
