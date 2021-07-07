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

            UnityEngine.Profiling.Profiler.BeginSample("UpdateTreeMeshes");
            allTrees = ScheduleTreeMeshJobs(finishMeshUpdates, instance.updatedTrees);
            UnityEngine.Profiling.Profiler.EndSample();
            return true;
        }
        #endregion

        #region GetHash
        static unsafe uint GetHash(NativeList<uint> list)
        {
            return math.hash(list.GetUnsafePtr(), sizeof(uint) * list.Length);
        }

        static unsafe uint GetHash(ref CompactHierarchy hierarchy, NativeList<CSGTreeBrush> list)
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
        #endregion

        static TreeUpdate[] s_TreeUpdates;

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

            #region Prepare Trees 
            Profiler.BeginSample("CSG_TreeUpdate_Allocate");
            if (s_TreeUpdates == null || s_TreeUpdates.Length < trees.Length)
                s_TreeUpdates = new TreeUpdate[trees.Length];
            Profiler.EndSample();
            #endregion

            var treeUpdateLength = 0;
            try
            {
                //
                // Schedule all the jobs that create new meshes based on our CSG trees
                //

                #region Schedule job to generate brushes (using generators)
                Profiler.BeginSample("CSG_GeneratorJobPool");
                var generatorPoolJobHandle = GeneratorJobPoolManager.ScheduleJobs();
                generatorPoolJobHandle.Complete();
                Profiler.EndSample();
                #endregion

                #region Schedule Mesh Update Jobs
                Profiler.BeginSample("CSG_RunMeshUpdateJobs");

                Profiler.BeginSample("CSG_Init");
                for (int t = 0; t < trees.Length; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    treeUpdate.tree = trees[t];
                    treeUpdate.treeCompactNodeID = CompactHierarchyManager.GetCompactNodeID(treeUpdate.tree);
                    ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeUpdate.treeCompactNodeID);
                    
                    // Skip invalid trees since they wouldn't work anyway
                    if (!compactHierarchy.IsValidCompactNodeID(treeUpdate.treeCompactNodeID))
                        continue;

                    if (!compactHierarchy.IsNodeDirty(treeUpdate.treeCompactNodeID))
                        continue;

                    treeUpdate.RunMeshInitJobs(ref compactHierarchy, generatorPoolJobHandle);
                    treeUpdateLength++;
                }
                Profiler.EndSample();

                JobHandle.ScheduleBatchedJobs();

                Profiler.BeginSample("CSG_Run");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeUpdate.treeCompactNodeID);

                    // Skip invalid trees since they wouldn't work anyway
                    if (!compactHierarchy.IsValidCompactNodeID(treeUpdate.treeCompactNodeID))
                        continue;

                    if (!compactHierarchy.IsNodeDirty(treeUpdate.treeCompactNodeID))
                        continue;

                    // TODO: figure out if there's a way around this ....
                    treeUpdate.JobHandles.rebuildTreeBrushIndexOrdersJobHandle.Complete();
                    treeUpdate.updateCount = treeUpdate.Temporaries.rebuildTreeBrushIndexOrders.Length;

                    if (treeUpdate.updateCount == 0) 
                        continue;
                    
                    treeUpdate.RunMeshUpdateJobs(ref compactHierarchy);
                }
                Profiler.EndSample();

                Profiler.EndSample();
                #endregion

                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(treeUpdate.treeCompactNodeID);

                    // Skip invalid trees since they wouldn't work anyway
                    if (!compactHierarchy.IsValidCompactNodeID(treeUpdate.treeCompactNodeID))
                        continue;

                    if (!compactHierarchy.IsNodeDirty(treeUpdate.treeCompactNodeID))
                        continue;
                    
                    treeUpdate.PreMeshUpdateDispose();
                }

                JobHandle.ScheduleBatchedJobs();

                //
                // Wait for our scheduled mesh update jobs to finish, ensure our components are setup correctly, and upload our mesh data to the meshes
                //

                #region Finish Mesh Updates / Update Components (not jobified)
                Profiler.BeginSample("FinishMeshUpdates");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate          = ref s_TreeUpdates[t];
                        ref var compactHierarchy    = ref CompactHierarchyManager.GetHierarchy(treeUpdate.treeCompactNodeID);

                        // Skip invalid trees since they wouldn't work anyway
                        if (!compactHierarchy.IsValidCompactNodeID(treeUpdate.treeCompactNodeID))
                            continue;

                        if (!compactHierarchy.IsNodeDirty(treeUpdate.treeCompactNodeID))
                            continue;

                        var dependencies = JobHandleExtensions.CombineDependencies(treeUpdate.JobHandles.meshDatasJobHandle,
                                                                                   treeUpdate.JobHandles.colliderMeshUpdatesJobHandle,
                                                                                   treeUpdate.JobHandles.debugHelperMeshesJobHandle,
                                                                                   treeUpdate.JobHandles.renderMeshesJobHandle,
                                                                                   treeUpdate.JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                                                   treeUpdate.JobHandles.vertexBufferContents_meshesJobHandle);
                        bool meshUpdated = false;
                        try
                        {
                            for (int b = 0; b < treeUpdate.brushCount; b++)
                            {
                                var brushIndexOrder = treeUpdate.Temporaries.allTreeBrushIndexOrders[b];
                                // TODO: get rid of these crazy legacy flags
                                compactHierarchy.ClearAllStatusFlags(brushIndexOrder.compactNodeID);
                            }

                            // TODO: get rid of these crazy legacy flags
                            compactHierarchy.ClearStatusFlag(treeUpdate.treeCompactNodeID, NodeStatusFlags.TreeNeedsUpdate);
                            compactHierarchy.ClearAllStatusFlags(treeUpdate.treeCompactNodeID);

                            if (treeUpdate.updateCount == 0)
                                continue;

                            if (finishMeshUpdates != null)
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
                        finally
                        {
                            compactHierarchy.ClearAllStatusFlags(treeUpdate.treeCompactNodeID);
                            dependencies.Complete(); // Whatever happens, our jobs need to be completed at this point
                            if (treeUpdate.updateCount > 0 && !meshUpdated)
                                treeUpdate.Temporaries.meshDataArray.Dispose();
                        }
                    }
                }
                finally { Profiler.EndSample(); }
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
                        treeUpdate.FreeTemporaries(ref finalJobHandle);
                    }
                    GeneratorJobPoolManager.Clear();
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
            public int                          maxNodeOrder;
            public int                          updateCount;


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
                public NativeReference<BlobAssetReference<CompactTree>> compactTreeRef;
                public NativeReference<bool>                needRemappingRef;

                public VertexBufferContents                 vertexBufferContents;

                public NativeList<int>                      nodeIDValueToNodeOrderArray;
                public NativeReference<int>                 nodeIDValueToNodeOrderOffsetRef;

                public NativeList<BrushData>                brushRenderData;
                public NativeList<SubMeshCounts>            subMeshCounts;
                public NativeListArray<SubMeshSurface>      subMeshSurfaces;

                public NativeStream                         dataStream1;
                public NativeStream                         dataStream2;
                public NativeStream                         intersectingBrushesStream;

            
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


            #region In Between JobHandles
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

            public JobHandle lastJobHandle;

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
                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);

                // Make sure that if we, somehow, run this while parts of the previous update is still running, we wait for the previous run to complete

                // TODO: STORE THIS ON THE TREE!!! THIS WILL BE FORGOTTEN
                this.lastJobHandle.Complete();
                this.lastJobHandle = default;

                const Allocator allocator = Allocator.Persistent;

                Profiler.BeginSample("CSG_Allocations");

                Temporaries.parameterCounts                = new NativeArray<int>(chiselLookupValues.parameters.Length, allocator);
                Temporaries.transformTreeBrushIndicesList  = new NativeList<NodeOrderNodeID>(allocator);
                Temporaries.brushBoundsUpdateList          = new NativeList<NodeOrderNodeID>(allocator);
                Temporaries.nodes                          = new NativeList<CompactNodeID>(allocator);
                Temporaries.brushes                        = new NativeList<CompactNodeID>(allocator);

                compactHierarchy.GetTreeNodes(Temporaries.nodes, Temporaries.brushes);
                    
                #region Allocations/Resize
                var newBrushCount = Temporaries.brushes.Length;
                chiselLookupValues.EnsureCapacity(newBrushCount);

                this.brushCount   = newBrushCount;
                this.maxNodeOrder = this.brushCount;

                Temporaries.meshDataArray   = default;
                Temporaries.meshDatas       = new NativeList<UnityEngine.Mesh.MeshData>(allocator);

                //var triangleArraySize         = GeometryMath.GetTriangleArraySize(newBrushCount);
                //var intersectionCount         = math.max(1, triangleArraySize);
                Temporaries.brushesThatNeedIndirectUpdateHashMap = new NativeHashSet<IndexOrder>(brushCount, allocator);
                Temporaries.brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(brushCount, allocator);

                // TODO: find actual vertex count
                Temporaries.outputSurfaceVertices           = new NativeList<float3>(65535 * 10, allocator);

                Temporaries.outputSurfaces                  = new NativeList<BrushIntersectionLoop>(brushCount * 16, allocator);
                Temporaries.brushIntersectionsWith          = new NativeList<BrushIntersectWith>(brushCount, allocator);

                Temporaries.nodeIDValueToNodeOrderOffsetRef = new NativeReference<int>(allocator);
                Temporaries.surfaceCountRef                 = new NativeReference<int>(allocator);
                Temporaries.compactTreeRef                  = new NativeReference<BlobAssetReference<CompactTree>>(allocator);
                Temporaries.needRemappingRef                = new NativeReference<bool>(allocator);

                Temporaries.uniqueBrushPairs                = new NativeList<BrushPair2>(brushCount * 16, allocator);

                Temporaries.rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(brushCount, allocator);
                Temporaries.allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(brushCount, allocator);
                Temporaries.allBrushMeshIDs                 = new NativeArray<int>(brushCount, allocator);
                Temporaries.brushRenderData                 = new NativeList<BrushData>(brushCount, allocator);
                Temporaries.allTreeBrushIndexOrders         = new NativeList<IndexOrder>(brushCount, allocator);
                Temporaries.allTreeBrushIndexOrders.Clear();
                Temporaries.allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);

                Temporaries.outputSurfacesRange             = new NativeArray<int2>(brushCount, allocator);
                Temporaries.brushIntersectionsWithRange     = new NativeArray<int2>(brushCount, allocator);
                Temporaries.nodeIDValueToNodeOrderArray     = new NativeList<int>(brushCount, allocator);
                Temporaries.brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(brushCount, allocator);

                Temporaries.brushBrushIntersections         = new NativeListArray<BrushIntersectWith>(16, allocator);
                Temporaries.brushBrushIntersections.ResizeExact(brushCount);

                Temporaries.subMeshSurfaces            = new NativeListArray<SubMeshSurface>(allocator);
                Temporaries.subMeshCounts              = new NativeList<SubMeshCounts>(allocator);

                Temporaries.colliderMeshUpdates        = new NativeList<ChiselMeshUpdate>(allocator);
                Temporaries.debugHelperMeshes          = new NativeList<ChiselMeshUpdate>(allocator);
                Temporaries.renderMeshes               = new NativeList<ChiselMeshUpdate>(allocator);


                Temporaries.loopVerticesLookup          = new NativeListArray<float3>(this.brushCount, allocator);
                Temporaries.loopVerticesLookup.ResizeExact(this.brushCount);

                Temporaries.vertexBufferContents.EnsureInitialized();

                var parameterPtr = (ChiselLayerParameters*)chiselLookupValues.parameters.GetUnsafePtr();
                // Regular index operator will return a copy instead of a reference *sigh*
                for (int l = 0; l < SurfaceLayers.ParameterCount; l++)
                    parameterPtr[l].Clear();


                #region MeshQueries
                // TODO: have more control over the queries
                Temporaries.meshQueries         = MeshQuery.DefaultQueries.ToNativeArray(allocator);
                Temporaries.meshQueriesLength   = Temporaries.meshQueries.Length;
                Temporaries.meshQueries.Sort(meshQueryComparer);
                #endregion

                Temporaries.subMeshSurfaces.ResizeExact(Temporaries.meshQueriesLength);
                for (int i = 0; i < Temporaries.meshQueriesLength; i++)
                    Temporaries.subMeshSurfaces.AllocateWithCapacityForIndex(i, 1000);

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

                Temporaries.basePolygonDisposeList             = new NativeList<BlobAssetReference<BasePolygonsBlob>>(chiselLookupValues.basePolygonCache.Length, allocator);
                Temporaries.treeSpaceVerticesDisposeList       = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(chiselLookupValues.treeSpaceVerticesCache.Length, allocator);
                Temporaries.brushesTouchedByBrushDisposeList   = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(chiselLookupValues.brushesTouchedByBrushCache.Length, allocator);
                Temporaries.routingTableDisposeList            = new NativeList<BlobAssetReference<RoutingTable>>(chiselLookupValues.routingTableCache.Length, allocator);
                Temporaries.brushTreeSpacePlaneDisposeList     = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(chiselLookupValues.brushTreeSpacePlaneCache.Length, allocator);
                Temporaries.brushRenderBufferDisposeList       = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(chiselLookupValues.brushRenderBufferCache.Length, allocator);

                #endregion
                Profiler.EndSample();
            }

            const bool runInParallelDefault = true;
            public void RunMeshInitJobs(ref CompactHierarchy compactHierarchy, JobHandle dependsOn)
            {
                // TODO: Remove the need for this Complete
                dependsOn.Complete(); // <-- Initialize has code that depends on the current state of the tree

                // Reset everything
                JobHandles = default;
                Temporaries = default;
                Initialize();

                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobCache;
                {
                    #region Build Lookup Tables
                    Profiler.BeginSample("Job_BuildLookupTables");
                    try
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
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region CacheRemapping
                    Profiler.BeginSample("Job_CacheRemapping");
                    try
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
                    finally { Profiler.EndSample(); }
                    #endregion
                    
                    #region Update BrushID Values
                    Profiler.BeginSample("Job_UpdateBrushIDValues");
                    try
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
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region Find Modified Brushes
                    Profiler.BeginSample("Job_FindModifiedBrushes");
                    try
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
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region Invalidate Brushes
                    Profiler.BeginSample("Job_InvalidateBrushes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var invalidateBrushesJob = new InvalidateBrushesJob
                        {
                            // Read
                            needRemappingRef                = Temporaries.needRemappingRef,
                            rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders       .AsJobArray(runInParallel).AsReadOnly(),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel).AsReadOnly(),
                            brushes                         = Temporaries.brushes                           .AsJobArray(runInParallel).AsReadOnly(),
                            brushCount                      = this.brushCount,
                            nodeIDValueToNodeOrderArray     = Temporaries.nodeIDValueToNodeOrderArray       .AsJobArray(runInParallel).AsReadOnly(),
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
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region Update BrushMesh IDs
                    Profiler.BeginSample("Job_UpdateBrushMeshIDs");
                    try
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
                    finally { Profiler.EndSample(); }
                    #endregion
                }
            }
                
            public void RunMeshUpdateJobs(ref CompactHierarchy compactHierarchy)
            {
                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobCache;

                #region Perform CSG

                #region Prepare
                     
                #region Build Transformations
                Profiler.BeginSample("Job_UpdateTransformations");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var updateTransformationsJob = new UpdateTransformationsJob
                    {
                        // Read
                        transformTreeBrushIndicesList   = Temporaries.transformTreeBrushIndicesList.AsJobArray(runInParallel),
                        //compactHierarchy              = compactHierarchy, //<-- cannot do ref or pointer here
                                                                            //    so we set it below using InitializeHierarchy

                        // Write
                        transformationCache             = chiselLookupValues.transformationCache.AsJobArray(runInParallel)
                    };
                    updateTransformationsJob.InitializeHierarchy(ref compactHierarchy);
                    updateTransformationsJob.Schedule(runInParallel, Temporaries.transformTreeBrushIndicesList, 8,
                        new ReadJobHandles(
                            JobHandles.transformTreeBrushIndicesListJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.transformationCacheJobHandle));
                } 
                finally { Profiler.EndSample(); }
                #endregion

                Profiler.BeginSample("Job_CompactTreeBuilder");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var buildCompactTreeJob = new BuildCompactTreeJob
                    {
                        // Read
                        treeCompactNodeID   = this.treeCompactNodeID,
                        brushes             = Temporaries.brushes.AsArray(),
                        nodes               = Temporaries.nodes.AsArray(),
                        //compactHierarchy  = compactHierarchy,  //<-- cannot do ref or pointer here, 
                                                                 //    so we set it below using InitializeHierarchy

                        // Write
                        compactTreeRef      = Temporaries.compactTreeRef
                    };
                    buildCompactTreeJob.InitializeHierarchy(ref compactHierarchy);
                    buildCompactTreeJob.Schedule(runInParallel,
                        new ReadJobHandles(
                            JobHandles.brushesJobHandle,
                            JobHandles.nodesJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.compactTreeRefJobHandle));
                }
                finally { Profiler.EndSample(); }

                // Create lookup table for all brushMeshBlobs, based on the node order in the tree
                Profiler.BeginSample("Job_FillBrushMeshBlobLookup");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var fillBrushMeshBlobLookupJob = new FillBrushMeshBlobLookupJob
                    {
                        // Read
                        brushMeshBlobs          = brushMeshBlobs,
                        allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders.AsJobArray(runInParallel),
                        allBrushMeshIDs         = Temporaries.allBrushMeshIDs,

                        // Write
                        brushMeshLookup         = Temporaries.brushMeshLookup,
                        surfaceCountRef         = Temporaries.surfaceCountRef
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
                finally { Profiler.EndSample(); }

                // Invalidate outdated caches for all modified brushes
                Profiler.BeginSample("Job_InvalidateBrushCache");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var invalidateBrushCacheJob = new InvalidateBrushCacheJob
                    {
                        // Read
                        rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders               .AsArray().AsReadOnly(),

                        // Read/Write
                        basePolygonCache            = chiselLookupValues.basePolygonCache,
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache,
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache,
                        routingTableCache           = chiselLookupValues.routingTableCache,
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache,
                        brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache,

                        // Write
                        basePolygonDisposeList              = Temporaries.basePolygonDisposeList            .AsParallelWriter(),
                        treeSpaceVerticesDisposeList        = Temporaries.treeSpaceVerticesDisposeList      .AsParallelWriter(),
                        brushesTouchedByBrushDisposeList    = Temporaries.brushesTouchedByBrushDisposeList  .AsParallelWriter(),
                        routingTableDisposeList             = Temporaries.routingTableDisposeList           .AsParallelWriter(),
                        brushTreeSpacePlaneDisposeList      = Temporaries.brushTreeSpacePlaneDisposeList    .AsParallelWriter(),
                        brushRenderBufferDisposeList        = Temporaries.brushRenderBufferDisposeList      .AsParallelWriter()
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
                finally { Profiler.EndSample(); }

                // Fix up brush order index in cache data (ordering of brushes may have changed)
                Profiler.BeginSample("Job_FixupBrushCacheIndices");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var fixupBrushCacheIndicesJob   = new FixupBrushCacheIndicesJob
                    {
                        // Read
                        allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders           .AsArray().AsReadOnly(),
                        nodeIDValueToNodeOrderArray     = Temporaries.nodeIDValueToNodeOrderArray       .AsArray().AsReadOnly(),
                        nodeIDValueToNodeOrderOffsetRef = Temporaries.nodeIDValueToNodeOrderOffsetRef,

                        // Read Write
                        basePolygonCache                = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                        brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel)
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
                finally { Profiler.EndSample(); }

                // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been modified
                Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                    var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                    {
                        // Read
                        rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders   .AsArray(),
                        transformationCache         = chiselLookupValues.transformationCache    .AsJobArray(runInParallel),
                        brushMeshLookup             = Temporaries.brushMeshLookup               .AsReadOnly(),
                        //ref hierarchyIDLookup     = ref CompactHierarchyManager.HierarchyIDLookup,    //<-- cannot do ref or pointer here
                                                                                                        //    so we set it below using InitializeHierarchy

                        // Read/Write
                        hierarchyList               = CompactHierarchyManager.HierarchyList,
                        
                        // Write
                        brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache   .AsJobArray(runInParallel),
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
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
                finally { Profiler.EndSample(); }

                // Find all pairs of brushes that intersect, for those brushes that have been modified
                Profiler.BeginSample("Job_FindAllBrushIntersectionPairs");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                    // TODO: optimize, use hashed grid
                    var findAllBrushIntersectionPairsJob = new FindAllBrushIntersectionPairsJob
                    {
                        // Read
                        allTreeBrushIndexOrders     = Temporaries.allTreeBrushIndexOrders           .AsArray().AsReadOnly(),
                        transformationCache         = chiselLookupValues.transformationCache        .AsJobArray(runInParallel),
                        brushMeshLookup             = Temporaries.brushMeshLookup                   .AsReadOnly(),
                        brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache   .AsJobArray(runInParallel),
                        rebuildTreeBrushIndexOrders = Temporaries.rebuildTreeBrushIndexOrders       .AsArray().AsReadOnly(),
                            
                        // Read / Write
                        brushBrushIntersections     = Temporaries.brushBrushIntersections,
                            
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
                finally { Profiler.EndSample(); }

                // Find all brushes that touch the brushes that have been modified
                Profiler.BeginSample("Job_FindUniqueIndirectBrushIntersections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: optimize, use hashed grid
                    var findUniqueIndirectBrushIntersectionsJob = new FindUniqueIndirectBrushIntersectionsJob
                    {
                        // Read
                        brushesThatNeedIndirectUpdateHashMap     = Temporaries.brushesThatNeedIndirectUpdateHashMap,
                        
                        // Write
                        brushesThatNeedIndirectUpdate            = Temporaries.brushesThatNeedIndirectUpdate
                    };
                    findUniqueIndirectBrushIntersectionsJob.Schedule(runInParallel, 
                        new ReadJobHandles(
                            JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.brushesThatNeedIndirectUpdateJobHandle));
                }
                finally { Profiler.EndSample(); }

                // Invalidate the cache for the brushes that have been indirectly modified (touch a brush that has changed)
                Profiler.BeginSample("Job_InvalidateBrushCache_Indirect");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var invalidateIndirectBrushCacheJob = new InvalidateIndirectBrushCacheJob
                    {
                        // Read
                        brushesThatNeedIndirectUpdate   = Temporaries.brushesThatNeedIndirectUpdate     .AsJobArray(runInParallel),

                        // Read Write
                        basePolygonCache                = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                        treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                        brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                        routingTableCache               = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel),
                        brushTreeSpacePlaneCache        = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                        brushRenderBufferCache          = chiselLookupValues.brushRenderBufferCache     .AsJobArray(runInParallel)
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
                finally { Profiler.EndSample(); }

                // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been indirectly modified
                Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds_Indirect");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                    {
                        // Read
                        rebuildTreeBrushIndexOrders = Temporaries.brushesThatNeedIndirectUpdate     .AsJobArray(runInParallel),
                        transformationCache         = chiselLookupValues.transformationCache        .AsJobArray(runInParallel),
                        brushMeshLookup             = Temporaries.brushMeshLookup                   .AsReadOnly(),
                        //ref hierarchyIDLookup     = ref CompactHierarchyManager.HierarchyIDLookup,    //<-- cannot do ref or pointer here
                                                                                                        //    so we set it below using InitializeHierarchy

                        // Read/Write
                        hierarchyList               = CompactHierarchyManager.HierarchyList,
                        
                        
                        // Write
                        brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache   .AsJobArray(runInParallel),
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
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
                finally { Profiler.EndSample(); }

                // Find all pairs of brushes that intersect, for those brushes that have been indirectly modified
                Profiler.BeginSample("Job_FindAllBrushIntersectionPairs_Indirect");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: optimize, use hashed grid
                    var findAllIndirectBrushIntersectionPairsJob = new FindAllIndirectBrushIntersectionPairsJob
                    {
                        // Read
                        allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders           .AsArray().AsReadOnly(),
                        transformationCache             = chiselLookupValues.transformationCache        .AsJobArray(runInParallel),
                        brushMeshLookup                 = Temporaries.brushMeshLookup                   .AsReadOnly(),
                        brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache   .AsJobArray(runInParallel),
                        brushesThatNeedIndirectUpdate   = Temporaries.brushesThatNeedIndirectUpdate     .AsJobArray(runInParallel),

                        // Read / Write
                        brushBrushIntersections         = Temporaries.brushBrushIntersections
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
                finally { Profiler.EndSample(); }

                // Add brushes that need to be indirectly updated to our list of brushes that need updates
                Profiler.BeginSample("Job_AddIndirectUpdatedBrushesToListAndSort");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var addIndirectUpdatedBrushesToListAndSortJob = new AddIndirectUpdatedBrushesToListAndSortJob
                    {
                        // Read
                        allTreeBrushIndexOrders         = Temporaries.allTreeBrushIndexOrders        .AsArray().AsReadOnly(),
                        brushesThatNeedIndirectUpdate   = Temporaries.brushesThatNeedIndirectUpdate  .AsJobArray(runInParallel),
                        rebuildTreeBrushIndexOrders     = Temporaries.rebuildTreeBrushIndexOrders    .AsArray().AsReadOnly(),

                        // Write
                        allUpdateBrushIndexOrders       = Temporaries.allUpdateBrushIndexOrders      .AsParallelWriter(),
                    };
                    addIndirectUpdatedBrushesToListAndSortJob.Schedule(runInParallel, 
                        new ReadJobHandles(
                            JobHandles.allTreeBrushIndexOrdersJobHandle,
                            JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                            JobHandles.rebuildTreeBrushIndexOrdersJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.allUpdateBrushIndexOrdersJobHandle));
                }
                finally { Profiler.EndSample(); }

                // Gather all found pairs of brushes that intersect with each other and cache them
                Profiler.BeginSample("Job_GatherAndStoreBrushIntersections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var gatherBrushIntersectionsJob = new GatherBrushIntersectionPairsJob
                    {
                        // Read
                        brushBrushIntersections         = Temporaries.brushBrushIntersections,

                        // Write
                        brushIntersectionsWith          = Temporaries.brushIntersectionsWith.GetUnsafeList(),
                        brushIntersectionsWithRange     = Temporaries.brushIntersectionsWithRange
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
                        treeCompactNodeID           = treeCompactNodeID,
                        compactTreeRef              = Temporaries.compactTreeRef,
                        allTreeBrushIndexOrders     = Temporaries.allTreeBrushIndexOrders            .AsJobArray(runInParallel),
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders          .AsJobArray(runInParallel),

                        brushIntersectionsWith      = Temporaries.brushIntersectionsWith             .AsJobArray(runInParallel),
                        brushIntersectionsWithRange = Temporaries.brushIntersectionsWithRange        .AsReadOnly(),

                        // Write
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel)
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
                } finally { Profiler.EndSample(); }
                #endregion
                /*
                #region Find Modified Brushes
                Profiler.BeginSample("Job_UpdateBrushBounds");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var updateBrushBoundsJobJob = new UpdateBrushBoundsJob
                    {
                        // Read
                        brushBoundsUpdateList   = Temporaries.brushBoundsUpdateList,
                        brushMeshBlobCache      = brushMeshBlobs,
                        transformationCache     = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                        //ref hierarchyIDLookup = ref CompactHierarchyManager.HierarchyIDLookup, //<-- cannot do ref or pointer here
                                                                                                         //    so we set it below using InitializeHierarchy

                        // Read/Write
                        hierarchyList           = CompactHierarchyManager.HierarchyList,
                    };
                    updateBrushBoundsJobJob.InitializeLookups();
                    updateBrushBoundsJobJob.Schedule(runInParallel,
                        new ReadJobHandles(
                            JobHandles.brushBoundsUpdateListJobHandle,
                            JobHandles.transformationCacheJobHandle,
                            JobHandles.brushMeshBlobsLookupJobHandle,
                            JobHandles.hierarchyIDJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.hierarchyListJobHandle));
                }
                finally { Profiler.EndSample(); }
                #endregion
                */
                //
                // Ensure vertices that should be identical on different brushes, ARE actually identical
                //
                /*
                #region Merge vertices
                Profiler.BeginSample("Job_MergeTouchingBrushVertices");
                try
                {
                    const bool runInParallel = runInParallelDefault;

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

                    // Merges original brush vertices together when they are close to avoid t-junctions
                    var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                    {
                        // Read
                        treeBrushIndexOrders        = Temporaries.allUpdateBrushIndexOrders.AsJobArray(sequential),
                        brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(sequential),

                        // Read Write
                        treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(sequential),
                    };
                    mergeTouchingBrushVerticesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                        new ReadJobHandles(
                            JobHandles.allUpdateBrushIndexOrdersJobHandle,
                            JobHandles.brushesTouchedByBrushCacheJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.treeSpaceVerticesCacheJobHandle));
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
                    var findBrushPairsJob = new FindBrushPairsJob
                    {
                        // Read
                        maxOrder                    = brushCount,
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders         .AsJobArray(runInParallel),
                        brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),

                        // Read (Re-allocate) / Write
                        uniqueBrushPairs            = Temporaries.uniqueBrushPairs.GetUnsafeList()
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
                                                        Allocator.TempJob);

                    var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                    {
                        // Read
                        uniqueBrushPairs            = Temporaries.uniqueBrushPairs          .AsJobArray(runInParallel),
                        transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                        brushMeshLookup             = Temporaries.brushMeshLookup           .AsReadOnly(),

                        // Write
                        intersectingBrushesStream   = Temporaries.intersectingBrushesStream .AsWriter()
                    };
                    prepareBrushPairIntersectionsJob.Schedule(runInParallel, Temporaries.uniqueBrushPairs, 1,
                        new ReadJobHandles(
                            JobHandles.uniqueBrushPairsJobHandle,
                            JobHandles.transformationCacheJobHandle,
                            JobHandles.brushMeshLookupJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.intersectingBrushesStreamJobHandle));
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GenerateBasePolygonLoops");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                    var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobsJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders         .AsJobArray(runInParallel),
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                        brushMeshLookup             = Temporaries.brushMeshLookup                   .AsReadOnly(),
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),

                        // Write
                        basePolygonCache            = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel)
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
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_UpdateBrushTreeSpacePlanes");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    // TODO: should only do this at creation time + when moved / store with brush component itself
                    var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders .AsJobArray(runInParallel),
                        brushMeshLookup             = Temporaries.brushMeshLookup           .AsReadOnly(),
                        transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),

                        // Write
                        brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsJobArray(runInParallel)
                    };
                    createBrushTreeSpacePlanesJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 16,
                        new ReadJobHandles(
                            JobHandles.allUpdateBrushIndexOrdersJobHandle,
                            JobHandles.brushMeshLookupJobHandle,
                            JobHandles.transformationCacheJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.brushTreeSpacePlaneCacheJobHandle));
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_CreateIntersectionLoops");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    NativeCollection.ScheduleEnsureCapacity(runInParallel, ref Temporaries.outputSurfaces, Temporaries.surfaceCountRef,
                                                        new ReadJobHandles(
                                                            JobHandles.surfaceCountRefJobHandle),
                                                        new WriteJobHandles(
                                                            ref JobHandles.outputSurfacesJobHandle),
                                                        Allocator.TempJob);

                    var createIntersectionLoopsJob = new CreateIntersectionLoopsJob
                    {
                        // Needed for count (forced & unused)
                        uniqueBrushPairs            = Temporaries.uniqueBrushPairs,

                        // Read
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                        treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                        intersectingBrushesStream   = Temporaries.intersectingBrushesStream         .AsReader(),

                        // Write
                        outputSurfaceVertices       = Temporaries.outputSurfaceVertices     .AsParallelWriterExt(),
                        outputSurfaces              = Temporaries.outputSurfaces            .AsParallelWriter()
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
                } finally { Profiler.EndSample(); }
            
                Profiler.BeginSample("Job_GatherOutputSurfaces");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                    {
                        // Read / Write (Sort)
                        outputSurfaces          = Temporaries.outputSurfaces.AsJobArray(runInParallel),

                        // Write
                        outputSurfacesRange     = Temporaries.outputSurfacesRange
                    };
                    gatherOutputSurfacesJob.Schedule(runInParallel, 
                        new ReadJobHandles(
                            JobHandles.outputSurfacesJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.outputSurfacesRangeJobHandle));
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_FindLoopOverlapIntersections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.dataStream1, Temporaries.allUpdateBrushIndexOrders,
                                                        new ReadJobHandles(
                                                            JobHandles.allUpdateBrushIndexOrdersJobHandle
                                                            ),
                                                        new WriteJobHandles(
                                                            ref JobHandles.dataStream1JobHandle
                                                            ),
                                                        Allocator.TempJob);

                    var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders          .AsJobArray(runInParallel),
                        outputSurfaceVertices       = Temporaries.outputSurfaceVertices              .AsJobArray(runInParallel),
                        outputSurfaces              = Temporaries.outputSurfaces                     .AsJobArray(runInParallel),
                        outputSurfacesRange         = Temporaries.outputSurfacesRange                .AsReadOnly(),
                        maxNodeOrder                = maxNodeOrder,
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                        basePolygonCache            = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),

                        // Read Write
                        loopVerticesLookup          = Temporaries.loopVerticesLookup,
                            
                        // Write
                        output                      = Temporaries.dataStream1                        .AsWriter()
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
                    var mergeTouchingBrushVerticesIndirectJob = new MergeTouchingBrushVerticesIndirectJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders         .AsJobArray(runInParallel),
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                        treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),

                        // Read Write
                        loopVerticesLookup          = Temporaries.loopVerticesLookup,
                    };
                    mergeTouchingBrushVerticesIndirectJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                        new ReadJobHandles(
                            JobHandles.allUpdateBrushIndexOrdersJobHandle,
                            JobHandles.treeSpaceVerticesCacheJobHandle,
                            JobHandles.brushesTouchedByBrushCacheJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.loopVerticesLookupJobHandle));
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
                    // TODO: determine when a brush is completely inside another brush (might not have *any* intersection loops)
                    var createRoutingTableJob = new CreateRoutingTableJob // Build categorization trees for brushes
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders         .AsJobArray(runInParallel),
                        brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                        compactTreeRef              = Temporaries.compactTreeRef,

                        // Write
                        routingTableLookup          = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel)
                    };
                    createRoutingTableJob.Schedule(runInParallel, Temporaries.allUpdateBrushIndexOrders, 1,
                        new ReadJobHandles(
                            JobHandles.allUpdateBrushIndexOrdersJobHandle,
                            JobHandles.brushesTouchedByBrushCacheJobHandle,
                            JobHandles.compactTreeRefJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.routingTableCacheJobHandle));
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PerformCSG");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    NativeCollection.ScheduleConstruct(runInParallel, out Temporaries.dataStream2, Temporaries.allUpdateBrushIndexOrders,
                                                        new ReadJobHandles(
                                                            JobHandles.allUpdateBrushIndexOrdersJobHandle
                                                            ),
                                                        new WriteJobHandles(
                                                            ref JobHandles.dataStream2JobHandle
                                                            ),
                                                        Allocator.TempJob);

                    // Perform CSG
                    var performCSGJob = new PerformCSGJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders         .AsJobArray(runInParallel),
                        routingTableCache           = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel),
                        brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                        brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                        input                       = Temporaries.dataStream1                       .AsReader(),
                        loopVerticesLookup          = Temporaries.loopVerticesLookup,

                        // Write
                        output                      = Temporaries.dataStream2                       .AsWriter(),
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
                    var generateSurfaceTrianglesJob = new GenerateSurfaceTrianglesJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders .AsJobArray(runInParallel),
                        basePolygonCache            = chiselLookupValues.basePolygonCache   .AsJobArray(runInParallel),
                        transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                        input                       = Temporaries.dataStream2               .AsReader(),
                        meshQueries                 = Temporaries.meshQueries               .AsReadOnly(),

                        // Write
                        brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache .AsJobArray(runInParallel)
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
                finally { Profiler.EndSample(); }
                #endregion


                //
                // Create meshes out of ALL the generated and cached surfaces
                //
                
                Profiler.BeginSample("Job_FindBrushRenderBuffers");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    NativeCollection.ScheduleEnsureCapacity(runInParallel, ref Temporaries.brushRenderData, Temporaries.allTreeBrushIndexOrders,
                                                        new ReadJobHandles(
                                                            JobHandles.allTreeBrushIndexOrdersJobHandle),
                                                        new WriteJobHandles(
                                                            ref JobHandles.brushRenderDataJobHandle),
                                                        Allocator.TempJob);

                    var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                    {
                        // Read
                        meshQueryLength         = Temporaries.meshQueriesLength,
                        allTreeBrushIndexOrders = Temporaries.allTreeBrushIndexOrders       .AsArray(),
                        brushRenderBufferCache  = chiselLookupValues.brushRenderBufferCache .AsJobArray(runInParallel),

                        // Write
                        brushRenderData         = Temporaries.brushRenderData,
                        subMeshCounts           = Temporaries.subMeshCounts,
                        subMeshSections         = Temporaries.vertexBufferContents.subMeshSections,
                    };
                    findBrushRenderBuffersJob.Schedule(runInParallel, 
                        new ReadJobHandles(
                            JobHandles.meshQueriesJobHandle,
                            JobHandles.allTreeBrushIndexOrdersJobHandle,
                            JobHandles.brushRenderBufferCacheJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.brushRenderDataJobHandle,
                            ref JobHandles.subMeshSurfacesJobHandle,
                            ref JobHandles.subMeshCountsJobHandle,
                            ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle));
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PrepareSubSections");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var prepareSubSectionsJob = new PrepareSubSectionsJob
                    {
                        // Read
                        meshQueries         = Temporaries.meshQueries       .AsReadOnly(),
                        brushRenderData     = Temporaries.brushRenderData   .AsJobArray(runInParallel),

                        // Write
                        subMeshSurfaces     = Temporaries.subMeshSurfaces,
                    };
                    prepareSubSectionsJob.Schedule(runInParallel, Temporaries.meshQueriesLength, 1,
                        new ReadJobHandles(
                            JobHandles.meshQueriesJobHandle,
                            JobHandles.brushRenderDataJobHandle,
                            JobHandles.sectionsJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.subMeshSurfacesJobHandle));
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_SortSurfaces");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var sortSurfacesParallelJob = new SortSurfacesParallelJob
                    {
                        // Read
                        meshQueries      = Temporaries.meshQueries      .AsReadOnly(),
                        subMeshSurfaces  = Temporaries.subMeshSurfaces,

                        // Write
                        subMeshCounts    = Temporaries.subMeshCounts
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
                        subMeshCounts       = Temporaries.subMeshCounts,

                        // Write
                        subMeshSections     = Temporaries.vertexBufferContents.subMeshSections,
                    };
                    gatherSurfacesJob.Schedule(runInParallel,
                        new ReadJobHandles(
                            JobHandles.sectionsJobHandle,
                            JobHandles.subMeshSurfacesJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.subMeshCountsJobHandle,
                            ref JobHandles.vertexBufferContents_subMeshSectionsJobHandle));
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_AllocateVertexBuffers");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                    {
                        // Read
                        subMeshSections         = Temporaries.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),

                        // Read Write
                        triangleBrushIndices    = Temporaries.vertexBufferContents.triangleBrushIndices
                    };
                    allocateVertexBuffersJob.Schedule(runInParallel,
                        new ReadJobHandles(
                            JobHandles.vertexBufferContents_subMeshSectionsJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle));
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GenerateMeshDescription");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                    {
                        // Read
                        subMeshCounts       = Temporaries.subMeshCounts.AsJobArray(runInParallel),

                        // Read Write
                        meshDescriptions    = Temporaries.vertexBufferContents.meshDescriptions
                    };
                    generateMeshDescriptionJob.Schedule(runInParallel,
                        new ReadJobHandles(
                            JobHandles.subMeshCountsJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.vertexBufferContents_meshDescriptionsJobHandle));
                }
                finally { Profiler.EndSample(); }


                // TODO: store parameterCounts per brush (precalculated), manage these counts in the hierarchy when brushes are added/removed/modified
                //       then we don't need to count them here & don't need to do a "complete" here
                JobHandles.parameterCountsJobHandle.Complete();

                #region Create Meshes
                Profiler.BeginSample("Mesh.AllocateWritableMeshData");
                try
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
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_CopyToMeshes");
                try
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

                    var renderCopyToMeshJob = new CopyToRenderMeshJob
                    {
                        // Read
                        subMeshSections         = Temporaries.vertexBufferContents.subMeshSections      .AsJobArray(runInParallel),
                        subMeshCounts           = Temporaries.subMeshCounts                             .AsJobArray(runInParallel),
                        subMeshSurfaces         = Temporaries.subMeshSurfaces,
                        renderDescriptors       = Temporaries.vertexBufferContents.renderDescriptors,
                        renderMeshes            = Temporaries.renderMeshes,

                        // Read/Write
                        triangleBrushIndices    = Temporaries.vertexBufferContents.triangleBrushIndices,
                        meshes                  = Temporaries.vertexBufferContents.meshes,
                    };
                    renderCopyToMeshJob.Schedule(runInParallel, Temporaries.renderMeshes, 1,
                        new ReadJobHandles(
                            JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandles.subMeshCountsJobHandle,
                            JobHandles.subMeshSurfacesJobHandle,
                            JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                            JobHandles.renderMeshesJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                            ref JobHandles.vertexBufferContents_meshesJobHandle));

                    var helperCopyToMeshJob = new CopyToRenderMeshJob
                    {
                        // Read
                        subMeshSections         = Temporaries.vertexBufferContents.subMeshSections  .AsJobArray(runInParallel),
                        subMeshCounts           = Temporaries.subMeshCounts                         .AsJobArray(runInParallel),
                        subMeshSurfaces         = Temporaries.subMeshSurfaces,
                        renderDescriptors       = Temporaries.vertexBufferContents.renderDescriptors,
                        renderMeshes            = Temporaries.debugHelperMeshes,

                        // Read/Write
                        triangleBrushIndices    = Temporaries.vertexBufferContents.triangleBrushIndices,
                        meshes                  = Temporaries.vertexBufferContents.meshes,
                    };
                    helperCopyToMeshJob.Schedule(runInParallel, Temporaries.debugHelperMeshes, 1,
                        new ReadJobHandles(
                            JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandles.subMeshCountsJobHandle,
                            JobHandles.subMeshSurfacesJobHandle,
                            JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                            JobHandles.debugHelperMeshesJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                            ref JobHandles.vertexBufferContents_meshesJobHandle));

                    var colliderCopyToMeshJob = new CopyToColliderMeshJob
                    {
                        // Read
                        subMeshSections         = Temporaries.vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                        subMeshCounts           = Temporaries.subMeshCounts.AsJobArray(runInParallel),
                        subMeshSurfaces         = Temporaries.subMeshSurfaces,
                        colliderDescriptors     = Temporaries.vertexBufferContents.colliderDescriptors,
                        colliderMeshes          = Temporaries.colliderMeshUpdates,
                            
                        // Read/Write
                        meshes                  = Temporaries.vertexBufferContents.meshes,
                    };
                    colliderCopyToMeshJob.Schedule(runInParallel, Temporaries.colliderMeshUpdates, 16,
                        new ReadJobHandles(
                            JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                            JobHandles.subMeshCountsJobHandle,
                            JobHandles.subMeshSurfacesJobHandle,
                            JobHandles.vertexBufferContents_colliderDescriptorsJobHandle,
                            JobHandles.colliderMeshUpdatesJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.subMeshCountsJobHandle, // Why?
                            ref JobHandles.vertexBufferContents_meshesJobHandle));
                }
                finally { Profiler.EndSample(); }
                #endregion

                //
                // Finally store the generated surfaces into our cache
                //

                #region Store cached values back into cache (by node Index)
                Profiler.BeginSample("Job_StoreToCache");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var storeToCacheJob = new StoreToCacheJob
                    {
                        // Read
                        allTreeBrushIndexOrders     = Temporaries.allTreeBrushIndexOrders.AsJobArray(runInParallel),
                        brushTreeSpaceBoundCache    = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                        brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache.AsJobArray(runInParallel),

                        // Read Write
                        brushTreeSpaceBoundLookup   = chiselLookupValues.brushTreeSpaceBoundLookup,
                        brushRenderBufferLookup     = chiselLookupValues.brushRenderBufferLookup
                    };
                    storeToCacheJob.Schedule(runInParallel,
                        new ReadJobHandles(
                            JobHandles.allTreeBrushIndexOrdersJobHandle,
                            JobHandles.brushTreeSpaceBoundCacheJobHandle,
                            JobHandles.brushRenderBufferCacheJobHandle),
                        new WriteJobHandles(
                            ref JobHandles.storeToCacheJobHandle));
                }
                finally { Profiler.EndSample(); }
                #endregion

                #region Create wireframes for all new/modified brushes
                Profiler.BeginSample("Job_UpdateBrushOutline");
                try
                {
                    const bool runInParallel = runInParallelDefault;
                    var updateBrushOutlineJob = new UpdateBrushOutlineJob
                    {
                        // Read
                        allUpdateBrushIndexOrders   = Temporaries.allUpdateBrushIndexOrders,
                        brushMeshBlobs              = brushMeshBlobs

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
                finally { Profiler.EndSample(); }
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
                
                lastJobHandle.AddDependency(Temporaries.loopVerticesLookup           .Dispose(dependencies));
                
                lastJobHandle.AddDependency(Temporaries.transformTreeBrushIndicesList.Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushBoundsUpdateList        .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushes                      .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.nodes                        .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.brushRenderData              .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.subMeshCounts                .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.subMeshSurfaces              .Dispose(dependencies));
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
                lastJobHandle.AddDependency(Temporaries.brushBrushIntersections             .Dispose(dependencies));
                
                
                // Note: cannot use "IsCreated" on this job, for some reason it won't be scheduled and then complain that it's leaking? Bug in IsCreated?
                lastJobHandle.AddDependency(Temporaries.meshQueries.Dispose(dependencies));

                lastJobHandle.AddDependency(NativeCollection.DisposeDeep(Temporaries.basePolygonDisposeList,           dependencies),
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
                lastJobHandle.AddDependency(Temporaries.allTreeBrushIndexOrders .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.colliderMeshUpdates     .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.debugHelperMeshes       .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.renderMeshes            .Dispose(dependencies));
                lastJobHandle.AddDependency(Temporaries.meshDatas               .Dispose(dependencies));                
                lastJobHandle.AddDependency(Temporaries.vertexBufferContents    .Dispose(dependencies));

                return lastJobHandle;
            }
        }
    }
}
