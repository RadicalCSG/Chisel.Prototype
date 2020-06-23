using Chisel.Core.LowLevel.Unsafe;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Chisel.Core
{
    public static unsafe class ChiselNativeListExtensions
    {
        [BurstDiscard] static void LogRangeError() { Debug.LogError("Invalid range used in RemoveRange"); }

        public static void AddRange<T>(this NativeList<T> list, NativeListArray<T>.NativeList elements) where T : unmanaged
        {
            list.AddRange(elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, ref BlobArray<T> elements, int length) where T : unmanaged
        {
            if (length < 0 || length > elements.Length)
                throw new ArgumentOutOfRangeException("length");
            if (length == 0)
                return;
            list.AddRangeNoResize(elements.GetUnsafePtr(), length);
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, NativeListArray<T>.NativeList elements) where T : unmanaged
        {
            list.AddRangeNoResize(elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, NativeList<T> elements, int start, int count) where T : unmanaged
        {
            if (count > elements.Length)
                throw new ArgumentOutOfRangeException("count");
            if (start < 0 || start + count > elements.Length)
                throw new ArgumentOutOfRangeException("start");
            list.AddRangeNoResize((T*)elements.GetUnsafeReadOnlyPtr() + start, count);
        }

        public static void CopyFrom<T>(this NativeList<T> list, NativeList<T> elements, int start, int count) where T : unmanaged
        {
            if (count > elements.Length)
                throw new ArgumentOutOfRangeException("count");
            if (start < 0 || start + count > elements.Length)
                throw new ArgumentOutOfRangeException("start");

            list.Clear();
            list.AddRangeNoResize((T*)elements.GetUnsafeReadOnlyPtr() + start, count);
        }

        public static void CopyFrom<T>(this NativeArray<T> dstArray, NativeList<T> srcList, int start, int count) where T : unmanaged
        {
            if (count > srcList.Length || count > dstArray.Length)
                throw new ArgumentOutOfRangeException("count");
            if (start < 0 || start + count > srcList.Length)
                throw new ArgumentOutOfRangeException("start");

            var srcPtr  = (T*)srcList.GetUnsafeReadOnlyPtr() + start;
            var dstPtr  = (T*)dstArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dstPtr, srcPtr, count * UnsafeUtility.SizeOf<T>());
        }

        public static void CopyFrom<T>(this NativeArray<T> dstArray, NativeArray<T> srcArray, int start, int count) where T : unmanaged
        {
            if (count > srcArray.Length || count > dstArray.Length)
                throw new ArgumentOutOfRangeException("count");
            if (start < 0 || start + count > srcArray.Length)
                throw new ArgumentOutOfRangeException("start");

            var srcPtr  = (T*)srcArray.GetUnsafeReadOnlyPtr() + start;
            var dstPtr  = (T*)dstArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dstPtr, srcPtr, count * UnsafeUtility.SizeOf<T>());
        }

        public static void RemoveRange<T>(NativeList<T> list, int index, int count) where T : unmanaged
        {
            if (count == 0)
                return;
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
            {
                var listPtr = (T*)list.GetUnsafePtr();
                int size = sizeof(T);
                UnsafeUtility.MemMove(listPtr + index, listPtr + (index + count), (list.Length - (index + count)) * size);
            }
            list.Resize(list.Length - count, NativeArrayOptions.ClearMemory);
        }

        public static void RemoveRange<T>(NativeArray<T> array, int index, int count, ref int arrayLength) where T : unmanaged
        {
            if (count == 0)
                return;
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
            {
                var listPtr = (T*)array.GetUnsafePtr();
                int size = sizeof(T);
                UnsafeUtility.MemMove(listPtr + index, listPtr + (index + count), (arrayLength - (index + count)) * size);
            }
            arrayLength -= count;
        }

        public static void RemoveRange<T>(NativeArray<T> array, ref int arrayLength, int index, int count) where T : unmanaged
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

            if (index + count < arrayLength)
            {
                var listPtr = (T*)array.GetUnsafePtr();
                int size = sizeof(T);
                UnsafeUtility.MemMove(listPtr + index, listPtr + (index + count), (arrayLength - (index + count)) * size);
            }
            arrayLength -= count;
        }

        public static void RemoveAt<T>(this NativeList<T> list, int index) where T : unmanaged
        {
            RemoveRange(list, index, 1);
        }

        
        public static void RemoveRange<T>(this NativeListArray<T>.NativeList list, int index, int count) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_Safety);
#endif
            if (count == 0)
                return;
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
            {
                var listPtr = (T*)list.GetUnsafePtr();
                int size = sizeof(T);
                UnsafeUtility.MemMove(listPtr + index, listPtr + (index + count), (list.Length - (index + count)) * size);
            }
            list.Resize(list.Length - count, NativeArrayOptions.ClearMemory);
        }
        /*
        public static void RemoveAt<T>(this NativeListArray<T>.NativeList list, int index) 
            where T : unmanaged, IEquatable<T>
        {
            RemoveRange(list, index, 1);
        }

        public static void Remove<T>(this NativeListArray<T>.NativeList list, T item) 
            where T : unmanaged, IEquatable<T>
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index].Equals(item))
                {
                    RemoveRange(list, index, 1);
                    return;
                }
            }
        }
        */
        public static void Remove(this NativeListArray<int>.NativeList list, int item) 
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index] == item)
                {
                    RemoveRange(list, index, 1);
                    return;
                }
            }
        }

        public static void Remove(this NativeListArray<Edge>.NativeList list, Edge item) 
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index].index1 == item.index1 &&
                    list[index].index2 == item.index2)
                {
                    RemoveRange(list, index, 1);
                    return;
                }
            }
        }
        /*
        public static void Remove<T>(this NativeList<T> list, T item) 
            where T : unmanaged, IEquatable<T> 
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index].Equals(item))
                {
                    RemoveRange(list, index, 1);
                    return;
                }
            }
        }
        */
        public static void Remove(this NativeList<Edge> list, Edge item)
        {
            for (int index = 0; index < list.Length; index++)
            {
                if (list[index].index1 == item.index1 &&
                    list[index].index2 == item.index2)
                {
                    RemoveRange(list, index, 1);
                    return;
                }
            }
        }

        public static bool Contains<T>(ref BlobArray<T> array, T value)
            where T : struct, IEquatable<T>
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Equals(value))
                    return true;
            }
            return false;
        }

        public static bool Contains<T>(this NativeList<T> array, T value)
            where T : struct, IEquatable<T>
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Equals(value))
                    return true;
            }
            return false;
        }

        public static bool Contains<T>(this NativeListArray<T>.NativeList array, T item)
            where T : unmanaged, IEquatable<T>
        {
            for (int index = 0; index < array.Length; index++)
            {
                if (array[index].Equals(item))
                    return true;
            }
            return false;
        }

        public static bool Contains(this NativeListArray<Edge>.NativeList array, Edge item)
        {
            for (int index = 0; index < array.Length; index++)
            {
                if (array[index].index1 == item.index1 &&
                    array[index].index2 == item.index2)
                    return true;
            }
            return false;
        }

        public static void* GetUnsafePtr<T>(this NativeListArray<T>.NativeList list) 
            where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_Safety);
#endif
            return list.m_ListData->Ptr;
        }

        public static NativeList<T> ToNativeList<T>(this List<T> list, Allocator allocator) where T : unmanaged
        {
            var nativeList = new NativeList<T>(list.Count, allocator);
            for (int i = 0; i < list.Count; i++)
                nativeList.AddNoResize(list[i]);
            return nativeList;
        }

        public static NativeList<T> ToNativeList<T>(this HashSet<T> hashSet, Allocator allocator) where T : unmanaged
        {
            var nativeList = new NativeList<T>(hashSet.Count, allocator);
            foreach(var item in hashSet)
                nativeList.AddNoResize(item);
            return nativeList;
        }
    }
}
