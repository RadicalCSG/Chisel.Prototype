using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using Profiler = UnityEngine.Profiling.Profiler;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace Chisel.Core
{
    struct ProfilerSample : IDisposable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProfilerSample(string name) { Profiler.BeginSample(name); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { Profiler.EndSample(); }
    }

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

    partial class CompactHierarchyManager
    {
        const bool runInParallelDefault = true;

        #region Update / Rebuild
        internal static bool UpdateAllTreeMeshes(FinishMeshUpdate finishMeshUpdates, out JobHandle allTrees)
        {
            allTrees = default(JobHandle);
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


            using (var profilerSample = new ProfilerSample("UpdateTreeMeshes"))
            {
                allTrees = TreeUpdate.ScheduleTreeMeshJobs(finishMeshUpdates, instance.updatedTrees);
                return true;
            }
        }
        #endregion
        
        const Allocator defaultAllocator = Allocator.TempJob;

        internal unsafe struct TreeUpdate
        {
            public CSGTree          tree;
            public CompactNodeID    treeCompactNodeID;
            public int              brushCount;
            public int              maxNodeOrder;
            public int              updateCount;

            public JobHandle        lastJobHandle;

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
                public NativeHashSet<IndexOrder>            brushesThatNeedIndirectUpdateHashMap;

                public NativeList<BrushPair2>               uniqueBrushPairs;

                public NativeList<float3>                   outputSurfaceVertices;
                public NativeList<BrushIntersectionLoop>    outputSurfaces;
                public NativeArray<int2>                    outputSurfacesRange;

                public NativeArray<ChiselBlobAssetReference<BrushMeshBlob>>   brushMeshLookup;

                public NativeArray<UnsafeList<float3>>      loopVerticesLookup;

                public NativeReference<int>                 surfaceCountRef;
                public NativeReference<ChiselBlobAssetReference<CompactTree>> compactTreeRef;
                public NativeReference<bool>                needRemappingRef;

                public VertexBufferContents                 vertexBufferContents;

                public NativeList<int>                      nodeIDValueToNodeOrderArray;
                public NativeReference<int>                 nodeIDValueToNodeOrderOffsetRef;

                public NativeList<BrushData>                brushRenderData;
                public NativeList<SubMeshCounts>            subMeshCounts;
                public NativeArray<UnsafeList<SubMeshSurface>>  subMeshSurfaces;

                public NativeStream                         dataStream1;
                public NativeStream                         dataStream2;
                public NativeStream                         intersectingBrushesStream;

            
                public NativeList<ChiselMeshUpdate>         colliderMeshUpdates;
                public NativeList<ChiselMeshUpdate>         debugHelperMeshes;
                public NativeList<ChiselMeshUpdate>         renderMeshes;

                public NativeList<ChiselBlobAssetReference<BasePolygonsBlob>>             basePolygonDisposeList;
                public NativeList<ChiselBlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesDisposeList;
                public NativeList<ChiselBlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushDisposeList;
                public NativeList<ChiselBlobAssetReference<RoutingTable>>                 routingTableDisposeList;
                public NativeList<ChiselBlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneDisposeList;
                public NativeList<ChiselBlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferDisposeList;
            }
            internal TemporariesStruct Temporaries;
            #endregion

            #region Sub tasks JobHandles
            internal struct JobHandlesStruct
            {
                internal JobHandle transformTreeBrushIndicesListJobHandle;
                internal JobHandle brushBoundsUpdateListJobHandle;
                internal JobHandle brushesJobHandle;
                internal JobHandle nodesJobHandle;
                internal JobHandle parametersJobHandle;
                internal JobHandle allKnownBrushMeshIndicesJobHandle;
                internal JobHandle parameterCountsJobHandle;

                internal JobHandle allBrushMeshIDsJobHandle;
                internal JobHandle allTreeBrushIndexOrdersJobHandle;
                internal JobHandle allUpdateBrushIndexOrdersJobHandle;

                internal JobHandle brushIDValuesJobHandle;
                internal JobHandle basePolygonCacheJobHandle;
                internal JobHandle brushBrushIntersectionsJobHandle;
                internal JobHandle brushesTouchedByBrushCacheJobHandle;
                internal JobHandle brushRenderBufferCacheJobHandle;
                internal JobHandle brushRenderDataJobHandle;
                internal JobHandle brushTreeSpacePlaneCacheJobHandle;
                internal JobHandle brushMeshBlobsLookupJobHandle;
                internal JobHandle hierarchyIDJobHandle;
                internal JobHandle hierarchyListJobHandle;
                internal JobHandle brushMeshLookupJobHandle;
                internal JobHandle brushIntersectionsWithJobHandle;
                internal JobHandle brushIntersectionsWithRangeJobHandle;
                internal JobHandle brushesThatNeedIndirectUpdateHashMapJobHandle;
                internal JobHandle brushesThatNeedIndirectUpdateJobHandle;
                internal JobHandle brushTreeSpaceBoundCacheJobHandle;

                internal JobHandle dataStream1JobHandle;
                internal JobHandle dataStream2JobHandle;

                internal JobHandle intersectingBrushesStreamJobHandle;

                internal JobHandle loopVerticesLookupJobHandle;

                internal JobHandle meshQueriesJobHandle;

                internal JobHandle nodeIDValueToNodeOrderArrayJobHandle;

                internal JobHandle outputSurfaceVerticesJobHandle;
                internal JobHandle outputSurfacesJobHandle;
                internal JobHandle outputSurfacesRangeJobHandle;

                internal JobHandle routingTableCacheJobHandle;
                internal JobHandle rebuildTreeBrushIndexOrdersJobHandle;

                internal JobHandle sectionsJobHandle;
                internal JobHandle surfaceCountRefJobHandle;
                internal JobHandle compactTreeRefJobHandle;
                internal JobHandle needRemappingRefJobHandle;
                internal JobHandle nodeIDValueToNodeOrderOffsetRefJobHandle;
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

                internal JobHandle preMeshUpdateCombinedJobHandle;
            }
            internal JobHandlesStruct JobHandles;
            #endregion

            static TreeUpdate[] s_TreeUpdates;
            public static JobHandle ScheduleTreeMeshJobs(FinishMeshUpdate finishMeshUpdates, NativeList<CSGTree> trees)
            {
                var finalJobHandle          = default(JobHandle);

                //
                // Schedule all the jobs that create new meshes based on our CSG trees
                //
                #region Schedule Generator Jobs
                var generatorPoolJobHandle = default(JobHandle);
                using (var profilerSample = new ProfilerSample("CSG_ScheduleGeneratorJobPool"))
                { 
                    var runInParallel = runInParallelDefault;
                    generatorPoolJobHandle = GeneratorJobPoolManager.ScheduleJobs(runInParallel);
                    
                    // TODO: Try to get rid of this Complete
                    generatorPoolJobHandle.Complete();
                    generatorPoolJobHandle = default;
                }
                #endregion

                //
                // Make a list of valid trees
                //
                #region Find all modified, valid trees
                var treeUpdateLength = 0;
                using (var profilerSample = new ProfilerSample("CSG_TreeUpdate_Allocate"))
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
                }
                #endregion


                // TODO: Try to get rid of this Complete
                generatorPoolJobHandle.Complete(); // <-- Initialize has code that depends on the current state of the tree
                generatorPoolJobHandle = default;


                //
                // Initialize our data structures
                //
                #region Find all modified, valid trees
                using (var profilerSample = new ProfilerSample("CSG_TreeUpdate_Initialize"))
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        // Make sure that if we, somehow, run this while parts of the previous update is still running, we wait for the previous run to complete

                        // TODO: STORE THIS ON THE TREE!!! THIS WILL BE FORGOTTEN
                        s_TreeUpdates[t].lastJobHandle.Complete();
                        s_TreeUpdates[t].lastJobHandle = default;

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
                        using (var profilerSample = new ProfilerSample("CSG_RunMeshInitJobs"))
                        {
                            for (int t = 0; t < treeUpdateLength; t++)
                                s_TreeUpdates[t].RunMeshInitJobs(generatorPoolJobHandle);
                        }
                        #endregion

                        //
                        // Schedule chain of jobs that will generate our surface meshes 
                        // At this point we need previously scheduled jobs to be completed so we know what actually needs to be updated, if anything
                        //
                        #region Schedule CSG jobs
                        using (var profilerSample = new ProfilerSample("CSG_RunMeshUpdateJobs"))
                        {
                            // Reverse order since we sorted the trees from big to small & small trees are more likely to have already completed
                            for (int t = treeUpdateLength - 1; t >= 0; t--)
                            {
                                ref var treeUpdate = ref s_TreeUpdates[t];

                                // TODO: figure out if there's a way around this ....
                                treeUpdate.JobHandles.rebuildTreeBrushIndexOrdersJobHandle.Complete();
                                treeUpdate.JobHandles.rebuildTreeBrushIndexOrdersJobHandle = default;
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
                        ref var treeUpdate          = ref s_TreeUpdates[t];
                        bool meshUpdated = false;
                        var dependencies = JobHandleExtensions.CombineDependencies(treeUpdate.JobHandles.meshDatasJobHandle,
                                                                                    treeUpdate.JobHandles.colliderMeshUpdatesJobHandle,
                                                                                    treeUpdate.JobHandles.debugHelperMeshesJobHandle,
                                                                                    treeUpdate.JobHandles.renderMeshesJobHandle,
                                                                                    treeUpdate.JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                                                    treeUpdate.JobHandles.vertexBufferContents_meshesJobHandle);
                        try
                        {
                            // TODO: get rid of these crazy legacy flags
                            #region Clear tree/brush status flags 
                            ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeUpdate.treeCompactNodeID);
                            using (var profilerSample = new ProfilerSample("CSG_ClearFlags"))
                            {
                                compactHierarchy.ClearAllStatusFlags(treeUpdate.treeCompactNodeID);
                                for (int b = 0; b < treeUpdate.brushCount; b++)
                                {
                                    var brushIndexOrder = treeUpdate.Temporaries.allTreeBrushIndexOrders[b];
                                    compactHierarchy.ClearAllStatusFlags(brushIndexOrder.compactNodeID);
                                }
                            }
                            #endregion

                            if (treeUpdate.updateCount == 0 &&
                                treeUpdate.brushCount != 0)
                                continue;

                            //
                            // Call delegate to convert the generated meshes in whatever we need 
                            //  For example, it could create/update Meshes/MeshRenderers/MeshFilters/Gameobjects etc.
                            //  But it could eventually, optionally, output entities instead at some point
                            //
                            #region Finish Mesh Updates
                            if (finishMeshUpdates != null)
                            {
                                using (var profilerSample = new ProfilerSample("CSG_FinishMeshUpdates"))
                                {
                                    meshUpdated = true;
                                    var usedMeshCount = finishMeshUpdates(treeUpdate.tree, ref treeUpdate.Temporaries.vertexBufferContents,
                                                                            treeUpdate.Temporaries.meshDataArray,
                                                                            treeUpdate.Temporaries.colliderMeshUpdates,
                                                                            treeUpdate.Temporaries.debugHelperMeshes,
                                                                            treeUpdate.Temporaries.renderMeshes,
                                                                            dependencies);
                                }
                            }
                            #endregion
                        }
                        finally
                        {
                            #region Ensure meshes are cleaned up
                            // Error or not, our jobs need to be completed at this point
                            dependencies.Complete(); 

                            // Ensure our meshDataArray ends up being disposed, even if we had errors
                            if (treeUpdate.updateCount > 0 && !meshUpdated)
                                treeUpdate.Temporaries.meshDataArray.Dispose();
                            #endregion
                        }
                    }
                }
                finally
                {
                    #region Free temporaries
                    // TODO: most of these disposes can be scheduled before we complete and write to the meshes, 
                    // so that these disposes can happen at the same time as the mesh updates in finishMeshUpdates
                    using (var profilerSample = new ProfilerSample("CSG_FreeTemporaries"))
                    {
                        for (int t = 0; t < treeUpdateLength; t++)
                        {
                            ref var treeUpdate = ref s_TreeUpdates[t];
                            treeUpdate.FreeTemporaries(ref finalJobHandle);
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
                // Reset everything
                JobHandles = default;
                Temporaries = default;

                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);

                Temporaries.parameterCounts                = new NativeArray<int>(chiselLookupValues.parameters.Length, defaultAllocator);
                Temporaries.transformTreeBrushIndicesList  = new NativeList<NodeOrderNodeID>(defaultAllocator);
                Temporaries.brushBoundsUpdateList          = new NativeList<NodeOrderNodeID>(defaultAllocator);
                Temporaries.nodes                          = new NativeList<CompactNodeID>(defaultAllocator);
                Temporaries.brushes                        = new NativeList<CompactNodeID>(defaultAllocator);

                compactHierarchy.GetTreeNodes(Temporaries.nodes, Temporaries.brushes);
                    
                #region Allocations/Resize
                var newBrushCount = Temporaries.brushes.Length;
                chiselLookupValues.EnsureCapacity(newBrushCount);

                this.brushCount   = newBrushCount;
                this.maxNodeOrder = this.brushCount;

                Temporaries.meshDataArray   = default;
                Temporaries.meshDatas       = new NativeList<UnityEngine.Mesh.MeshData>(defaultAllocator);

                Temporaries.brushesThatNeedIndirectUpdateHashMap = new NativeHashSet<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(brushCount, defaultAllocator);

                // TODO: find actual vertex count
                Temporaries.outputSurfaceVertices           = new NativeList<float3>(65535 * 10, defaultAllocator);

                Temporaries.outputSurfaces                  = new NativeList<BrushIntersectionLoop>(brushCount * 16, defaultAllocator);
                Temporaries.brushIntersectionsWith          = new NativeList<BrushIntersectWith>(brushCount, defaultAllocator);

                Temporaries.nodeIDValueToNodeOrderOffsetRef = new NativeReference<int>(defaultAllocator);
                Temporaries.surfaceCountRef                 = new NativeReference<int>(defaultAllocator);
                Temporaries.compactTreeRef                  = new NativeReference<ChiselBlobAssetReference<CompactTree>>(defaultAllocator);
                Temporaries.needRemappingRef                = new NativeReference<bool>(defaultAllocator);

                Temporaries.uniqueBrushPairs                = new NativeList<BrushPair2>(brushCount * 16, defaultAllocator);

                Temporaries.rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allBrushMeshIDs                 = new NativeArray<int>(brushCount, defaultAllocator);
                Temporaries.brushRenderData                 = new NativeList<BrushData>(brushCount, defaultAllocator);
                Temporaries.allTreeBrushIndexOrders         = new NativeList<IndexOrder>(brushCount, defaultAllocator);
                Temporaries.allTreeBrushIndexOrders.Clear();
                Temporaries.allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);

                Temporaries.outputSurfacesRange             = new NativeArray<int2>(brushCount, defaultAllocator);
                Temporaries.brushIntersectionsWithRange     = new NativeArray<int2>(brushCount, defaultAllocator);
                Temporaries.nodeIDValueToNodeOrderArray     = new NativeList<int>(brushCount, defaultAllocator);
                Temporaries.brushMeshLookup                 = new NativeArray<ChiselBlobAssetReference<BrushMeshBlob>>(brushCount, defaultAllocator);

                Temporaries.brushBrushIntersections         = new NativeArray<UnsafeList<BrushIntersectWith>>(brushCount, defaultAllocator);

                Temporaries.subMeshCounts                   = new NativeList<SubMeshCounts>(defaultAllocator);

                Temporaries.colliderMeshUpdates             = new NativeList<ChiselMeshUpdate>(defaultAllocator);
                Temporaries.debugHelperMeshes               = new NativeList<ChiselMeshUpdate>(defaultAllocator);
                Temporaries.renderMeshes                    = new NativeList<ChiselMeshUpdate>(defaultAllocator);


                Temporaries.loopVerticesLookup              = new NativeArray<UnsafeList<float3>>(this.brushCount, defaultAllocator);

                Temporaries.vertexBufferContents.EnsureInitialized();

                var parameterPtr = (ChiselLayerParameters*)chiselLookupValues.parameters.GetUnsafePtr();
                // Regular index operator will return a copy instead of a reference *sigh*
                for (int l = 0; l < SurfaceLayers.ParameterCount; l++)
                    parameterPtr[l].Clear();

                #region MeshQueries
                // TODO: have more control over the queries
                Temporaries.meshQueries         = MeshQuery.DefaultQueries.ToNativeArray(defaultAllocator);
                Temporaries.meshQueriesLength   = Temporaries.meshQueries.Length;
                Temporaries.meshQueries.Sort(meshQueryComparer);
                #endregion

                Temporaries.subMeshSurfaces = new NativeArray<UnsafeList<SubMeshSurface>>(Temporaries.meshQueriesLength, defaultAllocator);
                
                Temporaries.subMeshCounts.Clear();

                // Workaround for the incredibly dumb "can't create a stream that is zero sized" when the value is determined at runtime. Yeah, thanks
                Temporaries.uniqueBrushPairs.Add(new BrushPair2 { type = IntersectionType.InvalidValue });

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

                Temporaries.basePolygonDisposeList             = new NativeList<ChiselBlobAssetReference<BasePolygonsBlob>>(chiselLookupValues.basePolygonCache.Length, defaultAllocator);
                Temporaries.treeSpaceVerticesDisposeList       = new NativeList<ChiselBlobAssetReference<BrushTreeSpaceVerticesBlob>>(chiselLookupValues.treeSpaceVerticesCache.Length, defaultAllocator);
                Temporaries.brushesTouchedByBrushDisposeList   = new NativeList<ChiselBlobAssetReference<BrushesTouchedByBrush>>(chiselLookupValues.brushesTouchedByBrushCache.Length, defaultAllocator);
                Temporaries.routingTableDisposeList            = new NativeList<ChiselBlobAssetReference<RoutingTable>>(chiselLookupValues.routingTableCache.Length, defaultAllocator);
                Temporaries.brushTreeSpacePlaneDisposeList     = new NativeList<ChiselBlobAssetReference<BrushTreeSpacePlanes>>(chiselLookupValues.brushTreeSpacePlaneCache.Length, defaultAllocator);
                Temporaries.brushRenderBufferDisposeList       = new NativeList<ChiselBlobAssetReference<ChiselBrushRenderBuffer>>(chiselLookupValues.brushRenderBufferCache.Length, defaultAllocator);

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
                    using (var profilerSample = new ProfilerSample("Job_BuildLookupTablesJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var buildLookupTablesJob = new BuildLookupTablesJob
                        {
                            // Read
                            brushes                         = Temporaries.brushes,
                            brushCount                      = this.brushCount,

                            // Read/Write
                            nodeIDValueToNodeOrderArray     = Temporaries.nodeIDValueToNodeOrderArray,

                            // Write
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,
                            allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders
                        };
                        buildLookupTablesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.brushesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                ref JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle));
                    }
                    #endregion

                    #region CacheRemapping
                    using (var profilerSample = new ProfilerSample("Job_CacheRemappingJob"))
                    {
                        const bool runInParallel = false;// runInParallelDefault;
                        // TODO: update "previous siblings" when something with an intersection operation has been modified
                        var cacheRemappingJob = new CacheRemappingJob
                        {
                            // Read
                            nodeIDValueToNodeOrderArray     = Temporaries.nodeIDValueToNodeOrderArray,
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
                                JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                JobHandles.brushesJobHandle,
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.brushIDValuesJobHandle),
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
                    using (var profilerSample = new ProfilerSample("Job_UpdateBrushIDValuesJob"))
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
                                JobHandles.brushesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushIDValuesJobHandle));
                    }
                    #endregion

                    #region Find Modified Brushes
                    using (var profilerSample = new ProfilerSample("Job_FindModifiedBrushesJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
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
                            transformTreeBrushIndicesList   = Temporaries.transformTreeBrushIndicesList,
                            brushBoundsUpdateList           = Temporaries.brushBoundsUpdateList
                        };
                        findModifiedBrushesJob.InitializeHierarchy(ref compactHierarchy);
                        findModifiedBrushesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.brushesJobHandle,
                                JobHandles.allTreeBrushIndexOrdersJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.transformTreeBrushIndicesListJobHandle,
                                ref JobHandles.brushBoundsUpdateListJobHandle));
                    }
                    #endregion

                    #region Invalidate Brushes
                    using (var profilerSample = new ProfilerSample("Job_InvalidateBrushesJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var invalidateBrushesJob = new InvalidateBrushesJob
                        {
                            // Read
                            needRemappingRef                = Temporaries.needRemappingRef,
                            rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders       .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            brushes                         = Temporaries.brushes                           .AsJobArray(runInParallel),
                            brushCount                      = this.brushCount,
                            nodeIDValueToNodeOrderArray     = Temporaries.nodeIDValueToNodeOrderArray       .AsJobArray(runInParallel),
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,
                            //compactHierarchy              = compactHierarchy, //<-- cannot do ref or pointer here
                                                                                //    so we set it below using InitializeHierarchy

                            // Write
                            brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap
                        };
                        invalidateBrushesJob.InitializeHierarchy(ref compactHierarchy);
                        invalidateBrushesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.needRemappingRefJobHandle,
                                JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                JobHandles.brushesJobHandle,
                                JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle, // Why?
                                ref JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle));
                    }
                    #endregion

                    #region Update BrushMesh IDs
                    using (var profilerSample = new ProfilerSample("Job_UpdateBrushMeshIDsJob"))
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
                                JobHandles.brushMeshBlobsLookupJobHandle,
                                JobHandles.brushesJobHandle),
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
                    using (var profilerSample = new ProfilerSample("Job_UpdateTransformationsJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var updateTransformationsJob = new UpdateTransformationsJob
                        {
                            // Read
                            transformTreeBrushIndicesList = Temporaries.transformTreeBrushIndicesList.AsJobArray(runInParallel),
                            //compactHierarchy              = compactHierarchy, //<-- cannot do ref or pointer here
                            //    so we set it below using InitializeHierarchy

                            // Write
                            transformationCache = chiselLookupValues.transformationCache.AsJobArray(runInParallel)
                        };
                        updateTransformationsJob.InitializeHierarchy(ref compactHierarchy);
                        updateTransformationsJob.Schedule(runInParallel, Temporaries.transformTreeBrushIndicesList, 8,
                            new ReadJobHandles(
                                JobHandles.transformTreeBrushIndicesListJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.transformationCacheJobHandle));
                    }
                    #endregion

                    #region Build CSG Tree
                    using (var profilerSample = new ProfilerSample("Job_BuildCompactTreeJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var buildCompactTreeJob = new BuildCompactTreeJob
                        {
                            // Read
                            treeCompactNodeID = this.treeCompactNodeID,
                            brushes = Temporaries.brushes.AsArray(),
                            nodes = Temporaries.nodes.AsArray(),
                            //compactHierarchy  = compactHierarchy,  //<-- cannot do ref or pointer here, 
                            //    so we set it below using InitializeHierarchy

                            // Write
                            compactTreeRef = Temporaries.compactTreeRef
                        };
                        buildCompactTreeJob.InitializeHierarchy(ref compactHierarchy);
                        buildCompactTreeJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.brushesJobHandle,
                                JobHandles.nodesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.compactTreeRefJobHandle));
                    }
                    #endregion

                    #region Update BrushMeshBlob Lookup table
                    // Create lookup table for all brushMeshBlobs, based on the node order in the tree
                    using (var profilerSample = new ProfilerSample("Job_FillBrushMeshBlobLookupJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var fillBrushMeshBlobLookupJob = new FillBrushMeshBlobLookupJob
                        {
                            // Read
                            brushMeshBlobs = brushMeshBlobs,
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsJobArray(runInParallel),
                            allBrushMeshIDs = Temporaries.allBrushMeshIDs,

                            // Write
                            brushMeshLookup = Temporaries.brushMeshLookup,
                            surfaceCountRef = Temporaries.surfaceCountRef
                        };
                        fillBrushMeshBlobLookupJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.brushMeshBlobsLookupJobHandle,
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.allBrushMeshIDsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushMeshLookupJobHandle,
                                ref JobHandles.surfaceCountRefJobHandle));
                    }
                    #endregion

                    #region Invalidate outdated caches
                    // Invalidate outdated caches for all modified brushes
                    using (var profilerSample = new ProfilerSample("Job_InvalidateBrushCacheJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var invalidateBrushCacheJob = new InvalidateBrushCacheJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders.AsArray(),

                            // Read/Write
                            basePolygonCache = chiselLookupValues.basePolygonCache,
                            treeSpaceVerticesCache = chiselLookupValues.treeSpaceVerticesCache,
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache,
                            routingTableCache = chiselLookupValues.routingTableCache,
                            brushTreeSpacePlaneCache = chiselLookupValues.brushTreeSpacePlaneCache,
                            brushRenderBufferCache = chiselLookupValues.brushRenderBufferCache,

                            // Write
                            basePolygonDisposeList = Temporaries.basePolygonDisposeList.AsParallelWriter(),
                            treeSpaceVerticesDisposeList = Temporaries.treeSpaceVerticesDisposeList.AsParallelWriter(),
                            brushesTouchedByBrushDisposeList = Temporaries.brushesTouchedByBrushDisposeList.AsParallelWriter(),
                            routingTableDisposeList = Temporaries.routingTableDisposeList.AsParallelWriter(),
                            brushTreeSpacePlaneDisposeList = Temporaries.brushTreeSpacePlaneDisposeList.AsParallelWriter(),
                            brushRenderBufferDisposeList = Temporaries.brushRenderBufferDisposeList.AsParallelWriter()
                        };
                        invalidateBrushCacheJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                JobHandles.rebuildTreeBrushIndexOrdersJobHandle),
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
                    using (var profilerSample = new ProfilerSample("Job_FixupBrushCacheIndicesJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var fixupBrushCacheIndicesJob = new FixupBrushCacheIndicesJob
                        {
                            // Read
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsArray(),
                            nodeIDValueToNodeOrderArray = Temporaries.nodeIDValueToNodeOrderArray.AsArray(),
                            nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,

                            // Read Write
                            basePolygonCache = chiselLookupValues.basePolygonCache.AsJobArray(runInParallel),
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel)
                        };
                        fixupBrushCacheIndicesJob.Schedule(runInParallel, Temporaries.allTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.basePolygonCacheJobHandle,
                                ref JobHandles.brushesTouchedByBrushCacheJobHandle));
                    }
                    #endregion

                    #region Update brush tree space vertices and bounds
                    // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been modified
                    using (var profilerSample = new ProfilerSample("Job_CreateTreeSpaceVerticesAndBoundsJob"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders.AsArray(),
                            transformationCache = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup = Temporaries.brushMeshLookup,
                            //ref hierarchyIDLookup     = ref CompactHierarchyManager.HierarchyIDLookup,    //<-- cannot do ref or pointer here
                            //    so we set it below using InitializeHierarchy

                            // Read/Write
                            hierarchyList = CompactHierarchyManager.HierarchyList,

                            // Write
                            brushTreeSpaceBounds = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            treeSpaceVerticesCache = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),
                        };
                        createTreeSpaceVerticesAndBoundsJob.InitializeLookups();
                        createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.brushMeshBlobsLookupJobHandle,
                                JobHandles.hierarchyIDJobHandle,
                                JobHandles.brushMeshLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.hierarchyListJobHandle));
                    }
                    #endregion

                    #region Update intersection pairs
                    // Find all pairs of brushes that intersect, for those brushes that have been modified
                    using (var profilerSample = new ProfilerSample("Job_FindAllBrushIntersectionPairs"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                        // TODO: optimize, use hashed grid
                        var findAllBrushIntersectionPairsJob = new FindAllBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsArray(),
                            transformationCache = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup = Temporaries.brushMeshLookup,
                            brushTreeSpaceBounds = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders.AsArray(),

                            // Read / Write
                            allocator = defaultAllocator,
                            brushBrushIntersections = Temporaries.brushBrushIntersections,

                            // Write
                            brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap.AsParallelWriter()
                        };
                        findAllBrushIntersectionPairsJob.Schedule(runInParallel, Temporaries.rebuildTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.brushMeshLookupJobHandle,
                                JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                JobHandles.rebuildTreeBrushIndexOrdersJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushBrushIntersectionsJobHandle,
                                ref JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle));
                    }
                    #endregion

                    #region Update list of brushes that touch brushes
                    // Find all brushes that touch the brushes that have been modified
                    using (var profilerSample = new ProfilerSample("Job_FindUniqueIndirectBrushIntersections"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: optimize, use hashed grid
                        var findUniqueIndirectBrushIntersectionsJob = new FindUniqueIndirectBrushIntersectionsJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdateHashMap = Temporaries.brushesThatNeedIndirectUpdateHashMap,

                            // Write
                            brushesThatNeedIndirectUpdate = Temporaries.brushesThatNeedIndirectUpdate
                        };
                        findUniqueIndirectBrushIntersectionsJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushesThatNeedIndirectUpdateJobHandle));
                    }
                    #endregion

                    #region Invalidate indirectly outdated caches (when brush touches a brush that has changed)
                    // Invalidate the cache for the brushes that have been indirectly modified (touch a brush that has changed)
                    using (var profilerSample = new ProfilerSample("Job_InvalidateBrushCache_Indirect"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var invalidateIndirectBrushCacheJob = new InvalidateIndirectBrushCacheJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdate = Temporaries.brushesThatNeedIndirectUpdate.AsJobArray(runInParallel),

                            // Read/Write
                            basePolygonCache = chiselLookupValues.basePolygonCache.AsJobArray(runInParallel),
                            treeSpaceVerticesCache = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),
                            routingTableCache = chiselLookupValues.routingTableCache.AsJobArray(runInParallel),
                            brushTreeSpacePlaneCache = chiselLookupValues.brushTreeSpacePlaneCache.AsJobArray(runInParallel),
                            brushRenderBufferCache = chiselLookupValues.brushRenderBufferCache.AsJobArray(runInParallel)
                        };
                        invalidateIndirectBrushCacheJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 16,
                            new ReadJobHandles(
                                JobHandles.brushesThatNeedIndirectUpdateJobHandle),
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
                    using (var profilerSample = new ProfilerSample("Job_CreateTreeSpaceVerticesAndBounds_Indirect"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = Temporaries.brushesThatNeedIndirectUpdate.AsJobArray(runInParallel),
                            transformationCache = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup = Temporaries.brushMeshLookup,
                            //ref hierarchyIDLookup     = ref CompactHierarchyManager.HierarchyIDLookup,    //<-- cannot do ref or pointer here
                            //    so we set it below using InitializeHierarchy

                            // Read/Write
                            hierarchyList = CompactHierarchyManager.HierarchyList,


                            // Write
                            brushTreeSpaceBounds = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            treeSpaceVerticesCache = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),
                        };
                        createTreeSpaceVerticesAndBoundsJob.InitializeLookups();
                        createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 16,
                            new ReadJobHandles(
                                JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.brushMeshLookupJobHandle,
                                //JobHandles.brushMeshBlobsLookupJobHandle,
                                JobHandles.hierarchyIDJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle,
                                ref JobHandles.hierarchyListJobHandle));
                    }
                    #endregion

                    #region Update intersection pairs (when brush touches a brush that has changed)
                    // Find all pairs of brushes that intersect, for those brushes that have been indirectly modified
                    using (var profilerSample = new ProfilerSample("Job_FindAllBrushIntersectionPairs_Indirect"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: optimize, use hashed grid
                        var findAllIndirectBrushIntersectionPairsJob = new FindAllIndirectBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsArray(),
                            transformationCache = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup = Temporaries.brushMeshLookup,
                            brushTreeSpaceBounds = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            brushesThatNeedIndirectUpdate = Temporaries.brushesThatNeedIndirectUpdate.AsJobArray(runInParallel),

                            // Read / Write
                            allocator = defaultAllocator,
                            brushBrushIntersections = Temporaries.brushBrushIntersections
                        };
                        findAllIndirectBrushIntersectionPairsJob.Schedule(runInParallel, Temporaries.brushesThatNeedIndirectUpdate, 1,
                            new ReadJobHandles(
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.brushMeshLookupJobHandle,
                                JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                JobHandles.brushesThatNeedIndirectUpdateJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushBrushIntersectionsJobHandle));
                    }
                    #endregion

                    #region Update list of brushes that touch brushes (when brush touches a brush that has changed)
                    // Add brushes that need to be indirectly updated to our list of brushes that need updates
                    using (var profilerSample = new ProfilerSample("Job_AddIndirectUpdatedBrushesToListAndSort"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var addIndirectUpdatedBrushesToListAndSortJob = new AddIndirectUpdatedBrushesToListAndSortJob
                        {
                            // Read
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsArray(),
                            brushesThatNeedIndirectUpdate = Temporaries.brushesThatNeedIndirectUpdate.AsJobArray(runInParallel),
                            rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders.AsArray(),

                            // Write
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsParallelWriter(),
                        };
                        addIndirectUpdatedBrushesToListAndSortJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                JobHandles.rebuildTreeBrushIndexOrdersJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.allUpdateBrushIndexOrdersJobHandle));
                    }
                    #endregion

                    #region Gather all brush intersections
                    // Gather all found pairs of brushes that intersect with each other and cache them
                    using (var profilerSample = new ProfilerSample("Job_GatherAndStoreBrushIntersections"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var gatherBrushIntersectionsJob = new GatherBrushIntersectionPairsJob
                        {
                            // Read
                            brushBrushIntersections = Temporaries.brushBrushIntersections,

                            // Write
                            brushIntersectionsWith = Temporaries.brushIntersectionsWith.GetUnsafeList(),
                            brushIntersectionsWithRange = Temporaries.brushIntersectionsWithRange
                        };
                        gatherBrushIntersectionsJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.brushBrushIntersectionsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushIntersectionsWithJobHandle,
                                ref JobHandles.brushIntersectionsWithRangeJobHandle));

                        var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                        {
                            // Read
                            treeCompactNodeID = treeCompactNodeID,
                            compactTreeRef = Temporaries.compactTreeRef,
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsJobArray(runInParallel),
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),

                            brushIntersectionsWith = Temporaries.brushIntersectionsWith.AsJobArray(runInParallel),
                            brushIntersectionsWithRange = Temporaries.brushIntersectionsWithRange,

                            // Write
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel)
                        };
                        storeBrushIntersectionsJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                            new ReadJobHandles(
                                JobHandles.compactTreeRefJobHandle,
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.brushIntersectionsWithJobHandle,
                                JobHandles.brushIntersectionsWithRangeJobHandle),
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
                    using (var profilerSample = new ProfilerSample("Job_PrepareBrushPairIntersections"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var findBrushPairsJob = new FindBrushPairsJob
                        {
                            // Read
                            maxOrder = brushCount,
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushes = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),

                            // Read (Re-allocate) / Write
                            uniqueBrushPairs = Temporaries.uniqueBrushPairs.GetUnsafeList()
                        };
                        findBrushPairsJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.brushesTouchedByBrushCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.uniqueBrushPairsJobHandle));

                        NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.intersectingBrushesStream, Temporaries.uniqueBrushPairs,
                                                            new ReadJobHandles(
                                                                JobHandles.uniqueBrushPairsJobHandle
                                                                ),
                                                            new WriteJobHandles(
                                                                ref JobHandles.intersectingBrushesStreamJobHandle
                                                                ),
                                                            defaultAllocator);

                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
                            uniqueBrushPairs = Temporaries.uniqueBrushPairs.AsJobArray(runInParallel),
                            transformationCache = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup = Temporaries.brushMeshLookup,

                            // Write
                            intersectingBrushesStream = Temporaries.intersectingBrushesStream.AsWriter()
                        };
                        prepareBrushPairIntersectionsJob.Schedule(runInParallel, Temporaries.uniqueBrushPairs, 1,
                            new ReadJobHandles(
                                JobHandles.uniqueBrushPairsJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.brushMeshLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.intersectingBrushesStreamJobHandle));
                    }

                    using (var profilerSample = new ProfilerSample("Job_GenerateBasePolygonLoops"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),
                            brushMeshLookup = Temporaries.brushMeshLookup,
                            treeSpaceVerticesCache = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),

                            // Write
                            basePolygonCache = chiselLookupValues.basePolygonCache.AsJobArray(runInParallel)
                        };
                        createBlobPolygonsBlobs.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.brushesTouchedByBrushCacheJobHandle,
                                JobHandles.brushMeshLookupJobHandle,
                                JobHandles.treeSpaceVerticesCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.basePolygonCacheJobHandle));
                    }

                    using (var profilerSample = new ProfilerSample("Job_UpdateBrushTreeSpacePlanes"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this at creation time + when moved / store with brush component itself
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushMeshLookup = Temporaries.brushMeshLookup,
                            transformationCache = chiselLookupValues.transformationCache.AsJobArray(runInParallel),

                            // Write
                            brushTreeSpacePlanes = chiselLookupValues.brushTreeSpacePlaneCache.AsJobArray(runInParallel)
                        };
                        createBrushTreeSpacePlanesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.brushMeshLookupJobHandle,
                                JobHandles.transformationCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushTreeSpacePlaneCacheJobHandle));
                    }

                    using (var profilerSample = new ProfilerSample("Job_CreateIntersectionLoops"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        NativeCollection.ScheduleEnsureCapacity(runInParallel, ref Temporaries.outputSurfaces, Temporaries.surfaceCountRef,
                                                            new ReadJobHandles(
                                                                JobHandles.surfaceCountRefJobHandle),
                                                            new WriteJobHandles(
                                                                ref JobHandles.outputSurfacesJobHandle),
                                                            defaultAllocator);

                        var createIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Needed for count (forced & unused)
                            uniqueBrushPairs = Temporaries.uniqueBrushPairs,

                            // Read
                            brushTreeSpacePlaneCache = chiselLookupValues.brushTreeSpacePlaneCache.AsJobArray(runInParallel),
                            treeSpaceVerticesCache = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),
                            intersectingBrushesStream = Temporaries.intersectingBrushesStream.AsReader(),

                            // Write
                            outputSurfaceVertices = Temporaries.outputSurfaceVertices.AsParallelWriterExt(),
                            outputSurfaces = Temporaries.outputSurfaces.AsParallelWriter()
                        };
                        var currentJobHandle = createIntersectionLoopsJob.Schedule(runInParallel, Temporaries.uniqueBrushPairs, 8,
                            new ReadJobHandles(
                                JobHandles.uniqueBrushPairsJobHandle,
                                JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                JobHandles.treeSpaceVerticesCacheJobHandle,
                                JobHandles.intersectingBrushesStreamJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.outputSurfaceVerticesJobHandle,
                                ref JobHandles.outputSurfacesJobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref Temporaries.intersectingBrushesStream, currentJobHandle);
                    }

                    using (var profilerSample = new ProfilerSample("Job_GatherOutputSurfaces"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                        {
                            // Read / Write (Sort)
                            outputSurfaces = Temporaries.outputSurfaces.AsJobArray(runInParallel),

                            // Write
                            outputSurfacesRange = Temporaries.outputSurfacesRange
                        };
                        gatherOutputSurfacesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.outputSurfacesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.outputSurfacesRangeJobHandle));
                    }

                    using (var profilerSample = new ProfilerSample("Job_FindLoopOverlapIntersections"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.dataStream1, Temporaries.allUpdateBrushIndexOrders,
                                                            new ReadJobHandles(
                                                                JobHandles.allUpdateBrushIndexOrdersJobHandle
                                                                ),
                                                            new WriteJobHandles(
                                                                ref JobHandles.dataStream1JobHandle
                                                                ),
                                                            defaultAllocator);

                        var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            outputSurfaceVertices = Temporaries.outputSurfaceVertices.AsJobArray(runInParallel),
                            outputSurfaces = Temporaries.outputSurfaces.AsJobArray(runInParallel),
                            outputSurfacesRange = Temporaries.outputSurfacesRange,
                            maxNodeOrder = maxNodeOrder,
                            brushTreeSpacePlaneCache = chiselLookupValues.brushTreeSpacePlaneCache.AsJobArray(runInParallel),
                            basePolygonCache = chiselLookupValues.basePolygonCache.AsJobArray(runInParallel),

                            // Read Write
                            allocator = defaultAllocator,
                            loopVerticesLookup = Temporaries.loopVerticesLookup,

                            // Write
                            output = Temporaries.dataStream1.AsWriter()
                        };
                        findLoopOverlapIntersectionsJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.outputSurfaceVerticesJobHandle,
                                JobHandles.outputSurfacesJobHandle,
                                JobHandles.outputSurfacesRangeJobHandle,
                                JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                JobHandles.basePolygonCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.loopVerticesLookupJobHandle,
                                ref JobHandles.dataStream1JobHandle));
                    }
                    #endregion

                    //
                    // Ensure vertices that should be identical on different brushes, ARE actually identical
                    //

                    #region Merge vertices
                    using (var profilerSample = new ProfilerSample("Job_MergeTouchingBrushVerticesIndirect"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only try to merge the vertices beyond the original mesh vertices (the intersection vertices)
                        //       should also try to limit vertices to those that are on the same surfaces (somehow)
                        var mergeTouchingBrushVerticesIndirectJob = new MergeTouchingBrushVerticesIndirectJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),
                            treeSpaceVerticesArray = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),

                            // Read Write
                            loopVerticesLookup = Temporaries.loopVerticesLookup,
                        };
                        mergeTouchingBrushVerticesIndirectJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.treeSpaceVerticesCacheJobHandle,
                                JobHandles.brushesTouchedByBrushCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.loopVerticesLookupJobHandle));
                    }
                    #endregion

                    //
                    // Perform CSG on prepared surfaces, giving each surface a categorization
                    //

                    #region Perform CSG     
                    using (var profilerSample = new ProfilerSample("Job_UpdateBrushCategorizationTables"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: only update when brush or any touching brush has been added/removed or changes operation/order                    
                        // TODO: determine when a brush is completely inside another brush (might not have *any* intersection loops)
                        var createRoutingTableJob = new CreateRoutingTableJob // Build categorization trees for brushes
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushes = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),
                            compactTreeRef = Temporaries.compactTreeRef,

                            // Write
                            routingTableLookup = chiselLookupValues.routingTableCache.AsJobArray(runInParallel)
                        };
                        createRoutingTableJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.brushesTouchedByBrushCacheJobHandle,
                                JobHandles.compactTreeRefJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.routingTableCacheJobHandle));
                    }

                    using (var profilerSample = new ProfilerSample("Job_PerformCSG"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.dataStream2, Temporaries.allUpdateBrushIndexOrders,
                                                            new ReadJobHandles(
                                                                JobHandles.allUpdateBrushIndexOrdersJobHandle
                                                                ),
                                                            new WriteJobHandles(
                                                                ref JobHandles.dataStream2JobHandle
                                                                ),
                                                            defaultAllocator);

                        // Perform CSG
                        var performCSGJob = new PerformCSGJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            routingTableCache = chiselLookupValues.routingTableCache.AsJobArray(runInParallel),
                            brushTreeSpacePlaneCache = chiselLookupValues.brushTreeSpacePlaneCache.AsJobArray(runInParallel),
                            brushesTouchedByBrushCache = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),
                            input = Temporaries.dataStream1.AsReader(),
                            loopVerticesLookup = Temporaries.loopVerticesLookup,

                            // Write
                            output = Temporaries.dataStream2.AsWriter(),
                        };
                        var currentJobHandle = performCSGJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.routingTableCacheJobHandle,
                                JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                JobHandles.brushesTouchedByBrushCacheJobHandle,
                                JobHandles.dataStream1JobHandle,
                                JobHandles.loopVerticesLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.dataStream2JobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref Temporaries.dataStream1, currentJobHandle);
                    }
                    #endregion

                    //
                    // Triangulate the surfaces
                    //

                    #region Triangulate Surfaces
                    using (var profilerSample = new ProfilerSample("Job_GenerateSurfaceTriangles"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var generateSurfaceTrianglesJob = new GenerateSurfaceTrianglesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = Temporaries.allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            basePolygonCache = chiselLookupValues.basePolygonCache.AsJobArray(runInParallel),
                            transformationCache = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            input = Temporaries.dataStream2.AsReader(),
                            meshQueries = Temporaries.meshQueries,

                            // Write
                            brushRenderBufferCache = chiselLookupValues.brushRenderBufferCache.AsJobArray(runInParallel)
                        };
                        var currentJobHandle = generateSurfaceTrianglesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.meshQueriesJobHandle,
                                JobHandles.basePolygonCacheJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.dataStream2JobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushRenderBufferCacheJobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref Temporaries.dataStream2, currentJobHandle);
                    }
                    #endregion


                    //
                    // Create meshes out of ALL the generated AND cached surfaces
                    //

                    #region Find all generated brush specific geometry
                    using (var profilerSample = new ProfilerSample("Job_FindBrushRenderBuffers"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        NativeCollection.ScheduleEnsureCapacity(runInParallel, ref Temporaries.brushRenderData, Temporaries.allTreeBrushIndexOrders,
                                                            new ReadJobHandles(
                                                                JobHandles.allTreeBrushIndexOrdersJobHandle),
                                                            new WriteJobHandles(
                                                                ref JobHandles.brushRenderDataJobHandle),
                                                            defaultAllocator);

                        var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                        {
                            // Read
                            meshQueryLength = Temporaries.meshQueriesLength,
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsArray(),
                            brushRenderBufferCache = chiselLookupValues.brushRenderBufferCache.AsJobArray(runInParallel),

                            // Write
                            brushRenderData = Temporaries.brushRenderData.AsParallelWriter()
                        };
                        findBrushRenderBuffersJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.meshQueriesJobHandle,
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.brushRenderBufferCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushRenderDataJobHandle
                                ));
                    }
                    #endregion

                    #region Allocate sub meshes
                    using (var profilerSample = new ProfilerSample("Job_AllocateSubMeshes"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var allocateSubMeshesJob = new AllocateSubMeshesJob
                        {
                            // Read
                            meshQueryLength = Temporaries.meshQueriesLength,
                            surfaceCountRef = Temporaries.surfaceCountRef,

                            // Write
                            subMeshCounts = Temporaries.subMeshCounts,
                            subMeshSections = Temporaries.vertexBufferContents.subMeshSections,
                        };
                        allocateSubMeshesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.meshQueriesJobHandle,
                                JobHandles.surfaceCountRefJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.subMeshCountsJobHandle,
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle));
                    }
                    #endregion

                    #region Prepare sub sections
                    using (var profilerSample = new ProfilerSample("Job_PrepareSubSections"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var prepareSubSectionsJob = new PrepareSubSectionsJob
                        {
                            // Read
                            meshQueries = Temporaries.meshQueries,
                            brushRenderData = Temporaries.brushRenderData.AsJobArray(runInParallel),

                            // Write
                            allocator = defaultAllocator,
                            subMeshSurfaces = Temporaries.subMeshSurfaces,
                        };
                        prepareSubSectionsJob.Schedule(runInParallel, Temporaries.meshQueriesLength, 1,
                            new ReadJobHandles(
                                JobHandles.meshQueriesJobHandle,
                                JobHandles.brushRenderDataJobHandle,
                                JobHandles.sectionsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.subMeshSurfacesJobHandle));
                    }
                    #endregion

                    #region Sort surfaces
                    using (var profilerSample = new ProfilerSample("Job_SortSurfaces"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var sortSurfacesParallelJob = new SortSurfacesParallelJob
                        {
                            // Read
                            meshQueries = Temporaries.meshQueries,
                            subMeshSurfaces = Temporaries.subMeshSurfaces,

                            // Write
                            subMeshCounts = Temporaries.subMeshCounts
                        };
                        sortSurfacesParallelJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.sectionsJobHandle,
                                JobHandles.subMeshSurfacesJobHandle,
                                JobHandles.vertexBufferContents_subMeshSectionsJobHandle),
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
                                JobHandles.sectionsJobHandle,
                                JobHandles.subMeshCountsJobHandle,
                                JobHandles.subMeshSurfacesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.subMeshCountsJobHandle,
                                ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle));
                    }
                    #endregion

                    #region Allocate vertex buffers
                    using (var profilerSample = new ProfilerSample("Job_AllocateVertexBuffers"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                        {
                            // Read
                            subMeshSections = Temporaries.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),

                            // Read Write
                            allocator = defaultAllocator,
                            triangleBrushIndices = Temporaries.vertexBufferContents.triangleBrushIndices
                        };
                        allocateVertexBuffersJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.vertexBufferContents_subMeshSectionsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle));
                    }
                    #endregion

                    #region Generate mesh descriptions
                    using (var profilerSample = new ProfilerSample("Job_GenerateMeshDescription"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                        {
                            // Read
                            subMeshCounts = Temporaries.subMeshCounts.AsJobArray(runInParallel),

                            // Read Write
                            meshDescriptions = Temporaries.vertexBufferContents.meshDescriptions
                        };
                        generateMeshDescriptionJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.subMeshCountsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_meshDescriptionsJobHandle));
                    }
                    #endregion


                    // TODO: store parameterCounts per brush (precalculated), manage these counts in the hierarchy when brushes are added/removed/modified
                    //       then we don't need to count them here & don't need to do a "complete" here
                    JobHandles.parameterCountsJobHandle.Complete();
                    JobHandles.parameterCountsJobHandle = default;

                    #region Create Meshes
                    using (var profilerSample = new ProfilerSample("Mesh.AllocateWritableMeshData"))
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

                    using (var profilerSample = new ProfilerSample("Job_CopyToMeshes"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var assignMeshesJob = new AssignMeshesJob
                        {
                            // Read
                            meshDescriptions = Temporaries.vertexBufferContents.meshDescriptions,
                            subMeshSections = Temporaries.vertexBufferContents.subMeshSections,
                            meshDatas = Temporaries.meshDatas,

                            // Write
                            meshes = Temporaries.vertexBufferContents.meshes,
                            debugHelperMeshes = Temporaries.debugHelperMeshes,
                            renderMeshes = Temporaries.renderMeshes,

                            // Read / Write
                            colliderMeshUpdates = Temporaries.colliderMeshUpdates,
                        };
                        assignMeshesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.vertexBufferContents_meshDescriptionsJobHandle,
                                JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                JobHandles.meshDatasJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_meshesJobHandle,
                                ref JobHandles.debugHelperMeshesJobHandle,
                                ref JobHandles.renderMeshesJobHandle,
                                ref JobHandles.colliderMeshUpdatesJobHandle));

                        var colliderCopyToMeshJob = new CopyToColliderMeshJob
                        {
                            // Read
                            subMeshSections = Temporaries.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts = Temporaries.subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces = Temporaries.subMeshSurfaces,
                            colliderDescriptors = Temporaries.vertexBufferContents.colliderDescriptors,
                            colliderMeshes = Temporaries.colliderMeshUpdates.AsJobArray(runInParallel),

                            // Read/Write
                            meshes = Temporaries.vertexBufferContents.meshes,
                        };
                        var dependencies1 = JobHandleExtensions.CombineDependencies(
                                                JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                JobHandles.subMeshCountsJobHandle,
                                                JobHandles.subMeshSurfacesJobHandle,
                                                JobHandles.vertexBufferContents_colliderDescriptorsJobHandle,
                                                JobHandles.colliderMeshUpdatesJobHandle,
                                                JobHandles.vertexBufferContents_meshesJobHandle);
                        var job1 = colliderCopyToMeshJob.Schedule(runInParallel, Temporaries.colliderMeshUpdates, 1, dependencies1);

                        var renderCopyToMeshJob = new CopyToRenderMeshJob
                        {
                            // Read
                            subMeshSections = Temporaries.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts = Temporaries.subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces = Temporaries.subMeshSurfaces,
                            renderDescriptors = Temporaries.vertexBufferContents.renderDescriptors,
                            renderMeshes = Temporaries.renderMeshes.AsJobArray(runInParallel),

                            // Read/Write
                            triangleBrushIndices = Temporaries.vertexBufferContents.triangleBrushIndices,
                            meshes = Temporaries.vertexBufferContents.meshes,
                        };
                        var dependencies2 = JobHandleExtensions.CombineDependencies(
                                                JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                JobHandles.subMeshCountsJobHandle,
                                                JobHandles.subMeshSurfacesJobHandle,
                                                JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                                JobHandles.renderMeshesJobHandle,
                                                JobHandles.vertexBufferContents_meshesJobHandle,
                                                JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle);
                        var job2 = renderCopyToMeshJob.Schedule(runInParallel, Temporaries.renderMeshes, 1, dependencies2);

                        var helperCopyToMeshJob = new CopyToRenderMeshJob
                        {
                            // Read
                            subMeshSections = Temporaries.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts = Temporaries.subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces = Temporaries.subMeshSurfaces,
                            renderDescriptors = Temporaries.vertexBufferContents.renderDescriptors,
                            renderMeshes = Temporaries.debugHelperMeshes.AsJobArray(runInParallel),

                            // Read/Write
                            triangleBrushIndices = Temporaries.vertexBufferContents.triangleBrushIndices,
                            meshes = Temporaries.vertexBufferContents.meshes,
                        };
                        var dependencies3 = JobHandleExtensions.CombineDependencies(
                                                JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                JobHandles.subMeshCountsJobHandle,
                                                JobHandles.subMeshSurfacesJobHandle,
                                                JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                                JobHandles.debugHelperMeshesJobHandle,
                                                JobHandles.vertexBufferContents_meshesJobHandle,
                                                JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle);
                        var job3 = helperCopyToMeshJob.Schedule(runInParallel, Temporaries.debugHelperMeshes, 1, dependencies3);

                        var copyJobs = JobHandle.CombineDependencies(job1, job2, job3);
                        JobHandles.vertexBufferContents_meshesJobHandle = JobHandle.CombineDependencies(JobHandles.vertexBufferContents_meshesJobHandle, copyJobs);
                        JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle = JobHandle.CombineDependencies(JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle, copyJobs);
                    }
                    #endregion

                    //
                    // Finally store the generated surfaces into our cache
                    //

                    #region Store cached values back into cache (by node Index)
                    using (var profilerSample = new ProfilerSample("Job_StoreToCache"))
                    {
                        const bool runInParallel = runInParallelDefault;
                        var storeToCacheJob = new StoreToCacheJob
                        {
                            // Read
                            allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsJobArray(runInParallel),
                            brushTreeSpaceBoundCache = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            brushRenderBufferCache = chiselLookupValues.brushRenderBufferCache.AsJobArray(runInParallel),

                            // Read Write
                            brushTreeSpaceBoundLookup = chiselLookupValues.brushTreeSpaceBoundLookup,
                            brushRenderBufferLookup = chiselLookupValues.brushRenderBufferLookup
                        };
                        storeToCacheJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.allTreeBrushIndexOrdersJobHandle,
                                JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                JobHandles.brushRenderBufferCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.storeToCacheJobHandle));
                    }
                    #endregion

                    #region Create wireframes for all new/modified brushes
                    using (var profilerSample = new ProfilerSample("Job_UpdateBrushOutline"))
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
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.brushMeshBlobsLookupJobHandle),
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
                                                    JobHandles.allBrushMeshIDsJobHandle,
                                                    JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                    JobHandles.brushIDValuesJobHandle,
                                                    JobHandles.basePolygonCacheJobHandle,
                                                    JobHandles.brushBrushIntersectionsJobHandle,
                                                    JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                    JobHandles.brushRenderBufferCacheJobHandle,
                                                    JobHandles.brushRenderDataJobHandle,
                                                    JobHandles.brushTreeSpacePlaneCacheJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.brushMeshBlobsLookupJobHandle,
                                                    JobHandles.hierarchyIDJobHandle,
                                                    JobHandles.hierarchyListJobHandle,
                                                    JobHandles.brushMeshLookupJobHandle,
                                                    JobHandles.brushIntersectionsWithJobHandle,
                                                    JobHandles.brushIntersectionsWithRangeJobHandle,
                                                    JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                    JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                    JobHandles.brushTreeSpaceBoundCacheJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.dataStream1JobHandle,
                                                    JobHandles.dataStream2JobHandle,
                                                    JobHandles.intersectingBrushesStreamJobHandle,
                                                    JobHandles.loopVerticesLookupJobHandle,
                                                    JobHandles.meshQueriesJobHandle,
                                                    JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                    JobHandles.outputSurfaceVerticesJobHandle,
                                                    JobHandles.outputSurfacesJobHandle,
                                                    JobHandles.outputSurfacesRangeJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.routingTableCacheJobHandle,
                                                    JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                    JobHandles.sectionsJobHandle,
                                                    JobHandles.subMeshSurfacesJobHandle,
                                                    JobHandles.subMeshCountsJobHandle,
                                                    JobHandles.treeSpaceVerticesCacheJobHandle,
                                                    JobHandles.transformationCacheJobHandle,
                                                    JobHandles.uniqueBrushPairsJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.brushesJobHandle,
                                                    JobHandles.nodesJobHandle, 
                                                    JobHandles.parametersJobHandle,
                                                    JobHandles.allKnownBrushMeshIndicesJobHandle,
                                                    JobHandles.parameterCountsJobHandle,
                                                    JobHandles.storeToCacheJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.surfaceCountRefJobHandle,
                                                    JobHandles.compactTreeRefJobHandle,
                                                    JobHandles.needRemappingRefJobHandle,
                                                    JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle)
                                            );
                lastJobHandle = dependencies;

                lastJobHandle.AddDependency(Temporaries.brushMeshLookup              .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushIntersectionsWithRange  .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.outputSurfacesRange          .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.parameterCounts              .Dispose(dependencies));
                
                lastJobHandle.AddDependency(Temporaries.transformTreeBrushIndicesList.Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushBoundsUpdateList        .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushes                      .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.nodes                        .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushRenderData              .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.rebuildTreeBrushIndexOrders  .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.allUpdateBrushIndexOrders    .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.allBrushMeshIDs              .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.uniqueBrushPairs             .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushIntersectionsWith       .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.outputSurfaceVertices        .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.outputSurfaces               .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushesThatNeedIndirectUpdate.Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.nodeIDValueToNodeOrderArray  .Dispose(dependencies));
                
                lastJobHandle.AddDependency(Temporaries.brushesThatNeedIndirectUpdateHashMap.Dispose(dependencies));
                
                
                // Note: cannot use "IsCreated" on this job, for some reason it won't be scheduled and then complain that it's leaking? Bug in IsCreated?
                lastJobHandle.AddDependency(Temporaries.meshQueries.Dispose(dependencies));


                lastJobHandle.AddDependency(NativeCollection.DisposeDeep(Temporaries.brushBrushIntersections,          dependencies),
                                            NativeCollection.DisposeDeep(Temporaries.loopVerticesLookup,               dependencies),
                                            NativeCollection.DisposeDeep(Temporaries.basePolygonDisposeList,           dependencies),
                                            NativeCollection.DisposeDeep(Temporaries.treeSpaceVerticesDisposeList,     dependencies),
                                            NativeCollection.DisposeDeep(Temporaries.brushesTouchedByBrushDisposeList, dependencies),
                                            NativeCollection.DisposeDeep(Temporaries.routingTableDisposeList,          dependencies),
                                            NativeCollection.DisposeDeep(Temporaries.brushTreeSpacePlaneDisposeList,   dependencies),
                                            NativeCollection.DisposeDeep(Temporaries.brushRenderBufferDisposeList,     dependencies));
                

                lastJobHandle.AddDependency(Temporaries.surfaceCountRef                .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.needRemappingRef               .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.nodeIDValueToNodeOrderOffsetRef.Dispose(dependencies));
                lastJobHandle.AddDependency(NativeCollection.DisposeDeep(Temporaries.compactTreeRef, dependencies));

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
                                                    JobHandles.allBrushMeshIDsJobHandle,
                                                    JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                    JobHandles.brushIDValuesJobHandle,
                                                    JobHandles.basePolygonCacheJobHandle,
                                                    JobHandles.brushBrushIntersectionsJobHandle,
                                                    JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                    JobHandles.brushRenderBufferCacheJobHandle,
                                                    JobHandles.brushRenderDataJobHandle,
                                                    JobHandles.brushTreeSpacePlaneCacheJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.brushMeshBlobsLookupJobHandle,
                                                    JobHandles.hierarchyIDJobHandle,
                                                    JobHandles.hierarchyListJobHandle,
                                                    JobHandles.brushMeshLookupJobHandle,
                                                    JobHandles.brushIntersectionsWithJobHandle,
                                                    JobHandles.brushIntersectionsWithRangeJobHandle,
                                                    JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                    JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                    JobHandles.brushTreeSpaceBoundCacheJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.dataStream1JobHandle,
                                                    JobHandles.dataStream2JobHandle,
                                                    JobHandles.intersectingBrushesStreamJobHandle,
                                                    JobHandles.loopVerticesLookupJobHandle,
                                                    JobHandles.meshQueriesJobHandle,
                                                    JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                    JobHandles.outputSurfaceVerticesJobHandle,
                                                    JobHandles.outputSurfacesJobHandle,
                                                    JobHandles.outputSurfacesRangeJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.routingTableCacheJobHandle,
                                                    JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                    JobHandles.sectionsJobHandle,
                                                    JobHandles.subMeshSurfacesJobHandle,
                                                    JobHandles.subMeshCountsJobHandle,
                                                    JobHandles.treeSpaceVerticesCacheJobHandle,
                                                    JobHandles.transformationCacheJobHandle,
                                                    JobHandles.uniqueBrushPairsJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.transformTreeBrushIndicesListJobHandle,
                                                    JobHandles.brushBoundsUpdateListJobHandle,
                                                    JobHandles.brushesJobHandle,
                                                    JobHandles.nodesJobHandle,
                                                    JobHandles.parametersJobHandle,
                                                    JobHandles.allKnownBrushMeshIndicesJobHandle,
                                                    JobHandles.parameterCountsJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.storeToCacheJobHandle,

                                                    JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                    JobHandles.colliderMeshUpdatesJobHandle,
                                                    JobHandles.debugHelperMeshesJobHandle,
                                                    JobHandles.renderMeshesJobHandle,
                                                    JobHandles.surfaceCountRefJobHandle,
                                                    JobHandles.compactTreeRefJobHandle,
                                                    JobHandles.needRemappingRefJobHandle,
                                                    JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                                    JobHandles.vertexBufferContents_colliderDescriptorsJobHandle,
                                                    JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                    JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                    JobHandles.vertexBufferContents_meshDescriptionsJobHandle,
                                                    JobHandles.vertexBufferContents_meshesJobHandle,
                                                    JobHandles.meshDatasJobHandle)
                                        );

                // Technically not necessary, but Unity will complain about memory leaks that aren't there (jobs just haven't finished yet)
                // TODO: see if we can use domain reload events to ensure this job is completed before a domain reload occurs
                dependencies.Complete(); 
                                            

                // We let the final JobHandle dependend on the dependencies, but not on the disposal, 
                // because we do not need to wait for the disposal of native collections do use our generated data
                finalJobHandle.AddDependency(dependencies);

                lastJobHandle.AddDependency(dependencies);
                lastJobHandle.AddDependency(NativeCollection.DisposeDeep(Temporaries.subMeshSurfaces,   dependencies));
                lastJobHandle.AddDependency(Temporaries.allTreeBrushIndexOrders .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.colliderMeshUpdates     .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.debugHelperMeshes       .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.renderMeshes            .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.meshDatas               .Dispose(dependencies));                
                lastJobHandle.AddDependency(Temporaries.vertexBufferContents    .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.subMeshCounts           .Dispose(dependencies));

                return lastJobHandle;
            }
        }
    }
}
