using System;
using System.Collections.Generic;
using HRCG.Core;
using UnityEngine;

namespace HRCG.Generation
{
    public class ConnectionAnalyzer
    {
        private readonly int maxConnectionsPerRoom;

        public ConnectionAnalyzer(int maxConnectionsPerRoom)
        {
            this.maxConnectionsPerRoom = Math.Clamp(maxConnectionsPerRoom, 1, 6);
        }

        public void AnalyzeAndFlagEdges(HexGrid grid, Room room)
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
                    AnalyzeAndFlagEdge(grid, room, cell, edgeIndex);
                }
            }
        }

        private void AnalyzeAndFlagEdge(HexGrid grid, Room room, HexCell cell, int edgeIndex)
        {
            AxialCoord neighborCoord = cell.Coordinate.GetNeighbor(edgeIndex);
            HexCell neighborCell = grid.GetCell(neighborCoord);

            cell.SetEdgeFlag(edgeIndex, WallFlag.None);

            if (neighborCell == null || neighborCell.State == CellState.Empty)
            {
                cell.SetEdgeFlag(edgeIndex, WallFlag.Wall);
            }
            else if (neighborCell.RoomID == room.RoomID)
            {
                cell.SetEdgeFlag(edgeIndex, WallFlag.NoWall);
            }
            else if (neighborCell.RoomID >= 0 && neighborCell.RoomID != room.RoomID)
            {
                HandleRoomConnection(grid, room, cell, edgeIndex, neighborCell);
            }
            else
            {
                cell.SetEdgeFlag(edgeIndex, WallFlag.Wall);
            }
        }

        private void HandleRoomConnection(
            HexGrid grid,
            Room room,
            HexCell cell,
            int edgeIndex,
            HexCell neighborCell)
        {
            Room neighborRoom = grid.GetRoom(neighborCell.RoomID);
            if (neighborRoom == null)
            {
                cell.SetEdgeFlag(edgeIndex, WallFlag.Wall);
                return;
            }

            bool roomCanConnect = room.GetConnectionCount() < maxConnectionsPerRoom;
            bool neighborCanConnect = neighborRoom.GetConnectionCount() < maxConnectionsPerRoom;
            bool alreadyConnected = room.IsConnectedTo(neighborRoom.RoomID);

            if (alreadyConnected || (roomCanConnect && neighborCanConnect))
            {
                CreateConnection(grid, room, neighborRoom, cell, edgeIndex, neighborCell);
            }
            else
            {
                cell.SetEdgeFlag(edgeIndex, WallFlag.Wall);
            }
        }

        private void CreateConnection(
            HexGrid grid,
            Room roomA,
            Room roomB,
            HexCell cellA,
            int edgeA,
            HexCell cellB)
        {
            int edgeB = HexDirection.GetOppositeDirection(edgeA);

            cellA.SetEdgeFlag(edgeA, WallFlag.HasDoor);
            cellB.SetEdgeFlag(edgeB, WallFlag.HasDoor);

            roomA.AddConnection(roomB.RoomID, cellA.Coordinate, edgeA, cellB.Coordinate, edgeB);
            roomB.AddConnection(roomA.RoomID, cellB.Coordinate, edgeB, cellA.Coordinate, edgeA);
        }

        public bool ValidateRoomConnections(HexGrid grid, Room room, int minConnections)
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

        public int CountPotentialConnections(HexGrid grid, Room room)
        {
            if (room == null)
                return 0;

            HashSet<int> neighborRoomIDs = new HashSet<int>();

            foreach (AxialCoord cellCoord in room.Cells)
            {
                foreach (AxialCoord neighborCoord in cellCoord.GetAllNeighbors())
                {
                    HexCell neighborCell = grid.GetCell(neighborCoord);
                    if (neighborCell != null && 
                        neighborCell.State == CellState.Room && 
                        neighborCell.RoomID != room.RoomID &&
                        neighborCell.RoomID >= 0)
                    {
                        neighborRoomIDs.Add(neighborCell.RoomID);
                    }
                }
            }

            return neighborRoomIDs.Count;
        }
    }
}