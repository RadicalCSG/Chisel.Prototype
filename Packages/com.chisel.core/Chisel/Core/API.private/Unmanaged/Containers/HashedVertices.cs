using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{

    static class HashedVerticesUtility
    {
        // TODO: measure the hash function and see how well it works
        const long kHashMagicValue = (long)1099511628211ul;

        public static int GetHash(int3 index)
        {
            var hashCode = (uint)((index.y ^ ((index.x ^ index.z) * kHashMagicValue)) * kHashMagicValue);
            var hashIndex = ((int)(hashCode % HashedVertices.kHashTableSize)) + 1;
            return hashIndex;
        }

        
        public unsafe static float3 SnapToExistingVertex(ushort* hashTable, UnsafeList<ushort>* chainedIndices, UnsafeList<float3>* vertices, float3 vertex)
        {
            var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCellSize));
            var offsets = stackalloc int3[]
            {
                new int3(-1, -1, -1), new int3(-1, -1,  0), new int3(-1, -1, +1),
                new int3(-1,  0, -1), new int3(-1,  0,  0), new int3(-1,  0, +1),                
                new int3(-1, +1, -1), new int3(-1, +1,  0), new int3(-1, +1, +1),

                new int3( 0, -1, -1), new int3( 0, -1,  0), new int3( 0, -1, +1),
                new int3( 0,  0, -1), new int3( 0,  0,  0), new int3( 0,  0, +1),                
                new int3( 0, +1, -1), new int3( 0, +1,  0), new int3( 0, +1, +1),

                new int3(+1, -1, -1), new int3(+1, -1,  0), new int3(+1, -1, +1),
                new int3(+1,  0, -1), new int3(+1,  0,  0), new int3(+1,  0, +1),                
                new int3(+1, +1, -1), new int3(+1, +1,  0), new int3(+1, +1, +1)
            };

            float3* verticesPtr = (float3*)vertices->Ptr;

            ushort closestVertexIndex = ushort.MaxValue;
            for (int i = 0; i < 3 * 3 * 3; i++)
            {
                var index = centerIndex + offsets[i];
                var chainIndex = ((int)hashTable[GetHash(index)]) - 1;
                {
                    float closestDistance = CSGConstants.kSqrVertexEqualEpsilon;
                    while (chainIndex != -1)
                    {
                        var nextChainIndex  = ((int)((ushort*)chainedIndices->Ptr)[chainIndex]) - 1;
                        var sqrDistance     = math.lengthsq(verticesPtr[chainIndex] - vertex);
                        if (sqrDistance < closestDistance)
                        {
                            closestVertexIndex = (ushort)chainIndex;
                            closestDistance = sqrDistance;
                        }
                        chainIndex = nextChainIndex;
                    }
                }
            }
            if (closestVertexIndex == ushort.MaxValue)
                return vertex;
            
            return verticesPtr[closestVertexIndex];
        }
        
        public unsafe static void ReplaceIfExists(ushort* hashTable, UnsafeList<ushort>* chainedIndices, UnsafeList<float3>* vertices, float3 vertex)
        {
            var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCellSize));
            var offsets = stackalloc int3[]
            {
                new int3(-1, -1, -1), new int3(-1, -1,  0), new int3(-1, -1, +1),
                new int3(-1,  0, -1), new int3(-1,  0,  0), new int3(-1,  0, +1),                
                new int3(-1, +1, -1), new int3(-1, +1,  0), new int3(-1, +1, +1),

                new int3( 0, -1, -1), new int3( 0, -1,  0), new int3( 0, -1, +1),
                new int3( 0,  0, -1), new int3( 0,  0,  0), new int3( 0,  0, +1),                
                new int3( 0, +1, -1), new int3( 0, +1,  0), new int3( 0, +1, +1),

                new int3(+1, -1, -1), new int3(+1, -1,  0), new int3(+1, -1, +1),
                new int3(+1,  0, -1), new int3(+1,  0,  0), new int3(+1,  0, +1),                
                new int3(+1, +1, -1), new int3(+1, +1,  0), new int3(+1, +1, +1)
            };

            float3* verticesPtr = (float3*)vertices->Ptr;

            ushort closestVertexIndex = ushort.MaxValue;
            for (int i = 0; i < 3 * 3 * 3; i++)
            {
                var index = centerIndex + offsets[i];
                var chainIndex = ((int)hashTable[GetHash(index)]) - 1;
                {
                    float closestDistance = CSGConstants.kSqrVertexEqualEpsilon;
                    while (chainIndex != -1)
                    {
                        var nextChainIndex  = ((int)((ushort*)chainedIndices->Ptr)[chainIndex]) - 1;
                        var sqrDistance     = math.lengthsq(verticesPtr[chainIndex] - vertex);
                        if (sqrDistance < closestDistance)
                        {
                            closestVertexIndex = (ushort)chainIndex;
                            closestDistance = sqrDistance;
                        }
                        chainIndex = nextChainIndex;
                    }
                }
            }
            if (closestVertexIndex == ushort.MaxValue)
                return;
            
            verticesPtr[closestVertexIndex] = vertex;
        }
        
        // Add but make the assumption we're not growing any list
        public unsafe static ushort AddNoResize(ushort* hashTable, UnsafeList<ushort>* chainedIndices, UnsafeList<float3>* vertices, float3 vertex)
        {
            var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCellSize));
            var offsets = stackalloc int3[]
            {
                new int3(-1, -1, -1), new int3(-1, -1,  0), new int3(-1, -1, +1),
                new int3(-1,  0, -1), new int3(-1,  0,  0), new int3(-1,  0, +1),                
                new int3(-1, +1, -1), new int3(-1, +1,  0), new int3(-1, +1, +1),

                new int3( 0, -1, -1), new int3( 0, -1,  0), new int3( 0, -1, +1),
                new int3( 0,  0, -1), new int3( 0,  0,  0), new int3( 0,  0, +1),                
                new int3( 0, +1, -1), new int3( 0, +1,  0), new int3( 0, +1, +1),

                new int3(+1, -1, -1), new int3(+1, -1,  0), new int3(+1, -1, +1),
                new int3(+1,  0, -1), new int3(+1,  0,  0), new int3(+1,  0, +1),                
                new int3(+1, +1, -1), new int3(+1, +1,  0), new int3(+1, +1, +1)
            };

            float3* verticesPtr = (float3*)vertices->Ptr;

            ushort closestVertexIndex = ushort.MaxValue;
            for (int i = 0; i < 3 * 3 * 3; i++)
            {
                var index = centerIndex + offsets[i];
                var chainIndex = ((int)hashTable[GetHash(index)]) - 1;
                {
                    float closestDistance = CSGConstants.kSqrVertexEqualEpsilon;
                    while (chainIndex != -1)
                    {
                        var nextChainIndex  = ((int)((ushort*)chainedIndices->Ptr)[chainIndex]) - 1;
                        var sqrDistance     = math.lengthsq(verticesPtr[chainIndex] - vertex);
                        if (sqrDistance < closestDistance)
                        {
                            closestVertexIndex = (ushort)chainIndex;
                            closestDistance = sqrDistance;
                        }
                        chainIndex = nextChainIndex;
                    }
                }
            }
            if (closestVertexIndex != ushort.MaxValue)
                return closestVertexIndex;

            // Add Unique vertex
            {

                var hashCode        = GetHash(centerIndex);
                var prevChainIndex  = hashTable[hashCode];
                var newChainIndex   = chainedIndices->Length;
                vertices      ->AddNoResize(vertex);
                chainedIndices->AddNoResize((ushort)prevChainIndex);
                hashTable[(int)hashCode] = (ushort)(newChainIndex + 1);
                return (ushort)newChainIndex;
            }
        }

        // Add but make the assumption we're not growing any list
        public unsafe static ushort Add(ushort* hashTable, UnsafeList<ushort>* chainedIndices, UnsafeList<float3>* vertices, float3 vertex)
        {
            var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCellSize));
            var offsets = stackalloc int3[]
            {
                new int3(-1, -1, -1), new int3(-1, -1,  0), new int3(-1, -1, +1),
                new int3(-1,  0, -1), new int3(-1,  0,  0), new int3(-1,  0, +1),                
                new int3(-1, +1, -1), new int3(-1, +1,  0), new int3(-1, +1, +1),

                new int3( 0, -1, -1), new int3( 0, -1,  0), new int3( 0, -1, +1),
                new int3( 0,  0, -1), new int3( 0,  0,  0), new int3( 0,  0, +1),                
                new int3( 0, +1, -1), new int3( 0, +1,  0), new int3( 0, +1, +1),

                new int3(+1, -1, -1), new int3(+1, -1,  0), new int3(+1, -1, +1),
                new int3(+1,  0, -1), new int3(+1,  0,  0), new int3(+1,  0, +1),                
                new int3(+1, +1, -1), new int3(+1, +1,  0), new int3(+1, +1, +1)
            };

            float3* verticesPtr = (float3*)vertices->Ptr;

            ushort closestVertexIndex = ushort.MaxValue;
            for (int i = 0; i < 3 * 3 * 3; i++)
            {
                var index = centerIndex + offsets[i];
                var chainIndex = ((int)hashTable[GetHash(index)]) - 1;
                {
                    float closestDistance = CSGConstants.kSqrVertexEqualEpsilon;
                    while (chainIndex != -1)
                    {
                        var nextChainIndex = ((int)((ushort*)chainedIndices->Ptr)[chainIndex]) - 1;
                        var sqrDistance     = math.lengthsq(verticesPtr[chainIndex] - vertex);
                        if (sqrDistance < closestDistance)
                        {
                            closestVertexIndex = (ushort)chainIndex;
                            closestDistance = sqrDistance;
                        }
                        chainIndex = nextChainIndex;
                    }
                }
            }
            if (closestVertexIndex != ushort.MaxValue)
                return closestVertexIndex;

            // Add Unique vertex
            {

                var hashCode        = GetHash(centerIndex);
                var prevChainIndex  = hashTable[hashCode];
                var newChainIndex   = chainedIndices->Length;
                vertices      ->Add(vertex);
                chainedIndices->Add((ushort)prevChainIndex);
                hashTable[(int)hashCode] = (ushort)(newChainIndex + 1);
                return (ushort)newChainIndex;
            }
        }

        // Add but make the assumption we're not growing any list
        public unsafe static ushort AddNoResize(ushort* hashTable, ushort* chainedIndicesPtr, float3* verticesPtr, ref int verticesLength, float3 vertex)
        {
            var centerIndex = new int3((int)(vertex.x / HashedVertices.kCellSize), (int)(vertex.y / HashedVertices.kCellSize), (int)(vertex.z / HashedVertices.kCellSize));
            var offsets = stackalloc int3[]
            {
                new int3(-1, -1, -1), new int3(-1, -1,  0), new int3(-1, -1, +1),
                new int3(-1,  0, -1), new int3(-1,  0,  0), new int3(-1,  0, +1),                
                new int3(-1, +1, -1), new int3(-1, +1,  0), new int3(-1, +1, +1),

                new int3( 0, -1, -1), new int3( 0, -1,  0), new int3( 0, -1, +1),
                new int3( 0,  0, -1), new int3( 0,  0,  0), new int3( 0,  0, +1),                
                new int3( 0, +1, -1), new int3( 0, +1,  0), new int3( 0, +1, +1),

                new int3(+1, -1, -1), new int3(+1, -1,  0), new int3(+1, -1, +1),
                new int3(+1,  0, -1), new int3(+1,  0,  0), new int3(+1,  0, +1),                
                new int3(+1, +1, -1), new int3(+1, +1,  0), new int3(+1, +1, +1)
            };

            ushort closestVertexIndex = ushort.MaxValue;
            for (int i = 0; i < 3 * 3 * 3; i++)
            {
                var index = centerIndex + offsets[i];
                var chainIndex = ((int)hashTable[GetHash(index)]) - 1;
                {
                    float closestDistance = CSGConstants.kSqrVertexEqualEpsilon;
                    while (chainIndex != -1)
                    {
                        var nextChainIndex  = chainedIndicesPtr[chainIndex] - 1;
                        var sqrDistance     = math.lengthsq(verticesPtr[chainIndex] - vertex);
                        if (sqrDistance < closestDistance)
                        {
                            closestVertexIndex = (ushort)chainIndex;
                            closestDistance = sqrDistance;
                        }
                        chainIndex = nextChainIndex;
                    }
                }
            }
            if (closestVertexIndex != ushort.MaxValue)
                return closestVertexIndex;

            // Add Unique vertex
            {
                var newChainIndex = verticesLength;
                verticesLength++;

                var hashCode = GetHash(centerIndex);
                var prevChainIndex = hashTable[hashCode];
                hashTable[(int)hashCode] = (ushort)(newChainIndex + 1);

                verticesPtr[newChainIndex] = vertex;
                chainedIndicesPtr[newChainIndex] = (ushort)prevChainIndex;
                                
                return (ushort)newChainIndex;
            }
        }
    }

    // TODO: make this safely writable in parallel / maybe check out NativeMultiHashMap? 
    //       write multiple vertices -> combine? but what about indices? seperate vertex generation from getting indices?
    [NativeContainer, BurstCompile(CompileSynchronously = true)]
    public unsafe struct HashedVertices : INativeDisposable
    {
        public const ushort     kMaxVertexCount = 65000;
        internal const uint     kHashTableSize  = 509u;
        internal const float    kCellSize       = CSGConstants.kVertexEqualEpsilon * 2.5f;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#if UNITY_2020_1_OR_NEWER
        private static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<HashedVertices>();
        [BurstDiscard]
        private static void CreateStaticSafetyId()
        {
            s_staticSafetyId.Data = AtomicSafetyHandle.NewStaticSafetyId<HashedVertices>();
        }
#endif
        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel m_DisposeSentinel;
#endif
        [NativeDisableUnsafePtrRestriction] internal UnsafeList<float3>*    m_Vertices;
        [NativeDisableUnsafePtrRestriction] internal UnsafeList<ushort>*    m_ChainedIndices;
        [NativeDisableUnsafePtrRestriction] internal void*                  m_HashTable;

        // Keep track of where the memory for this was allocated
        Allocator m_AllocatorLabel;

        public bool IsCreated => m_Vertices != null && m_ChainedIndices != null && m_ChainedIndices != null;

        public NativeArray<float3> AsArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            CheckAllocated(m_Vertices);
            var arraySafety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
#endif
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float3>(m_Vertices->Ptr, m_Vertices->Length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
            return array;
        }

        #region Constructors

        public HashedVertices(int minCapacity, Allocator allocator = Allocator.Persistent)
            : this(minCapacity, minCapacity, allocator, 2)
        {
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckCapacityInRange(long value, long length)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");

            if (value < length)
                throw new ArgumentOutOfRangeException($"Value {value} is out of range in NativeListArray of '{length}' Length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private void CheckAllocator(Allocator a)
        {
            if (a <= Allocator.None)
            {
                throw new Exception("Allocator must be Temp, TempJob or Persistent.");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckArgPositive(int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException($"Value {value} must be positive.");
        }

        HashedVertices(int vertexCapacity, int chainedIndicesCapacity, Allocator allocator, int disposeSentinelStackDepth)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent.
            CheckAllocator(allocator);
            CheckArgPositive(chainedIndicesCapacity);

            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, disposeSentinelStackDepth, allocator);
#if UNITY_2020_1_OR_NEWER
            if (s_staticSafetyId.Data == 0)
            {
                CreateStaticSafetyId();
            }
            AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, s_staticSafetyId.Data);
#endif
#endif
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#endif
            m_AllocatorLabel = allocator;
            var hashTableMemSize = (ushort)(kHashTableSize + 1) * UnsafeUtility.SizeOf<ushort>();
            m_HashTable = UnsafeUtility.Malloc(hashTableMemSize, UnsafeUtility.AlignOf<ushort>(), m_AllocatorLabel);
            UnsafeUtility.MemClear(m_HashTable, hashTableMemSize);
            
            m_Vertices          = UnsafeList<float3>.Create(vertexCapacity, allocator);
            m_ChainedIndices    = UnsafeList<ushort>.Create(chainedIndicesCapacity, allocator);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.SetBumpSecondaryVersionOnScheduleWrite(m_Safety, true);
#endif
        }

        public HashedVertices(HashedVertices otherHashedVertices, Allocator allocator = Allocator.Persistent)
            : this((otherHashedVertices.m_Vertices != null) ? otherHashedVertices.m_Vertices->Length : 1, (otherHashedVertices.m_ChainedIndices != null) ? otherHashedVertices.m_ChainedIndices->Length : 1, allocator, 2)
        {
            CheckAllocated(otherHashedVertices);
            m_ChainedIndices->AddRangeNoResize(*otherHashedVertices.m_ChainedIndices);
            m_Vertices->AddRangeNoResize(*otherHashedVertices.m_Vertices);
        }

        public HashedVertices([ReadOnly] ref ChiselBlobArray<float3> uniqueVertices, Allocator allocator = Allocator.Persistent)
            : this(uniqueVertices.Length, allocator)
        {
            // Add Unique vertex
            for (int i = 0; i < uniqueVertices.Length; i++)
            {
                var vertex = uniqueVertices[i];

                var centerIndex     = new int3((int)(vertex.x / kCellSize), (int)(vertex.y / kCellSize), (int)(vertex.z / kCellSize));
                var hashCode        = HashedVerticesUtility.GetHash(centerIndex);
                var prevChainIndex  = ((ushort*)m_HashTable)[hashCode];
                var newChainIndex   = m_ChainedIndices->Length;
                m_Vertices      ->AddNoResize(vertex);
                m_ChainedIndices->AddNoResize((ushort)prevChainIndex);
                ((ushort*)m_HashTable)[(int)hashCode] = (ushort)(newChainIndex + 1);
            }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#endif
            UnsafeList<float3>.Destroy(m_Vertices);
            m_Vertices = null;
            UnsafeList<ushort>.Destroy(m_ChainedIndices);
            m_ChainedIndices = null;
            UnsafeUtility.Free(m_HashTable, m_AllocatorLabel);
            m_HashTable = null;
        }
        #endregion

        #region UnsafeDisposeJob
        internal static bool ShouldDeallocate(Allocator allocator)
        {
            // Allocator.Invalid == container is not initialized.
            // Allocator.None    == container is initialized, but container doesn't own data.
            return allocator > Allocator.None;
        }


        //[BurstCompile(CompileSynchronously = true)]
        internal unsafe struct UnsafeDisposeJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public UnsafeList<float3>* vertices;
            [NativeDisableUnsafePtrRestriction] public UnsafeList<ushort>* chainedIndices;
            [NativeDisableUnsafePtrRestriction] public void* hashTable;
            public Allocator allocator;

            public void Execute()
            {
                UnsafeList<float3>.Destroy(vertices);
                UnsafeList<ushort>.Destroy(chainedIndices);
                UnsafeUtility.Free(hashTable, allocator);
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

            var jobHandle = new UnsafeDisposeJob { vertices = m_Vertices, chainedIndices = m_ChainedIndices, hashTable = m_HashTable, allocator = m_AllocatorLabel }.Schedule(inputDeps);

            AtomicSafetyHandle.Release(m_Safety);
#else
            var jobHandle = new UnsafeDisposeJob { vertices = m_Vertices, chainedIndices = m_ChainedIndices, hashTable = m_HashTable, allocator = m_AllocatorLabel }.Schedule(inputDeps);
#endif 
            return jobHandle;
        }
        #endregion

        #region Checks
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void CheckIndexInRange(int value, int length)
        {
            if (value < 0)
                throw new IndexOutOfRangeException($"Value {value} must be positive.");

            if ((uint)value >= (uint)length)
                throw new IndexOutOfRangeException($"Value {value} is out of range of '{length}' Length.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]        
        private static void CheckAllocated<T>(UnsafeList<T>* listData) where T : unmanaged
        {
            if (listData == null || !listData->IsCreated)
                throw new Exception($"Expected {nameof(listData)} to be allocated.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal static void CheckAllocated(HashedVertices otherHashedVertices)
        {
            if (otherHashedVertices.IsCreated)
                throw new ArgumentException($"Value {otherHashedVertices} is not allocated.");
        }
        #endregion

        public void Clear()
        {
            if (m_Vertices != null) m_Vertices->Clear();
            if (m_ChainedIndices != null) m_ChainedIndices->Clear();
            if (m_HashTable != null)
            {
                var hashTableMemSize = (ushort)(kHashTableSize + 1) * UnsafeUtility.SizeOf<ushort>();
                UnsafeUtility.MemClear(m_HashTable, hashTableMemSize);
            }
        }

        public int Capacity
        {
            get
            {
                return m_Vertices->Capacity;
            }
        }

        // Ensure we have at least this many extra vertices in capacity
        public void ReserveAdditionalVertices(int extraIndices)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            var requiredVertCapacity    = m_Vertices->Length + extraIndices;
            var requiredVertices        = m_Vertices->Length + (extraIndices * 2);
            if (m_Vertices->Capacity < requiredVertCapacity)
                m_Vertices->SetCapacity(requiredVertices);

            var requiredIndexCapacity   = m_ChainedIndices->Length + extraIndices;
            var requiredIndices         = m_ChainedIndices->Length + (extraIndices * 2);
            if (m_ChainedIndices->Capacity < requiredIndexCapacity)
                m_ChainedIndices->SetCapacity(requiredIndices);
        }

        /// <summary>
        /// Retrieve a member of the contaner by index.
        /// </summary>
        /// <param name="index">The zero-based index into the list.</param>
        /// <value>The list item at the specified index.</value>
        /// <exception cref="IndexOutOfRangeException">Thrown if index is negative or >= to <see cref="Length"/>.</exception>
        public float3 this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                CheckIndexInRange(index, m_Vertices->Length);
#endif
                return UnsafeUtility.ReadArrayElement<float3>(m_Vertices->Ptr, index);
            }
        }

        public float3* GetUnsafeReadOnlyPtr()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return (float3*)(m_Vertices->Ptr);
        }

        /// <summary>
        /// The current number of items in the list.
        /// </summary>
        /// <value>The item count.</value>
        public int Length
        {
            [return: AssumeRange(0, kMaxVertexCount)]
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return m_Vertices->Length;
            }
        }

        public unsafe float3 GetUniqueVertex(float3 vertex)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            float3* verticesPtr = (float3*)m_Vertices->Ptr;
            return verticesPtr[HashedVerticesUtility.AddNoResize((ushort*)m_HashTable, m_ChainedIndices, m_Vertices, vertex)];
        }

        public unsafe ushort AddNoResize(float3 vertex) 
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            return HashedVerticesUtility.AddNoResize((ushort*)m_HashTable, m_ChainedIndices, m_Vertices, vertex);
        }


        public unsafe void AddUniqueVertices([ReadOnly] ref ChiselBlobArray<float3> uniqueVertices)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            // Add Unique vertex
            for (int i = 0; i < uniqueVertices.Length; i++)
            {
                var vertex = uniqueVertices[i];
                var centerIndex = new int3((int)(vertex.x / kCellSize), (int)(vertex.y / kCellSize), (int)(vertex.z / kCellSize));
                var hashCode = HashedVerticesUtility.GetHash(centerIndex);
                var prevChainIndex = ((ushort*)m_HashTable)[hashCode];
                var newChainIndex = m_ChainedIndices->Length;
                m_Vertices      ->AddNoResize(vertex);
                m_ChainedIndices->AddNoResize((ushort)prevChainIndex);
                ((ushort*)m_HashTable)[(int)hashCode] = (ushort)(newChainIndex + 1);
            }
        }
        
        public unsafe void AddUniqueVertices(in UnsafeList<float3> uniqueVertices)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            // Add Unique vertex
            for (int i = 0; i < uniqueVertices.Length; i++)
            {
                var vertex = uniqueVertices[i];
                var centerIndex = new int3((int)(vertex.x / kCellSize), (int)(vertex.y / kCellSize), (int)(vertex.z / kCellSize));
                var hashCode = HashedVerticesUtility.GetHash(centerIndex);
                var prevChainIndex = ((ushort*)m_HashTable)[hashCode];
                var newChainIndex = m_ChainedIndices->Length;
                m_Vertices      ->AddNoResize(vertex);
                m_ChainedIndices->AddNoResize((ushort)prevChainIndex);
                ((ushort*)m_HashTable)[(int)hashCode] = (ushort)(newChainIndex + 1);
            }
        }
        
        public unsafe void ReplaceIfExists([ReadOnly] ref ChiselBlobArray<float3> uniqueVertices)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            // Add Unique vertex
            for (int i = 0; i < uniqueVertices.Length; i++)
            {
                var vertex = uniqueVertices[i];
                HashedVerticesUtility.ReplaceIfExists((ushort*)m_HashTable, m_ChainedIndices, m_Vertices, vertex);
            }
        }

        public unsafe void ReplaceIfExists([ReadOnly] in UnsafeList<float3> uniqueVertices)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            // Add Unique vertex
            for (int i = 0; i < uniqueVertices.Length; i++)
            {
                var vertex = uniqueVertices[i];
                HashedVerticesUtility.ReplaceIfExists((ushort*)m_HashTable, m_ChainedIndices, m_Vertices, vertex);
            }
        }
    }
}
