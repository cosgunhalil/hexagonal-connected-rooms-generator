using UnityEditor;
using UnityEngine;
using HRCG.Runtime;

namespace HRCG.Editor
{
    [CustomEditor(typeof(HRCGLevelData))]
    public class HRCGLevelDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            HRCGLevelData data = (HRCGLevelData)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Level Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Rooms", data.Rooms.Count.ToString());
            EditorGUILayout.LabelField("Doors", data.Doors.Count.ToString());
            EditorGUILayout.LabelField("Hex Size", data.HexSize.ToString("F2"));

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
