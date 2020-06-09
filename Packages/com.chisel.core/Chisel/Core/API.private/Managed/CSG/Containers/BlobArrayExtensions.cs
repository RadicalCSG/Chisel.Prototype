using System;
using System.Collections.Generic;
using Unity.Entities;

namespace Chisel.Core
{
    internal static class BlobArrayExtensions
    {
        public static bool Contains<T>(ref BlobArray<T> array, T value)
            where T : struct
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(array[i], value))
                    return true;
            }
            return false;
        }
    }
}
