using System.Runtime.CompilerServices;
using Unity.Mathematics;

// Note: Based on Unity.Entities.MinMaxAABB
namespace Chisel.Core
{
    [System.Serializable]
    public struct ChiselAABB
    {
        public float3 Min;
        public float3 Max;

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return this.Equals(Empty); }
        }

        public static ChiselAABB Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new ChiselAABB { Min = math.float3(float.PositiveInfinity), Max = math.float3(float.NegativeInfinity) }; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encapsulate(ChiselAABB aabb)
        {
            Min = math.min(Min, aabb.Min);
            Max = math.max(Max, aabb.Max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encapsulate(float3 point)
        {
            Min = math.min(Min, point);
            Max = math.max(Max, point);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(ChiselAABB other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }
    }
}
