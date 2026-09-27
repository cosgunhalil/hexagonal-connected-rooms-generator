using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CRG.Building;
using CRG.Generation;
using CRG.Runtime;

namespace CRG.Tests
{
    public class LevelLayoutTests
    {
        private const float Tolerance = 1e-3f;

        private static IEnumerable<TestCaseData> ShapePairs()
        {
            foreach (CellShape a in CellShapes.All)
            {
                foreach (CellShape b in CellShapes.All)
                    yield return new TestCaseData(a, b).SetName($"Attach_{b}_OnEveryEdgeOf_{a}");
            }
        }

        [TestCaseSource(nameof(ShapePairs))]
        public void Attach_EveryShape_OnEveryEdge_SharesTheEdgeExactly(CellShape first, CellShape attached)
        {
            for (int edge = 0; edge < CellShapes.GetEdgeCount(first); edge++)
            {
                LevelLayout layout = new LevelLayout();
                LayoutCell a = layout.AddFirstCell(first);
                AttachPlan plan = layout.PlanAttach(a.id, edge, attached);
                Assert.IsTrue(plan.Fits, $"Edge {edge}: {plan.FailureReason}");

                LayoutCell b = layout.Attach(plan);
                Assert.AreEqual(1, layout.Links.Count);
                LayoutLink link = layout.Links[0];
                Assert.AreEqual(EdgeState.Door, link.state, "The attach edge becomes a door");
                Assert.AreEqual((a.id, edge, b.id), (link.cellA, link.edgeA, link.cellB));

                (Vector3 start, Vector3 end) = layout.GetEdge(a, edge);
                (Vector3 otherStart, Vector3 otherEnd) = layout.GetEdge(b, link.edgeB);
                Assert.Less(Vector3.Distance(start, otherEnd), Tolerance, $"Edge {edge}");
                Assert.Less(Vector3.Distance(end, otherStart), Tolerance, $"Edge {edge}");
                Assert.IsFalse(PlanarGeometry.ConvexPolygonsOverlap(layout.GetCorners(a), layout.GetCorners(b), Tolerance));

                Assert.IsFalse(layout.PlanAttach(a.id, edge, attached).Fits, "A shared edge can't take another cell");
            }
        }

        [Test]
        public void Attach_OverlappingCell_IsRefused()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell hex = layout.AddFirstCell(CellShape.Hexagon);
            Assert.IsNotNull(layout.Attach(hex.id, 0, CellShape.Octagon));

            // Octagons on neighboring hexagon edges would overlap each other.
            AttachPlan plan = layout.PlanAttach(hex.id, 1, CellShape.Octagon);
            Assert.IsFalse(plan.Fits);
            StringAssert.Contains("overlap", plan.FailureReason);
            Assert.IsNull(layout.Attach(plan));
            Assert.AreEqual(2, layout.Cells.Count);
        }

        [Test]
        public void Attach_IntoAGap_LinksEveryTouchingEdge_AsWalls()
        {
            // Two squares on neighboring hexagon edges leave a 60 degree gap that a triangle fills exactly.
            LevelLayout layout = new LevelLayout();
            LayoutCell hex = layout.AddFirstCell(CellShape.Hexagon);
            LayoutCell square = layout.Attach(hex.id, 0, CellShape.Square);
            Assert.IsNotNull(layout.Attach(hex.id, 1, CellShape.Square));

            AttachPlan gap = Enumerable.Range(0, 4)
                .Select(edge => layout.PlanAttach(square.id, edge, CellShape.Triangle))
                .FirstOrDefault(plan => plan.Fits && plan.Contacts.Count == 2);
            Assert.IsNotNull(gap, "A triangle fits between the squares, touching both");

            layout.Attach(gap);
            Assert.AreEqual(3, layout.Links.Count(link => link.state == EdgeState.Door));
            Assert.AreEqual(1, layout.Links.Count(link => link.state == EdgeState.Wall), "The edge touched by chance is a wall");
        }

        [Test]
        public void AddFirstCell_OnlyWhenEmpty()
        {
            LevelLayout layout = new LevelLayout();
            layout.AddFirstCell(CellShape.Square);
            Assert.Throws<InvalidOperationException>(() => layout.AddFirstCell(CellShape.Square));
        }

        [Test]
        public void OpenEdges_JoinCellsIntoRooms_AndDoorsDecideReachability()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell a = layout.AddFirstCell(CellShape.Hexagon);
            LayoutCell b = layout.Attach(a.id, 0, CellShape.Triangle);
            int freeEdge = Enumerable.Range(0, 3).First(edge => layout.IsOuterEdge(b.id, edge));
            LayoutCell c = layout.Attach(b.id, freeEdge, CellShape.Square);

            LayoutRooms rooms = layout.GetRooms();
            Assert.AreEqual(3, rooms.Rooms.Count);
            CollectionAssert.IsEmpty(rooms.Unreachable);

            layout.GetLink(a.id, 0).state = EdgeState.Open;
            rooms = layout.GetRooms();
            Assert.AreEqual(2, rooms.Rooms.Count, "Open edges join cells into one room");
            Assert.AreEqual(0, rooms.RoomOfCell[a.id]);
            Assert.AreEqual(0, rooms.RoomOfCell[b.id]);
            Assert.AreEqual(1, rooms.RoomOfCell[c.id]);

            layout.GetLink(c.id, layout.Links.First(link => link.cellB == c.id).edgeB).state = EdgeState.Wall;
            rooms = layout.GetRooms();
            CollectionAssert.AreEqual(new[] { 1 }, rooms.Unreachable, "A room behind a wall can't be reached");
        }

        [Test]
        public void Rooms_AreOrderedByTheirOldestCell()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell a = layout.AddFirstCell(CellShape.Square);
            LayoutCell b = layout.Attach(a.id, 0, CellShape.Square);
            layout.Attach(b.id, 1, CellShape.Square);

            layout.Remove(a.id);
            LayoutRooms rooms = layout.GetRooms();
            Assert.AreEqual(0, rooms.RoomOfCell[b.id], "The oldest remaining cell's room is the start room");
            Assert.AreEqual(0, rooms.StartRoom);
        }

        [Test]
        public void EdgeStates_CycleDoorWallOpen()
        {
            Assert.AreEqual(EdgeState.Wall, LevelLayout.GetNextState(EdgeState.Door));
            Assert.AreEqual(EdgeState.Open, LevelLayout.GetNextState(EdgeState.Wall));
            Assert.AreEqual(EdgeState.Door, LevelLayout.GetNextState(EdgeState.Open));
        }

        [Test]
        public void Remove_RefusesToSplitTheLevel_AndDropsLinks()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell a = layout.AddFirstCell(CellShape.Square);
            LayoutCell b = layout.Attach(a.id, 0, CellShape.Square);
            LayoutCell c = layout.Attach(b.id, 0, CellShape.Square);

            Assert.IsFalse(layout.CanRemove(b.id, out string reason));
            StringAssert.Contains("split", reason);
            Assert.IsFalse(layout.Remove(b.id));

            Assert.IsTrue(layout.Remove(c.id));
            Assert.IsNull(layout.GetCell(c.id));
            Assert.AreEqual(1, layout.Links.Count);

            Assert.IsTrue(layout.Remove(a.id));
            Assert.IsTrue(layout.Remove(b.id));
            Assert.IsTrue(layout.IsEmpty);
        }

        [Test]
        public void Picking_FindsCellsAndEdges()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell square = layout.AddFirstCell(CellShape.Square);

            Assert.AreEqual(square.id, layout.FindCellAt(Vector3.zero));
            Assert.AreEqual(-1, layout.FindCellAt(new Vector3(2f, 0f, 0f)));

            Assert.IsTrue(layout.FindNearestEdge(new Vector3(0.45f, 0f, 0f), 0.2f, null, out int cell, out int edge));
            Assert.AreEqual(square.id, cell);
            (Vector3 start, Vector3 end) = layout.GetEdge(square, edge);
            Assert.AreEqual(0.5f, (start.x + end.x) / 2f, Tolerance, "The east edge is closest");

            Assert.IsFalse(layout.FindNearestEdge(new Vector3(0.45f, 0f, 0f), 0.2f, (c, e) => false, out _, out _));
        }

        // Random building and removing keeps every rule: no overlaps, no partly shared edges, and every exactly
        // shared edge is linked once.
        [Test]
        public void RandomEditing_KeepsTheLayoutConsistent([NUnit.Framework.Range(0, 9)] int seed)
        {
            System.Random random = new System.Random(seed);
            LevelLayout layout = new LevelLayout();
            layout.AddFirstCell(CellShapes.All[random.Next(CellShapes.All.Length)]);

            for (int step = 0; step < 120; step++)
            {
                List<LayoutCell> cells = layout.Cells.ToList();
                if (random.NextDouble() < 0.15 && cells.Count > 1)
                {
                    layout.Remove(cells[random.Next(cells.Count)].id);
                    continue;
                }

                if (random.NextDouble() < 0.1 && layout.Links.Count > 0)
                    layout.Links[random.Next(layout.Links.Count)].state = (EdgeState)random.Next(3);

                LayoutCell cell = cells[random.Next(cells.Count)];
                layout.Attach(cell.id, random.Next(CellShapes.GetEdgeCount(cell.shape)), CellShapes.All[random.Next(CellShapes.All.Length)]);
            }

            List<LayoutCell> placed = layout.Cells.ToList();
            int linkedPairs = 0;

            for (int i = 0; i < placed.Count; i++)
            {
                for (int j = i + 1; j < placed.Count; j++)
                {
                    Assert.IsFalse(PlanarGeometry.ConvexPolygonsOverlap(layout.GetCorners(placed[i]), layout.GetCorners(placed[j]), Tolerance),
                        $"Cells {placed[i].id} and {placed[j].id} overlap");

                    for (int edgeI = 0; edgeI < CellShapes.GetEdgeCount(placed[i].shape); edgeI++)
                    {
                        for (int edgeJ = 0; edgeJ < CellShapes.GetEdgeCount(placed[j].shape); edgeJ++)
                        {
                            (Vector3 a0, Vector3 a1) = layout.GetEdge(placed[i], edgeI);
                            (Vector3 b0, Vector3 b1) = layout.GetEdge(placed[j], edgeJ);
                            PlanarGeometry.SegmentContact contact = PlanarGeometry.GetContact(a0, a1, b0, b1, Tolerance);
                            Assert.AreNotEqual(PlanarGeometry.SegmentContact.PartialOverlap, contact);

                            LayoutLink link = layout.GetLink(placed[i].id, edgeI);
                            bool linked = link != null && link.Involves(placed[j].id, edgeJ);
                            Assert.AreEqual(contact == PlanarGeometry.SegmentContact.ExactOpposite, linked,
                                $"Cell {placed[i].id} edge {edgeI} / cell {placed[j].id} edge {edgeJ}");
                            if (linked)
                                linkedPairs++;
                        }
                    }
                }
            }

            Assert.AreEqual(layout.Links.Count, linkedPairs, "Every link joins two touching edges");
            Assert.AreEqual(placed.Count, layout.GetRooms().Rooms.Sum(room => room.Count));
        }

        [Test]
        public void Builder_ScalesTheLayoutByCellSize_AndPrefersTheParametersAsset()
        {
            GameObject go = new GameObject("Builder");
            GenerationParametersAsset asset = ScriptableObject.CreateInstance<GenerationParametersAsset>();
            try
            {
                CRGLevelBuilder builder = go.AddComponent<CRGLevelBuilder>();
                builder.parameters = GenerationParameters.CreateDefault();
                builder.parameters.CellSize = 4f;

                Assert.AreEqual(new Vector3(4f, 0f, 2f), builder.LayoutToLocal(new Vector3(1f, 0f, 0.5f)));
                Assert.AreEqual(new Vector3(1f, 0f, 0.5f), builder.LocalToLayout(new Vector3(4f, 0f, 2f)));

                asset.parameters = GenerationParameters.CreateDefault();
                asset.parameters.CellSize = 7f;
                builder.parametersAsset = asset;
                Assert.AreEqual(7f, builder.CellSize, "The asset's settings win when it is set");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
