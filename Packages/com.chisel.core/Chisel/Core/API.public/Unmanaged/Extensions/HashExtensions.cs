using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    public static class HashExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint GetHashCode<T>([NoAlias, ReadOnly] ref T value) where T : unmanaged
        {
            fixed (T* valuePtr = &value)
            {
                return math.hash(valuePtr, sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint GetHashCode<T>([NoAlias, ReadOnly] ref ChiselBlobArray<T> value) where T : unmanaged
        {
            if (value.GetUnsafePtr() == null)
            {
                throw new NullReferenceException($"{nameof(value)} is null");
            }
            return math.hash(value.GetUnsafePtr(), sizeof(T) * value.Length);
        }
    }
}
