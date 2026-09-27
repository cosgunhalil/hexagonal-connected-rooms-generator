using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ProBuilder;
using CRG.Building;
using CRG.Core;
using CRG.Generation;
using CRG.Geometry;
using CRG.Runtime;

namespace CRG.Tests
{
    // Levels keep the settings, seed, materials and door prefab they were made with.
    public class SavedSettingsTests
    {
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

        [TestCase(GridType.Hexagon)]
        [TestCase(GridType.Triangle)]
        [TestCase(GridType.OctagonSquare)]
        [TestCase(GridType.Mixed)]
        public void RandomSeed_IsResolvedAndSaved_SoTheLevelCanBeMadeAgain(GridType gridType)
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.GridType = gridType;
            parameters.RandomSeed = -1;
            parameters.TargetRoomCount = 12;

            CRGLevelData first = Track(LevelGeometryGenerator.GenerateComplete(parameters)).GetComponent<CRGLevelData>();
            Assert.That(first.Seed, Is.GreaterThanOrEqualTo(0), "a random level saves the seed it used");
            Assert.That(first.GenerationSettings.RandomSeed, Is.EqualTo(first.Seed));
            Assert.That(parameters.RandomSeed, Is.EqualTo(-1), "the caller's settings are left alone");

            CRGLevelData again = Track(LevelGeometryGenerator.GenerateComplete(first.GenerationSettings.Clone())).GetComponent<CRGLevelData>();
            Assert.That(again.Seed, Is.EqualTo(first.Seed));
            Assert.That(again.Rooms.Count, Is.EqualTo(first.Rooms.Count));
            for (int room = 0; room < first.Rooms.Count; room++)
            {
                Assert.That(again.Rooms[room].Cells, Is.EqualTo(first.Rooms[room].Cells), $"cells of room {room}");
                Assert.That(again.Rooms[room].GridType, Is.EqualTo(first.Rooms[room].GridType), $"grid type of room {room}");
            }
            Assert.That(again.Doors.Select(d => (d.RoomA, d.RoomB, d.CellA, d.EdgeA)), Is.EqualTo(first.Doors.Select(d => (d.RoomA, d.RoomB, d.CellA, d.EdgeA))));
        }

        [Test]
        public void FixedSeed_IsSavedAsIs()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 4321;

            CRGLevelData data = Track(LevelGeometryGenerator.GenerateComplete(parameters)).GetComponent<CRGLevelData>();
            Assert.That(data.Seed, Is.EqualTo(4321));
            Assert.That(data.GenerationSettings.RandomSeed, Is.EqualTo(4321));
        }

        [Test]
        public void SavedSettings_AreACopy_WithMaterialsAndDoorPrefab()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 3;
            parameters.WallThickness = 1f;
            Material floor = Track(new Material(BuiltinMaterials.defaultMaterial));
            Material wall = Track(new Material(BuiltinMaterials.defaultMaterial));
            Material ceiling = Track(new Material(BuiltinMaterials.defaultMaterial));
            GameObject door = Track(new GameObject("Door Template"));

            CRGLevelData data = Track(LevelGeometryGenerator.GenerateComplete(parameters, floor, wall, door, ceiling)).GetComponent<CRGLevelData>();
            parameters.WallThickness = 2f;

            Assert.That(data.HasGenerationSettings, Is.True);
            Assert.That(data.GenerationSettings, Is.Not.SameAs(parameters));
            Assert.That(data.GenerationSettings.WallThickness, Is.EqualTo(1f), "later changes to the caller's settings don't leak in");
            Assert.That(data.FloorMaterial, Is.SameAs(floor));
            Assert.That(data.WallMaterial, Is.SameAs(wall));
            Assert.That(data.CeilingMaterial, Is.SameAs(ceiling));
            Assert.That(data.DoorPrefab, Is.SameAs(door));
        }

        [Test]
        public void LevelsBuiltWithoutSettings_SaveNone()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 5;
            CellGrid grid = new CRGGenerator().Generate(parameters);
            Assert.That(grid.Seed, Is.EqualTo(5));

            CRGLevelData data = Track(new LevelGeometryGenerator(grid).GenerateLevel()).GetComponent<CRGLevelData>();
            Assert.That(data.HasGenerationSettings, Is.False);
            Assert.That(data.GenerationSettings, Is.Null);
            Assert.That(data.Seed, Is.EqualTo(-1));
        }

        [Test]
        public void HandBuiltLevels_SaveSettingsButNoSeed()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            LevelLayout layout = new LevelLayout();
            LayoutCell hex = layout.AddFirstCell(CellShape.Hexagon);
            layout.Attach(hex.id, 0, CellShape.Square);

            CRGLevelData data = Track(LevelGeometryGenerator.FromParameters(layout, parameters).GenerateLevel()).GetComponent<CRGLevelData>();
            Assert.That(data.HasGenerationSettings, Is.True);
            Assert.That(data.Seed, Is.EqualTo(-1));
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }
    }
}
