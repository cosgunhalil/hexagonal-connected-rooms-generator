using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using CRG.Core;
using CRG.Generation;
using CRG.Geometry;

namespace CRG.Tests
{
    // Hex levels must not change when the code is refactored: every entry pins the layout (rooms, cells, doors,
    // ceilings) and the exact triangles of one generated level. A mismatch means existing seeds would now
    // produce different levels. If a change is intentional, update the hashes from the failure message.
    public class GoldenLevelTests
    {
        public readonly struct Golden
        {
            public readonly string Config;
            public readonly int Seed;
            public readonly ulong Layout;
            public readonly ulong Geometry;

            public Golden(string config, int seed, ulong layout, ulong geometry)
            {
                Config = config;
                Seed = seed;
                Layout = layout;
                Geometry = geometry;
            }

            public override string ToString() => $"{Config} seed {Seed}";
        }

        private static readonly Dictionary<string, Action<GenerationParameters>> Configs = new Dictionary<string, Action<GenerationParameters>>
        {
            { "default", p => { } },
            { "large", p => { p.MinCellsPerRoom = 3; p.MaxCellsPerRoom = 7; p.TargetRoomCount = 20; } },
            { "huge", p => { p.MaxCellsPerRoom = 10; p.TargetRoomCount = 100; } },
            { "maxConnections2", p => { p.MaxConnectionsPerRoom = 2; p.TargetRoomCount = 30; } },
            { "sprawling", p => { p.LayoutBias = 1f; p.TargetRoomCount = 30; } },
            { "clustered", p => { p.LayoutBias = -1f; p.TargetRoomCount = 30; } },
            { "tree", p => { p.LoopChance = 0f; p.TargetRoomCount = 30; } },
            { "thickHalfCeilings", p => { p.WallThickness = 1f; p.AddCeiling = true; p.CeilingChance = 0.5f; p.TargetRoomCount = 20; } },
            { "thickFullDoors", p => { p.WallThickness = 2.5f; p.DoorHeight = 3f; p.MinCellsPerRoom = 3; p.MaxCellsPerRoom = 7; p.TargetRoomCount = 20; } },
            { "ceilingsWideDoors", p => { p.AddCeiling = true; p.DoorWidthRatio = 0.35f; p.TargetRoomCount = 15; } },
        };

        private static IEnumerable<Golden> Levels()
        {
            return new[]
            {
                new Golden("default", 0, 0x2F1D818E490F43E1UL, 0x694F46840A18E11FUL),
                new Golden("default", 1, 0x940C4372871C1CC6UL, 0x7818F328A27C8353UL),
                new Golden("default", 2, 0x3143FFF00C79C988UL, 0x945285591FB5FCB3UL),
                new Golden("default", 3, 0xCA86721B522A82CEUL, 0xE4C98861423C50EFUL),
                new Golden("default", 4, 0x1093DA3D12AFEB40UL, 0x49BE1DC785A9B85FUL),
                new Golden("large", 0, 0xFF7AF718A1C6BCF3UL, 0xA6A3C98B65E6D673UL),
                new Golden("large", 1, 0xB459B1EA6412E8BFUL, 0xE26DFE463FC7CA87UL),
                new Golden("large", 2, 0x6BAC94C6879FD938UL, 0x6538074C33BCA981UL),
                new Golden("large", 3, 0x1DE6EC645FCA9643UL, 0x615EE3BC4D4234D9UL),
                new Golden("large", 4, 0x9DF7E661B724FB22UL, 0xA6ABC21BCCD1E8DDUL),
                new Golden("huge", 0, 0x3BD016A70816DD8FUL, 0x7BBD7FF4429BEBC9UL),
                new Golden("huge", 1, 0xB741975C9E4A3E29UL, 0xC4A2AEDFF368624DUL),
                new Golden("huge", 2, 0x634C7DBE78976DC9UL, 0xD75A652F74C40889UL),
                new Golden("huge", 3, 0xADF07B95AC97FB89UL, 0xE5BFF2B7538608BBUL),
                new Golden("huge", 4, 0xE0EF4728E3EC475CUL, 0x61BEF0EC5A74FAFFUL),
                new Golden("maxConnections2", 0, 0x98F0968116EBBD5EUL, 0x647A3438D4252789UL),
                new Golden("maxConnections2", 1, 0xB641E279CBD68430UL, 0x58CF2989A63D2711UL),
                new Golden("maxConnections2", 2, 0x77B3750B9ED71FDEUL, 0x95271FE7B51AC001UL),
                new Golden("maxConnections2", 3, 0x3D7FA3655F5228BCUL, 0x19A60A1B0133B0A7UL),
                new Golden("maxConnections2", 4, 0x01D770521111C7A1UL, 0x20F727D06E452F15UL),
                new Golden("sprawling", 0, 0xFCD44B3FCE92F24DUL, 0x88A89D94AB6F25E7UL),
                new Golden("sprawling", 1, 0xD985F35B544E837EUL, 0xBDED2176E149F969UL),
                new Golden("sprawling", 2, 0x188C195E1F430B32UL, 0x7E71605240BD75B3UL),
                new Golden("sprawling", 3, 0x542A05FCC37542CBUL, 0xE4660908A5FC7251UL),
                new Golden("sprawling", 4, 0xDDBE5D7578C568B2UL, 0x2697A88793C43B7DUL),
                new Golden("clustered", 0, 0x40C58767AA3BA656UL, 0x37347614F5F68EDDUL),
                new Golden("clustered", 1, 0xD9CBF04923CF1185UL, 0xF1CDC49BAB5C9C87UL),
                new Golden("clustered", 2, 0x4B03E6820489E8B8UL, 0x84C6DCB31F0EB68BUL),
                new Golden("clustered", 3, 0x4864CE7939E3BD56UL, 0x50B2E56D728BAF1FUL),
                new Golden("clustered", 4, 0x7FB1E94429C291EFUL, 0x4A07788A4C27BE1DUL),
                new Golden("tree", 0, 0xB34D7F5AC33395F8UL, 0x5F5419ADA5779E83UL),
                new Golden("tree", 1, 0x985F9551D22A1055UL, 0xD6FF6A99FBDD714BUL),
                new Golden("tree", 2, 0xADC82D41EBC58D40UL, 0xCAF48C73895B0F65UL),
                new Golden("tree", 3, 0x0C96E2B47588339FUL, 0x26C3C3675FC1EBBFUL),
                new Golden("tree", 4, 0x040FE5680CD01E29UL, 0x6A0D2428A9FA453FUL),
                new Golden("thickHalfCeilings", 0, 0xA0A6F96F107D8D70UL, 0x2536DDEF7C711655UL),
                new Golden("thickHalfCeilings", 1, 0x928D596DEF725E04UL, 0xADB6F1C9CDC82098UL),
                new Golden("thickHalfCeilings", 2, 0xD30A8572D623B7A7UL, 0xA6EA1B97399B6B28UL),
                new Golden("thickHalfCeilings", 3, 0x39CED796812EB052UL, 0xA6602BBC3473896FUL),
                new Golden("thickHalfCeilings", 4, 0x0946F4C94E2026DEUL, 0xAE063DCDB7AC2860UL),
                new Golden("thickFullDoors", 0, 0xFF7AF718A1C6BCF3UL, 0x4181FB3FFE995633UL),
                new Golden("thickFullDoors", 1, 0xB459B1EA6412E8BFUL, 0x9ABAE916A229187BUL),
                new Golden("thickFullDoors", 2, 0x6BAC94C6879FD938UL, 0x980F387628477411UL),
                new Golden("thickFullDoors", 3, 0x1DE6EC645FCA9643UL, 0x419ED48A10267B36UL),
                new Golden("thickFullDoors", 4, 0x9DF7E661B724FB22UL, 0x9BEA8DAE97DDAB5CUL),
                new Golden("ceilingsWideDoors", 0, 0xA9A191EE0632C4CAUL, 0x41445E5C6B7ECF79UL),
                new Golden("ceilingsWideDoors", 1, 0xEB00048B417F65E4UL, 0x50CAEEB0A3510519UL),
                new Golden("ceilingsWideDoors", 2, 0xC3F83946993D7473UL, 0x2428ABAED9BF0AD9UL),
                new Golden("ceilingsWideDoors", 3, 0x7D282249BED71B98UL, 0x9257E77F9739BDCEUL),
                new Golden("ceilingsWideDoors", 4, 0x9B6CA867B4DD8506UL, 0x148CBAA17665B04DUL),
            };
        }

        [TestCaseSource(nameof(Levels))]
        public void HexLevel_IsUnchanged(Golden golden)
        {
            GenerationParameters parameters = GenerationParameters.CreateDefault();
            Configs[golden.Config](parameters);
            parameters.RandomSeed = golden.Seed;

            CellGrid grid = new CRGGenerator().Generate(parameters);
            GameObject level = LevelGeometryGenerator.FromParameters(grid, parameters).GenerateLevel();

            try
            {
                ulong layout = Fnv(LayoutFingerprint(grid));
                ulong geometry = GeometryHash(level);

                Assert.That(layout, Is.EqualTo(golden.Layout),
                    $"{golden}: layout changed (actual 0x{layout:X16}UL)");
                Assert.That(geometry, Is.EqualTo(golden.Geometry),
                    $"{golden}: geometry changed (actual 0x{geometry:X16}UL)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(level);
            }
        }

        private static string LayoutFingerprint(CellGrid grid)
        {
            StringBuilder builder = new StringBuilder();

            foreach (Room room in grid.GetAllRooms().OrderBy(r => r.RoomID))
            {
                builder.Append('R').Append(room.RoomID).Append(':');
                foreach (CellCoord cell in room.Cells)
                    builder.Append(Key(cell)).Append(';');
                foreach (KeyValuePair<int, SharedWallData> connection in room.Connections.OrderBy(c => c.Key))
                {
                    EdgeConnection door = connection.Value.SharedEdges[0];
                    builder.Append('>').Append(connection.Key).Append('@').Append(Key(door.CellA)).Append('/').Append(door.EdgeIndexA);
                }
                builder.Append(room.HasCeiling ? "C" : "O").Append('\n');
            }

            return builder.ToString();
        }

        private static string Key(CellCoord cell)
        {
            return $"{cell.x},{cell.y}";
        }

        // Order-independent hash of every triangle (vertices rounded to 0.01, winding kept).
        private static ulong GeometryHash(GameObject level)
        {
            List<string> keys = new List<string>();

            foreach (MeshFilter filter in level.GetComponentsInChildren<MeshFilter>())
            {
                Vector3[] vertices = filter.sharedMesh.vertices;
                int[] triangles = filter.sharedMesh.triangles;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    string[] corners =
                    {
                        VertexKey(vertices[triangles[i]]), VertexKey(vertices[triangles[i + 1]]), VertexKey(vertices[triangles[i + 2]])
                    };

                    int start = 0;
                    for (int k = 1; k < 3; k++)
                    {
                        if (string.CompareOrdinal(corners[k], corners[start]) < 0)
                            start = k;
                    }

                    keys.Add($"{corners[start]}|{corners[(start + 1) % 3]}|{corners[(start + 2) % 3]}");
                }
            }

            keys.Sort(string.CompareOrdinal);
            return Fnv(string.Join("\n", keys));
        }

        private static string VertexKey(Vector3 vertex)
        {
            return $"{Round(vertex.x)},{Round(vertex.y)},{Round(vertex.z)}";
        }

        private static long Round(float value)
        {
            return (long)Math.Round(value * 100.0, MidpointRounding.AwayFromZero);
        }

        private static ulong Fnv(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
