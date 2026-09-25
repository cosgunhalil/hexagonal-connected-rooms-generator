using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using HRCG.Core;
using HRCG.Runtime;

namespace HRCG.Editor
{
    // Scene-view overlays for generated levels: room outlines and labels, door markers,
    // and the room connection graph routed through each door.
    public static class HRCGLevelDataGizmos
    {
        private const float OutlineLift = 0.05f;
        private const float OutlineWidth = 3f;
        private const float DoorMarkerWidth = 8f;
        private const float ConnectionWidth = 2f;
        private const float GoldenRatioConjugate = 0.618034f;

        private static readonly Color DoorColor = new Color(1f, 0.85f, 0.1f);
        private static GUIStyle labelStyle;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawLevelOverlays(HRCGLevelData data, GizmoType gizmoType)
        {
            if (data.Rooms.Count == 0)
                return;

            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.matrix = data.transform.localToWorldMatrix;

            if (data.showRoomOutlines)
                DrawRoomOutlines(data);

            if (data.showConnections)
                DrawConnections(data);

            if (data.showDoors)
                DrawDoors(data);

            if (data.showRoomLabels)
                DrawRoomLabels(data);

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
        }

        public static Color GetRoomColor(int roomID)
        {
            return Color.HSVToRGB((roomID * GoldenRatioConjugate) % 1f, 0.65f, 1f);
        }

        private static void DrawRoomOutlines(HRCGLevelData data)
        {
            Vector3[] corners = HexMath.GetHexVertices(data.HexSize * 0.92f);
            Vector3[] outline = new Vector3[7];
            Vector3 lift = Vector3.up * OutlineLift;

            foreach (HRCGLevelData.RoomData room in data.Rooms)
            {
                Handles.color = GetRoomColor(room.RoomID);

                foreach (AxialCoord cell in room.Cells)
                {
                    Vector3 center = data.GetCellLocalPosition(cell) + lift;
                    for (int i = 0; i < 6; i++)
                        outline[i] = center + corners[i];
                    outline[6] = outline[0];

                    Handles.DrawAAPolyLine(OutlineWidth, outline);
                }
            }
        }

        private static void DrawDoors(HRCGLevelData data)
        {
            Handles.color = DoorColor;
            Vector3 markerHeight = Vector3.up * (data.DoorHeight * 0.5f);

            foreach (HRCGLevelData.DoorData door in data.Doors)
            {
                (Vector3 start, Vector3 end) = data.GetDoorOpeningLocal(door);
                Handles.DrawAAPolyLine(DoorMarkerWidth, start + markerHeight, end + markerHeight);
                Handles.DrawAAPolyLine(OutlineWidth, start, start + markerHeight * 2f);
                Handles.DrawAAPolyLine(OutlineWidth, end, end + markerHeight * 2f);
            }
        }

        private static void DrawConnections(HRCGLevelData data)
        {
            Dictionary<int, Vector3> anchors = GetLabelAnchors(data);
            Vector3 lift = Vector3.up * (data.WallHeight * 0.5f);

            foreach (HRCGLevelData.DoorData door in data.Doors)
            {
                (Vector3 start, Vector3 end) = data.GetDoorOpeningLocal(door);
                Vector3 doorPoint = (start + end) * 0.5f + lift;

                Handles.color = GetRoomColor(door.RoomA);
                Handles.DrawAAPolyLine(ConnectionWidth, anchors[door.RoomA], doorPoint);

                Handles.color = GetRoomColor(door.RoomB);
                Handles.DrawAAPolyLine(ConnectionWidth, doorPoint, anchors[door.RoomB]);
            }
        }

        private static void DrawRoomLabels(HRCGLevelData data)
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 11,
                    richText = true
                };
            }

            Dictionary<int, Vector3> anchors = GetLabelAnchors(data);

            foreach (HRCGLevelData.RoomData room in data.Rooms)
            {
                string color = ColorUtility.ToHtmlStringRGB(GetRoomColor(room.RoomID));
                string doorsLabel = room.ConnectedRoomIDs.Count == 1 ? "door" : "doors";
                string text = $"<color=#{color}>Room {room.RoomID}</color>\n{room.Cells.Count} hex · {room.ConnectedRoomIDs.Count} {doorsLabel}";

                Handles.Label(anchors[room.RoomID], text, labelStyle);
            }
        }

        private static Dictionary<int, Vector3> GetLabelAnchors(HRCGLevelData data)
        {
            Dictionary<int, Vector3> anchors = new Dictionary<int, Vector3>();
            Vector3 lift = Vector3.up * (data.WallHeight * 0.5f);

            foreach (HRCGLevelData.RoomData room in data.Rooms)
                anchors[room.RoomID] = data.GetRoomAnchorLocalPosition(room) + lift;

            return anchors;
        }
    }
}
