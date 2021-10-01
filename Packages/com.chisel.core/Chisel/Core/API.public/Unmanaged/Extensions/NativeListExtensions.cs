using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions.Must;
using UnityEngine;
using System.Threading;
using System.Diagnostics;
using Unity.Burst.CompilerServices;

namespace Chisel.Core
{
    public static class NativeListExtensions
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemCpy<T>(this NativeArray<T> array, int destIndex, int sourceIndex, int count)
            where T : unmanaged
        {
            if (destIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} must be positive.", nameof(destIndex));
            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} must be positive.", nameof(sourceIndex));
            if (count < 0)
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (destIndex + count > array.Length)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} + {nameof(count)} must be within bounds of array ({array.Length}).", nameof(count));
            if (sourceIndex + count > array.Length)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} + {nameof(count)} must be within bounds of array ({array.Length}).", nameof(count));
            if (count == 0)
                return;
            if (destIndex == sourceIndex)
                return;
            var dataPtr = ((T*)array.GetUnsafePtr());
            UnsafeUtility.MemCpy(dataPtr + destIndex, dataPtr + sourceIndex, count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemCpy<T>(this NativeSlice<T> slice, int destIndex, int sourceIndex, int count)
            where T : unmanaged
        {
            if (destIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} must be positive.", nameof(destIndex));
            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} must be positive.", nameof(sourceIndex));
            if (count < 0)
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (destIndex + count > slice.Length)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} + {nameof(count)} must be within bounds of slice ({slice.Length}).", nameof(count));
            if (sourceIndex + count > slice.Length)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} + {nameof(count)} must be within bounds of slice ({slice.Length}).", nameof(count));
            if (count == 0)
                return;
            if (destIndex == sourceIndex)
                return;
            var dataPtr = ((T*)slice.GetUnsafePtr());
            UnsafeUtility.MemCpy(dataPtr + destIndex, dataPtr + sourceIndex, count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemCpy<T>(this NativeList<T> list, int destIndex, int sourceIndex, int count)
            where T : unmanaged
        {
            if (destIndex < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} must be positive.", nameof(destIndex));
            if (sourceIndex < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} must be positive.", nameof(sourceIndex));
            if (count < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (destIndex + count > list.Length) 
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} + {nameof(count)} must be within bounds of list ({list.Length}).", nameof(count));
            if (sourceIndex + count > list.Length) 
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} + {nameof(count)} must be within bounds of list ({list.Length}).", nameof(count));
            if (count == 0)
                return;
            if (destIndex == sourceIndex)
                return;
            var dataPtr = ((T*)list.GetUnsafePtr());
            UnsafeUtility.MemCpy(dataPtr + destIndex, dataPtr + sourceIndex, count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemMove<T>(this NativeArray<T> array, int destIndex, int sourceIndex, int count)
            where T : unmanaged
        {
            if (destIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} must be positive.", nameof(destIndex));
            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} must be positive.", nameof(sourceIndex));
            if (count < 0)
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (destIndex + count > array.Length)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} + {nameof(count)} must be within bounds of array ({array.Length}).", nameof(count));
            if (sourceIndex + count > array.Length)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} + {nameof(count)} must be within bounds of array ({array.Length}).", nameof(count));
            if (count == 0)
                return;
            if (destIndex == sourceIndex)
                return;
            var dataPtr = ((T*)array.GetUnsafePtr());
            UnsafeUtility.MemMove(dataPtr + destIndex, dataPtr + sourceIndex, count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemMove<T>(this NativeList<T> list, int destIndex, int sourceIndex, int count)
            where T : unmanaged
        {
            if (destIndex < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} must be positive.", nameof(destIndex));
            if (sourceIndex < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} must be positive.", nameof(sourceIndex));
            if (count < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (destIndex + count > list.Length) 
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} + {nameof(count)} must be within bounds of list ({list.Length}).", nameof(count));
            if (sourceIndex + count > list.Length) 
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} + {nameof(count)} must be within bounds of list ({list.Length}).", nameof(count));
            if (count == 0)
                return;
            if (destIndex == sourceIndex)
                return;
            var dataPtr = ((T*)list.GetUnsafePtr());
            UnsafeUtility.MemMove(dataPtr + destIndex, dataPtr + sourceIndex, count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemMove<T>(ref this UnsafeList<T> list, int destIndex, int sourceIndex, int count)
            where T : unmanaged
        {
            if (destIndex == sourceIndex)
                return;
            if (destIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} must be positive.", nameof(destIndex));
            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} must be positive.", nameof(sourceIndex));
            if (count < 0)
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (destIndex + count > list.Length)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} + {nameof(count)} must be within bounds of list ({list.Length}).", nameof(count));
            if (sourceIndex + count > list.Length)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} + {nameof(count)} must be within bounds of list ({list.Length}).", nameof(count));
            if (count <= 0)
                return;
            if (destIndex == sourceIndex)
                return;
            var dataPtr = list.Ptr;
            UnsafeUtility.MemMove(dataPtr + destIndex, dataPtr + sourceIndex, count * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemMove<T>(T* list, int listLength, int destIndex, int sourceIndex, int count)
            where T : unmanaged
        {
            if (destIndex < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} must be positive.", nameof(destIndex));
            if (sourceIndex < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} must be positive.", nameof(sourceIndex));
            if (count < 0) 
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (destIndex + count > listLength) 
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} + {nameof(count)} must be within bounds of list ({listLength}).", nameof(count));
            if (sourceIndex + count > listLength) 
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} + {nameof(count)} must be within bounds of list ({listLength}).", nameof(count));
            if (count == 0)
                return;
            if (destIndex == sourceIndex)
                return;
            UnsafeUtility.MemMove(list + destIndex, list + sourceIndex, count * sizeof(T));
        }
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemMove<T>(this NativeListArray<T>.NativeList list, int destIndex, int sourceIndex, int count)
            where T : unmanaged
        {
            if (destIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} must be positive.", nameof(destIndex));
            if (sourceIndex < 0)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} must be positive.", nameof(sourceIndex));
            if (count < 0)
                throw new ArgumentOutOfRangeException($"{nameof(count)} must be positive.", nameof(count));
            if (destIndex + count > list.Length)
                throw new ArgumentOutOfRangeException($"{nameof(destIndex)} + {nameof(count)} must be within bounds of list ({list.Length}).", nameof(count));
            if (sourceIndex + count > list.Length)
                throw new ArgumentOutOfRangeException($"{nameof(sourceIndex)} + {nameof(count)} must be within bounds of list ({list.Length}).", nameof(count));
            if (count == 0)
                return;
            if (destIndex == sourceIndex)
                return;
            var dataPtr = ((T*)list.GetUnsafePtr());
            UnsafeUtility.MemMove(dataPtr + destIndex, dataPtr + sourceIndex, count * sizeof(T));
        }*/

        /// <summary>
        /// Returns parallel writer instance.
        /// </summary>
        /// <returns>Parallel writer instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static ParallelWriterExt<T> AsParallelWriterExt<T>(this NativeList<T> list)
            where T : unmanaged
        {
            var m_ListData = list.GetUnsafeList();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var m_Safety = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list);
            return new ParallelWriterExt<T>(m_ListData->Ptr, m_ListData, ref m_Safety);
#else
            return new ParallelWriterExt<T>(m_ListData->Ptr, m_ListData);
#endif
        }

        /// <summary>
        /// Implements parallel writer. Use AsParallelWriterExt to obtain it from container.
        /// </summary>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public unsafe struct ParallelWriterExt<T>
            where T : unmanaged
        {
            /// <summary>
            ///
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public readonly void* Ptr;

            /// <summary>
            ///
            /// </summary>
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList<T>* ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal unsafe ParallelWriterExt(void* ptr, UnsafeList<T>* listData, ref AtomicSafetyHandle safety)
            {
                Ptr = ptr;
                ListData = listData;
                m_Safety = safety;
            }

#else
            internal unsafe ParallelWriterExt(void* ptr, UnsafeList<T>* listData)
            {
                Ptr = ptr;
                ListData = listData;
            }

#endif

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void CheckSufficientCapacity(int capacity, int length)
            {
                if (capacity < length)
                    throw new Exception($"Length {length} exceeds capacity Capacity {capacity}");
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void CheckArgPositive(int value)
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException($"Value {value} must be positive.");
            }

            /// <summary>
            /// Tell Burst that an integer can be assumed to map to an always positive value.
            /// </summary>
            /// <param name="value">The integer that is always positive.</param>
            /// <returns>Returns `x`, but allows the compiler to assume it is always positive.</returns>
            [return: AssumeRange(0, int.MaxValue)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static int AssumePositive(int value)
            {
                return value;
            }

            /// <summary>
            /// Adds an element to the list.
            /// </summary>
            /// <param name="value">The value to be added at the end of the list.</param>
            /// <remarks>
            /// If the list has reached its current capacity, internal array won't be resized, and exception will be thrown.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddNoResize(T value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var idx = Interlocked.Increment(ref ListData->m_length) - 1;
                CheckSufficientCapacity(ListData->Capacity, idx + 1);

                UnsafeUtility.WriteArrayElement(Ptr, idx, value);
                return idx;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int AddRangeNoResize(int sizeOf, int alignOf, void* ptr, int length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var idx = Interlocked.Add(ref ListData->m_length, length) - length;
                CheckSufficientCapacity(ListData->Capacity, idx + length);

                void* dst = (byte*)Ptr + idx * sizeOf;
                UnsafeUtility.MemCpy(dst, ptr, length * sizeOf);
                return idx;
            }

            /// <summary>
            /// Adds elements from a buffer to this list.
            /// </summary>
            /// <param name="ptr">A pointer to the buffer.</param>
            /// <param name="length">The number of elements to add to the list.</param>
            /// <remarks>
            /// If the list has reached its current capacity, internal array won't be resized, and exception will be thrown.
            /// </remarks>
            /// <exception cref="ArgumentOutOfRangeException">Thrown if length is negative.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddRangeNoResize(void* ptr, int length)
            {
                CheckArgPositive(length);
                return AddRangeNoResize(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), ptr, AssumePositive(length));
            }

            /// <summary>
            /// Adds elements from a list to this list.
            /// </summary>
            /// <param name="list">Other container to copy elements from.</param>
            /// <remarks>
            /// If the list has reached its current capacity, internal array won't be resized, and exception will be thrown.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddRangeNoResize(UnsafeList<T> list)
            {
                return AddRangeNoResize(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), list.Ptr, list.Length);
            }

            /// <summary>
            /// Adds elements from a list to this list.
            /// </summary>
            /// <param name="list">Other container to copy elements from.</param>
            /// <remarks>
            /// If the list has reached its current capacity, internal array won't be resized, and exception will be thrown.
            /// </remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int AddRangeNoResize(NativeList<T> list)
            {
                var m_ListData = list.GetUnsafeList();
                return AddRangeNoResize(*m_ListData);
            }
        }
    }
}
