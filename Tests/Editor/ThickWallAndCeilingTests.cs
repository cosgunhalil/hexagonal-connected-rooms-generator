using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using CRG.Core;
using CRG.Generation;
using CRG.Geometry;
using CRG.Runtime;

namespace CRG.Tests
{
    [TestFixture(GridType.Hexagon)]
    [TestFixture(GridType.Square)]
    [TestFixture(GridType.Triangle)]
    public class ThickWallAndCeilingTests
    {
        private readonly GridType gridType;

        public ThickWallAndCeilingTests(GridType gridType)
        {
            this.gridType = gridType;
        }

        private const float Epsilon = 1e-3f;

        private readonly List<Object> created = new List<Object>();
        private GenerationParameters parameters;

        [SetUp]
        public void SetUp()
        {
            parameters = GenerationParameters.CreateDefault();
            parameters.GridType = gridType;
            parameters.RandomSeed = 42;
            parameters.TargetRoomCount = 20;
            parameters.WallThickness = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        [Test]
        public void SingleCellRoom_WallTopsFormInsetRing()
        {
            parameters.MinCellsPerRoom = 1;
            parameters.MaxCellsPerRoom = 1;
            parameters.TargetRoomCount = 1;

            List<Triangle> tops = GetTriangles(Generate()).Where(t => IsHorizontalAt(t, parameters.WallHeight, up: true)).ToList();

            // Every wall is exterior (full thickness), so the tops fill the ring between the cell and the same
            // regular polygon inset by t, whose area scales with the square of the inner radius.
            float apothem = Topology.GetInnerRadius(new CellCoord(0, 0), parameters.CellSize);
            float insetScale = (apothem - parameters.WallThickness) / apothem;
            float expected = CellArea() * (1f - insetScale * insetScale);

            Assert.That(tops.Sum(t => t.Area), Is.EqualTo(expected).Within(expected * 1e-3f));
        }

        [TestCase(0.5f, 2.5f)]
        [TestCase(1.5f, 2.5f)]
        [TestCase(1f, 3f)]
        public void WallTops_NeitherOverlapNorLeaveGaps(float thickness, float doorHeight)
        {
            parameters.WallThickness = thickness;
            parameters.DoorHeight = doorHeight;

            for (int seed = 0; seed < 10; seed++)
            {
                parameters.RandomSeed = seed;
                CellGrid grid = new CRGGenerator().Generate(parameters);
                GameObject level = Track(LevelGeometryGenerator.FromParameters(grid, parameters).GenerateLevel());

                List<Triangle> tops = GetTriangles(level).Where(t => IsHorizontalAt(t, parameters.WallHeight, up: true)).ToList();
                AssertWallTopsCoverWallBands(grid, tops, $"seed {seed}");
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void Doorways_AreOpen_AndWallsBesideThemAreSolid(float thickness)
        {
            parameters.WallThickness = thickness;
            parameters.AddMeshCollider = true;
            GameObject level = Generate();
            CRGLevelData data = level.GetComponent<CRGLevelData>();
            Physics.SyncTransforms();

            float reach = thickness + 0.5f;
            Vector3 midHeight = Vector3.up * (parameters.DoorHeight / 2f);

            foreach (CRGLevelData.DoorData door in data.Doors)
            {
                Vector3 center = level.transform.TransformPoint(data.GetDoorCenterLocal(door)) + midHeight;
                Vector3 forward = level.transform.TransformDirection(data.GetDoorForwardLocal(door));
                (Vector3 openingStart, Vector3 openingEnd) = data.GetDoorOpeningLocal(door);
                Vector3 alongEdge = level.transform.TransformDirection((openingEnd - openingStart).normalized);

                Assert.That(Physics.Linecast(center - forward * reach, center + forward * reach), Is.False,
                    $"door {door.RoomA}-{door.RoomB} is blocked");

                // A quarter of the edge length from the door center is solid wall, clear of the door and of the
                // corner regions (which reach furthest at the 60 degree corners of triangle grids).
                Vector3 solid = center - alongEdge * (parameters.CellSize * 0.25f);
                Assert.That(Physics.Linecast(solid - forward * reach, solid + forward * reach), Is.True,
                    $"wall next to door {door.RoomA}-{door.RoomB} is open");
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void Ceiling_CoversEveryCellFacingDown(float thickness)
        {
            parameters.WallThickness = thickness;
            parameters.AddCeiling = true;
            GameObject level = Generate();
            int cells = level.GetComponent<CRGLevelData>().Rooms.Sum(room => room.Cells.Count);

            List<Triangle> ceiling = GetTriangles(level).Where(t => IsHorizontalAt(t, parameters.WallHeight, up: false)).ToList();
            float expected = cells * CellArea();

            Assert.That(ceiling.Sum(t => t.Area), Is.EqualTo(expected).Within(expected * 1e-3f));
        }

        [Test]
        public void Ceiling_UsesItsOwnMaterialSlot()
        {
            parameters.AddCeiling = true;
            Material floor = Track(new Material(UnityEngine.ProBuilder.BuiltinMaterials.defaultMaterial));
            Material wall = Track(new Material(UnityEngine.ProBuilder.BuiltinMaterials.defaultMaterial));
            Material ceilingMaterial = Track(new Material(UnityEngine.ProBuilder.BuiltinMaterials.defaultMaterial));

            GameObject level = Track(LevelGeometryGenerator.GenerateComplete(parameters, floor, wall, null, ceilingMaterial));

            Assert.That(level.GetComponent<MeshRenderer>().sharedMaterials, Is.EquivalentTo(new[] { floor, wall, ceilingMaterial }));
        }

        [Test]
        public void CeilingChanceZero_BuildsNoCeilings()
        {
            parameters.AddCeiling = true;
            parameters.CeilingChance = 0f;
            GameObject level = Generate();

            Assert.That(GetTriangles(level).Any(t => IsHorizontalAt(t, parameters.WallHeight, up: false)), Is.False);
            Assert.That(level.GetComponent<CRGLevelData>().Rooms.Any(room => room.HasCeiling), Is.False);
        }

        [Test]
        public void CeilingChanceOne_CoversEveryRoom()
        {
            parameters.AddCeiling = true;
            parameters.CeilingChance = 1f;

            Assert.That(Generate().GetComponent<CRGLevelData>().Rooms.All(room => room.HasCeiling), Is.True);
        }

        [Test]
        public void PartialCeilingChance_CoversExactlyTheRoomsMarkedAsCovered()
        {
            parameters.AddCeiling = true;
            parameters.CeilingChance = 0.5f;
            bool sawMixedLevel = false;

            for (int seed = 0; seed < 10; seed++)
            {
                parameters.RandomSeed = seed;
                GameObject level = Generate();
                List<CRGLevelData.RoomData> rooms = level.GetComponent<CRGLevelData>().Rooms.ToList();

                int coveredCells = rooms.Where(room => room.HasCeiling).Sum(room => room.Cells.Count);
                float expected = coveredCells * CellArea();
                float actual = GetTriangles(level).Where(t => IsHorizontalAt(t, parameters.WallHeight, up: false)).Sum(t => t.Area);
                Assert.That(actual, Is.EqualTo(expected).Within(Mathf.Max(expected, 1f) * 1e-3f), $"seed {seed}");

                sawMixedLevel |= rooms.Any(room => room.HasCeiling) && rooms.Any(room => !room.HasCeiling);
            }

            Assert.That(sawMixedLevel, Is.True, "a 0.5 chance should produce levels with both covered and open rooms");
        }

        [Test]
        public void CeilingChance_IsDeterministic_AndNeverChangesTheLayout()
        {
            parameters.AddCeiling = true;

            for (int seed = 0; seed < 10; seed++)
            {
                parameters.RandomSeed = seed;

                parameters.CeilingChance = 1f;
                string layoutWithAllCeilings = LayoutFingerprint(new CRGGenerator().Generate(parameters), includeCeilings: false);

                parameters.CeilingChance = 0.4f;
                CellGrid first = new CRGGenerator().Generate(parameters);
                CellGrid second = new CRGGenerator().Generate(parameters);

                Assert.That(LayoutFingerprint(first, includeCeilings: false), Is.EqualTo(layoutWithAllCeilings), $"seed {seed}: layout changed");
                Assert.That(LayoutFingerprint(first, includeCeilings: true), Is.EqualTo(LayoutFingerprint(second, includeCeilings: true)), $"seed {seed}: ceilings differ");
            }
        }

        [Test]
        public void NoCeiling_ByDefault()
        {
            parameters = GenerationParameters.CreateDefault();
            parameters.GridType = gridType;
            parameters.RandomSeed = 42;

            Assert.That(GetTriangles(Generate()).Any(t => IsHorizontalAt(t, parameters.WallHeight, up: false)), Is.False);
        }

        [Test]
        public void Validate_RejectsTooThickWalls()
        {
            parameters.WallThickness = parameters.CellSize * GenerationParameters.MaxWallThicknessRatio + 0.1f;
            Assert.That(parameters.Validate(out _), Is.False);
        }

        [Test]
        public void Validate_RejectsDoorsThatReachThickWallCorners()
        {
            parameters.WallThickness = 2f;
            parameters.DoorWidthRatio = 0.7f;
            Assert.That(parameters.Validate(out _), Is.False);

            parameters.DoorWidthRatio = 0.2f;
            Assert.That(parameters.Validate(out _), Is.True);
        }

#if CRG_AI_NAVIGATION
        [Test]
        public void NavMesh_WithThickWallsAndCeiling_ConnectsRoomsButNotRoof()
        {
            bool previousPersist = LevelNavMeshBaker.PersistEditorBakes;
            LevelNavMeshBaker.PersistEditorBakes = false;

            try
            {
                parameters.AddCeiling = true;
                parameters.BakeNavMesh = true;
                GameObject level = Generate();
                CRGLevelData data = level.GetComponent<CRGLevelData>();
                Unity.AI.Navigation.NavMeshSurface surface = level.GetComponent<Unity.AI.Navigation.NavMeshSurface>();

                Assert.That(NavMesh.SamplePosition(data.StartRoom.SpawnPoint.position, out NavMeshHit from, 2f, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(data.EndRoom.SpawnPoint.position, out NavMeshHit to, 2f, NavMesh.AllAreas), Is.True);

                NavMeshPath path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

                Vector3 roof = data.StartRoom.SpawnPoint.position + Vector3.up * (parameters.WallHeight + 0.1f);
                Assert.That(NavMesh.SamplePosition(roof, out _, 0.5f, NavMesh.AllAreas), Is.False, "the ceiling's top must not be walkable");

                surface.RemoveData();
            }
            finally
            {
                LevelNavMeshBaker.PersistEditorBakes = previousPersist;
            }
        }
#endif

        // Samples points in every room cell at wall-top height:
        // - at most one top may cover a point;
        // - the band along each of the cell's walls (away from corners) must be covered;
        // - around every corner of the cell that a wall touches, a small disc must be covered (this is where
        //   walls wrap around corners, including through cells without walls of their own there);
        // - nothing may reach much farther than the wall thickness from the nearest wall.
        private void AssertWallTopsCoverWallBands(CellGrid grid, List<Triangle> tops, string context)
        {
            IGridTopology topology = grid.Topology;
            float t = parameters.WallThickness;
            float size = parameters.CellSize;
            float cornerInset = topology.ThickWallCornerInsetPerThickness * t;
            float doorWidth = size * parameters.DoorWidthRatio;
            bool doorReachesTop = parameters.DoorHeight >= parameters.WallHeight;
            WallLayout layout = new WallLayout(grid, t);
            System.Random random = new System.Random(1);

            List<(Vector3 start, Vector3 end)> walls = new List<(Vector3, Vector3)>();
            foreach (GridCell cell in grid.GetAllCells().Where(c => c.IsPartOfRoom()))
            {
                for (int edge = 0; edge < cell.EdgeCount; edge++)
                {
                    if (layout.GetDepth(cell.Coordinate, edge) > 0f)
                        walls.Add(CellGeometry.GetEdgeWorldVertices(topology, cell.Coordinate, edge, size));
                }
            }

            foreach (GridCell cell in grid.GetAllCells().Where(c => c.IsPartOfRoom()))
            {
                CellCoord coord = cell.Coordinate;
                Vector3 center = grid.GetCellCenter(coord);
                float innerRadius = topology.GetInnerRadius(coord, size);
                List<Triangle> nearbyTops = tops.Where(tri => tri.IsNear(center, size * 1.5f)).ToList();
                List<(Vector3 start, Vector3 end)> nearbyWalls = walls
                    .Where(w => FlatDistance(w.start, center) < size * 1.5f || FlatDistance(w.end, center) < size * 1.5f).ToList();
                List<Vector3> walledCorners = Enumerable.Range(0, cell.EdgeCount)
                    .Select(k => center + topology.GetCornerOffset(coord, k, size))
                    .Where(v => nearbyWalls.Any(w => FlatDistance(w.start, v) < 1e-3f || FlatDistance(w.end, v) < 1e-3f))
                    .ToList();

                for (int sample = 0; sample < 60; sample++)
                {
                    Vector3 point = center + new Vector3((float)(random.NextDouble() * 2 - 1) * size, 0f, (float)(random.NextDouble() * 2 - 1) * size);
                    bool inside = Enumerable.Range(0, cell.EdgeCount)
                        .All(e => Vector3.Dot(point - center, topology.GetEdgeNormal(coord, e)) < innerRadius - 0.02f);
                    if (!inside)
                        continue;

                    int coverage = nearbyTops.Count(tri => tri.ContainsXZ(point));
                    Assert.That(coverage, Is.LessThanOrEqualTo(1), $"{context}: wall tops overlap at {point}");

                    bool mustCover = false;
                    for (int edge = 0; edge < cell.EdgeCount; edge++)
                    {
                        float depth = layout.GetDepth(coord, edge);
                        if (depth <= 0f)
                            continue;

                        (Vector3 start, Vector3 end) = CellGeometry.GetEdgeWorldVertices(topology, coord, edge, size);
                        float along = Vector3.Dot(point - start, (end - start).normalized);
                        float inward = -Vector3.Dot(point - start, topology.GetEdgeNormal(coord, edge));
                        bool inOpening = doorReachesTop && cell.GetEdgeFlag(edge).HasFlag(WallFlag.HasDoor) &&
                            !layout.IsExterior(coord, edge) && Mathf.Abs(along - size / 2f) < doorWidth / 2f + 0.05f;

                        if (inward > 0.02f && inward < depth - 0.02f &&
                            along > cornerInset + 0.05f && along < size - cornerInset - 0.05f && !inOpening)
                            mustCover = true;
                    }

                    if (walledCorners.Any(v => FlatDistance(point, v) < 0.4f * (t / 2f) - 0.02f))
                        mustCover = true;

                    float nearestWall = nearbyWalls.Count == 0
                        ? float.MaxValue
                        : nearbyWalls.Min(w => DistanceToSegmentXZ(point, w.start, w.end));

                    if (mustCover)
                        Assert.That(coverage, Is.EqualTo(1), $"{context}: gap in wall tops at {point}");

                    if (nearestWall > 1.2f * t + 0.05f)
                        Assert.That(coverage, Is.Zero, $"{context}: wall top reaches too far at {point}");
                }
            }
        }

        private GameObject Generate()
        {
            return Track(LevelGeometryGenerator.GenerateComplete(parameters));
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private static string LayoutFingerprint(CellGrid grid, bool includeCeilings)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            foreach (Room room in grid.GetAllRooms().OrderBy(r => r.RoomID))
            {
                builder.Append(room.RoomID).Append(':');
                foreach (CellCoord cell in room.Cells)
                    builder.Append(cell).Append(',');
                foreach (KeyValuePair<int, SharedWallData> connection in room.Connections.OrderBy(c => c.Key))
                    builder.Append('>').Append(connection.Key).Append('@').Append(connection.Value.SharedEdges[0]);
                if (includeCeilings)
                    builder.Append(room.HasCeiling ? "C" : "O");
                builder.Append(';');
            }

            return builder.ToString();
        }

        private IGridTopology Topology => GridTopology.Get(gridType);

        // Area of one cell (all cells of a regular tiling are congruent here).
        private float CellArea()
        {
            CellCoord cell = new CellCoord(0, 0);
            int count = Topology.GetEdgeCount(cell);
            float area = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = Topology.GetCornerOffset(cell, i, parameters.CellSize);
                Vector3 b = Topology.GetCornerOffset(cell, (i + 1) % count, parameters.CellSize);
                area += a.x * b.z - b.x * a.z;
            }
            return Mathf.Abs(area) / 2f;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        private static float DistanceToSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 start = new Vector2(a.x, a.z);
            Vector2 segment = new Vector2(b.x, b.z) - start;
            float t = Mathf.Clamp01(Vector2.Dot(p - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(p, start + segment * t);
        }

        private static bool IsHorizontalAt(Triangle triangle, float height, bool up)
        {
            return Mathf.Abs(triangle.A.y - height) < Epsilon && Mathf.Abs(triangle.B.y - height) < Epsilon &&
                   Mathf.Abs(triangle.C.y - height) < Epsilon && (up ? triangle.Normal.y > 0.99f : triangle.Normal.y < -0.99f);
        }

        private static List<Triangle> GetTriangles(GameObject level)
        {
            List<Triangle> triangles = new List<Triangle>();

            foreach (MeshFilter filter in level.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] indices = mesh.triangles;

                for (int i = 0; i < indices.Length; i += 3)
                {
                    triangles.Add(new Triangle(
                        filter.transform.TransformPoint(vertices[indices[i]]),
                        filter.transform.TransformPoint(vertices[indices[i + 1]]),
                        filter.transform.TransformPoint(vertices[indices[i + 2]])));
                }
            }

            return triangles;
        }

        private readonly struct Triangle
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly Vector3 C;
            public readonly Vector3 Normal;
            public readonly float Area;

            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                A = a;
                B = b;
                C = c;
                Vector3 cross = Vector3.Cross(b - a, c - a);
                Normal = cross.normalized;
                Area = cross.magnitude / 2f;
            }

            public bool IsNear(Vector3 point, float radius)
            {
                return FlatDistance(A, point) < radius || FlatDistance(B, point) < radius || FlatDistance(C, point) < radius;
            }

            public bool ContainsXZ(Vector3 point)
            {
                float d1 = Side(point, A, B);
                float d2 = Side(point, B, C);
                float d3 = Side(point, C, A);
                bool hasNegative = d1 < -1e-6f || d2 < -1e-6f || d3 < -1e-6f;
                bool hasPositive = d1 > 1e-6f || d2 > 1e-6f || d3 > 1e-6f;
                return !(hasNegative && hasPositive);
            }

            private static float Side(Vector3 p, Vector3 a, Vector3 b)
            {
                return (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.z - b.z);
            }
        }
    }
}
