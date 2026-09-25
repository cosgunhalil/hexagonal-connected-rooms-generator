using UnityEngine;
using HRCG.Generation;
using HRCG.Geometry;

namespace HRCG.Runtime
{
    public class HRCGRuntimeComponent : MonoBehaviour
    {
        [Header("Generation Settings")]
        public GenerationParameters parameters;

        [Header("Materials")]
        public Material floorMaterial;
        public Material wallMaterial;

        [Header("Generation Options")]
        public bool generateOnStart = true;
        public bool generateSeparateByRoom = false;

        [Header("Output")]
        public GameObject generatedLevel;

        private void Start()
        {
            if (generateOnStart)
            {
                Generate();
            }
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            ClearGenerated();

            if (parameters == null)
            {
                Debug.LogError("Generation parameters are null!");
                return;
            }

            if (!parameters.Validate(out string errorMessage))
            {
                Debug.LogError($"Invalid parameters: {errorMessage}");
                return;
            }

            float startTime = Time.realtimeSinceStartup;

            if (generateSeparateByRoom)
            {
                GenerateByRoom();
            }
            else
            {
                GenerateComplete();
            }

            float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"Level generated in {elapsed:F2}ms");
        }

        private void GenerateComplete()
        {
            generatedLevel = LevelGeometryGenerator.GenerateComplete(
                parameters,
                floorMaterial,
                wallMaterial
            );

            if (generatedLevel != null)
            {
                generatedLevel.transform.SetParent(transform);
                generatedLevel.transform.localPosition = Vector3.zero;
            }
        }

        private void GenerateByRoom()
        {
            HRCGGenerator generator = new HRCGGenerator();
            var grid = generator.Generate(parameters);

            LevelGeometryGenerator geoGen = LevelGeometryGenerator.FromParameters(grid, parameters);
            geoGen.SetMaterials(floorMaterial, wallMaterial);

            generatedLevel = geoGen.GenerateLevelSeparateByRoom();

            if (generatedLevel != null)
            {
                generatedLevel.transform.SetParent(transform);
                generatedLevel.transform.localPosition = Vector3.zero;
            }
        }

        [ContextMenu("Clear Generated")]
        public void ClearGenerated()
        {
            if (generatedLevel != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedLevel);
                }
                else
                {
                    DestroyImmediate(generatedLevel);
                }
                generatedLevel = null;
            }
        }

        [ContextMenu("Randomize Seed")]
        public void RandomizeSeed()
        {
            if (parameters != null)
            {
                parameters.RandomSeed = Random.Range(0, 999999);
                Debug.Log($"New seed: {parameters.RandomSeed}");
            }
        }

        [ContextMenu("Generate with Random Seed")]
        public void GenerateWithRandomSeed()
        {
            RandomizeSeed();
            Generate();
        }

        private void OnDestroy()
        {
            ClearGenerated();
        }
    }
}
