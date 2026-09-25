using System.Collections.Generic;
using UnityEngine;
using HRCG.Core;
using HRCG.Generation;

namespace HRCG.Geometry
{
    public class LevelGeometryGenerator
    {
        private readonly HexGrid grid;
        private readonly float wallHeight;
        private readonly float doorHeight;
        private readonly float doorWidthRatio;
        private Material floorMaterial;
        private Material wallMaterial;

        public LevelGeometryGenerator(HexGrid grid, float wallHeight = 3f, float doorHeight = 2.5f, float doorWidthRatio = 0.2f)
        {
            this.grid = grid;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
        }

        public static LevelGeometryGenerator FromParameters(HexGrid grid, GenerationParameters parameters)
        {
            return new LevelGeometryGenerator(grid, parameters.WallHeight, parameters.DoorHeight, parameters.DoorWidthRatio);
        }

        public void SetMaterials(Material floor, Material wall)
        {
            floorMaterial = floor;
            wallMaterial = wall;
        }

        public GameObject GenerateLevel()
        {
            return BuildMesh("Generated Level", GetRoomCells(), includeFloors: true, includeWalls: true);
        }

        public GameObject GenerateLevelSeparateByRoom()
        {
            GameObject levelRoot = new GameObject("Generated Level (By Room)");

            foreach (Room room in grid.GetAllRooms())
            {
                List<HexCell> roomCells = new List<HexCell>();
                foreach (AxialCoord coord in room.Cells)
                {
                    HexCell cell = grid.GetCell(coord);
                    if (cell != null && cell.IsPartOfRoom())
                    {
                        roomCells.Add(cell);
                    }
                }

                GameObject roomObject = BuildMesh($"Room_{room.RoomID}", roomCells, includeFloors: true, includeWalls: true);
                if (roomObject != null)
                {
                    roomObject.transform.SetParent(levelRoot.transform, false);
                }
            }

            return levelRoot;
        }

        public GameObject GenerateFloorOnly()
        {
            return BuildMesh("Generated Level (Floor Only)", GetRoomCells(), includeFloors: true, includeWalls: false);
        }

        public GameObject GenerateWallsOnly()
        {
            return BuildMesh("Generated Level (Walls Only)", GetRoomCells(), includeFloors: false, includeWalls: true);
        }

        public static GameObject GenerateComplete(GenerationParameters parameters, Material floorMaterial = null, Material wallMaterial = null)
        {
            HRCGGenerator generator = new HRCGGenerator();
            HexGrid grid = generator.Generate(parameters);

            LevelGeometryGenerator geometryGenerator = FromParameters(grid, parameters);
            geometryGenerator.SetMaterials(floorMaterial, wallMaterial);

            return geometryGenerator.GenerateLevel();
        }

        private List<HexCell> GetRoomCells()
        {
            List<HexCell> roomCells = new List<HexCell>();
            foreach (HexCell cell in grid.GetAllCells())
            {
                if (cell.IsPartOfRoom())
                {
                    roomCells.Add(cell);
                }
            }
            return roomCells;
        }

        private GameObject BuildMesh(string name, List<HexCell> cells, bool includeFloors, bool includeWalls)
        {
            ProBuilderMeshBuilder builder = new ProBuilderMeshBuilder(grid.HexSize, wallHeight, doorHeight, doorWidthRatio);

            foreach (HexCell cell in cells)
            {
                if (includeFloors)
                {
                    builder.AddFloor(cell.Coordinate, floorMaterial);
                }

                if (includeWalls)
                {
                    AddCellWalls(builder, cell);
                }
            }

            if (builder.IsEmpty)
            {
                Debug.LogWarning($"No geometry to build for '{name}'");
                return null;
            }

            return builder.Build(name);
        }

        private void AddCellWalls(ProBuilderMeshBuilder builder, HexCell cell)
        {
            for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
            {
                HexCell neighbor = grid.GetCell(cell.Coordinate.GetNeighbor(edgeIndex));
                bool neighborIsRoom = neighbor != null && neighbor.IsPartOfRoom();

                // An edge between two room cells is seen from both sides; only one of them builds it.
                if (neighborIsRoom && !OwnsSharedEdge(cell.Coordinate, neighbor.Coordinate))
                    continue;

                WallFlag flag = cell.GetEdgeFlag(edgeIndex);
                if (neighborIsRoom)
                {
                    flag |= neighbor.GetEdgeFlag(HexDirection.GetOppositeDirection(edgeIndex));
                }

                if (flag.HasFlag(WallFlag.HasDoor))
                {
                    builder.AddDoorWall(cell.Coordinate, edgeIndex, wallMaterial);
                }
                else if (flag.HasFlag(WallFlag.Wall))
                {
                    builder.AddWall(cell.Coordinate, edgeIndex, wallMaterial);
                }
            }
        }

        private static bool OwnsSharedEdge(AxialCoord cell, AxialCoord neighbor)
        {
            if (cell.columnIndex != neighbor.columnIndex)
                return cell.columnIndex < neighbor.columnIndex;

            return cell.rowIndex < neighbor.rowIndex;
        }
    }
}
