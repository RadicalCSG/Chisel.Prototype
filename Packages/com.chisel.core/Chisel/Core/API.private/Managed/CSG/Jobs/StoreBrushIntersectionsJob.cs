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
        [NoAlias,ReadOnly] public int                                   treeNodeIndex;
        [NoAlias,ReadOnly] public NativeArray<IndexOrder>               treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>                     nodeIndexToNodeOrder;
        [NoAlias, ReadOnly] public int                                  nodeIndexToNodeOrderOffset;
        [NoAlias,ReadOnly] public BlobAssetReference<CompactTree>       compactTree;
        [NoAlias,ReadOnly] public NativeMultiHashMap<int, BrushPair>    brushBrushIntersections;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias,WriteOnly] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushes;


        static void SetUsedNodesBits(BlobAssetReference<CompactTree> compactTree, NativeList<BrushIntersection> brushIntersections, int brushNodeIndex, int rootNodeIndex, BrushIntersectionLookup bitset)
        {
            bitset.Clear();
            bitset.Set(brushNodeIndex, IntersectionType.Intersection);
            bitset.Set(rootNodeIndex, IntersectionType.Intersection);

            var indexOffset = compactTree.Value.indexOffset;
            ref var bottomUpNodes               = ref compactTree.Value.bottomUpNodes;
            ref var bottomUpNodeIndices         = ref compactTree.Value.bottomUpNodeIndices;
            ref var brushIndexToBottomUpIndex   = ref compactTree.Value.brushIndexToBottomUpIndex;

            var intersectionIndex   = brushIndexToBottomUpIndex[brushNodeIndex - indexOffset];
            var intersectionInfo    = bottomUpNodeIndices[intersectionIndex];
            for (int b = intersectionInfo.bottomUpStart; b < intersectionInfo.bottomUpEnd; b++)
                bitset.Set(bottomUpNodes[b], IntersectionType.Intersection);

            for (int i = 0; i < brushIntersections.Length; i++)
            {
                var otherIntersectionInfo = brushIntersections[i];
                int otherNodeIndex = otherIntersectionInfo.nodeIndex;
                bitset.Set(otherNodeIndex, otherIntersectionInfo.type);
                for (int b = otherIntersectionInfo.bottomUpStart; b < otherIntersectionInfo.bottomUpEnd; b++)
                    bitset.Set(bottomUpNodes[b], IntersectionType.Intersection);
            }
        }

        public struct ListComparer : IComparer<BrushIntersection>
        {
            public NativeArray<int> nodeIndexToNodeOrder;
            public int              nodeIndexToNodeOrderOffset;

            public int Compare(BrushIntersection x, BrushIntersection y)
            {
                var orderX = nodeIndexToNodeOrder[x.nodeIndex - nodeIndexToNodeOrderOffset];
                var orderY = nodeIndexToNodeOrder[y.nodeIndex - nodeIndexToNodeOrderOffset];
                return orderX.CompareTo(orderY);
            }
        }

        static BlobAssetReference<BrushesTouchedByBrush> GenerateBrushesTouchedByBrush(BlobAssetReference<CompactTree> compactTree, int brushNodeIndex, int rootNodeIndex, NativeMultiHashMap<int, BrushPair>.Enumerator touchingBrushes, ListComparer comparer)
        {
            if (!compactTree.IsCreated)
                return BlobAssetReference<BrushesTouchedByBrush>.Null;

            var indexOffset = compactTree.Value.indexOffset;
            ref var bottomUpNodeIndices         = ref compactTree.Value.bottomUpNodeIndices;
            ref var brushIndexToBottomUpIndex   = ref compactTree.Value.brushIndexToBottomUpIndex;

            // Intersections
            var bitset                      = new BrushIntersectionLookup(indexOffset, bottomUpNodeIndices.Length, Allocator.Temp);
            //var brushIntersectionIndices    = new NativeList<BrushIntersectionIndex>(Allocator.Temp);
            var brushIntersections          = new NativeList<BrushIntersection>(Allocator.Temp);
            { 
                //var intersectionStart           = brushIntersections.Length;

                while (touchingBrushes.MoveNext())
                {
                    var touchingBrush   = touchingBrushes.Current;
                    var otherIndexOrder = touchingBrush.brushIndexOrder1;
                    int otherBrushIndex = otherIndexOrder.nodeIndex;
                    if ((otherBrushIndex < indexOffset || (otherBrushIndex-indexOffset) >= brushIndexToBottomUpIndex.Length))
                        continue;

                    var otherBottomUpIndex = brushIndexToBottomUpIndex[otherBrushIndex - indexOffset];
                    brushIntersections.Add(new BrushIntersection()
                    { 
                        nodeIndex       = otherIndexOrder.nodeIndex,
                        type            = touchingBrush.type,
                        bottomUpStart   = bottomUpNodeIndices[otherBottomUpIndex].bottomUpStart, 
                        bottomUpEnd     = bottomUpNodeIndices[otherBottomUpIndex].bottomUpEnd
                    });
                }
                /*var bottomUpIndex = brushIndexToBottomUpIndex[brushNodeIndex - indexOffset];
                
                brushIntersectionIndices.Add(new BrushIntersectionIndex()
                {
                    nodeIndex           = brushNodeIndex,
                    bottomUpStart       = bottomUpNodeIndices[bottomUpIndex].bottomUpStart,
                    bottomUpEnd         = bottomUpNodeIndices[bottomUpIndex].bottomUpEnd,    
                    intersectionStart   = intersectionStart,
                    intersectionEnd     = brushIntersections.Length
                });*/

                SetUsedNodesBits(compactTree, brushIntersections, brushNodeIndex, rootNodeIndex, bitset);
            }
            
            var totalBrushIntersectionsSize = 16 + (brushIntersections.Length * UnsafeUtility.SizeOf<BrushIntersection>());
            var totalIntersectionBitsSize   = 16 + (bitset.twoBits.Length * UnsafeUtility.SizeOf<uint>());
            var totalSize                   = totalBrushIntersectionsSize + totalIntersectionBitsSize;

            brushIntersections.Sort(comparer);

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
            int brushNodeIndex      = brushIndexOrder.nodeIndex;
            int brushNodeOrder      = brushIndexOrder.nodeOrder;
            var brushIntersections  = brushBrushIntersections.GetValuesForKey(brushNodeIndex);
            {
                var comparer = new ListComparer
                {
                    nodeIndexToNodeOrder        = nodeIndexToNodeOrder,
                    nodeIndexToNodeOrderOffset  = nodeIndexToNodeOrderOffset
                };

                var result = GenerateBrushesTouchedByBrush(compactTree, brushNodeIndex, treeNodeIndex, brushIntersections, comparer);
                if (result.IsCreated)
                    brushesTouchedByBrushes[brushNodeOrder] = result;
            }
            brushIntersections.Dispose();
        }
    }
}
