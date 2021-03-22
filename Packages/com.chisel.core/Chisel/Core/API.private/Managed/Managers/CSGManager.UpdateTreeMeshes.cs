using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using Profiler = UnityEngine.Profiling.Profiler;

namespace Chisel.Core
{
    partial class CompactHierarchyManager
    {
        #region Update / Rebuild
        static List<CSGTree>    s_AllTrees      = new List<CSGTree>();
        static List<NodeID>     s_TreeNodeIDs   = new List<NodeID>();
        internal static bool UpdateAllTreeMeshes(FinishMeshUpdate finishMeshUpdates, out JobHandle allTrees)
        {
            allTrees = default(JobHandle);
            bool needUpdate = false;

            CompactHierarchyManager.GetAllTrees(s_AllTrees);
            // Check if we have a tree that needs updates
            s_TreeNodeIDs.Clear();
            for (int t = 0; t < s_AllTrees.Count; t++)
            {
                var treeNode = s_AllTrees[t];
                if (treeNode.Valid &&
                    treeNode.IsStatusFlagSet(NodeStatusFlags.TreeNeedsUpdate))
                {
                    s_TreeNodeIDs.Add(treeNode.NodeID);
                    needUpdate = true;
                }
            }

            if (!needUpdate)
                return false;

            // TODO: update "previous siblings" when something with an intersection operation has been modified

            UnityEngine.Profiling.Profiler.BeginSample("UpdateTreeMeshes");
            allTrees = ScheduleTreeMeshJobs(finishMeshUpdates, s_TreeNodeIDs);
            UnityEngine.Profiling.Profiler.EndSample();
            return true;
        }
        #endregion

        internal unsafe static JobHandle ScheduleTreeMeshJobs(FinishMeshUpdate finishMeshUpdates, List<NodeID> treeNodeIDs)
        {
            var finalJobHandle = default(JobHandle);

            // TODO: sort the treeNodeIDs by their position in the hierarchy (so we do everything in the same order, every time)
            // TODO: store all data separately for each tree
            //              have a table between nodeIDs and nodeIndices 
            //              reorder nodes in backend every time a node is added/removed
            //              this ensures 
            //                  everything is sequential in memory
            //                  we don't have gaps between nodes
            //                  order is always predictable

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

            #region Update Unique BrushMeshBlobs
            // This cache stores all brushMeshes of all trees
            Profiler.BeginSample("CSG_BrushMeshBlob_Generation");
            ChiselMeshLookup.Update();
            Profiler.EndSample();
            #endregion

            #region Prepare Trees
            Profiler.BeginSample("CSG_TreeUpdate_Allocate");
            if (s_TreeUpdates == null || s_TreeUpdates.Length < treeNodeIDs.Count)
                s_TreeUpdates = new TreeUpdate[treeNodeIDs.Count];
            Profiler.EndSample();

            var treeUpdateLength = 0;
            for (int t = 0; t < treeNodeIDs.Count; t++)
            {
                ref var currentTree = ref s_TreeUpdates[treeUpdateLength];

                // Make sure that if we, somehow, run this while parts of the previous update is still running, we wait for it to complete
                currentTree.lastJobHandle.Complete();
                currentTree.lastJobHandle = default;

                var treeNodeID          = treeNodeIDs[t];
                var treeCompactNodeID   = CompactHierarchyManager.GetCompactNodeID(treeNodeID);
                if (currentTree.nodes == null) currentTree.nodes = new List<CompactNodeID>(); else currentTree.nodes.Clear();
                if (currentTree.brushes == null) currentTree.brushes = new List<CSGTreeBrush>(); else currentTree.brushes.Clear();
                CompactHierarchyManager.GetTreeNodes(treeNodeID, currentTree.nodes, currentTree.brushes);


                var allTreeBrushes  = currentTree.brushes;
                var nodes           = currentTree.nodes;

                #region MeshQueries
                // TODO: have more control over the queries
                var meshQueries = MeshQuery.DefaultQueries.ToNativeArray(Allocator.TempJob);
                var meshQueriesLength = MeshQuery.DefaultQueries.Length;
                meshQueries.Sort(meshQueryComparer);
                #endregion

                #region Allocations/Resize
                int brushCount = allTreeBrushes.Count;
                var chiselLookupValues = ChiselTreeLookup.Value[treeNodeID];
                chiselLookupValues.EnsureCapacity(brushCount);

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
                if (chiselLookupValues.brushIDValues.Length != brushCount)
                    chiselLookupValues.brushIDValues.ResizeUninitialized(brushCount);
                Profiler.EndSample();

                Profiler.BeginSample("CSG_Allocations");
                Profiler.BeginSample("ENSURE_SIZE");
                currentTree.EnsureSize(brushCount);
                Profiler.EndSample();

                if (!currentTree.subMeshSurfaces.IsCreated) currentTree.subMeshSurfaces = new NativeListArray<SubMeshSurface>(Allocator.Persistent);
                if (!currentTree.subMeshCounts  .IsCreated) currentTree.subMeshCounts   = new NativeList<SubMeshCounts>(Allocator.Persistent);

                if (!currentTree.colliderMeshUpdates.IsCreated) currentTree.colliderMeshUpdates = new NativeList<ChiselMeshUpdate>(Allocator.Persistent);
                if (!currentTree.debugHelperMeshes  .IsCreated) currentTree.debugHelperMeshes   = new NativeList<ChiselMeshUpdate>(Allocator.Persistent);
                if (!currentTree.renderMeshes       .IsCreated) currentTree.renderMeshes        = new NativeList<ChiselMeshUpdate>(Allocator.Persistent);

                currentTree.subMeshSurfaces.ResizeExact(meshQueriesLength);
                for (int i = 0; i < meshQueriesLength; i++)
                    currentTree.subMeshSurfaces.AllocateWithCapacityForIndex(i, 1000);

                currentTree.subMeshCounts.Clear();
                
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
                var nodeIDValueMin = int.MaxValue;
                var nodeIDValueMax = 0;
                if (brushCount > 0)
                { 
                    for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                    {
                        var brush              = allTreeBrushes[nodeOrder];
                        var nodeID             = brush.NodeID;
                        var compactNodeID      = CompactHierarchyManager.GetCompactNodeID(nodeID);
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
                for (int nodeOrder  = 0; nodeOrder  < brushCount; nodeOrder ++)
                {
                    var compactNodeID      = CompactHierarchyManager.GetCompactNodeID(allTreeBrushes[nodeOrder].NodeID);
                    var compactNodeIDValue = compactNodeID.value;
                    s_NodeIDValueToNodeOrderArray[compactNodeIDValue - nodeIDToNodeOrderOffset] = nodeOrder;
                    
                    // We need the index into the tree to ensure deterministic ordering
                    var brushIndexOrder = new IndexOrder { compactNodeID = compactNodeID, nodeOrder = nodeOrder };
                    allTreeBrushIndexOrders[nodeOrder] = brushIndexOrder;
                }

                currentTree.nodeIDValueToNodeOrderArray.Clear();
                ChiselNativeListExtensions.AddRange(currentTree.nodeIDValueToNodeOrderArray, s_NodeIDValueToNodeOrderArray);
                currentTree.nodeIDValueToNodeOrderOffset = nodeIDToNodeOrderOffset;

                Profiler.EndSample();
                #endregion

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

                    for (int n = 0; n < brushCount; n++)
                    {
                        var compactNodeID = allTreeBrushIndexOrders[n].compactNodeID;
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

                    Profiler.BeginSample("check");
                    bool needRemapping = false;
                    var maxCount = math.max(brushCount, previousBrushIDValuesLength) + 1;
                    s_RemovedBrushes.Clear();
                    for (int n = 0; n < previousBrushIDValuesLength; n++)
                    {
                        var sourceID        = brushIDValues[n];
                        var sourceIDValue   = sourceID.value;
                        var sourceOffset    = sourceIDValue - nodeIDToNodeOrderOffset;
                        var destination = (sourceOffset < 0 || sourceOffset >= s_NodeIDValueToNodeOrderArray.Length) ? -1 : s_IndexLookup[sourceOffset] - 1;
                        if (destination == -1)
                        {
                            s_RemovedBrushes.Add(new IndexOrder { compactNodeID = sourceID, nodeOrder = n });
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
                                var otherBrush          = new CSGTreeBrush { brushNodeID = CompactHierarchyManager.GetNodeID(otherBrushID) };

                                if (!otherBrush.Valid)
                                    continue;

                                // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                                if (!allTreeBrushes.Contains(otherBrush))
                                    continue;

                                var otherBrushIDValue   = otherBrushID.value;
                                var otherBrushOrder     = s_NodeIDValueToNodeOrderArray[otherBrushIDValue - nodeIDToNodeOrderOffset];
                                var otherIndexOrder     = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherBrushOrder };
                                brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
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
                for (int i = 0; i < brushCount; i++)
                    chiselLookupValues.brushIDValues[i] = allTreeBrushIndexOrders[i].compactNodeID;
                Profiler.EndSample();
                #endregion

                #region Build list of all brushes that have been modified
                Profiler.BeginSample("Modified_Brushes");
                s_TransformTreeBrushIndicesList.Clear();
                rebuildIndirectTreeBrushIndexOrders.Clear();
                if (rebuildIndirectTreeBrushIndexOrders.Capacity < brushCount)
                    rebuildIndirectTreeBrushIndexOrders.Capacity = brushCount;
                rebuildTreeBrushIndexOrders.Clear();
                if (rebuildTreeBrushIndexOrders.Capacity < brushCount)
                    rebuildTreeBrushIndexOrders.Capacity = brushCount;
                s_TempHashSet.Clear();
                var anyHierarchyModified = false;
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    var brush = allTreeBrushes[nodeOrder];
                    if (brush.IsAnyStatusFlagSet())
                    {
                        var indexOrder = allTreeBrushIndexOrders[nodeOrder];
                        if (!s_TempHashSet.Contains(indexOrder.compactNodeID))
                            rebuildTreeBrushIndexOrders.AddNoResize(indexOrder);
                        
                        // Fix up all flags

                        if (brush.IsStatusFlagSet(NodeStatusFlags.ShapeModified))
                        {
                            // Need to update the basePolygons for this node
                            brush.ClearStatusFlag(NodeStatusFlags.ShapeModified);
                            brush.SetStatusFlag(NodeStatusFlags.NeedAllTouchingUpdated);
                        }

                        if (brush.IsStatusFlagSet(NodeStatusFlags.HierarchyModified))
                        {
                            anyHierarchyModified = true;
                            brush.SetStatusFlag(NodeStatusFlags.NeedAllTouchingUpdated);
                        }

                        if (brush.IsStatusFlagSet(NodeStatusFlags.TransformationModified))
                        {
                            s_TransformTreeBrushIndicesList.Add(new NodeOrderNodeID { nodeOrder = indexOrder.nodeOrder, nodeID = brush.NodeID });
                            brush.ClearStatusFlag(NodeStatusFlags.TransformationModified);
                            brush.SetStatusFlag(NodeStatusFlags.NeedAllTouchingUpdated);
                        }
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
                        var brush       = new CSGTreeBrush { brushNodeID = CompactHierarchyManager.GetNodeID(indexOrder.compactNodeID) };
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
                            var otherBrushID        = brushIntersections[i].nodeIndexOrder.compactNodeID;
                            var otherBrush          = new CSGTreeBrush { brushNodeID = CompactHierarchyManager.GetNodeID(otherBrushID) };

                            if (!otherBrush.Valid)
                                continue;

                            // TODO: investigate how a brush can be "valid" but not be part of treeBrushes
                            if (!allTreeBrushes.Contains(otherBrush))
                                continue;

                            var otherBrushIDValue   = otherBrushID.value;
                            var otherBrushOrder     = s_NodeIDValueToNodeOrderArray[otherBrushIDValue - nodeIDToNodeOrderOffset];
                            var otherIndexOrder     = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherBrushOrder };
                            brushesThatNeedIndirectUpdateHashMap.Add(otherIndexOrder);
                        }
                    }
                    Profiler.EndSample();
                }
                #endregion

                #region Build Transformations
                // TODO: optimize, only do this when necessary
                Profiler.BeginSample("CSG_UpdateBrushTransformations");
                {
                    for (int b = 0; b < s_TransformTreeBrushIndicesList.Count; b++)
                    {
                        var lookup = s_TransformTreeBrushIndicesList[b];
                        transformationCache[lookup.nodeOrder] = CompactHierarchyManager.GetNodeTransformation(lookup.nodeID);
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

                    // TODO: put in job?
                    Profiler.BeginSample("CSG_CompactTree.Create");                    
                    compactTree = CompactTreeBuilder.Create(nodes, allTreeBrushes, treeNodeID);

                    // TODO: put in tree?
                    chiselLookupValues.compactTree = compactTree;
                    Profiler.EndSample();
                }
                Profiler.EndSample();
                #endregion

                #region Build per tree lookup
                Profiler.BeginSample("Init");
                currentTree.treeNodeID          = treeNodeID;
                currentTree.treeCompactNodeID   = treeCompactNodeID;
                currentTree.brushCount          = brushCount;
                currentTree.updateCount         = rebuildTreeBrushIndexOrders.Length;
                currentTree.maxNodeOrder        = allTreeBrushes.Count;
                currentTree.compactTree         = compactTree;
                currentTree.meshQueries         = meshQueries;
                currentTree.meshQueriesLength   = meshQueriesLength;
                Profiler.EndSample();
                #endregion
      
                #region Build all BrushMeshBlobs
                Profiler.BeginSample("CSG_AllBrushMeshInstanceIDs");
                ref var parameters1 = ref chiselLookupValues.parameters1;
                ref var parameters2 = ref chiselLookupValues.parameters2;
                var allKnownBrushMeshIndices    = chiselLookupValues.allKnownBrushMeshIndices;
                var previousMeshIDGeneration    = chiselLookupValues.previousMeshIDGeneration;

                ref var brushMeshBlobGeneration = ref ChiselMeshLookup.Value.brushMeshBlobGeneration;
                ref var allBrushMeshInstanceIDs = ref currentTree.allBrushMeshInstanceIDs;

                bool rebuildParameterList = false;
                s_FoundBrushMeshIndices.Clear();
                // TODO: just store CSGManager.brushInfos[xx].brushMeshInstanceID as a NativeList to begin with
                // TODO: do this in job, build brushMeshList in same job
                for (int nodeOrder = 0; nodeOrder < brushCount; nodeOrder++)
                {
                    var nodeID      = allTreeBrushes[nodeOrder].NodeID;
                    int brushMeshID = 0;
                    if (!CompactHierarchyManager.IsValidNodeID(nodeID) ||
                        // NOTE: Assignment is intended, this is not supposed to be a comparison
                        (brushMeshID = new CSGTreeBrush { brushNodeID = nodeID }.BrushMesh.brushMeshID) == 0)
                    {
                        // The brushMeshID is invalid: a Generator created/didn't update a TreeBrush correctly
                        Debug.LogError($"Brush with ID {nodeID} has its brushMeshID set to {brushMeshID}, which is invalid.");                        
                        allBrushMeshInstanceIDs[nodeOrder] = 0;
                    } else
                    {
                        if (!previousMeshIDGeneration.TryGetValue(brushMeshID, out var currentGeneration))
                        {
                            if (!brushMeshBlobGeneration.TryGetValue(brushMeshID - 1, out var newGeneration))
                                newGeneration = 0;
                            previousMeshIDGeneration[brushMeshID] = newGeneration;
                        } else
                        {
                            if (!brushMeshBlobGeneration.TryGetValue(brushMeshID - 1, out var newGeneration))
                                newGeneration = 0;
                            if (currentGeneration != newGeneration)
                            {
                                // TODO: we do not have the previous parameters for this mesh to unregister .. 
                                //       if we did, we could unregister those, and register the new ones instead
                                rebuildParameterList = true;
                                previousMeshIDGeneration[brushMeshID] = newGeneration;
                            }
                        }
                        allBrushMeshInstanceIDs[nodeOrder] = brushMeshID;
                        s_FoundBrushMeshIndices.Add(brushMeshID - 1);
                    }
                }

                // TODO: optimize all of this, especially slow for complete update
                
                s_RemoveBrushMeshIndices.Clear();
                foreach (var brushMeshIndex in allKnownBrushMeshIndices)
                {
                    if (s_FoundBrushMeshIndices.Contains(brushMeshIndex))
                        s_FoundBrushMeshIndices.Remove(brushMeshIndex);
                    else
                        s_RemoveBrushMeshIndices.Add(brushMeshIndex);
                }
                Profiler.EndSample();
                #endregion

                #region Find all Unique Parameters (Materials/PhysicMaterials)
                Profiler.BeginSample("CSG_FindUniqueParameters");
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                if (rebuildParameterList)
                {
                    foreach (int brushMeshIndex in s_RemoveBrushMeshIndices)
                        allKnownBrushMeshIndices.Remove(brushMeshIndex);
                    parameters1.Clear();
                    parameters2.Clear();
                    foreach (var brushMeshIndex in allKnownBrushMeshIndices)
                    {
                        ref var polygons = ref brushMeshBlobs[brushMeshIndex].Value.polygons;
                        for (int p = 0; p < polygons.Length; p++)
                        {
                            ref var polygon = ref polygons[p];
                            var layerUsage = polygon.layerDefinition.layerUsage;
                            if ((layerUsage & LayerUsageFlags.Renderable) != 0) parameters1.RegisterParameter(polygon.layerDefinition.layerParameter1);
                            if ((layerUsage & LayerUsageFlags.Collidable) != 0) parameters2.RegisterParameter(polygon.layerDefinition.layerParameter2);
                        }
                    }
                } else
                {
                    foreach (int brushMeshIndex in s_RemoveBrushMeshIndices)
                    {
                        ref var polygons = ref brushMeshBlobs[brushMeshIndex].Value.polygons;
                        for (int p = 0; p < polygons.Length; p++)
                        {
                            ref var polygon = ref polygons[p];
                            var layerUsage = polygon.layerDefinition.layerUsage;
                            if ((layerUsage & LayerUsageFlags.Renderable) != 0) parameters1.UnregisterParameter(polygon.layerDefinition.layerParameter1);
                            if ((layerUsage & LayerUsageFlags.Collidable) != 0) parameters2.UnregisterParameter(polygon.layerDefinition.layerParameter2);
                        }
                        allKnownBrushMeshIndices.Remove(brushMeshIndex);
                    }
                    foreach (int brushMeshIndex in s_FoundBrushMeshIndices)
                    {
                        ref var polygons = ref brushMeshBlobs[brushMeshIndex].Value.polygons;
                        for (int p = 0; p < polygons.Length; p++)
                        {
                            ref var polygon = ref polygons[p];
                            var layerUsage = polygon.layerDefinition.layerUsage;
                            if ((layerUsage & LayerUsageFlags.Renderable) != 0) parameters1.RegisterParameter(polygon.layerDefinition.layerParameter1);
                            if ((layerUsage & LayerUsageFlags.Collidable) != 0) parameters2.RegisterParameter(polygon.layerDefinition.layerParameter2);
                        }
                    }
                }
                
                foreach (int brushMeshIndex in s_FoundBrushMeshIndices)
                    allKnownBrushMeshIndices.Add(brushMeshIndex);

                currentTree.parameter1Count = chiselLookupValues.parameters1.uniqueParameterCount;
                currentTree.parameter2Count = chiselLookupValues.parameters2.uniqueParameterCount;
                Profiler.EndSample();
                #endregion

                treeUpdateLength++;

                #region Reset All JobHandles
                currentTree.allBrushMeshInstanceIDsJobHandle = default;
                currentTree.allTreeBrushIndexOrdersJobHandle = default;
                currentTree.allUpdateBrushIndexOrdersJobHandle = default;

                currentTree.basePolygonCacheJobHandle = default;
                currentTree.brushBrushIntersectionsJobHandle = default;
                currentTree.brushesTouchedByBrushCacheJobHandle = default;
                currentTree.brushRenderBufferCacheJobHandle = default;
                currentTree.brushRenderDataJobHandle = default;
                currentTree.brushTreeSpacePlaneCacheJobHandle = default;
                currentTree.brushMeshBlobsLookupJobHandle = default;
                currentTree.brushMeshLookupJobHandle = default;
                currentTree.brushIntersectionsWithJobHandle = default;
                currentTree.brushIntersectionsWithRangeJobHandle = default;
                currentTree.brushesThatNeedIndirectUpdateHashMapJobHandle = default;
                currentTree.brushesThatNeedIndirectUpdateJobHandle = default;
                currentTree.brushTreeSpaceBoundCacheJobHandle = default;

                currentTree.compactTreeJobHandle = default;

                currentTree.dataStream1JobHandle = default;
                currentTree.dataStream2JobHandle = default;

                currentTree.intersectingBrushesStreamJobHandle = default;

                currentTree.loopVerticesLookupJobHandle = default;

                currentTree.meshQueriesJobHandle = default;

                currentTree.nodeIndexToNodeOrderArrayJobHandle = default;
            
                currentTree.outputSurfaceVerticesJobHandle = default;
                currentTree.outputSurfacesJobHandle = default;
                currentTree.outputSurfacesRangeJobHandle = default;

                currentTree.routingTableCacheJobHandle = default;
                currentTree.rebuildTreeBrushIndexOrdersJobHandle = default;

                currentTree.sectionsJobHandle = default;
                currentTree.surfaceCountRefJobHandle = default;
                currentTree.subMeshSurfacesJobHandle = default;
                currentTree.subMeshCountsJobHandle = default;

                currentTree.treeSpaceVerticesCacheJobHandle = default;
                currentTree.transformationCacheJobHandle = default;

                currentTree.uniqueBrushPairsJobHandle = default;

                currentTree.vertexBufferContents_renderDescriptorsJobHandle = default;
                currentTree.vertexBufferContents_colliderDescriptorsJobHandle = default;
                currentTree.vertexBufferContents_subMeshSectionsJobHandle = default;
                currentTree.vertexBufferContents_meshesJobHandle = default;
                currentTree.colliderMeshUpdatesJobHandle = default;
                currentTree.debugHelperMeshesJobHandle = default;
                currentTree.renderMeshesJobHandle = default;

                currentTree.vertexBufferContents_triangleBrushIndicesJobHandle = default;
                currentTree.vertexBufferContents_meshDescriptionsJobHandle = default;

                currentTree.meshDatasJobHandle = default;
                currentTree.storeToCacheJobHandle = default;
                #endregion
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
            
            try
            {
                #region CSG Jobs
                Profiler.BeginSample("CSG_Jobs");

                #region Prepare
                // Create lookup table for all brushMeshBlobs, based on the node order in the tree
                Profiler.BeginSample("Job_FillBrushMeshBlobLookup");
                try
                {
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
                        var fillBrushMeshBlobLookupJob = new FillBrushMeshBlobLookupJob
                        {
                            // Read
                            brushMeshBlobs          = ChiselMeshLookup.Value.brushMeshBlobs,
                            allTreeBrushIndexOrders = treeUpdate.allTreeBrushIndexOrders.AsDeferredJobArray(),
                            allBrushMeshInstanceIDs = treeUpdate.allBrushMeshInstanceIDs,

                            // Write
                            brushMeshLookup         = treeUpdate.brushMeshLookup,
                            surfaceCountRef         = treeUpdate.surfaceCountRef
                        };

                        var currentJobHandle = fillBrushMeshBlobLookupJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
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
                        var currentJobHandle = invalidateBrushCacheJob.Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.nodeIndexToNodeOrderArrayJobHandle,
                                                                          treeUpdate.basePolygonCacheJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var fixupBrushCacheIndicesJob   = new FixupBrushCacheIndicesJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            nodeIDValueToNodeOrderArray     = treeUpdate.nodeIDValueToNodeOrderArray.AsArray().AsReadOnly(),
                            nodeIDValueToNodeOrderOffset    = treeUpdate.nodeIDValueToNodeOrderOffset,

                            // Read Write
                            basePolygonCache                = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),
                            brushesTouchedByBrushCache      = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray()
                        };
                        var currentJobHandle = fixupBrushCacheIndicesJob.Schedule(treeUpdate.allTreeBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
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
                        var currentJobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
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
                        var currentJobHandle = findAllIntersectionsJob.Schedule(treeUpdate.rebuildTreeBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

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
                    // TODO: optimize, use hashed grid
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle,
                                                                          treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                        var createUniqueIndicesArrayJob = new FindUniqueIndirectBrushIntersectionsJob
                        {
                            // Read
                            brushesThatNeedIndirectUpdateHashMap     = treeUpdate.brushesThatNeedIndirectUpdateHashMap,
                        
                            // Write
                            brushesThatNeedIndirectUpdate            = treeUpdate.brushesThatNeedIndirectUpdate
                        };
                        var currentJobHandle = createUniqueIndicesArrayJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

                        //treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateHashMapJobHandle);
                        treeUpdate.brushesThatNeedIndirectUpdateJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.brushesThatNeedIndirectUpdateJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                // Invalidate the cache for the brushes that have been indirectly modified (touch a brush that has changed)
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
                                                                          treeUpdate.brushRenderBufferCacheJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
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
                        var currentJobHandle = invalidateBrushCacheJob.Schedule(treeUpdate.brushesThatNeedIndirectUpdate, 16, dependencies);
                        //currentJobHandle.Complete();

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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
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
                        var currentJobHandle = createTreeSpaceVerticesAndBoundsJob.Schedule(treeUpdate.brushesThatNeedIndirectUpdate, 16, dependencies);
                        //currentJobHandle.Complete();

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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var findAllIntersectionsJob = new FindAllIndirectBrushIntersectionPairsJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            transformationCache             = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup                 = treeUpdate.brushMeshLookup.AsReadOnly(),
                            brushTreeSpaceBounds            = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsDeferredJobArray(),
                            
                            // Read / Write
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections
                        };
                        var currentJobHandle = findAllIntersectionsJob.Schedule(treeUpdate.brushesThatNeedIndirectUpdate, 1, dependencies);
                        //currentJobHandle.Complete();

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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushesThatNeedIndirectUpdateJobHandle,
                                                                          treeUpdate.rebuildTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var findAllIntersectionsJob = new AddIndirectUpdatedBrushesToListAndSortJob
                        {
                            // Read
                            allTreeBrushIndexOrders         = treeUpdate.allTreeBrushIndexOrders.AsArray().AsReadOnly(),
                            brushesThatNeedIndirectUpdate   = treeUpdate.brushesThatNeedIndirectUpdate.AsDeferredJobArray(),
                            rebuildTreeBrushIndexOrders     = treeUpdate.rebuildTreeBrushIndexOrders.AsArray().AsReadOnly(),

                            // Write
                            allUpdateBrushIndexOrders       = treeUpdate.allUpdateBrushIndexOrders.AsParallelWriter(),
                        };
                        var currentJobHandle = findAllIntersectionsJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        
                        var dependencies            = CombineDependencies(treeUpdate.brushBrushIntersectionsJobHandle,
                                                                          treeUpdate.brushIntersectionsWithJobHandle,
                                                                          treeUpdate.brushIntersectionsWithRangeJobHandle);
                        var gatherBrushIntersectionsJob = new GatherBrushIntersectionPairsJob
                        {
                            // Read
                            brushBrushIntersections         = treeUpdate.brushBrushIntersections,

                            // Write
                            brushIntersectionsWith          = treeUpdate.brushIntersectionsWith.GetUnsafeList(),
                            brushIntersectionsWithRange     = treeUpdate.brushIntersectionsWithRange
                        };
                        var currentJobHandle = gatherBrushIntersectionsJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

                        //treeUpdate.brushBrushIntersectionsJobHandle     = CombineDependencies(currentJobHandle, treeUpdate.brushBrushIntersectionsJobHandle);
                        treeUpdate.brushIntersectionsWithJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushIntersectionsWithJobHandle);
                        treeUpdate.brushIntersectionsWithRangeJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushIntersectionsWithRangeJobHandle);

                        dependencies                = CombineDependencies(treeUpdate.compactTreeJobHandle,
                                                                          treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                                          treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushIntersectionsWithJobHandle,
                                                                          treeUpdate.brushIntersectionsWithRangeJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle);                        
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var storeBrushIntersectionsJob = new StoreBrushIntersectionsJob
                        {
                            // Read
                            treeNodeID                  = treeUpdate.treeCompactNodeID,
                            compactTree                 = treeUpdate.compactTree,
                            allTreeBrushIndexOrders     = treeUpdate.allTreeBrushIndexOrders.AsDeferredJobArray(),
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),

                            brushIntersectionsWith      = treeUpdate.brushIntersectionsWith.AsDeferredJobArray(),
                            brushIntersectionsWithRange = treeUpdate.brushIntersectionsWithRange.AsReadOnly(),

                            // Write
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray()
                        };
                        currentJobHandle = storeBrushIntersectionsJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

                        //treeUpdate.compactTreeJobHandle               = CombineDependencies(currentJobHandle, treeUpdate.compactTreeJobHandle);
                        //treeUpdate.allTreeBrushIndexOrdersJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.allTreeBrushIndexOrdersJobHandle);
                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.brushIntersectionsWithJobHandle      = CombineDependencies(currentJobHandle, treeUpdate.brushIntersectionsWithJobHandle);
                        treeUpdate.brushIntersectionsWithRangeJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushIntersectionsWithRangeJobHandle);
                        treeUpdate.brushesTouchedByBrushCacheJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                    }
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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle);

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

                        var chiselLookupValues = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        // Merges original brush vertices together when they are close to avoid t-junctions
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesJob
                        {
                            // Read
                            treeBrushIndexOrders        = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),

                            // Read Write
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                        };
                        var currentJobHandle = mergeTouchingBrushVerticesJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        
                        var dependencies            = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.uniqueBrushPairsJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var findBrushPairsJob       = new FindBrushPairsJob
                        {
                            // Read
                            maxOrder                    = treeUpdate.brushCount,
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                                    
                            // Read (Re-allocate) / Write
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs.GetUnsafeList()
                        };
                        var currentJobHandle = findBrushPairsJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        treeUpdate.uniqueBrushPairsJobHandle             = CombineDependencies(currentJobHandle, treeUpdate.uniqueBrushPairsJobHandle);

                        dependencies                = CombineDependencies(treeUpdate.intersectingBrushesStreamJobHandle,
                                                                          treeUpdate.uniqueBrushPairsJobHandle);

                        currentJobHandle            = NativeStream.ScheduleConstruct(out treeUpdate.intersectingBrushesStream, treeUpdate.uniqueBrushPairs, dependencies, Allocator.TempJob);
                        //currentJobHandle.Complete();

                        //treeUpdate.uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                        treeUpdate.intersectingBrushesStreamJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.intersectingBrushesStreamJobHandle);

                        dependencies                = CombineDependencies(treeUpdate.uniqueBrushPairsJobHandle,
                                                                          treeUpdate.transformationCacheJobHandle,
                                                                          treeUpdate.brushMeshLookupJobHandle,
                                                                          treeUpdate.intersectingBrushesStreamJobHandle);
                        var prepareBrushPairIntersectionsJob = new PrepareBrushPairIntersectionsJob
                        {
                            // Read
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs.AsDeferredJobArray(),
                            transformationCache         = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            brushMeshLookup             = treeUpdate.brushMeshLookup.AsReadOnly(),

                            // Write
                            intersectingBrushesStream   = treeUpdate.intersectingBrushesStream.AsWriter()
                        };
                        currentJobHandle = prepareBrushPairIntersectionsJob.Schedule(treeUpdate.uniqueBrushPairs, 1, dependencies);
                        //currentJobHandle.Complete();

                        //treeUpdate.uniqueBrushPairsJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.uniqueBrushPairsJobHandle);
                        //treeUpdate.transformationCacheJobHandle       = CombineDependencies(currentJobHandle, treeUpdate.transformationCacheJobHandle);
                        //treeUpdate.brushMeshLookupJobHandle           = CombineDependencies(currentJobHandle, treeUpdate.brushMeshLookupJobHandle);
                        treeUpdate.intersectingBrushesStreamJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.intersectingBrushesStreamJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GenerateBasePolygonLoops");
                try
                {
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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var createBlobPolygonsBlobs = new CreateBlobPolygonsBlobsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            brushMeshLookup             = treeUpdate.brushMeshLookup.AsReadOnly(),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),

                            // Write
                            basePolygonCache            = chiselLookupValues.basePolygonCache.AsDeferredJobArray()
                        };
                        var currentJobHandle = createBlobPolygonsBlobs.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var createBrushTreeSpacePlanesJob = new CreateBrushTreeSpacePlanesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushMeshLookup             = treeUpdate.brushMeshLookup.AsReadOnly(),
                            transformationCache         = chiselLookupValues.transformationCache.AsDeferredJobArray(),

                            // Write
                            brushTreeSpacePlanes        = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray()
                        };
                        var currentJobHandle = createBrushTreeSpacePlanesJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        
                        var dependencies            = CombineDependencies(treeUpdate.surfaceCountRefJobHandle,
                                                                          treeUpdate.outputSurfacesJobHandle); 
                        var currentJobHandle        = NativeConstruct.ScheduleSetCapacity(ref treeUpdate.outputSurfaces, treeUpdate.surfaceCountRef, dependencies, Allocator.Persistent);
                        //currentJobHandle.Complete();

                        treeUpdate.outputSurfacesJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesJobHandle);

                        dependencies                = CombineDependencies(treeUpdate.uniqueBrushPairsJobHandle,
                                                                          treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                          treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                                          treeUpdate.intersectingBrushesStreamJobHandle,
                                                                          treeUpdate.outputSurfaceVerticesJobHandle,
                                                                          treeUpdate.outputSurfacesJobHandle);
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var findAllIntersectionLoopsJob = new CreateIntersectionLoopsJob
                        {
                            // Needed for count (forced & unused)
                            uniqueBrushPairs            = treeUpdate.uniqueBrushPairs,

                            // Read
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            treeSpaceVerticesCache      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                            intersectingBrushesStream   = treeUpdate.intersectingBrushesStream.AsReader(),

                            // Write
                            outputSurfaceVertices       = treeUpdate.outputSurfaceVertices.AsParallelWriterExt(),
                            outputSurfaces              = treeUpdate.outputSurfaces.AsParallelWriter()
                        };
                        currentJobHandle = findAllIntersectionLoopsJob.Schedule(treeUpdate.uniqueBrushPairs, 8, dependencies);
                        //currentJobHandle.Complete();
                        var disposeJobHandle = treeUpdate.intersectingBrushesStream.Dispose(currentJobHandle);
                        //disposeJobHandle.Complete();
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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.outputSurfacesJobHandle,
                                                                          treeUpdate.outputSurfacesRangeJobHandle);
                        var gatherOutputSurfacesJob = new GatherOutputSurfacesJob
                        {
                            // Read / Write (Sort)
                            outputSurfaces          = treeUpdate.outputSurfaces.AsDeferredJobArray(),

                            // Write
                            outputSurfacesRange     = treeUpdate.outputSurfacesRange
                        };
                        var currentJobHandle = gatherOutputSurfacesJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

                        treeUpdate.outputSurfacesJobHandle          = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesJobHandle);
                        treeUpdate.outputSurfacesRangeJobHandle     = CombineDependencies(currentJobHandle, treeUpdate.outputSurfacesRangeJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_FindLoopOverlapIntersections");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies            = CombineDependencies(treeUpdate.dataStream1JobHandle,
                                                                          treeUpdate.allUpdateBrushIndexOrdersJobHandle);

                        var currentJobHandle        = NativeStream.ScheduleConstruct(out treeUpdate.dataStream1, treeUpdate.allUpdateBrushIndexOrders, dependencies, Allocator.TempJob);
                        //currentJobHandle.Complete();

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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var findLoopOverlapIntersectionsJob = new FindLoopOverlapIntersectionsJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            outputSurfaceVertices       = treeUpdate.outputSurfaceVertices.AsDeferredJobArray(),
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
                        currentJobHandle = findLoopOverlapIntersectionsJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);
                        //currentJobHandle.Complete();

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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var mergeTouchingBrushVerticesJob = new MergeTouchingBrushVerticesIndirectJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            treeSpaceVerticesArray      = chiselLookupValues.treeSpaceVerticesCache.AsDeferredJobArray(),
                            
                            // Read Write
                            loopVerticesLookup          = treeUpdate.loopVerticesLookup,
                        };
                        var currentJobHandle = mergeTouchingBrushVerticesJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 16, dependencies);
                        //currentJobHandle.Complete();

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
                        // Build categorization trees for brushes
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var createRoutingTableJob   = new CreateRoutingTableJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            brushesTouchedByBrushes     = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            compactTree                 = treeUpdate.compactTree,

                            // Write
                            routingTableLookup          = chiselLookupValues.routingTableCache.AsDeferredJobArray()
                        };
                        var currentJobHandle = createRoutingTableJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);
                        //currentJobHandle.Complete();

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle  = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        //treeUpdate.brushesTouchedByBrushCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.brushesTouchedByBrushCacheJobHandle);
                        //treeUpdate.compactTreeJobHandle                = CombineDependencies(currentJobHandle, treeUpdate.compactTreeJobHandle);
                        treeUpdate.routingTableCacheJobHandle            = CombineDependencies(currentJobHandle, treeUpdate.routingTableCacheJobHandle);
                    }
                } finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_PerformCSG");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var dependencies            = CombineDependencies(treeUpdate.dataStream2JobHandle,
                                                                        treeUpdate.allUpdateBrushIndexOrdersJobHandle);

                        var currentJobHandle        = NativeStream.ScheduleConstruct(out treeUpdate.dataStream2, treeUpdate.allUpdateBrushIndexOrders, dependencies, Allocator.TempJob);
                        //currentJobHandle.Complete();

                        //treeUpdate.allUpdateBrushIndexOrdersJobHandle = CombineDependencies(currentJobHandle, treeUpdate.allUpdateBrushIndexOrdersJobHandle);
                        treeUpdate.dataStream2JobHandle                 = CombineDependencies(currentJobHandle, treeUpdate.dataStream2JobHandle);

                        dependencies                = CombineDependencies(treeUpdate.allUpdateBrushIndexOrdersJobHandle,
                                                                          treeUpdate.routingTableCacheJobHandle,
                                                                          treeUpdate.brushTreeSpacePlaneCacheJobHandle,
                                                                          treeUpdate.brushesTouchedByBrushCacheJobHandle,
                                                                          treeUpdate.dataStream1JobHandle,
                                                                          treeUpdate.loopVerticesLookupJobHandle,
                                                                          treeUpdate.dataStream2JobHandle);
                        // Perform CSG
                        // TODO: determine when a brush is completely inside another brush
                        //		 (might not have any intersection loops)
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var performCSGJob           = new PerformCSGJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            routingTableCache           = chiselLookupValues.routingTableCache.AsDeferredJobArray(),
                            brushTreeSpacePlaneCache    = chiselLookupValues.brushTreeSpacePlaneCache.AsDeferredJobArray(),
                            brushesTouchedByBrushCache  = chiselLookupValues.brushesTouchedByBrushCache.AsDeferredJobArray(),
                            input                       = treeUpdate.dataStream1.AsReader(),
                            loopVerticesLookup          = treeUpdate.loopVerticesLookup,

                            // Write
                            output                      = treeUpdate.dataStream2.AsWriter(),
                        };
                        currentJobHandle = performCSGJob.Schedule(treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);
                        //currentJobHandle.Complete();
                        var disposeJobHandle    = treeUpdate.dataStream1.Dispose(currentJobHandle);
                        //disposeJobHandle.Complete();
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
                        // TODO: Potentially merge this with PerformCSGJob?
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                        var generateSurfaceRenderBuffers = new GenerateSurfaceTrianglesJob
                        {
                            // Read
                            allUpdateBrushIndexOrders   = treeUpdate.allUpdateBrushIndexOrders.AsDeferredJobArray(),
                            basePolygonCache            = chiselLookupValues.basePolygonCache.AsDeferredJobArray(),
                            transformationCache         = chiselLookupValues.transformationCache.AsDeferredJobArray(),
                            input                       = treeUpdate.dataStream2.AsReader(),
                            meshQueries                 = treeUpdate.meshQueries.AsReadOnly(),

                            // Write
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray()
                        };
                        var currentJobHandle = generateSurfaceRenderBuffers.Schedule(treeUpdate.allUpdateBrushIndexOrders, 1, dependencies);
                        //currentJobHandle.Complete();
                        var disposeJobHandle = treeUpdate.dataStream2.Dispose(currentJobHandle);
                        //disposeJobHandle.Complete();
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
                        var chiselLookupValues      = ChiselTreeLookup.Value[treeUpdate.treeNodeID];                        
                        dependencies                = ChiselNativeListExtensions.ScheduleEnsureCapacity(treeUpdate.brushRenderData, treeUpdate.allTreeBrushIndexOrders, dependencies);
                        var findBrushRenderBuffersJob = new FindBrushRenderBuffersJob
                        {
                            // Read
                            meshQueryLength         = treeUpdate.meshQueriesLength,
                            allTreeBrushIndexOrders = treeUpdate.allTreeBrushIndexOrders.AsArray(),
                            brushRenderBufferCache  = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray(),

                            // Write
                            brushRenderData         = treeUpdate.brushRenderData,
                            subMeshCounts           = treeUpdate.subMeshCounts,
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections,
                        };
                        var currentJobHandle = findBrushRenderBuffersJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.meshQueriesJobHandle,
                                                                          treeUpdate.brushRenderDataJobHandle,
                                                                          treeUpdate.sectionsJobHandle,
                                                                          treeUpdate.subMeshSurfacesJobHandle);
                        var prepareJob = new PrepareSubSectionsJob
                        {
                            // Read
                            meshQueries         = treeUpdate.meshQueries.AsReadOnly(),
                            brushRenderData     = treeUpdate.brushRenderData.AsDeferredJobArray(),

                            // Write
                            subMeshSurfaces     = treeUpdate.subMeshSurfaces,
                        };
                        var currentJobHandle    = prepareJob.Schedule(treeUpdate.meshQueriesLength, 1, dependencies);
                        //currentJobHandle.Complete();

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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        
                        var dependencies = CombineDependencies(treeUpdate.sectionsJobHandle,
                                                               treeUpdate.subMeshSurfacesJobHandle,
                                                               treeUpdate.subMeshCountsJobHandle,
                                                               treeUpdate.vertexBufferContents_subMeshSectionsJobHandle);
                        
                        var parallelSortJob = new SortSurfacesParallelJob
                        {
                            // Read
                            meshQueries      = treeUpdate.meshQueries.AsReadOnly(),
                            subMeshSurfaces  = treeUpdate.subMeshSurfaces,

                            // Write
                            subMeshCounts    = treeUpdate.subMeshCounts
                        };
                        var currentJobHandle = parallelSortJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

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
                        currentJobHandle = sortJobGather.Schedule(dependencies);
                        //currentJobHandle.Complete();

                        treeUpdate.subMeshCountsJobHandle                           = CombineDependencies(currentJobHandle, treeUpdate.subMeshCountsJobHandle);
                        treeUpdate.vertexBufferContents_subMeshSectionsJobHandle    = CombineDependencies(currentJobHandle, treeUpdate.vertexBufferContents_subMeshSectionsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_AllocateVertexBuffers");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                                          treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle);
                        var allocateVertexBuffersJob = new AllocateVertexBuffersJob
                        {
                            // Read
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections.AsDeferredJobArray(),

                            // Read Write
                            triangleBrushIndices    = treeUpdate.vertexBufferContents.triangleBrushIndices
                        };
                        var currentJobHandle = allocateVertexBuffersJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

                        treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle   = CombineDependencies(currentJobHandle, treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_GenerateMeshDescription");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;
                        var dependencies            = CombineDependencies(treeUpdate.subMeshCountsJobHandle,
                                                                          treeUpdate.vertexBufferContents_meshDescriptionsJobHandle);
                        var generateMeshDescriptionJob = new GenerateMeshDescriptionJob
                        {
                            // Read
                            subMeshCounts       = treeUpdate.subMeshCounts.AsDeferredJobArray(),

                            // Read Write
                            meshDescriptions    = treeUpdate.vertexBufferContents.meshDescriptions
                        };
                        var currentJobHandle = generateMeshDescriptionJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

                        treeUpdate.vertexBufferContents_meshDescriptionsJobHandle = CombineDependencies(currentJobHandle, treeUpdate.vertexBufferContents_meshDescriptionsJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }

                Profiler.BeginSample("Job_CopyToMeshes");
                try
                {
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
                            var currentJobHandle = assignMeshesJob.Schedule(dependencies);
                            //currentJobHandle.Complete();

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
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections.AsDeferredJobArray(),
                            subMeshCounts           = treeUpdate.subMeshCounts.AsDeferredJobArray(),
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            renderDescriptors       = treeUpdate.vertexBufferContents.renderDescriptors,
                            renderMeshes            = treeUpdate.renderMeshes,

                            // Read/Write
                            triangleBrushIndices    = treeUpdate.vertexBufferContents.triangleBrushIndices,
                            meshes                  = treeUpdate.vertexBufferContents.meshes,
                        };
                        var renderMeshJobHandle = renderCopyToMeshJob.Schedule(treeUpdate.renderMeshes, 1, dependencies);
                        //renderMeshJobHandle.Complete();

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
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections.AsDeferredJobArray(),
                            subMeshCounts           = treeUpdate.subMeshCounts.AsDeferredJobArray(),
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            renderDescriptors       = treeUpdate.vertexBufferContents.renderDescriptors,
                            renderMeshes            = treeUpdate.debugHelperMeshes,

                            // Read/Write
                            triangleBrushIndices    = treeUpdate.vertexBufferContents.triangleBrushIndices,
                            meshes                  = treeUpdate.vertexBufferContents.meshes,
                        };
                        var helperMeshJobHandle = helperCopyToMeshJob.Schedule(treeUpdate.debugHelperMeshes, 1, dependencies);
                        //helperMeshJobHandle.Complete();

                        dependencies = CombineDependencies(treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                           treeUpdate.subMeshCountsJobHandle,
                                                           treeUpdate.subMeshSurfacesJobHandle,

                                                           treeUpdate.vertexBufferContents_colliderDescriptorsJobHandle,
                                                           treeUpdate.colliderMeshUpdatesJobHandle,
                                                           treeUpdate.vertexBufferContents_meshesJobHandle);
                        var colliderCopyToMeshJob = new CopyToColliderMeshJob
                        {
                            // Read
                            subMeshSections         = treeUpdate.vertexBufferContents.subMeshSections.AsDeferredJobArray(),
                            subMeshCounts           = treeUpdate.subMeshCounts.AsDeferredJobArray(),
                            subMeshSurfaces         = treeUpdate.subMeshSurfaces,
                            colliderDescriptors     = treeUpdate.vertexBufferContents.colliderDescriptors,
                            colliderMeshes          = treeUpdate.colliderMeshUpdates,
                            
                            // Read/Write
                            meshes                  = treeUpdate.vertexBufferContents.meshes,
                        };
                        var colliderMeshJobHandle = colliderCopyToMeshJob.Schedule(treeUpdate.colliderMeshUpdates, 16, dependencies);
                        //colliderMeshJobHandle.Complete();

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
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate          = ref s_TreeUpdates[t];
                        if (treeUpdate.updateCount == 0)
                            continue;

                        var chiselLookupValues              = ChiselTreeLookup.Value[treeUpdate.treeNodeID];
                    
                        var dependencies = CombineDependencies(treeUpdate.allTreeBrushIndexOrdersJobHandle,
                                                               treeUpdate.brushTreeSpaceBoundCacheJobHandle,
                                                               treeUpdate.brushRenderBufferCacheJobHandle);
                    
                        var storeToCacheJob = new StoreToCacheJob
                        {
                            // Read
                            allTreeBrushIndexOrders     = treeUpdate.allTreeBrushIndexOrders.AsDeferredJobArray(),
                            brushTreeSpaceBoundCache    = chiselLookupValues.brushTreeSpaceBoundCache.AsDeferredJobArray(),
                            brushRenderBufferCache      = chiselLookupValues.brushRenderBufferCache.AsDeferredJobArray(),

                            // Read Write
                            brushTreeSpaceBoundLookup   = chiselLookupValues.brushTreeSpaceBoundLookup,
                            brushRenderBufferLookup     = chiselLookupValues.brushRenderBufferLookup
                        };
                        var currentJobHandle = storeToCacheJob.Schedule(dependencies);
                        //currentJobHandle.Complete();

                        treeUpdate.storeToCacheJobHandle = CombineDependencies(currentJobHandle, treeUpdate.storeToCacheJobHandle);
                    }
                }
                finally { Profiler.EndSample(); }
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
                        var brush           = new CSGTreeBrush { brushNodeID = CompactHierarchyManager.GetNodeID(brushIndexOrder.compactNodeID) };
                        brush.ClearAllStatusFlags();
                    }

                    var tree    = new CSGTree { treeNodeID = treeUpdate.treeNodeID };
                    tree.ClearStatusFlag(NodeStatusFlags.TreeNeedsUpdate);
                    tree.SetStatusFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                }
                for (int t = 0; t < treeNodeIDs.Count; t++)
                {
                    var tree    = new CSGTree { treeNodeID = treeNodeIDs[t] };
                    tree.ClearStatusFlag(NodeStatusFlags.TreeNeedsUpdate);
                    tree.SetStatusFlag(NodeStatusFlags.TreeMeshNeedsUpdate);
                }
                Profiler.EndSample();
                #endregion

                #region Update Flags (not jobified)
                Profiler.BeginSample("UpdateTreeFlags");
                s_UpdateTrees.Clear();
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];

                    var tree = new CSGTree { treeNodeID = treeUpdate.treeNodeID };
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

                    s_UpdateTrees.Add(tree);
                }
                Profiler.EndSample();
                #endregion

                #region Clear Garbage (not jobified)
                Profiler.BeginSample("ClearGarbage");
                s_TransformTreeBrushIndicesList.Clear();
                Profiler.EndSample();
                #endregion

                //
                // Wait for our scheduled mesh update jobs to finish, ensure our components are setup correctly, and upload our mesh data to the meshes
                //

                #region Finish Mesh Updates / Update Components (not jobified)
                Profiler.BeginSample("FinishMeshUpdates");
                try
                {
                    for (int t = 0; t < treeUpdateLength; t++)
                    {
                        ref var treeUpdate = ref s_TreeUpdates[t];
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
                            var tree = new CSGTree { treeNodeID = treeUpdate.treeNodeID };
                            var usedMeshCount = finishMeshUpdates(tree, ref treeUpdate.vertexBufferContents,
                                                                  treeUpdate.meshDataArray,
                                                                  treeUpdate.colliderMeshUpdates,
                                                                  treeUpdate.debugHelperMeshes,
                                                                  treeUpdate.renderMeshes,
                                                                  dependencies);
                        }
                        dependencies.Complete(); // Whatever happens, our jobs need to be completed at this point
                    }
                } finally { Profiler.EndSample(); }
                #endregion

                #region Dirty all invalidated outlines (not jobified)
                // TODO: Jobify this (has dependencies on jobs, so can't be run before finishMeshUpdates)
                Profiler.BeginSample("CSG_DirtyModifiedOutlines");
                ref var brushMeshBlobs = ref ChiselMeshLookup.Value.brushMeshBlobs;
                for (int t = 0; t < treeUpdateLength; t++)
                {
                    ref var treeUpdate = ref s_TreeUpdates[t];
                    if (treeUpdate.updateCount == 0)
                        continue;
                    treeUpdate.allUpdateBrushIndexOrdersJobHandle.Complete();
                    for (int b = 0; b < treeUpdate.allUpdateBrushIndexOrders.Length; b++)
                    {
                        var brushIndexOrder = treeUpdate.allUpdateBrushIndexOrders[b];
                        var brushNodeID     = brushIndexOrder.compactNodeID;
                        var brush           = new CSGTreeBrush { brushNodeID = CompactHierarchyManager.GetNodeID(brushNodeID) };
                        var brushMeshID     = brush.BrushMesh.brushMeshID;
                        var brushMeshIndex  = brushMeshID - 1;
                        if (ChiselMeshLookup.Value.brushMeshBlobs.TryGetValue(brushMeshIndex, out BlobAssetReference<BrushMeshBlob> item))
                            brush.Outline.Fill(ref item.Value);
                    }
                }
                Profiler.EndSample();
                #endregion

                Profiler.EndSample();
                #endregion
            }
            finally
            {
                #region Free all temporaries
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
                                                        treeUpdate.allTreeBrushIndexOrdersJobHandle,
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
                                                        treeUpdate.surfaceCountRefJobHandle,
                                                        treeUpdate.subMeshSurfacesJobHandle,
                                                        treeUpdate.subMeshCountsJobHandle,
                                                        treeUpdate.treeSpaceVerticesCacheJobHandle,
                                                        treeUpdate.transformationCacheJobHandle,
                                                        treeUpdate.uniqueBrushPairsJobHandle),
                                                    CombineDependencies(
                                                        treeUpdate.vertexBufferContents_renderDescriptorsJobHandle,
                                                        treeUpdate.vertexBufferContents_colliderDescriptorsJobHandle,
                                                        treeUpdate.vertexBufferContents_subMeshSectionsJobHandle,
                                                        treeUpdate.vertexBufferContents_triangleBrushIndicesJobHandle,
                                                        treeUpdate.vertexBufferContents_meshesJobHandle),
                                                    CombineDependencies(
                                                        treeUpdate.colliderMeshUpdatesJobHandle,
                                                        treeUpdate.debugHelperMeshesJobHandle,
                                                        treeUpdate.renderMeshesJobHandle,
                                                        treeUpdate.vertexBufferContents_meshDescriptionsJobHandle,
                                                        treeUpdate.meshDatasJobHandle,
                                                        treeUpdate.storeToCacheJobHandle)
                                                );
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
            public NodeID treeNodeID;
            public CompactNodeID treeCompactNodeID;
            public int brushCount;
            public int updateCount;
            public int maxNodeOrder;
            public int parameter1Count;
            public int parameter2Count;

            public List<CSGTreeBrush>   brushes;
            public List<CompactNodeID>  nodes;

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

                if (brushes == null) brushes = new List<CSGTreeBrush>(); else brushes.Clear();
                if (nodes == null) nodes = new List<CompactNodeID>(); else nodes.Clear();
            }

            // TODO: We're not reusing buffers, so clear is useless?
            //       If we ARE reusing buffers, some allocations are not set to a brush size??
            public unsafe void EnsureSize(int newBrushCount)
            {
                if (this.brushCount == newBrushCount && nodeIDValueToNodeOrderArray.IsCreated)
                {
                    Profiler.BeginSample("CLEAR");
                    Clear();
                    Profiler.EndSample();
                    return;
                }

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
                nodeIDValueToNodeOrderArray       = new NativeList<int>(newBrushCount, Allocator.Persistent);
                brushMeshLookup                 = new NativeArray<BlobAssetReference<BrushMeshBlob>>(newBrushCount, Allocator.Persistent);

                brushBrushIntersections         = new NativeListArray<BrushIntersectWith>(16, Allocator.Persistent);
                brushBrushIntersections.ResizeExact(newBrushCount);
                
                loopVerticesLookup              = new NativeListArray<float3>(brushCount, Allocator.Persistent);
                loopVerticesLookup.ResizeExact(brushCount);
                
                vertexBufferContents.EnsureInitialized();
                
                meshQueries = default;

                Profiler.EndSample();
            }

            public JobHandle Dispose(JobHandle disposeJobHandle)
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
                if (brushRenderData              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushRenderData              .Dispose(disposeJobHandle));
                if (subMeshCounts                .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, subMeshCounts                .Dispose(disposeJobHandle));
                if (subMeshSurfaces              .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, subMeshSurfaces              .Dispose(disposeJobHandle));
                if (allTreeBrushIndexOrders      .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allTreeBrushIndexOrders      .Dispose(disposeJobHandle));
                if (rebuildTreeBrushIndexOrders  .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, rebuildTreeBrushIndexOrders  .Dispose(disposeJobHandle));
                if (allUpdateBrushIndexOrders    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allUpdateBrushIndexOrders    .Dispose(disposeJobHandle));
                if (allBrushMeshInstanceIDs      .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, allBrushMeshInstanceIDs      .Dispose(disposeJobHandle));
                if (uniqueBrushPairs             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, uniqueBrushPairs             .Dispose(disposeJobHandle));
                if (brushIntersectionsWith       .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushIntersectionsWith       .Dispose(disposeJobHandle));
                if (outputSurfaceVertices        .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfaceVertices        .Dispose(disposeJobHandle));
                if (outputSurfaces               .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, outputSurfaces               .Dispose(disposeJobHandle));
                if (brushesThatNeedIndirectUpdate.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdate.Dispose(disposeJobHandle));
                if (nodeIDValueToNodeOrderArray    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, nodeIDValueToNodeOrderArray    .Dispose(disposeJobHandle));
                if (colliderMeshUpdates          .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, colliderMeshUpdates          .Dispose(disposeJobHandle));
                if (debugHelperMeshes            .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, debugHelperMeshes            .Dispose(disposeJobHandle));
                if (renderMeshes                 .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, renderMeshes                 .Dispose(disposeJobHandle));
                if (meshDatas                    .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, meshDatas                    .Dispose(disposeJobHandle));
                Profiler.EndSample();

                Profiler.BeginSample("DISPOSE_HASMAP");
                if (brushesThatNeedIndirectUpdateHashMap.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushesThatNeedIndirectUpdateHashMap.Dispose(disposeJobHandle));
                if (brushBrushIntersections             .IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, brushBrushIntersections             .Dispose(disposeJobHandle));
                Profiler.EndSample();

                if (meshQueries.IsCreated) lastJobHandle = CombineDependencies(lastJobHandle, meshQueries.Dispose(disposeJobHandle));
                meshQueries = default;

                lastJobHandle = CombineDependencies(lastJobHandle, vertexBufferContents.Dispose(disposeJobHandle));
                


                Profiler.BeginSample("DISPOSE_surfaceCountRef");
                surfaceCountRefJobHandle.Complete();
                surfaceCountRefJobHandle = default;
                if (surfaceCountRef.IsCreated) surfaceCountRef.Dispose();
                surfaceCountRef = default;
                Profiler.EndSample();


                vertexBufferContents            = default;
                meshDataArray                   = default;
                meshDatas                       = default;

                brushRenderData                 = default;
                subMeshCounts                   = default;
                subMeshSurfaces                 = default;
                brushMeshLookup                 = default;
                allTreeBrushIndexOrders         = default;
                rebuildTreeBrushIndexOrders     = default;
                allUpdateBrushIndexOrders       = default;
                allBrushMeshInstanceIDs         = default;
                brushBrushIntersections         = default;
                brushIntersectionsWith          = default;
                brushIntersectionsWithRange     = default;
                nodeIDValueToNodeOrderArray       = default;
                brushesThatNeedIndirectUpdate   = default;
                brushesThatNeedIndirectUpdateHashMap = default;
                uniqueBrushPairs                = default;
                outputSurfaceVertices           = default;
                outputSurfaces                  = default;
                outputSurfacesRange             = default;
                meshQueries                     = default;
                
                colliderMeshUpdates             = default;
                debugHelperMeshes               = default;
                renderMeshes                    = default;

                brushCount = 0;

                return lastJobHandle;
            }
        }

        struct NodeOrderNodeID
        {
            public int      nodeOrder;
            public NodeID   nodeID;
        }

        static readonly HashSet<int>            s_FoundBrushMeshIndices         = new HashSet<int>();
        static readonly HashSet<int>            s_RemoveBrushMeshIndices        = new HashSet<int>();
        static readonly HashSet<CompactNodeID>  s_TempHashSet                   = new HashSet<CompactNodeID>();
        static readonly List<IndexOrder>        s_RemovedBrushes                = new List<IndexOrder>();
        static readonly List<NodeOrderNodeID>   s_TransformTreeBrushIndicesList = new List<NodeOrderNodeID>();
        static readonly List<CSGTree>           s_UpdateTrees                   = new List<CSGTree>();
        static int[]                            s_NodeIDValueToNodeOrderArray;
        static TreeUpdate[]                     s_TreeUpdates;

        static int[] s_IndexLookup;
        static int2[] s_RemapOldOrderToNewOrder;

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

            return x.treeNodeID.CompareTo(y.treeNodeID);
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
