using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using CRG.Building;
using CRG.Core;
using CRG.Generation;
using CRG.Geometry;
using CRG.Runtime;

namespace CRG.Tests
{
    public class HandBuiltGeometryTests
    {
        private readonly List<Object> created = new List<Object>();
        private GenerationParameters parameters;

        [SetUp]
        public void SetUp()
        {
            parameters = GenerationParameters.CreateDefault();
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

        // A random layout of every shape, with shared edges set to open, door or wall (built into layout if given).
        private static LevelLayout RandomLayout(int seed, double openChance = 0.45, int cellCount = 25, LevelLayout layout = null)
        {
            System.Random random = new System.Random(seed);
            layout = layout ?? new LevelLayout();
            layout.AddFirstCell(CellShapes.All[random.Next(CellShapes.All.Length)]);

            for (int attempt = 0; attempt < 400 && layout.Cells.Count < cellCount; attempt++)
            {
                List<LayoutCell> cells = layout.Cells.ToList();
                LayoutCell cell = cells[random.Next(cells.Count)];
                layout.Attach(cell.id, random.Next(CellShapes.GetEdgeCount(cell.shape)), CellShapes.All[random.Next(CellShapes.All.Length)]);
            }

            foreach (LayoutLink link in layout.Links)
            {
                double roll = random.NextDouble();
                link.state = roll < openChance ? EdgeState.Open : roll < openChance + (1 - openChance) / 2 ? EdgeState.Door : EdgeState.Wall;
            }

            return layout;
        }

        // A square and a triangle joined by an open edge: their corners meet at 90 and 60 degrees.
        private static LevelLayout SquareAndTriangle()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell square = layout.AddFirstCell(CellShape.Square);
            layout.Attach(square.id, 0, CellShape.Triangle);
            layout.Links[0].state = EdgeState.Open;
            return layout;
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void Floors_CoverEveryCellOnce_FacingUp(float thickness)
        {
            parameters.WallThickness = thickness;
            LevelLayout layout = RandomLayout(1);
            List<Triangle> triangles = GetTriangles(Build(layout));

            float cellArea = layout.Cells.Sum(cell => PolygonArea(WorldCorners(layout, cell)));
            float upArea = triangles.Where(t => t.IsHorizontalAt(0f) && t.Normal.y > 0.99f).Sum(t => t.Area);

            Assert.That(upArea, Is.EqualTo(cellArea).Within(cellArea * 1e-4f));
            Assert.That(triangles.Any(t => t.IsHorizontalAt(0f) && t.Normal.y < -0.99f), Is.False, "a floor faces down");
        }

        [Test]
        public void ThinWalls_BuildEachSharedWallAndDoorOnce_AndNothingOnOpenEdges()
        {
            for (int seed = 0; seed < 5; seed++)
            {
                LevelLayout layout = RandomLayout(seed);
                int faces = Build(layout).GetComponent<ProBuilderMesh>().faceCount;

                int outer = layout.Cells.Sum(cell => Enumerable.Range(0, CellShapes.GetEdgeCount(cell.shape)).Count(edge => layout.IsOuterEdge(cell.id, edge)));
                int walls = layout.Links.Count(link => link.state == EdgeState.Wall);
                int doors = layout.Links.Count(link => link.state == EdgeState.Door);
                int doorSegments = CellGeometry.GetDoorWallSegments(GridTopology.Get(GridType.Square), new CellCoord(0, 0), 0,
                    parameters.CellSize, parameters.WallHeight, parameters.CellSize * parameters.DoorWidthRatio, parameters.DoorHeight).Count;

                Assert.That(faces, Is.EqualTo(layout.Cells.Count + 2 * outer + 2 * walls + 2 * doorSegments * doors), $"seed {seed}");
            }
        }

        [TestCase(1f, 2.5f)]
        [TestCase(2f, 2.5f)]
        [TestCase(1f, 3f)]
        public void ThickWallTops_NeitherOverlapNorLeaveGaps_AcrossDifferentShapes(float thickness, float doorHeight)
        {
            parameters.WallThickness = thickness;
            parameters.DoorHeight = doorHeight;

            List<(string, LevelLayout)> layouts = new List<(string, LevelLayout)> { ("square + triangle", SquareAndTriangle()) };
            for (int seed = 0; seed < 6; seed++)
                layouts.Add(($"seed {seed}", RandomLayout(seed, seed % 2 == 0 ? 0.45 : 0.8)));

            foreach ((string name, LevelLayout layout) in layouts)
            {
                List<Triangle> triangles = GetTriangles(Build(layout));
                Assert.That(triangles.Any(t => t.IsHorizontalAt(parameters.WallHeight) && t.Normal.y < -0.99f), Is.False,
                    $"{name}: a wall top is folded over");

                List<Triangle> tops = triangles.Where(t => t.IsHorizontalAt(parameters.WallHeight) && t.Normal.y > 0.99f).ToList();
                AssertWallTops(layout, tops, name);
            }
        }

        [Test]
        public void Ceilings_CoverEveryCell()
        {
            parameters.AddCeiling = true;
            parameters.WallThickness = 1f;
            LevelLayout layout = RandomLayout(2);

            float cellArea = layout.Cells.Sum(cell => PolygonArea(WorldCorners(layout, cell)));
            float downArea = GetTriangles(Build(layout)).Where(t => t.IsHorizontalAt(parameters.WallHeight) && t.Normal.y < -0.99f).Sum(t => t.Area);

            Assert.That(downArea, Is.EqualTo(cellArea).Within(cellArea * 1e-4f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RoomCeilings_FollowEachRoomsSetting(bool levelCeilings)
        {
            parameters.AddCeiling = levelCeilings;
            parameters.WallThickness = 1f;
            LevelLayout layout = RandomLayout(8, 0.6);
            LayoutRooms rooms = layout.GetRooms();
            for (int room = 0; room < rooms.Rooms.Count; room++)
                rooms.Settings[room].roomCeiling = (RoomCeiling)(room % 3);

            GameObject level = Build(layout);
            float expected = 0f;
            for (int room = 0; room < rooms.Rooms.Count; room++)
            {
                if (rooms.HasCeiling(room, levelCeilings))
                    expected += rooms.Rooms[room].Sum(id => PolygonArea(WorldCorners(layout, layout.GetCell(id))));
            }

            float downArea = GetTriangles(level).Where(t => t.IsHorizontalAt(parameters.WallHeight) && t.Normal.y < -0.99f).Sum(t => t.Area);
            Assert.That(downArea, Is.EqualTo(expected).Within(1e-2f + expected * 1e-4f));

            CRGLevelData data = level.GetComponent<CRGLevelData>();
            foreach (CRGLevelData.RoomData room in data.Rooms)
                Assert.That(room.HasCeiling, Is.EqualTo(rooms.HasCeiling(room.RoomID, levelCeilings)), $"room {room.RoomID}");
        }

        [Test]
        public void LevelData_KeepsRoomNamesTagsAndChosenRoles()
        {
            LevelLayout layout = RandomLayout(9, 0.3);
            foreach (LayoutLink link in layout.Links.Where(link => link.state == EdgeState.Wall))
                link.state = EdgeState.Door;

            LayoutRooms rooms = layout.GetRooms();
            Assert.That(rooms.Rooms.Count, Is.GreaterThan(3));
            rooms.Settings[1].roomName = "Armory";
            rooms.Settings[2].roomTag = "Boss";
            rooms.Settings[3].roomTag = "Boss";
            layout.StartCell = rooms.Rooms[2][0];
            layout.EndCell = rooms.Rooms[1][0];

            CRGLevelData data = Build(layout).GetComponent<CRGLevelData>();
            Assert.That(data.FindRoom("Armory").RoomID, Is.EqualTo(1));
            Assert.That(data.FindRoom("Nowhere"), Is.Null);
            Assert.That(data.GetRoomsWithTag("Boss").Select(room => room.RoomID), Is.EquivalentTo(new[] { 2, 3 }));
            Assert.That(data.StartRoom.RoomID, Is.EqualTo(2));
            Assert.That(data.EndRoom.RoomID, Is.EqualTo(1));
            Assert.That(data.StartRoom.DistanceFromStart, Is.Zero);
            Assert.That(data.GetRoom(1).SpawnPoint.name, Is.EqualTo("Spawn_Room_1_Armory"));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void Doorways_AreOpen_AndWallsBesideThemAreSolid(float thickness)
        {
            parameters.WallThickness = thickness;
            CRGLevelData data = Build(RandomLayout(3)).GetComponent<CRGLevelData>();
            Physics.SyncTransforms();

            float reach = thickness + 0.5f;
            Vector3 midHeight = Vector3.up * (parameters.DoorHeight / 2f);
            Assert.That(data.Doors.Count, Is.GreaterThan(0));

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
        public void LevelData_DescribesTheLayout()
        {
            LevelLayout layout = RandomLayout(4);
            LayoutRooms rooms = layout.GetRooms();
            CRGLevelData data = Build(layout).GetComponent<CRGLevelData>();

            Assert.That(data.IsHandBuilt, Is.True);
            Assert.That(data.IsMixed, Is.False);
            Assert.That(data.Rooms.Count, Is.EqualTo(rooms.Rooms.Count));
            Assert.That(data.Doors.Count, Is.EqualTo(layout.Links.Count(link => link.state == EdgeState.Door)));

            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                Assert.That(room.Cells.Select(cell => cell.x), Is.EqualTo(rooms.Rooms[room.RoomID]), $"cells of room {room.RoomID}");

                foreach (CellCoord cell in room.Cells)
                {
                    LayoutCell layoutCell = layout.GetCell(cell.x);
                    Vector3[] corners = WorldCorners(layout, layoutCell);
                    Assert.That(data.GetCellEdgeCount(room, cell), Is.EqualTo(corners.Length));
                    Assert.That(Vector3.Distance(data.GetCellLocalPosition(room, cell), layout.GetCenter(layoutCell) * parameters.CellSize), Is.LessThan(1e-3f));
                    for (int corner = 0; corner < corners.Length; corner++)
                        Assert.That(Vector3.Distance(data.GetCellCornerLocal(room, cell, corner), corners[corner]), Is.LessThan(1e-3f));
                }

                Assert.That(room.DistanceFromStart < 0, Is.EqualTo(rooms.Unreachable.Contains(room.RoomID)), $"reachability of room {room.RoomID}");
            }

            foreach (CRGLevelData.DoorData door in data.Doors)
            {
                LayoutCell cell = layout.GetCell(door.CellA.x);
                LayoutLink link = layout.GetLink(cell.id, door.EdgeA);
                Assert.That(link.state, Is.EqualTo(EdgeState.Door));
                Assert.That(rooms.RoomOfCell[cell.id], Is.EqualTo(door.RoomA));

                (Vector3 start, Vector3 end) = layout.GetEdge(cell, door.EdgeA);
                Vector3 middle = (start + end) / 2f * parameters.CellSize;
                Assert.That(Vector3.Distance(data.GetDoorCenterLocal(door), middle), Is.LessThan(1e-3f));

                Vector3 intoB = layout.GetCenter(layout.GetCell(link.GetOther(cell.id))) * parameters.CellSize - middle;
                Assert.That(Vector3.Dot(data.GetDoorForwardLocal(door), intoB), Is.GreaterThan(0f), "doors point into room B");
            }
        }

        [Test]
        public void SpawnPoints_AreInsideTheirRooms()
        {
            LevelLayout layout = RandomLayout(5, 0.7);
            CRGLevelData data = Build(layout).GetComponent<CRGLevelData>();

            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                Assert.That(room.SpawnPoint, Is.Not.Null);
                bool inside = room.Cells.Any(cell => Inside(WorldCorners(layout, layout.GetCell(cell.x)), room.SpawnPoint.localPosition));
                Assert.That(inside, Is.True, $"spawn point of room {room.RoomID} is outside the room");
            }
        }

        [Test]
        public void SeparateRooms_MatchTheSingleMesh()
        {
            LevelLayout layout = RandomLayout(6, 0.6);
            LevelGeometryGenerator generator = LevelGeometryGenerator.FromParameters(layout, parameters);
            GameObject byRoom = Track(generator.GenerateLevelSeparateByRoom());
            GameObject single = Track(generator.GenerateLevel());

            Assert.That(byRoom.GetComponentsInChildren<ProBuilderMesh>().Length, Is.EqualTo(layout.GetRooms().Rooms.Count));
            Assert.That(byRoom.GetComponentsInChildren<ProBuilderMesh>().Sum(mesh => mesh.faceCount), Is.EqualTo(single.GetComponent<ProBuilderMesh>().faceCount));
        }

        [Test]
        public void Validation_UsesTheShapesInTheLayout()
        {
            // 2.5 m walls with 1/5 doors fit hexagons (inset 0.577) but not triangles (inset 1.732).
            parameters.WallThickness = 2.5f;
            LevelLayout layout = new LevelLayout();
            LayoutCell hex = layout.AddFirstCell(CellShape.Hexagon);
            Assert.That(parameters.ValidateHandBuilt(layout.GetCornerInsetPerThickness(), out _), Is.True, "hexagons only");

            layout.Attach(hex.id, 0, CellShape.Triangle);
            Assert.That(parameters.ValidateHandBuilt(layout.GetCornerInsetPerThickness(), out string error), Is.False, "with a triangle");
            StringAssert.Contains("Doors are too wide", error);
        }

        [Test]
        public void Builder_BakesAtItsPosition_AndManagesItsPreview()
        {
            GameObject builderObject = Track(new GameObject("Builder"));
            builderObject.transform.SetPositionAndRotation(new Vector3(40f, 0f, -20f), Quaternion.Euler(0f, 30f, 0f));
            CRGLevelBuilder builder = builderObject.AddComponent<CRGLevelBuilder>();
            builder.parameters = parameters;

            Assert.That(builder.Bake(false), Is.Null, "an empty layout doesn't bake");

            LayoutCell first = builder.Layout.AddFirstCell(CellShape.Octagon);
            builder.Layout.Attach(first.id, 0, CellShape.Square);

            builder.RebuildPreview();
            Assert.That(builderObject.transform.childCount, Is.EqualTo(1), "the preview is a child of the builder");
            Assert.That(builderObject.transform.GetChild(0).gameObject.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave), "the preview is never saved");

            builder.showPreview = false;
            builder.RebuildPreview();
            Assert.That(builderObject.transform.childCount, Is.Zero, "a hidden preview is removed");

            GameObject level = Track(builder.Bake(false));
            Assert.That(level, Is.Not.Null);
            Assert.That(level.transform.position, Is.EqualTo(builderObject.transform.position));
            Assert.That(Quaternion.Angle(level.transform.rotation, builderObject.transform.rotation), Is.LessThan(0.01f));
            Assert.That(level.GetComponent<CRGLevelData>().IsHandBuilt, Is.True);
            Assert.That(level.GetComponent<CRGLevelData>().Rooms.Count, Is.EqualTo(2));

            GameObject byRoom = Track(builder.Bake(true));
            Assert.That(byRoom.GetComponentsInChildren<ProBuilderMesh>().Length, Is.EqualTo(2));
        }

#if CRG_AI_NAVIGATION
        [Test]
        public void NavMesh_ConnectsStartRoomToEndRoom_WhenBakedAwayFromTheOrigin()
        {
            bool previousPersist = LevelNavMeshBaker.PersistEditorBakes;
            LevelNavMeshBaker.PersistEditorBakes = false;

            try
            {
                parameters.WallThickness = 1f;
                parameters.AddCeiling = true;
                parameters.BakeNavMesh = true;

                GameObject builderObject = Track(new GameObject("Builder"));
                builderObject.transform.SetPositionAndRotation(new Vector3(60f, 0f, 25f), Quaternion.Euler(0f, 45f, 0f));
                CRGLevelBuilder builder = builderObject.AddComponent<CRGLevelBuilder>();
                builder.parameters = parameters;
                builder.showPreview = false;

                // Every shared edge a door or open, so every room is reachable.
                RandomLayout(7, 0.5, 25, builder.Layout);
                foreach (LayoutLink link in builder.Layout.Links.Where(link => link.state == EdgeState.Wall))
                    link.state = EdgeState.Door;

                GameObject level = Track(builder.Bake(false));
                CRGLevelData data = level.GetComponent<CRGLevelData>();
                Unity.AI.Navigation.NavMeshSurface surface = level.GetComponent<Unity.AI.Navigation.NavMeshSurface>();

                Assert.That(NavMesh.SamplePosition(data.StartRoom.SpawnPoint.position, out NavMeshHit from, 2f, NavMesh.AllAreas), Is.True, "no NavMesh at the start room");
                Assert.That(NavMesh.SamplePosition(data.EndRoom.SpawnPoint.position, out NavMeshHit to, 2f, NavMesh.AllAreas), Is.True, "no NavMesh at the end room");

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

        // Samples every cell at wall-top height: at most one top per point; the band along each wall (away from
        // corners) and a disc around walled corners covered; points deeper than every wall's intended depth left
        // open (walls towards other cells are half thick on each side); nothing far from walls.
        private void AssertWallTops(LevelLayout layout, List<Triangle> tops, string context)
        {
            float t = parameters.WallThickness;
            float size = parameters.CellSize;
            LayoutWallLayout walls = new LayoutWallLayout(layout, t);

            List<(Vector3 a, Vector3 b)> wallEdges = new List<(Vector3, Vector3)>();
            foreach (LayoutCell cell in layout.Cells)
            {
                for (int edge = 0; edge < CellShapes.GetEdgeCount(cell.shape); edge++)
                {
                    if (walls.GetDepth(cell.id, edge) > 0f)
                        wallEdges.Add(WorldEdge(layout, cell, edge));
                }
            }

            System.Random random = new System.Random(1);
            foreach (LayoutCell cell in layout.Cells)
            {
                Vector3 center = layout.GetCenter(cell) * size;
                Vector3[] corners = WorldCorners(layout, cell);
                float inset = CellShapes.GetTopology(cell.shape).ThickWallCornerInsetPerThickness * t;
                List<Triangle> nearTops = tops.Where(tri => Flat(tri.A - center).magnitude < size * 1.6f).ToList();
                List<(Vector3 a, Vector3 b)> nearWalls = wallEdges.Where(w => Flat(w.a - center).magnitude < size * 1.6f || Flat(w.b - center).magnitude < size * 1.6f).ToList();
                List<Vector3> walledCorners = corners.Where(v => nearWalls.Any(w => Flat(w.a - v).magnitude < 1e-2f || Flat(w.b - v).magnitude < 1e-2f)).ToList();

                for (int sample = 0; sample < 60; sample++)
                {
                    Vector3 point = center + new Vector3((float)(random.NextDouble() * 2 - 1) * size, 0f, (float)(random.NextDouble() * 2 - 1) * size);
                    if (!Inside(corners, point, 0.02f))
                        continue;

                    int coverage = nearTops.Count(tri => tri.ContainsXZ(point));
                    Assert.That(coverage, Is.LessThanOrEqualTo(1), $"{context}: wall tops overlap in cell {cell.id} ({cell.shape})");

                    bool mustCover = walledCorners.Any(v => Flat(point - v).magnitude < 0.4f * (t / 2f) - 0.02f);
                    bool mustBeOpen = !walledCorners.Any(v => Flat(point - v).magnitude < 1.3f * t + 0.05f);

                    for (int edge = 0; edge < corners.Length; edge++)
                    {
                        float depth = walls.GetDepth(cell.id, edge);
                        if (depth <= 0f)
                            continue;

                        (Vector3 start, Vector3 end) = WorldEdge(layout, cell, edge);
                        Vector3 direction = Flat(end - start).normalized;
                        Vector3 inward = new Vector3(-direction.z, 0f, direction.x);
                        float along = Vector3.Dot(Flat(point - start), direction);
                        float perpendicular = Vector3.Dot(Flat(point - start), inward);
                        LayoutLink link = layout.GetLink(cell.id, edge);
                        bool inOpening = link != null && link.state == EdgeState.Door && parameters.DoorHeight >= parameters.WallHeight &&
                            Mathf.Abs(along - size / 2f) < size * parameters.DoorWidthRatio / 2f + 0.05f;

                        if (perpendicular > 0.02f && perpendicular < depth - 0.02f && along > inset + 0.05f && along < size - inset - 0.05f && !inOpening)
                            mustCover = true;

                        if (perpendicular < depth + 0.05f)
                            mustBeOpen = false;
                    }

                    float nearest = nearWalls.Count == 0 ? float.MaxValue : nearWalls.Min(w => DistanceToSegmentXZ(point, w.a, w.b));

                    if (mustCover)
                        Assert.That(coverage, Is.EqualTo(1), $"{context}: gap in wall tops in cell {cell.id} ({cell.shape}) at {point}");
                    if (mustBeOpen)
                        Assert.That(coverage, Is.Zero, $"{context}: wall deeper than intended in cell {cell.id} ({cell.shape}) at {point}");
                    if (nearest > 1.2f * t + 0.05f)
                        Assert.That(coverage, Is.Zero, $"{context}: wall top too far from walls in cell {cell.id} ({cell.shape}) at {point}");
                }
            }
        }

        private GameObject Build(LevelLayout layout)
        {
            return Track(LevelGeometryGenerator.FromParameters(layout, parameters).GenerateLevel());
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private Vector3[] WorldCorners(LevelLayout layout, LayoutCell cell)
        {
            return layout.GetCorners(cell).Select(corner => corner * parameters.CellSize).ToArray();
        }

        private (Vector3, Vector3) WorldEdge(LevelLayout layout, LayoutCell cell, int edge)
        {
            (Vector3 start, Vector3 end) = layout.GetEdge(cell, edge);
            return (start * parameters.CellSize, end * parameters.CellSize);
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
