using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using CRG.Building;
using CRG.Core;
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
        public void RoomSettings_ComeFromTheOldestCell_ThroughMergesAndSplits()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell a = layout.AddFirstCell(CellShape.Square);
            LayoutCell b = layout.Attach(a.id, 0, CellShape.Square);
            a.roomName = "Hall";
            a.roomCeiling = RoomCeiling.Off;
            b.roomName = "Boss";
            b.roomTag = "Boss";
            b.roomCeiling = RoomCeiling.On;

            LayoutRooms rooms = layout.GetRooms();
            Assert.AreEqual("Hall", rooms.Settings[0].roomName);
            Assert.AreEqual("Boss", rooms.Settings[1].roomTag);
            Assert.IsFalse(rooms.HasCeiling(0, true), "Off wins over the level setting");
            Assert.IsTrue(rooms.HasCeiling(1, false), "On wins over the level setting");

            layout.Links[0].state = EdgeState.Open;
            rooms = layout.GetRooms();
            Assert.AreEqual(1, rooms.Rooms.Count);
            Assert.AreEqual("Hall", rooms.Settings[0].roomName, "The older room's settings win when rooms merge");

            layout.Links[0].state = EdgeState.Wall;
            rooms = layout.GetRooms();
            Assert.AreEqual("Boss", rooms.Settings[1].roomName, "A split room gets its oldest cell's settings back");

            b.roomCeiling = RoomCeiling.LevelSetting;
            Assert.IsTrue(layout.GetRooms().HasCeiling(1, true));
            Assert.IsFalse(layout.GetRooms().HasCeiling(1, false));
        }

        [Test]
        public void StartAndEndRooms_AreAutomaticOrChosen()
        {
            // A straight chain of four squares joined by doors.
            LevelLayout layout = new LevelLayout();
            LayoutCell a = layout.AddFirstCell(CellShape.Square);
            LayoutCell b = layout.Attach(a.id, 0, CellShape.Square);
            LayoutCell c = layout.Attach(b.id, 2, CellShape.Square);
            LayoutCell d = layout.Attach(c.id, 2, CellShape.Square);

            LayoutRooms rooms = layout.GetRooms();
            Assert.AreEqual(0, rooms.StartRoom, "The oldest cell's room starts by default");
            Assert.AreEqual(3, rooms.EndRoom, "The farthest room ends by default");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, rooms.DistanceFromStart);

            layout.StartCell = c.id;
            rooms = layout.GetRooms();
            Assert.AreEqual(2, rooms.StartRoom);
            Assert.AreEqual(0, rooms.EndRoom, "Ties for the farthest room go to the lowest room");
            CollectionAssert.AreEqual(new[] { 2, 1, 0, 1 }, rooms.DistanceFromStart);

            layout.EndCell = b.id;
            Assert.AreEqual(1, layout.GetRooms().EndRoom, "A chosen end room wins");

            layout.EndCell = c.id;
            Assert.AreEqual(0, layout.GetRooms().EndRoom, "An end in the start room falls back to automatic");

            layout.Remove(d.id);
            layout.EndCell = b.id;
            layout.GetLink(b.id, 2).state = EdgeState.Open;
            Assert.AreEqual(1, layout.GetRooms().StartRoom, "The start follows its cell into a merged room");

            layout.Remove(c.id);
            Assert.AreEqual(-1, layout.StartCell, "Removing the chosen cell makes the start automatic again");
            Assert.AreEqual(0, layout.GetRooms().StartRoom);

            layout.Clear();
            Assert.AreEqual(-1, layout.EndCell);
        }

        [Test]
        public void Place_PutsACellAnywhere_AndLinksTouchingEdges()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell a = layout.Place(CellShape.Square, new RoomPlacement(0, 0, 0), EdgeState.Wall, out string reason);
            Assert.IsNotNull(a, reason);

            // Not touching anything is allowed when placing.
            LayoutCell far = layout.Place(CellShape.Hexagon, new RoomPlacement(10, 10, 0), EdgeState.Wall, out reason);
            Assert.IsNotNull(far, reason);
            Assert.AreEqual(0, layout.Links.Count);

            // A square right of the first one shares its east edge.
            LayoutCell b = layout.Place(CellShape.Square, new RoomPlacement(1, 0, 0), EdgeState.Open, out reason);
            Assert.IsNotNull(b, reason);
            Assert.AreEqual(1, layout.Links.Count);
            Assert.AreEqual(EdgeState.Open, layout.Links[0].state);
            Assert.AreEqual((a.id, b.id), (layout.Links[0].cellA, layout.Links[0].cellB));

            Assert.IsNull(layout.Place(CellShape.Square, new RoomPlacement(1.5, 0, 0), EdgeState.Wall, out reason));
            StringAssert.Contains("overlap", reason);
            Assert.IsNull(layout.Place(CellShape.Square, new RoomPlacement(0.5, 1, 0), EdgeState.Wall, out reason));
            StringAssert.Contains("partly", reason);
            Assert.AreEqual(3, layout.Cells.Count);
        }

        // Undo, redo and scene loads deserialize the layout, which can replace its cell and link objects; lookups
        // must then return the new objects.
        [Test]
        public void Lookups_FollowDeserialization()
        {
            LevelLayout layout = new LevelLayout();
            LayoutCell a = layout.AddFirstCell(CellShape.Square);
            layout.Attach(a.id, 0, CellShape.Square);
            string before = JsonUtility.ToJson(layout);

            Assert.AreEqual(EdgeState.Door, layout.GetLink(a.id, 0).state);
            layout.GetLink(a.id, 0).state = EdgeState.Open;

            JsonUtility.FromJsonOverwrite(before, layout);
            Assert.AreSame(layout.Links[0], layout.GetLink(a.id, 0), "the lookup returns the deserialized link");
            Assert.AreEqual(EdgeState.Door, layout.GetLink(a.id, 0).state);
            Assert.AreSame(layout.Cells[0], layout.GetCell(a.id));
            Assert.AreEqual(2, layout.GetRooms().Rooms.Count);
        }

        [Test]
        public void LargeLayouts_StayFast()
        {
            System.Random random = new System.Random(1);
            LevelLayout layout = new LevelLayout();
            layout.AddFirstCell(CellShape.Hexagon);

            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            for (int attempt = 0; attempt < 20000 && layout.Cells.Count < 1500; attempt++)
            {
                LayoutCell cell = layout.Cells[random.Next(layout.Cells.Count)];
                layout.Attach(cell.id, random.Next(CellShapes.GetEdgeCount(cell.shape)), CellShapes.All[random.Next(CellShapes.All.Length)]);
            }
            Assert.AreEqual(1500, layout.Cells.Count);
            Assert.Less(watch.ElapsedMilliseconds, 5000, "building 1500 cells");

            foreach (LayoutLink link in layout.Links)
                link.state = (EdgeState)random.Next(3);

            watch.Restart();
            for (int i = 0; i < 20; i++)
                layout.GetRooms();
            Assert.Less(watch.ElapsedMilliseconds, 2000, "finding rooms 20 times");

            watch.Restart();
            for (int i = 0; i < 500; i++)
            {
                Vector3 point = layout.GetCenter(layout.Cells[random.Next(layout.Cells.Count)]);
                layout.FindCellAt(point);
                layout.FindNearestEdge(point + new Vector3(0.3f, 0f, 0f), 0.2f, null, out _, out _);
            }
            Assert.Less(watch.ElapsedMilliseconds, 2000, "picking 500 times");
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
            // b sits on a with its own edge 0, so its opposite edge 2 continues the chain.
            LayoutCell c = layout.Attach(b.id, 2, CellShape.Square);
            Assert.IsNotNull(c, "a straight chain of three squares");

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
