using System;
using System.Collections.Generic;
using System.Linq;
using HRCG.Core;
using UnityEngine;

namespace HRCG.Generation
{
    public class RoomShapeGenerator
    {
        public List<AxialCoord> GenerateRoomShape(
            HexGrid grid,
            AxialCoord seedCell,
            int targetCellCount,
            int minAcceptableCount,
            System.Random random)
        {
            if (targetCellCount <= 0)
                throw new ArgumentException("Target cell count must be greater than 0");

            if (minAcceptableCount <= 0)
                minAcceptableCount = 1;

            List<AxialCoord> result = new List<AxialCoord>();
            HashSet<AxialCoord> visited = new HashSet<AxialCoord>();
            Queue<AxialCoord> frontier = new Queue<AxialCoord>();

            if (!IsCellValid(grid, seedCell))
            {
                Debug.LogWarning($"Seed cell {seedCell} is not valid for room generation");
                return result;
            }

            result.Add(seedCell);
            visited.Add(seedCell);

            AddValidNeighborsToFrontier(grid, seedCell, visited, frontier);

            while (result.Count < targetCellCount && frontier.Count > 0)
            {
                AxialCoord nextCell = SelectFromFrontier(frontier, random);

                if (IsCellValid(grid, nextCell) && !visited.Contains(nextCell))
                {
                    result.Add(nextCell);
                    visited.Add(nextCell);

                    AddValidNeighborsToFrontier(grid, nextCell, visited, frontier);
                }
            }

            if (result.Count < minAcceptableCount)
            {
                Debug.LogWarning($"Room generation only reached {result.Count} cells (target: {targetCellCount}, minimum: {minAcceptableCount})");
            }

            return result;
        }

        private bool IsCellValid(HexGrid grid, AxialCoord coord)
        {
            HexCell cell = grid.GetCell(coord);
            
            if (cell == null)
                return true;

            return cell.State == CellState.Empty;
        }

        private void AddValidNeighborsToFrontier(
            HexGrid grid,
            AxialCoord coord,
            HashSet<AxialCoord> visited,
            Queue<AxialCoord> frontier)
        {
            foreach (AxialCoord neighbor in coord.GetAllNeighbors())
            {
                if (!visited.Contains(neighbor) && IsCellValid(grid, neighbor))
                {
                    if (!frontier.Contains(neighbor))
                    {
                        frontier.Enqueue(neighbor);
                    }
                }
            }
        }

        private AxialCoord SelectFromFrontier(Queue<AxialCoord> frontier, System.Random random)
        {
            if (frontier.Count == 1)
                return frontier.Dequeue();

            int randomIndex = random.Next(frontier.Count);
            List<AxialCoord> tempList = frontier.ToList();
            AxialCoord selected = tempList[randomIndex];
            
            Queue<AxialCoord> newFrontier = new Queue<AxialCoord>();
            foreach (AxialCoord coord in tempList)
            {
                if (!coord.Equals(selected))
                {
                    newFrontier.Enqueue(coord);
                }
            }
            
            frontier.Clear();
            while (newFrontier.Count > 0)
            {
                frontier.Enqueue(newFrontier.Dequeue());
            }

            return selected;
        }

        public List<AxialCoord> GenerateRoomShapeWithRetry(
            HexGrid grid,
            AxialCoord seedCell,
            int targetCellCount,
            int minAcceptableCount,
            int maxRetries,
            System.Random random)
        {
            List<AxialCoord> bestResult = null;
            int bestCount = 0;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                List<AxialCoord> result = GenerateRoomShape(
                    grid,
                    seedCell,
                    targetCellCount,
                    minAcceptableCount,
                    random);

                if (result.Count >= targetCellCount)
                    return result;

                if (result.Count > bestCount)
                {
                    bestResult = result;
                    bestCount = result.Count;
                }

                if (result.Count >= minAcceptableCount)
                    return result;
            }

            return bestResult ?? new List<AxialCoord>();
        }
    }
}