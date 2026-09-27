using UnityEngine;
using CRG.Building;
using CRG.Generation;

namespace CRG.Runtime
{
    // Holds a hand-built level: cells placed one by one in the Scene view (with the CRG Build tool) and the state of
    // every shared edge. The layout is stored in units of the cell size and is saved with the scene, so it can be
    // edited again at any time.
    [DisallowMultipleComponent]
    [AddComponentMenu("CRG/CRG Level Builder")]
    public class CRGLevelBuilder : MonoBehaviour
    {
        [Tooltip("Optional: when set, the settings of this asset are used instead of the ones below")]
        public GenerationParametersAsset parametersAsset;

        [Tooltip("Cell size, walls, doors and gameplay extras; the random layout settings are not used")]
        public GenerationParameters parameters = new GenerationParameters();

        [SerializeField, HideInInspector]
        private LevelLayout layout = new LevelLayout();

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

        private void Reset()
        {
            parameters = GenerationParameters.CreateDefault();
        }
    }
}
