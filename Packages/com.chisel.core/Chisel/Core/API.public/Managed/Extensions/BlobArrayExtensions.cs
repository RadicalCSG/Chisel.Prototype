using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Chisel.Core
{
    internal static class BlobArrayExtensions
    {
        public static bool Contains<T>(ref BlobArray<T> array, T value)
            where T : unmanaged
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(array[i], value))
                    return true;
            }
            return false;
        }

        public static unsafe T2[] ToArray<T1, T2>(ref BlobArray<T1> array)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            var newArray = new T2[array.Length];
            if (array.Length > 0)
            {
                if (sizeof(T1) != sizeof(T2)) throw new InvalidOperationException();
                fixed (T2* newArrayPtr = &newArray[0])
                {
                    UnsafeUtility.MemCpy(newArrayPtr, array.GetUnsafePtr(), array.Length * sizeof(T2));
                }
            }
            return newArray;
        }
    }
}
