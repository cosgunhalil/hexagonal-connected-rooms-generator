using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HRCG.Core
{
    public class HexGrid
    {
        private readonly Dictionary<AxialCoord, HexCell> cells;
        private readonly Dictionary<int, Room> rooms;
        private int nextRoomID;

        public float HexSize { get; private set; }
        public int CellCount => cells.Count;
        public int RoomCount => rooms.Count;

        public HexGrid(float hexSize)
        {
            HexSize = hexSize;
            cells = new Dictionary<AxialCoord, HexCell>();
            rooms = new Dictionary<int, Room>();
            nextRoomID = 0;
        }

        public HexCell GetCell(AxialCoord coord)
        {
            return cells.TryGetValue(coord, out var cell) ? cell : null;
        }

        public HexCell GetOrCreateCell(AxialCoord coord)
        {
            if (!cells.ContainsKey(coord))
            {
                cells[coord] = new HexCell(coord);
            }
            return cells[coord];
        }

        public bool HasCell(AxialCoord coord)
        {
            return cells.ContainsKey(coord);
        }

        public void RemoveCell(AxialCoord coord)
        {
            cells.Remove(coord);
        }

        public IEnumerable<HexCell> GetAllCells()
        {
            return cells.Values;
        }

        public IEnumerable<AxialCoord> GetAllCoordinates()
        {
            return cells.Keys;
        }

        public HexCell GetNeighbor(AxialCoord coord, int direction)
        {
            AxialCoord neighborCoord = coord.GetNeighbor(direction);
            return GetCell(neighborCoord);
        }

        public IEnumerable<HexCell> GetNeighbors(AxialCoord coord)
        {
            foreach (var neighborCoord in coord.GetAllNeighbors())
            {
                var cell = GetCell(neighborCoord);
                if (cell != null)
                {
                    yield return cell;
                }
            }
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

        public void AssignCellToRoom(AxialCoord coord, int roomID)
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

        public IEnumerable<HexCell> GetCellsInRoom(int roomID)
        {
            return cells.Values.Where(c => c.RoomID == roomID);
        }

        public IEnumerable<AxialCoord> GetEmptyNeighbors(AxialCoord coord)
        {
            foreach (var neighborCoord in coord.GetAllNeighbors())
            {
                var cell = GetCell(neighborCoord);
                if (cell == null || cell.State == CellState.Empty)
                {
                    yield return neighborCoord;
                }
            }
        }

        public bool IsEdgeShared(AxialCoord coordA, int edgeA, AxialCoord coordB, int edgeB)
        {
            AxialCoord neighborFromA = coordA.GetNeighbor(edgeA);
            AxialCoord neighborFromB = coordB.GetNeighbor(edgeB);

            return neighborFromA == coordB && neighborFromB == coordA;
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
                Vector3 worldPos = coord.ToWorldPosition(HexSize);
                minX = Mathf.Min(minX, worldPos.x);
                maxX = Mathf.Max(maxX, worldPos.x);
                minZ = Mathf.Min(minZ, worldPos.z);
                maxZ = Mathf.Max(maxZ, worldPos.z);
            }

            Vector3 center = new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f);
            Vector3 size = new Vector3(maxX - minX + HexSize * 2f, 0, maxZ - minZ + HexSize * 2f);
            
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
            return $"HexGrid: {cells.Count} cells, {rooms.Count} rooms, HexSize: {HexSize}";
        }
    }
}
