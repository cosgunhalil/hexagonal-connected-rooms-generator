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

    public static class HexGeometry
    {
        private const float MinSegmentSize = 0.001f;

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

        private static (Vector3, Vector3) GetEdgeWorldVertices(AxialCoord coordinate, int edgeIndex, float hexSize, float floorHeight)
        {
            Vector3 center = coordinate.ToWorldPosition(hexSize);
            center.y = floorHeight;

            (Vector3 start, Vector3 end) = HexMath.GetEdgeVertices(edgeIndex, hexSize);
            return (center + start, center + end);
        }
    }
}
