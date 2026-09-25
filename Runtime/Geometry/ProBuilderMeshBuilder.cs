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
        private const float MinSize = 0.001f;

        private readonly float hexSize;
        private readonly float wallHeight;
        private readonly float doorHeight;
        private readonly float doorWidthRatio;
        private readonly float floorHeight;

        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Face> faces = new List<Face>();
        private readonly List<Material> materials = new List<Material>();

        public bool IsEmpty => faces.Count == 0;

        public ProBuilderMeshBuilder(float hexSize, float wallHeight = 3f, float doorHeight = 2.5f, float doorWidthRatio = 0.2f, float floorHeight = 0f)
        {
            this.hexSize = hexSize;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
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
            float doorWidth = HexMath.CalculateDoorWidth(HexMath.GetEdgeLength(hexSize), doorWidthRatio);
            List<WallSegment> segments = HexGeometry.GetDoorWallSegments(
                coordinate, edgeIndex, hexSize, wallHeight, doorWidth, doorHeight, floorHeight);

            foreach (WallSegment segment in segments)
            {
                AddDoubleSidedQuad(segment, material);
            }
        }

        // Hexagon at wall height facing down, visible from inside the room only.
        public void AddCeiling(AxialCoord coordinate, Material material)
        {
            Vector3[] vertices = HexGeometry.GetFloorVertices(coordinate, hexSize, floorHeight + wallHeight);
            List<Vector3> corners = new List<Vector3>(6);
            for (int i = 1; i <= 6; i++)
                corners.Add(vertices[i]);

            AddPolygon(corners, Vector3.down, material);
        }

        // One cell's side of a thick wall: inner face, top cap, chamfers at open corners, an outer face for
        // exterior walls and, for doors, an opening through the full depth with jambs and a lintel.
        public void AddThickWall(ThickWallStrip strip, bool hasDoor, bool isExterior, Material material)
        {
            float bottom = floorHeight;
            float top = floorHeight + wallHeight;
            float doorTop = floorHeight + Mathf.Min(doorHeight, wallHeight);
            bool hasLintel = top - doorTop > MinSize;

            Vector3 edgeDirection = (strip.EdgeEnd - strip.EdgeStart).normalized;
            Vector3 inset = strip.Inward * strip.Depth;

            if (strip.HasStartChamfer)
                AddVerticalQuad(strip.StartChamferPoint, strip.InnerStart, bottom, top, strip.CellCenter, material);

            if (strip.HasEndChamfer)
                AddVerticalQuad(strip.InnerEnd, strip.EndChamferPoint, bottom, top, strip.CellCenter, material);

            if (isExterior)
                AddVerticalQuad(strip.EdgeStart, strip.EdgeEnd, bottom, top, strip.EdgeStart - inset, material);

            if (!hasDoor)
            {
                AddVerticalQuad(strip.InnerStart, strip.InnerEnd, bottom, top, strip.CellCenter, material);
                AddPolygon(CapOutline(strip, strip.EdgeStart, strip.EdgeEnd, strip.InnerEnd, strip.InnerStart, true, true, top), Vector3.up, material);
                return;
            }

            float edgeLength = Vector3.Distance(strip.EdgeStart, strip.EdgeEnd);
            float doorWidth = Mathf.Min(HexMath.CalculateDoorWidth(edgeLength, doorWidthRatio), edgeLength);
            Vector3 doorStart = strip.EdgeStart + edgeDirection * ((edgeLength - doorWidth) / 2f);
            Vector3 doorEnd = doorStart + edgeDirection * doorWidth;
            Vector3 innerDoorStart = doorStart + inset;
            Vector3 innerDoorEnd = doorEnd + inset;

            // Inner face on both sides of the opening, and above it.
            AddVerticalQuad(strip.InnerStart, innerDoorStart, bottom, top, strip.CellCenter, material);
            AddVerticalQuad(innerDoorEnd, strip.InnerEnd, bottom, top, strip.CellCenter, material);
            if (hasLintel)
                AddVerticalQuad(innerDoorStart, innerDoorEnd, doorTop, top, strip.CellCenter, material);

            // Jambs face into the opening.
            AddVerticalQuad(doorStart, innerDoorStart, bottom, doorTop, doorStart + edgeDirection, material);
            AddVerticalQuad(doorEnd, innerDoorEnd, bottom, doorTop, doorEnd - edgeDirection, material);

            if (hasLintel)
            {
                AddPolygon(new List<Vector3>
                {
                    WithHeight(doorStart, doorTop), WithHeight(doorEnd, doorTop),
                    WithHeight(innerDoorEnd, doorTop), WithHeight(innerDoorStart, doorTop)
                }, Vector3.down, material);
            }

            // Top cap, split around the opening when the door reaches the top of the wall.
            if (hasLintel)
            {
                AddPolygon(CapOutline(strip, strip.EdgeStart, strip.EdgeEnd, strip.InnerEnd, strip.InnerStart, true, true, top), Vector3.up, material);
            }
            else
            {
                AddPolygon(CapOutline(strip, strip.EdgeStart, doorStart, innerDoorStart, strip.InnerStart, true, false, top), Vector3.up, material);
                AddPolygon(CapOutline(strip, doorEnd, strip.EdgeEnd, strip.InnerEnd, innerDoorEnd, false, true, top), Vector3.up, material);
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

        // Outline of a cap piece: edge-side start/end, inner end/start, plus the chamfer points where requested.
        private static List<Vector3> CapOutline(ThickWallStrip strip, Vector3 edgeStart, Vector3 edgeEnd, Vector3 innerEnd, Vector3 innerStart,
            bool includeStartChamfer, bool includeEndChamfer, float height)
        {
            List<Vector3> outline = new List<Vector3> { WithHeight(edgeStart, height), WithHeight(edgeEnd, height) };

            if (includeEndChamfer && strip.HasEndChamfer)
                outline.Add(WithHeight(strip.EndChamferPoint, height));

            outline.Add(WithHeight(innerEnd, height));
            outline.Add(WithHeight(innerStart, height));

            if (includeStartChamfer && strip.HasStartChamfer)
                outline.Add(WithHeight(strip.StartChamferPoint, height));

            return outline;
        }

        // Vertical rectangle on the segment from -> to, facing the side where facingPoint lies.
        private void AddVerticalQuad(Vector3 from, Vector3 to, float bottom, float top, Vector3 facingPoint, Material material)
        {
            if (Vector3.Distance(from, to) < MinSize || top - bottom < MinSize)
                return;

            Vector3 middle = (from + to) / 2f;
            Vector3 facing = facingPoint - middle;
            facing.y = 0f;

            AddPolygon(new List<Vector3>
            {
                WithHeight(from, bottom), WithHeight(from, top), WithHeight(to, top), WithHeight(to, bottom)
            }, facing, material);
        }

        // Convex polygon, triangulated as a fan and wound so that it faces desiredNormal.
        private void AddPolygon(List<Vector3> points, Vector3 desiredNormal, Material material)
        {
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 current = points[i];
                Vector3 next = points[(i + 1) % points.Count];
                normal += Vector3.Cross(current - points[0], next - points[0]);
            }

            if (normal.sqrMagnitude < MinSize * MinSize)
                return;

            if (Vector3.Dot(normal, desiredNormal) < 0f)
                points.Reverse();

            int baseIndex = positions.Count;
            positions.AddRange(points);

            int[] indices = new int[(points.Count - 2) * 3];
            for (int i = 0; i < points.Count - 2; i++)
            {
                indices[i * 3] = baseIndex;
                indices[i * 3 + 1] = baseIndex + i + 1;
                indices[i * 3 + 2] = baseIndex + i + 2;
            }

            AddFace(indices, material);
        }

        private static Vector3 WithHeight(Vector3 point, float height)
        {
            return new Vector3(point.x, height, point.z);
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
