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
        internal unsafe struct TreeUpdate
        {

            public int      treeNodeIndex;
            public int      brushCount;
            public int      updateCount;
            public int      maxNodeOrder;
            
            public NativeList<IndexOrder>               allTreeBrushIndexOrders;
            public NativeList<IndexOrder>               rebuildTreeBrushIndexOrders;
            public NativeList<IndexOrder>               allUpdateBrushIndexOrders;
            
            public BlobAssetReference<CompactTree>      compactTree;
            public NativeArray<MeshQuery>               meshQueries;

            public NativeListArray<BrushIntersectWith>  brushBrushIntersections;
            public NativeList<BrushIntersectWith>       brushIntersectionsWith;
            public NativeArray<int2>                    brushIntersectionsWithRange;
            public NativeList<IndexOrder>               brushesThatNeedIndirectUpdate;
            public NativeHashSet<IndexOrder>            brushesThatNeedIndirectUpdateHashMap;

            public NativeList<BrushPair2>               uniqueBrushPairs;

            public NativeList<BlobAssetReference<BrushIntersectionLoop>>    outputSurfaces;
            public NativeArray<int2>                                        outputSurfacesRange;
            public NativeArray<BlobAssetReference<BrushMeshBlob>>           brushMeshLookup;
            public NativeListArray<float3>                                  loopVerticesLookup;



            public VertexBufferContents             vertexBufferContents;

            public NativeList<int>                  nodeIndexToNodeOrderArray;
            public int                              nodeIndexToNodeOrderOffset;

            public NativeList<SectionData>          sections;
            public NativeList<BrushData>            brushRenderData;
            public NativeList<SubMeshCounts>        subMeshCounts;
            public NativeList<SubMeshSurface>       subMeshSurfaces;

            public NativeStream                     dataStream1;
            public NativeStream                     dataStream2;
            public NativeStream                     intersectingBrushesStream;


            internal JobHandle allTreeBrushIndexOrdersJobHandle;
            internal JobHandle allUpdateBrushIndexOrdersJobHandle;

            internal JobHandle basePolygonCacheJobHandle;
            internal JobHandle brushesTouchedByBrushCacheJobHandle;
            internal JobHandle brushTreeSpacePlaneCacheJobHandle;
            internal JobHandle brushRenderBufferCacheJobHandle;
            internal JobHandle brushRenderDataJobHandle;
            internal JobHandle brushTreeSpacePlanesJobHandle;
            internal JobHandle brushMeshLookupJobHandle;
            internal JobHandle brushBrushIntersectionsJobHandle;
            internal JobHandle brushIntersectionsWithJobHandle;
            internal JobHandle brushBrushIntersectionRangeJobHandle;
            internal JobHandle brushesThatNeedIndirectUpdateHashMapJobHandle;
            internal JobHandle brushesThatNeedIndirectUpdateJobHandle;
            internal JobHandle brushTreeSpaceBoundsJobHandle;

            internal JobHandle compactTreeJobHandle;

            internal JobHandle dataStream1JobHandle;
            internal JobHandle dataStream2JobHandle;

            internal JobHandle intersectingBrushesStreamJobHandle;

            internal JobHandle loopVerticesLookupJobHandle;

            internal JobHandle meshQueriesJobHandle;

            internal JobHandle nodeIndexToNodeOrderArrayJobHandle;

            internal JobHandle outputSurfacesJobHandle;
            internal JobHandle outputSurfacesRangeJobHandle;
            internal JobHandle outputJobHandle;

            internal JobHandle routingTableCacheJobHandle;
            internal JobHandle routingTableLookupJobHandle;
            internal JobHandle rebuildTreeBrushIndexOrdersJobHandle;

            internal JobHandle sectionsJobHandle;
            internal JobHandle subMeshSurfacesJobHandle;
            internal JobHandle subMeshCountsJobHandle;
            internal JobHandle subMeshSectionsJobHandle;

            internal JobHandle treeSpaceVerticesCacheJobHandle;
            internal JobHandle treeSpaceVerticesArrayJobHandle;
            internal JobHandle transformationCacheJobHandle;

            internal JobHandle uniqueBrushPairsJobHandle;

            public JobHandle lastJobHandle;

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
                outputSurfaces                  .Clear();
                uniqueBrushPairs                .Clear();
                rebuildTreeBrushIndexOrders     .Clear();
                allUpdateBrushIndexOrders       .Clear();

                sections                        .Clear();
                brushRenderData                 .Clear();
                subMeshCounts                   .Clear();
                subMeshSurfaces                 .Clear();
                Profiler.EndSample();

                if (vertexBufferContents.subMeshSections.IsCreated)
                    vertexBufferContents.subMeshSections.Clear();
                if (vertexBufferContents.meshDescriptions.IsCreated)
                    vertexBufferContents.meshDescriptions.Clear();
                
                brushMeshLookup.ClearStruct();
                
                brushIntersectionsWithRange     .ClearValues();
                outputSurfacesRange             .ClearValues();

                nodeIndexToNodeOrderArray.Clear();

                allTreeBrushIndexOrders.Clear();
                allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);

                loopVerticesLookup.ResizeExact(brushCount);
            }

            public unsafe void EnsureSize(int newBrushCount)
            {
                if (this.brushCount == newBrushCount && nodeIndexToNodeOrderArray.IsCreated)
                {
                    Profiler.BeginSample("CLEAR");
                    Clear();
                    Profiler.EndSample();
                    return;
                }


                //Profiler.BeginSample("DISPOSE");
                //if (this.brushCount > 0)
                //    Dispose(lastJobHandle, onlyBlobs: false);
                //Profiler.EndSample();

                Profiler.BeginSample("NEW");
                this.brushCount                 = newBrushCount;
                var triangleArraySize           = GeometryMath.GetTriangleArraySize(newBrushCount);
                var intersectionCount           = math.max(1, triangleArraySize);
                brushesThatNeedIndirectUpdateHashMap = new NativeHashSet<IndexOrder>(newBrushCount, Allocator.Persistent);
                brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                outputSurfaces                  = new NativeList<BlobAssetReference<BrushIntersectionLoop>>(newBrushCount * 16, Allocator.Persistent);
                brushIntersectionsWith          = new NativeList<BrushIntersectWith>(newBrushCount, Allocator.Persistent);
                Profiler.EndSample();

                Profiler.BeginSample("NEW3");
                uniqueBrushPairs                = new NativeList<BrushPair2>(newBrushCount * 16, Allocator.Persistent);
                Profiler.EndSample();

                Profiler.BeginSample("NEW4");
                rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                allUpdateBrushIndexOrders       = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                brushRenderData                 = new NativeList<BrushData>(newBrushCount, Allocator.Persistent);
                allTreeBrushIndexOrders         = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                allTreeBrushIndexOrders.Clear();
                allTreeBrushIndexOrders.Resize(newBrushCount, NativeArrayOptions.ClearMemory);

                outputSurfacesRange             = new NativeArray<int2>(newBrushCount, Allocator.Persistent);
                brushIntersectionsWithRange     = new NativeArray<int2>(newBrushCount, Allocator.Persistent);
                nodeIndexToNodeOrderArray       = new NativeList<int>(newBrushCount, Allocator.Persistent);
                brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(newBrushCount, Allocator.Persistent);

                brushBrushIntersections         = new NativeListArray<BrushIntersectWith>(16, Allocator.Persistent);
                brushBrushIntersections.ResizeExact(newBrushCount);
                
                loopVerticesLookup              = new NativeListArray<float3>(brushCount, Allocator.Persistent);
                loopVerticesLookup.ResizeExact(brushCount);
                
                if (!vertexBufferContents.subMeshes       .IsCreated) vertexBufferContents.subMeshes        = new NativeListArray<GeneratedSubMesh>(Allocator.Persistent);
                if (!vertexBufferContents.tangents        .IsCreated) vertexBufferContents.tangents         = new NativeListArray<float4>(Allocator.Persistent);
                if (!vertexBufferContents.normals         .IsCreated) vertexBufferContents.normals          = new NativeListArray<float3>(Allocator.Persistent);
                if (!vertexBufferContents.uv0             .IsCreated) vertexBufferContents.uv0              = new NativeListArray<float2>(Allocator.Persistent);
                if (!vertexBufferContents.positions       .IsCreated) vertexBufferContents.positions        = new NativeListArray<float3>(Allocator.Persistent);
                if (!vertexBufferContents.indices         .IsCreated) vertexBufferContents.indices          = new NativeListArray<int>(Allocator.Persistent);
                if (!vertexBufferContents.brushIndices    .IsCreated) vertexBufferContents.brushIndices     = new NativeListArray<int>(Allocator.Persistent);
                if (!vertexBufferContents.meshDescriptions.IsCreated) vertexBufferContents.meshDescriptions = new NativeList<GeneratedMeshDescription>(Allocator.Persistent);
                else vertexBufferContents.meshDescriptions.Clear();
                if (!vertexBufferContents.subMeshSections .IsCreated) vertexBufferContents.subMeshSections  = new NativeList<SubMeshSection>(Allocator.Persistent);
                else vertexBufferContents.subMeshSections.Clear();
                
                meshQueries = default;

                Profiler.EndSample();
            }
            
            public void Dispose(JobHandle disposeJobHandle)//, bool onlyBlobs = false)
            {
                //lastJobHandle = disposeJobHandle;


                Profiler.BeginSample("DISPOSE intersectionLoopBlobs");
                if (outputSurfaces.IsCreated && outputSurfaces.Length > 0)
                {
                    for (int i = 0; i < outputSurfaces.Length; i++)
                    {
                        if (outputSurfaces[i].IsCreated)
                            outputSurfaces[i].Dispose();
                    }
                    outputSurfaces.Clear();
                }
                Profiler.EndSample();

                if (meshQueries.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, meshQueries.Dispose(disposeJobHandle));
                meshQueries = default;

                //if (onlyBlobs)
                //  return;

                Profiler.BeginSample("DISPOSE ARRAY");
                if (brushMeshLookup              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushMeshLookup              .Dispose(disposeJobHandle));
                if (brushIntersectionsWithRange  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushIntersectionsWithRange  .Dispose(disposeJobHandle));
                if (outputSurfacesRange          .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfacesRange          .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE LISTARRAY");
                if (loopVerticesLookup           .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, loopVerticesLookup           .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE LIST");
                if (sections                     .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, sections                     .Dispose(disposeJobHandle));
                if (brushRenderData              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushRenderData              .Dispose(disposeJobHandle));
                if (subMeshCounts                .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, subMeshCounts                .Dispose(disposeJobHandle));
                if (subMeshSurfaces              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, subMeshSurfaces              .Dispose(disposeJobHandle));
                if (allTreeBrushIndexOrders      .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allTreeBrushIndexOrders      .Dispose(disposeJobHandle));
                if (rebuildTreeBrushIndexOrders  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, rebuildTreeBrushIndexOrders  .Dispose(disposeJobHandle));
                if (allUpdateBrushIndexOrders    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allUpdateBrushIndexOrders    .Dispose(disposeJobHandle));                
                if (uniqueBrushPairs             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, uniqueBrushPairs             .Dispose(disposeJobHandle));
                if (brushIntersectionsWith       .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushIntersectionsWith       .Dispose(disposeJobHandle));
                if (outputSurfaces               .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfaces               .Dispose(disposeJobHandle));
                if (brushesThatNeedIndirectUpdate.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdate.Dispose(disposeJobHandle));
                if (nodeIndexToNodeOrderArray    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, nodeIndexToNodeOrderArray    .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE HASMAP");
                if (brushesThatNeedIndirectUpdateHashMap.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdateHashMap.Dispose(disposeJobHandle));
                if (brushBrushIntersections             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushBrushIntersections             .Dispose(disposeJobHandle));
                Profiler.EndSample();

                if (meshQueries.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, meshQueries.Dispose(disposeJobHandle));
                meshQueries = default;

                
                if (vertexBufferContents.subMeshSections .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.subMeshSections.Dispose(disposeJobHandle));
                if (vertexBufferContents.meshDescriptions.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.meshDescriptions.Dispose(disposeJobHandle));
                if (vertexBufferContents.subMeshes       .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.subMeshes.Dispose(disposeJobHandle));
                if (vertexBufferContents.indices         .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.indices.Dispose(disposeJobHandle));
                if (vertexBufferContents.brushIndices    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.brushIndices.Dispose(disposeJobHandle));
                if (vertexBufferContents.positions       .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.positions.Dispose(disposeJobHandle));
                if (vertexBufferContents.tangents        .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.tangents.Dispose(disposeJobHandle));
                if (vertexBufferContents.normals         .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.normals.Dispose(disposeJobHandle));
                if (vertexBufferContents.uv0             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.uv0.Dispose(disposeJobHandle));

                vertexBufferContents.subMeshSections    = default;
                vertexBufferContents.meshDescriptions   = default;
                vertexBufferContents.subMeshes          = default;
                vertexBufferContents.indices            = default;
                vertexBufferContents.brushIndices       = default;
                vertexBufferContents.positions          = default;
                vertexBufferContents.tangents           = default;
                vertexBufferContents.normals            = default;
                vertexBufferContents.uv0                = default;
                vertexBufferContents                    = default;

                sections                        = default;
                brushRenderData                 = default;
                subMeshCounts                   = default;
                subMeshSurfaces                 = default;
                brushMeshLookup                 = default;
                allTreeBrushIndexOrders         = default;
                rebuildTreeBrushIndexOrders     = default;
                allUpdateBrushIndexOrders       = default;
                brushBrushIntersections         = default;
                brushIntersectionsWith          = default;
                brushIntersectionsWithRange     = default;
                nodeIndexToNodeOrderArray       = default;
                brushesThatNeedIndirectUpdate   = default;
                brushesThatNeedIndirectUpdateHashMap = default;
                uniqueBrushPairs                = default;
                outputSurfaces                  = default;
                outputSurfacesRange             = default;
                meshQueries                     = default;

                brushCount = 0;
            }



            public JobHandle streamDependencyHandle;
            public JobHandle generateTreeSpaceVerticesAndBoundsJobHandle;
            public JobHandle generateTreeSpaceVerticesAndBoundsIndirectJobHandle;
            public JobHandle generateBasePolygonLoopsJobHandle;
            public JobHandle mergeTouchingBrushVerticesJobHandle;
            public JobHandle mergeTouchingBrushVertices2JobHandle;

            public JobHandle invalidateBrushCacheJobHandle;
            public JobHandle invalidateIndirectBrushCacheJobHandle;
            public JobHandle fixupBrushCacheIndicesJobJobHandle;
            public JobHandle findAllIntersectionsJobHandle;
            public JobHandle createUniqueIndicesArrayJobHandle;
            public JobHandle findAllIndirectIntersectionsJobHandle;
            public JobHandle gatherBrushIntersectionsJobHandle;
            public JobHandle findIntersectingBrushesJobHandle;

            public JobHandle updateBrushTreeSpacePlanesJobHandle;

            public JobHandle updateBrushCategorizationTablesJobHandle;

            public JobHandle findBrushPairsJobHandle;
            public JobHandle prepareBrushPairIntersectionsJobHandle;
            public JobHandle findAllIntersectionLoopsJobHandle;
            public JobHandle gatherOutputSurfacesJobHandle;

            public JobHandle allFindLoopOverlapIntersectionsJobHandle;

            public JobHandle allPerformAllCSGJobHandle;
            public JobHandle allGenerateSurfaceTrianglesJobHandle;

            public JobHandle findBrushRenderBuffersJobHandle;
            public JobHandle prepareJobHandle;
            public JobHandle sortJobHandle;
            public JobHandle allocateVertexBufferJobHandle;
            public JobHandle fillVertexBuffersJobHandle;
            public JobHandle generateMeshDescriptionJobHandle;
        }


        static HashSet<int>                 s_TempHashSet                   = new HashSet<int>();
        static readonly List<IndexOrder>    s_RemovedBrushes                = new List<IndexOrder>();
        static readonly List<IndexOrder>    s_TransformTreeBrushIndicesList = new List<IndexOrder>();
        static int[]            s_NodeIndexToNodeOrderArray;
        static TreeUpdate[]     s_TreeUpdates;

        struct IndexOrderSort : IComparer<int2>
        {
            public int Compare(int2 x, int2 y)
            {
                int yCompare = x.y.CompareTo(y.y);
                if (yCompare != 0)
                    return yCompare;
                return x.x.CompareTo(y.x);
            }
        }

        static int[] s_IndexLookup;
        static int2[] s_RemapOldOrderToNewOrder;


        static JobHandle CombineDependencies(JobHandle handle0) { return handle0; }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1) { return JobHandle.CombineDependencies(handle0, handle1); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2) { return JobHandle.CombineDependencies(handle0, handle1, handle2); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), handle3 ); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4)); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5)); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), handle6); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7) { return JobHandle.CombineDependencies(JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7)); }
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3, JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8) { return JobHandle.CombineDependencies( JobHandle.CombineDependencies(handle0, handle1, handle2), JobHandle.CombineDependencies(handle3, handle4, handle5), JobHandle.CombineDependencies(handle6, handle7, handle8)); }
        
        static JobHandle CombineDependencies(JobHandle handle0, JobHandle handle1, JobHandle handle2, JobHandle handle3,
                                             JobHandle handle4, JobHandle handle5, JobHandle handle6, JobHandle handle7, JobHandle handle8, params JobHandle[] handles)
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


        internal unsafe static JobHandle UpdateTreeMeshes(UpdateMeshEvent updateMeshEvent, List<int> treeNodeIDs)
        {
            var finalJobHandle = default(JobHandle);

            #region Do the setup for the CSG Jobs
            Profiler.BeginSample("CSG_Setup");

            Profiler.BeginSample("TreeUpdate_Allocate");
            if (s_TreeUpdates == null || s_TreeUpdates.Length < treeNodeIDs.Count)
                s_TreeUpdates = new TreeUpdate[treeNodeIDs.Count];
            Profiler.EndSample();

            var treeUpdateLength = 0;
            for (int t = 0; t < treeNodeIDs.Count; t++)
            {
                var treeNodeIndex   = treeNodeIDs[t] - 1;
                var treeInfo        = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;                
                var allTreeBrushes  = treeInfo.allTreeBrushes.items;
                ref var currentTree = ref s_TreeUpdates[treeUpdateLength];

                currentTree.lastJobHandle.Complete();
                currentTree.lastJobHandle = default;


                int brushCount = allTreeBrushes.Count;

                var chiselLookupValues = ChiselTreeLookup.Value[treeNodeIndex];
                chiselLookupValues.EnsureCapacity(brushCount);

                #region MeshQueries
                // TODO: have more control over the queries
                var meshQueries = MeshQuery.DefaultQueries.ToNativeArray(Allocator.TempJob);
                #endregion

                #region All Native Allocations
                Profiler.BeginSample("CSG_Allocations");
                Profiler.BeginSample("ENSURE_SIZE");
                currentTree.EnsureSize(brushCount);
                Profiler.EndSample();

                if (!currentTree.sections       .IsCreated) currentTree.sections        = new NativeList<SectionData>(Allocator.Persistent);
                if (!currentTree.subMeshSurfaces.IsCreated) currentTree.subMeshSurfaces = new NativeList<SubMeshSurface>(Allocator.Persistent);
                if (!currentTree.subMeshCounts  .IsCreated) currentTree.subMeshCounts   = new NativeList<SubMeshCounts>(Allocator.Persistent);

                currentTree.subMeshCounts.Clear();
                currentTree.sections.Clear();
                if (currentTree.sections.Capacity < meshQueries.Length)
                    currentTree.sections.Capacity = meshQueries.Length;

                ref var brushesThatNeedIndirectUpdateHashMap = ref currentTree.brushesThatNeedIndirectUpdateHashMap;
                ref var brushesThatNeedIndirectUpdate        = ref currentTree.brushesThatNeedIndirectUpdate;
                ref var outputSurfaces                       = ref currentTree.outputSurfaces;
                ref var uniqueBrushPairs                     = ref currentTree.uniqueBrushPairs;

                // Workaround for the incredibly dumb "can't create a stream that is zero sized" when the value is determined at runtime. Yeah, thanks
                uniqueBrushPairs.Add(new BrushPair2() { type = IntersectionType.InvalidValue });


                ref var allTreeBrushIndexOrders              = ref currentTree.allTreeBrushIndexOrders;
                ref var rebuildTreeBrushIndexOrders          = ref currentTree.rebuildTreeBrushIndexOrders;
                ref var rebuildIndirectTreeBrushIndexOrders  = ref currentTree.allUpdateBrushIndexOrders;                
                ref var brushMeshLookup                      = ref currentTree.brushMeshLookup;

                ref var vertexBufferContents = ref currentTree.vertexBufferContents;


                brushesThatNeedIndirectUpdateHashMap.Clear();
                brushesThatNeedIndirectUpdate.Clear();

                Profiler.EndSample();
                #endregion

                #region Build lookup tables to find the tree node-order by node-index   
                Profiler.BeginSample("Lookup_Tables");
                var nodeIndexMin = int.MaxValue;
                var nodeIndexMax = 0;
                if (brushCount > 0)
                {
                    for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                    {
                        int nodeID = allTreeBrushes[nodeOrder];
                        int nodeIndex = nodeID - 1;
                        nodeIndexMin = math.min(nodeIndexMin, nodeIndex);
                        nodeIndexMax = math.max(nodeIndexMax, nodeIndex);
                    }
                } else
                    nodeIndexMin = 0;

                var nodeIndexToNodeOrderOffset  = nodeIndexMin;
                var desiredLength = (nodeIndexMax - nodeIndexMin) + 1;

                if (s_NodeIndexToNodeOrderArray == null ||
                    s_NodeIndexToNodeOrderArray.Length < desiredLength)
                    s_NodeIndexToNodeOrderArray = new int[desiredLength];
                for (int nodeOrder  = 0; nodeOrder  < brushCount; nodeOrder ++)
                {
                    int nodeID     = allTreeBrushes[nodeOrder];
                    int nodeIndex  = nodeID - 1;
                    s_NodeIndexToNodeOrderArray[nodeIndex - nodeIndexToNodeOrderOffset] = nodeOrder;
                    
                    // We need the index into the tree to ensure deterministic ordering
                    var brushIndexOrder = new IndexOrder { nodeIndex = nodeIndex, nodeOrder = nodeOrder };
                    allTreeBrushIndexOrders[nodeOrder] = brushIndexOrder;
                }

                currentTree.nodeIndexToNodeOrderArray.Clear();
                ChiselNativeListExtensions.AddRange(currentTree.nodeIndexToNodeOrderArray, s_NodeIndexToNodeOrderArray);
                currentTree.nodeIndexToNodeOrderOffset = nodeIndexToNodeOrderOffset;

                Profiler.EndSample();
                #endregion


                ref var brushIndices                = ref chiselLookupValues.brushIndices;
                ref var basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                ref var routingTableCache           = ref chiselLookupValues.routingTableCache;
                ref var transformationCache         = ref chiselLookupValues.transformationCache;
                ref var brushRenderBufferCache      = ref chiselLookupValues.brushRenderBufferCache;
                ref var treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                ref var brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                ref var brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                ref var brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;

                // TODO: if all brushes need to be rebuild, don't bother to remap since everything is going to be redone anyway

                // Remaps all cached data from previous brush order in tree, to new brush order
                Profiler.BeginSample("REMAP");
                var previousBrushIndexLength = chiselLookupValues.brushIndices.Length;
                if (previousBrushIndexLength > 0)
                {
                    Profiler.BeginSample("init.s_IndexLookup");
                    if (s_IndexLookup == null ||
                        s_IndexLookup.Length < s_NodeIndexToNodeOrderArray.Length)
                        s_IndexLookup = new int[s_NodeIndexToNodeOrderArray.Length];
                    else
                        Array.Clear(s_IndexLookup, 0, s_IndexLookup.Length);

                    for (int n = 0; n < brushCount; n++)
                    {
                        var offsetIndex = allTreeBrushIndexOrders[n].nodeIndex - nodeIndexToNodeOrderOffset;
                        s_IndexLookup[offsetIndex] = (n + 1);
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample("init.s_RemapOldOrderToNewOrder");
                    if (s_RemapOldOrderToNewOrder == null || s_RemapOldOrderToNewOrder.Length < previousBrushIndexLength)
                        s_RemapOldOrderToNewOrder = new int2[previousBrushIndexLength];
                    else
                        Array.Clear(s_RemapOldOrderToNewOrder, 0, s_RemapOldOrderToNewOrder.Length);
                    Profiler.EndSample();

                    Profiler.BeginSample("check");
                    bool needRemapping = false;
                    var maxCount = math.max(brushCount, previousBrushIndexLength) + 1;
                    s_RemovedBrushes.Clear();
                    for (int n = 0; n < previousBrushIndexLength; n++)
                    {
                        var sourceIndex = brushIndices[n] - nodeIndexToNodeOrderOffset;
                        var destination = (sourceIndex < 0 || sourceIndex >= s_NodeIndexToNodeOrderArray.Length) ? -1 : s_IndexLookup[sourceIndex] - 1;
                        if (destination == -1)
                        {
                            s_RemovedBrushes.Add(new IndexOrder { nodeIndex = sourceIndex, nodeOrder = n });
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
                        for (int b = 0; b < s_RemovedBrushes.Count; b++)
                        {
                            var indexOrder  = s_RemovedBrushes[b];
                            int nodeIndex   = indexOrder.nodeIndex;
                            int nodeOrder   = indexOrder.nodeOrder;

                            var brushTouchedByBrush = brushesTouchedByBrushCache[nodeOrder];
                            if (!brushTouchedByBrush.IsCreated ||
                                brushTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                                continue;

                            ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
                            for (int i = 0; i < brushIntersections.Length; i++)
                            {
                                int otherBrushIndex = brushIntersections[i].nodeIndexOrder.nodeIndex;
                                var otherBrushID    = otherBrushIndex + 1;

                                if (!IsValidNodeID(otherBrushID))
                                    continue;

                                // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                                if (!allTreeBrushes.Contains(otherBrushID))
                                    continue;

                                var otherBrushOrder = s_NodeIndexToNodeOrderArray[otherBrushIndex - nodeIndexToNodeOrderOffset];
                                var otherIndexOrder = new IndexOrder { nodeIndex = otherBrushIndex, nodeOrder = otherBrushOrder };
                                brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
                            }
                        }
                        Profiler.EndSample();

                        Profiler.BeginSample("sort");
                        Array.Sort(s_RemapOldOrderToNewOrder, 0, previousBrushIndexLength, new IndexOrderSort());
                        Profiler.EndSample();

                        Profiler.BeginSample("REMAP2");
                        for (int n = 0; n < previousBrushIndexLength; n++)
                        {
                            var overwrittenValue = s_RemapOldOrderToNewOrder[n].y;
                            var originValue = s_RemapOldOrderToNewOrder[n].x;
                            if (overwrittenValue == originValue)
                                continue;
                            // TODO: OPTIMIZE!
                            for (int n2 = n + 1; n2 < previousBrushIndexLength; n2++)
                            {
                                var tmp = s_RemapOldOrderToNewOrder[n2];
                                if (tmp.x == overwrittenValue)
                                {
                                    if (tmp.y == originValue ||
                                        tmp.y >= previousBrushIndexLength)
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
                        for (int n = 0; n < previousBrushIndexLength; n++)
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
                Profiler.EndSample();
                
                Profiler.BeginSample("RESIZE");
                if (chiselLookupValues.basePolygonCache.Length != brushCount)
                    chiselLookupValues.basePolygonCache.Resize(brushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.routingTableCache.Length != brushCount)
                    chiselLookupValues.routingTableCache.Resize(brushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.transformationCache.Length != brushCount)
                    chiselLookupValues.transformationCache.Resize(brushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushRenderBufferCache.Length != brushCount)
                    chiselLookupValues.brushRenderBufferCache.Resize(brushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.treeSpaceVerticesCache.Length != brushCount)
                    chiselLookupValues.treeSpaceVerticesCache.Resize(brushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpaceBoundCache.Length != brushCount)
                    chiselLookupValues.brushTreeSpaceBoundCache.Resize(brushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpacePlaneCache.Length != brushCount)
                    chiselLookupValues.brushTreeSpacePlaneCache.Resize(brushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushesTouchedByBrushCache.Length != brushCount)
                    chiselLookupValues.brushesTouchedByBrushCache.Resize(brushCount, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushIndices.Length != brushCount)
                    chiselLookupValues.brushIndices.ResizeUninitialized(brushCount);
                Profiler.EndSample();

                Profiler.BeginSample("chiselLookupValues.brushIndices");
                for (int i = 0; i < brushCount; i++)
                    chiselLookupValues.brushIndices[i] = allTreeBrushIndexOrders[i].nodeIndex;
                Profiler.EndSample();

                
                brushIndices                = ref chiselLookupValues.brushIndices;
                basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                routingTableCache           = ref chiselLookupValues.routingTableCache;
                transformationCache         = ref chiselLookupValues.transformationCache;
                brushRenderBufferCache      = ref chiselLookupValues.brushRenderBufferCache;
                treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;

                // TODO: do this in job, build brushMeshList in same job
                #region Build all BrushMeshBlobs
                Profiler.BeginSample("CSG_BrushMeshBlob_Generation");
                ChiselMeshLookup.Update();
                Profiler.EndSample();

                Profiler.BeginSample("CSG_BrushMeshBlobs");
                int surfaceCount = 0;
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID      = allTreeBrushes[nodeOrder];
                    int nodeIndex   = nodeID - 1;
                    int brushMeshID = 0;
                    if (!IsValidNodeID(nodeID) ||
                        // NOTE: Assignment is intended, this is not supposed to be a comparison
                        (brushMeshID = CSGManager.nodeHierarchies[nodeIndex].brushInfo.brushMeshInstanceID) == 0)
                    {
                        // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                        Debug.LogError($"Brush with ID {nodeID}, index {nodeIndex} has its brushMeshID set to {brushMeshID}, which is invalid.");
                        brushMeshLookup[nodeOrder] = BlobAssetReference<BrushMeshBlob>.Null;
                    } else
                    if (brushMeshBlobs.TryGetValue(brushMeshID - 1, out var item))
                    {
                        surfaceCount += item.Value.polygons.Length;
                        brushMeshLookup[nodeOrder] = item;
                    } else
                    {
                        Debug.LogError($"Brush with ID {nodeID}, index {nodeIndex} has its brushMeshID set to {brushMeshID}, which is not initialized.");
                        brushMeshLookup[nodeOrder] = BlobAssetReference<BrushMeshBlob>.Null;
                    }
                }
                Profiler.EndSample();
                #endregion

                // Since we cannot allocate in a job because DOTS is junk, and we cannot possibly predict the size of surfaces up front 
                // without doing the actual work, we're forced to allocate a ridiculous amount of memory and *hope* it's enough
                Profiler.BeginSample("CSG_OutputSurfacesCapacity");
                if (currentTree.outputSurfaces.Capacity < (surfaceCount + 1) * 100)
                    currentTree.outputSurfaces.Capacity = (surfaceCount + 1) * 100;
                var anyHierarchyModified = false;
                s_TransformTreeBrushIndicesList.Clear();
                Profiler.EndSample();

                #region Build list of all brushes that have been modified
                Profiler.BeginSample("Modified_Brushes");
                rebuildIndirectTreeBrushIndexOrders.Clear();
                if (rebuildIndirectTreeBrushIndexOrders.Capacity < brushCount)
                    rebuildIndirectTreeBrushIndexOrders.Capacity = brushCount;
                rebuildTreeBrushIndexOrders.Clear();
                if (rebuildTreeBrushIndexOrders.Capacity < brushCount)
                    rebuildTreeBrushIndexOrders.Capacity = brushCount;
                s_TempHashSet.Clear();
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID     = allTreeBrushes[nodeOrder];
                    int nodeIndex  = nodeID - 1;

                    var nodeFlags = CSGManager.nodeFlags[nodeIndex];
                    if (nodeFlags.status != NodeStatusFlags.None)
                    {
                        var indexOrder = allTreeBrushIndexOrders[nodeOrder];
                        if (!s_TempHashSet.Contains(indexOrder.nodeIndex))
                            rebuildTreeBrushIndexOrders.AddNoResize(indexOrder);
                        
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
                            s_TransformTreeBrushIndicesList.Add(indexOrder);
                            nodeFlags.status &= ~NodeStatusFlags.TransformationModified;
                            nodeFlags.status |= NodeStatusFlags.NeedAllTouchingUpdated;
                        }

                        CSGManager.nodeFlags[nodeIndex] = nodeFlags;
                    }
                }
                s_TempHashSet.Clear();
                Profiler.EndSample();
                #endregion

                #region Invalidate brushes that touch our modified brushes, so we rebuild those too
                if (rebuildTreeBrushIndexOrders.Length != brushCount ||
                    s_RemovedBrushes.Count > 0)
                {
                    Profiler.BeginSample("Invalidate_Brushes");
                    for (int b = 0; b < rebuildTreeBrushIndexOrders.Length; b++)
                    {
                        var indexOrder  = rebuildTreeBrushIndexOrders[b];
                        int nodeIndex   = indexOrder.nodeIndex;
                        int nodeOrder   = indexOrder.nodeOrder;

                        var nodeFlags = CSGManager.nodeFlags[nodeIndex];
                        if ((nodeFlags.status & NodeStatusFlags.NeedAllTouchingUpdated) == NodeStatusFlags.None)
                            continue;

                        var brushTouchedByBrush = brushesTouchedByBrushCache[nodeOrder];
                        if (!brushTouchedByBrush.IsCreated ||
                            brushTouchedByBrush == BlobAssetReference<BrushesTouchedByBrush>.Null)
                            continue;

                        ref var brushIntersections = ref brushTouchedByBrush.Value.brushIntersections;
                        for (int i = 0; i < brushIntersections.Length; i++)
                        {
                            int otherBrushIndex = brushIntersections[i].nodeIndexOrder.nodeIndex;
                            var otherBrushID    = otherBrushIndex + 1;

                            if (!IsValidNodeID(otherBrushID))
                                continue;

                            // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                            if (!allTreeBrushes.Contains(otherBrushID))
                                continue;

                            var otherBrushOrder = s_NodeIndexToNodeOrderArray[otherBrushIndex - nodeIndexToNodeOrderOffset];
                            var otherIndexOrder = new IndexOrder { nodeIndex = otherBrushIndex, nodeOrder = otherBrushOrder };
                            brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
                        }
                    }
                    Profiler.EndSample();
                }
                #endregion

                // TODO: optimize, only do this when necessary
                #region Build Transformations
                Profiler.BeginSample("CSG_UpdateBrushTransformations");
                {
                    for (int b = 0; b < s_TransformTreeBrushIndicesList.Count; b++)
                    {
                        var nodeIndexOrder = s_TransformTreeBrushIndicesList[b];
                        transformationCache[nodeIndexOrder.nodeOrder] = GetNodeTransformation(nodeIndexOrder.nodeIndex);
                    }
                }
                Profiler.EndSample();
                #endregion

                #region Build Compact Tree
                Profiler.BeginSample("CSG_CompactTree.Update");
                ref var compactTree = ref chiselLookupValues.compactTree;
                // only rebuild this when the hierarchy changes
                if (anyHierarchyModified ||
                    !compactTree.IsCreated)
                {
                    Profiler.BeginSample("CSG_CompactTree.Dispose");
                    if (compactTree.IsCreated)
                        compactTree.Dispose();
                    Profiler.EndSample();

                    // TODO: jobify?
                    Profiler.BeginSample("CSG_CompactTree.Create");
                    compactTree = CompactTree.Create(CSGManager.nodeHierarchies, treeNodeIndex);
                    chiselLookupValues.compactTree = compactTree;
                    Profiler.EndSample();
                }
                Profiler.EndSample();
                #endregion

                #region Build per tree lookup
                Profiler.BeginSample("Init");
                currentTree.treeNodeIndex   = treeNodeIndex;
                currentTree.brushCount      = brushCount;
                currentTree.updateCount     = rebuildTreeBrushIndexOrders.Length;
                currentTree.maxNodeOrder    = allTreeBrushes.Count;
                currentTree.compactTree     = compactTree;
                currentTree.meshQueries     = meshQueries;
                Profiler.EndSample();
                #endregion

                treeUpdateLength++;
            }

            Profiler.BeginSample("Sort");
            // Sort trees from largest (slowest) to smallest (fastest)
            // The slowest trees will run first, and the fastest trees can then hopefully fill the gaps
            Array.Sort(s_TreeUpdates, s_TreeSorter);
            Profiler.EndSample();

            Profiler.EndSample();
            #endregion

            for (int t = 0; t < treeUpdateLength; t++)
            {
                ref var treeUpdate = ref s_TreeUpdates[t];

                treeUpdate.allTreeBrushIndexOrdersJobHandle = default;
                treeUpdate.allUpdateBrushIndexOrdersJobHandle = default;

                treeUpdate.basePolygonCacheJobHandle = default;
                treeUpdate.brushesTouchedByBrushCacheJobHandle = default;
                treeUpdate.brushTreeSpacePlaneCacheJobHandle = default;
                treeUpdate.brushRenderBufferCacheJobHandle = default;
                treeUpdate.brushRenderDataJobHandle = default;
                treeUpdate.brushTreeSpacePlanesJobHandle = default;
                treeUpdate.brushMeshLookupJobHandle = default;
                treeUpdate.brushBrushIntersectionsJobHandle = default;
                treeUpdate.brushIntersectionsWithJobHandle = default;
                treeUpdate.brushBrushIntersectionRangeJobHandle = default;
                treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle = default;
                treeUpdate.brushesThatNeedIndirectUpdateJobHandle = default;
                treeUpdate.brushTreeSpaceBoundsJobHandle = default;

                treeUpdate.compactTreeJobHandle = default;

                treeUpdate.dataStream1JobHandle = default;
                treeUpdate.dataStream2JobHandle = default;

                treeUpdate.intersectingBrushesStreamJobHandle = default;

                treeUpdate.loopVerticesLookupJobHandle = default;

                treeUpdate.meshQueriesJobHandle = default;

                treeUpdate.nodeIndexToNodeOrderArrayJobHandle = default;

                treeUpdate.outputSurfacesJobHandle = default;
                treeUpdate.outputSurfacesRangeJobHandle = default;
                treeUpdate.outputJobHandle = default;

                treeUpdate.routingTableCacheJobHandle = default;
                treeUpdate.routingTableLookupJobHandle = default;
                treeUpdate.rebuildTreeBrushIndexOrdersJobHandle = default;

                treeUpdate.sectionsJobHandle = default;
                treeUpdate.subMeshSurfacesJobHandle = default;
                treeUpdate.subMeshCountsJobHandle = default;
                treeUpdate.subMeshSectionsJobHandle = default;

                treeUpdate.treeSpaceVerticesCacheJobHandle = default;
                treeUpdate.treeSpaceVerticesArrayJobHandle = default;
                treeUpdate.transformationCacheJobHandle = default;

                treeUpdate.uniqueBrushPairsJobHandle = default;
            }

            // TODO: ensure we only update exactly what we need, and nothing more

            try
            {
                #region CSG Jobs
                Profiler.BeginSample("CSG_Jobs");

                Profiler.BeginSample("Job_InvalidateBrushCache");
                try
                {
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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var invalidateBrushCacheJob = new InvalidateBrushCacheJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders.AsArray().AsReadOnly(),

                            // Read Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            routingTableCache           = chiselLookupValues.routingTableCache.AsDeferredJobArray(),
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray()
                        };
                        treeUpdate.invalidateBrushCacheJobHandle = invalidateBrushCacheJob.Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                        //treeUpdate.invalidateBrushCacheJobHandle.Complete();
                        
                        treeUpdate.rebuildTreeBrushIndexOrdersJobHandle = CombineDependencies(treeUpdate.invalidateBrushCacheJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        treeUpdate.basePolygonCacheJobHandle            = CombineDependencies(treeUpdate.invalidateBrushCacheJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle      = CombineDependencies(treeUpdate.invalidateBrushCacheJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.invalidateBrushCacheJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.routingTableCacheJobHandle           = CombineDependencies(treeUpdate.invalidateBrushCacheJobHandle, treeUpdate.routingTableCacheJobHandle);
                        treeUpdate.brushTreeSpacePlaneCacheJobHandle    = CombineDependencies(treeUpdate.invalidateBrushCacheJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        treeUpdate.brushRenderBufferCacheJobHandle      = CombineDependencies(treeUpdate.invalidateBrushCacheJobHandle, treeUpdate.brushRenderBufferCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies                = CombineDependencies(treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                                              treeUpdate.transformationCacheJobHandle,
                                                                              treeUpdate.brushMeshLookupJobHandle,
                                                                              treeUpdate.brushTreeSpaceBoundsJobHandle,                                                                              
                                                                              treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                                              treeUpdate.invalidateBrushCacheJobHandle);
                        var chiselLookupValues          = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
                            transformationCache         = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup             = treeUpdate.brushMeshLookup.AsReadOnly(),

                            // Write
                            brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                        };
                        treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                        //treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle.Complete();

                        treeUpdate.rebuildTreeBrushIndexOrdersJobHandle = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        treeUpdate.transformationCacheJobHandle         = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle, treeUpdate.transformationCacheJobHandle);
                        treeUpdate.brushMeshLookupJobHandle             = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.brushTreeSpaceBoundsJobHandle        = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle, treeUpdate.brushTreeSpaceBoundsJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle      = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // TODO: only change when brush or any touching brush has been added/removed or changes operation/order
                Profiler.BeginSample("Job_FindAllBrushIntersectionPairs");
                try
                {
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.brushTreeSpaceBoundsJobHandle,
                                                                          treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushBrushIntersectionsJobHandle,
                                                                          treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                                          treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findAllIntersectionsJob = new FindAllBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            transformationCache             = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup.AsReadOnly(),
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            rebuildTreeBrushIndexOrders     = treeUpdate.rebuildTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            
                            // Read / Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections,
                            
                            // Write
                            brushesThatNeedIndirectUpdateHashMap = treeUpdate.brushesThatNeedIndirectUpdateHashMap.AsParallelWriter()
                        };
                        treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                        //treeUpdate.findAllIntersectionsJobHandle.Complete();

                        treeUpdate.allTreeBrushIndexOrdersJobHandle                 = CombineDependencies(treeUpdate.findAllIntersectionsJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        treeUpdate.transformationCacheJobHandle                     = CombineDependencies(treeUpdate.findAllIntersectionsJobHandle, treeUpdate.transformationCacheJobHandle);
                        treeUpdate.brushMeshLookupJobHandle                         = CombineDependencies(treeUpdate.findAllIntersectionsJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.brushTreeSpaceBoundsJobHandle                    = CombineDependencies(treeUpdate.findAllIntersectionsJobHandle, treeUpdate.brushTreeSpaceBoundsJobHandle);
                        treeUpdate.rebuildTreeBrushIndexOrdersJobHandle             = CombineDependencies(treeUpdate.findAllIntersectionsJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        treeUpdate.brushBrushIntersectionsJobHandle                 = CombineDependencies(treeUpdate.findAllIntersectionsJobHandle, treeUpdate.brushBrushIntersectionsJobHandle);
                        treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle    = CombineDependencies(treeUpdate.findAllIntersectionsJobHandle, treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_FindUniqueIndirectBrushIntersections");
                try
                {
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies                = CombineDependencies(treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                                              treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                                              treeUpdate.findAllIntersectionsJobHandle);
                        var createUniqueIndicesArrayJob = new FindUniqueIndirectBrushIntersectionsJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdateHashMap     = treeUpdate.brushesThatNeedIndirectUpdateHashMap,
                        
                            // Write
                            brushesThatNeedIndirectUpdate            = treeUpdate.brushesThatNeedIndirectUpdate
                        };
                        treeUpdate.createUniqueIndicesArrayJobHandle = createUniqueIndicesArrayJob.Schedule(dependencies);
                        //treeUpdate.createUniqueIndicesArrayJobHandle.Complete();

                        treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle    = CombineDependencies(treeUpdate.createUniqueIndicesArrayJobHandle, treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle);
                        treeUpdate.brushesThatNeedIndirectUpdateJobHandle           = CombineDependencies(treeUpdate.createUniqueIndicesArrayJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_InvalidateBrushCache_Indirect");
                try
                {
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
                                                                          treeUpdate.brushRenderBufferCacheJobHandle,

                                                                          treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.createUniqueIndicesArrayJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var invalidateBrushCacheJob = new InvalidateIndirectBrushCacheJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsDeferredJobArray(),

                            // Read Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            routingTableCache               = chiselLookupValues.routingTableCache.AsDeferredJobArray(),
                            brushTreeSpacePlaneCache        = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            brushRenderBufferCache          = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray()
                        };
                        treeUpdate.invalidateIndirectBrushCacheJobHandle = invalidateBrushCacheJob.Schedule(treeUpdate.brushesThatNeedIndirectUpdate, 16, dependencies);
                        //treeUpdate.invalidateIndirectBrushCacheJobHandle.Complete();

                        treeUpdate.brushesThatNeedIndirectUpdateJobHandle   = CombineDependencies(treeUpdate.invalidateIndirectBrushCacheJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        treeUpdate.basePolygonCacheJobHandle                = CombineDependencies(treeUpdate.invalidateIndirectBrushCacheJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle          = CombineDependencies(treeUpdate.invalidateIndirectBrushCacheJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle      = CombineDependencies(treeUpdate.invalidateIndirectBrushCacheJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.routingTableCacheJobHandle               = CombineDependencies(treeUpdate.invalidateIndirectBrushCacheJobHandle, treeUpdate.routingTableCacheJobHandle);
                        treeUpdate.brushTreeSpacePlaneCacheJobHandle        = CombineDependencies(treeUpdate.invalidateIndirectBrushCacheJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        treeUpdate.brushRenderBufferCacheJobHandle          = CombineDependencies(treeUpdate.invalidateIndirectBrushCacheJobHandle, treeUpdate.brushRenderBufferCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_FixupBrushCacheIndices");
                try
                {   
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                               treeUpdate.nodeIndexToNodeOrderArrayJobHandle,
                                                               treeUpdate.basePolygonCacheJobHandle,
                                                               treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                               treeUpdate.invalidateBrushCacheJobHandle,
                                                               treeUpdate.invalidateIndirectBrushCacheJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var fixupBrushCacheIndicesJob = new FixupBrushCacheIndicesJob
                        {
                            // Read
                            allTreeBrushIndexOrders     = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            nodeIndexToNodeOrderArray   = treeUpdate.nodeIndexToNodeOrderArray.AsArray().AsReadOnly(),
                            nodeIndexToNodeOrderOffset  = treeUpdate.nodeIndexToNodeOrderOffset,

                            // Read Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray()
                        };
                        treeUpdate.fixupBrushCacheIndicesJobJobHandle = fixupBrushCacheIndicesJob.Schedule(treeUpdate.allTreeBrushIndexOrders, 16, dependencies);
                        //treeUpdate.fixupBrushCacheIndicesJobJobHandle.Complete();

                        treeUpdate.allTreeBrushIndexOrdersJobHandle     = CombineDependencies(treeUpdate.fixupBrushCacheIndicesJobJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        treeUpdate.nodeIndexToNodeOrderArrayJobHandle   = CombineDependencies(treeUpdate.fixupBrushCacheIndicesJobJobHandle, treeUpdate.nodeIndexToNodeOrderArrayJobHandle);
                        treeUpdate.basePolygonCacheJobHandle            = CombineDependencies(treeUpdate.fixupBrushCacheIndicesJobJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.fixupBrushCacheIndicesJobJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
                
                Profiler.BeginSample("Job_CreateTreeSpaceVerticesAndBounds_Indirect");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.brushTreeSpaceBoundsJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle, 
                                                                          treeUpdate.fixupBrushCacheIndicesJobJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders     = treeUpdate.brushesThatNeedIndirectUpdate.AsDeferredJobArray(),
                            transformationCache             = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup.AsReadOnly(),

                            // Write
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            treeSpaceVerticesCache          = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                        };
                        treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(treeUpdate.brushesThatNeedIndirectUpdate, 16, dependencies);
                        //treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle.Complete();

                        treeUpdate.brushesThatNeedIndirectUpdateJobHandle   = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        treeUpdate.transformationCacheJobHandle             = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle, treeUpdate.transformationCacheJobHandle);
                        treeUpdate.brushMeshLookupJobHandle                 = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.brushTreeSpaceBoundsJobHandle            = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle, treeUpdate.brushTreeSpaceBoundsJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle          = CombineDependencies(treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
                
                Profiler.BeginSample("Job_UpdateUpdateBrushIndexOrders");
                try
                {
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                               treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                               treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                               treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                               treeUpdate.findAllIntersectionsJobHandle,
                                                               treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle,
                                                               treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findAllIntersectionsJob = new UpdateUpdateBrushIndexOrdersJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsDeferredJobArray(),
                            rebuildTreeBrushIndexOrders     = treeUpdate.rebuildTreeBrushIndexOrders.AsArray().AsReadOnly(),

                            // Write
                            allUpdateBrushIndexOrders       = treeUpdate.allUpdateBrushIndexOrders.AsParallelWriter(),
                        };
                        treeUpdate.findAllIndirectIntersectionsJobHandle = findAllIntersectionsJob.Schedule(dependencies);
                        //treeUpdate.findAllIndirectIntersectionsJobHandle.Complete();

                        treeUpdate.allTreeBrushIndexOrdersJobHandle         = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        treeUpdate.brushesThatNeedIndirectUpdateJobHandle   = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        treeUpdate.rebuildTreeBrushIndexOrdersJobHandle     = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        treeUpdate.allUpdateBrushIndexOrdersJobHandle       = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
                
                Profiler.BeginSample("Job_FindAllBrushIntersectionPairs_Indirect");
                try
                {
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                               treeUpdate.transformationCacheJobHandle,
                                                               treeUpdate.brushMeshLookupJobHandle,
                                                               treeUpdate.brushTreeSpaceBoundsJobHandle,
                                                               treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                               treeUpdate.brushBrushIntersectionsJobHandle,
                                                               treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                               treeUpdate.findAllIntersectionsJobHandle, 
                                                               treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle,
                                                               treeUpdate.generateTreeSpaceVerticesAndBoundsIndirectJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findAllIntersectionsJob = new FindAllIndirectBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            transformationCache             = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup.AsReadOnly(),
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsDeferredJobArray(),
                            
                            // Read / Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections,

                            // Write
                            allUpdateBrushIndexOrders       = treeUpdate.allUpdateBrushIndexOrders.AsParallelWriter(),
                        };
                        treeUpdate.findAllIndirectIntersectionsJobHandle = findAllIntersectionsJob.Schedule(treeUpdate.brushesThatNeedIndirectUpdate, 1, dependencies);
                        //treeUpdate.findAllIndirectIntersectionsJobHandle.Complete();

                        treeUpdate.allTreeBrushIndexOrdersJobHandle         = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        treeUpdate.transformationCacheJobHandle             = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.transformationCacheJobHandle);
                        treeUpdate.brushMeshLookupJobHandle                 = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.brushTreeSpaceBoundsJobHandle            = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.brushTreeSpaceBoundsJobHandle);
                        treeUpdate.brushesThatNeedIndirectUpdateJobHandle   = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        treeUpdate.rebuildTreeBrushIndexOrdersJobHandle     = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.rebuildTreeBrushIndexOrdersJobHandle);
                        treeUpdate.brushBrushIntersectionsJobHandle         = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.brushBrushIntersectionsJobHandle);
                        treeUpdate.allUpdateBrushIndexOrdersJobHandle       = CombineDependencies(treeUpdate.findAllIndirectIntersectionsJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GatherBrushIntersectionPairs");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies                = CombineDependencies(treeUpdate.brushBrushIntersectionsJobHandle,
                                                                              treeUpdate.brushIntersectionsWithJobHandle,
                                                                              treeUpdate.brushBrushIntersectionRangeJobHandle,
                                                                              treeUpdate.findAllIntersectionsJobHandle, 
                                                                              treeUpdate.findAllIndirectIntersectionsJobHandle);
                        var gatherBrushIntersectionsJob = new GatherBrushIntersectionPairsJob
                        {
                            // Read
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections,

                            // Write
                            brushIntersectionsWith          = treeUpdate.brushIntersectionsWith.GetUnsafeList(),
                            brushIntersectionsWithRange     = treeUpdate.brushIntersectionsWithRange
                        };
                        treeUpdate.gatherBrushIntersectionsJobHandle    = gatherBrushIntersectionsJob.Schedule(dependencies);
                        //treeUpdate.gatherBrushIntersectionsJobHandle.Complete();

                        treeUpdate.brushBrushIntersectionsJobHandle     = CombineDependencies(treeUpdate.gatherBrushIntersectionsJobHandle, treeUpdate.brushBrushIntersectionsJobHandle);
                        treeUpdate.brushIntersectionsWithJobHandle      = CombineDependencies(treeUpdate.gatherBrushIntersectionsJobHandle, treeUpdate.brushIntersectionsWithJobHandle);
                        treeUpdate.brushBrushIntersectionRangeJobHandle = CombineDependencies(treeUpdate.gatherBrushIntersectionsJobHandle, treeUpdate.brushBrushIntersectionRangeJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_StoreBrushIntersections");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                               treeUpdate.compactTreeJobHandle,
                                                               treeUpdate.brushBrushIntersectionRangeJobHandle,
                                                               treeUpdate.brushIntersectionsWithJobHandle,
                                                               treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                               
                                                               treeUpdate.findAllIntersectionsJobHandle,
                                                               treeUpdate.findAllIndirectIntersectionsJobHandle,  
                                                               treeUpdate.gatherBrushIntersectionsJobHandle);
                        
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                        {
                            // Read
                            treeNodeIndex               = treeUpdate.treeNodeIndex,
                            compactTree                 = treeUpdate.compactTree,
                            allTreeBrushIndexOrders     = treeUpdate.allTreeBrushIndexOrders.AsDeferredJobArray(),
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),

                            brushIntersectionsWith      = treeUpdate.brushIntersectionsWith.AsDeferredJobArray(),
                            brushIntersectionsWithRange = treeUpdate.brushIntersectionsWithRange.AsReadOnly(),

                            // Write
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray()
                        };
                        treeUpdate.findIntersectingBrushesJobHandle = storeBrushIntersectionsJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //treeUpdate.findIntersectingBrushesJobHandle.Complete();

                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.findIntersectingBrushesJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.compactTreeJobHandle                 = CombineDependencies(treeUpdate.findIntersectingBrushesJobHandle, treeUpdate.compactTreeJobHandle);
                        treeUpdate.brushBrushIntersectionRangeJobHandle = CombineDependencies(treeUpdate.findIntersectingBrushesJobHandle, treeUpdate.brushBrushIntersectionRangeJobHandle);
                        treeUpdate.brushIntersectionsWithJobHandle    = CombineDependencies(treeUpdate.findIntersectingBrushesJobHandle, treeUpdate.brushIntersectionsWithJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.findIntersectingBrushesJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                // TODO: should only do this at creation time + when moved / store with brush component itself
                Profiler.BeginSample("Job_UpdateBrushTreeSpacePlanes");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                  treeUpdate.brushMeshLookupJobHandle,
                                                                  treeUpdate.transformationCacheJobHandle,
                                                                  treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                  
                                                                  treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                                  treeUpdate.findIntersectingBrushesJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushMeshLookup             = treeUpdate.brushMeshLookup.AsReadOnly(),
                            transformationCache         = chiselLookupValues.transformationCache.AsDeferredJobArray(),

                            // Write
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray()
                        };
                        treeUpdate.updateBrushTreeSpacePlanesJobHandle = createBrushTreeSpacePlanesJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //treeUpdate.updateBrushTreeSpacePlanesJobHandle.Complete();

                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.updateBrushTreeSpacePlanesJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.brushMeshLookupJobHandle             = CombineDependencies(treeUpdate.updateBrushTreeSpacePlanesJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.transformationCacheJobHandle         = CombineDependencies(treeUpdate.updateBrushTreeSpacePlanesJobHandle, treeUpdate.transformationCacheJobHandle);
                        treeUpdate.brushTreeSpacePlaneCacheJobHandle    = CombineDependencies(treeUpdate.updateBrushTreeSpacePlanesJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
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
                        var dependencies    = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                  treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                  treeUpdate.compactTreeJobHandle,
                                                                  treeUpdate.routingTableLookupJobHandle,
                                                                  
                                                                  treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                                  treeUpdate.findIntersectingBrushesJobHandle);
                        // Build categorization trees for brushes
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createRoutingTableJob = new CreateRoutingTableJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            compactTree                 = treeUpdate.compactTree,

                            // Write
                            routingTableLookup          = chiselLookupValues.routingTableCache.AsDeferredJobArray()
                        };
                        treeUpdate.updateBrushCategorizationTablesJobHandle = createRoutingTableJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);
                        //treeUpdate.updateBrushCategorizationTablesJobHandle.Complete();

                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.updateBrushCategorizationTablesJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.updateBrushCategorizationTablesJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.compactTreeJobHandle                 = CombineDependencies(treeUpdate.updateBrushCategorizationTablesJobHandle, treeUpdate.compactTreeJobHandle);
                        treeUpdate.routingTableLookupJobHandle          = CombineDependencies(treeUpdate.updateBrushCategorizationTablesJobHandle, treeUpdate.routingTableLookupJobHandle);
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
                        var dependencies = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                               treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                               treeUpdate.uniqueBrushPairsJobHandle,
                                                               
                                                               treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                               treeUpdate.findIntersectingBrushesJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findBrushPairsJob = new FindBrushPairsJob
                        {
                            // Read
                            maxOrder                    = treeUpdate.brushCount,
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                                    
                            // Read (Re-allocate) / Write
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs.GetUnsafeList()
                        };
                        treeUpdate.findBrushPairsJobHandle = findBrushPairsJob.Schedule(dependencies);
                        //treeUpdate.findBrushPairsJobHandle.Complete();

                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.findBrushPairsJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.findBrushPairsJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.uniqueBrushPairsJobHandle            = CombineDependencies(treeUpdate.findBrushPairsJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_AllocateStreams");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies = CombineDependencies(treeUpdate.dataStream1JobHandle,
                                                               treeUpdate.dataStream2JobHandle,
                                                               treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                               treeUpdate.findAllIndirectIntersectionsJobHandle);

                        treeUpdate.streamDependencyHandle = CombineDependencies(
                            NativeStream.ScheduleConstruct(out treeUpdate.dataStream1, treeUpdate.allUpdateBrushIndexOrders, dependencies, Allocator.TempJob),
                            NativeStream.ScheduleConstruct(out treeUpdate.dataStream2, treeUpdate.allUpdateBrushIndexOrders, dependencies, Allocator.TempJob));

                        treeUpdate.dataStream1JobHandle                 = CombineDependencies(treeUpdate.streamDependencyHandle, treeUpdate.dataStream1JobHandle);
                        treeUpdate.dataStream2JobHandle                 = CombineDependencies(treeUpdate.streamDependencyHandle, treeUpdate.dataStream2JobHandle);
                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.streamDependencyHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PrepareBrushPairStream");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies = CombineDependencies(treeUpdate.intersectingBrushesStreamJobHandle,
                                                               treeUpdate.uniqueBrushPairsJobHandle, 
                                                               treeUpdate.findBrushPairsJobHandle);

                        var streamAllocJobHandle = NativeStream.ScheduleConstruct(out treeUpdate.intersectingBrushesStream, treeUpdate.uniqueBrushPairs, dependencies, Allocator.TempJob);
                        
                        treeUpdate.uniqueBrushPairsJobHandle            = CombineDependencies(streamAllocJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                        treeUpdate.intersectingBrushesStreamJobHandle   = CombineDependencies(streamAllocJobHandle, treeUpdate.intersectingBrushesStreamJobHandle);
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


                        var dependencies            = CombineDependencies(treeUpdate.uniqueBrushPairsJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.intersectingBrushesStreamJobHandle, 
                                                                          treeUpdate.findBrushPairsJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs.AsDeferredJobArray(),
                            transformationCache         = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup             = treeUpdate.brushMeshLookup.AsReadOnly(),

                            // Write
                            intersectingBrushesStream   = treeUpdate.intersectingBrushesStream.AsWriter()
                        };
                        treeUpdate.prepareBrushPairIntersectionsJobHandle = prepareBrushPairIntersectionsJob.Schedule(treeUpdate.uniqueBrushPairs, 8, dependencies);
                        //treeUpdate.prepareBrushPairIntersectionsJobHandle.Complete();

                        treeUpdate.uniqueBrushPairsJobHandle            = CombineDependencies(treeUpdate.prepareBrushPairIntersectionsJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                        treeUpdate.transformationCacheJobHandle         = CombineDependencies(treeUpdate.prepareBrushPairIntersectionsJobHandle, treeUpdate.transformationCacheJobHandle);
                        treeUpdate.brushMeshLookupJobHandle             = CombineDependencies(treeUpdate.prepareBrushPairIntersectionsJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.intersectingBrushesStreamJobHandle   = CombineDependencies(treeUpdate.prepareBrushPairIntersectionsJobHandle, treeUpdate.intersectingBrushesStreamJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_MergeTouchingBrushVertices");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                               treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                               treeUpdate.treeSpaceVerticesCacheJobHandle, 
                                                               treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                               treeUpdate.findIntersectingBrushesJobHandle);

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

                        //*
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        // Merges original brush vertices together when they are close to avoid t-junctions
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                        {
                            // Read
                            treeBrushIndexOrders        = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),

                            // Read Write
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                        };
                        treeUpdate.mergeTouchingBrushVerticesJobHandle = mergeTouchingBrushVerticesJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //treeUpdate.mergeTouchingBrushVerticesJobHandle.Complete();

                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.mergeTouchingBrushVerticesJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.mergeTouchingBrushVerticesJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle      = CombineDependencies(treeUpdate.mergeTouchingBrushVerticesJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                        /*/
                        treeUpdate.mergeTouchingBrushVerticesJobHandle = dependencies;
                        //*/
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
                        var dependencies    = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                  treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                  treeUpdate.brushMeshLookupJobHandle,
                                                                  treeUpdate.treeSpaceVerticesCacheJobHandle, 
                                                                  treeUpdate.basePolygonCacheJobHandle,
                                                                  treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                                  treeUpdate.mergeTouchingBrushVerticesJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobs
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            brushMeshLookup             = treeUpdate.brushMeshLookup.AsReadOnly(),
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),

                            // Write
                            basePolygons                = chiselLookupValues.basePolygonCache.AsDeferredJobArray()
                        };
                        treeUpdate.generateBasePolygonLoopsJobHandle = createBlobPolygonsBlobs.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //treeUpdate.generateBasePolygonLoopsJobHandle.Complete();

                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.generateBasePolygonLoopsJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.generateBasePolygonLoopsJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.brushMeshLookupJobHandle             = CombineDependencies(treeUpdate.generateBasePolygonLoopsJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.treeSpaceVerticesCacheJobHandle      = CombineDependencies(treeUpdate.generateBasePolygonLoopsJobHandle, treeUpdate.treeSpaceVerticesCacheJobHandle);
                        treeUpdate.basePolygonCacheJobHandle            = CombineDependencies(treeUpdate.generateBasePolygonLoopsJobHandle, treeUpdate.basePolygonCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_CreateIntersectionLoops");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = CombineDependencies(treeUpdate.brushTreeSpacePlanesJobHandle,
                                                                  treeUpdate.treeSpaceVerticesArrayJobHandle,
                                                                  treeUpdate.intersectingBrushesStreamJobHandle,
                                                                  treeUpdate.outputSurfacesJobHandle,
                                                                  treeUpdate.uniqueBrushPairsJobHandle,
                                                                  treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                  treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                                  treeUpdate.findBrushPairsJobHandle,
                                                                  treeUpdate.mergeTouchingBrushVerticesJobHandle,
                                                                  treeUpdate.updateBrushTreeSpacePlanesJobHandle,
                                                                  treeUpdate.prepareBrushPairIntersectionsJobHandle);

                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Needed for count (forced & unused)
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs,

                            // Read
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                            intersectingBrushesStream   = treeUpdate.intersectingBrushesStream.AsReader(),
                            
                            // Write
                            outputSurfaces              = treeUpdate.outputSurfaces.AsParallelWriter()
                        };
                        treeUpdate.findAllIntersectionLoopsJobHandle = findAllIntersectionLoopsJob.Schedule(treeUpdate.uniqueBrushPairs, 8, dependencies);
                        //treeUpdate.findAllIntersectionLoopsJobHandle.Complete();
                        treeUpdate.findAllIntersectionLoopsJobHandle = treeUpdate.intersectingBrushesStream.Dispose(treeUpdate.findAllIntersectionLoopsJobHandle);
                        //treeUpdate.findAllIntersectionLoopsJobHandle.Complete();
                        
                        treeUpdate.brushTreeSpacePlanesJobHandle        = CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.brushTreeSpacePlanesJobHandle);
                        treeUpdate.treeSpaceVerticesArrayJobHandle      = CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.treeSpaceVerticesArrayJobHandle);
                        treeUpdate.intersectingBrushesStreamJobHandle   = CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.intersectingBrushesStreamJobHandle);
                        treeUpdate.outputSurfacesJobHandle              = CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.outputSurfacesJobHandle);
                        treeUpdate.uniqueBrushPairsJobHandle            = CombineDependencies(treeUpdate.findAllIntersectionLoopsJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                    }
                } finally { Profiler.EndSample(); }
            
                Profiler.BeginSample("Job_GatherOutputSurfaces");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = CombineDependencies(treeUpdate.outputSurfacesJobHandle,
                                                                  treeUpdate.outputSurfacesRangeJobHandle,
                                                                  treeUpdate.findAllIntersectionLoopsJobHandle);
                        var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                        {
                            // Read / Write (Sort)
                            outputSurfaces          = treeUpdate.outputSurfaces.AsDeferredJobArray(),

                            // Write
                            outputSurfacesRange     = treeUpdate.outputSurfacesRange
                        };
                        treeUpdate.gatherOutputSurfacesJobHandle = gatherOutputSurfacesJob.Schedule(dependencies);
                        //treeUpdate.gatherOutputSurfacesJobHandle.Complete();
                        
                        treeUpdate.outputSurfacesJobHandle      = CombineDependencies(treeUpdate.gatherOutputSurfacesJobHandle, treeUpdate.outputSurfacesJobHandle);
                        treeUpdate.outputSurfacesRangeJobHandle = CombineDependencies(treeUpdate.gatherOutputSurfacesJobHandle, treeUpdate.outputSurfacesRangeJobHandle);
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
                        var dependencies = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                               treeUpdate.outputSurfacesJobHandle,
                                                               treeUpdate.outputSurfacesRangeJobHandle,
                                                               treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                               treeUpdate.basePolygonCacheJobHandle,
                                                               treeUpdate.loopVerticesLookupJobHandle,
                                                               treeUpdate.outputJobHandle,
                                                               treeUpdate.prepareBrushPairIntersectionsJobHandle,
                                                               treeUpdate.findAllIntersectionLoopsJobHandle,
                                                               treeUpdate.gatherOutputSurfacesJobHandle,
                                                               treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                               treeUpdate.streamDependencyHandle,
                                                               treeUpdate.generateBasePolygonLoopsJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            outputSurfaces              = treeUpdate.outputSurfaces.AsDeferredJobArray(),
                            outputSurfacesRange         = treeUpdate.outputSurfacesRange.AsReadOnly(),
                            maxNodeOrder                = treeUpdate.maxNodeOrder,
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            basePolygonCache            = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),

                            // Read Write
                            loopVerticesLookup          = treeUpdate.loopVerticesLookup,
                            
                            // Write
                            output                      = treeUpdate.dataStream1.AsWriter()
                        };
                        treeUpdate.allFindLoopOverlapIntersectionsJobHandle = findLoopOverlapIntersectionsJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //treeUpdate.allFindLoopOverlapIntersectionsJobHandle.Complete();

                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.outputSurfacesJobHandle              = CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.outputSurfacesJobHandle);
                        treeUpdate.outputSurfacesRangeJobHandle         = CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.outputSurfacesRangeJobHandle);
                        treeUpdate.brushTreeSpacePlaneCacheJobHandle    = CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        treeUpdate.basePolygonCacheJobHandle            = CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.loopVerticesLookupJobHandle          = CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.loopVerticesLookupJobHandle);
                        treeUpdate.outputJobHandle                      = CombineDependencies(treeUpdate.allFindLoopOverlapIntersectionsJobHandle, treeUpdate.outputJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                // TODO: should only try to merge the vertices beyond the original mesh vertices (the intersection vertices)
                //       should also try to limit vertices to those that are on the same surfaces (somehow)
                Profiler.BeginSample("Job_MergeTouchingBrushVerticesIndirect");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                  treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                  treeUpdate.loopVerticesLookupJobHandle,
                                                                  treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                                  treeUpdate.allFindLoopOverlapIntersectionsJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesIndirectJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            
                            // Read Write
                            loopVerticesLookup          = treeUpdate.loopVerticesLookup,
                        };
                        treeUpdate.mergeTouchingBrushVertices2JobHandle = mergeTouchingBrushVerticesJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //treeUpdate.mergeTouchingBrushVertices2JobHandle.Complete();
                        
                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.mergeTouchingBrushVertices2JobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.mergeTouchingBrushVertices2JobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.loopVerticesLookupJobHandle          = CombineDependencies(treeUpdate.mergeTouchingBrushVertices2JobHandle, treeUpdate.loopVerticesLookupJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PerformCSG");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                  treeUpdate.routingTableLookupJobHandle,
                                                                  treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                  treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                  treeUpdate.dataStream1JobHandle,
                                                                  treeUpdate.dataStream2JobHandle,
                                                                  treeUpdate.loopVerticesLookupJobHandle,
                                                                  treeUpdate.mergeTouchingBrushVertices2JobHandle,
                                                                  treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                                  treeUpdate.updateBrushCategorizationTablesJobHandle,
                                                                  treeUpdate.streamDependencyHandle);

                        // Perform CSG
                        // TODO: determine when a brush is completely inside another brush
                        //		 (might not have any intersection loops)
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var performCSGJob = new PerformCSGJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            routingTableLookup          = chiselLookupValues.routingTableCache.AsDeferredJobArray(),
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            input                       = treeUpdate.dataStream1.AsReader(),
                            loopVerticesLookup          = treeUpdate.loopVerticesLookup,

                            // Write
                            output                      = treeUpdate.dataStream2.AsWriter(),
                        };
                        treeUpdate.allPerformAllCSGJobHandle = performCSGJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);
                        //treeUpdate.allPerformAllCSGJobHandle.Complete();
                        treeUpdate.allPerformAllCSGJobHandle    = treeUpdate.dataStream1.Dispose(treeUpdate.allPerformAllCSGJobHandle);
                        //treeUpdate.allPerformAllCSGJobHandle.Complete();
                        treeUpdate.dataStream1                  = default;

                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.routingTableLookupJobHandle          = CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.routingTableLookupJobHandle);
                        treeUpdate.brushTreeSpacePlaneCacheJobHandle    = CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.brushTreeSpacePlaneCacheJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.dataStream1JobHandle                 = CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.dataStream1JobHandle);
                        treeUpdate.loopVerticesLookupJobHandle          = CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.loopVerticesLookupJobHandle);
                        treeUpdate.dataStream2JobHandle                 = CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.dataStream2JobHandle);
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GenerateSurfaceTriangles");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                  treeUpdate.basePolygonCacheJobHandle,
                                                                  treeUpdate.transformationCacheJobHandle,
                                                                  treeUpdate.dataStream2JobHandle,
                                                                  treeUpdate.brushRenderBufferCacheJobHandle,

                                                                  treeUpdate.allPerformAllCSGJobHandle,
                                                                  treeUpdate.findAllIndirectIntersectionsJobHandle,
                                                                  treeUpdate.fixupBrushCacheIndicesJobJobHandle, 
                                                                  treeUpdate.generateBasePolygonLoopsJobHandle);

                        // TODO: Potentially merge this with PerformCSGJob?
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            basePolygons                = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),
                            transformationCache         = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            input                       = treeUpdate.dataStream2.AsReader(),

                            // Write
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray()
                        };
                        treeUpdate.allGenerateSurfaceTrianglesJobHandle = generateSurfaceRenderBuffers.Schedule(treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);
                        //treeUpdate.allGenerateSurfaceTrianglesJobHandle.Complete();
                        treeUpdate.allGenerateSurfaceTrianglesJobHandle = treeUpdate.dataStream2.Dispose(treeUpdate.allGenerateSurfaceTrianglesJobHandle);
                        //treeUpdate.allGenerateSurfaceTrianglesJobHandle.Complete();
                        treeUpdate.dataStream2 = default;
                        
                        treeUpdate.allUpdateBrushIndexOrdersJobHandle   = CombineDependencies(treeUpdate.allGenerateSurfaceTrianglesJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.basePolygonCacheJobHandle            = CombineDependencies(treeUpdate.allGenerateSurfaceTrianglesJobHandle, treeUpdate.basePolygonCacheJobHandle);
                        treeUpdate.transformationCacheJobHandle         = CombineDependencies(treeUpdate.allGenerateSurfaceTrianglesJobHandle, treeUpdate.transformationCacheJobHandle);
                        treeUpdate.dataStream2JobHandle                 = CombineDependencies(treeUpdate.allGenerateSurfaceTrianglesJobHandle, treeUpdate.dataStream2JobHandle);
                        treeUpdate.brushRenderBufferCacheJobHandle      = CombineDependencies(treeUpdate.allGenerateSurfaceTrianglesJobHandle, treeUpdate.brushRenderBufferCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("JOB_FindBrushRenderBuffers");
                try
                { 
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies        = CombineDependencies(treeUpdate.meshQueriesJobHandle,
                                                                      treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                      treeUpdate.brushRenderBufferCacheJobHandle,
                                                                      treeUpdate.brushRenderDataJobHandle,
                                                                      treeUpdate.subMeshSurfacesJobHandle,
                                                                      treeUpdate.subMeshCountsJobHandle,
                                                                      treeUpdate.subMeshSectionsJobHandle,
                                                                      treeUpdate.allGenerateSurfaceTrianglesJobHandle);
                        var chiselLookupValues  = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        
                        dependencies = ChiselNativeListExtensions.ScheduleEnsureCapacity(treeUpdate.brushRenderData, treeUpdate.allTreeBrushIndexOrders, dependencies);
                        var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                        {
                            // Read
                            meshQueryLength         = treeUpdate.meshQueries.Length,
                            allTreeBrushIndexOrders = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            brushRenderBufferCache  = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray(),

                            // Write
                            brushRenderData         = treeUpdate.brushRenderData,
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            subMeshCounts           = treeUpdate.subMeshCounts,
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections,
                        };
                        treeUpdate.findBrushRenderBuffersJobHandle = findBrushRenderBuffersJob.Schedule(dependencies);
                        //treeUpdate.findBrushRenderBuffersJobHandle.Complete();
                        
                        treeUpdate.meshQueriesJobHandle             = CombineDependencies(treeUpdate.findBrushRenderBuffersJobHandle, treeUpdate.meshQueriesJobHandle);
                        treeUpdate.allTreeBrushIndexOrdersJobHandle = CombineDependencies(treeUpdate.findBrushRenderBuffersJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        treeUpdate.brushRenderBufferCacheJobHandle  = CombineDependencies(treeUpdate.findBrushRenderBuffersJobHandle, treeUpdate.brushRenderBufferCacheJobHandle);
                        treeUpdate.brushRenderDataJobHandle         = CombineDependencies(treeUpdate.findBrushRenderBuffersJobHandle, treeUpdate.brushRenderDataJobHandle);
                        treeUpdate.subMeshSurfacesJobHandle         = CombineDependencies(treeUpdate.findBrushRenderBuffersJobHandle, treeUpdate.subMeshSurfacesJobHandle);
                        treeUpdate.subMeshCountsJobHandle           = CombineDependencies(treeUpdate.findBrushRenderBuffersJobHandle, treeUpdate.subMeshCountsJobHandle);
                        treeUpdate.subMeshSectionsJobHandle         = CombineDependencies(treeUpdate.findBrushRenderBuffersJobHandle, treeUpdate.subMeshSectionsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("JOB_PrepareSubSections");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies = CombineDependencies(treeUpdate.meshQueriesJobHandle,
                                                               treeUpdate.brushRenderDataJobHandle,
                                                               treeUpdate.sectionsJobHandle,
                                                               treeUpdate.subMeshSurfacesJobHandle,
                                                               treeUpdate.findBrushRenderBuffersJobHandle);

                        var prepareJob = new PrepareSubSectionsJob
                        {
                            // Read
                            meshQueries         = treeUpdate.meshQueries.AsReadOnly(),
                            brushRenderData     = treeUpdate.brushRenderData.AsDeferredJobArray(),

                            // Write
                            sections            = treeUpdate.sections,
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces,
                        };
                        treeUpdate.prepareJobHandle = prepareJob.Schedule(dependencies);
                        //treeUpdate.prepareJobHandle.Complete();

                        treeUpdate.meshQueriesJobHandle         = CombineDependencies(treeUpdate.prepareJobHandle, treeUpdate.meshQueriesJobHandle);
                        treeUpdate.brushRenderDataJobHandle     = CombineDependencies(treeUpdate.prepareJobHandle, treeUpdate.brushRenderDataJobHandle);
                        treeUpdate.sectionsJobHandle            = CombineDependencies(treeUpdate.prepareJobHandle, treeUpdate.sectionsJobHandle);
                        treeUpdate.subMeshSurfacesJobHandle     = CombineDependencies(treeUpdate.prepareJobHandle, treeUpdate.subMeshSurfacesJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("JOB_SortSurfaces");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies = CombineDependencies(treeUpdate.subMeshSectionsJobHandle,
                                                               treeUpdate.sectionsJobHandle,
                                                               treeUpdate.prepareJobHandle);

                        var sortJob = new SortSurfacesJob 
                        {
                            // Read
                            sections            = treeUpdate.sections.AsDeferredJobArray(),

                            // Read Write
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces.AsDeferredJobArray(),
                            subMeshCounts       = treeUpdate.subMeshCounts,

                            // Write
                            subMeshSections     = treeUpdate.vertexBufferContents.subMeshSections,
                        };
                        treeUpdate.sortJobHandle = sortJob.Schedule(dependencies);
                        //treeUpdate.sortJobHandle.Complete();

                        treeUpdate.subMeshSectionsJobHandle     = CombineDependencies(treeUpdate.sortJobHandle, treeUpdate.subMeshSectionsJobHandle);
                        treeUpdate.sectionsJobHandle            = CombineDependencies(treeUpdate.sortJobHandle, treeUpdate.sectionsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("JOB_AllocateVertexBuffers");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies    = treeUpdate.sortJobHandle;

                        var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                        {
                            // Read
                            subMeshSections     = treeUpdate.vertexBufferContents.subMeshSections.AsDeferredJobArray(),

                            // Read Write
                            subMeshesArray      = treeUpdate.vertexBufferContents.subMeshes,
                            indicesArray        = treeUpdate.vertexBufferContents.indices,
                            brushIndicesArray   = treeUpdate.vertexBufferContents.brushIndices,
                            positionsArray      = treeUpdate.vertexBufferContents.positions,
                            tangentsArray       = treeUpdate.vertexBufferContents.tangents,
                            normalsArray        = treeUpdate.vertexBufferContents.normals,
                            uv0Array            = treeUpdate.vertexBufferContents.uv0
                        };
                        treeUpdate.allocateVertexBufferJobHandle = allocateVertexBuffersJob.Schedule(dependencies);
                        //treeUpdate.allocateVertexBufferJobHandle.Complete();

                        treeUpdate.subMeshSectionsJobHandle = CombineDependencies(treeUpdate.allocateVertexBufferJobHandle, treeUpdate.subMeshSectionsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("JOB_GenerateMeshDescription");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies    = treeUpdate.sortJobHandle;

                        var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                        {
                            // Read
                            subMeshCounts       = treeUpdate.subMeshCounts.AsDeferredJobArray(),

                            // Read Write
                            meshDescriptions    = treeUpdate.vertexBufferContents.meshDescriptions
                        };
                        treeUpdate.generateMeshDescriptionJobHandle = generateMeshDescriptionJob.Schedule(dependencies);
                        //treeUpdate.generateMeshDescriptionJobHandle.Complete();
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("JOB_FillVertexBuffers");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies    = treeUpdate.allocateVertexBufferJobHandle;

                        var generateVertexBuffersJob = new FillVertexBuffersJob
                        {
                            // Read
                            subMeshSections     = treeUpdate.vertexBufferContents.subMeshSections.AsDeferredJobArray(),               
                            subMeshCounts       = treeUpdate.subMeshCounts.AsDeferredJobArray(),
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces.AsDeferredJobArray(),

                            // Read Write
                            subMeshesArray      = treeUpdate.vertexBufferContents.subMeshes,
                            tangentsArray       = treeUpdate.vertexBufferContents.tangents,
                            normalsArray        = treeUpdate.vertexBufferContents.normals,
                            uv0Array            = treeUpdate.vertexBufferContents.uv0,
                            positionsArray      = treeUpdate.vertexBufferContents.positions,
                            indicesArray        = treeUpdate.vertexBufferContents.indices,
                            brushIndicesArray   = treeUpdate.vertexBufferContents.brushIndices
                        };
                        treeUpdate.fillVertexBuffersJobHandle = generateVertexBuffersJob.Schedule(treeUpdate.vertexBufferContents.subMeshSections, 1, dependencies);
                        //treeUpdate.fillVertexBuffersJobHandle.Complete();
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.EndSample();
                #endregion

                #region Reset Flags
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
                Profiler.EndSample();
                #endregion

                #region Complete Jobs

                //JobHandle.ScheduleBatchedJobs();

                Profiler.BeginSample("CSG_JobComplete");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;

                    finalJobHandle = CombineDependencies(
                        
                                                treeUpdate.allTreeBrushIndexOrdersJobHandle ,
                                                treeUpdate.allUpdateBrushIndexOrdersJobHandle ,

                                                treeUpdate.basePolygonCacheJobHandle ,
                                                treeUpdate.brushesTouchedByBrushCacheJobHandle ,
                                                treeUpdate.brushTreeSpacePlaneCacheJobHandle ,
                                                treeUpdate.brushRenderBufferCacheJobHandle ,
                                                treeUpdate.brushRenderDataJobHandle ,
                                                treeUpdate.brushTreeSpacePlanesJobHandle ,
                                                treeUpdate.brushMeshLookupJobHandle ,
                                                treeUpdate.brushBrushIntersectionsJobHandle,
                                                treeUpdate.brushIntersectionsWithJobHandle,
                                                treeUpdate.brushBrushIntersectionRangeJobHandle ,
                                                treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle ,
                                                treeUpdate.brushesThatNeedIndirectUpdateJobHandle ,
                                                treeUpdate.brushTreeSpaceBoundsJobHandle ,

                                                treeUpdate.compactTreeJobHandle ,

                                                treeUpdate.dataStream1JobHandle ,
                                                treeUpdate.dataStream2JobHandle ,

                                                treeUpdate.intersectingBrushesStreamJobHandle ,

                                                treeUpdate.loopVerticesLookupJobHandle ,

                                                treeUpdate.meshQueriesJobHandle ,

                                                treeUpdate.nodeIndexToNodeOrderArrayJobHandle ,

                                                treeUpdate.outputSurfacesJobHandle ,
                                                treeUpdate.outputSurfacesRangeJobHandle ,
                                                treeUpdate.outputJobHandle ,

                                                treeUpdate.routingTableCacheJobHandle ,
                                                treeUpdate.routingTableLookupJobHandle ,
                                                treeUpdate.rebuildTreeBrushIndexOrdersJobHandle ,

                                                treeUpdate.sectionsJobHandle,
                                                treeUpdate.subMeshSurfacesJobHandle,
                                                treeUpdate.subMeshCountsJobHandle,
                                                treeUpdate.subMeshSectionsJobHandle,

                                                treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                treeUpdate.treeSpaceVerticesArrayJobHandle,
                                                treeUpdate.transformationCacheJobHandle,

                                                treeUpdate.uniqueBrushPairsJobHandle,
                                                treeUpdate.findBrushPairsJobHandle, 
                                                treeUpdate.mergeTouchingBrushVerticesJobHandle, 
                                                treeUpdate.updateBrushTreeSpacePlanesJobHandle,
                                                treeUpdate.generateMeshDescriptionJobHandle, 
                                                treeUpdate.fillVertexBuffersJobHandle, 
                                                treeUpdate.generateBasePolygonLoopsJobHandle,
                                                treeUpdate.invalidateIndirectBrushCacheJobHandle, 
                                                treeUpdate.fixupBrushCacheIndicesJobJobHandle,
                                                finalJobHandle
                                            );
                }
                finalJobHandle.Complete(); finalJobHandle = default;
                Profiler.EndSample();
                #endregion

                #region Dirty all invalidated outlines
                Profiler.BeginSample("CSG_DirtyModifiedOutlines");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;
                    {
                        for (int b = 0; b < treeUpdate.allUpdateBrushIndexOrders.Length; b++)
                        {
                            var brushIndexOrder = treeUpdate.allUpdateBrushIndexOrders[b];
                            int brushNodeIndex  = brushIndexOrder.nodeIndex;
                            var brushInfo       = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo;
                            if (brushInfo == null)
                                continue;
                            brushInfo.brushOutlineGeneration++;
                            brushInfo.brushOutlineDirty = true;
                        }
                    }
                }
                Profiler.EndSample();
                #endregion


                if (updateMeshEvent != null)
                {
                    Profiler.BeginSample("UpdateMeshEvents");
                    // TODO: clean this 
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];

                        var treeNodeIndex   = treeUpdate.treeNodeIndex;
                        var treeNodeID      = treeUpdate.treeNodeIndex + 1;
                        var tree            = new CSGTree { treeNodeID = treeNodeID };

                        var flags = CSGManager.nodeFlags[treeNodeIndex];
                        if (!flags.IsNodeFlagSet(NodeStatusFlags.TreeMeshNeedsUpdate))
                            continue;

                        bool wasDirty = tree.Dirty;

                        flags.UnSetNodeFlag(NodeStatusFlags.NeedCSGUpdate);
                        nodeFlags[treeNodeIndex] = flags;

                        if (treeUpdate.updateCount == 0 &&
                            treeUpdate.brushCount > 0)
                            continue;

                        // See if the tree has been modified
                        if (!wasDirty)
                            continue;

                        // Skip invalid brushes since they won't work anyway
                        if (!tree.Valid)
                            continue;

                        updateMeshEvent.Invoke(tree, ref treeUpdate.vertexBufferContents);
                    }
                    Profiler.EndSample();
                }


                #region Store cached values back into cache (by node Index)
                Profiler.BeginSample("CSG_StoreToCache");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate          = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;

                    var chiselLookupValues              = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                    ref var brushTreeSpaceBoundLookup   = ref chiselLookupValues.brushTreeSpaceBoundLookup;
                    ref var brushRenderBufferLookup     = ref chiselLookupValues.brushRenderBufferLookup;
                    brushTreeSpaceBoundLookup.Clear();
                    brushRenderBufferLookup.Clear();
                    for (int i = 0; i < treeUpdate.brushCount; i++)
                    {
                        var nodeIndex = treeUpdate.allTreeBrushIndexOrders[i].nodeIndex;
                        brushTreeSpaceBoundLookup[nodeIndex]     = chiselLookupValues.brushTreeSpaceBoundCache[i];
                        brushRenderBufferLookup[nodeIndex]       = chiselLookupValues.brushRenderBufferCache[i];
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

                        treeUpdate.Dispose(disposeJobHandle);//, onlyBlobs: false);
                    }
                }
                Profiler.EndSample();
                #endregion
            }

            #region Clear garbage
            Profiler.BeginSample("ClearGarbage");
            s_TransformTreeBrushIndicesList.Clear();
            Profiler.EndSample();
            #endregion

            return finalJobHandle;
        }

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

            return x.treeNodeIndex - y.treeNodeIndex;
        }
        static readonly Comparison<TreeUpdate> s_TreeSorter = TreeUpdateCompare;
        #endregion

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
        internal static bool UpdateAllTreeMeshes(UpdateMeshEvent updateMeshEvent, out JobHandle allTrees)
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

            UnityEngine.Profiling.Profiler.BeginSample("UpdateTreeMeshes");
            allTrees = UpdateTreeMeshes(updateMeshEvent, trees);
            UnityEngine.Profiling.Profiler.EndSample();
            return true;
        }

        internal static bool RebuildAll(UpdateMeshEvent updateMeshEvent)
        {
            if (!UpdateAllTreeMeshes(updateMeshEvent, out JobHandle handle))
                return false;
            handle.Complete();
            return true;
        }
        #endregion
    }
}
