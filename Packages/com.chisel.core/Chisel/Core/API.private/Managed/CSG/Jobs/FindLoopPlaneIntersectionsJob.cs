using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Entities;
using UnityEngine;
using Chisel.Core.LowLevel.Unsafe;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(Debug = false)]
    public struct FindLoopPlaneIntersectionsJob : IJob
    {
        public const int kMaxVertexCount    = short.MaxValue;
        const float kVertexEqualEpsilonSqr  = (float)CSGConstants.kVertexEqualEpsilonSqr;
        const float kPlaneDistanceEpsilon   = CSGConstants.kDistanceEpsilon;

        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushWorldPlanes>> brushWorldPlanes;
        [NoAlias, ReadOnly] public int                  otherBrushNodeIndex;
        [NoAlias, ReadOnly] public int                  selfBrushNodeIndex;
        
        //[NativeDisableContainerSafetyRestriction]
        [NoAlias] public HashedVertices                         hashedVertices; // <-- TODO: we're reading AND writing to the same NativeList!?!?!
        [NoAlias] public NativeListArray<Edge>.NativeList   edges;
        
        // TODO: find a way to share found intersections between loops, to avoid accuracy issues
        public unsafe void Execute()
        {
            if (edges.Length < 3)
                return;
            
            var inputEdgesLength    = edges.Length;
            var inputEdges          = stackalloc Edge[inputEdgesLength];// (Edge*)UnsafeUtility.Malloc(edges.Length * sizeof(Edge), 4, Allocator.TempJob);
            UnsafeUtility.MemCpyReplicate(inputEdges, edges.GetUnsafePtr(), sizeof(Edge) * edges.Length, 1);
            edges.Clear();

            var tempVertices = stackalloc ushort[] { 0, 0, 0, 0 };

            ref var otherPlanesNative    = ref brushWorldPlanes[otherBrushNodeIndex].Value.worldPlanes;// allWorldSpacePlanePtr + otherPlanesSegment.x;
            ref var selfPlanesNative     = ref brushWorldPlanes[selfBrushNodeIndex].Value.worldPlanes;//allWorldSpacePlanePtr + selfPlanesSegment.x;

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
                        if (distance1 <=  kPlaneDistanceEpsilon ||
                            distance0 >= -kPlaneDistanceEpsilon) continue;
                    } else
                    {
                        if (distance1 >= -kPlaneDistanceEpsilon ||
                            distance0 <=  kPlaneDistanceEpsilon) continue;
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
                    if (math.lengthsq(vertex0 - newVertex) <= kVertexEqualEpsilonSqr ||
                        math.lengthsq(vertex1 - newVertex) <= kVertexEqualEpsilonSqr)
                        continue;

                    var newVertexw = new float4(newVertex, 1);
                    for (int p2 = 0; p2 < otherPlaneCount; p2++)
                    {
                        otherPlane = otherPlanesNative[p2];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kPlaneDistanceEpsilon)
                            goto SkipEdge;
                    }
                    for (int p1 = 0; p1 < selfPlaneCount; p1++)
                    {
                        otherPlane = selfPlanesNative[p1];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kPlaneDistanceEpsilon)
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
                    var tempVertexIndex0 = tempVertices[1];
                    var tempVertex0 = hashedVertices[tempVertexIndex0];
                    var tempVertexIndex1 = tempVertexIndex0;
                    if (foundVertices == 2)
                    {
                        tempVertexIndex1 = tempVertices[2];
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
                            edges.AddNoResize(new Edge() { index1 = tempVertices[i - 1], index2 = tempVertices[i] });
                    }
                } else
                {
                    edges.AddNoResize(inputEdges[e]);
                }
            }

            //UnsafeUtility.Free(inputEdges, Allocator.TempJob);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    internal struct FindBasePolygonPlaneIntersectionsJob : IJob
    {
        public const int kMaxVertexCount    = short.MaxValue;
        const float kVertexEqualEpsilonSqr  = (float)CSGConstants.kVertexEqualEpsilonSqr;
        const float kPlaneDistanceEpsilon   = CSGConstants.kDistanceEpsilon;

        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushWorldPlanes>> brushWorldPlanes;
        [NoAlias, ReadOnly] public int                  otherBrushNodeIndex;
        [NoAlias, ReadOnly] public int                  selfBrushNodeIndex;
        
        //[NativeDisableContainerSafetyRestriction]
        [NoAlias] public HashedVertices                         hashedVertices; // <-- TODO: we're reading AND writing to the same NativeList!?!?!
        [NoAlias] public NativeListArray<Edge>.NativeList   edges;
        
        // TODO: find a way to share found intersections between loops, to avoid accuracy issues
        public unsafe void Execute()
        {
            if (edges.Length < 3)
                return;
            
            var inputEdgesLength    = edges.Length;
            var inputEdges          = stackalloc Edge[inputEdgesLength];// (Edge*)UnsafeUtility.Malloc(edges.Length * sizeof(Edge), 4, Allocator.TempJob);
            //var inputEdges          = (Edge*)UnsafeUtility.Malloc(edges.Length * sizeof(Edge), 4, Allocator.TempJob);
            UnsafeUtility.MemCpyReplicate(inputEdges, edges.GetUnsafePtr(), sizeof(Edge) * edges.Length, 1);
            edges.Clear();

            var tempVertices = stackalloc ushort[] { 0, 0, 0, 0 };

            ref var otherPlanesNative    = ref brushWorldPlanes[otherBrushNodeIndex].Value.worldPlanes;// allWorldSpacePlanePtr + otherPlanesSegment.x;
            ref var selfPlanesNative     = ref brushWorldPlanes[selfBrushNodeIndex].Value.worldPlanes;//allWorldSpacePlanePtr + selfPlanesSegment.x;

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
                        if (distance1 <=  kPlaneDistanceEpsilon ||
                            distance0 >= -kPlaneDistanceEpsilon) continue;
                    } else
                    {
                        if (distance1 >= -kPlaneDistanceEpsilon ||
                            distance0 <=  kPlaneDistanceEpsilon) continue;
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
                    if (math.lengthsq(vertex0 - newVertex) <= kVertexEqualEpsilonSqr ||
                        math.lengthsq(vertex1 - newVertex) <= kVertexEqualEpsilonSqr)
                        continue;

                    var newVertexw = new float4(newVertex, 1);
                    for (int p2 = 0; p2 < otherPlaneCount; p2++)
                    {
                        otherPlane = otherPlanesNative[p2];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kPlaneDistanceEpsilon)
                            goto SkipEdge;
                    }
                    for (int p1 = 0; p1 < selfPlaneCount; p1++)
                    {
                        otherPlane = selfPlanesNative[p1];
                        var distance = math.dot(otherPlane, newVertexw);
                        if (distance > kPlaneDistanceEpsilon)
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
                    var tempVertexIndex0 = tempVertices[1];
                    var tempVertex0 = hashedVertices[tempVertexIndex0];
                    var tempVertexIndex1 = tempVertexIndex0;
                    if (foundVertices == 2)
                    {
                        tempVertexIndex1 = tempVertices[2];
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
                            edges.AddNoResize(new Edge() { index1 = tempVertices[i - 1], index2 = tempVertices[i] });
                    }
                } else
                {
                    edges.AddNoResize(inputEdges[e]);
                }
            }

            //UnsafeUtility.Free(inputEdges, Allocator.TempJob);
        }
    }
}
