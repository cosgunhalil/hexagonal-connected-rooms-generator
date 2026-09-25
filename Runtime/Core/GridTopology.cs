using System;
using System.Collections.Generic;
using UnityEngine;

namespace CRG.Core
{
    public enum GridType
    {
        Hexagon
    }

    // Shape and connectivity of a cell grid. All sizes are in world units; cellSize is the edge length,
    // which is the same for every edge of a regular tiling.
    //
    // Conventions every topology must follow (geometry and thick walls rely on them):
    // - Corners are listed counter-clockwise seen from above (increasing angle from +X towards +Z).
    // - Edge i runs from corner (i - 1) to corner i and faces the neighbor returned by GetNeighbor(cell, i).
    // - GetNeighborEdge(cell, i) is the index of that same edge as seen from the neighbor.
    public interface IGridTopology
    {
        GridType Type { get; }

        int GetEdgeCount(CellCoord cell);

        CellCoord GetNeighbor(CellCoord cell, int edge);

        int GetNeighborEdge(CellCoord cell, int edge);

        Vector3 GetCellCenter(CellCoord cell, float cellSize);

        // Corner position relative to the cell center.
        Vector3 GetCornerOffset(CellCoord cell, int corner, float cellSize);

        // Unit vector in the XZ plane pointing out of the cell through edge i.
        Vector3 GetEdgeNormal(CellCoord cell, int edge);

        CellCoord GetCellAt(Vector3 position, float cellSize);

        // Number of steps between two cells, used to bias layouts towards or away from the start.
        int GetDistance(CellCoord a, CellCoord b);

        // Worst-case distance along an edge between a cell corner and the end of a thick wall's inner face,
        // per unit of wall thickness. Door openings must stay clear of it.
        float ThickWallCornerInsetPerThickness { get; }
    }

    public static class GridTopology
    {
        private static readonly HexTopology Hexagon = new HexTopology();

        public static IGridTopology Get(GridType type)
        {
            switch (type)
            {
                case GridType.Hexagon:
                    return Hexagon;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown grid type");
            }
        }

        public static IEnumerable<CellCoord> GetNeighbors(this IGridTopology topology, CellCoord cell)
        {
            int edgeCount = topology.GetEdgeCount(cell);
            for (int edge = 0; edge < edgeCount; edge++)
            {
                yield return topology.GetNeighbor(cell, edge);
            }
        }

        // Edge endpoints relative to the cell center, in corner order (corner i - 1, then corner i).
        public static (Vector3, Vector3) GetEdgeOffsets(this IGridTopology topology, CellCoord cell, int edge, float cellSize)
        {
            int edgeCount = topology.GetEdgeCount(cell);
            Vector3 start = topology.GetCornerOffset(cell, (edge + edgeCount - 1) % edgeCount, cellSize);
            Vector3 end = topology.GetCornerOffset(cell, edge, cellSize);
            return (start, end);
        }

        public static Vector3 GetEdgeCenterOffset(this IGridTopology topology, CellCoord cell, int edge, float cellSize)
        {
            (Vector3 start, Vector3 end) = topology.GetEdgeOffsets(cell, edge, cellSize);
            return (start + end) / 2f;
        }

        // Distance from the cell center to the nearest edge.
        public static float GetInnerRadius(this IGridTopology topology, CellCoord cell, float cellSize)
        {
            float radius = float.MaxValue;
            for (int edge = 0; edge < topology.GetEdgeCount(cell); edge++)
            {
                radius = Mathf.Min(radius, topology.GetEdgeCenterOffset(cell, edge, cellSize).magnitude);
            }
            return radius;
        }

        // Edge index through which neighbor is reached from cell, or -1 when they are not adjacent.
        public static int GetSharedEdge(this IGridTopology topology, CellCoord cell, CellCoord neighbor)
        {
            for (int edge = 0; edge < topology.GetEdgeCount(cell); edge++)
            {
                if (topology.GetNeighbor(cell, edge) == neighbor)
                    return edge;
            }
            return -1;
        }
    }
}
