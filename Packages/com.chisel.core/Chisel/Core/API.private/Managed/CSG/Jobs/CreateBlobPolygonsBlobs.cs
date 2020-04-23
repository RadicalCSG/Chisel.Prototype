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
    struct CreateBlobPolygonsBlobs : IJobParallelFor
    {
        [NoAlias,ReadOnly] public NativeArray<int>                                              treeBrushIndices;
        [NoAlias,ReadOnly] public NativeHashMap<int, BlobAssetReference<NodeTransformations>>   transformations;
        [NoAlias,ReadOnly] public NativeHashMap<int, BlobAssetReference<BrushMeshBlob>>         brushMeshLookup;

        [NoAlias,WriteOnly] public NativeHashMap<int, BlobAssetReference<BasePolygonsBlob>>.ParallelWriter basePolygons;


        // Per thread scratch memory
        [NativeDisableContainerSafetyRestriction] HashedVertices            hashedVertices;
        [NativeDisableContainerSafetyRestriction] NativeArray<Edge>         edges;
        [NativeDisableContainerSafetyRestriction] NativeArray<BasePolygon>  surfaces;
        [NativeDisableContainerSafetyRestriction] NativeArray<Edge>         polygonEdges;
        

        static bool IsDegenerate(HashedVertices hashedVertices, NativeArray<Edge> edges, int edgeCount)
        {
            if (edgeCount < 3)
                return true;

            for (int i = 0; i < edgeCount; i++)
            {
                var vertexIndex1 = edges[i].index1;
                var vertex1 = hashedVertices[vertexIndex1];
                for (int j = 0; j < edgeCount; j++)
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

                    var vertexA = hashedVertices[vertexIndexA];
                    var vertexB = hashedVertices[vertexIndexB];

                    var distance = GeometryMath.SqrDistanceFromPointToLineSegment(vertex1, vertexA, vertexB);
                    if (distance <= CSGConstants.kSqrDistanceEpsilon)
                        return true;
                }
            }
            return false;
        }
        
        static void RemoveDuplicates(NativeArray<Edge> edges, ref int edgeCount)
        {
            if (edgeCount < 3)
            {
                edgeCount = 0;
                return;
            }

            for (int e = edgeCount - 1; e >= 0; e--)
            {
                if (edges[e].index1 != edges[e].index2)
                    continue;
                edges[e] = edges[edgeCount - 1];
                //edges.RemoveAtSwapBack(e);
                edgeCount--;
            }
        }

        AABB CopyPolygonToIndices(BlobAssetReference<BrushMeshBlob> mesh, int polygonIndex, float4x4 nodeToTreeSpaceMatrix, HashedVertices hashedVertices, NativeArray<Edge> edges, ref int edgeCount)
        {
            ref var halfEdges   = ref mesh.Value.halfEdges;
            ref var vertices    = ref mesh.Value.vertices;
            ref var polygon     = ref mesh.Value.polygons[polygonIndex];

            var firstEdge   = polygon.firstEdge;
            var lastEdge    = firstEdge + polygon.edgeCount;

            //hashedVertices.Reserve(indexCount); // ensure we have at least this many extra vertices in capacity
                
            var min = float3.zero;
            var max = float3.zero;

            // TODO: put in job so we can burstify this, maybe join with RemoveIdenticalIndicesJob & IsDegenerate?
            for (int e = firstEdge; e < lastEdge; e++)
            {
                var vertexIndex = halfEdges[e].vertexIndex;
                var localVertex = new float4(vertices[vertexIndex], 1);
                var worldVertex = math.mul(nodeToTreeSpaceMatrix, localVertex);

                // TODO: could do this in separate loop on vertices
                if (e == firstEdge)
                {
                    min.x = worldVertex.x; max.x = worldVertex.x;
                    min.y = worldVertex.y; max.y = worldVertex.y;
                    min.z = worldVertex.z; max.z = worldVertex.z;
                } else
                {
                    min.x = math.min(min.x, worldVertex.x); max.x = math.max(max.x, worldVertex.x);
                    min.y = math.min(min.y, worldVertex.y); max.y = math.max(max.y, worldVertex.y);
                    min.z = math.min(min.z, worldVertex.z); max.z = math.max(max.z, worldVertex.z);
                }

                var newIndex = hashedVertices.AddNoResize(worldVertex.xyz);
                if (e > firstEdge)
                {
                    var edge = edges[edgeCount - 1];
                    edge.index2 = newIndex;
                    edges[edgeCount - 1] = edge;
                }
                edges[edgeCount] = new Edge() { index1 = newIndex };
                //edges.AddNoResize(new Edge() { index1 = newIndex });
                edgeCount++;
            }
            {
                var edge = edges[edgeCount - 1];
                edge.index2 = edges[0].index1;
                edges[edgeCount - 1] = edge;
            }

            RemoveDuplicates(edges, ref edgeCount);

            if (edgeCount > 0 && IsDegenerate(hashedVertices, edges, edgeCount))
            {
                edgeCount = 0;
            }

            if (edgeCount > 0)
            {
                var aabb = new AABB();
                aabb.min = min;
                aabb.max = max;
                return aabb;
            } else
                return new AABB();
        }

        public void Execute(int b)
        {
            var brushNodeIndex  = treeBrushIndices[b];
            var transform       = transformations[brushNodeIndex];

            var mesh                    = brushMeshLookup[brushNodeIndex];
            ref var vertices            = ref mesh.Value.vertices;
            ref var halfEdges           = ref mesh.Value.halfEdges;
            ref var planes              = ref mesh.Value.localPlanes;
            ref var polygons            = ref mesh.Value.polygons;
            var nodeToTreeSpaceMatrix   = transform.Value.nodeToTree;

            if (!edges.IsCreated)
            {
                hashedVertices  = new HashedVertices(math.max(vertices.Length, 1000), Allocator.Temp);
                edges           = new NativeArray<Edge>(math.max(halfEdges.Length, 1000), Allocator.Temp);
                surfaces        = new NativeArray<BasePolygon>(math.max(polygons.Length, 100), Allocator.Temp);
                polygonEdges    = new NativeArray<Edge>(math.max(halfEdges.Length, 1000), Allocator.Temp);
            } else
            {
                if (hashedVertices.Capacity < vertices.Length)
                {
                    hashedVertices.Dispose();
                    hashedVertices = new HashedVertices(vertices.Length, Allocator.Temp);
                } else
                    hashedVertices.Clear();
                if (surfaces.Length < polygons.Length)
                {
                    surfaces.Dispose();
                    surfaces = new NativeArray<BasePolygon>(polygons.Length, Allocator.Temp);
                }
                if (edges.Length < halfEdges.Length)
                {
                    edges.Dispose();
                    edges = new NativeArray<Edge>(halfEdges.Length, Allocator.Temp);
                }
                if (polygonEdges.Length < halfEdges.Length)
                {
                    polygonEdges.Dispose();
                    polygonEdges = new NativeArray<Edge>(halfEdges.Length, Allocator.Temp);
                }
            }

            //var nodeToTreeSpaceInvertedTransposedMatrix = math.transpose(math.inverse(nodeToTreeSpaceMatrix));

            var min = new float3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new float3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            var totalEdgeCount = 0;
            var totalSurfaceCount = 0;
            for (int p = 0; p < polygons.Length; p++)
            {
                var polygon = polygons[p];
                if (polygon.edgeCount < 3 || p >= planes.Length)
                    continue;

                // TODO: THEORY - can end up with duplicate vertices when close enough vertices are snapped together

                int edgeCount = 0;
                var aabb = CopyPolygonToIndices(mesh, p, nodeToTreeSpaceMatrix, hashedVertices, polygonEdges, ref edgeCount);
                if (edgeCount == 0)
                    continue;

                min = math.min(min, aabb.min);
                max = math.max(max, aabb.max);

                int startEdgeIndex = totalEdgeCount;
                for (int i = 0; i < edgeCount; i++)
                {
                    edges[totalEdgeCount] = polygonEdges[i];
                    totalEdgeCount++;
                }
                var endEdgeIndex = totalEdgeCount;

                surfaces[totalSurfaceCount] = new BasePolygon()
                {
                    surfaceInfo = new SurfaceInfo()
                    {
                        basePlaneIndex      = (ushort)p,
                        brushNodeIndex      = brushNodeIndex,
                        interiorCategory    = (CategoryGroupIndex)(int)CategoryIndex.ValidAligned,
                    },
                    layers          = polygon.layerDefinition,
                    UV0             = polygon.UV0,
                    startEdgeIndex  = startEdgeIndex,
                    endEdgeIndex    = endEdgeIndex
                };
                totalSurfaceCount++;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BasePolygonsBlob>();
            builder.Construct(ref root.surfaces, surfaces, totalSurfaceCount);
            builder.Construct(ref root.edges,    edges   , totalEdgeCount);
            builder.Construct(ref root.vertices, hashedVertices);
            root.bounds = new AABB() { min = min, max = max };
            var result = builder.CreateBlobAssetReference<BasePolygonsBlob>(Allocator.Persistent);
            
            //builder.Dispose();
            //hashedVertices.Dispose();
            //edges.Dispose();
            //surfaces.Dispose();
            //polygonEdges.Dispose();

            basePolygons.TryAdd(brushNodeIndex, result);
        }
    }
}
