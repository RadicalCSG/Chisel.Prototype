using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Chisel.Core
{
    public static class NativeStreamExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteArray<T>([NoAlias] ref NativeStream.Writer nativeStream, [NoAlias] ref NativeArray<T> array, int length) where T : unmanaged
        {
            length = math.min(length, array.Length);
            nativeStream.Write(length);
            for (int i = 0; i < length; i++)
                nativeStream.Write(array[i]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Read<T>([NoAlias] ref NativeStream.Reader nativeStream, [NoAlias] ref T item) where T : unmanaged
        {
            item = nativeStream.Read<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadArrayAndEnsureSize<T>([NoAlias] ref NativeStream.Reader nativeStream, [NoAlias] ref NativeArray<T> array, out int length) where T : unmanaged
        {
            length = nativeStream.Read<int>();
            NativeCollectionHelpers.EnsureMinimumSize(ref array, length);
            for (int i = 0; i < length; i++)
                array[i] = nativeStream.Read<T>();
        }
    }
}
