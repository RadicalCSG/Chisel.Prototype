using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Chisel.Core
{
    public static class ChiselNativeArrayExtensions
    {
        public unsafe static void ClearValues<T>(this NativeArray<T> array) where T : unmanaged
        {
            if (array.Length == 0)
                return;
            UnsafeUtility.MemSet(array.GetUnsafePtr(), 0, array.Length * sizeof(T));
        }

        public unsafe static NativeArray<T> ToNativeArray<T>(this List<T> list, Allocator allocator) where T : unmanaged
        {
            var nativeList = new NativeArray<T>(list.Count, allocator);
            for (int i = 0; i < list.Count; i++)
                nativeList[i] = list[i];
            return nativeList;
        }
    }
}
