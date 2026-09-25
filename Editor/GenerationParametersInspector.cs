using UnityEditor;
using UnityEngine;
using CRG.Generation;

namespace CRG.Editor
{
    [CustomEditor(typeof(GenerationParametersAsset))]
    public class GenerationParametersInspector : UnityEditor.Editor
    {
        private SerializedProperty parameters;

        private void OnEnable()
        {
            parameters = serializedObject.FindProperty("parameters");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Generation Parameters", EditorStyles.boldLabel);

            // Draw every field (headers come from the [Header] attributes) so new parameters show up automatically.
            SerializedProperty property = parameters.Copy();
            SerializedProperty end = parameters.GetEndProperty();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, end))
            {
                enterChildren = false;

                if (property.name == nameof(GenerationParameters.NavMeshAgentTypeID))
                {
                    property.intValue = CRGEditorGUI.AgentTypePopup(new GUIContent(property.displayName, property.tooltip), property.intValue);
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);

                if (property.name == nameof(GenerationParameters.RandomSeed) && GUILayout.Button("Randomize Seed"))
                {
                    property.intValue = Random.Range(0, 999999);
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            GenerationParametersAsset asset = (GenerationParametersAsset)target;
            if (asset.parameters == null)
                return;

            if (asset.parameters.Validate(out string errorMessage))
            {
                EditorGUILayout.HelpBox("Parameters are valid", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Invalid: {errorMessage}", MessageType.Error);
            }

            CRGEditorGUI.NavMeshWarnings(asset.parameters);
        }
    }
}
