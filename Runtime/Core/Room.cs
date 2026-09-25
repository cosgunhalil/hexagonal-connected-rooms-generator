using System.Collections.Generic;
using System.Linq;

namespace CRG.Core
{
    public class Room
    {
        public int RoomID { get; private set; }
        public List<CellCoord> Cells { get; private set; }
        public Dictionary<int, SharedWallData> Connections { get; private set; }

        // Whether this room is covered when ceilings are enabled (see GenerationParameters.CeilingChance).
        public bool HasCeiling { get; set; } = true;

        public Room(int roomID)
        {
            RoomID = roomID;
            Cells = new List<CellCoord>();
            Connections = new Dictionary<int, SharedWallData>();
        }

        public void AddCell(CellCoord coord)
        {
            if (!Cells.Contains(coord))
            {
                Cells.Add(coord);
            }
        }

        public void RemoveCell(CellCoord coord)
        {
            Cells.Remove(coord);
        }

        public bool ContainsCell(CellCoord coord)
        {
            return Cells.Contains(coord);
        }

        public int GetCellCount()
        {
            return Cells.Count;
        }

        public void AddConnection(int neighborRoomID, CellCoord cellA, int edgeA, CellCoord cellB, int edgeB)
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

        public IEnumerable<CellCoord> GetBorderCells(CellGrid grid)
        {
            HashSet<CellCoord> borderCells = new HashSet<CellCoord>();

            foreach (var coord in Cells)
            {
                foreach (var neighbor in grid.Topology.GetNeighbors(coord))
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

        public IEnumerable<CellCoord> GetExternalNeighbors(CellGrid grid)
        {
            HashSet<CellCoord> externalNeighbors = new HashSet<CellCoord>();

            foreach (var coord in Cells)
            {
                foreach (var neighbor in grid.Topology.GetNeighbors(coord))
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
