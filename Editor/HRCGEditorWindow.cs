using UnityEditor;
using UnityEngine;
using HRCG.Core;
using HRCG.Generation;
using HRCG.Geometry;

namespace HRCG.Editor
{
    public class HRCGEditorWindow : EditorWindow
    {
        private GenerationParameters parameters;
        private Material floorMaterial;
        private Material wallMaterial;
        private GameObject lastGeneratedLevel;
        
        private Vector2 scrollPosition;
        private bool showAdvancedSettings = false;
        private bool autoGenerate = false;

        [MenuItem("HRCG/Level Generator", false, 0)]
        [MenuItem("Window/HRCG/Level Generator")]
        public static void ShowWindow()
        {
            HRCGEditorWindow window = GetWindow<HRCGEditorWindow>("HRCG Level Generator");
            window.minSize = new Vector2(400, 600);
        }

        private void OnEnable()
        {
            if (parameters == null)
            {
                parameters = GenerationParameters.CreateDefault();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawBasicSettings();
            EditorGUILayout.Space(10);

            DrawRoomSettings();
            EditorGUILayout.Space(10);

            DrawConnectionSettings();
            EditorGUILayout.Space(10);

            DrawMaterialSettings();
            EditorGUILayout.Space(10);

            DrawAdvancedSettings();
            EditorGUILayout.Space(10);

            DrawGenerationButtons();
            EditorGUILayout.Space(10);

            DrawLastGeneratedInfo();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.LabelField("Hexagonal Connected Rooms Generator", headerStyle);
            EditorGUILayout.LabelField("Procedural Level Generation", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawBasicSettings()
        {
            EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);

            parameters.HexSize = EditorGUILayout.Slider(
                new GUIContent("Hex Size", "Size of each hexagon edge in world units"),
                parameters.HexSize, 1f, 50f);

            parameters.WallHeight = EditorGUILayout.Slider(
                new GUIContent("Wall Height", "Height of walls in world units"),
                parameters.WallHeight, 0.5f, 20f);

            parameters.DoorHeight = EditorGUILayout.Slider(
                new GUIContent("Door Height", "Height of door openings in world units"),
                parameters.DoorHeight, 0.5f, parameters.WallHeight);

            parameters.TargetRoomCount = EditorGUILayout.IntSlider(
                new GUIContent("Room Count", "Target number of rooms to generate"),
                parameters.TargetRoomCount, 1, 100);
        }

        private void DrawRoomSettings()
        {
            EditorGUILayout.LabelField("Room Size Settings", EditorStyles.boldLabel);

            parameters.MinHexagonsPerRoom = EditorGUILayout.IntSlider(
                new GUIContent("Min Hexagons/Room", "Minimum hexagons per room"),
                parameters.MinHexagonsPerRoom, 1, 20);

            parameters.MaxHexagonsPerRoom = EditorGUILayout.IntSlider(
                new GUIContent("Max Hexagons/Room", "Maximum hexagons per room"),
                parameters.MaxHexagonsPerRoom, 1, 20);

            if (parameters.MaxHexagonsPerRoom < parameters.MinHexagonsPerRoom)
            {
                parameters.MaxHexagonsPerRoom = parameters.MinHexagonsPerRoom;
            }
        }

        private void DrawConnectionSettings()
        {
            EditorGUILayout.LabelField("Connection Settings", EditorStyles.boldLabel);

            parameters.MinConnectionsPerRoom = EditorGUILayout.IntSlider(
                new GUIContent("Min Connections", "Minimum connections per room"),
                parameters.MinConnectionsPerRoom, 1, 6);

            parameters.MaxConnectionsPerRoom = EditorGUILayout.IntSlider(
                new GUIContent("Max Connections", "Maximum connections per room"),
                parameters.MaxConnectionsPerRoom, 1, 6);

            if (parameters.MaxConnectionsPerRoom < parameters.MinConnectionsPerRoom)
            {
                parameters.MaxConnectionsPerRoom = parameters.MinConnectionsPerRoom;
            }
        }

        private void DrawMaterialSettings()
        {
            EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);

            floorMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Floor Material", "Material for floor meshes"),
                floorMaterial, typeof(Material), false);

            wallMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Wall Material", "Material for wall meshes"),
                wallMaterial, typeof(Material), false);
        }

        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);

            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;

                parameters.RandomSeed = EditorGUILayout.IntField(
                    new GUIContent("Random Seed", "Seed for reproducible generation (-1 = random)"),
                    parameters.RandomSeed);

                parameters.MaxIterations = EditorGUILayout.IntSlider(
                    new GUIContent("Max Iterations", "Safety limit for generation loop"),
                    parameters.MaxIterations, 100, 10000);

                parameters.MaxRetriesPerRoom = EditorGUILayout.IntSlider(
                    new GUIContent("Max Retries/Room", "Retries when room generation fails"),
                    parameters.MaxRetriesPerRoom, 1, 10);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Start Position");
                parameters.StartPosition = new AxialCoord(
                    EditorGUILayout.IntField(parameters.StartPosition.columnIndex, GUILayout.Width(50)),
                    EditorGUILayout.IntField(parameters.StartPosition.rowIndex, GUILayout.Width(50))
                );
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }
        }

        private void DrawGenerationButtons()
        {
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Level", GUILayout.Height(40)))
            {
                GenerateLevel();
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate (Separate Rooms)"))
            {
                GenerateLevelByRoom();
            }

            if (GUILayout.Button("Clear Last"))
            {
                ClearLastGenerated();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Floor Only"))
            {
                GenerateFloorOnly();
            }

            if (GUILayout.Button("Walls Only"))
            {
                GenerateWallsOnly();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            autoGenerate = EditorGUILayout.Toggle(
                new GUIContent("Auto Regenerate", "Automatically regenerate on parameter change"),
                autoGenerate);

            if (GUILayout.Button("Randomize Seed"))
            {
                parameters.RandomSeed = Random.Range(0, 999999);
                if (autoGenerate)
                {
                    GenerateLevel();
                }
            }

            if (GUILayout.Button("Reset to Defaults"))
            {
                parameters = GenerationParameters.CreateDefault();
            }
        }

        private void DrawLastGeneratedInfo()
        {
            if (lastGeneratedLevel != null)
            {
                EditorGUILayout.LabelField("Last Generated Level", EditorStyles.boldLabel);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("GameObject", lastGeneratedLevel, typeof(GameObject), true);
                EditorGUI.EndDisabledGroup();

                MeshFilter[] meshFilters = lastGeneratedLevel.GetComponentsInChildren<MeshFilter>();
                int totalVertices = 0;
                int totalTriangles = 0;

                foreach (MeshFilter mf in meshFilters)
                {
                    if (mf.sharedMesh != null)
                    {
                        totalVertices += mf.sharedMesh.vertexCount;
                        totalTriangles += mf.sharedMesh.triangles.Length / 3;
                    }
                }

                EditorGUILayout.LabelField($"Mesh Objects: {meshFilters.Length}");
                EditorGUILayout.LabelField($"Total Vertices: {totalVertices}");
                EditorGUILayout.LabelField($"Total Triangles: {totalTriangles}");
            }
        }

        private void GenerateLevel()
        {
            Generate(geometryGenerator => geometryGenerator.GenerateLevel());
        }

        private void GenerateLevelByRoom()
        {
            Generate(geometryGenerator => geometryGenerator.GenerateLevelSeparateByRoom());
        }

        private void GenerateFloorOnly()
        {
            Generate(geometryGenerator => geometryGenerator.GenerateFloorOnly());
        }

        private void GenerateWallsOnly()
        {
            Generate(geometryGenerator => geometryGenerator.GenerateWallsOnly());
        }

        private void Generate(System.Func<LevelGeometryGenerator, GameObject> buildGeometry)
        {
            if (!ValidateParameters())
                return;

            ClearLastGenerated();

            float startTime = Time.realtimeSinceStartup;

            HRCGGenerator generator = new HRCGGenerator();
            HexGrid grid = generator.Generate(parameters);

            LevelGeometryGenerator geometryGenerator = new LevelGeometryGenerator(grid, parameters.WallHeight, parameters.DoorHeight);
            geometryGenerator.SetMaterials(floorMaterial, wallMaterial);

            lastGeneratedLevel = buildGeometry(geometryGenerator);

            float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
            Debug.Log($"Level generated in {elapsed:F2}ms");

            if (lastGeneratedLevel != null)
            {
                Undo.RegisterCreatedObjectUndo(lastGeneratedLevel, "Generate HRCG Level");
                Selection.activeGameObject = lastGeneratedLevel;
                EditorGUIUtility.PingObject(lastGeneratedLevel);
            }

            Repaint();
        }

        private void ClearLastGenerated()
        {
            if (lastGeneratedLevel != null)
            {
                Undo.DestroyObjectImmediate(lastGeneratedLevel);
                lastGeneratedLevel = null;
            }
        }

        private bool ValidateParameters()
        {
            if (!parameters.Validate(out string errorMessage))
            {
                EditorUtility.DisplayDialog("Invalid Parameters", errorMessage, "OK");
                return false;
            }

            return true;
        }
    }
}
