using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OMV = OpenMetaverse;

namespace org.herbal3d.cs.CommonEntitiesUtil {
    // A container for useful, static utility functions
    public class Utilities {
        // Base function that creates a 4x4 transform from pos, rotation, and scale.
        // Passing in pure arrays so not tied to types from different packages.
        // Code borrowed from ThreeJS.
        public static float[] ComposeMatrix4(float[] pos, float[] rot, float[] scale) {
            float[] ret = new float[16];

            float x = rot[0], y = rot[1], z = rot[2], w = rot[3];
            float x2 = x + x, y2 = y + y, z2 = z + z;
            float xx = x * x2, xy = x * y2, xz = x * z2;
            float yy = y * y2, yz = y * z2, zz = z * z2;
            float wx = w * x2, wy = w * y2, wz = w * z2;

            ret[0] = 1 - (yy + zz);
            ret[1] = xy - wz;
            ret[2] = xz + wy;

            ret[4] = xy + wz;
            ret[5] = 1 - (xx + zz);
            ret[6] = yz - wx;

            ret[8] = xz - wy;
            ret[9] = yz + wx;
            ret[10] = 1 - (xx + yy);

            // last column
            ret[3] = pos[0];
            ret[7] = pos[1];
            ret[11] = pos[2];

            // bottom row
            ret[12] = 0f;
            ret[13] = 0f;
            ret[14] = 0f;
            ret[15] = 1f;

            if (scale[0] != 1.0d || scale[1] != 1.0d || scale[2] != 1.0d) {
                ret[0] *= scale[0]; ret[4] *= scale[1]; ret[8] *= scale[2];
                ret[1] *= scale[0]; ret[5] *= scale[1]; ret[9] *= scale[2];
                ret[2] *= scale[0]; ret[6] *= scale[1]; ret[10] *= scale[2];
                ret[3] *= scale[0]; ret[7] *= scale[1]; ret[11] *= scale[2];
            }

            return ret;
        }

        public static float[] ComposeMatrix4(OMV.Vector3 pos, OMV.Quaternion rot, OMV.Vector3 scale) {
            float[] aPos = new float[3] { pos.X, pos.Y, pos.Z };
            float[] aRot = new float[4] { rot.X, rot.Y, rot.Z, rot.W };
            float[] aScale = new float[3] { scale.X, scale.Y, scale.Z };
            return Utilities.ComposeMatrix4(aPos, aRot, aScale);
        }
        public static float[] ComposeMatrix4(OMV.Vector3 pos, OMV.Quaternion rot) {
            float[] aPos = new float[3] { pos.X, pos.Y, pos.Z };
            float[] aRot = new float[4] { rot.X, rot.Y, rot.Z, rot.W };
            float[] aScale = new float[3] { 1.0f, 1.0f, 1.0f };
            return Utilities.ComposeMatrix4(aPos, aRot, aScale);
        }
    }
}
