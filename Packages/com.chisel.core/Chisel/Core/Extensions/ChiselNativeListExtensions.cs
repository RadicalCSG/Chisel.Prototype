using Chisel.Core.LowLevel.Unsafe;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    public static unsafe class ChiselNativeListExtensions
    {
        public static JobHandle ScheduleEnsureCapacity<T1,T2>(NativeList<T1> container, NativeList<T2> lengthList, int multiplier, JobHandle dependency)
            where T1 : struct
            where T2 : struct
        {
            var jobData = new EnsureCapacityListJob<T1> { List = lengthList.GetUnsafeList(), multiplier = multiplier, Container = container };
            return jobData.Schedule(dependency);
        }
        public static JobHandle ScheduleEnsureCapacity<T1, T2>(NativeList<T1> container, NativeList<T2> lengthList, JobHandle dependency)
            where T1 : struct
            where T2 : struct
        {
            var jobData = new EnsureCapacityListJob<T1> { List = lengthList.GetUnsafeList(), multiplier = 1, Container = container };
            return jobData.Schedule(dependency);
        }


        [BurstCompile]
        struct EnsureCapacityListJob<T> : IJob
            where T : struct
        {
            public NativeList<T> Container;
            public int multiplier;
            [ReadOnly]
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList* List;

            public void Execute()
            {
                Container.Clear();
                multiplier *= List->Length;
                if (Container.Capacity < multiplier)
                    Container.Capacity = multiplier;
            }
        }
        
        public static JobHandle ScheduleEnsureCapacity<T1,T2>(NativeList<T1> container, NativeArray<T2> lengthArray, JobHandle dependency)
            where T1 : struct
            where T2 : struct
        {
            var jobData = new EnsureCapacityArrayJob<T1,T2> { LengthArray = lengthArray, Container = container };
            return jobData.Schedule(dependency);
        }


        [BurstCompile]
        struct EnsureCapacityArrayJob<T1,T2> : IJob
            where T1 : struct
            where T2 : struct
        {
            public NativeList<T1> Container;

            public NativeArray<T2> LengthArray;

            public void Execute()
            {
                Container.Clear();
                if (Container.Capacity < LengthArray.Length)
                    Container.Capacity = LengthArray.Length;
            }
        }

        [BurstDiscard] static void LogRangeError() { Debug.LogError("Invalid range used in RemoveRange"); }

        public static void AddRange<T>(this NativeList<T> list, NativeListArray<T>.NativeList elements) where T : unmanaged
        {
            list.AddRange(elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckLengthInRange(int length, int range)
        {
            if (length < 0 || length > range)
                throw new ArgumentOutOfRangeException("length");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckIndexInRangeExc(int index, int length)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException("index");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckIndexInRangeInc(int index, int length)
        {
            if (index < 0 || index > length)
                throw new ArgumentOutOfRangeException("index");
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, ref BlobArray<T> elements, int length) where T : unmanaged
        {
            CheckLengthInRange(length, elements.Length);
            if (length == 0)
                return;
            list.AddRangeNoResize(elements.GetUnsafePtr(), length);
        }

        public static uint Hash<T>(this NativeList<T> list, int length) where T : unmanaged
        {
            CheckLengthInRange(length, list.Length);
            if (length == 0)
                return 0;
            return math.hash(list.GetUnsafeReadOnlyPtr(), length * sizeof(T));
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, NativeListArray<T>.NativeList elements) where T : unmanaged
        {
            list.AddRangeNoResize(elements.GetUnsafeReadOnlyPtr(), elements.Length);
        }

        public static void AddRange<T>(this NativeList<T> list, T[] elements) where T : struct
        {
            foreach (var item in elements)
                list.Add(item);
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, T[] elements) where T : struct
        {
            foreach (var item in elements)
                list.AddNoResize(item);
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, List<T> elements) where T : struct
        {
            foreach(var item in elements)
                list.AddNoResize(item);
        }

        public static void AddRangeNoResize<T>(this NativeList<T> list, NativeList<T> elements, int start, int count) where T : unmanaged
        {
            CheckLengthInRange(count, elements.Length);
            CheckIndexInRangeInc(start, elements.Length - count);
            list.AddRangeNoResize((T*)elements.GetUnsafeReadOnlyPtr() + start, count);
        }

        public static void CopyFrom<T>(this NativeList<T> list, NativeList<T> elements, int start, int count) where T : unmanaged
        {
            CheckLengthInRange(count, elements.Length);
            CheckIndexInRangeInc(start, elements.Length - count);

            list.Clear();
            list.AddRangeNoResize((T*)elements.GetUnsafeReadOnlyPtr() + start, count);
        }

        public static void CopyFrom<T>(this NativeArray<T> dstArray, NativeList<T> srcList, int start, int count) where T : unmanaged
        {
            CheckLengthInRange(count, srcList.Length);
            CheckLengthInRange(count, dstArray.Length);
            CheckIndexInRangeInc(start, srcList.Length - count);

            var srcPtr  = (T*)srcList.GetUnsafeReadOnlyPtr() + start;
            var dstPtr  = (T*)dstArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dstPtr, srcPtr, count * UnsafeUtility.SizeOf<T>());
        }

        public static void CopyFrom<T>(this NativeArray<T> dstArray, NativeArray<T> srcArray, int srcIndex, int srcCount) where T : unmanaged
        {
            CheckLengthInRange(srcCount, srcArray.Length);
            CheckLengthInRange(srcCount, dstArray.Length);
            CheckIndexInRangeInc(srcIndex, srcArray.Length - srcCount);

            var srcPtr  = (T*)srcArray.GetUnsafeReadOnlyPtr() + srcIndex;
            var dstPtr  = (T*)dstArray.GetUnsafePtr();

            UnsafeUtility.MemCpy(dstPtr, srcPtr, srcCount * UnsafeUtility.SizeOf<T>());
        }

        public static void CopyFrom<T>(this NativeArray<T> dstArray, int dstIndex, ref BlobArray<T> srcArray, int srcIndex, int srcCount) where T : unmanaged
        {
            CheckLengthInRange(srcCount, srcArray.Length);
            CheckLengthInRange(srcCount, dstArray.Length);
            CheckIndexInRangeInc(dstIndex, dstArray.Length - srcCount);
            CheckIndexInRangeInc(srcIndex, srcArray.Length - srcCount);

            var srcPtr = (T*)srcArray.GetUnsafePtr() + srcIndex;
            var dstPtr = (T*)dstArray.GetUnsafePtr() + dstIndex;

            UnsafeUtility.MemCpy(dstPtr, srcPtr, srcCount * UnsafeUtility.SizeOf<T>());
        }

        public static void CopyFrom<T>(this NativeSlice<T> dstArray, int dstIndex, ref BlobArray<T> srcArray, int srcIndex, int srcCount) where T : unmanaged
        {
            CheckLengthInRange(srcCount, srcArray.Length);
            CheckLengthInRange(srcCount, dstArray.Length);
            CheckIndexInRangeInc(dstIndex, dstArray.Length - srcCount);
            CheckIndexInRangeInc(srcIndex, srcArray.Length - srcCount);

            var srcPtr = (T*)srcArray.GetUnsafePtr() + srcIndex;
            var dstPtr = (T*)dstArray.GetUnsafePtr() + dstIndex;

            UnsafeUtility.MemCpy(dstPtr, srcPtr, srcCount * UnsafeUtility.SizeOf<T>());
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
