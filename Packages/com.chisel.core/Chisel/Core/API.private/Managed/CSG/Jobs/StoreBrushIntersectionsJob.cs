using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;
using Debug = UnityEngine.Debug;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct StoreBrushIntersectionsJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public CompactNodeID                    treeNodeID;
        [NoAlias, ReadOnly] public BlobAssetReference<CompactTree>  compactTree;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>          allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>          allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BrushIntersectWith>  brushIntersectionsWith;
        [NoAlias, ReadOnly] public NativeArray<int2>.ReadOnly       brushIntersectionsWithRange;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushCache;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeList<BrushIntersection> brushIntersections;

        static void SetUsedNodesBits([NoAlias] BlobAssetReference<CompactTree> compactTree, [NoAlias] in NativeList<BrushIntersection> brushIntersections, CompactNodeID brushNodeID, CompactNodeID rootNodeID, [NoAlias] ref BrushIntersectionLookup bitset)
        {
            var brushNodeIDValue = brushNodeID.value;
            var rootNodeIDValue  = rootNodeID.value;

            bitset.Clear();
            bitset.Set(brushNodeIDValue, IntersectionType.Intersection);
            bitset.Set(rootNodeIDValue, IntersectionType.Intersection);

            ref var compactTreeRef = ref compactTree.Value;
            var minBrushIDValue    = compactTreeRef.minBrushIDValue;
            ref var brushAncestors                  = ref compactTreeRef.brushAncestors;
            ref var brushAncestorLegend             = ref compactTreeRef.brushAncestorLegend;
            ref var brushIDValueToAncestorLegend    = ref compactTreeRef.brushIDValueToAncestorLegend;

            if (brushNodeIDValue < minBrushIDValue || (brushNodeIDValue - minBrushIDValue) >= brushIDValueToAncestorLegend.Length)
            {
                Debug.Log($"nodeIndex is out of bounds {brushNodeIDValue} - {minBrushIDValue} < {brushIDValueToAncestorLegend.Length}");
                return;
            }

            var intersectionIDValue = brushIDValueToAncestorLegend[brushNodeIDValue - minBrushIDValue];
            var intersectionInfo    = brushAncestorLegend[intersectionIDValue];
            for (int b = intersectionInfo.ancestorStartIDValue; b < intersectionInfo.ancestorEndIDValue; b++)
                bitset.Set(brushAncestors[b], IntersectionType.Intersection);

            for (int i = 0; i < brushIntersections.Length; i++)
            {
                var otherIntersectionInfo = brushIntersections[i];
                var otherNodeID           = otherIntersectionInfo.nodeIndexOrder.compactNodeID;
                var otherNodeIDValue      = otherNodeID.value;
                bitset.Set(otherNodeIDValue, otherIntersectionInfo.type);
                for (int b = otherIntersectionInfo.bottomUpStart; b < otherIntersectionInfo.bottomUpEnd; b++)
                    bitset.Set(brushAncestors[b], IntersectionType.Intersection);
            }
        }

        public struct ListComparer : IComparer<BrushIntersection>
        {
            public int Compare(BrushIntersection x, BrushIntersection y)
            {
                var orderX = x.nodeIndexOrder.nodeOrder;
                var orderY = y.nodeIndexOrder.nodeOrder;
                return orderX.CompareTo(orderY);
            }
        }

        static BlobAssetReference<BrushesTouchedByBrush> GenerateBrushesTouchedByBrush([NoAlias, ReadOnly] BlobAssetReference<CompactTree>  compactTree, 
                                                                                       [NoAlias, ReadOnly] NativeArray<IndexOrder>          allTreeBrushIndexOrders, 
                                                                                       IndexOrder brushIndexOrder, CompactNodeID rootNodeID,
                                                                                       [NoAlias, ReadOnly] NativeArray<BrushIntersectWith>  brushIntersectionsWith, int intersectionOffset, int intersectionCount, 
                                                                                       [NoAlias] ref NativeList<BrushIntersection>          brushIntersections)
        {
            if (!compactTree.IsCreated)
                return BlobAssetReference<BrushesTouchedByBrush>.Null;

            var brushNodeID     = brushIndexOrder.compactNodeID;            
            var minBrushIDValue = compactTree.Value.minBrushIDValue;
            var minNodeIDValue  = compactTree.Value.minNodeIDValue;
            var maxNodeIDValue  = compactTree.Value.maxNodeIDValue;
            ref var brushAncestorLegend             = ref compactTree.Value.brushAncestorLegend;
            ref var brushIDValueToAncestorLegend    = ref compactTree.Value.brushIDValueToAncestorLegend;

            // Intersections
            NativeCollectionHelpers.EnsureCapacityAndClear(ref brushIntersections, intersectionCount);

            {
                for (int i = 0; i < intersectionCount; i++)
                {
                    var touchingBrush = brushIntersectionsWith[intersectionOffset + i];
                    //Debug.Assert(touchingBrush.brushIndexOrder0.nodeOrder == brushNodeOrder);

                    var otherIndexOrder     = touchingBrush.brushNodeOrder1;
                    var otherBrushID        = allTreeBrushIndexOrders[otherIndexOrder].compactNodeID;
                    var otherBrushIDValue   = otherBrushID.value;
                    if ((otherBrushIDValue < minBrushIDValue || (otherBrushIDValue - minBrushIDValue) >= brushIDValueToAncestorLegend.Length))
                        continue;
                    
                    var otherBottomUpIDValue    = brushIDValueToAncestorLegend[otherBrushIDValue - minBrushIDValue];
                    brushIntersections.AddNoResize(new BrushIntersection
                    {
                        nodeIndexOrder  = new IndexOrder { compactNodeID = otherBrushID, nodeOrder = otherIndexOrder },
                        type            = touchingBrush.type,
                        bottomUpStart   = brushAncestorLegend[otherBottomUpIDValue].ancestorStartIDValue, 
                        bottomUpEnd     = brushAncestorLegend[otherBottomUpIDValue].ancestorEndIDValue
                    });
                }
                for (int b0 = 0; b0 < brushIntersections.Length; b0++)
                {
                    var brushIntersection0 = brushIntersections[b0];
                    ref var nodeIndexOrder0 = ref brushIntersection0.nodeIndexOrder;
                    for (int b1 = b0 + 1; b1 < brushIntersections.Length; b1++)
                    {
                        var brushIntersection1 = brushIntersections[b1];
                        ref var nodeIndexOrder1 = ref brushIntersection1.nodeIndexOrder;
                        if (nodeIndexOrder0.nodeOrder > nodeIndexOrder1.nodeOrder)
                        {
                            var t = nodeIndexOrder0;
                            nodeIndexOrder0 = nodeIndexOrder1;
                            nodeIndexOrder1 = t;
                        }
                        brushIntersections[b1] = brushIntersection1;
                    }
                    brushIntersections[b0] = brushIntersection0;
                }
            }

            // TODO: replace with NativeBitArray
            var bitset = new BrushIntersectionLookup(minNodeIDValue, (maxNodeIDValue - minNodeIDValue) + 1, Allocator.Temp);
            SetUsedNodesBits(compactTree, in brushIntersections, brushNodeID, rootNodeID, ref bitset);
            
            var totalBrushIntersectionsSize = 16 + (brushIntersections.Length * UnsafeUtility.SizeOf<BrushIntersection>());
            var totalIntersectionBitsSize   = 16 + (bitset.twoBits.Length * UnsafeUtility.SizeOf<uint>());
            var totalSize                   = totalBrushIntersectionsSize + totalIntersectionBitsSize;

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushesTouchedByBrush>();

            builder.Construct(ref root.brushIntersections, brushIntersections);
            builder.Construct(ref root.intersectionBits, bitset.twoBits);
            root.BitCount = bitset.Length;
            root.BitOffset = bitset.Offset;
            var result = builder.CreateBlobAssetReference<BrushesTouchedByBrush>(Allocator.Persistent);
            builder.Dispose();
            brushIntersections.Dispose();
            bitset.Dispose();
            return result;
        }

        public void Execute(int index)
        {
            var brushIndexOrder     = allUpdateBrushIndexOrders[index];
            int brushNodeOrder      = brushIndexOrder.nodeOrder;
            {
                var result = GenerateBrushesTouchedByBrush(compactTree, 
                                                           allTreeBrushIndexOrders, brushIndexOrder, treeNodeID,
                                                           brushIntersectionsWith, 
                                                           intersectionOffset: brushIntersectionsWithRange[brushNodeOrder].x, 
                                                           intersectionCount:  brushIntersectionsWithRange[brushNodeOrder].y, ref brushIntersections);
                if (result.IsCreated)
                    brushesTouchedByBrushCache[brushNodeOrder] = result;
            }
        }
    }
}
