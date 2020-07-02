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
    struct FindAllBrushIntersectionsJob : IJobParallelFor
    {
        const double kBoundsDistanceEpsilon = CSGConstants.kBoundsDistanceEpsilon;

        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                              allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>       brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                     transformations;
        [NoAlias, ReadOnly] public NativeArray<MinMaxAABB>                              brushTreeSpaceBounds;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                              updateBrushIndexOrders;

        // Write
        [NoAlias, WriteOnly] public NativeList<BrushPair>.ParallelWriter                brushBrushIntersections;
        [NoAlias, WriteOnly] public NativeHashMap<int, IndexOrder>.ParallelWriter       brushesThatNeedIndirectUpdate;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       transformedPlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       transformedPlanes1;
        [NativeDisableContainerSafetyRestriction] NativeBitArray            foundBrushes;

        static void TransformOtherIntoBrushSpace(ref float4x4 treeToBrushSpaceMatrix, ref float4x4 brushToTreeSpaceMatrix, ref BlobArray<float4> srcPlanes, NativeArray<float4> dstPlanes)
        {
            var brush1ToBrush0LocalLocalSpace = math.transpose(math.mul(treeToBrushSpaceMatrix, brushToTreeSpaceMatrix));
            for (int plane_index = 0; plane_index < srcPlanes.Length; plane_index++)
            {
                ref var srcPlane = ref srcPlanes[plane_index];
                dstPlanes[plane_index] = math.mul(brush1ToBrush0LocalLocalSpace, srcPlane);
            }
        }

         
        IntersectionType ConvexPolytopeTouching(ref BrushMeshBlob brushMesh0,
                                                ref float4x4 treeToNode0SpaceMatrix,
                                                ref float4x4 nodeToTree0SpaceMatrix,
                                                ref BrushMeshBlob brushMesh1,
                                                ref float4x4 treeToNode1SpaceMatrix,
                                                ref float4x4 nodeToTree1SpaceMatrix)
        {
            ref var brushPlanes0   = ref brushMesh0.localPlanes;
            
            if (!transformedPlanes0.IsCreated || transformedPlanes0.Length < brushPlanes0.Length)
            {
                if (transformedPlanes0.IsCreated) transformedPlanes0.Dispose();
                transformedPlanes0 = new NativeArray<float4>(brushPlanes0.Length, Allocator.Temp);
            }
            TransformOtherIntoBrushSpace(ref treeToNode0SpaceMatrix, ref nodeToTree1SpaceMatrix, ref brushPlanes0, transformedPlanes0);

            ref var brushVertices1 = ref brushMesh1.localVertices;
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

            ref var brushPlanes1    = ref brushMesh1.localPlanes;
            //*
            
            if (!transformedPlanes1.IsCreated || transformedPlanes1.Length < brushPlanes1.Length)
            {
                if (transformedPlanes1.IsCreated) transformedPlanes1.Dispose();
                transformedPlanes1 = new NativeArray<float4>(brushPlanes1.Length, Allocator.Temp);
            }
            TransformOtherIntoBrushSpace(ref treeToNode1SpaceMatrix, ref nodeToTree0SpaceMatrix, ref brushPlanes1, transformedPlanes1);


            ref var brushVertices0 = ref brushMesh0.localVertices;
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
        
        public void Execute(int index1)
        {
            if (allTreeBrushIndexOrders.Length == updateBrushIndexOrders.Length)
            {
                //for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
                {
                    var brush1IndexOrder = updateBrushIndexOrders[index1];
                    int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                    for (int index0 = 0; index0 < updateBrushIndexOrders.Length; index0++)
                    {
                        var brush0IndexOrder    = updateBrushIndexOrders[index0];
                        int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                        if (brush0NodeOrder <= brush1NodeOrder)
                            continue;
                        var result = FindIntersection(brush0NodeOrder, brush1NodeOrder);
                        if (result == IntersectionType.NoIntersection)
                            continue;
                        StoreIntersection(brush0IndexOrder, brush1IndexOrder, result);
                    }
                }
                return;
            }
            //*

            if (!foundBrushes.IsCreated || foundBrushes.Length < allTreeBrushIndexOrders.Length)
                foundBrushes = new NativeBitArray(allTreeBrushIndexOrders.Length, Allocator.Temp);
            foundBrushes.Clear();
            // TODO: figure out a way to avoid needing this
            for (int a = 0; a < updateBrushIndexOrders.Length; a++)
                foundBrushes.Set(updateBrushIndexOrders[a].nodeOrder, true);

            //for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
            {
                var brush1IndexOrder = updateBrushIndexOrders[index1];
                int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                for (int index0 = 0; index0 < allTreeBrushIndexOrders.Length; index0++)
                {
                    var brush0IndexOrder    = allTreeBrushIndexOrders[index0];
                    int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                    var found = foundBrushes.IsSet(brush0NodeOrder);
                    if (brush0NodeOrder <= brush1NodeOrder && found)
                        continue;
                    var result = FindIntersection(brush0NodeOrder, brush1NodeOrder);
                    if (result == IntersectionType.NoIntersection)
                        continue;
                    if (!found)
                    {
                        brushesThatNeedIndirectUpdate.TryAdd(brush0IndexOrder.nodeOrder, brush0IndexOrder);
                        //foundBrushes.Set(brush0NodeOrder, true);
                    }
                    if (brush0NodeOrder > brush1NodeOrder)
                        StoreIntersection(brush0IndexOrder, brush1IndexOrder, result);
                }
            }
            //*/

        }

        IntersectionType FindIntersection(int brush0NodeOrder, int brush1NodeOrder)
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

            var result = ConvexPolytopeTouching(ref brushMesh0.Value,
                                                ref treeToNode0SpaceMatrix,
                                                ref nodeToTree0SpaceMatrix,
                                                ref brushMesh1.Value,
                                                ref treeToNode1SpaceMatrix,
                                                ref nodeToTree1SpaceMatrix);
          
            return result;
        }

        void StoreIntersection(IndexOrder brush0IndexOrder, IndexOrder brush1IndexOrder, IntersectionType result)
        {
            if (result != IntersectionType.NoIntersection)
            {
                if (result == IntersectionType.Intersection)
                {
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.Intersection });
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.Intersection });
                } else
                if (result == IntersectionType.AInsideB)
                {
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.AInsideB });
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.BInsideA });
                } else
                //if (intersectionType == IntersectionType.BInsideA)
                {
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.BInsideA });
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.AInsideB });
                }
            }
        }
    
    }

    // TODO: make this a parallel job somehow
    [BurstCompile(CompileSynchronously = true)]
    struct FindAllIndirectBrushIntersectionsJob : IJob// IJobParallelFor
    {
        const double kBoundsDistanceEpsilon = CSGConstants.kBoundsDistanceEpsilon;

        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                              allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>       brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>                     transformations;
        [NoAlias, ReadOnly] public NativeArray<MinMaxAABB>                              brushTreeSpaceBounds;
        [NoAlias, ReadOnly] public NativeHashMap<int, IndexOrder>                       brushesThatNeedIndirectUpdate;
        
        // Read/Write
        [NoAlias] public NativeList<IndexOrder>                                         updateBrushIndexOrders;

        // Write
        [NoAlias, WriteOnly] public NativeList<BrushPair>.ParallelWriter    brushBrushIntersections;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       transformedPlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       transformedPlanes1;

        public void Execute()
        {
            var brushesThatNeedIndirectUpdateArray = brushesThatNeedIndirectUpdate.GetValueArray(Allocator.Temp);
            updateBrushIndexOrders.AddRange(brushesThatNeedIndirectUpdateArray);

            var comparer = new IndexOrderComparer();
            updateBrushIndexOrders.Sort(comparer);

            for (int index1 = 0; index1 < brushesThatNeedIndirectUpdateArray.Length; index1++)
            {
                var brush1IndexOrder = brushesThatNeedIndirectUpdateArray[index1];
                int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                for (int index0 = 0; index0 < allTreeBrushIndexOrders.Length; index0++)
                {
                    var brush0IndexOrder    = allTreeBrushIndexOrders[index0];
                    int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                    if (brush0NodeOrder <= brush1NodeOrder)
                        continue;
                    var result = FindIntersection(brush0NodeOrder, brush1NodeOrder);
                    if (result == IntersectionType.NoIntersection)
                        continue;
                    StoreIntersection(brush0IndexOrder, brush1IndexOrder, result);
                }
            }
        }

        struct IndexOrderComparer : IComparer<IndexOrder>
        {
            public int Compare(IndexOrder x, IndexOrder y)
            {
                return x.nodeOrder.CompareTo(y.nodeOrder);
            }
        }
        

        IntersectionType FindIntersection(int brush0NodeOrder, int brush1NodeOrder)
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

            var result = ConvexPolytopeTouching(ref brushMesh0.Value,
                                                ref treeToNode0SpaceMatrix,
                                                ref nodeToTree0SpaceMatrix,
                                                ref brushMesh1.Value,
                                                ref treeToNode1SpaceMatrix,
                                                ref nodeToTree1SpaceMatrix);
          
            return result;
        }

        void StoreIntersection(IndexOrder brush0IndexOrder, IndexOrder brush1IndexOrder, IntersectionType result)
        {
            if (result != IntersectionType.NoIntersection)
            {
                if (result == IntersectionType.Intersection)
                {
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.Intersection });
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.Intersection });
                } else
                if (result == IntersectionType.AInsideB)
                {
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.AInsideB });
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.BInsideA });
                } else
                //if (intersectionType == IntersectionType.BInsideA)
                {
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush0IndexOrder, brushIndexOrder1 = brush1IndexOrder, type = IntersectionType.BInsideA });
                    brushBrushIntersections.AddNoResize(new BrushPair { brushIndexOrder0 = brush1IndexOrder, brushIndexOrder1 = brush0IndexOrder, type = IntersectionType.AInsideB });
                }
            }
        }    
        
        IntersectionType ConvexPolytopeTouching(ref BrushMeshBlob brushMesh0,
                                                ref float4x4 treeToNode0SpaceMatrix,
                                                ref float4x4 nodeToTree0SpaceMatrix,
                                                ref BrushMeshBlob brushMesh1,
                                                ref float4x4 treeToNode1SpaceMatrix,
                                                ref float4x4 nodeToTree1SpaceMatrix)
        {
            ref var brushPlanes0   = ref brushMesh0.localPlanes;
            
            if (!transformedPlanes0.IsCreated || transformedPlanes0.Length < brushPlanes0.Length)
            {
                if (transformedPlanes0.IsCreated) transformedPlanes0.Dispose();
                transformedPlanes0 = new NativeArray<float4>(brushPlanes0.Length, Allocator.Temp);
            }
            TransformOtherIntoBrushSpace(ref treeToNode0SpaceMatrix, ref nodeToTree1SpaceMatrix, ref brushPlanes0, transformedPlanes0);

            ref var brushVertices1 = ref brushMesh1.localVertices;
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

            ref var brushPlanes1    = ref brushMesh1.localPlanes;
            //*
            
            if (!transformedPlanes1.IsCreated || transformedPlanes1.Length < brushPlanes1.Length)
            {
                if (transformedPlanes1.IsCreated) transformedPlanes1.Dispose();
                transformedPlanes1 = new NativeArray<float4>(brushPlanes1.Length, Allocator.Temp);
            }
            TransformOtherIntoBrushSpace(ref treeToNode1SpaceMatrix, ref nodeToTree0SpaceMatrix, ref brushPlanes1, transformedPlanes1);


            ref var brushVertices0 = ref brushMesh0.localVertices;
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

        static void TransformOtherIntoBrushSpace(ref float4x4 treeToBrushSpaceMatrix, ref float4x4 brushToTreeSpaceMatrix, ref BlobArray<float4> srcPlanes, NativeArray<float4> dstPlanes)
        {
            var brush1ToBrush0LocalLocalSpace = math.transpose(math.mul(treeToBrushSpaceMatrix, brushToTreeSpaceMatrix));
            for (int plane_index = 0; plane_index < srcPlanes.Length; plane_index++)
            {
                ref var srcPlane = ref srcPlanes[plane_index];
                dstPlanes[plane_index] = math.mul(brush1ToBrush0LocalLocalSpace, srcPlane);
            }
        }
    }
}
