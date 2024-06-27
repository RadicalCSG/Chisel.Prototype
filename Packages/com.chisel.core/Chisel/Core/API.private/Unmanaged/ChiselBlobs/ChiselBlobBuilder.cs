using System;
using Chisel.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;


// Note: Based on Unity.Entities.BlobBuilder
namespace Chisel.Core
{
    /// <summary>
    /// Used by the <see cref="ChiselBlobBuilder"/> methods to reference the arrays within a blob asset.
    /// </summary>
    /// <remarks>Use this reference to initialize the data of a newly created <see cref="ChiselBlobArray{T}"/>.</remarks>
    /// <typeparam name="T">The data type of the elements in the array.</typeparam>
    public unsafe ref struct ChiselBlobBuilderArray<T> where T : unmanaged
    {
        private void* m_data;
        private int m_length;

        /// <summary>
        /// For internal, <see cref="ChiselBlobBuilder"/>, use only.
        /// </summary>
        public ChiselBlobBuilderArray(void* data, int length)
        {
            m_data = data;
            m_length = length;
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckIndexOutOfRange(int index)
        {
            if (0 > index || index >= m_length)
                throw new IndexOutOfRangeException(string.Format("Index {0} is out of range of '{1}' Length.", (object)index, (object)this.m_length));
        }

        /// <summary>
        /// Array index accessor for the elements in the array.
        /// </summary>
        /// <param name="index">The sequential index of an array item.</param>
        /// <exception cref="IndexOutOfRangeException">Thrown when index is less than zero or greater than the length of the array (minus one).</exception>
        public ref T this[int index]
        {
            get
            {
                CheckIndexOutOfRange(index);
                return ref UnsafeUtility.ArrayElementAsRef<T>(m_data, index);
            }
        }

        /// <summary>
        /// Reports the number of elements in the array.
        /// </summary>
        public int Length
        {
            get { return m_length; }
        }

        /// <summary>
        /// Provides a pointer to the data stored in the array.
        /// </summary>
        /// <remarks>You can only call this function in an [unsafe context].
        /// [unsafe context]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/unsafe-code
        /// </remarks>
        /// <returns>A pointer to the first element in the array.</returns>
        public void* GetUnsafePtr()
        {
            return m_data;
        }
    }

    /// <summary>
    /// Creates blob assets.
    /// </summary>
    /// <remarks>
    /// A blob asset is an immutable data structure stored in unmanaged memory.
    /// Blob assets can contain primitive types, strings, structs, arrays, and arrays of arrays. Arrays and structs
    /// must only contain blittable types. Strings must be of type <see cref="BlobString"/> (or a specialized unmanaged
    /// string type such as <see cref="NativeString"/>).
    ///
    /// To use a BlobBuilder object to create a blob asset:
    /// 1. Declare the structure of the blob asset as a struct.
    /// 2. Create a BlobBuilder object.
    /// 3. Call the <see cref="ConstructRoot{T}"/> method, where `T` is the struct definng the asset structure.
    /// 4. Initialize primitive values defined at the root level of the asset.
    /// 5. Allocate memory for arrays, structs, and <see cref="BlobString"/> instances at the root.
    /// 6. Initialize the values of those arrays, structs, and strings.
    /// 7. Continue allocating memory and initializing values until you have fully constructed the asset.
    /// 8. Call <see cref="CreateBlobAssetReference{T}"/> to create a reference to the blob asset in memory.
    /// 9. Dispose the BlobBuilder object.
    ///
    /// Use the <see cref="ChiselBlobAssetReference{T}"/> returned by <see cref="CreateBlobAssetReference{T}"/> to reference
    /// the blob asset. You can use a <see cref="ChiselBlobAssetReference{T}"/> as a field of an <see cref="IComponentData"/>
    /// struct. More than one entity can reference the same blob asset.
    ///
    /// Call <see cref="ChiselBlobAssetReference{T}.Dispose()"/> to free the memory allocated for a blob asset.
    ///
    /// Blob assets cannot be modified once created. Instead, you must create a new blob asset, update any references
    /// to the old one and then dispose of it.
    /// </remarks>
    /// <example>
    /// <code source="../DocCodeSamples.Tests/BlobAssetExamples.cs" region="builderclassexample" title="BlobBuilder Example"/>
    /// </example>
    unsafe public struct ChiselBlobBuilder : IDisposable
    {
        Allocator m_allocator;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<BlobAllocation> m_allocations;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<OffsetPtrPatch> m_patches;
        int m_currentChunkIndex;
        int m_chunkSize;

        public bool IsCreated
        {
            get
            {
                return m_allocations.IsCreated && m_patches.IsCreated;
            }
        }

        struct BlobAllocation
        {
            public int size;
            public byte* p;
        }

        struct BlobDataRef
        {
            public int allocIndex;
            public int offset;
        }

        struct OffsetPtrPatch
        {
            public int* offsetPtr;
            public BlobDataRef target;
            public int length; // if length != 0 this is an array patch and the length should be patched
        }

        /// <summary>
        /// Constructs a BlobBuilder object.
        /// </summary>
        /// <param name="allocator">The type of allocator to use for the BlobBuilder's internal, temporary data. Use
        /// <see cref="Unity.Collections.Allocator.Temp"/> unless the BlobBuilder exists across more than four Unity frames.</param>
        /// <param name="chunkSize">(Optional) The minimum amount of memory to allocate while building an asset.
        /// The default value should suit most use cases. A smaller chunkSize results in more allocations; a larger
        /// chunkSize could increase the BlobBuilder's total memory allocation (which is freed when you dispose of
        /// the BlobBuilder.</param>
        public ChiselBlobBuilder(Allocator allocator, int chunkSize = 65536)
        {
            m_allocator = allocator;
            m_allocations = new NativeList<BlobAllocation>(16, m_allocator);
            m_patches = new NativeList<OffsetPtrPatch>(16, m_allocator);
            m_chunkSize = chunkSize;
            m_currentChunkIndex = -1;
        }

        /// <summary>
        /// Creates the top-level fields of a single blob asset.
        /// </summary>
        /// <remarks>
        /// This function allocates memory for the top-level fields of a blob asset and returns a reference to it. Use
        /// this root reference to initialize field values and to allocate memory for arrays and structs.
        /// </remarks>
        /// <typeparam name="T">A struct that defines the structure of the blob asset.</typeparam>
        /// <returns>A reference to the blob data under construction.</returns>
        public ref T ConstructRoot<T>() where T : struct
        {
            var allocation = Allocate(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>());
            return ref UnsafeUtility.AsRef<T>(AllocationToPointer(allocation));
        }

        /// <summary>
        /// Copies an array of structs to an array in a blob asset after allocating the necessary memory.
        /// </summary>
        /// <param name="blobArray">A reference to a BlobArray field in a blob asset.</param>
        /// <param name="data">An array  containing structs of type <typeparamref name="T"/>.</param>
        /// <typeparam name="T">The struct data type.</typeparam>
        /// <returns>A reference to the newly constructed array as a mutable BlobBuilderArray instance.</returns>
        public ChiselBlobBuilderArray<T> Construct<T>(ref ChiselBlobArray<T> blobArray, params T[] data) where T : unmanaged
        {
            var constructBlobArray = Allocate(ref blobArray, data.Length);
            for (int i = 0; i != data.Length; i++)
                constructBlobArray[i] = data[i];
            return constructBlobArray;
        }

        /// <summary>
        /// Allocates enough memory to store <paramref name="length"/> elements of struct <typeparamref name="T"/>.
        /// </summary>
        /// <param name="ptr">A reference to a BlobArray field in a blob asset.</param>
        /// <param name="length">The number of elements to allocate.</param>
        /// <typeparam name="T">The struct data type.</typeparam>
        /// <returns>A reference to the newly allocated array as a mutable BlobBuilderArray instance.</returns>
        public ChiselBlobBuilderArray<T> Allocate<T>(ref ChiselBlobArray<T> ptr, int length) where T : unmanaged
        {
            if (length <= 0)
                return new ChiselBlobBuilderArray<T>(null, 0);

            var offsetPtr = (int*)UnsafeUtility.AddressOf(ref ptr.m_OffsetPtr);

            ValidateAllocation(offsetPtr);

            var allocation = Allocate(UnsafeUtility.SizeOf<T>() * length, UnsafeUtility.AlignOf<T>());

            var patch = new OffsetPtrPatch
            {
                offsetPtr = offsetPtr,
                target = allocation,
                length = length
            };

            m_patches.Add(patch);
            return new ChiselBlobBuilderArray<T>(AllocationToPointer(allocation), length);
        }

        /// <summary>
        /// Allocates enough memory to store a struct of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="ptr">A reference to a blob pointer field in a blob asset.</param>
        /// <typeparam name="T">The struct data type.</typeparam>
        /// <returns>A reference to the newly allocated struct.</returns>
        public ref T Allocate<T>(ref ChiselBlobPtr<T> ptr) where T : struct
        {
            var offsetPtr = (int*)UnsafeUtility.AddressOf(ref ptr.m_OffsetPtr);

            ValidateAllocation(offsetPtr);

            var allocation = Allocate(UnsafeUtility.SizeOf<T>(), UnsafeUtility.AlignOf<T>());

            var patch = new OffsetPtrPatch
            {
                offsetPtr = offsetPtr,
                target = allocation,
                length = 0
            };

            m_patches.Add(patch);
            return ref UnsafeUtility.AsRef<T>(AllocationToPointer(allocation));
        }

        /// <summary>
        /// Sets a BlobPtr to point to the given object inside the blob.
        /// </summary>
        /// <param name="ptr">A reference to a blob pointer field in a blob asset.</param>
        /// <param name="obj">The struct that exists in the blob that you want to point to.</param>
        /// <returns>A reference to obj.</returns>
        public ref T SetPointer<T>(ref ChiselBlobPtr<T> ptr, ref T obj) where T : struct
        {
            var offsetPtr = (int*)UnsafeUtility.AddressOf(ref ptr.m_OffsetPtr);
            var objTargetPtr = UnsafeUtility.AddressOf(ref obj);

            ValidateAllocation(offsetPtr);

            if (GetPatchTarget(objTargetPtr, out var target))
            {
                var patch = new OffsetPtrPatch
                {
                    offsetPtr = offsetPtr,
                    target = target,
                    length = 0
                };

                m_patches.Add(patch);
            }
            else
            {
                throw new System.ArgumentException("Reference object is not a part of the blob; can only create BlobPtr to objects inside a blob");
            }

            return ref obj;
        }


        struct SortedIndex : IComparable<SortedIndex>
        {
            public byte* p;
            public int index;
            public int CompareTo(SortedIndex other)
            {
                return ((ulong)p).CompareTo((ulong)other.p);
            }
        }

        /// <summary>
        /// Completes construction of the blob asset and returns a reference to the asset in unmanaged memory.
        /// </summary>
        /// <remarks>Use the <see cref="ChiselBlobAssetReference{T}"/> to access the blob asset. When the asset is no longer
        /// needed, call<see cref="ChiselBlobAssetReference{T}.Dispose()"/> to destroy the blob asset and free its allocated
        /// memory.</remarks>
        /// <param name="allocator">The type of memory to allocate. Unless the asset has a very short life span, use
        /// <see cref="Allocator.Persistent"/>.</param>
        /// <typeparam name="T">The data type of the struct used to construct the asset's root. Use the same struct type
        /// that you used when calling <see cref="ConstructRoot{T}"/>.</typeparam>
        /// <returns></returns>
        public ChiselBlobAssetReference<T> CreateBlobAssetReference<T>(Allocator allocator) where T : struct
        {
            var offsets = new NativeArray<int>(m_allocations.Length + 1, Allocator.Temp);
            var sortedAllocs = new NativeArray<SortedIndex>(m_allocations.Length, Allocator.Temp);

            offsets[0] = 0;
            for (int i = 0; i < m_allocations.Length; ++i)
            {
                offsets[i + 1] = offsets[i] + m_allocations[i].size;
                sortedAllocs[i] = new SortedIndex {p = m_allocations[i].p, index = i};
            }
            int dataSize = offsets[m_allocations.Length];

            sortedAllocs.Sort();
            var sortedPatches = new NativeArray<SortedIndex>(m_patches.Length, Allocator.Temp);
            for (int i = 0; i < m_patches.Length; ++i)
                sortedPatches[i] = new SortedIndex {p = (byte*)m_patches[i].offsetPtr, index = i};
            sortedPatches.Sort();

            byte* buffer = (byte*)ChiselMemory.Unmanaged.Allocate(sizeof(ChiselBlobAssetHeader) + dataSize, 16, allocator);
            byte* data = buffer + sizeof(ChiselBlobAssetHeader);

            for (int i = 0; i < m_allocations.Length; ++i)
                UnsafeUtility.MemCpy(data + offsets[i], m_allocations[i].p, m_allocations[i].size);

            int iAlloc = 0;
            var allocStart = m_allocations[sortedAllocs[0].index].p;
            var allocEnd = allocStart + m_allocations[sortedAllocs[0].index].size;

            for (int i = 0; i < m_patches.Length; ++i)
            {
                int patchIndex = sortedPatches[i].index;
                int* offsetPtr = (int*)sortedPatches[i].p;

                while (offsetPtr > allocEnd)
                {
                    ++iAlloc;
                    allocStart = m_allocations[sortedAllocs[iAlloc].index].p;
                    allocEnd = allocStart + m_allocations[sortedAllocs[iAlloc].index].size;
                }

                var patch = m_patches[patchIndex];

                int offsetPtrInData = offsets[sortedAllocs[iAlloc].index] + (int)((byte*)offsetPtr - allocStart);
                int targetPtrInData = offsets[patch.target.allocIndex] + patch.target.offset;

                *(int*)(data + offsetPtrInData) = targetPtrInData - offsetPtrInData;
                if (patch.length != 0)
                {
                    *(int*)(data + offsetPtrInData + 4) = patch.length;
                }
            }

            sortedPatches.Dispose();
            sortedPatches = default;
            sortedAllocs.Dispose();
            sortedAllocs = default;
            offsets.Dispose();
            offsets = default;

            ChiselBlobAssetHeader* header = (ChiselBlobAssetHeader*)buffer;
            *header = new ChiselBlobAssetHeader();
            header->Length = (int)dataSize;
            header->Allocator = allocator;

            // @TODO use 64bit hash
            header->Hash = math.hash(buffer + sizeof(ChiselBlobAssetHeader), dataSize);

            ChiselBlobAssetReference<T> blobAssetReference;
            blobAssetReference.m_data.m_Align8Union = 0;
            header->ValidationPtr = blobAssetReference.m_data.m_Ptr = buffer + sizeof(ChiselBlobAssetHeader);

            return blobAssetReference;
        }

        void* AllocationToPointer(BlobDataRef blobDataRef)
        {
            return m_allocations[blobDataRef.allocIndex].p + blobDataRef.offset;
        }

        BlobAllocation EnsureEnoughRoomInChunk(int size, int alignment)
        {
            if (m_currentChunkIndex == -1)
                return AllocateNewChunk();

            var alloc = m_allocations[m_currentChunkIndex];
            int startOffset = CollectionHelper.Align(alloc.size, alignment);
            if (startOffset + size > m_chunkSize)
                return AllocateNewChunk();

            UnsafeUtility.MemClear(alloc.p + alloc.size, startOffset - alloc.size);

            alloc.size = startOffset;
            return alloc;
        }

        BlobDataRef Allocate(int size, int alignment)
        {
            if (size > m_chunkSize)
            {
                size = CollectionHelper.Align(size, 16);
                var allocIndex = m_allocations.Length;
                var mem = (byte*)ChiselMemory.Unmanaged.Allocate(size, alignment, m_allocator);
                UnsafeUtility.MemClear(mem, size);
                m_allocations.Add(new BlobAllocation {p = mem, size = size});
                return new BlobDataRef {allocIndex = allocIndex, offset = 0};
            }

            BlobAllocation alloc = EnsureEnoughRoomInChunk(size, alignment);

            var offset = alloc.size;
            UnsafeUtility.MemClear(alloc.p + alloc.size, size);
            alloc.size += size;
            m_allocations[m_currentChunkIndex] = alloc;
            return new BlobDataRef {allocIndex = m_currentChunkIndex, offset = offset};
        }

        BlobAllocation AllocateNewChunk()
        {
            // align size of last chunk to 16 bytes so chunks can be concatenated without breaking alignment
            if (m_currentChunkIndex != -1)
            {
                var currentAlloc = m_allocations[m_currentChunkIndex];
                currentAlloc.size = CollectionHelper.Align(currentAlloc.size, 16);
                m_allocations[m_currentChunkIndex] = currentAlloc;
            }

            m_currentChunkIndex = m_allocations.Length;
            var alloc = new BlobAllocation {p = (byte*)ChiselMemory.Unmanaged.Allocate(m_chunkSize, 16, m_allocator), size = 0};
            m_allocations.Add(alloc);
            return alloc;
        }

        [System.Diagnostics.Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ValidateAllocation(void* address)
        {
            // ValidateAllocation is most often called with data in recently allocated allocations
            // so this searches backwards
            for (int i = m_allocations.Length - 1; i >= 0; --i)
            {
                var allocation = m_allocations[i];
                if (address >= allocation.p && address < (allocation.p + allocation.size))
                    return;
            }

            throw new InvalidOperationException("The BlobArray passed to Allocate was not allocated by this BlobBuilder or the struct that embeds it was copied by value instead of by ref.");
        }

        private bool GetPatchTarget(void* address, out BlobDataRef blobDataRef)
        {
            // Search backwards; most likely to be referring to objects that were most recently added..
            for (int i = m_allocations.Length-1; i >= 0; i--)
            {
                var allocation = m_allocations[i];
                if (address >= allocation.p && address < (allocation.p + allocation.size))
                {
                    blobDataRef = new BlobDataRef
                    {
                        allocIndex = i,
                        offset = (int)((byte*)address - allocation.p),
                    };

                    return true;
                }
            }

            blobDataRef = default;
            return false;
        }

        /// <summary>
        /// Resets this BlobBuilder instance
        /// </summary>
        public void Reset()
        {
            m_allocations.Clear();
            m_patches.Clear();
            m_currentChunkIndex = -1;
        }

        /// <summary>
        /// Disposes of this BlobBuilder instance and frees its temporary memory allocations.
        /// </summary>
        /// <remarks>Call `Dispose()` after calling <see cref="CreateBlobAssetReference{T}"/>.</remarks>
        public void Dispose()
        {
            for (int i = 0; i < m_allocations.Length; ++i)
                ChiselMemory.Unmanaged.Free(m_allocations[i].p, m_allocator);
            m_allocations.Dispose();
            m_patches.Dispose();
            m_allocations = default;
            m_patches = default;
        }
    }
}
