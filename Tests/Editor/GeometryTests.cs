using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ProBuilder;
using HRCG.Core;
using HRCG.Generation;
using HRCG.Geometry;

namespace HRCG.Tests
{
    public class GeometryTests
    {
        private readonly List<Object> created = new List<Object>();

        private GenerationParameters parameters;
        private HexGrid grid;

        [SetUp]
        public void SetUp()
        {
            parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 42;
            parameters.TargetRoomCount = 20;
            grid = new HRCGGenerator().Generate(parameters);
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
        public void GenerateLevel_ProducesSingleProBuilderMesh()
        {
            GameObject level = Track(CreateGenerator().GenerateLevel());

            Assert.That(level.GetComponent<ProBuilderMesh>(), Is.Not.Null);
            Assert.That(level.transform.childCount, Is.Zero);
            Assert.That(level.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(1));
        }

        [Test]
        public void GenerateLevel_BuildsEachSharedEdgeOnce()
        {
            ProBuilderMesh mesh = Track(CreateGenerator().GenerateLevel()).GetComponent<ProBuilderMesh>();

            Assert.That(mesh.faceCount, Is.EqualTo(ExpectedFaceCount(includeFloors: true, includeWalls: true)));
        }

        [Test]
        public void GenerateLevel_WithoutMaterials_UsesSingleDefaultMaterial()
        {
            Material[] materials = Track(CreateGenerator().GenerateLevel()).GetComponent<MeshRenderer>().sharedMaterials;

            Assert.That(materials.Length, Is.EqualTo(1));
            Assert.That(materials[0], Is.EqualTo(BuiltinMaterials.defaultMaterial));
        }

        [Test]
        public void GenerateLevel_WithMaterials_UsesFloorAndWallSubmeshes()
        {
            Material floor = Track(new Material(BuiltinMaterials.defaultMaterial));
            Material wall = Track(new Material(BuiltinMaterials.defaultMaterial));

            LevelGeometryGenerator generator = CreateGenerator();
            generator.SetMaterials(floor, wall);
            GameObject level = Track(generator.GenerateLevel());

            Assert.That(level.GetComponent<MeshRenderer>().sharedMaterials, Is.EqualTo(new[] { floor, wall }));
            Assert.That(level.GetComponent<MeshFilter>().sharedMesh.subMeshCount, Is.EqualTo(2));
        }

        [Test]
        public void GenerateFloorOnly_FloorsFaceUp()
        {
            ProBuilderMesh mesh = Track(CreateGenerator().GenerateFloorOnly()).GetComponent<ProBuilderMesh>();
            Mesh unityMesh = mesh.GetComponent<MeshFilter>().sharedMesh;

            Assert.That(mesh.faceCount, Is.EqualTo(ExpectedFaceCount(includeFloors: true, includeWalls: false)));

            Vector3[] vertices = unityMesh.vertices;
            int[] triangles = unityMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 normal = Vector3.Cross(vertices[triangles[i + 1]] - a, vertices[triangles[i + 2]] - a);
                Assert.That(normal.y, Is.GreaterThan(0f), $"triangle {i / 3} faces down");
            }
        }

        [Test]
        public void GenerateWallsOnly_ContainsOnlyWalls()
        {
            ProBuilderMesh mesh = Track(CreateGenerator().GenerateWallsOnly()).GetComponent<ProBuilderMesh>();

            Assert.That(mesh.faceCount, Is.EqualTo(ExpectedFaceCount(includeFloors: false, includeWalls: true)));
        }

        [Test]
        public void GenerateLevelSeparateByRoom_MatchesSingleMesh()
        {
            LevelGeometryGenerator generator = CreateGenerator();
            GameObject byRoom = Track(generator.GenerateLevelSeparateByRoom());
            ProBuilderMesh single = Track(generator.GenerateLevel()).GetComponent<ProBuilderMesh>();

            int faces = 0;
            foreach (ProBuilderMesh roomMesh in byRoom.GetComponentsInChildren<ProBuilderMesh>())
                faces += roomMesh.faceCount;

            Assert.That(byRoom.transform.childCount, Is.EqualTo(grid.RoomCount));
            Assert.That(faces, Is.EqualTo(single.faceCount));
        }

        private LevelGeometryGenerator CreateGenerator()
        {
            return LevelGeometryGenerator.FromParameters(grid, parameters);
        }

        // One face per floor, two per wall quad (double-sided), counting each shared edge once.
        private int ExpectedFaceCount(bool includeFloors, bool includeWalls)
        {
            int doorSegments = HexGeometry.GetDoorWallSegments(
                new AxialCoord(0, 0), 0, parameters.HexSize, parameters.WallHeight,
                HexMath.CalculateDoorWidth(parameters.HexSize, parameters.DoorWidthRatio), parameters.DoorHeight).Count;

            int faces = 0;
            HashSet<(AxialCoord, AxialCoord)> countedEdges = new HashSet<(AxialCoord, AxialCoord)>();

            foreach (HexCell cell in grid.GetAllCells())
            {
                if (!cell.IsPartOfRoom())
                    continue;

                if (includeFloors)
                    faces++;

                if (!includeWalls)
                    continue;

                for (int edge = 0; edge < 6; edge++)
                {
                    AxialCoord neighbor = cell.Coordinate.GetNeighbor(edge);
                    if (countedEdges.Contains((neighbor, cell.Coordinate)))
                        continue;
                    countedEdges.Add((cell.Coordinate, neighbor));

                    WallFlag flag = cell.GetEdgeFlag(edge);
                    if (flag == WallFlag.HasDoor)
                        faces += 2 * doorSegments;
                    else if (flag == WallFlag.Wall)
                        faces += 2;
                }
            }

            return faces;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }
    }
}
