﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
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
        public static void EnsureSizeAndClear<T>(ref NativeListArray<T> array, int exactSize, Allocator allocator = Allocator.Temp)
            where T : unmanaged
        {
            if (!array.IsCreated || array.Capacity < exactSize)
            {
                if (array.IsCreated) array.Dispose();
                array = new NativeListArray<T>(exactSize, allocator);
            } else
                array.ClearChildren();
            array.ResizeExact(exactSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureConstantSizeAndClear<T>(ref NativeListArray<T> array, int constantSize, Allocator allocator = Allocator.Temp)
            where T : unmanaged
        {
            if (!array.IsCreated)
                array = new NativeListArray<T>(constantSize, allocator);
            else
                array.ClearChildren();
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
            where T : struct
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
            where T : struct
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
            where A : struct, IEquatable<A>
            where B : struct
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
            where T : struct
        {
            if (!container.IsCreated)
                container = new NativeList<T>(allocator);
            else
                container.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureConstantSizeAndClear<T>(ref NativeList<T> container, int constantSize, Allocator allocator = Allocator.Temp)
            where T : struct
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
                array = new NativeBitArray(minimumSize, Allocator.Temp);
            } else
                array.Clear();
        }
    }
}
