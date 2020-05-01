using System;
using Unity.Mathematics;
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


        public static bool Intersects(this MinMaxAABB left, MinMaxAABB right, double epsilon)
        {
            return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
                   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
                   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
        }
    }
}
