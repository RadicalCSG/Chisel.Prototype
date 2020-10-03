using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;

namespace Chisel.Core
{
    public struct AABB
    {
        public AABB(Bounds bounds) { min = bounds.min; max = bounds.max; }
        public float3 min, max;
    }

    public struct IndexOrder : IEquatable<IndexOrder>
    {
        public int nodeIndex;
        public int nodeOrder;

        public bool Equals(IndexOrder other)
        {
            return nodeIndex == other.nodeIndex;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is IndexOrder))
                return false;
            return Equals((IndexOrder)obj);
        }

        public override int GetHashCode()
        {
            return nodeIndex.GetHashCode();
        }
    }

    public struct BrushPair : IEquatable<BrushPair>, IEqualityComparer<BrushPair>, IComparable<BrushPair>, IComparer<BrushPair>
    {
        public int              brushNodeOrder0;
        public int              brushNodeOrder1;
        public IntersectionType type;

        public void Flip()
        {
            if      (type == IntersectionType.AInsideB) type = IntersectionType.BInsideA;
            else if (type == IntersectionType.BInsideA) type = IntersectionType.AInsideB;
            { var t = brushNodeOrder0; brushNodeOrder0 = brushNodeOrder1; brushNodeOrder1 = t; }
        }

        #region Equals
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is BrushPair))
                return false;

            var other = (BrushPair)obj;
            return Equals(other);
        }

        public bool Equals(BrushPair x, BrushPair y) { return x.Equals(y); }

        public bool Equals(BrushPair other)
        {
            return ((brushNodeOrder0 == other.brushNodeOrder0) && 
                    (brushNodeOrder1 == other.brushNodeOrder1));
        }
        #endregion

        #region Compare
        public int Compare(BrushPair x, BrushPair y) { return x.CompareTo(y); }

        public int CompareTo(BrushPair other)
        {
            if (brushNodeOrder0 < other.brushNodeOrder0)
                return -1;
            if (brushNodeOrder0 > other.brushNodeOrder0)
                return 1;
            if (brushNodeOrder1 < other.brushNodeOrder1)
                return -1;
            if (brushNodeOrder1 > other.brushNodeOrder1)
                return 1;
            if (type < other.type)
                return -1;
            if (type > other.type)
                return 1;
            return 0;
        }
        #endregion

        #region GetHashCode
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        public int GetHashCode(BrushPair obj)
        {
            return ((ulong)obj.brushNodeOrder0 + ((ulong)obj.brushNodeOrder1 << 32)).GetHashCode();
        }
        #endregion
    }
    
    public struct BrushIntersectWith
    {
        public int              brushNodeOrder1;
        public IntersectionType type;
    }

    public struct BrushPair2 : IEquatable<BrushPair2>, IEqualityComparer<BrushPair2>, IComparable<BrushPair2>, IComparer<BrushPair2>
    {
        public IndexOrder       brushIndexOrder0;
        public IndexOrder       brushIndexOrder1;
        public IntersectionType type;

        public void Flip()
        {
            if      (type == IntersectionType.AInsideB) type = IntersectionType.BInsideA;
            else if (type == IntersectionType.BInsideA) type = IntersectionType.AInsideB;
            { var t = brushIndexOrder0; brushIndexOrder0 = brushIndexOrder1; brushIndexOrder1 = t; }
        }

        #region Equals
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is BrushPair2))
                return false;

            var other = (BrushPair2)obj;
            return Equals(other);
        }

        public bool Equals(BrushPair2 x, BrushPair2 y) { return x.Equals(y); }

        public bool Equals(BrushPair2 other)
        {
            return ((brushIndexOrder0.nodeOrder == other.brushIndexOrder0.nodeOrder) && 
                    (brushIndexOrder1.nodeOrder == other.brushIndexOrder1.nodeOrder));
        }
        #endregion

        #region Compare
        public int Compare(BrushPair2 x, BrushPair2 y) { return x.CompareTo(y); }

        public int CompareTo(BrushPair2 other)
        {
            if (brushIndexOrder0.nodeOrder < other.brushIndexOrder0.nodeOrder)
                return -1;
            if (brushIndexOrder0.nodeOrder > other.brushIndexOrder0.nodeOrder)
                return 1;
            if (brushIndexOrder1.nodeOrder < other.brushIndexOrder1.nodeOrder)
                return -1;
            if (brushIndexOrder1.nodeOrder > other.brushIndexOrder1.nodeOrder)
                return 1;
            if (type < other.type)
                return -1;
            if (type > other.type)
                return 1;
            return 0;
        }
        #endregion

        #region GetHashCode
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        public int GetHashCode(BrushPair2 obj)
        {
            return ((ulong)obj.brushIndexOrder0.nodeOrder + ((ulong)obj.brushIndexOrder1.nodeOrder << 32)).GetHashCode();
        }
        #endregion
    }

    struct CompactTopDownNode
    {
        // TODO: combine bits
        public CSGNodeType      Type;
        public CSGOperationType Operation;
        public int              nodeIndex;
        public int              childCount;
        public int              childOffset;

        public override string ToString() { return $"({nameof(Type)}: {Type}, {nameof(childCount)}: {childCount}, {nameof(childOffset)}: {childOffset}, {nameof(Operation)}: {Operation}, {nameof(nodeIndex)}: {nodeIndex})"; }
    }

    struct BottomUpNodeIndex
    {
        public int  nodeIndex;      // TODO: might not be needed
        public int  bottomUpStart;
        public int  bottomUpEnd;

        public override string ToString() { return $"({nameof(nodeIndex)}: {nodeIndex}, {nameof(bottomUpStart)}: {bottomUpStart}, {nameof(bottomUpEnd)}: {bottomUpEnd})"; }
    }

    struct CompactTree
    {
        public BlobArray<CompactTopDownNode>        topDownNodes;
        public BlobArray<BottomUpNodeIndex>         bottomUpNodeIndices;
        public BlobArray<int>                       bottomUpNodes;

        public int                                  indexOffset;
        public BlobArray<int>                       brushIndexToBottomUpIndex;

        struct CompactTopDownBuilderNode
        {
            public CSGTreeNode node;
            public int index;
        }


        static readonly List<BottomUpNodeIndex>             s_BottomUpNodeIndices   = new List<BottomUpNodeIndex>();
        static readonly List<int>                           s_BottomUpNodes         = new List<int>();
        static readonly Queue<CompactTopDownBuilderNode>    s_NodeQueue             = new Queue<CompactTopDownBuilderNode>();
        static readonly List<CompactTopDownNode>            s_TopDownNodes          = new List<CompactTopDownNode>();
        static int[]    s_BrushIndexToBottomUpIndex;

        internal static BlobAssetReference<CompactTree> Create(List<CSGManager.NodeHierarchy> nodeHierarchies, int treeNodeIndex)
        {
            var treeInfo            = nodeHierarchies[treeNodeIndex].treeInfo;
            var allTreeBrushes      = treeInfo.allTreeBrushes.items;

            if (allTreeBrushes.Count == 0)
                return BlobAssetReference<CompactTree>.Null;

            s_BottomUpNodeIndices.Clear();
            s_BottomUpNodes.Clear();
            
            var minBrushIndex       = nodeHierarchies.Count;
            var maxBrushIndex       = 0;
            for (int b = 0; b < allTreeBrushes.Count; b++)
            {
                var brushNodeID = allTreeBrushes[b];
                if (brushNodeID == 0)
                    continue;

                minBrushIndex = math.min(brushNodeID - 1, minBrushIndex);
                maxBrushIndex = math.max(brushNodeID - 1, maxBrushIndex);
            }

            if (minBrushIndex == nodeHierarchies.Count)
                minBrushIndex = 0;

            var desiredBrushIndexToBottomUpLength = (maxBrushIndex + 1) - minBrushIndex;
            if (s_BrushIndexToBottomUpIndex == null ||
                s_BrushIndexToBottomUpIndex.Length < desiredBrushIndexToBottomUpLength)
                s_BrushIndexToBottomUpIndex = new int[desiredBrushIndexToBottomUpLength];

            // Bottom-up -> per brush list of all ancestors to root
            for (int b = 0; b < allTreeBrushes.Count; b++)
            {
                var brushNodeID = allTreeBrushes[b];
                if (!CSGManager.IsValidNodeID(brushNodeID))
                    continue;

                var brush = new CSGTreeNode() { nodeID = brushNodeID };
                if (!brush.Valid)
                    continue;

                var parentStart = s_BottomUpNodes.Count;

                var parent      = brush.Parent;
                var treeNodeID  = treeNodeIndex + 1;
                while (parent.Valid && parent.NodeID != treeNodeID)
                {
                    var parentIndex = parent.NodeID - 1;
                    s_BottomUpNodes.Add(parentIndex);
                    parent = parent.Parent;
                }

                var brushNodeIndex  = brushNodeID - 1;
                s_BrushIndexToBottomUpIndex[brushNodeIndex - minBrushIndex] = s_BottomUpNodeIndices.Count;
                s_BottomUpNodeIndices.Add(new BottomUpNodeIndex()
                {
                    nodeIndex  = brushNodeIndex,
                    bottomUpEnd     = s_BottomUpNodes.Count,
                    bottomUpStart   = parentStart
                });
            }

            if (s_BottomUpNodeIndices.Count == 0)
                return BlobAssetReference<CompactTree>.Null;

            // Top-down
            s_NodeQueue.Clear();
            s_TopDownNodes.Clear(); // TODO: set capacity to number of nodes in tree

            s_NodeQueue.Enqueue(new CompactTopDownBuilderNode() { node = new CSGTreeNode() { nodeID =  treeNodeIndex + 1 }, index = 0 });
            s_TopDownNodes.Add(new CompactTopDownNode()
            {
                Type        = CSGNodeType.Tree,
                Operation   = CSGOperationType.Additive,
                nodeIndex   = treeNodeIndex,
            });

            while (s_NodeQueue.Count > 0)
            {
                var parent      = s_NodeQueue.Dequeue();
                var nodeCount   = parent.node.Count;
                if (nodeCount == 0)
                {
                    var item = s_TopDownNodes[parent.index];
                    item.childOffset = -1;
                    item.childCount = 0;
                    s_TopDownNodes[parent.index] = item;
                    continue;
                }

                int firstIndex = 0;
                // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                for (; firstIndex < nodeCount && parent.node[firstIndex].Valid &&
                                    (parent.node[firstIndex].Operation != CSGOperationType.Additive &&
                                     parent.node[firstIndex].Operation != CSGOperationType.Copy); firstIndex++)
                    // NOP
                    ;

                var firstChildIndex = s_TopDownNodes.Count;
                for (int i = firstIndex; i < nodeCount; i++)
                {
                    var child = parent.node[i];
                    // skip invalid nodes (they don't contribute to the mesh)
                    if (!child.Valid)
                        continue;

                    var childType = child.Type;
                    if (childType != CSGNodeType.Brush)
                        s_NodeQueue.Enqueue(new CompactTopDownBuilderNode()
                        {
                            node = child,
                            index = s_TopDownNodes.Count
                        });
                    s_TopDownNodes.Add(new CompactTopDownNode()
                    {
                        Type        = childType,
                        Operation   = child.Operation,
                        nodeIndex   = child.NodeID - 1
                    });
                }

                {
                    var item = s_TopDownNodes[parent.index];
                    item.childOffset = firstChildIndex;
                    item.childCount = s_TopDownNodes.Count - firstChildIndex;
                    s_TopDownNodes[parent.index] = item;
                }
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CompactTree>();
            builder.Construct(ref root.topDownNodes, s_TopDownNodes);
            builder.Construct(ref root.bottomUpNodeIndices, s_BottomUpNodeIndices);
            builder.Construct(ref root.bottomUpNodes, s_BottomUpNodes);
            root.indexOffset = minBrushIndex;
            builder.Construct(ref root.brushIndexToBottomUpIndex, s_BrushIndexToBottomUpIndex, desiredBrushIndexToBottomUpLength);
            var compactTree = builder.CreateBlobAssetReference<CompactTree>(Allocator.Persistent);
            builder.Dispose();

            return compactTree;
        }
    }

    public struct ChiselSurfaceRenderBuffer
    {
        public BlobArray<Int32>		indices;
        public BlobArray<float3>	vertices;
        public BlobArray<float3>	normals;
        public BlobArray<float4>	tangents;
        public BlobArray<float2>    uv0;

        public uint             geometryHash;
        public uint             surfaceHash;

        public SurfaceLayers    surfaceLayers;
        public Int32		    surfaceIndex;
    };

    public struct ChiselBrushRenderBuffer
    {
        public BlobArray<ChiselSurfaceRenderBuffer> surfaces;
    };

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BrushIntersection
    {
        public IndexOrder       nodeIndexOrder;
        public IntersectionType type;
        public int              bottomUpStart;
        public int              bottomUpEnd;

        public override string ToString() { return $"({nameof(nodeIndexOrder.nodeIndex)}: {nodeIndexOrder.nodeIndex}, {nameof(type)}: {type}, {nameof(bottomUpStart)}: {bottomUpStart}, {nameof(bottomUpEnd)}: {bottomUpEnd})"; }
    }

    struct BrushesTouchedByBrush
    {
        public BlobArray<BrushIntersection> brushIntersections;
        public BlobArray<uint>              intersectionBits;
        public int Length;
        public int Offset;

        public IntersectionType Get(int index)
        {
            index -= Offset;
            if (index < 0 || index >= Length)
                return IntersectionType.InvalidValue;

            index <<= 1;
            var int32Index = index >> 5;	// divide by 32
            var bitIndex = index & 31;	    // remainder
            var twoBit = ((UInt32)3) << bitIndex;

            return (IntersectionType)((intersectionBits[int32Index] & twoBit) >> bitIndex);
        }

        public void Set(int index, IntersectionType value)
        {
            index -= Offset;
            if (index < 0 || index >= Length)
                return;

            index <<= 1;
            var int32Index = index >> 5;	// divide by 32
            var bitIndex = index & 31;	    // remainder
            var twoBit = (UInt32)3 << bitIndex;
            var twoBitValue = ((UInt32)value) << bitIndex;

            var originalInt32 = intersectionBits[int32Index];

            intersectionBits[int32Index] = (originalInt32 & ~twoBit) | twoBitValue;
        }
    }
        
    public struct NodeTransformations
    {
        public float4x4 nodeToTree;
        public float4x4 treeToNode;
    };

    // Note: Stored in BlobAsset at runtime/editor-time
    public struct SurfaceInfo
    {
        public ushort               basePlaneIndex;
        public CategoryGroupIndex   interiorCategory;
        //public int                  nodeIndex;
    }

    public struct IndexSurfaceInfo
    {
        public IndexOrder           brushIndexOrder;
        public ushort               basePlaneIndex;
        public CategoryGroupIndex   interiorCategory;
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BasePolygon
    {
        public IndexOrder       nodeIndexOrder;
        public SurfaceInfo      surfaceInfo;
        public int              startEdgeIndex;
        public int              endEdgeIndex;
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BaseSurface
    {
        public SurfaceLayers    layers;
        public float4           localPlane;
        public UVMatrix         UV0;
    }

    internal struct BasePolygonsBlob
    {
        public BlobArray<BasePolygon>   polygons;
        public BlobArray<Edge>          edges;
        public BlobArray<float3>        vertices;
        public BlobArray<BaseSurface>   surfaces;
        public int nodeIndex;
    }
    
    public enum IntersectionType : byte
    {
        NoIntersection,
        Intersection,
        AInsideB,
        BInsideA,

        InvalidValue
    };


    // Note: Stored in BlobAsset at runtime/editor-time
    public struct BrushSurfacePair
    {
        public int brushNodeOrder1; // BrushIntersectionLoop.surfaceInfo has brushNode*INDEX*
        public int basePlaneIndex;  // BrushIntersectionLoop.surfaceInfo has identical basePlaneIndex
    }

    // TODO: Could be optimized further by storing ALL vertices in a single array (somehow),
    // which would allow us to store everything else in a simple struct (no need for BlobAssetReference)
    // Note: Temporary BlobAssetReference that only exists during a single frame
    public struct BrushIntersectionLoop
    {
        public IndexOrder           indexOrder0;
        public IndexOrder           indexOrder1;
        public SurfaceInfo          surfaceInfo;
        public BlobArray<float3>    loopVertices;
    }

    // Note: Temporary BlobAssetReference that only exists during a single frame
    public struct BrushPairIntersection
    {
        public IntersectionType type;
        // Note: that the localSpacePlanes0/localSpacePlaneIndices0 parameters for both brush0 and brush1 are in localspace of >brush0<
        public BlobArray<BrushIntersectionInfo> brushes;
    }

    public struct BrushIntersectionInfo
    {
        public IndexOrder               brushIndexOrder;
        public float4x4                 nodeToTreeSpace;
        public float4x4                 toOtherBrushSpace;

        public BlobArray<PlanePair>     usedPlanePairs;
        public BlobArray<float4>        localSpacePlanes0;         // planes in local space of >brush0<
        public BlobArray<int>           localSpacePlaneIndices0;   // planes indices of >brush0<
        
        public BlobArray<ushort>        vertexIntersectionPlanes;
        public BlobArray<int2>          vertexIntersectionSegments;

        public BlobArray<float3>        usedVertices;
        public BlobArray<SurfaceInfo>   surfaceInfos;
    }


    internal sealed unsafe class ChiselTreeLookup : ScriptableObject
    {
        public unsafe class Data
        {
            public NativeList<int> brushIndices;
            
            public NativeList<BlobAssetReference<BasePolygonsBlob>>             basePolygonCache;
            public NativeList<MinMaxAABB>                                       brushTreeSpaceBoundCache;
            public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache;
            public NativeList<BlobAssetReference<RoutingTable>>                 routingTableCache;
            public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache;
            public NativeList<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache;
            public NativeList<NodeTransformations>                              transformationCache;
            public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache;
            
            public NativeHashMap<int, MinMaxAABB>                                   brushTreeSpaceBoundLookup;
            public NativeHashMap<int, BlobAssetReference<ChiselBrushRenderBuffer>>  brushRenderBufferLookup;

            public BlobAssetReference<CompactTree>                              compactTree;

            internal void Initialize()
            {
                brushIndices = new NativeList<int>(1000, Allocator.Persistent);
                
                brushTreeSpaceBoundLookup    = new NativeHashMap<int, MinMaxAABB>(1000, Allocator.Persistent);
                brushRenderBufferLookup      = new NativeHashMap<int, BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);

                // brushIndex
                basePolygonCache            = new NativeList<BlobAssetReference<BasePolygonsBlob>>(1000, Allocator.Persistent);
                brushTreeSpaceBoundCache    = new NativeList<MinMaxAABB>(1000, Allocator.Persistent);
                treeSpaceVerticesCache      = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(1000, Allocator.Persistent);
                routingTableCache           = new NativeList<BlobAssetReference<RoutingTable>>(1000, Allocator.Persistent);
                brushTreeSpacePlaneCache    = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(1000, Allocator.Persistent);
                brushesTouchedByBrushCache  = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(1000, Allocator.Persistent);
                transformationCache         = new NativeList<NodeTransformations>(1000, Allocator.Persistent);
                brushRenderBufferCache      = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);
            }

            internal void EnsureCapacity(int brushCount)
            {
                if (brushTreeSpaceBoundLookup.Capacity < brushCount)
                    brushTreeSpaceBoundLookup.Capacity = brushCount;

                if (brushRenderBufferLookup.Capacity < brushCount)
                    brushRenderBufferLookup.Capacity = brushCount;

                if (brushIndices.Capacity < brushCount)
                    brushIndices.Capacity = brushCount;

                if (basePolygonCache.Capacity < brushCount)
                    basePolygonCache.Capacity = brushCount;

                if (brushTreeSpaceBoundCache.Capacity < brushCount)
                    brushTreeSpaceBoundCache.Capacity = brushCount;

                if (treeSpaceVerticesCache.Capacity < brushCount)
                    treeSpaceVerticesCache.Capacity = brushCount;

                if (routingTableCache.Capacity < brushCount)
                    routingTableCache.Capacity = brushCount;

                if (brushTreeSpacePlaneCache.Capacity < brushCount)
                    brushTreeSpacePlaneCache.Capacity = brushCount;

                if (brushesTouchedByBrushCache.Capacity < brushCount)
                    brushesTouchedByBrushCache.Capacity = brushCount;

                if (transformationCache.Capacity < brushCount)
                    transformationCache.Capacity = brushCount;

                if (brushRenderBufferCache.Capacity < brushCount)
                    brushRenderBufferCache.Capacity = brushCount;
            }

            internal void Dispose()
            {
                if (brushIndices.IsCreated)
                    brushIndices.Dispose();
                brushIndices = default;
                if (basePolygonCache.IsCreated)
                {
                    foreach (var item in basePolygonCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    basePolygonCache.Clear();
                    basePolygonCache.Dispose();
                }
                basePolygonCache = default;
                if (brushTreeSpaceBoundCache.IsCreated)
                {
                    brushTreeSpaceBoundCache.Clear();
                    brushTreeSpaceBoundCache.Dispose();
                }
                brushTreeSpaceBoundCache = default;
                if (treeSpaceVerticesCache.IsCreated)
                {
                    foreach (var item in treeSpaceVerticesCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    treeSpaceVerticesCache.Clear();
                    treeSpaceVerticesCache.Dispose();
                }
                treeSpaceVerticesCache = default;
                if (routingTableCache.IsCreated)
                {
                    foreach (var item in routingTableCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    routingTableCache.Clear();
                    routingTableCache.Dispose();
                }
                routingTableCache = default;
                if (brushTreeSpacePlaneCache.IsCreated)
                {
                    foreach (var item in brushTreeSpacePlaneCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    brushTreeSpacePlaneCache.Clear();
                    brushTreeSpacePlaneCache.Dispose();
                }
                brushTreeSpacePlaneCache = default;
                if (brushesTouchedByBrushCache.IsCreated)
                {
                    foreach (var item in brushesTouchedByBrushCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    brushesTouchedByBrushCache.Clear();
                    brushesTouchedByBrushCache.Dispose();
                }
                brushesTouchedByBrushCache = default;
                if (transformationCache.IsCreated)
                {
                    transformationCache.Clear();
                    transformationCache.Dispose();
                }
                transformationCache = default;
                if (brushRenderBufferCache.IsCreated)
                {
                    foreach (var item in brushRenderBufferCache)
                    {
                        if (item.IsCreated)
                            item.Dispose();
                    }
                    brushRenderBufferCache.Clear();
                    brushRenderBufferCache.Dispose();
                }
                brushRenderBufferCache = default;
                if (compactTree.IsCreated)
                    compactTree.Dispose();
                compactTree = default;

                if (brushTreeSpaceBoundLookup.IsCreated)
                    brushTreeSpaceBoundLookup.Dispose();
                brushTreeSpaceBoundLookup = default;
                if (brushRenderBufferLookup.IsCreated)
                    brushRenderBufferLookup.Dispose();
                brushRenderBufferLookup = default;
            }
        }

        static ChiselTreeLookup _singleton;

        static void UpdateValue()
        {
            if (_singleton == null)
            {
                _singleton = ScriptableObject.CreateInstance<ChiselTreeLookup>();
                _singleton.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public static ChiselTreeLookup Value
        {
            get
            {
                if (_singleton == null)
                    UpdateValue();
                return _singleton;
            }
        }

        public Data this[int index]
        {
            get
            {
                if (!chiselTreeLookup.TryGetValue(index, out int dataIndex))
                {
                    dataIndex = chiselTreeData.Count;
                    chiselTreeLookup[index] = dataIndex;
                    chiselTreeData.Add(new Data());
                    chiselTreeData[dataIndex].Initialize();
                }
                return chiselTreeData[dataIndex];
            }
        }

        readonly Dictionary<int, int>   chiselTreeLookup    = new Dictionary<int, int>();
        readonly List<Data>             chiselTreeData      = new List<Data>();

        
        internal void OnDisable()
        {
            foreach (var data in chiselTreeData)
                data.Dispose();
            chiselTreeData.Clear();
            chiselTreeLookup.Clear();
            _singleton = null;
        }
    }
    
    internal sealed unsafe class ChiselMeshLookup : ScriptableObject
    {
        public unsafe class Data
        {
            public readonly HashSet<int> brushMeshUpdateList = new HashSet<int>();
            public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>> brushMeshBlobs;
            
            internal void Initialize()
            {
                brushMeshBlobs = new NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>(1000, Allocator.Persistent);
            }

            public void EnsureCapacity(int capacity)
            {
                if (brushMeshBlobs.Capacity < capacity)
                    brushMeshBlobs.Capacity = capacity;
            }

            internal void Dispose()
            {
                if (brushMeshBlobs.IsCreated)
                {
                    using (var items = brushMeshBlobs.GetValueArray(Allocator.Temp))
                    {
                        brushMeshBlobs.Clear();
                        brushMeshBlobs.Dispose();
                        foreach (var item in items)
                        {
                            if (item.IsCreated)
                                item.Dispose();
                        }
                    }
                }
            }
        }

        public static void Update()
        {
            var brushMeshBlobs = ChiselMeshLookup.Value.brushMeshBlobs;
            foreach (var brushMeshIndex in Value.brushMeshUpdateList)
            {
                var brushMeshID = brushMeshIndex + 1;
                var brushMesh   = BrushMeshManager.GetBrushMesh(brushMeshID);
                if (brushMesh == null)
                    brushMeshBlobs[brushMeshIndex] = BlobAssetReference<BrushMeshBlob>.Null;
                else
                    brushMeshBlobs[brushMeshIndex] = BrushMeshBlob.Build(brushMesh);
            }
            Value.brushMeshUpdateList.Clear();
        }


        static ChiselMeshLookup _singleton;

        static void UpdateValue()
        {
            if (_singleton == null)
            {
                _singleton = ScriptableObject.CreateInstance<ChiselMeshLookup>();
                _singleton.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public static Data Value
        {
            get
            {
                if (_singleton == null)
                    UpdateValue();
                return _singleton.data;
            }
        }

        readonly Data data = new Data();

        internal void OnEnable()
        {
            data.Initialize();
        }

        internal void OnDisable()
        {
            data.Dispose();
            _singleton = null;
        }
    }
}
