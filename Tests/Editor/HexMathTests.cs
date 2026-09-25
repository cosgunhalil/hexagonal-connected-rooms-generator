using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CRG.Core;
using CRG.Geometry;
using RangeAttribute = NUnit.Framework.RangeAttribute;

namespace CRG.Tests
{
    public class HexMathTests
    {
        private const float HexSize = 10f;
        private const float Tolerance = 1e-3f;

        [Test]
        public void NeighborDirection_LiesAtSixtyDegreeSteps([Range(0, 5)] int direction)
        {
            Vector3 position = new AxialCoord(0, 0).GetNeighbor(direction).ToWorldPosition(HexSize);
            float angle = (Mathf.Atan2(position.z, position.x) * Mathf.Rad2Deg + 360f) % 360f;

            Assert.That(angle, Is.EqualTo(60f * direction).Within(0.01f));
        }

        [Test]
        public void EdgeNormal_PointsTowardsNeighbor([Range(0, 5)] int edge)
        {
            Vector3 toNeighbor = new AxialCoord(0, 0).GetNeighbor(edge).ToWorldPosition(HexSize).normalized;

            Assert.That(Vector3.Distance(HexMath.GetEdgeNormal(edge), toNeighbor), Is.LessThan(Tolerance));
        }

        [Test]
        public void SharedEdge_CoincidesWithNeighborsOppositeEdge([Range(0, 5)] int edge)
        {
            AxialCoord neighbor = new AxialCoord(0, 0).GetNeighbor(edge);
            Vector3 offset = neighbor.ToWorldPosition(HexSize);

            (Vector3 start, Vector3 end) = HexMath.GetEdgeVertices(edge, HexSize);
            (Vector3 otherStart, Vector3 otherEnd) = HexMath.GetEdgeVertices(HexDirection.GetOppositeDirection(edge), HexSize);

            // Seen from the neighbor, the same edge runs in the opposite direction.
            Assert.That(Vector3.Distance(start, otherEnd + offset), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(end, otherStart + offset), Is.LessThan(Tolerance));
        }

        [Test]
        public void EdgeLength_EqualsHexSize([Range(0, 5)] int edge)
        {
            (Vector3 start, Vector3 end) = HexMath.GetEdgeVertices(edge, HexSize);

            Assert.That(Vector3.Distance(start, end), Is.EqualTo(HexSize).Within(Tolerance));
        }

        [Test]
        public void WorldPosition_RoundTripsToSameCoordinate()
        {
            for (int q = -5; q <= 5; q++)
            {
                for (int r = -5; r <= 5; r++)
                {
                    AxialCoord coord = new AxialCoord(q, r);
                    Assert.That(AxialCoord.FromWorldPosition(coord.ToWorldPosition(HexSize), HexSize), Is.EqualTo(coord));
                }
            }
        }

        [Test]
        public void DoorWall_HasCenteredOpeningOfRequestedWidth()
        {
            const float wallHeight = 3f;
            const float doorHeight = 2.5f;
            float doorWidth = HexMath.CalculateDoorWidth(HexSize, 0.2f);

            List<WallSegment> segments = HexGeometry.GetDoorWallSegments(
                new AxialCoord(0, 0), 0, HexSize, wallHeight, doorWidth, doorHeight);

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
            List<WallSegment> segments = HexGeometry.GetDoorWallSegments(
                new AxialCoord(0, 0), 0, HexSize, 3f, HexMath.CalculateDoorWidth(HexSize, 1f), 2.5f);

            Assert.That(segments.Count, Is.EqualTo(1));
            Assert.That(segments[0].Bottom, Is.EqualTo(2.5f).Within(Tolerance));
        }
    }
}
