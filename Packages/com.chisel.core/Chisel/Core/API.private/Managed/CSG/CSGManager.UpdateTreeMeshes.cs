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
            public readonly List<int> treeBrushes = new List<int>();
            public readonly List<GeneratedMeshDescription> meshDescriptions = new List<GeneratedMeshDescription>();
            public readonly List<SubMeshCounts> subMeshCounts = new List<SubMeshCounts>();


            public void Reset()
            {
                subMeshCounts.Clear();
            }
        }

        internal struct TreeUpdate
        {
            public int                      treeNodeIndex;
            public NativeArray<IndexOrder>  allTreeBrushIndexOrders;
            public NativeArray<int>         nodeIndexToNodeOrder;
            public int                      nodeIndexToNodeOrderOffset;
            public int                      maxNodeOrder;
            public NativeList<IndexOrder>   rebuildTreeBrushIndexOrders;
            
            public BlobAssetReference<CompactTree>  compactTree;

            // TODO: We store this per tree, and ensure brushes have ids from 0 to x per tree, then we can use an array here.
            //       Remap "local index" to "nodeindex" and back? How to make this efficiently work with caching stuff?
            public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>          treeSpaceVerticesArray;

            public NativeMultiHashMap<int, BrushPair>                                   brushBrushIntersections;
            
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
            

            public NativeStream   dataStream1;
            public NativeStream   dataStream2;

            public JobHandle generateTreeSpaceVerticesAndBoundsJobHandle;
            public JobHandle generateBasePolygonLoopsJobHandle;
            public JobHandle mergeTouchingBrushVerticesJobHandle;
            public JobHandle mergeTouchingBrushVertices2JobHandle;

            public JobHandle findAllIntersectionsJobHandle;
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

                if (x.rebuildTreeBrushIndexOrders.Length < y.rebuildTreeBrushIndexOrders.Length)
                    return 1;
                if (x.rebuildTreeBrushIndexOrders.Length > y.rebuildTreeBrushIndexOrders.Length)
                    return -1;

                return x.treeNodeIndex - y.treeNodeIndex;
            }
        }


        static readonly List<IndexOrder>                        s_AllTreeBrushIndexOrdersList     = new List<IndexOrder>();
        static readonly List<BlobAssetReference<BrushMeshBlob>> s_BrushMeshList                   = new List<BlobAssetReference<BrushMeshBlob>>();
        static readonly List<IndexOrder>                        s_RebuildTreeBrushIndexOrdersList = new List<IndexOrder>();
        static readonly List<int>                               s_TransformTreeBrushIndicesList   = new List<int>();
        static TreeUpdate[]     s_TreeUpdates;

        static readonly TreeSorter s_TreeSorter = new TreeSorter();

        internal static JobHandle UpdateTreeMeshes(int[] treeNodeIDs)
        {
            var finalJobHandle = default(JobHandle);
            
#if UNITY_EDITOR
            //JobsUtility.JobWorkerCount = math.max(1, ((JobsUtility.JobWorkerMaximumCount + 1) / 2) - 1);
#endif

            if (s_TreeUpdates == null ||
                s_TreeUpdates.Length < treeNodeIDs.Length)
                s_TreeUpdates = new TreeUpdate[treeNodeIDs.Length];
            var treeUpdateLength = 0;
            Profiler.BeginSample("Tag_Setup");
            for (int t = 0; t < treeNodeIDs.Length; t++)
            {
                var treeNodeIndex       = treeNodeIDs[t] - 1;
                var treeInfo            = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;

                Profiler.BeginSample("Tag_Reset");
                treeInfo.Reset();
                Profiler.EndSample();

                var treeBrushes = treeInfo.treeBrushes;
                if (treeBrushes.Count == 0)
                    continue;


                // TODO: do this in job, build brushMeshList in same job
                Profiler.BeginSample("CSG_BrushMeshBlob_Generation");
                ChiselMeshLookup.Update();
                Profiler.EndSample();

                var chiselMeshValues = ChiselMeshLookup.Value;
                ref var brushMeshBlobs = ref chiselMeshValues.brushMeshBlobs;

                // Removes all brushes that have MeshID == 0 from treeBrushesArray
                s_AllTreeBrushIndexOrdersList.Clear();
                s_BrushMeshList.Clear();
                s_RebuildTreeBrushIndexOrdersList.Clear();
                s_TransformTreeBrushIndicesList.Clear();
                var nodeIndexMin = int.MaxValue;
                var nodeIndexMax = 0;
                for (int brushNodeOrder = 0, i = 0; i < treeBrushes.Count; i++)
                {
                    // TODO: Make sure that when we remove brushes from the hierarchy, it's guaranteed to be removed from treeBrushes as well
                    int brushNodeID = treeBrushes[i];
                    if (!IsValidNodeID(brushNodeID))
                        continue;

                    int brushNodeIndex  = brushNodeID - 1;
                    var brushMeshID     = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo.brushMeshInstanceID;
                    if (brushMeshID == 0)
                        continue;

                    // We need the index into the tree to ensure deterministic ordering
                    var brushIndexOrder = new IndexOrder { nodeIndex = brushNodeIndex, nodeOrder = brushNodeOrder };
                    s_AllTreeBrushIndexOrdersList.Add(brushIndexOrder);
                    brushNodeOrder++;

                    var brushMeshIndex = brushMeshID - 1;
                    s_BrushMeshList.Add(brushMeshBlobs[brushMeshIndex]);

                    nodeIndexMin = math.min(nodeIndexMin, brushNodeIndex);
                    nodeIndexMax = math.max(nodeIndexMax, brushNodeIndex);

                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    if (nodeFlags.status == NodeStatusFlags.None)
                        continue;

                    s_RebuildTreeBrushIndexOrdersList.Add(brushIndexOrder);
                }


                
                var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeIndex];

                chiselLookupValues.EnsureCapacity(s_AllTreeBrushIndexOrdersList.Count);

                ref var transformationCache         = ref chiselLookupValues.transformationCache;
                ref var basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                ref var brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                ref var brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                ref var treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                ref var brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;
                ref var brushRenderBufferCache      = ref chiselLookupValues.brushRenderBufferCache;
                ref var routingTableCache           = ref chiselLookupValues.routingTableCache;

                // TODO: store nodes separately & sequentially per tree
                var nodeIndexToNodeOrderOffset  = nodeIndexMin;
                var nodeIndexToNodeOrderArray   = new int[(nodeIndexMax - nodeIndexMin) + 1];
                for (int i = 0; i < s_AllTreeBrushIndexOrdersList.Count; i++)
                {
                    var nodeIndex = s_AllTreeBrushIndexOrdersList[i].nodeIndex;
                    var nodeOrder = s_AllTreeBrushIndexOrdersList[i].nodeOrder;
                    nodeIndexToNodeOrderArray[nodeIndex - nodeIndexToNodeOrderOffset] = nodeOrder;
                }

                if (s_RebuildTreeBrushIndexOrdersList.Count == 0)
                {
                    var flags = nodeFlags[treeNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.TreeNeedsUpdate);
                    flags.UnSetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                    nodeFlags[treeNodeIndex] = flags;
                    continue;
                }

                var anyHierarchyModified = false;
                for (int b = 0; b < s_RebuildTreeBrushIndexOrdersList.Count; b++)
                {
                    var brushIndexOrder = s_RebuildTreeBrushIndexOrdersList[b];
                    int brushNodeIndex  = brushIndexOrder.nodeIndex;
                    
                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    if (basePolygonCache.TryGetValue(brushNodeIndex, out var basePolygonsBlob))
                    {
                        basePolygonCache.Remove(brushNodeIndex);
                        if (basePolygonsBlob.IsCreated)
                            basePolygonsBlob.Dispose();
                    }
                    if (brushTreeSpaceBoundCache.ContainsKey(brushNodeIndex))
                    {
                        brushTreeSpaceBoundCache.Remove(brushNodeIndex);
                    }

                    if (treeSpaceVerticesCache.TryGetValue(brushNodeIndex, out var treeSpaceVerticesBlob))
                    {
                        treeSpaceVerticesCache.Remove(brushNodeIndex);
                        if (treeSpaceVerticesBlob.IsCreated)
                            treeSpaceVerticesBlob.Dispose();
                    }

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
                        s_TransformTreeBrushIndicesList.Add(brushNodeIndex);
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }

                    CSGManager.nodeFlags[brushNodeIndex] = nodeFlags;
                }



                if (s_RebuildTreeBrushIndexOrdersList.Count != s_AllTreeBrushIndexOrdersList.Count)
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
                            int otherBrushIndex = brushIntersections[i].nodeIndex;
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


                Profiler.BeginSample("CSG_RemoveOldData");
                // Clean up values we're rebuilding below, including the ones with brushMeshID == 0
                chiselLookupValues.RemoveSurfaceRenderBuffersByBrushIndexOrder(s_RebuildTreeBrushIndexOrdersList);
                chiselLookupValues.RemoveRoutingTablesByBrushIndexOrder(s_RebuildTreeBrushIndexOrdersList);
                chiselLookupValues.RemoveBrushTouchesByBrushIndexOrder(s_AllTreeBrushIndexOrdersList);
                chiselLookupValues.RemoveBrushTreeSpacePlanesByBrushIndexOrder(s_RebuildTreeBrushIndexOrdersList);

                chiselLookupValues.RemoveTransformationsByBrushIndex(s_TransformTreeBrushIndicesList);
                Profiler.EndSample();


                Profiler.BeginSample("CSG_Allocations");//time=2.45ms
                var allTreeBrushIndexOrders     = s_AllTreeBrushIndexOrdersList.ToNativeArray(Allocator.TempJob);
                var nodeIndexToNodeOrder        = nodeIndexToNodeOrderArray.ToNativeArray(Allocator.TempJob);
                var rebuildTreeBrushIndexOrders = s_RebuildTreeBrushIndexOrdersList.ToNativeList(Allocator.TempJob);
                var brushMeshLookup             = s_BrushMeshList.ToNativeArray(Allocator.TempJob); 
                Profiler.EndSample();

                Profiler.BeginSample("CSG_DirtyAllOutlines");
                {
                    for (int b = 0; b < allTreeBrushIndexOrders.Length; b++)
                    {
                        var brushIndexOrder = allTreeBrushIndexOrders[b];
                        int brushNodeIndex  = brushIndexOrder.nodeIndex;
                        var brushInfo = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo;
                        brushInfo.brushOutlineGeneration++;
                        brushInfo.brushOutlineDirty = true;
                    }
                }
                Profiler.EndSample();
                

                // TODO: optimize, only do this when necessary
                Profiler.BeginSample("CSG_UpdateBrushTransformations");
                {
                    for (int b = 0; b < s_TransformTreeBrushIndicesList.Count; b++)
                    {
                        var brushNodeIndex = s_TransformTreeBrushIndicesList[b];
                        UpdateNodeTransformation(ref transformationCache, brushNodeIndex);
                        var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                        nodeFlags.status &= ~NodeStatusFlags.TransformationModified;
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }
                }
                Profiler.EndSample();

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


                Profiler.BeginSample("CSG_Allocations");
                Profiler.BeginSample("CSG_BrushOutputLoops");// TODO: make this a job
                var brushLoopCount = rebuildTreeBrushIndexOrders.Length;                
                for (int index = 0; index < brushLoopCount; index++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrders[index];

                    if (rebuildTreeBrushIndexOrders.Contains(brushIndexOrder))
                    {
                        int brushNodeIndex = brushIndexOrder.nodeIndex;
                        if (brushRenderBufferCache.TryGetValue(brushNodeIndex, out var oldBrushRenderBuffer) &&
                            oldBrushRenderBuffer.IsCreated)
                            oldBrushRenderBuffer.Dispose();
                        brushRenderBufferCache.Remove(brushNodeIndex);
                    }
                }
                Profiler.EndSample();
                
                // TODO: figure out more accurate maximum sizes
                var triangleArraySize       = GeometryMath.GetTriangleArraySize(allTreeBrushIndexOrders.Length);
                var intersectionCount       = triangleArraySize;
                var intersectionLoopBlobs   = new NativeMultiHashMap<int, BlobAssetReference<BrushIntersectionLoop>>(intersectionCount * 2, Allocator.TempJob);
                var brushBrushIntersections = new NativeMultiHashMap<int, BrushPair>(intersectionCount * 2, Allocator.TempJob);
                var uniqueBrushPairs        = new NativeList<BrushPair>(intersectionCount, Allocator.TempJob);
                var intersectingBrushes     = new NativeList<BlobAssetReference<BrushPairIntersection>>(intersectionCount, Allocator.TempJob);
                var dataStream1             = new NativeStream(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                var dataStream2             = new NativeStream(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                Profiler.EndSample();


                Profiler.BeginSample("CSG_CopyToArray");
                var transformations = new NativeArray<NodeTransformations>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    transformations[i] = transformationCache[nodeIndex];
                }
                
                var basePolygons = new NativeArray<BlobAssetReference<BasePolygonsBlob>>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!basePolygonCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<BasePolygonsBlob>.Null;
                    basePolygons[i] = item;
                }

                var brushTreeSpaceBounds = new NativeArray<MinMaxAABB>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!brushTreeSpaceBoundCache.TryGetValue(nodeIndex, out var item))
                        item = new MinMaxAABB();
                    brushTreeSpaceBounds[i] = item;
                }

                var brushTreeSpacePlanes = new NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!brushTreeSpacePlaneCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<BrushTreeSpacePlanes>.Null;
                    brushTreeSpacePlanes[i] = item; 
                }

                var routingTableLookup = new NativeArray<BlobAssetReference<RoutingTable>>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!routingTableCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<RoutingTable>.Null;
                    routingTableLookup[i] = item;
                }

                var brushesTouchedByBrushes = new NativeArray<BlobAssetReference<BrushesTouchedByBrush>>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!brushesTouchedByBrushCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<BrushesTouchedByBrush>.Null;
                    brushesTouchedByBrushes[i] = item;
                }

                /*
                var brushRenderBuffers = new NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!brushRenderBufferCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<ChiselBrushRenderBuffer>.Null;
                    brushRenderBuffers[i] = item;
                }*/

                var treeSpaceVerticesArray = new NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrders[i].nodeIndex;
                    if (!treeSpaceVerticesCache.TryGetValue(nodeIndex, out var item))
                        item = BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null;                    
                    treeSpaceVerticesArray[i] = item;
                }
                Profiler.EndSample();

                s_TreeUpdates[treeUpdateLength] = new TreeUpdate
                {
                    treeNodeIndex               = treeNodeIndex,
                    allTreeBrushIndexOrders     = allTreeBrushIndexOrders,
                    nodeIndexToNodeOrder        = nodeIndexToNodeOrder,
                    nodeIndexToNodeOrderOffset  = nodeIndexToNodeOrderOffset,
                    rebuildTreeBrushIndexOrders = rebuildTreeBrushIndexOrders,
                    maxNodeOrder                = treeBrushes.Count,
                    brushMeshLookup             = brushMeshLookup,
                    transformations             = transformations,
                    basePolygons                = basePolygons,
                    brushTreeSpaceBounds        = brushTreeSpaceBounds,
                    treeSpaceVerticesArray      = treeSpaceVerticesArray,
                    brushTreeSpacePlanes        = brushTreeSpacePlanes,
                    routingTableLookup          = routingTableLookup,
                    brushesTouchedByBrushes     = brushesTouchedByBrushes,
                    //brushRenderBuffers        = brushRenderBuffers,
                    brushBrushIntersections     = brushBrushIntersections,
                    uniqueBrushPairs            = uniqueBrushPairs,
                    intersectionLoopBlobs       = intersectionLoopBlobs,
                    intersectingBrushes         = intersectingBrushes,
                    dataStream1                 = dataStream1,
                    dataStream2                 = dataStream2,
                    compactTree                 = compactTree
                };
                treeUpdateLength++;
            }
            Profiler.EndSample();


            // Sort trees from largest to smallest
            Array.Sort(s_TreeUpdates, s_TreeSorter);

            // TODO: rewrite code to not need [NativeDisableParallelForRestriction]
            // TODO: ensure we only update exactly what we need, and nothing more

            try
            {
                Profiler.BeginSample("CSG_Jobs");

                // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                Profiler.BeginSample("Job_GenerateBoundsLoops");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];

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
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        var dependencies = treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle;
                        var findAllIntersectionsJob = new FindAllBrushIntersectionsJob
                        {
                            // Read
                            allTreeBrushIndexOrders = treeUpdate.allTreeBrushIndexOrders,
                            transformations         = treeUpdate.transformations,
                            brushMeshLookup         = treeUpdate.brushMeshLookup,
                            brushTreeSpaceBounds    = treeUpdate.brushTreeSpaceBounds,
                        
                            // Read/Write
                            updateBrushIndexOrders  = treeUpdate.rebuildTreeBrushIndexOrders,
                        
                            // Write
                            brushBrushIntersections = treeUpdate.brushBrushIntersections.AsParallelWriter()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.
                            Run(dependencies);
#else
                        treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.
                            Schedule(dependencies);
#endif
                    }

                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        var dependencies = treeUpdate.findAllIntersectionsJobHandle;
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                        {
                            // Read
                            treeNodeIndex               = treeUpdate.treeNodeIndex,
                            treeBrushIndexOrders        = rebuildTreeBrushIndexOrdersArray,
                            nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
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
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVertices2Job
                        {
                            // Read
                            treeBrushIndexOrders        = rebuildTreeBrushIndexOrdersArray,
                            nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
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
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.mergeTouchingBrushVerticesJobHandle, treeUpdate.findIntersectingBrushesJobHandle);
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobs
                        {
                            // Read
                            treeBrushIndexOrders        = rebuildTreeBrushIndexOrdersArray,
                            nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
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
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
                            treeBrushIndexOrders    = rebuildTreeBrushIndexOrdersArray,
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
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        // Build categorization trees for brushes
                        var createRoutingTableJob = new CreateRoutingTableJob
                        {
                            // Read
                            treeBrushIndexOrders    = rebuildTreeBrushIndexOrdersArray,
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
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var findBrushPairsJob = new FindBrushPairsJob
                        {
                            // Read
                            treeBrushIndexOrders        = rebuildTreeBrushIndexOrdersArray,
                            nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
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
                        var dependencies    = treeUpdate.findBrushPairsJobHandle;
#if RUN_IN_SERIAL
                        var uniqueBrushPairsArray = treeUpdate.uniqueBrushPairs.AsArray();
#else
                        var uniqueBrushPairsArray = treeUpdate.uniqueBrushPairs.AsDeferredJobArray();
#endif
                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
                            uniqueBrushPairs        = uniqueBrushPairsArray,
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
                        var dependencies    = JobHandle.CombineDependencies(
                                                            treeUpdate.mergeTouchingBrushVerticesJobHandle,
                                                            treeUpdate.updateBrushTreeSpacePlanesJobHandle, 
                                                            treeUpdate.prepareBrushPairIntersectionsJobHandle);
#if RUN_IN_SERIAL
                        var intersectingBrushesArray = treeUpdate.intersectingBrushes.AsArray();
#else
                        var intersectingBrushesArray = treeUpdate.intersectingBrushes.AsDeferredJobArray();
#endif
                        var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Read
                            brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
                            intersectingBrushes         = intersectingBrushesArray,
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,
                            nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,

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
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.prepareBrushPairIntersectionsJobHandle, treeUpdate.generateBasePolygonLoopsJobHandle);
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                        {
                            // Read
                            treeBrushIndexOrders        = rebuildTreeBrushIndexOrdersArray,
                            nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
                            maxNodeOrder                = treeUpdate.maxNodeOrder,
                            intersectionLoopBlobs       = treeUpdate.intersectionLoopBlobs,
                            brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
                            basePolygons                = treeUpdate.basePolygons,// by nodeOrder (non-bounds, non-surfaceinfo)

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
                        var dependencies    = treeUpdate.allFindLoopOverlapIntersectionsJobHandle;
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                        {
                            // Read
                            treeBrushIndexOrders        = rebuildTreeBrushIndexOrdersArray,
                            nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
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
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.mergeTouchingBrushVertices2JobHandle, 
                                                                            treeUpdate.updateBrushCategorizationTablesJobHandle);

                        // Perform CSG
                        // TODO: determine when a brush is completely inside another brush
                        //		 (might not have any intersection loops)
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var performCSGJob = new PerformCSGJob
                        {
                            // Read
                            treeBrushNodeIndexOrders    = rebuildTreeBrushIndexOrdersArray,    
                            nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
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
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.generateBasePolygonLoopsJobHandle);

                    
                        var chiselLookupValues  = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        ref var brushRenderBufferCache = ref chiselLookupValues.brushRenderBufferCache;

                        // TODO: Potentially merge this with PerformCSGJob?
#if RUN_IN_SERIAL
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsArray();
#else
                        var rebuildTreeBrushIndexOrdersArray = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray();
#endif
                        var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                        {
                            // Read
                            treeBrushNodeIndexOrders    = rebuildTreeBrushIndexOrdersArray,  
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


                // Reset the flags before the dispose of these containers are scheduled
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    for (int b = 0; b < treeUpdate.allTreeBrushIndexOrders.Length; b++)
                    { 
                        var brushIndexOrder = treeUpdate.allTreeBrushIndexOrders[b];
                        int brushNodeIndex  = brushIndexOrder.nodeIndex;
                        var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                        nodeFlags.status = NodeStatusFlags.None;
                        CSGManager.nodeFlags[brushNodeIndex] = nodeFlags;
                    }
                }


                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    var treeNodeIndex = treeUpdate.treeNodeIndex;
                    finalJobHandle = JobHandle.CombineDependencies(treeUpdate.allGenerateSurfaceTrianglesJobHandle, finalJobHandle);

                    {
                        var flags = nodeFlags[treeNodeIndex];
                        flags.UnSetNodeFlag(NodeStatusFlags.TreeNeedsUpdate);
                        flags.SetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                        nodeFlags[treeNodeIndex] = flags;
                    }
                }

                Profiler.EndSample();
            
                //JobHandle.ScheduleBatchedJobs();
                Profiler.BeginSample("Tag_Complete");
                finalJobHandle.Complete();
                Profiler.EndSample();
            

                Profiler.BeginSample("CSG_StoreToCache");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate          = ref s_TreeUpdates[t];
                    var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                    ref var transformationCache         = ref chiselLookupValues.transformationCache;
                    ref var basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                    ref var brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                    ref var brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                    ref var routingTableCache           = ref chiselLookupValues.routingTableCache;
                    ref var brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;
                    //ref var brushRenderBufferCache    = ref chiselLookupValues.brushRenderBufferCache;
                    ref var treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                    for (int i = 0; i < treeUpdate.allTreeBrushIndexOrders.Length; i++)
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
            }
            finally
            { 
                // Note: Seems that scheduling a Dispose will cause previous jobs to be completed?
                //       Actually faster to just call them on main thread?
                Profiler.BeginSample("Tag_BrushOutputLoopsDispose");
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
                        treeUpdate.nodeIndexToNodeOrder         .Dispose();//disposeJobHandle);
                        treeUpdate.rebuildTreeBrushIndexOrders  .Dispose();//disposeJobHandle);
                        treeUpdate.brushBrushIntersections      .Dispose();//disposeJobHandle);
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
            }

            //JobsUtility.JobWorkerCount = JobsUtility.JobWorkerMaximumCount;

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
#if UNITY_EDITOR
            GC.Collect(0, GCCollectionMode.Optimized, false, true);
#endif

            return finalJobHandle;
        }



#region Rebuild / Update
        static void UpdateNodeTransformation(ref NativeHashMap<int, NodeTransformations> transformations, int nodeIndex)
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

            transformations[nodeIndex] = new NodeTransformations { nodeToTree = nodeTransform.nodeToTree, treeToNode = nodeTransform.treeToNode };
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
