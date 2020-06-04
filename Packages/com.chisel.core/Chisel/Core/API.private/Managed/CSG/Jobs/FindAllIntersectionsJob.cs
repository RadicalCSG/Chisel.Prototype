using System;
using System.Collections.Generic;
using System.Linq;
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
    unsafe struct FindAllBrushIntersectionsJob : IJob// IJobParallelFor
    {
        const double kPlaneDistanceEpsilon = CSGConstants.kPlaneDistanceEpsilon;

        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                      allTreeBrushIndexOrders;

        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>        brushMeshLookup;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<NodeTransformations>>  transformations;
        [NoAlias, ReadOnly] public NativeHashMap<int, MinMaxAABB>                               brushTreeSpaceBounds;

        [NoAlias] public NativeList<IndexOrder>                                                 updateBrushIndexOrders;

        [NoAlias, WriteOnly] public NativeMultiHashMap<int, BrushPair>.ParallelWriter           brushBrushIntersections;

        static void TransformOtherIntoBrushSpace(ref float4x4 treeToBrushSpaceMatrix, ref float4x4 brushToTreeSpaceMatrix, ref BlobArray<float4> srcPlanes, float4* dstPlanes)
        {
            var brush1ToBrush0LocalLocalSpace = math.transpose(math.mul(treeToBrushSpaceMatrix, brushToTreeSpaceMatrix));
            for (int plane_index = 0; plane_index < srcPlanes.Length; plane_index++)
            {
                ref var srcPlane = ref srcPlanes[plane_index];
                dstPlanes[plane_index] = math.mul(brush1ToBrush0LocalLocalSpace, srcPlane);
            }
        }

         
        static IntersectionType ConvexPolytopeTouching(BlobAssetReference<BrushMeshBlob> brushMesh0,
                                                       ref float4x4 treeToNode0SpaceMatrix,
                                                       ref float4x4 nodeToTree0SpaceMatrix,
                                                       BlobAssetReference<BrushMeshBlob> brushMesh1,
                                                       ref float4x4 treeToNode1SpaceMatrix,
                                                       ref float4x4 nodeToTree1SpaceMatrix)
        {
            ref var brushPlanes0   = ref brushMesh0.Value.localPlanes;
            ref var brushPlanes1   = ref brushMesh1.Value.localPlanes;

            ref var brushVertices0 = ref brushMesh0.Value.vertices;
            ref var brushVertices1 = ref brushMesh1.Value.vertices;

            var transformedPlanes0 = stackalloc float4[brushPlanes0.Length];
            TransformOtherIntoBrushSpace(ref treeToNode0SpaceMatrix, ref nodeToTree1SpaceMatrix, ref brushPlanes0, transformedPlanes0);
            
            int negativeSides1 = 0;
            for (var i = 0; i < brushPlanes0.Length; i++)
            {
                var plane0 = transformedPlanes0[i];
                int side = WhichSide(ref brushVertices1, plane0, kPlaneDistanceEpsilon);
                if (side < 0) negativeSides1++;
                if (side > 0) return IntersectionType.NoIntersection;
            }

            //if (intersectingSides1 != transformedPlanes0.Length) return IntersectionType.Intersection;
            //if (intersectingSides > 0) return IntersectionType.Intersection;
            //if (positiveSides1 > 0) return IntersectionType.NoIntersection;
            //if (negativeSides > 0 && positiveSides > 0) return IntersectionType.Intersection;
            if (negativeSides1 == brushPlanes0.Length)
                return IntersectionType.BInsideA;

            //*
            var transformedPlanes1 = stackalloc float4[brushPlanes1.Length];
            TransformOtherIntoBrushSpace(ref treeToNode1SpaceMatrix, ref nodeToTree0SpaceMatrix, ref brushPlanes1, transformedPlanes1);

            int negativeSides2 = 0;
            int intersectingSides2 = 0;
            for (var i = 0; i < brushPlanes1.Length; i++)
            {
                var plane1 = transformedPlanes1[i];
                int side = WhichSide(ref brushVertices0, plane1, kPlaneDistanceEpsilon);
                if (side < 0) negativeSides2++;
                if (side > 0) return IntersectionType.NoIntersection;
                if (side == 0) intersectingSides2++;
            }

            if (intersectingSides2 > 0) return IntersectionType.Intersection;
            //if (negativeSides > 0 && positiveSides > 0) return IntersectionType.Intersection;
            if (negativeSides2 == brushPlanes1.Length)
                return IntersectionType.AInsideB;
            
            return IntersectionType.Intersection;//*/
        }

        static int WhichSide(ref BlobArray<float3> vertices, float4 plane, double epsilon)
        {
            {
                var t = math.dot(plane, new float4(vertices[0], 1));
                if (t >=  epsilon) goto HavePositive;
                if (t <= -epsilon) goto HaveNegative;
                return 0;
            }
        HaveNegative:
            for (var i = 1; i < vertices.Length; i++)
            {
                var t = math.dot(plane, new float4(vertices[i], 1));
                if (t > -epsilon)
                    return 0;
            }
            return -1;
        HavePositive:
            for (var i = 1; i < vertices.Length; i++)
            {
                var t = math.dot(plane, new float4(vertices[i], 1));
                if (t < epsilon)
                    return 0;
            }
            return 1;
        }

        public void Execute()
        {
            var updateBrushIndicesArray = updateBrushIndexOrders.AsArray();
            if (allTreeBrushIndexOrders.Length == updateBrushIndexOrders.Length)
            {
                for (int index0 = 0; index0 < updateBrushIndicesArray.Length; index0++)
                {
                    var brush0IndexOrder    = updateBrushIndicesArray[index0];
                    int brush0NodeIndex     = brush0IndexOrder.nodeIndex;
                    for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
                    {
                        var brush1IndexOrder = updateBrushIndicesArray[index1];
                        int brush1NodeIndex  = brush1IndexOrder.nodeIndex;
                        if (brush0NodeIndex <= brush1NodeIndex)
                            continue;
                        var result = FindIntersection(brush0NodeIndex, brush1NodeIndex);
                        StoreIntersection(brush0IndexOrder, brush1IndexOrder, result);
                    }
                }
                return;
            }
            //*

            var brushesThatNeedIndirectUpdate = new NativeList<IndexOrder>(allTreeBrushIndexOrders.Length, Allocator.Temp);
            for (int index0 = 0; index0 < allTreeBrushIndexOrders.Length; index0++)
            {
                var brush0IndexOrder    = allTreeBrushIndexOrders[index0];
                int brush0NodeIndex     = brush0IndexOrder.nodeIndex;
                var found = false;
                for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
                {
                    var brush1IndexOrder = updateBrushIndicesArray[index1];
                    int brush1NodeIndex  = brush1IndexOrder.nodeIndex;
                    var result = FindIntersection(brush0NodeIndex, brush1NodeIndex);
                    if (result == IntersectionType.NoIntersection)
                        continue;
                    found = true;
                    if (brush0NodeIndex > brush1NodeIndex)
                        StoreIntersection(brush0IndexOrder, brush1IndexOrder, result);
                }
                if (found)
                {
                    // TODO: Optimize
                    //if (!updateBrushIndicesArray.Contains(brush0IndexOrder)) // <-- won't work
                    for (int n=0;n< updateBrushIndicesArray.Length;n++)
                    {
                        if (updateBrushIndicesArray[n].nodeIndex == brush0NodeIndex)
                            goto SkipAdd; 
                    }
                        brushesThatNeedIndirectUpdate.Add(brush0IndexOrder);
                    SkipAdd:
                    ;
                }
            }

            if (allTreeBrushIndexOrders.Length == 0)
                return;

            var brushesThatNeedIndirectUpdateArray = brushesThatNeedIndirectUpdate.AsArray();

            updateBrushIndexOrders.AddRange(brushesThatNeedIndirectUpdateArray);

            for (int index0 = 0; index0 < allTreeBrushIndexOrders.Length; index0++)
            {
                var brush0IndexOrder    = allTreeBrushIndexOrders[index0];
                int brush0NodeIndex     = brush0IndexOrder.nodeIndex;
                for (int index1 = 0; index1 < brushesThatNeedIndirectUpdateArray.Length; index1++)
                {
                    var brush1IndexOrder = brushesThatNeedIndirectUpdateArray[index1];
                    int brush1NodeIndex  = brush1IndexOrder.nodeIndex;
                    if (brush0NodeIndex <= brush1NodeIndex)
                        continue;
                    var result = FindIntersection(brush0NodeIndex, brush1NodeIndex);
                    StoreIntersection(brush0IndexOrder, brush1IndexOrder, result);
                }
            }
            //*/

        }

        IntersectionType FindIntersection(int brush0NodeIndex, int brush1NodeIndex)
        {
            var brushMesh0 = brushMeshLookup[brush0NodeIndex];
            var brushMesh1 = brushMeshLookup[brush1NodeIndex];
            if (!brushMesh0.IsCreated || !brushMesh1.IsCreated)
                return IntersectionType.NoIntersection;

            var bounds0 = brushTreeSpaceBounds[brush0NodeIndex];
            var bounds1 = brushTreeSpaceBounds[brush1NodeIndex];

            if (!bounds0.Intersects(bounds1, kPlaneDistanceEpsilon))
                return IntersectionType.NoIntersection;
            
            ref var transformation0 = ref transformations[brush0NodeIndex].Value;
            ref var transformation1 = ref transformations[brush1NodeIndex].Value;

            var treeToNode0SpaceMatrix = transformation0.treeToNode;
            var nodeToTree0SpaceMatrix = transformation0.nodeToTree;
            var treeToNode1SpaceMatrix = transformation1.treeToNode;
            var nodeToTree1SpaceMatrix = transformation1.nodeToTree;

            var result = ConvexPolytopeTouching(brushMesh0,
                                                ref treeToNode0SpaceMatrix,
                                                ref nodeToTree0SpaceMatrix,
                                                brushMesh1,
                                                ref treeToNode1SpaceMatrix,
                                                ref nodeToTree1SpaceMatrix);
          
            return result;
        }

        void StoreIntersection(IndexOrder brush0IndexOrder, IndexOrder brush1IndexOrder, IntersectionType result)
        {
            if (result != IntersectionType.NoIntersection)
            {
                int brush0NodeIndex = brush0IndexOrder.nodeIndex;
                int brush1NodeIndex = brush1IndexOrder.nodeIndex;
                if (result == IntersectionType.Intersection)
                {
                    brushBrushIntersections.Add(brush0NodeIndex, new BrushPair() { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.Intersection });
                    brushBrushIntersections.Add(brush1NodeIndex, new BrushPair() { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.Intersection });
                } else
                if (result == IntersectionType.AInsideB)
                {
                    brushBrushIntersections.Add(brush0NodeIndex, new BrushPair() { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.AInsideB });
                    brushBrushIntersections.Add(brush1NodeIndex, new BrushPair() { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.BInsideA });
                } else
                //if (intersectionType == IntersectionType.BInsideA)
                {
                    brushBrushIntersections.Add(brush0NodeIndex, new BrushPair() { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.BInsideA });
                    brushBrushIntersections.Add(brush1NodeIndex, new BrushPair() { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.AInsideB });
                }
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    unsafe struct StoreBrushIntersectionsJob : IJobParallelFor
    {
        [NoAlias,ReadOnly] public int                                   treeNodeIndex;
        [NoAlias,ReadOnly] public NativeArray<IndexOrder>               treeBrushIndexOrders;
        [NoAlias,ReadOnly] public BlobAssetReference<CompactTree>       compactTree;
        [NoAlias,ReadOnly] public NativeMultiHashMap<int, BrushPair>    brushBrushIntersections;

        [NoAlias,WriteOnly] public NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>>.ParallelWriter brushesTouchedByBrushes;


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
                var otherIndexOrder = otherIntersectionInfo.nodeIndexOrder;
                int otherNodeIndex  = otherIndexOrder.nodeIndex;
                bitset.Set(otherNodeIndex, otherIntersectionInfo.type);
                for (int b = otherIntersectionInfo.bottomUpStart; b < otherIntersectionInfo.bottomUpEnd; b++)
                    bitset.Set(bottomUpNodes[b], IntersectionType.Intersection);
            }
        }
        
        static BlobAssetReference<BrushesTouchedByBrush> GenerateBrushesTouchedByBrush(BlobAssetReference<CompactTree> compactTree, int brushNodeIndex, int rootNodeIndex, NativeMultiHashMap<int, BrushPair>.Enumerator touchingBrushes)
        {
            if (!compactTree.IsCreated)
                return BlobAssetReference<BrushesTouchedByBrush>.Null;

            var indexOffset = compactTree.Value.indexOffset;
            ref var bottomUpNodeIndices         = ref compactTree.Value.bottomUpNodeIndices;
            ref var brushIndexToBottomUpIndex   = ref compactTree.Value.brushIndexToBottomUpIndex;

            // Intersections
            var bitset                      = new BrushIntersectionLookup(indexOffset, bottomUpNodeIndices.Length, Allocator.Temp);
            var brushIntersectionIndices    = new NativeList<BrushIntersectionIndex>(Allocator.Temp);
            var brushIntersections          = new NativeList<BrushIntersection>(Allocator.Temp);
            { 
                var intersectionStart           = brushIntersections.Length;

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
                        nodeIndexOrder  = otherIndexOrder,
                        type            = touchingBrush.type,
                        bottomUpStart   = bottomUpNodeIndices[otherBottomUpIndex].bottomUpStart, 
                        bottomUpEnd     = bottomUpNodeIndices[otherBottomUpIndex].bottomUpEnd
                    });
                }
                var bottomUpIndex = brushIndexToBottomUpIndex[brushNodeIndex - indexOffset];
                brushIntersectionIndices.Add(new BrushIntersectionIndex()
                {
                    nodeIndex           = brushNodeIndex,
                    bottomUpStart       = bottomUpNodeIndices[bottomUpIndex].bottomUpStart,
                    bottomUpEnd         = bottomUpNodeIndices[bottomUpIndex].bottomUpEnd,    
                    intersectionStart   = intersectionStart,
                    intersectionEnd     = brushIntersections.Length
                });

                SetUsedNodesBits(compactTree, brushIntersections, brushNodeIndex, rootNodeIndex, bitset);
            }
            
            var totalBrushIntersectionsSize = 16 + (brushIntersections.Length * UnsafeUtility.SizeOf<BrushIntersection>());
            var totalIntersectionBitsSize   = 16 + (bitset.twoBits.Length * UnsafeUtility.SizeOf<uint>());
            var totalSize                   = totalBrushIntersectionsSize + totalIntersectionBitsSize;


            var builder = new BlobBuilder(Allocator.Temp, totalSize);
            ref var root = ref builder.ConstructRoot<BrushesTouchedByBrush>();

            builder.Construct(ref root.brushIntersections, brushIntersections);
            builder.Construct(ref root.intersectionBits, bitset.twoBits);
            root.Length = bitset.Length;
            root.Offset = bitset.Offset;
            var result = builder.CreateBlobAssetReference<BrushesTouchedByBrush>(Allocator.Persistent);
            builder.Dispose();
            brushIntersectionIndices.Dispose();
            brushIntersections.Dispose();
            bitset.Dispose();
            return result;
        }

        public void Execute(int index)
        {
            var brushIndexOrder     = treeBrushIndexOrders[index];
            int brushNodeIndex      = brushIndexOrder.nodeIndex;
            var brushIntersections  = brushBrushIntersections.GetValuesForKey(brushNodeIndex);
            {
                var result = GenerateBrushesTouchedByBrush(compactTree, brushNodeIndex, treeNodeIndex, brushIntersections);
                if (result.IsCreated)
                    brushesTouchedByBrushes.TryAdd(brushNodeIndex, result);
            }
            brushIntersections.Dispose();
        }
    }
}
