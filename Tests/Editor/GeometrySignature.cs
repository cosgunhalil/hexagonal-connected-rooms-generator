using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace CRG.Tests
{
    // Tolerance-comparable fingerprint of a triangle soup. A hash of rounded vertices breaks on last-bit
    // floating-point differences between runtimes (values that sit exactly on a rounding boundary), so instead
    // each feature is a smooth sum over triangles, which moves only negligibly under float noise:
    // - position features: area * sin(w . centroid + phase) for fixed pseudo-random wave vectors w;
    // - facing features: (area-weighted normal . u) * sin(w . centroid + phase), which flip with the winding.
    // Moving, adding, removing or flipping a triangle changes several features far beyond the tolerance.
    public sealed class GeometrySignature
    {
        public const int FeatureCount = 16;
        // Measured: float noise of 2e-5 on every coordinate moves features by at most 0.03; moving a 1 m2
        // triangle by 0.1 units moves them by at least 0.17 (flipping one: 2.1).
        private const double Tolerance = 0.08;

        private static readonly double[,] PositionWaves = Waves(1, FeatureCount);
        private static readonly double[,] FacingWaves = Waves(2, FeatureCount);

        public readonly int Triangles;
        public readonly double Area;
        public readonly double[] Position;
        public readonly double[] Facing;

        public GeometrySignature(int triangles, double area, double[] position, double[] facing)
        {
            Triangles = triangles;
            Area = area;
            Position = position;
            Facing = facing;
        }

        public static GeometrySignature FromTriangles(IEnumerable<(Vector3 a, Vector3 b, Vector3 c)> triangles)
        {
            int count = 0;
            double totalArea = 0;
            double[] position = new double[FeatureCount];
            double[] facing = new double[FeatureCount];

            foreach ((Vector3 a, Vector3 b, Vector3 c) in triangles)
            {
                double abx = b.x - a.x, aby = b.y - a.y, abz = b.z - a.z;
                double acx = c.x - a.x, acy = c.y - a.y, acz = c.z - a.z;
                // Half the cross product: the area-weighted normal.
                double nx = (aby * acz - abz * acy) / 2.0;
                double ny = (abz * acx - abx * acz) / 2.0;
                double nz = (abx * acy - aby * acx) / 2.0;
                double area = Math.Sqrt(nx * nx + ny * ny + nz * nz);

                count++;
                totalArea += area;

                double cx = (a.x + b.x + c.x) / 3.0, cy = (a.y + b.y + c.y) / 3.0, cz = (a.z + b.z + c.z) / 3.0;

                for (int k = 0; k < FeatureCount; k++)
                {
                    position[k] += area * Wave(PositionWaves, k, cx, cy, cz);

                    double projectedNormal = nx * FacingWaves[k, 4] + ny * FacingWaves[k, 5] + nz * FacingWaves[k, 6];
                    facing[k] += projectedNormal * Wave(FacingWaves, k, cx, cy, cz);
                }
            }

            return new GeometrySignature(count, totalArea, position, facing);
        }

        public bool Matches(GeometrySignature actual, out string difference)
        {
            difference = null;

            if (actual.Triangles != Triangles)
            {
                difference = $"triangle count {actual.Triangles}, expected {Triangles}";
                return false;
            }

            if (Math.Abs(actual.Area - Area) > Tolerance)
            {
                difference = $"total area {Format(actual.Area)}, expected {Format(Area)}";
                return false;
            }

            for (int k = 0; k < FeatureCount; k++)
            {
                if (Math.Abs(actual.Position[k] - Position[k]) > Tolerance)
                {
                    difference = $"position feature {k}: {Format(actual.Position[k])}, expected {Format(Position[k])}";
                    return false;
                }

                if (Math.Abs(actual.Facing[k] - Facing[k]) > Tolerance)
                {
                    difference = $"facing feature {k}: {Format(actual.Facing[k])}, expected {Format(Facing[k])}";
                    return false;
                }
            }

            return true;
        }

        public string ToCode()
        {
            return $"new GeometrySignature({Triangles}, {Format(Area)}, new[] {{ {string.Join(", ", Position.Select(Format))} }}, " +
                   $"new[] {{ {string.Join(", ", Facing.Select(Format))} }})";
        }

        private static string Format(double value)
        {
            // Round-trip format keeps the stored value exact; a trailing ".0" keeps it a double literal.
            string text = value.ToString("R", CultureInfo.InvariantCulture);
            return text.Contains(".") || text.Contains("E") ? text : text + ".0";
        }

        private static double Wave(double[,] waves, int k, double x, double y, double z)
        {
            return Math.Sin(waves[k, 0] * x + waves[k, 1] * y + waves[k, 2] * z + waves[k, 3]);
        }

        // Fixed pseudo-random wave vectors (frequency 0.2..1 per world unit), phases and facing directions.
        private static double[,] Waves(int seed, int count)
        {
            System.Random random = new System.Random(seed);
            double[,] waves = new double[count, 7];

            for (int k = 0; k < count; k++)
            {
                double theta = random.NextDouble() * 2 * Math.PI;
                double tilt = (random.NextDouble() - 0.5) * 0.6;
                double frequency = 0.2 + random.NextDouble() * 0.8;
                waves[k, 0] = frequency * Math.Cos(theta);
                waves[k, 1] = frequency * tilt;
                waves[k, 2] = frequency * Math.Sin(theta);
                waves[k, 3] = random.NextDouble() * 2 * Math.PI;

                double ux = random.NextDouble() - 0.5, uy = random.NextDouble() - 0.5, uz = random.NextDouble() - 0.5;
                double length = Math.Sqrt(ux * ux + uy * uy + uz * uz);
                waves[k, 4] = ux / length;
                waves[k, 5] = uy / length;
                waves[k, 6] = uz / length;
            }

            return waves;
        }
    }
}
