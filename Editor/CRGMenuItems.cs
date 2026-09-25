using UnityEditor;
using UnityEngine;
using CRG.Generation;
using CRG.Geometry;

namespace CRG.Editor
{
    public static class CRGMenuItems
    {
        [MenuItem("GameObject/CRG/Create Level Generator", false, 10)]
        public static void CreateLevelGenerator(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("Level Generator");
            var component = go.AddComponent<CRG.Runtime.CRGRuntimeComponent>();
            component.parameters = GenerationParameters.CreateDefault();

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Level Generator");
            Selection.activeObject = go;
        }

        [MenuItem("Assets/Create/CRG/Generation Parameters")]
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

        [MenuItem("CRG/Generate Quick Level (Small)")]
        public static void GenerateQuickLevelSmall()
        {
            GenerationParameters parameters = new GenerationParameters
            {
                CellSize = 10f,
                MinCellsPerRoom = 1,
                MaxCellsPerRoom = 3,
                TargetRoomCount = 5,
                RandomSeed = Random.Range(0, 999999)
            };

            GenerateQuickLevel(parameters);
        }

        [MenuItem("CRG/Generate Quick Level (Medium)")]
        public static void GenerateQuickLevelMedium()
        {
            GenerationParameters parameters = new GenerationParameters
            {
                CellSize = 10f,
                MinCellsPerRoom = 2,
                MaxCellsPerRoom = 5,
                TargetRoomCount = 10,
                RandomSeed = Random.Range(0, 999999)
            };

            GenerateQuickLevel(parameters);
        }

        [MenuItem("CRG/Generate Quick Level (Large)")]
        public static void GenerateQuickLevelLarge()
        {
            GenerationParameters parameters = new GenerationParameters
            {
                CellSize = 10f,
                MinCellsPerRoom = 3,
                MaxCellsPerRoom = 7,
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

            Undo.RegisterCreatedObjectUndo(level, "Generate CRG Level");
            Selection.activeGameObject = level;
            EditorGUIUtility.PingObject(level);
        }

        [MenuItem("CRG/Clear All Generated Levels")]
        public static void ClearAllGeneratedLevels()
        {
            int count = 0;

            // Levels owned by a generator component are its children; clear them but keep the generator.
#if UNITY_2023_1_OR_NEWER
            CRG.Runtime.CRGRuntimeComponent[] generators = Object.FindObjectsByType<CRG.Runtime.CRGRuntimeComponent>(FindObjectsSortMode.None);
#else
            CRG.Runtime.CRGRuntimeComponent[] generators = Object.FindObjectsOfType<CRG.Runtime.CRGRuntimeComponent>();
#endif
            foreach (CRG.Runtime.CRGRuntimeComponent generator in generators)
            {
                if (generator.generatedLevel != null)
                {
                    Undo.DestroyObjectImmediate(generator.generatedLevel);
                    count++;
                }
            }

            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject obj in rootObjects)
            {
                if (obj.name.StartsWith("Generated Level"))
                {
                    Undo.DestroyObjectImmediate(obj);
                    count++;
                }
            }

            Debug.Log($"Cleared {count} generated level objects");
        }

        [MenuItem("CRG/Documentation/Open Design Document")]
        public static void OpenDesignDocument()
        {
            string[] guids = AssetDatabase.FindAssets("CRG_Design_Document");
            if (guids.Length == 0)
                guids = AssetDatabase.FindAssets("HRCG_Design_Document");
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

        [MenuItem("CRG/Documentation/GitHub Repository")]
        public static void OpenGitHub()
        {
            PackageManifest manifest = LoadPackageManifest();
            if (manifest?.author == null || string.IsNullOrEmpty(manifest.author.url))
            {
                Debug.LogWarning("Repository URL not found in package.json");
                return;
            }

            Application.OpenURL(manifest.author.url);
        }

        [MenuItem("CRG/About")]
        public static void ShowAbout()
        {
            PackageManifest manifest = LoadPackageManifest();
            string title = string.IsNullOrEmpty(manifest?.displayName) ? "Connected Rooms Generator" : manifest.displayName;
            string version = string.IsNullOrEmpty(manifest?.version) ? "unknown" : manifest.version;

            EditorUtility.DisplayDialog(
                title,
                $"Version {version}\n\n{manifest?.description}",
                "OK"
            );
        }

        // Filled in by JsonUtility.
#pragma warning disable 0649
        [System.Serializable]
        private class PackageManifest
        {
            public string displayName;
            public string version;
            public string description;
            public PackageAuthor author;
        }

        [System.Serializable]
        private class PackageAuthor
        {
            public string url;
        }
#pragma warning restore 0649

        // package.json sits one folder above this script's folder, whether the tool is installed
        // as a UPM package or embedded under Assets.
        private static PackageManifest LoadPackageManifest()
        {
            string[] guids = AssetDatabase.FindAssets($"{nameof(CRGMenuItems)} t:MonoScript");
            foreach (string guid in guids)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(scriptPath) != nameof(CRGMenuItems))
                    continue;

                string packageRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(scriptPath));
                string manifestPath = System.IO.Path.Combine(packageRoot, "package.json");
                if (System.IO.File.Exists(manifestPath))
                {
                    return JsonUtility.FromJson<PackageManifest>(System.IO.File.ReadAllText(manifestPath));
                }
            }

            Debug.LogWarning("CRG package.json not found");
            return null;
        }
    }
}
