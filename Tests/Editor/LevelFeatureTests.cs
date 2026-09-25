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
    // Post-generation features: colliders, spawn points, door prefabs, room roles, layout control and NavMesh.
    public class LevelFeatureTests
    {
        private const int Seeds = 30;

        private readonly List<Object> created = new List<Object>();
        private GenerationParameters parameters;

        [SetUp]
        public void SetUp()
        {
            parameters = GenerationParameters.CreateDefault();
            parameters.RandomSeed = 42;
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
        public void MeshCollider_AddedOnlyWhenEnabled()
        {
            parameters.AddMeshCollider = true;
            GameObject withCollider = Generate();
            MeshCollider meshCollider = withCollider.GetComponent<MeshCollider>();

            Assert.That(meshCollider, Is.Not.Null);
            Assert.That(meshCollider.sharedMesh, Is.EqualTo(withCollider.GetComponent<MeshFilter>().sharedMesh));

            parameters.AddMeshCollider = false;
            Assert.That(Generate().GetComponentsInChildren<Collider>(), Is.Empty);
        }

        [Test]
        public void SpawnPoints_OnePerRoomInsideTheRoom()
        {
            CRGLevelData data = Generate().GetComponent<CRGLevelData>();

            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                Assert.That(room.SpawnPoint, Is.Not.Null, $"room {room.RoomID}");
                Assert.That(data.GetSpawnPoint(room.RoomID), Is.EqualTo(room.SpawnPoint));

                CellCoord cell = data.Topology.GetCellAt(room.SpawnPoint.localPosition, data.CellSize);
                Assert.That(room.Cells, Does.Contain(cell), $"room {room.RoomID}");
            }
        }

        [Test]
        public void SpawnPoints_NotCreatedWhenDisabled()
        {
            parameters.CreateSpawnPoints = false;
            CRGLevelData data = Generate().GetComponent<CRGLevelData>();

            Assert.That(data.Rooms.All(room => room.SpawnPoint == null), Is.True);
        }

        [Test]
        public void DoorPrefab_PlacedInEveryDoorwayFacingThroughIt()
        {
            GameObject template = Track(new GameObject("Door Template"));
            GameObject level = Generate(template);
            CRGLevelData data = level.GetComponent<CRGLevelData>();

            Transform doorsParent = level.transform.Find("Doors");
            Assert.That(doorsParent, Is.Not.Null);
            Assert.That(doorsParent.childCount, Is.EqualTo(data.Doors.Count));

            foreach (CRGLevelData.DoorData door in data.Doors)
            {
                Assert.That(door.DoorObject, Is.Not.Null);
                Assert.That(Vector3.Distance(door.DoorObject.localPosition, data.GetDoorCenterLocal(door)), Is.LessThan(1e-3f));
                Assert.That(Vector3.Dot(door.DoorObject.forward, data.GetDoorForwardLocal(door)), Is.GreaterThan(0.999f));
            }
        }

        [Test]
        public void RoomRoles_StartIsFirstRoomAndEndIsFarthest()
        {
            for (int seed = 0; seed < Seeds; seed++)
            {
                parameters.RandomSeed = seed;
                CRGLevelData data = Generate().GetComponent<CRGLevelData>();

                CRGLevelData.RoomData start = data.StartRoom;
                CRGLevelData.RoomData end = data.EndRoom;
                int maxDistance = data.Rooms.Max(room => room.DistanceFromStart);

                Assert.That(start.RoomID, Is.EqualTo(data.Rooms.Min(room => room.RoomID)), $"seed {seed}");
                Assert.That(start.DistanceFromStart, Is.Zero);
                Assert.That(end, Is.Not.Null, $"seed {seed}");
                Assert.That(end.DistanceFromStart, Is.EqualTo(maxDistance), $"seed {seed}");
                Assert.That(data.Rooms.Count(room => room.Role == CRGLevelData.RoomRole.End), Is.EqualTo(1));

                foreach (CRGLevelData.RoomData room in data.Rooms)
                {
                    Assert.That(room.DistanceFromStart, Is.GreaterThanOrEqualTo(0), $"seed {seed}: room {room.RoomID} unreachable");
                    foreach (int neighborID in room.ConnectedRoomIDs)
                    {
                        int difference = Mathf.Abs(room.DistanceFromStart - data.GetRoom(neighborID).DistanceFromStart);
                        Assert.That(difference, Is.LessThanOrEqualTo(1), $"seed {seed}: rooms {room.RoomID}-{neighborID}");
                    }
                }
            }
        }

        [Test]
        public void LoopChanceZero_ProducesTree()
        {
            parameters.LoopChance = 0f;

            for (int seed = 0; seed < Seeds; seed++)
            {
                parameters.RandomSeed = seed;
                CellGrid grid = new CRGGenerator().Generate(parameters);
                int connections = grid.GetAllRooms().Sum(room => room.GetConnectionCount()) / 2;

                Assert.That(connections, Is.EqualTo(grid.RoomCount - 1), $"seed {seed}");
            }
        }

        [Test]
        public void LowerLoopChance_ProducesFewerLoops()
        {
            parameters.LoopChance = 0.3f;
            double fewLoops = AverageLoops();

            parameters.LoopChance = 1f;
            double manyLoops = AverageLoops();

            Assert.That(fewLoops, Is.LessThan(manyLoops));
        }

        [Test]
        public void PositiveLayoutBias_SpreadsFartherThanNegative()
        {
            parameters.TargetRoomCount = 30;

            parameters.LayoutBias = 1f;
            double sprawling = AverageExtent();

            parameters.LayoutBias = -1f;
            double clustered = AverageExtent();

            Assert.That(sprawling, Is.GreaterThan(clustered));
        }

#if CRG_AI_NAVIGATION
        [Test]
        public void NavMesh_ConnectsStartRoomToEndRoom()
        {
            bool previousPersist = LevelNavMeshBaker.PersistEditorBakes;
            LevelNavMeshBaker.PersistEditorBakes = false;

            try
            {
                parameters.BakeNavMesh = true;
                GameObject level = Generate();
                CRGLevelData data = level.GetComponent<CRGLevelData>();
                Unity.AI.Navigation.NavMeshSurface surface = level.GetComponent<Unity.AI.Navigation.NavMeshSurface>();

                Assert.That(surface, Is.Not.Null);
                Assert.That(surface.navMeshData, Is.Not.Null);

                Vector3 from = SampleNavMesh(data.StartRoom.SpawnPoint.position);
                Vector3 to = SampleNavMesh(data.EndRoom.SpawnPoint.position);

                NavMeshPath path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

                surface.RemoveData();
            }
            finally
            {
                LevelNavMeshBaker.PersistEditorBakes = previousPersist;
            }
        }

        private static Vector3 SampleNavMesh(Vector3 position)
        {
            Assert.That(NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas), Is.True,
                $"no NavMesh near {position}");
            return hit.position;
        }
#endif

        [Test]
        public void Validate_RejectsColliderNavMeshWithoutCollider()
        {
            parameters.BakeNavMesh = true;
            parameters.NavMeshGeometry = NavMeshGeometrySource.PhysicsColliders;
            parameters.AddMeshCollider = false;

            Assert.That(parameters.Validate(out _), Is.False);
        }

        [Test]
        public void DoorsFitAgent_DetectsNarrowDoors()
        {
            Assert.That(LevelNavMeshBaker.DoorsFitAgent(parameters, out _), Is.True, "default doors fit the default agent");

            parameters.DoorWidthRatio = 0.05f;
            parameters.CellSize = 10f;
            Assert.That(LevelNavMeshBaker.DoorsFitAgent(parameters, out _), Is.False);
        }

        private GameObject Generate(GameObject doorPrefab = null)
        {
            return Track(LevelGeometryGenerator.GenerateComplete(parameters, null, null, doorPrefab));
        }

        private double AverageLoops()
        {
            int total = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                parameters.RandomSeed = seed;
                CellGrid grid = new CRGGenerator().Generate(parameters);
                total += grid.GetAllRooms().Sum(room => room.GetConnectionCount()) / 2 - (grid.RoomCount - 1);
            }
            return (double)total / Seeds;
        }

        // Average distance (in hexes) from the start position to the farthest room cell.
        private double AverageExtent()
        {
            int total = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                parameters.RandomSeed = seed;
                CellGrid grid = new CRGGenerator().Generate(parameters);
                total += grid.GetAllRooms().SelectMany(room => room.Cells).Max(cell => grid.Topology.GetDistance(cell, parameters.StartPosition));
            }
            return (double)total / Seeds;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }
    }
}
