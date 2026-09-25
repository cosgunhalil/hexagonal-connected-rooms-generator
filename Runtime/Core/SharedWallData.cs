using System.Collections.Generic;

namespace CRG.Core
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

        public void AddSharedEdge(CellCoord cellA, int edgeIndexA, CellCoord cellB, int edgeIndexB)
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
        public CellCoord CellA;
        public int EdgeIndexA;
        public CellCoord CellB;
        public int EdgeIndexB;

        public EdgeConnection(CellCoord cellA, int edgeIndexA, CellCoord cellB, int edgeIndexB)
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
