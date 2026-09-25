using System.Collections.Generic;
using UnityEngine;
using HRCG.Core;
using HRCG.Generation;
using HRCG.Runtime;

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
        private GameObject doorPrefab;

        // Post-processing options (colliders, NavMesh, spawn points). Null means geometry only.
        private GenerationParameters options;

        public LevelGeometryGenerator(HexGrid grid, float wallHeight = 3f, float doorHeight = 2.5f, float doorWidthRatio = 0.2f)
        {
            this.grid = grid;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
        }

        public static LevelGeometryGenerator FromParameters(HexGrid grid, GenerationParameters parameters)
        {
            return new LevelGeometryGenerator(grid, parameters.WallHeight, parameters.DoorHeight, parameters.DoorWidthRatio)
            {
                options = parameters
            };
        }

        public void SetMaterials(Material floor, Material wall)
        {
            floorMaterial = floor;
            wallMaterial = wall;
        }

        // Optional prefab placed in every doorway. Its +Z axis points through the door (from RoomA into RoomB)
        // and its X axis runs along the opening.
        public void SetDoorPrefab(GameObject prefab)
        {
            doorPrefab = prefab;
        }

        public GameObject GenerateLevel()
        {
            return FinishLevel(BuildMesh("Generated Level", GetRoomCells(), includeFloors: true, includeWalls: true));
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

            return FinishLevel(levelRoot);
        }

        public GameObject GenerateFloorOnly()
        {
            return FinishLevel(BuildMesh("Generated Level (Floor Only)", GetRoomCells(), includeFloors: true, includeWalls: false));
        }

        public GameObject GenerateWallsOnly()
        {
            return FinishLevel(BuildMesh("Generated Level (Walls Only)", GetRoomCells(), includeFloors: false, includeWalls: true));
        }

        public static GameObject GenerateComplete(GenerationParameters parameters, Material floorMaterial = null, Material wallMaterial = null, GameObject doorPrefab = null)
        {
            HRCGGenerator generator = new HRCGGenerator();
            HexGrid grid = generator.Generate(parameters);

            LevelGeometryGenerator geometryGenerator = FromParameters(grid, parameters);
            geometryGenerator.SetMaterials(floorMaterial, wallMaterial);
            geometryGenerator.SetDoorPrefab(doorPrefab);

            return geometryGenerator.GenerateLevel();
        }

        // Order matters: the NavMesh is baked before door prefabs are placed so closed doors cannot block it.
        private GameObject FinishLevel(GameObject level)
        {
            if (level == null)
                return null;

            HRCGLevelData data = level.AddComponent<HRCGLevelData>();
            data.Initialize(grid, wallHeight, doorHeight, doorWidthRatio);

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

        private static void CreateSpawnPoints(GameObject level, HRCGLevelData data)
        {
            Transform parent = CreateChild(level.transform, "Spawn Points");

            foreach (HRCGLevelData.RoomData room in data.Rooms)
            {
                Transform spawnPoint = CreateChild(parent, $"Spawn_Room_{room.RoomID}");
                spawnPoint.localPosition = data.GetRoomAnchorLocalPosition(room);
                room.SpawnPoint = spawnPoint;
            }
        }

        private void PlaceDoors(GameObject level, HRCGLevelData data)
        {
            Transform parent = CreateChild(level.transform, "Doors");

            foreach (HRCGLevelData.DoorData door in data.Doors)
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
