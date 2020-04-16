using System;
using Mathf = UnityEngine.Mathf;

namespace Chisel.Core
{
    public static class BoundsExtensions
    {
        public static bool IsValid(UnityEngine.Vector3 min, UnityEngine.Vector3 max)
        {
            const float kMinSize = 0.0001f;
            if (Mathf.Abs(max.x - min.x) < kMinSize ||
                Mathf.Abs(max.y - min.y) < kMinSize ||
                Mathf.Abs(max.z - min.z) < kMinSize ||
                float.IsInfinity(min.x) || float.IsInfinity(min.y) || float.IsInfinity(min.z) ||
                float.IsInfinity(max.x) || float.IsInfinity(max.y) || float.IsInfinity(max.z) ||
                float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(min.z) ||
                float.IsNaN(max.x) || float.IsNaN(max.y) || float.IsNaN(max.z))
                return false;
            return true;
        }

        public static bool Intersects(this AABB left, AABB right, double epsilon)
        {
            return  ((right.max.x - left.min.x) >= -epsilon) && ((left.max.x - right.min.x) >= -epsilon) &&
                    ((right.max.y - left.min.y) >= -epsilon) && ((left.max.y - right.min.y) >= -epsilon) &&
                    ((right.max.z - left.min.z) >= -epsilon) && ((left.max.z - right.min.z) >= -epsilon);
        }
    }
}
