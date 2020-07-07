//#define RUN_IN_SERIAL
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Profiler = UnityEngine.Profiling.Profiler;
using Debug = UnityEngine.Debug;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.UIElements;

namespace Chisel.Core
{
    static partial class CSGManager
    {
        internal sealed class TreeInfo
        {
            public readonly List<int>                       treeBrushes         = new List<int>();
            public NativeList<GeneratedMeshDescription>     meshDescriptions;
            public VertexBufferContents                     vertexBufferContents;
            
            //public NativeList<SectionData>                  sections;
            //public NativeList<BrushData>                    brushRenderData;
            //public NativeList<SubMeshCounts>                subMeshCounts;
            //public NativeList<VertexBufferInit>             subMeshSections;
            //public NativeList<SubMeshSurface>               subMeshSurfaces;

            
            public void Reset()
            {
                if (meshDescriptions.IsCreated)
                    meshDescriptions.Clear();
                //if (sections.IsCreated)
                //    sections.Clear();
                //if (brushRenderData.IsCreated)
                //    brushRenderData.Clear();
                //if (subMeshCounts.IsCreated)
                //    subMeshCounts.Clear();
                //if (subMeshSections.IsCreated)
                //    subMeshSections.Clear();
                //if (subMeshSurfaces.IsCreated)
                //    subMeshSurfaces.Clear();
            }

            public void Dispose()
            {
                if (meshDescriptions.IsCreated)
                    meshDescriptions.Dispose();
                meshDescriptions = default;

                //if (sections.IsCreated)
                //    sections.Dispose();
                //sections = default;

                //if (brushRenderData.IsCreated)
                //    brushRenderData.Dispose();
                //brushRenderData = default;

                //if (subMeshCounts.IsCreated)
                //    subMeshCounts.Dispose();
                //subMeshCounts = default;

                //if (subMeshSections.IsCreated)
                //    subMeshSections.Dispose();
                //subMeshSections = default;

                //if (subMeshSurfaces.IsCreated)
                //    subMeshSurfaces.Dispose();
                //subMeshSurfaces = default;

                vertexBufferContents.Dispose();
                vertexBufferContents = default;
            }
        }

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

            // TODO: We store this per tree, and ensure brushes have ids from 0 to x per tree, then we can use an array here.
            //       Remap "local index" to "nodeindex" and back? How to make this efficiently work with caching stuff?
            public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>  treeSpaceVerticesArray;

            public NativeList<BrushPair>                                        brushBrushIntersections;
            public NativeArray<int2>                                            brushBrushIntersectionRange;
            public NativeList<IndexOrder>                                       brushesThatNeedIndirectUpdate;

            public NativeList<BrushPair>                                        uniqueBrushPairs;
            public NativeList<BlobAssetReference<BrushIntersectionLoop>>        outputSurfaces;
            public NativeArray<int2>                                            outputSurfacesRange;
            public NativeList<BlobAssetReference<BrushPairIntersection>>        intersectingBrushes;

            //public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>   brushRenderBuffers;
            public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>       brushesTouchedByBrushes;
            public NativeArray<BlobAssetReference<RoutingTable>>                routingTableLookup;
            public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>        brushTreeSpacePlanes;
            public NativeArray<MinMaxAABB>                                      brushTreeSpaceBounds;
            public NativeArray<BlobAssetReference<BasePolygonsBlob>>            basePolygons;
            public NativeArray<NodeTransformations>                             transformations;
            public NativeArray<BlobAssetReference<BrushMeshBlob>>               brushMeshLookup;
            
            public NativeList<SectionData>                  sections;
            public NativeList<BrushData>                    brushRenderData;
            public NativeList<SubMeshCounts>                subMeshCounts;
            public NativeList<VertexBufferInit>             subMeshSections;
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
                
                sections                    .Clear();
                brushRenderData             .Clear();
                subMeshCounts               .Clear();
                subMeshSections             .Clear();
                subMeshSurfaces             .Clear();
                Profiler.EndSample();
                

                brushesTouchedByBrushes         .ClearStruct();
                treeSpaceVerticesArray          .ClearStruct();
                basePolygons                    .ClearStruct();
                brushTreeSpacePlanes            .ClearStruct();
                routingTableLookup              .ClearStruct();
                brushMeshLookup                 .ClearStruct();

                brushBrushIntersectionRange     .ClearValues();
                outputSurfacesRange             .ClearValues();
                transformations                 .ClearValues();
                brushTreeSpaceBounds            .ClearValues();
                //brushRenderBuffers            .ClearValues();

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

                Profiler.BeginSample("DISPOSE");
                if (this.brushCount > 0)
                    Dispose(lastJobHandle, onlyBlobs: false);
                Profiler.EndSample();

                Profiler.BeginSample("NEW");
                this.brushCount                 = newBrushCount;
                var triangleArraySize           = GeometryMath.GetTriangleArraySize(newBrushCount);
                var intersectionCount           = triangleArraySize;
                brushesThatNeedIndirectUpdate   = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                outputSurfaces                  = new NativeList<BlobAssetReference<BrushIntersectionLoop>>(intersectionCount, Allocator.Persistent);
                brushBrushIntersections         = new NativeList<BrushPair>(intersectionCount * 2, Allocator.Persistent);
                uniqueBrushPairs                = new NativeList<BrushPair>(intersectionCount, Allocator.Persistent);
                intersectingBrushes             = new NativeList<BlobAssetReference<BrushPairIntersection>>(intersectionCount, Allocator.Persistent);                
                rebuildTreeBrushIndexOrders     = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                allTreeBrushIndexOrders         = new NativeList<IndexOrder>(newBrushCount, Allocator.Persistent);
                allTreeBrushIndexOrders.Resize(newBrushCount, NativeArrayOptions.ClearMemory);

                outputSurfacesRange             = new NativeArray<int2>(newBrushCount, Allocator.Persistent);
                brushBrushIntersectionRange     = new NativeArray<int2>(newBrushCount, Allocator.Persistent);
                brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(newBrushCount, Allocator.Persistent);
                brushesTouchedByBrushes         = new NativeArray<BlobAssetReference<BrushesTouchedByBrush>>(newBrushCount, Allocator.Persistent);
                treeSpaceVerticesArray          = new NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>(newBrushCount, Allocator.Persistent);
                basePolygons                    = new NativeArray<BlobAssetReference<BasePolygonsBlob>>(newBrushCount, Allocator.Persistent);
                transformations                 = new NativeArray<NodeTransformations>(newBrushCount, Allocator.Persistent);
                brushTreeSpacePlanes            = new NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>(newBrushCount, Allocator.Persistent);
                brushTreeSpaceBounds            = new NativeArray<MinMaxAABB>(newBrushCount, Allocator.Persistent);
                routingTableLookup              = new NativeArray<BlobAssetReference<RoutingTable>>(newBrushCount, Allocator.Persistent);
                //brushRenderBuffers            = new NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>(brushCount, Allocator.Persistent);
                
                brushRenderData                 = new NativeList<BrushData>(newBrushCount, Allocator.Persistent);
            
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

                if (onlyBlobs)
                    return;

                Profiler.BeginSample("DISPOSE ARRAY");
                if (transformations              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, transformations              .Dispose(disposeJobHandle));
                if (basePolygons                 .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, basePolygons                 .Dispose(disposeJobHandle));
                if (brushTreeSpaceBounds         .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushTreeSpaceBounds         .Dispose(disposeJobHandle));
                if (brushTreeSpacePlanes         .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushTreeSpacePlanes         .Dispose(disposeJobHandle));
                if (routingTableLookup           .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, routingTableLookup           .Dispose(disposeJobHandle));
                if (brushesTouchedByBrushes      .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushesTouchedByBrushes      .Dispose(disposeJobHandle));
                //if (brushRenderBuffers         .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushRenderBuffers           .Dispose(disposeJobHandle));
                if (brushMeshLookup              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushMeshLookup              .Dispose(disposeJobHandle));
                if (treeSpaceVerticesArray       .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, treeSpaceVerticesArray       .Dispose(disposeJobHandle));
                if (brushBrushIntersectionRange  .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushBrushIntersectionRange  .Dispose(disposeJobHandle));
                if (outputSurfacesRange          .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, outputSurfacesRange          .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE LIST");
                if (sections                     .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, sections                     .Dispose(disposeJobHandle));
                if (brushRenderData              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushRenderData              .Dispose(disposeJobHandle));
                if (subMeshCounts                .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshCounts                .Dispose(disposeJobHandle));
                if (subMeshSections              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshSections              .Dispose(disposeJobHandle));
                if (subMeshSurfaces              .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, subMeshSurfaces              .Dispose(disposeJobHandle));
                if (allTreeBrushIndexOrders      .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, allTreeBrushIndexOrders      .Dispose(disposeJobHandle));
                if (rebuildTreeBrushIndexOrders  .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, rebuildTreeBrushIndexOrders  .Dispose(disposeJobHandle));
                if (uniqueBrushPairs             .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, uniqueBrushPairs             .Dispose(disposeJobHandle));
                if (intersectingBrushes          .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, intersectingBrushes          .Dispose(disposeJobHandle));
                if (brushBrushIntersections      .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushBrushIntersections      .Dispose(disposeJobHandle));
                if (outputSurfaces               .IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, outputSurfaces               .Dispose(disposeJobHandle));
                if (brushesThatNeedIndirectUpdate.IsCreated) lastJobHandle = JobHandle.CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdate.Dispose(disposeJobHandle));
                Profiler.EndSample();

                if (meshQueries.IsCreated) meshQueries.Dispose();
                meshQueries = default;
                
                sections                      = default;
                brushRenderData               = default;
                subMeshCounts                 = default;
                subMeshSections               = default;
                subMeshSurfaces               = default;
                transformations               = default;
                basePolygons                  = default;
                brushTreeSpaceBounds          = default;
                brushTreeSpacePlanes          = default;
                routingTableLookup            = default;
                brushesTouchedByBrushes       = default;
                //brushRenderBuffers          = default;
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
                treeSpaceVerticesArray        = default;

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

        internal static JobHandle UpdateTreeMeshes(int[] treeNodeIDs)
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
                treeInfo.Reset();

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
                if (!currentTree.subMeshSections.IsCreated) currentTree.subMeshSections = new NativeList<VertexBufferInit>(Allocator.Persistent);

                currentTree.subMeshCounts.Clear();
                currentTree.subMeshSections.Clear();
                currentTree.sections.Clear();
                if (currentTree.sections.Capacity < meshQueries.Length)
                    currentTree.sections.Capacity = meshQueries.Length;

                var brushesThatNeedIndirectUpdate   = currentTree.brushesThatNeedIndirectUpdate;
                var outputSurfaces                  = currentTree.outputSurfaces;
                var brushBrushIntersections         = currentTree.brushBrushIntersections;
                var uniqueBrushPairs                = currentTree.uniqueBrushPairs;
                var intersectingBrushes             = currentTree.intersectingBrushes;

                var brushesTouchedByBrushes         = currentTree.brushesTouchedByBrushes;
                var treeSpaceVerticesArray          = currentTree.treeSpaceVerticesArray;
                var basePolygons                    = currentTree.basePolygons;
                var transformations                 = currentTree.transformations;
                var brushTreeSpacePlanes            = currentTree.brushTreeSpacePlanes;
                var brushTreeSpaceBounds            = currentTree.brushTreeSpaceBounds;
                var routingTableLookup              = currentTree.routingTableLookup;
                //var brushRenderBuffers            = currentTree.brushRenderBuffers;

                var allTreeBrushIndexOrders         = currentTree.allTreeBrushIndexOrders;
                var rebuildTreeBrushIndexOrders     = currentTree.rebuildTreeBrushIndexOrders;
                var brushMeshLookup                 = currentTree.brushMeshLookup;
                
                treeInfo.vertexBufferContents.EnsureAllocated();
                if (!treeInfo.meshDescriptions.IsCreated)
                    treeInfo.meshDescriptions = new NativeList<GeneratedMeshDescription>(Allocator.Persistent);
                treeInfo.meshDescriptions.Clear();

                Profiler.EndSample();
                #endregion

                ref var basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                ref var routingTableCache           = ref chiselLookupValues.routingTableCache;
                ref var transformationCache         = ref chiselLookupValues.transformationCache;
                ref var brushRenderBufferCache      = ref chiselLookupValues.brushRenderBufferCache;
                ref var treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;
                ref var brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                ref var brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                ref var brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;
               
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

                // TODO: store everything in the cache in node-order, move things around when the order changes
                //       this way we do not need to retrieve/store it
                #region Copy all values from caches to arrays in node-order
                Profiler.BeginSample("CSG_RetrieveFromCache");

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID = treeBrushes[nodeOrder];
                    int nodeIndex = nodeID - 1;
                    if (basePolygonCache.TryGetValue(nodeIndex, out var item) && item.IsCreated)
                    {
                        // Fix up node orders
                        ref var polygons = ref item.Value.polygons;
                        for (int p = 0; p < polygons.Length; p++)
                        {
                            ref var nodeIndexOrder = ref polygons[p].nodeIndexOrder;
                            nodeIndexOrder.nodeOrder = nodeIndexToNodeOrderArray[nodeIndexOrder.nodeIndex - nodeIndexToNodeOrderOffset];
                        }
                    } else
                        item = BlobAssetReference<BasePolygonsBlob>.Null;
                    basePolygons[nodeOrder] = item;
                }

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID = treeBrushes[nodeOrder];
                    int nodeIndex = nodeID - 1;
                    if (!brushTreeSpaceBoundCache.TryGetValue(nodeIndex, out var item))
                        item = default;
                    brushTreeSpaceBounds[nodeOrder] = item;
                }

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID = treeBrushes[nodeOrder];
                    int nodeIndex = nodeID - 1;
                    if (!treeSpaceVerticesCache.TryGetValue(nodeIndex, out var item) || !item.IsCreated)
                        item = BlobAssetReference<BrushTreeSpaceVerticesBlob>.Null;
                    treeSpaceVerticesArray[nodeOrder] = item;
                }

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID = treeBrushes[nodeOrder];
                    int nodeIndex = nodeID - 1;
                    if (brushesTouchedByBrushCache.TryGetValue(nodeIndex, out var item) && item.IsCreated)
                    {
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
                    } else
                    {
                        item = BlobAssetReference<BrushesTouchedByBrush>.Null;
                    }
                    brushesTouchedByBrushes[nodeOrder] = item;
                }

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID = treeBrushes[nodeOrder];
                    int nodeIndex = nodeID - 1;
                    if (transformationCache.TryGetValue(nodeIndex, out var item))
                        transformations[nodeOrder] = item;
                    else
                        // TODO: optimize, only do this when necessary
                        transformations[nodeOrder] = GetNodeTransformation(nodeIndex);
                }

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID = treeBrushes[nodeOrder];
                    int nodeIndex = nodeID - 1;
                    if (!brushTreeSpacePlaneCache.TryGetValue(nodeIndex, out var item) || !item.IsCreated)
                        item = BlobAssetReference<BrushTreeSpacePlanes>.Null;
                    brushTreeSpacePlanes[nodeOrder] = item;
                }

                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID = treeBrushes[nodeOrder];
                    int nodeIndex = nodeID - 1;
                    if (!routingTableCache.TryGetValue(nodeIndex, out var item) || !item.IsCreated)
                        item = BlobAssetReference<RoutingTable>.Null;
                    routingTableLookup[nodeOrder] = item;
                }

                /*
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    int nodeID     = treeBrushes[nodeOrder];
                    int nodeIndex  = nodeID - 1;
                    if (!brushRenderBufferCache.TryGetValue(nodeIndex, out var item) || !item.IsCreated)
                        item = BlobAssetReference<ChiselBrushRenderBuffer>.Null;
                    brushRenderBuffers[i] = item;
                }*/

                Profiler.EndSample();
                #endregion
     
                // TODO: do this in job, build brushMeshList in same job
                #region Build all BrushMeshBlobs
                Profiler.BeginSample("CSG_BrushMeshBlob_Generation");
                ChiselMeshLookup.Update();
                Profiler.EndSample();

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
                    }

                    brushMeshLookup[nodeOrder] = brushMeshID == 0 ? BlobAssetReference<BrushMeshBlob>.Null : brushMeshBlobs[brushMeshID - 1];                    
                }
                #endregion

                
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
                    if (basePolygons[nodeOrder] != null && basePolygons[nodeOrder].IsCreated) basePolygons[nodeOrder].Dispose();
                    basePolygons[nodeOrder] = default;

                    // Remove cached bounding box
                    brushTreeSpaceBounds[nodeOrder] = default;

                    // Remove treeSpace vertices
                    if (treeSpaceVerticesArray[nodeOrder] != null && treeSpaceVerticesArray[nodeOrder].IsCreated) treeSpaceVerticesArray[nodeOrder].Dispose();
                    treeSpaceVerticesArray[nodeOrder] = default;
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

                        var brushTouchedByBrush = brushesTouchedByBrushes[nodeOrder];
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
                    var original = routingTableLookup[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    routingTableLookup[brushIndexOrder.nodeOrder] = default;
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated brushTreeSpacePlanes
                Profiler.BeginSample("CSG_InvalidateBrushTreeSpacePlanes");
                for (int index = 0; index < rebuildTreeBrushIndexOrders.Length; index++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrders[index];
                    var original = brushTreeSpacePlanes[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    brushTreeSpacePlanes[brushIndexOrder.nodeOrder] = default;
                }
                Profiler.EndSample();
                #endregion

                #region Remove invalidated brushesTouchesByBrushes
                Profiler.BeginSample("CSG_InvalidateBrushesTouchesByBrushes");
                for (int index = 0; index < rebuildTreeBrushIndexOrders.Length; index++)
                {
                    var brushIndexOrder = rebuildTreeBrushIndexOrders[index];
                    var original = brushesTouchedByBrushes[brushIndexOrder.nodeOrder];
                    if (original.IsCreated) original.Dispose();
                    brushesTouchedByBrushes[brushIndexOrder.nodeOrder] = default;
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
                        int brushNodeIndex = brushIndexOrder.nodeIndex;
                        if (brushRenderBufferCache.TryGetValue(brushNodeIndex, out var oldBrushRenderBuffer))
                        {
                            if (oldBrushRenderBuffer.IsCreated)
                                oldBrushRenderBuffer.Dispose();
                            brushRenderBufferCache.Remove(brushNodeIndex);
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
                        transformations[nodeIndexOrder.nodeOrder] = GetNodeTransformation(nodeIndexOrder.nodeIndex);
                    }
                }
                Profiler.EndSample();
                #endregion

                #region Build Compact Tree
                var compactTree = chiselLookupValues.compactTree;
                // only rebuild this when the hierarchy changes
                if (anyHierarchyModified ||
                    !compactTree.IsCreated)
                {
                    if (chiselLookupValues.compactTree.IsCreated)
                        chiselLookupValues.compactTree.Dispose();

                    // TODO: jobify?
                    Profiler.BeginSample("CSG_CompactTree.Create");
                    compactTree = CompactTree.Create(CSGManager.nodeHierarchies, treeNodeIndex);
                    chiselLookupValues.compactTree = compactTree;
                    Profiler.EndSample();
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

            // TODO: rewrite code to not need [NativeDisableParallelForRestriction]
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
                        var createTreeSpaceVerticesAndBoundsJob = new CreateTreeSpaceVerticesAndBoundsJob
                        {
                            // Read
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders,
                            brushMeshLookup             = treeUpdate.brushMeshLookup,
                            transformations             = treeUpdate.transformations,

                            // Write
                            brushTreeSpaceBounds        = treeUpdate.brushTreeSpaceBounds,
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,
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
                        var findAllIntersectionsJob = new FindAllBrushIntersectionsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            transformations                 = treeUpdate.transformations,
                            brushMeshLookup                 = treeUpdate.brushMeshLookup,
                            brushTreeSpaceBounds            = treeUpdate.brushTreeSpaceBounds,
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
                        var findAllIntersectionsJob = new FindAllIndirectBrushIntersectionsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            transformations                 = treeUpdate.transformations,
                            brushMeshLookup                 = treeUpdate.brushMeshLookup,
                            brushTreeSpaceBounds            = treeUpdate.brushTreeSpaceBounds,
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
                            brushesTouchedByBrushes = treeUpdate.brushesTouchedByBrushes
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
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVertices2Job
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,

                            // Read / Write
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,
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
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobs
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                            brushMeshLookup             = treeUpdate.brushMeshLookup,
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,

                            // Write
                            basePolygons                = treeUpdate.basePolygons
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
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushMeshLookup         = treeUpdate.brushMeshLookup,
                            transformations         = treeUpdate.transformations,

                            // Write
                            brushTreeSpacePlanes    = treeUpdate.brushTreeSpacePlanes
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
                        var createRoutingTableJob = new CreateRoutingTableJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes = treeUpdate.brushesTouchedByBrushes,
                            compactTree             = treeUpdate.compactTree,

                            // Write
                            routingTableLookup      = treeUpdate.routingTableLookup
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
                        var findBrushPairsJob = new FindBrushPairsJob
                        {
                            // Read
                            maxOrder                    = treeUpdate.allTreeBrushIndexOrders.Length,
#if RUN_IN_SERIAL
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            rebuildTreeBrushIndexOrders = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
                                    
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
                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            uniqueBrushPairs        = treeUpdate.uniqueBrushPairs.AsArray(),
#else
                            uniqueBrushPairs        = treeUpdate.uniqueBrushPairs.AsDeferredJobArray(),
#endif
                            transformations         = treeUpdate.transformations,
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
                        var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Read
                            brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
#if RUN_IN_SERIAL
                            intersectingBrushes         = treeUpdate.intersectingBrushes.AsArray(),
#else
                            intersectingBrushes         = treeUpdate.intersectingBrushes.AsDeferredJobArray(),
#endif
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,
                            
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
                            brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
                            basePolygons                = treeUpdate.basePolygons,

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
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushIndexOrders        = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,

                            // Read / Write
                            treeSpaceVerticesArray      = treeUpdate.treeSpaceVerticesArray,
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
                        var performCSGJob = new PerformCSGJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            routingTableLookup          = treeUpdate.routingTableLookup,
                            brushTreeSpacePlanes        = treeUpdate.brushTreeSpacePlanes,
                            brushesTouchedByBrushes     = treeUpdate.brushesTouchedByBrushes,
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
                                            
                        var chiselLookupValues  = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                        ref var brushRenderBufferCache = ref chiselLookupValues.brushRenderBufferCache;

                        // TODO: Potentially merge this with PerformCSGJob?
                        var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                        {
                            // Read
#if RUN_IN_SERIAL
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsArray(),
#else
                            treeBrushNodeIndexOrders    = treeUpdate.rebuildTreeBrushIndexOrders.AsDeferredJobArray(),
#endif
                            basePolygons                = treeUpdate.basePolygons,
                            transformations             = treeUpdate.transformations,
                            input                       = treeUpdate.dataStream2.AsReader(),

                            // Write
                            brushRenderBufferCache      = brushRenderBufferCache.AsParallelWriter(),
                            //brushRenderBuffers        = treeUpdate.brushRenderBuffers,          // TODO: figure out why this doesn't work w/ incremental updates
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

                        var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                        {
                            meshQueryLength         = treeUpdate.meshQueries.Length,
                            allTreeBrushIndexOrders = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            brushRenderBufferCache  = chiselLookupValues.brushRenderBufferCache,

                            brushRenderData         = treeUpdate.brushRenderData,
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            subMeshCounts           = treeUpdate.subMeshCounts,
                            subMeshSections         = treeUpdate.subMeshSections,
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
                            subMeshSections     = treeUpdate.subMeshSections,
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
                        var treeInfo        = nodeHierarchies[treeUpdate.treeNodeIndex].treeInfo;

                        var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                        {
                            // ReadOnly
                            subMeshSections     = treeUpdate.subMeshSections.AsDeferredJobArray(),

                            // WriteOnly
                            subMeshesArray      = treeInfo.vertexBufferContents.subMeshes,
                            indicesArray        = treeInfo.vertexBufferContents.indices,
                            brushIndicesArray   = treeInfo.vertexBufferContents.brushIndices,
                            positionsArray      = treeInfo.vertexBufferContents.positions,
                            tangentsArray       = treeInfo.vertexBufferContents.tangents,
                            normalsArray        = treeInfo.vertexBufferContents.normals,
                            uv0Array            = treeInfo.vertexBufferContents.uv0
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
                        var treeInfo        = nodeHierarchies[treeUpdate.treeNodeIndex].treeInfo;

                        var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                        {
                            subMeshCounts       = treeUpdate.subMeshCounts.AsDeferredJobArray(),
                            meshDescriptions    = treeInfo.meshDescriptions
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
                        var treeInfo        = nodeHierarchies[treeUpdate.treeNodeIndex].treeInfo;

                        var generateVertexBuffersJob = new FillVertexBuffersJob
                        {
                            subMeshSections     = treeUpdate.subMeshSections.AsDeferredJobArray(),
               
                            subMeshCounts       = treeUpdate.subMeshCounts.AsDeferredJobArray(),
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces.AsDeferredJobArray(),

                            subMeshesArray      = treeInfo.vertexBufferContents.subMeshes,
                            tangentsArray       = treeInfo.vertexBufferContents.tangents,
                            normalsArray        = treeInfo.vertexBufferContents.normals,
                            uv0Array            = treeInfo.vertexBufferContents.uv0,
                            positionsArray      = treeInfo.vertexBufferContents.positions,
                            indicesArray        = treeInfo.vertexBufferContents.indices,
                            brushIndicesArray   = treeInfo.vertexBufferContents.brushIndices,
                        };
                        treeUpdate.fillVertexBuffersJobHandle = generateVertexBuffersJob.Schedule(treeUpdate.subMeshSections, 1, dependencies);
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

                #region Store cached values back into cache (by node Index)
                Profiler.BeginSample("CSG_StoreToCache");
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate          = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;

                    var chiselLookupValues              = ChiselTreeLookup.Value[treeUpdate.treeNodeIndex];
                    ref var transformationCache         = ref chiselLookupValues.transformationCache;
                    ref var basePolygonCache            = ref chiselLookupValues.basePolygonCache;
                    ref var brushTreeSpaceBoundCache    = ref chiselLookupValues.brushTreeSpaceBoundCache;
                    ref var brushTreeSpacePlaneCache    = ref chiselLookupValues.brushTreeSpacePlaneCache;
                    ref var routingTableCache           = ref chiselLookupValues.routingTableCache;
                    ref var brushesTouchedByBrushCache  = ref chiselLookupValues.brushesTouchedByBrushCache;
                    //ref var brushRenderBufferCache    = ref chiselLookupValues.brushRenderBufferCache;
                    ref var treeSpaceVerticesCache      = ref chiselLookupValues.treeSpaceVerticesCache;

                    // TODO: what if there are holes that are not disposed? what if we overwrite something that we didn't dispose?
                    transformationCache         .Clear();
                    basePolygonCache            .Clear();
                    brushTreeSpaceBoundCache    .Clear();
                    brushTreeSpacePlaneCache    .Clear();
                    routingTableCache           .Clear();
                    brushesTouchedByBrushCache  .Clear();
                    //brushRenderBufferCache    .Clear();
                    treeSpaceVerticesCache      .Clear();
                    for (int i = 0; i < treeUpdate.brushCount; i++)
                    {
                        var nodeIndex = treeUpdate.allTreeBrushIndexOrders[i].nodeIndex;
                        transformationCache[nodeIndex]          = treeUpdate.transformations[i];
                        basePolygonCache[nodeIndex]             = treeUpdate.basePolygons[i];
                        brushTreeSpaceBoundCache[nodeIndex]     = treeUpdate.brushTreeSpaceBounds[i];
                        brushTreeSpacePlaneCache[nodeIndex]     = treeUpdate.brushTreeSpacePlanes[i];
                        routingTableCache[nodeIndex]            = treeUpdate.routingTableLookup[i];
                        brushesTouchedByBrushCache[nodeIndex]   = treeUpdate.brushesTouchedByBrushes[i];
                        //brushRenderBufferCache[nodeIndex]     = treeUpdate.brushRenderBuffers[i];
                        treeSpaceVerticesCache[nodeIndex]       = treeUpdate.treeSpaceVerticesArray[i];
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

                        treeUpdate.meshQueries.Dispose();
                        treeUpdate.meshQueries = default;

                        // TODO: put in job?
                        treeUpdate.Dispose(disposeJobHandle, onlyBlobs: true);
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
            if (!UpdateAllTreeMeshes(out JobHandle handle))
                return false;
            handle.Complete();
            return true;
        }
#endregion
    }
}
