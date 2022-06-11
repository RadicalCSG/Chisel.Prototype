using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;

namespace Chisel.Core
{
    public static class HashExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint GetHashCode<T>(ref T value) where T : unmanaged
        {
            fixed (T* valuePtr = &value)
            {
                return math.hash(valuePtr, sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint GetHashCode<T>(ChiselBlobArray<T> value) where T : unmanaged
        {
            return math.hash(value.GetUnsafePtr(), sizeof(T) * value.Length);
        }
    }
}
