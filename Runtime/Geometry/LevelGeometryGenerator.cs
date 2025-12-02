using System.Collections.Generic;
using UnityEngine;
using HRCG.Core;
using HRCG.Generation;

namespace HRCG.Geometry
{
    public class LevelGeometryGenerator
    {
        private HexGrid grid;
        private ProBuilderMeshBuilder meshBuilder;
        private Material floorMaterial;
        private Material wallMaterial;

        public LevelGeometryGenerator(HexGrid grid, float wallHeight = 3f, float doorHeight = 2.5f)
        {
            this.grid = grid;
            this.meshBuilder = new ProBuilderMeshBuilder(grid.HexSize, wallHeight, doorHeight);
        }

        public void SetMaterials(Material floor, Material wall)
        {
            floorMaterial = floor;
            wallMaterial = wall;
        }

        public GameObject GenerateLevel(bool combineIntoSingleMesh = true, bool cleanupSourceObjects = true)
        {
            GameObject levelRoot = new GameObject("Generated Level");
            List<GameObject> allMeshObjects = new List<GameObject>();

            foreach (HexCell cell in grid.GetAllCells())
            {
                if (cell.State == CellState.Room)
                {
                    GameObject cellGeometry = meshBuilder.BuildCellGeometry(cell, floorMaterial, wallMaterial);
                    cellGeometry.transform.SetParent(levelRoot.transform);
                    allMeshObjects.Add(cellGeometry);
                }
            }

            if (combineIntoSingleMesh && allMeshObjects.Count > 0)
            {
                GameObject combinedMesh = MeshCombiner.CombineStandardMeshes(
                    allMeshObjects, 
                    wallMaterial, 
                    "Combined Level Mesh");

                if (combinedMesh != null)
                {
                    combinedMesh.transform.SetParent(levelRoot.transform);
                    MeshCombiner.OptimizeMesh(combinedMesh);

                    if (cleanupSourceObjects)
                    {
                        MeshCombiner.CleanupSourceObjects(allMeshObjects);
                    }
                }
            }

            return levelRoot;
        }

        public GameObject GenerateLevelSeparateByRoom(bool combinePerRoom = true)
        {
            GameObject levelRoot = new GameObject("Generated Level (By Room)");

            foreach (Room room in grid.GetAllRooms())
            {
                GameObject roomObject = new GameObject($"Room_{room.RoomID}");
                roomObject.transform.SetParent(levelRoot.transform);

                List<GameObject> roomMeshObjects = new List<GameObject>();

                foreach (AxialCoord coord in room.Cells)
                {
                    HexCell cell = grid.GetCell(coord);
                    if (cell != null && cell.State == CellState.Room)
                    {
                        GameObject cellGeometry = meshBuilder.BuildCellGeometry(cell, floorMaterial, wallMaterial);
                        cellGeometry.transform.SetParent(roomObject.transform);
                        roomMeshObjects.Add(cellGeometry);
                    }
                }

                if (combinePerRoom && roomMeshObjects.Count > 0)
                {
                    GameObject combinedRoom = MeshCombiner.CombineStandardMeshes(
                        roomMeshObjects,
                        wallMaterial,
                        $"Room_{room.RoomID}_Combined");

                    if (combinedRoom != null)
                    {
                        combinedRoom.transform.SetParent(roomObject.transform);
                        MeshCombiner.OptimizeMesh(combinedRoom);
                        MeshCombiner.CleanupSourceObjects(roomMeshObjects);
                    }
                }
            }

            return levelRoot;
        }

        public GameObject GenerateFloorOnly()
        {
            GameObject levelRoot = new GameObject("Generated Level (Floor Only)");
            List<GameObject> floorObjects = new List<GameObject>();

            foreach (HexCell cell in grid.GetAllCells())
            {
                if (cell.State == CellState.Room)
                {
                    GameObject floor = meshBuilder.BuildFloor(cell.Coordinate, floorMaterial);
                    floor.transform.SetParent(levelRoot.transform);
                    floorObjects.Add(floor);
                }
            }

            if (floorObjects.Count > 0)
            {
                GameObject combinedFloor = MeshCombiner.CombineStandardMeshes(
                    floorObjects,
                    floorMaterial,
                    "Combined Floor");

                if (combinedFloor != null)
                {
                    combinedFloor.transform.SetParent(levelRoot.transform);
                    MeshCombiner.OptimizeMesh(combinedFloor);
                    MeshCombiner.CleanupSourceObjects(floorObjects);
                }
            }

            return levelRoot;
        }

        public GameObject GenerateWallsOnly()
        {
            GameObject levelRoot = new GameObject("Generated Level (Walls Only)");
            List<GameObject> wallObjects = new List<GameObject>();

            foreach (HexCell cell in grid.GetAllCells())
            {
                if (cell.State == CellState.Room)
                {
                    for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
                    {
                        WallFlag flag = cell.GetEdgeFlag(edgeIndex);

                        if (flag.HasFlag(WallFlag.Wall))
                        {
                            GameObject wall = meshBuilder.BuildWall(cell.Coordinate, edgeIndex, wallMaterial);
                            wall.transform.SetParent(levelRoot.transform);
                            wallObjects.Add(wall);
                        }
                        else if (flag.HasFlag(WallFlag.HasDoor))
                        {
                            GameObject doorWall = meshBuilder.BuildDoorWall(cell.Coordinate, edgeIndex, wallMaterial);
                            doorWall.transform.SetParent(levelRoot.transform);
                            wallObjects.Add(doorWall);
                        }
                    }
                }
            }

            if (wallObjects.Count > 0)
            {
                GameObject combinedWalls = MeshCombiner.CombineStandardMeshes(
                    wallObjects,
                    wallMaterial,
                    "Combined Walls");

                if (combinedWalls != null)
                {
                    combinedWalls.transform.SetParent(levelRoot.transform);
                    MeshCombiner.OptimizeMesh(combinedWalls);
                    MeshCombiner.CleanupSourceObjects(wallObjects);
                }
            }

            return levelRoot;
        }

        public static GameObject GenerateComplete(GenerationParameters parameters, Material floorMaterial = null, Material wallMaterial = null)
        {
            HRCGGenerator generator = new HRCGGenerator();
            HexGrid grid = generator.Generate(parameters);

            LevelGeometryGenerator geometryGenerator = new LevelGeometryGenerator(grid);
            geometryGenerator.SetMaterials(floorMaterial, wallMaterial);

            return geometryGenerator.GenerateLevel(true, true);
        }
    }
}
