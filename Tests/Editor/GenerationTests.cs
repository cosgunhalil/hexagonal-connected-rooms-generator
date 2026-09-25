using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using CRG.Core;
using CRG.Generation;

namespace CRG.Tests
{
    public class GenerationTests
    {
        private const int SeedsPerCase = 50;

        private static IEnumerable<TestCaseData> ParameterCases()
        {
            yield return Case("Default", 1, 5, 10);
            yield return Case("Small", 1, 3, 5);
            yield return Case("Medium", 2, 5, 10);
            yield return Case("Large", 3, 7, 20);
            yield return Case("Huge", 1, 10, 100);
            yield return Case("MaxConnections2", 1, 5, 30, maxConnections: 2);
            yield return Case("MaxConnections2NoReserve", 1, 5, 30, maxConnections: 2, reserve: false);
            yield return Case("MinConnections3", 2, 4, 30, minConnections: 3);
            yield return Case("Sprawling", 1, 5, 30, layoutBias: 1f);
            yield return Case("Clustered", 1, 5, 30, layoutBias: -1f);
            yield return Case("TreeNoLoops", 1, 5, 30, loopChance: 0f);
            yield return Case("SprawlingTreeMaxConnections3", 2, 6, 30, maxConnections: 3, layoutBias: 0.7f, loopChance: 0.2f);
            yield return Case("SquareDefault", 1, 5, 10, gridType: GridType.Square);
            yield return Case("SquareLarge", 3, 7, 20, gridType: GridType.Square);
            yield return Case("SquareHuge", 1, 10, 100, gridType: GridType.Square);
            yield return Case("SquareMaxConnections2", 1, 5, 30, maxConnections: 2, gridType: GridType.Square);
            yield return Case("SquareTreeSprawling", 1, 5, 30, layoutBias: 1f, loopChance: 0f, gridType: GridType.Square);
            yield return Case("TriangleDefault", 1, 5, 10, gridType: GridType.Triangle);
            yield return Case("TriangleLarge", 4, 12, 25, gridType: GridType.Triangle);
            yield return Case("TriangleHuge", 1, 10, 100, gridType: GridType.Triangle);
            yield return Case("TriangleMaxConnections2", 2, 6, 30, maxConnections: 2, gridType: GridType.Triangle);
            yield return Case("TriangleTreeClustered", 1, 6, 30, layoutBias: -1f, loopChance: 0f, gridType: GridType.Triangle);
            yield return Case("OctagonSquareDefault", 1, 5, 10, gridType: GridType.OctagonSquare);
            yield return Case("OctagonSquareLarge", 3, 9, 25, gridType: GridType.OctagonSquare);
            yield return Case("OctagonSquareHuge", 1, 10, 100, gridType: GridType.OctagonSquare);
            yield return Case("OctagonSquareMaxConnections2", 1, 5, 30, maxConnections: 2, gridType: GridType.OctagonSquare);
            yield return Case("OctagonSquareTreeSprawling", 1, 6, 30, layoutBias: 1f, loopChance: 0f, gridType: GridType.OctagonSquare);
        }

        private static TestCaseData Case(string name, int minHex, int maxHex, int rooms,
            int minConnections = 1, int maxConnections = 6, bool reserve = true,
            float layoutBias = 0f, float loopChance = 1f, GridType gridType = GridType.Hexagon)
        {
            GenerationParameters parameters = new GenerationParameters
            {
                MinCellsPerRoom = minHex,
                MaxCellsPerRoom = maxHex,
                TargetRoomCount = rooms,
                MinConnectionsPerRoom = minConnections,
                MaxConnectionsPerRoom = maxConnections,
                ReserveConnectionForGrowth = reserve,
                LayoutBias = layoutBias,
                LoopChance = loopChance,
                GridType = gridType
            };
            return new TestCaseData(parameters).SetName($"Generation_Invariants_{name}");
        }

        [TestCaseSource(nameof(ParameterCases))]
        public void Generation_Invariants(GenerationParameters parameters)
        {
            for (int seed = 0; seed < SeedsPerCase; seed++)
            {
                parameters.RandomSeed = seed;
                CellGrid grid = new CRGGenerator().Generate(parameters);
                AssertLevelIsValid(grid, parameters, $"seed {seed}");
            }
        }

        [Test]
        public void DefaultParameters_AlwaysReachTargetRoomCount()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();

            for (int seed = 0; seed < SeedsPerCase; seed++)
            {
                parameters.RandomSeed = seed;
                Assert.That(new CRGGenerator().Generate(parameters).RoomCount, Is.EqualTo(parameters.TargetRoomCount), $"seed {seed}");
            }
        }

        [Test]
        public void SameSeed_ProducesSameLevel()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.TargetRoomCount = 30;

            for (int seed = 0; seed < 10; seed++)
            {
                parameters.RandomSeed = seed;
                Assert.That(Fingerprint(new CRGGenerator().Generate(parameters)),
                    Is.EqualTo(Fingerprint(new CRGGenerator().Generate(parameters))), $"seed {seed}");
            }
        }

        [Test]
        public void ReserveConnectionForGrowth_ProducesLargerLevelsAtLowMaxConnections()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.TargetRoomCount = 30;
            parameters.MaxConnectionsPerRoom = 2;

            parameters.ReserveConnectionForGrowth = true;
            double withReserve = AverageRoomCount(parameters);

            parameters.ReserveConnectionForGrowth = false;
            double withoutReserve = AverageRoomCount(parameters);

            Assert.That(withReserve, Is.GreaterThan(withoutReserve));
        }

        [Test]
        public void Validate_RejectsSingleConnectionWithMoreThanTwoRooms()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.MaxConnectionsPerRoom = 1;
            parameters.TargetRoomCount = 3;

            Assert.That(parameters.Validate(out _), Is.False);

            parameters.TargetRoomCount = 2;
            Assert.That(parameters.Validate(out _), Is.True);
        }

        [Test]
        public void Validate_RejectsDoorTallerThanWall()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.WallHeight = 2f;
            parameters.DoorHeight = 2.5f;

            Assert.That(parameters.Validate(out _), Is.False);
        }

        [TestCase(0f)]
        [TestCase(1.5f)]
        public void Validate_RejectsDoorWidthRatioOutOfRange(float ratio)
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.DoorWidthRatio = ratio;

            Assert.That(parameters.Validate(out _), Is.False);
        }

        private static void AssertLevelIsValid(CellGrid grid, GenerationParameters parameters, string context)
        {
            List<Room> rooms = grid.GetAllRooms().ToList();
            Assert.That(rooms, Is.Not.Empty, context);

            HashSet<(CellCoord, int)> doorEdges = new HashSet<(CellCoord, int)>();

            foreach (Room room in rooms)
            {
                string roomContext = $"{context}, room {room.RoomID}";

                Assert.That(room.Cells.Count, Is.InRange(parameters.MinCellsPerRoom, parameters.MaxCellsPerRoom), roomContext);
                Assert.That(IsContiguous(room, grid.Topology), Is.True, $"{roomContext}: cells are not contiguous");
                Assert.That(room.GetConnectionCount(), Is.LessThanOrEqualTo(parameters.MaxConnectionsPerRoom), roomContext);

                foreach (KeyValuePair<int, SharedWallData> connection in room.Connections)
                {
                    Assert.That(connection.Value.SharedEdges.Count, Is.EqualTo(1), $"{roomContext}: one door per connected room");
                    Assert.That(grid.GetRoom(connection.Key).IsConnectedTo(room.RoomID), Is.True, $"{roomContext}: connection must be symmetric");

                    EdgeConnection door = connection.Value.SharedEdges[0];
                    doorEdges.Add((door.CellA, door.EdgeIndexA));
                }
            }

            foreach (Room room in rooms)
            {
                foreach (CellCoord coord in room.Cells)
                {
                    GridCell cell = grid.GetCell(coord);

                    for (int edge = 0; edge < cell.EdgeCount; edge++)
                    {
                        GridCell neighbor = grid.GetNeighbor(coord, edge);
                        bool neighborIsRoom = neighbor != null && neighbor.IsPartOfRoom();

                        WallFlag expected =
                            neighborIsRoom && neighbor.RoomID == room.RoomID ? WallFlag.NoWall :
                            doorEdges.Contains((coord, edge)) ? WallFlag.HasDoor :
                            WallFlag.Wall;

                        Assert.That(cell.GetEdgeFlag(edge), Is.EqualTo(expected), $"{context}, cell {coord}, edge {edge}");

                        if (neighborIsRoom)
                        {
                            Assert.That(neighbor.GetEdgeFlag(grid.Topology.GetNeighborEdge(coord, edge)), Is.EqualTo(expected),
                                $"{context}, cell {coord}, edge {edge}: flags must agree from both sides");
                        }
                    }
                }
            }

            Assert.That(CountReachableRooms(grid, rooms), Is.EqualTo(rooms.Count), $"{context}: every room must be reachable through doors");
        }

        private static int CountReachableRooms(CellGrid grid, List<Room> rooms)
        {
            HashSet<int> reached = new HashSet<int> { rooms.Min(r => r.RoomID) };
            Queue<int> queue = new Queue<int>(reached);

            while (queue.Count > 0)
            {
                foreach (int next in grid.GetRoom(queue.Dequeue()).GetConnectedRoomIDs())
                {
                    if (reached.Add(next))
                        queue.Enqueue(next);
                }
            }

            return reached.Count;
        }

        private static bool IsContiguous(Room room, IGridTopology topology)
        {
            HashSet<CellCoord> cells = new HashSet<CellCoord>(room.Cells);
            HashSet<CellCoord> reached = new HashSet<CellCoord> { room.Cells[0] };
            Queue<CellCoord> queue = new Queue<CellCoord>(reached);

            while (queue.Count > 0)
            {
                foreach (CellCoord neighbor in topology.GetNeighbors(queue.Dequeue()))
                {
                    if (cells.Contains(neighbor) && reached.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return reached.Count == cells.Count;
        }

        private static double AverageRoomCount(GenerationParameters parameters)
        {
            int total = 0;
            for (int seed = 0; seed < SeedsPerCase; seed++)
            {
                parameters.RandomSeed = seed;
                total += new CRGGenerator().Generate(parameters).RoomCount;
            }
            return (double)total / SeedsPerCase;
        }

        private static string Fingerprint(CellGrid grid)
        {
            StringBuilder builder = new StringBuilder();

            foreach (Room room in grid.GetAllRooms().OrderBy(r => r.RoomID))
            {
                builder.Append(room.RoomID).Append(':');
                foreach (CellCoord coord in room.Cells)
                    builder.Append(coord).Append(',');
                foreach (int neighbor in room.Connections.Keys.OrderBy(id => id))
                    builder.Append('>').Append(neighbor);
                builder.Append(';');
            }

            return builder.ToString();
        }
    }
}
