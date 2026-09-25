using System;
using System.Collections.Generic;
using System.Linq;
using CRG.Core;
using UnityEngine;

namespace CRG.Generation
{
    public class CRGGenerator
    {
        private const int MaxLayoutCandidates = 8;

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

            EnsureMinimumConnections(grid);
            AssignCeilings(grid);
            ValidateGeneration(grid);

            return grid;
        }

        private Room CreateSeedRoom(HexGrid grid)
        {
            Debug.Log($"Creating seed room at {parameters.StartPosition}");

            List<AxialCoord> cells = GenerateRoomCells(grid, parameters.StartPosition);
            if (cells == null)
            {
                Debug.LogError($"Failed to generate seed room at {parameters.StartPosition}");
                return null;
            }

            Room room = PlaceRoom(grid, cells);

            Debug.Log($"Seed room created: {room.RoomID} with {cells.Count} cells");

            return room;
        }

        private Room TryCreateRoom(HexGrid grid, AxialCoord seedPosition)
        {
            List<AxialCoord> cells = GenerateRoomCells(grid, seedPosition);
            if (cells == null)
                return null;

            // Every room after the seed must be reachable: reject it unless at least one
            // adjacent room can still accept a connection.
            if (!CanConnectToExistingRoom(grid, cells))
                return null;

            Room room = PlaceRoom(grid, cells);

            int maxNewConnections = parameters.ReserveConnectionForGrowth
                ? Math.Max(1, parameters.MaxConnectionsPerRoom - 1)
                : int.MaxValue;
            connectionAnalyzer.ConnectToNeighbors(grid, room, random, maxNewConnections, parameters.LoopChance);

            return room;
        }

        // Returns null when the shape cannot reach MinHexagonsPerRoom.
        private List<AxialCoord> GenerateRoomCells(HexGrid grid, AxialCoord seedPosition)
        {
            int roomSize = random.Next(parameters.MinHexagonsPerRoom, parameters.MaxHexagonsPerRoom + 1);

            List<AxialCoord> cells = shapeGenerator.GenerateRoomShapeWithRetry(
                grid,
                seedPosition,
                roomSize,
                parameters.MinHexagonsPerRoom,
                parameters.MaxRetriesPerRoom,
                random);

            return cells.Count >= parameters.MinHexagonsPerRoom ? cells : null;
        }

        private bool CanConnectToExistingRoom(HexGrid grid, List<AxialCoord> cells)
        {
            foreach (int neighborID in connectionAnalyzer.FindSharedEdges(grid, cells, -1).Keys)
            {
                Room neighborRoom = grid.GetRoom(neighborID);
                if (neighborRoom != null && connectionAnalyzer.HasCapacity(neighborRoom))
                    return true;
            }

            return false;
        }

        private Room PlaceRoom(HexGrid grid, List<AxialCoord> cells)
        {
            Room room = grid.CreateRoom();

            foreach (AxialCoord coord in cells)
            {
                grid.AssignCellToRoom(coord, room.RoomID);
            }

            connectionAnalyzer.FlagRoomEdges(grid, room);

            return room;
        }

        // The frontier holds every empty cell touching the structure built so far.
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
                        frontier.Add(neighborCoord);
                    }
                }
            }
        }

        // Runs after the layout is final, so CeilingChance never changes which rooms and doors a seed produces.
        // At CeilingChance 1 no random numbers are drawn.
        private void AssignCeilings(HexGrid grid)
        {
            if (!parameters.AddCeiling || parameters.CeilingChance >= 1f)
                return;

            List<Room> rooms = new List<Room>(grid.GetAllRooms());
            rooms.Sort((a, b) => a.RoomID.CompareTo(b.RoomID));

            foreach (Room room in rooms)
            {
                room.HasCeiling = random.NextDouble() < parameters.CeilingChance;
            }
        }

        // Rooms placed early may end up below MinConnectionsPerRoom; add doors to adjacent rooms where limits allow.
        private void EnsureMinimumConnections(HexGrid grid)
        {
            List<Room> rooms = new List<Room>(grid.GetAllRooms());
            rooms.Sort((a, b) => a.RoomID.CompareTo(b.RoomID));

            foreach (Room room in rooms)
            {
                connectionAnalyzer.EnsureMinimumConnections(grid, room, parameters.MinConnectionsPerRoom, random);
            }
        }

        // Tournament selection: LayoutBias sets how many random frontier cells compete, and the one
        // farthest from (bias > 0) or closest to (bias < 0) the start position wins.
        // With bias 0 this is a single uniform pick.
        private AxialCoord SelectFromFrontier(HashSet<AxialCoord> frontier)
        {
            List<AxialCoord> cells = frontier.ToList();
            AxialCoord best = cells[random.Next(cells.Count)];

            int candidates = 1 + (int)Math.Round(Math.Abs(parameters.LayoutBias) * (MaxLayoutCandidates - 1));
            bool preferFar = parameters.LayoutBias > 0f;

            for (int i = 1; i < candidates; i++)
            {
                AxialCoord candidate = cells[random.Next(cells.Count)];
                int candidateDistance = candidate.DistanceTo(parameters.StartPosition);
                int bestDistance = best.DistanceTo(parameters.StartPosition);

                if (preferFar ? candidateDistance > bestDistance : candidateDistance < bestDistance)
                    best = candidate;
            }

            return best;
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
            if (parameters.AddCeiling)
            {
                int covered = grid.GetAllRooms().Count(room => room.HasCeiling);
                Debug.Log($"  Rooms with ceiling: {covered}/{grid.RoomCount} (Ceiling Chance {parameters.CeilingChance:F2})");
            }
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