using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using CRG.Building;
using CRG.Generation;
using CRG.Runtime;

namespace CRG.Editor
{
    [CustomEditor(typeof(CRGLevelBuilder))]
    public class CRGLevelBuilderInspector : UnityEditor.Editor
    {
        // The generation settings a hand-built level uses; the random layout settings don't apply to it.
        private static readonly string[] UsedParameters =
        {
            nameof(GenerationParameters.CellSize),
            nameof(GenerationParameters.WallHeight),
            nameof(GenerationParameters.DoorHeight),
            nameof(GenerationParameters.DoorWidthRatio),
            nameof(GenerationParameters.WallThickness),
            nameof(GenerationParameters.AddCeiling),
            nameof(GenerationParameters.AddMeshCollider),
            nameof(GenerationParameters.BakeNavMesh),
            nameof(GenerationParameters.NavMeshAgentTypeID),
            nameof(GenerationParameters.NavMeshGeometry),
            nameof(GenerationParameters.CreateSpawnPoints)
        };

        private static readonly string[] OutputProperties =
        {
            nameof(CRGLevelBuilder.floorMaterial),
            nameof(CRGLevelBuilder.wallMaterial),
            nameof(CRGLevelBuilder.ceilingMaterial),
            nameof(CRGLevelBuilder.doorPrefab),
            nameof(CRGLevelBuilder.showPreview)
        };

        private SerializedProperty parametersAsset;
        private SerializedProperty parameters;

        private void OnEnable()
        {
            parametersAsset = serializedObject.FindProperty(nameof(CRGLevelBuilder.parametersAsset));
            parameters = serializedObject.FindProperty(nameof(CRGLevelBuilder.parameters));
        }

        public override void OnInspectorGUI()
        {
            CRGLevelBuilder builder = (CRGLevelBuilder)target;

            DrawToolButton();
            DrawLayoutSummary(builder);
            DrawBake(builder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(parametersAsset);

            if (parametersAsset.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox("Using the settings of the parameters asset. Clear the field to use the settings of this builder.", MessageType.Info);
            }
            else
            {
                foreach (string name in UsedParameters)
                {
                    SerializedProperty property = parameters.FindPropertyRelative(name);
                    if (property == null)
                        continue;

                    if (name == nameof(GenerationParameters.NavMeshAgentTypeID))
                        property.intValue = CRGEditorGUI.AgentTypePopup(new GUIContent(property.displayName, property.tooltip), property.intValue);
                    else
                        EditorGUILayout.PropertyField(property, true);
                }
            }

            EditorGUILayout.Space();
            foreach (string name in OutputProperties)
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name));

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (changed)
            {
                builder.RequestPreviewRebuild();
                SceneView.RepaintAll();
            }

            if (builder.parametersAsset != null && GUILayout.Button("Refresh Preview", EditorStyles.miniButton))
                builder.RebuildPreview();
        }

        private static void DrawBake(CRGLevelBuilder builder)
        {
            bool valid = builder.Validate(out string error);
            if (!valid)
                EditorGUILayout.HelpBox($"Invalid settings: {error}", MessageType.Error);

            if (builder.Parameters != null)
            {
                CRGEditorGUI.NavMeshWarnings(builder.Parameters);

                if (builder.Parameters.BakeNavMesh && builder.transform.lossyScale != Vector3.one)
                    EditorGUILayout.HelpBox("The NavMesh doesn't follow the builder's scale; change Cell Size instead of scaling the builder.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(builder.Layout.IsEmpty || !valid))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Bake Level", "Create a level GameObject with one mesh, level data and the gameplay extras"), GUILayout.Height(26)))
                    CRGLevelBuilderActions.Bake(builder, false);
                if (GUILayout.Button(new GUIContent("Bake Separate Rooms", "Create a level GameObject with one mesh per room"), GUILayout.Height(26)))
                    CRGLevelBuilderActions.Bake(builder, true);
                EditorGUILayout.EndHorizontal();
            }

            if (!builder.showPreview && !builder.Layout.IsEmpty)
                EditorGUILayout.HelpBox("The geometry preview is hidden (it is hidden after baking so it doesn't overlap the baked level). Edit the layout or turn on Show Preview to see it again.", MessageType.None);
        }

        private static void DrawToolButton()
        {
            bool active = ToolManager.activeToolType == typeof(CRGLevelBuilderTool);

            EditorGUILayout.Space();
            Color previous = GUI.backgroundColor;
            if (active)
                GUI.backgroundColor = new Color(0.45f, 0.85f, 1f);

            if (GUILayout.Button(active ? "Stop Editing Layout" : "Edit Layout (CRG Build tool)", GUILayout.Height(30)))
            {
                if (active)
                    ToolManager.RestorePreviousTool();
                else
                    ToolManager.SetActiveTool<CRGLevelBuilderTool>();
            }

            GUI.backgroundColor = previous;
        }

        private static void DrawLayoutSummary(CRGLevelBuilder builder)
        {
            LevelLayout layout = builder.Layout;

            if (layout.IsEmpty)
            {
                EditorGUILayout.HelpBox("The layout is empty. Start the CRG Build tool and click in the Scene view to place the first cell.", MessageType.None);
                return;
            }

            LayoutRooms rooms = layout.GetRooms();
            int doors = layout.Links.Count(link => link.state == EdgeState.Door);
            int walls = layout.Links.Count(link => link.state == EdgeState.Wall);
            string shapes = string.Join(", ", layout.Cells.GroupBy(cell => cell.shape).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));

            EditorGUILayout.HelpBox($"{layout.Cells.Count} cells ({shapes})\n{rooms.Rooms.Count} rooms, {doors} doors, {walls} walls between rooms", MessageType.None);

            if (rooms.Unreachable.Count > 0)
            {
                EditorGUILayout.HelpBox($"Rooms {string.Join(", ", rooms.Unreachable)} can't be reached from the start room (room 0) through doors.", MessageType.Warning);
            }
        }
    }
}
