using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CRG.Building;
using CRG.Core;
using CRG.Generation;
using CRG.Geometry;
using CRG.Runtime;
using Object = UnityEngine.Object;

namespace CRG.Tests
{
    // Converting a level for the level builder and baking it again without changes gives back the same level.
    public class LevelConversionTests
    {
        private static readonly Dictionary<string, Action<GenerationParameters>> Configs = new Dictionary<string, Action<GenerationParameters>>
        {
            { "hexagon", p => { } },
            { "hexagonThickCeilings", p => { p.WallThickness = 1f; p.AddCeiling = true; p.CeilingChance = 0.5f; p.TargetRoomCount = 20; } },
            { "squareThick", p => { p.GridType = GridType.Square; p.WallThickness = 1f; p.MaxCellsPerRoom = 8; p.TargetRoomCount = 20; } },
            { "triangleThickCeilings", p => { p.GridType = GridType.Triangle; p.WallThickness = 1f; p.AddCeiling = true; p.CeilingChance = 0.5f; p.MaxCellsPerRoom = 10; p.TargetRoomCount = 20; } },
            { "octagonSquareThick", p => { p.GridType = GridType.OctagonSquare; p.WallThickness = 1f; p.MaxCellsPerRoom = 9; p.TargetRoomCount = 20; } },
            { "octagonSquareFullDoors", p => { p.GridType = GridType.OctagonSquare; p.WallThickness = 2f; p.DoorHeight = 3f; p.TargetRoomCount = 20; } },
            { "mixed", p => { p.GridType = GridType.Mixed; p.TargetRoomCount = 20; } },
            { "mixedThickCeilings", p => { p.GridType = GridType.Mixed; p.WallThickness = 1f; p.AddCeiling = true; p.CeilingChance = 0.5f; p.MaxCellsPerRoom = 6; p.TargetRoomCount = 20; } },
        };

        private readonly List<Object> created = new List<Object>();

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

        private static IEnumerable<TestCaseData> GeneratedLevels()
        {
            foreach (string config in Configs.Keys)
            {
                for (int seed = 0; seed < 3; seed++)
                    yield return new TestCaseData(config, seed).SetName($"GeneratedLevel_RoundTrips_{config}_{seed}");
            }
        }

        [TestCaseSource(nameof(GeneratedLevels))]
        public void GeneratedLevel_RoundTrips(string config, int seed)
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            Configs[config](parameters);
            parameters.RandomSeed = seed;

            LevelLayout direct;
            GameObject original;
            if (parameters.GridType == GridType.Mixed)
            {
                MixedLevel level = new MixedLevelGenerator().Generate(parameters);
                original = Track(LevelGeometryGenerator.FromParameters(level, parameters).GenerateLevel());
                direct = LayoutConversion.FromMixedLevel(level, parameters.AddCeiling);
            }
            else
            {
                CellGrid grid = new CRGGenerator().Generate(parameters);
                original = Track(LevelGeometryGenerator.FromParameters(grid, parameters).GenerateLevel());
                direct = LayoutConversion.FromGrid(grid, parameters.AddCeiling);
            }

            CRGLevelData data = original.GetComponent<CRGLevelData>();
            LevelLayout converted = LayoutConversion.FromLevelData(data);
            GenerationParameters settings = LayoutConversion.SettingsFor(data);

            Assert.That(settings.ValidateHandBuilt(converted.GetCornerInsetPerThickness(), out string error), Is.True, error);
            Assert.That(HandBuiltGoldenLayouts.Fingerprint(converted, settings), Is.EqualTo(HandBuiltGoldenLayouts.Fingerprint(direct, settings)),
                "converting the generated layout and the baked level give the same layout");

            GameObject baked = Track(LevelGeometryGenerator.FromParameters(converted, settings).GenerateLevel());
            AssertSameLevel(original, baked);
        }

        [Test]
        public void HandBuiltLevel_GivesBackItsLayout()
        {
            GenerationParameters parameters = HandBuiltGoldenLayouts.CreateParameters("handBuiltThickCeilings");
            LevelLayout layout = HandBuiltGoldenLayouts.CreateLayout("handBuiltThickCeilings", 2);
            LayoutRooms rooms = layout.GetRooms();
            rooms.Settings[1].roomName = "Armory";
            rooms.Settings[2].roomTag = "Boss";
            layout.StartCell = rooms.Rooms[2].Last();
            layout.EndCell = rooms.Rooms[1].Last();

            GameObject original = Track(LevelGeometryGenerator.FromParameters(layout, parameters).GenerateLevel());
            CRGLevelData data = original.GetComponent<CRGLevelData>();
            LevelLayout converted = LayoutConversion.FromLevelData(data);

            Assert.That(converted, Is.Not.SameAs(data.SourceLayout), "editing gets a copy");
            Assert.That(HandBuiltGoldenLayouts.Fingerprint(converted, parameters), Is.EqualTo(HandBuiltGoldenLayouts.Fingerprint(layout, parameters)));
            Assert.That(converted.StartCell, Is.EqualTo(layout.StartCell));
            Assert.That(converted.EndCell, Is.EqualTo(layout.EndCell));
            Assert.That(converted.GetRooms().Settings[1].roomName, Is.EqualTo("Armory"));

            GameObject baked = Track(LevelGeometryGenerator.FromParameters(converted, LayoutConversion.SettingsFor(data)).GenerateLevel());
            AssertSameLevel(original, baked);
        }

        [Test]
        public void LevelsWithoutSavedSettings_UseTheirLevelData()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 1;
            parameters.CellSize = 6f;
            parameters.WallHeight = 4f;
            parameters.DoorHeight = 3f;
            parameters.DoorWidthRatio = 0.3f;
            parameters.AddCeiling = true;

            CellGrid grid = new CRGGenerator().Generate(parameters);
            CRGLevelData data = Track(new LevelGeometryGenerator(grid, parameters.WallHeight, parameters.DoorHeight, parameters.DoorWidthRatio, 0f, true)
                .GenerateLevel()).GetComponent<CRGLevelData>();
            Assert.That(data.HasGenerationSettings, Is.False);

            GenerationParameters settings = LayoutConversion.SettingsFor(data);
            Assert.That(settings.CellSize, Is.EqualTo(6f));
            Assert.That(settings.WallHeight, Is.EqualTo(4f));
            Assert.That(settings.DoorHeight, Is.EqualTo(3f));
            Assert.That(settings.DoorWidthRatio, Is.EqualTo(0.3f));
            Assert.That(settings.AddCeiling, Is.True);

            LevelLayout converted = LayoutConversion.FromLevelData(data);
            Assert.That(converted.Cells.Count, Is.EqualTo(data.Rooms.Sum(room => room.Cells.Count)));
            Assert.That(converted.GetRooms().Rooms.Count, Is.EqualTo(data.Rooms.Count));
        }

        [TestCase(GridType.Hexagon, 0, CellShape.Hexagon)]
        [TestCase(GridType.Square, 0, CellShape.Square)]
        [TestCase(GridType.Triangle, 1, CellShape.Triangle)]
        [TestCase(GridType.OctagonSquare, 0, CellShape.Octagon)]
        [TestCase(GridType.OctagonSquare, 1, CellShape.Square)]
        public void GridCells_BecomeTheirShape(GridType gridType, int variant, CellShape shape)
        {
            Assert.That(LayoutConversion.GetShape(gridType, new CellCoord(3, -2, variant)), Is.EqualTo(shape));
        }

        [Test]
        public void Builder_TakesAConvertedLayout()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 2;
            CellGrid grid = new CRGGenerator().Generate(parameters);

            GameObject builderObject = Track(new GameObject("Builder"));
            CRGLevelBuilder builder = builderObject.AddComponent<CRGLevelBuilder>();
            builder.parameters = parameters;
            builder.showPreview = false;
            builder.SetLayout(LayoutConversion.FromGrid(grid, parameters.AddCeiling));

            Assert.That(builder.Layout.Cells.Count, Is.EqualTo(grid.GetAllRooms().Sum(room => room.Cells.Count)));
            CRGLevelData data = Track(builder.Bake(false)).GetComponent<CRGLevelData>();
            Assert.That(data.Rooms.Count, Is.EqualTo(grid.RoomCount));
        }

        // Same rooms (cells in place, ceilings, roles, connections), same doors and matching geometry.
        private static void AssertSameLevel(GameObject original, GameObject baked)
        {
            CRGLevelData a = original.GetComponent<CRGLevelData>();
            CRGLevelData b = baked.GetComponent<CRGLevelData>();

            Assert.That(b.Rooms.Count, Is.EqualTo(a.Rooms.Count), "room count");
            Assert.That(b.Doors.Count, Is.EqualTo(a.Doors.Count), "door count");

            for (int room = 0; room < a.Rooms.Count; room++)
            {
                CRGLevelData.RoomData roomA = a.Rooms[room];
                CRGLevelData.RoomData roomB = b.Rooms[room];
                Assert.That(roomB.Cells.Count, Is.EqualTo(roomA.Cells.Count), $"cells of room {room}");
                Assert.That(roomB.HasCeiling, Is.EqualTo(roomA.HasCeiling), $"ceiling of room {room}");
                Assert.That(roomB.Role, Is.EqualTo(roomA.Role), $"role of room {room}");
                Assert.That(roomB.DistanceFromStart, Is.EqualTo(roomA.DistanceFromStart), $"distance of room {room}");
                Assert.That(roomB.ConnectedRoomIDs, Is.EqualTo(roomA.ConnectedRoomIDs), $"connections of room {room}");
                Assert.That((roomB.Name, roomB.Tag), Is.EqualTo((roomA.Name, roomA.Tag)), $"name and tag of room {room}");

                for (int cell = 0; cell < roomA.Cells.Count; cell++)
                {
                    int corners = a.GetCellEdgeCount(roomA, roomA.Cells[cell]);
                    Assert.That(b.GetCellEdgeCount(roomB, roomB.Cells[cell]), Is.EqualTo(corners));
                    for (int corner = 0; corner < corners; corner++)
                    {
                        float distance = Vector3.Distance(a.GetCellCornerLocal(roomA, roomA.Cells[cell], corner), b.GetCellCornerLocal(roomB, roomB.Cells[cell], corner));
                        Assert.That(distance, Is.LessThan(1e-3f), $"corner {corner} of cell {cell} in room {room}");
                    }
                }
            }

            foreach (CRGLevelData.DoorData door in a.Doors)
            {
                Vector3 center = a.GetDoorCenterLocal(door);
                Assert.That(b.Doors.Any(d => d.RoomA == door.RoomA && d.RoomB == door.RoomB && Vector3.Distance(b.GetDoorCenterLocal(d), center) < 1e-3f),
                    Is.True, $"door {door.RoomA}-{door.RoomB} is missing");
            }

            GeometrySignature before = GeometrySignature.FromTriangles(GetTriangles(original));
            GeometrySignature after = GeometrySignature.FromTriangles(GetTriangles(baked));
            Assert.That(before.Matches(after, out string difference), Is.True, $"geometry changed: {difference}");
        }

        private static IEnumerable<(Vector3, Vector3, Vector3)> GetTriangles(GameObject level)
        {
            foreach (MeshFilter filter in level.GetComponentsInChildren<MeshFilter>())
            {
                Vector3[] vertices = filter.sharedMesh.vertices;
                int[] triangles = filter.sharedMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                    yield return (vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
            }
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }
    }
}
