using UnityEngine;

namespace CRG.Generation
{
    [CreateAssetMenu(fileName = "GenerationParameters", menuName = "CRG/Generation Parameters", order = 1)]
    public class GenerationParametersAsset : ScriptableObject
    {
        public GenerationParameters parameters;

        private void OnEnable()
        {
            if (parameters == null)
            {
                parameters = GenerationParameters.CreateDefault();
            }
        }

        public GenerationParameters GetParameters()
        {
            return parameters;
        }
    }
}
