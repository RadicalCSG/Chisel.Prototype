using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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
        /// <summary>
        /// Returns parallel writer instance.
        /// </summary>
        /// <returns>Parallel writer instance.</returns>
        public unsafe static ParallelWriterExt<T> AsParallelWriterExt<T>(this NativeList<T> list)
            where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var m_Safety = NativeListUnsafeUtility.GetAtomicSafetyHandle(ref list);
            var m_ListData = list.GetUnsafeList();
            return new ParallelWriterExt<T>(m_ListData->Ptr, m_ListData, ref m_Safety);
#else
            return new ParallelWriterExt<T>(m_ListData->Ptr, m_ListData);
#endif
        }

        /// <summary>
        /// Implements parallel writer. Use AsParallelWriter to obtain it from container.
        /// </summary>
        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public unsafe struct ParallelWriterExt<T>
            where T:struct
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
            public UnsafeList* ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;

            internal unsafe ParallelWriterExt(void* ptr, UnsafeList* listData, ref AtomicSafetyHandle safety)
            {
                Ptr = ptr;
                ListData = listData;
                m_Safety = safety;
            }

#else
            internal unsafe ParallelWriter(void* ptr, UnsafeList* listData)
            {
                Ptr = ptr;
                ListData = listData;
            }

#endif

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            static void CheckSufficientCapacity(int capacity, int length)
            {
                if (capacity < length)
                    throw new Exception($"Length {length} exceeds capacity Capacity {capacity}");
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
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
            public int AddNoResize(T value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var idx = Interlocked.Increment(ref ListData->Length) - 1;
                CheckSufficientCapacity(ListData->Capacity, idx + 1);

                UnsafeUtility.WriteArrayElement(Ptr, idx, value);
                return idx;
            }

            int AddRangeNoResize(int sizeOf, int alignOf, void* ptr, int length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                var idx = Interlocked.Add(ref ListData->Length, length) - length;
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
            public int AddRangeNoResize(UnsafeList list)
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
            public int AddRangeNoResize(NativeList<T> list)
            {
                var m_ListData = list.GetUnsafeList();
                return AddRangeNoResize(*m_ListData);
            }
        }
    }
}
