using System;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    unsafe struct PerformCSGJob : IJobParallelFor
    {
        // 'Required' for scheduling with index count
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                          treeBrushNodeIndexOrders;        

        [NoAlias, ReadOnly] public NativeArray<int>                                                 nodeIndexToNodeOrder;
        [NoAlias, ReadOnly] public int                                                              nodeIndexToNodeOrderOffset;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<RoutingTable>>                    routingTableLookup;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>            brushTreeSpacePlanes;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushesTouchedByBrush>>    brushesTouchedByBrushes;

        [NoAlias, ReadOnly] public NativeStream.Reader      input;
        [NoAlias, WriteOnly] public NativeStream.Writer     output;


        struct Empty {}

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
        
        void IntersectLoopsJob(HashedVertices                   brushVertices,
                               
                               NativeListArray<int>.NativeList  loopIndices, 
                               int                              surfaceLoopIndex, 
                               
                               NativeListArray<int>             holeIndices,
                               NativeList<SurfaceInfo>          allInfos,
                               NativeListArray<Edge>            allEdges, 

                               NativeListArray<Edge>.NativeList intersectionLoop, 
                               CategoryGroupIndex               intersectionCategory, 
                               SurfaceInfo                      intersectionInfo)
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

            var maxLength       = math.max(intersectionLoop.Length, currentLoopEdges.Length);
            if (maxLength < 3)
                return;

            int inside2 = 0, outside2 = 0;
            var categories2             = stackalloc EdgeCategory[currentLoopEdges.Length];
            int intersectionBrushIndex  = intersectionInfo.brushIndex;
            int intersectionBrushOrder  = nodeIndexToNodeOrder[intersectionBrushIndex - nodeIndexToNodeOrderOffset];
            var treeSpacePlanes1        = brushTreeSpacePlanes[intersectionBrushOrder];
            for (int e = 0; e < currentLoopEdges.Length; e++)
            {
                var category = BooleanEdgesUtility.CategorizeEdge(currentLoopEdges[e], ref treeSpacePlanes1.Value.treeSpacePlanes, intersectionLoop, brushVertices);
                categories2[e] = category;
                if      (category == EdgeCategory.Inside) inside2++;
                else if (category == EdgeCategory.Outside) outside2++;
            }
            var aligned2 = currentLoopEdges.Length - (inside2 + outside2);

            int inside1 = 0, outside1 = 0;
            var categories1         = stackalloc EdgeCategory[intersectionLoop.Length];
            int currentBrushIndex   = currentInfo.brushIndex;
            int currentBrushOrder   = nodeIndexToNodeOrder[currentBrushIndex - nodeIndexToNodeOrderOffset];
            var treeSpacePlanes2    = brushTreeSpacePlanes[currentBrushOrder];
            for (int e = 0; e < intersectionLoop.Length; e++)
            {
                var category = BooleanEdgesUtility.CategorizeEdge(intersectionLoop[e], ref treeSpacePlanes2.Value.treeSpacePlanes, currentLoopEdges, brushVertices);
                categories1[e] = category;
                if      (category == EdgeCategory.Inside) inside1++;
                else if (category == EdgeCategory.Outside) outside1++;
            }
            var aligned1 = intersectionLoop.Length - (inside1 + outside1);

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
            

            var outEdges        = stackalloc Edge[maxLength];
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


            if (currentHoleIndices.Length > 0 &&
                // TODO: fix touching not being updated properly
                brushesTouchedByBrushes.ContainsKey(currentBrushIndex))
            {
                // Figure out why this is seemingly not necessary?
                var intersectedHoleIndices = stackalloc int[currentHoleIndices.Length];
                var intersectedHoleIndicesLength = 0;

                // the output of cutting operations are both holes for the original polygon (categorized_loop)
                // and new polygons on the surface of the brush that need to be categorized
                
                ref var brushesTouchedByBrush   = ref brushesTouchedByBrushes[currentBrushIndex].Value;
                ref var brushIntersections      = ref brushesTouchedByBrushes[currentBrushIndex].Value.brushIntersections;
                for (int h = 0; h < currentHoleIndices.Length; h++)
                {
                    // Need to make a copy so we can edit it without causing side effects
                    var holeIndex = currentHoleIndices[h];
                    var holeEdges = allEdges[holeIndex];
                    if (holeEdges.Length < 3)
                        continue;

                    var holeInfo            = allInfos[holeIndex];
                    int holeBrushNodeIndex  = holeInfo.brushIndex;

                    bool touches = brushesTouchedByBrush.Get(holeBrushNodeIndex) != IntersectionType.NoIntersection;
                    
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
                if (currentHoleIndices.Capacity < allEdges.Length) // TODO: figure out why capacity is sometimes not enough
                    currentHoleIndices.Capacity = allEdges.Length;
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

        internal static unsafe float3 CalculatePlaneNormal(NativeListArray<Edge>.NativeList edges, HashedVertices hashedVertices)
        {
            // Newell's algorithm to create a plane for concave polygons.
            // NOTE: doesn't work well for self-intersecting polygons
            var normal = Vector3.zero;
            var vertices = hashedVertices.GetUnsafeReadOnlyPtr();
            for (int n = 0; n < edges.Length; n++)
            {
                var edge = edges[n];
                var prevVertex = vertices[edge.index1];
                var currVertex = vertices[edge.index2];
                normal.x = normal.x + ((prevVertex.y - currVertex.y) * (prevVertex.z + currVertex.z));
                normal.y = normal.y + ((prevVertex.z - currVertex.z) * (prevVertex.x + currVertex.x));
                normal.z = normal.z + ((prevVertex.x - currVertex.x) * (prevVertex.y + currVertex.y));
            }
            normal = normal.normalized;

            return normal;
        }

        void CleanUp(NativeList<SurfaceInfo> allInfos, NativeListArray<Edge> allEdges, HashedVertices brushVertices, NativeListArray<int>.NativeList loopIndices, NativeListArray<int> holeIndices)
        {
            for (int l = loopIndices.Length - 1; l >= 0; l--)
            {
                var baseloopIndex   = loopIndices[l];
                var baseLoopEdges   = allEdges[baseloopIndex];
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

                var baseLoopNormal = CalculatePlaneNormal(baseLoopEdges, brushVertices);
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
                    if (holeEdges.Length < 3)
                    {
                        holeIndicesList.RemoveAtSwapBack(h);
                        continue;
                    }
                    var holeNormal = CalculatePlaneNormal(holeEdges, brushVertices);
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
                    int brushNodeIndex  = allInfos[baseloopIndex].brushIndex;
                    int brushNodeOrder  = nodeIndexToNodeOrder[brushNodeIndex - nodeIndexToNodeOrderOffset];
                    ref var treeSpacePlanes = ref brushTreeSpacePlanes[brushNodeOrder].Value.treeSpacePlanes;
                    totalPlaneCount += treeSpacePlanes.Length;
                }
                for (int h = 0; h < holeIndicesList.Length; h++)
                {
                    var holeIndex = holeIndicesList[h];
                    var holeEdges = allEdges[holeIndex];
                    totalEdgeCount += holeEdges.Length;
                    
                    int brushNodeIndex  = allInfos[holeIndex].brushIndex;
                    int brushNodeOrder  = nodeIndexToNodeOrder[brushNodeIndex - nodeIndexToNodeOrderOffset];
                    ref var treeSpacePlanes = ref brushTreeSpacePlanes[brushNodeOrder].Value.treeSpacePlanes;

                    totalPlaneCount += treeSpacePlanes.Length;
                }



                var alltreeSpacePlanes  = new NativeList<float4>(totalPlaneCount, Allocator.Temp);
                var allSegments         = new NativeList<LoopSegment>(holeIndicesList.Length + 1, Allocator.Temp);
                var allCombinedEdges    = new NativeList<Edge>(totalEdgeCount, Allocator.Temp);
                {                
                    int edgeOffset = 0;
                    int planeOffset = 0;
                    for (int h = 0; h < holeIndicesList.Length; h++)
                    {
                        var holeIndex   = holeIndicesList[h];
                        var holeEdges   = allEdges[holeIndex];

                        // TODO: figure out why sometimes polygons are flipped around, and try to fix this at the source
                        var holeNormal  = CalculatePlaneNormal(holeEdges, brushVertices);
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

                        int brushNodeIndex  = allInfos[holeIndex].brushIndex;
                        int brushNodeOrder  = nodeIndexToNodeOrder[brushNodeIndex - nodeIndexToNodeOrderOffset];
                        ref var treeSpacePlanes = ref brushTreeSpacePlanes[brushNodeOrder].Value.treeSpacePlanes;
                        
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
                        alltreeSpacePlanes.AddRangeNoResize(treeSpacePlanes.GetUnsafePtr(), planesLength);

                        edgeOffset += edgesLength;
                        planeOffset += planesLength;
                    }
                    if (baseLoopEdges.Length > 0)
                    {
                        int brushNodeIndex  = allInfos[baseloopIndex].brushIndex;
                        int brushNodeOrder  = nodeIndexToNodeOrder[brushNodeIndex - nodeIndexToNodeOrderOffset];
                        ref var treeSpacePlanes = ref brushTreeSpacePlanes[brushNodeOrder].Value.treeSpacePlanes;

                        var planesLength    = treeSpacePlanes.Length;
                        var edgesLength     = baseLoopEdges.Length;

                        allSegments.AddNoResize(new LoopSegment()
                        {
                            edgeOffset      = edgeOffset,
                            edgeLength      = edgesLength,
                            planesOffset    = planeOffset,
                            planesLength    = planesLength
                        });

                        allCombinedEdges.AddRangeNoResize(baseLoopEdges);

                        // TODO: ideally we'd only use the planes that intersect our edges
                        alltreeSpacePlanes.AddRangeNoResize(treeSpacePlanes.GetUnsafePtr(), planesLength);

                        edgeOffset += edgesLength;
                        planeOffset += planesLength;
                    }

                    var destroyedEdges = stackalloc byte[edgeOffset];
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
                                    var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdges[segment1.edgeOffset + e], alltreeSpacePlanes, allCombinedEdges, segment2, brushVertices);
                                    if (category == EdgeCategory.Outside || category == EdgeCategory.Aligned)
                                        continue;
                                    destroyedEdges[segment1.edgeOffset + e] = 1;
                                }

                                for (int e = 0; e < segment2.edgeLength; e++)
                                {
                                    var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdges[segment2.edgeOffset + e], alltreeSpacePlanes, allCombinedEdges, segment1, brushVertices);
                                    if (category == EdgeCategory.Inside)
                                        continue;
                                    destroyedEdges[segment2.edgeOffset + e] = 1;
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
                                        var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdges[segment1.edgeOffset + e], alltreeSpacePlanes, allCombinedEdges, segment2, brushVertices);
                                        if (category == EdgeCategory.Outside ||
                                            category == EdgeCategory.Aligned)
                                            continue;
                                        destroyedEdges[segment1.edgeOffset + e] = 1;
                                    }

                                    for (int e = 0; e < segment2.edgeLength; e++)
                                    {
                                        var category = BooleanEdgesUtility.CategorizeEdge(allCombinedEdges[segment2.edgeOffset + e], alltreeSpacePlanes, allCombinedEdges, segment1, brushVertices);
                                        if (category == EdgeCategory.Outside)
                                            continue;
                                        destroyedEdges[segment2.edgeOffset + e] = 1;
                                    }
                                }
                            }
                        }

                        {
                            var segment = allSegments[holeIndicesList.Length];
                            for (int e = baseLoopEdges.Length - 1; e >= 0; e--)
                            {
                                if (destroyedEdges[segment.edgeOffset + e] == 0)
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
                                if (destroyedEdges[segment.edgeOffset + e] == 0)
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
            HashedVertices          brushVertices;
            NativeList<SurfaceInfo> basePolygonSurfaceInfos;
            NativeListArray<Edge>   basePolygonEdges;
            NativeList<SurfaceInfo> intersectionSurfaceInfos;
            NativeListArray<Edge>   intersectionEdges;

            var count = input.BeginForEachIndex(index);
            if (count == 0)
                return;
            var brushNodeIndex = input.Read<int>();
            var brushNodeOrder = input.Read<int>();
            var surfaceCount = input.Read<int>();
            var vertexCount = input.Read<int>();
            brushVertices = new HashedVertices(vertexCount, Allocator.Temp);
            for (int v = 0; v < vertexCount; v++)
            {
                var vertex = input.Read<float3>();
                brushVertices.AddNoResize(vertex);
            }

            var basePolygonEdgesLength = input.Read<int>();
            basePolygonSurfaceInfos = new NativeList<SurfaceInfo>(basePolygonEdgesLength, Allocator.Temp);
            basePolygonEdges = new NativeListArray<Edge>(basePolygonEdgesLength, Allocator.Temp);

            basePolygonSurfaceInfos.ResizeUninitialized(basePolygonEdgesLength);
            basePolygonEdges.ResizeExact(basePolygonEdgesLength);
            for (int l = 0; l < basePolygonEdgesLength; l++)
            {
                basePolygonSurfaceInfos[l] = input.Read<SurfaceInfo>();
                var edgesLength     = input.Read<int>();
                var edgesInner      = basePolygonEdges.AllocateWithCapacityForIndex(l, edgesLength);
                //edgesInner.ResizeUninitialized(edgesLength);
                for (int e = 0; e < edgesLength; e++)
                {
                    edgesInner.AddNoResize(input.Read<Edge>());
                }
            }

            var intersectionEdgesLength = input.Read<int>();
            intersectionSurfaceInfos = new NativeList<SurfaceInfo>(intersectionEdgesLength, Allocator.Temp);
            intersectionEdges = new NativeListArray<Edge>(intersectionEdgesLength, Allocator.Temp);

            intersectionSurfaceInfos.ResizeUninitialized(intersectionEdgesLength);
            intersectionEdges.ResizeExact(intersectionEdgesLength);
            for (int l = 0; l < intersectionEdgesLength; l++)
            {
                intersectionSurfaceInfos[l] = input.Read<SurfaceInfo>();
                var edgesLength = input.Read<int>();
                var edgesInner  = intersectionEdges.AllocateWithCapacityForIndex(l, edgesLength);
                //edgesInner.ResizeUninitialized(edgesLength);
                for (int e = 0; e < edgesLength; e++)
                {
                    edgesInner.AddNoResize(input.Read<Edge>());
                }
            }
            input.EndForEachIndex();

            //int brushNodeIndex = treeBrushNodeIndices[index];

            if (surfaceCount == 0)
                return;


            BlobAssetReference<RoutingTable> routingTableRef = routingTableLookup[brushNodeOrder];
            if (routingTableRef == BlobAssetReference<RoutingTable>.Null)
            {
                //Debug.LogError("No routing table found");
                return;
            }

            


            ref var routingTableNodeIndices = ref routingTableRef.Value.nodes;
            ref var routingLookups          = ref routingTableRef.Value.routingLookups;
            var routingLookupsLength        = routingLookups.Length;

            var intersectionSurfaceInfo = stackalloc SurfaceInfo[routingTableNodeIndices.Length * surfaceCount];
            var intersectionLoops       = new NativeListArray<Edge>(0, Allocator.Temp);
            intersectionLoops.ResizeExact(intersectionSurfaceInfos.Length + (routingTableNodeIndices.Length * surfaceCount));

            {
                int maxIndex = 0;
                for (int i = 0; i < routingTableNodeIndices.Length; i++)
                    maxIndex = math.max(maxIndex, routingTableNodeIndices[i]);

                var nodeIndextoTableIndex = stackalloc int[maxIndex + 1];
                for (int i = 0; i < routingTableNodeIndices.Length; i++)
                    nodeIndextoTableIndex[routingTableNodeIndices[i]] = i + 1;

                // TODO: Sort the brushSurfaceInfos/intersectionEdges based on nodeIndextoTableIndex[surfaceInfo.brushNodeID], 
                //       have a sequential list of all data. 
                //       Have segment list to determine which part of array belong to which brushNodeID
                //       Don't need bottom part, can determine this in Job

                for (int i = 0; i < intersectionSurfaceInfos.Length; i++)
                {
                    var surfaceInfo         = intersectionSurfaceInfos[i];
                    int brushNodeIndex1     = surfaceInfo.brushIndex;

                    // brush does not exist in routing table (has been deduced to not have any effect)
                    if (!ChiselNativeListExtensions.Contains(ref routingTableNodeIndices, brushNodeIndex1))
                        continue;

                    var routingTableIndex = nodeIndextoTableIndex[brushNodeIndex1];
                    if (routingTableIndex == 0)
                        continue;

                    routingTableIndex--;

                    var surfaceIndex    = surfaceInfo.basePlaneIndex;
                    int offset          = routingTableIndex + (surfaceIndex * routingLookupsLength);

                    var srcEdges = intersectionEdges[i];
                    var loops = intersectionLoops.AllocateWithCapacityForIndex(offset, srcEdges.Length);
                    loops.AddRangeNoResize(srcEdges);
                    intersectionSurfaceInfo[offset] = surfaceInfo;
                }
            }


            var maxLoops            = (routingLookupsLength + routingLookupsLength) * (surfaceCount + surfaceCount); // TODO: find a more reliable "max"
            var holeIndices         = new NativeListArray<int>(maxLoops, Allocator.Temp);
            var surfaceLoopIndices  = new NativeListArray<int>(Allocator.Temp);
            surfaceLoopIndices.ResizeExact(surfaceCount);

            var allInfos = new NativeList<SurfaceInfo>(maxLoops, Allocator.Temp);
            var allEdges = new NativeListArray<Edge>(maxLoops, Allocator.Temp);


            ref var routingTable = ref routingTableRef.Value;
            for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
            {
                var basePolygonSrc = basePolygonEdges[surfaceIndex];
                if (basePolygonSrc.Length < 3)
                    continue;

                var info = basePolygonSurfaceInfos[surfaceIndex];
                info.interiorCategory = CategoryGroupIndex.First; // TODO: make sure that it's always set to "First" so we don't need to do this

                var maxAllocation       = 1 + (2 * (routingLookupsLength + allEdges.Length)); // TODO: find a more reliable "max"
                var maxEdgeAllocation   = 1 + (brushVertices.Length * 2);

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
                                        BooleanEdgesUtility.AreLoopsOverlapping(surfaceLoopEdges, intersectionLoop);

                        if (overlap)
                        {
                            // If we overlap don't bother with creating a new polygon & hole and just reuse existing polygon + replace category
                            surfaceLoopInfo.interiorCategory = routingRow[intersectionInfo.interiorCategory];
                            allInfos[surfaceLoopIndex] = surfaceLoopInfo;
                            continue;
                        } else
                        {
                            surfaceLoopInfo.interiorCategory = routingRow[CategoryIndex.Outside];
                            allInfos[surfaceLoopIndex] = surfaceLoopInfo;
                        }

                        // Add all holes that share the same plane to the polygon
                        if (intersectionLoop.Length != 0)
                        {
                            // Categorize between original surface & intersection
                            var intersectionCategory = routingRow[intersectionInfo.interiorCategory];

                            // If the intersection polygon would get the same category, we don't need to do a pointless intersection
                            if (intersectionCategory == surfaceLoopInfo.interiorCategory)
                                continue;

                            IntersectLoopsJob(brushVertices, loopIndices, surfaceLoopIndex,
                                              holeIndices, allInfos, allEdges, 
                                              intersectionLoop, 
                                              intersectionCategory, 
                                              intersectionInfo);
                        }
                    }
                }
                CleanUp(allInfos, allEdges, brushVertices, loopIndices, holeIndices);
            }

            output.BeginForEachIndex(index);
            output.Write(brushNodeIndex);
            output.Write(brushNodeOrder);
            output.Write(brushVertices.Length);
            for (int l = 0; l < brushVertices.Length; l++)
                output.Write(brushVertices[l]);

            output.Write(surfaceLoopIndices.Length);
            for (int o = 0; o < surfaceLoopIndices.Length; o++)
            {
                var inner = surfaceLoopIndices[o];
                output.Write(inner.Length);
                for (int i = 0; i < inner.Length; i++)
                    output.Write(inner[i]);
            }

            output.Write(allEdges.Length);
            for (int l = 0; l < allEdges.Length; l++)
            {
                output.Write(allInfos[l]);
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
