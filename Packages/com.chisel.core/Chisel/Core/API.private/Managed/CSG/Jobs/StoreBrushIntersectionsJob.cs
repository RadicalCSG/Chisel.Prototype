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
        [NoAlias, ReadOnly] public int                              treeNodeIndex;
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

        static void SetUsedNodesBits([NoAlias] BlobAssetReference<CompactTree> compactTree, [NoAlias] in NativeList<BrushIntersection> brushIntersections, int brushNodeIndex, int rootNodeIndex, [NoAlias] ref BrushIntersectionLookup bitset)
        {
            bitset.Clear();
            bitset.Set(brushNodeIndex, IntersectionType.Intersection);
            bitset.Set(rootNodeIndex, IntersectionType.Intersection);

            ref var compactTreeRef = ref compactTree.Value;
            var minBrushIndex                   = compactTreeRef.minBrushIndex;
            ref var brushAncestors              = ref compactTreeRef.brushAncestors;
            ref var brushAncestorLegend         = ref compactTreeRef.brushAncestorLegend;
            ref var brushIndexToAncestorLegend  = ref compactTreeRef.brushIndexToAncestorLegend;

            if (brushNodeIndex < minBrushIndex || (brushNodeIndex - minBrushIndex) >= brushIndexToAncestorLegend.Length)
            {
                Debug.Log($"nodeIndex is out of bounds {brushNodeIndex} - {minBrushIndex} < {brushIndexToAncestorLegend.Length}");
                return;
            }

            var intersectionIndex   = brushIndexToAncestorLegend[brushNodeIndex - minBrushIndex];
            var intersectionInfo    = brushAncestorLegend[intersectionIndex];
            for (int b = intersectionInfo.ancestorStartIndex; b < intersectionInfo.ancestorEndIndex; b++)
                bitset.Set(brushAncestors[b], IntersectionType.Intersection);

            for (int i = 0; i < brushIntersections.Length; i++)
            {
                var otherIntersectionInfo   = brushIntersections[i];
                int otherNodeIndex          = otherIntersectionInfo.nodeIndexOrder.nodeIndex;
                bitset.Set(otherNodeIndex, otherIntersectionInfo.type);
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
                                                                                       [NoAlias, ReadOnly] NativeArray<IndexOrder>          allTreeBrushIndexOrders, IndexOrder brushIndexOrder, int rootNodeIndex,
                                                                                       [NoAlias, ReadOnly] NativeArray<BrushIntersectWith>  brushIntersectionsWith, int intersectionOffset, int intersectionCount, 
                                                                                       [NoAlias] ref NativeList<BrushIntersection>          brushIntersections)
        {
            if (!compactTree.IsCreated)
                return BlobAssetReference<BrushesTouchedByBrush>.Null;

            int brushNodeIndex = brushIndexOrder.nodeIndex;
            //int brushNodeOrder = brushIndexOrder.nodeOrder;

            var minBrushIndex = compactTree.Value.minBrushIndex;
            var minNodeIndex = compactTree.Value.minNodeIndex;
            var maxNodeIndex = compactTree.Value.maxNodeIndex;
            ref var brushAncestorLegend         = ref compactTree.Value.brushAncestorLegend;
            ref var brushIndexToAncestorLegend  = ref compactTree.Value.brushIndexToAncestorLegend;

            // Intersections

            if (!brushIntersections.IsCreated)
            {
                brushIntersections  = new NativeList<BrushIntersection>(intersectionCount, Allocator.Temp);
            } else
            {
                brushIntersections.Clear();
                if (brushIntersections.Capacity < intersectionCount)
                    brushIntersections.Capacity = intersectionCount;
            }

            {
                for (int i = 0; i < intersectionCount; i++)
                {
                    var touchingBrush = brushIntersectionsWith[intersectionOffset + i];
                    //Debug.Assert(touchingBrush.brushIndexOrder0.nodeOrder == brushNodeOrder);

                    var otherIndexOrder = touchingBrush.brushNodeOrder1;
                    var otherBrushIndex = allTreeBrushIndexOrders[otherIndexOrder].nodeIndex;
                    if ((otherBrushIndex < minBrushIndex || (otherBrushIndex - minBrushIndex) >= brushIndexToAncestorLegend.Length))
                        continue;

                    var otherBottomUpIndex = brushIndexToAncestorLegend[otherBrushIndex - minBrushIndex];
                    brushIntersections.AddNoResize(new BrushIntersection
                    {
                        nodeIndexOrder  = new IndexOrder { nodeOrder = otherIndexOrder, nodeIndex = otherBrushIndex },
                        type            = touchingBrush.type,
                        bottomUpStart   = brushAncestorLegend[otherBottomUpIndex].ancestorStartIndex, 
                        bottomUpEnd     = brushAncestorLegend[otherBottomUpIndex].ancestorEndIndex
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
            var bitset = new BrushIntersectionLookup(minNodeIndex, (maxNodeIndex - minNodeIndex) + 1, Allocator.Temp);
            SetUsedNodesBits(compactTree, in brushIntersections, brushNodeIndex, rootNodeIndex, ref bitset);
            
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
                                                           allTreeBrushIndexOrders, brushIndexOrder, treeNodeIndex,
                                                           brushIntersectionsWith, 
                                                           intersectionOffset: brushIntersectionsWithRange[brushNodeOrder].x, 
                                                           intersectionCount:  brushIntersectionsWithRange[brushNodeOrder].y, ref brushIntersections);
                if (result.IsCreated)
                    brushesTouchedByBrushCache[brushNodeOrder] = result;
            }
        }
    }
}
