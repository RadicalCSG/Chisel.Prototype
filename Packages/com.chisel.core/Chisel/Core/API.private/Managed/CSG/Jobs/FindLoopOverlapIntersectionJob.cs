using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;
using Unity.Entities;

namespace Chisel.Core
{

    [BurstCompile(CompileSynchronously = true)]
    internal unsafe struct FindLoopOverlapIntersectionsJob : IJobParallelFor
    { 
        [NoAlias, ReadOnly] public NativeArray<IndexOrder>                                      treeBrushIndexOrders;
        [NoAlias, ReadOnly] public NativeArray<int>                                             nodeIndexToNodeOrder;
        [NoAlias, ReadOnly] public int                                                          nodeIndexToNodeOrderOffset;
        [NoAlias, ReadOnly] public NativeArray<BlobAssetReference<BrushIntersectionLoops>>      intersectionLoopBlobs;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BasePolygonsBlob>>     basePolygons;
        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes;
        
        [NoAlias, WriteOnly] public NativeStream.Writer     output;

        public struct Empty { };

        public int CompareSortByBasePlaneIndex(int2 x, int2 y)
        {
            ref var vx = ref intersectionLoopBlobs[x.x].Value.loops[x.y];
            ref var vy = ref intersectionLoopBlobs[y.x].Value.loops[y.y];

            var diff = vx.surfaceInfo.basePlaneIndex - vy.surfaceInfo.basePlaneIndex;
            if (diff != 0)
                return diff;

            var vxBrushIndexOffset = vx.surfaceInfo.brushIndex - nodeIndexToNodeOrderOffset;
            var vyBrushIndexOffset = vy.surfaceInfo.brushIndex - nodeIndexToNodeOrderOffset;

            var vxBrushOrder = nodeIndexToNodeOrder[vxBrushIndexOffset];
            var vyBrushOrder = nodeIndexToNodeOrder[vyBrushIndexOffset];

            diff = vxBrushOrder - vyBrushOrder;
            if (diff != 0)
                return diff;
            return 0;
        }

        static unsafe void CopyFrom(NativeListArray<Edge> dst, int index, ref BrushIntersectionLoop brushIntersectionLoop, HashedVertices hashedVertices, int extraCapacity)
        {
            ref var vertices = ref brushIntersectionLoop.loopVertices;
            var srcIndices = stackalloc ushort[vertices.Length];
            hashedVertices.Reserve(vertices.Length);
            for (int j = 0; j < vertices.Length; j++)
                srcIndices[j] = hashedVertices.AddNoResize(vertices[j]);

            {
                var dstEdges = dst.AllocateWithCapacityForIndex(index, vertices.Length + extraCapacity);
                for (int j = 1; j < vertices.Length; j++)
                {
                    dstEdges.AddNoResize(new Edge() { index1 = srcIndices[j - 1], index2 = srcIndices[j] });
                }
                dstEdges.AddNoResize(new Edge() { index1 = srcIndices[vertices.Length - 1], index2 = srcIndices[0] });
            }
        }

        public unsafe void Execute(int index)
        {
            var brushIndexOrder     = treeBrushIndexOrders[index];
            int brushNodeIndex      = brushIndexOrder.nodeIndex;
            int brushNodeOrder      = brushIndexOrder.nodeOrder;

            ref var basePolygonBlob = ref basePolygons[brushNodeIndex].Value;

            var surfaceCount        = basePolygonBlob.polygons.Length;
            if (surfaceCount == 0)
                return;
            
            var basePolygonSurfaceInfos     = new NativeList<SurfaceInfo>(0, Allocator.Temp);
            var basePolygonEdges            = new NativeListArray<Edge>(0, Allocator.Temp);
            var intersectionSurfaceInfos    = new NativeList<SurfaceInfo>(0, Allocator.Temp);
            var intersectionEdges           = new NativeListArray<Edge>(0, Allocator.Temp);
            var hashedVertices              = new HashedVertices(2048, Allocator.Temp);

            basePolygonEdges.ResizeExact(surfaceCount);
            basePolygonSurfaceInfos.ResizeUninitialized(surfaceCount);



            // ***********************
            // TODO: get rid of this somehow
            var brushIntersectionLoops      = new NativeList<int2>(intersectionLoopBlobs.Length, Allocator.Temp);
            var uniqueBrushIndicesHashMap   = new NativeHashMap<int, Empty>(intersectionLoopBlobs.Length, Allocator.Temp);
            for (int k = 0; k < intersectionLoopBlobs.Length; k++)
            {
                ref var loops           = ref intersectionLoopBlobs[k].Value.loops;
                for (int n = 0; n < loops.Length; n++)
                { 
                    ref var outputSurface   = ref loops[n];
                    ref var pair            = ref outputSurface.pair;

                    var otherNodeOffset0    = pair.brushNodeIndex0 - nodeIndexToNodeOrderOffset;
                    var otherNodeOrder0     = nodeIndexToNodeOrder[otherNodeOffset0];

                    // TODO: get rid of this somehow
                    if (otherNodeOrder0 != brushNodeOrder)
                        continue;
                    
                    var otherNodeIndex1 = pair.brushNodeIndex1;
                    uniqueBrushIndicesHashMap.TryAdd(otherNodeIndex1, new Empty());
                    brushIntersectionLoops.Add(new int2(k, n)); /*OUTPUT*/
                }
            }
            // ***********************



            var uniqueBrushIndices = uniqueBrushIndicesHashMap.GetKeyArray(Allocator.Temp);
            uniqueBrushIndicesHashMap.Dispose();

            hashedVertices.AddUniqueVertices(ref basePolygonBlob.vertices); /*OUTPUT*/

            var uniqueBrushIndexCount = uniqueBrushIndices.Length;
            if (uniqueBrushIndexCount == 0)
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
            } else
            { 
                intersectionEdges.ResizeExact(brushIntersectionLoops.Length);
                for (int i = 0; i < brushIntersectionLoops.Length - 1; i++)
                {
                    for (int j = i + 1; j < brushIntersectionLoops.Length; j++)
                    {
                        if (CompareSortByBasePlaneIndex(brushIntersectionLoops[i], brushIntersectionLoops[j]) > 0)
                        {
                            var t = brushIntersectionLoops[i];
                            brushIntersectionLoops[i] = brushIntersectionLoops[j];
                            brushIntersectionLoops[j] = t;
                        }
                    }
                }

                var intersectionSurfaceSegments = stackalloc int2[surfaceCount];
                {
                    {
                        for (int s = 0; s < basePolygonBlob.polygons.Length; s++)
                        {
                            ref var input = ref basePolygonBlob.polygons[s];

                            var edges = basePolygonEdges.AllocateWithCapacityForIndex(s, (input.endEdgeIndex - input.startEdgeIndex) + (brushIntersectionLoops.Length * 4));
                            for (int e = input.startEdgeIndex; e < input.endEdgeIndex; e++)
                                edges.AddNoResize(basePolygonBlob.edges[e]);

                            basePolygonSurfaceInfos[s] = basePolygonBlob.polygons[s].surfaceInfo;
                        }

                        { 
                            int prevBasePlaneIndex = 0;
                            int startIndex = 0;
                            for (int l = 0; l < brushIntersectionLoops.Length; l++)
                            {
                                var brushIntersectionIndex      = brushIntersectionLoops[l];
                                ref var brushIntersectionLoop   = ref intersectionLoopBlobs[brushIntersectionIndex.x].Value.loops[brushIntersectionIndex.y];
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
                                CopyFrom(intersectionEdges, l, ref brushIntersectionLoop, hashedVertices, brushIntersectionLoops.Length * 4);
                            }
                            {
                                intersectionSurfaceSegments[prevBasePlaneIndex] = new int2(startIndex, brushIntersectionLoops.Length - startIndex);
                                startIndex = brushIntersectionLoops.Length;
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
                            var intersectionIndex0      = brushIntersectionLoops[intersectionSurfaceOffset + l0];
                            ref var intersection0       = ref intersectionLoopBlobs[intersectionIndex0.x].Value.loops[intersectionIndex0.y];
                            int intersectionBrushIndex0 = intersection0.surfaceInfo.brushIndex;
                            var edges                   = intersectionEdges[intersectionSurfaceOffset + l0];
                            for (int l1 = 0; l1 < intersectionSurfaceCount; l1++)
                            {
                                if (l0 == l1)
                                    continue;
                            
                                var intersectionIndex1      = brushIntersectionLoops[intersectionSurfaceOffset + l1];
                                ref var intersection1       = ref intersectionLoopBlobs[intersectionIndex1.x].Value.loops[intersectionIndex1.y];
                                int intersectionBrushIndex1 = intersection1.surfaceInfo.brushIndex;

                                var intersectionJob = new FindLoopPlaneIntersectionsJob()
                                {
                                    brushTreeSpacePlanes    = brushTreeSpacePlanes, 
                                    otherBrushNodeIndex     = intersectionBrushIndex1,
                                    selfBrushNodeIndex      = intersectionBrushIndex0,
                                    hashedVertices          = hashedVertices,
                                    edges                   = edges
                                };
                                intersectionJob.Execute();

                                // TODO: merge these so that intersections will be identical on both loops (without using math, use logic)
                                // TODO: make sure that intersections between loops will be identical on OTHER brushes (without using math, use logic)
                            }
                        }
                    }

                    // TODO: should only intersect with all brushes that each particular basepolygon intersects with
                    //       but also need adjency information between basePolygons to ensure that intersections exist on 
                    //       both sides of each edge on a brush. 
                    for (int b = 0; b < basePolygonEdges.Length; b++)
                    {
                        var edges = basePolygonEdges[b];
                        for (int i = 0; i < uniqueBrushIndices.Length; i++)
                        {
                            var intersectionJob = new FindBasePolygonPlaneIntersectionsJob()
                            {
                                brushTreeSpacePlanes    = brushTreeSpacePlanes,
                                otherBrushNodeIndex     = uniqueBrushIndices[i],
                                selfBrushNodeIndex      = brushNodeIndex,
                                hashedVertices          = hashedVertices,
                                edges                   = edges
                            };
                            intersectionJob.Execute();
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
                            var intersectionIndex       = brushIntersectionLoops[intersectionSurfaceOffset + l0];
                            ref var intersection        = ref intersectionLoopBlobs[intersectionIndex.x].Value.loops[intersectionIndex.y];
                            int intersectionBrushIndex  = intersection.surfaceInfo.brushIndex;
                            var in_edges                = intersectionEdges[intersectionSurfaceOffset + l0];
                            var intersectionJob2 = new FindLoopVertexOverlapsJob
                            {
                                brushTreeSpacePlanes    = brushTreeSpacePlanes,
                                selfBrushNodeIndex      = intersectionBrushIndex,
                                hashedVertices          = hashedVertices,
                                otherEdges              = bp_edges,
                                edges                   = in_edges
                            };
                            intersectionJob2.Execute();
                        }
                    } 

                    for (int i = 0; i < intersectionEdges.Length; i++)
                    {
                        // TODO: might not be necessary
                        var edges = intersectionEdges[i];
                        var removeIdenticalIndicesEdgesJob = new RemoveIdenticalIndicesEdgesJob { edges = edges };
                        removeIdenticalIndicesEdgesJob.Execute();
                    }

                    for (int i = 0; i < basePolygonEdges.Length; i++)
                    {
                        // TODO: might not be necessary
                        var edges = basePolygonEdges[i];
                        var removeIdenticalIndicesEdgesJob = new RemoveIdenticalIndicesEdgesJob { edges = edges };
                        removeIdenticalIndicesEdgesJob.Execute();
                    }


                    // TODO: merge indices across multiple loops when vertices are identical
                }

                intersectionSurfaceInfos.Capacity = brushIntersectionLoops.Length;
                for (int k = 0; k < brushIntersectionLoops.Length; k++)
                {
                    var intersectionIndex = brushIntersectionLoops[k];
                    ref var intersection = ref intersectionLoopBlobs[intersectionIndex.x].Value.loops[intersectionIndex.y];
                    intersectionSurfaceInfos.AddNoResize(intersection.surfaceInfo); /*OUTPUT*/
                }
            }


            output.BeginForEachIndex(index);
            output.Write(brushNodeIndex);
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


            //intersectionSurfaceSegments.Dispose();
            brushIntersectionLoops.Dispose();
            uniqueBrushIndices.Dispose();
        }
        
    }
}
