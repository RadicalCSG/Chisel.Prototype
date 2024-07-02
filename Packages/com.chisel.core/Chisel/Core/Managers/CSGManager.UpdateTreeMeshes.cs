using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Entities;

namespace Chisel.Core
{
    partial class CompactHierarchyManager
    {
        const bool runInParallelDefault = true;

        #region Update / Rebuild
        internal static bool UpdateAllTreeMeshes(FinishMeshUpdate finishMeshUpdates, out JobHandle allTrees)
        {
            allTrees = default;
            bool needUpdate = false;

            CompactHierarchyManager.GetAllTrees(instance.allTrees);
            // Check if we have a tree that needs updates
            instance.updatedTrees.Clear();
            for (int t = 0; t < instance.allTrees.Length; t++)
            {
                var tree = instance.allTrees[t];
                if (tree.Valid &&
                    tree.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                {
                    instance.updatedTrees.Add(tree);
                    needUpdate = true;
                }
            }

            if (!needUpdate)
                return false;


            using (new ProfilerMarker("UpdateTreeMeshes").Auto())
            {
                allTrees = TreeUpdate.ScheduleTreeMeshJobs(finishMeshUpdates, instance.updatedTrees);
                return true;
            }
        }
        #endregion
        
        const Allocator defaultAllocator = Allocator.TempJob;

        internal struct TreeUpdate
        {
            public CSGTree          tree;
            public CompactNodeID    treeCompactNodeID;
            public int              brushCount;
            public int              maxNodeOrder;
            public int              updateCount;

            public JobHandle        dependencies;

            #region All Native Collection Temporaries
            internal struct TemporariesStruct
            { 
                public UnityEngine.Mesh.MeshDataArray           meshDataArray;
                public NativeList<UnityEngine.Mesh.MeshData>    meshDatas;

                public NativeArray<int>                     parameterCounts;
                public NativeList<NodeOrderNodeID>          transformTreeBrushIndicesList;
                public NativeList<NodeOrderNodeID>          brushBoundsUpdateList;

                public NativeList<CompactNodeID>            brushes;
                public NativeList<CompactNodeID>            nodes;

                public NativeList<IndexOrder>               allTreeBrushIndexOrders;
                public NativeList<IndexOrder>               rebuildTreeBrushIndexOrders;
                public NativeList<IndexOrder>               allUpdateBrushIndexOrders;
                public NativeArray<int>                     allBrushMeshIDs;
            
                public NativeArray<MeshQuery>               meshQueries;
                public int                                  meshQueriesLength;

                public NativeArray<UnsafeList<BrushIntersectWith>>  brushBrushIntersections;
                public NativeList<BrushIntersectWith>       brushIntersectionsWith;
                public NativeArray<int2>                    brushIntersectionsWithRange;
                public NativeList<IndexOrder>               brushesThatNeedIndirectUpdate;
                public NativeParallelHashSet<IndexOrder>            brushesThatNeedIndirectUpdateHashMap;

                public NativeList<BrushPair2>               uniqueBrushPairs;

                public NativeList<float3>                   outputSurfaceVertices;
                public NativeList<BrushIntersectionLoop>    outputSurfaces;
                public NativeArray<int2>                    outputSurfacesRange;

                public NativeArray<BlobAssetReference<BrushMeshBlob>>   brushMeshLookup;

                public NativeArray<UnsafeList<float3>>      loopVerticesLookup;

                public NativeReference<int>                 surfaceCountRef;
                public NativeReference<BlobAssetReference<CompactTree>> compactTreeRef;
                public NativeReference<bool>                needRemappingRef;

                public VertexBufferContents                 vertexBufferContents;

                public NativeList<int>                      nodeIDValueToNodeOrder;
                public NativeReference<int>                 nodeIDValueToNodeOrderOffsetRef;

                public NativeList<BrushData>                brushRenderData;
                public NativeList<SubMeshCounts>            subMeshCounts;
                public NativeArray<UnsafeList<SubMeshSurface>>  subMeshSurfaces;

                public NativeStream                         dataStream1;
                public NativeStream                         dataStream2;
                public NativeStream                         intersectingBrushesStream;

                public NativeList<ChiselMeshUpdate>         meshUpdates;
                public NativeList<ChiselMeshUpdate>         colliderMeshUpdates;
                public NativeList<ChiselMeshUpdate>         debugHelperMeshes;
                public NativeList<ChiselMeshUpdate>         renderMeshes;

                public NativeList<BlobAssetReference<BasePolygonsBlob>>             basePolygonDisposeList;
                public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesDisposeList;
                public NativeList<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushDisposeList;
                public NativeList<BlobAssetReference<RoutingTable>>                 routingTableDisposeList;
                public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneDisposeList;
                public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferDisposeList;
            }
            internal TemporariesStruct Temporaries;
            #endregion

            #region Sub tasks JobHandles
            internal struct JobHandlesStruct
            {
                public DualJobHandle transformTreeBrushIndicesListJobHandle;
                public DualJobHandle brushBoundsUpdateListJobHandle;
                public DualJobHandle brushesJobHandle;
                public DualJobHandle nodesJobHandle;
                public DualJobHandle parametersJobHandle;
                public DualJobHandle allKnownBrushMeshIndicesJobHandle;
                public DualJobHandle parameterCountsJobHandle;

                public DualJobHandle allBrushMeshIDsJobHandle;
                public DualJobHandle allTreeBrushIndexOrdersJobHandle;
                public DualJobHandle allUpdateBrushIndexOrdersJobHandle;

                public DualJobHandle brushIDValuesJobHandle;
                public DualJobHandle basePolygonCacheJobHandle;
                public DualJobHandle brushBrushIntersectionsJobHandle;
                public DualJobHandle brushesTouchedByBrushCacheJobHandle;
                public DualJobHandle brushRenderBufferCacheJobHandle;
                public DualJobHandle brushRenderDataJobHandle;
                public DualJobHandle brushTreeSpacePlaneCacheJobHandle;
                public DualJobHandle brushMeshBlobsLookupJobHandle;
                public DualJobHandle hierarchyIDJobHandle;
                public DualJobHandle hierarchyListJobHandle;
                public DualJobHandle brushMeshLookupJobHandle;
                public DualJobHandle brushIntersectionsWithJobHandle;
                public DualJobHandle brushIntersectionsWithRangeJobHandle;
                public DualJobHandle brushesThatNeedIndirectUpdateHashMapJobHandle;
                public DualJobHandle brushesThatNeedIndirectUpdateJobHandle;
                public DualJobHandle brushTreeSpaceBoundCacheJobHandle;

                public DualJobHandle dataStream1JobHandle;
                public DualJobHandle dataStream2JobHandle;

                public DualJobHandle intersectingBrushesStreamJobHandle;

                public DualJobHandle loopVerticesLookupJobHandle;

                public DualJobHandle meshQueriesJobHandle;

                public DualJobHandle nodeIDValueToNodeOrderArrayJobHandle;

                public DualJobHandle outputSurfaceVerticesJobHandle;
                public DualJobHandle outputSurfacesJobHandle;
                public DualJobHandle outputSurfacesRangeJobHandle;

                public DualJobHandle routingTableCacheJobHandle;
                public DualJobHandle rebuildTreeBrushIndexOrdersJobHandle;

                public DualJobHandle sectionsJobHandle;
                public DualJobHandle surfaceCountRefJobHandle;
                public DualJobHandle compactTreeRefJobHandle;
                public DualJobHandle needRemappingRefJobHandle;
                public DualJobHandle nodeIDValueToNodeOrderOffsetRefJobHandle;
                public DualJobHandle subMeshSurfacesJobHandle;
                public DualJobHandle subMeshCountsJobHandle;

                public DualJobHandle treeSpaceVerticesCacheJobHandle;
                public DualJobHandle transformationCacheJobHandle;

                public DualJobHandle uniqueBrushPairsJobHandle;

                public DualJobHandle vertexBufferContents_renderDescriptorsJobHandle;
                public DualJobHandle vertexBufferContents_colliderDescriptorsJobHandle;
                public DualJobHandle vertexBufferContents_subMeshSectionsJobHandle;
                public DualJobHandle vertexBufferContents_meshesJobHandle;
                public DualJobHandle meshUpdatesJobHandle;
                public DualJobHandle colliderMeshUpdatesJobHandle;
                public DualJobHandle debugHelperMeshesJobHandle;
                public DualJobHandle renderMeshesJobHandle;

                public DualJobHandle vertexBufferContents_triangleBrushIndicesJobHandle;
                public DualJobHandle vertexBufferContents_meshDescriptionsJobHandle;

                public DualJobHandle meshDatasJobHandle;
                public DualJobHandle storeToCacheJobHandle;

                public DualJobHandle preMeshUpdateCombinedJobHandle;
            }
            internal JobHandlesStruct JobHandles;
            #endregion

            static TreeUpdate[] s_TreeUpdates;
            public static JobHandle ScheduleTreeMeshJobs(FinishMeshUpdate finishMeshUpdates, NativeList<CSGTree> trees)
            {
                var finalJobHandle = default(JobHandle);

                //
                // Schedule all the jobs that create new meshes based on our CSG trees
                //
                #region Schedule Generator Jobs
                var generatorPoolJobHandle = default(JobHandle);
                using (new ProfilerMarker("CSG_ScheduleGeneratorJobPool").Auto())
                { 
                    var runInParallel = runInParallelDefault;
                    generatorPoolJobHandle = GeneratorJobPoolManager.ScheduleJobs(runInParallel);
                }
                #endregion


                // TODO: Try to get rid of this Complete
                generatorPoolJobHandle.Complete(); // <-- Initialize has code that depends on the current state of the tree
                generatorPoolJobHandle = default;


                //
                // Make a list of valid trees
                //
                #region Find all modified, valid trees
                var treeUpdateLength = 0;
                using (new ProfilerMarker("CSG_TreeUpdate_Allocate").Auto())
                {
                    if (s_TreeUpdates == null || s_TreeUpdates.Length < trees.Length)
                        s_TreeUpdates = new TreeUpdate[trees.Length];
                    for (int t = 0; t < trees.Length; t++)
                    {
                        var tree = trees[t];
                        var treeCompactNodeID = CompactHierarchyManager.GetCompactNodeID(tree);
                        ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeCompactNodeID);

                        // Skip invalid trees since they wouldn't work anyway
                        if (!compactHierarchy.IsValidCompactNodeID(treeCompactNodeID))
                            continue;

                        if (!compactHierarchy.IsNodeDirty(treeCompactNodeID))
                            continue;

                        ref var treeUpdate = ref s_TreeUpdates[treeUpdateLength];
                        treeUpdate.tree = tree;
                        treeUpdate.treeCompactNodeID = treeCompactNodeID;
                        treeUpdateLength++;
                    }

                    if (treeUpdateLength == 0)
                        return finalJobHandle;
                }
                #endregion

                //
                // Initialize our data structures
                //
                #region Find all modified, valid trees
                using (new ProfilerMarker("CSG_TreeUpdate_Initialize").Auto())
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        s_TreeUpdates[t].Initialize();
                    }
                }
                #endregion

                try
                {
                    try
                    {
                        //
                        // Preprocess the data we need to perform CSG, figure what needs to be updated in the tree (might be nothing)
                        //
                        #region Schedule cache update jobs
                        using (new ProfilerMarker("CSG_RunMeshInitJobs").Auto())
                        {
                            for (int t = 0; t < treeUpdateLength; t++)
                            {
                                s_TreeUpdates[t].RunMeshInitJobs(generatorPoolJobHandle);
                            }
                        }
                        #endregion

                        //
                        // Schedule chain of jobs that will generate our surface meshes 
                        // At this point we need previously scheduled jobs to be completed so we know what actually needs to be updated, if anything
                        //
                        #region Schedule CSG jobs
                        using (new ProfilerMarker("CSG_RunMeshUpdateJobs").Auto())
                        {
                            // Reverse order since we sorted the trees from big to small & small trees are more likely to have already completed
                            for (int t = treeUpdateLength - 1; t >= 0; t--)
                            {
                                ref var treeUpdate = ref s_TreeUpdates[t];
                                // TODO: figure out if there's a way around this ....
                                treeUpdate.JobHandles.transformTreeBrushIndicesListJobHandle.readWriteBarrier.Complete();
                                treeUpdate.JobHandles.rebuildTreeBrushIndexOrdersJobHandle.writeBarrier.Complete();
                                treeUpdate.updateCount = treeUpdate.Temporaries.rebuildTreeBrushIndexOrders.Length;

                                if (treeUpdate.updateCount <= 0)
                                    continue;

                                treeUpdate.RunMeshUpdateJobs();
                            }
                        }
                        #endregion
                    }
                    finally
                    {
                        //
                        // Dispose temporaries that we don't need anymore
                        //
                        for (int t = 0; t < treeUpdateLength; t++)
                        {
                            ref var treeUpdate = ref s_TreeUpdates[t];
                            treeUpdate.PreMeshUpdateDispose();
                        }
                    }

                    //
                    // Wait for our scheduled mesh update jobs to finish, ensure our components are setup correctly, and upload our mesh data to the meshes
                    //

                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        treeUpdate.dependencies = JobHandleExtensions.CombineDependencies(treeUpdate.JobHandles.meshDatasJobHandle.writeBarrier,
                                                                                          treeUpdate.JobHandles.meshUpdatesJobHandle.writeBarrier,
                                                                                          treeUpdate.JobHandles.colliderMeshUpdatesJobHandle.writeBarrier,
                                                                                          treeUpdate.JobHandles.debugHelperMeshesJobHandle.writeBarrier,
                                                                                          treeUpdate.JobHandles.renderMeshesJobHandle.writeBarrier,
                                                                                          treeUpdate.JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle.writeBarrier,
                                                                                          treeUpdate.JobHandles.vertexBufferContents_meshesJobHandle.writeBarrier);

                        // TODO: get rid of these crazy legacy flags
                        #region Clear tree/brush status flags 
                        ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeUpdate.treeCompactNodeID);
                        using (new ProfilerMarker("CSG_ClearFlags").Auto())
                        {
                            compactHierarchy.ClearAllStatusFlags(treeUpdate.treeCompactNodeID);
                            for (int b = 0; b < treeUpdate.brushCount; b++)
                            {
                                var brushIndexOrder = treeUpdate.Temporaries.allTreeBrushIndexOrders[b];
                                compactHierarchy.ClearAllStatusFlags(brushIndexOrder.compactNodeID);
                            }
                        }
                        #endregion

                        if (treeUpdate.updateCount <= 0 &&
                            treeUpdate.brushCount > 0)
                            continue;

                        //
                        // Call delegate to convert the generated meshes in whatever we need 
                        //  For example, it could create/update Meshes/MeshRenderers/MeshFilters/Gameobjects etc.
                        //  But it could eventually, optionally, output entities instead at some point
                        //
                        #region Finish Mesh Updates
                        if (finishMeshUpdates != null)
                        {
                            using (new ProfilerMarker("CSG_FinishMeshUpdates").Auto())
                            {
                                var usedMeshCount = finishMeshUpdates(treeUpdate.tree, ref treeUpdate.Temporaries.vertexBufferContents,
                                                                      ref treeUpdate.Temporaries.meshDataArray,
                                                                      treeUpdate.Temporaries.colliderMeshUpdates,
                                                                      treeUpdate.Temporaries.debugHelperMeshes,
                                                                      treeUpdate.Temporaries.renderMeshes,
                                                                      treeUpdate.dependencies);
                            }
                        }
                        #endregion
                    }
                }
                finally
                {
                    #region Ensure meshes are cleaned up
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];

                        // Error or not, our jobs need to be completed at this point
                        treeUpdate.dependencies.Complete();

                        // Ensure our meshDataArray ends up being disposed, even if we had errors
                        if (treeUpdate.Temporaries.meshDataArray.Length > 0)
                        {
                            try { treeUpdate.Temporaries.meshDataArray.Dispose(); } catch { }
                        }
                        treeUpdate.Temporaries.meshDataArray = default;
                    }
                    #endregion

                    #region Free temporaries
                    // TODO: most of these disposes can be scheduled before we complete and write to the meshes, 
                    // so that these disposes can happen at the same time as the mesh updates in finishMeshUpdates
                    using (new ProfilerMarker("CSG_FreeTemporaries").Auto())
                    {
                        for (int t = 0; t < treeUpdateLength; t++)
                        {
                            ref var treeUpdate = ref s_TreeUpdates[t];
                            treeUpdate.FreeTemporaries(ref finalJobHandle);
                            treeUpdate.Temporaries = default;
                        }
                        GeneratorJobPoolManager.Clear();
                    }
                    #endregion
                }
                return finalJobHandle;
            }

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

            public void Initialize()
            {
                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];

                // Make sure that if we, somehow, run this while parts of the previous update is still running, we wait for the previous run to complete
                chiselLookupValues.lastJobHandle.Complete();
                chiselLookupValues.lastJobHandle = default;

                // Reset everything
                JobHandles = default;
                Temporaries = default;

                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);

                Temporaries.parameterCounts                = new NativeArray<int>(chiselLookupValues.parameters.Length, defaultAllocator);
                Temporaries.transformTreeBrushIndicesList  = new NativeList<NodeOrderNodeID>(defaultAllocator);
                Temporaries.brushBoundsUpdateList          = new NativeList<NodeOrderNodeID>(defaultAllocator);
                Temporaries.nodes                          = new NativeList<CompactNodeID>(defaultAllocator);
                Temporaries.brushes                        = new NativeList<CompactNodeID>(defaultAllocator);

                compactHierarchy.GetTreeNodes(Temporaries.nodes, Temporaries.brushes);
                var newBrushCount = Temporaries.brushes.Length;
                this.brushCount   = newBrushCount;
                    
                #region Allocations/Resize
                chiselLookupValues.EnsureCapacity(newBrushCount);

                this.maxNodeOrder = this.brushCount;

                Temporaries.meshDataArray   = default;
                Temporaries.meshDatas       = new NativeList<UnityEngine.Mesh.MeshData>(defaultAllocator);

                Temporaries.brushesThatNeedIndirectUpdateHashMap = new NativeParallelHashSet<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(brushCount, defaultAllocator);

                // TODO: find actual vertex count
                Temporaries.outputSurfaceVertices           = new NativeList<float3>(65535 * 10, defaultAllocator);

                Temporaries.outputSurfaces                  = new NativeList<BrushIntersectionLoop>(brushCount * 16, defaultAllocator);
                Temporaries.brushIntersectionsWith          = new NativeList<BrushIntersectWith>(brushCount, defaultAllocator);

                Temporaries.nodeIDValueToNodeOrderOffsetRef = new NativeReference<int>(defaultAllocator);
                Temporaries.surfaceCountRef                 = new NativeReference<int>(defaultAllocator);
                Temporaries.compactTreeRef                  = new NativeReference<BlobAssetReference<CompactTree>>(defaultAllocator);
                Temporaries.needRemappingRef                = new NativeReference<bool>(defaultAllocator);

                Temporaries.uniqueBrushPairs                = new NativeList<BrushPair2>(brushCount * 256, defaultAllocator);

                Temporaries.rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allBrushMeshIDs                 = new NativeArray<int>(brushCount, defaultAllocator);
                Temporaries.brushRenderData                 = new NativeList<BrushData>(brushCount, defaultAllocator);
                Temporaries.allTreeBrushIndexOrders         = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allTreeBrushIndexOrders.Clear();
                Temporaries.allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);

                Temporaries.outputSurfacesRange             = new NativeArray<int2>(brushCount, defaultAllocator);
                Temporaries.brushIntersectionsWithRange     = new NativeArray<int2>(brushCount, defaultAllocator);
                Temporaries.nodeIDValueToNodeOrder          = new NativeList<int>(brushCount, defaultAllocator);
                Temporaries.brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(brushCount, defaultAllocator);

                Temporaries.brushBrushIntersections         = new NativeArray<UnsafeList<BrushIntersectWith>>(brushCount, defaultAllocator);

                Temporaries.subMeshCounts                   = new NativeList<SubMeshCounts>(defaultAllocator);

                Temporaries.meshUpdates                     = new NativeList<ChiselMeshUpdate>(defaultAllocator);
                Temporaries.colliderMeshUpdates             = new NativeList<ChiselMeshUpdate>(defaultAllocator);
                Temporaries.debugHelperMeshes               = new NativeList<ChiselMeshUpdate>(defaultAllocator);
                Temporaries.renderMeshes                    = new NativeList<ChiselMeshUpdate>(defaultAllocator);


                Temporaries.loopVerticesLookup              = new NativeArray<UnsafeList<float3>>(this.brushCount, defaultAllocator);

                Temporaries.vertexBufferContents.EnsureInitialized();

                // Regular index operator will return a copy instead of a reference *sigh*
                for (int l = 0; l < SurfaceLayers.ParameterCount; l++)
                {
                    var parameter = chiselLookupValues.parameters[l];
                    parameter.Clear();
                    chiselLookupValues.parameters[l] = parameter;
                }

                #region MeshQueries
                // TODO: have more control over the queries
                Temporaries.meshQueries         = MeshQuery.DefaultQueries.ToNativeArray(defaultAllocator);
                Temporaries.meshQueriesLength   = Temporaries.meshQueries.Length;
                Temporaries.meshQueries.Sort(meshQueryComparer);
                #endregion

                Temporaries.subMeshSurfaces = new NativeArray<UnsafeList<SubMeshSurface>>(Temporaries.meshQueriesLength, defaultAllocator);
                
                Temporaries.subMeshCounts.Clear();

                Temporaries.allUpdateBrushIndexOrders.Clear();
                if (Temporaries.allUpdateBrushIndexOrders.Capacity < this.brushCount)
                    Temporaries.allUpdateBrushIndexOrders.Capacity = this.brushCount;


                Temporaries.brushesThatNeedIndirectUpdateHashMap.Clear();
                Temporaries.brushesThatNeedIndirectUpdate.Clear();

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
                if (chiselLookupValues.brushTreeSpacePlaneCache.Length < newBrushCount)
                    chiselLookupValues.brushTreeSpacePlaneCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpaceBoundCache.Length < newBrushCount)
                    chiselLookupValues.brushTreeSpaceBoundCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushesTouchedByBrushCache.Length < newBrushCount)
                    chiselLookupValues.brushesTouchedByBrushCache.Resize(newBrushCount, NativeArrayOptions.ClearMemory);

                Temporaries.basePolygonDisposeList             = new NativeList<BlobAssetReference<BasePolygonsBlob>>(chiselLookupValues.basePolygonCache.Length, defaultAllocator);
                Temporaries.treeSpaceVerticesDisposeList       = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(chiselLookupValues.treeSpaceVerticesCache.Length, defaultAllocator);
                Temporaries.brushesTouchedByBrushDisposeList   = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(chiselLookupValues.brushesTouchedByBrushCache.Length, defaultAllocator);
                Temporaries.routingTableDisposeList            = new NativeList<BlobAssetReference<RoutingTable>>(chiselLookupValues.routingTableCache.Length, defaultAllocator);
                Temporaries.brushTreeSpacePlaneDisposeList     = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(chiselLookupValues.brushTreeSpacePlaneCache.Length, defaultAllocator);
                Temporaries.brushRenderBufferDisposeList       = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(chiselLookupValues.brushRenderBufferCache.Length, defaultAllocator);

                #endregion
            }

            public void RunMeshInitJobs(
                JobHandle dependsOn // TODO: we're not actually depending on this jobhandle?
                )
            {
                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeCompactNodeID);
                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobCache;
                {
                    #region Build Lookup Tables
                    using (new ProfilerMarker("Job_BuildLookupTablesJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var buildLookupTablesJob = new BuildLookupTablesJob
                        {
                            // Read
                            brushes                         = Temporaries.brushes,
                            brushCount                      = this.brushCount,

                            // Read/Write
                            nodeIDValueToNodeOrder          = Temporaries.nodeIDValueToNodeOrder,

                            // Write
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders
                        };
                        buildLookupTablesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.brushesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                ref JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle));
                    }
                    #endregion

                    #region CacheRemapping
                    using (new ProfilerMarker("Job_CacheRemappingJob").Auto())
                    {
                        const bool runInParallel = false;// runInParallelDefault;
                        // TODO: update "previous siblings" when something with an intersection operation has been modified
                        var cacheRemappingJob = new CacheRemappingJob
                        {
                            // Read
                            nodeIDValueToNodeOrder          = Temporaries.nodeIDValueToNodeOrder,
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,
                            brushes                         = Temporaries.brushes,
                            brushCount                      = this.brushCount,
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                            brushIDValues                   = chiselLookupValues.brushIDValues,
                            //compactHierarchy              = compactHierarchy, //<-- cannot do ref or pointer here
                                                                                //    so we set it below using InitializeHierarchy

                            // Read/Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache,
                            routingTableCache               = chiselLookupValues.routingTableCache,
                            transformationCache             = chiselLookupValues.transformationCache,
                            brushRenderBufferCache          = chiselLookupValues.brushRenderBufferCache,
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache,
                            brushTreeSpacePlaneCache        = chiselLookupValues.brushTreeSpacePlaneCache,
                            brushTreeSpaceBoundCache        = chiselLookupValues.brushTreeSpaceBoundCache,
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache,

                            // Write
                            brushesThatNeedIndirectUpdateHashMap    = Temporaries.brushesThatNeedIndirectUpdateHashMap,
                            needRemappingRef                        = Temporaries.needRemappingRef
                        };
                        cacheRemappingJob.InitializeHierarchy(ref compactHierarchy);
                        cacheRemappingJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                ref JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                ref JobHandles.brushesJobHandle,
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.brushIDValuesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.basePolygonCacheJobHandle,
                                ref JobHandles.routingTableCacheJobHandle,
                                ref JobHandles.transformationCacheJobHandle,
                                ref JobHandles.brushRenderBufferCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle,
                                ref JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                ref JobHandles.needRemappingRefJobHandle));
                    }
                    #endregion

                    #region Update BrushID Values
                    using (new ProfilerMarker("Job_UpdateBrushIDValuesJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var updateBrushIDValuesJob = new UpdateBrushIDValuesJob
                        {
                            // Read
                            brushes         = Temporaries.brushes,
                            brushCount      = this.brushCount,

                            // Read/Write
                            brushIDValues   = chiselLookupValues.brushIDValues
                        };
                        updateBrushIDValuesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.brushesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushIDValuesJobHandle));
                    }
                    #endregion

                    #region Find Modified Brushes
                    using (new ProfilerMarker("Job_FindModifiedBrushesJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        Temporaries.transformTreeBrushIndicesList.Clear();
                        Temporaries.brushBoundsUpdateList.Clear();
                        if (Temporaries.transformTreeBrushIndicesList.Capacity < this.brushCount)
                            Temporaries.transformTreeBrushIndicesList.SetCapacity(this.brushCount);
                        if (Temporaries.brushBoundsUpdateList.Capacity < this.brushCount)
                            Temporaries.brushBoundsUpdateList.SetCapacity(this.brushCount);
                        var findModifiedBrushesJob = new FindModifiedBrushesJob
                        {
                            // Read
                            brushes                         = Temporaries.brushes,
                            brushCount                      = this.brushCount,
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                            //ref compactHierarchy          = ref compactHierarchy, //<-- cannot do ref or pointer here
                                                                                    //    so we set it below using InitializeHierarchy

                            // Read/Write
                            rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,

                            // Write
                            transformTreeBrushIndicesList   = Temporaries.transformTreeBrushIndicesList.AsParallelWriter(),
                            brushBoundsUpdateList           = Temporaries.brushBoundsUpdateList.AsParallelWriter()
                        };
                        findModifiedBrushesJob.InitializeHierarchy(ref compactHierarchy);
                        var handle = findModifiedBrushesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.brushesJobHandle,
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.transformTreeBrushIndicesListJobHandle,
                                ref JobHandles.brushBoundsUpdateListJobHandle));
                        handle.Complete();
                    }
                    #endregion

                    #region Invalidate Brushes
                    using (new ProfilerMarker("Job_InvalidateBrushesJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var invalidateBrushesJob = new InvalidateBrushesJob
                        {
                            // Read
                            needRemappingRef                = Temporaries.needRemappingRef,
                            rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache,
                            brushes                         = Temporaries.brushes,
                            brushCount                      = this.brushCount,
                            nodeIDValueToNodeOrder          = Temporaries.nodeIDValueToNodeOrder,
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,
                            //compactHierarchy              = compactHierarchy, //<-- cannot do ref or pointer here
                                                                                //    so we set it below using InitializeHierarchy

                            // Write
                            brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap
                        };
                        invalidateBrushesJob.InitializeHierarchy(ref compactHierarchy);
                        invalidateBrushesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.needRemappingRefJobHandle,
                                ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle,
                                ref JobHandles.brushesJobHandle,
                                ref JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                ref JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle));
                    }
                    #endregion

                    #region Update BrushMesh IDs
                    using (new ProfilerMarker("Job_UpdateBrushMeshIDsJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var updateBrushMeshIDsJob = new UpdateBrushMeshIDsJob
                        {
                            // Read
                            brushMeshBlobs              = brushMeshBlobs,
                            brushCount                  = this.brushCount,
                            brushes                     = Temporaries.brushes,
                            //compactHierarchy          = compactHierarchy, //<-- cannot do ref or pointer here
                                                                            //    so we set it below using InitializeHierarchy

                            // Read / Write
                            allKnownBrushMeshIndices    = chiselLookupValues.allKnownBrushMeshIndices,
                            parameters                  = chiselLookupValues.parameters,
                            parameterCounts             = Temporaries.parameterCounts,

                            // Write
                            allBrushMeshIDs             = Temporaries.allBrushMeshIDs
                        };
                        updateBrushMeshIDsJob.InitializeHierarchy(ref compactHierarchy);
                        updateBrushMeshIDsJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.brushMeshBlobsLookupJobHandle,
                                ref JobHandles.brushesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.allKnownBrushMeshIndicesJobHandle,
                                ref JobHandles.parametersJobHandle,
                                ref JobHandles.parameterCountsJobHandle,
                                ref JobHandles.allBrushMeshIDsJobHandle));
                    }
                    #endregion
                }
            }

            public void RunMeshUpdateJobs()
            {
                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeCompactNodeID);
                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobCache;

                #region Perform CSG

                    #region Prepare

                    #region Update Transformations
                    using (new ProfilerMarker("Job_UpdateTransformationsJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var updateTransformationsJob = new UpdateTransformationsJob
                        {
                            // Read
                            transformTreeBrushIndicesList   = Temporaries.transformTreeBrushIndicesList,
                            //compactHierarchy              = compactHierarchy, //<-- cannot do ref or pointer here
                            //    so we set it below using InitializeHierarchy

                            // Write
                            transformationCache             = chiselLookupValues.transformationCache
                        };
                        updateTransformationsJob.InitializeHierarchy(ref compactHierarchy);
                        updateTransformationsJob.Schedule(runInParallel, Temporaries.transformTreeBrushIndicesList, 8,
                            new ReadJobHandles(
                                ref JobHandles.transformTreeBrushIndicesListJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.transformationCacheJobHandle));
                    }
                    #endregion

                    #region Build CSG Tree
                    using (new ProfilerMarker("Job_BuildCompactTreeJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var buildCompactTreeJob = new BuildCompactTreeJob
                        {
                            // Read
                            treeCompactNodeID   = this.treeCompactNodeID,
                            brushes             = Temporaries.brushes,
                            nodes               = Temporaries.nodes,
                            //compactHierarchy  = compactHierarchy,  //<-- cannot do ref or pointer here, 
                            //    so we set it below using InitializeHierarchy

                            // Write
                            compactTreeRef = Temporaries.compactTreeRef
                        };
                        buildCompactTreeJob.InitializeHierarchy(ref compactHierarchy);
                        buildCompactTreeJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.brushesJobHandle,
                                ref JobHandles.nodesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.compactTreeRefJobHandle));
                    }
                    #endregion

                    #region Update BrushMeshBlob Lookup table
                    // Create lookup table for all brushMeshBlobs, based on the node order in the tree
                    using (new ProfilerMarker("Job_FillBrushMeshBlobLookupJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var fillBrushMeshBlobLookupJob = new FillBrushMeshBlobLookupJob
                        {
                            // Read
                            brushMeshBlobs          = brushMeshBlobs,
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders,
                            allBrushMeshIDs         = Temporaries.allBrushMeshIDs,

                            // Write
                            brushMeshLookup = Temporaries.brushMeshLookup,
                            surfaceCountRef = Temporaries.surfaceCountRef
                        };
                        fillBrushMeshBlobLookupJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.brushMeshBlobsLookupJobHandle,
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.allBrushMeshIDsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushMeshLookupJobHandle,
                                ref JobHandles.surfaceCountRefJobHandle));
                    }
                    #endregion

                    #region Invalidate outdated caches
                    // Invalidate outdated caches for all modified brushes
                    using (new ProfilerMarker("Job_InvalidateBrushCacheJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var invalidateBrushCacheJob = new InvalidateBrushCacheJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders,

                            // Read/Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache,
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                            routingTableCache           = chiselLookupValues.routingTableCache,
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache,

                            // Write
                            basePolygonDisposeList              = Temporaries.basePolygonDisposeList.AsParallelWriter(),
                            treeSpaceVerticesDisposeList        = Temporaries.treeSpaceVerticesDisposeList.AsParallelWriter(),
                            brushesTouchedByBrushDisposeList    = Temporaries.brushesTouchedByBrushDisposeList.AsParallelWriter(),
                            routingTableDisposeList             = Temporaries.routingTableDisposeList.AsParallelWriter(),
                            brushTreeSpacePlaneDisposeList      = Temporaries.brushTreeSpacePlaneDisposeList.AsParallelWriter(),
                            brushRenderBufferDisposeList        = Temporaries.brushRenderBufferDisposeList.AsParallelWriter()
                        };
                        invalidateBrushCacheJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.basePolygonCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle,
                                ref JobHandles.routingTableCacheJobHandle,
                                ref JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                ref JobHandles.brushRenderBufferCacheJobHandle));
                    }
                    #endregion

                    #region Fixup brush cache data order
                    // Fix up brush order index in cache data (ordering of brushes may have changed)
                    using (new ProfilerMarker("Job_FixupBrushCacheIndicesJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var fixupBrushCacheIndicesJob = new FixupBrushCacheIndicesJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                            nodeIDValueToNodeOrder          = Temporaries.nodeIDValueToNodeOrder,
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,

                            // Read Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache,
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache
                        };
                        fixupBrushCacheIndicesJob.Schedule(runInParallel, Temporaries.allTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                ref JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.basePolygonCacheJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle));
                    }
                    #endregion

                    #region Update brush tree space vertices and bounds
                    // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been modified
                    using (new ProfilerMarker("Job_CreateTreeSpaceVerticesAndBoundsJob").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,
                            transformationCache             = chiselLookupValues.transformationCache,
                            brushMeshLookup                 = Temporaries.brushMeshLookup,
                            //ref hierarchyIDLookup         = ref CompactHierarchyManager.HierarchyIDLookup,    //<-- cannot do ref or pointer here
                            //    so we set it below using InitializeHierarchy

                            // Read/Write
                            hierarchyList                   = CompactHierarchyManager.HierarchyList.AsArray(),

                            // Write
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache,
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache
                        };
                        createTreeSpaceVerticesAndBoundsJob.InitializeLookups();
                        createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.transformationCacheJobHandle,
                                ref JobHandles.brushMeshBlobsLookupJobHandle,
                                ref JobHandles.hierarchyIDJobHandle,
                                ref JobHandles.brushMeshLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.hierarchyListJobHandle));
                    }
                    #endregion

                    #region Update intersection pairs
                    // Find all pairs of brushes that intersect, for those brushes that have been modified
                    using (new ProfilerMarker("Job_FindAllBrushIntersectionPairs").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                        // TODO: optimize, use hashed grid
                        var findAllBrushIntersectionPairsJob = new FindAllBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                            transformationCache             = chiselLookupValues.transformationCache,
                            brushMeshLookup                 = Temporaries.brushMeshLookup,
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache,
                            rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,

                            // Read / Write
                            allocator                       = defaultAllocator,
                            brushBrushIntersections         = Temporaries.brushBrushIntersections,

                            // Write
                            brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap.AsParallelWriter()
                        };
                        findAllBrushIntersectionPairsJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.transformationCacheJobHandle,
                                ref JobHandles.brushMeshLookupJobHandle,
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushBrushIntersectionsJobHandle,
                                ref JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle));
                    }
                    #endregion

                    #region Update list of brushes that touch brushes
                    // Find all brushes that touch the brushes that have been modified
                    using (new ProfilerMarker("Job_FindUniqueIndirectBrushIntersections").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: optimize, use hashed grid
                        var findUniqueIndirectBrushIntersectionsJob = new FindUniqueIndirectBrushIntersectionsJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap,

                            // Read / Write
                            brushesThatNeedIndirectUpdate = Temporaries.brushesThatNeedIndirectUpdate
                        };
                        findUniqueIndirectBrushIntersectionsJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                ref JobHandles.brushesThatNeedIndirectUpdateJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushesThatNeedIndirectUpdateJobHandle));
                    }
                    #endregion

                    #region Invalidate indirectly outdated caches (when brush touches a brush that has changed)
                    // Invalidate the cache for the brushes that have been indirectly modified (touch a brush that has changed)
                    using (new ProfilerMarker("Job_InvalidateBrushCache_Indirect").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var invalidateIndirectBrushCacheJob = new InvalidateIndirectBrushCacheJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdate = Temporaries.brushesThatNeedIndirectUpdate,

                            // Read/Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache,
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                            routingTableCache           = chiselLookupValues.routingTableCache,
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache
                        };
                        invalidateIndirectBrushCacheJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 16,
                            new ReadJobHandles(
                                ref JobHandles.brushesThatNeedIndirectUpdateJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.basePolygonCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle,
                                ref JobHandles.routingTableCacheJobHandle,
                                ref JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                ref JobHandles.brushRenderBufferCacheJobHandle));
                    }
                    #endregion

                    #region Fixup indirectly brush cache data order (when brush touches a brush that has changed)
                    // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been indirectly modified
                    using (new ProfilerMarker("Job_CreateTreeSpaceVerticesAndBounds_Indirect").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = Temporaries.brushesThatNeedIndirectUpdate,
                            transformationCache         = chiselLookupValues.transformationCache,
                            brushMeshLookup             = Temporaries.brushMeshLookup,
                            //ref hierarchyIDLookup     = ref CompactHierarchyManager.HierarchyIDLookup,    //<-- cannot do ref or pointer here
                            //    so we set it below using InitializeHierarchy

                            // Read/Write
                            hierarchyList               = CompactHierarchyManager.HierarchyList.AsArray(),


                            // Write
                            brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache,
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                        };
                        createTreeSpaceVerticesAndBoundsJob.InitializeLookups();
                        createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 16,
                            new ReadJobHandles(
                                //ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                ref JobHandles.transformationCacheJobHandle,
                                ref JobHandles.brushMeshLookupJobHandle,
                                //ref JobHandles.brushMeshBlobsLookupJobHandle,
                                ref JobHandles.hierarchyIDJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.hierarchyListJobHandle));
                    }
                    #endregion

                    #region Update intersection pairs (when brush touches a brush that has changed)
                    // Find all pairs of brushes that intersect, for those brushes that have been indirectly modified
                    using (new ProfilerMarker("Job_FindAllBrushIntersectionPairs_Indirect").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: optimize, use hashed grid
                        var findAllIndirectBrushIntersectionPairsJob = new FindAllIndirectBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                            transformationCache             = chiselLookupValues.transformationCache,
                            brushMeshLookup                 = Temporaries.brushMeshLookup,
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache,
                            brushesThatNeedIndirectUpdate   = Temporaries.brushesThatNeedIndirectUpdate,

                            // Read / Write
                            allocator                       = defaultAllocator,
                            brushBrushIntersections         = Temporaries.brushBrushIntersections
                        };
                        findAllIndirectBrushIntersectionPairsJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 1,
                            new ReadJobHandles(
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.transformationCacheJobHandle,
                                ref JobHandles.brushMeshLookupJobHandle,
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.brushesThatNeedIndirectUpdateJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushBrushIntersectionsJobHandle));
                    }
                    #endregion

                    #region Update list of brushes that touch brushes (when brush touches a brush that has changed)
                    // Add brushes that need to be indirectly updated to our list of brushes that need updates
                    using (new ProfilerMarker("Job_AddIndirectUpdatedBrushesToListAndSort").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var addIndirectUpdatedBrushesToListAndSortJob = new AddIndirectUpdatedBrushesToListAndSortJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders,
                            brushesThatNeedIndirectUpdate   = Temporaries.brushesThatNeedIndirectUpdate,
                            rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders,

                            // Write
                            allUpdateBrushIndexOrders       = Temporaries.allUpdateBrushIndexOrders.AsParallelWriter(),
                        };
                        addIndirectUpdatedBrushesToListAndSortJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle));
                    }
                    #endregion

                    #region Gather all brush intersections
                    // Gather all found pairs of brushes that intersect with each other and cache them
                    using (new ProfilerMarker("Job_GatherAndStoreBrushIntersections").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var gatherBrushIntersectionsJob = new GatherBrushIntersectionPairsJob
                        {
                            // Read
                            brushBrushIntersections     = Temporaries.brushBrushIntersections,

                            // Write
                            brushIntersectionsWithRange = Temporaries.brushIntersectionsWithRange,

                            // Read / Write
                            brushIntersectionsWith      = Temporaries.brushIntersectionsWith
                        };
                        gatherBrushIntersectionsJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.brushBrushIntersectionsJobHandle,
                                ref JobHandles.brushIntersectionsWithJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushIntersectionsWithJobHandle,
                                ref JobHandles.brushIntersectionsWithRangeJobHandle));

                        var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                        {
                            // Read
                            treeCompactNodeID           = treeCompactNodeID,
                            compactTreeRef              = Temporaries.compactTreeRef,
                            allTreeBrushIndexOrders     = Temporaries.allTreeBrushIndexOrders,
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,

                            brushIntersectionsWith      = Temporaries.brushIntersectionsWith,
                            brushIntersectionsWithRange = Temporaries.brushIntersectionsWithRange,

                            // Write
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache
                        };
                        storeBrushIntersectionsJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                            new ReadJobHandles(
                                ref JobHandles.compactTreeRefJobHandle,
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.brushIntersectionsWithJobHandle,
                                ref JobHandles.brushIntersectionsWithRangeJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle));
                    }
                    #endregion

                    #endregion

                    //
                    // Determine all surfaces and intersections
                    //

                    #region Determine Intersection Surfaces
                    // Find all pairs of brush intersections for each brush
                    using (new ProfilerMarker("Job_PrepareBrushPairIntersections").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var findBrushPairsJob = new FindBrushPairsJob
                        {
                            // Read
                            maxOrder                    = brushCount,
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache,

                            // Read (Re-allocate) / Write
                            uniqueBrushPairs            = Temporaries.uniqueBrushPairs
                        };
                        findBrushPairsJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.uniqueBrushPairsJobHandle));

                        NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.intersectingBrushesStream, Temporaries.uniqueBrushPairs,
                                                            new ReadJobHandles(
                                                                ref JobHandles.uniqueBrushPairsJobHandle
                                                                ),
                                                            new WriteJobHandles(
                                                                ref JobHandles.intersectingBrushesStreamJobHandle
                                                                ),
                                                            defaultAllocator);

                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
                            uniqueBrushPairs        = Temporaries.uniqueBrushPairs,
                            transformationCache     = chiselLookupValues.transformationCache,
                            brushMeshLookup         = Temporaries.brushMeshLookup,

                            // Write
                            intersectingBrushesStream = Temporaries.intersectingBrushesStream.AsWriter()
                        };
                        prepareBrushPairIntersectionsJob.Schedule(runInParallel, Temporaries.uniqueBrushPairs, 1,
                            new ReadJobHandles(
                                ref JobHandles.uniqueBrushPairsJobHandle,
                                ref JobHandles.transformationCacheJobHandle,
                                ref JobHandles.brushMeshLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.intersectingBrushesStreamJobHandle));
                    }

                    using (new ProfilerMarker("Job_GenerateBasePolygonLoops").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                            brushMeshLookup             = Temporaries.brushMeshLookup,
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,

                            // Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache
                        };
                        createBlobPolygonsBlobs.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle,
                                ref JobHandles.brushMeshLookupJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.basePolygonCacheJobHandle));
                    }

                    using (new ProfilerMarker("Job_UpdateBrushTreeSpacePlanes").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this at creation time + when moved / store with brush component itself
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            brushMeshLookup             = Temporaries.brushMeshLookup,
                            transformationCache         = chiselLookupValues.transformationCache,

                            // Write
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache
                        };
                        createBrushTreeSpacePlanesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.brushMeshLookupJobHandle,
                                ref JobHandles.transformationCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushTreeSpacePlaneCacheJobHandle));
                    }

                    using (new ProfilerMarker("Job_CreateIntersectionLoops").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        NativeCollection.ScheduleEnsureCapacity(runInParallel, ref Temporaries.outputSurfaces, Temporaries.surfaceCountRef,
                                                            new ReadJobHandles(
                                                                ref JobHandles.surfaceCountRefJobHandle),
                                                            new WriteJobHandles(
                                                                ref JobHandles.outputSurfacesJobHandle),
                                                            defaultAllocator);

                        var createIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Needed for count (forced & unused)
                            uniqueBrushPairs            = Temporaries.uniqueBrushPairs,

                            // Read
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                            intersectingBrushesStream   = Temporaries.intersectingBrushesStream.AsReader(),

                            // Write
                            outputSurfaceVertices       = Temporaries.outputSurfaceVertices.AsParallelWriterExt(),
                            outputSurfaces              = Temporaries.outputSurfaces.AsParallelWriter()
                        };
                        var currentJobHandle = createIntersectionLoopsJob.Schedule(runInParallel, Temporaries.uniqueBrushPairs, 8,
                            new ReadJobHandles(
                                ref JobHandles.uniqueBrushPairsJobHandle,
                                ref JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.intersectingBrushesStreamJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.outputSurfaceVerticesJobHandle,
                                ref JobHandles.outputSurfacesJobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref Temporaries.intersectingBrushesStream, currentJobHandle);
                    }

                    using (new ProfilerMarker("Job_GatherOutputSurfaces").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                        {
                            // Read / Write (Sort)
                            outputSurfaces      = Temporaries.outputSurfaces,

                            // Write
                            outputSurfacesRange = Temporaries.outputSurfacesRange
                        };
                        gatherOutputSurfacesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.outputSurfacesJobHandle), // TODO: support not having any read-handles
                            new WriteJobHandles(
                                ref JobHandles.outputSurfacesJobHandle,
                                ref JobHandles.outputSurfacesRangeJobHandle));
                    }

                    using (new ProfilerMarker("Job_FindLoopOverlapIntersections").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.dataStream1, Temporaries.allUpdateBrushIndexOrders,
                                                            new ReadJobHandles(
                                                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle
                                                                ),
                                                            new WriteJobHandles(
                                                                ref JobHandles.dataStream1JobHandle
                                                                ),
                                                            defaultAllocator);

                        var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            outputSurfaceVertices       = Temporaries.outputSurfaceVertices,
                            outputSurfaces              = Temporaries.outputSurfaces,
                            outputSurfacesRange         = Temporaries.outputSurfacesRange,
                            maxNodeOrder                = maxNodeOrder,
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                            basePolygonCache            = chiselLookupValues.basePolygonCache,

                            // Read Write
                            allocator                   = defaultAllocator,
                            loopVerticesLookup          = Temporaries.loopVerticesLookup,

                            // Write
                            output = Temporaries.dataStream1.AsWriter()
                        };
                        findLoopOverlapIntersectionsJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.outputSurfaceVerticesJobHandle,
                                ref JobHandles.outputSurfacesJobHandle,
                                ref JobHandles.outputSurfacesRangeJobHandle,
                                ref JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                ref JobHandles.basePolygonCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.loopVerticesLookupJobHandle,
                                ref JobHandles.dataStream1JobHandle));
                    }
                    #endregion

                    //
                    // Ensure vertices that should be identical on different brushes, ARE actually identical
                    //

                    #region Merge vertices
                    using (new ProfilerMarker("Job_MergeTouchingBrushVerticesIndirect").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only try to merge the vertices beyond the original mesh vertices (the intersection vertices)
                        //       should also try to limit vertices to those that are on the same surfaces (somehow)
                        var mergeTouchingBrushVerticesIndirectJob = new MergeTouchingBrushVerticesIndirectJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache,

                            // Read Write
                            loopVerticesLookup          = Temporaries.loopVerticesLookup,
                        };
                        mergeTouchingBrushVerticesIndirectJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.loopVerticesLookupJobHandle));
                    }
                    #endregion

                    //
                    // Perform CSG on prepared surfaces, giving each surface a categorization
                    //

                    #region Perform CSG     
                    using (new ProfilerMarker("Job_UpdateBrushCategorizationTables").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: only update when brush or any touching brush has been added/removed or changes operation/order                    
                        // TODO: determine when a brush is completely inside another brush (might not have *any* intersection loops)
                        var createRoutingTableJob = new CreateRoutingTableJob // Build categorization trees for brushes
                        {
                            // Read
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache,
                            compactTreeRef              = Temporaries.compactTreeRef,

                            // Write
                            routingTableLookup          = chiselLookupValues.routingTableCache
                        };
                        createRoutingTableJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle,
                                ref JobHandles.compactTreeRefJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.routingTableCacheJobHandle));
                    }

                    using (new ProfilerMarker("Job_PerformCSG").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.dataStream2, Temporaries.allUpdateBrushIndexOrders,
                                                            new ReadJobHandles(
                                                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle
                                                                ),
                                                            new WriteJobHandles(
                                                                ref JobHandles.dataStream2JobHandle
                                                                ),
                                                            defaultAllocator);

                        // Perform CSG
                        var performCSGJob = new PerformCSGJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            routingTableCache           = chiselLookupValues.routingTableCache,
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                            loopVerticesLookup          = Temporaries.loopVerticesLookup,
                            input                       = Temporaries.dataStream1.AsReader(),

                            // Write
                            output                      = Temporaries.dataStream2.AsWriter(),
                        };
                        var currentJobHandle = performCSGJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.routingTableCacheJobHandle,
                                ref JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle,
                                ref JobHandles.dataStream1JobHandle,
                                ref JobHandles.loopVerticesLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.dataStream2JobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref Temporaries.dataStream1, currentJobHandle);
                    }
                    #endregion

                    //
                    // Triangulate the surfaces
                    //

                    #region Triangulate Surfaces
                    using (new ProfilerMarker("Job_GenerateSurfaceTriangles").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var generateSurfaceTrianglesJob = new GenerateSurfaceTrianglesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                            basePolygonCache            = chiselLookupValues.basePolygonCache,
                            transformationCache         = chiselLookupValues.transformationCache,
                            input                       = Temporaries.dataStream2.AsReader(),
                            meshQueries                 = Temporaries.meshQueries,

                            // Write
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache
                        };
                        var currentJobHandle = generateSurfaceTrianglesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.basePolygonCacheJobHandle,
                                ref JobHandles.transformationCacheJobHandle,
                                ref JobHandles.dataStream2JobHandle,
                                ref JobHandles.meshQueriesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushRenderBufferCacheJobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref Temporaries.dataStream2, currentJobHandle);
                    }
                    #endregion


                    //
                    // Create meshes out of ALL the generated AND cached surfaces
                    //

                    #region Find all generated brush specific geometry
                    using (new ProfilerMarker("Job_FindBrushRenderBuffers").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        NativeCollection.ScheduleEnsureCapacity(runInParallel, ref Temporaries.brushRenderData, Temporaries.allTreeBrushIndexOrders,
                                                            new ReadJobHandles(
                                                                ref JobHandles.allTreeBrushIndexOrdersJobHandle),
                                                            new WriteJobHandles(
                                                                ref JobHandles.brushRenderDataJobHandle),
                                                            defaultAllocator);

                        var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                        {
                            // Read
                            meshQueryLength         = Temporaries.meshQueriesLength,
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders,
                            brushRenderBufferCache  = chiselLookupValues.brushRenderBufferCache,

                            // Write
                            brushRenderData = Temporaries.brushRenderData.AsParallelWriter()
                        };
                        findBrushRenderBuffersJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.meshQueriesJobHandle,
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.brushRenderBufferCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushRenderDataJobHandle
                                ));
                    }
                    #endregion

                    #region Allocate sub meshes
                    using (new ProfilerMarker("Job_AllocateSubMeshes").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var allocateSubMeshesJob = new AllocateSubMeshesJob
                        {
                            // Read
                            meshQueryLength = Temporaries.meshQueriesLength,
                            surfaceCountRef = Temporaries.surfaceCountRef,

                            // Read/Write
                            subMeshCounts   = Temporaries.subMeshCounts,
                            subMeshSections = Temporaries.vertexBufferContents.subMeshSections,
                        };
                        allocateSubMeshesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.meshQueriesJobHandle,
                                ref JobHandles.surfaceCountRefJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.subMeshCountsJobHandle,
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle));
                    }
                    #endregion

                    #region Prepare sub sections
                    using (new ProfilerMarker("Job_PrepareSubSections").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var prepareSubSectionsJob = new PrepareSubSectionsJob
                        {
                            // Read
                            meshQueries     = Temporaries.meshQueries,
                            brushRenderData = Temporaries.brushRenderData,

                            // Write
                            allocator       = defaultAllocator,
                            subMeshSurfaces = Temporaries.subMeshSurfaces,
                        };
                        prepareSubSectionsJob.Schedule(runInParallel, Temporaries.meshQueriesLength, 1,
                            new ReadJobHandles(
                                ref JobHandles.meshQueriesJobHandle,
                                ref JobHandles.brushRenderDataJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.subMeshSurfacesJobHandle));
                    }
                    #endregion

                    #region Sort surfaces
                    using (new ProfilerMarker("Job_SortSurfaces").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var sortSurfacesParallelJob = new SortSurfacesParallelJob
                        {
                            // Read
                            meshQueries     = Temporaries.meshQueries,
                            subMeshSurfaces = Temporaries.subMeshSurfaces,

                            // Write
                            subMeshCounts   = Temporaries.subMeshCounts
                        };
                        sortSurfacesParallelJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.meshQueriesJobHandle,
                                ref JobHandles.subMeshSurfacesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.subMeshCountsJobHandle));

                        var gatherSurfacesJob = new GatherSurfacesJob
                        {
                            // Read / Write
                            subMeshCounts = Temporaries.subMeshCounts,

                            // Write
                            subMeshSections = Temporaries.vertexBufferContents.subMeshSections,
                        };
                        gatherSurfacesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.subMeshCountsJobHandle), // TODO: Can't do empty ReadJobHandles, fix this
                            new WriteJobHandles(
                                ref JobHandles.subMeshCountsJobHandle,
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle));
                    }
                    #endregion

                    #region Allocate vertex buffers
                    using (new ProfilerMarker("Job_AllocateVertexBuffers").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                        {
                            // Read
                            subMeshSections         = Temporaries.vertexBufferContents.subMeshSections,

                            // Read Write
                            allocator               = defaultAllocator,
                            triangleBrushIndices    = Temporaries.vertexBufferContents.triangleBrushIndices
                        };
                        allocateVertexBuffersJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle));
                    }
                    #endregion

                    #region Generate mesh descriptions
                    using (new ProfilerMarker("Job_GenerateMeshDescription").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                        {
                            // Read
                            subMeshCounts       = Temporaries.subMeshCounts,

                            // Read Write
                            meshDescriptions    = Temporaries.vertexBufferContents.meshDescriptions
                        };
                        generateMeshDescriptionJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.subMeshCountsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_meshDescriptionsJobHandle));
                    }
                    #endregion


                    // TODO: store parameterCounts per brush (precalculated), manage these counts in the hierarchy when brushes are added/removed/modified
                    //       then we don't need to count them here & don't need to do a "complete" here
                    JobHandles.parameterCountsJobHandle.Complete();
                    JobHandles.parameterCountsJobHandle = default;

                    #region Create Meshes
                    using (new ProfilerMarker("Mesh.AllocateWritableMeshData").Auto())
                    {
                        var meshAllocations = 0;
                        for (int m = 0; m < Temporaries.meshQueries.Length; m++)
                        {
                            var meshQuery = Temporaries.meshQueries[m];
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
                                meshAllocations += Temporaries.parameterCounts[SurfaceLayers.kColliderLayer];
                            } else
                                meshAllocations++;
                        }

                        Temporaries.meshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(meshAllocations);

                        for (int i = 0; i < meshAllocations; i++)
                            Temporaries.meshDatas.Add(Temporaries.meshDataArray[i]);
                    }

                    using (new ProfilerMarker("Job_CopyToMeshes").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var assignMeshesJob = new AssignMeshesJob
                        {
                            // Read
                            meshDescriptions    = Temporaries.vertexBufferContents.meshDescriptions,
                            subMeshSections     = Temporaries.vertexBufferContents.subMeshSections,
                            meshDatas           = Temporaries.meshDatas,

                            // Write
                            meshes              = Temporaries.vertexBufferContents.meshes,
                            debugHelperMeshes   = Temporaries.debugHelperMeshes,
                            renderMeshes        = Temporaries.renderMeshes,

                            // Read / Write (allcoate)
                            colliderMeshUpdates = Temporaries.colliderMeshUpdates,
                            meshUpdates         = Temporaries.meshUpdates,
                        };
                        assignMeshesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.vertexBufferContents_meshDescriptionsJobHandle,
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                ref JobHandles.meshDatasJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_meshesJobHandle,
                                ref JobHandles.debugHelperMeshesJobHandle,
                                ref JobHandles.renderMeshesJobHandle,
                                ref JobHandles.meshUpdatesJobHandle,
                                ref JobHandles.colliderMeshUpdatesJobHandle));

                        var copyToMeshJob = new CopyToMeshJob
                        {
                            // Read
                            subMeshSections     = Temporaries.vertexBufferContents.subMeshSections,
                            subMeshCounts       = Temporaries.subMeshCounts,
                            subMeshSurfaces     = Temporaries.subMeshSurfaces,
                            colliderDescriptors = Temporaries.vertexBufferContents.colliderDescriptors,
                            renderDescriptors   = Temporaries.vertexBufferContents.renderDescriptors,
                            meshUpdates         = Temporaries.meshUpdates,

                            // Read/Write
                            meshes              = Temporaries.vertexBufferContents.meshes,
                        };
                        copyToMeshJob.Schedule(runInParallel, Temporaries.meshUpdates, 1,
                            new ReadJobHandles(
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                ref JobHandles.subMeshCountsJobHandle,
                                ref JobHandles.subMeshSurfacesJobHandle,
                                ref JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                ref JobHandles.vertexBufferContents_colliderDescriptorsJobHandle,
                                ref JobHandles.meshUpdatesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_meshesJobHandle));

                        var renderTriangleBrushIndicesJob = new FindTriangleBrushIndicesJob
                        {
                            // Read
                            subMeshSections         = Temporaries.vertexBufferContents.subMeshSections,
                            subMeshCounts           = Temporaries.subMeshCounts,
                            subMeshSurfaces         = Temporaries.subMeshSurfaces,
                            meshUpdates             = Temporaries.meshUpdates,

                            // Read/Write
                            triangleBrushIndices    = Temporaries.vertexBufferContents.triangleBrushIndices,
                        };
                        renderTriangleBrushIndicesJob.Schedule(runInParallel, Temporaries.meshUpdates, 1,
                            new ReadJobHandles(
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                ref JobHandles.subMeshCountsJobHandle,
                                ref JobHandles.subMeshSurfacesJobHandle,
                                ref JobHandles.renderMeshesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle
                                ));
                    /*
                        var helperTriangleBrushIndicesJob = new FindTriangleBrushIndicesJob
                        {
                            // Read
                            subMeshSections         = Temporaries.vertexBufferContents.subMeshSections,
                            subMeshCounts           = Temporaries.subMeshCounts,
                            subMeshSurfaces         = Temporaries.subMeshSurfaces,
                            meshUpdates             = Temporaries.debugHelperMeshes,

                            // Read/Write
                            triangleBrushIndices    = Temporaries.vertexBufferContents.triangleBrushIndices,
                        };
                        helperTriangleBrushIndicesJob.Schedule(runInParallel, Temporaries.debugHelperMeshes, 1,
                            new ReadJobHandles(
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                ref JobHandles.subMeshCountsJobHandle,
                                ref JobHandles.subMeshSurfacesJobHandle,
                                ref JobHandles.renderMeshesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle));
                        */
                    }
                    #endregion

                    //
                    // Finally store the generated surfaces into our cache
                    //

                    #region Store cached values back into cache (by node Index)
                    using (new ProfilerMarker("Job_StoreToCache").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var storeToCacheJob = new StoreToCacheJob
                        {
                            // Read
                            allTreeBrushIndexOrders     = Temporaries.allTreeBrushIndexOrders,
                            brushTreeSpaceBoundCache    = chiselLookupValues.brushTreeSpaceBoundCache,
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache,

                            // Read Write
                            brushTreeSpaceBoundLookup   = chiselLookupValues.brushTreeSpaceBoundLookup,
                            brushRenderBufferLookup     = chiselLookupValues.brushRenderBufferLookup
                        };
                        storeToCacheJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.brushRenderBufferCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.storeToCacheJobHandle));
                    }
                    #endregion

                    #region Create wireframes for all new/modified brushes
                    using (new ProfilerMarker("Job_UpdateBrushOutline").Auto())
                    {
                        const bool runInParallel = runInParallelDefault;
                        var updateBrushOutlineJob = new UpdateBrushOutlineJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders,
                            brushMeshBlobs = brushMeshBlobs

                            // Write
                            //compactHierarchy          = compactHierarchy,  //<-- cannot do ref or pointer here
                            //    so we set it below using InitializeHierarchy
                        };
                        updateBrushOutlineJob.InitializeHierarchy(ref compactHierarchy);
                        updateBrushOutlineJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                ref JobHandles.brushMeshBlobsLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.compactTreeRefJobHandle));
                    }
                    #endregion

                    #endregion
            }

            public JobHandle PreMeshUpdateDispose()
            {
                var dependencies = JobHandleExtensions.CombineDependencies(
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.allBrushMeshIDsJobHandle.writeBarrier,
                                                    JobHandles.allUpdateBrushIndexOrdersJobHandle.writeBarrier,
                                                    JobHandles.brushIDValuesJobHandle.writeBarrier,
                                                    JobHandles.basePolygonCacheJobHandle.writeBarrier,
                                                    JobHandles.brushBrushIntersectionsJobHandle.writeBarrier,
                                                    JobHandles.brushesTouchedByBrushCacheJobHandle.writeBarrier,
                                                    JobHandles.brushRenderBufferCacheJobHandle.writeBarrier,
                                                    JobHandles.brushRenderDataJobHandle.writeBarrier,
                                                    JobHandles.brushTreeSpacePlaneCacheJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.brushMeshBlobsLookupJobHandle.writeBarrier,
                                                    JobHandles.hierarchyIDJobHandle.writeBarrier,
                                                    JobHandles.hierarchyListJobHandle.writeBarrier,
                                                    JobHandles.brushMeshLookupJobHandle.writeBarrier,
                                                    JobHandles.brushIntersectionsWithJobHandle.writeBarrier,
                                                    JobHandles.brushIntersectionsWithRangeJobHandle.writeBarrier,
                                                    JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle.writeBarrier,
                                                    JobHandles.brushesThatNeedIndirectUpdateJobHandle.writeBarrier,
                                                    JobHandles.brushTreeSpaceBoundCacheJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.dataStream1JobHandle.writeBarrier,
                                                    JobHandles.dataStream2JobHandle.writeBarrier,
                                                    JobHandles.intersectingBrushesStreamJobHandle.writeBarrier,
                                                    JobHandles.loopVerticesLookupJobHandle.writeBarrier,
                                                    JobHandles.meshQueriesJobHandle.writeBarrier,
                                                    JobHandles.nodeIDValueToNodeOrderArrayJobHandle.writeBarrier,
                                                    JobHandles.outputSurfaceVerticesJobHandle.writeBarrier,
                                                    JobHandles.outputSurfacesJobHandle.writeBarrier,
                                                    JobHandles.outputSurfacesRangeJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.routingTableCacheJobHandle.writeBarrier,
                                                    JobHandles.rebuildTreeBrushIndexOrdersJobHandle.writeBarrier,
                                                    JobHandles.sectionsJobHandle.writeBarrier,
                                                    JobHandles.subMeshSurfacesJobHandle.writeBarrier,
                                                    JobHandles.subMeshCountsJobHandle.writeBarrier,
                                                    JobHandles.treeSpaceVerticesCacheJobHandle.writeBarrier,
                                                    JobHandles.transformationCacheJobHandle.writeBarrier,
                                                    JobHandles.uniqueBrushPairsJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.brushesJobHandle.writeBarrier,
                                                    JobHandles.nodesJobHandle.writeBarrier, 
                                                    JobHandles.parametersJobHandle.writeBarrier,
                                                    JobHandles.allKnownBrushMeshIndicesJobHandle.writeBarrier,
                                                    JobHandles.parameterCountsJobHandle.writeBarrier,
                                                    JobHandles.storeToCacheJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.surfaceCountRefJobHandle.writeBarrier,
                                                    JobHandles.compactTreeRefJobHandle.writeBarrier,
                                                    JobHandles.needRemappingRefJobHandle.writeBarrier,
                                                    JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle.writeBarrier,
                                                    JobHandles.transformTreeBrushIndicesListJobHandle.writeBarrier,
                                                    JobHandles.brushBoundsUpdateListJobHandle.writeBarrier)
                                            );

                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                var lastJobHandle = dependencies;

                lastJobHandle.AddDependency(Temporaries.brushMeshLookup              .SafeDispose(JobHandles.brushMeshLookupJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushIntersectionsWithRange  .SafeDispose(JobHandles.brushIntersectionsWithRangeJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushIntersectionsWith       .SafeDispose(JobHandles.brushIntersectionsWithJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.outputSurfaceVertices        .SafeDispose(JobHandles.outputSurfaceVerticesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.outputSurfacesRange          .SafeDispose(JobHandles.outputSurfacesRangeJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.outputSurfaces               .SafeDispose(JobHandles.outputSurfacesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.parameterCounts              .SafeDispose(JobHandles.parameterCountsJobHandle.readWriteBarrier));
                
                lastJobHandle.AddDependency(Temporaries.transformTreeBrushIndicesList.SafeDispose(JobHandles.transformTreeBrushIndicesListJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushBoundsUpdateList        .SafeDispose(JobHandles.brushBoundsUpdateListJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushes                      .SafeDispose(JobHandles.brushesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.nodes                        .SafeDispose(JobHandles.nodesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushRenderData              .SafeDispose(JobHandles.brushRenderDataJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.rebuildTreeBrushIndexOrders  .SafeDispose(JobHandles.rebuildTreeBrushIndexOrdersJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.allUpdateBrushIndexOrders    .SafeDispose(JobHandles.allUpdateBrushIndexOrdersJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.allBrushMeshIDs              .SafeDispose(JobHandles.allBrushMeshIDsJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.uniqueBrushPairs             .SafeDispose(JobHandles.uniqueBrushPairsJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.brushesThatNeedIndirectUpdate.SafeDispose(JobHandles.brushesThatNeedIndirectUpdateJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.nodeIDValueToNodeOrder       .SafeDispose(JobHandles.nodeIDValueToNodeOrderArrayJobHandle.readWriteBarrier));
                
                lastJobHandle.AddDependency(Temporaries.brushesThatNeedIndirectUpdateHashMap.Dispose(JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle.readWriteBarrier));
                
                
                // Note: cannot use "IsCreated" on this job, for some reason it won't be scheduled and then complain that it's leaking? Bug in IsCreated?
                lastJobHandle.AddDependency(Temporaries.meshQueries.SafeDispose(JobHandles.meshQueriesJobHandle.readWriteBarrier));


                lastJobHandle.AddDependency(Temporaries.brushBrushIntersections         .DisposeDeep(JobHandles.brushBrushIntersectionsJobHandle.readWriteBarrier),
                                            Temporaries.loopVerticesLookup              .DisposeDeep(JobHandles.loopVerticesLookupJobHandle.readWriteBarrier),
                                            Temporaries.basePolygonDisposeList          .DisposeDeep(JobHandles.basePolygonCacheJobHandle.readWriteBarrier),
                                            Temporaries.treeSpaceVerticesDisposeList    .DisposeDeep(JobHandles.treeSpaceVerticesCacheJobHandle.readWriteBarrier),
                                            Temporaries.brushesTouchedByBrushDisposeList.DisposeDeep(JobHandles.brushesTouchedByBrushCacheJobHandle.readWriteBarrier),
                                            Temporaries.routingTableDisposeList         .DisposeDeep(JobHandles.routingTableCacheJobHandle.readWriteBarrier),
                                            Temporaries.brushTreeSpacePlaneDisposeList  .DisposeDeep(JobHandles.brushTreeSpacePlaneCacheJobHandle.readWriteBarrier),
                                            Temporaries.brushRenderBufferDisposeList    .DisposeDeep(JobHandles.brushRenderBufferCacheJobHandle.readWriteBarrier));
                

                lastJobHandle.AddDependency(Temporaries.surfaceCountRef                 .Dispose(JobHandles.surfaceCountRefJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.needRemappingRef                .Dispose(JobHandles.needRemappingRefJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.nodeIDValueToNodeOrderOffsetRef .Dispose(JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.compactTreeRef                  .DisposeBlobDeep(JobHandles.compactTreeRefJobHandle.readWriteBarrier));

                chiselLookupValues.lastJobHandle = lastJobHandle;
                return lastJobHandle;
            }

            public JobHandle FreeTemporaries(ref JobHandle finalJobHandle)
            {
                // Combine all JobHandles of all jobs to ensure that we wait for ALL of them to finish 
                // before we dispose of our temporaries.
                // Eventually we might want to put this in between other jobs, but for now this is safer
                // to work with while things are still being re-arranged.
                var dependencies = JobHandleExtensions.CombineDependencies(
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.allBrushMeshIDsJobHandle.writeBarrier,
                                                    JobHandles.allUpdateBrushIndexOrdersJobHandle.writeBarrier,
                                                    JobHandles.brushIDValuesJobHandle.writeBarrier,
                                                    JobHandles.basePolygonCacheJobHandle.writeBarrier,
                                                    JobHandles.brushBrushIntersectionsJobHandle.writeBarrier,
                                                    JobHandles.brushesTouchedByBrushCacheJobHandle.writeBarrier,
                                                    JobHandles.brushRenderBufferCacheJobHandle.writeBarrier,
                                                    JobHandles.brushRenderDataJobHandle.writeBarrier,
                                                    JobHandles.brushTreeSpacePlaneCacheJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.brushMeshBlobsLookupJobHandle.writeBarrier,
                                                    JobHandles.hierarchyIDJobHandle.writeBarrier,
                                                    JobHandles.hierarchyListJobHandle.writeBarrier,
                                                    JobHandles.brushMeshLookupJobHandle.writeBarrier,
                                                    JobHandles.brushIntersectionsWithJobHandle.writeBarrier,
                                                    JobHandles.brushIntersectionsWithRangeJobHandle.writeBarrier,
                                                    JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle.writeBarrier,
                                                    JobHandles.brushesThatNeedIndirectUpdateJobHandle.writeBarrier,
                                                    JobHandles.brushTreeSpaceBoundCacheJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.dataStream1JobHandle.writeBarrier,
                                                    JobHandles.dataStream2JobHandle.writeBarrier,
                                                    JobHandles.intersectingBrushesStreamJobHandle.writeBarrier,
                                                    JobHandles.loopVerticesLookupJobHandle.writeBarrier,
                                                    JobHandles.meshQueriesJobHandle.writeBarrier,
                                                    JobHandles.nodeIDValueToNodeOrderArrayJobHandle.writeBarrier,
                                                    JobHandles.outputSurfaceVerticesJobHandle.writeBarrier,
                                                    JobHandles.outputSurfacesJobHandle.writeBarrier,
                                                    JobHandles.outputSurfacesRangeJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.routingTableCacheJobHandle.writeBarrier,
                                                    JobHandles.rebuildTreeBrushIndexOrdersJobHandle.writeBarrier,
                                                    JobHandles.sectionsJobHandle.writeBarrier,
                                                    JobHandles.subMeshSurfacesJobHandle.writeBarrier,
                                                    JobHandles.subMeshCountsJobHandle.writeBarrier,
                                                    JobHandles.treeSpaceVerticesCacheJobHandle.writeBarrier,
                                                    JobHandles.transformationCacheJobHandle.writeBarrier,
                                                    JobHandles.uniqueBrushPairsJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.transformTreeBrushIndicesListJobHandle.writeBarrier,
                                                    JobHandles.brushBoundsUpdateListJobHandle.writeBarrier,
                                                    //JobHandles.brushesJobHandle.writeBarrier,
                                                    JobHandles.nodesJobHandle.writeBarrier,
                                                    JobHandles.parametersJobHandle.writeBarrier,
                                                    JobHandles.allKnownBrushMeshIndicesJobHandle.writeBarrier,
                                                    JobHandles.parameterCountsJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.storeToCacheJobHandle.writeBarrier,

                                                    JobHandles.allTreeBrushIndexOrdersJobHandle.writeBarrier,
                                                    JobHandles.meshUpdatesJobHandle.writeBarrier,
                                                    JobHandles.colliderMeshUpdatesJobHandle.writeBarrier,
                                                    JobHandles.debugHelperMeshesJobHandle.writeBarrier,
                                                    JobHandles.renderMeshesJobHandle.writeBarrier,
                                                    JobHandles.surfaceCountRefJobHandle.writeBarrier,
                                                    JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle.writeBarrier),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.vertexBufferContents_renderDescriptorsJobHandle.writeBarrier,
                                                    JobHandles.vertexBufferContents_colliderDescriptorsJobHandle.writeBarrier,
                                                    JobHandles.vertexBufferContents_subMeshSectionsJobHandle.writeBarrier,
                                                    JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle.writeBarrier,
                                                    JobHandles.vertexBufferContents_meshDescriptionsJobHandle.writeBarrier,
                                                    JobHandles.vertexBufferContents_meshesJobHandle.writeBarrier,
                                                    JobHandles.compactTreeRefJobHandle.writeBarrier,
                                                    JobHandles.needRemappingRefJobHandle.writeBarrier,
                                                    JobHandles.meshDatasJobHandle.writeBarrier)
                                        );

                // Technically not necessary, but Unity will complain about memory leaks that aren't there (jobs just haven't finished yet)
                // TODO: see if we can use domain reload events to ensure this job is completed before a domain reload occurs
                dependencies.Complete(); 
                                            

                // We let the final JobHandle dependend on the dependencies, but not on the disposal, 
                // because we do not need to wait for the disposal of native collections do use our generated data
                finalJobHandle.AddDependency(dependencies);


                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                var lastJobHandle = chiselLookupValues.lastJobHandle;
                lastJobHandle.AddDependency(Temporaries.subMeshSurfaces         .DisposeDeep(JobHandles.subMeshSurfacesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.allTreeBrushIndexOrders .SafeDispose(JobHandles.allTreeBrushIndexOrdersJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.meshUpdates             .SafeDispose(JobHandles.meshUpdatesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.colliderMeshUpdates     .SafeDispose(JobHandles.colliderMeshUpdatesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.debugHelperMeshes       .SafeDispose(JobHandles.debugHelperMeshesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.renderMeshes            .SafeDispose(JobHandles.renderMeshesJobHandle.readWriteBarrier));
                lastJobHandle.AddDependency(Temporaries.meshDatas               .SafeDispose(JobHandles.meshDatasJobHandle.readWriteBarrier));                
                lastJobHandle.AddDependency(Temporaries.subMeshCounts           .SafeDispose(JobHandles.subMeshCountsJobHandle.readWriteBarrier));
                
                var vertexbufferContentsJobHandle = JobHandleExtensions.CombineDependencies(
                                                            JobHandles.vertexBufferContents_renderDescriptorsJobHandle.readWriteBarrier,
                                                            JobHandles.vertexBufferContents_colliderDescriptorsJobHandle.readWriteBarrier,
                                                            JobHandles.vertexBufferContents_subMeshSectionsJobHandle.readWriteBarrier,
                                                            JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle.readWriteBarrier,
                                                            JobHandles.vertexBufferContents_meshDescriptionsJobHandle.readWriteBarrier,
                                                            JobHandles.vertexBufferContents_meshesJobHandle.readWriteBarrier);

                lastJobHandle.AddDependency(Temporaries.vertexBufferContents    .Dispose(vertexbufferContentsJobHandle));

                lastJobHandle.AddDependency(dependencies);
                
                chiselLookupValues.lastJobHandle = lastJobHandle;
                return lastJobHandle;
            }
        }
    }
}
