using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Profiler = UnityEngine.Profiling.Profiler;
using System.Runtime.CompilerServices;
using Unity.Burst;

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

    // Note: Stored in BlobAsset at runtime/editor-time
    public struct BrushSurfacePair
    {
        public int brushNodeIndex0;
        public int brushNodeIndex1;
        public int basePlaneIndex;
    }
    
    public struct BrushPair : IEquatable<BrushPair>, IEqualityComparer<BrushPair>, IComparable<BrushPair>, IComparer<BrushPair>
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
            if (obj == null || !(obj is BrushPair))
                return false;

            var other = (BrushPair)obj;
            return Equals(other);
        }

        public bool Equals(BrushPair x, BrushPair y) { return x.Equals(y); }

        public bool Equals(BrushPair other)
        {
            return ((brushIndexOrder0.nodeIndex == other.brushIndexOrder0.nodeIndex) && 
                    (brushIndexOrder1.nodeIndex == other.brushIndexOrder1.nodeIndex));
        }
        #endregion

        #region Compare
        public int Compare(BrushPair x, BrushPair y) { return x.CompareTo(y); }

        public int CompareTo(BrushPair other)
        {
            if (brushIndexOrder0.nodeIndex < other.brushIndexOrder0.nodeIndex)
                return -1;
            if (brushIndexOrder0.nodeIndex > other.brushIndexOrder0.nodeIndex)
                return 1;
            if (brushIndexOrder1.nodeIndex < other.brushIndexOrder1.nodeIndex)
                return -1;
            if (brushIndexOrder1.nodeIndex > other.brushIndexOrder1.nodeIndex)
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
            return ((ulong)obj.brushIndexOrder0.nodeIndex + ((ulong)obj.brushIndexOrder1.nodeIndex << 32)).GetHashCode();
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

        internal static BlobAssetReference<CompactTree> Create(List<CSGManager.NodeHierarchy> nodeHierarchies, int treeNodeIndex)
        {
            var treeInfo            = nodeHierarchies[treeNodeIndex].treeInfo;
            var treeBrushes         = treeInfo.treeBrushes;

            if (treeBrushes.Count == 0)
                return BlobAssetReference<CompactTree>.Null;

            var bottomUpNodeIndices = new List<BottomUpNodeIndex>();
            var bottomUpNodes       = new List<int>();
            
            var minBrushIndex       = nodeHierarchies.Count;
            var maxBrushIndex       = 0;
            for (int b = 0; b < treeBrushes.Count; b++)
            {
                var brushNodeID = treeBrushes[b];
                if (!CSGManager.IsValidNodeID(brushNodeID))
                    continue;

                var brush = new CSGTreeNode() { nodeID = brushNodeID };
                if (!brush.Valid)
                    continue;

                minBrushIndex = math.min(brush.NodeID - 1, minBrushIndex);
                maxBrushIndex = math.max(brush.NodeID - 1, maxBrushIndex);
            }
            var brushIndexToBottomUpIndex = new int[(maxBrushIndex + 1) - minBrushIndex];

            // Bottom-up -> per brush list of all ancestors to root
            for (int b = 0; b < treeBrushes.Count; b++)
            {
                var brushNodeID = treeBrushes[b];
                if (!CSGManager.IsValidNodeID(brushNodeID))
                    continue;

                var brush = new CSGTreeNode() { nodeID = brushNodeID };
                if (!brush.Valid)
                    continue;

                var parentStart = bottomUpNodes.Count;

                var parent      = brush.Parent;
                var treeNodeID  = treeNodeIndex + 1;
                while (parent.Valid && parent.NodeID != treeNodeID)
                {
                    var parentIndex = parent.NodeID - 1;
                    bottomUpNodes.Add(parentIndex);
                    parent = parent.Parent;
                }

                var brushNodeIndex  = brushNodeID - 1;
                brushIndexToBottomUpIndex[brushNodeIndex - minBrushIndex] = bottomUpNodeIndices.Count;
                bottomUpNodeIndices.Add(new BottomUpNodeIndex()
                {
                    nodeIndex  = brushNodeIndex,
                    bottomUpEnd     = bottomUpNodes.Count,
                    bottomUpStart   = parentStart
                });
            }

            if (bottomUpNodeIndices.Count == 0)
                return BlobAssetReference<CompactTree>.Null;

            // Top-down
            var nodeQueue       = new Queue<CompactTopDownBuilderNode>();
            var topDownNodes    = new List<CompactTopDownNode>(); // TODO: set capacity to number of nodes in tree

            nodeQueue.Enqueue(new CompactTopDownBuilderNode() { node = new CSGTreeNode() { nodeID =  treeNodeIndex + 1 }, index = 0 });
            topDownNodes.Add(new CompactTopDownNode()
            {
                Type        = CSGNodeType.Tree,
                Operation   = CSGOperationType.Additive,
                nodeIndex   = treeNodeIndex,
            });

            while (nodeQueue.Count > 0)
            {
                var parent      = nodeQueue.Dequeue();
                var nodeCount   = parent.node.Count;
                if (nodeCount == 0)
                {
                    var item = topDownNodes[parent.index];
                    item.childOffset = -1;
                    item.childCount = 0;
                    topDownNodes[parent.index] = item;
                    continue;
                }

                int firstIndex = 0;
                // Skip all nodes that are not additive at the start of the branch since they will never produce any geometry
                for (; firstIndex < nodeCount && parent.node[firstIndex].Valid && 
                                    (parent.node[firstIndex].Operation != CSGOperationType.Additive &&
                                     parent.node[firstIndex].Operation != CSGOperationType.Copy); firstIndex++)
                    firstIndex++;

                var firstChildIndex = topDownNodes.Count;
                for (int i = firstIndex; i < nodeCount; i++)
                {
                    var child = parent.node[i];
                    // skip invalid nodes (they don't contribute to the mesh)
                    if (!child.Valid)
                        continue;

                    var childType = child.Type;
                    if (childType != CSGNodeType.Brush)
                        nodeQueue.Enqueue(new CompactTopDownBuilderNode()
                        {
                            node = child,
                            index = topDownNodes.Count
                        });
                    topDownNodes.Add(new CompactTopDownNode()
                    {
                        Type        = childType,
                        Operation   = child.Operation,
                        nodeIndex   = child.NodeID - 1
                    });
                }

                {
                    var item = topDownNodes[parent.index];
                    item.childOffset = firstChildIndex;
                    item.childCount = topDownNodes.Count - firstChildIndex;
                    topDownNodes[parent.index] = item;
                }
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<CompactTree>();
            builder.Construct(ref root.topDownNodes, topDownNodes);
            builder.Construct(ref root.bottomUpNodeIndices, bottomUpNodeIndices);
            builder.Construct(ref root.bottomUpNodes, bottomUpNodes);
            root.indexOffset = minBrushIndex;
            builder.Construct(ref root.brushIndexToBottomUpIndex, brushIndexToBottomUpIndex);
            var compactTree = builder.CreateBlobAssetReference<CompactTree>(Allocator.Persistent);
            builder.Dispose();

            return compactTree;
        }
    }

    struct BrushIntersectionIndex
    {
        public int nodeIndex;
        public int bottomUpStart;
        public int bottomUpEnd;
        public int intersectionStart;
        public int intersectionEnd;

        public override string ToString() { return $"({nameof(nodeIndex)}: {nodeIndex}, {nameof(bottomUpStart)}: {bottomUpStart}, {nameof(bottomUpEnd)}: {bottomUpEnd}, {nameof(intersectionStart)}: {intersectionStart}, {nameof(intersectionEnd)}: {intersectionEnd})"; }
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BrushIntersection
    {
        public int              nodeIndex;
        public IntersectionType type;
        public int              bottomUpStart;
        public int              bottomUpEnd;

        public override string ToString() { return $"({nameof(nodeIndex)}: {nodeIndex}, {nameof(type)}: {type}, {nameof(bottomUpStart)}: {bottomUpStart}, {nameof(bottomUpEnd)}: {bottomUpEnd})"; }
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
            var bitIndex = index & 31;	// remainder
            var twoBit = ((UInt32)3) << bitIndex;

            return (IntersectionType)((intersectionBits[int32Index] & twoBit) >> bitIndex);
        }
    }
        
    public struct NodeTransformations
    {
        public float4x4 nodeToTree;
        public float4x4 treeToNode;

        public static BlobAssetReference<NodeTransformations> Build(float4x4 nodeToTree, float4x4 treeToNode)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<NodeTransformations>();
            root.nodeToTree = nodeToTree;
            root.treeToNode = treeToNode;
            var result = builder.CreateBlobAssetReference<NodeTransformations>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    };

    // Note: Stored in BlobAsset at runtime/editor-time
    public struct SurfaceInfo
    {
        public int                  brushIndex;
        public ushort               basePlaneIndex;
        public CategoryGroupIndex   interiorCategory;
    }

    // Note: Stored in BlobAsset at runtime/editor-time
    struct BasePolygon
    {
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
    }
    
    public enum IntersectionType : byte
    {
        NoIntersection,
        Intersection,
        AInsideB,
        BInsideA,

        InvalidValue
    };

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
    
    public struct BrushIntersectionLoop
    {
        public BrushSurfacePair     pair;
        public SurfaceInfo          surfaceInfo;
        public BlobArray<float3>    loopVertices;
    }

    public struct BrushIntersectionLoops
    {
        public BlobArray<BrushIntersectionLoop> loops;
    }

    public struct BrushPairIntersection
    {
        public IntersectionType type;
        // Note: that the localSpacePlanes0/localSpacePlaneIndices0 parameters for both brush0 and brush1 are in localspace of >brush0<
        public BlobArray<BrushIntersectionInfo> brushes;
    }


    internal sealed unsafe class ChiselTreeLookup : ScriptableObject
    {
        public unsafe class Data
        {
            public NativeHashMap<int, BlobAssetReference<BasePolygonsBlob>>         basePolygons;
            public NativeHashMap<int, MinMaxAABB>                                   brushTreeSpaceBounds;
            public NativeHashMap<int, BlobAssetReference<RoutingTable>>             routingTableLookup;
            public NativeHashMap<int, BlobAssetReference<BrushTreeSpacePlanes>>     brushTreeSpacePlanes;
            public NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>>    brushesTouchedByBrushes;
            public NativeHashMap<int, BlobAssetReference<NodeTransformations>>      transformations;
            public NativeHashMap<int, BlobAssetReference<ChiselBrushRenderBuffer>>  brushRenderBuffers;

            public BlobAssetReference<CompactTree>                                  compactTree;

            internal void RemoveBasePolygonsByBrushIndex(List<int> brushNodeIndices)
            {
                if (basePolygons.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndices.Count; b++)
                {
                    var brushNodeIndex = brushNodeIndices[b];
                    if (basePolygons.TryGetValue(brushNodeIndex, out var basePolygonsBlob))
                    {
                        basePolygons.Remove(brushNodeIndex);
                        if (basePolygonsBlob.IsCreated)
                            basePolygonsBlob.Dispose();
                    }
                }
            }

            internal void RemoveBrushTreeSpaceBoundsBrushIndex(List<int> brushNodeIndices)
            {
                if (brushTreeSpaceBounds.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndices.Count; b++)
                {
                    var brushNodeIndex = brushNodeIndices[b];
                    if (brushTreeSpaceBounds.TryGetValue(brushNodeIndex, out var basePolygonsBlob))
                    {
                        brushTreeSpaceBounds.Remove(brushNodeIndex);
                    }
                }
            }

            internal void RemoveRoutingTablesByBrushIndex(List<int> brushNodeIndices)
            {
                if (routingTableLookup.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndices.Count; b++)
                {
                    var brushNodeIndex = brushNodeIndices[b];
                    if (routingTableLookup.TryGetValue(brushNodeIndex, out var routingTable))
                    {
                        routingTableLookup.Remove(brushNodeIndex);
                        if (routingTable.IsCreated)
                            routingTable.Dispose();
                    }
                }
            }

            internal void RemoveBrushTreeSpacePlanesByBrushIndex(List<int> brushNodeIndices)
            {
                if (brushTreeSpacePlanes.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndices.Count; b++)
                {
                    var brushNodeIndex = brushNodeIndices[b];
                    if (brushTreeSpacePlanes.TryGetValue(brushNodeIndex, out var treeSpacePlanes))
                    {
                        brushTreeSpacePlanes.Remove(brushNodeIndex);
                        if (treeSpacePlanes.IsCreated)
                            treeSpacePlanes.Dispose();
                    }
                }
            }

            internal void RemoveBrushTouchesByBrushIndex(List<int> brushNodeIndices)
            {
                if (brushesTouchedByBrushes.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndices.Count; b++)
                {
                    var brushNodeIndex = brushNodeIndices[b];
                    if (brushesTouchedByBrushes.TryGetValue(brushNodeIndex, out var brushesTouchedByBrush))
                    {
                        brushesTouchedByBrushes.Remove(brushNodeIndex);
                        if (brushesTouchedByBrush.IsCreated)
                            brushesTouchedByBrush.Dispose();
                    }
                }
            }

            internal void RemoveTransformationsByBrushIndex(List<int> brushNodeIndices)
            {
                if (transformations.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndices.Count; b++)
                {
                    var brushNodeIndex = brushNodeIndices[b];
                    if (transformations.TryGetValue(brushNodeIndex, out var transformation))
                    {
                        transformations.Remove(brushNodeIndex);
                        if (transformation.IsCreated)
                            transformation.Dispose();
                    }
                }
            }

            internal void RemoveSurfaceRenderBuffersByBrushIndex(List<int> brushNodeIndices)
            {
                if (brushRenderBuffers.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndices.Count; b++)
                {
                    var brushNodeIndex = brushNodeIndices[b];
                    if (brushRenderBuffers.TryGetValue(brushNodeIndex, out var surfaceRenderBuffer))
                    {
                        brushRenderBuffers.Remove(brushNodeIndex);
                        if (surfaceRenderBuffer.IsCreated)
                            surfaceRenderBuffer.Dispose();
                    }
                }
            }

            internal void RemoveRoutingTablesByBrushIndexOrder(List<IndexOrder> brushNodeIndexOrders)
            {
                if (routingTableLookup.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndexOrders.Count; b++)
                {
                    int brushNodeIndex = brushNodeIndexOrders[b].nodeIndex;
                    if (routingTableLookup.TryGetValue(brushNodeIndex, out var routingTable))
                    {
                        routingTableLookup.Remove(brushNodeIndex);
                        if (routingTable.IsCreated)
                            routingTable.Dispose();
                    }
                }
            }

            internal void RemoveBrushTreeSpacePlanesByBrushIndexOrder(List<IndexOrder> brushNodeIndexOrders)
            {
                if (brushTreeSpacePlanes.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndexOrders.Count; b++)
                {
                    int brushNodeIndex = brushNodeIndexOrders[b].nodeIndex;
                    if (brushTreeSpacePlanes.TryGetValue(brushNodeIndex, out var treeSpacePlanes))
                    {
                        brushTreeSpacePlanes.Remove(brushNodeIndex);
                        if (treeSpacePlanes.IsCreated)
                            treeSpacePlanes.Dispose();
                    }
                }
            }

            internal void RemoveBrushTouchesByBrushIndexOrder(List<IndexOrder> brushNodeIndexOrders)
            {
                if (brushesTouchedByBrushes.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndexOrders.Count; b++)
                {
                    int brushNodeIndex = brushNodeIndexOrders[b].nodeIndex;
                    if (brushesTouchedByBrushes.TryGetValue(brushNodeIndex, out var brushesTouchedByBrush))
                    {
                        brushesTouchedByBrushes.Remove(brushNodeIndex);
                        if (brushesTouchedByBrush.IsCreated)
                            brushesTouchedByBrush.Dispose();
                    }
                }
            }

            internal void RemoveSurfaceRenderBuffersByBrushIndexOrder(List<IndexOrder> brushNodeIndexOrders)
            {
                if (brushRenderBuffers.Count() == 0)
                    return;
                for (int b = 0; b < brushNodeIndexOrders.Count; b++)
                {
                    int brushNodeIndex = brushNodeIndexOrders[b].nodeIndex;
                    if (brushRenderBuffers.TryGetValue(brushNodeIndex, out var surfaceRenderBuffer))
                    {
                        brushRenderBuffers.Remove(brushNodeIndex);
                        if (surfaceRenderBuffer.IsCreated)
                            surfaceRenderBuffer.Dispose();
                    }
                }
            }


            internal void Initialize()
            {
                // brushIndex
                basePolygons            = new NativeHashMap<int, BlobAssetReference<BasePolygonsBlob>>(1000, Allocator.Persistent);
                brushTreeSpaceBounds    = new NativeHashMap<int, MinMaxAABB>(1000, Allocator.Persistent);
                routingTableLookup      = new NativeHashMap<int, BlobAssetReference<RoutingTable>>(1000, Allocator.Persistent);
                brushTreeSpacePlanes    = new NativeHashMap<int, BlobAssetReference<BrushTreeSpacePlanes>>(1000, Allocator.Persistent);
                brushesTouchedByBrushes = new NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>>(1000, Allocator.Persistent);
                transformations         = new NativeHashMap<int, BlobAssetReference<NodeTransformations>>(1000, Allocator.Persistent);
                brushRenderBuffers      = new NativeHashMap<int, BlobAssetReference<ChiselBrushRenderBuffer>>(1000, Allocator.Persistent);
            }

            internal void Dispose()
            {
                if (basePolygons.IsCreated)
                {
                    using (var items = basePolygons.GetValueArray(Allocator.Temp))
                    {
                        foreach (var item in items)
                        {
                            if (item.IsCreated)
                                item.Dispose();
                        }
                        basePolygons.Clear();
                        basePolygons.Dispose();
                    }
                }
                if (brushTreeSpaceBounds.IsCreated)
                {
                    brushTreeSpaceBounds.Clear();
                    brushTreeSpaceBounds.Dispose();
                }
                if (routingTableLookup.IsCreated)
                {
                    using (var items = routingTableLookup.GetValueArray(Allocator.Temp))
                    {
                        foreach (var item in items)
                        {
                            if (item.IsCreated)
                                item.Dispose();
                        }
                        routingTableLookup.Clear();
                        routingTableLookup.Dispose();
                    }
                }
                if (brushTreeSpacePlanes.IsCreated)
                {
                    using (var items = brushTreeSpacePlanes.GetValueArray(Allocator.Temp))
                    {
                        foreach (var item in items)
                        {
                            if (item.IsCreated)
                                item.Dispose();
                        }
                        brushTreeSpacePlanes.Clear();
                        brushTreeSpacePlanes.Dispose();
                    }
                }
                if (brushesTouchedByBrushes.IsCreated)
                {
                    using (var items = brushesTouchedByBrushes.GetValueArray(Allocator.Temp))
                    {
                        foreach (var item in items)
                        {
                            if (item.IsCreated)
                                item.Dispose();
                        }
                        brushesTouchedByBrushes.Clear();
                        brushesTouchedByBrushes.Dispose();
                    }
                }
                if (transformations.IsCreated)
                {
                    using (var items = transformations.GetValueArray(Allocator.Temp))
                    {
                        foreach (var item in items)
                        {
                            if (item.IsCreated)
                                item.Dispose();
                        }
                        transformations.Clear();
                        transformations.Dispose();
                    }
                }
                if (brushRenderBuffers.IsCreated)
                {
                    using (var items = brushRenderBuffers.GetValueArray(Allocator.Temp))
                    {
                        foreach (var item in items)
                        {
                            if (item.IsCreated)
                                item.Dispose();
                        }
                        brushRenderBuffers.Clear();
                        brushRenderBuffers.Dispose();
                    }
                }
                if (compactTree.IsCreated)
                {
                    compactTree.Dispose();
                }
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
            public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>            brushMeshBlobs;
            
            internal void Initialize()
            {
                brushMeshBlobs = new NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>(1000, Allocator.Persistent);
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
