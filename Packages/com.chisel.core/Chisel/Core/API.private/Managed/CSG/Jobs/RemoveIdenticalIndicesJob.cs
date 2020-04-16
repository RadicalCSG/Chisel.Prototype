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
using UnityEngine;
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace Chisel.Core
{
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct RemoveIdenticalIndicesEdgesJob : IJob
    {
        [NoAlias] public NativeListArray<Edge>.NativeList edges;

        public static void RemoveDuplicates(ref NativeList<Edge> edges)
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

        public void Execute()
        {
            RemoveDuplicates(ref edges);
        }
    }


    // TODO: probably makes sense to break this up into multiple pieces/multiple jobs that can run parallel,
    //      but requires that change some storage formats first
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct CopyPolygonToIndicesJob : IJob
    {
        [NoAlias, ReadOnly] public BlobAssetReference<BrushMeshBlob> mesh;
        [NoAlias, ReadOnly] public int       polygonIndex;
        [NoAlias, ReadOnly] public float4x4  nodeToTreeSpaceMatrix;
        [NoAlias, ReadOnly] public float4x4  nodeToTreeSpaceInvertedTransposedMatrix;

        [NativeDisableContainerSafetyRestriction]
        [NoAlias] public HashedVertices           hashedVertices; // <-- TODO: we're reading AND writing to the same NativeList!?!?!
        [NoAlias] public NativeList<Edge>     edges;

        public AABB aabb;

        internal static unsafe bool IsDegenerate(in HashedVertices hashedVertices, NativeList<Edge> edges)
        {
            if (edges.Length < 3)
                return true;

            var vertices = hashedVertices.GetUnsafeReadOnlyPtr();
            for (int i = 0; i < edges.Length; i++)
            {
                var vertexIndex1 = edges[i].index1;
                var vertex1 = vertices[vertexIndex1];
                for (int j = 0; j < edges.Length; j++)
                {
                    if (i == j)
                        continue;

                    var vertexIndexA = edges[j].index1;
                    var vertexIndexB = edges[j].index2;

                    // Loop loops back on same vertex
                    if (vertexIndex1 == vertexIndexA ||
                        vertexIndex1 == vertexIndexB ||
                        vertexIndexA == vertexIndexB)
                        continue;

                    var vertexA = vertices[vertexIndexA];
                    var vertexB = vertices[vertexIndexB];

                    var distance = GeometryMath.SqrDistanceFromPointToLineSegment(vertex1, vertexA, vertexB);
                    if (distance <= CSGConstants.kSqrDistanceEpsilon)
                        return true;
                }
            }
            return false;
        }

        public void Execute()
        {
            ref var halfEdges   = ref mesh.Value.halfEdges;
            ref var vertices    = ref mesh.Value.vertices;
            ref var planes      = ref mesh.Value.localPlanes;
            ref var polygon     = ref mesh.Value.polygons[polygonIndex];

            var localPlane  = planes[polygonIndex];
            var firstEdge   = polygon.firstEdge;
            var lastEdge    = firstEdge + polygon.edgeCount;
            var indexCount  = lastEdge - firstEdge;

            hashedVertices.Reserve(indexCount); // ensure we have at least this many extra vertices in capacity

            var min = aabb.min;
            var max = aabb.max;

            // TODO: put in job so we can burstify this, maybe join with RemoveIdenticalIndicesJob & IsDegenerate?
            for (int e = firstEdge; e < lastEdge; e++)
            {
                var vertexIndex = halfEdges[e].vertexIndex;
                var localVertex = new float4(vertices[vertexIndex], 1);
                var worldVertex = math.mul(nodeToTreeSpaceMatrix, localVertex);

                // TODO: could do this in separate loop on vertices
                min.x = math.min(min.x, worldVertex.x); max.x = math.max(max.x, worldVertex.x);
                min.y = math.min(min.y, worldVertex.y); max.y = math.max(max.y, worldVertex.y);
                min.z = math.min(min.z, worldVertex.z); max.z = math.max(max.z, worldVertex.z);

                var newIndex = hashedVertices.AddNoResize(worldVertex.xyz);
                if (e > firstEdge)
                {
                    var edge = edges[edges.Length - 1];
                    edge.index2 = newIndex;
                    edges[edges.Length - 1] = edge;
                }
                edges.Add(new Edge() { index1 = newIndex });
            }
            {
                var edge = edges[edges.Length - 1];
                edge.index2 = edges[0].index1;
                edges[edges.Length - 1] = edge;
            }

            RemoveIdenticalIndicesEdgesJob.RemoveDuplicates(ref edges);

            if (edges.Length > 0 && IsDegenerate(hashedVertices, edges))
            {
                edges.Clear();
            }

            if (edges.Length > 0)
            {
                aabb.min = min;
                aabb.max = max;
            }
        }
    }
}
