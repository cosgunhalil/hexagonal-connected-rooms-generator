using System.Collections.Generic;

namespace HRCG.Core
{
    public class SharedWallData
    {
        public int NeighborRoomID { get; private set; }
        public List<EdgeConnection> SharedEdges { get; private set; }

        public SharedWallData(int neighborRoomID)
        {
            NeighborRoomID = neighborRoomID;
            SharedEdges = new List<EdgeConnection>();
        }

        public void AddSharedEdge(AxialCoord cellA, int edgeIndexA, AxialCoord cellB, int edgeIndexB)
        {
            SharedEdges.Add(new EdgeConnection(cellA, edgeIndexA, cellB, edgeIndexB));
        }

        public int GetSharedEdgeCount()
        {
            return SharedEdges.Count;
        }

        public override string ToString()
        {
            return $"SharedWall with Room {NeighborRoomID}: {SharedEdges.Count} edges";
        }
    }

    public struct EdgeConnection
    {
        public AxialCoord CellA;
        public int EdgeIndexA;
        public AxialCoord CellB;
        public int EdgeIndexB;

        public EdgeConnection(AxialCoord cellA, int edgeIndexA, AxialCoord cellB, int edgeIndexB)
        {
            CellA = cellA;
            EdgeIndexA = edgeIndexA;
            CellB = cellB;
            EdgeIndexB = edgeIndexB;
        }

        public override string ToString()
        {
            return $"Edge[{CellA}:E{EdgeIndexA} <-> {CellB}:E{EdgeIndexB}]";
        }
    }
}
