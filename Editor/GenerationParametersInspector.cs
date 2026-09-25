using UnityEditor;
using UnityEngine;
using HRCG.Generation;

namespace HRCG.Editor
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
            EditorGUILayout.Space();

            SerializedProperty hexSize = parameters.FindPropertyRelative("HexSize");
            SerializedProperty wallHeight = parameters.FindPropertyRelative("WallHeight");
            SerializedProperty doorHeight = parameters.FindPropertyRelative("DoorHeight");
            SerializedProperty doorWidthRatio = parameters.FindPropertyRelative("DoorWidthRatio");
            SerializedProperty reserveSlot = parameters.FindPropertyRelative("ReserveConnectionForGrowth");
            SerializedProperty minHex = parameters.FindPropertyRelative("MinHexagonsPerRoom");
            SerializedProperty maxHex = parameters.FindPropertyRelative("MaxHexagonsPerRoom");
            SerializedProperty targetRooms = parameters.FindPropertyRelative("TargetRoomCount");
            SerializedProperty startPos = parameters.FindPropertyRelative("StartPosition");
            SerializedProperty minConn = parameters.FindPropertyRelative("MinConnectionsPerRoom");
            SerializedProperty maxConn = parameters.FindPropertyRelative("MaxConnectionsPerRoom");
            SerializedProperty seed = parameters.FindPropertyRelative("RandomSeed");
            SerializedProperty maxIter = parameters.FindPropertyRelative("MaxIterations");
            SerializedProperty maxRetry = parameters.FindPropertyRelative("MaxRetriesPerRoom");

            EditorGUILayout.LabelField("Hexagon Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hexSize);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Walls", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(wallHeight);
            EditorGUILayout.PropertyField(doorHeight);
            EditorGUILayout.PropertyField(doorWidthRatio);

            if (doorHeight.floatValue > wallHeight.floatValue)
            {
                EditorGUILayout.HelpBox("Door Height must be <= Wall Height", MessageType.Warning);
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Room Size", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(minHex);
            EditorGUILayout.PropertyField(maxHex);
            
            if (maxHex.intValue < minHex.intValue)
            {
                EditorGUILayout.HelpBox("Max must be >= Min", MessageType.Warning);
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetRooms);
            EditorGUILayout.PropertyField(startPos);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(minConn);
            EditorGUILayout.PropertyField(maxConn);
            EditorGUILayout.PropertyField(reserveSlot);
            
            if (maxConn.intValue < minConn.intValue)
            {
                EditorGUILayout.HelpBox("Max must be >= Min", MessageType.Warning);
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(seed);
            if (GUILayout.Button("Randomize Seed"))
            {
                seed.intValue = Random.Range(0, 999999);
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Safety Limits", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(maxIter);
            EditorGUILayout.PropertyField(maxRetry);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            var asset = (GenerationParametersAsset)target;
            var errorMessage = string.Empty;
            if (asset.parameters != null && asset.parameters.Validate(out errorMessage))
            {
                EditorGUILayout.HelpBox("Parameters are valid", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Invalid: {errorMessage}", MessageType.Error);
            }
        }
    }
}
