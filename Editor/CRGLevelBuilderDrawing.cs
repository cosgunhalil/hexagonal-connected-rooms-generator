using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using CRG.Building;
using CRG.Runtime;

namespace CRG.Editor
{
    // Draws a builder's layout in the Scene view: cells filled per room, doors, walls and open edges, and room labels.
    // The CRG Build tool draws it itself while active (with selection and previews on top); otherwise it is a gizmo.
    public static class CRGLevelBuilderDrawing
    {
        private const float FillAlpha = 0.28f;
        private const float OuterEdgeWidth = 2f;
        private const float WallWidth = 5f;
        private const float DoorWidth = 7f;
        private const float Lift = 0.01f;

        public static readonly Color DoorColor = new Color(1f, 0.85f, 0.1f);
        public static readonly Color WallColor = new Color(0.85f, 0.85f, 0.85f);
        public static readonly Color OuterEdgeColor = new Color(1f, 1f, 1f, 0.8f);
        public static readonly Color UnreachableColor = new Color(1f, 0.25f, 0.2f);

        private static GUIStyle labelStyle;

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawBuilderGizmo(CRGLevelBuilder builder, GizmoType gizmoType)
        {
            if (ToolManager.activeToolType == typeof(CRGLevelBuilderTool) && Selection.activeGameObject == builder.gameObject)
                return;

            DrawLayout(builder, builder.Layout.GetRooms());
        }

        // Handles space where layout units are drawn: the builder's transform scaled by the cell size.
        public static Matrix4x4 GetLayoutMatrix(CRGLevelBuilder builder)
        {
            return builder.transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * builder.CellSize);
        }

        public static Vector3 Lifted(Vector3 point)
        {
            return point + Vector3.up * Lift;
        }

        public static void DrawLayout(CRGLevelBuilder builder, LayoutRooms rooms)
        {
            LevelLayout layout = builder.Layout;
            if (layout.IsEmpty)
                return;

            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.matrix = GetLayoutMatrix(builder);

            foreach (LayoutCell cell in layout.Cells)
            {
                int room = rooms.RoomOfCell[cell.id];
                Color color = rooms.Unreachable.Contains(room) ? UnreachableColor : CRGLevelDataGizmos.GetRoomColor(room);
                color.a = FillAlpha;
                Handles.color = color;
                Handles.DrawAAConvexPolygon(layout.GetCorners(cell).Select(Lifted).ToArray());
            }

            float doorRatio = builder.Parameters != null ? builder.Parameters.DoorWidthRatio : 0.2f;

            foreach (LayoutCell cell in layout.Cells)
            {
                int edgeCount = CellShapes.GetEdgeCount(cell.shape);
                for (int edge = 0; edge < edgeCount; edge++)
                {
                    LayoutLink link = layout.GetLink(cell.id, edge);
                    // Shared edges are drawn once, from cell A.
                    if (link != null && link.cellA != cell.id)
                        continue;

                    (Vector3 start, Vector3 end) = layout.GetEdge(cell, edge);
                    DrawEdge(Lifted(start), Lifted(end), link, doorRatio, CRGLevelDataGizmos.GetRoomColor(rooms.RoomOfCell[cell.id]));
                }
            }

            DrawRoomLabels(layout, rooms);

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
        }

        public static void DrawEdge(Vector3 start, Vector3 end, LayoutLink link, float doorRatio, Color roomColor)
        {
            if (link == null)
            {
                Handles.color = OuterEdgeColor;
                Handles.DrawAAPolyLine(OuterEdgeWidth, start, end);
                return;
            }

            switch (link.state)
            {
                case EdgeState.Wall:
                    Handles.color = WallColor;
                    Handles.DrawAAPolyLine(WallWidth, start, end);
                    break;
                case EdgeState.Door:
                    // Wall pieces on both sides of a centered opening as wide as the level's doors.
                    Vector3 middle = (start + end) / 2f;
                    Vector3 half = (end - start) * (Mathf.Clamp01(doorRatio) / 2f);
                    Handles.color = WallColor;
                    Handles.DrawAAPolyLine(WallWidth, start, middle - half);
                    Handles.DrawAAPolyLine(WallWidth, middle + half, end);
                    Handles.color = DoorColor;
                    Handles.DrawAAPolyLine(DoorWidth, middle - half, middle + half);
                    break;
                default:
                    Handles.color = roomColor;
                    Handles.DrawDottedLine(start, end, 4f);
                    break;
            }
        }

        private static void DrawRoomLabels(LevelLayout layout, LayoutRooms rooms)
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 10,
                    richText = true
                };
            }

            for (int room = 0; room < rooms.Rooms.Count; room++)
            {
                Vector3 center = Vector3.zero;
                foreach (int id in rooms.Rooms[room])
                    center += layout.GetCenter(layout.GetCell(id));
                center /= rooms.Rooms[room].Count;

                string text = $"Room {room}";
                if (room == rooms.StartRoom)
                    text += " · start";
                if (rooms.Unreachable.Contains(room))
                    text += $"\n<color=#{ColorUtility.ToHtmlStringRGB(UnreachableColor)}>unreachable</color>";

                Handles.Label(Lifted(center), text, labelStyle);
            }
        }
    }
}
