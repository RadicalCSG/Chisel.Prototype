using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Chisel.Core
{
    public static class BoundsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(float3 min, float3 max)
        {
            const float kMinSize = 0.0001f;
            if (math.abs(max.x - min.x) < kMinSize ||
                math.abs(max.y - min.y) < kMinSize ||
                math.abs(max.z - min.z) < kMinSize ||
                !math.isfinite(min.x) || !math.isfinite(min.y) || !math.isfinite(min.z) ||
                !math.isfinite(max.x) || !math.isfinite(max.y) || !math.isfinite(max.z) ||
                math.isnan(min.x) || math.isnan(min.y) || math.isnan(min.z) ||
                math.isnan(max.x) || math.isnan(max.y) || math.isnan(max.z))
                return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Intersects(this ChiselAABB left, ChiselAABB right, double epsilon)
        {
            return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
                   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
                   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
        }

        public static ChiselAABB Create(float4x4 transformation, float3[] vertices)
        {
            if (vertices == null ||
                vertices.Length == 0)
                return default;

            var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < vertices.Length; i++)
            {
                var vert = math.mul(transformation, new float4(vertices[i], 1)).xyz;
                min = math.min(min, vert);
                max = math.max(max, vert);
            }
            return new ChiselAABB { Min = min, Max = max };
        }

        public static ChiselAABB Create(ref ChiselBlobArray<float3> vertices, float4x4 transformation)
        {
            if (vertices.Length == 0)
                return default;

            var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < vertices.Length; i++)
            {
                var vert = math.mul(transformation, new float4(vertices[i], 1)).xyz;
                min = math.min(min, vert);
                max = math.max(max, vert);
            }
            return new ChiselAABB { Min = min, Max = max };
        }
    }
}
