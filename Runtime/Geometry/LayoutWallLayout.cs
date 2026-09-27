using System.Collections.Generic;
using CRG.Building;
using CRG.Core;

namespace CRG.Geometry
{
    // Thick-wall depths of a hand-built level, where every cell lives on its own grid. It follows the same rules as
    // WallLayout, but crosses open edges through the layout's links, so a wall can wrap around a corner shared by
    // cells of different shapes. A cell's side of a wall is half the thickness towards another cell (door or wall)
    // and the full thickness on an outer edge.
    public class LayoutWallLayout
    {
        // More than any cell count around one corner (six triangles is the most); stops walks around wall-free corners.
        private const int MaxStepsAroundCorner = 16;

        private readonly float wallThickness;
        private readonly Dictionary<int, CellShape> shapes = new Dictionary<int, CellShape>();
        private readonly Dictionary<(int, int), LayoutLink> linksByEdge = new Dictionary<(int, int), LayoutLink>();

        public LayoutWallLayout(LevelLayout layout, float wallThickness)
        {
            this.wallThickness = wallThickness;

            foreach (LayoutCell cell in layout.Cells)
                shapes[cell.id] = cell.shape;

            foreach (LayoutLink link in layout.Links)
            {
                linksByEdge[(link.cellA, link.edgeA)] = link;
                linksByEdge[(link.cellB, link.edgeB)] = link;
            }
        }

        // The layout seen from one cell, in the form CellGeometry works with (the cell is (0, 0) of its own grid).
        public IWallLayout ForCell(int cellID)
        {
            return new CellView(this, cellID);
        }

        public float GetDepth(int cellID, int edge)
        {
            if (!linksByEdge.TryGetValue((cellID, edge), out LayoutLink link))
                return wallThickness;

            return link.state == EdgeState.Open ? 0f : wallThickness / 2f;
        }

        public bool IsExterior(int cellID, int edge)
        {
            return !linksByEdge.ContainsKey((cellID, edge));
        }

        // Same blending as WallLayout.GetWrapDepth: by the number of wall-free cells between the edge and each wall.
        public float GetWrapDepth(int cellID, int edge, bool atEndCorner)
        {
            if (GetDepth(cellID, edge) > 0f)
                return 0f;

            (float acrossDepth, int acrossSteps) = WalkAround(cellID, edge, atEndCorner);
            if (acrossDepth <= 0f)
                return 0f;

            (int otherEdge, bool otherAtEnd) = OtherEdgeAtCorner(cellID, edge, atEndCorner);
            float ownDepth = GetDepth(cellID, otherEdge);
            float thisDepth;
            int thisSteps;

            if (ownDepth > 0f)
            {
                thisDepth = ownDepth;
                thisSteps = 0;
            }
            else
            {
                (thisDepth, thisSteps) = WalkAround(cellID, otherEdge, otherAtEnd);
                if (thisDepth <= 0f)
                    return 0f;
                thisSteps += 1;
            }

            if (acrossSteps + thisSteps == 0)
                return System.Math.Min(acrossDepth, thisDepth);

            return (acrossDepth * thisSteps + thisDepth * acrossSteps) / (acrossSteps + thisSteps);
        }

        public float GetAcrossCornerHalfAngle(int cellID, int openEdge, bool atEndCorner, float cellSize)
        {
            if (!linksByEdge.TryGetValue((cellID, openEdge), out LayoutLink link))
            {
                // Not an open edge; use the cell's own corner.
                CellShape own = shapes[cellID];
                int count = CellShapes.GetEdgeCount(own);
                int corner = atEndCorner ? openEdge : (openEdge + count - 1) % count;
                return CellGeometry.CornerHalfAngle(CellShapes.GetTopology(own), CellShapes.GetCell(own), corner, cellSize);
            }

            (int neighborID, int neighborEdge) = Across(link, cellID);
            CellShape neighbor = shapes[neighborID];
            int neighborEdgeCount = CellShapes.GetEdgeCount(neighbor);
            int neighborCorner = atEndCorner ? (neighborEdge + neighborEdgeCount - 1) % neighborEdgeCount : neighborEdge;
            return CellGeometry.CornerHalfAngle(CellShapes.GetTopology(neighbor), CellShapes.GetCell(neighbor), neighborCorner, cellSize);
        }

        // Crosses the open edge and keeps turning around the corner until a cell has a wall there. Returns that
        // wall's depth and the number of wall-free cells passed on the way (0 when the first neighbor has it).
        private (float, int) WalkAround(int cellID, int edge, bool atEndCorner)
        {
            for (int step = 0; step < MaxStepsAroundCorner; step++)
            {
                if (!linksByEdge.TryGetValue((cellID, edge), out LayoutLink link) || link.state != EdgeState.Open)
                    return (0f, 0);

                (int neighbor, int neighborEdge) = Across(link, cellID);

                // The shared edge runs the other way in the neighbor, so the corner switches ends.
                (int nextEdge, bool nextAtEnd) = OtherEdgeAtCorner(neighbor, neighborEdge, !atEndCorner);

                float depth = GetDepth(neighbor, nextEdge);
                if (depth > 0f)
                    return (depth, step);

                cellID = neighbor;
                edge = nextEdge;
                atEndCorner = nextAtEnd;
            }

            return (0f, 0);
        }

        private static (int, int) Across(LayoutLink link, int cellID)
        {
            return cellID == link.cellA ? (link.cellB, link.edgeB) : (link.cellA, link.edgeA);
        }

        // The cell's other edge touching the same corner: the corner at the end of edge i starts edge i + 1.
        private (int, bool) OtherEdgeAtCorner(int cellID, int edge, bool atEndCorner)
        {
            int edgeCount = CellShapes.GetEdgeCount(shapes[cellID]);
            return atEndCorner
                ? ((edge + 1) % edgeCount, false)
                : ((edge + edgeCount - 1) % edgeCount, true);
        }

        private class CellView : IWallLayout
        {
            private readonly LayoutWallLayout walls;
            private readonly int cellID;

            public CellView(LayoutWallLayout walls, int cellID)
            {
                this.walls = walls;
                this.cellID = cellID;
            }

            public float GetDepth(CellCoord cell, int edge) => walls.GetDepth(cellID, edge);

            public float GetWrapDepth(CellCoord cell, int edge, bool atEndCorner) => walls.GetWrapDepth(cellID, edge, atEndCorner);

            public float GetAcrossCornerHalfAngle(CellCoord cell, int openEdge, bool atEndCorner, float cellSize) =>
                walls.GetAcrossCornerHalfAngle(cellID, openEdge, atEndCorner, cellSize);
        }
    }
}
