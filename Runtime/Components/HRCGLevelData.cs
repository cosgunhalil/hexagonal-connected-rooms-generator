using System;
using System.Collections.Generic;
using UnityEngine;
using HRCG.Core;

namespace HRCG.Runtime
{
    // Room and door layout of a generated level, stored on the level GameObject so it survives
    // scene saves. Positions are in the level's local space (the mesh is built around its origin).
    public class HRCGLevelData : MonoBehaviour
    {
        public enum RoomRole
        {
            Normal,
            Start,
            End
        }

        [Serializable]
        public class RoomData
        {
            public int RoomID;
            public List<AxialCoord> Cells = new List<AxialCoord>();
            public List<int> ConnectedRoomIDs = new List<int>();

            [Tooltip("Number of doors on the shortest path from the start room")]
            public int DistanceFromStart;
            public RoomRole Role;
            public Transform SpawnPoint;
            public bool HasCeiling;

            public bool IsDeadEnd => ConnectedRoomIDs.Count == 1;
        }

        [Serializable]
        public class DoorData
        {
            public int RoomA;
            public int RoomB;
            public AxialCoord CellA;
            public int EdgeA;
            public Transform DoorObject;
        }

        [Header("Scene Overlays")]
        public bool showRoomOutlines = true;
        public bool showRoomLabels = true;
        public bool showDoors = true;
        public bool showConnections = true;
        public bool showRoomRoles = true;

        [SerializeField, HideInInspector] private float hexSize;
        [SerializeField, HideInInspector] private float wallHeight;
        [SerializeField, HideInInspector] private float doorHeight;
        [SerializeField, HideInInspector] private float doorWidthRatio;
        [SerializeField, HideInInspector] private List<RoomData> rooms = new List<RoomData>();
        [SerializeField, HideInInspector] private List<DoorData> doors = new List<DoorData>();

        public float HexSize => hexSize;
        public float WallHeight => wallHeight;
        public float DoorHeight => doorHeight;
        public IReadOnlyList<RoomData> Rooms => rooms;
        public IReadOnlyList<DoorData> Doors => doors;

        public RoomData StartRoom => rooms.Find(room => room.Role == RoomRole.Start);
        public RoomData EndRoom => rooms.Find(room => room.Role == RoomRole.End);

        public void Initialize(HexGrid grid, float wallHeight, float doorHeight, float doorWidthRatio, bool ceilingsEnabled = false)
        {
            hexSize = grid.HexSize;
            this.wallHeight = wallHeight;
            this.doorHeight = doorHeight;
            this.doorWidthRatio = doorWidthRatio;
            rooms.Clear();
            doors.Clear();

            List<Room> sortedRooms = new List<Room>(grid.GetAllRooms());
            sortedRooms.Sort((a, b) => a.RoomID.CompareTo(b.RoomID));

            foreach (Room room in sortedRooms)
            {
                RoomData roomData = new RoomData { RoomID = room.RoomID, HasCeiling = ceilingsEnabled && room.HasCeiling };
                roomData.Cells.AddRange(room.Cells);
                roomData.ConnectedRoomIDs.AddRange(room.GetConnectedRoomIDs());
                roomData.ConnectedRoomIDs.Sort();
                rooms.Add(roomData);

                foreach (KeyValuePair<int, SharedWallData> connection in room.Connections)
                {
                    // Each door is stored once, from the room with the lower ID.
                    if (connection.Key < room.RoomID)
                        continue;

                    foreach (EdgeConnection edge in connection.Value.SharedEdges)
                    {
                        doors.Add(new DoorData
                        {
                            RoomA = room.RoomID,
                            RoomB = connection.Key,
                            CellA = edge.CellA,
                            EdgeA = edge.EdgeIndexA
                        });
                    }
                }
            }

            AssignRoles();
        }

        public Transform GetSpawnPoint(int roomID)
        {
            return GetRoom(roomID)?.SpawnPoint;
        }

        // Breadth-first search over doors from the first room placed (lowest ID).
        private void AssignRoles()
        {
            if (rooms.Count == 0)
                return;

            foreach (RoomData room in rooms)
            {
                room.DistanceFromStart = -1;
                room.Role = RoomRole.Normal;
            }

            RoomData start = rooms[0];
            start.DistanceFromStart = 0;
            start.Role = RoomRole.Start;

            Queue<RoomData> queue = new Queue<RoomData>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                RoomData current = queue.Dequeue();
                foreach (int neighborID in current.ConnectedRoomIDs)
                {
                    RoomData neighbor = GetRoom(neighborID);
                    if (neighbor != null && neighbor.DistanceFromStart < 0)
                    {
                        neighbor.DistanceFromStart = current.DistanceFromStart + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Farthest room becomes the end; ties go to the lowest ID for reproducibility.
            RoomData end = start;
            foreach (RoomData room in rooms)
            {
                if (room.DistanceFromStart > end.DistanceFromStart)
                    end = room;
            }

            if (end != start)
                end.Role = RoomRole.End;
        }

        public RoomData GetRoom(int roomID)
        {
            return rooms.Find(room => room.RoomID == roomID);
        }

        public Vector3 GetCellLocalPosition(AxialCoord cell)
        {
            return cell.ToWorldPosition(hexSize);
        }

        // Center of the room cell closest to the room's centroid, so the anchor always lies inside the room.
        public Vector3 GetRoomAnchorLocalPosition(RoomData room)
        {
            if (room.Cells.Count == 0)
                return Vector3.zero;

            Vector3 centroid = Vector3.zero;
            foreach (AxialCoord cell in room.Cells)
                centroid += GetCellLocalPosition(cell);
            centroid /= room.Cells.Count;

            Vector3 best = GetCellLocalPosition(room.Cells[0]);
            foreach (AxialCoord cell in room.Cells)
            {
                Vector3 position = GetCellLocalPosition(cell);
                if ((position - centroid).sqrMagnitude < (best - centroid).sqrMagnitude)
                    best = position;
            }

            return best;
        }

        // Center of the door opening at floor level.
        public Vector3 GetDoorCenterLocal(DoorData door)
        {
            return GetCellLocalPosition(door.CellA) + HexMath.GetEdgeCenter(door.EdgeA, hexSize);
        }

        // Horizontal direction through the door, pointing from RoomA into RoomB.
        public Vector3 GetDoorForwardLocal(DoorData door)
        {
            return HexMath.GetEdgeNormal(door.EdgeA);
        }

        // Endpoints of the door opening at floor level.
        public (Vector3, Vector3) GetDoorOpeningLocal(DoorData door)
        {
            Vector3 center = GetDoorCenterLocal(door);
            (Vector3 start, Vector3 end) = HexMath.GetEdgeVertices(door.EdgeA, hexSize);
            Vector3 halfWidth = (end - start).normalized * (HexMath.CalculateDoorWidth(hexSize, doorWidthRatio) / 2f);

            return (center - halfWidth, center + halfWidth);
        }
    }
}
