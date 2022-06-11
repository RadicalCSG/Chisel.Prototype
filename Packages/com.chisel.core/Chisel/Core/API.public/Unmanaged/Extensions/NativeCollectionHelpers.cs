using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Chisel.Core
{
    public static class NativeCollectionHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureMinimumSizeAndClear<T>(ref NativeArray<T> array, int minimumSize, Allocator allocator = Allocator.Temp)
            where T: unmanaged
        {                    
            if (!array.IsCreated || array.Length < minimumSize)
            {
                if (array.IsCreated) array.Dispose();
                array = new NativeArray<T>(minimumSize, allocator);
            } else
                array.ClearValues(minimumSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureMinimumSize<T>(ref NativeArray<T> array, int minimumSize, Allocator allocator = Allocator.Temp)
            where T : struct
        {
            if (!array.IsCreated || array.Length < minimumSize)
            {
                if (array.IsCreated) array.Dispose();
                array = new NativeArray<T>(minimumSize, allocator);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureSizeAndClear<T>(ref NativeList<UnsafeList<T>> list, int exactSize, Allocator allocator = Allocator.Temp)
            where T : unmanaged
        {
            if (!list.IsCreated)
                list = new NativeList<UnsafeList<T>>(exactSize, allocator);
            list.Clear();
            list.Resize(exactSize, NativeArrayOptions.ClearMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCapacityAndClear(ref HashedVertices hashedVertices, int desiredCapacity, Allocator allocator = Allocator.Temp)
        {
            if (!hashedVertices.IsCreated)
            {
                hashedVertices = new HashedVertices(desiredCapacity, allocator);
            } else
            {
                if (hashedVertices.Capacity < desiredCapacity)
                {
                    hashedVertices.Dispose();
                    hashedVertices = new HashedVertices(desiredCapacity, Allocator.Temp);
                } else
                    hashedVertices.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCapacityAndClear<T>(ref NativeList<T> container, int desiredCapacity, Allocator allocator = Allocator.Temp)
            where T : unmanaged
        {
            if (!container.IsCreated)
            {
                container = new NativeList<T>(desiredCapacity, allocator);
            } else
            {
                container.Clear();
                if (container.Capacity < desiredCapacity)
                    container.Capacity = desiredCapacity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureSizeAndClear<T>(ref NativeList<T> container, int desiredSize, Allocator allocator = Allocator.Temp)
            where T : unmanaged
        {
            if (!container.IsCreated)
            {
                container = new NativeList<T>(desiredSize, allocator);
            } else
            {
                container.Clear();
                if (container.Capacity < desiredSize)
                    container.Capacity = desiredSize;
            }
            container.ResizeUninitialized(desiredSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCapacityAndClear<A,B>(ref NativeHashMap<A,B> container, int desiredCapacity, Allocator allocator = Allocator.Temp)
            where A : unmanaged, IEquatable<A>
            where B : unmanaged
        {
            if (!container.IsCreated)
            {
                container = new NativeHashMap<A, B>(desiredCapacity, allocator);
            } else
            {
                container.Clear();
                if (container.Capacity < desiredCapacity)
                    container.Capacity = desiredCapacity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCreatedAndClear<T>(ref NativeList<T> container, Allocator allocator = Allocator.Temp)
            where T : unmanaged
        {
            if (!container.IsCreated)
                container = new NativeList<T>(allocator);
            else
                container.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureConstantSizeAndClear<T>(ref NativeList<T> container, int constantSize, Allocator allocator = Allocator.Temp)
            where T : unmanaged
        {
            if (!container.IsCreated)
                container = new NativeList<T>(constantSize, allocator);
            else
                container.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureMinimumSizeAndClear(ref NativeBitArray array, int minimumSize, Allocator allocator = Allocator.Temp)
        {
            if (!array.IsCreated || array.Length < minimumSize)
            {
                if (array.IsCreated) array.Dispose();
                array = new NativeBitArray(minimumSize, allocator);
            } else
            {
                array.SetBits(0, false, array.Length);
                //array.Clear(); // <= does not clear bits as you'd expect, is it broken?
            }
        }
    }
}
