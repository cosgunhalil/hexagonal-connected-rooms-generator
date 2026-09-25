using UnityEngine;
using UnityEngine.AI;
using CRG.Generation;
#if CRG_AI_NAVIGATION
using Unity.AI.Navigation;
#endif

namespace CRG.Geometry
{
    // NavMesh baking for generated levels. Needs the AI Navigation package (com.unity.ai.navigation);
    // without it, baking is skipped with a warning and the rest of the tool keeps working.
    public static class LevelNavMeshBaker
    {
#if CRG_AI_NAVIGATION
        public static bool IsAvailable => true;

        // Raised after a successful bake. The editor uses it to save Edit Mode bakes as assets.
        public static event System.Action<NavMeshSurface> NavMeshBaked;

        // When false, Edit Mode bakes stay in memory instead of being saved as assets (used by tests).
        public static bool PersistEditorBakes = true;
#else
        public static bool IsAvailable => false;
#endif

        public static bool Bake(GameObject level, int agentTypeID, NavMeshGeometrySource geometry)
        {
#if CRG_AI_NAVIGATION
            NavMeshSurface surface = level.GetComponent<NavMeshSurface>();
            if (surface == null)
                surface = level.AddComponent<NavMeshSurface>();

            surface.agentTypeID = agentTypeID;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = geometry == NavMeshGeometrySource.PhysicsColliders
                ? NavMeshCollectGeometry.PhysicsColliders
                : NavMeshCollectGeometry.RenderMeshes;

            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogWarning($"NavMesh bake produced no data for '{level.name}'");
                return false;
            }

            NavMeshBaked?.Invoke(surface);
            return true;
#else
            Debug.LogWarning("Skipping NavMesh bake: install the AI Navigation package (com.unity.ai.navigation) to enable it");
            return false;
#endif
        }

        // Doors narrower than the agent's diameter or lower than its height leave rooms unreachable.
        public static bool DoorsFitAgent(GenerationParameters parameters, out string message)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(parameters.NavMeshAgentTypeID);
            if (settings.agentTypeID == -1)
            {
                message = $"Unknown NavMesh agent type ID {parameters.NavMeshAgentTypeID}";
                return false;
            }

            float doorWidth = parameters.CellSize * parameters.DoorWidthRatio;
            float agentDiameter = settings.agentRadius * 2f;

            if (doorWidth <= agentDiameter)
            {
                message = $"Doors ({doorWidth:F2} wide) are too narrow for the agent (diameter {agentDiameter:F2}); agents cannot pass between rooms";
                return false;
            }

            if (parameters.DoorHeight < settings.agentHeight)
            {
                message = $"Doors ({parameters.DoorHeight:F2} high) are lower than the agent ({settings.agentHeight:F2}); agents cannot pass between rooms";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
