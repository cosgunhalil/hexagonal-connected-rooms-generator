using System.Collections.Generic;
using UnityEngine;
using CRG.Core;

namespace CRG.Geometry
{
    // A vertical rectangle standing on the segment Start -> End, spanning Bottom..Top in Y.
    public struct WallSegment
    {
        public Vector3 Start;
        public Vector3 End;
        public float Bottom;
        public float Top;

        public WallSegment(Vector3 start, Vector3 end, float bottom, float top)
        {
            Start = start;
            End = end;
            Bottom = bottom;
            Top = top;
        }
    }

    // Footprint (at floor level) of the part of a thick wall that lies on one cell's side of an edge.
    // The strip runs from the edge line (EdgeStart -> EdgeEnd) to the inner line (InnerStart -> InnerEnd),
    // Depth units into the cell. Where the neighboring edge of the same cell is open, the strip ends in a
    // chamfer towards the point on that open edge, so strips of two cells of one room meet without a gap.
    public struct ThickWallStrip
    {
        public Vector3 CellCenter;
        public Vector3 EdgeStart;
        public Vector3 EdgeEnd;
        public Vector3 InnerStart;
        public Vector3 InnerEnd;
        public Vector3 Inward;
        public float Depth;

        public bool HasStartChamfer;
        public Vector3 StartChamferPoint;
        public bool HasEndChamfer;
        public Vector3 EndChamferPoint;
    }

    // Wall layout around cells, provided by the level builder for thick walls.
    public interface IWallLayout
    {
        // How far the wall on this cell's side of the edge reaches into the cell (0 = no wall on that edge).
        float GetDepth(CellCoord cell, int edge);

        // For an open edge (no wall), the depth of the wall that wraps around one of its corners through cells
        // of the same room, or 0 when no wall touches that corner. atEndCorner selects the edge's end corner.
        float GetWrapDepth(CellCoord cell, int edge, bool atEndCorner);
    }

    // Triangle a cell adds at a corner where a wall wraps around it without the cell having a wall there.
    public struct CornerFill
    {
        public Vector3 CellCenter;
        public Vector3 Corner;
        public Vector3 PointOnEndingEdge;
        public Vector3 PointOnStartingEdge;
    }

    // Floor, wall and thick-wall footprints for any grid topology, in the level's local space.
    public static class CellGeometry
    {
        private const float MinSegmentSize = 0.001f;

        public static float CalculateDoorWidth(float edgeLength, float doorWidthRatio = 0.2f)
        {
            return edgeLength * doorWidthRatio;
        }

        // Center followed by the cell's corners, all at floorHeight.
        public static Vector3[] GetFloorVertices(IGridTopology topology, CellCoord coordinate, float cellSize, float floorHeight = 0f)
        {
            Vector3 center = topology.GetCellCenter(coordinate, cellSize);
            center.y = floorHeight;

            int cornerCount = topology.GetEdgeCount(coordinate);
            Vector3[] vertices = new Vector3[cornerCount + 1];
            vertices[0] = center;

            for (int i = 0; i < cornerCount; i++)
            {
                vertices[i + 1] = center + topology.GetCornerOffset(coordinate, i, cellSize);
            }

            return vertices;
        }

        public static WallSegment GetWallSegment(
            IGridTopology topology,
            CellCoord coordinate,
            int edgeIndex,
            float cellSize,
            float wallHeight,
            float floorHeight = 0f)
        {
            (Vector3 start, Vector3 end) = GetEdgeWorldVertices(topology, coordinate, edgeIndex, cellSize, floorHeight);
            return new WallSegment(start, end, floorHeight, floorHeight + wallHeight);
        }

        // A wall with a centered opening: left piece, right piece and a lintel above the door.
        public static List<WallSegment> GetDoorWallSegments(
            IGridTopology topology,
            CellCoord coordinate,
            int edgeIndex,
            float cellSize,
            float wallHeight,
            float doorWidth,
            float doorHeight,
            float floorHeight = 0f)
        {
            (Vector3 start, Vector3 end) = GetEdgeWorldVertices(topology, coordinate, edgeIndex, cellSize, floorHeight);

            float edgeLength = Vector3.Distance(start, end);
            doorWidth = Mathf.Clamp(doorWidth, 0f, edgeLength);
            doorHeight = Mathf.Clamp(doorHeight, 0f, wallHeight);

            Vector3 direction = (end - start) / edgeLength;
            float sideLength = (edgeLength - doorWidth) / 2f;
            Vector3 doorStart = start + direction * sideLength;
            Vector3 doorEnd = doorStart + direction * doorWidth;

            float wallTop = floorHeight + wallHeight;
            float doorTop = floorHeight + doorHeight;

            List<WallSegment> segments = new List<WallSegment>(3);

            if (sideLength > MinSegmentSize)
            {
                segments.Add(new WallSegment(start, doorStart, floorHeight, wallTop));
                segments.Add(new WallSegment(doorEnd, end, floorHeight, wallTop));
            }

            if (wallTop - doorTop > MinSegmentSize)
            {
                segments.Add(new WallSegment(doorStart, doorEnd, doorTop, wallTop));
            }

            return segments;
        }

        // At a corner where the neighboring edge of the same cell also has a wall, the two inner lines meet at
        // their intersection. Where it is open, the strip is cut on the corner's bisector (the line towards the
        // center of a regular cell) at depth / sin(a/2), a being the cell's interior angle at that corner, and a
        // chamfer runs to the wrap point on the open edge (see GetWrapPoint).
        public static ThickWallStrip GetThickWallStrip(
            IGridTopology topology,
            CellCoord coordinate,
            int edgeIndex,
            float cellSize,
            IWallLayout layout,
            float floorHeight = 0f)
        {
            Vector3 center = topology.GetCellCenter(coordinate, cellSize);
            center.y = floorHeight;

            int edgeCount = topology.GetEdgeCount(coordinate);
            int nextEdge = (edgeIndex + 1) % edgeCount;
            int previousEdge = (edgeIndex + edgeCount - 1) % edgeCount;
            float depth = layout.GetDepth(coordinate, edgeIndex);
            float nextDepth = layout.GetDepth(coordinate, nextEdge);
            float previousDepth = layout.GetDepth(coordinate, previousEdge);

            (Vector3 edgeStart, Vector3 edgeEnd) = GetEdgeWorldVertices(topology, coordinate, edgeIndex, cellSize, floorHeight);

            ThickWallStrip strip = new ThickWallStrip
            {
                CellCenter = center,
                EdgeStart = edgeStart,
                EdgeEnd = edgeEnd,
                Inward = -topology.GetEdgeNormal(coordinate, edgeIndex),
                Depth = depth
            };

            // End corner (corner edgeIndex), shared with the next edge of this cell.
            if (nextDepth > 0f)
            {
                strip.InnerEnd = IntersectInsetLines(topology, coordinate, edgeStart, edgeIndex, depth, edgeEnd, nextEdge, nextDepth);
            }
            else
            {
                float halfAngle = CornerHalfAngle(topology, coordinate, edgeIndex, cellSize);
                strip.InnerEnd = edgeEnd + (center - edgeEnd).normalized * (depth / Mathf.Sin(halfAngle));
                strip.HasEndChamfer = true;
                strip.EndChamferPoint = GetWrapPoint(topology, coordinate, nextEdge, false, cellSize, layout, floorHeight);
            }

            // Start corner (corner previousEdge), shared with the previous edge of this cell.
            if (previousDepth > 0f)
            {
                strip.InnerStart = IntersectInsetLines(topology, coordinate, edgeStart, edgeIndex, depth, edgeStart, previousEdge, previousDepth);
            }
            else
            {
                float halfAngle = CornerHalfAngle(topology, coordinate, previousEdge, cellSize);
                strip.InnerStart = edgeStart + (center - edgeStart).normalized * (depth / Mathf.Sin(halfAngle));
                strip.HasStartChamfer = true;
                strip.StartChamferPoint = GetWrapPoint(topology, coordinate, previousEdge, true, cellSize, layout, floorHeight);
            }

            return strip;
        }

        // A corner of a cell that has no wall of its own there, although a wall wraps around the corner through
        // cells of the same room (four cells meet at a square corner, six at a triangle corner). The cell then
        // fills the triangle between the corner and the wrap points on its two edges, closing the wall.
        public static bool TryGetCornerFill(
            IGridTopology topology,
            CellCoord coordinate,
            int corner,
            float cellSize,
            IWallLayout layout,
            out CornerFill fill,
            float floorHeight = 0f)
        {
            fill = default;

            int edgeCount = topology.GetEdgeCount(coordinate);
            int endingEdge = corner;
            int startingEdge = (corner + 1) % edgeCount;

            if (layout.GetDepth(coordinate, endingEdge) > 0f || layout.GetDepth(coordinate, startingEdge) > 0f)
                return false;

            if (layout.GetWrapDepth(coordinate, endingEdge, true) <= 0f || layout.GetWrapDepth(coordinate, startingEdge, false) <= 0f)
                return false;

            Vector3 center = topology.GetCellCenter(coordinate, cellSize);
            center.y = floorHeight;

            fill = new CornerFill
            {
                CellCenter = center,
                Corner = center + topology.GetCornerOffset(coordinate, corner, cellSize),
                PointOnEndingEdge = GetWrapPoint(topology, coordinate, endingEdge, true, cellSize, layout, floorHeight),
                PointOnStartingEdge = GetWrapPoint(topology, coordinate, startingEdge, false, cellSize, layout, floorHeight)
            };
            return true;
        }

        // Where the inner face of a wall wrapping around a corner crosses an open edge touching that corner, a
        // being the smaller interior angle of the two cells sharing the edge there:
        // - wrapDepth / tan(a/2): the chamfer from the corner's bisector (hexagons, 120 degrees);
        // - wrapDepth / sin(a): where the wall's inner line meets the open edge (triangles, 60 degrees), so the
        //   face never bulges past the inner line.
        // The nearer of the two is used; both are equal at 90 degrees (squares). Both cells compute the same
        // point, so their wall pieces meet exactly. atEndCorner selects the edge's end corner (corner edge)
        // instead of its start corner (corner edge - 1).
        public static Vector3 GetWrapPoint(
            IGridTopology topology,
            CellCoord coordinate,
            int openEdge,
            bool atEndCorner,
            float cellSize,
            IWallLayout layout,
            float floorHeight = 0f)
        {
            int edgeCount = topology.GetEdgeCount(coordinate);
            int corner = atEndCorner ? openEdge : (openEdge + edgeCount - 1) % edgeCount;
            int otherCorner = atEndCorner ? (openEdge + edgeCount - 1) % edgeCount : openEdge;

            CellCoord neighbor = topology.GetNeighbor(coordinate, openEdge);
            int neighborEdge = topology.GetNeighborEdge(coordinate, openEdge);
            int neighborEdgeCount = topology.GetEdgeCount(neighbor);
            // The shared edge runs the other way in the neighbor, so this end corner is its start corner and vice versa.
            int neighborCorner = atEndCorner ? (neighborEdge + neighborEdgeCount - 1) % neighborEdgeCount : neighborEdge;

            float halfAngle = Mathf.Min(
                CornerHalfAngle(topology, coordinate, corner, cellSize),
                CornerHalfAngle(topology, neighbor, neighborCorner, cellSize));

            Vector3 center = topology.GetCellCenter(coordinate, cellSize);
            center.y = floorHeight;
            Vector3 cornerPosition = center + topology.GetCornerOffset(coordinate, corner, cellSize);
            Vector3 otherCornerPosition = center + topology.GetCornerOffset(coordinate, otherCorner, cellSize);

            float wrapDepth = layout.GetWrapDepth(coordinate, openEdge, atEndCorner);
            float distance = wrapDepth * Mathf.Min(1f / Mathf.Tan(halfAngle), 1f / Mathf.Sin(2f * halfAngle));
            return cornerPosition + (otherCornerPosition - cornerPosition).normalized * distance;
        }

        // Half of the cell's interior angle at the given corner.
        public static float CornerHalfAngle(IGridTopology topology, CellCoord coordinate, int corner, float cellSize)
        {
            int count = topology.GetEdgeCount(coordinate);
            Vector3 previous = topology.GetCornerOffset(coordinate, (corner + count - 1) % count, cellSize);
            Vector3 current = topology.GetCornerOffset(coordinate, corner, cellSize);
            Vector3 next = topology.GetCornerOffset(coordinate, (corner + 1) % count, cellSize);
            return HalfInteriorAngle(previous, current, next);
        }

        public static (Vector3, Vector3) GetEdgeWorldVertices(IGridTopology topology, CellCoord coordinate, int edgeIndex, float cellSize, float floorHeight = 0f)
        {
            Vector3 center = topology.GetCellCenter(coordinate, cellSize);
            center.y = floorHeight;

            (Vector3 start, Vector3 end) = topology.GetEdgeOffsets(coordinate, edgeIndex, cellSize);
            return (center + start, center + end);
        }

        // Half of the interior angle at corner, between the edges towards previous and next.
        private static float HalfInteriorAngle(Vector3 previous, Vector3 corner, Vector3 next)
        {
            Vector3 toPrevious = (previous - corner).normalized;
            Vector3 toNext = (next - corner).normalized;
            float cosine = Mathf.Clamp(Vector3.Dot(toPrevious, toNext), -1f, 1f);
            return Mathf.Acos(cosine) / 2f;
        }

        // Intersection (in XZ) of edge A's line moved depthA inwards with edge B's line moved depthB inwards.
        private static Vector3 IntersectInsetLines(IGridTopology topology, CellCoord coordinate,
            Vector3 pointOnA, int edgeA, float depthA, Vector3 pointOnB, int edgeB, float depthB)
        {
            Vector3 normalA = topology.GetEdgeNormal(coordinate, edgeA);
            Vector3 normalB = topology.GetEdgeNormal(coordinate, edgeB);

            // Points p on the inset line satisfy dot(n, p) = dot(n, pointOnEdge) - depth.
            float constantA = normalA.x * pointOnA.x + normalA.z * pointOnA.z - depthA;
            float constantB = normalB.x * pointOnB.x + normalB.z * pointOnB.z - depthB;

            float determinant = normalA.x * normalB.z - normalA.z * normalB.x;
            float x = (constantA * normalB.z - normalA.z * constantB) / determinant;
            float z = (normalA.x * constantB - constantA * normalB.x) / determinant;

            return new Vector3(x, pointOnA.y, z);
        }
    }
}
