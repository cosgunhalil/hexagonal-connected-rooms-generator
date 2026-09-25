using System;
using UnityEngine;

namespace CRG.Generation
{
    // 2D tests in the XZ plane used to place rooms of different grids next to each other.
    public static class PlanarGeometry
    {
        // True when two convex polygons (counter-clockwise, XZ) overlap by more than tolerance. Polygons that only
        // touch along an edge or at a corner do not overlap.
        public static bool ConvexPolygonsOverlap(Vector3[] a, Vector3[] b, float tolerance)
        {
            return !HasSeparatingAxis(a, a, b, tolerance) && !HasSeparatingAxis(b, a, b, tolerance);
        }

        private static bool HasSeparatingAxis(Vector3[] edgesFrom, Vector3[] a, Vector3[] b, float tolerance)
        {
            for (int i = 0; i < edgesFrom.Length; i++)
            {
                Vector3 start = edgesFrom[i];
                Vector3 end = edgesFrom[(i + 1) % edgesFrom.Length];
                double axisX = end.z - start.z;
                double axisZ = -(end.x - start.x);
                double length = Math.Sqrt(axisX * axisX + axisZ * axisZ);
                if (length <= 0)
                    continue;
                axisX /= length;
                axisZ /= length;

                Project(a, axisX, axisZ, out double minA, out double maxA);
                Project(b, axisX, axisZ, out double minB, out double maxB);

                double overlap = Math.Min(maxA - minB, maxB - minA);
                if (overlap <= tolerance)
                    return true;
            }
            return false;
        }

        private static void Project(Vector3[] polygon, double axisX, double axisZ, out double min, out double max)
        {
            min = double.MaxValue;
            max = double.MinValue;
            foreach (Vector3 point in polygon)
            {
                double value = point.x * axisX + point.z * axisZ;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
        }

        public enum SegmentContact
        {
            // Not on a common line, or on one but not sharing a stretch longer than the tolerance.
            None,
            // The same edge, running in opposite directions: the two cells lie on either side of it.
            ExactOpposite,
            // On a common line and sharing a stretch, but not the same edge.
            PartialOverlap
        }

        public static SegmentContact GetContact(Vector3 aStart, Vector3 aEnd, Vector3 bStart, Vector3 bEnd, float tolerance)
        {
            if (Distance(aStart, bEnd) <= tolerance && Distance(aEnd, bStart) <= tolerance)
                return SegmentContact.ExactOpposite;

            double dx = aEnd.x - aStart.x;
            double dz = aEnd.z - aStart.z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length <= 0)
                return SegmentContact.None;
            dx /= length;
            dz /= length;

            // Both endpoints of b must lie on a's line.
            if (Math.Abs(Cross(dx, dz, bStart.x - aStart.x, bStart.z - aStart.z)) > tolerance ||
                Math.Abs(Cross(dx, dz, bEnd.x - aStart.x, bEnd.z - aStart.z)) > tolerance)
                return SegmentContact.None;

            double t0 = (bStart.x - aStart.x) * dx + (bStart.z - aStart.z) * dz;
            double t1 = (bEnd.x - aStart.x) * dx + (bEnd.z - aStart.z) * dz;
            double shared = Math.Min(length, Math.Max(t0, t1)) - Math.Max(0, Math.Min(t0, t1));

            return shared > tolerance ? SegmentContact.PartialOverlap : SegmentContact.None;
        }

        private static double Cross(double ax, double az, double bx, double bz)
        {
            return ax * bz - az * bx;
        }

        private static double Distance(Vector3 a, Vector3 b)
        {
            double dx = a.x - b.x;
            double dz = a.z - b.z;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
