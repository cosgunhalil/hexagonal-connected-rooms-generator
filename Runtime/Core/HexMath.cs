using UnityEngine;

namespace HRCG.Core
{
    public static class HexMath
    {
        private const float SquareRootOfThree = 1.7320508075688772f;
        private const float SquareRootOfThreeHalf = 0.8660254037844386f;
        private const float SquareRootOfThreeThird = 0.5773502691896257f;
        private const int HexagonVertexCount = 6;
        private const int HexagonEdgeCount = 6;
        private const float DegreesPerVertex = 60f;

        public static Vector3[] GetHexVertices(float hexSize, bool flatTop = true)
        {
            Vector3[] vertices = new Vector3[HexagonVertexCount];
            
            if (flatTop)
            {
                for (int i = 0; i < HexagonVertexCount; i++)
                {
                    float angleDegrees = DegreesPerVertex * i;
                    float angleRadians = Mathf.Deg2Rad * angleDegrees;
                    vertices[i] = new Vector3(
                        hexSize * Mathf.Cos(angleRadians),
                        0,
                        hexSize * Mathf.Sin(angleRadians)
                    );
                }
            }
            else
            {
                for (int i = 0; i < HexagonVertexCount; i++)
                {
                    float angleDegrees = DegreesPerVertex * i + 30f;
                    float angleRadians = Mathf.Deg2Rad * angleDegrees;
                    vertices[i] = new Vector3(
                        hexSize * Mathf.Cos(angleRadians),
                        0,
                        hexSize * Mathf.Sin(angleRadians)
                    );
                }
            }
            
            return vertices;
        }

        public static Vector3 GetHexVertex(int vertexIndex, float hexSize, bool flatTop = true)
        {
            ValidateVertexIndex(vertexIndex);

            float angleDegrees = flatTop ? DegreesPerVertex * vertexIndex : DegreesPerVertex * vertexIndex + 30f;
            float angleRadians = Mathf.Deg2Rad * angleDegrees;
            
            return new Vector3(
                hexSize * Mathf.Cos(angleRadians),
                0,
                hexSize * Mathf.Sin(angleRadians)
            );
        }

        public static Vector3 GetEdgeCenter(int edgeIndex, float hexSize, bool flatTop = true)
        {
            ValidateEdgeIndex(edgeIndex);

            Vector3 vertex1 = GetHexVertex(edgeIndex, hexSize, flatTop);
            Vector3 vertex2 = GetHexVertex((edgeIndex + 1) % HexagonVertexCount, hexSize, flatTop);
            
            return (vertex1 + vertex2) / 2f;
        }

        public static float GetEdgeLength(float hexSize)
        {
            return hexSize;
        }

        public static Vector3 GetEdgeNormal(int edgeIndex, bool flatTop = true)
        {
            ValidateEdgeIndex(edgeIndex);

            float angleDegrees = flatTop ? DegreesPerVertex * edgeIndex + 30f : DegreesPerVertex * edgeIndex + 60f;
            float angleRadians = Mathf.Deg2Rad * angleDegrees;
            
            return new Vector3(
                Mathf.Cos(angleRadians),
                0,
                Mathf.Sin(angleRadians)
            );
        }

        public static (Vector3, Vector3) GetEdgeVertices(int edgeIndex, float hexSize, bool flatTop = true)
        {
            ValidateEdgeIndex(edgeIndex);

            Vector3 vertex1 = GetHexVertex(edgeIndex, hexSize, flatTop);
            Vector3 vertex2 = GetHexVertex((edgeIndex + 1) % HexagonVertexCount, hexSize, flatTop);
            
            return (vertex1, vertex2);
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

        public static Vector3[] GetWallVertices(int edgeIndex, float hexSize, float wallHeight, bool flatTop = true)
        {
            (Vector3 vertex1, Vector3 vertex2) = GetEdgeVertices(edgeIndex, hexSize, flatTop);
            
            return new Vector3[]
            {
                vertex1,
                vertex2,
                vertex2 + Vector3.up * wallHeight,
                vertex1 + Vector3.up * wallHeight
            };
        }

        public static Vector3[] GetDoorVertices(int edgeIndex, float hexSize, float wallHeight, float doorWidth, bool flatTop = true)
        {
            Vector3 edgeCenter = GetEdgeCenter(edgeIndex, hexSize, flatTop);
            (Vector3 vertex1, Vector3 vertex2) = GetEdgeVertices(edgeIndex, hexSize, flatTop);
            
            Vector3 edgeDirection = (vertex2 - vertex1).normalized;
            float halfDoorWidth = doorWidth / 2f;
            
            Vector3 doorVertex1 = edgeCenter - edgeDirection * halfDoorWidth;
            Vector3 doorVertex2 = edgeCenter + edgeDirection * halfDoorWidth;
            
            return new Vector3[]
            {
                doorVertex1,
                doorVertex2,
                doorVertex2 + Vector3.up * wallHeight,
                doorVertex1 + Vector3.up * wallHeight
            };
        }

        public static float CalculateDoorWidth(float edgeLength)
        {
            const float DoorWidthRatio = 0.2f;
            return edgeLength * DoorWidthRatio;
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
