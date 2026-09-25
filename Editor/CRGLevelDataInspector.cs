using UnityEditor;
using UnityEngine;
using CRG.Runtime;

namespace CRG.Editor
{
    [CustomEditor(typeof(CRGLevelData))]
    public class CRGLevelDataInspector : UnityEditor.Editor
    {
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
            EditorGUILayout.LabelField("Hex Size", data.HexSize.ToString("F2"));

            CRGLevelData.RoomData startRoom = data.StartRoom;
            CRGLevelData.RoomData endRoom = data.EndRoom;
            EditorGUILayout.LabelField("Start Room", startRoom != null ? $"Room {startRoom.RoomID}" : "-");
            EditorGUILayout.LabelField("End Room", endRoom != null ? $"Room {endRoom.RoomID} ({endRoom.DistanceFromStart} doors from start)" : "-");

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
