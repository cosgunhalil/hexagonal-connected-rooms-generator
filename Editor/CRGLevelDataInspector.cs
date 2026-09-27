using UnityEditor;
using UnityEngine;
using CRG.Runtime;

namespace CRG.Editor
{
    [CustomEditor(typeof(CRGLevelData))]
    public class CRGLevelDataInspector : UnityEditor.Editor
    {
        private static bool showSettings;

        // The settings the level was made with, read-only.
        private void DrawGenerationSettings(CRGLevelData data)
        {
            showSettings = EditorGUILayout.Foldout(showSettings, "Generation Settings", true);
            if (!showSettings)
                return;

            if (!data.HasGenerationSettings)
            {
                EditorGUILayout.HelpBox("This level was made before settings were saved with levels.", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.indentLevel++;
                SerializedProperty settings = serializedObject.FindProperty("generationSettings");
                SerializedProperty property = settings.Copy();
                SerializedProperty end = settings.GetEndProperty();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, end))
                {
                    enterChildren = false;
                    EditorGUILayout.PropertyField(property, true);
                }

                EditorGUILayout.ObjectField("Floor Material", data.FloorMaterial, typeof(Material), false);
                EditorGUILayout.ObjectField("Wall Material", data.WallMaterial, typeof(Material), false);
                EditorGUILayout.ObjectField("Ceiling Material", data.CeilingMaterial, typeof(Material), false);
                EditorGUILayout.ObjectField("Door Prefab", data.DoorPrefab, typeof(GameObject), false);
                EditorGUI.indentLevel--;
            }
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            CRGLevelData data = (CRGLevelData)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Level Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Rooms", data.Rooms.Count.ToString());
            EditorGUILayout.LabelField("Doors", data.Doors.Count.ToString());
            if (data.IsHandBuilt)
            {
                string shapes = string.Join(", ", System.Linq.Enumerable.Select(
                    System.Linq.Enumerable.GroupBy(System.Linq.Enumerable.SelectMany(data.Rooms, room => room.PlacedCells), cell => cell.GridType),
                    group => $"{System.Linq.Enumerable.Count(group)} {group.Key}"));
                EditorGUILayout.LabelField("Grid Type", $"Hand-built ({shapes})");
            }
            else if (data.IsMixed)
            {
                string mix = string.Join(", ", System.Linq.Enumerable.Select(
                    System.Linq.Enumerable.GroupBy(data.Rooms, room => room.GridType),
                    group => $"{System.Linq.Enumerable.Count(group)} {group.Key}"));
                EditorGUILayout.LabelField("Grid Type", $"Mixed ({mix})");
            }
            else
            {
                EditorGUILayout.LabelField("Grid Type", data.GridType.ToString());
            }
            EditorGUILayout.LabelField("Cell Size", data.CellSize.ToString("F2"));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Seed", data.Seed >= 0 ? data.Seed.ToString() : data.IsHandBuilt ? "- (hand-built)" : "- (not saved)");
            using (new EditorGUI.DisabledScope(data.Seed < 0))
            {
                if (GUILayout.Button(new GUIContent("Copy", "Copy the seed to the clipboard"), EditorStyles.miniButton, GUILayout.Width(50)))
                    EditorGUIUtility.systemCopyBuffer = data.Seed.ToString();
            }
            EditorGUILayout.EndHorizontal();

            CRGLevelData.RoomData startRoom = data.StartRoom;
            CRGLevelData.RoomData endRoom = data.EndRoom;
            EditorGUILayout.LabelField("Start Room", startRoom != null ? $"Room {startRoom.RoomID}" : "-");
            EditorGUILayout.LabelField("End Room", endRoom != null ? $"Room {endRoom.RoomID} ({endRoom.DistanceFromStart} doors from start)" : "-");

            DrawGenerationSettings(data);

            if (data.Rooms.Count > 0 && GUILayout.Button("Frame Level In Scene View"))
            {
                Renderer[] renderers = data.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0 && SceneView.lastActiveSceneView != null)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                        bounds.Encapsulate(renderer.bounds);

                    SceneView.lastActiveSceneView.Frame(bounds, false);
                }
            }
        }
    }
}
