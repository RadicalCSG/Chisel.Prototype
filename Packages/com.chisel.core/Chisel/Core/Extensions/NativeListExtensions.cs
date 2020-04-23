﻿using Chisel.Core.LowLevel.Unsafe;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Chisel.Core
{
    public static unsafe class NativeListExtensions
    {
        [BurstDiscard] static void LogRangeError() { Debug.LogError("Invalid range used in RemoveRange"); }

        public static void AddRange<T>(this NativeList<T> list, NativeListArray<T>.NativeList elements) where T : unmanaged
        {
            list.AddRange(elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, NativeListArray<T>.NativeList elements) where T : unmanaged
        {
            list.AddRangeNoResize(elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }

        public static void RemoveRange<T>(this NativeList<T> list, int index, int count) where T : unmanaged
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
