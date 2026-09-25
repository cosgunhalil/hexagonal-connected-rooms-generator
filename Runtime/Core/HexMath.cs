using UnityEngine;

namespace CRG.Core
{
    // Pointy-top hexagons in the XZ plane. Vertex i sits at 30 + 60 * i degrees from +X.
    // Edge i runs from vertex (i + 5) % 6 to vertex i, so its outward normal points at
    // 60 * i degrees, i.e. towards the neighbor in HexDirection i.
    public static class HexMath
    {
        // Worst-case distance along an edge between a hex corner and the end of a thick wall's inner face,
        // per unit of wall thickness (an interior half wall of depth t/2 meeting an exterior wall of depth t).
        // Door openings must stay clear of it.
        public const float ThickWallCornerInsetPerThickness = 0.8660254037844386f;

        private const float SquareRootOfThreeHalf = 0.8660254037844386f;
        private const int HexagonVertexCount = 6;
        private const int HexagonEdgeCount = 6;
        private const float DegreesPerVertex = 60f;
        private const float VertexAngleOffset = 30f;

        public static Vector3[] GetHexVertices(float hexSize)
        {
            Vector3[] vertices = new Vector3[HexagonVertexCount];

            for (int i = 0; i < HexagonVertexCount; i++)
            {
                vertices[i] = GetHexVertex(i, hexSize);
            }

            return vertices;
        }

        public static Vector3 GetHexVertex(int vertexIndex, float hexSize)
        {
            ValidateVertexIndex(vertexIndex);

            float angleRadians = Mathf.Deg2Rad * (DegreesPerVertex * vertexIndex + VertexAngleOffset);

            return new Vector3(
                hexSize * Mathf.Cos(angleRadians),
                0,
                hexSize * Mathf.Sin(angleRadians)
            );
        }

        public static (Vector3, Vector3) GetEdgeVertices(int edgeIndex, float hexSize)
        {
            ValidateEdgeIndex(edgeIndex);

            Vector3 start = GetHexVertex((edgeIndex + HexagonVertexCount - 1) % HexagonVertexCount, hexSize);
            Vector3 end = GetHexVertex(edgeIndex, hexSize);

            return (start, end);
        }

        public static Vector3 GetEdgeCenter(int edgeIndex, float hexSize)
        {
            (Vector3 start, Vector3 end) = GetEdgeVertices(edgeIndex, hexSize);
            return (start + end) / 2f;
        }

        public static Vector3 GetEdgeNormal(int edgeIndex)
        {
            ValidateEdgeIndex(edgeIndex);

            float angleRadians = Mathf.Deg2Rad * (DegreesPerVertex * edgeIndex);

            return new Vector3(
                Mathf.Cos(angleRadians),
                0,
                Mathf.Sin(angleRadians)
            );
        }

        public static float GetEdgeLength(float hexSize)
        {
            return hexSize;
        }

        public static float GetInnerRadius(float hexSize)
        {
            return hexSize * SquareRootOfThreeHalf;
        }

        public static float GetOuterRadius(float hexSize)
        {
            return hexSize;
        }

        public static Vector3 AxialToWorldPosition(AxialCoord coordinate, float hexSize)
        {
            return coordinate.ToWorldPosition(hexSize);
        }

        public static AxialCoord WorldToAxialPosition(Vector3 worldPosition, float hexSize)
        {
            return AxialCoord.FromWorldPosition(worldPosition, hexSize);
        }

        public static int GetOppositeEdge(int edgeIndex)
        {
            ValidateEdgeIndex(edgeIndex);
            return (edgeIndex + 3) % HexagonEdgeCount;
        }

        public static float CalculateDoorWidth(float edgeLength, float doorWidthRatio = 0.2f)
        {
            return edgeLength * doorWidthRatio;
        }

        private static void ValidateVertexIndex(int vertexIndex)
        {
            if (vertexIndex < 0 || vertexIndex >= HexagonVertexCount)
                throw new System.ArgumentOutOfRangeException(nameof(vertexIndex), $"Vertex index must be between 0 and {HexagonVertexCount - 1}");
        }

        private static void ValidateEdgeIndex(int edgeIndex)
        {
            if (edgeIndex < 0 || edgeIndex >= HexagonEdgeCount)
                throw new System.ArgumentOutOfRangeException(nameof(edgeIndex), $"Edge index must be between 0 and {HexagonEdgeCount - 1}");
        }
    }
}
