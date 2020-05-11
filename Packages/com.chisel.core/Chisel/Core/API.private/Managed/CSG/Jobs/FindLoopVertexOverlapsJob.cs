using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Chisel.Core.LowLevel.Unsafe;
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    public struct FindLoopVertexOverlapsJob : IJob
    {
        public const int kMaxVertexCount = short.MaxValue;
        const float kPlaneDistanceEpsilon = CSGConstants.kDistanceEpsilon;

        [NoAlias, ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushTreeSpacePlanes>> brushTreeSpacePlanes;
        [NoAlias, ReadOnly] public int                              selfBrushNodeIndex;
        [NoAlias, ReadOnly] public HashedVertices                   hashedVertices;
        [NoAlias, ReadOnly] public NativeListArray<Edge>.NativeList otherEdges;


        [NoAlias] public NativeListArray<Edge>.NativeList           edges;

        public unsafe void ExecuteEdges()
        {
            if (edges.Length < 3 ||
                otherEdges.Length < 3)
                return;

            ref var selfPlanes = ref brushTreeSpacePlanes[selfBrushNodeIndex].Value.treeSpacePlanes;

            var otherVerticesLength = 0;
            var otherVertices       = stackalloc ushort[otherEdges.Length];
            //var otherVertices       = (ushort*)UnsafeUtility.Malloc(otherEdges.Length * sizeof(ushort), 4, Allocator.TempJob);

            // TODO: use edges instead + 2 planes intersecting each edge
            var vertices = hashedVertices.GetUnsafeReadOnlyPtr();
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
                    if (distance > kPlaneDistanceEpsilon)
                        goto NextVertex;
                }
                // TODO: Check if vertex intersects at least 2 selfPlanes
                otherVertices[otherVerticesLength] = vertexIndex;
                otherVerticesLength++;
            NextVertex:
                ;
            }

            if (otherVerticesLength == 0)
            {
                //UnsafeUtility.Free(otherVertices, Allocator.TempJob);
                return;
            }

            var tempList = new NativeList<ushort>(Allocator.Temp);
            {
                var tempListPtr         = (ushort*)tempList.GetUnsafePtr();
                var inputEdgesLength    = edges.Length;
                var inputEdges          = stackalloc Edge[edges.Length];
                //var inputEdges = (Edge*)UnsafeUtility.Malloc(edges.Length * sizeof(Edge), 4, Allocator.TempJob);
                UnsafeUtility.MemCpyReplicate(inputEdges, edges.GetUnsafePtr(), sizeof(Edge) * edges.Length, 1);
                edges.Clear();

                // TODO: Optimize the hell out of this
                for (int e = 0; e < inputEdgesLength && otherVerticesLength > 0; e++)
                {
                    var vertexIndex0 = inputEdges[e].index1;
                    var vertexIndex1 = inputEdges[e].index2;

                    var vertex0 = vertices[vertexIndex0];
                    var vertex1 = vertices[vertexIndex1];

                    var vertex0w = new float4(vertex0, 1);
                    var vertex1w = new float4(vertex1, 1);

                    tempList.Clear();
                    tempList.Add(vertexIndex0);

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
                        tempList.Add(otherVertexIndex);

                        // TODO: figure out why removing vertices fails?
                        //if (v1 != otherVerticesLength - 1 && otherVerticesLength > 0)
                        //    otherVertices[v1] = otherVertices[otherVerticesLength - 1];
                        //otherVerticesLength--;
                    }
                    tempList.Add(vertexIndex1);

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
                                    tempListPtr[v1] = otherVertexIndex2;
                                    tempListPtr[v2] = otherVertexIndex1;
                                }
                            }
                        }
                        for (int i = 1; i < tempList.Length; i++)
                        {
                            if (tempList[i - 1] != tempList[i])
                                edges.AddNoResize(new Edge() { index1 = tempList[i - 1], index2 = tempList[i] });
                        }
                    } else
                    {
                        edges.AddNoResize(inputEdges[e]);
                    }
                }

                //UnsafeUtility.Free(inputEdges, Allocator.TempJob);
                //UnsafeUtility.Free(otherVertices, Allocator.TempJob);
            }
            tempList.Dispose();
        }

        public void Execute()
        {
            ExecuteEdges();
        }
    }
}
