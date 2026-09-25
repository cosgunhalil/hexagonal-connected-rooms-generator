using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using CRG.Generation;
using CRG.Geometry;

namespace CRG.Editor
{
    public static class CRGEditorGUI
    {
        // Dropdown of the agent types defined in Navigation settings; returns the selected agent type ID.
        public static int AgentTypePopup(GUIContent label, int agentTypeID)
        {
            List<int> ids = new List<int>();
            List<GUIContent> names = new List<GUIContent>();

            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                ids.Add(id);
                names.Add(new GUIContent(NavMesh.GetSettingsNameFromID(id)));
            }

            int index = ids.IndexOf(agentTypeID);
            if (index < 0)
            {
                ids.Add(agentTypeID);
                names.Add(new GUIContent($"Unknown ({agentTypeID})"));
                index = ids.Count - 1;
            }

            index = EditorGUILayout.Popup(label, index, names.ToArray());
            return ids[index];
        }

        public static void NavMeshWarnings(GenerationParameters parameters)
        {
            if (!parameters.BakeNavMesh)
                return;

            if (!LevelNavMeshBaker.IsAvailable)
            {
                EditorGUILayout.HelpBox("Install the AI Navigation package (com.unity.ai.navigation) to bake NavMeshes.", MessageType.Warning);
                return;
            }

            if (!LevelNavMeshBaker.DoorsFitAgent(parameters, out string message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Warning);
            }
        }
    }
}
