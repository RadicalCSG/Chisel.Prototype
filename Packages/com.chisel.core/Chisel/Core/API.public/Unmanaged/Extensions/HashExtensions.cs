using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using WriteOnlyAttribute = Unity.Collections.WriteOnlyAttribute;

namespace Chisel.Core
{
    public static class HashExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint GetHashCode<T>([NoAlias, ReadOnly] ref T value) where T : unmanaged
        {
            return MathExtensions.Hash(ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetHashCode<T>([NoAlias, ReadOnly] ref ChiselBlobArray<T> value) where T : unmanaged
        {
            return MathExtensions.Hash(ref value);
        }
    }
}
