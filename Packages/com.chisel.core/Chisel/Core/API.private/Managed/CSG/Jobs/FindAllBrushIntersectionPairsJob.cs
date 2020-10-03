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
    struct FindAllBrushIntersectionPairsJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>.ReadOnly             allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>.ReadOnly brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>             transformationCache;
        [NoAlias, ReadOnly] public NativeArray<MinMaxAABB>                      brushTreeSpaceBounds;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>.ReadOnly             rebuildTreeBrushIndexOrders;

        // Write
        [NoAlias, WriteOnly] public NativeList<BrushPair>.ParallelWriter        brushBrushIntersections;
        [NoAlias, WriteOnly] public NativeHashSet<IndexOrder>.ParallelWriter    brushesThatNeedIndirectUpdateHashMap;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>   transformedPlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>   transformedPlanes1;
        [NativeDisableContainerSafetyRestriction] NativeBitArray        foundBrushes;
        [NativeDisableContainerSafetyRestriction] NativeBitArray        usedBrushes;

        public void Execute(int index1)
        {
            if (allTreeBrushIndexOrders.Length == rebuildTreeBrushIndexOrders.Length)
            {
                //for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
                {
                    var brush1IndexOrder = rebuildTreeBrushIndexOrders[index1];
                    int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                    for (int index0 = 0; index0 < rebuildTreeBrushIndexOrders.Length; index0++)
                    {
                        var brush0IndexOrder    = rebuildTreeBrushIndexOrders[index0];
                        int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                        if (brush0NodeOrder <= brush1NodeOrder)
                            continue;
                        var result = IntersectionUtility.FindIntersection(brush0NodeOrder, brush1NodeOrder, 
                                                                          ref brushMeshLookup, ref brushTreeSpaceBounds, ref transformationCache,
                                                                          ref transformedPlanes0, ref transformedPlanes1);
                        if (result == IntersectionType.NoIntersection)
                            continue;
                        IntersectionUtility.StoreIntersection(ref brushBrushIntersections, brush0IndexOrder, brush1IndexOrder, result);
                    }
                }
                return;
            }
            
            if (!foundBrushes.IsCreated || foundBrushes.Length < allTreeBrushIndexOrders.Length)
                foundBrushes = new NativeBitArray(allTreeBrushIndexOrders.Length, Allocator.Temp);
            foundBrushes.Clear();

            if (!usedBrushes.IsCreated || usedBrushes.Length < allTreeBrushIndexOrders.Length)
                usedBrushes = new NativeBitArray(allTreeBrushIndexOrders.Length, Allocator.Temp);
            usedBrushes.Clear();

            // TODO: figure out a way to avoid needing this
            for (int a = 0; a < rebuildTreeBrushIndexOrders.Length; a++)
                foundBrushes.Set(rebuildTreeBrushIndexOrders[a].nodeOrder, true);

            //for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
            {
                var brush1IndexOrder = rebuildTreeBrushIndexOrders[index1];
                int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                for (int index0 = 0; index0 < allTreeBrushIndexOrders.Length; index0++)
                {
                    var brush0IndexOrder    = allTreeBrushIndexOrders[index0];
                    int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                    if (brush0NodeOrder == brush1NodeOrder)
                        continue;
                    var found = foundBrushes.IsSet(brush0NodeOrder);
                    if (brush0NodeOrder < brush1NodeOrder && found)
                        continue;
                    var result = IntersectionUtility.FindIntersection(brush0NodeOrder, brush1NodeOrder,
                                                                      ref brushMeshLookup, ref brushTreeSpaceBounds, ref transformationCache,
                                                                      ref transformedPlanes0, ref transformedPlanes1);
                    if (result == IntersectionType.NoIntersection)
                        continue;
                    if (!found)
                    {
                        if (!usedBrushes.IsSet(brush0IndexOrder.nodeOrder))
                        {
                            usedBrushes.Set(brush0IndexOrder.nodeOrder, true);
                            brushesThatNeedIndirectUpdateHashMap.Add(brush0IndexOrder);
                            IntersectionUtility.StoreIntersection(ref brushBrushIntersections, brush0IndexOrder, brush1IndexOrder, result);
                        }
                    } else
                    {
                        if (brush0NodeOrder > brush1NodeOrder)
                            IntersectionUtility.StoreIntersection(ref brushBrushIntersections, brush0IndexOrder, brush1IndexOrder, result);
                    }
                }
            }
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    struct FindUniqueIndirectBrushIntersectionsJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeHashSet<IndexOrder> brushesThatNeedIndirectUpdateHashMap;

        // Write
        [NoAlias, WriteOnly] public NativeList<IndexOrder> brushesThatNeedIndirectUpdate;

        public unsafe void Execute()
        {
            var keys = brushesThatNeedIndirectUpdateHashMap.ToNativeArray(Allocator.Temp);
            brushesThatNeedIndirectUpdate.AddRangeNoResize(keys.GetUnsafePtr(), keys.Length);
            keys.Dispose();
        }
    }

    // TODO: make this a parallel job somehow
    [BurstCompile(CompileSynchronously = true)]
    struct FindAllIndirectBrushIntersectionPairsJob : IJob// IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>.ReadOnly         allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushMeshBlob>>.ReadOnly brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>         transformationCache;
        [NoAlias, ReadOnly] public NativeArray<MinMaxAABB>                  brushTreeSpaceBounds;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  brushesThatNeedIndirectUpdate;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>.ReadOnly         rebuildTreeBrushIndexOrders;

        // Write
        [NoAlias, WriteOnly] public NativeList<BrushPair>.ParallelWriter    brushBrushIntersections;
        [NoAlias, WriteOnly] public NativeList<IndexOrder>.ParallelWriter   allUpdateBrushIndexOrders;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeList<IndexOrder>    requiredTemporaryBullShitByDOTS;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       transformedPlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       transformedPlanes1;

        [NativeDisableContainerSafetyRestriction] NativeBitArray            foundBrushes;

        public void Execute()
        {
            if (!requiredTemporaryBullShitByDOTS.IsCreated)
                requiredTemporaryBullShitByDOTS = new NativeList<IndexOrder>(allTreeBrushIndexOrders.Length, Allocator.Temp);
            requiredTemporaryBullShitByDOTS.Clear();


            if (!foundBrushes.IsCreated || foundBrushes.Length < allTreeBrushIndexOrders.Length)
                foundBrushes = new NativeBitArray(allTreeBrushIndexOrders.Length, Allocator.Temp);
            foundBrushes.Clear();

            //*
            for (int i = 0; i < rebuildTreeBrushIndexOrders.Length; i++)
            {
                foundBrushes.Set(rebuildTreeBrushIndexOrders[i].nodeOrder, true);
                requiredTemporaryBullShitByDOTS.AddNoResize(rebuildTreeBrushIndexOrders[i]);
            }
            for (int i = 0; i < brushesThatNeedIndirectUpdate.Length; i++)
            {
                var indexOrder = brushesThatNeedIndirectUpdate[i];
                if (!foundBrushes.IsSet(indexOrder.nodeOrder))
                {
                    requiredTemporaryBullShitByDOTS.AddNoResize(indexOrder);
                    foundBrushes.Set(indexOrder.nodeOrder, true);
                }
            }
            requiredTemporaryBullShitByDOTS.Sort(new IntersectionUtility.IndexOrderComparer());
            allUpdateBrushIndexOrders.AddRangeNoResize(requiredTemporaryBullShitByDOTS);

            for (int index1 = 0; index1 < brushesThatNeedIndirectUpdate.Length; index1++)
            {
                var brush1IndexOrder = brushesThatNeedIndirectUpdate[index1];
                int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                for (int index0 = 0; index0 < allTreeBrushIndexOrders.Length; index0++)
                {
                    var brush0IndexOrder    = allTreeBrushIndexOrders[index0];
                    int brush0NodeOrder     = brush0IndexOrder.nodeOrder;
                    if (brush0NodeOrder == brush1NodeOrder 
                        // TODO: figure out why this optimization causes iterative updates to fail
                        //|| foundBrushes.IsSet(brush0IndexOrder.nodeOrder)
                        )
                        continue;
                    if (brush0NodeOrder > brush1NodeOrder)
                    {
                        var result = IntersectionUtility.FindIntersection(brush0NodeOrder, brush1NodeOrder,
                                                                          ref brushMeshLookup, ref brushTreeSpaceBounds, ref transformationCache,
                                                                          ref transformedPlanes0, ref transformedPlanes1);
                        if (result == IntersectionType.NoIntersection)
                            continue;
                        IntersectionUtility.StoreIntersection(ref brushBrushIntersections, brush0IndexOrder, brush1IndexOrder, result);
                    } else
                    {
                        var result = IntersectionUtility.FindIntersection(brush1NodeOrder, brush0NodeOrder,
                                                                          ref brushMeshLookup, ref brushTreeSpaceBounds, ref transformationCache,
                                                                          ref transformedPlanes0, ref transformedPlanes1);
                        if (result == IntersectionType.NoIntersection)
                            continue;
                        IntersectionUtility.StoreIntersection(ref brushBrushIntersections, brush1IndexOrder, brush0IndexOrder, result);
                    }
                }
            }
            //*/
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal struct InvalidateBrushCacheJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>.ReadOnly rebuildTreeBrushIndexOrders;

        // Read Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BasePolygonsBlob>>             basePolygonCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<RoutingTable>>                 routingTableCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache;

        public void Execute(int index)
        {
            var indexOrder = rebuildTreeBrushIndexOrders[index];
            int nodeOrder = indexOrder.nodeOrder;

            if (nodeOrder < 0 ||
                nodeOrder >= basePolygonCache.Length)
            {
                Debug.LogError("nodeOrder out of bounds");
                return;
            }

            // try
            {
                { 
                    var original = basePolygonCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    basePolygonCache[nodeOrder] = default;
                }
                {
                    var original = treeSpaceVerticesCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    treeSpaceVerticesCache[nodeOrder] = default;
                }
                {
                    var original = brushesTouchedByBrushCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    brushesTouchedByBrushCache[nodeOrder] = default;
                }
                {
                    var original = routingTableCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    routingTableCache[nodeOrder] = default;
                }
                {
                    var original = brushTreeSpacePlaneCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    brushTreeSpacePlaneCache[nodeOrder] = default;
                }
                {
                    var original = brushRenderBufferCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    brushRenderBufferCache[nodeOrder] = default;
                }

            }
            //catch { Debug.Log($"FAIL {indexOrder.nodeIndex}"); throw; }
        }
    }
    

    [BurstCompile(CompileSynchronously = true)]
    internal struct InvalidateIndirectBrushCacheJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder> brushesThatNeedIndirectUpdate;

        // Read Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BasePolygonsBlob>>             basePolygonCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<RoutingTable>>                 routingTableCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache;

        public void Execute(int index)
        {
            var indexOrder = brushesThatNeedIndirectUpdate[index];
            int nodeOrder = indexOrder.nodeOrder;

            if (nodeOrder < 0 ||
                nodeOrder >= basePolygonCache.Length)
            {
                UnityEngine.Debug.LogError($"nodeOrder out of bounds {nodeOrder} / {basePolygonCache.Length}");
                return;
            }

            // try
            {
                { 
                    var original = basePolygonCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    basePolygonCache[nodeOrder] = default;
                }
                {
                    var original = treeSpaceVerticesCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    treeSpaceVerticesCache[nodeOrder] = default;
                }
                {
                    var original = brushesTouchedByBrushCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    brushesTouchedByBrushCache[nodeOrder] = default;
                }
                {
                    var original = routingTableCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    routingTableCache[nodeOrder] = default;
                }
                {
                    var original = brushTreeSpacePlaneCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    brushTreeSpacePlaneCache[nodeOrder] = default;
                }
                {
                    var original = brushRenderBufferCache[nodeOrder];
                    if (original != default && original.IsCreated) original.Dispose();
                    brushRenderBufferCache[nodeOrder] = default;
                }

            }
            //catch { Debug.Log($"FAIL {indexOrder.nodeIndex}"); throw; }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal struct FixupBrushCacheIndicesJob : IJobParallelFor
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>.ReadOnly allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>.ReadOnly        nodeIndexToNodeOrderArray;
        [NoAlias, ReadOnly] public int                              nodeIndexToNodeOrderOffset;

        // Read Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BasePolygonsBlob>>      basePolygonCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<BlobAssetReference<BrushesTouchedByBrush>> brushesTouchedByBrushCache;

        public void Execute(int index)
        {
            var indexOrder  = allTreeBrushIndexOrders[index];
            int nodeOrder   = indexOrder.nodeOrder;

            {
                var item = basePolygonCache[nodeOrder];
                if (item.IsCreated)
                {
                    // Fix up node orders
                    ref var polygons = ref item.Value.polygons;
                    for (int p = 0; p < polygons.Length; p++)
                    {
                        ref var nodeIndexOrder = ref polygons[p].nodeIndexOrder;
                        nodeIndexOrder.nodeOrder = nodeIndexToNodeOrderArray[nodeIndexOrder.nodeIndex - nodeIndexToNodeOrderOffset];
                    }
                    basePolygonCache[nodeOrder] = item;
                }
            }

            {
                var item = brushesTouchedByBrushCache[nodeOrder];
                if (item.IsCreated)
                { 
                    ref var brushesTouchedByBrush   = ref item.Value;
                    ref var brushIntersections      = ref brushesTouchedByBrush.brushIntersections;
                    for (int b = 0; b < brushIntersections.Length; b++)
                    {
                        ref var brushIntersection = ref brushIntersections[b];
                        ref var nodeIndexOrder = ref brushIntersection.nodeIndexOrder;
                        nodeIndexOrder.nodeOrder = nodeIndexToNodeOrderArray[nodeIndexOrder.nodeIndex - nodeIndexToNodeOrderOffset];
                    }
                    brushesTouchedByBrushCache[nodeOrder] = item;
                }
            }
        }
    }

    sealed class IntersectionUtility
    {
        public const double kBoundsDistanceEpsilon = CSGConstants.kBoundsDistanceEpsilon;


        public struct IndexOrderComparer : IComparer<IndexOrder>
        {
            public int Compare(IndexOrder x, IndexOrder y)
            {
                return x.nodeOrder.CompareTo(y.nodeOrder);
            }
        }

        public static void TransformOtherIntoBrushSpace(ref float4x4 treeToBrushSpaceMatrix, ref float4x4 brushToTreeSpaceMatrix, ref BlobArray<float4> srcPlanes, NativeArray<float4> dstPlanes)
        {
            var brush1ToBrush0LocalLocalSpace = math.transpose(math.mul(treeToBrushSpaceMatrix, brushToTreeSpaceMatrix));
            for (int plane_index = 0; plane_index < srcPlanes.Length; plane_index++)
            {
                ref var srcPlane = ref srcPlanes[plane_index];
                dstPlanes[plane_index] = math.mul(brush1ToBrush0LocalLocalSpace, srcPlane);
            }
        }


        public static IntersectionType ConvexPolytopeTouching([NoAlias] ref BrushMeshBlob brushMesh0,
                                                              [NoAlias] ref float4x4 treeToNode0SpaceMatrix,
                                                              [NoAlias] ref float4x4 nodeToTree0SpaceMatrix,
                                                              [NoAlias] ref BrushMeshBlob brushMesh1,
                                                              [NoAlias] ref float4x4 treeToNode1SpaceMatrix,
                                                              [NoAlias] ref float4x4 nodeToTree1SpaceMatrix,
                                                              [NoAlias] ref NativeArray<float4> transformedPlanes0,
                                                              [NoAlias] ref NativeArray<float4> transformedPlanes1)
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

        static int WhichSide([NoAlias,ReadOnly] ref BlobArray<float3> vertices, float4 plane, double epsilon)
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
        

        public static IntersectionType FindIntersection(int brush0NodeOrder, int brush1NodeOrder,
                                                        [NoAlias] ref NativeArray<BlobAssetReference<BrushMeshBlob>>.ReadOnly brushMeshLookup,
                                                        [NoAlias] ref NativeArray<MinMaxAABB>          brushTreeSpaceBounds,
                                                        [NoAlias] ref NativeArray<NodeTransformations> transformations,
                                                        [NoAlias] ref NativeArray<float4>              transformedPlanes0,
                                                        [NoAlias] ref NativeArray<float4>              transformedPlanes1)
        {
            var brushMesh0 = brushMeshLookup[brush0NodeOrder];
            var brushMesh1 = brushMeshLookup[brush1NodeOrder];
            if (!brushMesh0.IsCreated || !brushMesh1.IsCreated)
                return IntersectionType.NoIntersection;

            var bounds0 = brushTreeSpaceBounds[brush0NodeOrder];
            var bounds1 = brushTreeSpaceBounds[brush1NodeOrder];

            if (!bounds0.Intersects(bounds1, IntersectionUtility.kBoundsDistanceEpsilon))
                return IntersectionType.NoIntersection;
            
            var transformation0 = transformations[brush0NodeOrder];
            var transformation1 = transformations[brush1NodeOrder];

            var treeToNode0SpaceMatrix = transformation0.treeToNode;
            var nodeToTree0SpaceMatrix = transformation0.nodeToTree;
            var treeToNode1SpaceMatrix = transformation1.treeToNode;
            var nodeToTree1SpaceMatrix = transformation1.nodeToTree;

            var result = IntersectionUtility.ConvexPolytopeTouching(ref brushMesh0.Value,
                                                                    ref treeToNode0SpaceMatrix,
                                                                    ref nodeToTree0SpaceMatrix,
                                                                    ref brushMesh1.Value,
                                                                    ref treeToNode1SpaceMatrix,
                                                                    ref nodeToTree1SpaceMatrix,
                                                                    ref transformedPlanes0,
                                                                    ref transformedPlanes1);
          
            return result;
        }

        // TODO: check if each intersection is unique, store in stream
        public static void StoreIntersection([NoAlias] ref NativeList<BrushPair>.ParallelWriter brushBrushIntersections, IndexOrder brush0IndexOrder, IndexOrder brush1IndexOrder, IntersectionType result)
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
}
