using System.Collections.Generic;
using System.Linq;
using CRG.Core;
using UnityEngine;

namespace CRG.Generation
{
    // A level whose rooms each live on their own grid (of any type), placed so that rooms touch only along
    // exactly matching edges. Every such shared edge is a RoomLink: a door or a wall between two rooms.
    public class MixedLevel
    {
        public float CellSize { get; }
        public List<PlacedRoom> Rooms { get; } = new List<PlacedRoom>();
        public List<RoomLink> Links { get; } = new List<RoomLink>();

        public MixedLevel(float cellSize)
        {
            CellSize = cellSize;
        }

        public PlacedRoom GetRoom(int roomID)
        {
            return roomID >= 0 && roomID < Rooms.Count ? Rooms[roomID] : null;
        }

        public IEnumerable<RoomLink> Doors => Links.Where(link => link.IsDoor);

        public IEnumerable<RoomLink> GetLinks(int roomID)
        {
            return Links.Where(link => link.RoomA == roomID || link.RoomB == roomID);
        }
    }

    public class PlacedRoom
    {
        public int RoomID { get; internal set; }
        public GridType GridType { get; }
        public IGridTopology Topology => Grid.Topology;

        // The room's own grid in local coordinates; it contains only this room. Edge flags: NoWall inside the
        // room, Wall on every outer edge, HasDoor where a door to another room was placed.
        public CellGrid Grid { get; }
        public Room Room { get; }
        public RoomPlacement Placement { get; internal set; }

        // Doors keyed by the connected room's ID (one door per connected pair).
        public Dictionary<int, RoomLink> Doors { get; } = new Dictionary<int, RoomLink>();

        public int ConnectionCount => Doors.Count;
        public IEnumerable<int> ConnectedRoomIDs => Doors.Keys;

        public PlacedRoom(GridType gridType, CellGrid grid, Room room)
        {
            GridType = gridType;
            Grid = grid;
            Room = room;
        }

        public IEnumerable<(CellCoord cell, int edge)> GetOuterEdges()
        {
            foreach (CellCoord cell in Room.Cells)
            {
                GridCell gridCell = Grid.GetCell(cell);
                for (int edge = 0; edge < gridCell.EdgeCount; edge++)
                {
                    if (gridCell.GetEdgeFlag(edge) != WallFlag.NoWall)
                        yield return (cell, edge);
                }
            }
        }

        // Edge endpoints in the room's own grid, before placement.
        public (Vector3, Vector3) GetLocalEdge(CellCoord cell, int edge)
        {
            Vector3 center = Grid.GetCellCenter(cell);
            (Vector3 start, Vector3 end) = Topology.GetEdgeOffsets(cell, edge, Grid.CellSize);
            return (center + start, center + end);
        }

        public (Vector3, Vector3) GetWorldEdge(CellCoord cell, int edge)
        {
            (Vector3 start, Vector3 end) = GetLocalEdge(cell, edge);
            return (Placement.TransformPoint(start), Placement.TransformPoint(end));
        }

        public Vector3[] GetWorldCorners(CellCoord cell)
        {
            Vector3 center = Grid.GetCellCenter(cell);
            int count = Topology.GetEdgeCount(cell);
            Vector3[] corners = new Vector3[count];
            for (int i = 0; i < count; i++)
                corners[i] = Placement.TransformPoint(center + Topology.GetCornerOffset(cell, i, Grid.CellSize));
            return corners;
        }

        public Vector3 GetWorldCenter(CellCoord cell)
        {
            return Placement.TransformPoint(Grid.GetCellCenter(cell));
        }
    }

    // An edge shared exactly by two rooms (seen from room A as CellA/EdgeA and from room B as CellB/EdgeB,
    // running in opposite directions). IsDoor marks the pair's single door; other shared edges are walls.
    public class RoomLink
    {
        public int RoomA;
        public CellCoord CellA;
        public int EdgeA;
        public int RoomB;
        public CellCoord CellB;
        public int EdgeB;
        public bool IsDoor;

        public int GetOther(int roomID)
        {
            return roomID == RoomA ? RoomB : RoomA;
        }

        public override string ToString()
        {
            return $"{(IsDoor ? "Door" : "Wall")} {RoomA}:{CellA}/{EdgeA} <-> {RoomB}:{CellB}/{EdgeB}";
        }
    }
}
