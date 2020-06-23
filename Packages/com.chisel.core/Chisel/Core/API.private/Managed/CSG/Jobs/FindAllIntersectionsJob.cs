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
    struct FindAllBrushIntersectionsJob : IJob// IJobParallelFor
    {
        const double kBoundsDistanceEpsilon = CSGConstants.kBoundsDistanceEpsilon;

        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                              allTreeBrushIndexOrders;

        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>       brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                     transformations;
        [NoAlias, ReadOnly] public NativeArray<MinMaxAABB>                              brushTreeSpaceBounds;

        [NoAlias] public NativeList<IndexOrder>                                         updateBrushIndexOrders;

        [NoAlias, WriteOnly] public NativeMultiHashMap<int, BrushPair>.ParallelWriter   brushBrushIntersections;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float4> transformedPlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4> transformedPlanes1;

        static void TransformOtherIntoBrushSpace(ref float4x4 treeToBrushSpaceMatrix, ref float4x4 brushToTreeSpaceMatrix, ref BlobArray<float4> srcPlanes, NativeArray<float4> dstPlanes)
        {
            var brush1ToBrush0LocalLocalSpace = math.transpose(math.mul(treeToBrushSpaceMatrix, brushToTreeSpaceMatrix));
            for (int plane_index = 0; plane_index < srcPlanes.Length; plane_index++)
            {
                ref var srcPlane = ref srcPlanes[plane_index];
                dstPlanes[plane_index] = math.mul(brush1ToBrush0LocalLocalSpace, srcPlane);
            }
        }

         
        IntersectionType ConvexPolytopeTouching(BlobAssetReference<BrushMeshBlob> brushMesh0,
                                                ref float4x4 treeToNode0SpaceMatrix,
                                                ref float4x4 nodeToTree0SpaceMatrix,
                                                BlobAssetReference<BrushMeshBlob> brushMesh1,
                                                ref float4x4 treeToNode1SpaceMatrix,
                                                ref float4x4 nodeToTree1SpaceMatrix)
        {
            ref var brushPlanes0   = ref brushMesh0.Value.localPlanes;
            ref var brushPlanes1   = ref brushMesh1.Value.localPlanes;

            ref var brushVertices0 = ref brushMesh0.Value.localVertices;
            ref var brushVertices1 = ref brushMesh1.Value.localVertices;
            
            if (!transformedPlanes0.IsCreated || transformedPlanes0.Length < brushPlanes0.Length)
            {
                if (transformedPlanes0.IsCreated) transformedPlanes0.Dispose();
                transformedPlanes0 = new NativeArray<float4>(brushPlanes0.Length, Allocator.Temp);
            }

            //var transformedPlanes0 = stackalloc float4[brushPlanes0.Length];
            TransformOtherIntoBrushSpace(ref treeToNode0SpaceMatrix, ref nodeToTree1SpaceMatrix, ref brushPlanes0, transformedPlanes0);
            
            int negativeSides1 = 0;
            for (var i = 0; i < brushPlanes0.Length; i++)
            {
                var plane0 = transformedPlanes0[i];
                int side = WhichSide(ref brushVertices1, plane0, kBoundsDistanceEpsilon);
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
            
            if (!transformedPlanes1.IsCreated || transformedPlanes1.Length < brushPlanes1.Length)
            {
                if (transformedPlanes1.IsCreated) transformedPlanes1.Dispose();
                transformedPlanes1 = new NativeArray<float4>(brushPlanes1.Length, Allocator.Temp);
            }

            //var transformedPlanes1 = stackalloc float4[brushPlanes1.Length];
            TransformOtherIntoBrushSpace(ref treeToNode1SpaceMatrix, ref nodeToTree0SpaceMatrix, ref brushPlanes1, transformedPlanes1);

            int negativeSides2 = 0;
            int intersectingSides2 = 0;
            for (var i = 0; i < brushPlanes1.Length; i++)
            {
                var plane1 = transformedPlanes1[i];
                int side = WhichSide(ref brushVertices0, plane1, kBoundsDistanceEpsilon);
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
        struct IndexOrderComparer : IComparer<IndexOrder>
        {
            public int Compare(IndexOrder x, IndexOrder y)
            {
                return x.nodeOrder.CompareTo(y.nodeOrder);
            }
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
                    int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                    for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
                    {
                        var brush1IndexOrder = updateBrushIndicesArray[index1];
                        int brush1NodeIndex  = brush1IndexOrder.nodeIndex;
                        int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                        if (brush0NodeOrder <= brush1NodeOrder)
                            continue;
                        var result = FindIntersection(brush0NodeIndex, brush0NodeOrder, brush1NodeIndex, brush1NodeOrder);
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
                int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                var found = false;
                for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
                {
                    var brush1IndexOrder = updateBrushIndicesArray[index1];
                    int brush1NodeIndex  = brush1IndexOrder.nodeIndex;
                    int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                    var result = FindIntersection(brush0NodeIndex, brush0NodeOrder, brush1NodeIndex, brush1NodeOrder);
                    if (result == IntersectionType.NoIntersection)
                        continue;
                    found = true;
                    if (brush0NodeOrder > brush1NodeOrder)
                        StoreIntersection(brush0IndexOrder, brush1IndexOrder, result);
                }
                if (found)
                {
                    if (!updateBrushIndicesArray.Contains(brush0IndexOrder))
                        brushesThatNeedIndirectUpdate.Add(brush0IndexOrder);
                }
            }

            if (allTreeBrushIndexOrders.Length == 0)
                return;

            var brushesThatNeedIndirectUpdateArray = brushesThatNeedIndirectUpdate.AsArray();

            updateBrushIndexOrders.AddRange(brushesThatNeedIndirectUpdateArray);

            var comparer = new IndexOrderComparer();
            updateBrushIndexOrders.Sort(comparer);

            for (int index0 = 0; index0 < allTreeBrushIndexOrders.Length; index0++)
            {
                var brush0IndexOrder    = allTreeBrushIndexOrders[index0];
                int brush0NodeIndex     = brush0IndexOrder.nodeIndex;
                int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                for (int index1 = 0; index1 < brushesThatNeedIndirectUpdateArray.Length; index1++)
                {
                    var brush1IndexOrder = brushesThatNeedIndirectUpdateArray[index1];
                    int brush1NodeIndex  = brush1IndexOrder.nodeIndex;
                    int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                    if (brush0NodeOrder <= brush1NodeOrder)
                        continue;
                    var result = FindIntersection(brush0NodeIndex, brush0NodeOrder, brush1NodeIndex, brush1NodeOrder);
                    StoreIntersection(brush0IndexOrder, brush1IndexOrder, result);
                }
            }
            //*/

        }

        IntersectionType FindIntersection(int brush0NodeIndex, int brush0NodeOrder, int brush1NodeIndex, int brush1NodeOrder)
        {
            var brushMesh0 = brushMeshLookup[brush0NodeOrder];
            var brushMesh1 = brushMeshLookup[brush1NodeOrder];
            if (!brushMesh0.IsCreated || !brushMesh1.IsCreated)
                return IntersectionType.NoIntersection;

            var bounds0 = brushTreeSpaceBounds[brush0NodeOrder];
            var bounds1 = brushTreeSpaceBounds[brush1NodeOrder];

            if (!bounds0.Intersects(bounds1, kBoundsDistanceEpsilon))
                return IntersectionType.NoIntersection;
            
            var transformation0 = transformations[brush0NodeOrder];
            var transformation1 = transformations[brush1NodeOrder];

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
}
