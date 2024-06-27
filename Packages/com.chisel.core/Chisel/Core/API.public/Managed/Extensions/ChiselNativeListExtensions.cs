using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Chisel.Core
{
    public static unsafe class ChiselNativeListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(ref this UnsafeList<T> src, T[] dst, int count)
            where T : unmanaged
        {
            if (dst == null)
                throw new NullReferenceException();
            if (count < 0)
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (count > src.Length)
                throw new ArgumentOutOfRangeException($" {nameof(count)} must be within bounds of array ({src.Length}).", nameof(count));
            if (count > dst.Length)
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be within bounds of array ({dst.Length}).", nameof(count));
            if (count == 0)
                return;
            fixed (T* dstPtr = &dst[0])
            {
                UnsafeUtility.MemCpy(src.Ptr, dstPtr, count * sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] ToArray<T>(ref this UnsafeList<T> input) 
            where T : unmanaged
        {
            var array = new T[input.Length];
            if (input.Length == 0)
                return array;
            fixed(T* dstPtr = &array[0])
            {
                var srcPtr = input.Ptr;
                var byteCount = input.Length * UnsafeUtility.SizeOf<T>(); // can have overflow
                CheckPositive(byteCount);

                UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
            }
            return array;
        }


        [BurstDiscard] static void LogRangeError() { Debug.LogError("Invalid range used in RemoveRange"); }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CheckPositive(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", $"length ({length}) needs to be a positive number");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CheckLengthInRange(int length, int range)
        {
            if (length < 0 || length > range)
            {
                throw new ArgumentOutOfRangeException("length", $"{length} {range}");
            }
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CheckIndexInRangeExc(int index, int length)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException("index");
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void CheckIndexInRangeInc(int index, int length)
        {
            if (index < 0 || index > length)
                throw new ArgumentOutOfRangeException("index");
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckCreated(bool isCreated)
        {
            if (!isCreated)
                throw new ArgumentException("isCreated");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(ref this UnsafeList<T> list, ref UnsafeList<T> elements) where T : unmanaged
        {
            CheckCreated(elements.Ptr != null && elements.IsCreated);
            if (elements.Length == 0)
                return;
            CheckCreated(list.Ptr != null && list.IsCreated);
            CheckLengthInRange(elements.Length, list.Capacity);
            list.AddRange(elements.Ptr, elements.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(this NativeList<T> list, NativeArray<T> elements) where T : unmanaged
        {
            CheckCreated(list.IsCreated);
            if (elements.Length == 0)
                return;
            list.AddRange(elements.GetUnsafePtr(), elements.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(ref this UnsafeList<T> list, ref ChiselBlobArray<T> elements) where T : unmanaged
        {
            if (elements.Length == 0)
                return;
            CheckCreated(list.IsCreated);
            list.AddRange(elements.GetUnsafePtr(), elements.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(this NativeList<T> list, ref UnsafeList<T> elements) where T : unmanaged
        {
            CheckCreated(elements.Ptr != null && elements.IsCreated);
            if (elements.Length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(elements.Length, list.Length);
            list.AddRange(elements.Ptr, elements.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(this NativeList<T> list, ref ChiselBlobArray<T> elements) where T : unmanaged
        {
            if (elements.Length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(elements.Length, list.Length);
            list.AddRange(elements.GetUnsafePtr(), elements.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(this NativeList<T> list, ref ChiselBlobArray<T> elements, int length) where T : unmanaged
        {
            if (length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(length, elements.Length);
            list.AddRange(elements.GetUnsafePtr(), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(ref this UnsafeList<T> list, UnsafeList<T> elements, int length) where T : unmanaged
        {
            if (length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(length, list.Capacity);
            list.AddRangeNoResize(elements.Ptr, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(this NativeList<T> list, UnsafeList<T> elements) where T : unmanaged
        {
            if (elements.Length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(elements.Length, list.Capacity);
            list.AddRangeNoResize(elements.Ptr, elements.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(ref this UnsafeList<T> list, NativeArray<T> elements, int length) where T : unmanaged
        {
            if (length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(length, list.Capacity);
            list.AddRangeNoResize(elements.GetUnsafePtr(), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(this NativeList<T> list, ref ChiselBlobArray<T> elements, int length) where T : unmanaged
        {
            if (length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(length, list.Capacity);
            list.AddRangeNoResize(elements.GetUnsafePtr(), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(this NativeList<T> list, NativeArray<T> elements, int length) where T : unmanaged
        {
            if (length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(length, list.Capacity);
            list.AddRangeNoResize(elements.GetUnsafePtr(), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(this NativeList<T> list, ref ChiselBlobArray<T> elements) where T : unmanaged
        {
            if (elements.Length == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckLengthInRange(elements.Length, list.Capacity);
            list.AddRangeNoResize(elements.GetUnsafePtr(), elements.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash<T>(this NativeList<T> list, int length) where T : unmanaged
        {
            CheckCreated(list.IsCreated);
            CheckLengthInRange(length, list.Length);
            if (length == 0)
                return 0;
            return math.hash(list.GetUnsafeReadOnlyPtr(), length * sizeof(T));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRange<T>(this NativeList<T> list, T[] elements) where T : unmanaged
        {
            if (elements.Length == 0)
                return;
            CheckCreated(list.IsCreated);
            foreach (var item in elements)
                list.Add(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(this NativeList<T> list, T[] elements) where T : unmanaged
        {
            if (elements.Length == 0)
                return;
            CheckCreated(list.IsCreated);
            foreach (var item in elements)
                list.AddNoResize(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(this NativeList<T> list, List<T> elements) where T : unmanaged
        {
            if (elements.Count == 0)
                return;
            CheckCreated(list.IsCreated);
            foreach (var item in elements)
                list.AddNoResize(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddRangeNoResize<T>(this NativeList<T> list, NativeList<T> elements, int start, int count) where T : unmanaged
        {
            if (count == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckCreated(elements.IsCreated);
            CheckLengthInRange(count, list.Capacity);
            CheckIndexInRangeInc(start, elements.Length - count);
            list.AddRangeNoResize((T*)elements.GetUnsafeReadOnlyPtr() + start, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this NativeList<T> list, NativeList<T> elements, int start, int count) where T : unmanaged
        {
            if (count == 0)
                return;
            CheckCreated(list.IsCreated);
            CheckCreated(elements.IsCreated);
            CheckLengthInRange(count, list.Capacity);
            CheckIndexInRangeInc(start, elements.Length - count);

            list.Clear();
            list.AddRangeNoResize((T*)elements.GetUnsafeReadOnlyPtr() + start, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(ref this UnsafeList<T> list, NativeList<T> elements, int start, int count) where T : unmanaged
        {
            if (count == 0)
                return;
            CheckCreated(elements.IsCreated);
            CheckLengthInRange(count, list.Capacity);
            CheckIndexInRangeInc(start, elements.Length - count);

            list.Clear();
            list.AddRangeNoResize((T*)elements.GetUnsafeReadOnlyPtr() + start, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this NativeArray<T> dstArray, NativeList<T> srcList, int start, int count) where T : unmanaged
        {
            if (count == 0)
                return;
            CheckCreated(dstArray.IsCreated);
            CheckCreated(srcList.IsCreated);
            CheckLengthInRange(count, srcList.Length);
            CheckLengthInRange(count, dstArray.Length);
            CheckIndexInRangeInc(start, srcList.Length - count);
            
            var byteCount   = count * UnsafeUtility.SizeOf<T>(); // can have overflow
            CheckPositive(byteCount);

            var srcPtr      = (T*)srcList.GetUnsafeReadOnlyPtr() + start;
            var dstPtr      = (T*)dstArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this NativeArray<T> dstArray, NativeArray<T> srcArray, int srcIndex, int srcCount) where T : unmanaged
        {
            if (srcCount == 0)
                return;
            CheckCreated(dstArray.IsCreated);
            CheckCreated(srcArray.IsCreated);
            CheckLengthInRange(srcCount, srcArray.Length);
            CheckLengthInRange(srcCount, dstArray.Length);
            CheckIndexInRangeInc(srcIndex, srcArray.Length - srcCount);

            var byteCount   = srcCount * UnsafeUtility.SizeOf<T>(); // can have overflow
            CheckPositive(byteCount);

            var srcPtr      = (T*)srcArray.GetUnsafeReadOnlyPtr() + srcIndex;
            var dstPtr      = (T*)dstArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this NativeArray<T> dstArray, UnsafeList<T> srcList, int srcIndex, int srcCount) where T : unmanaged
        {
            if (srcCount == 0)
                return;
            CheckCreated(dstArray.IsCreated);
            CheckCreated(srcList.IsCreated);
            CheckLengthInRange(srcCount, srcList.Length);
            CheckLengthInRange(srcCount, dstArray.Length);
            CheckIndexInRangeInc(srcIndex, srcList.Length - srcCount);

            var byteCount   = srcCount * UnsafeUtility.SizeOf<T>(); // can have overflow
            CheckPositive(byteCount);

            var srcPtr      = (T*)srcList.Ptr + srcIndex;
            var dstPtr      = (T*)dstArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]//*
        public static void CopyFrom<T>(this NativeArray<T> dstArray, int dstIndex, ref ChiselBlobArray<T> srcArray, int srcIndex, int srcCount) where T : unmanaged
        {
            if (srcCount <= 0)
                return;
            CheckCreated(dstArray.IsCreated);
            CheckLengthInRange(srcCount, srcArray.Length);
            CheckLengthInRange(srcCount, dstArray.Length);
            CheckIndexInRangeInc(dstIndex, dstArray.Length - srcCount);
            CheckIndexInRangeInc(srcIndex, srcArray.Length - srcCount);

            var byteCount   = srcCount * UnsafeUtility.SizeOf<T>(); // can have overflow
            CheckPositive(byteCount);

            var srcPtr      = (T*)srcArray.GetUnsafePtr() + srcIndex;
            var dstPtr      = (T*)dstArray.GetUnsafePtr() + dstIndex;

            UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this NativeList<T> dstList, int dstIndex, ref ChiselBlobArray<T> srcArray, int srcIndex, int srcCount) where T : unmanaged
        {
            if (srcCount == 0)
                return;
            CheckCreated(dstList.IsCreated);
            CheckLengthInRange(srcCount, srcArray.Length);
            CheckLengthInRange(srcCount, dstList.Length);
            CheckIndexInRangeInc(dstIndex, dstList.Length - srcCount);
            CheckIndexInRangeInc(srcIndex, srcArray.Length - srcCount);

            var byteCount   = srcCount * UnsafeUtility.SizeOf<T>(); // can have overflow
            CheckPositive(byteCount);

            var srcPtr      = (T*)srcArray.GetUnsafePtr() + srcIndex;
            var dstPtr      = (T*)dstList.GetUnsafePtr() + dstIndex;

            UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyFrom<T>(this NativeSlice<T> dstSlice, int dstIndex, ref ChiselBlobArray<T> srcArray, int srcIndex, int srcCount) where T : unmanaged
        {
            if (srcCount == 0)
                return;
            CheckLengthInRange(srcCount, srcArray.Length);
            CheckLengthInRange(srcCount, dstSlice.Length);
            CheckIndexInRangeInc(dstIndex, dstSlice.Length - srcCount);
            CheckIndexInRangeInc(srcIndex, srcArray.Length - srcCount);

            var byteCount   = srcCount * UnsafeUtility.SizeOf<T>(); // can have overflow
            CheckPositive(byteCount);

            var srcPtr      = (T*)srcArray.GetUnsafePtr() + srcIndex;
            var dstPtr      = (T*)dstSlice.GetUnsafePtr() + dstIndex;

            UnsafeUtility.MemCpy(dstPtr, srcPtr, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertAt<T>(this NativeList<T> list, int index, T item) where T : unmanaged
        {
            CheckCreated(list.IsCreated);
            if (index < 0 || index > list.Length)
            {
                LogRangeError();
                return;
            }
            if (index == list.Length)
            {
                list.Add(item);
                return;
            }
            list.Resize(list.Length + 1, NativeArrayOptions.ClearMemory);
            list.MemMove(index + 1, index, list.Length - (index + 1));
            list[index] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertAt<T>(ref this UnsafeList<T> list, int index, T item) where T : unmanaged
        {
            CheckCreated(list.IsCreated);
            if (index < 0 || index > list.Length)
            {
                LogRangeError();
                return;
            }
            if (index == list.Length)
            {
                list.Add(item);
                return;
            }

            list.Resize(list.Length + 1, NativeArrayOptions.ClearMemory);
            list.MemMove(index + 1, index, list.Length - (index + 1));
            list[index] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveRange<T>(ref this UnsafeList<T> list, int index, int count) where T : unmanaged
        {
            if (count == 0)
                return;

            CheckCreated(list.IsCreated);
            if (index < 0 || index + count > list.Length)
            {
                LogRangeError();
                return;
            }
            if (index == 0 && count == list.Length)
            {
                list.Clear();
                return;
            }

            if (index + count < list.Length)
                list.MemMove(index, index + count, list.Length - (index + count));
            list.Resize(list.Length - count, NativeArrayOptions.ClearMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveRange<T>(this NativeList<T> list, int index, int count) where T : unmanaged
        {
            if (count == 0)
                return;

            CheckCreated(list.IsCreated);
            if (index < 0 || index + count > list.Length)
            {
                LogRangeError();
                return;
            }
            if (index == 0 && count == list.Length)
            {
                list.Clear();
                return;
            }

            if (index + count < list.Length)
                list.MemMove(index, index + count, list.Length - (index + count));
            list.Resize(list.Length - count, NativeArrayOptions.ClearMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveRange<T>(this NativeArray<T> array, int index, int count, ref int arrayLength) where T : unmanaged
        {
            if (count == 0)
                return;
            CheckCreated(array.IsCreated);
            if (arrayLength > array.Length)
            {
                LogRangeError();
                return;
            }
            if (index < 0 || index + count > arrayLength)
            {
                LogRangeError();
                return;
            }
            if (index == 0 && count >= arrayLength)
            {
                arrayLength = 0;
                return;
            }

            if (index + count < arrayLength)
                array.MemMove(index, index + count, arrayLength - (index + count));
            arrayLength -= count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveRange<T>(this NativeArray<T> array, ref int arrayLength, int index, int count) where T : unmanaged
        {
            if (count == 0)
                return;
            if (index < 0 || index + count > arrayLength)
            {
                LogRangeError();
                return;
            }
            if (index == 0 && count == arrayLength)
            {
                arrayLength = 0;
                return;
            }

            CheckCreated(array.IsCreated);
            if (index + count < arrayLength)
                array.MemMove(index, index + count, arrayLength - (index + count));
            arrayLength -= count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveAt<T>(ref this UnsafeList<T> list, int index) where T : unmanaged
        {
            list.RemoveRange(index, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveAt<T>(this NativeList<T> list, int index) where T : unmanaged
        {
            list.RemoveRange(index, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Remove<T>(ref this UnsafeList<T> list, T item)
            where T : unmanaged
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index].Equals(item))
                {
                    list.RemoveRange(index, 1);
                    return true; 
                }
            }
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Remove(ref this UnsafeList<Edge> list, Edge item)
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index].index1 == item.index1 &&
                    list[index].index2 == item.index2)
                {
                    list.RemoveRange(index, 1);
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Remove(ref this UnsafeList<int> list, int item)
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index] == item)
                {
                    list.RemoveRange(index, 1);
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Remove<T>(this NativeList<T> list, T item) 
            where T : unmanaged
        { 
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index].Equals(item))
                {
                    RemoveRange(list, index, 1);
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Remove(this NativeList<Edge> list, Edge item)
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index].index1 == item.index1 &&
                    list[index].index2 == item.index2)
                {
                    list.RemoveRange(index, 1);
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains<T>(ref this ChiselBlobArray<T> array, T value)
            where T : unmanaged, IEquatable<T>
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Equals(value))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains<T>(this NativeList<T> array, T value)
            where T : unmanaged, IEquatable<T>
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Equals(value))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<T> ToNativeList<T>(this List<T> list, Allocator allocator) where T : unmanaged
        {
            var nativeList = new NativeList<T>(list.Count, allocator);
            for (int i = 0; i < list.Count; i++)
                nativeList.AddNoResize(list[i]);
            return nativeList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<T> ToNativeList<T>(this HashSet<T> hashSet, Allocator allocator) where T : unmanaged
        {
            var nativeList = new NativeList<T>(hashSet.Count, allocator);
            foreach(var item in hashSet)
                nativeList.AddNoResize(item);
            return nativeList;
        }
    }
}
