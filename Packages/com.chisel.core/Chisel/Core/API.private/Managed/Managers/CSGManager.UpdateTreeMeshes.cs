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
                Profiler.EndSample();
                generatorPoolJobHandle.Complete();
                #endregion

                #region Schedule Mesh Update Jobs
                Profiler.BeginSample("CSG_RunMeshUpdateJobs");
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

                    treeUpdate.Initialize();
                    treeUpdate.RunMeshUpdateJobs();
                    treeUpdateLength++;
                }
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
                            if (treeUpdate.updateCount == 0)
                                continue;

                            for (int b = 0; b < treeUpdate.brushCount; b++)
                            {
                                var brushIndexOrder = treeUpdate.allTreeBrushIndexOrders[b];
                                // TODO: get rid of these crazy legacy flags
                                compactHierarchy.ClearAllStatusFlags(brushIndexOrder.compactNodeID);
                            }

                            // TODO: get rid of these crazy legacy flags
                            compactHierarchy.ClearStatusFlag(treeUpdate.treeCompactNodeID, NodeStatusFlags.TreeNeedsUpdate);
                            //compactHierarchy.SetStatusFlag(treeUpdate.treeCompactNodeID, NodeStatusFlags.TreeMeshNeedsUpdate);
                            //if (!compactHierarchy.IsStatusFlagSet(treeUpdate.treeCompactNodeID, NodeStatusFlags.TreeMeshNeedsUpdate))
                            //    continue;

                            //bool wasDirty = compactHierarchy.IsNodeDirty(treeUpdate.treeCompactNodeID);

                            compactHierarchy.ClearAllStatusFlags(treeUpdate.treeCompactNodeID);

                            // Don't update the mesh if the tree hasn't actually been modified
                            //if (!wasDirty)
                            //    continue;

                            if (finishMeshUpdates != null)
                            {
                                meshUpdated = true;
                                var usedMeshCount = finishMeshUpdates(treeUpdate.tree, ref treeUpdate.vertexBufferContents,
                                                                        treeUpdate.meshDataArray,
                                                                        treeUpdate.colliderMeshUpdates,
                                                                        treeUpdate.debugHelperMeshes,
                                                                        treeUpdate.renderMeshes,
                                                                        dependencies);
                            }
                        }
                        finally
                        {
                            compactHierarchy.ClearAllStatusFlags(treeUpdate.treeCompactNodeID);
                            dependencies.Complete(); // Whatever happens, our jobs need to be completed at this point
                            if (treeUpdate.updateCount > 0 && !meshUpdated)
                                treeUpdate.meshDataArray.Dispose();
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

                        // Combine all JobHandles of all jobs to ensure that we wait for ALL of them to finish 
                        // before we dispose of our temporaries.
                        // Eventually we might want to put this in between other jobs, but for now this is safer
                        // to work with while things are still being re-arranged.
                        var dependencies = JobHandleExtensions.CombineDependencies(
                                                        JobHandleExtensions.CombineDependencies(
                                                            treeUpdate.JobHandles.allBrushMeshIDsJobHandle,
                                                            treeUpdate.JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                            treeUpdate.JobHandles.brushIDValuesJobHandle,
                                                            treeUpdate.JobHandles.basePolygonCacheJobHandle,
                                                            treeUpdate.JobHandles.brushBrushIntersectionsJobHandle,
                                                            treeUpdate.JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                            treeUpdate.JobHandles.brushRenderBufferCacheJobHandle,
                                                            treeUpdate.JobHandles.brushRenderDataJobHandle,
                                                            treeUpdate.JobHandles.brushTreeSpacePlaneCacheJobHandle),
                                                        JobHandleExtensions.CombineDependencies(
                                                            treeUpdate.JobHandles.brushMeshBlobsLookupJobHandle,
                                                            treeUpdate.JobHandles.brushMeshLookupJobHandle,
                                                            treeUpdate.JobHandles.brushIntersectionsWithJobHandle,
                                                            treeUpdate.JobHandles.brushIntersectionsWithRangeJobHandle,
                                                            treeUpdate.JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                            treeUpdate.JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                            treeUpdate.JobHandles.brushTreeSpaceBoundCacheJobHandle),
                                                        JobHandleExtensions.CombineDependencies(
                                                            treeUpdate.JobHandles.dataStream1JobHandle,
                                                            treeUpdate.JobHandles.dataStream2JobHandle,
                                                            treeUpdate.JobHandles.intersectingBrushesStreamJobHandle,
                                                            treeUpdate.JobHandles.loopVerticesLookupJobHandle,
                                                            treeUpdate.JobHandles.meshQueriesJobHandle,
                                                            treeUpdate.JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                            treeUpdate.JobHandles.outputSurfaceVerticesJobHandle,
                                                            treeUpdate.JobHandles.outputSurfacesJobHandle,
                                                            treeUpdate.JobHandles.outputSurfacesRangeJobHandle),
                                                        JobHandleExtensions.CombineDependencies(
                                                            treeUpdate.JobHandles.routingTableCacheJobHandle,
                                                            treeUpdate.JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                            treeUpdate.JobHandles.sectionsJobHandle,
                                                            treeUpdate.JobHandles.subMeshSurfacesJobHandle,
                                                            treeUpdate.JobHandles.subMeshCountsJobHandle,
                                                            treeUpdate.JobHandles.treeSpaceVerticesCacheJobHandle,
                                                            treeUpdate.JobHandles.transformationCacheJobHandle,
                                                            treeUpdate.JobHandles.uniqueBrushPairsJobHandle),
                                                        JobHandleExtensions.CombineDependencies(
                                                            treeUpdate.JobHandles.transformTreeBrushIndicesListJobHandle,
                                                            treeUpdate.JobHandles.brushesJobHandle,
                                                            treeUpdate.JobHandles.nodesJobHandle,
                                                            treeUpdate.JobHandles.parametersJobHandle,
                                                            treeUpdate.JobHandles.allKnownBrushMeshIndicesJobHandle,
                                                            treeUpdate.JobHandles.parameterCountsJobHandle),
                                                        JobHandleExtensions.CombineDependencies(
                                                            treeUpdate.JobHandles.storeToCacheJobHandle,

                                                            treeUpdate.JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                            treeUpdate.JobHandles.colliderMeshUpdatesJobHandle,
                                                            treeUpdate.JobHandles.debugHelperMeshesJobHandle,
                                                            treeUpdate.JobHandles.renderMeshesJobHandle,
                                                            treeUpdate.JobHandles.surfaceCountRefJobHandle,
                                                            treeUpdate.JobHandles.compactTreeRefJobHandle,
                                                            treeUpdate.JobHandles.needRemappingRefJobHandle,
                                                            treeUpdate.JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle),
                                                        JobHandleExtensions.CombineDependencies(
                                                            treeUpdate.JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                                            treeUpdate.JobHandles.vertexBufferContents_colliderDescriptorsJobHandle,
                                                            treeUpdate.JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                            treeUpdate.JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                            treeUpdate.JobHandles.vertexBufferContents_meshDescriptionsJobHandle,
                                                            treeUpdate.JobHandles.vertexBufferContents_meshesJobHandle,
                                                            treeUpdate.JobHandles.meshDatasJobHandle)
                                                );
                        dependencies.Complete();
                        treeUpdate.Dispose(dependencies);

                        // We let the final JobHandle dependend on the dependencies, but not on the disposal, 
                        // because we do not need to wait for the disposal of native collections do use our generated data
                        finalJobHandle.AddDependency(dependencies);
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

            #region Meshes
            public UnityEngine.Mesh.MeshDataArray           meshDataArray;
            public NativeList<UnityEngine.Mesh.MeshData>    meshDatas;
            #endregion

            #region All Native Collection Temporaries
            public NativeArray<int>                     parameterCounts;
            public NativeList<NodeOrderNodeID>          transformTreeBrushIndicesList;

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
            #endregion


            #region In Between JobHandles
            internal struct JobHandlesStruct
            {

                internal JobHandle transformTreeBrushIndicesListJobHandle;
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

            internal JobHandle disposeJobHandle;
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
                this.lastJobHandle.Complete();
                this.lastJobHandle = default;

                const Allocator allocator = Allocator.Persistent;

                Profiler.BeginSample("CSG_Allocations");

                this.parameterCounts                = new NativeArray<int>(chiselLookupValues.parameters.Length, allocator);
                this.transformTreeBrushIndicesList  = new NativeList<NodeOrderNodeID>(allocator);
                this.nodes                          = new NativeList<CompactNodeID>(allocator);
                this.brushes                        = new NativeList<CompactNodeID>(allocator);

                compactHierarchy.GetTreeNodes(this.nodes, this.brushes);
                    
                #region Allocations/Resize
                var newBrushCount = this.brushes.Length;
                chiselLookupValues.EnsureCapacity(newBrushCount);

                this.brushCount   = newBrushCount;
                this.maxNodeOrder = this.brushCount;

                meshDataArray   = default;
                meshDatas       = new NativeList<UnityEngine.Mesh.MeshData>(allocator);

                //var triangleArraySize         = GeometryMath.GetTriangleArraySize(newBrushCount);
                //var intersectionCount         = math.max(1, triangleArraySize);
                brushesThatNeedIndirectUpdateHashMap = new NativeHashSet<IndexOrder>(brushCount, allocator);
                brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(brushCount, allocator);

                // TODO: find actual vertex count
                outputSurfaceVertices           = new NativeList<float3>(65535 * 10, allocator); 

                outputSurfaces                  = new NativeList<BrushIntersectionLoop>(brushCount * 16, allocator);
                brushIntersectionsWith          = new NativeList<BrushIntersectWith>(brushCount, allocator);

                nodeIDValueToNodeOrderOffsetRef = new NativeReference<int>(allocator);
                surfaceCountRef                 = new NativeReference<int>(allocator);
                compactTreeRef                  = new NativeReference<BlobAssetReference<CompactTree>>(allocator);
                needRemappingRef                = new NativeReference<bool>(allocator);

                uniqueBrushPairs                = new NativeList<BrushPair2>(brushCount * 16, allocator);
                
                rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(brushCount, allocator);
                allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(brushCount, allocator);
                allBrushMeshIDs                 = new NativeArray<int>(brushCount, allocator);
                brushRenderData                 = new NativeList<BrushData>(brushCount, allocator);
                allTreeBrushIndexOrders         = new NativeList<IndexOrder>(brushCount, allocator);
                allTreeBrushIndexOrders.Clear();
                allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);

                outputSurfacesRange             = new NativeArray<int2>(brushCount, allocator);
                brushIntersectionsWithRange     = new NativeArray<int2>(brushCount, allocator);
                nodeIDValueToNodeOrderArray     = new NativeList<int>(brushCount, allocator);
                brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(brushCount, allocator);

                brushBrushIntersections         = new NativeListArray<BrushIntersectWith>(16, allocator);
                brushBrushIntersections.ResizeExact(brushCount);

                this.subMeshSurfaces            = new NativeListArray<SubMeshSurface>(allocator);
                this.subMeshCounts              = new NativeList<SubMeshCounts>(allocator);

                this.colliderMeshUpdates        = new NativeList<ChiselMeshUpdate>(allocator);
                this.debugHelperMeshes          = new NativeList<ChiselMeshUpdate>(allocator);
                this.renderMeshes               = new NativeList<ChiselMeshUpdate>(allocator);

                
                loopVerticesLookup          = new NativeListArray<float3>(this.brushCount, allocator);
                loopVerticesLookup.ResizeExact(this.brushCount);
                
                vertexBufferContents.EnsureInitialized();

                var parameterPtr = (ChiselLayerParameters*)chiselLookupValues.parameters.GetUnsafePtr();
                // Regular index operator will return a copy instead of a reference *sigh*
                for (int l = 0; l < SurfaceLayers.ParameterCount; l++)
                    parameterPtr[l].Clear();


                #region MeshQueries
                // TODO: have more control over the queries
                this.meshQueries         = MeshQuery.DefaultQueries.ToNativeArray(allocator);
                this.meshQueriesLength   = this.meshQueries.Length;
                this.meshQueries.Sort(meshQueryComparer);
                #endregion

                this.subMeshSurfaces.ResizeExact(this.meshQueriesLength);
                for (int i = 0; i < this.meshQueriesLength; i++)
                    this.subMeshSurfaces.AllocateWithCapacityForIndex(i, 1000);

                this.subMeshCounts.Clear();

                // Workaround for the incredibly dumb "can't create a stream that is zero sized" when the value is determined at runtime. Yeah, thanks
                this.uniqueBrushPairs.Add(new BrushPair2 { type = IntersectionType.InvalidValue });

                this.allUpdateBrushIndexOrders.Clear();
                if (this.allUpdateBrushIndexOrders.Capacity < this.brushCount)
                    this.allUpdateBrushIndexOrders.Capacity = this.brushCount;


                this.brushesThatNeedIndirectUpdateHashMap.Clear();
                this.brushesThatNeedIndirectUpdate.Clear();

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
                
                this.basePolygonDisposeList             = new NativeList<BlobAssetReference<BasePolygonsBlob>>(chiselLookupValues.basePolygonCache.Length, allocator);
                this.treeSpaceVerticesDisposeList       = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(chiselLookupValues.treeSpaceVerticesCache.Length, allocator);
                this.brushesTouchedByBrushDisposeList   = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(chiselLookupValues.brushesTouchedByBrushCache.Length, allocator);
                this.routingTableDisposeList            = new NativeList<BlobAssetReference<RoutingTable>>(chiselLookupValues.routingTableCache.Length, allocator);
                this.brushTreeSpacePlaneDisposeList     = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(chiselLookupValues.brushTreeSpacePlaneCache.Length, allocator);
                this.brushRenderBufferDisposeList       = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(chiselLookupValues.brushRenderBufferCache.Length, allocator);

                #endregion
                Profiler.EndSample();
            }

            const bool runInParallelDefault = true;
            public void RunMeshUpdateJobs()
            {
                // Reset All JobHandles
                JobHandles = default;
                disposeJobHandle = default;

                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);
                {
                    #region Build Lookup Tables
                    Profiler.BeginSample("Job_BuildLookupTables");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var buildLookupTablesJob = new BuildLookupTablesJob
                        {
                            // Read
                            brushes                         = this.brushes,
                            brushCount                      = this.brushCount,

                            // Read/Write
                            brushIDValues                   = chiselLookupValues.brushIDValues,
                            nodeIDValueToNodeOrderArray     = this.nodeIDValueToNodeOrderArray,

                            // Write
                            nodeIDValueToNodeOrderOffsetRef = this.nodeIDValueToNodeOrderOffsetRef,
                            allTreeBrushIndexOrders         = this.allTreeBrushIndexOrders
                        };
                        buildLookupTablesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.brushesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                ref JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                ref JobHandles.allTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.brushIDValuesJobHandle));
                    }
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region CacheRemapping
                    Profiler.BeginSample("Job_CacheRemapping");
                    try
                    {
                        const bool runInParallel = false;//runInParallelDefault; // Why?
                        var cacheRemappingJob = new CacheRemappingJob
                        {
                            // Read
                            //compactHierarchy              = compactHierarchy,  //<-- cannot do ref or pointer here
                                                                                 //    so we set it below using InitializeHierarchy

                            nodeIDValueToNodeOrderArray     = this.nodeIDValueToNodeOrderArray,
                            nodeIDValueToNodeOrderOffsetRef = this.nodeIDValueToNodeOrderOffsetRef,
                            brushes                         = this.brushes,
                            brushCount                      = this.brushCount,
                            allTreeBrushIndexOrders         = this.allTreeBrushIndexOrders,
                            brushIDValues                   = chiselLookupValues.brushIDValues,

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
                            brushesThatNeedIndirectUpdateHashMap = this.brushesThatNeedIndirectUpdateHashMap,
                            needRemappingRef                     = this.needRemappingRef
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

                    #region Find Modified Brushes
                    Profiler.BeginSample("Job_FindModifiedBrushes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var findModifiedBrushesJob = new FindModifiedBrushesJob
                        {
                            // Read
                            //compactHierarchy              = compactHierarchy,  //<-- cannot do ref or pointer here
                                                                                 //    so we set it below using InitializeHierarchy

                            brushes                         = this.brushes,
                            brushCount                      = this.brushCount,
                            allTreeBrushIndexOrders         = this.allTreeBrushIndexOrders,

                            // Read/Write
                            rebuildTreeBrushIndexOrders     = this.rebuildTreeBrushIndexOrders,

                            // Write
                            transformTreeBrushIndicesList   = this.transformTreeBrushIndicesList,
                        };
                        findModifiedBrushesJob.InitializeHierarchy(ref compactHierarchy);
                        findModifiedBrushesJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.brushesJobHandle,
                                JobHandles.allTreeBrushIndexOrdersJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                ref JobHandles.transformTreeBrushIndicesListJobHandle));
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
                            //compactHierarchy              = compactHierarchy,  //<-- cannot do ref or pointer here
                                                                                 //    so we set it below using InitializeHierarchy

                            needRemappingRef                = this.needRemappingRef,
                            rebuildTreeBrushIndexOrders     = this.rebuildTreeBrushIndexOrders,
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache,
                            brushes                         = this.brushes,
                            brushCount                      = this.brushCount,
                            nodeIDValueToNodeOrderArray     = this.nodeIDValueToNodeOrderArray,
                            nodeIDValueToNodeOrderOffsetRef = this.nodeIDValueToNodeOrderOffsetRef,

                            // Write
                            brushesThatNeedIndirectUpdateHashMap = this.brushesThatNeedIndirectUpdateHashMap
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
                            //compactHierarchy          = compactHierarchy, //<-- cannot do ref or pointer here
                                                                            //    so we set it below using InitializeHierarchy
                            brushMeshBlobs              = brushMeshBlobs,
                            brushCount                  = this.brushCount,
                            brushes                     = this.brushes,

                            // Read / Write
                            allKnownBrushMeshIndices    = chiselLookupValues.allKnownBrushMeshIndices,
                            parameters                  = chiselLookupValues.parameters,
                            parameterCounts             = this.parameterCounts,

                            // Write
                            allBrushMeshIDs             = this.allBrushMeshIDs
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

                
                JobHandles.rebuildTreeBrushIndexOrdersJobHandle.Complete();
                this.updateCount = rebuildTreeBrushIndexOrders.Length;
                if (this.updateCount > 0)
                {                 
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
                            transformTreeBrushIndicesList   = transformTreeBrushIndicesList.AsJobArray(runInParallel),
                            //compactHierarchy              = compactHierarchy,  //<-- cannot do ref or pointer here
                                                                                 //    so we set it below using InitializeHierarchy

                            // Write
                            transformationCache             = chiselLookupValues.transformationCache
                        };
                        updateTransformationsJob.InitializeHierarchy(ref compactHierarchy);
                        updateTransformationsJob.Schedule(runInParallel, transformTreeBrushIndicesList, 8,
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
                            brushes             = brushes.AsArray(),
                            nodes               = nodes.AsArray(),
                            //compactHierarchy  = compactHierarchy,  //<-- cannot do ref or pointer here, 
                                                                     //    so we set it below using InitializeHierarchy

                            // Write
                            compactTreeRef      = this.compactTreeRef
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
                            allTreeBrushIndexOrders = allTreeBrushIndexOrders.AsJobArray(runInParallel),
                            allBrushMeshIDs         = allBrushMeshIDs,

                            // Write
                            brushMeshLookup         = brushMeshLookup,
                            surfaceCountRef         = surfaceCountRef
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
                            rebuildTreeBrushIndexOrders = rebuildTreeBrushIndexOrders.AsArray().AsReadOnly(),

                            // Read Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            routingTableCache           = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel),
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache     .AsJobArray(runInParallel),

                            // Write
                            basePolygonDisposeList              = basePolygonDisposeList.AsParallelWriter(),
                            treeSpaceVerticesDisposeList        = treeSpaceVerticesDisposeList.AsParallelWriter(),
                            brushesTouchedByBrushDisposeList    = brushesTouchedByBrushDisposeList.AsParallelWriter(),
                            routingTableDisposeList             = routingTableDisposeList.AsParallelWriter(),
                            brushTreeSpacePlaneDisposeList      = brushTreeSpacePlaneDisposeList.AsParallelWriter(),
                            brushRenderBufferDisposeList        = brushRenderBufferDisposeList.AsParallelWriter()
                        };
                        invalidateBrushCacheJob.Schedule(runInParallel, rebuildTreeBrushIndexOrders, 16,
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
                            allTreeBrushIndexOrders         = allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            nodeIDValueToNodeOrderArray     = nodeIDValueToNodeOrderArray.AsArray().AsReadOnly(),
                            nodeIDValueToNodeOrderOffsetRef = nodeIDValueToNodeOrderOffsetRef,

                            // Read Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel)
                        };
                        fixupBrushCacheIndicesJob.Schedule(runInParallel, allTreeBrushIndexOrders, 16,
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
                            rebuildTreeBrushIndexOrders = rebuildTreeBrushIndexOrders.AsArray(),
                            transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup             = brushMeshLookup.AsReadOnly(),

                            // Write
                            brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),
                        };
                        createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, rebuildTreeBrushIndexOrders, 16,
                            new ReadJobHandles(
                                JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.brushMeshLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle));
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
                            allTreeBrushIndexOrders         = allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            transformationCache             = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup                 = brushMeshLookup.AsReadOnly(),
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            rebuildTreeBrushIndexOrders     = rebuildTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            
                            // Read / Write
                            brushBrushIntersections         = brushBrushIntersections,
                            
                            // Write
                            brushesThatNeedIndirectUpdateHashMap = brushesThatNeedIndirectUpdateHashMap.AsParallelWriter()
                        };
                        findAllBrushIntersectionPairsJob.Schedule(runInParallel, rebuildTreeBrushIndexOrders, 16,
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
                            brushesThatNeedIndirectUpdateHashMap     = brushesThatNeedIndirectUpdateHashMap,
                        
                            // Write
                            brushesThatNeedIndirectUpdate            = brushesThatNeedIndirectUpdate
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
                            brushesThatNeedIndirectUpdate   = brushesThatNeedIndirectUpdate      .AsJobArray(runInParallel),

                            // Read Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            routingTableCache               = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel),
                            brushTreeSpacePlaneCache        = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            brushRenderBufferCache          = chiselLookupValues.brushRenderBufferCache     .AsJobArray(runInParallel)
                        };
                        invalidateIndirectBrushCacheJob.Schedule(runInParallel, brushesThatNeedIndirectUpdate, 16,
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
                            rebuildTreeBrushIndexOrders     = brushesThatNeedIndirectUpdate         .AsJobArray(runInParallel),
                            transformationCache             = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup                 = brushMeshLookup                       .AsReadOnly(),

                            // Write
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache   .AsJobArray(runInParallel),
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                        };
                        createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, brushesThatNeedIndirectUpdate, 16,
                            new ReadJobHandles(
                                JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.brushMeshLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                ref JobHandles.treeSpaceVerticesCacheJobHandle));
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
                            allTreeBrushIndexOrders         = allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            transformationCache             = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup                 = brushMeshLookup.AsReadOnly(),
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsJobArray(runInParallel),
                            brushesThatNeedIndirectUpdate   = brushesThatNeedIndirectUpdate.AsJobArray(runInParallel),

                            // Read / Write
                            brushBrushIntersections         = brushBrushIntersections
                        };
                        findAllIndirectBrushIntersectionPairsJob.Schedule(runInParallel, brushesThatNeedIndirectUpdate, 1,
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
                            allTreeBrushIndexOrders         = allTreeBrushIndexOrders        .AsArray().AsReadOnly(),
                            brushesThatNeedIndirectUpdate   = brushesThatNeedIndirectUpdate  .AsJobArray(runInParallel),
                            rebuildTreeBrushIndexOrders     = rebuildTreeBrushIndexOrders    .AsArray().AsReadOnly(),

                            // Write
                            allUpdateBrushIndexOrders       = allUpdateBrushIndexOrders      .AsParallelWriter(),
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
                            brushBrushIntersections         = brushBrushIntersections,

                            // Write
                            brushIntersectionsWith          = brushIntersectionsWith.GetUnsafeList(),
                            brushIntersectionsWithRange     = brushIntersectionsWithRange
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
                            compactTreeRef              = compactTreeRef,
                            allTreeBrushIndexOrders     = allTreeBrushIndexOrders            .AsJobArray(runInParallel),
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders          .AsJobArray(runInParallel),

                            brushIntersectionsWith      = brushIntersectionsWith             .AsJobArray(runInParallel),
                            brushIntersectionsWithRange = brushIntersectionsWithRange        .AsReadOnly(),

                            // Write
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel)
                        };
                        storeBrushIntersectionsJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 16,
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
                            treeBrushIndexOrders        = allUpdateBrushIndexOrders.AsJobArray(sequential),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(sequential),

                            // Read Write
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(sequential),
                        };
                        mergeTouchingBrushVerticesJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 16,
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
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),

                            // Read (Re-allocate) / Write
                            uniqueBrushPairs            = uniqueBrushPairs.GetUnsafeList()
                        };
                        findBrushPairsJob.Schedule(runInParallel, 
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.brushesTouchedByBrushCacheJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.uniqueBrushPairsJobHandle));

                        NativeCollection.ScheduleConstruct(runInParallel, out intersectingBrushesStream, uniqueBrushPairs,
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
                            uniqueBrushPairs            = uniqueBrushPairs                      .AsJobArray(runInParallel),
                            transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup             = brushMeshLookup                       .AsReadOnly(),

                            // Write
                            intersectingBrushesStream   = intersectingBrushesStream  .AsWriter()
                        };
                        prepareBrushPairIntersectionsJob.Schedule(runInParallel, uniqueBrushPairs, 1,
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
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders                     .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            brushMeshLookup             = brushMeshLookup                               .AsReadOnly(),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),

                            // Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel)
                        };
                        createBlobPolygonsBlobs.Schedule(runInParallel, allUpdateBrushIndexOrders, 16,
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
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders             .AsJobArray(runInParallel),
                            brushMeshLookup             = brushMeshLookup                       .AsReadOnly(),
                            transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),

                            // Write
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsJobArray(runInParallel)
                        };
                        createBrushTreeSpacePlanesJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 16,
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
                        NativeCollection.ScheduleSetCapacity(runInParallel, ref outputSurfaces, surfaceCountRef,
                                                           new ReadJobHandles(
                                                               JobHandles.surfaceCountRefJobHandle
                                                               ),
                                                            new WriteJobHandles(
                                                                ref JobHandles.outputSurfacesJobHandle
                                                                ),
                                                            Allocator.TempJob);

                        var createIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Needed for count (forced & unused)
                            uniqueBrushPairs            = uniqueBrushPairs,

                            // Read
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache     .AsJobArray(runInParallel),
                            intersectingBrushesStream   = intersectingBrushesStream                     .AsReader(),

                            // Write
                            outputSurfaceVertices       = outputSurfaceVertices     .AsParallelWriterExt(),
                            outputSurfaces              = outputSurfaces            .AsParallelWriter()
                        };
                        var currentJobHandle = createIntersectionLoopsJob.Schedule(runInParallel, uniqueBrushPairs, 8,
                            new ReadJobHandles(
                                JobHandles.uniqueBrushPairsJobHandle,
                                JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                JobHandles.treeSpaceVerticesCacheJobHandle,
                                JobHandles.intersectingBrushesStreamJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.outputSurfaceVerticesJobHandle,
                                ref JobHandles.outputSurfacesJobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref intersectingBrushesStream, currentJobHandle);
                    } finally { Profiler.EndSample(); }
            
                    Profiler.BeginSample("Job_GatherOutputSurfaces");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                        {
                            // Read / Write (Sort)
                            outputSurfaces          = outputSurfaces.AsJobArray(runInParallel),

                            // Write
                            outputSurfacesRange     = outputSurfacesRange
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
                        NativeCollection.ScheduleConstruct(runInParallel, out dataStream1, allUpdateBrushIndexOrders,
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
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders          .AsJobArray(runInParallel),
                            outputSurfaceVertices       = outputSurfaceVertices              .AsJobArray(runInParallel),
                            outputSurfaces              = outputSurfaces                     .AsJobArray(runInParallel),
                            outputSurfacesRange         = outputSurfacesRange                .AsReadOnly(),
                            maxNodeOrder                = maxNodeOrder,
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            basePolygonCache            = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),

                            // Read Write
                            loopVerticesLookup          = loopVerticesLookup,
                            
                            // Write
                            output                      = dataStream1                        .AsWriter()
                        };
                        findLoopOverlapIntersectionsJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 1,
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
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),

                            // Read Write
                            loopVerticesLookup          = loopVerticesLookup,
                        };
                        mergeTouchingBrushVerticesIndirectJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 1,
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
                        // Build categorization trees for brushes
                        var createRoutingTableJob   = new CreateRoutingTableJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders                     .AsJobArray(runInParallel),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            compactTreeRef              = compactTreeRef,

                            // Write
                            routingTableLookup          = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel)
                        };
                        createRoutingTableJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 1,
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
                        NativeCollection.ScheduleConstruct(runInParallel, out dataStream2, allUpdateBrushIndexOrders,
                                                           new ReadJobHandles(
                                                               JobHandles.allUpdateBrushIndexOrdersJobHandle
                                                               ),
                                                           new WriteJobHandles(
                                                               ref JobHandles.dataStream2JobHandle
                                                               ),
                                                           Allocator.TempJob);

                        // Perform CSG
                        // TODO: determine when a brush is completely inside another brush
                        //		 (might not have any intersection loops)
                        var performCSGJob = new PerformCSGJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders                     .AsJobArray(runInParallel),
                            routingTableCache           = chiselLookupValues.routingTableCache          .AsJobArray(runInParallel),
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache   .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel),
                            input                       = dataStream1                                   .AsReader(),
                            loopVerticesLookup          = loopVerticesLookup,

                            // Write
                            output                      = dataStream2                                   .AsWriter(),
                        };
                        var currentJobHandle = performCSGJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.routingTableCacheJobHandle,
                                JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                JobHandles.brushesTouchedByBrushCacheJobHandle,
                                JobHandles.dataStream1JobHandle,
                                JobHandles.loopVerticesLookupJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.dataStream2JobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref dataStream1, currentJobHandle);
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
                        // TODO: Potentially merge this with PerformCSGJob?
                        var generateSurfaceTrianglesJob = new GenerateSurfaceTrianglesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders             .AsJobArray(runInParallel),
                            basePolygonCache            = chiselLookupValues.basePolygonCache   .AsJobArray(runInParallel),
                            transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            input                       = dataStream2                           .AsReader(),
                            meshQueries                 = meshQueries                           .AsReadOnly(),

                            // Write
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = generateSurfaceTrianglesJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 1,
                            new ReadJobHandles(
                                JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                JobHandles.meshQueriesJobHandle,
                                JobHandles.basePolygonCacheJobHandle,
                                JobHandles.transformationCacheJobHandle,
                                JobHandles.dataStream2JobHandle),
                            new WriteJobHandles(
                                ref JobHandles.brushRenderBufferCacheJobHandle));

                        NativeCollection.ScheduleDispose(runInParallel, ref dataStream2, currentJobHandle);
                    }
                    finally { Profiler.EndSample(); }
                    #endregion

                    // Schedule all the jobs
                    //JobHandle.ScheduleBatchedJobs();

                    //
                    // Create meshes out of ALL the generated and cached surfaces
                    //

                    // TODO: store parameterCounts per brush (precalculated), manage these counts in the hierarchy when brushes are added/removed/modified
                    //       then we don't need to count them here & don't need to do a "complete" here
                    JobHandles.parameterCountsJobHandle.Complete();

                    #region Create Meshes
                    Profiler.BeginSample("Mesh.AllocateWritableMeshData");
                    try
                    {
                        var meshAllocations = 0;
                        for (int m = 0; m < meshQueries.Length; m++)
                        {
                            var meshQuery = meshQueries[m];
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
                                meshAllocations += parameterCounts[SurfaceLayers.kColliderLayer];
                            } else
                                meshAllocations++;
                        }

                        meshDataArray = UnityEngine.Mesh.AllocateWritableMeshData(meshAllocations);

                        for (int i = 0; i < meshAllocations; i++)
                            meshDatas.Add(meshDataArray[i]);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_FindBrushRenderBuffers");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        { 
                            var dependencies = JobHandleExtensions.CombineDependencies(JobHandles.meshQueriesJobHandle,
                                                                                                  JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                                                                  JobHandles.brushRenderBufferCacheJobHandle,
                                                                                                  JobHandles.brushRenderDataJobHandle,
                                                                                                  JobHandles.subMeshSurfacesJobHandle,
                                                                                                  JobHandles.subMeshCountsJobHandle,
                                                                                                  JobHandles.vertexBufferContents_subMeshSectionsJobHandle);
                            var brushRenderDataCapacity = ChiselNativeListExtensions.ScheduleEnsureCapacity(brushRenderData, allTreeBrushIndexOrders, dependencies);
                            JobHandles.brushRenderDataJobHandle.AddDependency(brushRenderDataCapacity);
                        }

                        var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                        {
                            // Read
                            meshQueryLength         = meshQueriesLength,
                            allTreeBrushIndexOrders = allTreeBrushIndexOrders.AsArray(),
                            brushRenderBufferCache  = chiselLookupValues.brushRenderBufferCache.AsJobArray(runInParallel),

                            // Write
                            brushRenderData         = brushRenderData,
                            subMeshCounts           = subMeshCounts,
                            subMeshSections         = vertexBufferContents.subMeshSections,
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
                            meshQueries         = meshQueries.AsReadOnly(),
                            brushRenderData     = brushRenderData.AsJobArray(runInParallel),

                            // Write
                            subMeshSurfaces     = subMeshSurfaces,
                        };
                        prepareSubSectionsJob.Schedule(runInParallel, meshQueriesLength, 1,
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
                            meshQueries      = meshQueries.AsReadOnly(),
                            subMeshSurfaces  = subMeshSurfaces,

                            // Write
                            subMeshCounts    = subMeshCounts
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
                            subMeshCounts   = subMeshCounts,

                            // Write
                            subMeshSections = vertexBufferContents.subMeshSections,
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
                            subMeshSections         = vertexBufferContents.subMeshSections.AsJobArray(runInParallel),

                            // Read Write
                            triangleBrushIndices    = vertexBufferContents.triangleBrushIndices
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
                            subMeshCounts       = subMeshCounts.AsJobArray(runInParallel),

                            // Read Write
                            meshDescriptions    = vertexBufferContents.meshDescriptions
                        };
                        generateMeshDescriptionJob.Schedule(runInParallel,
                            new ReadJobHandles(
                                JobHandles.subMeshCountsJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.vertexBufferContents_meshDescriptionsJobHandle));
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_CopyToMeshes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                         
                        var assignMeshesJob = new AssignMeshesJob
                        {
                            // Read
                            meshDescriptions    = vertexBufferContents.meshDescriptions,
                            subMeshSections     = vertexBufferContents.subMeshSections,
                            meshDatas           = meshDatas,

                            // Write
                            meshes              = vertexBufferContents.meshes,
                            debugHelperMeshes   = debugHelperMeshes,
                            renderMeshes        = renderMeshes,

                            // Read / Write
                            colliderMeshUpdates = colliderMeshUpdates,
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
                            subMeshSections         = vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts           = subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces         = subMeshSurfaces,
                            renderDescriptors       = vertexBufferContents.renderDescriptors,
                            renderMeshes            = renderMeshes,

                            // Read/Write
                            triangleBrushIndices    = vertexBufferContents.triangleBrushIndices,
                            meshes                  = vertexBufferContents.meshes,
                        };
                        renderCopyToMeshJob.Schedule(runInParallel, renderMeshes, 1,
                            new ReadJobHandles(
                                JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                JobHandles.subMeshCountsJobHandle,
                                JobHandles.subMeshSurfacesJobHandle,
                                JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                JobHandles.renderMeshesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.subMeshCountsJobHandle, // Why?
                                ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                                ref JobHandles.vertexBufferContents_meshesJobHandle));

                        var helperCopyToMeshJob = new CopyToRenderMeshJob
                        {
                            // Read
                            subMeshSections         = vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts           = subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces         = subMeshSurfaces,
                            renderDescriptors       = vertexBufferContents.renderDescriptors,
                            renderMeshes            = debugHelperMeshes,

                            // Read/Write
                            triangleBrushIndices    = vertexBufferContents.triangleBrushIndices,
                            meshes                  = vertexBufferContents.meshes,
                        };
                        helperCopyToMeshJob.Schedule(runInParallel, debugHelperMeshes, 1,
                            new ReadJobHandles(
                                JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                JobHandles.subMeshCountsJobHandle,
                                JobHandles.subMeshSurfacesJobHandle,
                                JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                JobHandles.debugHelperMeshesJobHandle),
                            new WriteJobHandles(
                                ref JobHandles.subMeshCountsJobHandle, // Why?
                                ref JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                                ref JobHandles.vertexBufferContents_meshesJobHandle));

                        var colliderCopyToMeshJob = new CopyToColliderMeshJob
                        {
                            // Read
                            subMeshSections         = vertexBufferContents.subMeshSections.AsJobArray(runInParallel),
                            subMeshCounts           = subMeshCounts.AsJobArray(runInParallel),
                            subMeshSurfaces         = subMeshSurfaces,
                            colliderDescriptors     = vertexBufferContents.colliderDescriptors,
                            colliderMeshes          = colliderMeshUpdates,
                            
                            // Read/Write
                            meshes                  = vertexBufferContents.meshes,
                        };
                        colliderCopyToMeshJob.Schedule(runInParallel, colliderMeshUpdates, 16,
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
                            allTreeBrushIndexOrders     = allTreeBrushIndexOrders.AsJobArray(runInParallel),
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
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders,
                            brushMeshBlobs              = brushMeshBlobs

                            // Write
                            //compactHierarchy          = compactHierarchy,  //<-- cannot do ref or pointer here
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

                    // Schedule all the jobs
                    JobHandle.ScheduleBatchedJobs();
                }

                #region Free all temporaries
                // TODO: most of these disposes can be scheduled before we complete and write to the meshes, 
                // so that these disposes can happen at the same time as the mesh updates in finishMeshUpdates
                Profiler.BeginSample("CSG_PreMeshDeallocate");
                {
                    // Combine all JobHandles of all jobs to ensure that we wait for ALL of them to finish 
                    // before we dispose of our temporaries.
                    // Eventually we might want to put this in between other jobs, but for now this is safer
                    // to work with while things are still being re-arranged.
                    disposeJobHandle = JobHandleExtensions.CombineDependencies(
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.allBrushMeshIDsJobHandle,
                                                    JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                    JobHandles.brushIDValuesJobHandle,
                                                    JobHandles.basePolygonCacheJobHandle,
                                                    JobHandles.brushBrushIntersectionsJobHandle,
                                                    JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                    JobHandles.brushRenderBufferCacheJobHandle,
                                                    JobHandles.brushRenderDataJobHandle),
                                                JobHandleExtensions.CombineDependencies(
                                                    JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                                    JobHandles.brushMeshBlobsLookupJobHandle,
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
                                                    JobHandles.compactTreeRefJobHandle,
                                                    JobHandles.parametersJobHandle,
                                                    JobHandles.allKnownBrushMeshIndicesJobHandle,
                                                    JobHandles.parameterCountsJobHandle,
                                                    JobHandles.storeToCacheJobHandle)
                                            );
                }
                Profiler.EndSample();
                #endregion
            }

            public JobHandle PreMeshUpdateDispose()
            {
                lastJobHandle = disposeJobHandle;
                //Debug.Log("PreMeshUpdateDispose");

                if (brushMeshLookup              .IsCreated) lastJobHandle.AddDependency(brushMeshLookup              .Dispose(disposeJobHandle));
                if (brushIntersectionsWithRange  .IsCreated) lastJobHandle.AddDependency(brushIntersectionsWithRange  .Dispose(disposeJobHandle));
                if (outputSurfacesRange          .IsCreated) lastJobHandle.AddDependency(outputSurfacesRange          .Dispose(disposeJobHandle));
                if (parameterCounts              .IsCreated) lastJobHandle.AddDependency(parameterCounts              .Dispose(disposeJobHandle));
                
                if (loopVerticesLookup           .IsCreated) lastJobHandle.AddDependency(loopVerticesLookup           .Dispose(disposeJobHandle));
                
                if (transformTreeBrushIndicesList.IsCreated) lastJobHandle.AddDependency(transformTreeBrushIndicesList.Dispose(disposeJobHandle));
                if (brushes                      .IsCreated) lastJobHandle.AddDependency(brushes                      .Dispose(disposeJobHandle));
                if (nodes                        .IsCreated) lastJobHandle.AddDependency(nodes                        .Dispose(disposeJobHandle));
                if (brushRenderData              .IsCreated) lastJobHandle.AddDependency(brushRenderData              .Dispose(disposeJobHandle));
                if (subMeshCounts                .IsCreated) lastJobHandle.AddDependency(subMeshCounts                .Dispose(disposeJobHandle));
                if (subMeshSurfaces              .IsCreated) lastJobHandle.AddDependency(subMeshSurfaces              .Dispose(disposeJobHandle));
                if (rebuildTreeBrushIndexOrders  .IsCreated) lastJobHandle.AddDependency(rebuildTreeBrushIndexOrders  .Dispose(disposeJobHandle));
                if (allUpdateBrushIndexOrders    .IsCreated) lastJobHandle.AddDependency(allUpdateBrushIndexOrders    .Dispose(disposeJobHandle));
                if (allBrushMeshIDs              .IsCreated) lastJobHandle.AddDependency(allBrushMeshIDs              .Dispose(disposeJobHandle));
                if (uniqueBrushPairs             .IsCreated) lastJobHandle.AddDependency(uniqueBrushPairs             .Dispose(disposeJobHandle));
                if (brushIntersectionsWith       .IsCreated) lastJobHandle.AddDependency(brushIntersectionsWith       .Dispose(disposeJobHandle));
                if (outputSurfaceVertices        .IsCreated) lastJobHandle.AddDependency(outputSurfaceVertices        .Dispose(disposeJobHandle));
                if (outputSurfaces               .IsCreated) lastJobHandle.AddDependency(outputSurfaces               .Dispose(disposeJobHandle));
                if (brushesThatNeedIndirectUpdate.IsCreated) lastJobHandle.AddDependency(brushesThatNeedIndirectUpdate.Dispose(disposeJobHandle));
                if (nodeIDValueToNodeOrderArray  .IsCreated) lastJobHandle.AddDependency(nodeIDValueToNodeOrderArray  .Dispose(disposeJobHandle));
                
                if (brushesThatNeedIndirectUpdateHashMap.IsCreated) lastJobHandle.AddDependency(brushesThatNeedIndirectUpdateHashMap.Dispose(disposeJobHandle));
                if (brushBrushIntersections             .IsCreated) lastJobHandle.AddDependency(brushBrushIntersections             .Dispose(disposeJobHandle));
                
                
                // Note: cannot use "IsCreated" on this job, for some reason it won't be scheduled and then complain that it's leaking? Bug in IsCreated?
                lastJobHandle.AddDependency(meshQueries.Dispose(disposeJobHandle));

                lastJobHandle.AddDependency(DisposeTool.Dispose(true, basePolygonDisposeList,           disposeJobHandle),
                                            DisposeTool.Dispose(true, treeSpaceVerticesDisposeList,     disposeJobHandle),
                                            DisposeTool.Dispose(true, brushesTouchedByBrushDisposeList, disposeJobHandle),
                                            DisposeTool.Dispose(true, routingTableDisposeList,          disposeJobHandle),
                                            DisposeTool.Dispose(true, brushTreeSpacePlaneDisposeList,   disposeJobHandle),
                                            DisposeTool.Dispose(true, brushRenderBufferDisposeList,     disposeJobHandle));

                meshQueries                     = default;
                transformTreeBrushIndicesList   = default;
                parameterCounts                 = default;
                brushes                         = default;
                nodes                           = default;

                brushRenderData                 = default;
                subMeshCounts                   = default;
                subMeshSurfaces                 = default;
                brushMeshLookup                 = default;
                rebuildTreeBrushIndexOrders     = default;
                allUpdateBrushIndexOrders       = default;
                allBrushMeshIDs                 = default;
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

                return lastJobHandle;
            }

            public JobHandle Dispose(JobHandle disposeJobHandle)
            {
                //Debug.Log($"Dispose");
                lastJobHandle.AddDependency(disposeJobHandle);

                if (allTreeBrushIndexOrders      .IsCreated) lastJobHandle.AddDependency(allTreeBrushIndexOrders      .Dispose(disposeJobHandle));
                if (colliderMeshUpdates          .IsCreated) lastJobHandle.AddDependency(colliderMeshUpdates          .Dispose(disposeJobHandle));
                if (debugHelperMeshes            .IsCreated) lastJobHandle.AddDependency(debugHelperMeshes            .Dispose(disposeJobHandle));
                if (renderMeshes                 .IsCreated) lastJobHandle.AddDependency(renderMeshes                 .Dispose(disposeJobHandle));
                if (meshDatas                    .IsCreated) lastJobHandle.AddDependency(meshDatas                    .Dispose(disposeJobHandle));
                
                if (vertexBufferContents         .IsCreated) lastJobHandle.AddDependency(vertexBufferContents         .Dispose(disposeJobHandle));



                JobHandles.surfaceCountRefJobHandle.Complete();
                JobHandles.surfaceCountRefJobHandle = default;
                if (surfaceCountRef.IsCreated) surfaceCountRef.Dispose();

                JobHandles.compactTreeRefJobHandle.Complete();
                JobHandles.compactTreeRefJobHandle = default;
                if (compactTreeRef.IsCreated)
                {
                    if (compactTreeRef.Value.IsCreated) compactTreeRef.Value.Dispose();
                    compactTreeRef.Dispose();
                }

                JobHandles.needRemappingRefJobHandle.Complete();
                JobHandles.needRemappingRefJobHandle = default;
                if (needRemappingRef.IsCreated) needRemappingRef.Dispose();

                JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle.Complete();
                JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle = default;
                if (nodeIDValueToNodeOrderOffsetRef.IsCreated) nodeIDValueToNodeOrderOffsetRef.Dispose();
                
                allTreeBrushIndexOrders         = default;
                colliderMeshUpdates             = default;
                debugHelperMeshes               = default;
                renderMeshes                    = default;
                vertexBufferContents            = default;
                meshDataArray                   = default;
                meshDatas                       = default;
                surfaceCountRef                 = default;
                nodeIDValueToNodeOrderOffsetRef = default;
                compactTreeRef                  = default;
                needRemappingRef                = default;

                brushCount = 0;

                return lastJobHandle;
            }
        }

        #region CheckDependencies
        static void CheckDependencies(bool runInParallel, JobHandle dependencies) { if (!runInParallel) dependencies.Complete(); }
        #endregion
    }
}
