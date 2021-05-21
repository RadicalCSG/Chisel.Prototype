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

                        var dependencies = CombineDependencies(treeUpdate.meshDatasJobHandle,
                                                               treeUpdate.colliderMeshUpdatesJobHandle,
                                                               treeUpdate.debugHelperMeshesJobHandle,
                                                               treeUpdate.renderMeshesJobHandle,
                                                               treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                               treeUpdate.vertexBufferContents_meshesJobHandle);
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
                                                            treeUpdate.brushTreeSpaceBoundCacheJobHandle),
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
                                                            treeUpdate.renderMeshesJobHandle,
                                                            treeUpdate.surfaceCountRefJobHandle,
                                                            treeUpdate.compactTreeRefJobHandle),
                                                        CombineDependencies(
                                                            treeUpdate.vertexBufferContents_renderDescriptorsJobHandle,
                                                            treeUpdate.vertexBufferContents_colliderDescriptorsJobHandle,
                                                            treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                            treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                            treeUpdate.vertexBufferContents_meshDescriptionsJobHandle,
                                                            treeUpdate.vertexBufferContents_meshesJobHandle,
                                                            treeUpdate.meshDatasJobHandle)
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
            public int                          updateCount;
            public int                          maxNodeOrder;
            public int                          parameter1Count;
            public int                          parameter2Count;
            #region Meshes
            public UnityEngine.Mesh.MeshDataArray           meshDataArray;
            public NativeList<UnityEngine.Mesh.MeshData>    meshDatas;
            #endregion

            #region All Native Collection Temporaries           
            public NativeList<NodeOrderNodeID>  transformTreeBrushIndicesList;

            public NativeList<CompactNodeID>    brushes;
            public NativeList<CompactNodeID>    nodes;

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

            public NativeList<BlobAssetReference<BasePolygonsBlob>>             basePolygonDisposeList;
            public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesDisposeList;
            public NativeList<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushDisposeList;
            public NativeList<BlobAssetReference<RoutingTable>>                 routingTableDisposeList;
            public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneDisposeList;
            public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferDisposeList;

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
            internal JobHandle compactTreeRefJobHandle;
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

            internal JobHandle disposeJobHandle;
            #endregion

            public JobHandle lastJobHandle;

            public unsafe void EnsureSize(int newBrushCount)
            {
                //Debug.Log("EnsureSize");
                meshDataArray   = default;
                meshDatas       = new NativeList<UnityEngine.Mesh.MeshData>(Allocator.TempJob);

                this.brushCount                 = newBrushCount;
                //var triangleArraySize         = GeometryMath.GetTriangleArraySize(newBrushCount);
                //var intersectionCount         = math.max(1, triangleArraySize);
                brushesThatNeedIndirectUpdateHashMap = new NativeHashSet<IndexOrder>(newBrushCount, Allocator.TempJob);
                brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(newBrushCount, Allocator.TempJob);

                // TODO: find actual vertex count
                outputSurfaceVertices           = new NativeList<float3>(65535 * 10, Allocator.TempJob); 

                outputSurfaces                  = new NativeList<BrushIntersectionLoop>(newBrushCount * 16, Allocator.TempJob);
                brushIntersectionsWith          = new NativeList<BrushIntersectWith>(newBrushCount, Allocator.TempJob);

                surfaceCountRef                 = new NativeReference<int>(Allocator.TempJob);
                compactTreeRef                  = new NativeReference<BlobAssetReference<CompactTree>>(Allocator.TempJob);
                
                uniqueBrushPairs                = new NativeList<BrushPair2>(newBrushCount * 16, Allocator.TempJob);
                
                rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(newBrushCount, Allocator.TempJob);
                allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(newBrushCount, Allocator.TempJob);
                allBrushMeshIDs                 = new NativeArray<int>(newBrushCount, Allocator.TempJob);
                brushRenderData                 = new NativeList<BrushData>(newBrushCount, Allocator.TempJob);
                allTreeBrushIndexOrders         = new NativeList<IndexOrder>(newBrushCount, Allocator.TempJob);
                allTreeBrushIndexOrders.Clear();
                allTreeBrushIndexOrders.Resize(newBrushCount, NativeArrayOptions.ClearMemory);

                outputSurfacesRange             = new NativeArray<int2>(newBrushCount, Allocator.TempJob);
                brushIntersectionsWithRange     = new NativeArray<int2>(newBrushCount, Allocator.TempJob);
                nodeIDValueToNodeOrderArray     = new NativeList<int>(newBrushCount, Allocator.TempJob);
                brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(newBrushCount, Allocator.TempJob);

                brushBrushIntersections         = new NativeListArray<BrushIntersectWith>(16, Allocator.TempJob);
                brushBrushIntersections.ResizeExact(newBrushCount);
                
                loopVerticesLookup              = new NativeListArray<float3>(brushCount, Allocator.TempJob);
                loopVerticesLookup.ResizeExact(brushCount);
                
                vertexBufferContents.EnsureInitialized();
                
                meshQueries = default;
            }

            private static void BuildLookupTables([ReadOnly] NativeList<CompactNodeID>  brushes, 
                                                  [ReadOnly] int                        brushCount,
                                                  
                                                  // Read/Write
                                                  NativeList<int>                       nodeIDValueToNodeOrderArray,
                                                  [WriteOnly] out int                   nodeIDValueToNodeOrderOffset,

                                                  [WriteOnly] NativeList<IndexOrder>    allTreeBrushIndexOrders,
                                                  [WriteOnly] NativeList<CompactNodeID> brushIDValues)
            {
                if (brushIDValues.Length != brushCount)
                    brushIDValues.ResizeUninitialized(brushCount);

                var nodeIDValueMin = int.MaxValue;
                var nodeIDValueMax = 0;
                if (brushCount > 0)
                {
                    Debug.Assert(brushCount == brushes.Length);
                    for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                    {
                        var brushCompactNodeID      = brushes[nodeOrder];
                        var brushCompactNodeIDValue = brushCompactNodeID.value;
                        nodeIDValueMin = math.min(nodeIDValueMin, brushCompactNodeIDValue);
                        nodeIDValueMax = math.max(nodeIDValueMax, brushCompactNodeIDValue);
                    }
                } else
                    nodeIDValueMin = 0;

                nodeIDValueToNodeOrderOffset = nodeIDValueMin;
                var desiredLength = (nodeIDValueMax + 1) - nodeIDValueMin;
                    
                nodeIDValueToNodeOrderArray.Clear();
                nodeIDValueToNodeOrderArray.Resize(desiredLength, NativeArrayOptions.ClearMemory);
                for (int nodeOrder  = 0; nodeOrder  < brushCount; nodeOrder ++)
                {
                    var brushCompactNodeID      = brushes[nodeOrder];
                    var brushCompactNodeIDValue = brushCompactNodeID.value;
                    nodeIDValueToNodeOrderArray[brushCompactNodeIDValue - nodeIDValueToNodeOrderOffset] = nodeOrder;
                    
                    // We need the index into the tree to ensure deterministic ordering
                    var brushIndexOrder = new IndexOrder { compactNodeID = brushCompactNodeID, nodeOrder = nodeOrder };
                    allTreeBrushIndexOrders[nodeOrder] = brushIndexOrder;
                    brushIDValues[nodeOrder] = brushCompactNodeID;
                }
            }


            private static void CacheRemapping([ReadOnly] in CompactHierarchy                                   compactHierarchy,
                                               [ReadOnly] NativeList<int>                                       nodeIDValueToNodeOrderArray,
                                               [ReadOnly] int                                                   nodeIDToNodeOrderOffset,
                                               [ReadOnly] NativeList<CompactNodeID>                             brushes,
                                               [ReadOnly] int                                                   brushCount,
                                               [ReadOnly] NativeList<IndexOrder>                                allTreeBrushIndexOrders,
                                               
                                               // Read/Write
                                               ref NativeList<CompactNodeID>                                    brushIDValues,
                                               ref NativeList<BlobAssetReference<BasePolygonsBlob>>             basePolygonCache,
                                               ref NativeList<BlobAssetReference<RoutingTable>>                 routingTableCache,
                                               ref NativeList<NodeTransformations>                              transformationCache,
                                               ref NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache,
                                               ref NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache,
                                               ref NativeList<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache,
                                               ref NativeList<MinMaxAABB>                                       brushTreeSpaceBoundCache,
                                               ref NativeList<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache,

                                               [WriteOnly] NativeHashSet<IndexOrder>                            brushesThatNeedIndirectUpdateHashMap,
                                               [WriteOnly] out bool                                             needRemapping)
            {                    
                needRemapping = false;
                
                // Remaps all cached data from previous brush order in tree, to new brush order
                // TODO: if all brushes need to be rebuild, don't bother to remap since everything is going to be redone anyway
                var previousBrushIDValuesLength = brushIDValues.Length;
                if (previousBrushIDValuesLength > 0)
                {
                    var indexLookup = new NativeArray<int>(nodeIDValueToNodeOrderArray.Length, Allocator.Temp);
                    var remapOldOrderToNewOrder = new NativeArray<int2>(previousBrushIDValuesLength, Allocator.Temp);

                    for (int n = 0; n < brushCount; n++)
                    {
                        var compactNodeID = allTreeBrushIndexOrders[n].compactNodeID;
                        var offsetIDValue = compactNodeID.value - nodeIDToNodeOrderOffset;
                        indexLookup[offsetIDValue] = (n + 1);
                    }
                    using (indexLookup)
                    {
                        using (var removedBrushes = new NativeList<IndexOrder>(previousBrushIDValuesLength, Allocator.Temp))
                        { 
                            var maxCount = math.max(brushCount, previousBrushIDValuesLength) + 1;
                            for (int n = 0; n < previousBrushIDValuesLength; n++)
                            {
                                var sourceID        = brushIDValues[n];
                                var sourceIDValue   = sourceID.value;
                                var sourceOffset    = sourceIDValue - nodeIDToNodeOrderOffset;
                                var destination = (sourceOffset < 0 || sourceOffset >= nodeIDValueToNodeOrderArray.Length) ? -1 : indexLookup[sourceOffset] - 1;
                                if (destination == -1)
                                {
                                    removedBrushes.Add(new IndexOrder { compactNodeID = sourceID, nodeOrder = n });
                                    destination = -1;
                                    needRemapping = true;
                                } else
                                    maxCount = math.max(maxCount, destination + 1);
                                remapOldOrderToNewOrder[n] = new int2(n, destination);
                                needRemapping = needRemapping || (n != destination);
                            }
                            
                            if (needRemapping)
                            {
                                for (int b = 0; b < removedBrushes.Length; b++)
                                {
                                    var indexOrder  = removedBrushes[b];
                                    //int nodeIndex = indexOrder.nodeIndex;
                                    int nodeOrder   = indexOrder.nodeOrder;

                                    var brushTouchedByBrush = brushesTouchedByBrushCache[nodeOrder];
                                    if (!brushTouchedByBrush.IsCreated ||
                                        brushTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                                        continue;

                                    ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
                                    for (int i = 0; i < brushIntersections.Length; i++)
                                    {
                                        var otherBrushID        = brushIntersections[i].nodeIndexOrder.compactNodeID;
                                    
                                        if (!compactHierarchy.IsValidCompactNodeID(otherBrushID))
                                            continue;

                                        // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                                        if (!brushes.Contains(otherBrushID))
                                            continue;

                                        var otherBrushIDValue   = otherBrushID.value;
                                        var otherBrushOrder     = nodeIDValueToNodeOrderArray[otherBrushIDValue - nodeIDToNodeOrderOffset];
                                        var otherIndexOrder     = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherBrushOrder };
                                        brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
                                    }
                                }
                                
                                remapOldOrderToNewOrder.Sort(indexOrderComparer);
                                
                                for (int n = 0; n < previousBrushIDValuesLength; n++)
                                {
                                    var overwrittenValue = remapOldOrderToNewOrder[n].y;
                                    var originValue = remapOldOrderToNewOrder[n].x;
                                    if (overwrittenValue == originValue)
                                        continue;
                                    // TODO: OPTIMIZE!
                                    for (int n2 = n + 1; n2 < previousBrushIDValuesLength; n2++)
                                    {
                                        var tmp = remapOldOrderToNewOrder[n2];
                                        if (tmp.x == overwrittenValue)
                                        {
                                            if (tmp.y == originValue ||
                                                tmp.y >= previousBrushIDValuesLength)
                                            {
                                                remapOldOrderToNewOrder[n2] = new int2(-1, -1);
                                                break;
                                            }
                                            remapOldOrderToNewOrder[n2] = new int2(originValue, tmp.y);
                                            break;
                                        }
                                    }
                                }
                                
                                using (remapOldOrderToNewOrder)
                                {
                                    if (basePolygonCache.Length < maxCount)
                                        basePolygonCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                                    if (routingTableCache.Length < maxCount)
                                        routingTableCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                                    if (transformationCache.Length < maxCount)
                                        transformationCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                                    if (brushRenderBufferCache.Length < maxCount)
                                        brushRenderBufferCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                                    if (treeSpaceVerticesCache.Length < maxCount)
                                        treeSpaceVerticesCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                                    if (brushTreeSpaceBoundCache.Length < maxCount)
                                        brushTreeSpaceBoundCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                                    if (brushTreeSpacePlaneCache.Length < maxCount)
                                        brushTreeSpacePlaneCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                                    if (brushesTouchedByBrushCache.Length < maxCount)
                                        brushesTouchedByBrushCache.Resize(maxCount, NativeArrayOptions.ClearMemory);
                                    
                                    for (int n = 0; n < previousBrushIDValuesLength; n++)
                                    {
                                        var source = remapOldOrderToNewOrder[n].x;
                                        var destination = remapOldOrderToNewOrder[n].y;
                                        if (source == -1)
                                            continue;

                                        if (source == destination)
                                            continue;

                                        if (destination == -1)
                                        {
                                            { var tmp = basePolygonCache[source]; basePolygonCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                            { var tmp = routingTableCache[source]; routingTableCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                            { var tmp = transformationCache[source]; transformationCache[source] = default; }
                                            { var tmp = brushRenderBufferCache[source]; brushRenderBufferCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                            { var tmp = treeSpaceVerticesCache[source]; treeSpaceVerticesCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                            { var tmp = brushTreeSpaceBoundCache[source]; brushTreeSpaceBoundCache[source] = default; }
                                            { var tmp = brushTreeSpacePlaneCache[source]; brushTreeSpacePlaneCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
                                            { var tmp = brushesTouchedByBrushCache[source]; brushesTouchedByBrushCache[source] = default; if (tmp.IsCreated) tmp.Dispose(); }
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
                                }
                            }
                        }
                    }
                }
            }

            private static void FindModifiedBrushes([ReadOnly] in CompactHierarchy          compactHierarchy, 
                                                    [ReadOnly] in NativeList<CompactNodeID> brushes, 
                                                    [ReadOnly] in NativeList<IndexOrder>    allTreeBrushIndexOrders,
                                                    int brushCount,
                                                    [WriteOnly] ref NativeList<NodeOrderNodeID> transformTreeBrushIndicesList, 
                                                    [WriteOnly] ref NativeList<IndexOrder> rebuildTreeBrushIndexOrders,
                                                    [WriteOnly] out int updateCount)
            {
                transformTreeBrushIndicesList.Clear();
                rebuildTreeBrushIndexOrders.Clear();
                if (rebuildTreeBrushIndexOrders.Capacity < brushCount)
                    rebuildTreeBrushIndexOrders.Capacity = brushCount;

                using (var usedBrushes = new NativeBitArray(brushes.Length, Allocator.Temp, NativeArrayOptions.ClearMemory))
                {
                    for (int nodeOrder = 0; nodeOrder < brushes.Length; nodeOrder++)
                    {
                        var brushCompactNodeID = brushes[nodeOrder];
                        if (compactHierarchy.IsAnyStatusFlagSet(brushCompactNodeID))
                        {
                            var indexOrder = allTreeBrushIndexOrders[nodeOrder];
                            Debug.Assert(indexOrder.nodeOrder == nodeOrder);
                            if (!usedBrushes.IsSet(nodeOrder))
                            {
                                usedBrushes.Set(nodeOrder, true);
                                rebuildTreeBrushIndexOrders.AddNoResize(indexOrder);
                            }

                            // Fix up all flags

                            if (compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.ShapeModified))
                            {
                                // Need to update the basePolygons for this node
                                compactHierarchy.ClearStatusFlag(brushCompactNodeID, NodeStatusFlags.ShapeModified);
                                compactHierarchy.SetStatusFlag(brushCompactNodeID, NodeStatusFlags.NeedAllTouchingUpdated);
                            }

                            if (compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.HierarchyModified))
                                compactHierarchy.SetStatusFlag(brushCompactNodeID, NodeStatusFlags.NeedAllTouchingUpdated);

                            if (compactHierarchy.IsStatusFlagSet(brushCompactNodeID, NodeStatusFlags.TransformationModified))
                            {
                                if (compactHierarchy.IsValidCompactNodeID(brushCompactNodeID))
                                    transformTreeBrushIndicesList.Add(new NodeOrderNodeID { nodeOrder = indexOrder.nodeOrder, compactNodeID = brushCompactNodeID });
                                compactHierarchy.ClearStatusFlag(brushCompactNodeID, NodeStatusFlags.TransformationModified);
                                compactHierarchy.SetStatusFlag(brushCompactNodeID, NodeStatusFlags.NeedAllTouchingUpdated);
                            }
                        }
                    }
                }
                updateCount = rebuildTreeBrushIndexOrders.Length;
            }

            private static void InvalidateBrushes([ReadOnly] in CompactHierarchy                                    compactHierarchy,
                                                  bool                                                              needRemapping,
                                                  [ReadOnly] NativeList<IndexOrder>                                 rebuildTreeBrushIndexOrders,
                                                  [ReadOnly] NativeList<BlobAssetReference<BrushesTouchedByBrush>>  brushesTouchedByBrushCache,
                                                  [ReadOnly] NativeList<CompactNodeID>                              brushes,
                                                  int                                                               brushCount,
                                                  [ReadOnly] NativeList<int>                                        nodeIDValueToNodeOrderArray,
                                                  int                                                               nodeIDToNodeOrderOffset,
                                                  [WriteOnly] NativeHashSet<IndexOrder>                             brushesThatNeedIndirectUpdateHashMap)
            {
                if (rebuildTreeBrushIndexOrders.Length == brushCount && !needRemapping)
                    return;
                
                for (int b = 0; b < rebuildTreeBrushIndexOrders.Length; b++)
                {
                    var indexOrder  = rebuildTreeBrushIndexOrders[b];
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
                                
                        if (!compactHierarchy.IsValidCompactNodeID(otherBrushID))
                            continue;

                        // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                        if (!brushes.Contains(otherBrushID))
                            continue;

                        var otherBrushIDValue   = otherBrushID.value;
                        var otherBrushOrder     = nodeIDValueToNodeOrderArray[otherBrushIDValue - nodeIDToNodeOrderOffset];
                        var otherIndexOrder     = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherBrushOrder };
                        brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
                    }
                }
            }

            private static void UpdateBrushMeshInstanceIDs([ReadOnly] in CompactHierarchy compactHierarchy, 
                                                           ref ChiselLayerParameters    parameters1, 
                                                           ref ChiselLayerParameters    parameters2,
                                                           NativeHashSet<int>           allKnownBrushMeshIndices,
                                                           NativeHashMap<int, RefCountedBrushMeshBlob> brushMeshBlobs,
                                                           int brushCount,
                                                           NativeList<CompactNodeID>    brushes,
                                                           NativeArray<int>             allBrushMeshIDs,
                                                           out int parameter1Count,
                                                           out int parameter2Count)
            {   
                bool rebuildParameterList = true;
                var capacity = math.max(allKnownBrushMeshIndices.Count(), brushCount);
                using (var foundBrushMeshIndices = new NativeHashSet<int>(capacity, Allocator.Temp))
                {
                    foundBrushMeshIndices.Clear();
                    for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                    {
                        var brushCompactNodeID = brushes[nodeOrder];
                        int brushMeshHash = 0;
                        if (!compactHierarchy.IsValidCompactNodeID(brushCompactNodeID) ||
                            // NOTE: Assignment is intended, this is not supposed to be a comparison
                            (brushMeshHash = compactHierarchy.GetBrushMeshID(brushCompactNodeID)) == 0)
                        {
                            // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                            Debug.LogError($"Brush with ID {brushCompactNodeID} has its brushMeshID set to {brushMeshHash}, which is invalid.");
                            allBrushMeshIDs[nodeOrder] = 0;
                        } else
                        {
                            allBrushMeshIDs[nodeOrder] = brushMeshHash;
                            foundBrushMeshIndices.Add(brushMeshHash);
                        }
                    }

                    // TODO: optimize all of this, especially slow for complete update
                    using (var removeBrushMeshIndices = new NativeHashSet<int>(capacity, Allocator.Temp))
                    {
                        removeBrushMeshIndices.Clear();
                        foreach (var brushMeshIndex in allKnownBrushMeshIndices)
                        {
                            if (foundBrushMeshIndices.Contains(brushMeshIndex))
                                foundBrushMeshIndices.Remove(brushMeshIndex);
                            else
                                removeBrushMeshIndices.Add(brushMeshIndex);
                        }

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

                parameter1Count = parameters1.uniqueParameterCount;
                parameter2Count = parameters2.uniqueParameterCount;
            }


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

            public void Initialize()
            {
                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);

                Profiler.BeginSample("CSG_Allocations");
                // Make sure that if we, somehow, run this while parts of the previous update is still running, we wait for it to complete
                this.lastJobHandle.Complete();
                this.lastJobHandle = default;

                this.transformTreeBrushIndicesList  = new NativeList<NodeOrderNodeID>(Allocator.TempJob);
                this.nodes                          = new NativeList<CompactNodeID>(Allocator.TempJob);
                this.brushes                        = new NativeList<CompactNodeID>(Allocator.TempJob);

                compactHierarchy.GetTreeNodes(this.nodes, this.brushes);
                    
                #region Allocations/Resize
                var newBrushCount = this.brushes.Length;
                chiselLookupValues.EnsureCapacity(newBrushCount);

                this.EnsureSize(newBrushCount);
                this.brushCount   = newBrushCount;
                this.maxNodeOrder = this.brushCount;

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
                var chiselLookupValues = ChiselTreeLookup.Value[this.tree];
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                ref var compactHierarchy = ref CompactHierarchyManager.GetHierarchy(this.treeCompactNodeID);
                {
                    BuildLookupTables(this.brushes, this.brushCount,

                                      // Read/Write
                                      this.nodeIDValueToNodeOrderArray, out this.nodeIDValueToNodeOrderOffset,

                                      this.allTreeBrushIndexOrders,
                                      chiselLookupValues.brushIDValues);

                    CacheRemapping(in compactHierarchy,
                                   nodeIDValueToNodeOrderArray, this.nodeIDValueToNodeOrderOffset,
                                   brushes, brushCount,
                                   allTreeBrushIndexOrders,

                                   // Read/Write
                                   ref chiselLookupValues.brushIDValues,
                                   ref chiselLookupValues.basePolygonCache,
                                   ref chiselLookupValues.routingTableCache,
                                   ref chiselLookupValues.transformationCache,
                                   ref chiselLookupValues.brushRenderBufferCache,
                                   ref chiselLookupValues.treeSpaceVerticesCache,
                                   ref chiselLookupValues.brushTreeSpacePlaneCache,
                                   ref chiselLookupValues.brushTreeSpaceBoundCache,
                                   ref chiselLookupValues.brushesTouchedByBrushCache,

                                   brushesThatNeedIndirectUpdateHashMap,
                                   out var needRemapping);

                    FindModifiedBrushes(in compactHierarchy, 
                                        in this.brushes, 
                                        in this.allTreeBrushIndexOrders, 
                                        brushCount, 
                                        ref this.transformTreeBrushIndicesList, 
                                        ref this.rebuildTreeBrushIndexOrders, 
                                        out this.updateCount);

                    InvalidateBrushes(in compactHierarchy,
                                      needRemapping, rebuildTreeBrushIndexOrders,
                                      chiselLookupValues.brushesTouchedByBrushCache,
                                      brushes, brushCount,
                                      nodeIDValueToNodeOrderArray, this.nodeIDValueToNodeOrderOffset,
                                      brushesThatNeedIndirectUpdateHashMap);

                    UpdateBrushMeshInstanceIDs(in compactHierarchy,
                                               ref chiselLookupValues.parameters1,
                                               ref chiselLookupValues.parameters2,
                                               chiselLookupValues.allKnownBrushMeshIndices,
                                               brushMeshBlobs,
                                               brushCount, brushes,
                                               allBrushMeshIDs,
                                               out this.parameter1Count,
                                               out this.parameter2Count);
                }

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
                this.compactTreeRefJobHandle = default;
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

                if (updateCount > 0)
                {                 
                    #region Perform CSG

                    #region Prepare

                    #region Build Transformations
                    Profiler.BeginSample("Job_UpdateTransformations");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = transformationCacheJobHandle;
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

                        transformationCacheJobHandle        = CombineDependencies(currentJobHandle, transformationCacheJobHandle);
                    } 
                    finally { Profiler.EndSample(); }
                    #endregion

                    Profiler.BeginSample("Job_CompactTreeBuilder");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = compactTreeRefJobHandle;
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
                        compactTreeRefJobHandle = CombineDependencies(currentJobHandle, compactTreeRefJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Create lookup table for all brushMeshBlobs, based on the node order in the tree
                    Profiler.BeginSample("Job_FillBrushMeshBlobLookup");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(brushMeshBlobsLookupJobHandle,
                                                                          allTreeBrushIndexOrdersJobHandle,
                                                                          allBrushMeshInstanceIDsJobHandle,
                                                                          brushMeshLookupJobHandle,
                                                                          surfaceCountRefJobHandle);
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

                        //brushMeshBlobsLookupJobHandle      = CombineDependencies(currentJobHandle, brushMeshBlobsLookupJobHandle);
                        //allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, allTreeBrushIndexOrdersJobHandle);
                        //allBrushMeshInstanceIDsJobHandle   = CombineDependencies(currentJobHandle, allBrushMeshInstanceIDsJobHandle);
                        brushMeshLookupJobHandle             = CombineDependencies(currentJobHandle, brushMeshLookupJobHandle);
                        surfaceCountRefJobHandle             = CombineDependencies(currentJobHandle, surfaceCountRefJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Invalidate outdated caches for all modified brushes
                    Profiler.BeginSample("Job_InvalidateBrushCache");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                    
                        var dependencies            = CombineDependencies(rebuildTreeBrushIndexOrdersJobHandle,
                                                                          basePolygonCacheJobHandle,
                                                                          treeSpaceVerticesCacheJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle,
                                                                          routingTableCacheJobHandle,
                                                                          brushTreeSpacePlaneCacheJobHandle,
                                                                          brushRenderBufferCacheJobHandle);

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

                        //rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, rebuildTreeBrushIndexOrdersJobHandle);
                        basePolygonCacheJobHandle                = CombineDependencies(currentJobHandle, basePolygonCacheJobHandle);
                        treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, treeSpaceVerticesCacheJobHandle);
                        brushesTouchedByBrushCacheJobHandle      = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                        routingTableCacheJobHandle               = CombineDependencies(currentJobHandle, routingTableCacheJobHandle);
                        brushTreeSpacePlaneCacheJobHandle        = CombineDependencies(currentJobHandle, brushTreeSpacePlaneCacheJobHandle);
                        brushRenderBufferCacheJobHandle          = CombineDependencies(currentJobHandle, brushRenderBufferCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Fix up brush order index in cache data (ordering of brushes may have changed)
                    Profiler.BeginSample("Job_FixupBrushCacheIndices");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                    
                        var dependencies            = CombineDependencies(allTreeBrushIndexOrdersJobHandle,
                                                                          nodeIndexToNodeOrderArrayJobHandle,
                                                                          basePolygonCacheJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var fixupBrushCacheIndicesJob   = new FixupBrushCacheIndicesJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            nodeIDValueToNodeOrderArray     = nodeIDValueToNodeOrderArray.AsArray().AsReadOnly(),
                            nodeIDValueToNodeOrderOffset    = nodeIDValueToNodeOrderOffset,

                            // Read Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache           .AsJobArray(runInParallel),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache .AsJobArray(runInParallel)
                        };
                        var currentJobHandle = fixupBrushCacheIndicesJob.Schedule(runInParallel, allTreeBrushIndexOrders, 16, dependencies);
                        
                        //allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, allTreeBrushIndexOrdersJobHandle);
                        //nodeIndexToNodeOrderArrayJobHandle = CombineDependencies(currentJobHandle, nodeIndexToNodeOrderArrayJobHandle);
                        basePolygonCacheJobHandle            = CombineDependencies(currentJobHandle, basePolygonCacheJobHandle);
                        brushesTouchedByBrushCacheJobHandle  = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been modified
                    Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself                    
                        var dependencies            = CombineDependencies(rebuildTreeBrushIndexOrdersJobHandle,
                                                                          transformationCacheJobHandle,
                                                                          brushMeshLookupJobHandle,
                                                                          brushTreeSpaceBoundCacheJobHandle,                                                                              
                                                                          treeSpaceVerticesCacheJobHandle);
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
                        
                        //rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, rebuildTreeBrushIndexOrdersJobHandle);
                        //transformationCacheJobHandle           = CombineDependencies(currentJobHandle, transformationCacheJobHandle);
                        //brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, brushMeshLookupJobHandle);
                        brushTreeSpaceBoundCacheJobHandle        = CombineDependencies(currentJobHandle, brushTreeSpaceBoundCacheJobHandle);
                        treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, treeSpaceVerticesCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Find all pairs of brushes that intersect, for those brushes that have been modified
                    Profiler.BeginSample("Job_FindAllBrushIntersectionPairs");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                        // TODO: optimize, use hashed grid                    
                        var dependencies            = CombineDependencies(allTreeBrushIndexOrdersJobHandle,
                                                                          transformationCacheJobHandle,
                                                                          brushMeshLookupJobHandle,
                                                                          brushTreeSpaceBoundCacheJobHandle,
                                                                          rebuildTreeBrushIndexOrdersJobHandle,
                                                                          brushBrushIntersectionsJobHandle,
                                                                          brushesThatNeedIndirectUpdateHashMapJobHandle);
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

                        //allTreeBrushIndexOrdersJobHandle               = CombineDependencies(currentJobHandle, allTreeBrushIndexOrdersJobHandle);
                        //transformationCacheJobHandle                   = CombineDependencies(currentJobHandle, transformationCacheJobHandle);
                        //brushMeshLookupJobHandle                       = CombineDependencies(currentJobHandle, brushMeshLookupJobHandle);
                        //brushTreeSpaceBoundCacheJobHandle              = CombineDependencies(currentJobHandle, brushTreeSpaceBoundCacheJobHandle);
                        //rebuildTreeBrushIndexOrdersJobHandle           = CombineDependencies(currentJobHandle, rebuildTreeBrushIndexOrdersJobHandle);
                        brushBrushIntersectionsJobHandle                 = CombineDependencies(currentJobHandle, brushBrushIntersectionsJobHandle);
                        brushesThatNeedIndirectUpdateHashMapJobHandle    = CombineDependencies(currentJobHandle, brushesThatNeedIndirectUpdateHashMapJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Find all brushes that touch the brushes that have been modified
                    Profiler.BeginSample("Job_FindUniqueIndirectBrushIntersections");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: optimize, use hashed grid                    
                        var dependencies            = CombineDependencies(brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                                          brushesThatNeedIndirectUpdateJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var createUniqueIndicesArrayJob = new FindUniqueIndirectBrushIntersectionsJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdateHashMap     = brushesThatNeedIndirectUpdateHashMap,
                        
                            // Write
                            brushesThatNeedIndirectUpdate            = brushesThatNeedIndirectUpdate
                        };
                        var currentJobHandle = createUniqueIndicesArrayJob.Schedule(runInParallel, dependencies);
                        
                        //brushesThatNeedIndirectUpdateHashMapJobHandle  = CombineDependencies(currentJobHandle, brushesThatNeedIndirectUpdateHashMapJobHandle);
                        brushesThatNeedIndirectUpdateJobHandle           = CombineDependencies(currentJobHandle, brushesThatNeedIndirectUpdateJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Invalidate the cache for the brushes that have been indirectly modified (touch a brush that has changed)
                    Profiler.BeginSample("Job_InvalidateBrushCache_Indirect");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                    
                        var dependencies            = CombineDependencies(brushesThatNeedIndirectUpdateJobHandle,
                                                                          basePolygonCacheJobHandle,
                                                                          treeSpaceVerticesCacheJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle,
                                                                          routingTableCacheJobHandle,
                                                                          brushTreeSpacePlaneCacheJobHandle,
                                                                          brushRenderBufferCacheJobHandle);
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
                        
                        //brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, brushesThatNeedIndirectUpdateJobHandle);
                        basePolygonCacheJobHandle                = CombineDependencies(currentJobHandle, basePolygonCacheJobHandle);
                        treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, treeSpaceVerticesCacheJobHandle);
                        brushesTouchedByBrushCacheJobHandle      = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                        routingTableCacheJobHandle               = CombineDependencies(currentJobHandle, routingTableCacheJobHandle);
                        brushTreeSpacePlaneCacheJobHandle        = CombineDependencies(currentJobHandle, brushTreeSpacePlaneCacheJobHandle);
                        brushRenderBufferCacheJobHandle          = CombineDependencies(currentJobHandle, brushRenderBufferCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Create tree space vertices from local vertices + transformations & an AABB for each brush that has been indirectly modified
                    Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds_Indirect");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                    
                        var dependencies            = CombineDependencies(brushesThatNeedIndirectUpdateJobHandle,
                                                                          transformationCacheJobHandle,
                                                                          brushMeshLookupJobHandle,
                                                                          brushTreeSpaceBoundCacheJobHandle,
                                                                          treeSpaceVerticesCacheJobHandle);
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
                        
                        //brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, brushesThatNeedIndirectUpdateJobHandle);
                        //transformationCacheJobHandle           = CombineDependencies(currentJobHandle, transformationCacheJobHandle);
                        //brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, brushMeshLookupJobHandle);
                        brushTreeSpaceBoundCacheJobHandle        = CombineDependencies(currentJobHandle, brushTreeSpaceBoundCacheJobHandle);
                        treeSpaceVerticesCacheJobHandle          = CombineDependencies(currentJobHandle, treeSpaceVerticesCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Find all pairs of brushes that intersect, for those brushes that have been indirectly modified
                    Profiler.BeginSample("Job_FindAllBrushIntersectionPairs_Indirect");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: optimize, use hashed grid                    
                        var dependencies            = CombineDependencies(allTreeBrushIndexOrdersJobHandle,
                                                                          transformationCacheJobHandle,
                                                                          brushMeshLookupJobHandle,
                                                                          brushTreeSpaceBoundCacheJobHandle,
                                                                          brushesThatNeedIndirectUpdateJobHandle,
                                                                          brushBrushIntersectionsJobHandle);
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

                        //allTreeBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, allTreeBrushIndexOrdersJobHandle);
                        //transformationCacheJobHandle           = CombineDependencies(currentJobHandle, transformationCacheJobHandle);
                        //brushMeshLookupJobHandle               = CombineDependencies(currentJobHandle, brushMeshLookupJobHandle);
                        //brushTreeSpaceBoundCacheJobHandle      = CombineDependencies(currentJobHandle, brushTreeSpaceBoundCacheJobHandle);
                        //brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, brushesThatNeedIndirectUpdateJobHandle);
                        brushBrushIntersectionsJobHandle         = CombineDependencies(currentJobHandle, brushBrushIntersectionsJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Add brushes that need to be indirectly updated to our list of brushes that need updates
                    Profiler.BeginSample("Job_AddIndirectUpdatedBrushesToListAndSort");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(allTreeBrushIndexOrdersJobHandle,
                                                                          brushesThatNeedIndirectUpdateJobHandle,
                                                                          rebuildTreeBrushIndexOrdersJobHandle,
                                                                          allUpdateBrushIndexOrdersJobHandle);
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
                        
                        //allTreeBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, allTreeBrushIndexOrdersJobHandle);
                        //brushesThatNeedIndirectUpdateJobHandle = CombineDependencies(currentJobHandle, brushesThatNeedIndirectUpdateJobHandle);
                        //rebuildTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, rebuildTreeBrushIndexOrdersJobHandle);
                        allUpdateBrushIndexOrdersJobHandle       = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    // Gather all found pairs of brushes that intersect with each other and cache them
                    Profiler.BeginSample("Job_GatherAndStoreBrushIntersections");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(brushBrushIntersectionsJobHandle,
                                                                          brushIntersectionsWithJobHandle,
                                                                          brushIntersectionsWithRangeJobHandle);
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
                        
                        //brushBrushIntersectionsJobHandle   = CombineDependencies(currentJobHandle, brushBrushIntersectionsJobHandle);
                        brushIntersectionsWithJobHandle      = CombineDependencies(currentJobHandle, brushIntersectionsWithJobHandle);
                        brushIntersectionsWithRangeJobHandle = CombineDependencies(currentJobHandle, brushIntersectionsWithRangeJobHandle);

                        dependencies                = CombineDependencies(compactTreeRefJobHandle,
                                                                          allTreeBrushIndexOrdersJobHandle,
                                                                          allUpdateBrushIndexOrdersJobHandle,
                                                                          brushIntersectionsWithJobHandle,
                                                                          brushIntersectionsWithRangeJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle);
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
                        
                        //compactTreeRefJobHandle            = CombineDependencies(currentJobHandle, compactTreeRefJobHandle);
                        //allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, allTreeBrushIndexOrdersJobHandle);
                        //allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        brushIntersectionsWithJobHandle      = CombineDependencies(currentJobHandle, brushIntersectionsWithJobHandle);
                        brushIntersectionsWithRangeJobHandle = CombineDependencies(currentJobHandle, brushIntersectionsWithRangeJobHandle);
                        brushesTouchedByBrushCacheJobHandle  = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
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
                        var dependencies            = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle,
                                                                          treeSpaceVerticesCacheJobHandle);
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

                        //allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                        treeSpaceVerticesCacheJobHandle       = CombineDependencies(currentJobHandle, treeSpaceVerticesCacheJobHandle);
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
                        var dependencies            = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle,
                                                                          uniqueBrushPairsJobHandle);
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

                        //allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                        uniqueBrushPairsJobHandle             = CombineDependencies(currentJobHandle, uniqueBrushPairsJobHandle);

                        dependencies                = CombineDependencies(intersectingBrushesStreamJobHandle,
                                                                          uniqueBrushPairsJobHandle);

                        currentJobHandle            = NativeStream.ScheduleConstruct(out intersectingBrushesStream, uniqueBrushPairs, dependencies, Allocator.TempJob);
                        
                        //uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, uniqueBrushPairsJobHandle);
                        intersectingBrushesStreamJobHandle   = CombineDependencies(currentJobHandle, intersectingBrushesStreamJobHandle);

                        dependencies                = CombineDependencies(uniqueBrushPairsJobHandle,
                                                                          transformationCacheJobHandle,
                                                                          brushMeshLookupJobHandle,
                                                                          intersectingBrushesStreamJobHandle);
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

                        //uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, uniqueBrushPairsJobHandle);
                        //transformationCacheJobHandle       = CombineDependencies(currentJobHandle, transformationCacheJobHandle);
                        //brushMeshLookupJobHandle           = CombineDependencies(currentJobHandle, brushMeshLookupJobHandle);
                        intersectingBrushesStreamJobHandle   = CombineDependencies(currentJobHandle, intersectingBrushesStreamJobHandle);
                    } finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_GenerateBasePolygonLoops");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                        var dependencies            = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle,
                                                                          brushMeshLookupJobHandle,
                                                                          treeSpaceVerticesCacheJobHandle, 
                                                                          basePolygonCacheJobHandle);
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
                        
                        //allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                        //brushMeshLookupJobHandle            = CombineDependencies(currentJobHandle, brushMeshLookupJobHandle);
                        //treeSpaceVerticesCacheJobHandle     = CombineDependencies(currentJobHandle, treeSpaceVerticesCacheJobHandle);
                        basePolygonCacheJobHandle             = CombineDependencies(currentJobHandle, basePolygonCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_UpdateBrushTreeSpacePlanes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        // TODO: should only do this at creation time + when moved / store with brush component itself
                        var dependencies            = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          brushMeshLookupJobHandle,
                                                                          transformationCacheJobHandle,
                                                                          brushTreeSpacePlaneCacheJobHandle);
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
                        
                        //allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //brushMeshLookupJobHandle           = CombineDependencies(currentJobHandle, brushMeshLookupJobHandle);
                        //transformationCacheJobHandle       = CombineDependencies(currentJobHandle, transformationCacheJobHandle);
                        brushTreeSpacePlaneCacheJobHandle    = CombineDependencies(currentJobHandle, brushTreeSpacePlaneCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_CreateIntersectionLoops");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;                        
                        var dependencies            = CombineDependencies(surfaceCountRefJobHandle,
                                                                          outputSurfacesJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var currentJobHandle        = NativeConstruct.ScheduleSetCapacity(ref outputSurfaces, surfaceCountRef, dependencies, Allocator.Persistent);
                        
                        outputSurfacesJobHandle  = CombineDependencies(currentJobHandle, outputSurfacesJobHandle);

                        dependencies                = CombineDependencies(uniqueBrushPairsJobHandle,
                                                                          brushTreeSpacePlaneCacheJobHandle,
                                                                          treeSpaceVerticesCacheJobHandle,
                                                                          intersectingBrushesStreamJobHandle,
                                                                          outputSurfaceVerticesJobHandle,
                                                                          outputSurfacesJobHandle);
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

                        //uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, uniqueBrushPairsJobHandle);
                        //brushTreeSpacePlaneCacheJobHandle  = CombineDependencies(currentJobHandle, brushTreeSpacePlaneCacheJobHandle);
                        //treeSpaceVerticesCacheJobHandle    = CombineDependencies(currentJobHandle, treeSpaceVerticesCacheJobHandle);
                        //intersectingBrushesStreamJobHandle = CombineDependencies(currentJobHandle, intersectingBrushesStreamJobHandle);
                        outputSurfaceVerticesJobHandle       = CombineDependencies(currentJobHandle, outputSurfaceVerticesJobHandle);
                        outputSurfacesJobHandle              = CombineDependencies(currentJobHandle, outputSurfacesJobHandle);
                    } finally { Profiler.EndSample(); }
            
                    Profiler.BeginSample("Job_GatherOutputSurfaces");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(outputSurfacesJobHandle,
                                                                          outputSurfacesRangeJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                        {
                            // Read / Write (Sort)
                            outputSurfaces          = outputSurfaces.AsJobArray(runInParallel),

                            // Write
                            outputSurfacesRange     = outputSurfacesRange
                        };
                        var currentJobHandle = gatherOutputSurfacesJob.Schedule(runInParallel, dependencies);
                        
                        outputSurfacesJobHandle          = CombineDependencies(currentJobHandle, outputSurfacesJobHandle);
                        outputSurfacesRangeJobHandle     = CombineDependencies(currentJobHandle, outputSurfacesRangeJobHandle);
                    } finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_FindLoopOverlapIntersections");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(dataStream1JobHandle,
                                                                          allUpdateBrushIndexOrdersJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var currentJobHandle        = NativeStream.ScheduleConstruct(out dataStream1, allUpdateBrushIndexOrders, dependencies, Allocator.TempJob);
                        
                        //allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        dataStream1JobHandle                 = CombineDependencies(currentJobHandle, dataStream1JobHandle);

                        dependencies                = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          outputSurfaceVerticesJobHandle,
                                                                          outputSurfacesJobHandle,
                                                                          outputSurfacesRangeJobHandle,
                                                                          brushTreeSpacePlaneCacheJobHandle,
                                                                          basePolygonCacheJobHandle,
                                                                          loopVerticesLookupJobHandle,
                                                                          dataStream1JobHandle);
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

                        //allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //outputSurfaceVerticesJobHandle     = CombineDependencies(currentJobHandle, outputSurfaceVerticesJobHandle);
                        //outputSurfacesJobHandle            = CombineDependencies(currentJobHandle, outputSurfacesJobHandle);
                        //outputSurfacesRangeJobHandle       = CombineDependencies(currentJobHandle, outputSurfacesRangeJobHandle);
                        //brushTreeSpacePlaneCacheJobHandle  = CombineDependencies(currentJobHandle, brushTreeSpacePlaneCacheJobHandle);
                        //basePolygonCacheJobHandle          = CombineDependencies(currentJobHandle, basePolygonCacheJobHandle);
                        loopVerticesLookupJobHandle          = CombineDependencies(currentJobHandle, loopVerticesLookupJobHandle);
                        dataStream1JobHandle                 = CombineDependencies(currentJobHandle, dataStream1JobHandle);
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
                        var dependencies            = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          treeSpaceVerticesCacheJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle,
                                                                          loopVerticesLookupJobHandle);
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

                        //allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                        loopVerticesLookupJobHandle           = CombineDependencies(currentJobHandle, loopVerticesLookupJobHandle);
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
                        var dependencies            = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle,
                                                                          compactTreeRefJobHandle,
                                                                          routingTableCacheJobHandle);
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

                        //allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                        //compactTreeRefJobHandle             = CombineDependencies(currentJobHandle, compactTreeRefJobHandle);
                        routingTableCacheJobHandle            = CombineDependencies(currentJobHandle, routingTableCacheJobHandle);
                    } finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_PerformCSG");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(dataStream2JobHandle,
                                                                          allUpdateBrushIndexOrdersJobHandle);
                        CheckDependencies(runInParallel, dependencies);

                        var currentJobHandle        = NativeStream.ScheduleConstruct(out dataStream2, allUpdateBrushIndexOrders, dependencies, Allocator.TempJob);
                        
                        //allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        dataStream2JobHandle                 = CombineDependencies(currentJobHandle, dataStream2JobHandle);

                        dependencies                = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          routingTableCacheJobHandle,
                                                                          brushTreeSpacePlaneCacheJobHandle,
                                                                          brushesTouchedByBrushCacheJobHandle,
                                                                          dataStream1JobHandle,
                                                                          loopVerticesLookupJobHandle,
                                                                          dataStream2JobHandle);
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

                        //allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //routingTableCacheJobHandle          = CombineDependencies(currentJobHandle, routingTableCacheJobHandle);
                        //brushTreeSpacePlaneCacheJobHandle   = CombineDependencies(currentJobHandle, brushTreeSpacePlaneCacheJobHandle);
                        //brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, brushesTouchedByBrushCacheJobHandle);
                        //dataStream1JobHandle                = CombineDependencies(currentJobHandle, dataStream1JobHandle);
                        //loopVerticesLookupJobHandle         = CombineDependencies(currentJobHandle, loopVerticesLookupJobHandle);
                        dataStream2JobHandle                  = CombineDependencies(currentJobHandle, dataStream2JobHandle);
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
                        var dependencies            = CombineDependencies(allUpdateBrushIndexOrdersJobHandle,
                                                                          meshQueriesJobHandle,
                                                                          basePolygonCacheJobHandle,
                                                                          transformationCacheJobHandle,
                                                                          dataStream2JobHandle,
                                                                          brushRenderBufferCacheJobHandle);
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

                        //allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
                        //basePolygonCacheJobHandle          = CombineDependencies(currentJobHandle, basePolygonCacheJobHandle);
                        //transformationCacheJobHandle       = CombineDependencies(currentJobHandle, transformationCacheJobHandle);
                        //dataStream2JobHandle               = CombineDependencies(currentJobHandle, dataStream2JobHandle);
                        brushRenderBufferCacheJobHandle      = CombineDependencies(currentJobHandle, brushRenderBufferCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }
                
                    // Schedule all the jobs
                    //JobHandle.ScheduleBatchedJobs();
                    #endregion

                    //
                    // Create meshes out of ALL the generated and cached surfaces
                    //

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
                                meshAllocations += parameter2Count; 
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
                        var dependencies            = CombineDependencies(meshQueriesJobHandle,
                                                                          allTreeBrushIndexOrdersJobHandle,
                                                                          brushRenderBufferCacheJobHandle,
                                                                          brushRenderDataJobHandle,
                                                                          subMeshSurfacesJobHandle,
                                                                          subMeshCountsJobHandle,
                                                                          vertexBufferContents_subMeshSectionsJobHandle);
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

                        //meshQueriesJobHandle                           = CombineDependencies(currentJobHandle, meshQueriesJobHandle);
                        //allTreeBrushIndexOrdersJobHandle               = CombineDependencies(currentJobHandle, allTreeBrushIndexOrdersJobHandle);
                        //reeUpdate.brushRenderBufferCacheJobHandle      = CombineDependencies(currentJobHandle, brushRenderBufferCacheJobHandle);
                        brushRenderDataJobHandle                         = CombineDependencies(currentJobHandle, brushRenderDataJobHandle);
                        subMeshSurfacesJobHandle                         = CombineDependencies(currentJobHandle, subMeshSurfacesJobHandle);
                        subMeshCountsJobHandle                           = CombineDependencies(currentJobHandle, subMeshCountsJobHandle);
                        vertexBufferContents_subMeshSectionsJobHandle    = CombineDependencies(currentJobHandle, vertexBufferContents_subMeshSectionsJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_PrepareSubSections");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(meshQueriesJobHandle,
                                                                          brushRenderDataJobHandle,
                                                                          sectionsJobHandle,
                                                                          subMeshSurfacesJobHandle);
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

                        //meshQueriesJobHandle       = CombineDependencies(currentJobHandle, meshQueriesJobHandle);
                        //brushRenderDataJobHandle   = CombineDependencies(currentJobHandle, brushRenderDataJobHandle);
                        sectionsJobHandle            = CombineDependencies(currentJobHandle, sectionsJobHandle);
                        subMeshSurfacesJobHandle     = CombineDependencies(currentJobHandle, subMeshSurfacesJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_SortSurfaces");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = CombineDependencies(sectionsJobHandle,
                                                               subMeshSurfacesJobHandle,
                                                               subMeshCountsJobHandle,
                                                               vertexBufferContents_subMeshSectionsJobHandle);
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

                        //sectionsJobHandle          = CombineDependencies(currentJobHandle, sectionsJobHandle);
                        //subMeshSurfacesJobHandle   = CombineDependencies(currentJobHandle, subMeshSurfacesJobHandle);
                        subMeshCountsJobHandle       = CombineDependencies(currentJobHandle, subMeshCountsJobHandle);


                        dependencies = CombineDependencies(sectionsJobHandle,
                                                           subMeshSurfacesJobHandle,
                                                           subMeshCountsJobHandle,
                                                           vertexBufferContents_subMeshSectionsJobHandle,
                                                           currentJobHandle);

                        var sortJobGather = new GatherSurfacesJob
                        {
                            // Read / Write
                            subMeshCounts   = subMeshCounts,

                            // Write
                            subMeshSections = vertexBufferContents.subMeshSections,
                        };
                        currentJobHandle = sortJobGather.Schedule(runInParallel, dependencies);

                        subMeshCountsJobHandle                           = CombineDependencies(currentJobHandle, subMeshCountsJobHandle);
                        vertexBufferContents_subMeshSectionsJobHandle    = CombineDependencies(currentJobHandle, vertexBufferContents_subMeshSectionsJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_AllocateVertexBuffers");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(vertexBufferContents_subMeshSectionsJobHandle,
                                                                          vertexBufferContents_triangleBrushIndicesJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                        {
                            // Read
                            subMeshSections         = vertexBufferContents.subMeshSections.AsJobArray(runInParallel),

                            // Read Write
                            triangleBrushIndices    = vertexBufferContents.triangleBrushIndices
                        };
                        var currentJobHandle = allocateVertexBuffersJob.Schedule(runInParallel, dependencies);

                        vertexBufferContents_triangleBrushIndicesJobHandle   = CombineDependencies(currentJobHandle, vertexBufferContents_triangleBrushIndicesJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_GenerateMeshDescription");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies            = CombineDependencies(subMeshCountsJobHandle,
                                                                          vertexBufferContents_meshDescriptionsJobHandle);
                        CheckDependencies(runInParallel, dependencies);
                        var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                        {
                            // Read
                            subMeshCounts       = subMeshCounts.AsJobArray(runInParallel),

                            // Read Write
                            meshDescriptions    = vertexBufferContents.meshDescriptions
                        };
                        var currentJobHandle = generateMeshDescriptionJob.Schedule(runInParallel, dependencies);

                        vertexBufferContents_meshDescriptionsJobHandle = CombineDependencies(currentJobHandle, vertexBufferContents_meshDescriptionsJobHandle);
                        subMeshCountsJobHandle                         = CombineDependencies(currentJobHandle, subMeshCountsJobHandle);
                    }
                    finally { Profiler.EndSample(); }

                    Profiler.BeginSample("Job_CopyToMeshes");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        JobHandle dependencies;
                        { 
                            dependencies = CombineDependencies(vertexBufferContents_meshDescriptionsJobHandle,
                                                               vertexBufferContents_subMeshSectionsJobHandle,
                                                               meshDatasJobHandle,
                                                               vertexBufferContents_meshesJobHandle,
                                                               colliderMeshUpdatesJobHandle,
                                                               debugHelperMeshesJobHandle,
                                                               renderMeshesJobHandle);
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

                            vertexBufferContents_meshesJobHandle = CombineDependencies(currentJobHandle, vertexBufferContents_meshesJobHandle);
                            debugHelperMeshesJobHandle           = CombineDependencies(currentJobHandle, debugHelperMeshesJobHandle);
                            renderMeshesJobHandle                = CombineDependencies(currentJobHandle, renderMeshesJobHandle);
                            colliderMeshUpdatesJobHandle         = CombineDependencies(currentJobHandle, colliderMeshUpdatesJobHandle);
                        }

                        dependencies = CombineDependencies(vertexBufferContents_subMeshSectionsJobHandle,
                                                           subMeshCountsJobHandle,
                                                           subMeshSurfacesJobHandle,                                                            
                                                           vertexBufferContents_renderDescriptorsJobHandle,
                                                           renderMeshesJobHandle,
                                                           vertexBufferContents_triangleBrushIndicesJobHandle,
                                                           vertexBufferContents_meshesJobHandle);
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

                        dependencies = CombineDependencies(vertexBufferContents_subMeshSectionsJobHandle,
                                                           subMeshCountsJobHandle,
                                                           subMeshSurfacesJobHandle,                                                           
                                                           vertexBufferContents_renderDescriptorsJobHandle,
                                                           debugHelperMeshesJobHandle,
                                                           vertexBufferContents_triangleBrushIndicesJobHandle,
                                                           vertexBufferContents_meshesJobHandle);
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

                        dependencies = CombineDependencies(vertexBufferContents_subMeshSectionsJobHandle,
                                                           subMeshCountsJobHandle,
                                                           subMeshSurfacesJobHandle,

                                                           vertexBufferContents_colliderDescriptorsJobHandle,
                                                           colliderMeshUpdatesJobHandle,
                                                           vertexBufferContents_meshesJobHandle);
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


                        subMeshCountsJobHandle = CombineDependencies(renderMeshJobHandle, helperMeshJobHandle, colliderMeshJobHandle, subMeshCountsJobHandle);

                        vertexBufferContents_triangleBrushIndicesJobHandle = CombineDependencies(renderMeshJobHandle, vertexBufferContents_triangleBrushIndicesJobHandle);
                        vertexBufferContents_triangleBrushIndicesJobHandle = CombineDependencies(helperMeshJobHandle, vertexBufferContents_triangleBrushIndicesJobHandle);
                        
                        vertexBufferContents_meshesJobHandle = CombineDependencies(renderMeshJobHandle, vertexBufferContents_meshesJobHandle);
                        vertexBufferContents_meshesJobHandle = CombineDependencies(helperMeshJobHandle, vertexBufferContents_meshesJobHandle);
                        vertexBufferContents_meshesJobHandle = CombineDependencies(colliderMeshJobHandle, vertexBufferContents_meshesJobHandle);
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
                        var dependencies = CombineDependencies(allTreeBrushIndexOrdersJobHandle,
                                                               brushTreeSpaceBoundCacheJobHandle,
                                                               brushRenderBufferCacheJobHandle);
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
                        storeToCacheJobHandle = CombineDependencies(currentJobHandle, storeToCacheJobHandle);
                    }
                    finally { Profiler.EndSample(); }
                    #endregion

                    #region Create wireframes for all new/modified brushes
                    Profiler.BeginSample("Job_UpdateBrushOutline");
                    try
                    {
                        const bool runInParallel = runInParallelDefault;
                        var dependencies = allUpdateBrushIndexOrdersJobHandle;
                        var updateBrushOutlineJob = new UpdateBrushOutlineJob
                        {
                            // Read
                            allUpdateBrushIndexOrders = allUpdateBrushIndexOrders,
                            brushMeshBlobs = brushMeshBlobs

                            // Write
                            //compactHierarchy          = compactHierarchy,  //<-- cannot do ref or pointer here
                        };
                        updateBrushOutlineJob.InitializeHierarchy(ref compactHierarchy);
                        updateBrushOutlineJobHandle = updateBrushOutlineJob.Schedule(runInParallel, dependencies);

                        //brushMeshBlobsLookupJobHandle      = CombineDependencies(currentJobHandle, brushMeshBlobsLookupJobHandle);
                        //allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, allUpdateBrushIndexOrdersJobHandle);
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
                                                    allBrushMeshInstanceIDsJobHandle,
                                                    allUpdateBrushIndexOrdersJobHandle,
                                                    basePolygonCacheJobHandle,
                                                    brushBrushIntersectionsJobHandle,
                                                    brushesTouchedByBrushCacheJobHandle,
                                                    brushRenderBufferCacheJobHandle,
                                                    brushRenderDataJobHandle),
                                                CombineDependencies(
                                                    brushTreeSpacePlaneCacheJobHandle,
                                                    brushMeshBlobsLookupJobHandle,
                                                    brushMeshLookupJobHandle,
                                                    brushIntersectionsWithJobHandle,
                                                    brushIntersectionsWithRangeJobHandle,
                                                    brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                    brushesThatNeedIndirectUpdateJobHandle,
                                                    brushTreeSpaceBoundCacheJobHandle),
                                                CombineDependencies(
                                                    dataStream1JobHandle,
                                                    dataStream2JobHandle,
                                                    intersectingBrushesStreamJobHandle,
                                                    loopVerticesLookupJobHandle,
                                                    meshQueriesJobHandle,
                                                    nodeIndexToNodeOrderArrayJobHandle,
                                                    outputSurfaceVerticesJobHandle,
                                                    outputSurfacesJobHandle,
                                                    outputSurfacesRangeJobHandle),
                                                CombineDependencies(
                                                    routingTableCacheJobHandle,
                                                    rebuildTreeBrushIndexOrdersJobHandle,
                                                    sectionsJobHandle,
                                                    subMeshSurfacesJobHandle,
                                                    subMeshCountsJobHandle,
                                                    treeSpaceVerticesCacheJobHandle,
                                                    transformationCacheJobHandle,
                                                    uniqueBrushPairsJobHandle),
                                                CombineDependencies(
                                                    updateBrushOutlineJobHandle,
                                                    storeToCacheJobHandle)
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
                
                
                if (meshQueries.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, meshQueries.Dispose(disposeJobHandle));

                lastJobHandle = CombineDependencies(lastJobHandle,
                                                    DisposeTool.Dispose(true, basePolygonDisposeList,           disposeJobHandle),
                                                    DisposeTool.Dispose(true, treeSpaceVerticesDisposeList,     disposeJobHandle),
                                                    DisposeTool.Dispose(true, brushesTouchedByBrushDisposeList, disposeJobHandle),
                                                    DisposeTool.Dispose(true, routingTableDisposeList,          disposeJobHandle),
                                                    DisposeTool.Dispose(true, brushTreeSpacePlaneDisposeList,   disposeJobHandle),
                                                    DisposeTool.Dispose(true, brushRenderBufferDisposeList,     disposeJobHandle));

                meshQueries                     = default;
                transformTreeBrushIndicesList   = default;
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
                meshQueries                     = default;

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
                


                surfaceCountRefJobHandle.Complete();
                surfaceCountRefJobHandle = default;
                if (surfaceCountRef.IsCreated) surfaceCountRef.Dispose();
                
                compactTreeRefJobHandle.Complete();
                compactTreeRefJobHandle = default;
                if (compactTreeRef.IsCreated)
                {
                    if (compactTreeRef.Value.IsCreated) compactTreeRef.Value.Dispose();
                    compactTreeRef.Dispose();
                }
                

                
                allTreeBrushIndexOrders         = default;
                colliderMeshUpdates             = default;
                debugHelperMeshes               = default;
                renderMeshes                    = default;
                vertexBufferContents            = default;
                meshDataArray                   = default;
                meshDatas                       = default;
                surfaceCountRef                 = default;
                compactTreeRef                  = default;

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
