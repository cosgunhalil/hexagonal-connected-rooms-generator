using System;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using CRG.Building;
using CRG.Generation;
using CRG.Runtime;

namespace CRG.Editor
{
    // Undoable builder actions shared by the CRG Build tool, the builder's and level data's Inspectors and the
    // Level Generator window.
    public static class CRGLevelBuilderActions
    {
        // Runs an edit of the layout as one undo step, then refreshes the preview (shown again if a bake hid it).
        public static void Edit(CRGLevelBuilder builder, string undoName, Action edit)
        {
            Undo.RecordObject(builder, undoName);
            edit();
            builder.showPreview = true;
            EditorUtility.SetDirty(builder);
            builder.RequestPreviewRebuild();
        }

        // Bakes a new level GameObject next to the builder. The preview is hidden so it doesn't overlap the baked
        // level; editing the layout shows it again.
        public static GameObject Bake(CRGLevelBuilder builder, bool separateRooms)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            GameObject level = builder.Bake(separateRooms);
            if (level == null)
                return null;

            Undo.RegisterCreatedObjectUndo(level, "Bake CRG Level");
            Undo.RecordObject(builder, "Bake CRG Level");
            builder.showPreview = false;
            EditorUtility.SetDirty(builder);
            builder.RequestPreviewRebuild();
            Undo.CollapseUndoOperations(group);

            EditorGUIUtility.PingObject(level);
            return level;
        }

        // Creates a Level Builder holding a layout, as one undo step (name it with undoName), selects it and starts
        // the CRG Build tool.
        public static CRGLevelBuilder CreateBuilder(string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale,
            LevelLayout layout, GenerationParameters settings, Material floor, Material wall, Material ceiling, GameObject doorPrefab, string undoName)
        {
            GameObject builderObject = new GameObject(name);
            builderObject.transform.SetParent(parent, false);
            builderObject.transform.SetPositionAndRotation(position, rotation);
            builderObject.transform.localScale = scale;

            CRGLevelBuilder builder = builderObject.AddComponent<CRGLevelBuilder>();
            builder.parameters = settings;
            builder.floorMaterial = floor;
            builder.wallMaterial = wall;
            builder.ceilingMaterial = ceiling;
            builder.doorPrefab = doorPrefab;
            builder.SetLayout(layout);
            builder.RequestPreviewRebuild();

            Undo.RegisterCreatedObjectUndo(builderObject, undoName);
            Selection.activeGameObject = builderObject;

            // The tool needs the new selection to be applied first.
            EditorApplication.delayCall += () =>
            {
                if (builderObject != null && Selection.activeGameObject == builderObject)
                    ToolManager.SetActiveTool<CRGLevelBuilderTool>();
            };

            return builder;
        }

        // Replaces a baked level (generated or hand-built) with a Level Builder holding the same layout and settings,
        // at the same place, as one undo step. Returns null (after telling the user) when it can't be converted.
        public static CRGLevelBuilder EditInLevelBuilder(CRGLevelData data)
        {
            LevelLayout layout;
            try
            {
                layout = LayoutConversion.FromLevelData(data);
            }
            catch (InvalidOperationException exception)
            {
                EditorUtility.DisplayDialog("Can't Edit This Level", exception.Message, "OK");
                return null;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            Transform level = data.transform;
            CRGLevelBuilder builder = CreateBuilder($"{level.name} Builder", level.parent, level.position, level.rotation, level.localScale, layout,
                LayoutConversion.SettingsFor(data), data.FloorMaterial, data.WallMaterial, data.CeilingMaterial, data.DoorPrefab, "Edit Level in Level Builder");
            builder.transform.SetSiblingIndex(level.GetSiblingIndex());

            Undo.DestroyObjectImmediate(level.gameObject);
            Undo.CollapseUndoOperations(group);
            return builder;
        }
    }
}
