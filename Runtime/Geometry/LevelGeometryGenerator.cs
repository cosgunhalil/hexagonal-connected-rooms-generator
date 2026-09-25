using System.Collections.Generic;
using UnityEngine;
using CRG.Core;
using CRG.Generation;
using CRG.Runtime;

namespace CRG.Geometry
{
    public class LevelGeometryGenerator
    {
        private readonly HexGrid grid;
        private readonly float wallHeight;
        private readonly float doorHeight;
        private readonly float doorWidthRatio;
        private readonly float wallThickness;
        private readonly bool addCeiling;
        private Material floorMaterial;
        private Material wallMaterial;
        private Material ceilingMaterial;
        private GameObject doorPrefab;

        // Post-processing options (colliders, NavMesh, spawn points). Null means geometry only.
        private GenerationParameters options;

        // wallThickness 0 builds zero-thickness double-sided walls; above 0 builds solid walls of that thickness.
        public LevelGeometryGenerator(HexGrid grid, float wallHeight = 3f, float doorHeight = 2.5f, float doorWidthRatio = 0.2f,
            float wallThickness = 0f, bool addCeiling = false)
        {
            this.grid = grid;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
            this.wallThickness = Mathf.Max(0f, wallThickness);
            this.addCeiling = addCeiling;
        }

        public static LevelGeometryGenerator FromParameters(HexGrid grid, GenerationParameters parameters)
        {
            return new LevelGeometryGenerator(grid, parameters.WallHeight, parameters.DoorHeight, parameters.DoorWidthRatio,
                parameters.WallThickness, parameters.AddCeiling)
            {
                options = parameters
            };
        }

        // A null ceiling material falls back to the floor material.
        public void SetMaterials(Material floor, Material wall, Material ceiling = null)
        {
            floorMaterial = floor;
            wallMaterial = wall;
            ceilingMaterial = ceiling;
        }

        // Optional prefab placed in every doorway. Its +Z axis points through the door (from RoomA into RoomB)
        // and its X axis runs along the opening.
        public void SetDoorPrefab(GameObject prefab)
        {
            doorPrefab = prefab;
        }

        public GameObject GenerateLevel()
        {
            return FinishLevel(BuildMesh("Generated Level", GetRoomCells(), includeFloors: true, includeWalls: true, includeCeiling: addCeiling));
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

                GameObject roomObject = BuildMesh($"Room_{room.RoomID}", roomCells, includeFloors: true, includeWalls: true, includeCeiling: addCeiling);
                if (roomObject != null)
                {
                    roomObject.transform.SetParent(levelRoot.transform, false);
                }
            }

            return FinishLevel(levelRoot);
        }

        public GameObject GenerateFloorOnly()
        {
            return FinishLevel(BuildMesh("Generated Level (Floor Only)", GetRoomCells(), includeFloors: true, includeWalls: false, includeCeiling: false));
        }

        public GameObject GenerateWallsOnly()
        {
            return FinishLevel(BuildMesh("Generated Level (Walls Only)", GetRoomCells(), includeFloors: false, includeWalls: true, includeCeiling: false));
        }

        public static GameObject GenerateComplete(GenerationParameters parameters, Material floorMaterial = null, Material wallMaterial = null,
            GameObject doorPrefab = null, Material ceilingMaterial = null)
        {
            CRGGenerator generator = new CRGGenerator();
            HexGrid grid = generator.Generate(parameters);

            LevelGeometryGenerator geometryGenerator = FromParameters(grid, parameters);
            geometryGenerator.SetMaterials(floorMaterial, wallMaterial, ceilingMaterial);
            geometryGenerator.SetDoorPrefab(doorPrefab);

            return geometryGenerator.GenerateLevel();
        }

        // Order matters: the NavMesh is baked before door prefabs are placed so closed doors cannot block it.
        private GameObject FinishLevel(GameObject level)
        {
            if (level == null)
                return null;

            CRGLevelData data = level.AddComponent<CRGLevelData>();
            data.Initialize(grid, wallHeight, doorHeight, doorWidthRatio, addCeiling);

            if (options != null)
            {
                if (options.AddMeshCollider)
                    AddMeshColliders(level);

                if (options.BakeNavMesh)
                {
                    if (!LevelNavMeshBaker.DoorsFitAgent(options, out string message))
                        Debug.LogWarning(message);

                    LevelNavMeshBaker.Bake(level, options.NavMeshAgentTypeID, options.NavMeshGeometry);
                }

                if (options.CreateSpawnPoints)
                    CreateSpawnPoints(level, data);
            }

            if (doorPrefab != null)
                PlaceDoors(level, data);

            return level;
        }

        private static void AddMeshColliders(GameObject level)
        {
            foreach (MeshFilter meshFilter in level.GetComponentsInChildren<MeshFilter>())
            {
                MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
                if (meshCollider == null)
                    meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();

                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        private static void CreateSpawnPoints(GameObject level, CRGLevelData data)
        {
            Transform parent = CreateChild(level.transform, "Spawn Points");

            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                Transform spawnPoint = CreateChild(parent, $"Spawn_Room_{room.RoomID}");
                spawnPoint.localPosition = data.GetRoomAnchorLocalPosition(room);
                room.SpawnPoint = spawnPoint;
            }
        }

        private void PlaceDoors(GameObject level, CRGLevelData data)
        {
            Transform parent = CreateChild(level.transform, "Doors");

            foreach (CRGLevelData.DoorData door in data.Doors)
            {
                GameObject instance = InstantiateDoor(parent);
                instance.name = $"Door_{door.RoomA}_{door.RoomB}";
                instance.transform.localPosition = data.GetDoorCenterLocal(door);
                instance.transform.localRotation = Quaternion.LookRotation(data.GetDoorForwardLocal(door), Vector3.up);
                door.DoorObject = instance.transform;
            }
        }

        private GameObject InstantiateDoor(Transform parent)
        {
#if UNITY_EDITOR
            // Keep the prefab link when generating in the Editor.
            if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(doorPrefab))
                return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(doorPrefab, parent);
#endif
            return Object.Instantiate(doorPrefab, parent);
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
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

        private GameObject BuildMesh(string name, List<HexCell> cells, bool includeFloors, bool includeWalls, bool includeCeiling)
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
                    if (wallThickness > 0f)
                        AddCellThickWalls(builder, cell);
                    else
                        AddCellWalls(builder, cell);
                }

                if (includeCeiling && grid.GetRoom(cell.RoomID).HasCeiling)
                {
                    builder.AddCeiling(cell.Coordinate, ceilingMaterial != null ? ceilingMaterial : floorMaterial);
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

        // Every room cell builds its own side of each of its walls: half the thickness towards another room,
        // the full thickness (plus an outer face) towards empty space. Doors are always between rooms.
        private void AddCellThickWalls(ProBuilderMeshBuilder builder, HexCell cell)
        {
            float[] depths = new float[6];
            bool[] exterior = new bool[6];

            for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
            {
                WallFlag flag = cell.GetEdgeFlag(edgeIndex);
                if (!flag.HasFlag(WallFlag.Wall) && !flag.HasFlag(WallFlag.HasDoor))
                    continue;

                HexCell neighbor = grid.GetCell(cell.Coordinate.GetNeighbor(edgeIndex));
                exterior[edgeIndex] = neighbor == null || !neighbor.IsPartOfRoom();
                depths[edgeIndex] = exterior[edgeIndex] ? wallThickness : wallThickness / 2f;
            }

            for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
            {
                if (depths[edgeIndex] <= 0f)
                    continue;

                ThickWallStrip strip = HexGeometry.GetThickWallStrip(cell.Coordinate, edgeIndex, grid.HexSize, depths);
                bool hasDoor = cell.GetEdgeFlag(edgeIndex).HasFlag(WallFlag.HasDoor) && !exterior[edgeIndex];
                builder.AddThickWall(strip, hasDoor, exterior[edgeIndex], wallMaterial);
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
