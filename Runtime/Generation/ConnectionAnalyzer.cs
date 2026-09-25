using System;
using System.Collections.Generic;
using HRCG.Core;
using UnityEngine;

namespace HRCG.Generation
{
    // Flags room edges and places doors. Each connected pair of rooms gets exactly one door,
    // on a randomly chosen edge they share.
    public class ConnectionAnalyzer
    {
        private readonly int maxConnectionsPerRoom;

        public ConnectionAnalyzer(int maxConnectionsPerRoom)
        {
            this.maxConnectionsPerRoom = Math.Clamp(maxConnectionsPerRoom, 1, 6);
        }

        public bool HasCapacity(Room room)
        {
            return room.GetConnectionCount() < maxConnectionsPerRoom;
        }

        // Internal edges become NoWall, every other edge becomes Wall. Doors are added separately.
        public void FlagRoomEdges(HexGrid grid, Room room)
        {
            if (room == null)
                throw new ArgumentNullException(nameof(room));

            foreach (AxialCoord cellCoord in room.Cells)
            {
                HexCell cell = grid.GetCell(cellCoord);
                if (cell == null)
                    continue;

                for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
                {
                    HexCell neighborCell = grid.GetCell(cellCoord.GetNeighbor(edgeIndex));
                    bool sameRoom = neighborCell != null && neighborCell.RoomID == room.RoomID;
                    cell.SetEdgeFlag(edgeIndex, sameRoom ? WallFlag.NoWall : WallFlag.Wall);
                }
            }
        }

        // Rooms adjacent to the given cells, keyed by room ID, with the shared edges seen from the given cells.
        public Dictionary<int, List<EdgeConnection>> FindSharedEdges(HexGrid grid, IEnumerable<AxialCoord> cells, int ownRoomID)
        {
            Dictionary<int, List<EdgeConnection>> sharedEdges = new Dictionary<int, List<EdgeConnection>>();

            foreach (AxialCoord cellCoord in cells)
            {
                for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
                {
                    AxialCoord neighborCoord = cellCoord.GetNeighbor(edgeIndex);
                    HexCell neighborCell = grid.GetCell(neighborCoord);

                    if (neighborCell == null || !neighborCell.IsPartOfRoom() || neighborCell.RoomID == ownRoomID)
                        continue;

                    if (!sharedEdges.TryGetValue(neighborCell.RoomID, out List<EdgeConnection> edges))
                    {
                        edges = new List<EdgeConnection>();
                        sharedEdges[neighborCell.RoomID] = edges;
                    }

                    edges.Add(new EdgeConnection(
                        cellCoord, edgeIndex,
                        neighborCoord, HexDirection.GetOppositeDirection(edgeIndex)));
                }
            }

            return sharedEdges;
        }

        // Connects the room to up to maxNewConnections adjacent rooms (bounded by the max-connection
        // limit), visiting neighbors in random order. Returns the number of new connections.
        public int ConnectToNeighbors(HexGrid grid, Room room, System.Random random, int maxNewConnections = int.MaxValue)
        {
            Dictionary<int, List<EdgeConnection>> sharedEdges = FindSharedEdges(grid, room.Cells, room.RoomID);

            List<int> neighborIDs = new List<int>(sharedEdges.Keys);
            neighborIDs.Sort();
            Shuffle(neighborIDs, random);

            int created = 0;

            foreach (int neighborID in neighborIDs)
            {
                if (created >= maxNewConnections || !HasCapacity(room))
                    break;

                Room neighborRoom = grid.GetRoom(neighborID);
                if (neighborRoom == null || room.IsConnectedTo(neighborID) || !HasCapacity(neighborRoom))
                    continue;

                List<EdgeConnection> edges = sharedEdges[neighborID];
                CreateDoor(grid, room, neighborRoom, edges[random.Next(edges.Count)]);
                created++;
            }

            return created;
        }

        // Adds connections until the room has at least minConnections, if adjacent rooms allow it.
        public int EnsureMinimumConnections(HexGrid grid, Room room, int minConnections, System.Random random)
        {
            int missing = minConnections - room.GetConnectionCount();
            return missing > 0 ? ConnectToNeighbors(grid, room, random, missing) : 0;
        }

        public bool ValidateRoomConnections(Room room, int minConnections)
        {
            if (room == null)
                return false;

            int connectionCount = room.GetConnectionCount();

            if (connectionCount < minConnections)
            {
                Debug.LogWarning($"Room {room.RoomID} has only {connectionCount} connections (minimum: {minConnections})");
                return false;
            }

            if (connectionCount > maxConnectionsPerRoom)
            {
                Debug.LogWarning($"Room {room.RoomID} has {connectionCount} connections (maximum: {maxConnectionsPerRoom})");
                return false;
            }

            return true;
        }

        private static void CreateDoor(HexGrid grid, Room roomA, Room roomB, EdgeConnection edge)
        {
            grid.GetCell(edge.CellA).SetEdgeFlag(edge.EdgeIndexA, WallFlag.HasDoor);
            grid.GetCell(edge.CellB).SetEdgeFlag(edge.EdgeIndexB, WallFlag.HasDoor);

            roomA.AddConnection(roomB.RoomID, edge.CellA, edge.EdgeIndexA, edge.CellB, edge.EdgeIndexB);
            roomB.AddConnection(roomA.RoomID, edge.CellB, edge.EdgeIndexB, edge.CellA, edge.EdgeIndexA);
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
