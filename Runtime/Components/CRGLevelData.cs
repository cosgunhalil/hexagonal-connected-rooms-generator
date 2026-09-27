using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using CRG.Building;
using CRG.Core;
using CRG.Generation;

namespace CRG.Runtime
{
    // Room and door layout of a generated level, stored on the level GameObject so it survives
    // scene saves. Positions are in the level's local space (the mesh is built around its origin).
    public class CRGLevelData : MonoBehaviour
    {
        public enum RoomRole
        {
            Normal,
            Start,
            End
        }

        [Serializable]
        public class RoomData
        {
            public int RoomID;
            public List<CellCoord> Cells = new List<CellCoord>();
            public List<int> ConnectedRoomIDs = new List<int>();

            [Tooltip("Number of doors on the shortest path from the start room")]
            public int DistanceFromStart;
            public RoomRole Role;
            public Transform SpawnPoint;
            public bool HasCeiling;

            [Tooltip("Mixed-grid levels only: the room's own grid type")]
            public GridType GridType;

            [Tooltip("Mixed-grid levels only: where the room's own grid sits in the level")]
            public RoomPlacement Placement;

            [Tooltip("Hand-built levels only: the grid type and placement of each cell (Cells hold the layout cell IDs as x)")]
            public List<PlacedCellData> PlacedCells = new List<PlacedCellData>();

            public bool IsDeadEnd => ConnectedRoomIDs.Count == 1;
        }

        // A cell of a hand-built level: cell (0, 0) of its own grid, moved into the level by Placement.
        [Serializable]
        public class PlacedCellData
        {
            public int CellID;
            public GridType GridType;
            public RoomPlacement Placement;
        }

        [Serializable]
        public class DoorData
        {
            public int RoomA;
            public int RoomB;
            public CellCoord CellA;
            public int EdgeA;
            public Transform DoorObject;
        }

        [Header("Scene Overlays")]
        public bool showRoomOutlines = true;
        public bool showRoomLabels = true;
        public bool showDoors = true;
        public bool showConnections = true;
        public bool showRoomRoles = true;

        [SerializeField, HideInInspector] private GridType gridType = GridType.Hexagon;
        // Mixed-grid level: every room has its own grid type and placement (RoomData.GridType / Placement).
        [SerializeField, HideInInspector] private bool mixed;
        // Hand-built level: every cell has its own grid type and placement (RoomData.PlacedCells).
        [SerializeField, HideInInspector] private bool handBuilt;
        [SerializeField, HideInInspector, FormerlySerializedAs("hexSize")] private float cellSize;
        [SerializeField, HideInInspector] private float wallHeight;
        [SerializeField, HideInInspector] private float doorHeight;
        [SerializeField, HideInInspector] private float doorWidthRatio;
        [SerializeField, HideInInspector] private List<RoomData> rooms = new List<RoomData>();
        [SerializeField, HideInInspector] private List<DoorData> doors = new List<DoorData>();

        public float CellSize => cellSize;
        public GridType GridType => gridType;
        // The level's grid for single-grid levels. Mixed levels have one per room: use GetTopology(room).
        public IGridTopology Topology => GridTopology.Get(gridType);
        public bool IsMixed => mixed;
        public bool IsHandBuilt => handBuilt;
        public float WallHeight => wallHeight;
        public float DoorHeight => doorHeight;
        public IReadOnlyList<RoomData> Rooms => rooms;
        public IReadOnlyList<DoorData> Doors => doors;

        public RoomData StartRoom => rooms.Find(room => room.Role == RoomRole.Start);
        public RoomData EndRoom => rooms.Find(room => room.Role == RoomRole.End);

        public void Initialize(CellGrid grid, float wallHeight, float doorHeight, float doorWidthRatio, bool ceilingsEnabled = false)
        {
            mixed = false;
            handBuilt = false;
            gridType = grid.Topology.Type;
            cellSize = grid.CellSize;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
            rooms.Clear();
            doors.Clear();

            List<Room> sortedRooms = new List<Room>(grid.GetAllRooms());
            sortedRooms.Sort((a, b) => a.RoomID.CompareTo(b.RoomID));

            foreach (Room room in sortedRooms)
            {
                RoomData roomData = new RoomData { RoomID = room.RoomID, HasCeiling = ceilingsEnabled && room.HasCeiling };
                roomData.Cells.AddRange(room.Cells);
                roomData.ConnectedRoomIDs.AddRange(room.GetConnectedRoomIDs());
                roomData.ConnectedRoomIDs.Sort();
                rooms.Add(roomData);

                foreach (KeyValuePair<int, SharedWallData> connection in room.Connections)
                {
                    // Each door is stored once, from the room with the lower ID.
                    if (connection.Key < room.RoomID)
                        continue;

                    foreach (EdgeConnection edge in connection.Value.SharedEdges)
                    {
                        doors.Add(new DoorData
                        {
                            RoomA = room.RoomID,
                            RoomB = connection.Key,
                            CellA = edge.CellA,
                            EdgeA = edge.EdgeIndexA
                        });
                    }
                }
            }

            AssignRoles();
        }

        public void Initialize(MixedLevel level, float wallHeight, float doorHeight, float doorWidthRatio, bool ceilingsEnabled = false)
        {
            mixed = true;
            handBuilt = false;
            gridType = level.Rooms.Count > 0 ? level.Rooms[0].GridType : GridType.Hexagon;
            cellSize = level.CellSize;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
            rooms.Clear();
            doors.Clear();

            foreach (PlacedRoom room in level.Rooms)
            {
                RoomData roomData = new RoomData
                {
                    RoomID = room.RoomID,
                    HasCeiling = ceilingsEnabled && room.Room.HasCeiling,
                    GridType = room.GridType,
                    Placement = room.Placement
                };
                roomData.Cells.AddRange(room.Room.Cells);
                roomData.ConnectedRoomIDs.AddRange(room.ConnectedRoomIDs);
                roomData.ConnectedRoomIDs.Sort();
                rooms.Add(roomData);
            }

            // Each door is stored once, seen from the room with the lower ID.
            foreach (RoomLink link in level.Doors)
            {
                bool aFirst = link.RoomA < link.RoomB;
                doors.Add(new DoorData
                {
                    RoomA = aFirst ? link.RoomA : link.RoomB,
                    RoomB = aFirst ? link.RoomB : link.RoomA,
                    CellA = aFirst ? link.CellA : link.CellB,
                    EdgeA = aFirst ? link.EdgeA : link.EdgeB
                });
            }

            AssignRoles();
        }

        // Rooms are the layout's rooms (cells joined by open edges); every door link becomes a door, also one between
        // two cells of the same room. A cell is stored as CellCoord(cell ID, 0) with its grid in RoomData.PlacedCells.
        public void Initialize(LevelLayout layout, float cellSize, float wallHeight, float doorHeight, float doorWidthRatio, bool ceilingsEnabled = false)
        {
            mixed = false;
            handBuilt = true;
            gridType = layout.Cells.Count > 0 ? CellShapes.GetGridType(layout.Cells[0].shape) : GridType.Hexagon;
            this.cellSize = cellSize;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
            rooms.Clear();
            doors.Clear();

            LayoutRooms layoutRooms = layout.GetRooms();
            for (int room = 0; room < layoutRooms.Rooms.Count; room++)
            {
                RoomData roomData = new RoomData { RoomID = room, HasCeiling = ceilingsEnabled };
                foreach (int id in layoutRooms.Rooms[room])
                {
                    LayoutCell cell = layout.GetCell(id);
                    roomData.Cells.Add(new CellCoord(id, 0));
                    roomData.PlacedCells.Add(new PlacedCellData
                    {
                        CellID = id,
                        GridType = CellShapes.GetGridType(cell.shape),
                        Placement = LevelLayout.GetGridPlacement(cell, cellSize)
                    });
                }
                rooms.Add(roomData);
            }

            foreach (LayoutLink link in layout.Links.Where(link => link.state == EdgeState.Door))
            {
                int roomA = layoutRooms.RoomOfCell[link.cellA];
                int roomB = layoutRooms.RoomOfCell[link.cellB];
                bool aFirst = roomA <= roomB;

                // Each door is stored once, seen from the room with the lower ID.
                doors.Add(new DoorData
                {
                    RoomA = aFirst ? roomA : roomB,
                    RoomB = aFirst ? roomB : roomA,
                    CellA = new CellCoord(aFirst ? link.cellA : link.cellB, 0),
                    EdgeA = aFirst ? link.edgeA : link.edgeB
                });

                if (roomA != roomB)
                {
                    if (!rooms[roomA].ConnectedRoomIDs.Contains(roomB))
                        rooms[roomA].ConnectedRoomIDs.Add(roomB);
                    if (!rooms[roomB].ConnectedRoomIDs.Contains(roomA))
                        rooms[roomB].ConnectedRoomIDs.Add(roomA);
                }
            }

            foreach (RoomData room in rooms)
                room.ConnectedRoomIDs.Sort();

            AssignRoles();
        }

        public Transform GetSpawnPoint(int roomID)
        {
            return GetRoom(roomID)?.SpawnPoint;
        }

        // Breadth-first search over doors from the first room placed (lowest ID).
        private void AssignRoles()
        {
            if (rooms.Count == 0)
                return;

            foreach (RoomData room in rooms)
            {
                room.DistanceFromStart = -1;
                room.Role = RoomRole.Normal;
            }

            RoomData start = rooms[0];
            start.DistanceFromStart = 0;
            start.Role = RoomRole.Start;

            Queue<RoomData> queue = new Queue<RoomData>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                RoomData current = queue.Dequeue();
                foreach (int neighborID in current.ConnectedRoomIDs)
                {
                    RoomData neighbor = GetRoom(neighborID);
                    if (neighbor != null && neighbor.DistanceFromStart < 0)
                    {
                        neighbor.DistanceFromStart = current.DistanceFromStart + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Farthest room becomes the end; ties go to the lowest ID for reproducibility.
            RoomData end = start;
            foreach (RoomData room in rooms)
            {
                if (room.DistanceFromStart > end.DistanceFromStart)
                    end = room;
            }

            if (end != start)
                end.Role = RoomRole.End;
        }

        public RoomData GetRoom(int roomID)
        {
            return rooms.Find(room => room.RoomID == roomID);
        }

        // Single-grid levels only; use the RoomData overload for levels that may be mixed.
        public Vector3 GetCellLocalPosition(CellCoord cell)
        {
            return Topology.GetCellCenter(cell, cellSize);
        }

        // The room's grid. The cells of a hand-built room can have different shapes: use GetTopology(room, cell).
        public IGridTopology GetTopology(RoomData room)
        {
            return Resolve(room, room.Cells.Count > 0 ? room.Cells[0] : default).topology;
        }

        public IGridTopology GetTopology(RoomData room, CellCoord cell)
        {
            return Resolve(room, cell).topology;
        }

        public Vector3 GetCellLocalPosition(RoomData room, CellCoord cell)
        {
            CellFrame frame = Resolve(room, cell);
            return frame.ToLevelPoint(frame.topology.GetCellCenter(frame.cell, cellSize));
        }

        // Corner of a cell in the level's local space; scale shrinks it towards the cell center (1 = true corner).
        public Vector3 GetCellCornerLocal(RoomData room, CellCoord cell, int corner, float scale = 1f)
        {
            CellFrame frame = Resolve(room, cell);
            Vector3 local = frame.topology.GetCellCenter(frame.cell, cellSize) + frame.topology.GetCornerOffset(frame.cell, corner, cellSize) * scale;
            return frame.ToLevelPoint(local);
        }

        public int GetCellEdgeCount(RoomData room, CellCoord cell)
        {
            CellFrame frame = Resolve(room, cell);
            return frame.topology.GetEdgeCount(frame.cell);
        }

        // Distance from the cell center to its nearest edge.
        public float GetCellInnerRadius(RoomData room, CellCoord cell)
        {
            CellFrame frame = Resolve(room, cell);
            return frame.topology.GetInnerRadius(frame.cell, cellSize);
        }

        // The room cell closest to the room's centroid, so the anchor always lies inside the room.
        public CellCoord GetRoomAnchorCell(RoomData room)
        {
            if (room.Cells.Count == 0)
                return default;

            Vector3 centroid = Vector3.zero;
            foreach (CellCoord cell in room.Cells)
                centroid += GetCellLocalPosition(room, cell);
            centroid /= room.Cells.Count;

            CellCoord best = room.Cells[0];
            float bestDistance = (GetCellLocalPosition(room, best) - centroid).sqrMagnitude;
            foreach (CellCoord cell in room.Cells)
            {
                float distance = (GetCellLocalPosition(room, cell) - centroid).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public Vector3 GetRoomAnchorLocalPosition(RoomData room)
        {
            return room.Cells.Count == 0 ? Vector3.zero : GetCellLocalPosition(room, GetRoomAnchorCell(room));
        }

        // Center of the door opening at floor level.
        public Vector3 GetDoorCenterLocal(DoorData door)
        {
            CellFrame frame = Resolve(GetRoom(door.RoomA), door.CellA);
            Vector3 local = frame.topology.GetCellCenter(frame.cell, cellSize) + frame.topology.GetEdgeCenterOffset(frame.cell, door.EdgeA, cellSize);
            return frame.ToLevelPoint(local);
        }

        // Horizontal direction through the door, pointing from RoomA into RoomB.
        public Vector3 GetDoorForwardLocal(DoorData door)
        {
            CellFrame frame = Resolve(GetRoom(door.RoomA), door.CellA);
            return frame.ToLevelDirection(frame.topology.GetEdgeNormal(frame.cell, door.EdgeA));
        }

        // Endpoints of the door opening at floor level.
        public (Vector3, Vector3) GetDoorOpeningLocal(DoorData door)
        {
            CellFrame frame = Resolve(GetRoom(door.RoomA), door.CellA);
            Vector3 center = GetDoorCenterLocal(door);
            (Vector3 start, Vector3 end) = frame.topology.GetEdgeOffsets(frame.cell, door.EdgeA, cellSize);
            Vector3 halfWidth = frame.ToLevelDirection((end - start).normalized) * (cellSize * doorWidthRatio / 2f);

            return (center - halfWidth, center + halfWidth);
        }

        // Where a stored cell lives: its grid, its coordinate in that grid, and how that grid is moved into the level.
        private readonly struct CellFrame
        {
            public readonly IGridTopology topology;
            public readonly CellCoord cell;
            private readonly RoomPlacement placement;
            private readonly bool placed;

            public CellFrame(IGridTopology topology, CellCoord cell, RoomPlacement placement, bool placed)
            {
                this.topology = topology;
                this.cell = cell;
                this.placement = placement;
                this.placed = placed;
            }

            public Vector3 ToLevelPoint(Vector3 local) => placed ? placement.TransformPoint(local) : local;

            public Vector3 ToLevelDirection(Vector3 local) => placed ? placement.TransformDirection(local) : local;
        }

        private CellFrame Resolve(RoomData room, CellCoord cell)
        {
            if (handBuilt)
            {
                PlacedCellData placedCell = room.PlacedCells.Find(c => c.CellID == cell.x);
                if (placedCell != null)
                    return new CellFrame(GridTopology.Get(placedCell.GridType), new CellCoord(0, 0), placedCell.Placement, true);
            }

            if (mixed)
                return new CellFrame(GridTopology.Get(room.GridType), cell, room.Placement, true);

            return new CellFrame(Topology, cell, RoomPlacement.Identity, false);
        }
    }
}
