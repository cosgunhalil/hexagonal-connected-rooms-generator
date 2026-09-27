using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using CRG.Building;
using CRG.Core;
using CRG.Runtime;

namespace CRG.Editor
{
    // Scene-view tool for building a level cell by cell. With a CRGLevelBuilder selected:
    // - the first click places the first cell at the builder's origin;
    // - clicking a cell selects it; hovering an outer edge of the selected cell previews the chosen shape there
    //   (green when it fits, red when not) and clicking attaches it with a door on that edge;
    // - clicking a shared edge switches it between door, wall and open;
    // - keys 1-4 pick the shape, Delete removes the selected cell, Escape clears the selection.
    [EditorTool("CRG Build", typeof(CRGLevelBuilder))]
    public class CRGLevelBuilderTool : EditorTool
    {
        private const float EdgePickDistance = 0.2f;
        private const float PanelWidth = 290f;
        private const float PanelHeight = 250f;
        private const string ShapePrefKey = "CRG.LevelBuilder.Shape";

        private static readonly Color FitsColor = new Color(0.3f, 1f, 0.45f);
        private static readonly Color BlockedColor = new Color(1f, 0.3f, 0.25f);
        private static readonly Color SelectedColor = new Color(0.25f, 0.8f, 1f);
        private static readonly string[] ShapeLabels = { "1 Triangle", "2 Square", "3 Hexagon", "4 Octagon" };

        private enum HoverKind
        {
            None,
            FirstCell,
            Attach,
            SharedEdge,
            Cell
        }

        // The selected cell is shared with the builder's Inspector, which edits the selected cell's room.
        private static int selection = -1;
        private static CRGLevelBuilder selectionOwner;

        private static int SelectedCell
        {
            get => selection;
            set
            {
                if (selection == value)
                    return;
                selection = value;
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        // The cell selected with the CRG Build tool in this builder, or -1.
        public static int GetSelectedCell(CRGLevelBuilder builder)
        {
            return builder != null && builder == selectionOwner && ToolManager.activeToolType == typeof(CRGLevelBuilderTool) &&
                   builder.Layout.GetCell(selection) != null
                ? selection
                : -1;
        }
        private HoverKind hoverKind;
        private int hoverCell = -1;
        private int hoverEdge = -1;
        private AttachPlan hoverPlan;
        private string message;
        private GUIContent icon;

        private static CellShape Shape
        {
            get => (CellShape)EditorPrefs.GetInt(ShapePrefKey, (int)CellShape.Hexagon);
            set => EditorPrefs.SetInt(ShapePrefKey, (int)value);
        }

        public override GUIContent toolbarIcon =>
            icon ?? (icon = new GUIContent("CRG", "CRG Build: place cells one by one to build a level"));

        public override void OnActivated()
        {
            SelectedCell = -1;
            message = null;
        }

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView sceneView) || !(target is CRGLevelBuilder builder))
                return;

            LevelLayout layout = builder.Layout;
            if (selectionOwner != builder)
            {
                selectionOwner = builder;
                selection = -1;
            }
            if (layout.GetCell(SelectedCell) == null)
                SelectedCell = -1;

            Event current = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(controlID);

            Rect panel = GetPanelRect(sceneView);
            bool overPanel = panel.Contains(current.mousePosition);

            HandleKeys(builder, current);
            UpdateHover(builder, current, overPanel);

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt && !overPanel)
            {
                Click(builder);
                current.Use();
            }

            if (current.type == EventType.MouseLeaveWindow)
            {
                hoverKind = HoverKind.None;
                hoverPlan = null;
            }

            if (current.type == EventType.MouseMove || current.type == EventType.MouseLeaveWindow)
                sceneView.Repaint();

            if (current.type == EventType.Repaint)
            {
                LayoutRooms rooms = layout.GetRooms();
                CRGLevelBuilderDrawing.DrawLayout(builder, rooms);
                DrawSelectionAndHover(builder);
            }

            DrawPanel(builder, panel);
        }

        private void HandleKeys(CRGLevelBuilder builder, Event current)
        {
            // Claim Delete so it removes the selected cell instead of the builder GameObject.
            if ((current.type == EventType.ValidateCommand || current.type == EventType.ExecuteCommand) &&
                (current.commandName == "SoftDelete" || current.commandName == "Delete") && SelectedCell >= 0)
            {
                if (current.type == EventType.ExecuteCommand)
                    RemoveSelected(builder);
                current.Use();
                return;
            }

            if (current.type != EventType.KeyDown)
                return;

            switch (current.keyCode)
            {
                case KeyCode.Alpha1: case KeyCode.Keypad1: SetShape(CellShape.Triangle, current); break;
                case KeyCode.Alpha2: case KeyCode.Keypad2: SetShape(CellShape.Square, current); break;
                case KeyCode.Alpha3: case KeyCode.Keypad3: SetShape(CellShape.Hexagon, current); break;
                case KeyCode.Alpha4: case KeyCode.Keypad4: SetShape(CellShape.Octagon, current); break;
                case KeyCode.Escape:
                    if (SelectedCell >= 0)
                    {
                        SelectedCell = -1;
                        current.Use();
                    }
                    break;
            }
        }

        private void SetShape(CellShape shape, Event current)
        {
            Shape = shape;
            current.Use();
        }

        private void UpdateHover(CRGLevelBuilder builder, Event current, bool overPanel)
        {
            if (current.type != EventType.MouseMove && current.type != EventType.MouseDown && current.type != EventType.Layout)
                return;

            LevelLayout layout = builder.Layout;
            hoverKind = HoverKind.None;
            hoverCell = hoverEdge = -1;
            hoverPlan = null;

            if (overPanel)
                return;

            if (layout.IsEmpty)
            {
                hoverKind = HoverKind.FirstCell;
                return;
            }

            if (!TryGetLayoutPoint(builder, current.mousePosition, out Vector3 point))
                return;

            if (SelectedCell >= 0 && layout.FindNearestEdge(point, EdgePickDistance,
                    (cell, edge) => cell == SelectedCell && layout.IsOuterEdge(cell, edge), out hoverCell, out hoverEdge))
            {
                hoverKind = HoverKind.Attach;
                hoverPlan = layout.PlanAttach(hoverCell, hoverEdge, Shape);
            }
            else if (layout.FindNearestEdge(point, EdgePickDistance,
                         (cell, edge) => !layout.IsOuterEdge(cell, edge), out hoverCell, out hoverEdge))
            {
                hoverKind = HoverKind.SharedEdge;
            }
            else
            {
                hoverCell = layout.FindCellAt(point);
                hoverKind = hoverCell >= 0 ? HoverKind.Cell : HoverKind.None;
            }
        }

        private void Click(CRGLevelBuilder builder)
        {
            LevelLayout layout = builder.Layout;
            message = null;

            switch (hoverKind)
            {
                case HoverKind.FirstCell:
                    CRGLevelBuilderActions.Edit(builder, "Place First CRG Cell", () => SelectedCell = layout.AddFirstCell(Shape).id);
                    break;

                case HoverKind.Attach:
                    if (hoverPlan == null || !hoverPlan.Fits)
                    {
                        message = hoverPlan?.FailureReason ?? "It doesn't fit there";
                        break;
                    }
                    AttachPlan plan = hoverPlan;
                    CRGLevelBuilderActions.Edit(builder, $"Attach CRG {Shape}", () => SelectedCell = layout.Attach(plan).id);
                    break;

                case HoverKind.SharedEdge:
                    LayoutLink link = layout.GetLink(hoverCell, hoverEdge);
                    EdgeState next = LevelLayout.GetNextState(link.state);
                    CRGLevelBuilderActions.Edit(builder, $"Set CRG Edge to {next}", () => link.state = next);
                    break;

                case HoverKind.Cell:
                    SelectedCell = hoverCell;
                    break;

                default:
                    SelectedCell = -1;
                    break;
            }

            hoverPlan = null;
        }

        private void RemoveSelected(CRGLevelBuilder builder)
        {
            LevelLayout layout = builder.Layout;
            if (!layout.CanRemove(SelectedCell, out string reason))
            {
                message = reason;
                return;
            }

            int removed = SelectedCell;
            CRGLevelBuilderActions.Edit(builder, "Remove CRG Cell", () => layout.Remove(removed));
            SelectedCell = -1;
            message = null;
        }

        private void DrawSelectionAndHover(CRGLevelBuilder builder)
        {
            LevelLayout layout = builder.Layout;
            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.matrix = CRGLevelBuilderDrawing.GetLayoutMatrix(builder);

            LayoutCell selected = layout.GetCell(SelectedCell);
            if (selected != null)
                DrawOutline(layout.GetCorners(selected), SelectedColor, 4f);

            switch (hoverKind)
            {
                case HoverKind.FirstCell:
                    DrawGhost(LevelLayout.GetCorners(Shape, RoomPlacement.Identity), FitsColor);
                    break;

                case HoverKind.Attach when hoverPlan != null:
                    DrawGhost(hoverPlan.Corners, hoverPlan.Fits ? FitsColor : BlockedColor);
                    DrawHoverEdge(layout, hoverPlan.Fits ? FitsColor : BlockedColor);
                    break;

                case HoverKind.SharedEdge:
                    DrawHoverEdge(layout, SelectedColor);
                    break;

                case HoverKind.Cell when hoverCell != SelectedCell:
                    DrawOutline(layout.GetCorners(layout.GetCell(hoverCell)), new Color(SelectedColor.r, SelectedColor.g, SelectedColor.b, 0.5f), 3f);
                    break;
            }

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
        }

        private void DrawHoverEdge(LevelLayout layout, Color color)
        {
            (Vector3 start, Vector3 end) = layout.GetEdge(layout.GetCell(hoverCell), hoverEdge);
            Handles.color = color;
            Handles.DrawAAPolyLine(9f, CRGLevelBuilderDrawing.Lifted(start), CRGLevelBuilderDrawing.Lifted(end));
        }

        private static void DrawGhost(Vector3[] corners, Color color)
        {
            Vector3[] lifted = corners.Select(CRGLevelBuilderDrawing.Lifted).ToArray();
            Handles.color = new Color(color.r, color.g, color.b, 0.3f);
            Handles.DrawAAConvexPolygon(lifted);
            DrawOutline(corners, color, 3f);
        }

        private static void DrawOutline(Vector3[] corners, Color color, float width)
        {
            Vector3[] outline = new Vector3[corners.Length + 1];
            for (int i = 0; i < corners.Length; i++)
                outline[i] = CRGLevelBuilderDrawing.Lifted(corners[i]);
            outline[corners.Length] = outline[0];

            Handles.color = color;
            Handles.DrawAAPolyLine(width, outline);
        }

        private void DrawPanel(CRGLevelBuilder builder, Rect panel)
        {
            LevelLayout layout = builder.Layout;

            Handles.BeginGUI();
            GUILayout.BeginArea(panel, GUI.skin.box);

            GUILayout.Label("CRG Build", EditorStyles.boldLabel);

            int shape = GUILayout.Toolbar((int)Shape, ShapeLabels, EditorStyles.miniButton);
            if (shape != (int)Shape)
                Shape = (CellShape)shape;

            // Every label is drawn in every event (empty when there is nothing to say): a click can change the
            // state between GUILayout's layout and input passes, and a changing control count would break them.
            GUILayout.Label(GetHint(layout), EditorStyles.wordWrappedMiniLabel);

            GUIStyle messageStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = BlockedColor } };
            GUILayout.Label(message ?? string.Empty, messageStyle);

            GUILayout.FlexibleSpace();

            string stats = string.Empty;
            string unreachable = string.Empty;
            if (!layout.IsEmpty)
            {
                LayoutRooms rooms = layout.GetRooms();
                int doors = layout.Links.Count(link => link.state == EdgeState.Door);
                stats = $"{layout.Cells.Count} cells · {rooms.Rooms.Count} rooms · {doors} doors";
                if (rooms.Unreachable.Count > 0)
                    unreachable = $"{rooms.Unreachable.Count} room(s) can't be reached from the start room";
            }

            bool valid = builder.Validate(out string error);

            GUILayout.Label(stats, EditorStyles.miniLabel);
            GUIStyle warningStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = CRGLevelBuilderDrawing.UnreachableColor } };
            GUILayout.Label(unreachable, warningStyle);
            GUILayout.Label(valid ? string.Empty : $"Settings: {error}", warningStyle);

            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(SelectedCell < 0))
            {
                if (GUILayout.Button("Remove Selected (Del)", EditorStyles.miniButton))
                    RemoveSelected(builder);
            }
            using (new EditorGUI.DisabledScope(layout.IsEmpty))
            {
                if (GUILayout.Button("Clear All", EditorStyles.miniButton) &&
                    EditorUtility.DisplayDialog("Clear Layout", "Remove every cell of this level?", "Clear", "Cancel"))
                {
                    CRGLevelBuilderActions.Edit(builder, "Clear CRG Layout", layout.Clear);
                    SelectedCell = -1;
                    message = null;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(layout.IsEmpty || !valid))
            {
                if (GUILayout.Button(new GUIContent("Bake", "Create a level GameObject with one mesh"), EditorStyles.miniButton))
                    CRGLevelBuilderActions.Bake(builder, false);
                if (GUILayout.Button(new GUIContent("Bake Separate Rooms", "Create a level GameObject with one mesh per room"), EditorStyles.miniButton))
                    CRGLevelBuilderActions.Bake(builder, true);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private string GetHint(LevelLayout layout)
        {
            if (layout.IsEmpty)
                return $"Click in the Scene view to place the first {Shape.ToString().ToLowerInvariant()} at the builder's position.";

            if (hoverKind == HoverKind.SharedEdge)
            {
                EdgeState state = layout.GetLink(hoverCell, hoverEdge).state;
                return $"This edge is {Describe(state)}. Click to make it {Describe(LevelLayout.GetNextState(state))}.";
            }

            if (hoverKind == HoverKind.Attach && hoverPlan != null && !hoverPlan.Fits)
                return $"Can't attach here: {hoverPlan.FailureReason}.";

            if (SelectedCell < 0)
                return "Click a cell to select it. Click a shared edge to switch it between door, wall and open.";

            return $"Click an outer edge of the selected cell to attach a {Shape.ToString().ToLowerInvariant()} with a door. " +
                   "Click a shared edge to switch it between door, wall and open. Room settings are in the Inspector.";
        }

        private static string Describe(EdgeState state)
        {
            switch (state)
            {
                case EdgeState.Door: return "a door";
                case EdgeState.Wall: return "a wall";
                default: return "open (the cells form one room)";
            }
        }

        private static Rect GetPanelRect(SceneView sceneView)
        {
            float height = sceneView.camera.pixelRect.height / EditorGUIUtility.pixelsPerPoint;
            return new Rect(10f, height - PanelHeight - 10f, PanelWidth, PanelHeight);
        }

        private static bool TryGetLayoutPoint(CRGLevelBuilder builder, Vector2 guiPosition, out Vector3 point)
        {
            point = default;
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            Plane plane = new Plane(builder.transform.up, builder.transform.position);
            if (!plane.Raycast(ray, out float distance))
                return false;

            Vector3 local = builder.transform.InverseTransformPoint(ray.GetPoint(distance));
            point = builder.LocalToLayout(local);
            return true;
        }
    }
}
