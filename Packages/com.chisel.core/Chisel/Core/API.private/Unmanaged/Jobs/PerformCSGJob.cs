using System;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    struct PerformCSGJob : IJobParallelForDefer
    {
        // Read
        // 'Required' for scheduling with index count
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  allUpdateBrushIndexOrders;        

        [NoAlias, ReadOnly] public NativeArray<ChiselBlobAssetReference<RoutingTable>>            routingTableCache;
        [NoAlias, ReadOnly] public NativeArray<ChiselBlobAssetReference<BrushTreeSpacePlanes>>    brushTreeSpacePlaneCache;
        [NoAlias, ReadOnly] public NativeArray<ChiselBlobAssetReference<BrushesTouchedByBrush>>   brushesTouchedByBrushCache;
        [NoAlias, ReadOnly] public NativeListArray<float3>                                  loopVerticesLookup;

        [NoAlias, ReadOnly] public NativeStream.Reader      input;
        
        // Write
        [NoAlias, WriteOnly] public NativeStream.Writer     output;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<EdgeCategory>     categories1;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<EdgeCategory>     categories2;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<Edge>             outEdges;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<int>              intersectedHoleIndices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<IndexSurfaceInfo> intersectionSurfaceInfo;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeBitArray                destroyedEdges;        
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<IndexSurfaceInfo>  allInfos;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<IndexSurfaceInfo>  intersectionSurfaceInfos;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<IndexSurfaceInfo>  basePolygonSurfaceInfos;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<float4>            alltreeSpacePlanes;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<LoopSegment>       allSegments;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeList<Edge>              allCombinedEdges;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeListArray<int>          holeIndices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeListArray<int>          surfaceLoopIndices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeListArray<Edge>         allEdges;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeListArray<Edge>         intersectionLoops;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeListArray<Edge>         basePolygonEdges;
        [NativeDisableContainerSafetyRestriction, NoAlias] HashedVertices                hashedTreeSpaceVertices;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeArray<ushort>           indexRemap;
        [NativeDisableContainerSafetyRestriction, NoAlias] NativeListArray<Edge>         intersectionEdges;

        [BurstDiscard]
        private static void NotUniqueEdgeException() 
        {
            throw new Exception("Edge is not unique");
        }

        static bool AddEdgesNoResize(NativeListArray<Edge>.NativeList dstEdges, NativeListArray<Edge>.NativeList srcEdges)
        {
            if (srcEdges.Length == 0)  
                return false;

            for (int ae = srcEdges.Length - 1; ae >= 0; ae--)
            {
                var addEdge = srcEdges[ae];
                for (int e = dstEdges.Length - 1; e >= 0; )
                {
                    if (addEdge.Equals(dstEdges[e]))
                    {
                        NotUniqueEdgeException();
                        dstEdges.RemoveAtSwapBack(e);
                    } else
                        e--;
                }
            }

            bool duplicates = false;

            for (int v = 0; v < srcEdges.Length;v++)
            {
                var addEdge = srcEdges[v];
                var index = IndexOf(dstEdges, addEdge, out bool _);
                if (index != -1)
                {
                    //Debug.Log($"Duplicate edge {inverted}  {values[v].index1}/{values[v].index2} {edges[index].index1}/{edges[index].index2}");
                    duplicates = true;
                    continue;
                }
                dstEdges.AddNoResize(addEdge);
            }

            return duplicates;
        }

        static int IndexOf(NativeListArray<Edge>.NativeList edges, Edge edge, out bool inverted)
        {
            //var builder = new System.Text.StringBuilder();
            for (int e = 0; e < edges.Length; e++)
            {
                //builder.AppendLine($"{e}/{edges.Count}: {edges[e]} {edge}");
                if (edges[e].index1 == edge.index1 && edges[e].index2 == edge.index2) { inverted = false; return e; }
                if (edges[e].index1 == edge.index2 && edges[e].index2 == edge.index1) { inverted = true;  return e; }
            }
            //Debug.Log(builder.ToString());
            inverted = false;
            return -1;
        }
        
        void IntersectLoops([NoAlias] in HashedVertices                   hashedTreeSpaceVertices,

                            [NoAlias] ref NativeListArray<int>.NativeList loopIndices, 
                            int                                           surfaceLoopIndex,

                            [NoAlias] ref NativeListArray<int>            holeIndices,
                            [NoAlias] ref NativeList<IndexSurfaceInfo>    allInfos,
                            [NoAlias] ref NativeListArray<Edge>           allEdges,

                            [NoAlias] in NativeListArray<Edge>.NativeList intersectionLoop, 
                            CategoryGroupIndex                            intersectionCategory,
                            IndexSurfaceInfo                              intersectionInfo)
        {
            if (intersectionLoop.Length == 0)
                return;

            //Debug.Assert(allEdges.Length == allInfos.Length);
            //Debug.Assert(allInfos.Length == holeIndices.Length);

            var currentLoopEdges    = allEdges[surfaceLoopIndex];
            var currentInfo         = allInfos[surfaceLoopIndex];
            var currentHoleIndices  = holeIndices[surfaceLoopIndex];

            // It might look like we could just set the interiorCategory of brush_intersection here, and let all other cut loops copy from it below,
            // but the same brush_intersection might be used by another categorized_loop and then we'd try to reroute it again, which wouldn't work
            //brush_intersection.interiorCategory = newHoleCategory;

            if (currentLoopEdges.Length == 0)
                return;

            var maxLength = math.max(16, intersectionLoop.Length + currentLoopEdges.Length);
            if (maxLength < 3)
                return;

            NativeCollectionHelpers.EnsureMinimumSize(ref categories1, intersectionLoop.Length);
            NativeCollectionHelpers.EnsureMinimumSize(ref categories2, currentLoopEdges.Length);

            int inside1 = 0, outside1 = 0;
            int currentBrushOrder = currentInfo.brushIndexOrder.nodeOrder;
            ref var treeSpacePlanes2 = ref brushTreeSpacePlaneCache[currentBrushOrder].Value.treeSpacePlanes;
            for (int e = 0; e < intersectionLoop.Length; e++)
            {
                var category = BooleanEdgesUtility.CategorizeEdge(intersectionLoop[e], ref treeSpacePlanes2, in currentLoopEdges, in hashedTreeSpaceVertices);
                categories1[e] = category;
                if      (category == EdgeCategory.Inside) inside1++;
                else if (category == EdgeCategory.Outside) outside1++;
            }
            var aligned1 = intersectionLoop.Length - (inside1 + outside1);

            int intersectionBrushOrder  = intersectionInfo.brushIndexOrder.nodeOrder;
            ref var treeSpacePlanes1    = ref brushTreeSpacePlaneCache[intersectionBrushOrder].Value.treeSpacePlanes;
            int inside2 = 0, outside2 = 0;
            for (int e = 0; e < currentLoopEdges.Length; e++)
            {
                var category = BooleanEdgesUtility.CategorizeEdge(currentLoopEdges[e], ref treeSpacePlanes1, in intersectionLoop, in hashedTreeSpaceVertices);
                categories2[e] = category;
                if      (category == EdgeCategory.Inside) inside2++;
                else if (category == EdgeCategory.Outside) outside2++;
            }
            var aligned2 = currentLoopEdges.Length - (inside2 + outside2);

            // Completely outside
            if ((inside1 + aligned1) == 0 && (aligned2 + inside2) == 0)
                return;

            if ((inside1 + (inside2 + aligned2)) < 3)
                return;

            // Completely aligned
            if (((outside1 + inside1) == 0 && (outside2 + inside2) == 0) ||
                // polygon1 edges Completely inside polygon2
                (inside1 == 0 && outside2 == 0))
            {
                // New polygon overrides the existing polygon
                currentInfo.interiorCategory = intersectionCategory;
                allInfos[surfaceLoopIndex] = currentInfo;
                //Debug.Assert(holeIndices.IsAllocated(surfaceLoopIndex));
                return; 
            }

            NativeCollectionHelpers.EnsureMinimumSize(ref outEdges, maxLength);

            //var outEdges        = stackalloc Edge[maxLength];
            var outEdgesLength  = 0;

            // polygon2 edges Completely inside polygon1
            if (outside1 == 0 && inside2 == 0)
            {
                // polygon1 Completely inside polygon2
                for (int n = 0; n < intersectionLoop.Length; n++)
                {
                    outEdges[outEdgesLength] = intersectionLoop[n];
                    outEdgesLength++;
                }
                //OperationResult.Polygon1InsidePolygon2;
            } else
            {
                //int outEdgesLength = 0; // Can't read from outEdges.Length since it's marked as WriteOnly
                for (int e = 0; e < intersectionLoop.Length; e++)
                {
                    var category = categories1[e];
                    if (category == EdgeCategory.Inside)
                    {
                        outEdges[outEdgesLength] = intersectionLoop[e];
                        outEdgesLength++;
                    }
                }

                for (int e = 0; e < currentLoopEdges.Length; e++)
                {
                    var category = categories2[e];
                    if (category != EdgeCategory.Outside)
                    {
                        outEdges[outEdgesLength] = currentLoopEdges[e];
                        outEdgesLength++;
                    }
                }
                //OperationResult.Cut;
            }

            if (outEdgesLength < 3)
                return;

            // FIXME: when brush_intersection and categorized_loop are grazing each other, 
            //          technically we cut it but we shouldn't be creating it as a separate polygon + hole (bug7)

            // the output of cutting operations are both holes for the original polygon (categorized_loop)
            // and new polygons on the surface of the brush that need to be categorized
            intersectionInfo.interiorCategory = intersectionCategory;


            var brushesTouchedByBrush = brushesTouchedByBrushCache[currentBrushOrder];
            if (currentHoleIndices.Length > 0 &&
                // TODO: fix touching not being updated properly
                brushesTouchedByBrush != ChiselBlobAssetReference<BrushesTouchedByBrush>.Null)
            {
                NativeCollectionHelpers.EnsureMinimumSize(ref intersectedHoleIndices, currentHoleIndices.Length);
                var intersectedHoleIndicesLength = 0;

                // the output of cutting operations are both holes for the original polygon (categorized_loop)
                // and new polygons on the surface of the brush that need to be categorized

                ref var brushesTouchedByBrushRef    = ref brushesTouchedByBrush.Value;
                ref var brushIntersections          = ref brushesTouchedByBrushRef.brushIntersections;
                for (int h = 0; h < currentHoleIndices.Length; h++)
                {
                    // Need to make a copy so we can edit it without causing side effects
                    var holeIndex = currentHoleIndices[h];
                    var holeEdges = allEdges[holeIndex];
                    if (holeEdges.Length < 3)
                        continue;

                    var holeInfo            = allInfos[holeIndex];
                    var holeBrushNodeID     = holeInfo.brushIndexOrder.compactNodeID;/**/

                    bool touches = brushesTouchedByBrushRef.Get(holeBrushNodeID) != IntersectionType.NoIntersection;
                    
                    // Only add if they touch
                    if (touches)
                    {
                        intersectedHoleIndices[intersectedHoleIndicesLength] = allEdges.Length;
                        intersectedHoleIndicesLength++;
                        holeIndices.AddAndAllocateWithCapacity(1);
                        if (allInfos.Capacity < allInfos.Length + 1)
                            allInfos.Capacity = allInfos.Length + 16;
                        allInfos.AddNoResize(holeInfo);
                        allEdges.AllocateItemAndAddValues(holeEdges);
                        //Debug.Assert(allEdges.Length == allInfos.Length);
                        //Debug.Assert(allInfos.Length == holeIndices.Length);
                        //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));
                    }
                }

                // This loop is a hole 
                if (currentHoleIndices.Capacity < currentHoleIndices.Length + 1) // TODO: figure out why capacity is sometimes not enough
                    currentHoleIndices.Capacity = currentHoleIndices.Length + 16;
                currentHoleIndices.AddNoResize(allEdges.Length);
                holeIndices.AddAndAllocateWithCapacity(1);
                if (allInfos.Capacity < allInfos.Length + 1)
                    allInfos.Capacity = allInfos.Length + 16; 
                allInfos.AddNoResize(intersectionInfo);
                allEdges.AllocateItemAndAddValues(outEdges, outEdgesLength);
                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);
                //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));

                // But also a polygon on its own
                if (loopIndices.Capacity < loopIndices.Length + 1) // TODO: figure out why capacity is sometimes not enough
                    loopIndices.Capacity = loopIndices.Length + 16;
                loopIndices.AddNoResize(allEdges.Length);
                holeIndices.AllocateItemAndAddValues(intersectedHoleIndices, intersectedHoleIndicesLength);
                if (allInfos.Capacity < allInfos.Length + 1)
                    allInfos.Capacity = allInfos.Length + 16; 
                allInfos.AddNoResize(intersectionInfo);
                allEdges.AllocateItemAndAddValues(outEdges, outEdgesLength);
                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);
                //Debug.Assert(holeIndices.IsAllocated(allEdges.Length - 1));
            } else
            {
                // This loop is a hole 
                currentHoleIndices.AddNoResize(allEdges.Length);
                holeIndices.AddAndAllocateWithCapacity(1);
                if (allInfos.Capacity < allInfos.Length + 1)
                    allInfos.Capacity = allInfos.Length + 16;
                allInfos.AddNoResize(intersectionInfo);
                allEdges.AllocateItemAndAddValues(outEdges, outEdgesLength);
                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);
                //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));

                // But also a polygon on its own
                if (loopIndices.Capacity < loopIndices.Length + 1) // TODO: figure out why capacity is sometimes not enough
                    loopIndices.Capacity = loopIndices.Length + 16;
                loopIndices.AddNoResize(allEdges.Length);
                holeIndices.AddAndAllocateWithCapacity(1);
                if (allInfos.Capacity < allInfos.Length + 1)
                    allInfos.Capacity = allInfos.Length + 16;
                allInfos.AddNoResize(intersectionInfo);
                allEdges.AllocateItemAndAddValues(outEdges, outEdgesLength);
                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);
                //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));
            }
        }

        internal static float3 CalculatePlaneNormal(in NativeListArray<Edge>.NativeList edges, in HashedVertices hashedVertices)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var normal      = float3.zero;
            var vertices    = hashedVertices;
            for (int n = 0; n < edges.Length; n++)
            {
                var edge = edges[n];
                var prevVertex = vertices[(int)edge.index1];
                var currVertex = vertices[(int)edge.index2];
                normal.x = normal.x + ((prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z));
                normal.y = normal.y + ((prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x));
                normal.z = normal.z + ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));
            }
            normal = math.normalizesafe(normal);

            return normal;
        }

        void CleanUp(in NativeList<IndexSurfaceInfo> allInfos, in NativeListArray<Edge> allEdges, in HashedVertices brushVertices, ref NativeListArray<int>.NativeList loopIndices, in NativeListArray<int> holeIndices)
        {
            for (int l = loopIndices.Length - 1; l >= 0; l--)
            {
                var baseloopIndex   = loopIndices[l];
                var baseLoopEdges   = allEdges[baseloopIndex];


                // Remove degenerate edges that loop back on itself
                TryNext:
                if (baseLoopEdges.Length >= 3)
                {
                    for (int a = 0; a < baseLoopEdges.Length; a++)
                    {
                        for (int b = a + 1; b < baseLoopEdges.Length; b++)
                        {
                            if (baseLoopEdges[a].index1 == baseLoopEdges[b].index2 &&
                                baseLoopEdges[a].index2 == baseLoopEdges[b].index1)
                            {
                                //Debug.Log($"loop [{a},{b}] ({baseLoopEdges[a].index1}, {baseLoopEdges[a].index2}) ({baseLoopEdges[b].index1}, {baseLoopEdges[b].index2})");
                                baseLoopEdges.RemoveAtSwapBack(b);
                                baseLoopEdges.RemoveAtSwapBack(a);
                                goto TryNext;
                            }
                        }
                    }
                }

                if (baseLoopEdges.Length < 3)
                {
                    baseLoopEdges.Clear();
                    continue;
                }

                var surfaceLoopInfo     = allInfos[baseloopIndex];
                var interiorCategory    = (CategoryIndex)surfaceLoopInfo.interiorCategory;
                if (interiorCategory != CategoryIndex.ValidAligned &&
                    interiorCategory != CategoryIndex.ValidReverseAligned)
                {
                    baseLoopEdges.Clear();
                    continue;
                }

                var baseLoopNormal = CalculatePlaneNormal(in baseLoopEdges, in brushVertices);
                if (math.all(baseLoopNormal == float3.zero))
                {
                    baseLoopEdges.Clear();
                    continue;
                }

                var holeIndicesList = holeIndices[baseloopIndex];
                if (holeIndicesList.Length == 0)
                    continue;


                for (int h = holeIndicesList.Length - 1; h >= 0; h--)
                {
                    var holeIndex   = holeIndicesList[h];
                    var holeEdges   = allEdges[holeIndex];
                    
                    // Remove degenerate edges that loop back on itself
                    TryNextHole:
                    if (holeEdges.Length >= 3)
                    {
                        for (int a = 0; a < holeEdges.Length; a++)
                        {
                            for (int b = a + 1; b < holeEdges.Length; b++)
                            {
                                if (holeEdges[a].index1 == holeEdges[b].index2 &&
                                    holeEdges[a].index2 == holeEdges[b].index1)
                                {
                                    //Debug.Log($"hole [{a},{b}] ({holeEdges[a].index1}, {holeEdges[a].index2}) ({holeEdges[b].index1}, {holeEdges[b].index2})");
                                    holeEdges.RemoveAtSwapBack(b);
                                    holeEdges.RemoveAtSwapBack(a);
                                    goto TryNextHole;
                                }
                            }
                        }
                    }

                    if (holeEdges.Length < 3)
                    {
                        holeIndicesList.RemoveAtSwapBack(h);
                        continue;
                    }

                    var holeNormal = CalculatePlaneNormal(in holeEdges, in brushVertices);
                    if (math.all(holeNormal == float3.zero))
                    {
                        holeIndicesList.RemoveAtSwapBack(h);
                        continue;
                    }
                }
                if (holeIndicesList.Length == 0)
                    continue;

                int totalPlaneCount = 0;
                int totalEdgeCount = baseLoopEdges.Length;
                {
                    int brushNodeOrder  = allInfos[baseloopIndex].brushIndexOrder.nodeOrder;
                    ref var treeSpacePlanes = ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes;
                    totalPlaneCount += treeSpacePlanes.Length;
                }
                for (int h = 0; h < holeIndicesList.Length; h++)
                {
                    var holeIndex = holeIndicesList[h];
                    var holeEdges = allEdges[holeIndex];
                    totalEdgeCount += holeEdges.Length;
                    
                    int brushNodeOrder  = allInfos[holeIndex].brushIndexOrder.nodeOrder;
                    ref var treeSpacePlanes = ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes;

                    totalPlaneCount += treeSpacePlanes.Length;
                }


                NativeCollectionHelpers.EnsureCapacityAndClear(ref alltreeSpacePlanes, totalPlaneCount);
                NativeCollectionHelpers.EnsureCapacityAndClear(ref allSegments, holeIndicesList.Length + 1);
                NativeCollectionHelpers.EnsureCapacityAndClear(ref allCombinedEdges, totalEdgeCount);

                {                
                    int edgeOffset = 0;
                    int planeOffset = 0;
                    for (int h = 0; h < holeIndicesList.Length; h++)
                    {
                        var holeIndex   = holeIndicesList[h];
                        var holeEdges   = allEdges[holeIndex];

                        // TODO: figure out why sometimes polygons are flipped around, and try to fix this at the source
                        var holeNormal  = CalculatePlaneNormal(in holeEdges, in brushVertices);
                        if (math.dot(holeNormal, baseLoopNormal) > 0)
                        {
                            for (int n = 0; n < holeEdges.Length; n++)
                            {
                                var holeEdge = holeEdges[n];
                                var i1 = holeEdge.index1;
                                var i2 = holeEdge.index2;
                                holeEdge.index1 = i2;
                                holeEdge.index2 = i1;
                                holeEdges[n] = holeEdge;
                            }
                        }

                        int brushNodeOrder = allInfos[holeIndex].brushIndexOrder.nodeOrder;
                        ref var treeSpacePlanes = ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes;
                        
                        var planesLength    = treeSpacePlanes.Length;
                        var edgesLength     = holeEdges.Length;

                        allSegments.AddNoResize(new LoopSegment
                        {
                            edgeOffset      = edgeOffset,
                            edgeLength      = edgesLength,
                            planesOffset    = planeOffset,
                            planesLength    = planesLength
                        });

                        allCombinedEdges.AddRangeNoResize(holeEdges);

                        // TODO: ideally we'd only use the planes that intersect our edges
                        alltreeSpacePlanes.AddRangeNoResize(ref treeSpacePlanes, planesLength);

                        edgeOffset += edgesLength;
                        planeOffset += planesLength;
                    }
                    if (baseLoopEdges.Length > 0)
                    {
                        int brushNodeOrder = allInfos[baseloopIndex].brushIndexOrder.nodeOrder;
                        ref var treeSpacePlanes = ref brushTreeSpacePlaneCache[brushNodeOrder].Value.treeSpacePlanes;

                        var planesLength    = treeSpacePlanes.Length;
                        var edgesLength     = baseLoopEdges.Length;

                        allSegments.AddNoResize(new LoopSegment
                        {
                            edgeOffset      = edgeOffset,
                            edgeLength      = edgesLength,
                            planesOffset    = planeOffset,
                            planesLength    = planesLength
                        });

                        allCombinedEdges.AddRangeNoResize(baseLoopEdges);

                        // TODO: ideally we'd only use the planes that intersect our edges
                        alltreeSpacePlanes.AddRangeNoResize(ref treeSpacePlanes, planesLength);

                        edgeOffset += edgesLength;
                        planeOffset += planesLength;
                    }

                    //*
                    NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref destroyedEdges, edgeOffset);
                    /*/
                    var destroyedEdges = stackalloc byte[edgeOffset];
                    UnsafeUtility.MemSet(destroyedEdges, 0, edgeOffset);
                    //*/

                    var allCombinedEdgesArray = allCombinedEdges.AsArray();

                    {
                        {
                            var segment1 = allSegments[holeIndicesList.Length];
                            for (int j = 0; j < holeIndicesList.Length; j++)
                            {
                                var segment2 = allSegments[j];

                                if (segment1.edgeLength == 0 ||
                                    segment2.edgeLength == 0)
                                    continue;

                                for (int e = 0; e < segment1.edgeLength; e++)
                                {
                                    var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdgesArray[segment1.edgeOffset + e], in alltreeSpacePlanes, in allCombinedEdgesArray, segment2, in brushVertices);
                                    if (category == EdgeCategory.Outside || category == EdgeCategory.Aligned)
                                        continue;
                                    destroyedEdges.Set(segment1.edgeOffset + e, true);
                                }

                                for (int e = 0; e < segment2.edgeLength; e++)
                                {
                                    var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdgesArray[segment2.edgeOffset + e], in alltreeSpacePlanes, in allCombinedEdgesArray, segment1, in brushVertices);
                                    if (category == EdgeCategory.Inside)
                                        continue;
                                    destroyedEdges.Set(segment2.edgeOffset + e, true);
                                }
                            }
                        }

                        // TODO: optimize, keep track which holes (potentially) intersect
                        // TODO: create our own bounds data structure that doesn't use stupid slow properties for everything
                        {
                            for (int j = 0, length = GeometryMath.GetTriangleArraySize(holeIndicesList.Length); j < length; j++)
                            {
                                var arrayIndex = GeometryMath.GetTriangleArrayIndex(j, holeIndicesList.Length);
                                var segmentIndex1 = arrayIndex.x;
                                var segmentIndex2 = arrayIndex.y;
                                var segment1 = allSegments[segmentIndex1];
                                var segment2 = allSegments[segmentIndex2];
                                if (segment1.edgeLength > 0 && segment2.edgeLength > 0)
                                {
                                    for (int e = 0; e < segment1.edgeLength; e++)
                                    {
                                        var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdgesArray[segment1.edgeOffset + e], in alltreeSpacePlanes, in allCombinedEdgesArray, segment2, in brushVertices);
                                        if (category == EdgeCategory.Outside ||
                                            category == EdgeCategory.Aligned)
                                            continue;
                                        destroyedEdges.Set(segment1.edgeOffset + e, true);
                                    }

                                    for (int e = 0; e < segment2.edgeLength; e++)
                                    {
                                        var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdgesArray[segment2.edgeOffset + e], in alltreeSpacePlanes, in allCombinedEdgesArray, segment1, in brushVertices);
                                        if (category == EdgeCategory.Outside)
                                            continue;
                                        destroyedEdges.Set(segment2.edgeOffset + e, true);
                                    }
                                }
                            }
                        }

                        {
                            var segment = allSegments[holeIndicesList.Length];
                            for (int e = baseLoopEdges.Length - 1; e >= 0; e--)
                            {
                                if (!destroyedEdges.IsSet(segment.edgeOffset + e))
                                    continue;
                                baseLoopEdges.RemoveAtSwapBack(e);
                            }
                        }

                        for (int h1 = holeIndicesList.Length - 1; h1 >= 0; h1--)
                        {
                            var holeIndex1  = holeIndicesList[h1];
                            var holeEdges1  = allEdges[holeIndex1];
                            var segment     = allSegments[h1];
                            for (int e = holeEdges1.Length - 1; e >= 0; e--)
                            {
                                if (!destroyedEdges.IsSet(segment.edgeOffset + e))
                                    continue;
                                holeEdges1.RemoveAtSwapBack(e);
                            }
                        }
                    }
                }
                //alltreeSpacePlanes  .Dispose();
                //allSegments     .Dispose();
                //allCombinedEdges.Dispose();

                for (int h = holeIndicesList.Length - 1; h >= 0; h--)
                {
                    var holeIndex   = holeIndicesList[h];
                    var holeEdges   = allEdges[holeIndex];


                    // TODO: why is baseLoopEdges sometimes not properly allocated?
                    if (baseLoopEdges.Capacity < baseLoopEdges.Length + holeEdges.Length)
                        baseLoopEdges.Capacity = baseLoopEdges.Capacity + (holeEdges.Length * 2);

                    // Note: can have duplicate edges when multiple holes share an edge
                    //          (only edges between holes and base-loop are guaranteed to not be duplciate)
                    AddEdgesNoResize(baseLoopEdges, holeEdges);
                }

                holeIndicesList.Clear();
            }

            // TODO: remove the need for this
            for (int l = loopIndices.Length - 1; l >= 0; l--)
            {
                var baseloopIndex   = loopIndices[l];
                var baseLoopEdges   = allEdges[baseloopIndex];
                if (baseLoopEdges.Length < 3)
                {
                    loopIndices.RemoveAtSwapBack(l);
                    continue;
                }
            }
        }

        public void Execute(int index)
        {
            var count = input.BeginForEachIndex(index);
            if (count == 0)
            {
                input.EndForEachIndex();

                output.BeginForEachIndex(index);
                output.Write(new IndexOrder());
                output.Write(0);
                output.Write(0);
                output.Write(0);
                output.EndForEachIndex();
                return;
            }

            var brushIndexOrder = input.Read<IndexOrder>();
            var brushNodeOrder = brushIndexOrder.nodeOrder;
            var surfaceCount = input.Read<int>();

            var inputVertices = loopVerticesLookup[brushNodeOrder];
            var vertexCount = inputVertices.Length;

            NativeCollectionHelpers.EnsureCapacityAndClear(ref hashedTreeSpaceVertices, vertexCount);
            NativeCollectionHelpers.EnsureMinimumSize(ref indexRemap, vertexCount);
            

            hashedTreeSpaceVertices.Clear();
            for (int v = 0; v < inputVertices.Length; v++)
            {
                var vertex = inputVertices[v];
                indexRemap[v] = hashedTreeSpaceVertices.AddNoResize(vertex);
            }
            //Debug.Assert(hashedTreeSpaceVertices.Length == inputVertices.Length);

            var basePolygonEdgesLength = input.Read<int>();
            NativeCollectionHelpers.EnsureSizeAndClear(ref basePolygonSurfaceInfos, basePolygonEdgesLength);
            NativeCollectionHelpers.EnsureSizeAndClear(ref basePolygonEdges, basePolygonEdgesLength);
            int polygonIndex = 0;
            for (int l = 0; l < basePolygonEdgesLength; l++)
            {
                var indexSurfaceInfo = input.Read<IndexSurfaceInfo>();
                //if (l >= basePolygonSurfaceInfos.Length)
                //    Debug.Log("F");
                basePolygonSurfaceInfos[l] = indexSurfaceInfo;

                var edgesLength     = input.Read<int>();
                if (edgesLength == 0)
                {
                    //if (l >= basePolygonEdges.Length)
                    //    Debug.Log("C");
                    if (basePolygonEdges.IsIndexCreated(l))
                        basePolygonEdges[l].Clear();
                    continue;
                }
                //if (l >= basePolygonEdges.Length)
                //    Debug.Log($"D {l} {basePolygonEdges.Length} {basePolygonSurfaceInfos.Length} {basePolygonEdgesLength} {basePolygonEdges.Length}");
                var edgesInner      = basePolygonEdges.AllocateWithCapacityForIndex(l, edgesLength);
                //Debug.Log("E");
                //edgesInner.ResizeUninitialized(edgesLength);
                for (int e = 0; e < edgesLength; e++)
                {
                    var edge = input.Read<Edge>();
                    edge.index1 = indexRemap[edge.index1];
                    edge.index2 = indexRemap[edge.index2];
                    if (edge.index1 == edge.index2)
                        continue;
                    //Debug.Assert(edge.index1 >= 0 && edge.index1 < hashedTreeSpaceVertices.Length);
                    //Debug.Assert(edge.index2 >= 0 && edge.index2 < hashedTreeSpaceVertices.Length);
                    //if (edgesInner.Length + 1 >= edgesInner.Capacity)
                    //    Debug.Log("E");
                    edgesInner.AddNoResize(edge);
                }
                if (edgesInner.Length < 3)
                    edgesInner.Clear();
            }
            //basePolygonSurfaceInfos.ResizeUninitialized(polygonIndex);
            //basePolygonEdges.ResizeExact(polygonIndex);

            var intersectionEdgesLength = input.Read<int>();
            NativeCollectionHelpers.EnsureSizeAndClear(ref intersectionSurfaceInfos, intersectionEdgesLength);
            NativeCollectionHelpers.EnsureSizeAndClear(ref intersectionEdges, intersectionEdgesLength);
            polygonIndex = 0;
            for (int l = 0; l < intersectionEdgesLength; l++)
            {
                var indexSurfaceInfo = input.Read<IndexSurfaceInfo>();
                var edgesLength = input.Read<int>();
                var edgesInner  = intersectionEdges.AllocateWithCapacityForIndex(polygonIndex, edgesLength);
                //edgesInner.ResizeUninitialized(edgesLength);
                for (int e = 0; e < edgesLength; e++)
                {
                    var edge = input.Read<Edge>();
                    edge.index1 = indexRemap[edge.index1];
                    edge.index2 = indexRemap[edge.index2];
                    if (edge.index1 == edge.index2)
                        continue;
                    //Debug.Assert(edge.index1 >= 0 && edge.index1 < hashedTreeSpaceVertices.Length);
                    //Debug.Assert(edge.index2 >= 0 && edge.index2 < hashedTreeSpaceVertices.Length);
                    edgesInner.AddNoResize(edge);
                }
                if (edgesInner.Length >= 3)
                {
                    intersectionSurfaceInfos[polygonIndex] = indexSurfaceInfo;
                    polygonIndex++;
                } else
                    edgesInner.Clear();
            }
            intersectionSurfaceInfos.ResizeUninitialized(polygonIndex);
            intersectionEdges.ResizeExact(polygonIndex);
            input.EndForEachIndex();

            //int brushNodeIndex = treeBrushNodeIndices[index];

            if (surfaceCount == 0)
            {
                output.BeginForEachIndex(index);
                output.Write(brushIndexOrder);
                output.Write(0);
                output.Write(0);
                output.Write(0);
                output.EndForEachIndex();
                return;
            }


            ChiselBlobAssetReference<RoutingTable> routingTableRef = routingTableCache[brushNodeOrder];
            if (routingTableRef == ChiselBlobAssetReference<RoutingTable>.Null)
            {
                //Debug.LogError("No routing table found");
                output.BeginForEachIndex(index);
                output.Write(brushIndexOrder);
                output.Write(0);
                output.Write(0);
                output.Write(0);
                output.EndForEachIndex();
                return;
            }

            

            ref var nodeIDToTableIndex      = ref routingTableRef.Value.nodeIDToTableIndex;
            ref var nodeIDOffset            = ref routingTableRef.Value.nodeIDOffset;
            ref var routingLookups          = ref routingTableRef.Value.routingLookups;
            var routingLookupsLength        = routingLookups.Length;


            int maxIndex = intersectionSurfaceInfos.Length + (surfaceCount * routingLookupsLength);
            for (int i = 0; i < intersectionSurfaceInfos.Length; i++)
            {
                var surfaceInfo     = intersectionSurfaceInfos[i];
                var brushNodeID1    = surfaceInfo.brushIndexOrder.compactNodeID;

                // check if brush does not exist in routing table (will not have any effect)
                var idWithOffset = brushNodeID1.value - nodeIDOffset;
                if (idWithOffset < 0 || idWithOffset >= nodeIDToTableIndex.Length)
                    continue;

                var routingTableIndex = nodeIDToTableIndex[idWithOffset];
                if (routingTableIndex == -1)
                    continue;

                var surfaceIndex = surfaceInfo.basePlaneIndex;
                maxIndex = math.max(maxIndex, routingTableIndex + (surfaceIndex * routingLookupsLength));
            }


            int intersectionLoopCount = maxIndex + 1;
            NativeCollectionHelpers.EnsureSizeAndClear(ref intersectionLoops, intersectionLoopCount);
            NativeCollectionHelpers.EnsureMinimumSize(ref intersectionSurfaceInfo, intersectionLoopCount);

            {
                // TODO: Sort the brushSurfaceInfos/intersectionEdges based on nodeIndexToTableIndex[surfaceInfo.brushNodeID], 
                //       have a sequential list of all data. 
                //       Have segment list to determine which part of array belong to which brushNodeID
                //       Don't need bottom part, can determine this in Job

                for (int i = 0; i < intersectionSurfaceInfos.Length; i++)
                {
                    var surfaceInfo      = intersectionSurfaceInfos[i];
                    var brushNodeID1     = surfaceInfo.brushIndexOrder.compactNodeID;

                    // check if brush does not exist in routing table (will not have any effect)
                    var idWithOffset = brushNodeID1.value - nodeIDOffset;
                    if (idWithOffset < 0 || idWithOffset >= nodeIDToTableIndex.Length)
                        continue;

                    var routingTableIndex = nodeIDToTableIndex[idWithOffset];
                    if (routingTableIndex == -1)
                        continue;

                    var surfaceIndex    = surfaceInfo.basePlaneIndex;
                    int offset          = routingTableIndex + (surfaceIndex * routingLookupsLength);

                    var srcEdges = intersectionEdges[i];
                    var loops = intersectionLoops.AllocateWithCapacityForIndex(offset, srcEdges.Length);
                    loops.AddRangeNoResize(srcEdges);
                    intersectionSurfaceInfo[offset] = surfaceInfo;
                }
            }


            var maxLoops            = (routingLookupsLength + routingLookupsLength) * (surfaceCount + surfaceCount); // TODO: find a more reliable "max"

            NativeCollectionHelpers.EnsureConstantSizeAndClear(ref holeIndices, maxLoops);
            NativeCollectionHelpers.EnsureSizeAndClear(ref surfaceLoopIndices, surfaceCount);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref allInfos, maxLoops);
            NativeCollectionHelpers.EnsureConstantSizeAndClear(ref allEdges, maxLoops);


            ref var routingTable = ref routingTableRef.Value;
            for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
            {
                if (!basePolygonEdges.IsIndexCreated(surfaceIndex))
                    continue;
                var basePolygonSrc = basePolygonEdges[surfaceIndex];
                if (basePolygonSrc.Length < 3)
                    continue;

                var info = basePolygonSurfaceInfos[surfaceIndex];
                info.interiorCategory = CategoryGroupIndex.First; // TODO: make sure that it's always set to "First" so we don't need to do this

                var maxAllocation       = 1 + (2 * (routingLookupsLength + allEdges.Length)); // TODO: find a more reliable "max"
                var maxEdgeAllocation   = 1 + (hashedTreeSpaceVertices.Length * 2);

                var loopIndices = surfaceLoopIndices.AllocateWithCapacityForIndex(surfaceIndex, maxAllocation);

                loopIndices.AddNoResize(allEdges.Length);                
                holeIndices.AddAndAllocateWithCapacity(maxAllocation);
                allInfos   .AddNoResize(info);
                //Debug.Assert(holeIndices.IsAllocated(allInfos.Length - 1));

                var basePolygonDst = allEdges.AddAndAllocateWithCapacity(basePolygonSrc.Length + maxEdgeAllocation);// TODO: find a more reliable "max"
                basePolygonDst.AddRangeNoResize(basePolygonSrc);

                //Debug.Assert(allEdges.Length == allInfos.Length);
                //Debug.Assert(allInfos.Length == holeIndices.Length);

                for (int routingTableIndex = 0; routingTableIndex < routingLookupsLength; routingTableIndex++)
                {
                    int offset              = routingTableIndex + (surfaceIndex * routingLookupsLength);
                    ref var routingLookup   = ref routingLookups[routingTableIndex];
                    var intersectionLoop    = intersectionLoops.SafeGet(offset);
                    var intersectionInfo    = intersectionSurfaceInfo[offset];
                    for (int l = loopIndices.Length - 1; l >= 0; l--)
                    {
                        var surfaceLoopIndex = loopIndices[l];
                        var surfaceLoopEdges = allEdges[surfaceLoopIndex];
                        if (surfaceLoopEdges.Length < 3)
                            continue;

                        var surfaceLoopInfo = allInfos[surfaceLoopIndex];
                        //Debug.Assert(holeIndices.IsAllocated(surfaceLoopIndex));

                        // Lookup categorization between original surface & other surface ...
                        if (!routingLookup.TryGetRoute(ref routingTable, surfaceLoopInfo.interiorCategory, out CategoryRoutingRow routingRow))
                        { 
                            Debug.Assert(false, "Could not find route");
                            continue;
                        }

                        bool overlap = intersectionLoop.Length != 0 &&
                                        BooleanEdgesUtility.AreLoopsOverlapping(in surfaceLoopEdges, in intersectionLoop);

                        if (overlap)
                        {
                            // If we overlap don't bother with creating a new polygon & hole and just reuse existing polygon + replace category
                            surfaceLoopInfo.interiorCategory = routingRow[(int)intersectionInfo.interiorCategory];
                            allInfos[surfaceLoopIndex] = surfaceLoopInfo;
                            continue;
                        } else
                        {
                            surfaceLoopInfo.interiorCategory = routingRow[(int)CategoryIndex.Outside];
                            allInfos[surfaceLoopIndex] = surfaceLoopInfo;
                        }

                        // Add all holes that share the same plane to the polygon
                        if (intersectionLoop.Length != 0)
                        {
                            // Categorize between original surface & intersection
                            var intersectionCategory = routingRow[(int)intersectionInfo.interiorCategory];

                            // If the intersection polygon would get the same category, we don't need to do a pointless intersection
                            if (intersectionCategory == surfaceLoopInfo.interiorCategory)
                                continue;

                            IntersectLoops(in hashedTreeSpaceVertices, ref loopIndices, surfaceLoopIndex,
                                           ref holeIndices, ref allInfos, ref allEdges,
                                           in intersectionLoop, 
                                           intersectionCategory, 
                                           intersectionInfo);
                        }
                    }
                }
                CleanUp(in allInfos, in allEdges, in hashedTreeSpaceVertices, ref loopIndices, in holeIndices);
            }

            output.BeginForEachIndex(index);
            output.Write(brushIndexOrder);
            output.Write(hashedTreeSpaceVertices.Length);
            for (int l = 0; l < hashedTreeSpaceVertices.Length; l++)
                output.Write(hashedTreeSpaceVertices[l]);

            output.Write(surfaceLoopIndices.Length);
            for (int o = 0; o < surfaceLoopIndices.Length; o++)
            {
                if (!surfaceLoopIndices.IsIndexCreated(o))
                {
                    output.Write(0);
                    continue;
                }
                var inner = surfaceLoopIndices[o];
                output.Write(inner.Length);
                for (int i = 0; i < inner.Length; i++)
                    output.Write(inner[i]);
            }

            output.Write(allEdges.Length);
            for (int l = 0; l < allEdges.Length; l++)
            {
                var surfaceInfo = allInfos[l];
                output.Write(new SurfaceInfo { basePlaneIndex = surfaceInfo.basePlaneIndex, interiorCategory = surfaceInfo.interiorCategory//, nodeIndex = surfaceInfo.brushIndexOrder.nodeIndex 
                                });
                var edges = allEdges[l].AsArray();
                output.Write(edges.Length);
                for (int e = 0; e < edges.Length; e++)
                    output.Write(edges[e]);
            }
            output.EndForEachIndex();


            //intersectionLoops.Dispose();
            //holeIndices.Dispose();
        }
    }
} 
