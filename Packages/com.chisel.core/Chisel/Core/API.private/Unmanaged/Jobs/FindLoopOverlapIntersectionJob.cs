using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{

    [BurstCompile(CompileSynchronously = true)]
    struct FindLoopOverlapIntersectionsJob : IJobParallelForDefer
    { 
        public const int kMaxVertexCount        = short.MaxValue;
        const float kSqrVertexEqualEpsilon      = CSGConstants.kSqrVertexEqualEpsilon;
        const float kFatPlaneWidthEpsilon       = CSGConstants.kFatPlaneWidthEpsilon;

        // Read
        [NoAlias, ReadOnly] public NativeList<IndexOrder>                                       allUpdateBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeList<float3>                                           outputSurfaceVertices;
        [NoAlias, ReadOnly] public NativeList<BrushIntersectionLoop>                            outputSurfaces;
        [NoAlias, ReadOnly] public NativeArray<int2>                                            outputSurfacesRange;
        [NoAlias, ReadOnly] public int                                                          maxNodeOrder;
        [NoAlias, ReadOnly] public NativeList<ChiselBlobAssetReference<BasePolygonsBlob>>       basePolygonCache;
        [NoAlias, ReadOnly] public NativeList<ChiselBlobAssetReference<BrushTreeSpacePlanes>>   brushTreeSpacePlaneCache;

        // Read Write
        public Allocator allocator;
        [NativeDisableParallelForRestriction]
        [NoAlias] public NativeArray<UnsafeList<float3>>                    loopVerticesLookup;

        // Write
        [NativeDisableParallelForRestriction]
        [NoAlias, WriteOnly] public NativeStream.Writer                     output;

        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] NativeArray<Edge>         newSelfEdges;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>       srcIndices;
        [NativeDisableContainerSafetyRestriction] NativeArray<IndexSurfaceInfo> basePolygonSurfaceInfos;
        [NativeDisableContainerSafetyRestriction] NativeList<ushort>        tempList;
        [NativeDisableContainerSafetyRestriction] NativeList<BrushIntersectionLoop> brushIntersections;
        [NativeDisableContainerSafetyRestriction] NativeList<IndexSurfaceInfo>  intersectionSurfaceInfos;
        [NativeDisableContainerSafetyRestriction] NativeList<UnsafeList<Edge>>  basePolygonEdges;
        [NativeDisableContainerSafetyRestriction] NativeList<UnsafeList<Edge>>  intersectionEdges;
        [NativeDisableContainerSafetyRestriction] NativeBitArray            usedNodeOrders;
        [NativeDisableContainerSafetyRestriction] HashedVertices            hashedTreeSpaceVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<int2>         intersectionSurfaceSegments;
        [NativeDisableContainerSafetyRestriction] NativeArray<ushort>       otherVertices;

        struct CompareSortByBasePlaneIndex : System.Collections.Generic.IComparer<BrushIntersectionLoop>
        {
            public int Compare(BrushIntersectionLoop x, BrushIntersectionLoop y)
            {
                var diff = x.surfaceInfo.basePlaneIndex - y.surfaceInfo.basePlaneIndex;
                if (diff != 0)
                    return diff;

                return x.indexOrder1.nodeOrder - y.indexOrder1.nodeOrder;
            }
        }

        static readonly CompareSortByBasePlaneIndex kCompareSortByBasePlaneIndex = new CompareSortByBasePlaneIndex();

        void CopyFrom(NativeList<UnsafeList<Edge>> dst, int index, ref BrushIntersectionLoop brushIntersectionLoop, HashedVertices hashedTreeSpaceVertices, int extraCapacity)
        {
            Debug.Assert(extraCapacity >= 0);
            ref var vertexIndex     = ref brushIntersectionLoop.loopVertexIndex;
            ref var loopVertexCount = ref brushIntersectionLoop.loopVertexCount;
            
            NativeCollectionHelpers.EnsureMinimumSize(ref srcIndices, loopVertexCount);

            hashedTreeSpaceVertices.ReserveAdditionalVertices(loopVertexCount);
            for (int j = 0; j < loopVertexCount; j++)
                srcIndices[j] = hashedTreeSpaceVertices.AddNoResize(outputSurfaceVertices[vertexIndex + j]);

            var dstEdges = new UnsafeList<Edge>(loopVertexCount + extraCapacity, Allocator.Temp);
            for (int j = 1; j < loopVertexCount; j++)
            {
                dstEdges.AddNoResize(new Edge { index1 = srcIndices[j - 1], index2 = srcIndices[j] });
            }
            dstEdges.AddNoResize(new Edge { index1 = srcIndices[loopVertexCount - 1], index2 = srcIndices[0] });

            dst[index] = dstEdges;
        }


        public void Execute(int index)
        {
            var brushIndexOrder     = allUpdateBrushIndexOrders[index];
            int brushNodeOrder      = brushIndexOrder.nodeOrder;

            // Can happen when BrushMeshes are not initialized correctly
            if (basePolygonCache[brushNodeOrder] == ChiselBlobAssetReference<BasePolygonsBlob>.Null)
            {
                loopVerticesLookup[brushIndexOrder.nodeOrder] = new UnsafeList<float3>(16, allocator);
                output.BeginForEachIndex(index);
                output.Write(brushIndexOrder);
                output.Write(0);
                output.Write(0);
                output.Write(0);
                output.EndForEachIndex();
                return;
            }

            ref var basePolygonBlob = ref basePolygonCache[brushNodeOrder].Value;
            var surfaceCount        = basePolygonBlob.polygons.Length;
            if (surfaceCount == 0)
            {
                loopVerticesLookup[brushIndexOrder.nodeOrder] = new UnsafeList<float3>(16, allocator);
                output.BeginForEachIndex(index);
                output.Write(brushIndexOrder);
                output.Write(0);
                output.Write(0);
                output.Write(0);
                output.EndForEachIndex();
                return;
            }

            NativeCollectionHelpers.EnsureMinimumSize(ref basePolygonSurfaceInfos, surfaceCount);
            NativeCollectionHelpers.EnsureCapacityAndClear(ref hashedTreeSpaceVertices, HashedVertices.kMaxVertexCount);
            NativeCollectionHelpers.EnsureSizeAndClear(ref basePolygonEdges, surfaceCount);

            var intersectionOffset = outputSurfacesRange[brushNodeOrder].x;
            var intersectionCount  = outputSurfacesRange[brushNodeOrder].y;
            
            hashedTreeSpaceVertices.AddUniqueVertices(ref basePolygonBlob.vertices); /*OUTPUT*/

            if (intersectionCount == 0)
            {
                // If we don't have any intersection loops, just convert basePolygonBlob to loops and be done
                // TODO: should do this per surface!

                for (int s = 0; s < basePolygonBlob.polygons.Length; s++)
                {
                    ref var input = ref basePolygonBlob.polygons[s];
                    
                    ref var nodeIndexOrder = ref basePolygonBlob.polygons[s].nodeIndexOrder;
                    ref var surfaceInfo = ref basePolygonBlob.polygons[s].surfaceInfo;
                    basePolygonSurfaceInfos[s] = new IndexSurfaceInfo
                    {
                        brushIndexOrder = nodeIndexOrder,
                        interiorCategory = surfaceInfo.interiorCategory,
                        basePlaneIndex = surfaceInfo.basePlaneIndex
                    };

                    if (input.endEdgeIndex == input.startEdgeIndex)
                        continue;

                    var edges = new UnsafeList<Edge>(input.endEdgeIndex - input.startEdgeIndex, Allocator.Temp);
                    for (int e = input.startEdgeIndex; e < input.endEdgeIndex; e++)
                        edges.AddNoResize(basePolygonBlob.edges[e]);
                    basePolygonEdges[s] = edges;
                }

                if (intersectionEdges.IsCreated)
                    intersectionEdges.Clear();

                if (intersectionSurfaceInfos.IsCreated)
                    intersectionSurfaceInfos.Clear();

                var writeVertices = new UnsafeList<float3>(hashedTreeSpaceVertices.Length, allocator);
                writeVertices.Resize(hashedTreeSpaceVertices.Length, NativeArrayOptions.UninitializedMemory);
                for (int l = 0; l < hashedTreeSpaceVertices.Length; l++)
                    writeVertices[l] = hashedTreeSpaceVertices[l];
                loopVerticesLookup[brushIndexOrder.nodeOrder] = writeVertices;

                output.BeginForEachIndex(index);
                output.Write(brushIndexOrder);
                output.Write(surfaceCount);
            
                output.Write(basePolygonEdges.Length);
                for (int l = 0; l < basePolygonEdges.Length; l++)
                {
                    output.Write(basePolygonSurfaceInfos[l]);
                    if (!basePolygonEdges[l].IsCreated)
                    {
                        output.Write(0);
                        continue;
                    }
                    var edges = basePolygonEdges[l];
                    output.Write(edges.Length);
                    for (int e = 0; e < edges.Length; e++)
                        output.Write(edges[e]);
                }

                output.Write(0);
                output.EndForEachIndex();
            } else
            {
                NativeCollectionHelpers.EnsureCapacityAndClear(ref brushIntersections, intersectionCount);
                NativeCollectionHelpers.EnsureMinimumSizeAndClear(ref usedNodeOrders, maxNodeOrder);

                var lastIntersectionIndex = intersectionCount + intersectionOffset;
                for (int i = intersectionOffset; i < lastIntersectionIndex; i++)
                {
                    var item            = outputSurfaces[i];
                    var otherNodeOrder1 = item.indexOrder1.nodeOrder;
                    
                    usedNodeOrders.Set(otherNodeOrder1, true);

                    //Debug.Assert(outputSurface.surfaceInfo.brushIndex == pair.brushNodeIndex1);
                    //Debug.Assert(outputSurface.surfaceInfo.basePlaneIndex == pair.basePlaneIndex);

                    brushIntersections.AddNoResize(item);
                }

                NativeCollectionHelpers.EnsureSizeAndClear(ref intersectionEdges, brushIntersections.Length);

                brushIntersections.Sort(kCompareSortByBasePlaneIndex);

                NativeCollectionHelpers.EnsureMinimumSize(ref intersectionSurfaceSegments, surfaceCount + 1);
                {
                    for (int s = 0; s < basePolygonBlob.polygons.Length; s++)
                    {
                        ref var input = ref basePolygonBlob.polygons[s];

                        ref var surfaceInfo = ref basePolygonBlob.polygons[s].surfaceInfo;
                        ref var nodeIndexOrder = ref basePolygonBlob.polygons[s].nodeIndexOrder;
                        basePolygonSurfaceInfos[s] = new IndexSurfaceInfo
                        {
                            brushIndexOrder     = nodeIndexOrder,
                            interiorCategory    = surfaceInfo.interiorCategory,
                            basePlaneIndex      = surfaceInfo.basePlaneIndex
                        };

                        if (input.endEdgeIndex == input.startEdgeIndex)
                            continue;

                        var edges = new UnsafeList<Edge>((input.endEdgeIndex - input.startEdgeIndex) + (brushIntersections.Length * 4), Allocator.Temp);
                        for (int e = input.startEdgeIndex; e < input.endEdgeIndex; e++)
                            edges.AddNoResize(basePolygonBlob.edges[e]);
                        basePolygonEdges[s] = edges;
                    }


                    int prevBasePlaneIndex = 0;
                    int startIndex = 0;
                    for (int l = 0; l < brushIntersections.Length; l++)
                    {
                        var brushIntersectionLoop   = brushIntersections[l];
                        ref var surfaceInfo         = ref brushIntersectionLoop.surfaceInfo;
                        //UnityEngine.Debug.Assert(brushIntersectionLoop.indexOrder0.compactNodeID == brushIndexOrder.compactNodeID);
                        UnityEngine.Debug.Assert(brushIntersectionLoop.indexOrder0.nodeOrder == brushIndexOrder.nodeOrder);

                        var basePlaneIndex = surfaceInfo.basePlaneIndex;
                        if (prevBasePlaneIndex != basePlaneIndex)
                        {
                            intersectionSurfaceSegments[prevBasePlaneIndex] = new int2(startIndex, l - startIndex);
                            startIndex = l;
                            for (int s = prevBasePlaneIndex + 1; s < basePlaneIndex; s++)
                                intersectionSurfaceSegments[s] = new int2(startIndex, 0);
                            prevBasePlaneIndex = basePlaneIndex;
                        }
                        CopyFrom(intersectionEdges, l, ref brushIntersectionLoop, hashedTreeSpaceVertices, brushIntersections.Length * 4);
                    }

                    intersectionSurfaceSegments[prevBasePlaneIndex] = new int2(startIndex, brushIntersections.Length - startIndex);
                    startIndex = brushIntersections.Length;
                    for (int s = prevBasePlaneIndex + 1; s < surfaceCount; s++)
                        intersectionSurfaceSegments[s] = new int2(startIndex, 0);

                    
                    for (int s = 0; s < surfaceCount; s++)
                    {
                        var intersectionSurfaceCount    = intersectionSurfaceSegments[s].y;
                        var intersectionSurfaceOffset   = intersectionSurfaceSegments[s].x;
                        for (int l0 = intersectionSurfaceCount - 1; l0 >= 0; l0--)
                        {
                            //int intersectionBrushOrder0 = brushIntersections[intersectionSurfaceOffset + l0].indexOrder1.nodeOrder;
                            var edges                   = intersectionEdges[intersectionSurfaceOffset + l0];
                            for (int l1 = 0; l1 < intersectionSurfaceCount; l1++)
                            {
                                if (l0 == l1)
                                    continue;
                            
                                int intersectionBrushOrder1 = brushIntersections[intersectionSurfaceOffset + l1].indexOrder1.nodeOrder;// intersectionIndex1.w;

                                FindLoopPlaneIntersections(brushTreeSpacePlaneCache, 
                                                           intersectionBrushOrder1, 
                                                           //intersectionBrushOrder0, 
                                                           hashedTreeSpaceVertices, ref edges);

                                // TODO: merge these so that intersections will be identical on both loops (without using math, use logic)
                                // TODO: make sure that intersections between loops will be identical on OTHER brushes (without using math, use logic)
                            }

                            intersectionEdges[intersectionSurfaceOffset + l0] = edges;
                        }
                    }

                    ref var selfPlanes = ref brushTreeSpacePlaneCache[brushIndexOrder.nodeOrder].Value.treeSpacePlanes;

                    // TODO: should only intersect with all brushes that each particular basepolygon intersects with
                    //       but also need adjency information between basePolygons to ensure that intersections exist on 
                    //       both sides of each edge on a brush. 
                    for (int otherBrushNodeOrder = 0; otherBrushNodeOrder < maxNodeOrder; otherBrushNodeOrder++)
                    {
                        if (!usedNodeOrders.IsSet(otherBrushNodeOrder) ||
                            otherBrushNodeOrder == brushNodeOrder)
                            continue;

                        ref var otherPlanes = ref brushTreeSpacePlaneCache[otherBrushNodeOrder].Value.treeSpacePlanes;
                        for (int b = 0; b < basePolygonEdges.Length; b++)
                        {
                            if (!basePolygonEdges[b].IsCreated)
                                continue;
                            var selfEdges = basePolygonEdges[b];
                            //var before = selfEdges.Length;

                            FindBasePolygonPlaneIntersections(ref otherPlanes, //ref selfPlanes, 
                                                              ref selfEdges, hashedTreeSpaceVertices);
                            basePolygonEdges[b] = selfEdges;
                        }
                    }

                    for (int s = 0; s < surfaceCount; s++)
                    {
                        var intersectionSurfaceCount    = intersectionSurfaceSegments[s].y;
                        var intersectionSurfaceOffset   = intersectionSurfaceSegments[s].x;
                        if (intersectionSurfaceCount == 0)
                            continue;

                        if (!basePolygonEdges[s].IsCreated)
                            continue;

                        var bp_edges = basePolygonEdges[s];
                        for (int l0 = 0; l0 < intersectionSurfaceCount; l0++)
                        {
                            int intersectionBrushOrder  = brushIntersections[intersectionSurfaceOffset + l0].indexOrder1.nodeOrder;// intersectionIndex.w;
                            var in_edges                = intersectionEdges[intersectionSurfaceOffset + l0];
                            
                            ref var otherPlanes = ref brushTreeSpacePlaneCache[intersectionBrushOrder].Value.treeSpacePlanes;

                            FindLoopVertexOverlaps(ref selfPlanes, ref in_edges, bp_edges, hashedTreeSpaceVertices);
                            intersectionEdges[intersectionSurfaceOffset + l0] = in_edges;

                            // TODO: The following call, strictly speaking, is unncessary, but it'll hide bugs in other parts of the pipeline.
                            //          it'll add missing vertices on overlapping loops, but they will be missing on touching surfaces, so there will be gaps.
                            //          the alternative is that this surface could possible fail to triangulate and be completely missing .. (larger gap)
                            //       Somehow it can also cause artifacts sometimes???
                            //FindLoopVertexOverlaps(ref otherPlanes, ref bp_edges, in_edges, hashedTreeSpaceVertices);
                        }
                    } 

                    for (int i = 0; i < intersectionEdges.Length; i++)
                    {
                        // TODO: might not be necessary
                        var edges = intersectionEdges[i];
                        RemoveDuplicates(ref edges);
                        intersectionEdges[i] = edges;
                    }

                    for (int i = 0; i < basePolygonEdges.Length; i++)
                    {
                        if (!basePolygonEdges[i].IsCreated)
                            continue;
                        // TODO: might not be necessary
                        var edges = basePolygonEdges[i];
                        RemoveDuplicates(ref edges);
                        basePolygonEdges[i] = edges;
                    }


                    // TODO: merge indices across multiple loops when vertices are identical
                }


                NativeCollectionHelpers.EnsureCapacityAndClear(ref intersectionSurfaceInfos, brushIntersections.Length);

                for (int k = 0; k < brushIntersections.Length; k++)
                {
                    var intersection = brushIntersections[k];
                    ref var surfaceInfo  = ref intersection.surfaceInfo;
                    intersectionSurfaceInfos.AddNoResize(
                        new IndexSurfaceInfo
                        {
                            brushIndexOrder = intersection.indexOrder1,
                            interiorCategory = surfaceInfo.interiorCategory,
                            basePlaneIndex = surfaceInfo.basePlaneIndex
                        }); //OUTPUT
                }

                var writeVertices = new UnsafeList<float3>(hashedTreeSpaceVertices.Length, allocator);
                writeVertices.Resize(hashedTreeSpaceVertices.Length, NativeArrayOptions.UninitializedMemory);
                for (int l = 0; l < hashedTreeSpaceVertices.Length; l++)
                    writeVertices[l] = hashedTreeSpaceVertices[l];
                loopVerticesLookup[brushIndexOrder.nodeOrder] = writeVertices;

                output.BeginForEachIndex(index);
                output.Write(brushIndexOrder);
                output.Write(surfaceCount);

                output.Write(basePolygonEdges.Length);
                for (int l = 0; l < basePolygonEdges.Length; l++)
                {
                    output.Write(basePolygonSurfaceInfos[l]);
                    if (!basePolygonEdges[l].IsCreated)
                    {
                        output.Write(0);
                        continue;
                    }
                    var edges = basePolygonEdges[l];
                    output.Write(edges.Length);
                    for (int e = 0; e < edges.Length; e++)
                        output.Write(edges[e]);
                }

                output.Write(intersectionEdges.Length);
                for (int l = 0; l < intersectionEdges.Length; l++)
                {
                    output.Write(intersectionSurfaceInfos[l]);
                    var edges = intersectionEdges[l];
                    output.Write(edges.Length);
                    for (int e = 0; e < edges.Length; e++)
                        output.Write(edges[e]);
                }
                output.EndForEachIndex();
            }
        }

        public void FindLoopPlaneIntersections(NativeArray<ChiselBlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes,
                                               int intersectionBrushOrder1, //int intersectionBrushOrder0,
                                               HashedVertices hashedTreeSpaceVertices, 
                                               ref UnsafeList<Edge> edges)
        {
            if (edges.Length < 3)
                return;

            var inputEdgesLength = edges.Length;

            NativeCollectionHelpers.EnsureMinimumSize(ref newSelfEdges, inputEdgesLength);

            newSelfEdges.CopyFrom(edges, 0, edges.Length);
            edges.Clear();

            int4 tempVertices = int4.zero;

            ref var otherPlanes    = ref brushTreeSpacePlanes[intersectionBrushOrder1].Value.treeSpacePlanes;
            //ref var selfPlanes     = ref brushTreeSpacePlanes[intersectionBrushOrder0].Value.treeSpacePlanes;

            var otherPlaneCount = otherPlanes.Length;
            //var selfPlaneCount  = selfPlanes.Length;

            hashedTreeSpaceVertices.ReserveAdditionalVertices(otherPlaneCount); // ensure we have at least this many extra vertices in capacity

            // TODO: Optimize the hell out of this
            for (int e = 0; e < inputEdgesLength; e++)
            {
                var vertexIndex0 = newSelfEdges[e].index1;
                var vertexIndex1 = newSelfEdges[e].index2;

                var vertex0 = hashedTreeSpaceVertices[vertexIndex0];
                var vertex1 = hashedTreeSpaceVertices[vertexIndex1];

                var vertex0w = new float4(vertex0, 1);
                var vertex1w = new float4(vertex1, 1);

                var foundVertices = 0;

                for (int p = 0; p < otherPlaneCount; p++)
                {
                    var otherPlane = otherPlanes[p];

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

                    // Check if the new vertex is identical to one of the edge vertices
                    if (math.lengthsq(vertex0 - newVertex) <= kSqrVertexEqualEpsilon ||
                        math.lengthsq(vertex1 - newVertex) <= kSqrVertexEqualEpsilon)
                        continue;

                    var newVertex4 = new float4(newVertex, 1);
                    for (int p2 = 0; p2 < otherPlaneCount; p2++)
                    {
                        otherPlane = otherPlanes[p2];
                        var distance = math.dot(otherPlane, newVertex4);
                        if (distance > kFatPlaneWidthEpsilon)
                            goto SkipEdge;
                    }

                    // TODO: store two end planes for each edge instead (the planes whose intersections with the infinite edge create the vertices)
                    /*
                    for (int p1 = 0; p1 < selfPlaneCount; p1++)
                    {
                        otherPlane = selfPlanes[p1];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kFatPlaneWidthEpsilon)
                            goto SkipEdge;
                    }
                    //*/

                    var tempVertexIndex = hashedTreeSpaceVertices.AddNoResize(newVertex);
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
                        var tempVertex0 = hashedTreeSpaceVertices[tempVertexIndex0];
                        var tempVertex1 = hashedTreeSpaceVertices[tempVertexIndex1];
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
                    edges.AddNoResize(newSelfEdges[e]);
                }
            }
        }
        
        public void FindBasePolygonPlaneIntersections([NoAlias, ReadOnly] ref ChiselBlobArray<float4>   otherPlanes,
                                                      //[NoAlias] ref BlobArray<float4>       selfPlanes,
                                                      [NoAlias] ref UnsafeList<Edge>          selfEdges,
                                                      [NoAlias, ReadOnly] HashedVertices      combinedVertices)
        {
            if (selfEdges.Length < 3)
                return;
            
            var newSelfEdgesLength = selfEdges.Length;

            NativeCollectionHelpers.EnsureMinimumSize(ref newSelfEdges, newSelfEdgesLength); 

            newSelfEdges.CopyFrom(selfEdges, 0, selfEdges.Length);
            selfEdges.Clear();

            int4 tempVertices = int4.zero;

            var otherPlaneCount = otherPlanes.Length;
            //var selfPlaneCount  = selfPlanes.Length;

            combinedVertices.ReserveAdditionalVertices(otherPlaneCount); // ensure we have at least this many extra vertices in capacity

            // TODO: Optimize the hell out of this
            for (int e = 0; e < newSelfEdgesLength; e++)
            {
                var vertexIndex0 = newSelfEdges[e].index1;
                var vertexIndex1 = newSelfEdges[e].index2;

                var vertex0 = combinedVertices[vertexIndex0];
                var vertex1 = combinedVertices[vertexIndex1];

                var vertex0w = new float4(vertex0, 1);
                var vertex1w = new float4(vertex1, 1);

                var foundVertices = 0;

                for (int p = 0; p < otherPlaneCount; p++)
                {
                    var otherPlane = otherPlanes[p];

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
                        otherPlane = otherPlanes[p2];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kFatPlaneWidthEpsilon)
                            goto SkipEdge;
                    }
                    // TODO: store two end planes for each edge instead (the planes whose intersections with the infinite edge create the vertices)
                    /*
                    for (int p1 = 0; p1 < selfPlaneCount; p1++)
                    {
                        otherPlane = selfPlanes[p1];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kFatPlaneWidthEpsilon)
                            goto SkipEdge;
                    }
                    //*/

                    var tempVertexIndex = combinedVertices.AddNoResize(newVertex);
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
                        var tempVertex0 = combinedVertices[tempVertexIndex0];
                        var tempVertex1 = combinedVertices[tempVertexIndex1];
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
                            selfEdges.AddNoResize(new Edge() { index1 = (ushort)tempVertices[i - 1], index2 = (ushort)tempVertices[i] });
                    }
                } else
                {
                    selfEdges.AddNoResize(newSelfEdges[e]);
                }
            }
        }


        public void FindLoopVertexOverlaps([NoAlias, ReadOnly] ref ChiselBlobArray<float4> selfPlanes,
                                           [NoAlias] ref UnsafeList<Edge> selfEdges,
                                           [NoAlias, ReadOnly] UnsafeList<Edge> otherEdges,
                                           [NoAlias, ReadOnly] HashedVertices combinedVertices)
        {
            if (selfEdges.Length < 3 ||
                otherEdges.Length < 3)
                return;

            var otherVerticesLength = 0;
            NativeCollectionHelpers.EnsureMinimumSize(ref otherVertices, otherEdges.Length);
            
            // TODO: use edges instead + 2 planes intersecting each edge
            for (int v = 0; v < otherEdges.Length; v++)
            {
                var vertexIndex = otherEdges[v].index1; // <- assumes no gaps
                for (int e = 0; e < selfEdges.Length; e++)
                {
                    // If the exact edge already exists in other polygon, skip it
                    if (selfEdges[e].index1 == vertexIndex ||
                        selfEdges[e].index2 == vertexIndex)
                        goto NextVertex;
                }

                // TODO: store two end planes for each edge instead (the planes whose intersections with the infinite edge create the vertices)
                //*
                var vertex = combinedVertices[vertexIndex];                
                for (int p = 0; p < selfPlanes.Length; p++)
                {
                    var distance = math.dot(selfPlanes[p], new float4(vertex, 1));
                    if (distance > kFatPlaneWidthEpsilon)
                        goto NextVertex;
                }
                //*/

                // TODO: Check if vertex intersects at least 2 selfPlanes
                otherVertices[otherVerticesLength] = vertexIndex;
                otherVerticesLength++;
            NextVertex:
                ;
            }

            if (otherVerticesLength == 0)
                return;

            NativeCollectionHelpers.EnsureCreatedAndClear(ref tempList);

            var tempListCapacity = (selfEdges.Length * 2) + otherVerticesLength;
            if (tempList.Capacity < tempListCapacity)
                tempList.Capacity = tempListCapacity;

            {
                var inputEdgesLength    = selfEdges.Length;
                NativeCollectionHelpers.EnsureMinimumSize(ref newSelfEdges, inputEdgesLength);

                newSelfEdges.CopyFrom(selfEdges, 0, selfEdges.Length);
                selfEdges.Clear();

                // TODO: Optimize the hell out of this
                for (int e = 0; e < inputEdgesLength && otherVerticesLength > 0; e++)
                {
                    var vertexIndex0 = newSelfEdges[e].index1;
                    var vertexIndex1 = newSelfEdges[e].index2;

                    var vertex0 = combinedVertices[vertexIndex0];
                    var vertex1 = combinedVertices[vertexIndex1];

                    tempList.Clear();

                    var delta = math.normalize(vertex1 - vertex0);
                    var max = math.dot(vertex1 - vertex0, delta);
                    for (int v1 = otherVerticesLength - 1; v1 >= 0; v1--)
                    {
                        var otherVertexIndex = otherVertices[v1];
                        if (otherVertexIndex == vertexIndex0 ||
                            otherVertexIndex == vertexIndex1)
                            continue;
                        var otherVertex = combinedVertices[otherVertexIndex];
                        var dot = math.dot(otherVertex - vertex0, delta);
                        if (dot <= 0 || dot >= max)
                            continue;
                        if (!GeometryMath.IsPointOnLineSegment(otherVertex, vertex0, vertex1, CSGConstants.kVertexEqualEpsilon, CSGConstants.kEdgeIntersectionEpsilon))
                            continue;

                        // Note: the otherVertices array cannot contain any indices that are part in 
                        //       the input indices, since we checked for that when we created it.
                        tempList.AddNoResize(otherVertexIndex);

                        // TODO: figure out why removing vertices fails?
                        //if (v1 != otherVerticesLength - 1 && otherVerticesLength > 0)
                        //    otherVertices[v1] = otherVertices[otherVerticesLength - 1];
                        //otherVerticesLength--;
                    }

                    if (tempList.Length > 0)
                    {
                        float dot1, dot2;
                        for (int v1 = 0; v1 < tempList.Length - 1; v1++)
                        {
                            for (int v2 = v1 + 1; v2 < tempList.Length; v2++)
                            {
                                var otherVertexIndex1 = tempList[v1];
                                var otherVertexIndex2 = tempList[v2];
                                dot1 = math.dot(combinedVertices[otherVertexIndex1] - vertex0, delta);
                                dot2 = math.dot(combinedVertices[otherVertexIndex2] - vertex0, delta);
                                if (dot1 >= dot2)
                                {
                                    tempList[v1] = otherVertexIndex2;
                                    tempList[v2] = otherVertexIndex1;
                                }
                            }
                        }
                        if (vertexIndex0 != tempList[0])
                            selfEdges.AddNoResize(new Edge { index1 = vertexIndex0, index2 = tempList[0] });
                        for (int i = 1; i < tempList.Length; i++)
                        {
                            if (tempList[i - 1] != tempList[i])
                                selfEdges.AddNoResize(new Edge { index1 = tempList[i - 1], index2 = tempList[i] });
                        }
                        if (tempList[tempList.Length - 1] != vertexIndex1)
                            selfEdges.AddNoResize(new Edge { index1 = tempList[tempList.Length - 1], index2 = vertexIndex1 });
                    } else
                    {
                        selfEdges.AddNoResize(newSelfEdges[e]);
                    }
                }
            }
        }

        public static void RemoveDuplicates(ref UnsafeList<Edge> edges)
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
