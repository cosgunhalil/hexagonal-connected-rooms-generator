using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using HRCG.Core;
using HRCG.Generation;

namespace HRCG.Tests
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
        }

        private static TestCaseData Case(string name, int minHex, int maxHex, int rooms,
            int minConnections = 1, int maxConnections = 6, bool reserve = true)
        {
            GenerationParameters parameters = new GenerationParameters
            {
                MinHexagonsPerRoom = minHex,
                MaxHexagonsPerRoom = maxHex,
                TargetRoomCount = rooms,
                MinConnectionsPerRoom = minConnections,
                MaxConnectionsPerRoom = maxConnections,
                ReserveConnectionForGrowth = reserve
            };
            return new TestCaseData(parameters).SetName($"Generation_Invariants_{name}");
        }

        [TestCaseSource(nameof(ParameterCases))]
        public void Generation_Invariants(GenerationParameters parameters)
        {
            for (int seed = 0; seed < SeedsPerCase; seed++)
            {
                parameters.RandomSeed = seed;
                HexGrid grid = new HRCGGenerator().Generate(parameters);
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
                Assert.That(new HRCGGenerator().Generate(parameters).RoomCount, Is.EqualTo(parameters.TargetRoomCount), $"seed {seed}");
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
                Assert.That(Fingerprint(new HRCGGenerator().Generate(parameters)),
                    Is.EqualTo(Fingerprint(new HRCGGenerator().Generate(parameters))), $"seed {seed}");
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

        private static void AssertLevelIsValid(HexGrid grid, GenerationParameters parameters, string context)
        {
            List<Room> rooms = grid.GetAllRooms().ToList();
            Assert.That(rooms, Is.Not.Empty, context);

            HashSet<(AxialCoord, int)> doorEdges = new HashSet<(AxialCoord, int)>();

            foreach (Room room in rooms)
            {
                string roomContext = $"{context}, room {room.RoomID}";

                Assert.That(room.Cells.Count, Is.InRange(parameters.MinHexagonsPerRoom, parameters.MaxHexagonsPerRoom), roomContext);
                Assert.That(IsContiguous(room), Is.True, $"{roomContext}: cells are not contiguous");
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
                foreach (AxialCoord coord in room.Cells)
                {
                    HexCell cell = grid.GetCell(coord);

                    for (int edge = 0; edge < 6; edge++)
                    {
                        HexCell neighbor = grid.GetCell(coord.GetNeighbor(edge));
                        bool neighborIsRoom = neighbor != null && neighbor.IsPartOfRoom();

                        WallFlag expected =
                            neighborIsRoom && neighbor.RoomID == room.RoomID ? WallFlag.NoWall :
                            doorEdges.Contains((coord, edge)) ? WallFlag.HasDoor :
                            WallFlag.Wall;

                        Assert.That(cell.GetEdgeFlag(edge), Is.EqualTo(expected), $"{context}, cell {coord}, edge {edge}");

                        if (neighborIsRoom)
                        {
                            Assert.That(neighbor.GetEdgeFlag(HexDirection.GetOppositeDirection(edge)), Is.EqualTo(expected),
                                $"{context}, cell {coord}, edge {edge}: flags must agree from both sides");
                        }
                    }
                }
            }

            Assert.That(CountReachableRooms(grid, rooms), Is.EqualTo(rooms.Count), $"{context}: every room must be reachable through doors");
        }

        private static int CountReachableRooms(HexGrid grid, List<Room> rooms)
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

        private static bool IsContiguous(Room room)
        {
            HashSet<AxialCoord> cells = new HashSet<AxialCoord>(room.Cells);
            HashSet<AxialCoord> reached = new HashSet<AxialCoord> { room.Cells[0] };
            Queue<AxialCoord> queue = new Queue<AxialCoord>(reached);

            while (queue.Count > 0)
            {
                foreach (AxialCoord neighbor in queue.Dequeue().GetAllNeighbors())
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
                total += new HRCGGenerator().Generate(parameters).RoomCount;
            }
            return (double)total / SeedsPerCase;
        }

        private static string Fingerprint(HexGrid grid)
        {
            StringBuilder builder = new StringBuilder();

            foreach (Room room in grid.GetAllRooms().OrderBy(r => r.RoomID))
            {
                builder.Append(room.RoomID).Append(':');
                foreach (AxialCoord coord in room.Cells)
                    builder.Append(coord).Append(',');
                foreach (int neighbor in room.Connections.Keys.OrderBy(id => id))
                    builder.Append('>').Append(neighbor);
                builder.Append(';');
            }

            return builder.ToString();
        }
    }
}
