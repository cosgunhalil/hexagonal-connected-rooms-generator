using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
#if HRCG_AI_NAVIGATION
using Unity.AI.Navigation;
using HRCG.Geometry;
#endif

namespace HRCG.Editor
{
    // Edit Mode NavMesh bakes only live in memory; this saves them as assets next to the scene
    // (<SceneFolder>/<SceneName>/HRCG-NavMesh-*.asset) so they survive scene reloads.
    public static class HRCGNavMeshAssets
    {
        private const string AssetPrefix = "HRCG-NavMesh-";
        private const string UnsavedSceneFolder = "Assets/HRCG NavMesh";

#if HRCG_AI_NAVIGATION
        [InitializeOnLoadMethod]
        private static void Register()
        {
            LevelNavMeshBaker.NavMeshBaked -= SaveBakedNavMesh;
            LevelNavMeshBaker.NavMeshBaked += SaveBakedNavMesh;
        }

        private static void SaveBakedNavMesh(NavMeshSurface surface)
        {
            if (Application.isPlaying || !LevelNavMeshBaker.PersistEditorBakes ||
                surface.navMeshData == null || AssetDatabase.Contains(surface.navMeshData))
                return;

            string folder = GetTargetFolder(surface.gameObject.scene);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{AssetPrefix}{Sanitize(surface.gameObject.name)}.asset");
            AssetDatabase.CreateAsset(surface.navMeshData, path);
        }
#endif

        [MenuItem("HRCG/Delete Unused NavMesh Assets")]
        public static void DeleteUnusedNavMeshAssets()
        {
            HashSet<NavMeshData> used = CollectUsedNavMeshData();
            List<string> unused = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets($"{AssetPrefix} t:NavMeshData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).StartsWith(AssetPrefix))
                    continue;

                NavMeshData data = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
                if (data != null && !used.Contains(data))
                    unused.Add(path);
            }

            if (unused.Count == 0)
            {
                EditorUtility.DisplayDialog("Delete Unused NavMesh Assets", "No unused HRCG NavMesh assets found.", "OK");
                return;
            }

            string list = string.Join("\n", unused.Count > 15 ? unused.GetRange(0, 15) : unused);
            if (unused.Count > 15)
                list += $"\n... and {unused.Count - 15} more";

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Unused NavMesh Assets",
                $"Delete {unused.Count} HRCG NavMesh asset(s) not used by any open scene? This cannot be undone.\n\n" +
                "Assets used only by scenes that are not open will also be deleted.\n\n" + list,
                "Delete", "Cancel");

            if (!confirmed)
                return;

            foreach (string path in unused)
                AssetDatabase.DeleteAsset(path);

            Debug.Log($"Deleted {unused.Count} unused HRCG NavMesh asset(s)");
        }

        private static HashSet<NavMeshData> CollectUsedNavMeshData()
        {
            HashSet<NavMeshData> used = new HashSet<NavMeshData>();
#if HRCG_AI_NAVIGATION
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (NavMeshSurface surface in root.GetComponentsInChildren<NavMeshSurface>(true))
                    {
                        if (surface.navMeshData != null)
                            used.Add(surface.navMeshData);
                    }
                }
            }
#endif
            return used;
        }

        private static string GetTargetFolder(Scene scene)
        {
            string folder = UnsavedSceneFolder;

            if (!string.IsNullOrEmpty(scene.path))
            {
                string sceneDirectory = Path.GetDirectoryName(scene.path).Replace('\\', '/');
                folder = $"{sceneDirectory}/{Path.GetFileNameWithoutExtension(scene.path)}";
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }

            return folder;
        }

        private static string Sanitize(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }
    }
}
