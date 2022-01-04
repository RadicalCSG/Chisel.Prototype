using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct FindAllBrushIntersectionPairsJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                      allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>             transformationCache;
        [NoAlias, ReadOnly] public NativeArray<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<ChiselAABB>                      brushTreeSpaceBounds;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                      rebuildTreeBrushIndexOrders;

        // Read (Re-alloc) / Write
        public Allocator allocator;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<UnsafeList<BrushIntersectWith>>            brushBrushIntersections;

        // Write
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
                    var brush1Intersections = brushBrushIntersections[brush1NodeOrder];
                    if (!brush1Intersections.IsCreated)
                        brush1Intersections = new UnsafeList<BrushIntersectWith>(16, allocator);
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
                        result = IntersectionUtility.Flip(result);
                        IntersectionUtility.StoreIntersection(ref brush1Intersections, brush0IndexOrder, result);
                    }
                    brushBrushIntersections[brush1NodeOrder] = brush1Intersections;
                }
                return;
            }

            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref foundBrushes, allTreeBrushIndexOrders.Length);
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref usedBrushes, allTreeBrushIndexOrders.Length);

            // TODO: figure out a way to avoid needing this
            for (int a = 0; a < rebuildTreeBrushIndexOrders.Length; a++)
                foundBrushes.Set(rebuildTreeBrushIndexOrders[a].nodeOrder, true);

            //for (int index1 = 0; index1 < updateBrushIndicesArray.Length; index1++)
            {
                var brush1IndexOrder = rebuildTreeBrushIndexOrders[index1];
                int brush1NodeOrder  = brush1IndexOrder.nodeOrder;

                var brush1Intersections = brushBrushIntersections[brush1NodeOrder];
                if (!brush1Intersections.IsCreated)
                    brush1Intersections = new UnsafeList<BrushIntersectWith>(16, allocator);
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
                            result = IntersectionUtility.Flip(result);
                            IntersectionUtility.StoreIntersection(ref brush1Intersections, brush0IndexOrder, result);
                        }
                    } else
                    {
                        if (brush0NodeOrder > brush1NodeOrder)
                        {
                            result = IntersectionUtility.Flip(result);
                            IntersectionUtility.StoreIntersection(ref brush1Intersections, brush0IndexOrder, result);
                        }
                    }
                }
                brushBrushIntersections[brush1NodeOrder] = brush1Intersections;
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

    [BurstCompile(CompileSynchronously = true)]
    struct AddIndirectUpdatedBrushesToListAndSortJob : IJob
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  brushesThatNeedIndirectUpdate;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  rebuildTreeBrushIndexOrders;

        // Write
        [NoAlias, WriteOnly] public NativeList<IndexOrder>.ParallelWriter   allUpdateBrushIndexOrders;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeList<IndexOrder>    requiredTemporaryBullShitByDOTS;

        [NativeDisableContainerSafetyRestriction] NativeBitArray            foundBrushes;

        public void Execute()
        {
            NativeCollectionHelpers.EnsureCapacityAndClear(ref requiredTemporaryBullShitByDOTS, allTreeBrushIndexOrders.Length);
            NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref foundBrushes, allTreeBrushIndexOrders.Length);

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
        }
    }
    

    // TODO: make this a parallel job somehow
    [BurstCompile(CompileSynchronously = true)]
    struct FindAllIndirectBrushIntersectionPairsJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<NodeTransformations>         transformationCache;
        [NoAlias, ReadOnly] public NativeArray<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshLookup;
        [NoAlias, ReadOnly] public NativeArray<ChiselAABB>                  brushTreeSpaceBounds;
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                  brushesThatNeedIndirectUpdate;

        // Read (Re-alloc) / Write
        public Allocator allocator;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<UnsafeList<BrushIntersectWith>>        brushBrushIntersections;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       transformedPlanes0;
        [NativeDisableContainerSafetyRestriction] NativeArray<float4>       transformedPlanes1;

        public void Execute(int index1)
        {
            //for (int index1 = 0; index1 < brushesThatNeedIndirectUpdate.Length; index1++)
            {
                var brush1IndexOrder = brushesThatNeedIndirectUpdate[index1];
                int brush1NodeOrder  = brush1IndexOrder.nodeOrder;
                
                var brush1Intersections = brushBrushIntersections[brush1NodeOrder];
                if (!brush1Intersections.IsCreated)
                    brush1Intersections = new UnsafeList<BrushIntersectWith>(16, allocator);
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
                        result = IntersectionUtility.Flip(result);
                        IntersectionUtility.StoreIntersection(ref brush1Intersections, brush0IndexOrder, result);
                    } else
                    {
                        var result = IntersectionUtility.FindIntersection(brush1NodeOrder, brush0NodeOrder,
                                                                          ref brushMeshLookup, ref brushTreeSpaceBounds, ref transformationCache,
                                                                          ref transformedPlanes0, ref transformedPlanes1);
                        if (result == IntersectionType.NoIntersection)
                            continue;
                        IntersectionUtility.StoreIntersection(ref brush1Intersections, brush0IndexOrder, result);
                    }
                }
                brushBrushIntersections[brush1NodeOrder] = brush1Intersections;
            }
            //*/
        }
    }


    // TODO: create separate job to dispose all the items that need to be disposed & 
    //          just set them to default in these arrays .. then we can just let the 
    //          dispose job run without us having to wait for it
    [BurstCompile(CompileSynchronously = true)]
    struct InvalidateBrushCacheJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  rebuildTreeBrushIndexOrders;

        // Read/Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<ChiselBlobAssetReference<BasePolygonsBlob>>             basePolygonCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<ChiselBlobAssetReference<BrushTreeSpaceVerticesBlob>>   treeSpaceVerticesCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<ChiselBlobAssetReference<BrushesTouchedByBrush>>        brushesTouchedByBrushCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<ChiselBlobAssetReference<RoutingTable>>                 routingTableCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<ChiselBlobAssetReference<BrushTreeSpacePlanes>>         brushTreeSpacePlaneCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeList<ChiselBlobAssetReference<ChiselBrushRenderBuffer>>      brushRenderBufferCache;

        // Write
        [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<BasePolygonsBlob>>.ParallelWriter             basePolygonDisposeList;
        [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<BrushTreeSpaceVerticesBlob>>.ParallelWriter   treeSpaceVerticesDisposeList;
        [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<BrushesTouchedByBrush>>.ParallelWriter        brushesTouchedByBrushDisposeList;
        [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<RoutingTable>>.ParallelWriter                 routingTableDisposeList;
        [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<BrushTreeSpacePlanes>>.ParallelWriter         brushTreeSpacePlaneDisposeList;
        [NoAlias, WriteOnly] public NativeList<ChiselBlobAssetReference<ChiselBrushRenderBuffer>>.ParallelWriter      brushRenderBufferDisposeList;

        public void Execute(int index)
        {
            var indexOrder = rebuildTreeBrushIndexOrders[index];
            int nodeOrder = indexOrder.nodeOrder;

            if (nodeOrder < 0 ||
                nodeOrder >= basePolygonCache.Length)
            {
                Debug.LogError($"nodeOrder ({nodeOrder}) out of bounds (0..{basePolygonCache.Length})");
                return;
            }

            // try
            {
                { 
                    var original = basePolygonCache[nodeOrder];
                    if (original != default && original.IsCreated)
                        basePolygonDisposeList.AddNoResize(original);
                    basePolygonCache[nodeOrder] = default;
                }
                {
                    var original = treeSpaceVerticesCache[nodeOrder];
                    if (original != default && original.IsCreated)
                        treeSpaceVerticesDisposeList.AddNoResize(original);
                    treeSpaceVerticesCache[nodeOrder] = default;
                }
                {
                    var original = brushesTouchedByBrushCache[nodeOrder];
                    if (original != default && original.IsCreated)
                        brushesTouchedByBrushDisposeList.AddNoResize(original);
                    brushesTouchedByBrushCache[nodeOrder] = default;
                }
                {
                    var original = routingTableCache[nodeOrder];
                    if (original != default && original.IsCreated)
                        routingTableDisposeList.AddNoResize(original);
                    routingTableCache[nodeOrder] = default;
                }
                {
                    var original = brushTreeSpacePlaneCache[nodeOrder];
                    if (original != default && original.IsCreated)
                        brushTreeSpacePlaneDisposeList.AddNoResize(original);
                    brushTreeSpacePlaneCache[nodeOrder] = default;
                }
                {
                    var original = brushRenderBufferCache[nodeOrder];
                    if (original != default && original.IsCreated)
                        brushRenderBufferDisposeList.AddNoResize(original);
                    brushRenderBufferCache[nodeOrder] = default;
                }

            }
            //catch { Debug.Log($"FAIL {indexOrder.nodeIndex}"); throw; }
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    struct InvalidateIndirectBrushCacheJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  brushesThatNeedIndirectUpdate;

        // Read Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<BasePolygonsBlob>>            basePolygonCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<BrushTreeSpaceVerticesBlob>>  treeSpaceVerticesCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<BrushesTouchedByBrush>>       brushesTouchedByBrushCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<RoutingTable>>                routingTableCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<BrushTreeSpacePlanes>>        brushTreeSpacePlaneCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<ChiselBrushRenderBuffer>>     brushRenderBufferCache;

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
    struct FixupBrushCacheIndicesJob : IJobParallelForDefer
    {
        // Read
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>  allTreeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>         nodeIDValueToNodeOrderArray;
        [NoAlias, ReadOnly] public NativeReference<int>     nodeIDValueToNodeOrderOffsetRef;

        // Read Write
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<BasePolygonsBlob>>        basePolygonCache;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<ChiselBlobAssetReference<BrushesTouchedByBrush>>   brushesTouchedByBrushCache;

        public void Execute(int index)
        {
            var nodeIDValueToNodeOrderOffset = nodeIDValueToNodeOrderOffsetRef.Value;
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
                        var nodeIDValue = nodeIndexOrder.compactNodeID.value;
                        nodeIndexOrder.nodeOrder = nodeIDValueToNodeOrderArray[nodeIDValue - nodeIDValueToNodeOrderOffset];
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
                        var nodeIDValue = nodeIndexOrder.compactNodeID.value;
                        nodeIndexOrder.nodeOrder = nodeIDValueToNodeOrderArray[nodeIDValue - nodeIDValueToNodeOrderOffset];
                    }
                    for (int b0 = 0; b0 < brushIntersections.Length; b0++)
                    {
                        ref var brushIntersection0 = ref brushIntersections[b0];
                        ref var nodeIndexOrder0 = ref brushIntersection0.nodeIndexOrder;
                        for (int b1 = b0+1; b1 < brushIntersections.Length; b1++)
                        {
                            ref var brushIntersection1 = ref brushIntersections[b1];
                            ref var nodeIndexOrder1 = ref brushIntersection1.nodeIndexOrder;
                            if (nodeIndexOrder0.nodeOrder > nodeIndexOrder1.nodeOrder)
                            {
                                var t = nodeIndexOrder0;
                                nodeIndexOrder0 = nodeIndexOrder1;
                                nodeIndexOrder1 = t;
                            }
                        }
                    }
                    brushesTouchedByBrushCache[nodeOrder] = item;
                }
            }
        }
    }

    sealed class IntersectionUtility
    {
        public const double kBoundsDistanceEpsilon = CSGConstants.kBoundsDistanceEpsilon;


        public struct IndexOrderComparer : System.Collections.Generic.IComparer<IndexOrder>
        {
            public int Compare(IndexOrder x, IndexOrder y)
            {
                return x.nodeOrder.CompareTo(y.nodeOrder);
            }
        }

        public static void TransformOtherIntoBrushSpace(ref float4x4 treeToBrushSpaceMatrix, ref float4x4 brushToTreeSpaceMatrix, ref ChiselBlobArray<float4> srcPlanes, NativeArray<float4> dstPlanes)
        {
            var brush1ToBrush0LocalLocalSpace = math.transpose(math.mul(treeToBrushSpaceMatrix, brushToTreeSpaceMatrix));
            for (int plane_index = 0; plane_index < srcPlanes.Length; plane_index++)
            {
                ref var srcPlane = ref srcPlanes[plane_index];
                dstPlanes[plane_index] = math.mul(brush1ToBrush0LocalLocalSpace, srcPlane);
            }
        }


        public static IntersectionType ConvexPolytopeTouching([NoAlias] ref BrushMeshBlob       brushMesh0,
                                                              [NoAlias] ref float4x4            treeToNode0SpaceMatrix,
                                                              [NoAlias] ref float4x4            nodeToTree0SpaceMatrix,
                                                              [NoAlias] ref BrushMeshBlob       brushMesh1,
                                                              [NoAlias] ref float4x4            treeToNode1SpaceMatrix,
                                                              [NoAlias] ref float4x4            nodeToTree1SpaceMatrix,
                                                              [NoAlias] ref NativeArray<float4> transformedPlanes0,
                                                              [NoAlias] ref NativeArray<float4> transformedPlanes1)
        {
            ref var brushPlanes0   = ref brushMesh0.localPlanes;

            NativeCollectionHelpers.EnsureMinimumSize(ref transformedPlanes0, brushPlanes0.Length);
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

            NativeCollectionHelpers.EnsureMinimumSize(ref transformedPlanes1, brushPlanes1.Length);
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

        static int WhichSide([NoAlias,ReadOnly] ref ChiselBlobArray<float3> vertices, float4 plane, double epsilon)
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
                                                        [NoAlias] ref NativeArray<ChiselBlobAssetReference<BrushMeshBlob>> brushMeshLookup,
                                                        [NoAlias] ref NativeArray<ChiselAABB>           brushTreeSpaceBounds,
                                                        [NoAlias] ref NativeArray<NodeTransformations>  transformations,
                                                        [NoAlias] ref NativeArray<float4>               transformedPlanes0,
                                                        [NoAlias] ref NativeArray<float4>               transformedPlanes1)
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

        public static IntersectionType Flip(IntersectionType type)
        {
            if (type == IntersectionType.AInsideB) type = IntersectionType.BInsideA;
            else if (type == IntersectionType.BInsideA) type = IntersectionType.AInsideB;
            return type;
        }

        // TODO: check if each intersection is unique, store in stream
        public static void StoreIntersection([NoAlias] ref UnsafeList<BrushIntersectWith> intersections, IndexOrder brush1IndexOrder, IntersectionType result)
        {
            if (intersections.Capacity < intersections.Length + 1)
                intersections.SetCapacity((int)((intersections.Length + 1)*1.5f));
            if (result != IntersectionType.NoIntersection)
            {
                if (result == IntersectionType.Intersection)
                {
                    intersections.AddNoResize(new BrushIntersectWith { brushNodeOrder1 = brush1IndexOrder.nodeOrder, type = IntersectionType.Intersection });
                } else
                if (result == IntersectionType.AInsideB)
                {
                    intersections.AddNoResize(new BrushIntersectWith { brushNodeOrder1 = brush1IndexOrder.nodeOrder, type = IntersectionType.AInsideB });
                } else
                //if (intersectionType == IntersectionType.BInsideA)
                {
                    intersections.AddNoResize(new BrushIntersectWith { brushNodeOrder1 = brush1IndexOrder.nodeOrder, type = IntersectionType.BInsideA });
                }
            }
        }
    }
}
