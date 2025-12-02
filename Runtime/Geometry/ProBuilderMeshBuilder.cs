using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using HRCG.Core;

namespace HRCG.Geometry
{
    public class ProBuilderMeshBuilder
    {
        private float hexSize;
        private float wallHeight;
        private float doorHeight;
        private float floorHeight;

        public ProBuilderMeshBuilder(float hexSize, float wallHeight = 3f, float doorHeight = 2.5f, float floorHeight = 0f)
        {
            this.hexSize = hexSize;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.floorHeight = floorHeight;
        }

        private List<Vector4> ConvertUVsToVector4(List<Vector2> uvs)
        {
            List<Vector4> uvs4 = new List<Vector4>();
            foreach (Vector2 uv in uvs)
            {
                uvs4.Add(new Vector4(uv.x, uv.y, 0, 0));
            }
            return uvs4;
        }

        public GameObject BuildFloor(AxialCoord coordinate, Material material = null)
        {
            GameObject floorObject = new GameObject($"Floor_{coordinate}");
            ProBuilderMesh pbMesh = floorObject.AddComponent<ProBuilderMesh>();

            List<Vector3> vertices = HexGeometry.GenerateFloorVertices(coordinate, hexSize, floorHeight);
            List<int> triangles = HexGeometry.GenerateFloorTriangles();
            List<Vector2> uvs = HexGeometry.GenerateFloorUVs();

            List<Face> faces = new List<Face>();
            Face floor = new Face(triangles.ToArray());
            faces.Add(floor);

            pbMesh.RebuildWithPositionsAndFaces(vertices, faces);
            pbMesh.SetUVs(0, ConvertUVsToVector4(uvs));
            pbMesh.ToMesh();
            pbMesh.Refresh();

            if (material != null)
            {
                MeshRenderer renderer = floorObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            return floorObject;
        }

        public GameObject BuildWall(AxialCoord coordinate, int edgeIndex, Material material = null)
        {
            GameObject wallObject = new GameObject($"Wall_{coordinate}_E{edgeIndex}");
            ProBuilderMesh pbMesh = wallObject.AddComponent<ProBuilderMesh>();

            List<Vector3> vertices = HexGeometry.GenerateWallVertices(coordinate, edgeIndex, hexSize, wallHeight, floorHeight);
            List<int> triangles = HexGeometry.GenerateWallTriangles();
            List<Vector2> uvs = HexGeometry.GenerateWallUVs();

            List<Face> faces = new List<Face>();
            Face wall = new Face(triangles.ToArray());
            faces.Add(wall);

            pbMesh.RebuildWithPositionsAndFaces(vertices, faces);
            pbMesh.SetUVs(0, ConvertUVsToVector4(uvs));
            pbMesh.ToMesh();
            pbMesh.Refresh();

            if (material != null)
            {
                MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            return wallObject;
        }

        public GameObject BuildDoorWall(AxialCoord coordinate, int edgeIndex, Material material = null)
        {
            GameObject doorWallObject = new GameObject($"DoorWall_{coordinate}_E{edgeIndex}");
            ProBuilderMesh pbMesh = doorWallObject.AddComponent<ProBuilderMesh>();

            float doorWidth = HexMath.CalculateDoorWidth(hexSize);
            var (vertices, triangles, uvs) = HexGeometry.GenerateDoorWallGeometry(
                coordinate, edgeIndex, hexSize, wallHeight, doorWidth, doorHeight, floorHeight);

            List<Face> faces = new List<Face>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Face face = new Face(new int[] { triangles[i], triangles[i + 1], triangles[i + 2] });
                faces.Add(face);
            }

            pbMesh.RebuildWithPositionsAndFaces(vertices, faces);
            pbMesh.SetUVs(0, ConvertUVsToVector4(uvs));
            pbMesh.ToMesh();
            pbMesh.Refresh();

            if (material != null)
            {
                MeshRenderer renderer = doorWallObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            return doorWallObject;
        }

        public GameObject BuildCellGeometry(HexCell cell, Material floorMaterial = null, Material wallMaterial = null)
        {
            GameObject cellObject = new GameObject($"Cell_{cell.Coordinate}");

            GameObject floor = BuildFloor(cell.Coordinate, floorMaterial);
            floor.transform.SetParent(cellObject.transform);

            for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
            {
                WallFlag flag = cell.GetEdgeFlag(edgeIndex);

                if (flag.HasFlag(WallFlag.Wall))
                {
                    GameObject wall = BuildWall(cell.Coordinate, edgeIndex, wallMaterial);
                    wall.transform.SetParent(cellObject.transform);
                }
                else if (flag.HasFlag(WallFlag.HasDoor))
                {
                    GameObject doorWall = BuildDoorWall(cell.Coordinate, edgeIndex, wallMaterial);
                    doorWall.transform.SetParent(cellObject.transform);
                }
            }

            return cellObject;
        }
    }
}