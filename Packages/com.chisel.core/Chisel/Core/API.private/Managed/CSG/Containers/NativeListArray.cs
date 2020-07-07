using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Chisel.Core
{
    using Chisel.Core.LowLevel.Unsafe;
    using UnityEngine;

    [DebuggerDisplay("Length = {Length}, Capacity = {Capacity}, IsCreated = {IsCreated}")]
    public unsafe struct UnsafeListArray : IDisposable
    {
        // TODO: use "FixedList" instead
        [NativeDisableUnsafePtrRestriction]
        public UnsafeList** Ptr;

        public int Length;
        public int Capacity;
        public int InitialListCapacity;

        public Allocator Allocator;

        public static UnsafeListArray* Create(Allocator allocator)
        {
            UnsafeListArray* arrayData = (UnsafeListArray*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<UnsafeListArray>(), UnsafeUtility.AlignOf<UnsafeListArray>(), allocator);
            UnsafeUtility.MemClear(arrayData, UnsafeUtility.SizeOf<UnsafeListArray>());

            arrayData->Allocator = allocator;
            arrayData->Length = 0;
            arrayData->Capacity = 0;

            return arrayData;
        }

        void ResizeCapacity(int newCapacity)
        {
            CheckAllocator(Allocator);
            var oldBytesToMalloc = sizeof(UnsafeList*) * Capacity;
            var newBytesToMalloc = sizeof(UnsafeList*) * newCapacity;
            var newPtr = (UnsafeList**)UnsafeUtility.Malloc(newBytesToMalloc, UnsafeUtility.AlignOf<long>(), Allocator);
            UnsafeUtility.MemClear(newPtr, newBytesToMalloc);
            if (Ptr != null)
            {
                UnsafeUtility.MemCpy(newPtr, Ptr, oldBytesToMalloc);
                UnsafeUtility.Free(Ptr, Allocator);
            }
            Ptr = newPtr;
            Capacity = newCapacity;
        }

        public void ResizeExact(int newLength)
        {
            if (newLength == Length)
                return;

            if (Length > 0 && newLength < Length)
            {
                for (int i = newLength; i < Length; i++)
                {
                    if (Ptr[i] != null &&
                        Ptr[i]->IsCreated)
                    {
                        Ptr[i]->Clear();
                    } else
                        Ptr[i] = null;
                }
            }
            if (Capacity < newLength)
                ResizeCapacity(newLength);
            Length = newLength;
        }

        void Resize(int newLength)
        {
            if (newLength == Length)
                return;

            if (Length > 0 && newLength < Length)
            {
                for (int i = newLength; i < Length; i++)
                {
                    if (Ptr[i] != null &&
                        Ptr[i]->IsCreated)
                    {
                        Ptr[i]->Clear();
                    } else
                        Ptr[i] = null;
                }
            }

            if (Capacity < newLength)
            {
                var capacity = (int)((newLength * 1.5f) + 0.5f);
                ResizeCapacity(capacity);
            }
            Length = newLength;
        }

        public UnsafeList* InitializeIndex(int index, int sizeOf, int alignOf, int capacity, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            CheckIndexInRange(index, Length);
            var ptr = UnsafeList.Create(sizeOf, alignOf, capacity, Allocator, options);
            Ptr[index] = ptr;
            return ptr;
        }

        public UnsafeList* InitializeIndex(int index, int sizeOf, int alignOf, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            return InitializeIndex(index, sizeOf, alignOf, InitialListCapacity, options);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckAlreadyAllocated(int length)
        {
            if (length > 0)
                throw new IndexOutOfRangeException($"NativeListArray already allocated.");
        }


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndexInRange(int value, int length)
        {
            if (value < 0)
                throw new IndexOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new IndexOutOfRangeException($"Value {value} is out of range in NativeList of '{length}' Length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void NullCheck(void* arrayData)
        {
            if (arrayData == null)
            {
                throw new Exception("UnsafeListArray has yet to be created or has been destroyed!");
            }
        }

        public static void Destroy(UnsafeListArray* arrayData)
        {
            NullCheck(arrayData);
            var allocator = arrayData->Allocator;
            CheckAllocator(allocator);
            arrayData->Dispose();
            UnsafeUtility.Free(arrayData, allocator);
        }

        public bool IsCreated => Ptr != null;

        internal static bool ShouldDeallocate(Allocator allocator)
        {
            // Allocator.Invalid == container is not initialized.
            // Allocator.None    == container is initialized, but container doesn't own data.
            return allocator > Allocator.None;
        }

        public void Dispose()
        {
            if (ShouldDeallocate(Allocator))
            {
                for (int i = 0; i < Length; i++)
                {
                    if (Ptr[i] != null &&
                        Ptr[i]->IsCreated)
                    {
                        Ptr[i]->Clear();
                        Ptr[i]->Dispose();
                    }
                    Ptr[i] = null;
                }
                UnsafeUtility.Free(Ptr, Allocator);
                Allocator = Allocator.Invalid;
            }

            Ptr = null;
            Length = 0;
            Capacity = 0;
        }

        public void Clear()
        {
            if (Length == 0)
                return;
            
            for (int i = 0; i < Length; i++)
            {
                if (Ptr[i] != null &&
                    Ptr[i]->IsCreated)
                {
                    Ptr[i]->Clear();
                    Ptr[i]->Dispose();
                }
                Ptr[i] = null;
            }
            Length = 0;
        }

        public void ClearChildren()
        {
            if (Length == 0)
                return;

            for (int i = 0; i < Length; i++)
            {
                if (Ptr[i] != null &&
                    Ptr[i]->IsCreated)
                {
                    Ptr[i]->Clear();
                } else
                    Ptr[i] = null;
            }
            Length = 0;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static private void CheckAllocator(Allocator a)
        {
            if (a <= Allocator.None)
            {
                throw new Exception("UnsafeListArray is not initialized, it must be initialized with allocator before use.");
            }
        }

        public int IndexOf<T>(T value) where T : unmanaged, IEquatable<T>
        {
            return NativeArrayExtensions.IndexOf<T, T>(Ptr, Length, value);
        }

        public bool Contains<T>(T value) where T : unmanaged, IEquatable<T>
        {
            return IndexOf(value) != -1;
        }

        public int AllocateItem()
        {
            var prevLength = Length;
            Resize(prevLength + 1);
            return prevLength;
        }
    }

    interface IDisposableJob
    {
        JobHandle Dispose(JobHandle handle);
    }
    
    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Length = {Length}")]
    public unsafe struct NativeListArray<T> : IDisposable, IDisposableJob
        where T : unmanaged
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel m_DisposeSentinel;
#endif

        [NativeDisableUnsafePtrRestriction]
        internal UnsafeListArray* m_Array;
        
        public int Length { [return: AssumeRange(0, int.MaxValue)] get { return m_Array->Length; } }
        public int Count { [return: AssumeRange(0, int.MaxValue)] get { return m_Array->Length; } }
        public int Capacity { [return: AssumeRange(0, int.MaxValue)] get { return m_Array->Capacity; } }
        public bool IsCreated => m_Array != null;

        public NativeListArray(Allocator allocator)
            : this(1, allocator, 2)
        {
        }

        public NativeListArray(int initialListCapacity, Allocator allocator)
            : this(initialListCapacity, allocator, 2)
        {
        }

        NativeListArray(int initialListCapacity, Allocator allocator, int disposeSentinelStackDepth)
        {
            var totalSize = UnsafeUtility.SizeOf<T>() * (long)initialListCapacity;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent.
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (initialListCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialListCapacity), "InitialListCapacity must be >= 0");


            CollectionHelper.CheckIsUnmanaged<T>();

            if (totalSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(initialListCapacity), $"InitialListCapacity * sizeof(T) cannot exceed {int.MaxValue} bytes");

            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
#endif
            m_Array = UnsafeListArray.Create(allocator);
            m_Array->InitialListCapacity = initialListCapacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }


        public void ResizeExact(int length)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");
            m_Array->ResizeExact(length);
        }

        internal void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_Array->Clear();
        }

        internal void ClearChildren()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            m_Array->ClearChildren();
        }

        public bool IsIndexCreated(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            var ptr = m_Array->Ptr[index];
            return (ptr != null && ptr->IsCreated);
        }

        public NativeList SafeGet(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            CheckArgPositive(index);
            var positiveIndex = AssumePositive(index);
            CheckArgInRange(positiveIndex, m_Array->Length);
            var ptr = m_Array->Ptr[positiveIndex];
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (ptr == null ||
                !ptr->IsCreated)
                return new NativeList(null, ref m_Safety);
            return new NativeList(m_Array->Ptr[positiveIndex], ref m_Safety);
#else
            if (ptr == null ||
                !ptr->IsCreated)
                return new NativeList(null);
            return new NativeList(m_Array->Ptr[positiveIndex]);
#endif
        }

        public bool IsAllocated(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            CheckArgPositive(index);
            var positiveIndex = AssumePositive(index);
            CheckArgInRange(positiveIndex, m_Array->Length);
            var ptr = m_Array->Ptr[positiveIndex];
            return (ptr != null && ptr->IsCreated);
        }

        public NativeList AllocateWithCapacityForIndex(int index, int capacity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            CheckArgPositive(index);
            var positiveIndex = AssumePositive(index);
            CheckArgInRange(positiveIndex, m_Array->Length);

            capacity = Math.Max(1, capacity);

            var ptr = m_Array->Ptr[index];
            if (ptr == null || !ptr->IsCreated)
            {
                m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), capacity);
            } else
            {
                ptr->Clear();
                if (ptr->Capacity < capacity)
                    ptr->SetCapacity<T>(capacity);
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocated(m_Array->Ptr[index]);
            return new NativeList(m_Array->Ptr[index], ref m_Safety);
#else
            return new NativeList(m_Array->Ptr[index]);
#endif
        }

        public NativeList AddAndAllocateWithCapacity(int capacity)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            int index = m_Array->AllocateItem();
            var ptr = m_Array->Ptr[index];
            capacity = Math.Max(1, capacity);
            if (ptr == null || !ptr->IsCreated)
            {
                m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), capacity);
            } else
            {
                ptr->Clear();
                if (ptr->Capacity < capacity)
                    ptr->SetCapacity<T>(capacity);
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocated(m_Array->Ptr[index]);
            return new NativeList(m_Array->Ptr[index], ref m_Safety);
#else
            return new NativeList(m_Array->Ptr[index]);
#endif
        }

        public int AllocateItemAndAddValues(NativeArray<T> other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            var index = m_Array->AllocateItem();
            var ptr = m_Array->Ptr[index];
            CheckNotAllocated(ptr);
            m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), other.Length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocated(m_Array->Ptr[index]);
            var dstList = new NativeList(m_Array->Ptr[index], ref m_Safety);
#else
            var dstList = new NativeList(m_Array->Ptr[index]);
#endif
            dstList.AddRangeNoResize(other);
            return index;
        }

        public int AllocateItemAndAddValues(NativeListArray<T>.NativeList other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            var index = m_Array->AllocateItem();
            var ptr = m_Array->Ptr[index];

            if (ptr == null || !ptr->IsCreated)
            {
                m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), other.Length);
            } else
            {
                ptr->Clear();
                if (ptr->Capacity < other.Length)
                    ptr->SetCapacity<T>(other.Length);
            }
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocated(m_Array->Ptr[index]);
            var dstList = new NativeList(m_Array->Ptr[index], ref m_Safety);
#else
            var dstList = new NativeList(m_Array->Ptr[index]);
#endif
            dstList.AddRangeNoResize(other);
            return index;
        }

        public unsafe int AllocateItemAndAddValues(T* otherPtr, int otherLength)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            var index = m_Array->AllocateItem();
            var ptr = m_Array->Ptr[index];
            var capacity = Math.Max(1, otherLength);
            m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), capacity);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocated(m_Array->Ptr[index]);
#endif
            if (otherLength > 0)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var dstList = new NativeList(m_Array->Ptr[index], ref m_Safety);
#else
                var dstList = new NativeList(m_Array->Ptr[index]);
#endif
                dstList.AddRangeNoResize(otherPtr, otherLength);
            }
            Debug.Assert(IsAllocated(index));
            return index;
        }

        public unsafe int AllocateItemAndAddValues(NativeArray<T> other, int otherLength)
        {
            if (otherLength > other.Length)
                throw new ArgumentOutOfRangeException("otherLength");
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            var index = m_Array->AllocateItem();
            var ptr = m_Array->Ptr[index];
            var capacity = Math.Max(1, otherLength);
            m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), capacity);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CheckAllocated(m_Array->Ptr[index]);
#endif
            if (otherLength > 0)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                var dstList = new NativeList(m_Array->Ptr[index], ref m_Safety);
#else
                var dstList = new NativeList(m_Array->Ptr[index]);
#endif
                dstList.AddRangeNoResize(other, otherLength);
            }
            Debug.Assert(IsAllocated(index));
            return index;
        }

#if false
        public int AllocatItemAndAddValues(NativeList<T> other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            var index = m_Array->AllocateItem();
            var ptr = m_Array->Ptr[index];
            if (ptr == null ||
                !ptr->IsCreated)
            {
                m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), other.Length);
                var dstList = new NativeList(m_Array->Ptr[index], ref m_Safety);
                dstList.AddRangeNoResize(other);
            } else
            {
                var dstList = new NativeList(m_Array->Ptr[index], ref m_Safety);
                dstList.AddRange(other);
            }
            return index;
        }

        public int AllocatItemAndAddValues(List<T> other)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
            var index = m_Array->AllocateItem();
            var ptr = m_Array->Ptr[index];
            if (ptr == null ||
                !ptr->IsCreated)
            {
                ptr = m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), other.Count);
            }
            var dstList = new NativeList(m_Array->Ptr[index], ref m_Safety);
            dstList.AddRange(other);
            return index;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckSufficientCapacity(int capacity, int length)
        {
            if (capacity < length)
            {
                throw new Exception($"Length {length} exceeds capacity Capacity {capacity}");
            }
        }
#endif

        [return: AssumeRange(0, int.MaxValue)]
        internal static int AssumePositive(int x)
        {
            return x;
        }


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckNull(UnsafeList* listData)
        {
            if (listData == null)
                throw new Exception($"Expected {nameof(listData)} to not be null.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckAllocated(UnsafeList* listData)
        {
            if (listData == null || !listData->IsCreated)
                throw new Exception($"Expected {nameof(listData)} to be allocated.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckNotAllocated(UnsafeList* listData)
        {
            if (listData != null && listData->IsCreated)
                throw new Exception($"Expected {nameof(listData)} to not be allocated.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckIndexInRange(int value, int length)
        {
            if (value < 0)
                throw new IndexOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new IndexOutOfRangeException($"Value {value} is out of range in NativeListArray of '{length}' Length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCapacityInRange(int value, int length)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value < (uint)length)
                throw new ArgumentOutOfRangeException($"Value {value} is out of range in NativeListArray of '{length}' Length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckArgInRange(int value, int length)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new ArgumentOutOfRangeException($"Value {value} is out of range in NativeListArray of '{length}' Length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckArgPositive(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private unsafe static void CheckPtrInitialized(UnsafeList* ptr, int index)
        {
            if (ptr == null ||
                !ptr->IsCreated)
                throw new IndexOutOfRangeException($"Index {index} has not been initialized.");
        }

        public NativeList this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                CheckArgPositive(index);
                var positiveIndex = AssumePositive(index);
                CheckArgInRange(positiveIndex, m_Array->Length);
                var ptr = m_Array->Ptr[positiveIndex];
                CheckPtrInitialized(ptr, index);
                if (ptr == null ||
                    !ptr->IsCreated)
                    ptr = m_Array->InitializeIndex(index, UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>());
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                return new NativeList(m_Array->Ptr[positiveIndex], ref m_Safety);
#else
                return new NativeList(m_Array->Ptr[positiveIndex]);
#endif
            }
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            UnsafeListArray.Destroy(m_Array);
            m_Array = null;
        }

        [BurstCompile]
        internal unsafe struct UnsafeDisposeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            public UnsafeListArray* array;

            public void Execute()
            {
                UnsafeListArray.Destroy(array);
            }
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // [DeallocateOnJobCompletion] is not supported, but we want the deallocation
            // to happen in a thread. DisposeSentinel needs to be cleared on main thread.
            // AtomicSafetyHandle can be destroyed after the job was scheduled (Job scheduling
            // will check that no jobs are writing to the container).
            DisposeSentinel.Clear(ref m_DisposeSentinel);

            var jobHandle = new UnsafeDisposeJob { array = m_Array }.Schedule(inputDeps);

            AtomicSafetyHandle.Release(m_Safety);
#else
            var jobHandle = new UnsafeDisposeJob { array = m_Array }.Schedule(inputDeps);
#endif
            return jobHandle;
        }

        [NativeContainer]
        public unsafe struct NativeList
        {
            [NativeDisableUnsafePtrRestriction]
            public UnsafeList* m_ListData;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle m_Safety;

            public unsafe NativeList(UnsafeList* listData, ref AtomicSafetyHandle safety)
            {
                m_ListData = listData;
                m_Safety = safety;
            }
#else
            public unsafe NativeList(UnsafeList* listData)
            {
                m_ListData = listData;
            }
#endif

            public T this[int index]
            {
                get
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                    CheckAllocated(m_ListData);
                    CheckIndexInRange(index, m_ListData->Length);
#endif
                    return UnsafeUtility.ReadArrayElement<T>(m_ListData->Ptr, AssumePositive(index));
                }
                set
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                    CheckAllocated(m_ListData);
                    CheckIndexInRange(index, m_ListData->Length);
#endif
                    UnsafeUtility.WriteArrayElement(m_ListData->Ptr, AssumePositive(index), value);
                }
            }

            public int Length
            {
                get
                {
                    if (m_ListData == null)
                        return 0;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    if (!m_ListData->IsCreated)
                        return 0;
                    return AssumePositive(m_ListData->Length);
                }
            }

            public int Count
            {
                get
                {
                    if (m_ListData == null)
                        return 0;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    if (m_ListData == null ||
                        !m_ListData->IsCreated)
                        return 0;
                    return AssumePositive(m_ListData->Length);
                }
            }

            public int Capacity
            {
                get
                {
                    if (m_ListData == null)
                        return 0;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                    if (m_ListData == null ||
                        !m_ListData->IsCreated)
                        return 0;
                    return AssumePositive(m_ListData->Capacity);
                }

                set
                {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
                    CheckNull(m_ListData);
                    CheckCapacityInRange(value, m_ListData->Length);
#endif
                    m_ListData->SetCapacity<T>(value);
                }
            }

            public void AddNoResize(T value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                CheckAllocated(m_ListData);
#endif
                m_ListData->AddNoResize(value);
            }

            public void AddRangeNoResize(void* ptr, int length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                CheckNull(m_ListData);
#endif
                CheckArgPositive(length);
                m_ListData->AddRangeNoResize<T>(ptr, length);
            }

            public void AddRangeNoResize(NativeArray<T> other, int length)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                CheckNull(m_ListData);
#endif
                CheckArgPositive(length);
                m_ListData->AddRangeNoResize<T>(other.GetUnsafeReadOnlyPtr(), length);
            }

            public void AddRangeNoResize(NativeArray<T> list)
            {
                AddRangeNoResize(list.GetUnsafeReadOnlyPtr(), list.Length);
            }

            public void AddRangeNoResize(NativeList<T> list)
            {
                AddRangeNoResize(list.GetUnsafeReadOnlyPtr(), list.Length);
            }

            public void AddRangeNoResize(NativeListArray<T>.NativeList list)
            {
                AddRangeNoResize(list.GetUnsafeReadOnlyPtr(), list.Length);
            }
            /*
            public void Add(T value)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
                CheckNull(m_ListData);
#endif
                m_ListData->Add(value);
            }

            public void AddRange(List<T> items)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
                CheckNull(m_ListData);
#endif
                var length = items.Count;
                CheckArgPositive(length);
                if (m_ListData->Capacity < length)
                    m_ListData->SetCapacity<T>(length);
                for (int i=0;i<length;i++)
                    m_ListData->Add<T>(items[i]);
            }

            public unsafe void AddRange(void* elements, int count)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
                CheckNull(m_ListData);
                CheckArgPositive(count);
#endif
                m_ListData->AddRange<T>(elements, AssumePositive(count));
            }

            public void AddRange(NativeArray<T> elements)
            {
                AddRange(elements.GetUnsafeReadOnlyPtr(), elements.Length);
            }
            */
            public void RemoveAtSwapBack(int index)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
                CheckAllocated(m_ListData);
                CheckArgInRange(index, Length);
#endif
                m_ListData->RemoveAtSwapBack<T>(AssumePositive(index));
            }

            public void Clear()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
                CheckAllocated(m_ListData);
#endif
                m_ListData->Clear();
            }

            public static implicit operator NativeArray<T>(NativeList nativeList)
            {
                return nativeList.AsArray();
            }

            public NativeArray<T> AsArray()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
                CheckAllocated(m_ListData);
                var arraySafety = m_Safety;
                AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
                var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(m_ListData->Ptr, m_ListData->Length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
                return array;
            }
            /*
            public T[] ToArray()
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
                CheckAllocated(m_ListData);
#endif
                return AsArray().ToArray();
            }

            public NativeArray<T> ToArray(Allocator allocator)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
                CheckAllocated(m_ListData);
#endif
                var result = new NativeArray<T>(Length, allocator, NativeArrayOptions.UninitializedMemory);
                result.CopyFrom(this);
                return result;
            }
            */
            public NativeList<T> ToList(Allocator allocator)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
                CheckAllocated(m_ListData);
#endif
                var result = new NativeList<T>(Length, allocator);
                result.AddRange(this);
                return result;
            }
            /*
            public void CopyFrom(T[] array)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
                CheckAllocated(m_ListData);
#endif
                Resize(array.Length, NativeArrayOptions.UninitializedMemory);
                NativeArray<T> na = AsArray();
                na.CopyFrom(array);
            }
            */
            public void Resize(int length, NativeArrayOptions options)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                CheckAllocated(m_ListData);
#endif
                m_ListData->Resize(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>(), length, options);
            }
            /*
            public void ResizeUninitialized(int length)
            {
                Resize(length, NativeArrayOptions.UninitializedMemory);
            }*/
        }
    }
}



namespace Chisel.Core.LowLevel.Unsafe
{
    public unsafe static class NativeArrayListUnsafeUtility
    {
        public static void* GetUnsafePtr<T>(this NativeListArray<T>.NativeList list) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(list.m_Safety);
#endif
            return list.m_ListData->Ptr;
        }

        public static unsafe void* GetUnsafeReadOnlyPtr<T>(this NativeListArray<T>.NativeList list) where T : unmanaged
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(list.m_Safety);
#endif
            return list.m_ListData->Ptr;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public static AtomicSafetyHandle GetAtomicSafetyHandle<T>(ref NativeListArray<T>.NativeList list) where T : unmanaged
        {
            return list.m_Safety;
        }
#endif

        public static void* GetInternalListDataPtrUnchecked<T>(ref NativeListArray<T>.NativeList list) where T : unmanaged
        {
            return list.m_ListData;
        }


        public static void AddRange<T>(this NativeList<T> dst, in NativeListArray<T>.NativeList list) where T : unmanaged
        {
            var offset = dst.Length;
            dst.ResizeUninitialized(offset + list.Length);
            for (int i = 0; i < list.Length; i++)
                dst[offset + i] = list[i];
        }
    }
}
