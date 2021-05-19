using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using Profiler = UnityEngine.Profiling.Profiler;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;

namespace Chisel.Core
{
    partial class CompactHierarchyManager
    {
        #region Update / Rebuild
        static NativeList<CSGTree>  allTrees;
        static NativeList<CSGTree>  updatedTrees;
        internal static bool UpdateAllTreeMeshes(FinishMeshUpdate finishMeshUpdates, out JobHandle allTrees)
        {
            allTrees = default(JobHandle);
            bool needUpdate = false;

            CompactHierarchyManager.GetAllTrees(CompactHierarchyManager.allTrees);
            // Check if we have a tree that needs updates
            updatedTrees.Clear();
            for (int t = 0; t < CompactHierarchyManager.allTrees.Length; t++)
            {
                var tree = CompactHierarchyManager.allTrees[t];
                if (tree.Valid &&
                    tree.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                {
                    updatedTrees.Add(tree);
                    needUpdate = true;
                }
            }

            if (!needUpdate)
                return false;

            // TODO: update "previous siblings" when something with an intersection operation has been modified

            UnityEngine.Profiling.Profiler.BeginSample("UpdateTreeMeshes");
            allTrees = ScheduleTreeMeshJobs(finishMeshUpdates, updatedTrees);
            UnityEngine.Profiling.Profiler.EndSample();
            return true;
        }
        #endregion

        static unsafe uint GetHash(NativeList<uint> list)
        {
            return math.hash(list.GetUnsafePtr(), sizeof(uint) * list.Length);
        }

        static unsafe uint GetHash(in CompactHierarchy hierarchy, NativeList<CSGTreeBrush> list)
        {
            using (var hashes = new NativeList<uint>(Allocator.Temp))
            {
                for (int i = 0; i < list.Length; i++)
                {
                    var compactNodeID = CompactHierarchyManager.GetCompactNodeID(list[i]);
                    if (!hierarchy.IsValidCompactNodeID(compactNodeID))
                        continue;
                    ref var node = ref hierarchy.GetNodeRef(compactNodeID);
                    hashes.Add(hierarchy.GetHash(in node));
                }
                return GetHash(hashes);
            }
        }


        [BurstCompile]
        private static void UpdateTransformations([ReadOnly] in CompactHierarchy treeHierarchy, [ReadOnly] NativeList<NodeOrderNodeID> transformTreeBrushIndicesList, [WriteOnly] NativeList<NodeTransformations> transformationCache)
        {
            for (int b = 0; b < transformTreeBrushIndicesList.Length; b++)
            {
                var lookup = transformTreeBrushIndicesList[b];
                transformationCache[lookup.nodeOrder] = CompactHierarchyManager.GetNodeTransformation(in treeHierarchy, lookup.compactNodeID);
            }
        }

        [BurstCompile]
        private static void FindModifiedBrushes([ReadOnly] in CompactHierarchy hierarchy, [ReadOnly] in NativeList<CSGTreeBrush> allTreeBrushes, [ReadOnly] in NativeList<IndexOrder> allTreeBrushIndexOrders, [WriteOnly] ref NativeList<NodeOrderNodeID> transformTreeBrushIndicesList, [WriteOnly] ref NativeList<IndexOrder> rebuildTreeBrushIndexOrders)
        {
            using (var usedBrushes = new NativeBitArray(allTreeBrushes.Length, Allocator.Temp, NativeArrayOptions.ClearMemory))
            {
                for (int nodeOrder = 0; nodeOrder < allTreeBrushes.Length; nodeOrder++)
                {
                    var brush = allTreeBrushes[nodeOrder];
                    if (brush.IsAnyStatusFlagSet())
                    {
                        var indexOrder = allTreeBrushIndexOrders[nodeOrder];
                        Debug.Assert(indexOrder.nodeOrder == nodeOrder);
                        if (!usedBrushes.IsSet(nodeOrder))
                        {
                            usedBrushes.Set(nodeOrder, true);
                            rebuildTreeBrushIndexOrders.AddNoResize(indexOrder);
                        }

                        // Fix up all flags

                        if (brush.IsStatusFlagSet(NodeStatusFlags.ShapeModified))
                        {
                            // Need to update the basePolygons for this node
                            brush.ClearStatusFlag(NodeStatusFlags.ShapeModified);
                            brush.SetStatusFlag(NodeStatusFlags.NeedAllTouchingUpdated);
                        }

                        if (brush.IsStatusFlagSet(NodeStatusFlags.HierarchyModified))
                            brush.SetStatusFlag(NodeStatusFlags.NeedAllTouchingUpdated);

                        if (brush.IsStatusFlagSet(NodeStatusFlags.TransformationModified))
                        {
                            var compactNodeID = CompactHierarchyManager.GetCompactNodeID(brush);
                            if (hierarchy.IsValidCompactNodeID(compactNodeID))
                                transformTreeBrushIndicesList.Add(new NodeOrderNodeID { nodeOrder = indexOrder.nodeOrder, compactNodeID = compactNodeID });
                            brush.ClearStatusFlag(NodeStatusFlags.TransformationModified);
                            brush.SetStatusFlag(NodeStatusFlags.NeedAllTouchingUpdated);
                        }
                    }
                }
            }
        }

        static void CheckDependencies(bool runInParallel, JobHandle dependencies)
        {
            if (!runInParallel) dependencies.Complete();
        }

        internal unsafe static JobHandle ScheduleTreeMeshJobs(FinishMeshUpdate finishMeshUpdates, NativeList<CSGTree> trees)
        {
            var finalJobHandle = default(JobHandle);

            // TODO: reorder nodes in backend every time a node is added/removed
            //          this ensures 
            //              everything is sequential in memory
            //              we don't have gaps between nodes
            //              order is always predictable

            // TODO: ensure we only update exactly what we need, and nothing more

            // TODO: figure out exactly what materials/physicMaterials we have per tree
            //          => give each material a unique index per tree.
            //          => cache this material index 
            //          => have a lookup table for material <=> material index
            //       have array of lists for indices, colliderVertices, renderVertices
            //       our number of meshes is now 100% predictable
            //       instead of storing indices, vertices etc. in blobs, store these in these lists, per query
            //       at beginning of frame remove all invalidated pieces of these lists and pack them
            //       when adding new geometry, add them at the end
            //       then figure out if its worth it to keep these lists "in order"

            // TODO: use parameter1Count/parameter2Count for submeshes etc. just pre-allocate blocks for all possible meshes/submeshes

            #region Do the setup for the CSG Jobs (not jobified)
            Profiler.BeginSample("CSG_Prepare");


            Profiler.BeginSample("CSG_GeneratorJobPool");
            var generatorPoolJobHandle = GeneratorJobPoolManager.ScheduleJobs();
            Profiler.EndSample();
            generatorPoolJobHandle.Complete();

            GeneratorJobPoolManager.Clear();


            #region Prepare Trees 
            Profiler.BeginSample("CSG_TreeUpdate_Allocate");
            if (s_TreeUpdates == null || s_TreeUpdates.Length < trees.Length)
                s_TreeUpdates = new TreeUpdate[trees.Length];
            Profiler.EndSample();

            var treeUpdateLength = 0;
            for (int t = 0; t < trees.Length; t++)
            {
                ref var currentTree = ref s_TreeUpdates[treeUpdateLength];
                currentTree.Initialize(trees[t]);
                treeUpdateLength++;
            }
            #endregion

            #region Sort trees from largest (slowest) to smallest (fastest)
            Profiler.BeginSample("Sort");
            // The slowest trees will run first, and the fastest trees can then hopefully fill the gaps
            Array.Sort(s_TreeUpdates, s_TreeSorter);
            Profiler.EndSample();
            #endregion

            Profiler.EndSample();
            #endregion

            const bool runInParallelDefault = true;
            try
            {
                #region CSG Jobs
                Profiler.BeginSample("CSG_Jobs");

                #region Prepare
                // Create lookup table for all brushMeshBlobs, based on the node order in the tree
                Profiler.BeginSample("Job_FillBrushMeshBlobLookup");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.brushMeshBlobsLookupJobHandle,
                                                                          treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.allBrushMeshInstanceIDsJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.surfaceCountRefJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var fillBrushMeshBlobLookupJob = new FillBrushMeshBlobLookupJob
                        {
                            // Read
                            brushMeshBlobs          = ChiselMeshLookup.Value.brushMeshBlobs,
                            allTreeBrushIndexOrders = treeUpdate.allTreeBrushIndexOrders.AsJobArray(runInParallel),
                            allBrushMeshInstanceIDs = treeUpdate.allBrushMeshInstanceIDs,

                            // Write
                            brushMeshLookup         = treeUpdate.brushMeshLookup,
                            surfaceCountRef         = treeUpdate.surfaceCountRef
                        };
                        var currentJobHandle = fillBrushMeshBlobLookupJob.Schedule(runInParallel, dependencies);

                        //treeUpdate.brushMeshBlobsLookupJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushMeshBlobsLookupJobHandle);
                        //treeUpdate.allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        //treeUpdate.allBrushMeshInstanceIDsJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.allBrushMeshInstanceIDsJobHandle);
                        treeUpdate.brushMeshLookupJobHandle             = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.surfaceCountRefJobHandle             = CombineDependencies(currentJobHandle, treeUpdate.surfaceCountRefJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Invalidate outdated caches for all modified brushes
                Profiler.BeginSample("Job_InvalidateBrushCache");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.basePolygonCacheJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.routingTableCacheJobHandle,
                                                                          treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                          treeUpdate.brushRenderBufferCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var invalidateBrushCacheJob = new InvalidateBrushCacheJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders.AsArray().AsReadOnly(),

                            // Read Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            routingTableCache           = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel),
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache     .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = invalidateBrushCacheJob.Schedule(runInParallel, treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                        
                        //treeUpdate.rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        treeUpdate.basePolygonCacheJobHandle                = CombineDependencies(currentJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.routingTableCacheJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.routingTableCacheJobHandle);
                        treeUpdate.brushTreeSpacePlaneCacheJobHandle        = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        treeUpdate.brushRenderBufferCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.brushRenderBufferCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Fix up brush order index in cache data (ordering of brushes may have changed)
                Profiler.BeginSample("Job_FixupBrushCacheIndices");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.nodeIndexToNodeOrderArrayJobHandle,
                                                                          treeUpdate.basePolygonCacheJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var fixupBrushCacheIndicesJob   = new FixupBrushCacheIndicesJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            nodeIDValueToNodeOrderArray     = treeUpdate.nodeIDValueToNodeOrderArray.AsArray().AsReadOnly(),
                            nodeIDValueToNodeOrderOffset    = treeUpdate.nodeIDValueToNodeOrderOffset,

                            // Read Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = fixupBrushCacheIndicesJob.Schedule(runInParallel, treeUpdate.allTreeBrushIndexOrders, 16, dependencies);
                        
                        //treeUpdate.allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        //treeUpdate.nodeIndexToNodeOrderArrayJobHandle = CombineDependencies(currentJobHandle, treeUpdate.nodeIndexToNodeOrderArrayJobHandle);
                        treeUpdate.basePolygonCacheJobHandle            = CombineDependencies(currentJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been modified
                Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.brushTreeSpaceBoundCacheJobHandle,                                                                              
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
                            transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup             = treeUpdate.brushMeshLookup.AsReadOnly(),

                            // Write
                            brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),
                        };
                        var currentJobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                        
                        //treeUpdate.rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        //treeUpdate.transformationCacheJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.transformationCacheJobHandle);
                        //treeUpdate.brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.brushTreeSpaceBoundCacheJobHandle        = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpaceBoundCacheJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Find all pairs of brushes that intersect, for those brushes that have been modified
                Profiler.BeginSample("Job_FindAllBrushIntersectionPairs");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.brushTreeSpaceBoundCacheJobHandle,
                                                                          treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushBrushIntersectionsJobHandle,
                                                                          treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var findAllIntersectionsJob = new FindAllBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            transformationCache             = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup.AsReadOnly(),
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            rebuildTreeBrushIndexOrders     = treeUpdate.rebuildTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            
                            // Read / Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections,
                            
                            // Write
                            brushesThatNeedIndirectUpdateHashMap = treeUpdate.brushesThatNeedIndirectUpdateHashMap.AsParallelWriter()
                        };
                        var currentJobHandle = findAllIntersectionsJob.Schedule(runInParallel, treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);

                        //treeUpdate.allTreeBrushIndexOrdersJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        //treeUpdate.transformationCacheJobHandle                   = CombineDependencies(currentJobHandle, treeUpdate.transformationCacheJobHandle);
                        //treeUpdate.brushMeshLookupJobHandle                       = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        //treeUpdate.brushTreeSpaceBoundCacheJobHandle              = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpaceBoundCacheJobHandle);
                        //treeUpdate.rebuildTreeBrushIndexOrdersJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        treeUpdate.brushBrushIntersectionsJobHandle                 = CombineDependencies(currentJobHandle, treeUpdate.brushBrushIntersectionsJobHandle);
                        treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle    = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Find all brushes that touch the brushes that have been modified
                Profiler.BeginSample("Job_FindUniqueIndirectBrushIntersections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                                          treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var createUniqueIndicesArrayJob = new FindUniqueIndirectBrushIntersectionsJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdateHashMap     = treeUpdate.brushesThatNeedIndirectUpdateHashMap,
                        
                            // Write
                            brushesThatNeedIndirectUpdate            = treeUpdate.brushesThatNeedIndirectUpdate
                        };
                        var currentJobHandle = createUniqueIndicesArrayJob.Schedule(runInParallel, dependencies);
                        
                        //treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle);
                        treeUpdate.brushesThatNeedIndirectUpdateJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Invalidate the cache for the brushes that have been indirectly modified (touch a brush that has changed)
                Profiler.BeginSample("Job_InvalidateBrushCache_Indirect");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                                          treeUpdate.basePolygonCacheJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.routingTableCacheJobHandle,
                                                                          treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                          treeUpdate.brushRenderBufferCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var invalidateBrushCacheJob = new InvalidateIndirectBrushCacheJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate      .AsJobArray(runInParallel),

                            // Read Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            routingTableCache               = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel),
                            brushTreeSpacePlaneCache        = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            brushRenderBufferCache          = chiselLookupValues.brushRenderBufferCache     .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = invalidateBrushCacheJob.Schedule(runInParallel, treeUpdate.brushesThatNeedIndirectUpdate, 16, dependencies);
                        
                        //treeUpdate.brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        treeUpdate.basePolygonCacheJobHandle                = CombineDependencies(currentJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.routingTableCacheJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.routingTableCacheJobHandle);
                        treeUpdate.brushTreeSpacePlaneCacheJobHandle        = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        treeUpdate.brushRenderBufferCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.brushRenderBufferCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been indirectly modified
                Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds_Indirect");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.brushTreeSpaceBoundCacheJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders     = treeUpdate.brushesThatNeedIndirectUpdate      .AsJobArray(runInParallel),
                            transformationCache             = chiselLookupValues.transformationCache        .AsJobArray(runInParallel),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup                    .AsReadOnly(),

                            // Write
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache   .AsJobArray(runInParallel),
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                        };
                        var currentJobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, treeUpdate.brushesThatNeedIndirectUpdate, 16, dependencies);
                        
                        //treeUpdate.brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        //treeUpdate.transformationCacheJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.transformationCacheJobHandle);
                        //treeUpdate.brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.brushTreeSpaceBoundCacheJobHandle        = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpaceBoundCacheJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Find all pairs of brushes that intersect, for those brushes that have been indirectly modified
                Profiler.BeginSample("Job_FindAllBrushIntersectionPairs_Indirect");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.brushTreeSpaceBoundCacheJobHandle,
                                                                          treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                                          treeUpdate.brushBrushIntersectionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var findAllIntersectionsJob = new FindAllIndirectBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            transformationCache             = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup.AsReadOnly(),
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsJobArray(runInParallel),

                            // Read / Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections
                        };
                        var currentJobHandle = findAllIntersectionsJob.Schedule(runInParallel, treeUpdate.brushesThatNeedIndirectUpdate, 1, dependencies);

                        //treeUpdate.allTreeBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        //treeUpdate.transformationCacheJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.transformationCacheJobHandle);
                        //treeUpdate.brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        //treeUpdate.brushTreeSpaceBoundCacheJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpaceBoundCacheJobHandle);
                        //treeUpdate.brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        treeUpdate.brushBrushIntersectionsJobHandle         = CombineDependencies(currentJobHandle, treeUpdate.brushBrushIntersectionsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Add brushes that need to be indirectly updated to our list of brushes that need updates
                Profiler.BeginSample("Job_AddIndirectUpdatedBrushesToListAndSort");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                                          treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var findAllIntersectionsJob = new AddIndirectUpdatedBrushesToListAndSortJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders        .AsArray().AsReadOnly(),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate  .AsJobArray(runInParallel),
                            rebuildTreeBrushIndexOrders     = treeUpdate.rebuildTreeBrushIndexOrders    .AsArray().AsReadOnly(),

                            // Write
                            allUpdateBrushIndexOrders       = treeUpdate.allUpdateBrushIndexOrders      .AsParallelWriter(),
                        };
                        var currentJobHandle = findAllIntersectionsJob.Schedule(runInParallel, dependencies);
                        
                        //treeUpdate.allTreeBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        //treeUpdate.brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        //treeUpdate.rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        treeUpdate.allUpdateBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Gather all found pairs of brushes that intersect with each other and cache them
                Profiler.BeginSample("Job_GatherAndStoreBrushIntersections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        
                        var dependencies            = CombineDependencies(treeUpdate.brushBrushIntersectionsJobHandle,
                                                                          treeUpdate.brushIntersectionsWithJobHandle,
                                                                          treeUpdate.brushIntersectionsWithRangeJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var gatherBrushIntersectionsJob = new GatherBrushIntersectionPairsJob
                        {
                            // Read
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections,

                            // Write
                            brushIntersectionsWith          = treeUpdate.brushIntersectionsWith.GetUnsafeList(),
                            brushIntersectionsWithRange     = treeUpdate.brushIntersectionsWithRange
                        };
                        var currentJobHandle = gatherBrushIntersectionsJob.Schedule(runInParallel, dependencies);
                        
                        //treeUpdate.brushBrushIntersectionsJobHandle     = CombineDependencies(currentJobHandle, treeUpdate.brushBrushIntersectionsJobHandle);
                        treeUpdate.brushIntersectionsWithJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushIntersectionsWithJobHandle);
                        treeUpdate.brushIntersectionsWithRangeJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushIntersectionsWithRangeJobHandle);

                        dependencies                = CombineDependencies(treeUpdate.compactTreeJobHandle,
                                                                          treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushIntersectionsWithJobHandle,
                                                                          treeUpdate.brushIntersectionsWithRangeJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle);                        
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                        {
                            // Read
                            treeCompactNodeID           = treeUpdate.treeCompactNodeID,
                            compactTree                 = treeUpdate.compactTree,
                            allTreeBrushIndexOrders     = treeUpdate.allTreeBrushIndexOrders            .AsJobArray(runInParallel),
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders          .AsJobArray(runInParallel),

                            brushIntersectionsWith      = treeUpdate.brushIntersectionsWith             .AsJobArray(runInParallel),
                            brushIntersectionsWithRange = treeUpdate.brushIntersectionsWithRange        .AsReadOnly(),

                            // Write
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel)
                        };
                        currentJobHandle = storeBrushIntersectionsJob.Schedule(runInParallel, treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        
                        //treeUpdate.compactTreeJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.compactTreeJobHandle);
                        //treeUpdate.allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.brushIntersectionsWithJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushIntersectionsWithJobHandle);
                        treeUpdate.brushIntersectionsWithRangeJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushIntersectionsWithRangeJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                    }
                } finally { Profiler.EndSample(); }
                #endregion

                #region Create wireframes for all new/modified brushes
                Profiler.BeginSample("CSG_DirtyModifiedOutlines");
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;
                    ref var treeHierarchy = ref CompactHierarchyManager.GetHierarchy(treeUpdate.treeCompactNodeID);
                    var updateBrushOutlineJob = new UpdateBrushOutlineJob
                    {
                        // Read
                        allUpdateBrushIndexOrders = treeUpdate.allUpdateBrushIndexOrders,
                        brushMeshBlobs = ChiselMeshLookup.Value.brushMeshBlobs

                        // Write
                        //compactHierarchy          = treeHierarchy,  //<-- cannot do ref or pointer here
                    };
                    updateBrushOutlineJob.InitializeHierarchy(ref treeHierarchy);
                    treeUpdate.updateBrushOutlineJobHandle = updateBrushOutlineJob.Schedule(treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                }
                Profiler.EndSample();
                #endregion

                //
                // Ensure vertices that should be identical on different brushes, ARE actually identical
                //
                /*
                #region Merge vertices
                Profiler.BeginSample("Job_MergeTouchingBrushVertices");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);

                        // ************************
                        // ************************
                        // ************************
                        //
                        // * This job causes plane-vertex alignment problems when called because vertices are snapped to other vertices 
                        //   and aren't on their planes anymore. 
                        // * HOWEVER, this job will remove t-junctions so it is necessary
                        // 
                        // -> find a way to snap vertices further on
                        // -> OR, do all plane distance checks beforehand, and use that information
                        //
                        // ************************
                        // ************************
                        // ************************

                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.tree];
                        // Merges original brush vertices together when they are close to avoid t-junctions
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                        {
                            // Read
                            treeBrushIndexOrders        = treeUpdate.allUpdateBrushIndexOrders.AsJobArray(sequential),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(sequential),

                            // Read Write
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(sequential),
                        };
                        var currentJobHandle = mergeTouchingBrushVerticesJob.Schedule(sequential, treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
                #endregion
                */
                //
                // Determine all surfaces and intersections
                //

                #region Determine Intersection Surfaces
                // Find all pairs of brush intersections for each brush
                Profiler.BeginSample("Job_PrepareBrushPairIntersections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.uniqueBrushPairsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var findBrushPairsJob       = new FindBrushPairsJob
                        {
                            // Read
                            maxOrder                    = treeUpdate.brushCount,
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),

                            // Read (Re-allocate) / Write
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs.GetUnsafeList()
                        };
                        var currentJobHandle = findBrushPairsJob.Schedule(runInParallel, dependencies);

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.uniqueBrushPairsJobHandle             = CombineDependencies(currentJobHandle, treeUpdate.uniqueBrushPairsJobHandle);

                        dependencies                = CombineDependencies(treeUpdate.intersectingBrushesStreamJobHandle,
                                                                          treeUpdate.uniqueBrushPairsJobHandle);

                        currentJobHandle            = NativeStream.ScheduleConstruct(out treeUpdate.intersectingBrushesStream, treeUpdate.uniqueBrushPairs, dependencies, Allocator.TempJob);
                        
                        //treeUpdate.uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                        treeUpdate.intersectingBrushesStreamJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.intersectingBrushesStreamJobHandle);

                        dependencies                = CombineDependencies(treeUpdate.uniqueBrushPairsJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.intersectingBrushesStreamJobHandle);
                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs           .AsJobArray(runInParallel),
                            transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup             = treeUpdate.brushMeshLookup            .AsReadOnly(),

                            // Write
                            intersectingBrushesStream   = treeUpdate.intersectingBrushesStream  .AsWriter()
                        };
                        currentJobHandle = prepareBrushPairIntersectionsJob.Schedule(runInParallel, treeUpdate.uniqueBrushPairs, 1, dependencies);

                        //treeUpdate.uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                        //treeUpdate.transformationCacheJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.transformationCacheJobHandle);
                        //treeUpdate.brushMeshLookupJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.intersectingBrushesStreamJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.intersectingBrushesStreamJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GenerateBasePolygonLoops");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle, 
                                                                          treeUpdate.basePolygonCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders          .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            brushMeshLookup             = treeUpdate.brushMeshLookup                    .AsReadOnly(),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),

                            // Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = createBlobPolygonsBlobs.Schedule(runInParallel, treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        
                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        //treeUpdate.brushMeshLookupJobHandle            = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        //treeUpdate.treeSpaceVerticesCacheJobHandle     = CombineDependencies(currentJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                        treeUpdate.basePolygonCacheJobHandle             = CombineDependencies(currentJobHandle, treeUpdate.basePolygonCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_UpdateBrushTreeSpacePlanes");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this at creation time + when moved / store with brush component itself
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders          .AsJobArray(runInParallel),
                            brushMeshLookup             = treeUpdate.brushMeshLookup                    .AsReadOnly(),
                            transformationCache         = chiselLookupValues.transformationCache        .AsJobArray(runInParallel),

                            // Write
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = createBrushTreeSpacePlanesJob.Schedule(runInParallel, treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        
                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.brushMeshLookupJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        //treeUpdate.transformationCacheJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.transformationCacheJobHandle);
                        treeUpdate.brushTreeSpacePlaneCacheJobHandle    = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_CreateIntersectionLoops");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        
                        var dependencies            = CombineDependencies(treeUpdate.surfaceCountRefJobHandle,
                                                                          treeUpdate.outputSurfacesJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var currentJobHandle        = NativeConstruct.ScheduleSetCapacity(ref treeUpdate.outputSurfaces, treeUpdate.surfaceCountRef, dependencies, Allocator.Persistent);
                        
                        treeUpdate.outputSurfacesJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesJobHandle);

                        dependencies                = CombineDependencies(treeUpdate.uniqueBrushPairsJobHandle,
                                                                          treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                                          treeUpdate.intersectingBrushesStreamJobHandle,
                                                                          treeUpdate.outputSurfaceVerticesJobHandle,
                                                                          treeUpdate.outputSurfacesJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Needed for count (forced & unused)
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs,

                            // Read
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                            intersectingBrushesStream   = treeUpdate.intersectingBrushesStream          .AsReader(),

                            // Write
                            outputSurfaceVertices       = treeUpdate.outputSurfaceVertices              .AsParallelWriterExt(),
                            outputSurfaces              = treeUpdate.outputSurfaces                     .AsParallelWriter()
                        };
                        currentJobHandle = findAllIntersectionLoopsJob.Schedule(runInParallel, treeUpdate.uniqueBrushPairs, 8, dependencies);

                        var disposeJobHandle = treeUpdate.intersectingBrushesStream.Dispose(currentJobHandle);
                        
                        treeUpdate.intersectingBrushesStream = default;

                        //treeUpdate.uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                        //treeUpdate.brushTreeSpacePlaneCacheJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        //treeUpdate.treeSpaceVerticesCacheJobHandle    = CombineDependencies(currentJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                        //treeUpdate.intersectingBrushesStreamJobHandle = CombineDependencies(currentJobHandle, treeUpdate.intersectingBrushesStreamJobHandle);
                        treeUpdate.outputSurfaceVerticesJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.outputSurfaceVerticesJobHandle);
                        treeUpdate.outputSurfacesJobHandle              = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesJobHandle);
                    }
                } finally { Profiler.EndSample(); }
            
                Profiler.BeginSample("Job_GatherOutputSurfaces");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.outputSurfacesJobHandle,
                                                                          treeUpdate.outputSurfacesRangeJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                        {
                            // Read / Write (Sort)
                            outputSurfaces          = treeUpdate.outputSurfaces.AsJobArray(runInParallel),

                            // Write
                            outputSurfacesRange     = treeUpdate.outputSurfacesRange
                        };
                        var currentJobHandle = gatherOutputSurfacesJob.Schedule(runInParallel, dependencies);
                        
                        treeUpdate.outputSurfacesJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesJobHandle);
                        treeUpdate.outputSurfacesRangeJobHandle     = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesRangeJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_FindLoopOverlapIntersections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies            = CombineDependencies(treeUpdate.dataStream1JobHandle,
                                                                          treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var currentJobHandle        = NativeStream.ScheduleConstruct(out treeUpdate.dataStream1, treeUpdate.allUpdateBrushIndexOrders, dependencies, Allocator.TempJob);
                        
                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.dataStream1JobHandle                 = CombineDependencies(currentJobHandle, treeUpdate.dataStream1JobHandle);

                        dependencies                = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.outputSurfaceVerticesJobHandle,
                                                                          treeUpdate.outputSurfacesJobHandle,
                                                                          treeUpdate.outputSurfacesRangeJobHandle,
                                                                          treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                          treeUpdate.basePolygonCacheJobHandle,
                                                                          treeUpdate.loopVerticesLookupJobHandle,
                                                                          treeUpdate.dataStream1JobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders          .AsJobArray(runInParallel),
                            outputSurfaceVertices       = treeUpdate.outputSurfaceVertices              .AsJobArray(runInParallel),
                            outputSurfaces              = treeUpdate.outputSurfaces                     .AsJobArray(runInParallel),
                            outputSurfacesRange         = treeUpdate.outputSurfacesRange                .AsReadOnly(),
                            maxNodeOrder                = treeUpdate.maxNodeOrder,
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            basePolygonCache            = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),

                            // Read Write
                            loopVerticesLookup          = treeUpdate.loopVerticesLookup,
                            
                            // Write
                            output                      = treeUpdate.dataStream1                        .AsWriter()
                        };
                        currentJobHandle = findLoopOverlapIntersectionsJob.Schedule(runInParallel, treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.outputSurfaceVerticesJobHandle     = CombineDependencies(currentJobHandle, treeUpdate.outputSurfaceVerticesJobHandle);
                        //treeUpdate.outputSurfacesJobHandle            = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesJobHandle);
                        //treeUpdate.outputSurfacesRangeJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesRangeJobHandle);
                        //treeUpdate.brushTreeSpacePlaneCacheJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        //treeUpdate.basePolygonCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.loopVerticesLookupJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.loopVerticesLookupJobHandle);
                        treeUpdate.dataStream1JobHandle                 = CombineDependencies(currentJobHandle, treeUpdate.dataStream1JobHandle);
                    }
                } finally { Profiler.EndSample(); }
                #endregion

                //
                // Ensure vertices that should be identical on different brushes, ARE actually identical
                //

                #region Merge vertices
                Profiler.BeginSample("Job_MergeTouchingBrushVerticesIndirect");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only try to merge the vertices beyond the original mesh vertices (the intersection vertices)
                    //       should also try to limit vertices to those that are on the same surfaces (somehow)
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.loopVerticesLookupJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesIndirectJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),

                            // Read Write
                            loopVerticesLookup          = treeUpdate.loopVerticesLookup,
                        };
                        var currentJobHandle = mergeTouchingBrushVerticesJob.Schedule(runInParallel, treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.loopVerticesLookupJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.loopVerticesLookupJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
                #endregion

                //
                // Perform CSG on prepared surfaces, giving each surface a categorization
                //

                #region Perform CSG     
                Profiler.BeginSample("Job_UpdateBrushCategorizationTables");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: only update when brush or any touching brush has been added/removed or changes operation/order
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.compactTreeJobHandle,
                                                                          treeUpdate.routingTableCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        // Build categorization trees for brushes
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var createRoutingTableJob   = new CreateRoutingTableJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders          .AsJobArray(runInParallel),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            compactTree                 = treeUpdate.compactTree,

                            // Write
                            routingTableLookup          = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = createRoutingTableJob.Schedule(runInParallel, treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        //treeUpdate.compactTreeJobHandle                = CombineDependencies(currentJobHandle, treeUpdate.compactTreeJobHandle);
                        treeUpdate.routingTableCacheJobHandle            = CombineDependencies(currentJobHandle, treeUpdate.routingTableCacheJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PerformCSG");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies            = CombineDependencies(treeUpdate.dataStream2JobHandle,
                                                                          treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        CheckDependencies(runInParallel, dependencies);

                        var currentJobHandle        = NativeStream.ScheduleConstruct(out treeUpdate.dataStream2, treeUpdate.allUpdateBrushIndexOrders, dependencies, Allocator.TempJob);
                        
                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.dataStream2JobHandle                 = CombineDependencies(currentJobHandle, treeUpdate.dataStream2JobHandle);

                        dependencies                = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.routingTableCacheJobHandle,
                                                                          treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.dataStream1JobHandle,
                                                                          treeUpdate.loopVerticesLookupJobHandle,
                                                                          treeUpdate.dataStream2JobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        // Perform CSG
                        // TODO: determine when a brush is completely inside another brush
                        //		 (might not have any intersection loops)
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var performCSGJob           = new PerformCSGJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders          .AsJobArray(runInParallel),
                            routingTableCache           = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel),
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            input                       = treeUpdate.dataStream1                        .AsReader(),
                            loopVerticesLookup          = treeUpdate.loopVerticesLookup,

                            // Write
                            output                      = treeUpdate.dataStream2                        .AsWriter(),
                        };
                        currentJobHandle = performCSGJob.Schedule(runInParallel, treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);

                        var disposeJobHandle = treeUpdate.dataStream1.Dispose(currentJobHandle);
                        
                        treeUpdate.dataStream1 = default;

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.routingTableCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.routingTableCacheJobHandle);
                        //treeUpdate.brushTreeSpacePlaneCacheJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        //treeUpdate.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        //treeUpdate.dataStream1JobHandle                = CombineDependencies(currentJobHandle, treeUpdate.dataStream1JobHandle);
                        //treeUpdate.loopVerticesLookupJobHandle         = CombineDependencies(currentJobHandle, treeUpdate.loopVerticesLookupJobHandle);
                        treeUpdate.dataStream2JobHandle                  = CombineDependencies(currentJobHandle, treeUpdate.dataStream2JobHandle);
                    }
                } finally { Profiler.EndSample(); }
                #endregion

                //
                // Triangulate the surfaces
                //

                #region Triangulate Surfaces
                Profiler.BeginSample("Job_GenerateSurfaceTriangles");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.meshQueriesJobHandle,
                                                                          treeUpdate.basePolygonCacheJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.dataStream2JobHandle,
                                                                          treeUpdate.brushRenderBufferCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        // TODO: Potentially merge this with PerformCSGJob?
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];
                        var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders      .AsJobArray(runInParallel),
                            basePolygonCache            = chiselLookupValues.basePolygonCache       .AsJobArray(runInParallel),
                            transformationCache         = chiselLookupValues.transformationCache    .AsJobArray(runInParallel),
                            input                       = treeUpdate.dataStream2                    .AsReader(),
                            meshQueries                 = treeUpdate.meshQueries                    .AsReadOnly(),

                            // Write
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = generateSurfaceRenderBuffers.Schedule(runInParallel, treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);
                        var disposeJobHandle = treeUpdate.dataStream2.Dispose(currentJobHandle);

                        treeUpdate.dataStream2 = default;

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.basePolygonCacheJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        //treeUpdate.transformationCacheJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.transformationCacheJobHandle);
                        //treeUpdate.dataStream2JobHandle               = CombineDependencies(currentJobHandle, treeUpdate.dataStream2JobHandle);
                        treeUpdate.brushRenderBufferCacheJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushRenderBufferCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
                
                // Schedule all the jobs
                JobHandle.ScheduleBatchedJobs();
                #endregion

                //
                // Create meshes out of ALL the generated and cached surfaces
                //

                #region Create Meshes
                Profiler.BeginSample("Mesh.AllocateWritableMeshData");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var meshAllocations = 0;
                        for (int m = 0; m < treeUpdate.meshQueries.Length; m++)
                        {
                            var meshQuery = treeUpdate.meshQueries[m];
                            var surfaceParameterIndex = (meshQuery.LayerParameterIndex >= LayerParameterIndex.LayerParameter1 &&
                                                         meshQuery.LayerParameterIndex <= LayerParameterIndex.MaxLayerParameterIndex) ?
                                                         (int)meshQuery.LayerParameterIndex : 0;

                            // Query uses Material
                            if ((meshQuery.LayerQuery & LayerUsageFlags.Renderable) != 0 && surfaceParameterIndex == 1)
                            {
                                // Each Material is stored as a submesh in the same mesh
                                meshAllocations += 1;  
                            }
                            // Query uses PhysicMaterial
                            else if ((meshQuery.LayerQuery & LayerUsageFlags.Collidable) != 0 && surfaceParameterIndex == 2)
                            {
                                // Each PhysicMaterial is stored in its own separate mesh
                                meshAllocations += treeUpdate.parameter2Count; 
                            } else
                                meshAllocations++;
                        }

                        treeUpdate.meshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(meshAllocations);

                        for (int i = 0; i < meshAllocations; i++)
                            treeUpdate.meshDatas.Add(treeUpdate.meshDataArray[i]);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_FindBrushRenderBuffers");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.meshQueriesJobHandle,
                                                                          treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushRenderBufferCacheJobHandle,
                                                                          treeUpdate.brushRenderDataJobHandle,
                                                                          treeUpdate.subMeshSurfacesJobHandle,
                                                                          treeUpdate.subMeshCountsJobHandle,
                                                                          treeUpdate.vertexBufferContents_subMeshSectionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.tree];                        
                        dependencies                = ChiselNativeListExtensions.ScheduleEnsureCapacity(treeUpdate.brushRenderData, treeUpdate.allTreeBrushIndexOrders, dependencies);
                        var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                        {
                            // Read
                            meshQueryLength         = treeUpdate.meshQueriesLength,
                            allTreeBrushIndexOrders = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            brushRenderBufferCache  = chiselLookupValues.brushRenderBufferCache.AsJobArray(runInParallel),

                            // Write
                            brushRenderData         = treeUpdate.brushRenderData,
                            subMeshCounts           = treeUpdate.subMeshCounts,
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections,
                        };
                        var currentJobHandle = findBrushRenderBuffersJob.Schedule(runInParallel, dependencies);

                        //treeUpdate.meshQueriesJobHandle                           = CombineDependencies(currentJobHandle, treeUpdate.meshQueriesJobHandle);
                        //treeUpdate.allTreeBrushIndexOrdersJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        //reeUpdate.brushRenderBufferCacheJobHandle                 = CombineDependencies(currentJobHandle, treeUpdate.brushRenderBufferCacheJobHandle);
                        treeUpdate.brushRenderDataJobHandle                         = CombineDependencies(currentJobHandle, treeUpdate.brushRenderDataJobHandle);
                        treeUpdate.subMeshSurfacesJobHandle                         = CombineDependencies(currentJobHandle, treeUpdate.subMeshSurfacesJobHandle);
                        treeUpdate.subMeshCountsJobHandle                           = CombineDependencies(currentJobHandle, treeUpdate.subMeshCountsJobHandle);
                        treeUpdate.vertexBufferContents_subMeshSectionsJobHandle    = CombineDependencies(currentJobHandle, treeUpdate.vertexBufferContents_subMeshSectionsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PrepareSubSections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.meshQueriesJobHandle,
                                                                          treeUpdate.brushRenderDataJobHandle,
                                                                          treeUpdate.sectionsJobHandle,
                                                                          treeUpdate.subMeshSurfacesJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var prepareJob = new PrepareSubSectionsJob
                        {
                            // Read
                            meshQueries         = treeUpdate.meshQueries.AsReadOnly(),
                            brushRenderData     = treeUpdate.brushRenderData.AsJobArray(runInParallel),

                            // Write
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces,
                        };
                        var currentJobHandle    = prepareJob.Schedule(runInParallel, treeUpdate.meshQueriesLength, 1, dependencies);

                        //treeUpdate.meshQueriesJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.meshQueriesJobHandle);
                        //treeUpdate.brushRenderDataJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.brushRenderDataJobHandle);
                        treeUpdate.sectionsJobHandle            = CombineDependencies(currentJobHandle, treeUpdate.sectionsJobHandle);
                        treeUpdate.subMeshSurfacesJobHandle     = CombineDependencies(currentJobHandle, treeUpdate.subMeshSurfacesJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_SortSurfaces");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        
                        var dependencies = CombineDependencies(treeUpdate.sectionsJobHandle,
                                                               treeUpdate.subMeshSurfacesJobHandle,
                                                               treeUpdate.subMeshCountsJobHandle,
                                                               treeUpdate.vertexBufferContents_subMeshSectionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var parallelSortJob = new SortSurfacesParallelJob
                        {
                            // Read
                            meshQueries      = treeUpdate.meshQueries.AsReadOnly(),
                            subMeshSurfaces  = treeUpdate.subMeshSurfaces,

                            // Write
                            subMeshCounts    = treeUpdate.subMeshCounts
                        };
                        var currentJobHandle = parallelSortJob.Schedule(runInParallel, dependencies);

                        //treeUpdate.sectionsJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.sectionsJobHandle);
                        //treeUpdate.subMeshSurfacesJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.subMeshSurfacesJobHandle);
                        treeUpdate.subMeshCountsJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.subMeshCountsJobHandle);


                        dependencies = CombineDependencies(treeUpdate.sectionsJobHandle,
                                                           treeUpdate.subMeshSurfacesJobHandle,
                                                           treeUpdate.subMeshCountsJobHandle,
                                                           treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                           currentJobHandle);

                        var sortJobGather = new GatherSurfacesJob
                        {
                            // Read / Write
                            subMeshCounts   = treeUpdate.subMeshCounts,

                            // Write
                            subMeshSections = treeUpdate.vertexBufferContents.subMeshSections,
                        };
                        currentJobHandle = sortJobGather.Schedule(runInParallel, dependencies);

                        treeUpdate.subMeshCountsJobHandle                           = CombineDependencies(currentJobHandle, treeUpdate.subMeshCountsJobHandle);
                        treeUpdate.vertexBufferContents_subMeshSectionsJobHandle    = CombineDependencies(currentJobHandle, treeUpdate.vertexBufferContents_subMeshSectionsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_AllocateVertexBuffers");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                                          treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                        {
                            // Read
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),

                            // Read Write
                            triangleBrushIndices    = treeUpdate.vertexBufferContents.triangleBrushIndices
                        };
                        var currentJobHandle = allocateVertexBuffersJob.Schedule(runInParallel, dependencies);

                        treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GenerateMeshDescription");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.subMeshCountsJobHandle,
                                                                          treeUpdate.vertexBufferContents_meshDescriptionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                        {
                            // Read
                            subMeshCounts       = treeUpdate.subMeshCounts.AsJobArray(runInParallel),

                            // Read Write
                            meshDescriptions    = treeUpdate.vertexBufferContents.meshDescriptions
                        };
                        var currentJobHandle = generateMeshDescriptionJob.Schedule(runInParallel, dependencies);

                        treeUpdate.vertexBufferContents_meshDescriptionsJobHandle = CombineDependencies(currentJobHandle, treeUpdate.vertexBufferContents_meshDescriptionsJobHandle);
                        treeUpdate.subMeshCountsJobHandle = CombineDependencies(currentJobHandle, treeUpdate.subMeshCountsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_CopyToMeshes");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        JobHandle dependencies;
                        { 
                            dependencies = CombineDependencies(treeUpdate.vertexBufferContents_meshDescriptionsJobHandle,
                                                               treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                               treeUpdate.meshDatasJobHandle,
                                                               treeUpdate.vertexBufferContents_meshesJobHandle,
                                                               treeUpdate.colliderMeshUpdatesJobHandle,
                                                               treeUpdate.debugHelperMeshesJobHandle,
                                                               treeUpdate.renderMeshesJobHandle);
                            var assignMeshesJob = new AssignMeshesJob
                            {
                                // Read
                                meshDescriptions    = treeUpdate.vertexBufferContents.meshDescriptions,
                                subMeshSections     = treeUpdate.vertexBufferContents.subMeshSections,
                                meshDatas           = treeUpdate.meshDatas,

                                // Write
                                meshes              = treeUpdate.vertexBufferContents.meshes,
                                debugHelperMeshes   = treeUpdate.debugHelperMeshes,
                                renderMeshes        = treeUpdate.renderMeshes,

                                // Read / Write
                                colliderMeshUpdates = treeUpdate.colliderMeshUpdates,
                            };
                            var currentJobHandle = assignMeshesJob.Schedule(runInParallel, dependencies);

                            treeUpdate.vertexBufferContents_meshesJobHandle = CombineDependencies(currentJobHandle, treeUpdate.vertexBufferContents_meshesJobHandle);
                            treeUpdate.debugHelperMeshesJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.debugHelperMeshesJobHandle);
                            treeUpdate.renderMeshesJobHandle                = CombineDependencies(currentJobHandle, treeUpdate.renderMeshesJobHandle);
                            treeUpdate.colliderMeshUpdatesJobHandle         = CombineDependencies(currentJobHandle, treeUpdate.colliderMeshUpdatesJobHandle);
                        }

                        dependencies = CombineDependencies(treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                           treeUpdate.subMeshCountsJobHandle,
                                                           treeUpdate.subMeshSurfacesJobHandle,                                                            
                                                           treeUpdate.vertexBufferContents_renderDescriptorsJobHandle,
                                                           treeUpdate.renderMeshesJobHandle,
                                                           treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                           treeUpdate.vertexBufferContents_meshesJobHandle);
                        var renderCopyToMeshJob = new CopyToRenderMeshJob
                        {
                            // Read
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts           = treeUpdate.subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            renderDescriptors       = treeUpdate.vertexBufferContents.renderDescriptors,
                            renderMeshes            = treeUpdate.renderMeshes,

                            // Read/Write
                            triangleBrushIndices    = treeUpdate.vertexBufferContents.triangleBrushIndices,
                            meshes                  = treeUpdate.vertexBufferContents.meshes,
                        };
                        var renderMeshJobHandle = renderCopyToMeshJob.Schedule(runInParallel, treeUpdate.renderMeshes, 1, dependencies);

                        dependencies = CombineDependencies(treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                           treeUpdate.subMeshCountsJobHandle,
                                                           treeUpdate.subMeshSurfacesJobHandle,                                                           
                                                           treeUpdate.vertexBufferContents_renderDescriptorsJobHandle,
                                                           treeUpdate.debugHelperMeshesJobHandle,
                                                           treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                           treeUpdate.vertexBufferContents_meshesJobHandle);
                        var helperCopyToMeshJob = new CopyToRenderMeshJob
                        {
                            // Read
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts           = treeUpdate.subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            renderDescriptors       = treeUpdate.vertexBufferContents.renderDescriptors,
                            renderMeshes            = treeUpdate.debugHelperMeshes,

                            // Read/Write
                            triangleBrushIndices    = treeUpdate.vertexBufferContents.triangleBrushIndices,
                            meshes                  = treeUpdate.vertexBufferContents.meshes,
                        };
                        var helperMeshJobHandle = helperCopyToMeshJob.Schedule(runInParallel, treeUpdate.debugHelperMeshes, 1, dependencies);

                        dependencies = CombineDependencies(treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                           treeUpdate.subMeshCountsJobHandle,
                                                           treeUpdate.subMeshSurfacesJobHandle,

                                                           treeUpdate.vertexBufferContents_colliderDescriptorsJobHandle,
                                                           treeUpdate.colliderMeshUpdatesJobHandle,
                                                           treeUpdate.vertexBufferContents_meshesJobHandle);
                        var colliderCopyToMeshJob = new CopyToColliderMeshJob
                        {
                            // Read
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts           = treeUpdate.subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            colliderDescriptors     = treeUpdate.vertexBufferContents.colliderDescriptors,
                            colliderMeshes          = treeUpdate.colliderMeshUpdates,
                            
                            // Read/Write
                            meshes                  = treeUpdate.vertexBufferContents.meshes,
                        };
                        var colliderMeshJobHandle = colliderCopyToMeshJob.Schedule(runInParallel, treeUpdate.colliderMeshUpdates, 16, dependencies);


                        treeUpdate.subMeshCountsJobHandle = CombineDependencies(renderMeshJobHandle, helperMeshJobHandle, colliderMeshJobHandle, treeUpdate.subMeshCountsJobHandle);

                        treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle = CombineDependencies(renderMeshJobHandle, treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle);
                        treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle = CombineDependencies(helperMeshJobHandle, treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle);
                        
                        treeUpdate.vertexBufferContents_meshesJobHandle = CombineDependencies(renderMeshJobHandle, treeUpdate.vertexBufferContents_meshesJobHandle);
                        treeUpdate.vertexBufferContents_meshesJobHandle = CombineDependencies(helperMeshJobHandle, treeUpdate.vertexBufferContents_meshesJobHandle);
                        treeUpdate.vertexBufferContents_meshesJobHandle = CombineDependencies(colliderMeshJobHandle, treeUpdate.vertexBufferContents_meshesJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
                #endregion

                //
                // Finally store the generated surfaces into our cache
                //

                #region Store cached values back into cache (by node Index)
                Profiler.BeginSample("CSG_StoreToCache");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate          = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var chiselLookupValues              = ChiselTreeLookup.Value[treeUpdate.tree];
                    
                        var dependencies = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                               treeUpdate.brushTreeSpaceBoundCacheJobHandle,
                                                               treeUpdate.brushRenderBufferCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var storeToCacheJob = new StoreToCacheJob
                        {
                            // Read
                            allTreeBrushIndexOrders     = treeUpdate.allTreeBrushIndexOrders.AsJobArray(runInParallel),
                            brushTreeSpaceBoundCache    = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache.AsJobArray(runInParallel),

                            // Read Write
                            brushTreeSpaceBoundLookup   = chiselLookupValues.brushTreeSpaceBoundLookup,
                            brushRenderBufferLookup     = chiselLookupValues.brushRenderBufferLookup
                        };
                        var currentJobHandle = storeToCacheJob.Schedule(runInParallel, dependencies);
                        treeUpdate.storeToCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.storeToCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
                #endregion

                #region Free all temporaries
                // TODO: most of these disposes can be scheduled before we complete and write to the meshes, 
                // so that these disposes can happen at the same time as the mesh updates in finishMeshUpdates
                Profiler.BeginSample("CSG_Deallocate");
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];

                        // Combine all JobHandles of all jobs to ensure that we wait for ALL of them to finish 
                        // before we dispose of our temporaries.
                        // Eventually we might want to put this in between other jobs, but for now this is safer
                        // to work with while things are still being re-arranged.
                        treeUpdate.preMeshUpdateCombinedJobHandle = CombineDependencies(
                                                    CombineDependencies(
                                                        treeUpdate.allBrushMeshInstanceIDsJobHandle,
                                                        treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                        treeUpdate.basePolygonCacheJobHandle,
                                                        treeUpdate.brushBrushIntersectionsJobHandle,
                                                        treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                        treeUpdate.brushRenderBufferCacheJobHandle,
                                                        treeUpdate.brushRenderDataJobHandle),
                                                    CombineDependencies(
                                                        treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                        treeUpdate.brushMeshBlobsLookupJobHandle,
                                                        treeUpdate.brushMeshLookupJobHandle,
                                                        treeUpdate.brushIntersectionsWithJobHandle,
                                                        treeUpdate.brushIntersectionsWithRangeJobHandle,
                                                        treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                        treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                        treeUpdate.brushTreeSpaceBoundCacheJobHandle,
                                                        treeUpdate.compactTreeJobHandle),
                                                    CombineDependencies(
                                                        treeUpdate.dataStream1JobHandle,
                                                        treeUpdate.dataStream2JobHandle,
                                                        treeUpdate.intersectingBrushesStreamJobHandle,
                                                        treeUpdate.loopVerticesLookupJobHandle,
                                                        treeUpdate.meshQueriesJobHandle,
                                                        treeUpdate.nodeIndexToNodeOrderArrayJobHandle,
                                                        treeUpdate.outputSurfaceVerticesJobHandle,
                                                        treeUpdate.outputSurfacesJobHandle,
                                                        treeUpdate.outputSurfacesRangeJobHandle),
                                                    CombineDependencies(
                                                        treeUpdate.routingTableCacheJobHandle,
                                                        treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                        treeUpdate.sectionsJobHandle,
                                                        treeUpdate.subMeshSurfacesJobHandle,
                                                        treeUpdate.subMeshCountsJobHandle,
                                                        treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                        treeUpdate.transformationCacheJobHandle,
                                                        treeUpdate.uniqueBrushPairsJobHandle),
                                                    CombineDependencies(
                                                        treeUpdate.updateBrushOutlineJobHandle,
                                                        treeUpdate.storeToCacheJobHandle)
                                                );
                        treeUpdate.preMeshUpdateCombinedJobHandle.Complete();
                        treeUpdate.PreMeshUpdateDispose(treeUpdate.preMeshUpdateCombinedJobHandle);
                    }
                }
                Profiler.EndSample();
                #endregion


                // Make sure we start the already scheduled jobs on the worker threads
                JobHandle.ScheduleBatchedJobs();

                //
                // Do some main thread work that we need to do anyway, while the previously scheduled jobs are running
                //

                #region Reset Flags (not jobified)
                Profiler.BeginSample("Reset_Flags");
                // Reset the flags before the dispose of these containers are scheduled
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;

                    for (int b = 0; b < treeUpdate.brushCount; b++)
                    { 
                        var brushIndexOrder = treeUpdate.allTreeBrushIndexOrders[b];
                        var brush           = CSGTreeBrush.Find(brushIndexOrder.compactNodeID);
                        brush.ClearAllStatusFlags();
                    }

                    var tree = treeUpdate.tree;
                    tree.ClearStatusFlag(NodeStatusFlags.TreeNeedsUpdate);
                    tree.SetStatusFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                }
                for (int t = 0; t < trees.Length; t++)
                {
                    var tree = trees[t];
                    tree.ClearStatusFlag(NodeStatusFlags.TreeNeedsUpdate);
                    tree.SetStatusFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                }
                Profiler.EndSample();
                #endregion

                using (var s_ModifiedTreeIndices = new NativeList<int>(treeUpdateLength, Allocator.Temp))
                {
                    #region Update Flags (not jobified)
                    Profiler.BeginSample("UpdateTreeFlags");
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];

                        var tree = treeUpdate.tree;
                        if (!tree.Valid ||
                            !tree.IsStatusFlagSet(NodeStatusFlags.TreeMeshNeedsUpdate))
                        {
                            treeUpdate.updateCount = 0;
                            continue;
                        }

                        bool wasDirty = tree.Dirty;

                        tree.ClearStatusFlag(NodeStatusFlags.NeedCSGUpdate);

                        if (treeUpdate.updateCount == 0 &&
                            treeUpdate.brushCount > 0)
                        {
                            treeUpdate.updateCount = 0;
                            treeUpdate.brushCount = 0;
                            continue;
                        }

                        // Don't update the mesh if the tree hasn't actually been modified
                        if (!wasDirty)
                        {
                            treeUpdate.updateCount = 0;
                            continue;
                        }

                        // Skip invalid trees since they won't work anyway
                        if (!tree.Valid)
                            continue;

                        s_ModifiedTreeIndices.Add(t);
                    }
                    Profiler.EndSample();
                    #endregion
                    
                    //
                    // Wait for our scheduled mesh update jobs to finish, ensure our components are setup correctly, and upload our mesh data to the meshes
                    //

                    #region Finish Mesh Updates / Update Components (not jobified)
                    Profiler.BeginSample("FinishMeshUpdates");
                    try
                    {
                        for (int t = 0; t < s_ModifiedTreeIndices.Length; t++)
                        {
                            ref var treeUpdate = ref s_TreeUpdates[s_ModifiedTreeIndices[t]];
                            if (treeUpdate.updateCount == 0)
                                continue;

                            var dependencies = CombineDependencies(treeUpdate.meshDatasJobHandle,
                                                                   treeUpdate.colliderMeshUpdatesJobHandle,
                                                                   treeUpdate.debugHelperMeshesJobHandle,
                                                                   treeUpdate.renderMeshesJobHandle,
                                                                   treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                                   treeUpdate.vertexBufferContents_meshesJobHandle);
                            if (finishMeshUpdates != null)
                            {
                                var tree = treeUpdate.tree;
                                var usedMeshCount = finishMeshUpdates(tree, ref treeUpdate.vertexBufferContents,
                                                                      treeUpdate.meshDataArray,
                                                                      treeUpdate.colliderMeshUpdates,
                                                                      treeUpdate.debugHelperMeshes,
                                                                      treeUpdate.renderMeshes,
                                                                      dependencies);
                            }
                            dependencies.Complete(); // Whatever happens, our jobs need to be completed at this point
                        }
                    }
                    finally { Profiler.EndSample(); }
                    #endregion
                }

                Profiler.EndSample();
                #endregion
            }
            finally
            {
                #region Free all temporaries
                // TODO: most of these disposes can be scheduled before we complete and write to the meshes, 
                // so that these disposes can happen at the same time as the mesh updates in finishMeshUpdates
                Profiler.BeginSample("CSG_Deallocate");
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];

                        // Combine all JobHandles of all jobs to ensure that we wait for ALL of them to finish 
                        // before we dispose of our temporaries.
                        // Eventually we might want to put this in between other jobs, but for now this is safer
                        // to work with while things are still being re-arranged.
                        var dependencies = CombineDependencies(
                                                        CombineDependencies(
                                                            treeUpdate.allBrushMeshInstanceIDsJobHandle,
                                                            treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                            treeUpdate.basePolygonCacheJobHandle,
                                                            treeUpdate.brushBrushIntersectionsJobHandle,
                                                            treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                            treeUpdate.brushRenderBufferCacheJobHandle,
                                                            treeUpdate.brushRenderDataJobHandle,
                                                            treeUpdate.brushTreeSpacePlaneCacheJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.brushMeshBlobsLookupJobHandle,
                                                            treeUpdate.brushMeshLookupJobHandle,
                                                            treeUpdate.brushIntersectionsWithJobHandle,
                                                            treeUpdate.brushIntersectionsWithRangeJobHandle,
                                                            treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                            treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                            treeUpdate.brushTreeSpaceBoundCacheJobHandle,
                                                            treeUpdate.compactTreeJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.dataStream1JobHandle,
                                                            treeUpdate.dataStream2JobHandle,
                                                            treeUpdate.intersectingBrushesStreamJobHandle,
                                                            treeUpdate.loopVerticesLookupJobHandle,
                                                            treeUpdate.meshQueriesJobHandle,
                                                            treeUpdate.nodeIndexToNodeOrderArrayJobHandle,
                                                            treeUpdate.outputSurfaceVerticesJobHandle,
                                                            treeUpdate.outputSurfacesJobHandle,
                                                            treeUpdate.outputSurfacesRangeJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.routingTableCacheJobHandle,
                                                            treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                            treeUpdate.sectionsJobHandle,
                                                            treeUpdate.subMeshSurfacesJobHandle,
                                                            treeUpdate.subMeshCountsJobHandle,
                                                            treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                            treeUpdate.transformationCacheJobHandle,
                                                            treeUpdate.uniqueBrushPairsJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.updateBrushOutlineJobHandle,
                                                            treeUpdate.storeToCacheJobHandle,

                                                            treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                            treeUpdate.colliderMeshUpdatesJobHandle,
                                                            treeUpdate.debugHelperMeshesJobHandle,
                                                            treeUpdate.renderMeshesJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.vertexBufferContents_renderDescriptorsJobHandle,
                                                            treeUpdate.vertexBufferContents_colliderDescriptorsJobHandle,
                                                            treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                            treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                            treeUpdate.vertexBufferContents_meshDescriptionsJobHandle,
                                                            treeUpdate.vertexBufferContents_meshesJobHandle,
                                                            treeUpdate.meshDatasJobHandle,
                                                            treeUpdate.surfaceCountRefJobHandle)
                                                );
                        dependencies.Complete();
                        treeUpdate.Dispose(dependencies);

                        // We let the final JobHandle dependend on the dependencies, but not on the disposal, 
                        // because we do not need to wait for the disposal of native collections do use our generated data
                        finalJobHandle = CombineDependencies(finalJobHandle, dependencies);
                    }
                }
                Profiler.EndSample();
                #endregion
            }

            return finalJobHandle;
        }

        internal unsafe struct TreeUpdate
        {
            public CSGTree                      tree;
            public CompactNodeID                treeCompactNodeID;
            public int                          brushCount;
            public int                          updateCount;
            public int                          maxNodeOrder;
            public int                          parameter1Count;
            public int                          parameter2Count;
            
            public NativeList<NodeOrderNodeID>  transformTreeBrushIndicesList;
            public NativeList<CSGTreeBrush>     brushes;
            public NativeList<CompactNodeID>    nodes;

            #region Meshes
            public UnityEngine.Mesh.MeshDataArray           meshDataArray;
            public NativeList<UnityEngine.Mesh.MeshData>    meshDatas;
            #endregion

            #region All Native Collection Temporaries
            public NativeList<IndexOrder>               allTreeBrushIndexOrders;
            public NativeList<IndexOrder>               rebuildTreeBrushIndexOrders;
            public NativeList<IndexOrder>               allUpdateBrushIndexOrders;
            public NativeArray<int>                     allBrushMeshInstanceIDs;
            
            public BlobAssetReference<CompactTree>      compactTree;
            public NativeArray<MeshQuery>               meshQueries;
            public int                                  meshQueriesLength;

            public NativeListArray<BrushIntersectWith>  brushBrushIntersections;
            public NativeList<BrushIntersectWith>       brushIntersectionsWith;
            public NativeArray<int2>                    brushIntersectionsWithRange;
            public NativeList<IndexOrder>               brushesThatNeedIndirectUpdate;
            public NativeHashSet<IndexOrder>            brushesThatNeedIndirectUpdateHashMap;

            public NativeList<BrushPair2>               uniqueBrushPairs;

            public NativeList<float3>                   outputSurfaceVertices;
            public NativeList<BrushIntersectionLoop>    outputSurfaces;
            public NativeArray<int2>                    outputSurfacesRange;

            public NativeArray<BlobAssetReference<BrushMeshBlob>>   brushMeshLookup;

            public NativeListArray<float3>              loopVerticesLookup;

            public NativeReference<int>                 surfaceCountRef;

            public VertexBufferContents                 vertexBufferContents;

            public NativeList<int>                      nodeIDValueToNodeOrderArray;
            public int                                  nodeIDValueToNodeOrderOffset;

            public NativeList<BrushData>                brushRenderData;
            public NativeList<SubMeshCounts>            subMeshCounts;
            public NativeListArray<SubMeshSurface>      subMeshSurfaces;

            public NativeStream                         dataStream1;
            public NativeStream                         dataStream2;
            public NativeStream                         intersectingBrushesStream;

            
            public NativeList<ChiselMeshUpdate>         colliderMeshUpdates;
            public NativeList<ChiselMeshUpdate>         debugHelperMeshes;
            public NativeList<ChiselMeshUpdate>         renderMeshes;
            #endregion

            #region In Between JobHandles

            internal JobHandle allBrushMeshInstanceIDsJobHandle;
            internal JobHandle allTreeBrushIndexOrdersJobHandle;
            internal JobHandle allUpdateBrushIndexOrdersJobHandle;

            internal JobHandle basePolygonCacheJobHandle;
            internal JobHandle brushBrushIntersectionsJobHandle;
            internal JobHandle brushesTouchedByBrushCacheJobHandle;
            internal JobHandle brushRenderBufferCacheJobHandle;
            internal JobHandle brushRenderDataJobHandle;
            internal JobHandle brushTreeSpacePlaneCacheJobHandle;
            internal JobHandle brushMeshBlobsLookupJobHandle;
            internal JobHandle brushMeshLookupJobHandle;
            internal JobHandle brushIntersectionsWithJobHandle;
            internal JobHandle brushIntersectionsWithRangeJobHandle;
            internal JobHandle brushesThatNeedIndirectUpdateHashMapJobHandle;
            internal JobHandle brushesThatNeedIndirectUpdateJobHandle;
            internal JobHandle brushTreeSpaceBoundCacheJobHandle;

            internal JobHandle compactTreeJobHandle;

            internal JobHandle dataStream1JobHandle;
            internal JobHandle dataStream2JobHandle;

            internal JobHandle intersectingBrushesStreamJobHandle;

            internal JobHandle loopVerticesLookupJobHandle;

            internal JobHandle meshQueriesJobHandle;

            internal JobHandle nodeIndexToNodeOrderArrayJobHandle;
            
            internal JobHandle outputSurfaceVerticesJobHandle;
            internal JobHandle outputSurfacesJobHandle;
            internal JobHandle outputSurfacesRangeJobHandle;

            internal JobHandle routingTableCacheJobHandle;
            internal JobHandle rebuildTreeBrushIndexOrdersJobHandle;

            internal JobHandle sectionsJobHandle;
            internal JobHandle surfaceCountRefJobHandle;
            internal JobHandle subMeshSurfacesJobHandle;
            internal JobHandle subMeshCountsJobHandle;

            internal JobHandle treeSpaceVerticesCacheJobHandle;
            internal JobHandle transformationCacheJobHandle;

            internal JobHandle uniqueBrushPairsJobHandle;
            
            internal JobHandle vertexBufferContents_renderDescriptorsJobHandle;
            internal JobHandle vertexBufferContents_colliderDescriptorsJobHandle;
            internal JobHandle vertexBufferContents_subMeshSectionsJobHandle;
            internal JobHandle vertexBufferContents_meshesJobHandle;
            internal JobHandle colliderMeshUpdatesJobHandle;
            internal JobHandle debugHelperMeshesJobHandle;
            internal JobHandle renderMeshesJobHandle;

            internal JobHandle vertexBufferContents_triangleBrushIndicesJobHandle;
            internal JobHandle vertexBufferContents_meshDescriptionsJobHandle;

            internal JobHandle meshDatasJobHandle;
            internal JobHandle storeToCacheJobHandle;

            internal JobHandle updateBrushOutlineJobHandle;
            internal JobHandle preMeshUpdateCombinedJobHandle;
            #endregion

            public JobHandle lastJobHandle;

            // TODO: We're not reusing buffers, so clear is useless?
            //       If we ARE reusing buffers, some allocations are not set to a brush size??
            public void Clear()
            {
                Profiler.BeginSample("HASMAP_CLEAR");
                brushesThatNeedIndirectUpdateHashMap.Clear();
                Profiler.EndSample();

                Profiler.BeginSample("LISTARRAY_CLEAR");
                loopVerticesLookup.ClearChildren();
                Profiler.EndSample();

                Profiler.BeginSample("LIST_CLEAR");
                brushBrushIntersections         .Clear();
                brushIntersectionsWith          .Clear();
                brushesThatNeedIndirectUpdate   .Clear();
                outputSurfaceVertices           .Clear();
                outputSurfaces                  .Clear();
                uniqueBrushPairs                .Clear();
                rebuildTreeBrushIndexOrders     .Clear();
                allUpdateBrushIndexOrders       .Clear();
                allBrushMeshInstanceIDs         .ClearValues();

                brushRenderData                 .Clear();
                subMeshCounts                   .Clear();
                subMeshSurfaces                 .Clear();

                colliderMeshUpdates             .Clear();
                debugHelperMeshes               .Clear();
                renderMeshes                    .Clear();
                Profiler.EndSample();

                vertexBufferContents.Clear();
                
                brushMeshLookup.ClearStruct();

                brushIntersectionsWithRange     .ClearValues();
                outputSurfacesRange             .ClearValues();

                nodeIDValueToNodeOrderArray.Clear();

                allTreeBrushIndexOrders.Clear();
                allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);

                surfaceCountRef.Value = default;
                //outputSurfaceLoopsRef.Value = default;

                loopVerticesLookup.ResizeExact(brushCount);

                meshDatas.Clear();
            }

            // TODO: We're not reusing buffers, so clear is useless?
            //       If we ARE reusing buffers, some allocations are not set to a brush size??
            public unsafe void EnsureSize(int newBrushCount)
            {/*
                if (this.brushCount == newBrushCount && nodeIDValueToNodeOrderArray.IsCreated)
                {
                    Profiler.BeginSample("CLEAR");
                    Clear();
                    Profiler.EndSample();
                    return;
                }*/

                meshDataArray   = default;
                meshDatas       = new NativeList<UnityEngine.Mesh.MeshData>(Allocator.Persistent);

                Profiler.BeginSample("NEW");
                this.brushCount                 = newBrushCount;
                //var triangleArraySize         = GeometryMath.GetTriangleArraySize(newBrushCount);
                //var intersectionCount         = math.max(1, triangleArraySize);
                brushesThatNeedIndirectUpdateHashMap = new NativeHashSet<IndexOrder>(newBrushCount, Allocator.Persistent);
                brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);

                // TODO: find actual vertex count
                outputSurfaceVertices           = new NativeList<float3>(65535 * 10, Allocator.Persistent); 

                outputSurfaces                  = new NativeList<BrushIntersectionLoop>(newBrushCount * 16, Allocator.Persistent);
                brushIntersectionsWith          = new NativeList<BrushIntersectWith>(newBrushCount, Allocator.Persistent);

                surfaceCountRef                 = new NativeReference<int>(Allocator.Persistent);
                Profiler.EndSample();

                Profiler.BeginSample("NEW3");
                uniqueBrushPairs                = new NativeList<BrushPair2>(newBrushCount * 16, Allocator.Persistent);
                Profiler.EndSample();


                Profiler.BeginSample("NEW4");
                rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                allBrushMeshInstanceIDs         = new NativeArray<int>(newBrushCount, Allocator.Persistent);
                brushRenderData                 = new NativeList<BrushData>(newBrushCount, Allocator.Persistent);
                allTreeBrushIndexOrders         = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                allTreeBrushIndexOrders.Clear();
                allTreeBrushIndexOrders.Resize(newBrushCount, NativeArrayOptions.ClearMemory);

                outputSurfacesRange             = new NativeArray<int2>(newBrushCount, Allocator.Persistent);
                brushIntersectionsWithRange     = new NativeArray<int2>(newBrushCount, Allocator.Persistent);
                nodeIDValueToNodeOrderArray     = new NativeList<int>(newBrushCount, Allocator.Persistent);
                brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(newBrushCount, Allocator.Persistent);

                brushBrushIntersections         = new NativeListArray<BrushIntersectWith>(16, Allocator.Persistent);
                brushBrushIntersections.ResizeExact(newBrushCount);
                
                loopVerticesLookup              = new NativeListArray<float3>(brushCount, Allocator.Persistent);
                loopVerticesLookup.ResizeExact(brushCount);
                
                vertexBufferContents.EnsureInitialized();
                
                meshQueries = default;

                Profiler.EndSample();
            }

            public void Initialize(CSGTree tree)
            {
                this.tree = tree;

                // Make sure that if we, somehow, run this while parts of the previous update is still running, we wait for it to complete
                this.lastJobHandle.Complete();
                this.lastJobHandle = default;

                this.treeCompactNodeID   = CompactHierarchyManager.GetCompactNodeID(this.tree);
                if (!this.transformTreeBrushIndicesList.IsCreated) this.transformTreeBrushIndicesList = new NativeList<NodeOrderNodeID>(Allocator.Persistent); else this.transformTreeBrushIndicesList.Clear();
                if (!this.nodes  .IsCreated) this.nodes   = new NativeList<CompactNodeID>(Allocator.Persistent); else this.nodes  .Clear();
                if (!this.brushes.IsCreated) this.brushes = new NativeList<CSGTreeBrush> (Allocator.Persistent); else this.brushes.Clear();

                var treeHierarchy = CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);
                treeHierarchy.GetTreeNodes(this.nodes, this.brushes);


                #region Allocations/Resize
                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                var newBrushCount = this.brushes.Length;
                chiselLookupValues.EnsureCapacity(newBrushCount);

                Profiler.BeginSample("RESIZE");
                if (chiselLookupValues.basePolygonCache.Length < newBrushCount)
                    chiselLookupValues.basePolygonCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.routingTableCache.Length < newBrushCount)
                    chiselLookupValues.routingTableCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.transformationCache.Length < newBrushCount)
                    chiselLookupValues.transformationCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushRenderBufferCache.Length < newBrushCount)
                    chiselLookupValues.brushRenderBufferCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.treeSpaceVerticesCache.Length < newBrushCount)
                    chiselLookupValues.treeSpaceVerticesCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpaceBoundCache.Length < newBrushCount)
                    chiselLookupValues.brushTreeSpaceBoundCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpacePlaneCache.Length < newBrushCount)
                    chiselLookupValues.brushTreeSpacePlaneCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushesTouchedByBrushCache.Length < newBrushCount)
                    chiselLookupValues.brushesTouchedByBrushCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushIDValues.Length != newBrushCount)
                    chiselLookupValues.brushIDValues.ResizeUninitialized(newBrushCount);
                Profiler.EndSample();

                Profiler.BeginSample("CSG_Allocations");
                Profiler.BeginSample("ENSURE_SIZE");
                this.EnsureSize(newBrushCount);
                this.brushCount   = newBrushCount;
                this.maxNodeOrder = this.brushCount;
                Profiler.EndSample();

                if (!this.subMeshSurfaces.IsCreated) this.subMeshSurfaces = new NativeListArray<SubMeshSurface>(Allocator.Persistent);
                if (!this.subMeshCounts  .IsCreated) this.subMeshCounts   = new NativeList<SubMeshCounts>(Allocator.Persistent);

                if (!this.colliderMeshUpdates.IsCreated) this.colliderMeshUpdates = new NativeList<ChiselMeshUpdate>(Allocator.Persistent);
                if (!this.debugHelperMeshes  .IsCreated) this.debugHelperMeshes   = new NativeList<ChiselMeshUpdate>(Allocator.Persistent);
                if (!this.renderMeshes       .IsCreated) this.renderMeshes        = new NativeList<ChiselMeshUpdate>(Allocator.Persistent);

                #region MeshQueries
                // TODO: have more control over the queries
                this.meshQueries         = MeshQuery.DefaultQueries.ToNativeArray(Allocator.TempJob);
                this.meshQueriesLength   = this.meshQueries.Length;
                this.meshQueries.Sort(meshQueryComparer);
                #endregion

                this.subMeshSurfaces.ResizeExact(this.meshQueriesLength);
                for (int i = 0; i < this.meshQueriesLength; i++)
                    this.subMeshSurfaces.AllocateWithCapacityForIndex(i, 1000);

                this.subMeshCounts.Clear();

                // Workaround for the incredibly dumb "can't create a stream that is zero sized" when the value is determined at runtime. Yeah, thanks
                this.uniqueBrushPairs.Add(new BrushPair2() { type = IntersectionType.InvalidValue });

                this.allUpdateBrushIndexOrders.Clear();
                if (this.allUpdateBrushIndexOrders.Capacity < this.brushCount)
                    this.allUpdateBrushIndexOrders.Capacity = this.brushCount;


                this.brushesThatNeedIndirectUpdateHashMap.Clear();
                this.brushesThatNeedIndirectUpdate.Clear();

                Profiler.EndSample();
                #endregion

                #region Build lookup tables to find the tree node-order by node-index   
                Profiler.BeginSample("Lookup_Tables");
                var nodeIDValueMin = int.MaxValue;
                var nodeIDValueMax = 0;
                if (this.brushCount > 0)
                {
                    Debug.Assert(this.brushCount == this.brushes.Length);
                    for (int nodeOrder = 0; nodeOrder < this.brushCount; nodeOrder++)
                    {
                        var brush              = this.brushes[nodeOrder];
                        var compactNodeID      = CompactHierarchyManager.GetCompactNodeID(brush);
                        var compactNodeIDValue = compactNodeID.value;
                        nodeIDValueMin = math.min(nodeIDValueMin, compactNodeIDValue);
                        nodeIDValueMax = math.max(nodeIDValueMax, compactNodeIDValue);
                    }
                } else
                    nodeIDValueMin = 0;

                var nodeIDToNodeOrderOffset = nodeIDValueMin;
                var desiredLength           = (nodeIDValueMax + 1) - nodeIDValueMin;

                if (s_NodeIDValueToNodeOrderArray == null ||
                    s_NodeIDValueToNodeOrderArray.Length < desiredLength)
                    s_NodeIDValueToNodeOrderArray = new int[desiredLength];
                for (int nodeOrder  = 0; nodeOrder  < this.brushCount; nodeOrder ++)
                {
                    var compactNodeID      = CompactHierarchyManager.GetCompactNodeID(this.brushes[nodeOrder]);
                    var compactNodeIDValue = compactNodeID.value;
                    s_NodeIDValueToNodeOrderArray[compactNodeIDValue - nodeIDToNodeOrderOffset] = nodeOrder;
                    
                    // We need the index into the tree to ensure deterministic ordering
                    var brushIndexOrder = new IndexOrder { compactNodeID = compactNodeID, nodeOrder = nodeOrder };
                    this.allTreeBrushIndexOrders[nodeOrder] = brushIndexOrder;
                }

                this.nodeIDValueToNodeOrderArray.Clear();
                ChiselNativeListExtensions.AddRange(this.nodeIDValueToNodeOrderArray, s_NodeIDValueToNodeOrderArray);
                this.nodeIDValueToNodeOrderOffset = nodeIDToNodeOrderOffset;

                Profiler.EndSample();
                #endregion

                bool needRemapping = false;

                #region Remap Brush Tree Order For Cached Data
                ref var brushIDValues               = ref chiselLookupValues.brushIDValues;
                ref var basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                ref var routingTableCache           = ref chiselLookupValues.routingTableCache;
                ref var transformationCache         = ref chiselLookupValues.transformationCache;
                ref var brushRenderBufferCache      = ref chiselLookupValues.brushRenderBufferCache;
                ref var treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                ref var brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                ref var brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                ref var brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;

                // Remaps all cached data from previous brush order in tree, to new brush order
                // TODO: if all brushes need to be rebuild, don't bother to remap since everything is going to be redone anyway
                Profiler.BeginSample("REMAP");
                var previousBrushIDValuesLength = chiselLookupValues.brushIDValues.Length;
                if (previousBrushIDValuesLength > 0)
                {
                    Profiler.BeginSample("init.s_IndexLookup");
                    if (s_IndexLookup == null ||
                        s_IndexLookup.Length < s_NodeIDValueToNodeOrderArray.Length)
                        s_IndexLookup = new int[s_NodeIDValueToNodeOrderArray.Length];
                    else
                        Array.Clear(s_IndexLookup, 0, s_IndexLookup.Length);

                    for (int n = 0; n < this.brushCount; n++)
                    {
                        var compactNodeID = this.allTreeBrushIndexOrders[n].compactNodeID;
                        var offsetIDValue = compactNodeID.value - nodeIDToNodeOrderOffset;
                        s_IndexLookup[offsetIDValue] = (n + 1);
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample("init.s_RemapOldOrderToNewOrder");
                    if (s_RemapOldOrderToNewOrder == null || s_RemapOldOrderToNewOrder.Length < previousBrushIDValuesLength)
                        s_RemapOldOrderToNewOrder = new int2[previousBrushIDValuesLength];
                    else
                        Array.Clear(s_RemapOldOrderToNewOrder, 0, s_RemapOldOrderToNewOrder.Length);
                    Profiler.EndSample();

                    using (var removedBrushes = new NativeList<IndexOrder>(previousBrushIDValuesLength, Allocator.Temp))
                    { 
                        Profiler.BeginSample("check");
                        var maxCount = math.max(this.brushCount, previousBrushIDValuesLength) + 1;
                        for (int n = 0; n < previousBrushIDValuesLength; n++)
                        {
                            var sourceID        = brushIDValues[n];
                            var sourceIDValue   = sourceID.value;
                            var sourceOffset    = sourceIDValue - nodeIDToNodeOrderOffset;
                            var destination = (sourceOffset < 0 || sourceOffset >= s_NodeIDValueToNodeOrderArray.Length) ? -1 : s_IndexLookup[sourceOffset] - 1;
                            if (destination == -1)
                            {
                                removedBrushes.Add(new IndexOrder { compactNodeID = sourceID, nodeOrder = n });
                                destination = -1;
                                needRemapping = true;
                            } else
                                maxCount = math.max(maxCount, destination + 1);
                            s_RemapOldOrderToNewOrder[n] = new int2(n, destination);
                            needRemapping = needRemapping || (n != destination);
                        }
                        Profiler.EndSample();

                        if (needRemapping)
                    {
                        Profiler.BeginSample("build");
                        for (int b = 0; b < removedBrushes.Length; b++)
                        {
                            var indexOrder  = removedBrushes[b];
                            //int nodeIndex   = indexOrder.nodeIndex;
                            int nodeOrder   = indexOrder.nodeOrder;

                            var brushTouchedByBrush = brushesTouchedByBrushCache[nodeOrder];
                            if (!brushTouchedByBrush.IsCreated ||
                                brushTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                                continue;

                            ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
                            for (int i = 0; i < brushIntersections.Length; i++)
                            {
                                var otherBrushID        = brushIntersections[i].nodeIndexOrder.compactNodeID;
                                var otherBrush          = CSGTreeBrush.FindNoErrors(otherBrushID);

                                if (!otherBrush.Valid)
                                    continue;

                                // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                                if (!this.brushes.Contains(otherBrush))
                                    continue;

                                var otherBrushIDValue   = otherBrushID.value;
                                var otherBrushOrder     = s_NodeIDValueToNodeOrderArray[otherBrushIDValue - nodeIDToNodeOrderOffset];
                                var otherIndexOrder     = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherBrushOrder };
                                this.brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
                            }
                        }
                        Profiler.EndSample();

                        Profiler.BeginSample("sort");
                        Array.Sort(s_RemapOldOrderToNewOrder, 0, previousBrushIDValuesLength, indexOrderComparer);
                        Profiler.EndSample();

                        Profiler.BeginSample("REMAP2");
                        for (int n = 0; n < previousBrushIDValuesLength; n++)
                        {
                            var overwrittenValue = s_RemapOldOrderToNewOrder[n].y;
                            var originValue = s_RemapOldOrderToNewOrder[n].x;
                            if (overwrittenValue == originValue)
                                continue;
                            // TODO: OPTIMIZE!
                            for (int n2 = n + 1; n2 < previousBrushIDValuesLength; n2++)
                            {
                                var tmp = s_RemapOldOrderToNewOrder[n2];
                                if (tmp.x == overwrittenValue)
                                {
                                    if (tmp.y == originValue ||
                                        tmp.y >= previousBrushIDValuesLength)
                                    {
                                        s_RemapOldOrderToNewOrder[n2] = new int2(-1, -1);
                                        break;
                                    }
                                    s_RemapOldOrderToNewOrder[n2] = new int2(originValue, tmp.y);
                                    break;
                                }
                            }
                        }
                        Profiler.EndSample();

                        Profiler.BeginSample("RESIZE");
                        if (chiselLookupValues.basePolygonCache.Length < maxCount)
                            chiselLookupValues.basePolygonCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                        if (chiselLookupValues.routingTableCache.Length < maxCount)
                            chiselLookupValues.routingTableCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                        if (chiselLookupValues.transformationCache.Length < maxCount)
                            chiselLookupValues.transformationCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                        if (chiselLookupValues.brushRenderBufferCache.Length < maxCount)
                            chiselLookupValues.brushRenderBufferCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                        if (chiselLookupValues.treeSpaceVerticesCache.Length < maxCount)
                            chiselLookupValues.treeSpaceVerticesCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                        if (chiselLookupValues.brushTreeSpaceBoundCache.Length < maxCount)
                            chiselLookupValues.brushTreeSpaceBoundCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                        if (chiselLookupValues.brushTreeSpacePlaneCache.Length < maxCount)
                            chiselLookupValues.brushTreeSpacePlaneCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                        if (chiselLookupValues.brushesTouchedByBrushCache.Length < maxCount)
                            chiselLookupValues.brushesTouchedByBrushCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                        Profiler.EndSample();

                        Profiler.BeginSample("SWAP");
                        for (int n = 0; n < previousBrushIDValuesLength; n++)
                        {
                            var source = s_RemapOldOrderToNewOrder[n].x;
                            var destination = s_RemapOldOrderToNewOrder[n].y;
                            if (source == -1)
                                continue;

                            if (source == destination)
                                continue;

                            if (destination == -1)
                            {
                                Profiler.BeginSample("Dispose");
                                { var tmp = basePolygonCache[source]; basePolygonCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                { var tmp = routingTableCache[source]; routingTableCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                { var tmp = transformationCache[source]; transformationCache[source] = default; }
                                { var tmp = brushRenderBufferCache[source]; brushRenderBufferCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                { var tmp = treeSpaceVerticesCache[source]; treeSpaceVerticesCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                { var tmp = brushTreeSpaceBoundCache[source]; brushTreeSpaceBoundCache[source] = default; }
                                { var tmp = brushTreeSpacePlaneCache[source]; brushTreeSpacePlaneCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                { var tmp = brushesTouchedByBrushCache[source]; brushesTouchedByBrushCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                Profiler.EndSample();
                                continue;
                            }

                            { var tmp = basePolygonCache[destination]; basePolygonCache[destination] = basePolygonCache[source]; basePolygonCache[source] = tmp; }
                            { var tmp = routingTableCache[destination]; routingTableCache[destination] = routingTableCache[source]; routingTableCache[source] = tmp; }
                            { var tmp = transformationCache[destination]; transformationCache[destination] = transformationCache[source]; transformationCache[source] = tmp; }
                            { var tmp = brushRenderBufferCache[destination]; brushRenderBufferCache[destination] = brushRenderBufferCache[source]; brushRenderBufferCache[source] = tmp; }
                            { var tmp = treeSpaceVerticesCache[destination]; treeSpaceVerticesCache[destination] = treeSpaceVerticesCache[source]; treeSpaceVerticesCache[source] = tmp; }
                            { var tmp = brushTreeSpaceBoundCache[destination]; brushTreeSpaceBoundCache[destination] = brushTreeSpaceBoundCache[source]; brushTreeSpaceBoundCache[source] = tmp; }
                            { var tmp = brushTreeSpacePlaneCache[destination]; brushTreeSpacePlaneCache[destination] = brushTreeSpacePlaneCache[source]; brushTreeSpacePlaneCache[source] = tmp; }
                            { var tmp = brushesTouchedByBrushCache[destination]; brushesTouchedByBrushCache[destination] = brushesTouchedByBrushCache[source]; brushesTouchedByBrushCache[source] = tmp; }
                        }
                        Profiler.EndSample();
                    }
                    }
                    brushIDValues               = ref chiselLookupValues.brushIDValues;
                    basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                    routingTableCache           = ref chiselLookupValues.routingTableCache;
                    transformationCache         = ref chiselLookupValues.transformationCache;
                    brushRenderBufferCache      = ref chiselLookupValues.brushRenderBufferCache;
                    treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                    brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                    brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                    brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;
                }                    
                Profiler.EndSample();
                #endregion

                #region Node Index by Brush Index lookup
                Profiler.BeginSample("chiselLookupValues.brushIDValues");
                for (int i = 0; i < this.brushCount; i++)
                    chiselLookupValues.brushIDValues[i] = this.allTreeBrushIndexOrders[i].compactNodeID;
                Profiler.EndSample();
                #endregion

                #region Build list of all brushes that have been modified
                Profiler.BeginSample("FindModifiedBrushes");
                this.rebuildTreeBrushIndexOrders.Clear();
                if (this.rebuildTreeBrushIndexOrders.Capacity < this.brushCount)
                    this.rebuildTreeBrushIndexOrders.Capacity = this.brushCount;
                this.transformTreeBrushIndicesList.Clear();
                FindModifiedBrushes(in treeHierarchy, in this.brushes, in this.allTreeBrushIndexOrders, ref this.transformTreeBrushIndicesList, ref this.rebuildTreeBrushIndexOrders);
                this.updateCount = this.rebuildTreeBrushIndexOrders.Length;
                Profiler.EndSample();
                #endregion

                #region Invalidate brushes that touch our modified brushes, so we rebuild those too
                if (this.rebuildTreeBrushIndexOrders.Length != this.brushCount ||
                    needRemapping)
                {
                    Profiler.BeginSample("Invalidate_Brushes");
                    for (int b = 0; b < this.rebuildTreeBrushIndexOrders.Length; b++)
                    {
                        var indexOrder  = this.rebuildTreeBrushIndexOrders[b];
                        var brush       = CSGTreeBrush.Find(indexOrder.compactNodeID);
                        int nodeOrder   = indexOrder.nodeOrder;

                        if (!brush.IsStatusFlagSet(NodeStatusFlags.NeedAllTouchingUpdated))
                            continue;

                        var brushTouchedByBrush = brushesTouchedByBrushCache[nodeOrder];
                        if (!brushTouchedByBrush.IsCreated ||
                            brushTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                            continue;

                        ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
                        for (int i = 0; i < brushIntersections.Length; i++)
                        {
                            var otherBrushID = brushIntersections[i].nodeIndexOrder.compactNodeID;
                            var otherBrush   = CSGTreeBrush.FindNoErrors(otherBrushID);

                            if (!otherBrush.Valid)
                                continue;

                            // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                            if (!this.brushes.Contains(otherBrush))
                                continue;

                            var otherBrushIDValue   = otherBrushID.value;
                            var otherBrushOrder     = s_NodeIDValueToNodeOrderArray[otherBrushIDValue - nodeIDToNodeOrderOffset];
                            var otherIndexOrder     = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherBrushOrder };
                            this.brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
                        }
                    }
                    Profiler.EndSample();
                }
                #endregion

                #region Build Transformations
                // TODO: optimize, only do this when necessary
                Profiler.BeginSample("UpdateTransformations");
                UpdateTransformations(in treeHierarchy, this.transformTreeBrushIndicesList, transformationCache);
                Profiler.EndSample();
                #endregion

                #region Build Compact Tree
                Profiler.BeginSample("CompactTreeBuilder");
                this.compactTree = CompactTreeBuilder.Create(ref this.nodes, ref this.brushes, this.tree);
                Profiler.EndSample();
                #endregion
      
                #region Build all BrushMeshBlobs
                Profiler.BeginSample("UpdateBrushMeshInstanceIDs");
                ref var parameters1 = ref chiselLookupValues.parameters1;
                ref var parameters2 = ref chiselLookupValues.parameters2;

                var allKnownBrushMeshIndices = chiselLookupValues.allKnownBrushMeshIndices;

                bool rebuildParameterList = true;
                using (var foundBrushMeshIndices = new NativeHashSet<int>(math.max(allKnownBrushMeshIndices.Count, this.brushCount), Allocator.Temp))
                {
                    foundBrushMeshIndices.Clear();
                    for (int nodeOrder = 0; nodeOrder < this.brushCount; nodeOrder++)
                    {
                        var brush = this.brushes[nodeOrder];
                        int brushMeshHash = 0;
                        if (!CompactHierarchyManager.IsValidNodeID(brush) ||
                            // NOTE: Assignment is intended, this is not supposed to be a comparison
                            (brushMeshHash = brush.BrushMesh.brushMeshHash) == 0)
                        {
                            // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                            Debug.LogError($"Brush with ID {brush} has its brushMeshID set to {brushMeshHash}, which is invalid.");
                            this.allBrushMeshInstanceIDs[nodeOrder] = 0;
                        } else
                        {
                            this.allBrushMeshInstanceIDs[nodeOrder] = brushMeshHash;
                            foundBrushMeshIndices.Add(brushMeshHash);
                        }
                    }

                    // TODO: optimize all of this, especially slow for complete update
                    using (var removeBrushMeshIndices = new NativeHashSet<int>(allKnownBrushMeshIndices.Count, Allocator.Temp))
                    {
                        removeBrushMeshIndices.Clear();
                        foreach (var brushMeshIndex in allKnownBrushMeshIndices)
                        {
                            if (foundBrushMeshIndices.Contains(brushMeshIndex))
                                foundBrushMeshIndices.Remove(brushMeshIndex);
                            else
                                removeBrushMeshIndices.Add(brushMeshIndex);
                        }

                        ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                        if (rebuildParameterList)
                        {
                            foreach (int brushMeshIndex in removeBrushMeshIndices)
                                allKnownBrushMeshIndices.Remove(brushMeshIndex);
                            parameters1.Clear();
                            parameters2.Clear();
                            foreach (var brushMeshIndex in allKnownBrushMeshIndices)
                            {
                                ref var polygons = ref brushMeshBlobs[brushMeshIndex].brushMeshBlob.Value.polygons;
                                for (int p = 0; p < polygons.Length; p++)
                                {
                                    ref var polygon = ref polygons[p];
                                    var layerUsage = polygon.surface.layerDefinition.layerUsage;
                                    if ((layerUsage & LayerUsageFlags.Renderable) != 0) parameters1.RegisterParameter(polygon.surface.layerDefinition.layerParameter1);
                                    if ((layerUsage & LayerUsageFlags.Collidable) != 0) parameters2.RegisterParameter(polygon.surface.layerDefinition.layerParameter2);
                                }
                            }
                        }

                        foreach (int brushMeshIndex in foundBrushMeshIndices)
                            allKnownBrushMeshIndices.Add(brushMeshIndex);
                    }
                }

                this.parameter1Count = chiselLookupValues.parameters1.uniqueParameterCount;
                this.parameter2Count = chiselLookupValues.parameters2.uniqueParameterCount;
                Profiler.EndSample();
                #endregion

                #region Reset All JobHandles
                this.allBrushMeshInstanceIDsJobHandle = default;
                this.allTreeBrushIndexOrdersJobHandle = default;
                this.allUpdateBrushIndexOrdersJobHandle = default;
                
                this.basePolygonCacheJobHandle = default;
                this.brushBrushIntersectionsJobHandle = default;
                this.brushesTouchedByBrushCacheJobHandle = default;
                this.brushRenderBufferCacheJobHandle = default;
                this.brushRenderDataJobHandle = default;
                this.brushTreeSpacePlaneCacheJobHandle = default;
                this.brushMeshBlobsLookupJobHandle = default;
                this.brushMeshLookupJobHandle = default;
                this.brushIntersectionsWithJobHandle = default;
                this.brushIntersectionsWithRangeJobHandle = default;
                this.brushesThatNeedIndirectUpdateHashMapJobHandle = default;
                this.brushesThatNeedIndirectUpdateJobHandle = default;
                this.brushTreeSpaceBoundCacheJobHandle = default;

                this.compactTreeJobHandle = default;

                this.dataStream1JobHandle = default;
                this.dataStream2JobHandle = default;

                this.intersectingBrushesStreamJobHandle = default;

                this.loopVerticesLookupJobHandle = default;

                this.meshQueriesJobHandle = default;

                this.nodeIndexToNodeOrderArrayJobHandle = default;
            
                this.outputSurfaceVerticesJobHandle = default;
                this.outputSurfacesJobHandle = default;
                this.outputSurfacesRangeJobHandle = default;

                this.routingTableCacheJobHandle = default;
                this.rebuildTreeBrushIndexOrdersJobHandle = default;

                this.sectionsJobHandle = default;
                this.surfaceCountRefJobHandle = default;
                this.subMeshSurfacesJobHandle = default;
                this.subMeshCountsJobHandle = default;

                this.treeSpaceVerticesCacheJobHandle = default;
                this.transformationCacheJobHandle = default;

                this.uniqueBrushPairsJobHandle = default;

                this.vertexBufferContents_renderDescriptorsJobHandle = default;
                this.vertexBufferContents_colliderDescriptorsJobHandle = default;
                this.vertexBufferContents_subMeshSectionsJobHandle = default;
                this.vertexBufferContents_meshesJobHandle = default;
                this.colliderMeshUpdatesJobHandle = default;
                this.debugHelperMeshesJobHandle = default;
                this.renderMeshesJobHandle = default;

                this.vertexBufferContents_triangleBrushIndicesJobHandle = default;
                this.vertexBufferContents_meshDescriptionsJobHandle = default;

                this.meshDatasJobHandle = default;
                this.storeToCacheJobHandle = default;
                this.updateBrushOutlineJobHandle = default;
                this.preMeshUpdateCombinedJobHandle = default;
                #endregion
            }

            public void RunJobs()
            { 
            }

            public JobHandle PreMeshUpdateDispose(JobHandle disposeJobHandle)
            {
                lastJobHandle = disposeJobHandle;

                Profiler.BeginSample("DISPOSE_ARRAY");
                if (brushMeshLookup              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushMeshLookup              .Dispose(disposeJobHandle));
                if (brushIntersectionsWithRange  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushIntersectionsWithRange  .Dispose(disposeJobHandle));
                if (outputSurfacesRange          .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfacesRange          .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE_LISTARRAY");
                if (loopVerticesLookup           .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, loopVerticesLookup           .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE_LIST");
                if (transformTreeBrushIndicesList.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, transformTreeBrushIndicesList.Dispose(disposeJobHandle));
                if (brushes                      .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushes                      .Dispose(disposeJobHandle));
                if (nodes                        .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, nodes                        .Dispose(disposeJobHandle));
                if (brushRenderData              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushRenderData              .Dispose(disposeJobHandle));
                if (subMeshCounts                .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, subMeshCounts                .Dispose(disposeJobHandle));
                if (subMeshSurfaces              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, subMeshSurfaces              .Dispose(disposeJobHandle));
                if (rebuildTreeBrushIndexOrders  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, rebuildTreeBrushIndexOrders  .Dispose(disposeJobHandle));
                if (allUpdateBrushIndexOrders    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allUpdateBrushIndexOrders    .Dispose(disposeJobHandle));
                if (allBrushMeshInstanceIDs      .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allBrushMeshInstanceIDs      .Dispose(disposeJobHandle));
                if (uniqueBrushPairs             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, uniqueBrushPairs             .Dispose(disposeJobHandle));
                if (brushIntersectionsWith       .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushIntersectionsWith       .Dispose(disposeJobHandle));
                if (outputSurfaceVertices        .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfaceVertices        .Dispose(disposeJobHandle));
                if (outputSurfaces               .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfaces               .Dispose(disposeJobHandle));
                if (brushesThatNeedIndirectUpdate.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdate.Dispose(disposeJobHandle));
                if (nodeIDValueToNodeOrderArray  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, nodeIDValueToNodeOrderArray  .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE_HASMAP");
                if (brushesThatNeedIndirectUpdateHashMap.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdateHashMap.Dispose(disposeJobHandle));
                if (brushBrushIntersections             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushBrushIntersections             .Dispose(disposeJobHandle));
                Profiler.EndSample();

                if (meshQueries.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, meshQueries.Dispose(disposeJobHandle));

                if (compactTree.IsCreated) compactTree.Dispose(); compactTree = default;


                meshQueries = default;
                transformTreeBrushIndicesList = default;
                brushes                         = default;
                nodes                           = default;

                brushRenderData                 = default;
                subMeshCounts                   = default;
                subMeshSurfaces                 = default;
                brushMeshLookup                 = default;
                rebuildTreeBrushIndexOrders     = default;
                allUpdateBrushIndexOrders       = default;
                allBrushMeshInstanceIDs         = default;
                brushBrushIntersections         = default;
                brushIntersectionsWith          = default;
                brushIntersectionsWithRange     = default;
                nodeIDValueToNodeOrderArray     = default;
                brushesThatNeedIndirectUpdate   = default;
                brushesThatNeedIndirectUpdateHashMap = default;
                uniqueBrushPairs                = default;
                outputSurfaceVertices           = default;
                outputSurfaces                  = default;
                outputSurfacesRange             = default;
                meshQueries                     = default;

                return lastJobHandle;
            }

            public JobHandle Dispose(JobHandle disposeJobHandle)
            {
                lastJobHandle = disposeJobHandle;

                Profiler.BeginSample("DISPOSE_LIST");
                if (allTreeBrushIndexOrders      .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allTreeBrushIndexOrders      .Dispose(disposeJobHandle));
                if (colliderMeshUpdates          .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, colliderMeshUpdates          .Dispose(disposeJobHandle));
                if (debugHelperMeshes            .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, debugHelperMeshes            .Dispose(disposeJobHandle));
                if (renderMeshes                 .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, renderMeshes                 .Dispose(disposeJobHandle));
                if (meshDatas                    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, meshDatas                    .Dispose(disposeJobHandle));
                Profiler.EndSample();

                if (vertexBufferContents         .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents         .Dispose(disposeJobHandle));
                


                Profiler.BeginSample("DISPOSE_surfaceCountRef");
                surfaceCountRefJobHandle.Complete();
                surfaceCountRefJobHandle = default;
                if (surfaceCountRef.IsCreated) surfaceCountRef.Dispose();
                Profiler.EndSample();
                
                allTreeBrushIndexOrders         = default;
                colliderMeshUpdates             = default;
                debugHelperMeshes               = default;
                renderMeshes                    = default;
                vertexBufferContents            = default;
                meshDataArray                   = default;
                meshDatas                       = default;
                surfaceCountRef                 = default;

                brushCount = 0;

                return lastJobHandle;
            }
        }

        internal struct NodeOrderNodeID
        {
            public int              nodeOrder;
            public CompactNodeID    compactNodeID;
        }

        static int[]        s_NodeIDValueToNodeOrderArray;

        static int[]        s_IndexLookup;
        static int2[]       s_RemapOldOrderToNewOrder;

        static TreeUpdate[] s_TreeUpdates;

        #region TreeSorter
        // Sort trees so we try to schedule the slowest ones first, so the faster ones can then fill the gaps in between
        static int TreeUpdateCompare(TreeUpdate x, TreeUpdate y)
        {
            if (!x.allTreeBrushIndexOrders.IsCreated)
            {
                if (!y.allTreeBrushIndexOrders.IsCreated)
                    return 0;
                return 1;
            }
            if (!y.allTreeBrushIndexOrders.IsCreated)
                return -1;
            var xBrushBrushIntersectionsCount = x.brushCount;
            var yBrushBrushIntersectionsCount = y.brushCount;
            if (xBrushBrushIntersectionsCount < yBrushBrushIntersectionsCount)
                return 1;
            if (xBrushBrushIntersectionsCount > yBrushBrushIntersectionsCount)
                return -1;

            if (x.updateCount < y.updateCount)
                return 1;
            if (x.updateCount > y.updateCount)
                return -1;

            return x.tree.CompareTo(y.tree);
        }
        static readonly Comparison<TreeUpdate> s_TreeSorter = TreeUpdateCompare;
        #endregion

        #region IndexOrderComparer - Sort index order to ensure consistency
        struct IndexOrderComparer : System.Collections.Generic.IComparer<int2>
        {
            public int Compare(int2 x, int2 y)
            {
                int yCompare = x.y.CompareTo(y.y);
                if (yCompare != 0)
                    return yCompare;
                return x.x.CompareTo(y.x);
            }
        }
        static readonly IndexOrderComparer indexOrderComparer = new IndexOrderComparer();
        #endregion

        #region MeshQueryComparer - Sort mesh mesh queries to help ensure consistency
        struct MeshQueryComparer : System.Collections.Generic.IComparer<MeshQuery>
        {
            public int Compare(MeshQuery x, MeshQuery y)
            {
                if (x.LayerParameterIndex != y.LayerParameterIndex) return ((int)x.LayerParameterIndex) - ((int)y.LayerParameterIndex);
                if (x.LayerQuery != y.LayerQuery) return ((int)x.LayerQuery) - ((int)y.LayerQuery);
                return 0;
            }
        }

        static readonly MeshQueryComparer meshQueryComparer = new MeshQueryComparer();
        #endregion

        #region CombineDependencies
        static JobHandle CombineDependencies(JobHandle handle0) { return handle0; }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1) { return JobHandle.CombineDependencies(handle0, handle1); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2) { return JobHandle.CombineDependencies(handle0, handle1, handle2); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), handle3 ); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4)); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5)); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), handle6); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7)); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8) { return JobHandle.CombineDependencies( JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7, handle8)); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, JobHandle handle9)  { return JobHandle.CombineDependencies( JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5),  JobHandle.CombineDependencies(handle6, handle7, JobHandle.CombineDependencies(handle8, handle9)));  }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, JobHandle handle9, JobHandle handle10) { return JobHandle.CombineDependencies( JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7, JobHandle.CombineDependencies(handle8, handle9, handle10))); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, JobHandle handle9, JobHandle handle10, JobHandle handle11) { return JobHandle.CombineDependencies( JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, JobHandle.CombineDependencies(handle7, handle8, handle9), JobHandle.CombineDependencies(handle10, handle11))); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, JobHandle handle9, JobHandle handle10, JobHandle handle11, JobHandle handle12) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, JobHandle.CombineDependencies(handle7, handle8, handle9), JobHandle.CombineDependencies(handle10, handle11, handle12))); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, params JobHandle[] handles)
        {
            JobHandle handle = JobHandle.CombineDependencies(
                                    JobHandle.CombineDependencies(handle0, handle1, handle2),
                                    JobHandle.CombineDependencies(handle3, handle4, handle5),
                                    JobHandle.CombineDependencies(handle6, handle7, handle8)
                                );

            for (int i = 0; i < handles.Length; i++)
                handle = JobHandle.CombineDependencies(handle, handles[i]);
            return handle;
        }
        #endregion
    }
}
