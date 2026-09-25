using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CRG.Core;
using CRG.Geometry;

namespace CRG.Tests
{
    // Invariants every grid topology must satisfy; runs for every GridType.
    public class GridTopologyTests
    {
        private const float CellSize = 10f;
        private const float Tolerance = 1e-3f;

        private static IEnumerable<GridType> AllGridTypes()
        {
            return Enum.GetValues(typeof(GridType)).Cast<GridType>();
        }

        // A patch of cells around the origin, including every variant the topology uses there.
        private static List<CellCoord> SampleCells(IGridTopology topology)
        {
            HashSet<CellCoord> cells = new HashSet<CellCoord> { new CellCoord(0, 0) };
            for (int ring = 0; ring < 3; ring++)
            {
                foreach (CellCoord cell in cells.ToList())
                {
                    foreach (CellCoord neighbor in topology.GetNeighbors(cell))
                        cells.Add(neighbor);
                }
            }
            return cells.ToList();
        }

        [TestCaseSource(nameof(AllGridTypes))]
        public void NeighborEdge_PointsBackToTheSameEdge(GridType type)
        {
            IGridTopology topology = GridTopology.Get(type);

            foreach (CellCoord cell in SampleCells(topology))
            {
                for (int edge = 0; edge < topology.GetEdgeCount(cell); edge++)
                {
                    CellCoord neighbor = topology.GetNeighbor(cell, edge);
                    int neighborEdge = topology.GetNeighborEdge(cell, edge);

                    Assert.That(topology.GetNeighbor(neighbor, neighborEdge), Is.EqualTo(cell), $"{cell} edge {edge}");
                    Assert.That(topology.GetNeighborEdge(neighbor, neighborEdge), Is.EqualTo(edge), $"{cell} edge {edge}");
                }
            }
        }

        [TestCaseSource(nameof(AllGridTypes))]
        public void SharedEdge_CoincidesFromBothSides(GridType type)
        {
            IGridTopology topology = GridTopology.Get(type);

            foreach (CellCoord cell in SampleCells(topology))
            {
                for (int edge = 0; edge < topology.GetEdgeCount(cell); edge++)
                {
                    CellCoord neighbor = topology.GetNeighbor(cell, edge);
                    (Vector3 start, Vector3 end) = CellGeometry.GetEdgeWorldVertices(topology, cell, edge, CellSize);
                    (Vector3 otherStart, Vector3 otherEnd) = CellGeometry.GetEdgeWorldVertices(
                        topology, neighbor, topology.GetNeighborEdge(cell, edge), CellSize);

                    // Seen from the neighbor, the same edge runs in the opposite direction.
                    Assert.That(Vector3.Distance(start, otherEnd), Is.LessThan(Tolerance), $"{cell} edge {edge}");
                    Assert.That(Vector3.Distance(end, otherStart), Is.LessThan(Tolerance), $"{cell} edge {edge}");
                }
            }
        }

        [TestCaseSource(nameof(AllGridTypes))]
        public void Edges_HaveCellSizeLength_AndNormalsPointOutTowardsTheNeighbor(GridType type)
        {
            IGridTopology topology = GridTopology.Get(type);

            foreach (CellCoord cell in SampleCells(topology))
            {
                Vector3 center = topology.GetCellCenter(cell, CellSize);

                for (int edge = 0; edge < topology.GetEdgeCount(cell); edge++)
                {
                    (Vector3 start, Vector3 end) = topology.GetEdgeOffsets(cell, edge, CellSize);
                    Vector3 normal = topology.GetEdgeNormal(cell, edge);
                    Vector3 toNeighbor = topology.GetCellCenter(topology.GetNeighbor(cell, edge), CellSize) - center;

                    Assert.That(Vector3.Distance(start, end), Is.EqualTo(CellSize).Within(Tolerance), $"{cell} edge {edge}");
                    Assert.That(normal.magnitude, Is.EqualTo(1f).Within(Tolerance));
                    Assert.That(Mathf.Abs(Vector3.Dot(normal, end - start)), Is.LessThan(Tolerance), "normal must be perpendicular to the edge");
                    Assert.That(Vector3.Dot(normal, (start + end) / 2f), Is.GreaterThan(0f), "normal must point out of the cell");
                    Assert.That(Vector3.Dot(normal, toNeighbor), Is.GreaterThan(0f), "normal must point towards the neighbor");
                }
            }
        }

        [TestCaseSource(nameof(AllGridTypes))]
        public void Corners_AreCounterClockwiseAndConvex(GridType type)
        {
            IGridTopology topology = GridTopology.Get(type);

            foreach (CellCoord cell in SampleCells(topology))
            {
                int count = topology.GetEdgeCount(cell);
                for (int i = 0; i < count; i++)
                {
                    Vector3 a = topology.GetCornerOffset(cell, i, CellSize);
                    Vector3 b = topology.GetCornerOffset(cell, (i + 1) % count, CellSize);
                    Vector3 c = topology.GetCornerOffset(cell, (i + 2) % count, CellSize);

                    // Counter-clockwise seen from above, in the XZ plane with +Z up on screen.
                    float turn = (b.x - a.x) * (c.z - b.z) - (b.z - a.z) * (c.x - b.x);
                    Assert.That(turn, Is.GreaterThan(0f), $"{cell} corner {i}");
                }
            }
        }

        [TestCaseSource(nameof(AllGridTypes))]
        public void CellAt_RoundTripsCenters(GridType type)
        {
            IGridTopology topology = GridTopology.Get(type);

            foreach (CellCoord cell in SampleCells(topology))
            {
                Assert.That(topology.GetCellAt(topology.GetCellCenter(cell, CellSize), CellSize), Is.EqualTo(cell));
            }
        }

        [TestCaseSource(nameof(AllGridTypes))]
        public void Distance_IsZeroForSelfAndOneForNeighbors(GridType type)
        {
            IGridTopology topology = GridTopology.Get(type);

            foreach (CellCoord cell in SampleCells(topology))
            {
                Assert.That(topology.GetDistance(cell, cell), Is.Zero);
                foreach (CellCoord neighbor in topology.GetNeighbors(cell))
                    Assert.That(topology.GetDistance(cell, neighbor), Is.EqualTo(1), $"{cell} -> {neighbor}");
            }
        }

        [Test]
        public void Hexagon_NeighborsLieAtSixtyDegreeSteps([NUnit.Framework.Range(0, 5)] int direction)
        {
            IGridTopology topology = GridTopology.Get(GridType.Hexagon);
            Vector3 position = topology.GetCellCenter(topology.GetNeighbor(new CellCoord(0, 0), direction), CellSize);
            float angle = (Mathf.Atan2(position.z, position.x) * Mathf.Rad2Deg + 360f) % 360f;

            Assert.That(angle, Is.EqualTo(60f * direction).Within(0.01f));
        }

        [Test]
        public void DoorWall_HasCenteredOpeningOfRequestedWidth()
        {
            IGridTopology topology = GridTopology.Get(GridType.Hexagon);
            const float wallHeight = 3f;
            const float doorHeight = 2.5f;
            float doorWidth = CellGeometry.CalculateDoorWidth(CellSize, 0.2f);

            List<WallSegment> segments = CellGeometry.GetDoorWallSegments(
                topology, new CellCoord(0, 0), 0, CellSize, wallHeight, doorWidth, doorHeight);

            Assert.That(segments.Count, Is.EqualTo(3), "left side, right side and lintel");

            WallSegment left = segments[0];
            WallSegment right = segments[1];
            WallSegment lintel = segments[2];

            Assert.That(Vector3.Distance(left.End, right.Start), Is.EqualTo(doorWidth).Within(Tolerance));
            Assert.That(Vector3.Distance(left.Start, left.End), Is.EqualTo(Vector3.Distance(right.Start, right.End)).Within(Tolerance));
            Assert.That(lintel.Bottom, Is.EqualTo(doorHeight).Within(Tolerance));
            Assert.That(lintel.Top, Is.EqualTo(wallHeight).Within(Tolerance));
        }

        [Test]
        public void DoorWall_FullWidthDoorLeavesOnlyLintel()
        {
            IGridTopology topology = GridTopology.Get(GridType.Hexagon);
            List<WallSegment> segments = CellGeometry.GetDoorWallSegments(
                topology, new CellCoord(0, 0), 0, CellSize, 3f, CellGeometry.CalculateDoorWidth(CellSize, 1f), 2.5f);

            Assert.That(segments.Count, Is.EqualTo(1));
            Assert.That(segments[0].Bottom, Is.EqualTo(2.5f).Within(Tolerance));
        }
    }
}
