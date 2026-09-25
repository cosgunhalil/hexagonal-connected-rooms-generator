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

        // wallDepths[i] is how far the wall on edge i reaches into this cell (0 = no wall on that edge).
        //
        // At a corner where the neighboring edge of the same cell also has a wall, the two inner lines meet at
        // their intersection. Where it is open, the strip is cut on the corner's bisector (the line towards the
        // center of a regular cell) at depth / sin(a/2), and a chamfer runs to the point depth / tan(a/2) along
        // the open edge, a being the cell's interior angle at that corner.
        public static ThickWallStrip GetThickWallStrip(
            IGridTopology topology,
            CellCoord coordinate,
            int edgeIndex,
            float cellSize,
            float[] wallDepths,
            float floorHeight = 0f)
        {
            Vector3 center = topology.GetCellCenter(coordinate, cellSize);
            center.y = floorHeight;

            int edgeCount = topology.GetEdgeCount(coordinate);
            int nextEdge = (edgeIndex + 1) % edgeCount;
            int previousEdge = (edgeIndex + edgeCount - 1) % edgeCount;
            float depth = wallDepths[edgeIndex];

            (Vector3 edgeStart, Vector3 edgeEnd) = GetEdgeWorldVertices(topology, coordinate, edgeIndex, cellSize, floorHeight);

            ThickWallStrip strip = new ThickWallStrip
            {
                CellCenter = center,
                EdgeStart = edgeStart,
                EdgeEnd = edgeEnd,
                Inward = -topology.GetEdgeNormal(coordinate, edgeIndex),
                Depth = depth
            };

            // End corner, shared with the next edge of this cell.
            if (wallDepths[nextEdge] > 0f)
            {
                strip.InnerEnd = IntersectInsetLines(topology, coordinate, edgeStart, edgeIndex, depth, edgeEnd, nextEdge, wallDepths[nextEdge]);
            }
            else
            {
                (_, Vector3 nextEdgeEnd) = GetEdgeWorldVertices(topology, coordinate, nextEdge, cellSize, floorHeight);
                float halfAngle = HalfInteriorAngle(edgeStart, edgeEnd, nextEdgeEnd);
                strip.InnerEnd = edgeEnd + (center - edgeEnd).normalized * (depth / Mathf.Sin(halfAngle));
                strip.HasEndChamfer = true;
                strip.EndChamferPoint = edgeEnd + (nextEdgeEnd - edgeEnd).normalized * (depth / Mathf.Tan(halfAngle));
            }

            // Start corner, shared with the previous edge of this cell.
            if (wallDepths[previousEdge] > 0f)
            {
                strip.InnerStart = IntersectInsetLines(topology, coordinate, edgeStart, edgeIndex, depth, edgeStart, previousEdge, wallDepths[previousEdge]);
            }
            else
            {
                (Vector3 previousEdgeStart, _) = GetEdgeWorldVertices(topology, coordinate, previousEdge, cellSize, floorHeight);
                float halfAngle = HalfInteriorAngle(edgeEnd, edgeStart, previousEdgeStart);
                strip.InnerStart = edgeStart + (center - edgeStart).normalized * (depth / Mathf.Sin(halfAngle));
                strip.HasStartChamfer = true;
                strip.StartChamferPoint = edgeStart + (previousEdgeStart - edgeStart).normalized * (depth / Mathf.Tan(halfAngle));
            }

            return strip;
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
