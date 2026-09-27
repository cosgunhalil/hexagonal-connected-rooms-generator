using System;
using UnityEditor;
using UnityEngine;
using CRG.Runtime;

namespace CRG.Editor
{
    // Undoable builder actions shared by the CRG Build tool and the builder's Inspector.
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
    }
}
