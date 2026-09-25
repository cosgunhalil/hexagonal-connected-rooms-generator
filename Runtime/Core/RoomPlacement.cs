using System;
using UnityEngine;

namespace CRG.Core
{
    // Rigid placement of a room's local grid in the level: a rotation about Y (counter-clockwise seen from
    // above, from +X towards +Z) followed by a translation in the XZ plane. Stored in double precision so long
    // chains of rotated rooms do not drift apart before their edges are compared.
    [Serializable]
    public struct RoomPlacement
    {
        public double x;
        public double z;
        public double angle;

        public static RoomPlacement Identity => new RoomPlacement();

        public RoomPlacement(double x, double z, double angle)
        {
            this.x = x;
            this.z = z;
            this.angle = angle;
        }

        public Vector3 TransformPoint(Vector3 local)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return new Vector3(
                (float)(x + cos * local.x - sin * local.z),
                local.y,
                (float)(z + sin * local.x + cos * local.z));
        }

        public Vector3 TransformDirection(Vector3 local)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return new Vector3(
                (float)(cos * local.x - sin * local.z),
                local.y,
                (float)(sin * local.x + cos * local.z));
        }

        // The placement that maps localStart onto worldStart and the direction localStart -> localEnd onto the
        // direction worldStart -> worldEnd (both segments are expected to have the same length).
        public static RoomPlacement Align(Vector3 localStart, Vector3 localEnd, Vector3 worldStart, Vector3 worldEnd)
        {
            double localAngle = Math.Atan2(localEnd.z - localStart.z, localEnd.x - localStart.x);
            double worldAngle = Math.Atan2(worldEnd.z - worldStart.z, worldEnd.x - worldStart.x);
            double angle = worldAngle - localAngle;

            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            double x = worldStart.x - (cos * localStart.x - sin * localStart.z);
            double z = worldStart.z - (sin * localStart.x + cos * localStart.z);

            return new RoomPlacement(x, z, angle);
        }
    }
}
