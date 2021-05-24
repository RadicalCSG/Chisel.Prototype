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

                        var dependencies = CombineDependencies(treeUpdate.JobHandles.meshDatasJobHandle,
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
                        var dependencies = CombineDependencies(
                                                        CombineDependencies(
                                                            treeUpdate.JobHandles.allBrushMeshIDsJobHandle,
                                                            treeUpdate.JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                            treeUpdate.JobHandles.brushIDValuesJobHandle,
                                                            treeUpdate.JobHandles.basePolygonCacheJobHandle,
                                                            treeUpdate.JobHandles.brushBrushIntersectionsJobHandle,
                                                            treeUpdate.JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                            treeUpdate.JobHandles.brushRenderBufferCacheJobHandle,
                                                            treeUpdate.JobHandles.brushRenderDataJobHandle,
                                                            treeUpdate.JobHandles.brushTreeSpacePlaneCacheJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.JobHandles.brushMeshBlobsLookupJobHandle,
                                                            treeUpdate.JobHandles.brushMeshLookupJobHandle,
                                                            treeUpdate.JobHandles.brushIntersectionsWithJobHandle,
                                                            treeUpdate.JobHandles.brushIntersectionsWithRangeJobHandle,
                                                            treeUpdate.JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                            treeUpdate.JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                            treeUpdate.JobHandles.brushTreeSpaceBoundCacheJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.JobHandles.dataStream1JobHandle,
                                                            treeUpdate.JobHandles.dataStream2JobHandle,
                                                            treeUpdate.JobHandles.intersectingBrushesStreamJobHandle,
                                                            treeUpdate.JobHandles.loopVerticesLookupJobHandle,
                                                            treeUpdate.JobHandles.meshQueriesJobHandle,
                                                            treeUpdate.JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                            treeUpdate.JobHandles.outputSurfaceVerticesJobHandle,
                                                            treeUpdate.JobHandles.outputSurfacesJobHandle,
                                                            treeUpdate.JobHandles.outputSurfacesRangeJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.JobHandles.routingTableCacheJobHandle,
                                                            treeUpdate.JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                            treeUpdate.JobHandles.sectionsJobHandle,
                                                            treeUpdate.JobHandles.subMeshSurfacesJobHandle,
                                                            treeUpdate.JobHandles.subMeshCountsJobHandle,
                                                            treeUpdate.JobHandles.treeSpaceVerticesCacheJobHandle,
                                                            treeUpdate.JobHandles.transformationCacheJobHandle,
                                                            treeUpdate.JobHandles.uniqueBrushPairsJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.JobHandles.transformTreeBrushIndicesListJobHandle,
                                                            treeUpdate.JobHandles.brushesJobHandle,
                                                            treeUpdate.JobHandles.nodesJobHandle,
                                                            treeUpdate.JobHandles.parametersJobHandle,
                                                            treeUpdate.JobHandles.allKnownBrushMeshIndicesJobHandle,
                                                            treeUpdate.JobHandles.parameterCountsJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.JobHandles.updateBrushOutlineJobHandle,
                                                            treeUpdate.JobHandles.storeToCacheJobHandle,

                                                            treeUpdate.JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                            treeUpdate.JobHandles.colliderMeshUpdatesJobHandle,
                                                            treeUpdate.JobHandles.debugHelperMeshesJobHandle,
                                                            treeUpdate.JobHandles.renderMeshesJobHandle,
                                                            treeUpdate.JobHandles.surfaceCountRefJobHandle,
                                                            treeUpdate.JobHandles.compactTreeRefJobHandle,
                                                            treeUpdate.JobHandles.needRemappingRefJobHandle,
                                                            treeUpdate.JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle),
                                                        CombineDependencies(
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
                        finalJobHandle = CombineDependencies(finalJobHandle, dependencies);
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

                internal JobHandle updateBrushOutlineJobHandle;
                internal JobHandle preMeshUpdateCombinedJobHandle;
            }
            internal JobHandlesStruct JobHandles;

            internal JobHandle disposeJobHandle;
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

                Profiler.BeginSample("CSG_Allocations");
                // Make sure that if we, somehow, run this while parts of the previous update is still running, we wait for it to complete
                this.lastJobHandle.Complete();
                this.lastJobHandle = default;

                this.parameterCounts                = new NativeArray<int>(chiselLookupValues.parameters.Length, Allocator.TempJob);
                this.transformTreeBrushIndicesList  = new NativeList<NodeOrderNodeID>(Allocator.TempJob);
                this.nodes                          = new NativeList<CompactNodeID>(Allocator.TempJob);
                this.brushes                        = new NativeList<CompactNodeID>(Allocator.TempJob);

                compactHierarchy.GetTreeNodes(this.nodes, this.brushes);
                    
                #region Allocations/Resize
                var newBrushCount = this.brushes.Length;
                chiselLookupValues.EnsureCapacity(newBrushCount);

                this.brushCount   = newBrushCount;
                this.maxNodeOrder = this.brushCount;

                meshDataArray   = default;
                meshDatas       = new NativeList<UnityEngine.Mesh.MeshData>(Allocator.TempJob);

                //var triangleArraySize         = GeometryMath.GetTriangleArraySize(newBrushCount);
                //var intersectionCount         = math.max(1, triangleArraySize);
                brushesThatNeedIndirectUpdateHashMap = new NativeHashSet<IndexOrder>(brushCount, Allocator.TempJob);
                brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(brushCount, Allocator.TempJob);

                // TODO: find actual vertex count
                outputSurfaceVertices           = new NativeList<float3>(65535 * 10, Allocator.TempJob); 

                outputSurfaces                  = new NativeList<BrushIntersectionLoop>(brushCount * 16, Allocator.TempJob);
                brushIntersectionsWith          = new NativeList<BrushIntersectWith>(brushCount, Allocator.TempJob);

                nodeIDValueToNodeOrderOffsetRef = new NativeReference<int>(Allocator.TempJob);
                surfaceCountRef                 = new NativeReference<int>(Allocator.TempJob);
                compactTreeRef                  = new NativeReference<BlobAssetReference<CompactTree>>(Allocator.TempJob);
                needRemappingRef                = new NativeReference<bool>(Allocator.TempJob);

                uniqueBrushPairs                = new NativeList<BrushPair2>(brushCount * 16, Allocator.TempJob);
                
                rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(brushCount, Allocator.TempJob);
                allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(brushCount, Allocator.TempJob);
                allBrushMeshIDs                 = new NativeArray<int>(brushCount, Allocator.TempJob);
                brushRenderData                 = new NativeList<BrushData>(brushCount, Allocator.TempJob);
                allTreeBrushIndexOrders         = new NativeList<IndexOrder>(brushCount, Allocator.TempJob);
                allTreeBrushIndexOrders.Clear();
                allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);

                outputSurfacesRange             = new NativeArray<int2>(brushCount, Allocator.TempJob);
                brushIntersectionsWithRange     = new NativeArray<int2>(brushCount, Allocator.TempJob);
                nodeIDValueToNodeOrderArray     = new NativeList<int>(brushCount, Allocator.TempJob);
                brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(brushCount, Allocator.TempJob);

                brushBrushIntersections         = new NativeListArray<BrushIntersectWith>(16, Allocator.TempJob);
                brushBrushIntersections.ResizeExact(brushCount);

                this.subMeshSurfaces            = new NativeListArray<SubMeshSurface>(Allocator.TempJob);
                this.subMeshCounts              = new NativeList<SubMeshCounts>(Allocator.TempJob);

                this.colliderMeshUpdates        = new NativeList<ChiselMeshUpdate>(Allocator.TempJob);
                this.debugHelperMeshes          = new NativeList<ChiselMeshUpdate>(Allocator.TempJob);
                this.renderMeshes               = new NativeList<ChiselMeshUpdate>(Allocator.TempJob);

                
                loopVerticesLookup          = new NativeListArray<float3>(this.brushCount, Allocator.TempJob);
                loopVerticesLookup.ResizeExact(this.brushCount);
                
                vertexBufferContents.EnsureInitialized();

                var parameterPtr = (ChiselLayerParameters*)chiselLookupValues.parameters.GetUnsafePtr();
                // Regular index operator will return a copy instead of a reference *sigh*
                for (int l = 0; l < SurfaceLayers.ParameterCount; l++)
                    parameterPtr[l].Clear();


                #region MeshQueries
                // TODO: have more control over the queries
                this.meshQueries         = MeshQuery.DefaultQueries.ToNativeArray(Allocator.Persistent);
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
                
                this.basePolygonDisposeList             = new NativeList<BlobAssetReference<BasePolygonsBlob>>(chiselLookupValues.basePolygonCache.Length, Allocator.TempJob);
                this.treeSpaceVerticesDisposeList       = new NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(chiselLookupValues.treeSpaceVerticesCache.Length, Allocator.TempJob);
                this.brushesTouchedByBrushDisposeList   = new NativeList<BlobAssetReference<BrushesTouchedByBrush>>(chiselLookupValues.brushesTouchedByBrushCache.Length, Allocator.TempJob);
                this.routingTableDisposeList            = new NativeList<BlobAssetReference<RoutingTable>>(chiselLookupValues.routingTableCache.Length, Allocator.TempJob);
                this.brushTreeSpacePlaneDisposeList     = new NativeList<BlobAssetReference<BrushTreeSpacePlanes>>(chiselLookupValues.brushTreeSpacePlaneCache.Length, Allocator.TempJob);
                this.brushRenderBufferDisposeList       = new NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>(chiselLookupValues.brushRenderBufferCache.Length, Allocator.TempJob);

                #endregion
                Profiler.EndSample();
            }

            const bool runInParallelDefault = true;
            public void RunMeshUpdateJobs()
            {
                // Reset All JobHandles
                JobHandles = default;

                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);
                {
                    #region Build Lookup Tables
                    Profiler.BeginSample("Job_BuildLookupTables");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = CombineDependencies(JobHandles.brushesJobHandle,
                                                               JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                               JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                                               JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                               JobHandles.brushIDValuesJobHandle);

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
                        var currentJobHandle = buildLookupTablesJob.Schedule(runInParallel, dependencies);

                        //brushesJobHandle                       = CombineDependencies(currentJobHandle, brushesJobHandle);
                        JobHandles.brushIDValuesJobHandle                   = CombineDependencies(currentJobHandle, JobHandles.brushIDValuesJobHandle);
                        JobHandles.nodeIDValueToNodeOrderArrayJobHandle     = CombineDependencies(currentJobHandle, JobHandles.nodeIDValueToNodeOrderArrayJobHandle);
                        JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle = CombineDependencies(currentJobHandle, JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle);
                        JobHandles.allTreeBrushIndexOrdersJobHandle         = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                    } 
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region CacheRemapping
                    Profiler.BeginSample("Job_CacheRemapping");
                    try
                    {
                        const bool runInParallel = false;//runInParallelDefault;
                        var dependencies = CombineDependencies(JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                               JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                                               JobHandles.brushesJobHandle,
                                                               JobHandles.allTreeBrushIndexOrdersJobHandle,

                                                               JobHandles.brushIDValuesJobHandle,
                                                               JobHandles.basePolygonCacheJobHandle,
                                                               JobHandles.routingTableCacheJobHandle,
                                                               JobHandles.transformationCacheJobHandle,
                                                               JobHandles.brushRenderBufferCacheJobHandle,
                                                               JobHandles.treeSpaceVerticesCacheJobHandle,
                                                               JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                                               JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                                               JobHandles.brushesTouchedByBrushCacheJobHandle,

                                                               JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                               JobHandles.needRemappingRefJobHandle);

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

                            // Read/Write
                            brushIDValues                   = chiselLookupValues.brushIDValues,
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
                        var currentJobHandle = cacheRemappingJob.Schedule(runInParallel, dependencies);

                        //JobHandles.nodeIDValueToNodeOrderArrayJobHandle          = CombineDependencies(currentJobHandle, JobHandles.nodeIDValueToNodeOrderArrayJobHandle);
                        //JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle      = CombineDependencies(currentJobHandle, JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle);
                        //JobHandles.brushesJobHandle                              = CombineDependencies(currentJobHandle, JobHandles.brushesJobHandle);
                        //JobHandles.allTreeBrushIndexOrdersJobHandle              = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);

                        JobHandles.brushIDValuesJobHandle                          = CombineDependencies(currentJobHandle, JobHandles.brushIDValuesJobHandle);
                        JobHandles.basePolygonCacheJobHandle                       = CombineDependencies(currentJobHandle, JobHandles.basePolygonCacheJobHandle);
                        JobHandles.routingTableCacheJobHandle                      = CombineDependencies(currentJobHandle, JobHandles.routingTableCacheJobHandle);
                        JobHandles.transformationCacheJobHandle                    = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                        JobHandles.brushRenderBufferCacheJobHandle                 = CombineDependencies(currentJobHandle, JobHandles.brushRenderBufferCacheJobHandle);
                        JobHandles.treeSpaceVerticesCacheJobHandle                 = CombineDependencies(currentJobHandle, JobHandles.treeSpaceVerticesCacheJobHandle);
                        JobHandles.brushTreeSpacePlaneCacheJobHandle               = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpacePlaneCacheJobHandle);
                        JobHandles.brushTreeSpaceBoundCacheJobHandle               = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpaceBoundCacheJobHandle);
                        JobHandles.brushesTouchedByBrushCacheJobHandle             = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);

                        JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle   = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle);
                        JobHandles.needRemappingRefJobHandle                       = CombineDependencies(currentJobHandle, JobHandles.needRemappingRefJobHandle);
                    } 
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region Find Modified Brushes
                    Profiler.BeginSample("Job_FindModifiedBrushes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = CombineDependencies(JobHandles.brushesJobHandle,
                                                               JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                               JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                               JobHandles.transformTreeBrushIndicesListJobHandle);

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
                        var currentJobHandle = findModifiedBrushesJob.Schedule(runInParallel, dependencies);

                        //JobHandles.brushesJobHandle                      = CombineDependencies(currentJobHandle, JobHandles.brushesJobHandle);
                        //JobHandles.allTreeBrushIndexOrdersJobHandle      = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                        JobHandles.rebuildTreeBrushIndexOrdersJobHandle    = CombineDependencies(currentJobHandle, JobHandles.rebuildTreeBrushIndexOrdersJobHandle);
                        JobHandles.transformTreeBrushIndicesListJobHandle  = CombineDependencies(currentJobHandle, JobHandles.transformTreeBrushIndicesListJobHandle);
                    } 
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region Invalidate Brushes
                    Profiler.BeginSample("Job_InvalidateBrushes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = CombineDependencies(JobHandles.needRemappingRefJobHandle,
                                                               JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                               JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                               JobHandles.brushesJobHandle,
                                                               JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                               JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                                               JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle);

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
                        var currentJobHandle = invalidateBrushesJob.Schedule(runInParallel, dependencies);

                        //JobHandles.needRemappingRefJobHandle                   = CombineDependencies(currentJobHandle, JobHandles.needRemappingRefJobHandle);                        
                        //JobHandles.rebuildTreeBrushIndexOrdersJobHandle        = CombineDependencies(currentJobHandle, JobHandles.rebuildTreeBrushIndexOrdersJobHandle);
                        JobHandles.brushesTouchedByBrushCacheJobHandle           = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle); // Why?
                        //JobHandles.brushesJobHandle                            = CombineDependencies(currentJobHandle, JobHandles.brushesJobHandle);
                        //JobHandles.nodeIDToNodeOrderArrayJobHandle             = CombineDependencies(currentJobHandle, JobHandles.nodeIDToNodeOrderArrayJobHandle);
                        //JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle    = CombineDependencies(currentJobHandle, JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle);
                        JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle);
                    } 
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region Update BrushMesh IDs
                    Profiler.BeginSample("Job_UpdateBrushMeshIDs");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = CombineDependencies(JobHandles.brushMeshBlobsLookupJobHandle,
                                                               JobHandles.brushesJobHandle,
                                                               JobHandles.parametersJobHandle,
                                                               JobHandles.allKnownBrushMeshIndicesJobHandle,
                                                               JobHandles.allBrushMeshIDsJobHandle,
                                                               JobHandles.parameterCountsJobHandle);

                        var updateBrushMeshIDsJob = new UpdateBrushMeshIDsJob
                        {
                            // Read
                            //compactHierarchy          = compactHierarchy,  //<-- cannot do ref or pointer here
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
                        var currentJobHandle = updateBrushMeshIDsJob.Schedule(runInParallel, dependencies);

                        //JobHandles.brushMeshBlobsLookupJobHandle     = CombineDependencies(currentJobHandle, JobHandles.brushMeshBlobsLookupJobHandle);
                        //JobHandles.brushesJobHandle                  = CombineDependencies(currentJobHandle, JobHandles.brushesJobHandle);
                        JobHandles.parametersJobHandle                 = CombineDependencies(currentJobHandle, JobHandles.parametersJobHandle);
                        JobHandles.allKnownBrushMeshIndicesJobHandle   = CombineDependencies(currentJobHandle, JobHandles.allKnownBrushMeshIndicesJobHandle);
                        JobHandles.allBrushMeshIDsJobHandle            = CombineDependencies(currentJobHandle, JobHandles.allBrushMeshIDsJobHandle);
                        JobHandles.parameterCountsJobHandle            = CombineDependencies(currentJobHandle, JobHandles.parameterCountsJobHandle);
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
                        var dependencies = CombineDependencies(JobHandles.transformTreeBrushIndicesListJobHandle,
                                                               JobHandles.transformationCacheJobHandle);
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
                        var currentJobHandle = updateTransformationsJob.Schedule(runInParallel, transformTreeBrushIndicesList, 8, dependencies);

                        //JobHandles.transformTreeBrushIndicesListJobHandle    = CombineDependencies(currentJobHandle, JobHandles.transformTreeBrushIndicesListJobHandle);
                        JobHandles.transformationCacheJobHandle                = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                    } 
                    finally { Profiler.EndSample(); }
                    #endregion

                    Profiler.BeginSample("Job_CompactTreeBuilder");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = CombineDependencies(JobHandles.brushesJobHandle,
                                                               JobHandles.nodesJobHandle,
                                                               JobHandles.compactTreeRefJobHandle);
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
                        var currentJobHandle = buildCompactTreeJob.Schedule(runInParallel, dependencies);

                        //JobHandles.brushesJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushesJobHandle);
                        //JobHandles.nodesJobHandle        = CombineDependencies(currentJobHandle, JobHandles.nodesJobHandle);
                        JobHandles.compactTreeRefJobHandle = CombineDependencies(currentJobHandle, JobHandles.compactTreeRefJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Create lookup table for all brushMeshBlobs, based on the node order in the tree
                    Profiler.BeginSample("Job_FillBrushMeshBlobLookup");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.brushMeshBlobsLookupJobHandle,
                                                                          JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.allBrushMeshIDsJobHandle,
                                                                          JobHandles.brushMeshLookupJobHandle,
                                                                          JobHandles.surfaceCountRefJobHandle);
                        CheckDependencies(runInParallel, dependencies);
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
                        var currentJobHandle = fillBrushMeshBlobLookupJob.Schedule(runInParallel, dependencies);

                        //JobHandles.brushMeshBlobsLookupJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushMeshBlobsLookupJobHandle);
                        //JobHandles.allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                        //JobHandles.allBrushMeshIDsJobHandle           = CombineDependencies(currentJobHandle, JobHandles.allBrushMeshIDsJobHandle);
                        JobHandles.brushMeshLookupJobHandle             = CombineDependencies(currentJobHandle, JobHandles.brushMeshLookupJobHandle);
                        JobHandles.surfaceCountRefJobHandle             = CombineDependencies(currentJobHandle, JobHandles.surfaceCountRefJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Invalidate outdated caches for all modified brushes
                    Profiler.BeginSample("Job_InvalidateBrushCache");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                    
                        var dependencies            = CombineDependencies(JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.basePolygonCacheJobHandle,
                                                                          JobHandles.treeSpaceVerticesCacheJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                                          JobHandles.routingTableCacheJobHandle,
                                                                          JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                                                          JobHandles.brushRenderBufferCacheJobHandle);

                        CheckDependencies(runInParallel, dependencies);
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
                        var currentJobHandle = invalidateBrushCacheJob.Schedule(runInParallel, rebuildTreeBrushIndexOrders, 16, dependencies);

                        //JobHandles.rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, JobHandles.rebuildTreeBrushIndexOrdersJobHandle);
                        JobHandles.basePolygonCacheJobHandle                = CombineDependencies(currentJobHandle, JobHandles.basePolygonCacheJobHandle);
                        JobHandles.treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.treeSpaceVerticesCacheJobHandle);
                        JobHandles.brushesTouchedByBrushCacheJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                        JobHandles.routingTableCacheJobHandle               = CombineDependencies(currentJobHandle, JobHandles.routingTableCacheJobHandle);
                        JobHandles.brushTreeSpacePlaneCacheJobHandle        = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpacePlaneCacheJobHandle);
                        JobHandles.brushRenderBufferCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.brushRenderBufferCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Fix up brush order index in cache data (ordering of brushes may have changed)
                    Profiler.BeginSample("Job_FixupBrushCacheIndices");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                    
                        var dependencies            = CombineDependencies(JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                                          JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle,
                                                                          JobHandles.basePolygonCacheJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
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
                        var currentJobHandle = fixupBrushCacheIndicesJob.Schedule(runInParallel, allTreeBrushIndexOrders, 16, dependencies);

                        //JobHandles.allTreeBrushIndexOrdersJobHandle          = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                        //JobHandles.nodeIndexToNodeOrderArrayJobHandle        = CombineDependencies(currentJobHandle, JobHandles.nodeIndexToNodeOrderArrayJobHandle);
                        //JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle  = CombineDependencies(currentJobHandle, JobHandles.nodeIDValueToNodeOrderOffsetRefJobHandle);
                        JobHandles.basePolygonCacheJobHandle                   = CombineDependencies(currentJobHandle, JobHandles.basePolygonCacheJobHandle);
                        JobHandles.brushesTouchedByBrushCacheJobHandle         = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been modified
                    Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself                    
                        var dependencies            = CombineDependencies(JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.transformationCacheJobHandle,
                                                                          JobHandles.brushMeshLookupJobHandle,
                                                                          JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                                                          JobHandles.treeSpaceVerticesCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
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
                        var currentJobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, rebuildTreeBrushIndexOrders, 16, dependencies);

                        //JobHandles.rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, JobHandles.rebuildTreeBrushIndexOrdersJobHandle);
                        //JobHandles.transformationCacheJobHandle           = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                        //JobHandles.brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, JobHandles.brushMeshLookupJobHandle);
                        JobHandles.brushTreeSpaceBoundCacheJobHandle        = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpaceBoundCacheJobHandle);
                        JobHandles.treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.treeSpaceVerticesCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Find all pairs of brushes that intersect, for those brushes that have been modified
                    Profiler.BeginSample("Job_FindAllBrushIntersectionPairs");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                        // TODO: optimize, use hashed grid                    
                        var dependencies            = CombineDependencies(JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.transformationCacheJobHandle,
                                                                          JobHandles.brushMeshLookupJobHandle,
                                                                          JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                                                          JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushBrushIntersectionsJobHandle,
                                                                          JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var findAllIntersectionsJob = new FindAllBrushIntersectionPairsJob
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
                        var currentJobHandle = findAllIntersectionsJob.Schedule(runInParallel, rebuildTreeBrushIndexOrders, 16, dependencies);

                        //JobHandles.allTreeBrushIndexOrdersJobHandle               = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                        //JobHandles.transformationCacheJobHandle                   = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                        //JobHandles.brushMeshLookupJobHandle                       = CombineDependencies(currentJobHandle, JobHandles.brushMeshLookupJobHandle);
                        //JobHandles.brushTreeSpaceBoundCacheJobHandle              = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpaceBoundCacheJobHandle);
                        //JobHandles.rebuildTreeBrushIndexOrdersJobHandle           = CombineDependencies(currentJobHandle, JobHandles.rebuildTreeBrushIndexOrdersJobHandle);
                        JobHandles.brushBrushIntersectionsJobHandle                 = CombineDependencies(currentJobHandle, JobHandles.brushBrushIntersectionsJobHandle);
                        JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle    = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Find all brushes that touch the brushes that have been modified
                    Profiler.BeginSample("Job_FindUniqueIndirectBrushIntersections");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: optimize, use hashed grid                    
                        var dependencies            = CombineDependencies(JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                                          JobHandles.brushesThatNeedIndirectUpdateJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var createUniqueIndicesArrayJob = new FindUniqueIndirectBrushIntersectionsJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdateHashMap     = brushesThatNeedIndirectUpdateHashMap,
                        
                            // Write
                            brushesThatNeedIndirectUpdate            = brushesThatNeedIndirectUpdate
                        };
                        var currentJobHandle = createUniqueIndicesArrayJob.Schedule(runInParallel, dependencies);

                        //JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle  = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle);
                        JobHandles.brushesThatNeedIndirectUpdateJobHandle           = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Invalidate the cache for the brushes that have been indirectly modified (touch a brush that has changed)
                    Profiler.BeginSample("Job_InvalidateBrushCache_Indirect");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                    
                        var dependencies            = CombineDependencies(JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                                          JobHandles.basePolygonCacheJobHandle,
                                                                          JobHandles.treeSpaceVerticesCacheJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                                          JobHandles.routingTableCacheJobHandle,
                                                                          JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                                                          JobHandles.brushRenderBufferCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var invalidateBrushCacheJob = new InvalidateIndirectBrushCacheJob
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
                        var currentJobHandle = invalidateBrushCacheJob.Schedule(runInParallel, brushesThatNeedIndirectUpdate, 16, dependencies);

                        //JobHandles.brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateJobHandle);
                        JobHandles.basePolygonCacheJobHandle                = CombineDependencies(currentJobHandle, JobHandles.basePolygonCacheJobHandle);
                        JobHandles.treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.treeSpaceVerticesCacheJobHandle);
                        JobHandles.brushesTouchedByBrushCacheJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                        JobHandles.routingTableCacheJobHandle               = CombineDependencies(currentJobHandle, JobHandles.routingTableCacheJobHandle);
                        JobHandles.brushTreeSpacePlaneCacheJobHandle        = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpacePlaneCacheJobHandle);
                        JobHandles.brushRenderBufferCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.brushRenderBufferCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been indirectly modified
                    Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds_Indirect");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                    
                        var dependencies            = CombineDependencies(JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                                          JobHandles.transformationCacheJobHandle,
                                                                          JobHandles.brushMeshLookupJobHandle,
                                                                          JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                                                          JobHandles.treeSpaceVerticesCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
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
                        var currentJobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(runInParallel, brushesThatNeedIndirectUpdate, 16, dependencies);

                        //JobHandles.brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateJobHandle);
                        //JobHandles.transformationCacheJobHandle           = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                        //JobHandles.brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, JobHandles.brushMeshLookupJobHandle);
                        JobHandles.brushTreeSpaceBoundCacheJobHandle        = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpaceBoundCacheJobHandle);
                        JobHandles.treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.treeSpaceVerticesCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Find all pairs of brushes that intersect, for those brushes that have been indirectly modified
                    Profiler.BeginSample("Job_FindAllBrushIntersectionPairs_Indirect");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: optimize, use hashed grid                    
                        var dependencies            = CombineDependencies(JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.transformationCacheJobHandle,
                                                                          JobHandles.brushMeshLookupJobHandle,
                                                                          JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                                                          JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                                          JobHandles.brushBrushIntersectionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var findAllIntersectionsJob = new FindAllIndirectBrushIntersectionPairsJob
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
                        var currentJobHandle = findAllIntersectionsJob.Schedule(runInParallel, brushesThatNeedIndirectUpdate, 1, dependencies);

                        //JobHandles.allTreeBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                        //JobHandles.transformationCacheJobHandle           = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                        //JobHandles.brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, JobHandles.brushMeshLookupJobHandle);
                        //JobHandles.brushTreeSpaceBoundCacheJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpaceBoundCacheJobHandle);
                        //JobHandles.brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateJobHandle);
                        JobHandles.brushBrushIntersectionsJobHandle         = CombineDependencies(currentJobHandle, JobHandles.brushBrushIntersectionsJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Add brushes that need to be indirectly updated to our list of brushes that need updates
                    Profiler.BeginSample("Job_AddIndirectUpdatedBrushesToListAndSort");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                                          JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var findAllIntersectionsJob = new AddIndirectUpdatedBrushesToListAndSortJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = allTreeBrushIndexOrders        .AsArray().AsReadOnly(),
                            brushesThatNeedIndirectUpdate   = brushesThatNeedIndirectUpdate  .AsJobArray(runInParallel),
                            rebuildTreeBrushIndexOrders     = rebuildTreeBrushIndexOrders    .AsArray().AsReadOnly(),

                            // Write
                            allUpdateBrushIndexOrders       = allUpdateBrushIndexOrders      .AsParallelWriter(),
                        };
                        var currentJobHandle = findAllIntersectionsJob.Schedule(runInParallel, dependencies);

                        //JobHandles.allTreeBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                        //JobHandles.brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesThatNeedIndirectUpdateJobHandle);
                        //JobHandles.rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, JobHandles.rebuildTreeBrushIndexOrdersJobHandle);
                        JobHandles.allUpdateBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Gather all found pairs of brushes that intersect with each other and cache them
                    Profiler.BeginSample("Job_GatherAndStoreBrushIntersections");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.brushBrushIntersectionsJobHandle,
                                                                          JobHandles.brushIntersectionsWithJobHandle,
                                                                          JobHandles.brushIntersectionsWithRangeJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var gatherBrushIntersectionsJob = new GatherBrushIntersectionPairsJob
                        {
                            // Read
                            brushBrushIntersections         = brushBrushIntersections,

                            // Write
                            brushIntersectionsWith          = brushIntersectionsWith.GetUnsafeList(),
                            brushIntersectionsWithRange     = brushIntersectionsWithRange
                        };
                        var currentJobHandle = gatherBrushIntersectionsJob.Schedule(runInParallel, dependencies);

                        //JobHandles.brushBrushIntersectionsJobHandle   = CombineDependencies(currentJobHandle, JobHandles.brushBrushIntersectionsJobHandle);
                        JobHandles.brushIntersectionsWithJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushIntersectionsWithJobHandle);
                        JobHandles.brushIntersectionsWithRangeJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushIntersectionsWithRangeJobHandle);

                        dependencies                = CombineDependencies(JobHandles.compactTreeRefJobHandle,
                                                                          JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushIntersectionsWithJobHandle,
                                                                          JobHandles.brushIntersectionsWithRangeJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle);
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
                        currentJobHandle = storeBrushIntersectionsJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 16, dependencies);

                        //JobHandles.compactTreeRefJobHandle            = CombineDependencies(currentJobHandle, JobHandles.compactTreeRefJobHandle);
                        //JobHandles.allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                        //JobHandles.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        JobHandles.brushIntersectionsWithJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushIntersectionsWithJobHandle);
                        JobHandles.brushIntersectionsWithRangeJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushIntersectionsWithRangeJobHandle);
                        JobHandles.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
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
                        var dependencies            = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                                          JobHandles.treeSpaceVerticesCacheJobHandle);
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

                        // Merges original brush vertices together when they are close to avoid t-junctions
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                        {
                            // Read
                            treeBrushIndexOrders        = allUpdateBrushIndexOrders.AsJobArray(sequential),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(sequential),

                            // Read Write
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(sequential),
                        };
                        var currentJobHandle = mergeTouchingBrushVerticesJob.Schedule(sequential, allUpdateBrushIndexOrders, 16, dependencies);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                        JobHandles.treeSpaceVerticesCacheJobHandle       = CombineDependencies(currentJobHandle, JobHandles.treeSpaceVerticesCacheJobHandle);
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
                        var dependencies            = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                                          JobHandles.uniqueBrushPairsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var findBrushPairsJob       = new FindBrushPairsJob
                        {
                            // Read
                            maxOrder                    = brushCount,
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),

                            // Read (Re-allocate) / Write
                            uniqueBrushPairs            = uniqueBrushPairs.GetUnsafeList()
                        };
                        var currentJobHandle = findBrushPairsJob.Schedule(runInParallel, dependencies);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                        JobHandles.uniqueBrushPairsJobHandle             = CombineDependencies(currentJobHandle, JobHandles.uniqueBrushPairsJobHandle);

                        dependencies                = CombineDependencies(JobHandles.intersectingBrushesStreamJobHandle,
                                                                          JobHandles.uniqueBrushPairsJobHandle);

                        currentJobHandle            = NativeStream.ScheduleConstruct(out intersectingBrushesStream, uniqueBrushPairs, dependencies, Allocator.TempJob);

                        //JobHandles.uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, JobHandles.uniqueBrushPairsJobHandle);
                        JobHandles.intersectingBrushesStreamJobHandle   = CombineDependencies(currentJobHandle, JobHandles.intersectingBrushesStreamJobHandle);

                        dependencies                = CombineDependencies(JobHandles.uniqueBrushPairsJobHandle,
                                                                          JobHandles.transformationCacheJobHandle,
                                                                          JobHandles.brushMeshLookupJobHandle,
                                                                          JobHandles.intersectingBrushesStreamJobHandle);
                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
                            uniqueBrushPairs            = uniqueBrushPairs                      .AsJobArray(runInParallel),
                            transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),
                            brushMeshLookup             = brushMeshLookup                       .AsReadOnly(),

                            // Write
                            intersectingBrushesStream   = intersectingBrushesStream  .AsWriter()
                        };
                        currentJobHandle = prepareBrushPairIntersectionsJob.Schedule(runInParallel, uniqueBrushPairs, 1, dependencies);

                        //JobHandles.uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, JobHandles.uniqueBrushPairsJobHandle);
                        //JobHandles.transformationCacheJobHandle       = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                        //JobHandles.brushMeshLookupJobHandle           = CombineDependencies(currentJobHandle, JobHandles.brushMeshLookupJobHandle);
                        JobHandles.intersectingBrushesStreamJobHandle   = CombineDependencies(currentJobHandle, JobHandles.intersectingBrushesStreamJobHandle);
                    } finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_GenerateBasePolygonLoops");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                        var dependencies            = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                                          JobHandles.brushMeshLookupJobHandle,
                                                                          JobHandles.treeSpaceVerticesCacheJobHandle,
                                                                          JobHandles.basePolygonCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
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
                        var currentJobHandle = createBlobPolygonsBlobs.Schedule(runInParallel, allUpdateBrushIndexOrders, 16, dependencies);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                        //JobHandles.brushMeshLookupJobHandle            = CombineDependencies(currentJobHandle, JobHandles.brushMeshLookupJobHandle);
                        //JobHandles.treeSpaceVerticesCacheJobHandle     = CombineDependencies(currentJobHandle, JobHandles.treeSpaceVerticesCacheJobHandle);
                        JobHandles.basePolygonCacheJobHandle             = CombineDependencies(currentJobHandle, JobHandles.basePolygonCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_UpdateBrushTreeSpacePlanes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this at creation time + when moved / store with brush component itself
                        var dependencies            = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushMeshLookupJobHandle,
                                                                          JobHandles.transformationCacheJobHandle,
                                                                          JobHandles.brushTreeSpacePlaneCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders             .AsJobArray(runInParallel),
                            brushMeshLookup             = brushMeshLookup                       .AsReadOnly(),
                            transformationCache         = chiselLookupValues.transformationCache.AsJobArray(runInParallel),

                            // Write
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsJobArray(runInParallel)
                        };
                        var currentJobHandle = createBrushTreeSpacePlanesJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 16, dependencies);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.brushMeshLookupJobHandle           = CombineDependencies(currentJobHandle, JobHandles.brushMeshLookupJobHandle);
                        //JobHandles.transformationCacheJobHandle       = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                        JobHandles.brushTreeSpacePlaneCacheJobHandle    = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpacePlaneCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_CreateIntersectionLoops");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                        
                        var dependencies            = CombineDependencies(JobHandles.surfaceCountRefJobHandle,
                                                                          JobHandles.outputSurfacesJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var currentJobHandle        = NativeConstruct.ScheduleSetCapacity(ref outputSurfaces, surfaceCountRef, dependencies, Allocator.Persistent);

                        JobHandles.outputSurfacesJobHandle  = CombineDependencies(currentJobHandle, JobHandles.outputSurfacesJobHandle);


                        dependencies                = CombineDependencies(JobHandles.uniqueBrushPairsJobHandle,
                                                                          JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                                                          JobHandles.treeSpaceVerticesCacheJobHandle,
                                                                          JobHandles.intersectingBrushesStreamJobHandle,
                                                                          JobHandles.outputSurfaceVerticesJobHandle,
                                                                          JobHandles.outputSurfacesJobHandle);
                        var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
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
                        currentJobHandle = findAllIntersectionLoopsJob.Schedule(runInParallel, uniqueBrushPairs, 8, dependencies);

                        var disposeJobHandle = intersectingBrushesStream.Dispose(currentJobHandle);
                        
                        intersectingBrushesStream = default;

                        //JobHandles.uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, JobHandles.uniqueBrushPairsJobHandle);
                        //JobHandles.brushTreeSpacePlaneCacheJobHandle  = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpacePlaneCacheJobHandle);
                        //JobHandles.treeSpaceVerticesCacheJobHandle    = CombineDependencies(currentJobHandle, JobHandles.treeSpaceVerticesCacheJobHandle);
                        //JobHandles.intersectingBrushesStreamJobHandle = CombineDependencies(currentJobHandle, JobHandles.intersectingBrushesStreamJobHandle);
                        JobHandles.outputSurfaceVerticesJobHandle       = CombineDependencies(currentJobHandle, JobHandles.outputSurfaceVerticesJobHandle);
                        JobHandles.outputSurfacesJobHandle              = CombineDependencies(currentJobHandle, JobHandles.outputSurfacesJobHandle);
                    } finally { Profiler.EndSample(); }
            
                    Profiler.BeginSample("Job_GatherOutputSurfaces");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.outputSurfacesJobHandle,
                                                                          JobHandles.outputSurfacesRangeJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                        {
                            // Read / Write (Sort)
                            outputSurfaces          = outputSurfaces.AsJobArray(runInParallel),

                            // Write
                            outputSurfacesRange     = outputSurfacesRange
                        };
                        var currentJobHandle = gatherOutputSurfacesJob.Schedule(runInParallel, dependencies);

                        JobHandles.outputSurfacesJobHandle          = CombineDependencies(currentJobHandle, JobHandles.outputSurfacesJobHandle);
                        JobHandles.outputSurfacesRangeJobHandle     = CombineDependencies(currentJobHandle, JobHandles.outputSurfacesRangeJobHandle);
                    } finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_FindLoopOverlapIntersections");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.dataStream1JobHandle,
                                                                          JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var currentJobHandle        = NativeStream.ScheduleConstruct(out dataStream1, allUpdateBrushIndexOrders, dependencies, Allocator.TempJob);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        JobHandles.dataStream1JobHandle                 = CombineDependencies(currentJobHandle, JobHandles.dataStream1JobHandle);

                        dependencies                = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.outputSurfaceVerticesJobHandle,
                                                                          JobHandles.outputSurfacesJobHandle,
                                                                          JobHandles.outputSurfacesRangeJobHandle,
                                                                          JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                                                          JobHandles.basePolygonCacheJobHandle,
                                                                          JobHandles.loopVerticesLookupJobHandle,
                                                                          JobHandles.dataStream1JobHandle);
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
                        currentJobHandle = findLoopOverlapIntersectionsJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 1, dependencies);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.outputSurfaceVerticesJobHandle     = CombineDependencies(currentJobHandle, JobHandles.outputSurfaceVerticesJobHandle);
                        //JobHandles.outputSurfacesJobHandle            = CombineDependencies(currentJobHandle, JobHandles.outputSurfacesJobHandle);
                        //JobHandles.outputSurfacesRangeJobHandle       = CombineDependencies(currentJobHandle, JobHandles.outputSurfacesRangeJobHandle);
                        //JobHandles.brushTreeSpacePlaneCacheJobHandle  = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpacePlaneCacheJobHandle);
                        //JobHandles.basePolygonCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.basePolygonCacheJobHandle);
                        JobHandles.loopVerticesLookupJobHandle          = CombineDependencies(currentJobHandle, JobHandles.loopVerticesLookupJobHandle);
                        JobHandles.dataStream1JobHandle                 = CombineDependencies(currentJobHandle, JobHandles.dataStream1JobHandle);
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
                        var dependencies            = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.treeSpaceVerticesCacheJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                                          JobHandles.loopVerticesLookupJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesIndirectJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders.AsJobArray(runInParallel),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsJobArray(runInParallel),
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsJobArray(runInParallel),

                            // Read Write
                            loopVerticesLookup          = loopVerticesLookup,
                        };
                        var currentJobHandle = mergeTouchingBrushVerticesJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 16, dependencies);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                        JobHandles.loopVerticesLookupJobHandle           = CombineDependencies(currentJobHandle, JobHandles.loopVerticesLookupJobHandle);
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
                        var dependencies            = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                                          JobHandles.compactTreeRefJobHandle,
                                                                          JobHandles.routingTableCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
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
                        var currentJobHandle = createRoutingTableJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 1, dependencies);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                        //JobHandles.compactTreeRefJobHandle             = CombineDependencies(currentJobHandle, JobHandles.compactTreeRefJobHandle);
                        JobHandles.routingTableCacheJobHandle            = CombineDependencies(currentJobHandle, JobHandles.routingTableCacheJobHandle);
                    } finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_PerformCSG");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.dataStream2JobHandle,
                                                                          JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        CheckDependencies(runInParallel, dependencies);

                        var currentJobHandle        = NativeStream.ScheduleConstruct(out dataStream2, allUpdateBrushIndexOrders, dependencies, Allocator.TempJob);

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        JobHandles.dataStream2JobHandle                 = CombineDependencies(currentJobHandle, JobHandles.dataStream2JobHandle);

                        dependencies                = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.routingTableCacheJobHandle,
                                                                          JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                                                          JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                                          JobHandles.dataStream1JobHandle,
                                                                          JobHandles.loopVerticesLookupJobHandle,
                                                                          JobHandles.dataStream2JobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        // Perform CSG
                        // TODO: determine when a brush is completely inside another brush
                        //		 (might not have any intersection loops)
                        var performCSGJob           = new PerformCSGJob
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
                        currentJobHandle = performCSGJob.Schedule(runInParallel, allUpdateBrushIndexOrders, 1, dependencies);

                        var disposeJobHandle = dataStream1.Dispose(currentJobHandle);
                        
                        dataStream1 = default;

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.routingTableCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.routingTableCacheJobHandle);
                        //JobHandles.brushTreeSpacePlaneCacheJobHandle   = CombineDependencies(currentJobHandle, JobHandles.brushTreeSpacePlaneCacheJobHandle);
                        //JobHandles.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, JobHandles.brushesTouchedByBrushCacheJobHandle);
                        //JobHandles.dataStream1JobHandle                = CombineDependencies(currentJobHandle, JobHandles.dataStream1JobHandle);
                        //JobHandles.loopVerticesLookupJobHandle         = CombineDependencies(currentJobHandle, JobHandles.loopVerticesLookupJobHandle);
                        JobHandles.dataStream2JobHandle                  = CombineDependencies(currentJobHandle, JobHandles.dataStream2JobHandle);
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
                        var dependencies            = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                                          JobHandles.meshQueriesJobHandle,
                                                                          JobHandles.basePolygonCacheJobHandle,
                                                                          JobHandles.transformationCacheJobHandle,
                                                                          JobHandles.dataStream2JobHandle,
                                                                          JobHandles.brushRenderBufferCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        // TODO: Potentially merge this with PerformCSGJob?
                        var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
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
                        var currentJobHandle = generateSurfaceRenderBuffers.Schedule(runInParallel, allUpdateBrushIndexOrders, 1, dependencies);
                        var disposeJobHandle = dataStream2.Dispose(currentJobHandle);

                        dataStream2 = default;

                        //JobHandles.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        //JobHandles.basePolygonCacheJobHandle          = CombineDependencies(currentJobHandle, JobHandles.basePolygonCacheJobHandle);
                        //JobHandles.transformationCacheJobHandle       = CombineDependencies(currentJobHandle, JobHandles.transformationCacheJobHandle);
                        //JobHandles.dataStream2JobHandle               = CombineDependencies(currentJobHandle, JobHandles.dataStream2JobHandle);
                        JobHandles.brushRenderBufferCacheJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushRenderBufferCacheJobHandle);
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
                        var dependencies            = CombineDependencies(JobHandles.meshQueriesJobHandle,
                                                                          JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                                          JobHandles.brushRenderBufferCacheJobHandle,
                                                                          JobHandles.brushRenderDataJobHandle,
                                                                          JobHandles.subMeshSurfacesJobHandle,
                                                                          JobHandles.subMeshCountsJobHandle,
                                                                          JobHandles.vertexBufferContents_subMeshSectionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        dependencies                = ChiselNativeListExtensions.ScheduleEnsureCapacity(brushRenderData, allTreeBrushIndexOrders, dependencies);
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
                        var currentJobHandle = findBrushRenderBuffersJob.Schedule(runInParallel, dependencies);

                        //JobHandles.meshQueriesJobHandle                           = CombineDependencies(currentJobHandle, JobHandles.meshQueriesJobHandle);
                        //JobHandles.allTreeBrushIndexOrdersJobHandle               = CombineDependencies(currentJobHandle, JobHandles.allTreeBrushIndexOrdersJobHandle);
                        //JobHandles.brushRenderBufferCacheJobHandle                = CombineDependencies(currentJobHandle, JobHandles.brushRenderBufferCacheJobHandle);
                        JobHandles.brushRenderDataJobHandle                         = CombineDependencies(currentJobHandle, JobHandles.brushRenderDataJobHandle);
                        JobHandles.subMeshSurfacesJobHandle                         = CombineDependencies(currentJobHandle, JobHandles.subMeshSurfacesJobHandle);
                        JobHandles.subMeshCountsJobHandle                           = CombineDependencies(currentJobHandle, JobHandles.subMeshCountsJobHandle);
                        JobHandles.vertexBufferContents_subMeshSectionsJobHandle    = CombineDependencies(currentJobHandle, JobHandles.vertexBufferContents_subMeshSectionsJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_PrepareSubSections");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.meshQueriesJobHandle,
                                                                          JobHandles.brushRenderDataJobHandle,
                                                                          JobHandles.sectionsJobHandle,
                                                                          JobHandles.subMeshSurfacesJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var prepareJob = new PrepareSubSectionsJob
                        {
                            // Read
                            meshQueries         = meshQueries.AsReadOnly(),
                            brushRenderData     = brushRenderData.AsJobArray(runInParallel),

                            // Write
                            subMeshSurfaces     = subMeshSurfaces,
                        };
                        var currentJobHandle    = prepareJob.Schedule(runInParallel, meshQueriesLength, 1, dependencies);

                        //JobHandles.meshQueriesJobHandle       = CombineDependencies(currentJobHandle, JobHandles.meshQueriesJobHandle);
                        //JobHandles.brushRenderDataJobHandle   = CombineDependencies(currentJobHandle, JobHandles.brushRenderDataJobHandle);
                        JobHandles.sectionsJobHandle            = CombineDependencies(currentJobHandle, JobHandles.sectionsJobHandle);
                        JobHandles.subMeshSurfacesJobHandle     = CombineDependencies(currentJobHandle, JobHandles.subMeshSurfacesJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_SortSurfaces");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = CombineDependencies(JobHandles.sectionsJobHandle,
                                                               JobHandles.subMeshSurfacesJobHandle,
                                                               JobHandles.subMeshCountsJobHandle,
                                                               JobHandles.vertexBufferContents_subMeshSectionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var parallelSortJob = new SortSurfacesParallelJob
                        {
                            // Read
                            meshQueries      = meshQueries.AsReadOnly(),
                            subMeshSurfaces  = subMeshSurfaces,

                            // Write
                            subMeshCounts    = subMeshCounts
                        };
                        var currentJobHandle = parallelSortJob.Schedule(runInParallel, dependencies);

                        //JobHandles.sectionsJobHandle          = CombineDependencies(currentJobHandle, JobHandles.sectionsJobHandle);
                        //JobHandles.subMeshSurfacesJobHandle   = CombineDependencies(currentJobHandle, JobHandles.subMeshSurfacesJobHandle);
                        JobHandles.subMeshCountsJobHandle       = CombineDependencies(currentJobHandle, JobHandles.subMeshCountsJobHandle);


                        dependencies = CombineDependencies(JobHandles.sectionsJobHandle,
                                                           JobHandles.subMeshSurfacesJobHandle,
                                                           JobHandles.subMeshCountsJobHandle,
                                                           JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                           currentJobHandle);

                        var sortJobGather = new GatherSurfacesJob
                        {
                            // Read / Write
                            subMeshCounts   = subMeshCounts,

                            // Write
                            subMeshSections = vertexBufferContents.subMeshSections,
                        };
                        currentJobHandle = sortJobGather.Schedule(runInParallel, dependencies);

                        JobHandles.subMeshCountsJobHandle                           = CombineDependencies(currentJobHandle, JobHandles.subMeshCountsJobHandle);
                        JobHandles.vertexBufferContents_subMeshSectionsJobHandle    = CombineDependencies(currentJobHandle, JobHandles.vertexBufferContents_subMeshSectionsJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_AllocateVertexBuffers");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                                          JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                        {
                            // Read
                            subMeshSections         = vertexBufferContents.subMeshSections.AsJobArray(runInParallel),

                            // Read Write
                            triangleBrushIndices    = vertexBufferContents.triangleBrushIndices
                        };
                        var currentJobHandle = allocateVertexBuffersJob.Schedule(runInParallel, dependencies);

                        JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle   = CombineDependencies(currentJobHandle, JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_GenerateMeshDescription");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(JobHandles.subMeshCountsJobHandle,
                                                                          JobHandles.vertexBufferContents_meshDescriptionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                        {
                            // Read
                            subMeshCounts       = subMeshCounts.AsJobArray(runInParallel),

                            // Read Write
                            meshDescriptions    = vertexBufferContents.meshDescriptions
                        };
                        var currentJobHandle = generateMeshDescriptionJob.Schedule(runInParallel, dependencies);

                        JobHandles.vertexBufferContents_meshDescriptionsJobHandle = CombineDependencies(currentJobHandle, JobHandles.vertexBufferContents_meshDescriptionsJobHandle);
                        JobHandles.subMeshCountsJobHandle                         = CombineDependencies(currentJobHandle, JobHandles.subMeshCountsJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_CopyToMeshes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        JobHandle dependencies;
                        { 
                            dependencies = CombineDependencies(JobHandles.vertexBufferContents_meshDescriptionsJobHandle,
                                                               JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                               JobHandles.meshDatasJobHandle,
                                                               JobHandles.vertexBufferContents_meshesJobHandle,
                                                               JobHandles.colliderMeshUpdatesJobHandle,
                                                               JobHandles.debugHelperMeshesJobHandle,
                                                               JobHandles.renderMeshesJobHandle);
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
                            var currentJobHandle = assignMeshesJob.Schedule(runInParallel, dependencies);

                            JobHandles.vertexBufferContents_meshesJobHandle = CombineDependencies(currentJobHandle, JobHandles.vertexBufferContents_meshesJobHandle);
                            JobHandles.debugHelperMeshesJobHandle           = CombineDependencies(currentJobHandle, JobHandles.debugHelperMeshesJobHandle);
                            JobHandles.renderMeshesJobHandle                = CombineDependencies(currentJobHandle, JobHandles.renderMeshesJobHandle);
                            JobHandles.colliderMeshUpdatesJobHandle         = CombineDependencies(currentJobHandle, JobHandles.colliderMeshUpdatesJobHandle);
                        }

                        dependencies = CombineDependencies(JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                           JobHandles.subMeshCountsJobHandle,
                                                           JobHandles.subMeshSurfacesJobHandle,
                                                           JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                                           JobHandles.renderMeshesJobHandle,
                                                           JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                           JobHandles.vertexBufferContents_meshesJobHandle);
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
                        var renderMeshJobHandle = renderCopyToMeshJob.Schedule(runInParallel, renderMeshes, 1, dependencies);

                        dependencies = CombineDependencies(JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                           JobHandles.subMeshCountsJobHandle,
                                                           JobHandles.subMeshSurfacesJobHandle,
                                                           JobHandles.vertexBufferContents_renderDescriptorsJobHandle,
                                                           JobHandles.debugHelperMeshesJobHandle,
                                                           JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                           JobHandles.vertexBufferContents_meshesJobHandle);
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
                        var helperMeshJobHandle = helperCopyToMeshJob.Schedule(runInParallel, debugHelperMeshes, 1, dependencies);

                        dependencies = CombineDependencies(JobHandles.vertexBufferContents_subMeshSectionsJobHandle,
                                                           JobHandles.subMeshCountsJobHandle,
                                                           JobHandles.subMeshSurfacesJobHandle,
                                                           JobHandles.vertexBufferContents_colliderDescriptorsJobHandle,
                                                           JobHandles.colliderMeshUpdatesJobHandle,
                                                           JobHandles.vertexBufferContents_meshesJobHandle);
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
                        var colliderMeshJobHandle = colliderCopyToMeshJob.Schedule(runInParallel, colliderMeshUpdates, 16, dependencies);


                        JobHandles.subMeshCountsJobHandle = CombineDependencies(renderMeshJobHandle, helperMeshJobHandle, colliderMeshJobHandle, JobHandles.subMeshCountsJobHandle);

                        JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle = CombineDependencies(renderMeshJobHandle, JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle);
                        JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle = CombineDependencies(helperMeshJobHandle, JobHandles.vertexBufferContents_triangleBrushIndicesJobHandle);

                        JobHandles.vertexBufferContents_meshesJobHandle = CombineDependencies(renderMeshJobHandle, JobHandles.vertexBufferContents_meshesJobHandle);
                        JobHandles.vertexBufferContents_meshesJobHandle = CombineDependencies(helperMeshJobHandle, JobHandles.vertexBufferContents_meshesJobHandle);
                        JobHandles.vertexBufferContents_meshesJobHandle = CombineDependencies(colliderMeshJobHandle, JobHandles.vertexBufferContents_meshesJobHandle);
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
                        var dependencies = CombineDependencies(JobHandles.allTreeBrushIndexOrdersJobHandle,
                                                               JobHandles.brushTreeSpaceBoundCacheJobHandle,
                                                               JobHandles.brushRenderBufferCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
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
                        var currentJobHandle = storeToCacheJob.Schedule(runInParallel, dependencies);
                        JobHandles.storeToCacheJobHandle = CombineDependencies(currentJobHandle, JobHandles.storeToCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region Create wireframes for all new/modified brushes
                    Profiler.BeginSample("Job_UpdateBrushOutline");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = CombineDependencies(JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                               JobHandles.brushMeshBlobsLookupJobHandle);
                        var updateBrushOutlineJob = new UpdateBrushOutlineJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = allUpdateBrushIndexOrders,
                            brushMeshBlobs              = brushMeshBlobs

                            // Write
                            //compactHierarchy          = compactHierarchy,  //<-- cannot do ref or pointer here
                        };
                        updateBrushOutlineJob.InitializeHierarchy(ref compactHierarchy);
                        var currentJobHandle = updateBrushOutlineJob.Schedule(runInParallel, dependencies);
                        JobHandles.updateBrushOutlineJobHandle = currentJobHandle;

                        //JobHandles.brushMeshBlobsLookupJobHandle      = CombineDependencies(currentJobHandle, JobHandles.brushMeshBlobsLookupJobHandle);
                        //JobHandles.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, JobHandles.allUpdateBrushIndexOrdersJobHandle);
                        JobHandles.compactTreeRefJobHandle              = CombineDependencies(currentJobHandle, JobHandles.compactTreeRefJobHandle);
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
                    disposeJobHandle = CombineDependencies(
                                                CombineDependencies(
                                                    JobHandles.allBrushMeshIDsJobHandle,
                                                    JobHandles.allUpdateBrushIndexOrdersJobHandle,
                                                    JobHandles.brushIDValuesJobHandle,
                                                    JobHandles.basePolygonCacheJobHandle,
                                                    JobHandles.brushBrushIntersectionsJobHandle,
                                                    JobHandles.brushesTouchedByBrushCacheJobHandle,
                                                    JobHandles.brushRenderBufferCacheJobHandle,
                                                    JobHandles.brushRenderDataJobHandle),
                                                CombineDependencies(
                                                    JobHandles.brushTreeSpacePlaneCacheJobHandle,
                                                    JobHandles.brushMeshBlobsLookupJobHandle,
                                                    JobHandles.brushMeshLookupJobHandle,
                                                    JobHandles.brushIntersectionsWithJobHandle,
                                                    JobHandles.brushIntersectionsWithRangeJobHandle,
                                                    JobHandles.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                    JobHandles.brushesThatNeedIndirectUpdateJobHandle,
                                                    JobHandles.brushTreeSpaceBoundCacheJobHandle),
                                                CombineDependencies(
                                                    JobHandles.dataStream1JobHandle,
                                                    JobHandles.dataStream2JobHandle,
                                                    JobHandles.intersectingBrushesStreamJobHandle,
                                                    JobHandles.loopVerticesLookupJobHandle,
                                                    JobHandles.meshQueriesJobHandle,
                                                    JobHandles.nodeIDValueToNodeOrderArrayJobHandle,
                                                    JobHandles.outputSurfaceVerticesJobHandle,
                                                    JobHandles.outputSurfacesJobHandle,
                                                    JobHandles.outputSurfacesRangeJobHandle),
                                                CombineDependencies(
                                                    JobHandles.routingTableCacheJobHandle,
                                                    JobHandles.rebuildTreeBrushIndexOrdersJobHandle,
                                                    JobHandles.sectionsJobHandle,
                                                    JobHandles.subMeshSurfacesJobHandle,
                                                    JobHandles.subMeshCountsJobHandle,
                                                    JobHandles.treeSpaceVerticesCacheJobHandle,
                                                    JobHandles.transformationCacheJobHandle,
                                                    JobHandles.uniqueBrushPairsJobHandle),
                                                CombineDependencies(
                                                    JobHandles.brushesJobHandle,
                                                    JobHandles.nodesJobHandle,
                                                    JobHandles.parametersJobHandle,
                                                    JobHandles.allKnownBrushMeshIndicesJobHandle,
                                                    JobHandles.parameterCountsJobHandle,

                                                    JobHandles.updateBrushOutlineJobHandle,
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

                if (brushMeshLookup              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushMeshLookup              .Dispose(disposeJobHandle));
                if (brushIntersectionsWithRange  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushIntersectionsWithRange  .Dispose(disposeJobHandle));
                if (outputSurfacesRange          .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfacesRange          .Dispose(disposeJobHandle));
                if (parameterCounts              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, parameterCounts              .Dispose(disposeJobHandle));
                
                if (loopVerticesLookup           .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, loopVerticesLookup           .Dispose(disposeJobHandle));
                
                if (transformTreeBrushIndicesList.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, transformTreeBrushIndicesList.Dispose(disposeJobHandle));
                if (brushes                      .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushes                      .Dispose(disposeJobHandle));
                if (nodes                        .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, nodes                        .Dispose(disposeJobHandle));
                if (brushRenderData              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushRenderData              .Dispose(disposeJobHandle));
                if (subMeshCounts                .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, subMeshCounts                .Dispose(disposeJobHandle));
                if (subMeshSurfaces              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, subMeshSurfaces              .Dispose(disposeJobHandle));
                if (rebuildTreeBrushIndexOrders  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, rebuildTreeBrushIndexOrders  .Dispose(disposeJobHandle));
                if (allUpdateBrushIndexOrders    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allUpdateBrushIndexOrders    .Dispose(disposeJobHandle));
                if (allBrushMeshIDs              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allBrushMeshIDs              .Dispose(disposeJobHandle));
                if (uniqueBrushPairs             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, uniqueBrushPairs             .Dispose(disposeJobHandle));
                if (brushIntersectionsWith       .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushIntersectionsWith       .Dispose(disposeJobHandle));
                if (outputSurfaceVertices        .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfaceVertices        .Dispose(disposeJobHandle));
                if (outputSurfaces               .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfaces               .Dispose(disposeJobHandle));
                if (brushesThatNeedIndirectUpdate.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdate.Dispose(disposeJobHandle));
                if (nodeIDValueToNodeOrderArray  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, nodeIDValueToNodeOrderArray  .Dispose(disposeJobHandle));
                
                if (brushesThatNeedIndirectUpdateHashMap.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdateHashMap.Dispose(disposeJobHandle));
                if (brushBrushIntersections             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushBrushIntersections             .Dispose(disposeJobHandle));
                
                
                // Note: cannot use "IsCreated" on this job, for some reason it won't be scheduled and then complain that it's leaking? Bug in IsCreated?
                lastJobHandle = CombineDependencies(lastJobHandle, meshQueries.Dispose(disposeJobHandle));

                lastJobHandle = CombineDependencies(lastJobHandle, 
                                                    DisposeTool.Dispose(true, basePolygonDisposeList,           disposeJobHandle),
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
                lastJobHandle = CombineDependencies(lastJobHandle, disposeJobHandle);

                if (allTreeBrushIndexOrders      .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allTreeBrushIndexOrders      .Dispose(disposeJobHandle));
                if (colliderMeshUpdates          .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, colliderMeshUpdates          .Dispose(disposeJobHandle));
                if (debugHelperMeshes            .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, debugHelperMeshes            .Dispose(disposeJobHandle));
                if (renderMeshes                 .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, renderMeshes                 .Dispose(disposeJobHandle));
                if (meshDatas                    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, meshDatas                    .Dispose(disposeJobHandle));
                
                if (vertexBufferContents         .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents         .Dispose(disposeJobHandle));



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
