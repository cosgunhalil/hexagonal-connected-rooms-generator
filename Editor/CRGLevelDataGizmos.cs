using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CRG.Core;
using CRG.Runtime;

namespace CRG.Editor
{
    // Scene-view overlays for generated levels: room outlines and labels, door markers,
    // and the room connection graph routed through each door.
    public static class CRGLevelDataGizmos
    {
        private const float OutlineLift = 0.05f;
        private const float OutlineWidth = 3f;
        private const float DoorMarkerWidth = 8f;
        private const float ConnectionWidth = 2f;
        private const float GoldenRatioConjugate = 0.618034f;

        private static readonly Color DoorColor = new Color(1f, 0.85f, 0.1f);
        private static readonly Color StartColor = new Color(0.2f, 1f, 0.35f);
        private static readonly Color EndColor = new Color(1f, 0.3f, 0.3f);
        private static GUIStyle labelStyle;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawLevelOverlays(CRGLevelData data, GizmoType gizmoType)
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

            if (data.showRoomRoles)
                DrawRoomRoles(data);

            if (data.showRoomLabels)
                DrawRoomLabels(data);

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
        }

        public static Color GetRoomColor(int roomID)
        {
            return Color.HSVToRGB((roomID * GoldenRatioConjugate) % 1f, 0.65f, 1f);
        }

        private static void DrawRoomOutlines(CRGLevelData data)
        {
            Vector3[] corners = HexMath.GetHexVertices(data.HexSize * 0.92f);
            Vector3[] outline = new Vector3[7];
            Vector3 lift = Vector3.up * OutlineLift;

            foreach (CRGLevelData.RoomData room in data.Rooms)
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

        private static void DrawDoors(CRGLevelData data)
        {
            Handles.color = DoorColor;
            Vector3 markerHeight = Vector3.up * (data.DoorHeight * 0.5f);

            foreach (CRGLevelData.DoorData door in data.Doors)
            {
                (Vector3 start, Vector3 end) = data.GetDoorOpeningLocal(door);
                Handles.DrawAAPolyLine(DoorMarkerWidth, start + markerHeight, end + markerHeight);
                Handles.DrawAAPolyLine(OutlineWidth, start, start + markerHeight * 2f);
                Handles.DrawAAPolyLine(OutlineWidth, end, end + markerHeight * 2f);
            }
        }

        private static void DrawConnections(CRGLevelData data)
        {
            Dictionary<int, Vector3> anchors = GetLabelAnchors(data);
            Vector3 lift = Vector3.up * (data.WallHeight * 0.5f);

            foreach (CRGLevelData.DoorData door in data.Doors)
            {
                (Vector3 start, Vector3 end) = data.GetDoorOpeningLocal(door);
                Vector3 doorPoint = (start + end) * 0.5f + lift;

                Handles.color = GetRoomColor(door.RoomA);
                Handles.DrawAAPolyLine(ConnectionWidth, anchors[door.RoomA], doorPoint);

                Handles.color = GetRoomColor(door.RoomB);
                Handles.DrawAAPolyLine(ConnectionWidth, doorPoint, anchors[door.RoomB]);
            }
        }

        private static void DrawRoomLabels(CRGLevelData data)
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

            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                string color = ColorUtility.ToHtmlStringRGB(GetRoomColor(room.RoomID));
                string doorsLabel = room.ConnectedRoomIDs.Count == 1 ? "door" : "doors";
                string text = $"<color=#{color}>Room {room.RoomID}</color>\n{room.Cells.Count} hex · {room.ConnectedRoomIDs.Count} {doorsLabel}";

                if (room.HasCeiling)
                    text += " · ceiling";

                if (data.showRoomRoles)
                    text += GetRoleLine(room);

                Handles.Label(anchors[room.RoomID], text, labelStyle);
            }
        }

        // Rings around the start (green) and end (red) rooms' anchor cells.
        private static void DrawRoomRoles(CRGLevelData data)
        {
            float radius = HexMath.GetInnerRadius(data.HexSize) * 0.6f;
            Vector3 lift = Vector3.up * OutlineLift;

            foreach (CRGLevelData.RoomData room in data.Rooms)
            {
                if (room.Role == CRGLevelData.RoomRole.Normal)
                    continue;

                Handles.color = room.Role == CRGLevelData.RoomRole.Start ? StartColor : EndColor;
                Vector3 center = data.GetRoomAnchorLocalPosition(room) + lift;
                Handles.DrawWireDisc(center, Vector3.up, radius, DoorMarkerWidth);
                Handles.DrawWireDisc(center, Vector3.up, radius * 0.8f, OutlineWidth);
            }
        }

        private static string GetRoleLine(CRGLevelData.RoomData room)
        {
            switch (room.Role)
            {
                case CRGLevelData.RoomRole.Start:
                    return $"\n<color=#{ColorUtility.ToHtmlStringRGB(StartColor)}>START</color>";
                case CRGLevelData.RoomRole.End:
                    return $"\n<color=#{ColorUtility.ToHtmlStringRGB(EndColor)}>END</color> · {room.DistanceFromStart} doors from start";
                default:
                    return $"\n{room.DistanceFromStart} doors from start{(room.IsDeadEnd ? " · dead end" : "")}";
            }
        }

        private static Dictionary<int, Vector3> GetLabelAnchors(CRGLevelData data)
        {
            Dictionary<int, Vector3> anchors = new Dictionary<int, Vector3>();
            Vector3 lift = Vector3.up * (data.WallHeight * 0.5f);

            foreach (CRGLevelData.RoomData room in data.Rooms)
                anchors[room.RoomID] = data.GetRoomAnchorLocalPosition(room) + lift;

            return anchors;
        }
    }
}
