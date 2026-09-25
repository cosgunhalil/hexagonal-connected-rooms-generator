using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using CRG.Core;
using CRG.Generation;
using CRG.Geometry;
using CRG.Runtime;

namespace CRG.Tests
{
    public class MixedGeometryTests
    {
        private readonly List<Object> created = new List<Object>();
        private GenerationParameters parameters;

        [SetUp]
        public void SetUp()
        {
            parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 7;
            parameters.TargetRoomCount = 20;
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
        public void MixedLevel_BuildsSingleMeshWithMixedLevelData()
        {
            (MixedLevel level, GameObject levelObject) = Build();

            Assert.That(levelObject.GetComponentsInChildren<ProBuilderMesh>().Length, Is.EqualTo(1));

            CRGLevelData data = levelObject.GetComponent<CRGLevelData>();
            Assert.That(data.IsMixed, Is.True);
            Assert.That(data.Rooms.Count, Is.EqualTo(level.Rooms.Count));
            Assert.That(data.Doors.Count, Is.EqualTo(level.Doors.Count()));

            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                PlacedRoom placed = level.GetRoom(room.RoomID);
                Assert.That(room.GridType, Is.EqualTo(placed.GridType));
                Assert.That(room.Cells, Is.EquivalentTo(placed.Room.Cells));
                Assert.That(room.ConnectedRoomIDs, Is.EquivalentTo(placed.ConnectedRoomIDs));
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void Floors_CoverEveryCellOnce_FacingUp(float thickness)
        {
            parameters.WallThickness = thickness;
            (MixedLevel level, GameObject levelObject) = Build();

            List<Triangle> triangles = GetTriangles(levelObject);
            float cellArea = level.Rooms.Sum(room => room.Room.Cells.Sum(cell => PolygonArea(room.GetWorldCorners(cell))));
            float upArea = triangles.Where(t => t.IsHorizontalAt(0f) && t.Normal.y > 0.99f).Sum(t => t.Area);

            Assert.That(upArea, Is.EqualTo(cellArea).Within(cellArea * 1e-4f));
            Assert.That(triangles.Any(t => t.IsHorizontalAt(0f) && t.Normal.y < -0.99f), Is.False, "a floor faces down");
        }

        [Test]
        public void ThinWalls_BuildEachSharedWallAndDoorOnce()
        {
            (MixedLevel level, GameObject levelObject) = Build();
            int faces = levelObject.GetComponent<ProBuilderMesh>().faceCount;

            HashSet<(int, CellCoord, int)> linked = new HashSet<(int, CellCoord, int)>(
                level.Links.SelectMany(l => new[] { (l.RoomA, l.CellA, l.EdgeA), (l.RoomB, l.CellB, l.EdgeB) }));
            int cells = level.Rooms.Sum(room => room.Room.Cells.Count);
            int outerWalls = level.Rooms.Sum(room => room.GetOuterEdges().Count(e => !linked.Contains((room.RoomID, e.cell, e.edge))));
            int doorSegments = CellGeometry.GetDoorWallSegments(GridTopology.Get(GridType.Square), new CellCoord(0, 0), 0,
                parameters.CellSize, parameters.WallHeight, parameters.CellSize * parameters.DoorWidthRatio, parameters.DoorHeight).Count;

            int expected = cells + 2 * outerWalls + 2 * level.Links.Count(l => !l.IsDoor) + 2 * doorSegments * level.Doors.Count();
            Assert.That(faces, Is.EqualTo(expected));
        }

        [TestCase(1f, 2.5f)]
        [TestCase(2f, 2.5f)]
        [TestCase(1f, 3f)]
        public void ThickWallTops_NeitherOverlapNorLeaveGaps_AndKeepTheirDepth(float thickness, float doorHeight)
        {
            parameters.WallThickness = thickness;
            parameters.DoorHeight = doorHeight;

            for (int seed = 0; seed < 6; seed++)
            {
                parameters.RandomSeed = seed;
                (MixedLevel level, GameObject levelObject) = Build();
                List<Triangle> tops = GetTriangles(levelObject).Where(t => t.IsHorizontalAt(parameters.WallHeight) && t.Normal.y > 0.99f).ToList();
                AssertWallTops(level, tops, $"seed {seed}");
            }
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void Doorways_AreOpen_AndWallsBesideThemAreSolid(float thickness)
        {
            parameters.WallThickness = thickness;
            (_, GameObject levelObject) = Build();
            CRGLevelData data = levelObject.GetComponent<CRGLevelData>();
            Physics.SyncTransforms();

            float reach = thickness + 0.5f;
            Vector3 midHeight = Vector3.up * (parameters.DoorHeight / 2f);

            foreach (CRGLevelData.DoorData door in data.Doors)
            {
                Vector3 center = data.GetDoorCenterLocal(door) + midHeight;
                Vector3 forward = data.GetDoorForwardLocal(door);
                (Vector3 openingStart, Vector3 openingEnd) = data.GetDoorOpeningLocal(door);
                Vector3 alongEdge = (openingEnd - openingStart).normalized;

                Assert.That(Physics.Linecast(center - forward * reach, center + forward * reach), Is.False, $"door {door.RoomA}-{door.RoomB} is blocked");

                Vector3 solid = center - alongEdge * (parameters.CellSize * 0.25f);
                Assert.That(Physics.Linecast(solid - forward * reach, solid + forward * reach), Is.True, $"wall next to door {door.RoomA}-{door.RoomB} is open");
            }
        }

        [Test]
        public void Ceilings_CoverEveryCell()
        {
            parameters.AddCeiling = true;
            parameters.WallThickness = 1f;
            (MixedLevel level, GameObject levelObject) = Build();

            float cellArea = level.Rooms.Sum(room => room.Room.Cells.Sum(cell => PolygonArea(room.GetWorldCorners(cell))));
            float downArea = GetTriangles(levelObject).Where(t => t.IsHorizontalAt(parameters.WallHeight) && t.Normal.y < -0.99f).Sum(t => t.Area);

            Assert.That(downArea, Is.EqualTo(cellArea).Within(cellArea * 1e-4f));
        }

        [Test]
        public void SpawnPoints_AndDoorPrefabs_AreInPlace()
        {
            GameObject template = Track(new GameObject("Door Template"));
            MixedLevel level = new MixedLevelGenerator().Generate(parameters);
            LevelGeometryGenerator generator = LevelGeometryGenerator.FromParameters(level, parameters);
            generator.SetDoorPrefab(template);
            GameObject levelObject = Track(generator.GenerateLevel());
            CRGLevelData data = levelObject.GetComponent<CRGLevelData>();

            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                Assert.That(room.SpawnPoint, Is.Not.Null);
                PlacedRoom placed = level.GetRoom(room.RoomID);
                bool insideOwnRoom = placed.Room.Cells.Any(cell => Inside(placed.GetWorldCorners(cell), room.SpawnPoint.localPosition));
                Assert.That(insideOwnRoom, Is.True, $"spawn point of room {room.RoomID} is outside the room");
            }

            foreach (CRGLevelData.DoorData door in data.Doors)
            {
                RoomLink link = level.GetRoom(door.RoomA).Doors[door.RoomB];
                (Vector3 start, Vector3 end) = level.GetRoom(link.RoomA).GetWorldEdge(link.CellA, link.EdgeA);

                Assert.That(door.DoorObject, Is.Not.Null);
                Assert.That(Vector3.Distance(door.DoorObject.localPosition, (start + end) / 2f), Is.LessThan(1e-3f), "door prefab is not centered on the shared edge");

                PlacedRoom roomB = level.GetRoom(door.RoomB);
                Vector3 towardsB = roomB.GetWorldCenter(link.RoomB == door.RoomB ? link.CellB : link.CellA) - door.DoorObject.localPosition;
                Assert.That(Vector3.Dot(door.DoorObject.forward, towardsB), Is.GreaterThan(0f), "door prefab does not face into RoomB");
            }
        }

        [Test]
        public void RoomRoles_StartAndEndAreSet()
        {
            (_, GameObject levelObject) = Build();
            CRGLevelData data = levelObject.GetComponent<CRGLevelData>();

            Assert.That(data.StartRoom.RoomID, Is.EqualTo(0));
            Assert.That(data.EndRoom, Is.Not.Null);
            Assert.That(data.EndRoom.DistanceFromStart, Is.EqualTo(data.Rooms.Max(room => room.DistanceFromStart)));
            Assert.That(data.Rooms.All(room => room.DistanceFromStart >= 0), Is.True, "a room is unreachable");
        }

        [Test]
        public void SeparateByRoom_MatchesSingleMesh()
        {
            MixedLevel level = new MixedLevelGenerator().Generate(parameters);
            LevelGeometryGenerator generator = LevelGeometryGenerator.FromParameters(level, parameters);
            GameObject byRoom = Track(generator.GenerateLevelSeparateByRoom());
            GameObject single = Track(generator.GenerateLevel());

            Assert.That(byRoom.GetComponentsInChildren<ProBuilderMesh>().Length, Is.EqualTo(level.Rooms.Count));
            Assert.That(byRoom.GetComponentsInChildren<ProBuilderMesh>().Sum(mesh => mesh.faceCount), Is.EqualTo(single.GetComponent<ProBuilderMesh>().faceCount));
        }

        [Test]
        public void GridTypeMixed_IsRoutedToTheMixedGenerator()
        {
            parameters.GridType = GridType.Mixed;
            GameObject levelObject = Track(LevelGeometryGenerator.GenerateComplete(parameters));

            CRGLevelData data = levelObject.GetComponent<CRGLevelData>();
            Assert.That(data.IsMixed, Is.True);
            Assert.That(data.Rooms.Count, Is.EqualTo(parameters.TargetRoomCount));
            Assert.That(data.Rooms.Select(room => room.GridType).Distinct().Count(), Is.GreaterThan(1), "a mixed level should use several grid types");

            GameObject byRoom = Track(LevelGeometryGenerator.CreateFor(parameters).GenerateLevelSeparateByRoom());
            Assert.That(byRoom.GetComponent<CRGLevelData>().IsMixed, Is.True);
        }

        [Test]
        public void SingleGridGenerator_RefusesMixed()
        {
            parameters.GridType = GridType.Mixed;
            Assert.Throws<System.ArgumentException>(() => new CRGGenerator().Generate(parameters));
            Assert.Throws<System.ArgumentException>(() => GridTopology.Get(GridType.Mixed));
        }

        [Test]
        public void Validate_Mixed_NeedsAWeight_AndUsesTheEnabledGridsForDoorClearance()
        {
            parameters.GridType = GridType.Mixed;
            parameters.HexagonWeight = 0f;
            parameters.SquareWeight = 0f;
            parameters.TriangleWeight = 0f;
            parameters.OctagonSquareWeight = 0f;
            Assert.That(parameters.Validate(out _), Is.False, "all weights 0");

            // 2.5 m walls with 1/5 doors fit hexagons (inset 0.866) but not triangles (inset 1.732).
            parameters.WallThickness = 2.5f;
            parameters.HexagonWeight = 1f;
            Assert.That(parameters.Validate(out _), Is.True, "hexagons only");

            parameters.TriangleWeight = 1f;
            Assert.That(parameters.Validate(out _), Is.False, "triangles enabled");
        }

        [Test]
        public void LoopChanceZero_ProducesTree()
        {
            parameters.LoopChance = 0f;

            for (int seed = 0; seed < 10; seed++)
            {
                parameters.RandomSeed = seed;
                MixedLevel level = new MixedLevelGenerator().Generate(parameters);
                Assert.That(level.Doors.Count(), Is.EqualTo(level.Rooms.Count - 1), $"seed {seed}");
            }
        }

        [Test]
        public void ColliderAndSpawnToggles_ApplyToMixedLevels()
        {
            parameters.GridType = GridType.Mixed;
            parameters.AddMeshCollider = false;
            parameters.CreateSpawnPoints = false;
            GameObject levelObject = Track(LevelGeometryGenerator.GenerateComplete(parameters));

            Assert.That(levelObject.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(levelObject.GetComponent<CRGLevelData>().Rooms.All(room => room.SpawnPoint == null), Is.True);

            parameters.AddMeshCollider = true;
            Assert.That(Track(LevelGeometryGenerator.GenerateComplete(parameters)).GetComponent<MeshCollider>(), Is.Not.Null);
        }

#if CRG_AI_NAVIGATION
        [Test]
        public void NavMesh_ConnectsStartRoomToEndRoom()
        {
            bool previousPersist = LevelNavMeshBaker.PersistEditorBakes;
            LevelNavMeshBaker.PersistEditorBakes = false;

            try
            {
                parameters.WallThickness = 1f;
                parameters.AddCeiling = true;
                parameters.BakeNavMesh = true;
                (_, GameObject levelObject) = Build();
                CRGLevelData data = levelObject.GetComponent<CRGLevelData>();
                Unity.AI.Navigation.NavMeshSurface surface = levelObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>();

                Assert.That(NavMesh.SamplePosition(data.StartRoom.SpawnPoint.position, out NavMeshHit from, 2f, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(data.EndRoom.SpawnPoint.position, out NavMeshHit to, 2f, NavMesh.AllAreas), Is.True);

                NavMeshPath path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

                surface.RemoveData();
            }
            finally
            {
                LevelNavMeshBaker.PersistEditorBakes = previousPersist;
            }
        }
#endif

        // Samples every room cell at wall-top height, in level space: at most one top per point; the band along
        // each wall (away from corners) and a disc around walled corners covered; points deeper than every wall's
        // intended depth left open (walls towards other rooms are half thick on each side); nothing far from walls.
        private void AssertWallTops(MixedLevel level, List<Triangle> tops, string context)
        {
            float t = parameters.WallThickness;
            float size = parameters.CellSize;
            HashSet<(int, CellCoord, int)> linked = new HashSet<(int, CellCoord, int)>(
                level.Links.SelectMany(l => new[] { (l.RoomA, l.CellA, l.EdgeA), (l.RoomB, l.CellB, l.EdgeB) }));

            Dictionary<int, WallLayout> layouts = new Dictionary<int, WallLayout>();
            List<(Vector3 a, Vector3 b)> walls = new List<(Vector3, Vector3)>();
            foreach (PlacedRoom room in level.Rooms)
            {
                int id = room.RoomID;
                WallLayout layout = new WallLayout(room.Grid, t, (cell, edge) => !linked.Contains((id, cell, edge)));
                layouts[id] = layout;
                foreach ((CellCoord cell, int edge) in room.GetOuterEdges())
                {
                    if (layout.GetDepth(cell, edge) > 0f)
                        walls.Add(room.GetWorldEdge(cell, edge));
                }
            }

            System.Random random = new System.Random(1);
            foreach (PlacedRoom room in level.Rooms)
            {
                WallLayout layout = layouts[room.RoomID];
                float inset = room.Topology.ThickWallCornerInsetPerThickness * t;

                foreach (CellCoord cell in room.Room.Cells)
                {
                    GridCell gridCell = room.Grid.GetCell(cell);
                    Vector3 center = room.GetWorldCenter(cell);
                    Vector3[] corners = room.GetWorldCorners(cell);
                    List<Triangle> nearTops = tops.Where(tri => Flat(tri.A - center).magnitude < size * 1.6f).ToList();
                    List<(Vector3 a, Vector3 b)> nearWalls = walls.Where(w => Flat(w.a - center).magnitude < size * 1.6f || Flat(w.b - center).magnitude < size * 1.6f).ToList();
                    List<Vector3> walledCorners = corners.Where(v => nearWalls.Any(w => Flat(w.a - v).magnitude < 1e-2f || Flat(w.b - v).magnitude < 1e-2f)).ToList();

                    for (int sample = 0; sample < 50; sample++)
                    {
                        Vector3 point = center + new Vector3((float)(random.NextDouble() * 2 - 1) * size, 0f, (float)(random.NextDouble() * 2 - 1) * size);
                        if (!Inside(corners, point, 0.02f))
                            continue;

                        int coverage = nearTops.Count(tri => tri.ContainsXZ(point));
                        Assert.That(coverage, Is.LessThanOrEqualTo(1), $"{context}: wall tops overlap in room {room.RoomID} ({room.GridType})");

                        bool mustCover = walledCorners.Any(v => Flat(point - v).magnitude < 0.4f * (t / 2f) - 0.02f);
                        bool mustBeOpen = !walledCorners.Any(v => Flat(point - v).magnitude < 1.3f * t + 0.05f);

                        for (int edge = 0; edge < gridCell.EdgeCount; edge++)
                        {
                            float depth = layout.GetDepth(cell, edge);
                            if (depth <= 0f)
                                continue;

                            (Vector3 start, Vector3 end) = room.GetWorldEdge(cell, edge);
                            Vector3 direction = Flat(end - start).normalized;
                            Vector3 inward = new Vector3(-direction.z, 0f, direction.x);
                            float along = Vector3.Dot(Flat(point - start), direction);
                            float perpendicular = Vector3.Dot(Flat(point - start), inward);
                            bool inOpening = gridCell.GetEdgeFlag(edge).HasFlag(WallFlag.HasDoor) && parameters.DoorHeight >= parameters.WallHeight &&
                                Mathf.Abs(along - size / 2f) < size * parameters.DoorWidthRatio / 2f + 0.05f;

                            if (perpendicular > 0.02f && perpendicular < depth - 0.02f && along > inset + 0.05f && along < size - inset - 0.05f && !inOpening)
                                mustCover = true;

                            if (perpendicular < depth + 0.05f)
                                mustBeOpen = false;
                        }

                        float nearest = nearWalls.Count == 0 ? float.MaxValue : nearWalls.Min(w => DistanceToSegmentXZ(point, w.a, w.b));

                        if (mustCover)
                            Assert.That(coverage, Is.EqualTo(1), $"{context}: gap in wall tops in room {room.RoomID} ({room.GridType}) at {point}");
                        if (mustBeOpen)
                            Assert.That(coverage, Is.Zero, $"{context}: wall deeper than intended in room {room.RoomID} ({room.GridType}) at {point}");
                        if (nearest > 1.2f * t + 0.05f)
                            Assert.That(coverage, Is.Zero, $"{context}: wall top too far from walls in room {room.RoomID} ({room.GridType}) at {point}");
                    }
                }
            }
        }

        private (MixedLevel, GameObject) Build()
        {
            MixedLevel level = new MixedLevelGenerator().Generate(parameters);
            GameObject levelObject = Track(LevelGeometryGenerator.FromParameters(level, parameters).GenerateLevel());
            return (level, levelObject);
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static float PolygonArea(Vector3[] corners)
        {
            float area = 0f;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 a = corners[i];
                Vector3 b = corners[(i + 1) % corners.Length];
                area += a.x * b.z - b.x * a.z;
            }
            return Mathf.Abs(area) / 2f;
        }

        // Inside a counter-clockwise convex polygon, at least margin away from its edges.
        private static bool Inside(Vector3[] corners, Vector3 point, float margin = 0f)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 a = corners[i];
                Vector3 direction = Flat(corners[(i + 1) % corners.Length] - a).normalized;
                if (direction.x * (point.z - a.z) - direction.z * (point.x - a.x) < margin)
                    return false;
            }
            return true;
        }

        private static float DistanceToSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 start = new Vector2(a.x, a.z);
            Vector2 segment = new Vector2(b.x, b.z) - start;
            float s = Mathf.Clamp01(Vector2.Dot(p - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(p, start + segment * s);
        }

        private static List<Triangle> GetTriangles(GameObject level)
        {
            List<Triangle> triangles = new List<Triangle>();
            foreach (MeshFilter filter in level.GetComponentsInChildren<MeshFilter>())
            {
                Vector3[] vertices = filter.sharedMesh.vertices;
                int[] indices = filter.sharedMesh.triangles;
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

            public bool IsHorizontalAt(float height)
            {
                return Mathf.Abs(A.y - height) < 1e-3f && Mathf.Abs(B.y - height) < 1e-3f && Mathf.Abs(C.y - height) < 1e-3f;
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
