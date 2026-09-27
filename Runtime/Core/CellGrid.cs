using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CRG.Core
{
    // Sparse grid of cells plus the rooms built on it. Shape and connectivity come from the topology.
    public class CellGrid
    {
        private readonly Dictionary<CellCoord, GridCell> cells;
        private readonly Dictionary<int, Room> rooms;
        private int nextRoomID;

        public IGridTopology Topology { get; }
        public float CellSize { get; }
        public int CellCount => cells.Count;
        public int RoomCount => rooms.Count;

        // The seed the grid was generated with, or -1 when it wasn't generated.
        public int Seed { get; set; } = -1;

        public CellGrid(IGridTopology topology, float cellSize)
        {
            Topology = topology;
            CellSize = cellSize;
            cells = new Dictionary<CellCoord, GridCell>();
            rooms = new Dictionary<int, Room>();
            nextRoomID = 0;
        }

        public GridCell GetCell(CellCoord coord)
        {
            return cells.TryGetValue(coord, out var cell) ? cell : null;
        }

        public GridCell GetOrCreateCell(CellCoord coord)
        {
            if (!cells.ContainsKey(coord))
            {
                cells[coord] = new GridCell(coord, Topology.GetEdgeCount(coord));
            }
            return cells[coord];
        }

        public bool HasCell(CellCoord coord)
        {
            return cells.ContainsKey(coord);
        }

        public void RemoveCell(CellCoord coord)
        {
            cells.Remove(coord);
        }

        public IEnumerable<GridCell> GetAllCells()
        {
            return cells.Values;
        }

        public IEnumerable<CellCoord> GetAllCoordinates()
        {
            return cells.Keys;
        }

        public GridCell GetNeighbor(CellCoord coord, int edge)
        {
            return GetCell(Topology.GetNeighbor(coord, edge));
        }

        public IEnumerable<GridCell> GetNeighbors(CellCoord coord)
        {
            foreach (var neighborCoord in Topology.GetNeighbors(coord))
            {
                var cell = GetCell(neighborCoord);
                if (cell != null)
                {
                    yield return cell;
                }
            }
        }

        public Vector3 GetCellCenter(CellCoord coord)
        {
            return Topology.GetCellCenter(coord, CellSize);
        }

        public Room CreateRoom()
        {
            int roomID = nextRoomID++;
            Room room = new Room(roomID);
            rooms[roomID] = room;
            return room;
        }

        public Room GetRoom(int roomID)
        {
            return rooms.TryGetValue(roomID, out var room) ? room : null;
        }

        public bool HasRoom(int roomID)
        {
            return rooms.ContainsKey(roomID);
        }

        public void RemoveRoom(int roomID)
        {
            if (rooms.TryGetValue(roomID, out var room))
            {
                foreach (var coord in room.Cells.ToList())
                {
                    var cell = GetCell(coord);
                    if (cell != null && cell.RoomID == roomID)
                    {
                        cell.Reset();
                    }
                }
                rooms.Remove(roomID);
            }
        }

        public IEnumerable<Room> GetAllRooms()
        {
            return rooms.Values;
        }

        public void AssignCellToRoom(CellCoord coord, int roomID)
        {
            var cell = GetOrCreateCell(coord);
            cell.State = CellState.Room;
            cell.RoomID = roomID;

            var room = GetRoom(roomID);
            if (room != null)
            {
                room.AddCell(coord);
            }
        }

        public IEnumerable<GridCell> GetCellsInRoom(int roomID)
        {
            return cells.Values.Where(c => c.RoomID == roomID);
        }

        public IEnumerable<CellCoord> GetEmptyNeighbors(CellCoord coord)
        {
            foreach (var neighborCoord in Topology.GetNeighbors(coord))
            {
                var cell = GetCell(neighborCoord);
                if (cell == null || cell.State == CellState.Empty)
                {
                    yield return neighborCoord;
                }
            }
        }

        public bool IsEdgeShared(CellCoord coordA, int edgeA, CellCoord coordB, int edgeB)
        {
            return Topology.GetNeighbor(coordA, edgeA) == coordB &&
                   Topology.GetNeighbor(coordB, edgeB) == coordA;
        }

        public Bounds GetBounds()
        {
            if (cells.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            foreach (var coord in cells.Keys)
            {
                Vector3 worldPos = GetCellCenter(coord);
                minX = Mathf.Min(minX, worldPos.x);
                maxX = Mathf.Max(maxX, worldPos.x);
                minZ = Mathf.Min(minZ, worldPos.z);
                maxZ = Mathf.Max(maxZ, worldPos.z);
            }

            Vector3 center = new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f);
            Vector3 size = new Vector3(maxX - minX + CellSize * 2f, 0, maxZ - minZ + CellSize * 2f);

            return new Bounds(center, size);
        }

        public void Clear()
        {
            cells.Clear();
            rooms.Clear();
            nextRoomID = 0;
        }

        public void ClearCells()
        {
            cells.Clear();
        }

        public void ClearRooms()
        {
            foreach (var room in rooms.Values.ToList())
            {
                RemoveRoom(room.RoomID);
            }
            rooms.Clear();
            nextRoomID = 0;
        }

        public override string ToString()
        {
            return $"CellGrid ({Topology.Type}): {cells.Count} cells, {rooms.Count} rooms, CellSize: {CellSize}";
        }
    }
}
