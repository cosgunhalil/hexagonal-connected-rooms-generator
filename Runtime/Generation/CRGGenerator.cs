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
        private IGridTopology topology;

        public CellGrid Generate(GenerationParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            if (!parameters.Validate(out string errorMessage))
                throw new ArgumentException($"Invalid parameters: {errorMessage}");

            if (parameters.GridType == GridType.Mixed)
                throw new ArgumentException("Mixed levels use a grid per room; generate them with MixedLevelGenerator");

            this.parameters = parameters;
            int seed = parameters.ResolveSeed();
            this.random = new System.Random(seed);

            this.shapeGenerator = new RoomShapeGenerator();
            this.connectionAnalyzer = new ConnectionAnalyzer(parameters.MaxConnectionsPerRoom);

            this.topology = GridTopology.Get(parameters.GridType);
            CellGrid grid = new CellGrid(topology, parameters.CellSize) { Seed = seed };

            Debug.Log($"Starting generation with parameters: {parameters}");

            Room seedRoom = CreateSeedRoom(grid);
            if (seedRoom == null)
            {
                Debug.LogError("Failed to create seed room");
                return grid;
            }

            HashSet<CellCoord> frontier = new HashSet<CellCoord>();
            UpdateFrontier(grid, seedRoom, frontier);

            int iterations = 0;
            int generatedRooms = 1;

            while (generatedRooms < parameters.TargetRoomCount && 
                   iterations < parameters.MaxIterations && 
                   frontier.Count > 0)
            {
                iterations++;

                CellCoord seedPosition = SelectFromFrontier(frontier);

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

        private Room CreateSeedRoom(CellGrid grid)
        {
            Debug.Log($"Creating seed room at {parameters.StartPosition}");

            List<CellCoord> cells = GenerateRoomCells(grid, parameters.StartPosition);
            if (cells == null)
            {
                Debug.LogError($"Failed to generate seed room at {parameters.StartPosition}");
                return null;
            }

            Room room = PlaceRoom(grid, cells);

            Debug.Log($"Seed room created: {room.RoomID} with {cells.Count} cells");

            return room;
        }

        private Room TryCreateRoom(CellGrid grid, CellCoord seedPosition)
        {
            List<CellCoord> cells = GenerateRoomCells(grid, seedPosition);
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

        // Returns null when the shape cannot reach MinCellsPerRoom.
        private List<CellCoord> GenerateRoomCells(CellGrid grid, CellCoord seedPosition)
        {
            int roomSize = random.Next(parameters.MinCellsPerRoom, parameters.MaxCellsPerRoom + 1);

            List<CellCoord> cells = shapeGenerator.GenerateRoomShapeWithRetry(
                grid,
                seedPosition,
                roomSize,
                parameters.MinCellsPerRoom,
                parameters.MaxRetriesPerRoom,
                random);

            return cells.Count >= parameters.MinCellsPerRoom ? cells : null;
        }

        private bool CanConnectToExistingRoom(CellGrid grid, List<CellCoord> cells)
        {
            foreach (int neighborID in connectionAnalyzer.FindSharedEdges(grid, cells, -1).Keys)
            {
                Room neighborRoom = grid.GetRoom(neighborID);
                if (neighborRoom != null && connectionAnalyzer.HasCapacity(neighborRoom))
                    return true;
            }

            return false;
        }

        private Room PlaceRoom(CellGrid grid, List<CellCoord> cells)
        {
            Room room = grid.CreateRoom();

            foreach (CellCoord coord in cells)
            {
                grid.AssignCellToRoom(coord, room.RoomID);
            }

            connectionAnalyzer.FlagRoomEdges(grid, room);

            return room;
        }

        // The frontier holds every empty cell touching the structure built so far.
        private void UpdateFrontier(CellGrid grid, Room room, HashSet<CellCoord> frontier)
        {
            foreach (CellCoord cellCoord in room.Cells)
            {
                frontier.Remove(cellCoord);

                foreach (CellCoord neighborCoord in grid.Topology.GetNeighbors(cellCoord))
                {
                    GridCell neighborCell = grid.GetCell(neighborCoord);
                    if (neighborCell == null || neighborCell.State == CellState.Empty)
                    {
                        frontier.Add(neighborCoord);
                    }
                }
            }
        }

        // Runs after the layout is final, so CeilingChance never changes which rooms and doors a seed produces.
        // At CeilingChance 1 no random numbers are drawn.
        private void AssignCeilings(CellGrid grid)
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
        private void EnsureMinimumConnections(CellGrid grid)
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
        private CellCoord SelectFromFrontier(HashSet<CellCoord> frontier)
        {
            List<CellCoord> cells = frontier.ToList();
            CellCoord best = cells[random.Next(cells.Count)];

            int candidates = 1 + (int)Math.Round(Math.Abs(parameters.LayoutBias) * (MaxLayoutCandidates - 1));
            bool preferFar = parameters.LayoutBias > 0f;

            for (int i = 1; i < candidates; i++)
            {
                CellCoord candidate = cells[random.Next(cells.Count)];
                int candidateDistance = topology.GetDistance(candidate, parameters.StartPosition);
                int bestDistance = topology.GetDistance(best, parameters.StartPosition);

                if (preferFar ? candidateDistance > bestDistance : candidateDistance < bestDistance)
                    best = candidate;
            }

            return best;
        }

        private void ValidateGeneration(CellGrid grid)
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