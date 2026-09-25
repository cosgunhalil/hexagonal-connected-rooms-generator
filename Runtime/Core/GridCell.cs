using System;
using UnityEngine;

namespace CRG.Core
{
    public enum CellState
    {
        Empty,
        Wall,
        Room
    }

    [Flags]
    public enum WallFlag
    {
        None = 0,
        NoWall = 1,
        Wall = 2,
        HasDoor = 4
    }

    [Serializable]
    public class GridCell
    {
        public CellCoord Coordinate { get; private set; }
        public CellState State { get; set; }
        public int RoomID { get; set; }

        private WallFlag[] edgeFlags;

        public int EdgeCount => edgeFlags.Length;

        public GridCell(CellCoord coordinate, int edgeCount)
        {
            Coordinate = coordinate;
            State = CellState.Empty;
            RoomID = -1;
            edgeFlags = new WallFlag[edgeCount];

            for (int i = 0; i < edgeCount; i++)
            {
                edgeFlags[i] = WallFlag.None;
            }
        }

        public WallFlag GetEdgeFlag(int edgeIndex)
        {
            ValidateEdgeIndex(edgeIndex);
            return edgeFlags[edgeIndex];
        }

        public void SetEdgeFlag(int edgeIndex, WallFlag flag)
        {
            ValidateEdgeIndex(edgeIndex);
            edgeFlags[edgeIndex] = flag;
        }

        public bool HasEdgeFlag(int edgeIndex, WallFlag flag)
        {
            ValidateEdgeIndex(edgeIndex);
            return (edgeFlags[edgeIndex] & flag) == flag;
        }

        public void AddEdgeFlag(int edgeIndex, WallFlag flag)
        {
            ValidateEdgeIndex(edgeIndex);
            edgeFlags[edgeIndex] |= flag;
        }

        public void RemoveEdgeFlag(int edgeIndex, WallFlag flag)
        {
            ValidateEdgeIndex(edgeIndex);
            edgeFlags[edgeIndex] &= ~flag;
        }

        public void ClearAllEdgeFlags()
        {
            for (int i = 0; i < EdgeCount; i++)
            {
                edgeFlags[i] = WallFlag.None;
            }
        }

        public void Reset()
        {
            State = CellState.Empty;
            RoomID = -1;
            ClearAllEdgeFlags();
        }

        public bool IsPartOfRoom()
        {
            return State == CellState.Room && RoomID >= 0;
        }

        private void ValidateEdgeIndex(int edgeIndex)
        {
            if (edgeIndex < 0 || edgeIndex >= EdgeCount)
                throw new ArgumentOutOfRangeException(nameof(edgeIndex), $"Edge index must be between 0 and {EdgeCount - 1}");
        }

        public override string ToString()
        {
            return $"GridCell[{Coordinate}] State:{State} RoomID:{RoomID}";
        }
    }
}
