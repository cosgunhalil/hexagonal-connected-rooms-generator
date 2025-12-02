using System.Collections.Generic;
using UnityEngine;
using HRCG.Core;

namespace HRCG.Geometry
{
    public class HexGeometry
    {
        public static List<Vector3> GenerateFloorVertices(AxialCoord coordinate, float hexSize, float floorHeight = 0f)
        {
            List<Vector3> vertices = new List<Vector3>();
            Vector3 center = coordinate.ToWorldPosition(hexSize);
            center.y = floorHeight;

            Vector3[] hexVertices = HexMath.GetHexVertices(hexSize, true);

            vertices.Add(center);

            for (int i = 0; i < 6; i++)
            {
                Vector3 vertex = center + hexVertices[i];
                vertex.y = floorHeight;
                vertices.Add(vertex);
            }

            return vertices;
        }

        public static List<int> GenerateFloorTriangles(int vertexOffset = 0)
        {
            List<int> triangles = new List<int>();

            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                triangles.Add(vertexOffset + 0);
                triangles.Add(vertexOffset + i + 1);
                triangles.Add(vertexOffset + next + 1);
            }

            return triangles;
        }

        public static List<Vector2> GenerateFloorUVs()
        {
            List<Vector2> uvs = new List<Vector2>
            {
                new Vector2(0.5f, 0.5f)
            };

            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i);
                float u = 0.5f + 0.5f * Mathf.Cos(angle);
                float v = 0.5f + 0.5f * Mathf.Sin(angle);
                uvs.Add(new Vector2(u, v));
            }

            return uvs;
        }

        public static List<Vector3> GenerateWallVertices(
            AxialCoord coordinate,
            int edgeIndex,
            float hexSize,
            float wallHeight,
            float floorHeight = 0f)
        {
            List<Vector3> vertices = new List<Vector3>();
            Vector3 center = coordinate.ToWorldPosition(hexSize);

            (Vector3 v1, Vector3 v2) = HexMath.GetEdgeVertices(edgeIndex, hexSize, true);

            Vector3 bottomLeft = center + v1;
            bottomLeft.y = floorHeight;

            Vector3 bottomRight = center + v2;
            bottomRight.y = floorHeight;

            Vector3 topLeft = bottomLeft + Vector3.up * wallHeight;
            Vector3 topRight = bottomRight + Vector3.up * wallHeight;

            vertices.Add(bottomLeft);
            vertices.Add(bottomRight);
            vertices.Add(topRight);
            vertices.Add(topLeft);

            return vertices;
        }

        public static List<int> GenerateWallTriangles(int vertexOffset = 0)
        {
            List<int> triangles = new List<int>
            {
                vertexOffset + 0, vertexOffset + 1, vertexOffset + 2,
                vertexOffset + 0, vertexOffset + 2, vertexOffset + 3
            };

            return triangles;
        }

        public static List<Vector2> GenerateWallUVs()
        {
            return new List<Vector2>
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
        }

        public static (List<Vector3>, List<int>, List<Vector2>) GenerateDoorWallGeometry(
            AxialCoord coordinate,
            int edgeIndex,
            float hexSize,
            float wallHeight,
            float doorWidth,
            float doorHeight,
            float floorHeight = 0f)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            Vector3 center = coordinate.ToWorldPosition(hexSize);
            (Vector3 v1, Vector3 v2) = HexMath.GetEdgeVertices(edgeIndex, hexSize, true);

            Vector3 edgeStart = center + v1;
            Vector3 edgeEnd = center + v2;
            float edgeLength = Vector3.Distance(v1, v2);

            Vector3 edgeDirection = (edgeEnd - edgeStart).normalized;
            float doorOffset = (edgeLength - doorWidth) / 2f;

            Vector3 doorStart = edgeStart + edgeDirection * doorOffset;
            Vector3 doorEnd = doorStart + edgeDirection * doorWidth;

            Vector3 bl1 = edgeStart;
            bl1.y = floorHeight;
            Vector3 bl2 = doorStart;
            bl2.y = floorHeight;
            Vector3 bl3 = doorStart;
            bl3.y = floorHeight + doorHeight;
            Vector3 bl4 = doorEnd;
            bl4.y = floorHeight + doorHeight;
            Vector3 bl5 = doorEnd;
            bl5.y = floorHeight;
            Vector3 bl6 = edgeEnd;
            bl6.y = floorHeight;

            Vector3 tl1 = bl1 + Vector3.up * wallHeight;
            Vector3 tl2 = bl2 + Vector3.up * wallHeight;
            Vector3 tl3 = bl3;
            Vector3 tl4 = bl4;
            Vector3 tl5 = bl5 + Vector3.up * wallHeight;
            Vector3 tl6 = bl6 + Vector3.up * wallHeight;

            int vOffset = 0;

            if (doorOffset > 0.01f)
            {
                vertices.Add(bl1);
                vertices.Add(bl2);
                vertices.Add(tl2);
                vertices.Add(tl1);

                triangles.Add(vOffset + 0);
                triangles.Add(vOffset + 1);
                triangles.Add(vOffset + 2);
                triangles.Add(vOffset + 0);
                triangles.Add(vOffset + 2);
                triangles.Add(vOffset + 3);

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(0.3f, 0));
                uvs.Add(new Vector2(0.3f, 1));
                uvs.Add(new Vector2(0, 1));

                vOffset += 4;
            }

            vertices.Add(bl2);
            vertices.Add(bl3);
            vertices.Add(tl2);

            triangles.Add(vOffset + 0);
            triangles.Add(vOffset + 1);
            triangles.Add(vOffset + 2);

            uvs.Add(new Vector2(0.3f, 0));
            uvs.Add(new Vector2(0.3f, doorHeight / wallHeight));
            uvs.Add(new Vector2(0.3f, 1));

            vOffset += 3;

            vertices.Add(tl3);
            vertices.Add(tl4);
            vertices.Add(tl2);
            vertices.Add(tl5);

            triangles.Add(vOffset + 0);
            triangles.Add(vOffset + 1);
            triangles.Add(vOffset + 2);
            triangles.Add(vOffset + 1);
            triangles.Add(vOffset + 3);
            triangles.Add(vOffset + 2);

            uvs.Add(new Vector2(0.3f, doorHeight / wallHeight));
            uvs.Add(new Vector2(0.7f, doorHeight / wallHeight));
            uvs.Add(new Vector2(0.3f, 1));
            uvs.Add(new Vector2(0.7f, 1));

            vOffset += 4;

            vertices.Add(bl4);
            vertices.Add(bl5);
            vertices.Add(tl5);

            triangles.Add(vOffset + 0);
            triangles.Add(vOffset + 1);
            triangles.Add(vOffset + 2);

            uvs.Add(new Vector2(0.7f, 0));
            uvs.Add(new Vector2(0.7f, doorHeight / wallHeight));
            uvs.Add(new Vector2(0.7f, 1));

            vOffset += 3;

            if (doorOffset > 0.01f)
            {
                vertices.Add(bl5);
                vertices.Add(bl6);
                vertices.Add(tl6);
                vertices.Add(tl5);

                triangles.Add(vOffset + 0);
                triangles.Add(vOffset + 1);
                triangles.Add(vOffset + 2);
                triangles.Add(vOffset + 0);
                triangles.Add(vOffset + 2);
                triangles.Add(vOffset + 3);

                uvs.Add(new Vector2(0.7f, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1));
                uvs.Add(new Vector2(0.7f, 1));
            }

            return (vertices, triangles, uvs);
        }
    }
}
