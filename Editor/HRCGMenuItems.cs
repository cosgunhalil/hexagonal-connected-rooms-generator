using UnityEditor;
using UnityEngine;
using HRCG.Generation;
using HRCG.Geometry;

namespace HRCG.Editor
{
    public static class HRCGMenuItems
    {
        [MenuItem("GameObject/HRCG/Create Level Generator", false, 10)]
        public static void CreateLevelGenerator(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Level Generator");
            var component = go.AddComponent<HRCG.Runtime.HRCGRuntimeComponent>();
            component.parameters = GenerationParameters.CreateDefault();

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Level Generator");
            Selection.activeObject = go;
        }

        [MenuItem("Assets/Create/HRCG/Generation Parameters")]
        public static void CreateGenerationParameters()
        {
            GenerationParametersAsset asset = ScriptableObject.CreateInstance<GenerationParametersAsset>();
            asset.parameters = GenerationParameters.CreateDefault();

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets";
            }
            else if (System.IO.Path.GetExtension(path) != "")
            {
                path = path.Replace(System.IO.Path.GetFileName(path), "");
            }

            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/GenerationParameters.asset");

            AssetDatabase.CreateAsset(asset, assetPathAndName);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

        [MenuItem("HRCG/Generate Quick Level (Small)")]
        public static void GenerateQuickLevelSmall()
        {
            GenerationParameters parameters = new GenerationParameters
            {
                HexSize = 10f,
                MinHexagonsPerRoom = 1,
                MaxHexagonsPerRoom = 3,
                TargetRoomCount = 5,
                RandomSeed = Random.Range(0, 999999)
            };

            GenerateQuickLevel(parameters);
        }

        [MenuItem("HRCG/Generate Quick Level (Medium)")]
        public static void GenerateQuickLevelMedium()
        {
            GenerationParameters parameters = new GenerationParameters
            {
                HexSize = 10f,
                MinHexagonsPerRoom = 2,
                MaxHexagonsPerRoom = 5,
                TargetRoomCount = 10,
                RandomSeed = Random.Range(0, 999999)
            };

            GenerateQuickLevel(parameters);
        }

        [MenuItem("HRCG/Generate Quick Level (Large)")]
        public static void GenerateQuickLevelLarge()
        {
            GenerationParameters parameters = new GenerationParameters
            {
                HexSize = 10f,
                MinHexagonsPerRoom = 3,
                MaxHexagonsPerRoom = 7,
                TargetRoomCount = 20,
                RandomSeed = Random.Range(0, 999999)
            };

            GenerateQuickLevel(parameters);
        }

        private static void GenerateQuickLevel(GenerationParameters parameters)
        {
            GameObject level = LevelGeometryGenerator.GenerateComplete(parameters, null, null);
            if (level == null)
                return;

            Undo.RegisterCreatedObjectUndo(level, "Generate HRCG Level");
            Selection.activeGameObject = level;
            EditorGUIUtility.PingObject(level);
        }

        [MenuItem("HRCG/Clear All Generated Levels")]
        public static void ClearAllGeneratedLevels()
        {
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int count = 0;

            foreach (GameObject obj in rootObjects)
            {
                if (obj.name.Contains("Generated Level") || obj.name.Contains("Level Generator"))
                {
                    Undo.DestroyObjectImmediate(obj);
                    count++;
                }
            }

            Debug.Log($"Cleared {count} generated level objects");
        }

        [MenuItem("HRCG/Documentation/Open Design Document")]
        public static void OpenDesignDocument()
        {
            string[] guids = AssetDatabase.FindAssets("HRCG_Design_Document");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Application.OpenURL("file://" + System.IO.Path.GetFullPath(path));
            }
            else
            {
                Debug.LogWarning("Design document not found in project");
            }
        }

        [MenuItem("HRCG/Documentation/GitHub Repository")]
        public static void OpenGitHub()
        {
            Application.OpenURL("https://github.com/yourusername/hrcg");
        }

        [MenuItem("HRCG/About")]
        public static void ShowAbout()
        {
            EditorUtility.DisplayDialog(
                "Hexagonal Connected Rooms Generator",
                "Version 1.0.0\n\n" +
                "A procedural level generation tool for Unity.\n\n" +
                "Creates interconnected hexagonal rooms with automatic door placement.\n\n" +
                "Developed with Phase 1-4 implementation complete.",
                "OK"
            );
        }
    }
}
