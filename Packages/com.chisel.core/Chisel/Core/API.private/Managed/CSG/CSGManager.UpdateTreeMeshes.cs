using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Profiler = UnityEngine.Profiling.Profiler;
using Debug = UnityEngine.Debug;
using System.Diagnostics;
using Unity.Mathematics;
using Unity.Entities.UniversalDelegates;

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
            public NativeList<IndexOrder>   rebuildTreeBrushIndexOrders;
            
            public BlobAssetReference<CompactTree>  compactTree;

            // TODO: We store this per tree, and ensure brushes have ids from 0 to x per tree, then we can use an array here.
            //       Remap "local index" to "nodeindex" and back? How to make this efficiently work with caching stuff?
            public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>            brushMeshLookup;
            public NativeHashMap<int, BlobAssetReference<NodeTransformations>>      transformations;
            public NativeHashMap<int, BlobAssetReference<BasePolygonsBlob>>         basePolygons;
            public NativeHashMap<int, MinMaxAABB>                                   brushTreeSpaceBounds;
            public NativeHashMap<int, BlobAssetReference<BrushTreeSpacePlanes>>     brushTreeSpacePlanes;
            public NativeHashMap<int, BlobAssetReference<RoutingTable>>             routingTableLookup;
            public NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>>    brushesTouchedByBrushes;

            public NativeHashMap<int, BlobAssetReference<ChiselBrushRenderBuffer>>  brushRenderBuffers;
            public NativeMultiHashMap<int, BrushPair>                               brushBrushIntersections;
            public NativeList<BrushPair>                                            uniqueBrushPairs;
            public NativeList<BlobAssetReference<BrushIntersectionLoops>>           intersectionLoopBlobs;
            public NativeList<BlobAssetReference<BrushPairIntersection>>            intersectingBrushes;
            
            public NativeStream   dataStream1;
            public NativeStream   dataStream2;

            public JobHandle generateBoundsJobHandle;
            public JobHandle generateBasePolygonLoopsJobHandle;

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


        internal static JobHandle UpdateTreeMeshes(int[] treeNodeIDs)
        {
            var finalJobHandle = default(JobHandle);

#if UNITY_EDITOR
            //JobsUtility.JobWorkerCount = math.max(1, ((JobsUtility.JobWorkerMaximumCount + 1) / 2) - 1);
#endif

            var treeUpdates = new TreeUpdate[treeNodeIDs.Length];
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
                
                var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeIndex];
                var chiselMeshValues    = ChiselMeshLookup.Value;

                chiselLookupValues.EnsureCapacity(treeBrushes.Count);

                ref var brushMeshBlobs          = ref chiselMeshValues.brushMeshBlobs;
                ref var transformations         = ref chiselLookupValues.transformations;
                ref var basePolygons            = ref chiselLookupValues.basePolygons;
                ref var brushTreeSpaceBounds    = ref chiselLookupValues.brushTreeSpaceBounds;
                ref var brushesTouchedByBrushes = ref chiselLookupValues.brushesTouchedByBrushes;

                // Removes all brushes that have MeshID == 0 from treeBrushesArray
                var allTreeBrushIndexOrdersList     = new List<IndexOrder>();
                var rebuildTreeBrushIndexOrdersList = new List<IndexOrder>();
                var transformTreeBrushIndicesList   = new List<int>();
                var nodeIndexMin = int.MaxValue;
                var nodeIndexMax = 0;
                for (int brushNodeOrder = 0; brushNodeOrder < treeBrushes.Count; brushNodeOrder++)
                {
                    int brushNodeID = treeBrushes[brushNodeOrder];
                    // TODO: Ensure this list is correct on removal of brush from hierarchy
                    if (!IsValidNodeID(brushNodeID))
                        continue;

                    int brushNodeIndex  = brushNodeID - 1;
                    var brushMeshID     = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo.brushMeshInstanceID;
                    if (brushMeshID == 0)
                        continue;

                    // We need the index into the tree to ensure deterministic ordering
                    var brushIndexOrder = new IndexOrder { nodeIndex = brushNodeIndex, nodeOrder = brushNodeOrder };
                    allTreeBrushIndexOrdersList.Add(brushIndexOrder);

                    nodeIndexMin = math.min(nodeIndexMin, brushNodeIndex);
                    nodeIndexMax = math.max(nodeIndexMax, brushNodeIndex);

                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    if (nodeFlags.status == NodeStatusFlags.None)
                        continue;

                    rebuildTreeBrushIndexOrdersList.Add(brushIndexOrder);
                }

                // TODO: store nodes separately & sequentially per tree
                var nodeIndexToNodeOrderOffset  = nodeIndexMin;
                var nodeIndexToNodeOrderArray   = new int[(nodeIndexMax - nodeIndexMin) + 1];
                for (int i = 0; i < allTreeBrushIndexOrdersList.Count; i++)
                {
                    var nodeIndex = allTreeBrushIndexOrdersList[i].nodeIndex;
                    var nodeOrder = allTreeBrushIndexOrdersList[i].nodeOrder;
                    nodeIndexToNodeOrderArray[nodeIndex - nodeIndexToNodeOrderOffset] = nodeOrder;
                }

                if (rebuildTreeBrushIndexOrdersList.Count == 0)
                {
                    var flags = nodeFlags[treeNodeIndex];
                    flags.UnSetNodeFlag(NodeStatusFlags.TreeNeedsUpdate);
                    flags.UnSetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                    nodeFlags[treeNodeIndex] = flags;
                    continue;
                }

                var anyHierarchyModified = false;
                for (int b = 0; b < rebuildTreeBrushIndexOrdersList.Count; b++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrdersList[b];
                    int brushNodeIndex  = brushIndexOrder.nodeIndex;
                    
                    var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                    if (basePolygons.TryGetValue(brushNodeIndex, out var basePolygonsBlob))
                    {
                        basePolygons.Remove(brushNodeIndex);
                        if (basePolygonsBlob.IsCreated)
                            basePolygonsBlob.Dispose();
                    }
                    if (brushTreeSpaceBounds.TryGetValue(brushNodeIndex, out var brushTreeSpaceBoundsBlob))
                    {
                        brushTreeSpaceBounds.Remove(brushNodeIndex);
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
                        transformTreeBrushIndicesList.Add(brushNodeIndex);
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }

                    CSGManager.nodeFlags[brushNodeIndex] = nodeFlags;
                }



                if (rebuildTreeBrushIndexOrdersList.Count != allTreeBrushIndexOrdersList.Count)
                {
                    for (int b = 0; b < rebuildTreeBrushIndexOrdersList.Count; b++)
                    {
                        var brushIndexOrder = rebuildTreeBrushIndexOrdersList[b];
                        int brushNodeIndex  = brushIndexOrder.nodeIndex;
                        var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                        if ((nodeFlags.status & NodeStatusFlags.NeedAllTouchingUpdated) == NodeStatusFlags.None)
                            continue;

                        if (!chiselLookupValues.brushesTouchedByBrushes.TryGetValue(brushNodeIndex, out var brushTouchedByBrush))
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
                            if (!rebuildTreeBrushIndexOrdersList.Contains(otherIndexOrder))
                                rebuildTreeBrushIndexOrdersList.Add(otherIndexOrder);
                        }
                    }
                }


                // Clean up values we're rebuilding below, including the ones with brushMeshID == 0
                chiselLookupValues.RemoveSurfaceRenderBuffersByBrushIndexOrder(rebuildTreeBrushIndexOrdersList);
                chiselLookupValues.RemoveRoutingTablesByBrushIndexOrder(rebuildTreeBrushIndexOrdersList);
                chiselLookupValues.RemoveBrushTouchesByBrushIndexOrder(allTreeBrushIndexOrdersList);
                chiselLookupValues.RemoveBrushTreeSpacePlanesByBrushIndexOrder(rebuildTreeBrushIndexOrdersList);

                chiselLookupValues.RemoveTransformationsByBrushIndex(transformTreeBrushIndicesList);


                Profiler.BeginSample("Tag_Allocations");//time=2.45ms
                var allTreeBrushIndexOrders     = allTreeBrushIndexOrdersList.ToNativeArray(Allocator.TempJob);
                var nodeIndexToNodeOrder        = nodeIndexToNodeOrderArray.ToNativeArray(Allocator.TempJob);
                var rebuildTreeBrushIndexOrders = rebuildTreeBrushIndexOrdersList.ToNativeList(Allocator.TempJob);
                var brushMeshLookup             = new NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                Profiler.EndSample();


                // NOTE: needs to contain ALL brushes in tree, EVEN IF THEY ARE NOT UPDATED!
                Profiler.BeginSample("Tag_BuildBrushMeshLookup");
                {
                    for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                    {
                        var brushIndexOrder = allTreeBrushIndexOrders[i];
                        int brushNodeIndex  = brushIndexOrder.nodeIndex;
                        var brushMeshIndex  = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo.brushMeshInstanceID - 1;
                        brushMeshLookup[brushNodeIndex] = brushMeshBlobs[brushMeshIndex];
                    }

                    // TODO: make this more efficient, adding some meshes twice
                    if (rebuildTreeBrushIndexOrders.Length != allTreeBrushIndexOrders.Length)
                    {
                        for (int i = 0; i < rebuildTreeBrushIndexOrders.Length; i++)
                        {
                            var brushIndexOrder = rebuildTreeBrushIndexOrders[i];
                            int brushNodeIndex  = brushIndexOrder.nodeIndex;
                            var brushMeshIndex  = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo.brushMeshInstanceID - 1;
                            brushMeshLookup[brushNodeIndex] = brushMeshBlobs[brushMeshIndex];
                        }
                    }
                }
                Profiler.EndSample();

                Profiler.BeginSample("Tag_DirtyAllOutlines");
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
                Profiler.BeginSample("Tag_UpdateBrushTransformations");
                {
                    for (int b = 0; b < transformTreeBrushIndicesList.Count; b++)
                    {
                        var brushNodeIndex = transformTreeBrushIndicesList[b];
                        UpdateNodeTransformation(ref transformations, brushNodeIndex);
                        var nodeFlags = CSGManager.nodeFlags[brushNodeIndex];
                        nodeFlags.status &= ~NodeStatusFlags.TransformationModified;
                        nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                    }
                }
                Profiler.EndSample();

                BlobAssetReference<CompactTree> compactTree = chiselLookupValues.compactTree;

                // only rebuild this when the hierarchy changes
                if (anyHierarchyModified ||
                    !compactTree.IsCreated)
                {
                    if (chiselLookupValues.compactTree.IsCreated)
                        chiselLookupValues.compactTree.Dispose();

                    // TODO: jobify?
                    Profiler.BeginSample("Tag_CompactTree.Create");
                    compactTree = CompactTree.Create(CSGManager.nodeHierarchies, treeNodeIndex); 
                    chiselLookupValues.compactTree = compactTree;
                    Profiler.EndSample();
                }


                Profiler.BeginSample("Tag_Allocations");
                Profiler.BeginSample("Tag_BrushOutputLoops");
                var brushLoopCount = rebuildTreeBrushIndexOrders.Length;                
                for (int index = 0; index < brushLoopCount; index++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrders[index];

                    if (rebuildTreeBrushIndexOrders.Contains(brushIndexOrder))
                    {
                        int brushNodeIndex = brushIndexOrder.nodeIndex;
                        if (chiselLookupValues.brushRenderBuffers.TryGetValue(brushNodeIndex, out var oldBrushRenderBuffer) &&
                            oldBrushRenderBuffer.IsCreated)
                            oldBrushRenderBuffer.Dispose();
                        chiselLookupValues.brushRenderBuffers.Remove(brushNodeIndex);
                    }
                }
                Profiler.EndSample();
                
                // TODO: figure out more accurate maximum sizes
                var triangleArraySize       = GeometryMath.GetTriangleArraySize(allTreeBrushIndexOrders.Length);
                var intersectionCount       = triangleArraySize;
                var intersectionLoopBlobs   = new NativeList<BlobAssetReference<BrushIntersectionLoops>>(intersectionCount * 2, Allocator.TempJob);
                var brushBrushIntersections = new NativeMultiHashMap<int, BrushPair>(intersectionCount * 2, Allocator.TempJob);
                var uniqueBrushPairs        = new NativeList<BrushPair>(intersectionCount, Allocator.TempJob);
                var intersectingBrushes     = new NativeList<BlobAssetReference<BrushPairIntersection>>(intersectionCount, Allocator.TempJob);
                var dataStream1             = new NativeStream(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                var dataStream2             = new NativeStream(allTreeBrushIndexOrders.Length, Allocator.TempJob);
                Profiler.EndSample();


                treeUpdates[treeUpdateLength] = new TreeUpdate
                {
                    treeNodeIndex               = treeNodeIndex,
                    allTreeBrushIndexOrders     = allTreeBrushIndexOrders,
                    nodeIndexToNodeOrder        = nodeIndexToNodeOrder,
                    nodeIndexToNodeOrderOffset  = nodeIndexToNodeOrderOffset,
                    rebuildTreeBrushIndexOrders = rebuildTreeBrushIndexOrders,
                    brushMeshLookup             = brushMeshLookup,
                    transformations             = chiselLookupValues.transformations,
                    basePolygons                = chiselLookupValues.basePolygons,
                    brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBounds,
                    brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlanes,
                    routingTableLookup          = chiselLookupValues.routingTableLookup,
                    brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushes,
                    brushRenderBuffers          = chiselLookupValues.brushRenderBuffers,
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
            var treeSorter = new TreeSorter();
            Array.Sort(treeUpdates, treeSorter);

            Profiler.BeginSample("Tag_Jobified");

            // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
            Profiler.BeginSample("Tag_GenerateBoundsLoops");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate  = ref treeUpdates[t];

                    var createBoundsJob = new CreateBoundsJob
                    {
                        // Read
                        treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders,
                        brushMeshLookup         = treeUpdate.brushMeshLookup,
                        transformations         = treeUpdate.transformations,

                        // Write
                        brushTreeSpaceBounds    = treeUpdate.brushTreeSpaceBounds.AsParallelWriter()
                    };
                    treeUpdate.generateBoundsJobHandle = createBoundsJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16);
                }
            }
            finally { Profiler.EndSample(); }

            // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
            Profiler.BeginSample("Tag_FindIntersectingBrushes");
            try
            {
                // TODO: optimize, use hashed grid
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];

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
                    treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.
                        Schedule(treeUpdate.generateBoundsJobHandle);
                }
                
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findAllIntersectionsJobHandle;
                    var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                    {
                        // Read
                        treeNodeIndex               = treeUpdate.treeNodeIndex,
                        treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                        nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                        nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
                        compactTree                 = treeUpdate.compactTree,
                        brushBrushIntersections     = treeUpdate.brushBrushIntersections,

                        // Write
                        brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes.AsParallelWriter()
                    };
                    treeUpdate.findIntersectingBrushesJobHandle = storeBrushIntersectionsJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                    treeUpdate.findIntersectingBrushesJobHandle.Complete();
                }
            } finally { Profiler.EndSample(); }

            // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
            Profiler.BeginSample("Tag_GenerateBasePolygonLoops");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies            = treeUpdate.findIntersectingBrushesJobHandle;
                    var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobs
                    {
                        // Read
                        treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                        nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                        nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
                        brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                        brushMeshLookup             = treeUpdate.brushMeshLookup,
                        transformations             = treeUpdate.transformations,

                        // Write
                        basePolygons                = treeUpdate.basePolygons.AsParallelWriter()
                    };
                    treeUpdate.generateBasePolygonLoopsJobHandle = createBlobPolygonsBlobs.
                        Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                }
            }
            finally { Profiler.EndSample(); }

            // TODO: should only do this at creation time + when moved / store with brush component itself
            Profiler.BeginSample("Tag_UpdateBrushTreeSpacePlanes");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findIntersectingBrushesJobHandle;
                    var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                    {
                        // Read
                        treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                        brushMeshLookup         = treeUpdate.brushMeshLookup,
                        transformations         = treeUpdate.transformations,

                        // Write
                        brushTreeSpacePlanes    = treeUpdate.brushTreeSpacePlanes.AsParallelWriter()
                    };
                    treeUpdate.updateBrushTreeSpacePlanesJobHandle = createBrushTreeSpacePlanesJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                }
            }
            finally { Profiler.EndSample(); }

            // TODO: only update when brush or any touching brush has been added/removed or changes operation/order
            Profiler.BeginSample("Tag_UpdateBrushCategorizationTables");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findIntersectingBrushesJobHandle;
                    // Build categorization trees for brushes
                    var createRoutingTableJob = new CreateRoutingTableJob
                    {
                        // Read
                        treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                        brushesTouchedByBrushes = treeUpdate.brushesTouchedByBrushes,
                        compactTree             = treeUpdate.compactTree,

                        // Write
                        routingTableLookup      = treeUpdate.routingTableLookup.AsParallelWriter()
                    };
                    treeUpdate.updateBrushCategorizationTablesJobHandle = createRoutingTableJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                }
            } finally { Profiler.EndSample(); }
                                
            // Create unique loops between brush intersections
            Profiler.BeginSample("Tag_FindAllIntersectionLoops");
            try
            {
                // TODO: merge this with another job, there's not enough work 
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findIntersectingBrushesJobHandle;
                    var findBrushPairsJob = new FindBrushPairsJob
                    {
                        // Read
                        treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                        nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                        nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
                        brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                                    
                        // Write
                        uniqueBrushPairs            = treeUpdate.uniqueBrushPairs,
                    };
                    treeUpdate.findBrushPairsJobHandle = findBrushPairsJob.
                        Schedule(dependencies);
                }

                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = treeUpdate.findBrushPairsJobHandle;
                    var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                    {
                        // Read
                        uniqueBrushPairs        = treeUpdate.uniqueBrushPairs.AsDeferredJobArray(),
                        brushMeshBlobLookup     = treeUpdate.brushMeshLookup,
                        transformations         = treeUpdate.transformations,

                        // Write
                        intersectingBrushes     = treeUpdate.intersectingBrushes.AsParallelWriter()
                    };
                    treeUpdate.prepareBrushPairIntersectionsJobHandle = prepareBrushPairIntersectionsJob.
                        Schedule(treeUpdate.uniqueBrushPairs, 4, dependencies);
                }

                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = JobHandle.CombineDependencies(treeUpdate.updateBrushTreeSpacePlanesJobHandle, treeUpdate.prepareBrushPairIntersectionsJobHandle);
                    var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                    {
                        // Read
                        brushTreeSpacePlanes    = treeUpdate.brushTreeSpacePlanes,
                        intersectingBrushes     = treeUpdate.intersectingBrushes.AsDeferredJobArray(),

                        // Write
                        outputSurfaces          = treeUpdate.intersectionLoopBlobs.AsParallelWriter()
                    };
                    treeUpdate.findAllIntersectionLoopsJobHandle = findAllIntersectionLoopsJob.
                        Schedule(treeUpdate.intersectingBrushes, 4, dependencies);
                }
            } finally { Profiler.EndSample(); }

            Profiler.BeginSample("Tag_FindLoopOverlapIntersections");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];                    
                    var dependencies = JobHandle.CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.prepareBrushPairIntersectionsJobHandle, treeUpdate.generateBasePolygonLoopsJobHandle);
                    var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                    {
                        // Read
                        treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                        nodeIndexToNodeOrder        = treeUpdate.nodeIndexToNodeOrder,
                        nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,
                        intersectionLoopBlobs       = treeUpdate.intersectionLoopBlobs.AsDeferredJobArray(),
                        brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
                        basePolygons                = treeUpdate.basePolygons,// by nodeIndex (non-bounds, non-surfaceinfo)

                        // Write
                        output                      = treeUpdate.dataStream1.AsWriter()
                    };
                    treeUpdate.allFindLoopOverlapIntersectionsJobHandle = findLoopOverlapIntersectionsJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 64, dependencies);
                }
            } finally { Profiler.EndSample(); }

            Profiler.BeginSample("Tag_PerformCSGJob");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = JobHandle.CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.updateBrushCategorizationTablesJobHandle);

                    // Perform CSG
                    // TODO: determine when a brush is completely inside another brush
                    //		 (might not have any intersection loops)
                    var performCSGJob = new PerformCSGJob
                    {
                        // Read
                        treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                        routingTableLookup          = treeUpdate.routingTableLookup,
                        brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
                        brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                        input                       = treeUpdate.dataStream1.AsReader(),

                        // Write
                        output                      = treeUpdate.dataStream2.AsWriter(),
                    };
                    treeUpdate.allPerformAllCSGJobHandle = performCSGJob.
                        Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 32, dependencies);
                }
            } finally { Profiler.EndSample(); }

            Profiler.BeginSample("Tag_GenerateSurfaceTrianglesJob");
            try
            {
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];
                    var dependencies = JobHandle.CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.generateBasePolygonLoopsJobHandle);

                    // TODO: Make this work with burst so we can, potentially, merge it with PerformCSGJob?
                    var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                    {
                        // Read
                        treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                        basePolygons                = treeUpdate.basePolygons,
                        transformations             = treeUpdate.transformations,
                        input                       = treeUpdate.dataStream2.AsReader(),

                        // Write
                        brushRenderBuffers          = treeUpdate.brushRenderBuffers.AsParallelWriter(),
                    };
                    treeUpdate.allGenerateSurfaceTrianglesJobHandle = generateSurfaceRenderBuffers.
                        Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 64, dependencies);
                }
            } finally { Profiler.EndSample(); }

            // Reset the flags before the dispose of these containers are scheduled
            for (int t = 0; t < treeUpdateLength; t++)
            {
                ref var treeUpdate = ref treeUpdates[t];
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
                ref var treeUpdate = ref treeUpdates[t];
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


            // Note: Seems that scheduling a Dispose will cause previous jobs to be completed?
            //       Actually faster to just call them on main thread?
            Profiler.BeginSample("Tag_BrushOutputLoopsDispose");
            {
                var disposeJobHandle = finalJobHandle;
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref treeUpdates[t];

                    treeUpdate.dataStream1                  .Dispose();//disposeJobHandle);
                    treeUpdate.dataStream2                  .Dispose();//disposeJobHandle);
                    treeUpdate.brushMeshLookup              .Dispose();//disposeJobHandle);
                    treeUpdate.allTreeBrushIndexOrders      .Dispose();//disposeJobHandle);
                    treeUpdate.nodeIndexToNodeOrder         .Dispose();//disposeJobHandle);
                    treeUpdate.rebuildTreeBrushIndexOrders  .Dispose();//disposeJobHandle);
                    treeUpdate.brushBrushIntersections      .Dispose();//disposeJobHandle);
                    treeUpdate.uniqueBrushPairs             .Dispose();//disposeJobHandle);

                    foreach (var item in treeUpdate.intersectionLoopBlobs)
                        if (item.IsCreated) item.Dispose();
                    treeUpdate.intersectionLoopBlobs        .Dispose();//disposeJobHandle);

                    foreach (var item in treeUpdate.intersectingBrushes)
                        if (item.IsCreated) item.Dispose();
                    treeUpdate.intersectingBrushes          .Dispose();//disposeJobHandle);
                }
            }
            Profiler.EndSample();

            //JobsUtility.JobWorkerCount = JobsUtility.JobWorkerMaximumCount;

            return finalJobHandle;
        }



        #region Rebuild / Update
        static void UpdateNodeTransformation(ref NativeHashMap<int, BlobAssetReference<NodeTransformations>> transformations, int nodeIndex)
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

            if (transformations.TryGetValue(nodeIndex, out BlobAssetReference<NodeTransformations> oldTransformations))
            {
                if (oldTransformations.IsCreated)
                    oldTransformations.Dispose();
            }
            transformations[nodeIndex] = NodeTransformations.Build(nodeTransform.nodeToTree, nodeTransform.treeToNode);
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
