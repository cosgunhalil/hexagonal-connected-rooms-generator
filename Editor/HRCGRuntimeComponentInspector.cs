using UnityEditor;
using UnityEngine;
using HRCG.Runtime;

namespace HRCG.Editor
{
    [CustomEditor(typeof(HRCGRuntimeComponent))]
    public class HRCGRuntimeComponentInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            HRCGRuntimeComponent component = (HRCGRuntimeComponent)target;

            EditorGUILayout.LabelField("Generation Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                component.Generate();
            }

            if (GUILayout.Button("Clear", GUILayout.Height(30)))
            {
                component.ClearGenerated();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Randomize Seed"))
            {
                component.RandomizeSeed();
            }

            if (GUILayout.Button("Generate Random"))
            {
                component.GenerateWithRandomSeed();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (component.generatedLevel != null)
            {
                EditorGUILayout.LabelField("Generated Level Info", EditorStyles.boldLabel);

                MeshFilter[] meshFilters = component.generatedLevel.GetComponentsInChildren<MeshFilter>();
                int totalVertices = 0;
                int totalTriangles = 0;

                foreach (MeshFilter mf in meshFilters)
                {
                    if (mf.sharedMesh != null)
                    {
                        totalVertices += mf.sharedMesh.vertexCount;
                        totalTriangles += mf.sharedMesh.triangles.Length / 3;
                    }
                }

                EditorGUILayout.LabelField($"Mesh Objects: {meshFilters.Length}");
                EditorGUILayout.LabelField($"Total Vertices: {totalVertices}");
                EditorGUILayout.LabelField($"Total Triangles: {totalTriangles}");
            }

            if (component.parameters != null)
            {
                EditorGUILayout.Space();

                if (!component.parameters.Validate(out string errorMessage))
                {
                    EditorGUILayout.HelpBox($"Invalid parameters: {errorMessage}", MessageType.Error);
                }

                HRCGEditorGUI.NavMeshWarnings(component.parameters);
            }
        }
    }
}
