using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using HRCG.Core;
using HRCG.Generation;
using HRCG.Geometry;
using HRCG.Runtime;

namespace HRCG.Tests
{
    public class ThickWallAndCeilingTests
    {
        private const float Epsilon = 1e-3f;

        private readonly List<Object> created = new List<Object>();
        private GenerationParameters parameters;

        [SetUp]
        public void SetUp()
        {
            parameters = GenerationParameters.CreateDefault();
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
            parameters.MinHexagonsPerRoom = 1;
            parameters.MaxHexagonsPerRoom = 1;
            parameters.TargetRoomCount = 1;

            List<Triangle> tops = GetTriangles(Generate()).Where(t => IsHorizontalAt(t, parameters.WallHeight, up: true)).ToList();

            // All six walls are exterior (full thickness), so the tops fill the ring between the hex and a hex inset by t.
            float outer = parameters.HexSize;
            float inner = outer - 2f * parameters.WallThickness / Mathf.Sqrt(3f);
            float expected = HexArea(outer) - HexArea(inner);

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
                HexGrid grid = new HRCGGenerator().Generate(parameters);
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
            HRCGLevelData data = level.GetComponent<HRCGLevelData>();
            Physics.SyncTransforms();

            float reach = thickness + 0.5f;
            Vector3 midHeight = Vector3.up * (parameters.DoorHeight / 2f);

            foreach (HRCGLevelData.DoorData door in data.Doors)
            {
                Vector3 center = level.transform.TransformPoint(data.GetDoorCenterLocal(door)) + midHeight;
                Vector3 forward = level.transform.TransformDirection(data.GetDoorForwardLocal(door));
                (Vector3 openingStart, Vector3 openingEnd) = data.GetDoorOpeningLocal(door);
                Vector3 alongEdge = level.transform.TransformDirection((openingEnd - openingStart).normalized);

                Assert.That(Physics.Linecast(center - forward * reach, center + forward * reach), Is.False,
                    $"door {door.RoomA}-{door.RoomB} is blocked");

                // 35% of the edge length from the door center is solid wall, well clear of the corners.
                Vector3 solid = center - alongEdge * (parameters.HexSize * 0.35f);
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
            int cells = level.GetComponent<HRCGLevelData>().Rooms.Sum(room => room.Cells.Count);

            List<Triangle> ceiling = GetTriangles(level).Where(t => IsHorizontalAt(t, parameters.WallHeight, up: false)).ToList();
            float expected = cells * HexArea(parameters.HexSize);

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
            Assert.That(level.GetComponent<HRCGLevelData>().Rooms.Any(room => room.HasCeiling), Is.False);
        }

        [Test]
        public void CeilingChanceOne_CoversEveryRoom()
        {
            parameters.AddCeiling = true;
            parameters.CeilingChance = 1f;

            Assert.That(Generate().GetComponent<HRCGLevelData>().Rooms.All(room => room.HasCeiling), Is.True);
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
                List<HRCGLevelData.RoomData> rooms = level.GetComponent<HRCGLevelData>().Rooms.ToList();

                int coveredCells = rooms.Where(room => room.HasCeiling).Sum(room => room.Cells.Count);
                float expected = coveredCells * HexArea(parameters.HexSize);
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
                string layoutWithAllCeilings = LayoutFingerprint(new HRCGGenerator().Generate(parameters), includeCeilings: false);

                parameters.CeilingChance = 0.4f;
                HexGrid first = new HRCGGenerator().Generate(parameters);
                HexGrid second = new HRCGGenerator().Generate(parameters);

                Assert.That(LayoutFingerprint(first, includeCeilings: false), Is.EqualTo(layoutWithAllCeilings), $"seed {seed}: layout changed");
                Assert.That(LayoutFingerprint(first, includeCeilings: true), Is.EqualTo(LayoutFingerprint(second, includeCeilings: true)), $"seed {seed}: ceilings differ");
            }
        }

        [Test]
        public void NoCeiling_ByDefault()
        {
            parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 42;

            Assert.That(GetTriangles(Generate()).Any(t => IsHorizontalAt(t, parameters.WallHeight, up: false)), Is.False);
        }

        [Test]
        public void Validate_RejectsTooThickWalls()
        {
            parameters.WallThickness = parameters.HexSize * GenerationParameters.MaxWallThicknessRatio + 0.1f;
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

#if HRCG_AI_NAVIGATION
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
                HRCGLevelData data = level.GetComponent<HRCGLevelData>();
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

        // Samples points in every room cell at wall-top height: at most one top may cover a point, the band along
        // each wall (away from corners) must be covered, the corners of walled edges must be covered, and nothing
        // may reach much farther than the wall thickness.
        private void AssertWallTopsCoverWallBands(HexGrid grid, List<Triangle> tops, string context)
        {
            float t = parameters.WallThickness;
            float size = parameters.HexSize;
            float innerRadius = HexMath.GetInnerRadius(size);
            float doorWidth = size * parameters.DoorWidthRatio;
            bool doorReachesTop = parameters.DoorHeight >= parameters.WallHeight;
            System.Random random = new System.Random(1);

            foreach (HexCell cell in grid.GetAllCells().Where(c => c.IsPartOfRoom()))
            {
                Vector3 center = cell.Coordinate.ToWorldPosition(size);
                List<Triangle> nearby = tops.Where(tri => tri.IsNear(center, size * 1.05f)).ToList();

                for (int sample = 0; sample < 60; sample++)
                {
                    Vector3 point = center + new Vector3((float)(random.NextDouble() * 2 - 1) * size, 0f, (float)(random.NextDouble() * 2 - 1) * size);
                    bool inside = Enumerable.Range(0, 6).All(e => Vector3.Dot(point - center, HexMath.GetEdgeNormal(e)) < innerRadius - 0.02f);
                    if (!inside)
                        continue;

                    int coverage = nearby.Count(tri => tri.ContainsXZ(point));
                    Assert.That(coverage, Is.LessThanOrEqualTo(1), $"{context}: wall tops overlap at {point}");

                    bool mustCover = false;
                    bool anyWall = false;
                    float nearestWall = float.MaxValue;

                    for (int edge = 0; edge < 6; edge++)
                    {
                        WallFlag flag = cell.GetEdgeFlag(edge);
                        if (!flag.HasFlag(WallFlag.Wall) && !flag.HasFlag(WallFlag.HasDoor))
                            continue;

                        HexCell neighbor = grid.GetCell(cell.Coordinate.GetNeighbor(edge));
                        bool exterior = neighbor == null || !neighbor.IsPartOfRoom();
                        float depth = exterior ? t : t / 2f;
                        anyWall = true;

                        (Vector3 start, Vector3 end) = HexMath.GetEdgeVertices(edge, size);
                        start += center;
                        end += center;
                        Vector3 direction = (end - start).normalized;
                        float along = Vector3.Dot(point - start, direction);
                        float inward = -Vector3.Dot(point - start, HexMath.GetEdgeNormal(edge));
                        nearestWall = Mathf.Min(nearestWall, DistanceToSegmentXZ(point, start, end));

                        bool inOpening = doorReachesTop && flag.HasFlag(WallFlag.HasDoor) &&
                            Mathf.Abs(along - size / 2f) < doorWidth / 2f + 0.05f;

                        if (inward > 0.02f && inward < depth - 0.02f && along > t + 0.05f && along < size - t - 0.05f && !inOpening)
                            mustCover = true;

                        if (FlatDistance(point, start) < 0.5f * depth - 0.02f || FlatDistance(point, end) < 0.5f * depth - 0.02f)
                            mustCover = true;
                    }

                    if (mustCover)
                        Assert.That(coverage, Is.EqualTo(1), $"{context}: gap in wall tops at {point}");

                    if (!anyWall || nearestWall > 1.2f * t + 0.05f)
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

        private static string LayoutFingerprint(HexGrid grid, bool includeCeilings)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();

            foreach (Room room in grid.GetAllRooms().OrderBy(r => r.RoomID))
            {
                builder.Append(room.RoomID).Append(':');
                foreach (AxialCoord cell in room.Cells)
                    builder.Append(cell).Append(',');
                foreach (KeyValuePair<int, SharedWallData> connection in room.Connections.OrderBy(c => c.Key))
                    builder.Append('>').Append(connection.Key).Append('@').Append(connection.Value.SharedEdges[0]);
                if (includeCeilings)
                    builder.Append(room.HasCeiling ? "C" : "O");
                builder.Append(';');
            }

            return builder.ToString();
        }

        private static float HexArea(float circumradius)
        {
            return 1.5f * Mathf.Sqrt(3f) * circumradius * circumradius;
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
