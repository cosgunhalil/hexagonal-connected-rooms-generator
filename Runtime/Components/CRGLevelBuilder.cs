using UnityEngine;
using CRG.Building;
using CRG.Generation;
using CRG.Geometry;

namespace CRG.Runtime
{
    // Holds a hand-built level: cells placed one by one in the Scene view (with the CRG Build tool) and the state of
    // every shared edge. The layout is stored in units of the cell size and is saved with the scene, so it can be
    // edited again at any time. In the Editor it shows a live preview of the level's geometry; Bake creates a normal
    // level GameObject from it.
    [DisallowMultipleComponent]
    [SelectionBase]
    [AddComponentMenu("CRG/CRG Level Builder")]
    public class CRGLevelBuilder : MonoBehaviour
    {
        private const string PreviewName = "CRG Level Preview";

        [Tooltip("Optional: when set, the settings of this asset are used instead of the ones below")]
        public GenerationParametersAsset parametersAsset;

        [Tooltip("Cell size, walls, doors and gameplay extras; the random layout settings are not used")]
        public GenerationParameters parameters = new GenerationParameters();

        [Header("Materials")]
        public Material floorMaterial;
        public Material wallMaterial;
        [Tooltip("Used when Add Ceiling is enabled; falls back to the floor material when empty")]
        public Material ceilingMaterial;

        [Header("Doors")]
        [Tooltip("Optional prefab placed in every doorway when baking (+Z points through the door)")]
        public GameObject doorPrefab;

        [Tooltip("Show the level's geometry while editing (Editor only, never saved)")]
        public bool showPreview = true;

        [SerializeField, HideInInspector]
        private LevelLayout layout = new LevelLayout();

        private GameObject preview;

        public LevelLayout Layout => layout ?? (layout = new LevelLayout());

        public GenerationParameters Parameters =>
            parametersAsset != null && parametersAsset.parameters != null ? parametersAsset.parameters : parameters;

        public float CellSize => Parameters != null ? Parameters.CellSize : 10f;

        // From layout units to the builder's local space.
        public Vector3 LayoutToLocal(Vector3 layoutPoint)
        {
            return layoutPoint * CellSize;
        }

        public Vector3 LocalToLayout(Vector3 localPoint)
        {
            return localPoint / CellSize;
        }

        // The settings must be valid for the shapes in the layout (doors must clear the thick-wall corners).
        public bool Validate(out string errorMessage)
        {
            if (Parameters == null)
            {
                errorMessage = "The builder has no settings";
                return false;
            }

            return Parameters.ValidateHandBuilt(Layout.GetCornerInsetPerThickness(), out errorMessage);
        }

        // Builds a level GameObject from the layout, with level data and the gameplay extras of the settings, at
        // the builder's position and rotation. Returns null when the layout is empty or the settings are invalid.
        public GameObject Bake(bool separateRooms)
        {
            if (Layout.IsEmpty)
            {
                Debug.LogWarning("The level builder has no cells to bake");
                return null;
            }

            if (!Validate(out string errorMessage))
            {
                Debug.LogError($"Can't bake the level: {errorMessage}");
                return null;
            }

            LevelGeometryGenerator generator = LevelGeometryGenerator.FromParameters(Layout, Parameters);
            generator.SetMaterials(floorMaterial, wallMaterial, ceilingMaterial);
            generator.SetDoorPrefab(doorPrefab);

            GameObject level = separateRooms ? generator.GenerateLevelSeparateByRoom() : generator.GenerateLevel();
            if (level == null)
                return null;

            level.name = $"{name} Level";
            level.transform.SetParent(transform.parent, false);
            level.transform.SetPositionAndRotation(transform.position, transform.rotation);
            level.transform.localScale = transform.localScale;

            // The NavMesh was baked with the level at the origin; move it along with the level.
            LevelNavMeshBaker.RefreshPlacement(level);
            return level;
        }

        // Rebuilds the Editor preview from the current layout and settings, or removes it when it shouldn't show.
        public void RebuildPreview()
        {
            DestroyPreview();

            if (!showPreview || Application.isPlaying || Layout.IsEmpty || !Validate(out _))
                return;

            GenerationParameters settings = Parameters;
            LevelGeometryGenerator generator = new LevelGeometryGenerator(Layout, settings.CellSize, settings.WallHeight,
                settings.DoorHeight, settings.DoorWidthRatio, settings.WallThickness, settings.AddCeiling);
            generator.SetMaterials(floorMaterial, wallMaterial, ceilingMaterial);

            preview = generator.GenerateMeshOnly(PreviewName);
            if (preview == null)
                return;

            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.transform.SetParent(transform, false);
        }

        public void DestroyPreview()
        {
            // Also removes previews left over from before a script reload, when the reference was lost.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.name == PreviewName && (child.hideFlags & HideFlags.DontSave) != 0)
                    DestroyObject(child);
            }

            if (preview != null)
                DestroyObject(preview);
            preview = null;
        }

        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void Reset()
        {
            parameters = GenerationParameters.CreateDefault();
        }

#if UNITY_EDITOR
        private bool previewQueued;

        // Rebuilds the preview after the current Editor event (settings changes, undo and redo come through here).
        public void RequestPreviewRebuild()
        {
            if (previewQueued)
                return;

            previewQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                previewQueued = false;
                if (this != null)
                    RebuildPreview();
            };
        }

        private void OnEnable()
        {
            RequestPreviewRebuild();
        }

        private void OnValidate()
        {
            RequestPreviewRebuild();
        }

        // Children can't be destroyed while the builder is being deactivated, so the preview goes a moment later
        // (when the builder itself is destroyed, the preview goes with it).
        private void OnDisable()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && !isActiveAndEnabled)
                    DestroyPreview();
            };
        }
#endif
    }
}
