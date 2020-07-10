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
        internal struct TreeUpdate
        {
            public int                      treeNodeIndex;
            public int                      brushCount;
            public int                      updateCount;
            public int                      maxNodeOrder;
            
            public NativeList<IndexOrder>   allTreeBrushIndexOrders;
            public NativeList<IndexOrder>   rebuildTreeBrushIndexOrders;
            
            public BlobAssetReference<CompactTree>  compactTree;
            public NativeArray<MeshQuery>           meshQueries;

            public NativeList<BrushPair>                                        brushBrushIntersections;
            public NativeArray<int2>                                            brushBrushIntersectionRange;
            public NativeList<IndexOrder>                                       brushesThatNeedIndirectUpdate;

            public NativeList<BrushPair>                                        uniqueBrushPairs;
            public NativeList<BlobAssetReference<BrushIntersectionLoop>>        outputSurfaces;
            public NativeArray<int2>                                            outputSurfacesRange;
            public NativeList<BlobAssetReference<BrushPairIntersection>>        intersectingBrushes;
            public NativeArray<BlobAssetReference<BrushMeshBlob>>               brushMeshLookup;

            public VertexBufferContents                                         vertexBufferContents;

            public NativeList<SectionData>                  sections;
            public NativeList<BrushData>                    brushRenderData;
            public NativeList<SubMeshCounts>                subMeshCounts;
            public NativeList<SubMeshSurface>               subMeshSurfaces;

            public NativeStream     dataStream1;
            public NativeStream     dataStream2;

            public JobHandle lastJobHandle;

            public void Clear()
            {
                Profiler.BeginSample("COMPLETE_PREVIOUS_DISPOSE");
                lastJobHandle.Complete();
                lastJobHandle = default;
                Profiler.EndSample();

                Profiler.BeginSample("LIST_CLEAR");
                brushBrushIntersections         .Clear();
                brushesThatNeedIndirectUpdate   .Clear();
                outputSurfaces                  .Clear();
                uniqueBrushPairs                .Clear();
                intersectingBrushes             .Clear();
                rebuildTreeBrushIndexOrders     .Clear();
                
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
                
                brushBrushIntersectionRange     .ClearValues();
                outputSurfacesRange             .ClearValues();

                allTreeBrushIndexOrders.Clear();
                allTreeBrushIndexOrders.Resize(brushCount, NativeArrayOptions.ClearMemory);
            }

            public void EnsureSize(int newBrushCount)
            {
                if (this.brushCount == newBrushCount)
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

                lastJobHandle.Complete();
                lastJobHandle = default;

                Profiler.BeginSample("NEW");
                this.brushCount                 = newBrushCount;
                var triangleArraySize           = GeometryMath.GetTriangleArraySize(newBrushCount);
                var intersectionCount           = math.max(1, triangleArraySize);
                brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                outputSurfaces                  = new NativeList<BlobAssetReference<BrushIntersectionLoop>>(newBrushCount * 16, Allocator.Persistent);
                brushBrushIntersections         = new NativeList<BrushPair>(intersectionCount * 2, Allocator.Persistent);
                uniqueBrushPairs                = new NativeList<BrushPair>(intersectionCount, Allocator.Persistent);
                intersectingBrushes             = new NativeList<BlobAssetReference<BrushPairIntersection>>(intersectionCount, Allocator.Persistent);                
                rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                brushRenderData                 = new NativeList<BrushData>(newBrushCount, Allocator.Persistent);
                allTreeBrushIndexOrders         = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                allTreeBrushIndexOrders.Resize(newBrushCount, NativeArrayOptions.ClearMemory);

                outputSurfacesRange             = new NativeArray<int2>(newBrushCount, Allocator.Persistent);
                brushBrushIntersectionRange     = new NativeArray<int2>(newBrushCount, Allocator.Persistent);
                brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(newBrushCount, Allocator.Persistent);

                
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
            
            public void Dispose(JobHandle disposeJobHandle, bool onlyBlobs = false)
            {
                lastJobHandle = disposeJobHandle;


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

                Profiler.BeginSample("DISPOSE intersectingBrushes");
                if (intersectingBrushes.IsCreated && intersectingBrushes.Length > 0)
                {
                    foreach (var item in intersectingBrushes)
                        if (item.IsCreated) item.Dispose();
                    intersectingBrushes.Clear();
                }
                Profiler.EndSample();

                if (meshQueries.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, meshQueries.Dispose(disposeJobHandle));
                meshQueries = default;

                if (onlyBlobs)
                    return;

                Profiler.BeginSample("DISPOSE ARRAY");
                if (brushMeshLookup              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushMeshLookup              .Dispose(disposeJobHandle));
                if (brushBrushIntersectionRange  .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushBrushIntersectionRange  .Dispose(disposeJobHandle));
                if (outputSurfacesRange          .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, outputSurfacesRange          .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE LIST");
                if (sections                     .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, sections                     .Dispose(disposeJobHandle));
                if (brushRenderData              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushRenderData              .Dispose(disposeJobHandle));
                if (subMeshCounts                .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshCounts                .Dispose(disposeJobHandle));
                if (subMeshSurfaces              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshSurfaces              .Dispose(disposeJobHandle));
                if (allTreeBrushIndexOrders      .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, allTreeBrushIndexOrders      .Dispose(disposeJobHandle));
                if (rebuildTreeBrushIndexOrders  .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, rebuildTreeBrushIndexOrders  .Dispose(disposeJobHandle));
                if (uniqueBrushPairs             .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, uniqueBrushPairs             .Dispose(disposeJobHandle));
                if (intersectingBrushes          .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, intersectingBrushes          .Dispose(disposeJobHandle));
                if (brushBrushIntersections      .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushBrushIntersections      .Dispose(disposeJobHandle));
                if (outputSurfaces               .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, outputSurfaces               .Dispose(disposeJobHandle));
                if (brushesThatNeedIndirectUpdate.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdate.Dispose(disposeJobHandle));
                Profiler.EndSample();

                if (meshQueries.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, meshQueries.Dispose(disposeJobHandle));
                meshQueries = default;

                
                if (vertexBufferContents.subMeshSections .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.subMeshSections.Dispose(disposeJobHandle));
                if (vertexBufferContents.meshDescriptions.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.meshDescriptions.Dispose(disposeJobHandle));
                if (vertexBufferContents.subMeshes       .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.subMeshes.Dispose(disposeJobHandle));
                if (vertexBufferContents.indices         .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.indices.Dispose(disposeJobHandle));
                if (vertexBufferContents.brushIndices    .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.brushIndices.Dispose(disposeJobHandle));
                if (vertexBufferContents.positions       .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.positions.Dispose(disposeJobHandle));
                if (vertexBufferContents.tangents        .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.tangents.Dispose(disposeJobHandle));
                if (vertexBufferContents.normals         .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.normals.Dispose(disposeJobHandle));
                if (vertexBufferContents.uv0             .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, vertexBufferContents.uv0.Dispose(disposeJobHandle));

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

                sections = default;
                brushRenderData               = default;
                subMeshCounts                 = default;
                subMeshSurfaces               = default;
                brushMeshLookup               = default;
                allTreeBrushIndexOrders       = default;
                rebuildTreeBrushIndexOrders   = default;
                brushBrushIntersections       = default;
                brushBrushIntersectionRange   = default;
                brushesThatNeedIndirectUpdate = default;
                uniqueBrushPairs              = default;
                outputSurfaces                = default;
                outputSurfacesRange           = default;
                intersectingBrushes           = default;
                meshQueries = default;

                brushCount = 0;
            }



            public JobHandle streamDependencyHandle;
            public JobHandle generateTreeSpaceVerticesAndBoundsJobHandle;
            public JobHandle generateBasePolygonLoopsJobHandle;
            public JobHandle mergeTouchingBrushVerticesJobHandle;
            public JobHandle mergeTouchingBrushVertices2JobHandle;

            public JobHandle findAllIntersectionsJobHandle;
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


        static readonly List<IndexOrder> s_TransformTreeBrushIndicesList = new List<IndexOrder>();
        static int[]            nodeIndexToNodeOrderArray;
        static TreeUpdate[]     s_TreeUpdates;

        static readonly TreeSorter s_TreeSorter = new TreeSorter();

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

        static readonly Dictionary<int, int> s_IndexLookup = new Dictionary<int, int>();
        static int2[] s_RemapOldOrderToNewOrder;


        internal static JobHandle UpdateTreeMeshes(UpdateMeshEvent updateMeshEvent, int[] treeNodeIDs)
        {
            var finalJobHandle = default(JobHandle);

            #region Do the setup for the CSG Jobs
            Profiler.BeginSample("CSG_Setup");
            if (s_TreeUpdates == null || s_TreeUpdates.Length < treeNodeIDs.Length)
            {
                if (s_TreeUpdates != null)
                {
                    for (int i = 0; i < s_TreeUpdates.Length; i++)
                        s_TreeUpdates[i].Dispose(s_TreeUpdates[i].lastJobHandle, false);
                }
                s_TreeUpdates = new TreeUpdate[treeNodeIDs.Length];
            }
            var treeUpdateLength = 0;
            for (int t = 0; t < treeNodeIDs.Length; t++)
            {
                var treeNodeIndex       = treeNodeIDs[t] - 1;
                var treeInfo            = CSGManager.nodeHierarchies[treeNodeIndex].treeInfo;
                
                var treeBrushes = treeInfo.treeBrushes;
                if (treeBrushes.Count == 0)
                    continue;

                int brushCount = treeBrushes.Count;

                var chiselLookupValues  = ChiselTreeLookup.Value[treeNodeIndex];
                chiselLookupValues.EnsureCapacity(brushCount);

                #region MeshQueries
                // TODO: have more control over the queries
                var meshQueries = new NativeArray<MeshQuery>(6, Allocator.TempJob);
                meshQueries[0] = new MeshQuery(
                    parameterIndex: LayerParameterIndex.RenderMaterial,
                    query:          LayerUsageFlags.RenderReceiveCastShadows,
                    mask:           LayerUsageFlags.RenderReceiveCastShadows,
                    vertexChannels: VertexChannelFlags.All
                );
                meshQueries[1] = new MeshQuery(
                    parameterIndex: LayerParameterIndex.RenderMaterial,
                    query:          LayerUsageFlags.RenderCastShadows,
                    mask:           LayerUsageFlags.RenderReceiveCastShadows,
                    vertexChannels: VertexChannelFlags.All
                );
                meshQueries[2] = new MeshQuery(
                    parameterIndex: LayerParameterIndex.RenderMaterial,
                    query:          LayerUsageFlags.RenderReceiveShadows,
                    mask:           LayerUsageFlags.RenderReceiveCastShadows,
                    vertexChannels: VertexChannelFlags.All
                );
                meshQueries[3] = new MeshQuery(
                    parameterIndex: LayerParameterIndex.RenderMaterial,
                    query:          LayerUsageFlags.Renderable,
                    mask:           LayerUsageFlags.RenderReceiveCastShadows,
                    vertexChannels: VertexChannelFlags.All
                );
                meshQueries[4] = new MeshQuery(
                    parameterIndex: LayerParameterIndex.RenderMaterial,
                    query:          LayerUsageFlags.CastShadows,
                    mask:           LayerUsageFlags.RenderReceiveCastShadows,
                    vertexChannels: VertexChannelFlags.All
                );
                meshQueries[5] = new MeshQuery(
                    parameterIndex: LayerParameterIndex.PhysicsMaterial,
                    query:          LayerUsageFlags.Collidable,
                    mask:           LayerUsageFlags.Collidable,
                    vertexChannels: VertexChannelFlags.Position
                );
                #endregion

                #region All Native Allocations
                Profiler.BeginSample("CSG_Allocations");
                ref var currentTree = ref s_TreeUpdates[treeUpdateLength];
                
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

                var brushesThatNeedIndirectUpdate   = currentTree.brushesThatNeedIndirectUpdate;
                var outputSurfaces                  = currentTree.outputSurfaces;
                var brushBrushIntersections         = currentTree.brushBrushIntersections;
                var uniqueBrushPairs                = currentTree.uniqueBrushPairs;
                var intersectingBrushes             = currentTree.intersectingBrushes;

                var allTreeBrushIndexOrders         = currentTree.allTreeBrushIndexOrders;
                var rebuildTreeBrushIndexOrders     = currentTree.rebuildTreeBrushIndexOrders;
                var brushMeshLookup                 = currentTree.brushMeshLookup;

                ref var vertexBufferContents = ref currentTree.vertexBufferContents;                
                
                Profiler.EndSample();
                #endregion

                #region Build lookup tables to find the tree node-order by node-index                
                var nodeIndexMin = int.MaxValue;
                var nodeIndexMax = 0;
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID     = treeBrushes[nodeOrder];
                    int nodeIndex  = nodeID - 1;
                    nodeIndexMin = math.min(nodeIndexMin, nodeIndex);
                    nodeIndexMax = math.max(nodeIndexMax, nodeIndex);
                }

                var nodeIndexToNodeOrderOffset  = nodeIndexMin;
                var desiredLength = (nodeIndexMax - nodeIndexMin) + 1;
                if (nodeIndexToNodeOrderArray == null ||
                    nodeIndexToNodeOrderArray.Length < desiredLength)
                    nodeIndexToNodeOrderArray = new int[desiredLength];
                for (int nodeOrder  = 0; nodeOrder  < brushCount; nodeOrder ++)
                {
                    int nodeID     = treeBrushes[nodeOrder];
                    int nodeIndex  = nodeID - 1;
                    nodeIndexToNodeOrderArray[nodeIndex - nodeIndexToNodeOrderOffset] = nodeOrder;
                    
                    // We need the index into the tree to ensure deterministic ordering
                    var brushIndexOrder = new IndexOrder { nodeIndex = nodeIndex, nodeOrder = nodeOrder };
                    allTreeBrushIndexOrders[nodeOrder] = brushIndexOrder;
                }
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
                //Debug.Log("---");
                //var builder = new System.Text.StringBuilder();
                var previousBrushIndexLength = chiselLookupValues.brushIndices.Length;
                if (previousBrushIndexLength > 0)
                {
                    //if (allTreeBrushIndexOrders.Length > previousBrushIndexLength)
                    //    chiselLookupValues.brushIndices.ResizeUninitialized(allTreeBrushIndexOrders.Length);
                    for (int n = 0; n < allTreeBrushIndexOrders.Length; n++)
                        s_IndexLookup[allTreeBrushIndexOrders[n].nodeIndex] = n;

                    bool needSwapping = false;

                    if (s_RemapOldOrderToNewOrder == null ||
                        s_RemapOldOrderToNewOrder.Length < previousBrushIndexLength)
                        s_RemapOldOrderToNewOrder = new int2[previousBrushIndexLength];
                    Array.Clear(s_RemapOldOrderToNewOrder, 0, previousBrushIndexLength);

                    //builder.Append("Previous ");
                    for (int n = 0; n < previousBrushIndexLength; n++)
                    {
                        var source = brushIndices[n];
                        //builder.Append($"{brushIndices[n]}, ");
                        if (!s_IndexLookup.TryGetValue(source, out var destination)) destination = -1;
                        s_RemapOldOrderToNewOrder[n] = new int2(n, destination);
                        needSwapping = needSwapping || (n != destination);
                    }

                    if (needSwapping)
                    {
                        /*
                        Debug.Log(builder.ToString());

                        builder.Clear();
                        builder.Append("New ");
                        for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                            builder.Append($"({allTreeBrushIndexOrders[i].nodeIndex}, {allTreeBrushIndexOrders[i].nodeOrder}), ");
                        Debug.Log(builder.ToString());
                        */
                        Array.Sort(s_RemapOldOrderToNewOrder, new IndexOrderSort());

                        /*
                        builder.Clear();
                        builder.Append("Remap ");
                        for (int n = 0; n < temp.Length; n++)
                        {
                            builder.Append($"({temp[n].x} <=> {temp[n].y}), ");
                        }
                        Debug.Log(builder.ToString());

                        builder.Clear();
                        builder.Append("Remap ");
                        */
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
                                    if (tmp.y == originValue)
                                    {
                                        s_RemapOldOrderToNewOrder[n2] = new int2(-1, -1);
                                        break;
                                    }
                                    //Debug.Log($"({tmp.x} => {overwrittenValue}, {tmp.y}, {originValue})");
                                    s_RemapOldOrderToNewOrder[n2] = new int2(originValue, tmp.y);
                                    break;
                                }
                            }
                            //builder.Append($"({temp[n].x} <=> {temp[n].y}), ");
                        }
                        Profiler.EndSample();
                        //Debug.Log(builder.ToString());

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
                                //brushIndices[source] = -1;

                                { var tmp = basePolygonCache[source]; basePolygonCache[source] = default; tmp.Dispose(); }
                                { var tmp = routingTableCache[source]; routingTableCache[source] = default; tmp.Dispose(); }
                                { var tmp = transformationCache[source]; transformationCache[source] = default; }
                                { var tmp = brushRenderBufferCache[source]; brushRenderBufferCache[source] = default; tmp.Dispose(); }
                                { var tmp = treeSpaceVerticesCache[source]; treeSpaceVerticesCache[source] = default; tmp.Dispose(); }
                                { var tmp = brushTreeSpaceBoundCache[source]; brushTreeSpaceBoundCache[source] = default; }
                                { var tmp = brushTreeSpacePlaneCache[source]; brushTreeSpacePlaneCache[source] = default; tmp.Dispose(); }
                                { var tmp = brushesTouchedByBrushCache[source]; brushesTouchedByBrushCache[source] = default; tmp.Dispose(); }
                            
                                //someOtherArray[source].Dispose();
                                //someOtherArray[source] = default;
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
                            //{ var tmp = brushIndices[destination]; brushIndices[destination] = brushIndices[source]; [source] = tmp; }
                            /*
                            {
                                builder.Clear();
                                builder.Append($"[{n}] ");
                                for (int b = 0; b < temp.Length; b++)
                                {
                                    builder.Append($"{brushIndices[b]}, ");
                                }
                                Debug.Log(builder.ToString());
                            }*/
                        }
                        Profiler.EndSample();
                    }
                }
                    
                //Debug.Log("---");
                Profiler.EndSample();

                Profiler.BeginSample("RESIZE");
                if (chiselLookupValues.brushIndices.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.brushIndices.ResizeUninitialized(allTreeBrushIndexOrders.Length);
                if (chiselLookupValues.basePolygonCache.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.basePolygonCache            .Resize(allTreeBrushIndexOrders.Length, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.routingTableCache.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.routingTableCache           .Resize(allTreeBrushIndexOrders.Length, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.transformationCache.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.transformationCache         .Resize(allTreeBrushIndexOrders.Length, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushRenderBufferCache.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.brushRenderBufferCache      .Resize(allTreeBrushIndexOrders.Length, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.treeSpaceVerticesCache.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.treeSpaceVerticesCache      .Resize(allTreeBrushIndexOrders.Length, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpaceBoundCache.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.brushTreeSpaceBoundCache    .Resize(allTreeBrushIndexOrders.Length, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushTreeSpacePlaneCache.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.brushTreeSpacePlaneCache    .Resize(allTreeBrushIndexOrders.Length, NativeArrayOptions.ClearMemory);
                if (chiselLookupValues.brushesTouchedByBrushCache.Length != allTreeBrushIndexOrders.Length)
                    chiselLookupValues.brushesTouchedByBrushCache  .Resize(allTreeBrushIndexOrders.Length, NativeArrayOptions.ClearMemory);
                Profiler.EndSample();

                for (int i = 0; i < allTreeBrushIndexOrders.Length; i++)
                    chiselLookupValues.brushIndices[i] = allTreeBrushIndexOrders[i].nodeIndex;
                
                brushIndices                = ref chiselLookupValues.brushIndices;
                basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                routingTableCache           = ref chiselLookupValues.routingTableCache;
                transformationCache         = ref chiselLookupValues.transformationCache;
                brushRenderBufferCache      = ref chiselLookupValues.brushRenderBufferCache;
                treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;

                // TODO: store everything in the cache in node-order, move things around when the order changes
                //       this way we do not need to retrieve/store it
                #region Copy all values from caches to arrays in node-order
                Profiler.BeginSample("CSG_FixUpCache");

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    var item = basePolygonCache[nodeOrder];
                    if (!item.IsCreated)
                        continue;
                    
                    // Fix up node orders
                    ref var polygons = ref item.Value.polygons;
                    for (int p = 0; p < polygons.Length; p++)
                    {
                        ref var nodeIndexOrder = ref polygons[p].nodeIndexOrder;
                        nodeIndexOrder.nodeOrder = nodeIndexToNodeOrderArray[nodeIndexOrder.nodeIndex - nodeIndexToNodeOrderOffset];
                    }
                    basePolygonCache[nodeOrder] = item;
                }

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    var item = brushesTouchedByBrushCache[nodeOrder];
                    if (!item.IsCreated)
                        continue;
                    
                    ref var brushesTouchedByBrush = ref item.Value;

                    // Fix up node orders

                    //ref var intersectionBits = ref brushesTouchedByBrush.intersectionBits;
                    //BlobBuilderExtensions.ClearValues(ref intersectionBits);

                    ref var brushIntersections = ref brushesTouchedByBrush.brushIntersections;
                    for (int b = 0; b < brushIntersections.Length; b++)
                    {
                        ref var brushIntersection = ref brushIntersections[b];
                        ref var nodeIndexOrder = ref brushIntersection.nodeIndexOrder;
                        nodeIndexOrder.nodeOrder = nodeIndexToNodeOrderArray[nodeIndexOrder.nodeIndex - nodeIndexToNodeOrderOffset];
                        //brushesTouchedByBrush.Set(nodeIndexOrder.nodeOrder, brushIntersection.type);
                    }
                    brushesTouchedByBrushCache[nodeOrder] = item;
                }
                Profiler.EndSample();
                #endregion
     
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
                    int nodeID      = treeBrushes[nodeOrder];
                    int nodeIndex   = nodeID - 1;
                    int brushMeshID = 0;
                    if (!IsValidNodeID(nodeID) ||
                        // NOTE: Assignment is intended, this is not supposed to be a comparison
                        (brushMeshID = CSGManager.nodeHierarchies[nodeIndex].brushInfo.brushMeshInstanceID) == 0)
                    {
                        // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                        Debug.LogError($"Brush with ID {nodeID}, index {nodeIndex} has its brushMeshID set to {brushMeshID}, which is invalid."); 
                    } else
                    {
                        surfaceCount += brushMeshBlobs[brushMeshID - 1].Value.polygons.Length;
                    }

                    brushMeshLookup[nodeOrder] = brushMeshID == 0 ? BlobAssetReference<BrushMeshBlob>.Null : brushMeshBlobs[brushMeshID - 1];
                }
                Profiler.EndSample();
                #endregion

                Profiler.BeginSample("CSG_OutputSurfacesCapacity");
                if (currentTree.outputSurfaces.Capacity < surfaceCount)
                    currentTree.outputSurfaces.Capacity = surfaceCount;
                Profiler.EndSample();

                var anyHierarchyModified = false;
                rebuildTreeBrushIndexOrders.Clear();
                s_TransformTreeBrushIndicesList.Clear();
                #region Build list of all brushes that have been modified
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID     = treeBrushes[nodeOrder];
                    int nodeIndex  = nodeID - 1;

                    var nodeFlags = CSGManager.nodeFlags[nodeIndex];
                    if (nodeFlags.status != NodeStatusFlags.None)
                    {
                        var indexOrder = allTreeBrushIndexOrders[nodeOrder];
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
                #endregion

                #region Invalidate modified brush caches
                for (int nodeOrder = 0; nodeOrder < rebuildTreeBrushIndexOrders.Length; nodeOrder++)
                {
                    var indexOrder = rebuildTreeBrushIndexOrders[nodeOrder];
                    int nodeIndex  = indexOrder.nodeIndex;
                    
                    // Remove base polygons
                    if (basePolygonCache[nodeOrder] != null && basePolygonCache[nodeOrder].IsCreated) basePolygonCache[nodeOrder].Dispose();
                    basePolygonCache[nodeOrder] = default;

                    // Remove cached bounding box
                    brushTreeSpaceBoundCache[nodeOrder] = default;

                    // Remove treeSpace vertices
                    if (treeSpaceVerticesCache[nodeOrder] != null && treeSpaceVerticesCache[nodeOrder].IsCreated) treeSpaceVerticesCache[nodeOrder].Dispose();
                    treeSpaceVerticesCache[nodeOrder] = default;
                }
                #endregion

                #region Invalidate brushes that touch our modified brushes, so we rebuild those too
                if (rebuildTreeBrushIndexOrders.Length != brushCount)
                {
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

                            // TODO: Remove nodes from "brushIntersections" when the brush is removed from the hierarchy
                            if (!IsValidNodeID(otherBrushID))
                                continue;

                            var otherBrushOrder = nodeIndexToNodeOrderArray[otherBrushIndex - nodeIndexToNodeOrderOffset];
                            var otherIndexOrder = new IndexOrder { nodeIndex = otherBrushIndex, nodeOrder = otherBrushOrder };
                            if (!rebuildTreeBrushIndexOrders.Contains(otherIndexOrder))
                                rebuildTreeBrushIndexOrders.Add(otherIndexOrder);
                        }
                    }
                }
                #endregion

                #region Dirty all invalidated outlines
                Profiler.BeginSample("CSG_DirtyModifiedOutlines");
                {
                    for (int b = 0; b < rebuildTreeBrushIndexOrders.Length; b++)
                    {
                        var brushIndexOrder = rebuildTreeBrushIndexOrders[b];
                        int brushNodeIndex  = brushIndexOrder.nodeIndex;
                        var brushInfo = CSGManager.nodeHierarchies[brushNodeIndex].brushInfo;
                        brushInfo.brushOutlineGeneration++;
                        brushInfo.brushOutlineDirty = true;
                    }
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated routing-tables
                Profiler.BeginSample("CSG_InvalidateRoutingTables");
                for (int index = 0; index < rebuildTreeBrushIndexOrders.Length; index++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrders[index];
                    var original = routingTableCache[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    routingTableCache[brushIndexOrder.nodeOrder] = default;
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated brushTreeSpacePlanes
                Profiler.BeginSample("CSG_InvalidateBrushTreeSpacePlanes");
                for (int index = 0; index < rebuildTreeBrushIndexOrders.Length; index++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrders[index];
                    var original = brushTreeSpacePlaneCache[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    brushTreeSpacePlaneCache[brushIndexOrder.nodeOrder] = default;
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated brushesTouchesByBrushes
                Profiler.BeginSample("CSG_InvalidateBrushesTouchesByBrushes");
                for (int index = 0; index < rebuildTreeBrushIndexOrders.Length; index++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrders[index];
                    var original = brushesTouchedByBrushCache[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    brushesTouchedByBrushCache[brushIndexOrder.nodeOrder] = default;
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated renderbuffers
                Profiler.BeginSample("CSG_InvalidateBrushRenderBuffers");
                var brushLoopCount = rebuildTreeBrushIndexOrders.Length;
                for (int index = 0; index < brushLoopCount; index++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrders[index];

                    // Why was I doing this??
                    //if (s_RebuildTreeBrushIndexOrdersList.Contains(brushIndexOrder))
                    {
                        int brushNodeOrder = brushIndexOrder.nodeOrder;
                        if (brushRenderBufferCache[brushNodeOrder].IsCreated)
                        {
                            brushRenderBufferCache[brushNodeOrder].Dispose();
                            brushRenderBufferCache[brushNodeOrder] = default;
                        }
                    }
                }
                Profiler.EndSample();
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
                ref var compactTree = ref chiselLookupValues.compactTree;
                // only rebuild this when the hierarchy changes
                if (anyHierarchyModified ||
                    !compactTree.IsCreated)
                {
                    if (compactTree.IsCreated)
                        compactTree.Dispose();

                    // TODO: jobify?
                    Profiler.BeginSample("CSG_CompactTree.Create");
                    compactTree = CompactTree.Create(CSGManager.nodeHierarchies, treeNodeIndex);
                    Profiler.EndSample();

                    chiselLookupValues.compactTree = compactTree;
                }
                #endregion

                #region Build per tree lookup
                currentTree.treeNodeIndex   = treeNodeIndex;
                currentTree.brushCount      = brushCount;
                currentTree.updateCount     = rebuildTreeBrushIndexOrders.Length;
                currentTree.maxNodeOrder    = treeBrushes.Count;
                currentTree.compactTree     = compactTree;
                currentTree.meshQueries     = meshQueries;
                #endregion

                treeUpdateLength++;
            }
            
            // Sort trees from largest (slowest) to smallest (fastest)
            // The slowest trees will run first, and the fastest trees can then hopefully fill the gaps
            Array.Sort(s_TreeUpdates, s_TreeSorter);

            Profiler.EndSample();
            #endregion

            // TODO: ensure we only update exactly what we need, and nothing more

            try
            {
                #region CSG Jobs
                Profiler.BeginSample("CSG_Jobs");

                // TODO: should only do this once at creation time, part of brushMeshBlob? store with brush component itself
                Profiler.BeginSample("Job_GenerateBoundsLoops");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders,
                            brushMeshLookup             = treeUpdate.brushMeshLookup,
                            transformations             = chiselLookupValues.transformationCache.AsDeferredJobArray(),

                            // Write
                            brushTreeSpaceBounds        = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
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
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.generateTreeSpaceVerticesAndBoundsJobHandle;
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findAllIntersectionsJob = new FindAllBrushIntersectionsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            transformations                 = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup,
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            updateBrushIndexOrders          = treeUpdate.rebuildTreeBrushIndexOrders,
                        
                            // Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections.AsParallelWriter(),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsParallelWriter()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#else
                        treeUpdate.findAllIntersectionsJobHandle = findAllIntersectionsJob.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
#endif
                    }
                    
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findAllIntersectionsJobHandle;
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findAllIntersectionsJob = new FindAllIndirectBrushIntersectionsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            transformations                 = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup,
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsDeferredJobArray(),                            

                            // Read/Write
                            updateBrushIndexOrders          = treeUpdate.rebuildTreeBrushIndexOrders,
                        
                            // Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections.AsParallelWriter()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.findAllIndirectIntersectionsJobHandle = findAllIntersectionsJob.
                            Run(dependencies);
#else
                        treeUpdate.findAllIndirectIntersectionsJobHandle = findAllIntersectionsJob.
                            Schedule(dependencies);
#endif
                    }

                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findAllIndirectIntersectionsJobHandle;
                        var gatherBrushIntersectionsJob = new GatherBrushIntersectionsJob
                        {
                            // Read / Write (Sort)
                            brushBrushIntersections     = treeUpdate.brushBrushIntersections.AsDeferredJobArray(),

                            // Write
                            brushBrushIntersectionRange = treeUpdate.brushBrushIntersectionRange
                        };
#if RUN_IN_SERIAL
                        treeUpdate.gatherBrushIntersectionsJobHandle = gatherBrushIntersectionsJob.
                            Run(dependencies);
#else
                        treeUpdate.gatherBrushIntersectionsJobHandle = gatherBrushIntersectionsJob.
                            Schedule(dependencies);
#endif
                    }

                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.gatherBrushIntersectionsJobHandle;
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                        {
                            // Read
                            treeNodeIndex               = treeUpdate.treeNodeIndex,
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            compactTree                 = treeUpdate.compactTree,
                            brushBrushIntersectionRange = treeUpdate.brushBrushIntersectionRange,
#if RUN_IN_SERIAL
                            brushBrushIntersections     = treeUpdate.brushBrushIntersections.AsArray(),
#else
                            brushBrushIntersections     = treeUpdate.brushBrushIntersections.AsDeferredJobArray(),
#endif

                            // Write
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray()
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

                Profiler.BeginSample("Job_MergeTouchingBrushVertices");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVertices2Job
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),

                            // Read / Write
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
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
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.mergeTouchingBrushVerticesJobHandle, treeUpdate.findIntersectingBrushesJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobs
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            brushMeshLookup             = treeUpdate.brushMeshLookup,
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),

                            // Write
                            basePolygons                = chiselLookupValues.basePolygonCache.AsDeferredJobArray()
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
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushMeshLookup         = treeUpdate.brushMeshLookup,
                            transformations         = chiselLookupValues.transformationCache.AsDeferredJobArray(),

                            // Write
                            brushTreeSpacePlanes    = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray()
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
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
                        // Build categorization trees for brushes
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var createRoutingTableJob = new CreateRoutingTableJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            compactTree             = treeUpdate.compactTree,

                            // Write
                            routingTableLookup      = chiselLookupValues.routingTableCache.AsDeferredJobArray()
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
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.findIntersectingBrushesJobHandle;
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findBrushPairsJob = new FindBrushPairsJob
                        {
                            // Read
                            maxOrder                    = treeUpdate.allTreeBrushIndexOrders.Length,
#if RUN_IN_SERIAL
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                                    
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

                Profiler.BeginSample("Job_AllocateStreams");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        treeUpdate.streamDependencyHandle = JobHandle.CombineDependencies(
                            NativeStream.ScheduleConstruct(out treeUpdate.dataStream1, treeUpdate.allTreeBrushIndexOrders, default, Allocator.TempJob),
                            NativeStream.ScheduleConstruct(out treeUpdate.dataStream2, treeUpdate.allTreeBrushIndexOrders, default, Allocator.TempJob));
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
                        var dependencies    = treeUpdate.findBrushPairsJobHandle;
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            uniqueBrushPairs        = treeUpdate.uniqueBrushPairs.AsArray(),
#else
                            uniqueBrushPairs        = treeUpdate.uniqueBrushPairs.AsDeferredJobArray(),
#endif
                            transformations         = chiselLookupValues.transformationCache.AsDeferredJobArray(),
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
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = JobHandle.CombineDependencies(
                                                            treeUpdate.mergeTouchingBrushVerticesJobHandle,
                                                            treeUpdate.updateBrushTreeSpacePlanesJobHandle, 
                                                            treeUpdate.prepareBrushPairIntersectionsJobHandle);
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Read
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
#if RUN_IN_SERIAL
                            intersectingBrushes         = treeUpdate.intersectingBrushes.AsArray(),
#else
                            intersectingBrushes         = treeUpdate.intersectingBrushes.AsDeferredJobArray(),
#endif
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                            
                            // Write
                            outputSurfaces              = treeUpdate.outputSurfaces.AsParallelWriter()
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
                

                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;
                    var dependencies    = treeUpdate.findAllIntersectionLoopsJobHandle;
                    var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                    {
                        // Read / Write (Sort)
                        outputSurfaces          = treeUpdate.outputSurfaces.AsDeferredJobArray(),

                        // Write
                        outputSurfacesRange     = treeUpdate.outputSurfacesRange
                    };
#if RUN_IN_SERIAL
                    treeUpdate.gatherOutputSurfacesJobHandle = gatherOutputSurfacesJob.
                        Run(dependencies);
#else
                    treeUpdate.gatherOutputSurfacesJobHandle = gatherOutputSurfacesJob.
                        Schedule(dependencies);
#endif
                }

                Profiler.BeginSample("Job_FindLoopOverlapIntersections");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies = JobHandle.CombineDependencies(treeUpdate.gatherOutputSurfacesJobHandle, 
                                                                         treeUpdate.prepareBrushPairIntersectionsJobHandle,
                                           JobHandle.CombineDependencies(treeUpdate.generateBasePolygonLoopsJobHandle,
                                                                         treeUpdate.streamDependencyHandle));
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
                            outputSurfaces              = treeUpdate.outputSurfaces.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
                            outputSurfaces              = treeUpdate.outputSurfaces.AsDeferredJobArray(),
#endif
                            outputSurfacesRange         = treeUpdate.outputSurfacesRange,
                            maxNodeOrder                = treeUpdate.maxNodeOrder,
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            basePolygons                = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),

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
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = treeUpdate.allFindLoopOverlapIntersectionsJobHandle;
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),

                            // Read / Write
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
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

                Profiler.BeginSample("Job_PerformCSG");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate  = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.mergeTouchingBrushVertices2JobHandle, 
                                                                            treeUpdate.updateBrushCategorizationTablesJobHandle,
                                                                            treeUpdate.streamDependencyHandle);

                        // Perform CSG
                        // TODO: determine when a brush is completely inside another brush
                        //		 (might not have any intersection loops)
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var performCSGJob = new PerformCSGJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            routingTableLookup          = chiselLookupValues.routingTableCache.AsDeferredJobArray(),
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
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
                        treeUpdate.allPerformAllCSGJobHandle = treeUpdate.dataStream1.Dispose(treeUpdate.allPerformAllCSGJobHandle);
                        treeUpdate.dataStream1 = default;
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
                        var dependencies    = JobHandle.CombineDependencies(treeUpdate.allPerformAllCSGJobHandle, treeUpdate.generateBasePolygonLoopsJobHandle);

                        // TODO: Potentially merge this with PerformCSGJob?
                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            basePolygons                = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),
                            transformations             = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            input                       = treeUpdate.dataStream2.AsReader(),

                            // Write
                            brushRenderBuffers          = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray()
                        };
#if RUN_IN_SERIAL
                        treeUpdate.allGenerateSurfaceTrianglesJobHandle = generateSurfaceRenderBuffers.
                            Run(treeUpdate.rebuildTreeBrushIndexOrders, 64, dependencies);
#else
                        treeUpdate.allGenerateSurfaceTrianglesJobHandle = generateSurfaceRenderBuffers.
                            Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 64, dependencies);
#endif
                        treeUpdate.allGenerateSurfaceTrianglesJobHandle = treeUpdate.dataStream2.Dispose(treeUpdate.allGenerateSurfaceTrianglesJobHandle);
                        treeUpdate.dataStream2 = default;
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

                        var dependencies        = treeUpdate.allGenerateSurfaceTrianglesJobHandle;
                        var chiselLookupValues  = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        
                        dependencies = ChiselNativeListExtensions.ScheduleEnsureCapacity(treeUpdate.brushRenderData, treeUpdate.allTreeBrushIndexOrders, dependencies);
                        var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                        {
                            meshQueryLength         = treeUpdate.meshQueries.Length,
                            allTreeBrushIndexOrders = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            brushRenderBuffers      = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray(),

                            brushRenderData         = treeUpdate.brushRenderData,
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            subMeshCounts           = treeUpdate.subMeshCounts,
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections,
                        };
                        treeUpdate.findBrushRenderBuffersJobHandle = findBrushRenderBuffersJob.Schedule(dependencies);
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

                        var dependencies = treeUpdate.findBrushRenderBuffersJobHandle;

                        var prepareJob = new PrepareSubSectionsJob
                        {
                            meshQueries         = treeUpdate.meshQueries,
                            brushRenderData     = treeUpdate.brushRenderData.AsDeferredJobArray(),

                            sections            = treeUpdate.sections,
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces,
                        };
                        treeUpdate.prepareJobHandle = prepareJob.Schedule(dependencies);

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

                        var dependencies = treeUpdate.prepareJobHandle;

                        var sortJob = new SortSurfacesJob 
                        {
                            sections            = treeUpdate.sections.AsDeferredJobArray(),
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces.AsDeferredJobArray(),
                            subMeshCounts       = treeUpdate.subMeshCounts,
                            subMeshSections     = treeUpdate.vertexBufferContents.subMeshSections,
                        };
                        treeUpdate.sortJobHandle = sortJob.Schedule(dependencies);

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
                            // ReadOnly
                            subMeshSections     = treeUpdate.vertexBufferContents.subMeshSections.AsDeferredJobArray(),

                            // WriteOnly
                            subMeshesArray      = treeUpdate.vertexBufferContents.subMeshes,
                            indicesArray        = treeUpdate.vertexBufferContents.indices,
                            brushIndicesArray   = treeUpdate.vertexBufferContents.brushIndices,
                            positionsArray      = treeUpdate.vertexBufferContents.positions,
                            tangentsArray       = treeUpdate.vertexBufferContents.tangents,
                            normalsArray        = treeUpdate.vertexBufferContents.normals,
                            uv0Array            = treeUpdate.vertexBufferContents.uv0
                        };
                        treeUpdate.allocateVertexBufferJobHandle = allocateVertexBuffersJob.Schedule(dependencies);
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
                            subMeshCounts       = treeUpdate.subMeshCounts.AsDeferredJobArray(),
                            meshDescriptions    = treeUpdate.vertexBufferContents.meshDescriptions
                        };
                        treeUpdate.generateMeshDescriptionJobHandle = generateMeshDescriptionJob.Schedule(dependencies);
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
                            subMeshSections     = treeUpdate.vertexBufferContents.subMeshSections.AsDeferredJobArray(),
               
                            subMeshCounts       = treeUpdate.subMeshCounts.AsDeferredJobArray(),
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces.AsDeferredJobArray(),

                            subMeshesArray      = treeUpdate.vertexBufferContents.subMeshes,
                            tangentsArray       = treeUpdate.vertexBufferContents.tangents,
                            normalsArray        = treeUpdate.vertexBufferContents.normals,
                            uv0Array            = treeUpdate.vertexBufferContents.uv0,
                            positionsArray      = treeUpdate.vertexBufferContents.positions,
                            indicesArray        = treeUpdate.vertexBufferContents.indices,
                            brushIndicesArray   = treeUpdate.vertexBufferContents.brushIndices,
                        };
                        treeUpdate.fillVertexBuffersJobHandle = generateVertexBuffersJob.Schedule(treeUpdate.vertexBufferContents.subMeshSections, 1, dependencies);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.EndSample();
                #endregion

                #region Reset Flags
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
                #endregion
                 
                #region Complete Jobs

                //JobHandle.ScheduleBatchedJobs();

                Profiler.BeginSample("CSG_JobComplete");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;

                    finalJobHandle = JobHandle.CombineDependencies(treeUpdate.generateMeshDescriptionJobHandle, treeUpdate.fillVertexBuffersJobHandle, finalJobHandle);
                }
                finalJobHandle.Complete();
                Profiler.EndSample();
                #endregion

                if (updateMeshEvent != null)
                {
                    // TODO: clean this 
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var treeNodeIndex   = treeUpdate.treeNodeIndex;
                        var treeNodeID      = treeUpdate.treeNodeIndex + 1;
                        var tree            = new CSGTree { treeNodeID = treeNodeID };

                        var flags = CSGManager.nodeFlags[treeNodeIndex];
                        if (!flags.IsNodeFlagSet(NodeStatusFlags.TreeMeshNeedsUpdate))
                            continue;

                        // See if the tree has been modified
                        if (!tree.Dirty)
                            continue;

                        // Skip invalid brushes since they won't work anyway
                        if (!tree.Valid)
                            continue;

                        flags.UnSetNodeFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                        nodeFlags[treeNodeIndex] = flags;
                        updateMeshEvent.Invoke(tree, ref treeUpdate.vertexBufferContents);
                    }
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

                        // TODO: put in job?
                        treeUpdate.Dispose(disposeJobHandle, onlyBlobs: false);
                        //Clear(); // <-- do this in same job
                    }
                }
                Profiler.EndSample();
                #endregion
            }

            #region Clear garbage
            s_TransformTreeBrushIndicesList.Clear();
            #endregion

            return finalJobHandle;
        }

        #region TreeSorter
        // Sort trees so we try to schedule the slowest ones first, so the faster ones can then fill the gaps in between
        struct TreeSorter : IComparer<TreeUpdate>
        {
            public int Compare(TreeUpdate x, TreeUpdate y)
            {
                if (!x.allTreeBrushIndexOrders.IsCreated)
                {
                    if (!y.allTreeBrushIndexOrders.IsCreated)
                        return 0;
                    return 1;
                }
                if (!y.allTreeBrushIndexOrders.IsCreated)
                    return -1;
                var xBrushBrushIntersectionsCount = x.allTreeBrushIndexOrders.Length;
                var yBrushBrushIntersectionsCount = y.allTreeBrushIndexOrders.Length;
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
        }
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

            allTrees = UpdateTreeMeshes(updateMeshEvent, trees.ToArray());
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
