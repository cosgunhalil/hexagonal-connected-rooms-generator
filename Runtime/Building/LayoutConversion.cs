using System;
using System.Collections.Generic;
using System.Linq;
using CRG.Core;
using CRG.Generation;
using CRG.Runtime;
using UnityEngine;

namespace CRG.Building
{
    // Turns generated and baked levels into layouts for the level builder, so they can be edited by hand.
    // Every cell becomes a layout cell at exactly the same place: its grid cell's center and the direction of its
    // corner 0 fix the placement, so edge i stays edge i. Edges inside a room become open, doors stay doors and other
    // edges between rooms become walls. Room order (and so room IDs), ceilings, names, tags and chosen start and end
    // rooms are kept.
    public static class LayoutConversion
    {
        private class SourceRoom
        {
            public int RoomID;
            public readonly List<(GridType gridType, CellCoord cell, RoomPlacement placement, CellCoord key)> Cells =
                new List<(GridType, CellCoord, RoomPlacement, CellCoord)>();
            public bool HasCeiling;
            public string Name = string.Empty;
            public string Tag = string.Empty;
        }

        public static LevelLayout FromGrid(CellGrid grid, bool levelCeilings)
        {
            List<SourceRoom> rooms = new List<SourceRoom>();
            foreach (Room room in grid.GetAllRooms().OrderBy(room => room.RoomID))
            {
                SourceRoom source = new SourceRoom { RoomID = room.RoomID, HasCeiling = levelCeilings && room.HasCeiling };
                foreach (CellCoord cell in room.Cells)
                    source.Cells.Add((grid.Topology.Type, cell, RoomPlacement.Identity, cell));
                rooms.Add(source);
            }

            return Build(rooms, grid.CellSize, levelCeilings,
                (roomID, cell, edge) => grid.GetCell(cell).GetEdgeFlag(edge).HasFlag(WallFlag.HasDoor), -1, -1);
        }

        public static LevelLayout FromMixedLevel(MixedLevel level, bool levelCeilings)
        {
            List<SourceRoom> rooms = new List<SourceRoom>();
            foreach (PlacedRoom room in level.Rooms)
            {
                SourceRoom source = new SourceRoom { RoomID = room.RoomID, HasCeiling = levelCeilings && room.Room.HasCeiling };
                foreach (CellCoord cell in room.Room.Cells)
                    source.Cells.Add((room.GridType, cell, room.Placement, cell));
                rooms.Add(source);
            }

            return Build(rooms, level.CellSize, levelCeilings,
                (roomID, cell, edge) => level.GetRoom(roomID).Grid.GetCell(cell).GetEdgeFlag(edge).HasFlag(WallFlag.HasDoor), -1, -1);
        }

        // Any baked level: generated (single grid or mixed) or hand-built. A hand-built level gives back a copy of the
        // layout it was baked from; older hand-built levels without one are converted like generated ones, with
        // every edge inside a room open unless it is a door.
        public static LevelLayout FromLevelData(CRGLevelData data)
        {
            if (data.SourceLayout != null)
                return data.SourceLayout.Clone();

            List<SourceRoom> rooms = new List<SourceRoom>();
            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                SourceRoom source = new SourceRoom
                {
                    RoomID = room.RoomID,
                    HasCeiling = room.HasCeiling,
                    Name = room.Name ?? string.Empty,
                    Tag = room.Tag ?? string.Empty
                };
                foreach (CellCoord cell in room.Cells)
                {
                    (GridType gridType, CellCoord gridCell, RoomPlacement placement) = data.GetCellGrid(room, cell);
                    source.Cells.Add((gridType, gridCell, placement, cell));
                }
                rooms.Add(source);
            }

            HashSet<(int, CellCoord, int)> doors = new HashSet<(int, CellCoord, int)>(data.Doors.Select(door => (door.RoomA, door.CellA, door.EdgeA)));
            bool levelCeilings = data.HasGenerationSettings ? data.GenerationSettings.AddCeiling : data.Rooms.Any(room => room.HasCeiling);
            int start = data.StartRoom != null ? rooms.FindIndex(room => room.RoomID == data.StartRoom.RoomID) : -1;
            int end = data.EndRoom != null ? rooms.FindIndex(room => room.RoomID == data.EndRoom.RoomID) : -1;

            return Build(rooms, data.CellSize, levelCeilings, (roomID, cell, edge) => doors.Contains((roomID, cell, edge)), start, end);
        }

        // The builder settings for a baked level: the ones it was made with, or, for levels made before settings
        // were saved, defaults with the values the level data has.
        public static GenerationParameters SettingsFor(CRGLevelData data)
        {
            if (data.HasGenerationSettings)
                return data.GenerationSettings.Clone();

            GenerationParameters parameters = GenerationParameters.CreateDefault();
            parameters.CellSize = data.CellSize;
            parameters.WallHeight = data.WallHeight;
            parameters.DoorHeight = data.DoorHeight;
            parameters.DoorWidthRatio = data.DoorWidthRatio;
            parameters.AddCeiling = data.Rooms.Any(room => room.HasCeiling);
            return parameters;
        }

        // The layout shape of a grid cell: octagon + square grids hold octagons (variant 0) and squares (variant 1).
        public static CellShape GetShape(GridType gridType, CellCoord cell)
        {
            switch (gridType)
            {
                case GridType.Hexagon: return CellShape.Hexagon;
                case GridType.Square: return CellShape.Square;
                case GridType.Triangle: return CellShape.Triangle;
                case GridType.OctagonSquare: return cell.variant == 0 ? CellShape.Octagon : CellShape.Square;
                default: throw new ArgumentException($"{gridType} is not a grid of its own", nameof(gridType));
            }
        }

        // isDoor(roomID, key, edge) says whether a source cell's edge is a door; start and end are room indices in
        // rooms (-1 = automatic).
        private static LevelLayout Build(List<SourceRoom> rooms, float cellSize, bool levelCeilings, Func<int, CellCoord, int, bool> isDoor, int start, int end)
        {
            LevelLayout layout = new LevelLayout();
            Dictionary<int, (int roomIndex, CellCoord key)> sources = new Dictionary<int, (int, CellCoord)>();
            List<int> firstCells = new List<int>();

            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                SourceRoom room = rooms[roomIndex];
                foreach ((GridType gridType, CellCoord cell, RoomPlacement placement, CellCoord key) in room.Cells)
                {
                    CellShape shape = GetShape(gridType, cell);
                    LayoutCell layoutCell = layout.Place(shape, ToLayoutPlacement(gridType, cell, placement, shape, cellSize), EdgeState.Wall, out string reason);
                    if (layoutCell == null)
                        throw new InvalidOperationException($"Cell {cell} of room {room.RoomID} can't be converted: {reason}");

                    sources[layoutCell.id] = (roomIndex, key);
                    if (firstCells.Count == roomIndex)
                    {
                        firstCells.Add(layoutCell.id);
                        layoutCell.roomName = room.Name;
                        layoutCell.roomTag = room.Tag;
                        layoutCell.roomCeiling = room.HasCeiling
                            ? (levelCeilings ? RoomCeiling.LevelSetting : RoomCeiling.On)
                            : (levelCeilings ? RoomCeiling.Off : RoomCeiling.LevelSetting);
                    }
                }
            }

            foreach (LayoutLink link in layout.Links)
            {
                (int roomA, CellCoord keyA) = sources[link.cellA];
                (int roomB, CellCoord keyB) = sources[link.cellB];

                bool door = isDoor(rooms[roomA].RoomID, keyA, link.edgeA) || isDoor(rooms[roomB].RoomID, keyB, link.edgeB);
                if (door)
                    link.state = EdgeState.Door;
                else
                    link.state = roomA == roomB ? EdgeState.Open : EdgeState.Wall;
            }

            // Rooms keep their order, so only start and end rooms that differ from the automatic ones need choosing.
            if (start > 0 && start < firstCells.Count)
                layout.StartCell = firstCells[start];
            if (end >= 0 && end < firstCells.Count && end != layout.GetRooms().EndRoom)
                layout.EndCell = firstCells[end];

            return layout;
        }

        // The placement (in units of the cell size) that puts the layout shape exactly onto a grid cell, with its
        // corner 0 on the cell's corner 0, so both number their edges the same way.
        private static RoomPlacement ToLayoutPlacement(GridType gridType, CellCoord cell, RoomPlacement gridPlacement, CellShape shape, float cellSize)
        {
            IGridTopology topology = GridTopology.Get(gridType);
            Vector3 center = gridPlacement.TransformPoint(topology.GetCellCenter(cell, cellSize));
            Vector3 corner = gridPlacement.TransformDirection(topology.GetCornerOffset(cell, 0, cellSize));
            Vector3 shapeCorner = CellShapes.GetCorner(shape, 0);

            double angle = Math.Atan2(corner.z, corner.x) - Math.Atan2(shapeCorner.z, shapeCorner.x);
            return new RoomPlacement(center.x / cellSize, center.z / cellSize, angle);
        }
    }
}
