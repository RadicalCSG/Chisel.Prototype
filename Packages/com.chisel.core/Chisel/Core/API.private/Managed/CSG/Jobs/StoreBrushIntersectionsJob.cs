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
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>          treeBrushIndexOrders;
        [NoAlias, ReadOnly] public BlobAssetReference<CompactTree>  compactTree;
        [NoAlias, ReadOnly] public NativeArray<int2>                brushBrushIntersectionRange;
        [NoAlias, ReadOnly] public NativeArray<BrushPair>           brushBrushIntersections;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushes;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeList<BrushIntersection> brushIntersections;

        static void SetUsedNodesBits(BlobAssetReference<CompactTree> compactTree, NativeList<BrushIntersection> brushIntersections, int brushNodeIndex, int rootNodeIndex, BrushIntersectionLookup bitset)
        {
            bitset.Clear();
            bitset.Set(brushNodeIndex, IntersectionType.Intersection);
            bitset.Set(rootNodeIndex, IntersectionType.Intersection);

            ref var compactTreeRef = ref compactTree.Value;
            var indexOffset                     = compactTreeRef.indexOffset;
            ref var bottomUpNodes               = ref compactTreeRef.bottomUpNodes;
            ref var bottomUpNodeIndices         = ref compactTreeRef.bottomUpNodeIndices;
            ref var brushIndexToBottomUpIndex   = ref compactTreeRef.brushIndexToBottomUpIndex;

            if (brushNodeIndex < indexOffset || brushNodeIndex >= brushIndexToBottomUpIndex.Length)
            {
                Debug.Log($"nodeIndex is out of bounds {brushNodeIndex} - {indexOffset} < {brushIndexToBottomUpIndex.Length} | {compactTreeRef.brushCount}");
                return;
            }

            var intersectionIndex   = brushIndexToBottomUpIndex[brushNodeIndex - indexOffset];
            var intersectionInfo    = bottomUpNodeIndices[intersectionIndex];
            for (int b = intersectionInfo.bottomUpStart; b < intersectionInfo.bottomUpEnd; b++)
                bitset.Set(bottomUpNodes[b], IntersectionType.Intersection);

            for (int i = 0; i < brushIntersections.Length; i++)
            {
                var otherIntersectionInfo = brushIntersections[i];
                int otherNodeIndex = otherIntersectionInfo.nodeIndexOrder.nodeIndex;
                bitset.Set(otherNodeIndex, otherIntersectionInfo.type);
                for (int b = otherIntersectionInfo.bottomUpStart; b < otherIntersectionInfo.bottomUpEnd; b++)
                    bitset.Set(bottomUpNodes[b], IntersectionType.Intersection);
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

        BlobAssetReference<BrushesTouchedByBrush> GenerateBrushesTouchedByBrush(BlobAssetReference<CompactTree> compactTree, IndexOrder brushIndexOrder, int rootNodeIndex, 
                                                                                NativeArray<BrushPair> touchingBrushes, int intersectionOffset, int intersectionCount)
        {
            if (!compactTree.IsCreated)
                return BlobAssetReference<BrushesTouchedByBrush>.Null;

            int brushNodeIndex = brushIndexOrder.nodeIndex;
            int brushNodeOrder = brushIndexOrder.nodeOrder;

            var indexOffset = compactTree.Value.indexOffset;
            ref var bottomUpNodeIndices         = ref compactTree.Value.bottomUpNodeIndices;
            ref var brushIndexToBottomUpIndex   = ref compactTree.Value.brushIndexToBottomUpIndex;

            // Intersections

            // TODO: replace with NativeBitArray
            var bitset              = new BrushIntersectionLookup(indexOffset, bottomUpNodeIndices.Length, Allocator.Temp);
            
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
                    var touchingBrush = touchingBrushes[intersectionOffset + i];
                    //Debug.Assert(touchingBrush.brushIndexOrder0.nodeOrder == brushNodeOrder);

                    var otherIndexOrder = touchingBrush.brushIndexOrder1;
                    int otherBrushIndex = otherIndexOrder.nodeIndex;
                    if ((otherBrushIndex < indexOffset || (otherBrushIndex-indexOffset) >= brushIndexToBottomUpIndex.Length))
                        continue;

                    var otherBottomUpIndex = brushIndexToBottomUpIndex[otherBrushIndex - indexOffset];
                    brushIntersections.AddNoResize(new BrushIntersection
                    {
                        nodeIndexOrder  = otherIndexOrder,
                        type            = touchingBrush.type,
                        bottomUpStart   = bottomUpNodeIndices[otherBottomUpIndex].bottomUpStart, 
                        bottomUpEnd     = bottomUpNodeIndices[otherBottomUpIndex].bottomUpEnd
                    });
                }

                SetUsedNodesBits(compactTree, brushIntersections, brushNodeIndex, rootNodeIndex, bitset);
            }
            
            var totalBrushIntersectionsSize = 16 + (brushIntersections.Length * UnsafeUtility.SizeOf<BrushIntersection>());
            var totalIntersectionBitsSize   = 16 + (bitset.twoBits.Length * UnsafeUtility.SizeOf<uint>());
            var totalSize                   = totalBrushIntersectionsSize + totalIntersectionBitsSize;

            //brushIntersections.Sort(new ListComparer());

            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushesTouchedByBrush>();

            builder.Construct(ref root.brushIntersections, brushIntersections);
            builder.Construct(ref root.intersectionBits, bitset.twoBits);
            root.Length = bitset.Length;
            root.Offset = bitset.Offset;
            var result = builder.CreateBlobAssetReference<BrushesTouchedByBrush>(Allocator.Persistent);
            builder.Dispose();
            //brushIntersectionIndices.Dispose();
            brushIntersections.Dispose();
            bitset.Dispose();
            return result;
        }

        public void Execute(int index)
        {
            var brushIndexOrder     = treeBrushIndexOrders[index];
            int brushNodeOrder      = brushIndexOrder.nodeOrder;
            {
                var result = GenerateBrushesTouchedByBrush(compactTree, brushIndexOrder, treeNodeIndex, brushBrushIntersections, 
                                                           intersectionOffset: brushBrushIntersectionRange[brushNodeOrder].x, intersectionCount: brushBrushIntersectionRange[brushNodeOrder].y);
                if (result.IsCreated)
                    brushesTouchedByBrushes[brushNodeOrder] = result;
            }
        }
    }
}
