using System;
using System.Collections.Generic;
using System.Linq;
using HRCG.Core;
using UnityEngine;

namespace HRCG.Generation
{
    public class HRCGGenerator
    {
        private GenerationParameters parameters;
        private RoomShapeGenerator shapeGenerator;
        private ConnectionAnalyzer connectionAnalyzer;
        private System.Random random;

        public HexGrid Generate(GenerationParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            if (!parameters.Validate(out string errorMessage))
                throw new ArgumentException($"Invalid parameters: {errorMessage}");

            this.parameters = parameters;
            this.random = parameters.RandomSeed >= 0 
                ? new System.Random(parameters.RandomSeed)
                : new System.Random();

            this.shapeGenerator = new RoomShapeGenerator();
            this.connectionAnalyzer = new ConnectionAnalyzer(parameters.MaxConnectionsPerRoom);

            HexGrid grid = new HexGrid(parameters.HexSize);

            Debug.Log($"Starting generation with parameters: {parameters}");

            Room seedRoom = CreateSeedRoom(grid);
            if (seedRoom == null)
            {
                Debug.LogError("Failed to create seed room");
                return grid;
            }

            HashSet<AxialCoord> frontier = new HashSet<AxialCoord>();
            UpdateFrontier(grid, seedRoom, frontier);

            int iterations = 0;
            int generatedRooms = 1;

            while (generatedRooms < parameters.TargetRoomCount && 
                   iterations < parameters.MaxIterations && 
                   frontier.Count > 0)
            {
                iterations++;

                AxialCoord seedPosition = SelectFromFrontier(frontier);

                Room newRoom = TryCreateRoom(grid, seedPosition);

                if (newRoom != null)
                {
                    UpdateFrontier(grid, newRoom, frontier);
                    generatedRooms++;

                    if (generatedRooms % 10 == 0)
                    {
                        Debug.Log($"Generated {generatedRooms}/{parameters.TargetRoomCount} rooms (iteration {iterations})");
                    }
                }
                else
                {
                    frontier.Remove(seedPosition);
                }
            }

            Debug.Log($"Generation complete: {generatedRooms} rooms in {iterations} iterations");

            ValidateGeneration(grid);

            return grid;
        }

        private Room CreateSeedRoom(HexGrid grid)
        {
            Debug.Log($"Creating seed room at {parameters.StartPosition}");

            int roomSize = random.Next(parameters.MinHexagonsPerRoom, parameters.MaxHexagonsPerRoom + 1);
            int minAcceptableSize = CalculateMinAcceptableSize(roomSize);

            List<AxialCoord> cells = shapeGenerator.GenerateRoomShapeWithRetry(
                grid,
                parameters.StartPosition,
                roomSize,
                minAcceptableSize,
                parameters.MaxRetriesPerRoom,
                random);

            if (cells.Count == 0)
            {
                Debug.LogError($"Failed to generate seed room at {parameters.StartPosition}");
                return null;
            }

            Room room = grid.CreateRoom();

            foreach (AxialCoord coord in cells)
            {
                grid.AssignCellToRoom(coord, room.RoomID);
            }

            connectionAnalyzer.AnalyzeAndFlagEdges(grid, room);

            Debug.Log($"Seed room created: {room.RoomID} with {cells.Count} cells");

            return room;
        }

        private Room TryCreateRoom(HexGrid grid, AxialCoord seedPosition)
        {
            int roomSize = random.Next(parameters.MinHexagonsPerRoom, parameters.MaxHexagonsPerRoom + 1);
            int minAcceptableSize = CalculateMinAcceptableSize(roomSize);

            List<AxialCoord> cells = shapeGenerator.GenerateRoomShapeWithRetry(
                grid,
                seedPosition,
                roomSize,
                minAcceptableSize,
                parameters.MaxRetriesPerRoom,
                random);

            if (cells.Count < minAcceptableSize)
            {
                return null;
            }

            Room room = grid.CreateRoom();

            foreach (AxialCoord coord in cells)
            {
                grid.AssignCellToRoom(coord, room.RoomID);
            }

            connectionAnalyzer.AnalyzeAndFlagEdges(grid, room);

            ReanalyzeAdjacentRooms(grid, room);

            return room;
        }

        private void ReanalyzeAdjacentRooms(HexGrid grid, Room newRoom)
        {
            HashSet<int> adjacentRoomIDs = new HashSet<int>();

            foreach (AxialCoord cellCoord in newRoom.Cells)
            {
                foreach (AxialCoord neighborCoord in cellCoord.GetAllNeighbors())
                {
                    HexCell neighborCell = grid.GetCell(neighborCoord);
                    if (neighborCell != null && 
                        neighborCell.State == CellState.Room && 
                        neighborCell.RoomID != newRoom.RoomID &&
                        neighborCell.RoomID >= 0)
                    {
                        adjacentRoomIDs.Add(neighborCell.RoomID);
                    }
                }
            }

            foreach (int roomID in adjacentRoomIDs)
            {
                Room adjacentRoom = grid.GetRoom(roomID);
                if (adjacentRoom != null)
                {
                    connectionAnalyzer.AnalyzeAndFlagEdges(grid, adjacentRoom);
                }
            }
        }

        private void UpdateFrontier(HexGrid grid, Room room, HashSet<AxialCoord> frontier)
        {
            foreach (AxialCoord cellCoord in room.Cells)
            {
                frontier.Remove(cellCoord);

                foreach (AxialCoord neighborCoord in cellCoord.GetAllNeighbors())
                {
                    HexCell neighborCell = grid.GetCell(neighborCoord);
                    if (neighborCell == null || neighborCell.State == CellState.Empty)
                    {
                        if (!IsAdjacentToRoom(grid, neighborCoord, room.RoomID))
                        {
                            frontier.Add(neighborCoord);
                        }
                    }
                    else
                    {
                        frontier.Remove(neighborCoord);
                    }
                }
            }
        }

        private bool IsAdjacentToRoom(HexGrid grid, AxialCoord coord, int roomID)
        {
            foreach (AxialCoord neighbor in coord.GetAllNeighbors())
            {
                HexCell cell = grid.GetCell(neighbor);
                if (cell != null && cell.RoomID == roomID)
                {
                    return true;
                }
            }
            return false;
        }

        private AxialCoord SelectFromFrontier(HashSet<AxialCoord> frontier)
        {
            int index = random.Next(frontier.Count);
            return frontier.ElementAt(index);
        }

        private int CalculateMinAcceptableSize(int targetSize)
        {
            int halfTarget = Math.Max(1, targetSize / 2);
            return Math.Min(halfTarget, parameters.MinHexagonsPerRoom);
        }

        private void ValidateGeneration(HexGrid grid)
        {
            Debug.Log("Validating generation...");

            int roomsWithTooFewConnections = 0;
            int roomsWithTooManyConnections = 0;
            int totalConnections = 0;

            foreach (Room room in grid.GetAllRooms())
            {
                int connectionCount = room.GetConnectionCount();
                totalConnections += connectionCount;

                if (connectionCount < parameters.MinConnectionsPerRoom)
                {
                    roomsWithTooFewConnections++;
                    Debug.LogWarning($"Room {room.RoomID} has only {connectionCount} connections");
                }

                if (connectionCount > parameters.MaxConnectionsPerRoom)
                {
                    roomsWithTooManyConnections++;
                    Debug.LogWarning($"Room {room.RoomID} has {connectionCount} connections (exceeds max)");
                }
            }

            Debug.Log($"Validation complete:");
            Debug.Log($"  Total rooms: {grid.RoomCount}");
            Debug.Log($"  Total cells: {grid.CellCount}");
            Debug.Log($"  Total connections: {totalConnections / 2}");
            Debug.Log($"  Rooms with too few connections: {roomsWithTooFewConnections}");
            Debug.Log($"  Rooms with too many connections: {roomsWithTooManyConnections}");

            if (roomsWithTooFewConnections > 0)
            {
                Debug.LogWarning("Some rooms do not meet minimum connection requirements");
            }
        }
    }
}