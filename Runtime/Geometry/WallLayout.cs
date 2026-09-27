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
        private readonly System.Func<CellCoord, int, bool> isExterior;

        // isExterior overrides which walls face empty space; by default a wall is exterior unless a room cell of
        // the same grid lies behind it. Mixed levels pass their own rule, since other rooms live on other grids.
        public WallLayout(CellGrid grid, float wallThickness, System.Func<CellCoord, int, bool> isExterior = null)
        {
            this.grid = grid;
            this.wallThickness = wallThickness;
            this.isExterior = isExterior;
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
            if (isExterior != null)
                return isExterior(cell, edge);

            GridCell neighbor = grid.GetNeighbor(cell, edge);
            return neighbor == null || !neighbor.IsPartOfRoom();
        }

        // The two walls around the corner may differ in depth (at a triangle corner the outside can be both empty
        // space and another room). The depth is blended by how many cells lie between the edge and each wall, so a
        // wall keeps its full depth inside its own cell and the change happens in corner-fill cells in between.
        // When both walls are in the two cells sharing the edge, the smaller depth keeps both caps convex.
        public float GetWrapDepth(CellCoord cell, int edge, bool atEndCorner)
        {
            if (GetDepth(cell, edge) > 0f)
                return 0f;

            (float acrossDepth, int acrossSteps) = WalkAround(cell, edge, atEndCorner);
            if (acrossDepth <= 0f)
                return 0f;

            (int otherEdge, bool otherAtEnd) = OtherEdgeAtCorner(cell, edge, atEndCorner);
            float ownDepth = GetDepth(cell, otherEdge);
            float thisDepth;
            int thisSteps;

            if (ownDepth > 0f)
            {
                thisDepth = ownDepth;
                thisSteps = 0;
            }
            else
            {
                (thisDepth, thisSteps) = WalkAround(cell, otherEdge, otherAtEnd);
                if (thisDepth <= 0f)
                    return 0f;
                thisSteps += 1;
            }

            if (acrossSteps + thisSteps == 0)
                return System.Math.Min(acrossDepth, thisDepth);

            return (acrossDepth * thisSteps + thisDepth * acrossSteps) / (acrossSteps + thisSteps);
        }

        public float GetAcrossCornerHalfAngle(CellCoord cell, int openEdge, bool atEndCorner, float cellSize)
        {
            IGridTopology topology = grid.Topology;
            CellCoord neighbor = topology.GetNeighbor(cell, openEdge);
            int neighborEdge = topology.GetNeighborEdge(cell, openEdge);
            int neighborEdgeCount = topology.GetEdgeCount(neighbor);
            int neighborCorner = atEndCorner ? (neighborEdge + neighborEdgeCount - 1) % neighborEdgeCount : neighborEdge;
            return CellGeometry.CornerHalfAngle(topology, neighbor, neighborCorner, cellSize);
        }

        // Crosses the open edge and keeps turning around the corner until a cell has a wall there. Returns that
        // wall's depth and the number of wall-free cells passed on the way (0 when the first neighbor has it).
        private (float, int) WalkAround(CellCoord cell, int edge, bool atEndCorner)
        {
            IGridTopology topology = grid.Topology;

            for (int step = 0; step < MaxStepsAroundCorner; step++)
            {
                GridCell current = grid.GetCell(cell);
                if (current == null || current.GetEdgeFlag(edge) != WallFlag.NoWall)
                    return (0f, 0);

                CellCoord neighbor = topology.GetNeighbor(cell, edge);
                int neighborEdge = topology.GetNeighborEdge(cell, edge);

                // The shared edge runs the other way in the neighbor, so the corner switches ends.
                (int nextEdge, bool nextAtEnd) = OtherEdgeAtCorner(neighbor, neighborEdge, !atEndCorner);

                float depth = GetDepth(neighbor, nextEdge);
                if (depth > 0f)
                    return (depth, step);

                cell = neighbor;
                edge = nextEdge;
                atEndCorner = nextAtEnd;
            }

            return (0f, 0);
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
