using CRG.Core;

namespace CRG.Geometry
{
    // Thick-wall depths of a generated grid. A room cell's side of a wall is half the thickness towards another
    // room and the full thickness towards empty space. Wrap depths are found by walking around a corner through
    // cells of the same room (across open edges) until a wall is reached in each direction.
    public class WallLayout : IWallLayout
    {
        // More than any cell count around one corner in the supported tilings; stops walks around wall-free corners.
        private const int MaxStepsAroundCorner = 16;

        private readonly CellGrid grid;
        private readonly float wallThickness;

        public WallLayout(CellGrid grid, float wallThickness)
        {
            this.grid = grid;
            this.wallThickness = wallThickness;
        }

        public float GetDepth(CellCoord cell, int edge)
        {
            GridCell gridCell = grid.GetCell(cell);
            if (gridCell == null || !gridCell.IsPartOfRoom())
                return 0f;

            WallFlag flag = gridCell.GetEdgeFlag(edge);
            if (!flag.HasFlag(WallFlag.Wall) && !flag.HasFlag(WallFlag.HasDoor))
                return 0f;

            return IsExterior(cell, edge) ? wallThickness : wallThickness / 2f;
        }

        public bool IsExterior(CellCoord cell, int edge)
        {
            GridCell neighbor = grid.GetNeighbor(cell, edge);
            return neighbor == null || !neighbor.IsPartOfRoom();
        }

        public float GetWrapDepth(CellCoord cell, int edge, bool atEndCorner)
        {
            if (GetDepth(cell, edge) > 0f)
                return 0f;

            float acrossEdge = WalkAround(cell, edge, atEndCorner);

            (int otherEdge, bool otherAtEnd) = OtherEdgeAtCorner(cell, edge, atEndCorner);
            float otherDepth = GetDepth(cell, otherEdge);
            float thisSide = otherDepth > 0f ? otherDepth : WalkAround(cell, otherEdge, otherAtEnd);

            return acrossEdge > 0f && thisSide > 0f ? System.Math.Min(acrossEdge, thisSide) : 0f;
        }

        // Crosses the open edge and keeps turning around the corner until a cell has a wall there.
        private float WalkAround(CellCoord cell, int edge, bool atEndCorner)
        {
            IGridTopology topology = grid.Topology;

            for (int step = 0; step < MaxStepsAroundCorner; step++)
            {
                GridCell current = grid.GetCell(cell);
                if (current == null || current.GetEdgeFlag(edge) != WallFlag.NoWall)
                    return 0f;

                CellCoord neighbor = topology.GetNeighbor(cell, edge);
                int neighborEdge = topology.GetNeighborEdge(cell, edge);

                // The shared edge runs the other way in the neighbor, so the corner switches ends.
                (int nextEdge, bool nextAtEnd) = OtherEdgeAtCorner(neighbor, neighborEdge, !atEndCorner);

                float depth = GetDepth(neighbor, nextEdge);
                if (depth > 0f)
                    return depth;

                cell = neighbor;
                edge = nextEdge;
                atEndCorner = nextAtEnd;
            }

            return 0f;
        }

        // The cell's other edge touching the same corner: the corner at the end of edge i starts edge i + 1.
        private (int, bool) OtherEdgeAtCorner(CellCoord cell, int edge, bool atEndCorner)
        {
            int edgeCount = grid.Topology.GetEdgeCount(cell);
            return atEndCorner
                ? ((edge + 1) % edgeCount, false)
                : ((edge + edgeCount - 1) % edgeCount, true);
        }
    }
}
