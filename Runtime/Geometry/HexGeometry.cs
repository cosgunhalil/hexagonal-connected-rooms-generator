using System.Collections.Generic;
using UnityEngine;
using HRCG.Core;

namespace HRCG.Geometry
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

    public static class HexGeometry
    {
        private const float MinSegmentSize = 0.001f;
        private const float SquareRootOfThree = 1.7320508075688772f;

        // Center followed by the 6 corners, all at floorHeight.
        public static Vector3[] GetFloorVertices(AxialCoord coordinate, float hexSize, float floorHeight = 0f)
        {
            Vector3 center = coordinate.ToWorldPosition(hexSize);
            center.y = floorHeight;

            Vector3[] vertices = new Vector3[7];
            vertices[0] = center;

            Vector3[] corners = HexMath.GetHexVertices(hexSize);
            for (int i = 0; i < 6; i++)
            {
                vertices[i + 1] = center + corners[i];
            }

            return vertices;
        }

        public static WallSegment GetWallSegment(
            AxialCoord coordinate,
            int edgeIndex,
            float hexSize,
            float wallHeight,
            float floorHeight = 0f)
        {
            (Vector3 start, Vector3 end) = GetEdgeWorldVertices(coordinate, edgeIndex, hexSize, floorHeight);
            return new WallSegment(start, end, floorHeight, floorHeight + wallHeight);
        }

        // A wall with a centered opening: left piece, right piece and a lintel above the door.
        public static List<WallSegment> GetDoorWallSegments(
            AxialCoord coordinate,
            int edgeIndex,
            float hexSize,
            float wallHeight,
            float doorWidth,
            float doorHeight,
            float floorHeight = 0f)
        {
            (Vector3 start, Vector3 end) = GetEdgeWorldVertices(coordinate, edgeIndex, hexSize, floorHeight);

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
        public static ThickWallStrip GetThickWallStrip(
            AxialCoord coordinate,
            int edgeIndex,
            float hexSize,
            float[] wallDepths,
            float floorHeight = 0f)
        {
            Vector3 center = coordinate.ToWorldPosition(hexSize);
            center.y = floorHeight;

            int nextEdge = (edgeIndex + 1) % 6;
            int previousEdge = (edgeIndex + 5) % 6;
            float depth = wallDepths[edgeIndex];

            (Vector3 edgeStart, Vector3 edgeEnd) = GetEdgeWorldVertices(coordinate, edgeIndex, hexSize, floorHeight);

            ThickWallStrip strip = new ThickWallStrip
            {
                CellCenter = center,
                EdgeStart = edgeStart,
                EdgeEnd = edgeEnd,
                Inward = -HexMath.GetEdgeNormal(edgeIndex),
                Depth = depth
            };

            // End corner, shared with the next edge of this cell.
            if (wallDepths[nextEdge] > 0f)
            {
                strip.InnerEnd = IntersectInsetLines(edgeStart, edgeIndex, depth, edgeEnd, nextEdge, wallDepths[nextEdge]);
            }
            else
            {
                (_, Vector3 nextEdgeEnd) = GetEdgeWorldVertices(coordinate, nextEdge, hexSize, floorHeight);
                strip.InnerEnd = edgeEnd + (center - edgeEnd).normalized * (2f * depth / SquareRootOfThree);
                strip.HasEndChamfer = true;
                strip.EndChamferPoint = edgeEnd + (nextEdgeEnd - edgeEnd).normalized * (depth / SquareRootOfThree);
            }

            // Start corner, shared with the previous edge of this cell.
            if (wallDepths[previousEdge] > 0f)
            {
                strip.InnerStart = IntersectInsetLines(edgeStart, edgeIndex, depth, edgeStart, previousEdge, wallDepths[previousEdge]);
            }
            else
            {
                (Vector3 previousEdgeStart, _) = GetEdgeWorldVertices(coordinate, previousEdge, hexSize, floorHeight);
                strip.InnerStart = edgeStart + (center - edgeStart).normalized * (2f * depth / SquareRootOfThree);
                strip.HasStartChamfer = true;
                strip.StartChamferPoint = edgeStart + (previousEdgeStart - edgeStart).normalized * (depth / SquareRootOfThree);
            }

            return strip;
        }

        // Intersection (in XZ) of edge A's line moved depthA inwards with edge B's line moved depthB inwards.
        private static Vector3 IntersectInsetLines(Vector3 pointOnA, int edgeA, float depthA, Vector3 pointOnB, int edgeB, float depthB)
        {
            Vector3 normalA = HexMath.GetEdgeNormal(edgeA);
            Vector3 normalB = HexMath.GetEdgeNormal(edgeB);

            // Points p on the inset line satisfy dot(n, p) = dot(n, pointOnEdge) - depth.
            float constantA = normalA.x * pointOnA.x + normalA.z * pointOnA.z - depthA;
            float constantB = normalB.x * pointOnB.x + normalB.z * pointOnB.z - depthB;

            float determinant = normalA.x * normalB.z - normalA.z * normalB.x;
            float x = (constantA * normalB.z - normalA.z * constantB) / determinant;
            float z = (normalA.x * constantB - constantA * normalB.x) / determinant;

            return new Vector3(x, pointOnA.y, z);
        }

        private static (Vector3, Vector3) GetEdgeWorldVertices(AxialCoord coordinate, int edgeIndex, float hexSize, float floorHeight)
        {
            Vector3 center = coordinate.ToWorldPosition(hexSize);
            center.y = floorHeight;

            (Vector3 start, Vector3 end) = HexMath.GetEdgeVertices(edgeIndex, hexSize);
            return (center + start, center + end);
        }
    }
}
