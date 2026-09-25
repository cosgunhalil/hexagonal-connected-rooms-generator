using System;
using HRCG.Core;
using UnityEngine;

namespace HRCG.Generation
{
    public enum NavMeshGeometrySource
    {
        RenderMeshes,
        PhysicsColliders
    }

    [Serializable]
    public class GenerationParameters
    {
        public const float MaxWallThicknessRatio = 0.25f;

        private const float MinDoorClearance = 0.01f;

        [Header("Hexagon Settings")]
        [Tooltip("Size of each hexagon edge in world units")]
        [Range(1f, 50f)]
        public float HexSize = 10f;

        [Header("Wall Settings")]
        [Tooltip("Height of walls in world units")]
        [Range(0.5f, 20f)]
        public float WallHeight = 3f;

        [Tooltip("Height of door openings in world units (must not exceed WallHeight)")]
        [Range(0.5f, 20f)]
        public float DoorHeight = 2.5f;

        [Tooltip("Door width as a fraction of the hexagon edge length (0.2 = 1/5 of the edge)")]
        [Range(0.05f, 1f)]
        public float DoorWidthRatio = 0.2f;

        [Tooltip("Wall thickness in world units. 0 builds zero-thickness double-sided walls")]
        [Range(0f, 5f)]
        public float WallThickness = 0f;

        [Tooltip("Cover every room with a ceiling at wall height (visible from inside only)")]
        public bool AddCeiling = false;

        [Tooltip("With Add Ceiling, the chance that each room gets a ceiling. 1 = every room, 0 = none")]
        [Range(0f, 1f)]
        public float CeilingChance = 1f;

        [Header("Room Size Settings")]
        [Tooltip("Minimum number of hexagons per room")]
        [Range(1, 20)]
        public int MinHexagonsPerRoom = 1;

        [Tooltip("Maximum number of hexagons per room")]
        [Range(1, 20)]
        public int MaxHexagonsPerRoom = 5;

        [Header("Generation Settings")]
        [Tooltip("Target number of rooms to generate")]
        [Range(1, 100)]
        public int TargetRoomCount = 10;

        [Tooltip("Starting position for the first room")]
        public AxialCoord StartPosition = new AxialCoord(0, 0);

        [Header("Connection Constraints")]
        [Tooltip("Minimum connections each room must have to other rooms")]
        [Range(1, 6)]
        public int MinConnectionsPerRoom = 1;

        [Tooltip("Maximum connections each room can have to other rooms")]
        [Range(1, 6)]
        public int MaxConnectionsPerRoom = 6;

        [Tooltip("New rooms leave one connection slot free so later rooms can attach to them. " +
                 "Produces larger levels at low MaxConnectionsPerRoom, with fewer loops")]
        public bool ReserveConnectionForGrowth = true;

        [Header("Layout")]
        [Tooltip("-1 grows compact, clustered levels; 0 is uniform random; 1 grows long, sprawling branches")]
        [Range(-1f, 1f)]
        public float LayoutBias = 0f;

        [Tooltip("Chance that a new room also connects to each additional adjacent room after its first door. " +
                 "0 = tree (no loops), 1 = connect whenever limits allow")]
        [Range(0f, 1f)]
        public float LoopChance = 1f;

        [Header("Physics & Navigation")]
        [Tooltip("Add a MeshCollider to the generated level mesh")]
        public bool AddMeshCollider = true;

        [Tooltip("Bake a NavMesh for the level (requires the AI Navigation package)")]
        public bool BakeNavMesh = false;

        [Tooltip("NavMesh agent type ID from Navigation settings (0 = Humanoid)")]
        public int NavMeshAgentTypeID = 0;

        [Tooltip("Geometry the NavMesh is baked from")]
        public NavMeshGeometrySource NavMeshGeometry = NavMeshGeometrySource.RenderMeshes;

        [Header("Spawns")]
        [Tooltip("Create an empty spawn point Transform inside every room")]
        public bool CreateSpawnPoints = true;

        [Header("Randomization")]
        [Tooltip("Random seed for reproducible generation (-1 for random)")]
        public int RandomSeed = -1;

        [Header("Safety Limits")]
        [Tooltip("Maximum iterations before aborting generation")]
        [Range(100, 10000)]
        public int MaxIterations = 1000;

        [Tooltip("Maximum retries when a room fails to generate")]
        [Range(1, 10)]
        public int MaxRetriesPerRoom = 3;

        public GenerationParameters()
        {
        }

        public GenerationParameters(
            float hexSize,
            int minHexPerRoom,
            int maxHexPerRoom,
            int targetRoomCount)
        {
            HexSize = hexSize;
            MinHexagonsPerRoom = minHexPerRoom;
            MaxHexagonsPerRoom = maxHexPerRoom;
            TargetRoomCount = targetRoomCount;
        }

        public bool Validate(out string errorMessage)
        {
            if (HexSize <= 0)
            {
                errorMessage = "HexSize must be greater than 0";
                return false;
            }

            if (WallHeight <= 0)
            {
                errorMessage = "WallHeight must be greater than 0";
                return false;
            }

            if (DoorHeight <= 0 || DoorHeight > WallHeight)
            {
                errorMessage = "DoorHeight must be greater than 0 and not exceed WallHeight";
                return false;
            }

            if (DoorWidthRatio <= 0 || DoorWidthRatio > 1)
            {
                errorMessage = "DoorWidthRatio must be greater than 0 and at most 1";
                return false;
            }

            if (WallThickness < 0f || WallThickness > HexSize * MaxWallThicknessRatio)
            {
                errorMessage = $"WallThickness must be between 0 and {HexSize * MaxWallThicknessRatio:F2} (a quarter of HexSize)";
                return false;
            }

            // Wall corners move the inner face of a wall inwards along the edge; the door opening must stay clear of them.
            if (WallThickness > 0f &&
                (HexSize - HexSize * DoorWidthRatio) / 2f < WallThickness * HexMath.ThickWallCornerInsetPerThickness + MinDoorClearance)
            {
                errorMessage = "Doors are too wide for this WallThickness; lower DoorWidthRatio or WallThickness";
                return false;
            }

            if (CeilingChance < 0f || CeilingChance > 1f)
            {
                errorMessage = "CeilingChance must be between 0 and 1";
                return false;
            }

            if (MinHexagonsPerRoom <= 0)
            {
                errorMessage = "MinHexagonsPerRoom must be at least 1";
                return false;
            }

            if (MaxHexagonsPerRoom < MinHexagonsPerRoom)
            {
                errorMessage = "MaxHexagonsPerRoom must be greater than or equal to MinHexagonsPerRoom";
                return false;
            }

            if (TargetRoomCount <= 0)
            {
                errorMessage = "TargetRoomCount must be at least 1";
                return false;
            }

            if (MinConnectionsPerRoom <= 0 || MinConnectionsPerRoom > 6)
            {
                errorMessage = "MinConnectionsPerRoom must be between 1 and 6";
                return false;
            }

            if (MaxConnectionsPerRoom < MinConnectionsPerRoom || MaxConnectionsPerRoom > 6)
            {
                errorMessage = "MaxConnectionsPerRoom must be between MinConnectionsPerRoom and 6";
                return false;
            }

            if (MaxConnectionsPerRoom == 1 && TargetRoomCount > 2)
            {
                errorMessage = "MaxConnectionsPerRoom = 1 allows at most 2 connected rooms; increase it or lower TargetRoomCount";
                return false;
            }

            if (LayoutBias < -1f || LayoutBias > 1f)
            {
                errorMessage = "LayoutBias must be between -1 and 1";
                return false;
            }

            if (LoopChance < 0f || LoopChance > 1f)
            {
                errorMessage = "LoopChance must be between 0 and 1";
                return false;
            }

            if (BakeNavMesh && NavMeshGeometry == NavMeshGeometrySource.PhysicsColliders && !AddMeshCollider)
            {
                errorMessage = "Baking the NavMesh from colliders requires AddMeshCollider";
                return false;
            }

            if (MaxIterations <= 0)
            {
                errorMessage = "MaxIterations must be greater than 0";
                return false;
            }

            if (MaxRetriesPerRoom <= 0)
            {
                errorMessage = "MaxRetriesPerRoom must be greater than 0";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public static GenerationParameters CreateDefault()
        {
            return new GenerationParameters
            {
                HexSize = 10f,
                WallHeight = 3f,
                DoorHeight = 2.5f,
                DoorWidthRatio = 0.2f,
                WallThickness = 0f,
                AddCeiling = false,
                CeilingChance = 1f,
                MinHexagonsPerRoom = 1,
                MaxHexagonsPerRoom = 5,
                TargetRoomCount = 10,
                StartPosition = new AxialCoord(0, 0),
                MinConnectionsPerRoom = 1,
                MaxConnectionsPerRoom = 6,
                ReserveConnectionForGrowth = true,
                LayoutBias = 0f,
                LoopChance = 1f,
                AddMeshCollider = true,
                BakeNavMesh = false,
                NavMeshAgentTypeID = 0,
                NavMeshGeometry = NavMeshGeometrySource.RenderMeshes,
                CreateSpawnPoints = true,
                RandomSeed = -1,
                MaxIterations = 1000,
                MaxRetriesPerRoom = 3
            };
        }

        public override string ToString()
        {
            return $"GenerationParameters: {TargetRoomCount} rooms, " +
                   $"{MinHexagonsPerRoom}-{MaxHexagonsPerRoom} hexagons per room, " +
                   $"HexSize={HexSize}, Seed={RandomSeed}";
        }
    }
}