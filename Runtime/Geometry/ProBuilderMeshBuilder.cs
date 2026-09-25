using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using HRCG.Core;

namespace HRCG.Geometry
{
    // Accumulates floor and wall faces and emits them as a single ProBuilderMesh,
    // with one submesh per distinct material.
    public class ProBuilderMeshBuilder
    {
        private readonly float hexSize;
        private readonly float wallHeight;
        private readonly float doorHeight;
        private readonly float floorHeight;

        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Face> faces = new List<Face>();
        private readonly List<Material> materials = new List<Material>();

        public bool IsEmpty => faces.Count == 0;

        public ProBuilderMeshBuilder(float hexSize, float wallHeight = 3f, float doorHeight = 2.5f, float floorHeight = 0f)
        {
            this.hexSize = hexSize;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.floorHeight = floorHeight;
        }

        public void AddFloor(AxialCoord coordinate, Material material)
        {
            int baseIndex = positions.Count;
            positions.AddRange(HexGeometry.GetFloorVertices(coordinate, hexSize, floorHeight));

            // Clockwise when seen from above, so the floor faces +Y.
            int[] indices = new int[18];
            for (int i = 0; i < 6; i++)
            {
                indices[i * 3] = baseIndex;
                indices[i * 3 + 1] = baseIndex + 1 + (i + 1) % 6;
                indices[i * 3 + 2] = baseIndex + 1 + i;
            }

            AddFace(indices, material);
        }

        public void AddWall(AxialCoord coordinate, int edgeIndex, Material material)
        {
            AddDoubleSidedQuad(HexGeometry.GetWallSegment(coordinate, edgeIndex, hexSize, wallHeight, floorHeight), material);
        }

        public void AddDoorWall(AxialCoord coordinate, int edgeIndex, Material material)
        {
            float doorWidth = HexMath.CalculateDoorWidth(HexMath.GetEdgeLength(hexSize));
            List<WallSegment> segments = HexGeometry.GetDoorWallSegments(
                coordinate, edgeIndex, hexSize, wallHeight, doorWidth, doorHeight, floorHeight);

            foreach (WallSegment segment in segments)
            {
                AddDoubleSidedQuad(segment, material);
            }
        }

        public GameObject Build(string name)
        {
            ProBuilderMesh mesh = ProBuilderMesh.Create(positions, faces);
            mesh.gameObject.name = name;
            mesh.GetComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            mesh.ToMesh();
            mesh.Refresh();
            return mesh.gameObject;
        }

        private void AddDoubleSidedQuad(WallSegment segment, Material material)
        {
            Vector3 bottomStart = new Vector3(segment.Start.x, segment.Bottom, segment.Start.z);
            Vector3 bottomEnd = new Vector3(segment.End.x, segment.Bottom, segment.End.z);
            Vector3 topStart = new Vector3(segment.Start.x, segment.Top, segment.Start.z);
            Vector3 topEnd = new Vector3(segment.End.x, segment.Top, segment.End.z);

            // Separate vertices per side so each side gets its own normal.
            AddQuad(bottomStart, topStart, topEnd, bottomEnd, material);
            AddQuad(bottomEnd, topEnd, topStart, bottomStart, material);
        }

        private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Material material)
        {
            int baseIndex = positions.Count;
            positions.Add(a);
            positions.Add(b);
            positions.Add(c);
            positions.Add(d);

            AddFace(new[]
            {
                baseIndex, baseIndex + 1, baseIndex + 2,
                baseIndex, baseIndex + 2, baseIndex + 3
            }, material);
        }

        private void AddFace(int[] indices, Material material)
        {
            faces.Add(new Face(indices) { submeshIndex = GetSubmeshIndex(material) });
        }

        private int GetSubmeshIndex(Material material)
        {
            if (material == null)
                material = BuiltinMaterials.defaultMaterial;

            int index = materials.IndexOf(material);
            if (index < 0)
            {
                index = materials.Count;
                materials.Add(material);
            }

            return index;
        }
    }
}
