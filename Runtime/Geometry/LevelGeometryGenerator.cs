using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CRG.Building;
using CRG.Core;
using CRG.Generation;
using CRG.Runtime;
using Object = UnityEngine.Object;

namespace CRG.Geometry
{
    // Builds level geometry for a single-grid level (CellGrid), a mixed-grid level (MixedLevel) or a hand-built
    // level (LevelLayout). All are handled as room parts: cells of one grid plus the placement that moves them into
    // the level. A single-grid level is one part in place; a mixed level has one part per placed room; a hand-built
    // level has one part per cell, since its cells each come from their own grid.
    public class LevelGeometryGenerator
    {
        private readonly CellGrid grid;
        private readonly MixedLevel mixedLevel;
        private readonly LevelLayout handBuilt;
        private readonly float cellSize;
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

        // For mixed levels: every edge shared by two rooms, keyed from both sides.
        private Dictionary<(int, CellCoord, int), RoomLink> linksByEdge;

        // For hand-built levels: rooms (cells joined by open edges) and thick-wall depths across all cells.
        private LayoutRooms handBuiltRooms;
        private LayoutWallLayout handBuiltWalls;

        // One grid's worth of cells to build, with the rules that differ between single-grid and mixed levels.
        private class RoomPart
        {
            public string Name;
            public CellGrid Grid;
            public RoomPlacement Placement;
            public List<GridCell> Cells;
            public Func<GridCell, bool> HasCeiling;
            // Edges between two room cells are seen from both sides; only one side builds a thin wall.
            public Func<GridCell, int, bool> BuildsThinWall;
            // Flags of the edge as seen from both sides (a door on either side makes it a door).
            public Func<GridCell, int, WallFlag> CombinedFlag;
            public IWallLayout Layout;
            // Whether a wall faces empty space (full thickness plus an outer face) rather than another cell.
            public Func<CellCoord, int, bool> IsExterior;
        }

        // wallThickness 0 builds zero-thickness double-sided walls; above 0 builds solid walls of that thickness.
        public LevelGeometryGenerator(CellGrid grid, float wallHeight = 3f, float doorHeight = 2.5f, float doorWidthRatio = 0.2f,
            float wallThickness = 0f, bool addCeiling = false)
            : this(grid, null, null, grid.CellSize, wallHeight, doorHeight, doorWidthRatio, wallThickness, addCeiling)
        {
        }

        public LevelGeometryGenerator(MixedLevel mixedLevel, float wallHeight = 3f, float doorHeight = 2.5f, float doorWidthRatio = 0.2f,
            float wallThickness = 0f, bool addCeiling = false)
            : this(null, mixedLevel, null, mixedLevel.CellSize, wallHeight, doorHeight, doorWidthRatio, wallThickness, addCeiling)
        {
        }

        // A hand-built layout is stored in units of the cell size; cellSize scales it into the level.
        public LevelGeometryGenerator(LevelLayout layout, float cellSize, float wallHeight = 3f, float doorHeight = 2.5f,
            float doorWidthRatio = 0.2f, float wallThickness = 0f, bool addCeiling = false)
            : this(null, null, layout, cellSize, wallHeight, doorHeight, doorWidthRatio, wallThickness, addCeiling)
        {
        }

        private LevelGeometryGenerator(CellGrid grid, MixedLevel mixedLevel, LevelLayout handBuilt, float cellSize, float wallHeight,
            float doorHeight, float doorWidthRatio, float wallThickness, bool addCeiling)
        {
            this.grid = grid;
            this.mixedLevel = mixedLevel;
            this.handBuilt = handBuilt;
            this.cellSize = cellSize;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
            this.wallThickness = Mathf.Max(0f, wallThickness);
            this.addCeiling = addCeiling;

            if (mixedLevel != null)
            {
                linksByEdge = new Dictionary<(int, CellCoord, int), RoomLink>();
                foreach (RoomLink link in mixedLevel.Links)
                {
                    linksByEdge[(link.RoomA, link.CellA, link.EdgeA)] = link;
                    linksByEdge[(link.RoomB, link.CellB, link.EdgeB)] = link;
                }
            }

            if (handBuilt != null)
            {
                handBuiltRooms = handBuilt.GetRooms();
                handBuiltWalls = new LayoutWallLayout(handBuilt, this.wallThickness);
            }
        }

        public static LevelGeometryGenerator FromParameters(CellGrid grid, GenerationParameters parameters)
        {
            return new LevelGeometryGenerator(grid, parameters.WallHeight, parameters.DoorHeight, parameters.DoorWidthRatio,
                parameters.WallThickness, parameters.AddCeiling)
            {
                options = parameters
            };
        }

        public static LevelGeometryGenerator FromParameters(MixedLevel mixedLevel, GenerationParameters parameters)
        {
            return new LevelGeometryGenerator(mixedLevel, parameters.WallHeight, parameters.DoorHeight, parameters.DoorWidthRatio,
                parameters.WallThickness, parameters.AddCeiling)
            {
                options = parameters
            };
        }

        public static LevelGeometryGenerator FromParameters(LevelLayout layout, GenerationParameters parameters)
        {
            return new LevelGeometryGenerator(layout, parameters.CellSize, parameters.WallHeight, parameters.DoorHeight,
                parameters.DoorWidthRatio, parameters.WallThickness, parameters.AddCeiling)
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
            return FinishLevel(BuildMesh("Generated Level", GetParts(), includeFloors: true, includeWalls: true, includeCeiling: AnyCeilings));
        }

        // Whether any room can get a ceiling: the level setting, or (hand-built levels) a room that turns it on.
        private bool AnyCeilings =>
            addCeiling || (handBuilt != null && Enumerable.Range(0, handBuiltRooms.Rooms.Count).Any(room => handBuiltRooms.HasCeiling(room, addCeiling)));

        public GameObject GenerateLevelSeparateByRoom()
        {
            GameObject levelRoot = new GameObject("Generated Level (By Room)");

            foreach ((string name, List<RoomPart> parts) in GetRoomGroups())
            {
                GameObject roomObject = BuildMesh(name, parts, includeFloors: true, includeWalls: true, includeCeiling: AnyCeilings);
                if (roomObject != null)
                {
                    roomObject.transform.SetParent(levelRoot.transform, false);
                }
            }

            return FinishLevel(levelRoot);
        }

        // The level mesh alone, without level data or any post-processing (for previews).
        public GameObject GenerateMeshOnly(string name)
        {
            return BuildMesh(name, GetParts(), includeFloors: true, includeWalls: true, includeCeiling: AnyCeilings);
        }

        public GameObject GenerateFloorOnly()
        {
            return FinishLevel(BuildMesh("Generated Level (Floor Only)", GetParts(), includeFloors: true, includeWalls: false, includeCeiling: false));
        }

        public GameObject GenerateWallsOnly()
        {
            return FinishLevel(BuildMesh("Generated Level (Walls Only)", GetParts(), includeFloors: false, includeWalls: true, includeCeiling: false));
        }

        // Generates the layout (single-grid or mixed, by parameters.GridType) and returns a generator ready to build it.
        public static LevelGeometryGenerator CreateFor(GenerationParameters parameters)
        {
            if (parameters.GridType == GridType.Mixed)
                return FromParameters(new MixedLevelGenerator().Generate(parameters), parameters);

            return FromParameters(new CRGGenerator().Generate(parameters), parameters);
        }

        public static GameObject GenerateComplete(GenerationParameters parameters, Material floorMaterial = null, Material wallMaterial = null,
            GameObject doorPrefab = null, Material ceilingMaterial = null)
        {
            LevelGeometryGenerator geometryGenerator = CreateFor(parameters);
            geometryGenerator.SetMaterials(floorMaterial, wallMaterial, ceilingMaterial);
            geometryGenerator.SetDoorPrefab(doorPrefab);

            return geometryGenerator.GenerateLevel();
        }

        public static GameObject GenerateCompleteMixed(GenerationParameters parameters, Material floorMaterial = null, Material wallMaterial = null,
            GameObject doorPrefab = null, Material ceilingMaterial = null)
        {
            MixedLevel level = new MixedLevelGenerator().Generate(parameters);

            LevelGeometryGenerator geometryGenerator = FromParameters(level, parameters);
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
            if (mixedLevel != null)
                data.Initialize(mixedLevel, wallHeight, doorHeight, doorWidthRatio, addCeiling);
            else if (handBuilt != null)
                data.Initialize(handBuilt, cellSize, wallHeight, doorHeight, doorWidthRatio, addCeiling);
            else
                data.Initialize(grid, wallHeight, doorHeight, doorWidthRatio, addCeiling);

            if (options != null)
            {
                // A copy of the settings, with the seed actually used, so the level can be made or edited again.
                int seed = grid != null ? grid.Seed : mixedLevel != null ? mixedLevel.Seed : -1;
                GenerationParameters used = options.Clone();
                if (seed >= 0)
                    used.RandomSeed = seed;
                data.SetGenerationSettings(used, seed, floorMaterial, wallMaterial, ceilingMaterial, doorPrefab);

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
                Transform spawnPoint = CreateChild(parent, $"Spawn_Room_{room.RoomID}{NameSuffix(room.Name)}");
                spawnPoint.localPosition = data.GetRoomAnchorLocalPosition(room);
                room.SpawnPoint = spawnPoint;
            }
        }

        private static string NameSuffix(string roomName)
        {
            return string.IsNullOrEmpty(roomName) ? string.Empty : $"_{roomName}";
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

        private List<RoomPart> GetParts()
        {
            if (mixedLevel != null)
                return GetPartsByRoom();

            if (handBuilt != null)
                return handBuilt.Cells.Select(HandBuiltPart).ToList();

            return new List<RoomPart> { SingleGridPart("Generated Level", grid.GetAllCells().Where(cell => cell.IsPartOfRoom())) };
        }

        private List<RoomPart> GetPartsByRoom()
        {
            if (mixedLevel != null)
                return mixedLevel.Rooms.Select(MixedRoomPart).ToList();

            return grid.GetAllRooms()
                .Select(room => SingleGridPart($"Room_{room.RoomID}", room.Cells.Select(grid.GetCell).Where(cell => cell != null && cell.IsPartOfRoom())))
                .ToList();
        }

        // The parts of each room, for building one mesh per room.
        private List<(string, List<RoomPart>)> GetRoomGroups()
        {
            if (handBuilt != null)
            {
                return handBuiltRooms.Rooms
                    .Select((cells, room) => ($"Room_{room}{NameSuffix(handBuiltRooms.Settings[room].roomName)}",
                        cells.Select(id => HandBuiltPart(handBuilt.GetCell(id))).ToList()))
                    .ToList();
            }

            return GetPartsByRoom().Select(part => (part.Name, new List<RoomPart> { part })).ToList();
        }

        private RoomPart SingleGridPart(string name, IEnumerable<GridCell> cells)
        {
            WallLayout layout = new WallLayout(grid, wallThickness);
            return new RoomPart
            {
                Name = name,
                Grid = grid,
                Placement = RoomPlacement.Identity,
                Cells = cells.ToList(),
                HasCeiling = cell => grid.GetRoom(cell.RoomID).HasCeiling,
                BuildsThinWall = (cell, edge) =>
                {
                    GridCell neighbor = grid.GetNeighbor(cell.Coordinate, edge);
                    return neighbor == null || !neighbor.IsPartOfRoom() || OwnsSharedEdge(cell.Coordinate, neighbor.Coordinate);
                },
                CombinedFlag = (cell, edge) =>
                {
                    WallFlag flag = cell.GetEdgeFlag(edge);
                    GridCell neighbor = grid.GetNeighbor(cell.Coordinate, edge);
                    if (neighbor != null && neighbor.IsPartOfRoom())
                        flag |= neighbor.GetEdgeFlag(grid.Topology.GetNeighborEdge(cell.Coordinate, edge));
                    return flag;
                },
                Layout = layout,
                IsExterior = layout.IsExterior
            };
        }

        // In a mixed level a room's own grid holds only that room; walls towards other rooms come from its links.
        private RoomPart MixedRoomPart(PlacedRoom room)
        {
            int roomID = room.RoomID;
            WallLayout layout = new WallLayout(room.Grid, wallThickness, (cell, edge) => !linksByEdge.ContainsKey((roomID, cell, edge)));
            return new RoomPart
            {
                Name = $"Room_{roomID}",
                Grid = room.Grid,
                Placement = room.Placement,
                Cells = room.Room.Cells.Select(room.Grid.GetCell).ToList(),
                HasCeiling = cell => room.Room.HasCeiling,
                // A wall shared with another room is built by the room with the lower ID.
                BuildsThinWall = (cell, edge) =>
                    !linksByEdge.TryGetValue((roomID, cell.Coordinate, edge), out RoomLink link) || Math.Min(link.RoomA, link.RoomB) == roomID,
                CombinedFlag = (cell, edge) => cell.GetEdgeFlag(edge),
                Layout = layout,
                IsExterior = layout.IsExterior
            };
        }

        // A hand-built cell is cell (0, 0) of its shape's own grid, moved into place. Its edge flags come from the
        // layout's links: outer edges are walls, and a shared edge is a door, a wall or nothing (open).
        private RoomPart HandBuiltPart(LayoutCell layoutCell)
        {
            int cellID = layoutCell.id;
            IGridTopology topology = CellShapes.GetTopology(layoutCell.shape);
            CellCoord coordinate = CellShapes.GetCell(layoutCell.shape);

            CellGrid cellGrid = new CellGrid(topology, cellSize);
            Room room = cellGrid.CreateRoom();
            cellGrid.AssignCellToRoom(coordinate, room.RoomID);
            GridCell cell = cellGrid.GetCell(coordinate);

            for (int edge = 0; edge < cell.EdgeCount; edge++)
                cell.SetEdgeFlag(edge, GetHandBuiltFlag(handBuilt.GetLink(cellID, edge)));

            return new RoomPart
            {
                Name = $"Cell_{cellID}",
                Grid = cellGrid,
                Placement = LevelLayout.GetGridPlacement(layoutCell, cellSize),
                Cells = new List<GridCell> { cell },
                HasCeiling = _ => handBuiltRooms.HasCeiling(handBuiltRooms.RoomOfCell[cellID], addCeiling),
                // A shared wall is built once, by the older cell (cell A of the link).
                BuildsThinWall = (_, edge) =>
                {
                    LayoutLink link = handBuilt.GetLink(cellID, edge);
                    return link == null || link.cellA == cellID;
                },
                CombinedFlag = (gridCell, edge) => gridCell.GetEdgeFlag(edge),
                Layout = handBuiltWalls.ForCell(cellID),
                IsExterior = (_, edge) => handBuiltWalls.IsExterior(cellID, edge)
            };
        }

        private static WallFlag GetHandBuiltFlag(LayoutLink link)
        {
            if (link == null)
                return WallFlag.Wall;

            switch (link.state)
            {
                case EdgeState.Door: return WallFlag.HasDoor;
                case EdgeState.Wall: return WallFlag.Wall;
                default: return WallFlag.NoWall;
            }
        }

        private GameObject BuildMesh(string name, List<RoomPart> parts, bool includeFloors, bool includeWalls, bool includeCeiling)
        {
            ProBuilderMeshBuilder builder = new ProBuilderMeshBuilder(parts.Count > 0 ? parts[0].Grid.Topology : null, cellSize,
                wallHeight, doorHeight, doorWidthRatio);

            foreach (RoomPart part in parts)
            {
                builder.SetRoom(part.Grid.Topology, part.Placement);

                foreach (GridCell cell in part.Cells)
                {
                    if (includeFloors)
                    {
                        builder.AddFloor(cell.Coordinate, floorMaterial);
                    }

                    if (includeWalls)
                    {
                        if (wallThickness > 0f)
                            AddCellThickWalls(builder, part, cell);
                        else
                            AddCellWalls(builder, part, cell);
                    }

                    if (includeCeiling && part.HasCeiling(cell))
                    {
                        builder.AddCeiling(cell.Coordinate, ceilingMaterial != null ? ceilingMaterial : floorMaterial);
                    }
                }
            }

            if (builder.IsEmpty)
            {
                Debug.LogWarning($"No geometry to build for '{name}'");
                return null;
            }

            return builder.Build(name);
        }

        private void AddCellWalls(ProBuilderMeshBuilder builder, RoomPart part, GridCell cell)
        {
            for (int edgeIndex = 0; edgeIndex < cell.EdgeCount; edgeIndex++)
            {
                if (!part.BuildsThinWall(cell, edgeIndex))
                    continue;

                WallFlag flag = part.CombinedFlag(cell, edgeIndex);

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
        private void AddCellThickWalls(ProBuilderMeshBuilder builder, RoomPart part, GridCell cell)
        {
            IWallLayout layout = part.Layout;
            IGridTopology topology = part.Grid.Topology;
            CellCoord coordinate = cell.Coordinate;

            for (int edgeIndex = 0; edgeIndex < cell.EdgeCount; edgeIndex++)
            {
                if (layout.GetDepth(coordinate, edgeIndex) <= 0f)
                    continue;

                bool exterior = part.IsExterior(coordinate, edgeIndex);
                bool hasDoor = cell.GetEdgeFlag(edgeIndex).HasFlag(WallFlag.HasDoor) && !exterior;
                ThickWallStrip strip = CellGeometry.GetThickWallStrip(topology, coordinate, edgeIndex, cellSize, layout);
                builder.AddThickWall(strip, hasDoor, exterior, wallMaterial);
            }

            for (int corner = 0; corner < cell.EdgeCount; corner++)
            {
                if (CellGeometry.TryGetCornerFill(topology, coordinate, corner, cellSize, layout, out CornerFill fill))
                    builder.AddCornerFill(fill, wallMaterial);
            }
        }

        private static bool OwnsSharedEdge(CellCoord cell, CellCoord neighbor)
        {
            if (cell.x != neighbor.x)
                return cell.x < neighbor.x;

            if (cell.y != neighbor.y)
                return cell.y < neighbor.y;

            return cell.variant < neighbor.variant;
        }
    }
}
