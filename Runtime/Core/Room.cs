using System.Collections.Generic;
using System.Linq;

namespace HRCG.Core
{
    public class Room
    {
        public int RoomID { get; private set; }
        public List<AxialCoord> Cells { get; private set; }
        public Dictionary<int, SharedWallData> Connections { get; private set; }

        public Room(int roomID)
        {
            RoomID = roomID;
            Cells = new List<AxialCoord>();
            Connections = new Dictionary<int, SharedWallData>();
        }

        public void AddCell(AxialCoord coord)
        {
            if (!Cells.Contains(coord))
            {
                Cells.Add(coord);
            }
        }

        public void RemoveCell(AxialCoord coord)
        {
            Cells.Remove(coord);
        }

        public bool ContainsCell(AxialCoord coord)
        {
            return Cells.Contains(coord);
        }

        public int GetCellCount()
        {
            return Cells.Count;
        }

        public void AddConnection(int neighborRoomID, AxialCoord cellA, int edgeA, AxialCoord cellB, int edgeB)
        {
            if (!Connections.ContainsKey(neighborRoomID))
            {
                Connections[neighborRoomID] = new SharedWallData(neighborRoomID);
            }

            Connections[neighborRoomID].AddSharedEdge(cellA, edgeA, cellB, edgeB);
        }

        public void RemoveConnection(int neighborRoomID)
        {
            Connections.Remove(neighborRoomID);
        }

        public bool IsConnectedTo(int neighborRoomID)
        {
            return Connections.ContainsKey(neighborRoomID);
        }

        public int GetConnectionCount()
        {
            return Connections.Count;
        }

        public SharedWallData GetConnectionData(int neighborRoomID)
        {
            return Connections.TryGetValue(neighborRoomID, out var data) ? data : null;
        }

        public IEnumerable<int> GetConnectedRoomIDs()
        {
            return Connections.Keys;
        }

        public bool CanAddConnection(int minConnections, int maxConnections)
        {
            return GetConnectionCount() < maxConnections;
        }

        public bool MeetsMinimumConnections(int minConnections)
        {
            return GetConnectionCount() >= minConnections;
        }

        public IEnumerable<AxialCoord> GetBorderCells(HexGrid grid)
        {
            HashSet<AxialCoord> borderCells = new HashSet<AxialCoord>();

            foreach (var coord in Cells)
            {
                foreach (var neighbor in coord.GetAllNeighbors())
                {
                    if (!Cells.Contains(neighbor))
                    {
                        borderCells.Add(coord);
                        break;
                    }
                }
            }

            return borderCells;
        }

        public IEnumerable<AxialCoord> GetExternalNeighbors(HexGrid grid)
        {
            HashSet<AxialCoord> externalNeighbors = new HashSet<AxialCoord>();

            foreach (var coord in Cells)
            {
                foreach (var neighbor in coord.GetAllNeighbors())
                {
                    if (!Cells.Contains(neighbor))
                    {
                        externalNeighbors.Add(neighbor);
                    }
                }
            }

            return externalNeighbors;
        }

        public void Clear()
        {
            Cells.Clear();
            Connections.Clear();
        }

        public override string ToString()
        {
            return $"Room {RoomID}: {Cells.Count} cells, {Connections.Count} connections";
        }
    }
}
