using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Chisel.Core
{

    [BurstCompile(CompileSynchronously = true)]
    struct FindLoopOverlapIntersectionsJob : IJobParallelFor
    { 
        public const int kMaxVertexCount        = short.MaxValue;
        const float kSqrVertexEqualEpsilon      = CSGConstants.kSqrVertexEqualEpsilon;
        const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;

        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                  treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>                                         nodeIndexToNodeOrder;
        [NoAlias, ReadOnly] public int                                                      nodeIndexToNodeOrderOffset;
        [NoAlias, ReadOnly] public int                                                      maxNodeOrder;
        [NoAlias, ReadOnly] public NativeMultiHashMap<int, BlobAssetReference<BrushIntersectionLoop>>  intersectionLoopBlobs;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BasePolygonsBlob>>        basePolygons;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushTreeSpacePlanes>>    brushTreeSpacePlanes;
        
        [NoAlias, WriteOnly] public NativeStream.Writer     output;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<Edge>         inputEdges;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>       srcIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<SurfaceInfo>  basePolygonSurfaceInfos;
        [NativeDisableContainerSafetyRestriction] NativeList<ushort>        tempList;
        [NativeDisableContainerSafetyRestriction] NativeList<BlobAssetReference<BrushIntersectionLoop>> brushIntersections;
        [NativeDisableContainerSafetyRestriction] NativeList<SurfaceInfo>   intersectionSurfaceInfos;
        [NativeDisableContainerSafetyRestriction] NativeListArray<Edge>     basePolygonEdges;
        [NativeDisableContainerSafetyRestriction] NativeListArray<Edge>     intersectionEdges;
        [NativeDisableContainerSafetyRestriction] NativeBitArray            usedNodeOrders;
        [NativeDisableContainerSafetyRestriction] HashedVertices            hashedVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<int2>         intersectionSurfaceSegments;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>       otherVertices;

        struct CompareSortByBasePlaneIndex : IComparer<BlobAssetReference<BrushIntersectionLoop>>
        {
            public int Compare(BlobAssetReference<BrushIntersectionLoop> x, BlobAssetReference<BrushIntersectionLoop> y)
            {
                var diff = x.Value.pair.basePlaneIndex - y.Value.pair.basePlaneIndex;
                if (diff != 0)
                    return diff;

                return x.Value.pair.brushNodeOrder1 - y.Value.pair.brushNodeOrder1;
            }
        }

        void CopyFrom(NativeListArray<Edge> dst, int index, ref BrushIntersectionLoop brushIntersectionLoop, HashedVertices hashedVertices, int extraCapacity)
        {
            ref var vertices = ref brushIntersectionLoop.loopVertices;

            if (!srcIndices.IsCreated || srcIndices.Length < vertices.Length)
            {
                if (srcIndices.IsCreated) srcIndices.Dispose();
                srcIndices = new NativeArray<ushort>(vertices.Length, Allocator.Temp);
            }

            //var srcIndices = stackalloc ushort[vertices.Length];
            hashedVertices.Reserve(vertices.Length);
            for (int j = 0; j < vertices.Length; j++)
                srcIndices[j] = hashedVertices.AddNoResize(vertices[j]);

            {
                var dstEdges = dst.AllocateWithCapacityForIndex(index, vertices.Length + extraCapacity);
                for (int j = 1; j < vertices.Length; j++)
                {
                    dstEdges.AddNoResize(new Edge { index1 = srcIndices[j - 1], index2 = srcIndices[j] });
                }
                dstEdges.AddNoResize(new Edge { index1 = srcIndices[vertices.Length - 1], index2 = srcIndices[0] });
            }
        }


        public void Execute(int index)
        {
            var brushIndexOrder     = treeBrushIndexOrders[index];
            int brushNodeIndex      = brushIndexOrder.nodeIndex;
            int brushNodeOrder      = brushIndexOrder.nodeOrder;

            // Can happen when BrushMeshes are not initialized correctly
            if (basePolygons[brushNodeOrder] == BlobAssetReference<BasePolygonsBlob>.Null)
                return;

            ref var basePolygonBlob = ref basePolygons[brushNodeOrder].Value;

            var surfaceCount        = basePolygonBlob.polygons.Length;
            if (surfaceCount == 0)
                return;

            var intersectionCount = intersectionLoopBlobs.CountValuesForKey(brushNodeOrder);
            if (intersectionCount == 0)
                return;

            if (!basePolygonSurfaceInfos.IsCreated || basePolygonSurfaceInfos.Length < surfaceCount)
            {
                if (basePolygonSurfaceInfos.IsCreated) basePolygonSurfaceInfos.Dispose();
                basePolygonSurfaceInfos = new NativeArray<SurfaceInfo>(surfaceCount, Allocator.Temp);
            }

            if (!hashedVertices.IsCreated)
            {
                hashedVertices = new HashedVertices(HashedVertices.kMaxVertexCount, Allocator.Temp);
            } else
                hashedVertices.Clear();

            if (!basePolygonEdges.IsCreated || basePolygonEdges.Capacity < surfaceCount)
            {
                if (basePolygonEdges.IsCreated) basePolygonEdges.Dispose();
                basePolygonEdges = new NativeListArray<Edge>(surfaceCount, Allocator.Temp);
            } else
                basePolygonEdges.ClearChildren();
            basePolygonEdges.ResizeExact(surfaceCount);
            
            if (!brushIntersections.IsCreated)
            {
                brushIntersections = new NativeList<BlobAssetReference<BrushIntersectionLoop>>(intersectionCount, Allocator.Temp);
            } else
            {
                brushIntersections.Clear();
                if (brushIntersections.Capacity < intersectionCount)
                    brushIntersections.Capacity = intersectionCount;
            }
 
            if (!usedNodeOrders.IsCreated || usedNodeOrders.Length < maxNodeOrder)
            {
                if (usedNodeOrders.IsCreated) usedNodeOrders.Dispose();
                usedNodeOrders = new NativeBitArray(maxNodeOrder, Allocator.Temp);
            } else
                usedNodeOrders.Clear();

            var uniqueBrushOrderCount = 0;
            var enumerator = intersectionLoopBlobs.GetValuesForKey(brushNodeOrder);
            
            while (enumerator.MoveNext())
            {
                var item                = enumerator.Current;
                ref var pair            = ref item.Value.pair;
                    
                var otherNodeOrder1 = pair.brushNodeOrder1;
                uniqueBrushOrderCount += usedNodeOrders.IsSet(otherNodeOrder1) ? 1 : 0;
                usedNodeOrders.Set(otherNodeOrder1, true);

                //Debug.Assert(outputSurface.surfaceInfo.brushIndex == pair.brushNodeIndex1);
                //Debug.Assert(outputSurface.surfaceInfo.basePlaneIndex == pair.basePlaneIndex);

                brushIntersections.Add(item);
            }


            
            hashedVertices.AddUniqueVertices(ref basePolygonBlob.vertices); /*OUTPUT*/

            if (uniqueBrushOrderCount == 0)
            {
                // If we don't have any intersection loops, just convert basePolygonBlob to loops and be done
                // TODO: should do this per surface!

                for (int s = 0; s < basePolygonBlob.polygons.Length; s++)
                {
                    ref var input = ref basePolygonBlob.polygons[s];

                    var edges = basePolygonEdges.AllocateWithCapacityForIndex(s, input.endEdgeIndex - input.startEdgeIndex);
                    for (int e = input.startEdgeIndex; e < input.endEdgeIndex; e++)
                        edges.AddNoResize(basePolygonBlob.edges[e]);

                    basePolygonSurfaceInfos[s] = basePolygonBlob.polygons[s].surfaceInfo;
                }

                if (intersectionEdges.IsCreated)
                    intersectionEdges.ClearChildren();

                if (intersectionSurfaceInfos.IsCreated)
                    intersectionSurfaceInfos.Clear();
                

                output.BeginForEachIndex(index);
                output.Write(brushNodeIndex);
                output.Write(brushNodeOrder);
                output.Write(surfaceCount);
                output.Write(hashedVertices.Length);
                for (int l = 0; l < hashedVertices.Length; l++)
                    output.Write(hashedVertices[l]);
            
                output.Write(basePolygonEdges.Length);
                for (int l = 0; l < basePolygonEdges.Length; l++)
                {
                    output.Write(basePolygonSurfaceInfos[l]);
                    var edges = basePolygonEdges[l].AsArray();
                    output.Write(edges.Length);
                    for (int e = 0; e < edges.Length; e++)
                        output.Write(edges[e]);
                }

                output.Write(0);
                output.EndForEachIndex();
            } else
            { 

                if (!intersectionEdges.IsCreated || intersectionEdges.Capacity < brushIntersections.Length)
                {
                    if (intersectionEdges.IsCreated) intersectionEdges.Dispose();
                    intersectionEdges = new NativeListArray<Edge>(brushIntersections.Length, Allocator.Temp);
                } else
                    intersectionEdges.ClearChildren();
                intersectionEdges.ResizeExact(brushIntersections.Length);

                var compareSortByBasePlaneIndex = new CompareSortByBasePlaneIndex();
                brushIntersections.Sort(compareSortByBasePlaneIndex);

                if (!intersectionSurfaceSegments.IsCreated || intersectionSurfaceSegments.Length < surfaceCount)
                {
                    if (intersectionSurfaceSegments.IsCreated) intersectionSurfaceSegments.Dispose();
                    intersectionSurfaceSegments = new NativeArray<int2>(surfaceCount, Allocator.Temp);
                }
                {
                    {
                        for (int s = 0; s < basePolygonBlob.polygons.Length; s++)
                        {
                            ref var input = ref basePolygonBlob.polygons[s];

                            var edges = basePolygonEdges.AllocateWithCapacityForIndex(s, (input.endEdgeIndex - input.startEdgeIndex) + (brushIntersections.Length * 4));
                            for (int e = input.startEdgeIndex; e < input.endEdgeIndex; e++)
                                edges.AddNoResize(basePolygonBlob.edges[e]);

                            basePolygonSurfaceInfos[s] = basePolygonBlob.polygons[s].surfaceInfo;
                        }

                        { 
                            int prevBasePlaneIndex = 0;
                            int startIndex = 0;
                            for (int l = 0; l < brushIntersections.Length; l++)
                            {
                                ref var brushIntersectionLoop   = ref brushIntersections[l].Value;
                                ref var surfaceInfo             = ref brushIntersectionLoop.surfaceInfo;
                                var basePlaneIndex = surfaceInfo.basePlaneIndex;
                                if (prevBasePlaneIndex != basePlaneIndex)
                                {
                                    intersectionSurfaceSegments[prevBasePlaneIndex] = new int2(startIndex, l - startIndex);
                                    startIndex = l;
                                    for (int s = prevBasePlaneIndex + 1; s < basePlaneIndex; s++)
                                        intersectionSurfaceSegments[s] = new int2(startIndex, 0);
                                    prevBasePlaneIndex = basePlaneIndex;
                                }
                                CopyFrom(intersectionEdges, l, ref brushIntersectionLoop, hashedVertices, brushIntersections.Length * 4);
                            }
                            {
                                intersectionSurfaceSegments[prevBasePlaneIndex] = new int2(startIndex, brushIntersections.Length - startIndex);
                                startIndex = brushIntersections.Length;
                                for (int s = prevBasePlaneIndex + 1; s < surfaceCount; s++)
                                    intersectionSurfaceSegments[s] = new int2(startIndex, 0);
                            }
                        }
                    }

                    for (int s = 0; s < surfaceCount; s++)
                    {
                        var intersectionSurfaceCount    = intersectionSurfaceSegments[s].y;
                        var intersectionSurfaceOffset   = intersectionSurfaceSegments[s].x;
                        for (int l0 = intersectionSurfaceCount - 1; l0 >= 0; l0--)
                        {
                            int intersectionBrushOrder0 = brushIntersections[intersectionSurfaceOffset + l0].Value.pair.brushNodeOrder1;
                            var edges                   = intersectionEdges[intersectionSurfaceOffset + l0];
                            for (int l1 = 0; l1 < intersectionSurfaceCount; l1++)
                            {
                                if (l0 == l1)
                                    continue;
                            
                                int intersectionBrushOrder1 = brushIntersections[intersectionSurfaceOffset + l1].Value.pair.brushNodeOrder1;// intersectionIndex1.w;

                                FindLoopPlaneIntersections(brushTreeSpacePlanes, intersectionBrushOrder1, intersectionBrushOrder0, hashedVertices, edges);

                                // TODO: merge these so that intersections will be identical on both loops (without using math, use logic)
                                // TODO: make sure that intersections between loops will be identical on OTHER brushes (without using math, use logic)
                            }
                        }
                    }

                    // TODO: should only intersect with all brushes that each particular basepolygon intersects with
                    //       but also need adjency information between basePolygons to ensure that intersections exist on 
                    //       both sides of each edge on a brush. 
                    for (int i = 0; i < maxNodeOrder; i++)
                    {
                        if (!usedNodeOrders.IsSet(i))
                            continue;
                        for (int b = 0; b < basePolygonEdges.Length; b++)
                        {
                            var edges = basePolygonEdges[b];
                            FindBasePolygonPlaneIntersections(brushTreeSpacePlanes, i, brushNodeOrder, hashedVertices, edges);
                        }
                    }

                    for (int s = 0; s < surfaceCount; s++)
                    {
                        var intersectionSurfaceCount    = intersectionSurfaceSegments[s].y;
                        var intersectionSurfaceOffset   = intersectionSurfaceSegments[s].x;
                        if (intersectionSurfaceCount == 0)
                            continue;

                        var bp_edges = basePolygonEdges[s];
                        for (int l0 = 0; l0 < intersectionSurfaceCount; l0++)
                        {
                            int intersectionBrushOrder  = brushIntersections[intersectionSurfaceOffset + l0].Value.pair.brushNodeOrder1;// intersectionIndex.w;
                            var in_edges                = intersectionEdges[intersectionSurfaceOffset + l0];

                            FindLoopVertexOverlaps(brushTreeSpacePlanes, intersectionBrushOrder, hashedVertices, bp_edges, in_edges);
                        }
                    } 

                    for (int i = 0; i < intersectionEdges.Length; i++)
                    {
                        // TODO: might not be necessary
                        var edges = intersectionEdges[i];
                        RemoveDuplicates(ref edges);
                    }

                    for (int i = 0; i < basePolygonEdges.Length; i++)
                    {
                        // TODO: might not be necessary
                        var edges = basePolygonEdges[i];
                        RemoveDuplicates(ref edges);
                    }


                    // TODO: merge indices across multiple loops when vertices are identical
                }


                if (!intersectionSurfaceInfos.IsCreated)
                {
                    intersectionSurfaceInfos = new NativeList<SurfaceInfo>(brushIntersections.Length, Allocator.Temp);
                } else
                {
                    intersectionSurfaceInfos.Clear();
                    if (intersectionSurfaceInfos.Capacity < brushIntersections.Length)
                        intersectionSurfaceInfos.Capacity = brushIntersections.Length;
                }

                for (int k = 0; k < brushIntersections.Length; k++)
                {
                    ref var intersection = ref brushIntersections[k].Value;
                    intersectionSurfaceInfos.AddNoResize(intersection.surfaceInfo); //OUTPUT
                }

                output.BeginForEachIndex(index);
                output.Write(brushNodeIndex);
                output.Write(brushNodeOrder);
                output.Write(surfaceCount);
                output.Write(hashedVertices.Length);
                for (int l = 0; l < hashedVertices.Length; l++)
                    output.Write(hashedVertices[l]);

                output.Write(basePolygonEdges.Length);
                for (int l = 0; l < basePolygonEdges.Length; l++)
                {
                    output.Write(basePolygonSurfaceInfos[l]);
                    var edges = basePolygonEdges[l].AsArray();
                    output.Write(edges.Length);
                    for (int e = 0; e < edges.Length; e++)
                        output.Write(edges[e]);
                }

                output.Write(intersectionEdges.Length);
                for (int l = 0; l < intersectionEdges.Length; l++)
                {
                    output.Write(intersectionSurfaceInfos[l]);
                    var edges = intersectionEdges[l].AsArray();
                    output.Write(edges.Length);
                    for (int e = 0; e < edges.Length; e++)
                        output.Write(edges[e]);
                }
                output.EndForEachIndex();

            }
        }

        public void FindLoopPlaneIntersections(NativeArray<BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes,
                                               int intersectionBrushOrder1, int intersectionBrushOrder0,
                                               HashedVertices hashedVertices, NativeListArray<Edge>.NativeList edges)
        {
            if (edges.Length < 3)
                return;

            var inputEdgesLength = edges.Length;
            if (!inputEdges.IsCreated || inputEdges.Length < inputEdgesLength)
            {
                if (inputEdges.IsCreated) inputEdges.Dispose();
                inputEdges = new NativeArray<Edge>(inputEdgesLength, Allocator.Temp);
            }

            inputEdges.CopyFrom(edges, 0, edges.Length);
            edges.Clear();

            int4 tempVertices = int4.zero;

            ref var otherPlanesNative    = ref brushTreeSpacePlanes[intersectionBrushOrder1].Value.treeSpacePlanes;
            ref var selfPlanesNative     = ref brushTreeSpacePlanes[intersectionBrushOrder0].Value.treeSpacePlanes;

            var otherPlaneCount = otherPlanesNative.Length;
            var selfPlaneCount  = selfPlanesNative.Length;

            hashedVertices.Reserve(otherPlaneCount); // ensure we have at least this many extra vertices in capacity

            // TODO: Optimize the hell out of this
            for (int e = 0; e < inputEdgesLength; e++)
            {
                var vertexIndex0 = inputEdges[e].index1;
                var vertexIndex1 = inputEdges[e].index2;

                var vertex0 = hashedVertices[vertexIndex0];
                var vertex1 = hashedVertices[vertexIndex1];

                var vertex0w = new float4(vertex0, 1);
                var vertex1w = new float4(vertex1, 1);

                var foundVertices = 0;

                for (int p = 0; p < otherPlaneCount; p++)
                {
                    var otherPlane = otherPlanesNative[p];

                    var distance0 = math.dot(otherPlane, vertex0w);
                    var distance1 = math.dot(otherPlane, vertex1w);

                    if (distance0 < 0)
                    {
                        if (distance1 <=  kFatPlaneWidthEpsilon ||
                            distance0 >= -kFatPlaneWidthEpsilon) continue;
                    } else
                    {
                        if (distance1 >= -kFatPlaneWidthEpsilon ||
                            distance0 <=  kFatPlaneWidthEpsilon) continue;
                    }

                    float3 newVertex;

                    // Ensure we always do the intersection calculations in the exact same 
                    // direction across a plane to increase floating point consistency
                    if (distance0 > 0)
                    {
                        var length = distance0 - distance1;
                        var delta = distance0 / length;
                        if (delta <= 0 || delta >= 1)
                            continue;
                        var vector = vertex0 - vertex1;
                        newVertex = vertex0 - (vector * delta);
                    } else
                    {
                        var length = distance1 - distance0;
                        var delta = distance1 / length;
                        if (delta <= 0 || delta >= 1)
                            continue;
                        var vector = vertex1 - vertex0;
                        newVertex = vertex1 - (vector * delta);
                    }

                    // Check if the new vertex is identical to one of our existing vertices
                    if (math.lengthsq(vertex0 - newVertex) <= kSqrVertexEqualEpsilon ||
                        math.lengthsq(vertex1 - newVertex) <= kSqrVertexEqualEpsilon)
                        continue;

                    var newVertexw = new float4(newVertex, 1);
                    for (int p2 = 0; p2 < otherPlaneCount; p2++)
                    {
                        otherPlane = otherPlanesNative[p2];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kFatPlaneWidthEpsilon)
                            goto SkipEdge;
                    }
                    for (int p1 = 0; p1 < selfPlaneCount; p1++)
                    {
                        otherPlane = selfPlanesNative[p1];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kFatPlaneWidthEpsilon)
                            goto SkipEdge;
                    }

                    var tempVertexIndex = hashedVertices.AddNoResize(newVertex);
                    if ((foundVertices == 0 || tempVertexIndex != tempVertices[1]) &&
                        vertexIndex0 != tempVertexIndex &&
                        vertexIndex1 != tempVertexIndex)
                    {
                        tempVertices[foundVertices + 1] = tempVertexIndex;
                        foundVertices++;

                        // It's impossible to have more than 2 intersections on a single edge when intersecting with a convex shape
                        if (foundVertices == 2)
                            break;
                    }
                    SkipEdge:
                    ;
                }

                if (foundVertices > 0)
                {
                    if (foundVertices == 2)
                    {
                        var tempVertexIndex0 = tempVertices[1];
                        var tempVertexIndex1 = tempVertices[2];
                        var tempVertex0 = hashedVertices[tempVertexIndex0];
                        var tempVertex1 = hashedVertices[tempVertexIndex1];
                        var dot0 = math.lengthsq(tempVertex0 - vertex1);
                        var dot1 = math.lengthsq(tempVertex1 - vertex1);
                        if (dot0 < dot1)
                        {
                            tempVertices[1] = tempVertexIndex1;
                            tempVertices[2] = tempVertexIndex0;
                        }
                    }
                    tempVertices[0] = vertexIndex0;
                    tempVertices[1 + foundVertices] = vertexIndex1;
                    for (int i = 1; i < 2 + foundVertices; i++)
                    {
                        if (tempVertices[i - 1] != tempVertices[i])
                            edges.AddNoResize(new Edge() { index1 = (ushort)tempVertices[i - 1], index2 = (ushort)tempVertices[i] });
                    }
                } else
                {
                    edges.AddNoResize(inputEdges[e]);
                }
            }
        }
        
        public void FindBasePolygonPlaneIntersections(NativeArray<BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes, 
                                                      int otherBrushNodeOrder, int selfBrushNodeOrder,
                                                      HashedVertices hashedVertices, NativeListArray<Edge>.NativeList edges)
        {
            if (edges.Length < 3)
                return;
            
            var inputEdgesLength    = edges.Length;
            if (!inputEdges.IsCreated || inputEdges.Length < inputEdgesLength)
            {
                if (inputEdges.IsCreated) inputEdges.Dispose();
                inputEdges = new NativeArray<Edge>(inputEdgesLength, Allocator.Temp);
            }

            inputEdges.CopyFrom(edges, 0, edges.Length);
            edges.Clear();

            int4 tempVertices = int4.zero;

            ref var otherPlanesNative    = ref brushTreeSpacePlanes[otherBrushNodeOrder].Value.treeSpacePlanes;
            ref var selfPlanesNative     = ref brushTreeSpacePlanes[selfBrushNodeOrder].Value.treeSpacePlanes;

            var otherPlaneCount = otherPlanesNative.Length;
            var selfPlaneCount  = selfPlanesNative.Length;

            hashedVertices.Reserve(otherPlaneCount); // ensure we have at least this many extra vertices in capacity

            // TODO: Optimize the hell out of this
            for (int e = 0; e < inputEdgesLength; e++)
            {
                var vertexIndex0 = inputEdges[e].index1;
                var vertexIndex1 = inputEdges[e].index2;

                var vertex0 = hashedVertices[vertexIndex0];
                var vertex1 = hashedVertices[vertexIndex1];

                var vertex0w = new float4(vertex0, 1);
                var vertex1w = new float4(vertex1, 1);

                var foundVertices = 0;

                for (int p = 0; p < otherPlaneCount; p++)
                {
                    var otherPlane = otherPlanesNative[p];

                    var distance0 = math.dot(otherPlane, vertex0w);
                    var distance1 = math.dot(otherPlane, vertex1w);

                    if (distance0 < 0)
                    {
                        if (distance1 <=  kFatPlaneWidthEpsilon ||
                            distance0 >= -kFatPlaneWidthEpsilon) continue;
                    } else
                    {
                        if (distance1 >= -kFatPlaneWidthEpsilon ||
                            distance0 <=  kFatPlaneWidthEpsilon) continue;
                    }

                    float3 newVertex;

                    // Ensure we always do the intersection calculations in the exact same 
                    // direction across a plane to increase floating point consistency
                    if (distance0 > 0)
                    {
                        var length = distance0 - distance1;
                        var delta = distance0 / length;
                        if (delta <= 0 || delta >= 1)
                            continue;
                        var vector = vertex0 - vertex1;
                        newVertex = vertex0 - (vector * delta);
                    } else
                    {
                        var length = distance1 - distance0;
                        var delta = distance1 / length;
                        if (delta <= 0 || delta >= 1)
                            continue;
                        var vector = vertex1 - vertex0;
                        newVertex = vertex1 - (vector * delta);
                    }

                    // Check if the new vertex is identical to one of our existing vertices
                    if (math.lengthsq(vertex0 - newVertex) <= kSqrVertexEqualEpsilon ||
                        math.lengthsq(vertex1 - newVertex) <= kSqrVertexEqualEpsilon)
                        continue;

                    var newVertexw = new float4(newVertex, 1);
                    for (int p2 = 0; p2 < otherPlaneCount; p2++)
                    {
                        otherPlane = otherPlanesNative[p2];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kFatPlaneWidthEpsilon)
                            goto SkipEdge;
                    }
                    for (int p1 = 0; p1 < selfPlaneCount; p1++)
                    {
                        otherPlane = selfPlanesNative[p1];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kFatPlaneWidthEpsilon)
                            goto SkipEdge;
                    }

                    var tempVertexIndex = hashedVertices.AddNoResize(newVertex);
                    if ((foundVertices == 0 || tempVertexIndex != tempVertices[1]) &&
                        vertexIndex0 != tempVertexIndex &&
                        vertexIndex1 != tempVertexIndex)
                    {
                        tempVertices[foundVertices + 1] = tempVertexIndex;
                        foundVertices++;

                        // It's impossible to have more than 2 intersections on a single edge when intersecting with a convex shape
                        if (foundVertices == 2)
                            break;
                    }
                    SkipEdge:
                    ;
                }

                if (foundVertices > 0)
                {
                    if (foundVertices == 2)
                    {
                        var tempVertexIndex0 = tempVertices[1];
                        var tempVertexIndex1 = tempVertices[2];
                        var tempVertex0 = hashedVertices[tempVertexIndex0];
                        var tempVertex1 = hashedVertices[tempVertexIndex1];
                        var dot0 = math.lengthsq(tempVertex0 - vertex1);
                        var dot1 = math.lengthsq(tempVertex1 - vertex1);
                        if (dot0 < dot1)
                        {
                            tempVertices[1] = tempVertexIndex1;
                            tempVertices[2] = tempVertexIndex0;
                        }
                    }
                    tempVertices[0] = vertexIndex0;
                    tempVertices[1 + foundVertices] = vertexIndex1;
                    for (int i = 1; i < 2 + foundVertices; i++)
                    {
                        if (tempVertices[i - 1] != tempVertices[i])
                            edges.AddNoResize(new Edge() { index1 = (ushort)tempVertices[i - 1], index2 = (ushort)tempVertices[i] });
                    }
                } else
                {
                    edges.AddNoResize(inputEdges[e]);
                }
            }
        }


        public void FindLoopVertexOverlaps(NativeArray<BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes,
                                           int selfBrushNodeOrder, HashedVertices vertices,
                                           NativeListArray<Edge>.NativeList otherEdges, NativeListArray<Edge>.NativeList edges)
        {
            if (edges.Length < 3 ||
                otherEdges.Length < 3)
                return;

            ref var selfPlanes = ref brushTreeSpacePlanes[selfBrushNodeOrder].Value.treeSpacePlanes;

            var otherVerticesLength = 0;
            if (!otherVertices.IsCreated || otherVertices.Length < otherEdges.Length)
            {
                if (otherVertices.IsCreated) otherVertices.Dispose();
                otherVertices = new NativeArray<ushort>(otherEdges.Length, Allocator.Temp);
            }
            
            // TODO: use edges instead + 2 planes intersecting each edge
            for (int v = 0; v < otherEdges.Length; v++)
            {
                var vertexIndex = otherEdges[v].index1; // <- assumes no gaps
                for (int e = 0; e < edges.Length; e++)
                {
                    if (edges[e].index1 == vertexIndex ||
                        edges[e].index2 == vertexIndex)
                        goto NextVertex;
                }

                var vertex = vertices[vertexIndex];
                for (int p = 0; p < selfPlanes.Length; p++)
                {
                    var distance = math.dot(selfPlanes[p], new float4(vertex, 1));
                    if (distance > kFatPlaneWidthEpsilon)
                        goto NextVertex;
                }
                // TODO: Check if vertex intersects at least 2 selfPlanes
                otherVertices[otherVerticesLength] = vertexIndex;
                otherVerticesLength++;
            NextVertex:
                ;
            }

            if (otherVerticesLength == 0)
                return;

            if (!tempList.IsCreated)
                tempList = new NativeList<ushort>(Allocator.Temp);
            else
                tempList.Clear();

            var tempListCapacity = (edges.Length * 2) + otherVerticesLength;
            if (tempList.Capacity < tempListCapacity)
                tempList.Capacity = tempListCapacity;

            {
                var inputEdgesLength    = edges.Length;
                if (!inputEdges.IsCreated || inputEdges.Length < inputEdgesLength)
                {
                    if (inputEdges.IsCreated) inputEdges.Dispose();
                    inputEdges = new NativeArray<Edge>(inputEdgesLength, Allocator.Temp);
                }

                inputEdges.CopyFrom(edges, 0, edges.Length);
                edges.Clear();

                // TODO: Optimize the hell out of this
                for (int e = 0; e < inputEdgesLength && otherVerticesLength > 0; e++)
                {
                    var vertexIndex0 = inputEdges[e].index1;
                    var vertexIndex1 = inputEdges[e].index2;

                    var vertex0 = vertices[vertexIndex0];
                    var vertex1 = vertices[vertexIndex1];

                    tempList.Clear();
                    tempList.AddNoResize(vertexIndex0);

                    var delta = (vertex1 - vertex0);
                    var max = math.dot(vertex1 - vertex0, delta);
                    for (int v1 = otherVerticesLength - 1; v1 >= 0; v1--)
                    {
                        var otherVertexIndex = otherVertices[v1];
                        if (otherVertexIndex == vertexIndex0 ||
                            otherVertexIndex == vertexIndex1)
                            continue;
                        var otherVertex = vertices[otherVertexIndex];
                        var dot = math.dot(otherVertex - vertex0, delta);
                        if (dot <= 0 || dot >= max)
                            continue;
                        if (!GeometryMath.IsPointOnLineSegment(otherVertex, vertex0, vertex1))
                            continue;

                        // Note: the otherVertices array cannot contain any indices that are part in 
                        //       the input indices, since we checked for that when we created it.
                        tempList.AddNoResize(otherVertexIndex);

                        // TODO: figure out why removing vertices fails?
                        //if (v1 != otherVerticesLength - 1 && otherVerticesLength > 0)
                        //    otherVertices[v1] = otherVertices[otherVerticesLength - 1];
                        //otherVerticesLength--;
                    }
                    tempList.AddNoResize(vertexIndex1);

                    if (tempList.Length > 2)
                    {
                        float dot1, dot2;
                        var last = tempList.Length - 1;
                        for (int v1 = 1; v1 < last - 1; v1++)
                        {
                            for (int v2 = 2; v2 < last; v2++)
                            {
                                var otherVertexIndex1 = tempList[v1];
                                var otherVertexIndex2 = tempList[v2];
                                var otherVertex1 = vertices[otherVertexIndex1];
                                var otherVertex2 = vertices[otherVertexIndex2];
                                dot1 = math.dot(delta, otherVertex1);
                                dot2 = math.dot(delta, otherVertex2);
                                if (dot1 >= dot2)
                                {
                                    tempList[v1] = otherVertexIndex2;
                                    tempList[v2] = otherVertexIndex1;
                                }
                            }
                        }
                        for (int i = 1; i < tempList.Length; i++)
                        {
                            if (tempList[i - 1] != tempList[i])
                                edges.AddNoResize(new Edge { index1 = tempList[i - 1], index2 = tempList[i] });
                        }
                    } else
                    {
                        edges.AddNoResize(inputEdges[e]);
                    }
                }
            }
        }

        public static void RemoveDuplicates(ref NativeListArray<Edge>.NativeList edges)
        {
            if (edges.Length < 3)
            {
                edges.Clear();
                return;
            }

            for (int e = edges.Length - 1; e >= 0; e--)
            {
                if (edges[e].index1 != edges[e].index2)
                    continue;
                edges.RemoveAtSwapBack(e);
            }
        }
    }
}
