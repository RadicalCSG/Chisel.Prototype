//#define RUN_IN_SERIAL
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Profiler = UnityEngine.Profiling.Profiler;
using Debug = UnityEngine.Debug;
using Unity.Mathematics;

namespace Chisel.Core
{
    static partial class CSGManager
    {
        internal sealed class TreeInfo
        {
            public readonly List<int>                       treeBrushes         = new List<int>();
            public readonly List<GeneratedMeshDescription>  meshDescriptions    = new List<GeneratedMeshDescription>();
            public readonly List<SubMeshCounts>             subMeshCounts       = new List<SubMeshCounts>();

            public void Reset() { subMeshCounts.Clear(); }
        }

        internal struct TreeUpdate
        {
            public int                      treeNodeIndex;
            public int                      brushCount;
            public int                      updateCount;
            public NativeArray<IndexOrder>  allTreeBrushIndexOrders;
            public int                      maxNodeOrder;
            public NativeList<IndexOrder>   rebuildTreeBrushIndexOrders;
            
            public BlobAssetReference<CompactTree>  compactTree;

            // TODO: We store this per tree, and ensure brushes have ids from 0 to x per tree, then we can use an array here.
            //       Remap "local index" to "nodeindex" and back? How to make this efficiently work with caching stuff?
            public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>          treeSpaceVerticesArray;

            public NativeMultiHashMap<int, BrushPair>                                   brushBrushIntersections;
            public NativeHashMap<int, IndexOrder>                                       brushesThatNeedIndirectUpdate;

            public NativeList<BrushPair>                                                uniqueBrushPairs;
            public NativeMultiHashMap<int, BlobAssetReference<BrushIntersectionLoop>>   intersectionLoopBlobs;
            public NativeList<BlobAssetReference<BrushPairIntersection>>                intersectingBrushes;
            
            //public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>           brushRenderBuffers;
            public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>               brushesTouchedByBrushes;
            public NativeArray<BlobAssetReference<RoutingTable>>                        routingTableLookup;
            public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>                brushTreeSpacePlanes;
            public NativeArray<MinMaxAABB>                                              brushTreeSpaceBounds;
            public NativeArray<BlobAssetReference<BasePolygonsBlob>>                    basePolygons;
            public NativeArray<NodeTransformations>                                     transformations;
            public NativeArray<BlobAssetReference<BrushMeshBlob>>                       brushMeshLookup;
            
            public NativeStream     dataStream1;
            public NativeStream     dataStream2;


            public JobHandle generateTreeSpaceVerticesAndBoundsJobHandle;
            public JobHandle generateBasePolygonLoopsJobHandle;
            public JobHandle mergeTouchingBrushVerticesJobHandle;
            public JobHandle mergeTouchingBrushVertices2JobHandle;

            public JobHandle findAllIntersectionsJobHandle;
            public JobHandle findAllIndirectIntersectionsJobHandle;
            public JobHandle findIntersectingBrushesJobHandle;

            public JobHandle updateBrushTreeSpacePlanesJobHandle;

            public JobHandle updateBrushCategorizationTablesJobHandle;

            public JobHandle findBrushPairsJobHandle;
            public JobHandle prepareBrushPairIntersectionsJobHandle;
            public JobHandle findAllIntersectionLoopsJobHandle;

            public JobHandle allFindLoopOverlapIntersectionsJobHandle;

            public JobHandle allPerformAllCSGJobHandle;
            public JobHandle allGenerateSurfaceTrianglesJobHandle;
        }


        static readonly List<IndexOrder>                        s_AllTreeBrushIndexOrdersList     = new List<IndexOrder>();
        static readonly List<BlobAssetReference<BrushMeshBlob>> s_BrushMeshList                   = new List<BlobAssetReference<BrushMeshBlob>>();
        static readonly List<IndexOrder>                        s_RebuildTreeBrushIndexOrdersList = new List<IndexOrder>();
        static readonly List<IndexOrder>                        s_TransformTreeBrushIndicesList   = new List<IndexOrder>();
        static int[]            nodeIndexToNodeOrderArray;
        static TreeUpdate[]     s_TreeUpdates;

        static readonly TreeSorter s_TreeSorter = new TreeSorter();

        internal static JobHandle UpdateTreeMeshes(int[] treeNodeIDs)
        {
            var finalJobHandle = default(JobHandle);

            #region Do the setup for the CSG Jobs
            Profiler.BeginSample("CSG_Setup");            
            if (s_TreeUpdates == null || s_TreeUpdates.Length < treeNodeIDs.Length)
                s_TreeUpdates = new TreeUpdate[treeNodeIDs.Length];
            var treeUpdateLength = 0;
            for (int t = 0; t < treeNodeIDs.Length; t++)
            {
                var treeNodeIndex       = treeNodeIDs[t] - 1;
                var treeInfo            = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;
                treeInfo.Reset();

                var treeBrushes = treeInfo.treeBrushes;
                if (treeBrushes.Count == 0)
                    continue;

                int brushCount = treeBrushes.Count;

                #region Build lookup tables to find the tree node-order by node-index                
                var nodeIndexMin = int.MaxValue;
                var nodeIndexMax = 0;
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID     = treeBrushes[nodeOrder];
                    int nodeIndex  = nodeID - 1;                    
                    nodeIndexMin = math.min(nodeIndexMin, nodeIndex);
                    nodeIndexMax = math.max(nodeIndexMax, nodeIndex);
                }

                var nodeIndexToNodeOrderOffset  = nodeIndexMin;
                var desiredLength = (nodeIndexMax - nodeIndexMin) + 1;
                if (nodeIndexToNodeOrderArray == null ||
                    nodeIndexToNodeOrderArray.Length < desiredLength)
                    nodeIndexToNodeOrderArray = new int[desiredLength];
                for (int nodeOrder  = 0; nodeOrder  < brushCount; nodeOrder ++)
                {
                    int nodeID     = treeBrushes[nodeOrder];
                    int nodeIndex  = nodeID - 1;             
                    nodeIndexToNodeOrderArray[nodeIndex - nodeIndexToNodeOrderOffset] = nodeOrder;
                }
                #endregion


                var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeIndex];
                chiselLookupValues.EnsureCapacity(brushCount);

                Profiler.BeginSample("CSG_Allocations[1]");
                var triangleArraySize               = GeometryMath.GetTriangleArraySize(brushCount);
                var intersectionCount               = triangleArraySize;
                var brushesThatNeedIndirectUpdate   = new NativeHashMap<int, IndexOrder>(brushCount, Allocator.TempJob);
                var dataStream1                     = new NativeStream(brushCount, Allocator.TempJob);
                var dataStream2                     = new NativeStream(brushCount, Allocator.TempJob);
                var intersectionLoopBlobs           = new NativeMultiHashMap<int, BlobAssetReference<BrushIntersectionLoop>>(intersectionCount * 2, Allocator.TempJob);
                var brushBrushIntersections         = new NativeMultiHashMap<int, BrushPair>(intersectionCount * 2, Allocator.TempJob);
                var uniqueBrushPairs                = new NativeList<BrushPair>(intersectionCount, Allocator.TempJob);
                var intersectingBrushes             = new NativeList<BlobAssetReference<BrushPairIntersection>>(intersectionCount, Allocator.TempJob);

                var brushesTouchedByBrushes         = new NativeArray<BlobAssetReference<BrushesTouchedByBrush>>(brushCount, Allocator.TempJob);
                var treeSpaceVerticesArray          = new NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(brushCount, Allocator.TempJob);
                var basePolygons                    = new NativeArray<BlobAssetReference<BasePolygonsBlob>>(brushCount, Allocator.TempJob);
                var transformations                 = new NativeArray<NodeTransformations>(brushCount, Allocator.TempJob);
                var brushTreeSpacePlanes            = new NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>(brushCount, Allocator.TempJob);
                var brushTreeSpaceBounds            = new NativeArray<MinMaxAABB>(brushCount, Allocator.TempJob);
                var routingTableLookup              = new NativeArray<BlobAssetReference<RoutingTable>>(brushCount, Allocator.TempJob);
                //var brushRenderBuffers            = new NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>(brushCount, Allocator.TempJob);
                Profiler.EndSample();

                ref var basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                ref var brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                ref var treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                ref var brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;

                
                // TODO: do this in job, build brushMeshList in same job
                #region Build all BrushMeshBlobs
                Profiler.BeginSample("CSG_BrushMeshBlob_Generation");
                ChiselMeshLookup.Update();
                Profiler.EndSample();
                #endregion

                #region Build list of all brushes that have been modified
                s_AllTreeBrushIndexOrdersList.Clear();
                s_BrushMeshList.Clear();
                s_RebuildTreeBrushIndexOrdersList.Clear();
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                for (int brushNodeOrder = 0, i = 0; i < brushCount; i++)
                {
                    int brushNodeID     = treeBrushes[i];
                    int brushNodeIndex  = brushNodeID - 1;
                    int brushMeshID     = 0;
                    if (!IsValidNodeID(brushNodeID) ||
                        // NOTE: Assignment is intended, this is not supposed to be a comparison
                        (brushMeshID = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo.brushMeshInstanceID) == 0)
                    {
                        // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                        Debug.LogError($"Brush with ID {brushNodeID}, index {brushNodeIndex} has its brushMeshID set to {brushMeshID}, which is invalid."); 
                    }

                    // We need the index into the tree to ensure deterministic ordering
                    var brushIndexOrder = new IndexOrder { nodeIndex = brushNodeIndex, nodeOrder = brushNodeOrder };
                    s_AllTreeBrushIndexOrdersList.Add(brushIndexOrder);
                    brushNodeOrder++;

                    s_BrushMeshList.Add(brushMeshID == 0 ? BlobAssetReference<BrushMeshBlob>.Null : brushMeshBlobs[brushMeshID - 1]);

                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    if (nodeFlags.status != NodeStatusFlags.None)
                        s_RebuildTreeBrushIndexOrdersList.Add(brushIndexOrder);
                }
                #endregion
                
                #region Invalidate modified brush caches
                var anyHierarchyModified = false;
                s_TransformTreeBrushIndicesList.Clear();
                for (int b = 0; b < s_RebuildTreeBrushIndexOrdersList.Count; b++)
                {
                    var brushIndexOrder = s_RebuildTreeBrushIndexOrdersList[b];
                    int brushNodeIndex  = brushIndexOrder.nodeIndex;
                    
                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    
                    // Remove base polygons
                    if (basePolygonCache.TryGetValue(brushNodeIndex, out var basePolygonsBlob))
                    {
                        basePolygonCache.Remove(brushNodeIndex);
                        if (basePolygonsBlob.IsCreated)
                            basePolygonsBlob.Dispose();
                    }

                    // Remove cached bounding box
                    if (brushTreeSpaceBoundCache.ContainsKey(brushNodeIndex))
                    {
                        brushTreeSpaceBoundCache.Remove(brushNodeIndex);
                    }

                    // Remove treeSpace vertices
                    if (treeSpaceVerticesCache.TryGetValue(brushNodeIndex, out var treeSpaceVerticesBlob))
                    {
                        treeSpaceVerticesCache.Remove(brushNodeIndex);
                        if (treeSpaceVerticesBlob.IsCreated)
                            treeSpaceVerticesBlob.Dispose();
                    }

                    
                    // Fix up all flags

                    if ((nodeFlags.status & NodeStatusFlags.ShapeModified) != NodeStatusFlags.None)
                    {
                        // Need to update the basePolygons for this node
                        nodeFlags.status &= ~NodeStatusFlags.ShapeModified;
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }

                    if ((nodeFlags.status & NodeStatusFlags.HierarchyModified) != NodeStatusFlags.None)
                    {
                        anyHierarchyModified = true;
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }

                    if ((nodeFlags.status & NodeStatusFlags.TransformationModified) != NodeStatusFlags.None)
                    {
                        s_TransformTreeBrushIndicesList.Add(brushIndexOrder);
                        nodeFlags.status &= ~NodeStatusFlags.TransformationModified;
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }

                    CSGManager.nodeFlags[brushNodeIndex] = nodeFlags;
                }
                #endregion

                #region Invalidate brushes that touch our modified brushes, so we rebuild those too
                if (s_RebuildTreeBrushIndexOrdersList.Count != brushCount)
                {
                    for (int b = 0; b < s_RebuildTreeBrushIndexOrdersList.Count; b++)
                    {
                        var brushIndexOrder = s_RebuildTreeBrushIndexOrdersList[b];
                        int brushNodeIndex  = brushIndexOrder.nodeIndex;
                        var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                        if ((nodeFlags.status & NodeStatusFlags.NeedAllTouchingUpdated) == NodeStatusFlags.None)
                            continue;

                        if (!brushesTouchedByBrushCache.TryGetValue(brushNodeIndex, out var brushTouchedByBrush) ||
                            brushTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                            continue;

                        ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
                        for (int i = 0; i < brushIntersections.Length; i++)
                        {
                            int otherBrushIndex = brushIntersections[i].nodeIndexOrder.nodeIndex;
                            var otherBrushID    = otherBrushIndex + 1;

                            // TODO: Remove nodes from "brushIntersections" when the brush is removed from the hierarchy
                            if (!IsValidNodeID(otherBrushID))
                                continue;

                            var otherBrushOrder = nodeIndexToNodeOrderArray[otherBrushIndex - nodeIndexToNodeOrderOffset];
                            var otherIndexOrder = new IndexOrder { nodeIndex = otherBrushIndex, nodeOrder = otherBrushOrder };
                            if (!s_RebuildTreeBrushIndexOrdersList.Contains(otherIndexOrder))
                                s_RebuildTreeBrushIndexOrdersList.Add(otherIndexOrder);
                        }
                    }
                }
                #endregion
                
                Profiler.BeginSample("CSG_Allocations[2]");
                var allTreeBrushIndexOrders         = s_AllTreeBrushIndexOrdersList.ToNativeArray(Allocator.TempJob);
                var rebuildTreeBrushIndexOrders     = s_RebuildTreeBrushIndexOrdersList.ToNativeList(Allocator.TempJob);
                var brushMeshLookup                 = s_BrushMeshList.ToNativeArray(Allocator.TempJob);
                Profiler.EndSample();

                // TODO: figure out more accurate maximum sizes


                ref var brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                ref var routingTableCache           = ref chiselLookupValues.routingTableCache;
                ref var transformationCache         = ref chiselLookupValues.transformationCache;
                ref var brushRenderBufferCache      = ref chiselLookupValues.brushRenderBufferCache;

                #region Copy all values from caches to arrays in node-order
                Profiler.BeginSample("CSG_CopyToArray");
                for (int i = 0; i < brushCount; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (brushesTouchedByBrushCache.TryGetValue(nodeIndex, out var item))
                    {
                        ref var brushesTouchedByBrush = ref item.Value;

                        // Fix up node orders

                        //ref var intersectionBits = ref brushesTouchedByBrush.intersectionBits;
                        //BlobBuilderExtensions.ClearValues(ref intersectionBits);

                        ref var brushIntersections = ref brushesTouchedByBrush.brushIntersections;
                        for (int b = 0; b < brushIntersections.Length; b++)
                        {
                            ref var brushIntersection = ref brushIntersections[b];
                            ref var nodeIndexOrder = ref brushIntersection.nodeIndexOrder;
                            nodeIndexOrder.nodeOrder = nodeIndexToNodeOrderArray[nodeIndexOrder.nodeIndex - nodeIndexToNodeOrderOffset];
                            //brushesTouchedByBrush.Set(nodeIndexOrder.nodeOrder, brushIntersection.type);
                        }
                    } else
                    {
                        item = BlobAssetReference<BrushesTouchedByBrush>.Null;
                    }
                    brushesTouchedByBrushes[i] = item;
                }

                for (int i = 0; i < brushCount; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!treeSpaceVerticesCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null;
                    treeSpaceVerticesArray[i] = item;
                }

                for (int i = 0; i < brushCount; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (basePolygonCache.TryGetValue(nodeIndex, out var item))
                    {
                        // Fix up node orders
                        ref var polygons = ref item.Value.polygons;
                        for (int p = 0; p < polygons.Length; p++)
                        {
                            ref var nodeIndexOrder = ref polygons[p].nodeIndexOrder;
                            nodeIndexOrder.nodeOrder = nodeIndexToNodeOrderArray[nodeIndexOrder.nodeIndex - nodeIndexToNodeOrderOffset];
                        }
                    } else
                        item = BlobAssetReference<BasePolygonsBlob>.Null;
                    basePolygons[i] = item;
                }

                for (int i = 0; i < brushCount; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!transformationCache.TryGetValue(nodeIndex, out var item))
                        transformations[i] = item;
                    else
                        // TODO: optimize, only do this when necessary
                        transformations[i] = GetNodeTransformation(nodeIndex);
                }

                for (int i = 0; i < brushCount; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!brushTreeSpacePlaneCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<BrushTreeSpacePlanes>.Null;
                    brushTreeSpacePlanes[i] = item; 
                }

                for (int i = 0; i < brushCount; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!brushTreeSpaceBoundCache.TryGetValue(nodeIndex, out var item))
                        item = default;
                    brushTreeSpaceBounds[i] = item;
                }

                for (int i = 0; i < brushCount; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!routingTableCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<RoutingTable>.Null;
                    routingTableLookup[i] = item;
                }

                /*
                for (int i = 0; i < brushCount; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!brushRenderBufferCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<ChiselBrushRenderBuffer>.Null;
                    brushRenderBuffers[i] = item;
                }*/

                Profiler.EndSample();
                #endregion
                
                
                #region Dirty all invalidated outlines
                Profiler.BeginSample("CSG_DirtyModifiedOutlines");
                {
                    for (int b = 0; b < s_RebuildTreeBrushIndexOrdersList.Count; b++)
                    {
                        var brushIndexOrder = s_RebuildTreeBrushIndexOrdersList[b];
                        int brushNodeIndex  = brushIndexOrder.nodeIndex;
                        var brushInfo = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo;
                        brushInfo.brushOutlineGeneration++;
                        brushInfo.brushOutlineDirty = true;
                    }
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated routing-tables
                Profiler.BeginSample("CSG_InvalidateRoutingTables");
                for (int index = 0; index < s_RebuildTreeBrushIndexOrdersList.Count; index++)
                {
                    var brushIndexOrder = s_RebuildTreeBrushIndexOrdersList[index];
                    var original = routingTableLookup[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    routingTableLookup[brushIndexOrder.nodeOrder] = default;
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated brushTreeSpacePlanes
                Profiler.BeginSample("CSG_InvalidateBrushTreeSpacePlanes");
                for (int index = 0; index < s_RebuildTreeBrushIndexOrdersList.Count; index++)
                {
                    var brushIndexOrder = s_RebuildTreeBrushIndexOrdersList[index];
                    var original = brushTreeSpacePlanes[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    brushTreeSpacePlanes[brushIndexOrder.nodeOrder] = default;
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated brushesTouchesByBrushes
                Profiler.BeginSample("CSG_InvalidateBrushesTouchesByBrushes");
                for (int index = 0; index < s_RebuildTreeBrushIndexOrdersList.Count; index++)
                {
                    var brushIndexOrder = s_RebuildTreeBrushIndexOrdersList[index];
                    var original = brushesTouchedByBrushes[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    brushesTouchedByBrushes[brushIndexOrder.nodeOrder] = default;
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated renderbuffers
                Profiler.BeginSample("CSG_InvalidateBrushRenderBuffers");
                var brushLoopCount = s_RebuildTreeBrushIndexOrdersList.Count;
                for (int index = 0; index < brushLoopCount; index++)
                {
                    var brushIndexOrder = s_RebuildTreeBrushIndexOrdersList[index];

                    // Why was I doing this??
                    //if (s_RebuildTreeBrushIndexOrdersList.Contains(brushIndexOrder))
                    {
                        int brushNodeIndex = brushIndexOrder.nodeIndex;
                        if (brushRenderBufferCache.TryGetValue(brushNodeIndex, out var oldBrushRenderBuffer))
                        {
                            if (oldBrushRenderBuffer.IsCreated)
                                oldBrushRenderBuffer.Dispose();
                            brushRenderBufferCache.Remove(brushNodeIndex);
                        }
                    }
                }
                Profiler.EndSample();
                #endregion

                // TODO: optimize, only do this when necessary
                #region Build Transformations
                Profiler.BeginSample("CSG_UpdateBrushTransformations");
                {
                    for (int b = 0; b < s_TransformTreeBrushIndicesList.Count; b++)
                    {
                        var nodeIndexOrder = s_TransformTreeBrushIndicesList[b];
                        transformations[nodeIndexOrder.nodeOrder] = GetNodeTransformation(nodeIndexOrder.nodeIndex);
                    }
                }
                Profiler.EndSample();
                #endregion

                #region Build Compact Tree
                var compactTree = chiselLookupValues.compactTree;
                // only rebuild this when the hierarchy changes
                if (anyHierarchyModified ||
                    !compactTree.IsCreated)
                {
                    if (chiselLookupValues.compactTree.IsCreated)
                        chiselLookupValues.compactTree.Dispose();

                    // TODO: jobify?
                    Profiler.BeginSample("CSG_CompactTree.Create");
                    compactTree = CompactTree.Create(CSGManager.nodeHierarchies, treeNodeIndex);
                    chiselLookupValues.compactTree = compactTree;
                    Profiler.EndSample();
                }
                #endregion

                #region Build per tree lookup
                s_TreeUpdates[treeUpdateLength] = new TreeUpdate
                {
                    treeNodeIndex                   = treeNodeIndex,
                    brushCount                      = brushCount,
                    updateCount                     = rebuildTreeBrushIndexOrders.Length,
                    allTreeBrushIndexOrders         = allTreeBrushIndexOrders,
                    rebuildTreeBrushIndexOrders     = rebuildTreeBrushIndexOrders,
                    maxNodeOrder                    = treeBrushes.Count,
                    brushMeshLookup                 = brushMeshLookup,
                    transformations                 = transformations,
                    basePolygons                    = basePolygons,
                    brushTreeSpaceBounds            = brushTreeSpaceBounds,
                    treeSpaceVerticesArray          = treeSpaceVerticesArray,
                    brushTreeSpacePlanes            = brushTreeSpacePlanes,
                    routingTableLookup              = routingTableLookup,
                    brushesTouchedByBrushes         = brushesTouchedByBrushes,
                    //brushRenderBuffers            = brushRenderBuffers,
                    brushBrushIntersections         = brushBrushIntersections,
                    brushesThatNeedIndirectUpdate   = brushesThatNeedIndirectUpdate,
                    uniqueBrushPairs                = uniqueBrushPairs,
                    intersectionLoopBlobs           = intersectionLoopBlobs,
                    intersectingBrushes             = intersectingBrushes,
                    dataStream1                     = dataStream1,
                    dataStream2                     = dataStream2,
                    compactTree                     = compactTree
                };
                #endregion

                treeUpdateLength++;
            }
            
            // Sort trees from largest (slowest) to smallest (fastest)
            // The slowest trees will run first, and the fastest trees can then hopefully fill the gaps
            Array.Sort(s_TreeUpdates, s_TreeSorter);

            Profiler.EndSample();
            #endregion

            // TODO: rewrite code to not need [NativeDisableParallelForRestriction]
            // TODO: ensure we only update exactly what we need, and nothing more

            try
            {
                #region CSG Jobs
                Profiler.BeginSample("CSG_Jobs");

                // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                Profiler.BeginSample("Job_GenerateBoundsLoops");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders,
                            brushMeshLookup         = treeUpdate.brushMeshLookup,
                            transformations         = treeUpdate.transformations,

                            // Write
                            brushTreeSpaceBounds    = treeUpdate.brushTreeSpaceBounds,
                            treeSpaceVerticesArray  = treeUpdate.treeSpaceVerticesArray,
                        };
#if RUN_IN_SERIAL
                        treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle = createTreeSpaceVerticesAndBoundsJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16);
#else
                        treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle = createTreeSpaceVerticesAndBoundsJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16);
#endif
                    }
                }
                finally { Profiler.EndSample(); }

                // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                Profiler.BeginSample("Job_FindIntersectingBrushes");
                try
                {
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle;
                        var findAllIntersectionsJob = new FindAllBrushIntersectionsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders,
                            transformations                 = treeUpdate.transformations,
                            brushMeshLookup                 = treeUpdate.brushMeshLookup,
                            brushTreeSpaceBounds            = treeUpdate.brushTreeSpaceBounds,
                            updateBrushIndexOrders          = treeUpdate.rebuildTreeBrushIndexOrders,
                        
                            // Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections.AsParallelWriter(),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsParallelWriter()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                    
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findAllIntersectionsJobHandle;
                        var findAllIntersectionsJob = new FindAllIndirectBrushIntersectionsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders,
                            transformations                 = treeUpdate.transformations,
                            brushMeshLookup                 = treeUpdate.brushMeshLookup,
                            brushTreeSpaceBounds            = treeUpdate.brushTreeSpaceBounds,
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate,

                            // Read/Write
                            updateBrushIndexOrders          = treeUpdate.rebuildTreeBrushIndexOrders,
                        
                            // Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections.AsParallelWriter()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.findAllIndirectIntersectionsJobHandle = findAllIntersectionsJob.
                            Run(dependencies);
#else
                        treeUpdate.findAllIndirectIntersectionsJobHandle = findAllIntersectionsJob.
                            Schedule(dependencies);
#endif
                    }

                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findAllIndirectIntersectionsJobHandle;
                        var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                        {
                            // Read
                            treeNodeIndex               = treeUpdate.treeNodeIndex,
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            compactTree                 = treeUpdate.compactTree,
                            brushBrushIntersections     = treeUpdate.brushBrushIntersections,

                            // Write
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes
                        };
#if RUN_IN_SERIAL
                        treeUpdate.findIntersectingBrushesJobHandle = storeBrushIntersectionsJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.findIntersectingBrushesJobHandle = storeBrushIntersectionsJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_MergeTouchingBrushVerticesJob");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVertices2Job
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,

                            // Read / Write
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,
                        };
#if RUN_IN_SERIAL
                        treeUpdate.mergeTouchingBrushVerticesJobHandle = mergeTouchingBrushVerticesJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.mergeTouchingBrushVerticesJobHandle = mergeTouchingBrushVerticesJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                }
                finally { Profiler.EndSample(); }

                // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                Profiler.BeginSample("Job_GenerateBasePolygonLoops");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.mergeTouchingBrushVerticesJobHandle, treeUpdate.findIntersectingBrushesJobHandle);
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobs
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                            brushMeshLookup             = treeUpdate.brushMeshLookup,
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,

                            // Write
                            basePolygons                = treeUpdate.basePolygons
                        };
#if RUN_IN_SERIAL
                        treeUpdate.generateBasePolygonLoopsJobHandle = createBlobPolygonsBlobs.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.generateBasePolygonLoopsJobHandle = createBlobPolygonsBlobs.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                }
                finally { Profiler.EndSample(); }

                // TODO: should only do this at creation time + when moved / store with brush component itself
                Profiler.BeginSample("Job_UpdateBrushTreeSpacePlanes");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushMeshLookup         = treeUpdate.brushMeshLookup,
                            transformations         = treeUpdate.transformations,

                            // Write
                            brushTreeSpacePlanes    = treeUpdate.brushTreeSpacePlanes
                        };
#if RUN_IN_SERIAL
                        treeUpdate.updateBrushTreeSpacePlanesJobHandle = createBrushTreeSpacePlanesJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.updateBrushTreeSpacePlanesJobHandle = createBrushTreeSpacePlanesJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                }
                finally { Profiler.EndSample(); }

                // TODO: only update when brush or any touching brush has been added/removed or changes operation/order
                Profiler.BeginSample("Job_UpdateBrushCategorizationTables");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
                        // Build categorization trees for brushes
                        var createRoutingTableJob = new CreateRoutingTableJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes = treeUpdate.brushesTouchedByBrushes,
                            compactTree             = treeUpdate.compactTree,

                            // Write
                            routingTableLookup      = treeUpdate.routingTableLookup
                        };
#if RUN_IN_SERIAL
                        treeUpdate.updateBrushCategorizationTablesJobHandle = createRoutingTableJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.updateBrushCategorizationTablesJobHandle = createRoutingTableJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                } finally { Profiler.EndSample(); }
                                
                // Create unique loops between brush intersections
                Profiler.BeginSample("Job_FindBrushPairs");
                try
                {
                    // TODO: merge this with another job, there's not enough work 
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
                        var findBrushPairsJob = new FindBrushPairsJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                                    
                            // Write
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs,
                        };
#if RUN_IN_SERIAL
                        treeUpdate.findBrushPairsJobHandle = findBrushPairsJob.
                            Run(dependencies);
#else
                        treeUpdate.findBrushPairsJobHandle = findBrushPairsJob.
                            Schedule(dependencies);
#endif
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PrepareBrushPairIntersections");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findBrushPairsJobHandle;
                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            uniqueBrushPairs        = treeUpdate.uniqueBrushPairs.AsArray(),
#else
                            uniqueBrushPairs        = treeUpdate.uniqueBrushPairs.AsDeferredJobArray(),
#endif
                            transformations         = treeUpdate.transformations,
                            brushMeshLookup         = treeUpdate.brushMeshLookup,

                            // Write
                            intersectingBrushes     = treeUpdate.intersectingBrushes.AsParallelWriter()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.prepareBrushPairIntersectionsJobHandle = prepareBrushPairIntersectionsJob.
                            Run(treeUpdate.uniqueBrushPairs, 8, dependencies);
#else
                        treeUpdate.prepareBrushPairIntersectionsJobHandle = prepareBrushPairIntersectionsJob.
                            Schedule(treeUpdate.uniqueBrushPairs, 8, dependencies);
#endif
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_CreateIntersectionLoops");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = JobHandle.CombineDependencies(
                                                            treeUpdate.mergeTouchingBrushVerticesJobHandle,
                                                            treeUpdate.updateBrushTreeSpacePlanesJobHandle, 
                                                            treeUpdate.prepareBrushPairIntersectionsJobHandle);
                        var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Read
                            brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
#if RUN_IN_SERIAL
                            intersectingBrushes         = treeUpdate.intersectingBrushes.AsArray(),
#else
                            intersectingBrushes         = treeUpdate.intersectingBrushes.AsDeferredJobArray(),
#endif
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,
                            
                            // Write
                            outputSurfaces              = treeUpdate.intersectionLoopBlobs.AsParallelWriter()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.findAllIntersectionLoopsJobHandle = findAllIntersectionLoopsJob.
                            Run(treeUpdate.intersectingBrushes, 8, dependencies);
#else
                        treeUpdate.findAllIntersectionLoopsJobHandle = findAllIntersectionLoopsJob.
                            Schedule(treeUpdate.intersectingBrushes, 8, dependencies);
#endif
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_FindLoopOverlapIntersections");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.prepareBrushPairIntersectionsJobHandle, treeUpdate.generateBasePolygonLoopsJobHandle);
                        var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            maxNodeOrder                = treeUpdate.maxNodeOrder,
                            intersectionLoopBlobs       = treeUpdate.intersectionLoopBlobs,
                            brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
                            basePolygons                = treeUpdate.basePolygons,

                            // Write
                            output                      = treeUpdate.dataStream1.AsWriter()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.allFindLoopOverlapIntersectionsJobHandle = findLoopOverlapIntersectionsJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.allFindLoopOverlapIntersectionsJobHandle = findLoopOverlapIntersectionsJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                } finally { Profiler.EndSample(); }

                // TODO: should only try to merge the vertices beyond the original mesh vertices (the intersection vertices)
                //       should also try to limit vertices to those that are on the same surfaces (somehow)
                Profiler.BeginSample("Job_MergeTouchingBrushVerticesJob2");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.allFindLoopOverlapIntersectionsJobHandle;
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,

                            // Read / Write
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,
                        };
#if RUN_IN_SERIAL
                        treeUpdate.mergeTouchingBrushVertices2JobHandle = mergeTouchingBrushVerticesJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.mergeTouchingBrushVertices2JobHandle = mergeTouchingBrushVerticesJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PerformCSGJob");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.mergeTouchingBrushVertices2JobHandle, 
                                                                            treeUpdate.updateBrushCategorizationTablesJobHandle);

                        // Perform CSG
                        // TODO: determine when a brush is completely inside another brush
                        //		 (might not have any intersection loops)
                        var performCSGJob = new PerformCSGJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            routingTableLookup          = treeUpdate.routingTableLookup,
                            brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                            input                       = treeUpdate.dataStream1.AsReader(),

                            // Write
                            output                      = treeUpdate.dataStream2.AsWriter(),
                        };
#if RUN_IN_SERIAL
                        treeUpdate.allPerformAllCSGJobHandle = performCSGJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 8, dependencies);
#else
                        treeUpdate.allPerformAllCSGJobHandle = performCSGJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 8, dependencies);
#endif
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GenerateSurfaceTrianglesJob");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.generateBasePolygonLoopsJobHandle);
                                            
                        var chiselLookupValues  = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        ref var brushRenderBufferCache = ref chiselLookupValues.brushRenderBufferCache;

                        // TODO: Potentially merge this with PerformCSGJob?
                        var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            basePolygons                = treeUpdate.basePolygons,
                            transformations             = treeUpdate.transformations,
                            input                       = treeUpdate.dataStream2.AsReader(),

                            // Write
                            brushRenderBufferCache      = brushRenderBufferCache.AsParallelWriter(),
                            //brushRenderBuffers          = treeUpdate.brushRenderBuffers,          // TODO: figure out why this doesn't work w/ incremental updates
                        };
#if RUN_IN_SERIAL
                        treeUpdate.allGenerateSurfaceTrianglesJobHandle = generateSurfaceRenderBuffers.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 64, dependencies);
#else
                        treeUpdate.allGenerateSurfaceTrianglesJobHandle = generateSurfaceRenderBuffers.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 64, dependencies);
#endif
                    }
                } finally { Profiler.EndSample(); }
                
                Profiler.EndSample();
                #endregion

                #region Reset Flags
                // Reset the flags before the dispose of these containers are scheduled
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;
                    for (int b = 0; b < treeUpdate.brushCount; b++)
                    { 
                        var brushIndexOrder = treeUpdate.allTreeBrushIndexOrders[b];
                        int brushNodeIndex  = brushIndexOrder.nodeIndex;
                        var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                        nodeFlags.status = NodeStatusFlags.None;
                        CSGManager.nodeFlags[brushNodeIndex] = nodeFlags;
                    }

                    var treeNodeIndex = treeUpdate.treeNodeIndex;
                    {
                        var flags = nodeFlags[treeNodeIndex];
                        flags.UnSetNodeFlag(NodeStatusFlags.TreeNeedsUpdate);
                        flags.SetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                        nodeFlags[treeNodeIndex] = flags;
                    }
                }
                #endregion

                #region Complete Jobs

                //JobHandle.ScheduleBatchedJobs();

                Profiler.BeginSample("CSG_JobComplete");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;
                    finalJobHandle = JobHandle.CombineDependencies(treeUpdate.allGenerateSurfaceTrianglesJobHandle, finalJobHandle);
                }
                finalJobHandle.Complete();
                Profiler.EndSample();
                #endregion

                #region Store cached values back into cache (by node Index)
                Profiler.BeginSample("CSG_StoreToCache");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate          = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;
                    var chiselLookupValues              = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                    ref var transformationCache         = ref chiselLookupValues.transformationCache;
                    ref var basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                    ref var brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                    ref var brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                    ref var routingTableCache           = ref chiselLookupValues.routingTableCache;
                    ref var brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;
                    //ref var brushRenderBufferCache    = ref chiselLookupValues.brushRenderBufferCache;
                    ref var treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;

                    // TODO: what if there are holes that are not disposed? what if we overwrite something that we didn't dispose?
                    transformationCache         .Clear();
                    basePolygonCache            .Clear();
                    brushTreeSpaceBoundCache    .Clear();
                    brushTreeSpacePlaneCache    .Clear();
                    routingTableCache           .Clear();
                    brushesTouchedByBrushCache  .Clear();
                    //brushRenderBufferCache    .Clear();
                    treeSpaceVerticesCache      .Clear();
                    for (int i = 0; i < treeUpdate.brushCount; i++)
                    {
                        var nodeIndex = treeUpdate.allTreeBrushIndexOrders[i].nodeIndex;
                        transformationCache[nodeIndex]          = treeUpdate.transformations[i];
                        basePolygonCache[nodeIndex]             = treeUpdate.basePolygons[i];
                        brushTreeSpaceBoundCache[nodeIndex]     = treeUpdate.brushTreeSpaceBounds[i];
                        brushTreeSpacePlaneCache[nodeIndex]     = treeUpdate.brushTreeSpacePlanes[i];
                        routingTableCache[nodeIndex]            = treeUpdate.routingTableLookup[i];
                        brushesTouchedByBrushCache[nodeIndex]   = treeUpdate.brushesTouchedByBrushes[i];
                        //brushRenderBufferCache[nodeIndex]     = treeUpdate.brushRenderBuffers[i];
                        treeSpaceVerticesCache[nodeIndex]       = treeUpdate.treeSpaceVerticesArray[i];
                    }
                }
                Profiler.EndSample();
                #endregion
            }
            finally
            {
                #region Deallocate all temporaries
                Profiler.BeginSample("CSG_Deallocate");
                {
                    var disposeJobHandle = finalJobHandle;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];

                        treeUpdate.transformations              .Dispose();
                        treeUpdate.basePolygons                 .Dispose();
                        treeUpdate.brushTreeSpaceBounds         .Dispose();
                        treeUpdate.brushTreeSpacePlanes         .Dispose();
                        treeUpdate.routingTableLookup           .Dispose();
                        treeUpdate.brushesTouchedByBrushes      .Dispose();
                        //treeUpdate.brushRenderBuffers         .Dispose();
                        treeUpdate.dataStream1                  .Dispose();//disposeJobHandle);
                        treeUpdate.dataStream2                  .Dispose();//disposeJobHandle);
                        treeUpdate.brushMeshLookup              .Dispose();//disposeJobHandle);
                        treeUpdate.allTreeBrushIndexOrders      .Dispose();//disposeJobHandle);
                        treeUpdate.rebuildTreeBrushIndexOrders  .Dispose();//disposeJobHandle);
                        treeUpdate.brushBrushIntersections      .Dispose();//disposeJobHandle);
                        treeUpdate.brushesThatNeedIndirectUpdate.Dispose();//disposeJobHandle);
                        treeUpdate.uniqueBrushPairs             .Dispose();//disposeJobHandle);

                        var values = treeUpdate.intersectionLoopBlobs.GetValueArray(Allocator.Temp);
                        foreach (var item in values)
                            if (item.IsCreated) item.Dispose();
                        treeUpdate.intersectionLoopBlobs        .Dispose();//disposeJobHandle);
                        values.Dispose();

                        foreach (var item in treeUpdate.intersectingBrushes)
                            if (item.IsCreated) item.Dispose();
                        treeUpdate.intersectingBrushes          .Dispose();//disposeJobHandle);

                        treeUpdate.treeSpaceVerticesArray       .Dispose();//disposeJobHandle);
                    }
                }
                Profiler.EndSample();
                #endregion
            }

            #region Clear garbage
            // Remove garbage
            s_AllTreeBrushIndexOrdersList.Clear();
            s_BrushMeshList.Clear();
            s_RebuildTreeBrushIndexOrdersList.Clear();
            s_TransformTreeBrushIndicesList.Clear();
            if (s_TreeUpdates != null)
            {
                for (int i = 0; i < s_TreeUpdates.Length; i++)
                    s_TreeUpdates[i] = default;
            }
            #endregion

            return finalJobHandle;
        }

        

        // Sort trees so we try to schedule the slowest ones first, so the faster ones can then fill the gaps in between
        struct TreeSorter : IComparer<TreeUpdate>
        {
            public int Compare(TreeUpdate x, TreeUpdate y)
            {
                if (!x.brushBrushIntersections.IsCreated)
                {
                    if (!y.brushBrushIntersections.IsCreated)
                        return 0;
                    return 1;
                }
                if (!y.brushBrushIntersections.IsCreated)
                    return -1;
                var xBrushBrushIntersectionsCount = x.brushBrushIntersections.Count();
                var yBrushBrushIntersectionsCount = y.brushBrushIntersections.Count();
                if (xBrushBrushIntersectionsCount < yBrushBrushIntersectionsCount)
                    return 1;
                if (xBrushBrushIntersectionsCount > yBrushBrushIntersectionsCount)
                    return -1;

                if (x.updateCount < y.updateCount)
                    return 1;
                if (x.updateCount > y.updateCount)
                    return -1;

                return x.treeNodeIndex - y.treeNodeIndex;
            }
        }

        #region Rebuild / Update
        static NodeTransformations GetNodeTransformation(int nodeIndex)
        {
            // TODO: clean this up and make this sensible

            // Note: Currently "localTransformation" is actually nodeToTree, but only for all the brushes. 
            //       Branches do not have a transformation set at the moment.

            // TODO: should be transformations the way up to the tree, not just tree vs brush
            var brushLocalTransformation     = CSGManager.nodeLocalTransforms[nodeIndex].localTransformation;
            var brushLocalInvTransformation  = CSGManager.nodeLocalTransforms[nodeIndex].invLocalTransformation;

            var nodeTransform                = CSGManager.nodeTransforms[nodeIndex];
            nodeTransform.nodeToTree = brushLocalTransformation;
            nodeTransform.treeToNode = brushLocalInvTransformation;
            CSGManager.nodeTransforms[nodeIndex] = nodeTransform;

            return new NodeTransformations { nodeToTree = nodeTransform.nodeToTree, treeToNode = nodeTransform.treeToNode };
        }
        #endregion

        #region Reset/Rebuild
        static void Reset()
        {
            for (int t = 0; t < trees.Count; t++)
            {
                var treeNodeID = trees[t];
                if (!IsValidNodeID(treeNodeID) || !AssertNodeType(treeNodeID, CSGNodeType.Tree))
                    continue;

                var treeNodeIndex   = treeNodeID - 1;
                var treeInfo        = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;
                treeInfo.Reset();
            }
        }

        internal static bool UpdateAllTreeMeshes(out JobHandle allTrees)
        {
            allTrees = default(JobHandle);
            bool needUpdate = false;
            // Check if we have a tree that needs updates
            for (int t = 0; t < trees.Count; t++)
            {
                var treeNodeID = trees[t];
                var treeNodeIndex = treeNodeID - 1;
                if (nodeFlags[treeNodeIndex].IsNodeFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                {
                    needUpdate = true;
                    break;
                }
            }

            if (!needUpdate)
                return false;

            UpdateDelayedHierarchyModifications();

            allTrees = UpdateTreeMeshes(trees.ToArray());
            return true;
        }

        internal static bool RebuildAll()
        {
            Reset();
            if (!UpdateAllTreeMeshes(out JobHandle handle))
                return false;
            handle.Complete();
            return true;
        }
        #endregion
    }
}
