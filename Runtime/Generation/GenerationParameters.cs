using System;
using HRCG.Core;
using UnityEngine;

namespace HRCG.Generation
{
    [Serializable]
    public class GenerationParameters
    {
        [Header("Hexagon Settings")]
        [Tooltip("Size of each hexagon edge in world units")]
        [Range(1f, 50f)]
        public float HexSize = 10f;

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
                MinHexagonsPerRoom = 1,
                MaxHexagonsPerRoom = 5,
                TargetRoomCount = 10,
                StartPosition = new AxialCoord(0, 0),
                MinConnectionsPerRoom = 1,
                MaxConnectionsPerRoom = 6,
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