using System;
using System.Collections.Generic;
using System.Linq;
using CRG.Core;
using CRG.Generation;
using UnityEngine;

namespace CRG.Building
{
    // Shapes a hand-built level can use. Every shape has edges one cell size long, so any edge of one cell can be
    // matched exactly by an edge of any other.
    public enum CellShape
    {
        Triangle,
        Square,
        Hexagon,
        Octagon
    }

    // What a shared edge between two cells becomes: a door, a wall, or nothing (the cells form one room).
    public enum EdgeState
    {
        Door,
        Wall,
        Open
    }

    // Whether a room gets a ceiling: as the level's Add Ceiling setting says, or always, or never.
    public enum RoomCeiling
    {
        [InspectorName("Use Level Setting")]
        LevelSetting,
        On,
        Off
    }

    public static class CellShapes
    {
        public static readonly CellShape[] All = { CellShape.Triangle, CellShape.Square, CellShape.Hexagon, CellShape.Octagon };

        // The grid a shape comes from; the shape is that grid's cell (0, 0, variant 0).
        public static GridType GetGridType(CellShape shape)
        {
            switch (shape)
            {
                case CellShape.Triangle: return GridType.Triangle;
                case CellShape.Square: return GridType.Square;
                case CellShape.Hexagon: return GridType.Hexagon;
                case CellShape.Octagon: return GridType.OctagonSquare;
                default: throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown cell shape");
            }
        }

        public static IGridTopology GetTopology(CellShape shape)
        {
            return GridTopology.Get(GetGridType(shape));
        }

        public static CellCoord GetCell(CellShape shape)
        {
            return new CellCoord(0, 0);
        }

        public static int GetEdgeCount(CellShape shape)
        {
            return GetTopology(shape).GetEdgeCount(GetCell(shape));
        }

        // Corner i relative to the cell center, for an edge length of 1 (same corner and edge order as the topology).
        public static Vector3 GetCorner(CellShape shape, int corner)
        {
            return GetTopology(shape).GetCornerOffset(GetCell(shape), corner, 1f);
        }
    }

    [Serializable]
    public class LayoutCell
    {
        public int id;
        public CellShape shape;

        // Where the cell's center and orientation sit in the layout, in units of the cell size.
        public RoomPlacement placement;

        // Room settings. Only the room's oldest cell's are used: when rooms merge the older room's settings win,
        // and when a room is split each part keeps its own oldest cell's.
        public string roomName = string.Empty;
        public string roomTag = string.Empty;
        public RoomCeiling roomCeiling;
    }

    // An edge shared exactly by two cells, seen from cell A as edgeA and from cell B as edgeB.
    [Serializable]
    public class LayoutLink
    {
        public int cellA;
        public int edgeA;
        public int cellB;
        public int edgeB;
        public EdgeState state;

        public bool Involves(int cellID, int edge)
        {
            return (cellA == cellID && edgeA == edge) || (cellB == cellID && edgeB == edge);
        }

        public int GetOther(int cellID)
        {
            return cellID == cellA ? cellB : cellA;
        }
    }

    // Result of checking where a new cell would go when attached to an outer edge.
    public class AttachPlan
    {
        public int TargetCell;
        public int TargetEdge;
        public CellShape Shape;
        public RoomPlacement Placement;
        public Vector3[] Corners;
        public bool Fits;
        public string FailureReason;

        // Edges of the new cell that exactly match free edges of placed cells (the target edge among them).
        public readonly List<(int cellID, int edge, int newEdge)> Contacts = new List<(int, int, int)>();
    }

    // Rooms of a layout: cells joined by open edges form one room.
    public class LayoutRooms
    {
        // Cell IDs per room, ordered by their lowest cell ID, so room 0 holds the oldest cell. Each list is sorted,
        // so its first cell is the room's oldest cell, which holds the room's settings.
        public readonly List<List<int>> Rooms = new List<List<int>>();
        public readonly Dictionary<int, int> RoomOfCell = new Dictionary<int, int>();

        // Settings cell (oldest cell) of each room.
        public readonly List<LayoutCell> Settings = new List<LayoutCell>();

        // Doors on the shortest path from the start room, or -1 when a room can't be reached.
        public readonly List<int> DistanceFromStart = new List<int>();

        // Rooms that can't be reached from the start room through doors.
        public readonly List<int> Unreachable = new List<int>();

        // The room of the chosen start cell, or room 0.
        public int StartRoom { get; internal set; } = -1;

        // The room of the chosen end cell, or the reachable room farthest from the start by door count (ties go to
        // the lowest room); -1 when no other room can be reached.
        public int EndRoom { get; internal set; } = -1;

        public bool HasCeiling(int room, bool levelCeilings)
        {
            switch (Settings[room].roomCeiling)
            {
                case RoomCeiling.On: return true;
                case RoomCeiling.Off: return false;
                default: return levelCeilings;
            }
        }
    }

    // A hand-built level: cells of any shape placed edge to edge, and the state of every shared edge. All positions
    // are in units of the cell size, so changing the cell size scales the level without breaking it.
    [Serializable]
    public class LevelLayout
    {
        public const float Tolerance = 1e-3f;

        [SerializeField] private List<LayoutCell> cells = new List<LayoutCell>();
        [SerializeField] private List<LayoutLink> links = new List<LayoutLink>();
        [SerializeField] private int nextCellID;

        // Cells whose rooms are the start and end rooms when chosen by hand; -1 picks them automatically.
        [SerializeField] private int startCell = -1;
        [SerializeField] private int endCell = -1;

        public IReadOnlyList<LayoutCell> Cells => cells;
        public IReadOnlyList<LayoutLink> Links => links;
        public bool IsEmpty => cells.Count == 0;

        // The cell whose room is the start room, or -1 for automatic (the room of the oldest cell).
        public int StartCell
        {
            get => GetCell(startCell) != null ? startCell : -1;
            set => startCell = value;
        }

        // The cell whose room is the end room, or -1 for automatic (the room farthest from the start).
        public int EndCell
        {
            get => GetCell(endCell) != null ? endCell : -1;
            set => endCell = value;
        }

        public LayoutCell GetCell(int id)
        {
            return cells.FirstOrDefault(cell => cell.id == id);
        }

        public Vector3 GetCenter(LayoutCell cell)
        {
            return cell.placement.TransformPoint(Vector3.zero);
        }

        public Vector3[] GetCorners(LayoutCell cell)
        {
            return GetCorners(cell.shape, cell.placement);
        }

        public static Vector3[] GetCorners(CellShape shape, RoomPlacement placement)
        {
            int count = CellShapes.GetEdgeCount(shape);
            Vector3[] corners = new Vector3[count];
            for (int i = 0; i < count; i++)
                corners[i] = placement.TransformPoint(CellShapes.GetCorner(shape, i));
            return corners;
        }

        // Edge endpoints in corner order (corner edge - 1, then corner edge).
        public (Vector3, Vector3) GetEdge(LayoutCell cell, int edge)
        {
            return GetEdge(cell.shape, cell.placement, edge);
        }

        private static (Vector3, Vector3) GetEdge(CellShape shape, RoomPlacement placement, int edge)
        {
            int count = CellShapes.GetEdgeCount(shape);
            return (placement.TransformPoint(CellShapes.GetCorner(shape, (edge + count - 1) % count)),
                    placement.TransformPoint(CellShapes.GetCorner(shape, edge)));
        }

        // Placement of the cell shape's own grid, in world units, that puts the grid's cell (0, 0) onto this cell.
        // Geometry and level data build every cell in its own grid and move it into place with this.
        public static RoomPlacement GetGridPlacement(LayoutCell cell, float cellSize)
        {
            Vector3 gridCenter = CellShapes.GetTopology(cell.shape).GetCellCenter(CellShapes.GetCell(cell.shape), cellSize);
            double cos = Math.Cos(cell.placement.angle);
            double sin = Math.Sin(cell.placement.angle);
            return new RoomPlacement(
                cell.placement.x * cellSize - (cos * gridCenter.x - sin * gridCenter.z),
                cell.placement.z * cellSize - (sin * gridCenter.x + cos * gridCenter.z),
                cell.placement.angle);
        }

        // Largest thick-wall corner inset of the shapes in the layout, for validating door widths.
        public float GetCornerInsetPerThickness()
        {
            float inset = 0f;
            foreach (CellShape shape in cells.Select(cell => cell.shape).Distinct())
                inset = Math.Max(inset, CellShapes.GetTopology(shape).ThickWallCornerInsetPerThickness);
            return inset;
        }

        public LayoutLink GetLink(int cellID, int edge)
        {
            return links.FirstOrDefault(link => link.Involves(cellID, edge));
        }

        public bool IsOuterEdge(int cellID, int edge)
        {
            return GetLink(cellID, edge) == null;
        }

        public IEnumerable<LayoutLink> GetLinks(int cellID)
        {
            return links.Where(link => link.cellA == cellID || link.cellB == cellID);
        }

        public LayoutCell AddFirstCell(CellShape shape)
        {
            if (!IsEmpty)
                throw new InvalidOperationException("The layout already has cells; attach new cells to their edges");

            return AddCell(shape, RoomPlacement.Identity);
        }

        // Where a new cell of the given shape would go on an outer edge, and whether it fits there: it must not
        // overlap any cell, and it may touch other cells only along exactly matching edges.
        public AttachPlan PlanAttach(int cellID, int edge, CellShape shape)
        {
            AttachPlan plan = new AttachPlan { TargetCell = cellID, TargetEdge = edge, Shape = shape };
            LayoutCell target = GetCell(cellID);

            if (target == null || edge < 0 || edge >= CellShapes.GetEdgeCount(target.shape))
            {
                plan.FailureReason = "There is no such edge";
                return plan;
            }

            // The new cell's edge 0 goes onto the target edge, running the other way. Every shape is regular, so
            // the choice of edge doesn't change where the cell ends up.
            (Vector3 targetStart, Vector3 targetEnd) = GetEdge(target, edge);
            (Vector3 localStart, Vector3 localEnd) = GetEdge(shape, RoomPlacement.Identity, 0);
            plan.Placement = RoomPlacement.Align(localStart, localEnd, targetEnd, targetStart);
            plan.Corners = GetCorners(shape, plan.Placement);

            if (!IsOuterEdge(cellID, edge))
            {
                plan.FailureReason = "That edge is already shared with another cell";
                return plan;
            }

            float radius = GetCircumradius(shape);
            Vector3 center = plan.Placement.TransformPoint(Vector3.zero);

            foreach (LayoutCell other in cells)
            {
                if (Vector3.Distance(center, GetCenter(other)) > radius + GetCircumradius(other.shape) + Tolerance)
                    continue;

                if (PlanarGeometry.ConvexPolygonsOverlap(plan.Corners, GetCorners(other), Tolerance))
                {
                    plan.FailureReason = "It would overlap another cell";
                    return plan;
                }

                int newEdgeCount = plan.Corners.Length;
                int otherEdgeCount = CellShapes.GetEdgeCount(other.shape);
                for (int newEdge = 0; newEdge < newEdgeCount; newEdge++)
                {
                    (Vector3 start, Vector3 end) = GetEdge(shape, plan.Placement, newEdge);

                    for (int otherEdge = 0; otherEdge < otherEdgeCount; otherEdge++)
                    {
                        (Vector3 otherStart, Vector3 otherEnd) = GetEdge(other, otherEdge);
                        switch (PlanarGeometry.GetContact(start, end, otherStart, otherEnd, Tolerance))
                        {
                            case PlanarGeometry.SegmentContact.ExactOpposite:
                                if (!IsOuterEdge(other.id, otherEdge))
                                {
                                    plan.FailureReason = "It would touch an edge that is already shared";
                                    return plan;
                                }
                                plan.Contacts.Add((other.id, otherEdge, newEdge));
                                break;
                            case PlanarGeometry.SegmentContact.PartialOverlap:
                                plan.FailureReason = "Its edges would only partly line up with another cell";
                                return plan;
                        }
                    }
                }
            }

            if (!plan.Contacts.Any(contact => contact.cellID == cellID && contact.edge == edge))
            {
                plan.FailureReason = "It doesn't line up with the edge";
                return plan;
            }

            plan.Fits = true;
            return plan;
        }

        // Adds the cell a fitting plan describes. The target edge becomes a door and every other matching edge a
        // wall. Returns null when the plan doesn't fit.
        public LayoutCell Attach(AttachPlan plan)
        {
            if (plan == null || !plan.Fits)
                return null;

            LayoutCell cell = AddCell(plan.Shape, plan.Placement);
            foreach ((int cellID, int edge, int newEdge) in plan.Contacts)
            {
                bool isTarget = cellID == plan.TargetCell && edge == plan.TargetEdge;
                links.Add(new LayoutLink
                {
                    cellA = cellID, edgeA = edge,
                    cellB = cell.id, edgeB = newEdge,
                    state = isTarget ? EdgeState.Door : EdgeState.Wall
                });
            }

            return cell;
        }

        public LayoutCell Attach(int cellID, int edge, CellShape shape)
        {
            return Attach(PlanAttach(cellID, edge, shape));
        }

        // A cell can be removed unless the rest of the layout would fall apart into pieces that no longer touch.
        public bool CanRemove(int cellID, out string reason)
        {
            reason = null;
            if (GetCell(cellID) == null)
            {
                reason = "There is no such cell";
                return false;
            }

            List<int> remaining = cells.Where(cell => cell.id != cellID).Select(cell => cell.id).ToList();
            if (remaining.Count == 0)
                return true;

            HashSet<int> reached = new HashSet<int> { remaining[0] };
            Queue<int> queue = new Queue<int>(reached);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (LayoutLink link in GetLinks(current))
                {
                    int other = link.GetOther(current);
                    if (other != cellID && reached.Add(other))
                        queue.Enqueue(other);
                }
            }

            if (reached.Count < remaining.Count)
            {
                reason = "Removing it would split the level into separate pieces";
                return false;
            }

            return true;
        }

        public bool Remove(int cellID)
        {
            if (!CanRemove(cellID, out _))
                return false;

            cells.RemoveAll(cell => cell.id == cellID);
            links.RemoveAll(link => link.cellA == cellID || link.cellB == cellID);
            if (startCell == cellID)
                startCell = -1;
            if (endCell == cellID)
                endCell = -1;
            return true;
        }

        public void Clear()
        {
            cells.Clear();
            links.Clear();
            nextCellID = 0;
            startCell = -1;
            endCell = -1;
        }

        public static EdgeState GetNextState(EdgeState state)
        {
            switch (state)
            {
                case EdgeState.Door: return EdgeState.Wall;
                case EdgeState.Wall: return EdgeState.Open;
                default: return EdgeState.Door;
            }
        }

        public LayoutRooms GetRooms()
        {
            LayoutRooms result = new LayoutRooms();
            Dictionary<int, int> parent = cells.ToDictionary(cell => cell.id, cell => cell.id);

            int Find(int id)
            {
                while (parent[id] != id)
                    id = parent[id] = parent[parent[id]];
                return id;
            }

            foreach (LayoutLink link in links.Where(link => link.state == EdgeState.Open))
            {
                int a = Find(link.cellA);
                int b = Find(link.cellB);
                if (a != b)
                    parent[Math.Max(a, b)] = Math.Min(a, b);
            }

            foreach (IGrouping<int, int> group in cells.Select(cell => cell.id).GroupBy(Find).OrderBy(g => g.Min()))
            {
                List<int> roomCells = group.OrderBy(id => id).ToList();
                foreach (int id in roomCells)
                    result.RoomOfCell[id] = result.Rooms.Count;
                result.Rooms.Add(roomCells);
                result.Settings.Add(GetCell(roomCells[0]));
                result.DistanceFromStart.Add(-1);
            }

            if (result.Rooms.Count == 0)
                return result;

            result.StartRoom = StartCell >= 0 ? result.RoomOfCell[StartCell] : 0;

            // Breadth-first over doors from the start room.
            result.DistanceFromStart[result.StartRoom] = 0;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(result.StartRoom);
            while (queue.Count > 0)
            {
                int room = queue.Dequeue();
                foreach (int id in result.Rooms[room])
                {
                    foreach (LayoutLink link in GetLinks(id).Where(link => link.state == EdgeState.Door))
                    {
                        int other = result.RoomOfCell[link.GetOther(id)];
                        if (result.DistanceFromStart[other] < 0)
                        {
                            result.DistanceFromStart[other] = result.DistanceFromStart[room] + 1;
                            queue.Enqueue(other);
                        }
                    }
                }
            }

            for (int room = 0; room < result.Rooms.Count; room++)
            {
                if (result.DistanceFromStart[room] < 0)
                    result.Unreachable.Add(room);
            }

            int chosenEnd = EndCell >= 0 ? result.RoomOfCell[EndCell] : -1;
            if (chosenEnd >= 0 && chosenEnd != result.StartRoom)
            {
                result.EndRoom = chosenEnd;
            }
            else
            {
                for (int room = 0; room < result.Rooms.Count; room++)
                {
                    if (room != result.StartRoom && result.DistanceFromStart[room] > 0 &&
                        (result.EndRoom < 0 || result.DistanceFromStart[room] > result.DistanceFromStart[result.EndRoom]))
                        result.EndRoom = room;
                }
            }

            return result;
        }

        // The cell containing a layout point, or -1.
        public int FindCellAt(Vector3 point)
        {
            foreach (LayoutCell cell in cells)
            {
                if (Contains(GetCorners(cell), point))
                    return cell.id;
            }
            return -1;
        }

        // The edge closest to a layout point among those the filter accepts, within maxDistance.
        public bool FindNearestEdge(Vector3 point, float maxDistance, Func<int, int, bool> filter, out int cellID, out int edge)
        {
            cellID = -1;
            edge = -1;
            float best = maxDistance;

            foreach (LayoutCell cell in cells)
            {
                int count = CellShapes.GetEdgeCount(cell.shape);
                for (int i = 0; i < count; i++)
                {
                    if (filter != null && !filter(cell.id, i))
                        continue;

                    (Vector3 start, Vector3 end) = GetEdge(cell, i);
                    float distance = DistanceToSegment(point, start, end);
                    if (distance <= best)
                    {
                        best = distance;
                        cellID = cell.id;
                        edge = i;
                    }
                }
            }

            return cellID >= 0;
        }

        private LayoutCell AddCell(CellShape shape, RoomPlacement placement)
        {
            LayoutCell cell = new LayoutCell { id = nextCellID++, shape = shape, placement = placement };
            cells.Add(cell);
            return cell;
        }

        private static float GetCircumradius(CellShape shape)
        {
            return CellShapes.GetCorner(shape, 0).magnitude;
        }

        // Point in a counter-clockwise convex polygon (XZ plane).
        private static bool Contains(Vector3[] corners, Vector3 point)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 a = corners[i];
                Vector3 b = corners[(i + 1) % corners.Length];
                if ((b.x - a.x) * (point.z - a.z) - (b.z - a.z) * (point.x - a.x) < 0f)
                    return false;
            }
            return true;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            float dx = end.x - start.x;
            float dz = end.z - start.z;
            float t = ((point.x - start.x) * dx + (point.z - start.z) * dz) / (dx * dx + dz * dz);
            t = Math.Max(0f, Math.Min(1f, t));
            float offsetX = point.x - (start.x + dx * t);
            float offsetZ = point.z - (start.z + dz * t);
            return (float)Math.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
        }
    }
}
