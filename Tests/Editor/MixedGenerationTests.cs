using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using CRG.Core;
using CRG.Generation;

namespace CRG.Tests
{
    public class MixedGenerationTests
    {
        private const int SeedsPerCase = 15;

        private static IEnumerable<TestCaseData> ParameterCases()
        {
            yield return Case("Default", p => { });
            yield return Case("LargeRooms", p => { p.MinCellsPerRoom = 3; p.MaxCellsPerRoom = 8; p.TargetRoomCount = 30; });
            yield return Case("SmallRoomsMany", p => { p.MaxCellsPerRoom = 3; p.TargetRoomCount = 60; });
            yield return Case("SprawlingTree", p => { p.LayoutBias = 1f; p.LoopChance = 0f; p.TargetRoomCount = 30; });
            yield return Case("Clustered", p => { p.LayoutBias = -1f; p.TargetRoomCount = 30; });
            yield return Case("MaxConnections2", p => { p.MaxConnectionsPerRoom = 2; p.TargetRoomCount = 20; });
            yield return Case("MinConnections2", p => { p.MinConnectionsPerRoom = 2; p.TargetRoomCount = 25; });
            yield return Case("HeavyOctagons", p => { p.OctagonSquareWeight = 5f; p.TargetRoomCount = 25; });
        }

        private static TestCaseData Case(string name, Action<GenerationParameters> configure)
        {
            return new TestCaseData(configure).SetName($"MixedLevel_Invariants_{name}");
        }

        [TestCaseSource(nameof(ParameterCases))]
        public void MixedLevel_Invariants(Action<GenerationParameters> configure)
        {
            for (int seed = 0; seed < SeedsPerCase; seed++)
            {
                GenerationParameters parameters = Parameters(configure, seed);
                AssertLevelIsValid(new MixedLevelGenerator().Generate(parameters), parameters, $"seed {seed}");
            }
        }

        [Test]
        public void DefaultParameters_ReachTargetRoomCount_AndUseEveryGridType()
        {
            HashSet<GridType> used = new HashSet<GridType>();

            for (int seed = 0; seed < SeedsPerCase; seed++)
            {
                GenerationParameters parameters = Parameters(p => { }, seed);
                MixedLevel level = new MixedLevelGenerator().Generate(parameters);

                Assert.That(level.Rooms.Count, Is.EqualTo(parameters.TargetRoomCount), $"seed {seed}");
                used.UnionWith(level.Rooms.Select(room => room.GridType));
            }

            Assert.That(used, Is.EquivalentTo(new[] { GridType.Hexagon, GridType.Square, GridType.Triangle, GridType.OctagonSquare }));
        }

        [Test]
        public void SameSeed_ProducesSameLevel()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                GenerationParameters parameters = Parameters(p => p.TargetRoomCount = 25, seed);
                Assert.That(Fingerprint(new MixedLevelGenerator().Generate(parameters)),
                    Is.EqualTo(Fingerprint(new MixedLevelGenerator().Generate(parameters))), $"seed {seed}");
            }
        }

        [Test]
        public void ZeroWeight_ExcludesGridType()
        {
            for (int seed = 0; seed < SeedsPerCase; seed++)
            {
                GenerationParameters parameters = Parameters(p => { p.HexagonWeight = 0f; p.TriangleWeight = 0f; p.TargetRoomCount = 20; }, seed);
                MixedLevel level = new MixedLevelGenerator().Generate(parameters);

                Assert.That(level.Rooms.All(room => room.GridType == GridType.Square || room.GridType == GridType.OctagonSquare), Is.True, $"seed {seed}");
            }
        }

        [Test]
        public void SingleWeight_BuildsSingleGridType()
        {
            GenerationParameters parameters = Parameters(p =>
            {
                p.HexagonWeight = 0f;
                p.SquareWeight = 0f;
                p.OctagonSquareWeight = 0f;
                p.TargetRoomCount = 15;
            }, 3);

            Assert.That(new MixedLevelGenerator().Generate(parameters).Rooms.All(room => room.GridType == GridType.Triangle), Is.True);
        }

        [Test]
        public void GridTypeMix_FollowsWeights()
        {
            int octagonRooms = 0;
            int totalRooms = 0;

            for (int seed = 0; seed < 20; seed++)
            {
                GenerationParameters parameters = Parameters(p => { p.OctagonSquareWeight = 5f; p.TargetRoomCount = 25; }, seed);
                MixedLevel level = new MixedLevelGenerator().Generate(parameters);
                octagonRooms += level.Rooms.Count(room => room.GridType == GridType.OctagonSquare);
                totalRooms += level.Rooms.Count;
            }

            // Weight 5 of 8: 62.5% expected.
            Assert.That((double)octagonRooms / totalRooms, Is.InRange(0.5, 0.75));
        }

        [Test]
        public void AllWeightsZero_IsRejected()
        {
            GenerationParameters parameters = Parameters(p =>
            {
                p.HexagonWeight = 0f;
                p.SquareWeight = 0f;
                p.TriangleWeight = 0f;
                p.OctagonSquareWeight = 0f;
            }, 0);

            Assert.Throws<ArgumentException>(() => new MixedLevelGenerator().Generate(parameters));
        }

        [Test]
        public void NegativeWeight_FailsValidation()
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.SquareWeight = -1f;

            Assert.That(parameters.Validate(out _), Is.False);
        }

        [Test]
        public void PlanarGeometry_DistinguishesTouchingOverlappingAndPartialEdges()
        {
            Vector3[] square = { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) };
            Vector3[] touching = { new Vector3(1, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 0, 1), new Vector3(1, 0, 1) };
            Vector3[] overlapping = { new Vector3(0.5f, 0, 0), new Vector3(1.5f, 0, 0), new Vector3(1.5f, 0, 1), new Vector3(0.5f, 0, 1) };

            Assert.That(PlanarGeometry.ConvexPolygonsOverlap(square, touching, 1e-3f), Is.False);
            Assert.That(PlanarGeometry.ConvexPolygonsOverlap(square, overlapping, 1e-3f), Is.True);

            Vector3 a = new Vector3(0, 0, 0), b = new Vector3(1, 0, 0);
            Assert.That(PlanarGeometry.GetContact(a, b, b, a, 1e-3f), Is.EqualTo(PlanarGeometry.SegmentContact.ExactOpposite));
            Assert.That(PlanarGeometry.GetContact(a, b, new Vector3(1.5f, 0, 0), new Vector3(0.5f, 0, 0), 1e-3f), Is.EqualTo(PlanarGeometry.SegmentContact.PartialOverlap));
            Assert.That(PlanarGeometry.GetContact(a, b, new Vector3(2f, 0, 0), new Vector3(1f, 0, 0), 1e-3f), Is.EqualTo(PlanarGeometry.SegmentContact.None));
            Assert.That(PlanarGeometry.GetContact(a, b, new Vector3(1f, 0, 1f), new Vector3(0f, 0, 1f), 1e-3f), Is.EqualTo(PlanarGeometry.SegmentContact.None));
        }

        [Test]
        public void RoomPlacement_AlignMapsLocalEdgeOntoTargetEdge()
        {
            Vector3 localStart = new Vector3(3f, 0, 1f), localEnd = new Vector3(3f, 0, 11f);
            Vector3 worldStart = new Vector3(-4f, 0, 7f), worldEnd = new Vector3(4.66025f, 0, 12f);
            RoomPlacement placement = RoomPlacement.Align(localStart, localEnd, worldStart, worldEnd);

            Assert.That(Vector3.Distance(placement.TransformPoint(localStart), worldStart), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(placement.TransformPoint(localEnd), worldEnd), Is.LessThan(1e-3f));
        }

        private static GenerationParameters Parameters(Action<GenerationParameters> configure, int seed)
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            configure(parameters);
            parameters.RandomSeed = seed;
            return parameters;
        }

        private static void AssertLevelIsValid(MixedLevel level, GenerationParameters parameters, string context)
        {
            float tolerance = parameters.CellSize * 1e-3f;

            // No two rooms overlap.
            var cells = level.Rooms.SelectMany(room => room.Room.Cells.Select(cell =>
                (room, corners: room.GetWorldCorners(cell), center: room.GetWorldCenter(cell)))).ToList();
            for (int i = 0; i < cells.Count; i++)
            {
                for (int j = i + 1; j < cells.Count; j++)
                {
                    if (cells[i].room == cells[j].room || (cells[i].center - cells[j].center).magnitude > parameters.CellSize * 3f)
                        continue;

                    Assert.That(PlanarGeometry.ConvexPolygonsOverlap(cells[i].corners, cells[j].corners, tolerance), Is.False,
                        $"{context}: rooms {cells[i].room.RoomID} and {cells[j].room.RoomID} overlap");
                }
            }

            // Every link is an exactly shared edge with matching flags.
            HashSet<(int, CellCoord, int)> linkedEdges = new HashSet<(int, CellCoord, int)>();
            foreach (RoomLink link in level.Links)
            {
                (Vector3 aStart, Vector3 aEnd) = level.GetRoom(link.RoomA).GetWorldEdge(link.CellA, link.EdgeA);
                (Vector3 bStart, Vector3 bEnd) = level.GetRoom(link.RoomB).GetWorldEdge(link.CellB, link.EdgeB);
                Assert.That(PlanarGeometry.GetContact(aStart, aEnd, bStart, bEnd, tolerance), Is.EqualTo(PlanarGeometry.SegmentContact.ExactOpposite),
                    $"{context}: {link} is not an exactly shared edge");

                WallFlag expected = link.IsDoor ? WallFlag.HasDoor : WallFlag.Wall;
                Assert.That(level.GetRoom(link.RoomA).Grid.GetCell(link.CellA).GetEdgeFlag(link.EdgeA), Is.EqualTo(expected), $"{context}: {link}");
                Assert.That(level.GetRoom(link.RoomB).Grid.GetCell(link.CellB).GetEdgeFlag(link.EdgeB), Is.EqualTo(expected), $"{context}: {link}");

                linkedEdges.Add((link.RoomA, link.CellA, link.EdgeA));
                linkedEdges.Add((link.RoomB, link.CellB, link.EdgeB));
            }

            // Rooms touch only along exactly shared edges, and every such edge is linked.
            var outer = level.Rooms.SelectMany(room => room.GetOuterEdges().Select(e => (room, e.cell, e.edge, segment: room.GetWorldEdge(e.cell, e.edge)))).ToList();
            for (int i = 0; i < outer.Count; i++)
            {
                for (int j = i + 1; j < outer.Count; j++)
                {
                    if (outer[i].room == outer[j].room)
                        continue;

                    Vector3 middleI = (outer[i].segment.Item1 + outer[i].segment.Item2) / 2f;
                    Vector3 middleJ = (outer[j].segment.Item1 + outer[j].segment.Item2) / 2f;
                    if ((middleI - middleJ).magnitude > parameters.CellSize * 1.5f)
                        continue;

                    PlanarGeometry.SegmentContact contact = PlanarGeometry.GetContact(
                        outer[i].segment.Item1, outer[i].segment.Item2, outer[j].segment.Item1, outer[j].segment.Item2, tolerance);

                    Assert.That(contact, Is.Not.EqualTo(PlanarGeometry.SegmentContact.PartialOverlap),
                        $"{context}: rooms {outer[i].room.RoomID} and {outer[j].room.RoomID} have partly overlapping edges");

                    if (contact == PlanarGeometry.SegmentContact.ExactOpposite)
                    {
                        Assert.That(linkedEdges.Contains((outer[i].room.RoomID, outer[i].cell, outer[i].edge)), Is.True,
                            $"{context}: shared edge between rooms {outer[i].room.RoomID} and {outer[j].room.RoomID} is not linked");
                    }
                }
            }

            foreach (PlacedRoom room in level.Rooms)
            {
                Assert.That(room.Room.Cells.Count, Is.InRange(parameters.MinCellsPerRoom, parameters.MaxCellsPerRoom), $"{context}: room {room.RoomID}");
                Assert.That(room.ConnectionCount, Is.LessThanOrEqualTo(parameters.MaxConnectionsPerRoom), $"{context}: room {room.RoomID}");
                foreach (int other in room.ConnectedRoomIDs)
                    Assert.That(level.GetRoom(other).Doors.ContainsKey(room.RoomID), Is.True, $"{context}: door {room.RoomID}-{other} is one-sided");
            }

            Assert.That(level.Doors.GroupBy(d => (Math.Min(d.RoomA, d.RoomB), Math.Max(d.RoomA, d.RoomB))).All(g => g.Count() == 1), Is.True,
                $"{context}: a pair of rooms has more than one door");

            // Every room is reachable from the first one through doors.
            HashSet<int> reached = new HashSet<int> { 0 };
            Queue<int> queue = new Queue<int>(reached);
            while (queue.Count > 0)
            {
                foreach (int next in level.GetRoom(queue.Dequeue()).ConnectedRoomIDs)
                {
                    if (reached.Add(next))
                        queue.Enqueue(next);
                }
            }
            Assert.That(reached.Count, Is.EqualTo(level.Rooms.Count), $"{context}: not every room is reachable");
        }

        private static string Fingerprint(MixedLevel level)
        {
            StringBuilder builder = new StringBuilder();
            foreach (PlacedRoom room in level.Rooms)
            {
                builder.Append(room.GridType).Append('@').Append(room.Placement.x.ToString("R")).Append(',')
                    .Append(room.Placement.z.ToString("R")).Append(',').Append(room.Placement.angle.ToString("R")).Append(':');
                foreach (CellCoord cell in room.Room.Cells)
                    builder.Append(cell);
                builder.Append(';');
            }
            foreach (RoomLink link in level.Links)
                builder.Append(link).Append(';');
            return builder.ToString();
        }
    }
}
