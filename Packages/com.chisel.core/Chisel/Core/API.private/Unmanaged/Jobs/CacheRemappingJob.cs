using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile]
    unsafe struct CacheRemappingJob : IJob
    {
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
        public void InitializeHierarchy(ref CompactHierarchy hierarchy)
        {
            compactHierarchyPtr = (CompactHierarchy*)UnsafeUtility.AddressOf(ref hierarchy);
        }

        [NativeDisableUnsafePtrRestriction]
        [NoAlias, ReadOnly] public CompactHierarchy*                    compactHierarchyPtr;
        [NoAlias, ReadOnly] public NativeList<int>                      nodeIDValueToNodeOrderArray;
        [NoAlias, ReadOnly] public NativeReference<int>                 nodeIDValueToNodeOrderOffsetRef;
        [NoAlias, ReadOnly] public NativeList<CompactNodeID>            brushes;
        [NoAlias, ReadOnly] public int                                  brushCount;
        [NoAlias, ReadOnly] public NativeList<IndexOrder>               allTreeBrushIndexOrders;

        // Read/Write
        [NoAlias] public NativeList<CompactNodeID>                                   brushIDValues;
        [NoAlias] public NativeList<BlobAssetReference<BasePolygonsBlob>>            basePolygonCache;
        [NoAlias] public NativeList<BlobAssetReference<RoutingTable>>                routingTableCache;
        [NoAlias] public NativeList<NodeTransformations>                             transformationCache;
        [NoAlias] public NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>     brushRenderBufferCache;
        [NoAlias] public NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>  treeSpaceVerticesCache;
        [NoAlias] public NativeList<BlobAssetReference<BrushTreeSpacePlanes>>        brushTreeSpacePlaneCache;
        [NoAlias] public NativeList<MinMaxAABB>                                      brushTreeSpaceBoundCache;
        [NoAlias] public NativeList<BlobAssetReference<BrushesTouchedByBrush>>       brushesTouchedByBrushCache;

        // Write
        [NoAlias, WriteOnly] public NativeHashSet<IndexOrder>           brushesThatNeedIndirectUpdateHashMap;
        [NoAlias, WriteOnly] public NativeReference<bool>               needRemappingRef;

        public void Execute()
        {
            ref var compactHierarchy = ref UnsafeUtility.AsRef<CompactHierarchy>(compactHierarchyPtr);
            CacheRemapping(ref compactHierarchy,
                            nodeIDValueToNodeOrderArray, nodeIDValueToNodeOrderOffsetRef,
                            brushes, brushCount,
                            allTreeBrushIndexOrders,
                            brushIDValues,
                            basePolygonCache,
                            routingTableCache,
                            transformationCache,
                            brushRenderBufferCache,
                            treeSpaceVerticesCache,
                            brushTreeSpacePlaneCache,
                            brushTreeSpaceBoundCache,
                            brushesTouchedByBrushCache,
                            brushesThatNeedIndirectUpdateHashMap,
                            needRemappingRef);
        }
        
        public static void CacheRemapping([NoAlias, ReadOnly] ref CompactHierarchy                              compactHierarchy,
                                          [NoAlias, ReadOnly] NativeList<int>                                   nodeIDValueToNodeOrderArray,
                                          [NoAlias, ReadOnly] NativeReference<int>                              nodeIDValueToNodeOrderOffsetRef,
                                          [NoAlias, ReadOnly] NativeList<CompactNodeID>                         brushes,
                                          [NoAlias, ReadOnly] int                                               brushCount,
                                          [NoAlias, ReadOnly] NativeList<IndexOrder>                            allTreeBrushIndexOrders,

                                          // Read/Write
                                          [NoAlias] NativeList<CompactNodeID>                                   brushIDValues,
                                          [NoAlias] NativeList<BlobAssetReference<BasePolygonsBlob>>            basePolygonCache,
                                          [NoAlias] NativeList<BlobAssetReference<RoutingTable>>                routingTableCache,
                                          [NoAlias] NativeList<NodeTransformations>                             transformationCache,
                                          [NoAlias] NativeList<BlobAssetReference<ChiselBrushRenderBuffer>>     brushRenderBufferCache,
                                          [NoAlias] NativeList<BlobAssetReference<BrushTreeSpaceVerticesBlob>>  treeSpaceVerticesCache,
                                          [NoAlias] NativeList<BlobAssetReference<BrushTreeSpacePlanes>>        brushTreeSpacePlaneCache,
                                          [NoAlias] NativeList<MinMaxAABB>                                      brushTreeSpaceBoundCache,
                                          [NoAlias] NativeList<BlobAssetReference<BrushesTouchedByBrush>>       brushesTouchedByBrushCache,

                                          // Write
                                          [NoAlias, WriteOnly] NativeHashSet<IndexOrder>                        brushesThatNeedIndirectUpdateHashMap,
                                          [NoAlias, WriteOnly] NativeReference<bool>                            needRemappingRef)
        {
            var needRemapping = false;
            var nodeIDValueToNodeOrderOffset = nodeIDValueToNodeOrderOffsetRef.Value;

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
                    var offsetIDValue = compactNodeID.value - nodeIDValueToNodeOrderOffset;
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
                            var sourceOffset    = sourceIDValue - nodeIDValueToNodeOrderOffset;
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
                                    var otherBrushOrder     = nodeIDValueToNodeOrderArray[otherBrushIDValue - nodeIDValueToNodeOrderOffset];
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

            needRemappingRef.Value = needRemapping;
        }
    }
}
