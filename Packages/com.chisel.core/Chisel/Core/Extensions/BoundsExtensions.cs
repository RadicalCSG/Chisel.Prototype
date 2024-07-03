using System.Runtime.CompilerServices;

using Unity.Entities;
using Unity.Mathematics;

using UnityEngine;

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
		public static bool Intersects(this AABB left, AABB right, double epsilon)
		{
			return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
				   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
				   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Intersects(this AABB left, MinMaxAABB right, double epsilon)
		{
			return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
				   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
				   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Intersects(this MinMaxAABB left, AABB right, double epsilon)
		{
			return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
				   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
				   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Intersects(this MinMaxAABB left, MinMaxAABB right, double epsilon)
        {
            return ((right.Max.x - left.Min.x) >= -epsilon) && ((left.Max.x - right.Min.x) >= -epsilon) &&
                   ((right.Max.y - left.Min.y) >= -epsilon) && ((left.Max.y - right.Min.y) >= -epsilon) &&
                   ((right.Max.z - left.Min.z) >= -epsilon) && ((left.Max.z - right.Min.z) >= -epsilon);
        }

        public static MinMaxAABB Create(float4x4 transformation, float3[] vertices)
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
			return new MinMaxAABB { Min = min, Max = max };
		}

        public static MinMaxAABB Create(ref BlobArray<float3> vertices, float4x4 transformation)
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
            return new MinMaxAABB { Min = min, Max = max };
        }


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsEmpty(this MinMaxAABB self) { return self.Equals(Empty); }
		readonly static MinMaxAABB Empty = new() { Min = math.float3(float.NegativeInfinity), Max = math.float3(float.PositiveInfinity) };

	
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Encapsulate(this MinMaxAABB self, MinMaxAABB other)
		{
            var min = math.min(self.Min, other.Min);
			var max = math.max(self.Max, other.Max);
            self.Min = min;
            self.Max = max;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Encapsulate(this MinMaxAABB self, float3 point)
		{
			var min = math.min(self.Min, point);
			var max = math.max(self.Max, point);
			self.Min = min;
			self.Max = max;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Encapsulate(this AABB self, AABB other)
		{
			var min = math.min(self.Min, other.Min);
			var max = math.max(self.Max, other.Max);
			self.Center = (min + max) * 0.5f;
			self.Extents = (max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Encapsulate(this AABB self, float3 point)
		{
			var min = math.min(self.Min, point);
			var max = math.max(self.Max, point);
			self.Center = (min + max) * 0.5f;
			self.Extents = (max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MinMaxAABB CreateAABB(float3 min, float3 max) { return new AABB { Center = (max + min) * 0.5f, Extents = (max - min) * 0.5f }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MinMaxAABB ToMinMaxAABB(this AABB bounds) { return new MinMaxAABB { Min = bounds.Min, Max = bounds.Max }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MinMaxAABB ToMinMaxAABB(this Bounds bounds) { return new MinMaxAABB { Min = bounds.min, Max = bounds.max }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Bounds ToBounds(this MinMaxAABB aabb) { return new Bounds { center = (aabb.Max + aabb.Min) * 0.5f, extents = (aabb.Max - aabb.Min) * 0.5f }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static AABB ToAABB(this MinMaxAABB aabb) { return new AABB { Center = (aabb.Max + aabb.Min) * 0.5f, Extents = (aabb.Max - aabb.Min) * 0.5f }; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetCenter(this MinMaxAABB aabb, float3 center) 
        {
            var min = aabb.Min;
			var max = aabb.Max;
            var extents = math.abs(max - min) * 0.5f;
			aabb.Min = center - extents;
			aabb.Max = center + extents;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetExtents(this MinMaxAABB aabb, float3 extents)
		{
			var min = aabb.Min;
			var max = aabb.Max;
			var center = math.abs(max + min) * 0.5f;
			aabb.Min = center - extents;
			aabb.Max = center + extents;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMin(this MinMaxAABB aabb, float3 min)
		{
			aabb.Min = min;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMax(this MinMaxAABB aabb, float3 max)
		{
			aabb.Max = max;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMinMax(this MinMaxAABB aabb, float3 min, float3 max)
		{
			aabb.Min = min;
			aabb.Max = max;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SetCenter(this AABB aabb, float3 center) { aabb.Center = center; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SetExtents(this AABB aabb, float3 extents) { aabb.Extents = extents; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMin(this AABB aabb, float3 min)
		{
			var max = aabb.Max;
			aabb.Extents = math.abs(max + min) * 0.5f;
			aabb.Center = math.abs(max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMax(this AABB aabb, float3 max)
		{
			var min = aabb.Min;
			aabb.Extents = math.abs(max + min) * 0.5f;
			aabb.Center = math.abs(max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMinMax(this AABB aabb, float3 min, float3 max)
		{
			aabb.Extents = math.abs(max + min) * 0.5f;
			aabb.Center = math.abs(max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SetCenter(this Bounds aabb, float3 center) { aabb.center = center; }
		[MethodImpl(MethodImplOptions.AggressiveInlining)] public static void SetExtents(this Bounds aabb, float3 extents) { aabb.extents = extents; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMin(this Bounds aabb, float3 min)
		{
			var max = (float3)aabb.max;
			aabb.extents = math.abs(max + min) * 0.5f;
			aabb.center = math.abs(max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMax(this Bounds aabb, float3 max)
		{
			var min = (float3)aabb.min;
			aabb.extents = math.abs(max + min) * 0.5f;
			aabb.center = math.abs(max - min) * 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetMinMax(this Bounds aabb, float3 min, float3 max)
		{
			aabb.extents = math.abs(max + min) * 0.5f;
			aabb.center = math.abs(max - min) * 0.5f;
		}
    }
}
