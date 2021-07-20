using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Chisel.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

// Note: Based on Unity.Entities.BlobAssetReference
namespace Chisel.Core
{
    readonly unsafe struct ChiselBlobAssetPtr : IEquatable<ChiselBlobAssetPtr>
    {
        public readonly ChiselBlobAssetHeader* Header;
        public void* Data => Header + 1;
        public int Length => Header->Length;
        public ulong Hash => Header->Hash;

        public ChiselBlobAssetPtr(ChiselBlobAssetHeader* header)
            => Header = header;

        public bool Equals(ChiselBlobAssetPtr other)
            => Header == other.Header;

        public override int GetHashCode()
        {
            ChiselBlobAssetHeader* onStack = Header;
            return (int)math.hash(&onStack, sizeof(ChiselBlobAssetHeader*));
        }
    }

    // TODO: For now the size of BlobAssetHeader needs to be multiple of 16 to ensure alignment of blob assets
    // TODO: Add proper alignment support to blob assets
    // TODO: Reduce the size of the header at runtime or remove it completely
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    unsafe struct ChiselBlobAssetHeader
    {
        [FieldOffset(0)]  public void* ValidationPtr;
        [FieldOffset(8)]  public int Length;
        [FieldOffset(12)] public Allocator Allocator;
        [FieldOffset(16)] public ulong Hash;
        [FieldOffset(24)] private ulong Padding;

        internal static ChiselBlobAssetHeader CreateForSerialize(int length, ulong hash)
        {
            return new ChiselBlobAssetHeader
            {
                ValidationPtr = null,
                Length = length,
                Allocator = Allocator.None,
                Hash = hash,
                Padding = 0
            };
        }

        public void Invalidate()
        {
            ValidationPtr = (void*)0xdddddddddddddddd;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    internal unsafe struct ChiselBlobAssetReferenceData
    {
        [NativeDisableUnsafePtrRestriction]
        [FieldOffset(0)]
        public byte* m_Ptr;


        /// <summary>
        /// This field overlaps m_Ptr similar to a C union.
        /// It is an internal (so we can initialize the struct) field which
        /// is here to force the alignment of BlobAssetReferenceData to be 8-bytes.
        /// </summary>
        [FieldOffset(0)]
        internal long m_Align8Union;

        internal ChiselBlobAssetHeader* Header
        {
            get { return ((ChiselBlobAssetHeader*) m_Ptr) - 1; }
        }



#if !NET_DOTS
        /// <summary>
        /// This member is exposed to Unity.Properties to support EqualityComparison and Serialization within managed objects.
        /// </summary>
        /// <remarks>
        /// This member is used to expose the value of the <see cref="m_Ptr"/> to properties (which does not handle pointers by default).
        ///
        /// It's used for two managed object cases.
        ///
        /// 1) EqualityComparison - The equality comparison visitor will encounter this member and compare the value (i.e. blob address).
        ///
        ///
        /// 2) Serialization - Before serialization, the <see cref="m_Ptr"/> field is patched with a serialized hash. The visitor encounters this member
        ///                    and writes/reads back the value. The value is then patched back to the new ptr.
        ///
        /// 3) ManagedObjectClone - When cloning managed objects Unity.Properties does not have access to the internal pointer field. This property is used to copy the bits for this struct.
        /// </remarks>
        // ReSharper disable once UnusedMember.Local
        long SerializedHash
        {
            get => m_Align8Union;
            set => m_Align8Union = value;
        }
#endif

        [BurstDiscard]
        void ValidateNonBurst()
        {
            void* validationPtr = null;
            try
            {
                // Try to read ValidationPtr, this might throw if the memory has been unmapped
                validationPtr = Header->ValidationPtr;
            }
            catch(Exception)
            {
            }

            if (validationPtr != m_Ptr)
                throw new InvalidOperationException("The BlobAssetReference is not valid. Likely it has already been unloaded or released.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        void ValidateBurst()
        {
            void* validationPtr = Header->ValidationPtr;
            if(validationPtr != m_Ptr)
                throw new InvalidOperationException("The BlobAssetReference is not valid. Likely it has already been unloaded or released.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void ValidateNotNull()
        {
            if(m_Ptr == null)
                throw new InvalidOperationException("The BlobAssetReference is null.");

            ValidateNonBurst();
            ValidateBurst();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        public void ValidateAllowNull()
        {
            if (m_Ptr == null)
                return;

            ValidateNonBurst();
            ValidateBurst();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void ValidateNotDeserialized()
        {
            if (Header->Allocator == Allocator.None)
                throw new InvalidOperationException("It's not possible to release a blob asset reference that was deserialized. It will be automatically released when the scene is unloaded ");
            Header->Invalidate();
        }

        public void Dispose()
        {
            ValidateNotNull();
            ValidateNotDeserialized();
            ChiselMemory.Unmanaged.Free(Header, Header->Allocator);
            m_Ptr = null;
        }
    }

    /// <summary>
    /// A reference to a blob asset stored in unmanaged memory.
    /// </summary>
    /// <remarks>Create a blob asset using a <see cref="ChiselBlobBuilder"/> or by deserializing a serialized blob asset.</remarks>
    /// <typeparam name="T">The struct data type defining the data structure of the blob asset.</typeparam>
    public unsafe struct ChiselBlobAssetReference<T> : IDisposable, IEquatable<ChiselBlobAssetReference<T>>
        where T : struct
    {
        internal ChiselBlobAssetReferenceData m_data;
        /// <summary>
        /// Reports whether this instance references a valid blob asset.
        /// </summary>
        /// <value>True, if this instance references a valid blob instance.</value>
        public bool IsCreated
        {
            get { return m_data.m_Ptr != null; }
        }

        /// <summary>
        /// Provides an unsafe pointer to the blob asset data.
        /// </summary>
        /// <remarks>You can only use unsafe pointers in [unsafe contexts].
        /// [unsafe contexts]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/unsafe-code
        /// </remarks>
        /// <returns>An unsafe pointer. The pointer is null for invalid BlobAssetReference instances.</returns>
        public void* GetUnsafePtr()
        {
            m_data.ValidateAllowNull();
            return m_data.m_Ptr;
        }

        /// <summary>
        /// Destroys the referenced blob asset and frees its memory.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if you attempt to dispose a blob asset that loaded as
        /// part of a scene or subscene.</exception>
        public void Dispose()
        {
            m_data.Dispose();
        }

        /// <summary>
        /// A reference to the blob asset data.
        /// </summary>
        /// <remarks>The property is a
        /// <see href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/ref-returns">
        /// reference return</see>.</remarks>
        /// <typeparam name="T">The struct type stored in the blob asset.</typeparam>
        /// <value>The root data structure of the blob asset data.</value>
        public ref T Value
        {
            get
            {
                m_data.ValidateNotNull();
                return ref UnsafeUtility.AsRef<T>(m_data.m_Ptr);
            }
        }


        /// <summary>
        /// Creates a blob asset from a pointer to data and a specified size.
        /// </summary>
        /// <remarks>The blob asset is created in unmanaged memory. Call <see cref="Dispose"/> to free the asset memory
        /// when it is no longer needed. This function can only be used in an [unsafe context].
        /// [unsafe context]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/unsafe-code
        /// </remarks>
        /// <param name="ptr">A pointer to the buffer containing the data to store in the blob asset.</param>
        /// <param name="length">The length of the buffer in bytes.</param>
        /// <returns>A reference to newly created blob asset.</returns>
        /// <seealso cref="ChiselBlobBuilder"/>
        public static ChiselBlobAssetReference<T> Create(void* ptr, int length)
        {
            byte* buffer =
                (byte*)ChiselMemory.Unmanaged.Allocate(sizeof(ChiselBlobAssetHeader) + length, 16, Allocator.Persistent);
            UnsafeUtility.MemCpy(buffer + sizeof(ChiselBlobAssetHeader), ptr, length);

            ChiselBlobAssetHeader* header = (ChiselBlobAssetHeader*) buffer;
            *header = new ChiselBlobAssetHeader();

            header->Length = length;
            header->Allocator = Allocator.Persistent;

            // @TODO use 64bit hash
            header->Hash = math.hash(ptr, length);

            ChiselBlobAssetReference<T> blobAssetReference;
            blobAssetReference.m_data.m_Align8Union = 0;
            header->ValidationPtr = blobAssetReference.m_data.m_Ptr = buffer + sizeof(ChiselBlobAssetHeader);
            return blobAssetReference;
        }

        /// <summary>
        /// Creates a blob asset from a byte array.
        /// </summary>
        /// <remarks>The blob asset is created in unmanaged memory. Call <see cref="Dispose"/> to free the asset memory
        /// when it is no longer needed. This function can only be used in an [unsafe context].
        /// [unsafe context]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/unsafe-code
        /// </remarks>
        /// <param name="data">The byte array containing the data to store in the blob asset.</param>
        /// <returns>A reference to newly created blob asset.</returns>
        /// <seealso cref="ChiselBlobBuilder"/>
        public static ChiselBlobAssetReference<T> Create(byte[] data)
        {
            fixed (byte* ptr = &data[0])
            {
                return Create(ptr, data.Length);
            }
        }

        /// <summary>
        /// Creates a blob asset from an instance of a struct.
        /// </summary>
        /// <remarks>The struct must only contain blittable fields (primitive types, fixed-length arrays, or other structs
        /// meeting these same criteria). The blob asset is created in unmanaged memory. Call <see cref="Dispose"/> to
        /// free the asset memory when it is no longer needed. This function can only be used in an [unsafe context].
        /// [unsafe context]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/unsafe-code</remarks>
        /// <param name="value">An instance of <typeparamref name="T"/>.</param>
        /// <returns>A reference to newly created blob asset.</returns>
        /// <seealso cref="ChiselBlobBuilder"/>
        public static ChiselBlobAssetReference<T> Create(T value)
        {
            return Create(UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Construct a BlobAssetReference from the blob data
        /// </summary>
        /// <param name="blobData">The blob data to attach to the returned object</param>
        /// <returns>The created BlobAssetReference</returns>
        internal static ChiselBlobAssetReference<T> Create(ChiselBlobAssetReferenceData blobData)
        {
            return new ChiselBlobAssetReference<T> { m_data = blobData };
        }
        /*
        public static bool TryRead<U>(U binaryReader, int version, out BlobAssetReference<T> result)
        where U : BinaryReader
        {
            var storedVersion = binaryReader.ReadInt();
            if (storedVersion != version)
            {
                result = default;
                return false;
            }
            result = binaryReader.Read<T>();
            return true;
        }

#if !UNITY_DOTSRUNTIME
        /// <summary>
        /// Reads bytes from a fileName, validates the expected serialized version, and deserializes them into a new blob asset.
        /// </summary>
        /// <param name="path">The path of the blob data to read.</param>
        /// <param name="version">Expected version number of the blob data.</param>
        /// <param name="result">The resulting BlobAssetReference if the data was read successful.</param>
        /// <returns>A bool if the read was successful or not.</returns>
        public static bool TryRead(string path, int version, out BlobAssetReference<T> result)
        {
            if (string.IsNullOrEmpty(path))
            {
                result = default;
                return false;
            }
            using (var binaryReader = new StreamBinaryReader(path, UnsafeUtility.SizeOf<T>() + sizeof(int)))
            {
                return TryRead(binaryReader, version, out result);
            }
        }
#else
        /// <summary>
        /// Reads bytes from a buffer, validates the expected serialized version, and deserializes them into a new blob asset.
        /// </summary>
        /// <param name="data">A byte stream of the blob data to read.</param>
        /// <param name="version">Expected version number of the blob data.</param>
        /// <param name="result">The resulting BlobAssetReference if the data was read successful.</param>
        /// <returns>A bool if the read was successful or not.</returns>
        public static bool TryRead(byte* data, int version, out BlobAssetReference<T> result)
        {
            var binaryReader = new MemoryBinaryReader(data);
            var storedVersion = binaryReader.ReadInt();
            if (storedVersion != version)
            {
                result = default;
                return false;
            }

            result = binaryReader.Read<T>();

            return true;
        }
#endif

        public static void Write<U>(U writer, BlobBuilder builder, int verison)
        where U : BinaryWriter
        {
            using (var asset = builder.CreateBlobAssetReference<T>(Allocator.TempJob))
            {
                writer.Write(verison);
                writer.Write(asset);
            }
        }
#if !NET_DOTS
        /// <summary>
        /// Writes the blob data to a path with serialized version.
        /// </summary>
        /// <param name="builder">The BlobBuilder containing the blob to write.</param>
        /// <param name="path">The path to write the blob data.</param>
        /// <param name="version">Serialized version number of the blob data.</param>
        public static void Write(BlobBuilder builder, string path, int verison)
        {
            using (var writer = new StreamBinaryWriter(path))
            {
                Write(writer, builder, verison);
            }
        }
#endif*/

        /// <summary>
        /// A "null" blob asset reference that can be used to test if a BlobAssetReference instance
        /// </summary>
        public static ChiselBlobAssetReference<T> Null => new ChiselBlobAssetReference<T>();

        /// <summary>
        /// Two BlobAssetReferences are equal when they reference the same data.
        /// </summary>
        /// <param name="lhs">The BlobAssetReference on the left side of the operator.</param>
        /// <param name="rhs">The BlobAssetReference on the right side of the operator.</param>
        /// <returns>True, if both references point to the same data or if both are <see cref="Null"/>.</returns>
        public static bool operator ==(ChiselBlobAssetReference<T> lhs, ChiselBlobAssetReference<T> rhs)
        {
            return lhs.m_data.m_Ptr == rhs.m_data.m_Ptr;
        }

        /// <summary>
        /// Two BlobAssetReferences are not equal unless they reference the same data.
        /// </summary>
        /// <param name="lhs">The BlobAssetReference on the left side of the operator.</param>
        /// <param name="rhs">The BlobAssetReference on the right side of the operator.</param>
        /// <returns>True, if the references point to different data in memory or if one is <see cref="Null"/>.</returns>
        public static bool operator !=(ChiselBlobAssetReference<T> lhs, ChiselBlobAssetReference<T> rhs)
        {
            return lhs.m_data.m_Ptr != rhs.m_data.m_Ptr;
        }

        /// <summary>
        /// Two BlobAssetReferences are equal when they reference the same data.
        /// </summary>
        /// <param name="other">The reference to compare to this one.</param>
        /// <returns>True, if both references point to the same data or if both are <see cref="Null"/>.</returns>
        public bool Equals(ChiselBlobAssetReference<T> other)
        {
            return m_data.Equals(other.m_data);
        }

        /// <summary>
        /// Two BlobAssetReferences are equal when they reference the same data.
        /// </summary>
        /// <param name="obj">The object to compare to this reference</param>
        /// <returns>True, if the object is a BlobAssetReference instance that references to the same data as this one,
        /// or if both objects are <see cref="Null"/> BlobAssetReference instances.</returns>
        public override bool Equals(object obj)
        {
            return this == (ChiselBlobAssetReference<T>)obj;
        }

        /// <summary>
        /// Generates the hash code for this object.
        /// </summary>
        /// <returns>A standard C# value-type hash code.</returns>
        public override int GetHashCode()
        {
            return m_data.GetHashCode();
        }
    }

    /// <summary>
    /// A pointer referencing a struct, array, or field inside a blob asset.
    /// </summary>
    /// <typeparam name="T">The data type of the referenced object.</typeparam>
    /// <seealso cref="ChiselBlobBuilder"/>
    unsafe public struct ChiselBlobPtr<T> where T : struct
    {
        internal int m_OffsetPtr;

        /// <summary>
        /// Returns 'true' if this is a valid pointer (not null)
        /// </summary>
        public bool IsValid => m_OffsetPtr != 0;

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void AssertIsValid()
        {
            if (!IsValid)
                throw new System.InvalidOperationException("The accessed BlobPtr hasn't been allocated.");
        }

        /// <summary>
        /// The value, of type <typeparamref name="T"/> to which the pointer refers.
        /// </summary>
        /// <remarks>The property is a
        /// <see href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/ref-returns">
        /// reference return</see>.</remarks>
        /// <exception cref="InvalidOperationException">Thrown if the pointer does not reference a valid instance of
        /// a data type.</exception>
        public ref T Value
        {
            get
            {
                AssertIsValid();
                fixed (int* thisPtr = &m_OffsetPtr)
                {
                    return ref UnsafeUtility.AsRef<T>((byte*) thisPtr + m_OffsetPtr);
                }
            }
        }

        /// <summary>
        /// Provides an unsafe pointer to the referenced data.
        /// </summary>
        /// <remarks>You can only use unsafe pointers in [unsafe contexts].
        /// [unsafe contexts]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/unsafe-code
        /// </remarks>
        /// <returns>An unsafe pointer.</returns>
        public void* GetUnsafePtr()
        {
            if (m_OffsetPtr == 0)
                return null;

            fixed (int* thisPtr = &m_OffsetPtr)
            {
                return (byte*) thisPtr + m_OffsetPtr;
            }
        }
    }

    /// <summary>
    ///  An immutable array of value types stored in a blob asset.
    /// </summary>
    /// <remarks>When creating a blob asset, use the <see cref="ChiselBlobBuilderArray{T}"/> provided by a
    /// <see cref="ChiselBlobBuilder"/> instance to set the array elements.</remarks>
    /// <typeparam name="T">The data type of the elements in the array. Must be a struct or other value type.</typeparam>
    /// <seealso cref="ChiselBlobBuilder"/>
    unsafe public struct ChiselBlobArray<T> where T : unmanaged
    {
        internal int m_OffsetPtr;
        internal int m_Length;

        /// <summary>
        /// The number of elements in the array.
        /// </summary>
        public int Length
        {
            get { return m_Length; }
        }

        /// <summary>
        /// Provides an unsafe pointer to the array data.
        /// </summary>
        /// <remarks>You can only use unsafe pointers in [unsafe contexts].
        /// [unsafe contexts]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/unsafe-code
        /// </remarks>
        /// <returns>An unsafe pointer.</returns>
        public void* GetUnsafePtr()
        {
            // for an unallocated array this will return an invalid pointer which is ok since it
            // should never be accessed as Length will be 0
            fixed (int* thisPtr = &m_OffsetPtr)
            {
                return (byte*) thisPtr + m_OffsetPtr;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void AssertIndexInRange(int index)
        {
            if ((uint)index >= (uint)m_Length)
                throw new System.IndexOutOfRangeException(string.Format("Index {0} is out of range Length {1}",
                    index, m_Length));
        }

        /// <summary>
        /// The element of the array at the <paramref name="index"/> position.
        /// </summary>
        /// <param name="index">The array index.</param>
        /// <remarks>The array element is a
        /// <see href="https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/ref-returns">
        /// reference return</see>.</remarks>
        /// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="index"/> is out of bounds.</exception>
        public ref T this[int index]
        {
            get
            {
                AssertIndexInRange(index);

                fixed (int* thisPtr = &m_OffsetPtr)
                {
                    return ref UnsafeUtility.ArrayElementAsRef<T>((byte*) thisPtr + m_OffsetPtr, index);
                }
            }
        }

        /// <summary>
        /// Copies the elements of this BlobArray to a new managed array.
        /// </summary>
        /// <returns>An array containing copies of the elements of the BlobArray.</returns>
        public T[] ToArray()
        {
            var result = new T[m_Length];
            if (m_Length > 0)
            {
                var src = GetUnsafePtr();

                var handle = GCHandle.Alloc(result, GCHandleType.Pinned);
                var addr = handle.AddrOfPinnedObject();

                UnsafeUtility.MemCpy((void*)addr, src, m_Length * UnsafeUtility.SizeOf<T>());

                handle.Free();
            }
            return result;
        }
    }

    /// <summary>
    /// An untyped reference to a blob assets. ChiselUnsafeUntypedBlobAssetReference can be cast to specific typed BlobAssetReferences.
    /// </summary>
    public struct ChiselUnsafeUntypedBlobAssetReference : IDisposable, IEquatable<ChiselUnsafeUntypedBlobAssetReference>
    {
        internal ChiselBlobAssetReferenceData m_data;

        public static ChiselUnsafeUntypedBlobAssetReference Create<T> (ChiselBlobAssetReference<T> blob) where T : struct
        {
            ChiselUnsafeUntypedBlobAssetReference value;
            value.m_data = blob.m_data;
            return value;
        }

        public ChiselBlobAssetReference<T> Reinterpret<T>() where T : struct
        {
            ChiselBlobAssetReference<T> value;
            value.m_data = m_data;
            return value;
        }

        public void Dispose()
        {
            m_data.Dispose();
        }

        public bool Equals(ChiselUnsafeUntypedBlobAssetReference other)
        {
            return m_data.Equals(other.m_data);
        }
    }
}
