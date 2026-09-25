using System;
using System.Collections.Generic;
using System.Linq;
using CRG.Core;
using UnityEngine;

namespace CRG.Generation
{
    public class RoomShapeGenerator
    {
        public List<CellCoord> GenerateRoomShape(
            CellGrid grid,
            CellCoord seedCell,
            int targetCellCount,
            int minAcceptableCount,
            System.Random random)
        {
            if (targetCellCount <= 0)
                throw new ArgumentException("Target cell count must be greater than 0");

            if (minAcceptableCount <= 0)
                minAcceptableCount = 1;

            List<CellCoord> result = new List<CellCoord>();
            HashSet<CellCoord> visited = new HashSet<CellCoord>();
            Queue<CellCoord> frontier = new Queue<CellCoord>();

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
                CellCoord nextCell = SelectFromFrontier(frontier, random);

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

        private bool IsCellValid(CellGrid grid, CellCoord coord)
        {
            GridCell cell = grid.GetCell(coord);
            
            if (cell == null)
                return true;

            return cell.State == CellState.Empty;
        }

        private void AddValidNeighborsToFrontier(
            CellGrid grid,
            CellCoord coord,
            HashSet<CellCoord> visited,
            Queue<CellCoord> frontier)
        {
            foreach (CellCoord neighbor in grid.Topology.GetNeighbors(coord))
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

        private CellCoord SelectFromFrontier(Queue<CellCoord> frontier, System.Random random)
        {
            if (frontier.Count == 1)
                return frontier.Dequeue();

            int randomIndex = random.Next(frontier.Count);
            List<CellCoord> tempList = frontier.ToList();
            CellCoord selected = tempList[randomIndex];
            
            Queue<CellCoord> newFrontier = new Queue<CellCoord>();
            foreach (CellCoord coord in tempList)
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

        public List<CellCoord> GenerateRoomShapeWithRetry(
            CellGrid grid,
            CellCoord seedCell,
            int targetCellCount,
            int minAcceptableCount,
            int maxRetries,
            System.Random random)
        {
            List<CellCoord> bestResult = null;
            int bestCount = 0;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                List<CellCoord> result = GenerateRoomShape(
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

            return bestResult ?? new List<CellCoord>();
        }
    }
}